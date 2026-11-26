using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using IsekaiChaos.Core;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace IsekaiChaos.SaveSystem
{
    public enum SlotKind { Manual, Quick, Auto }

    /// <summary>
    /// Multi-slot save/load.
    ///
    /// Slots:  manual_00..manual_11 (12 manual), quick, auto_0..auto_2 (rolling).
    /// Files per slot in persistentDataPath/Saves:
    ///   {slot}.sav       gzip( header line + "\n" + JSON )      — header = "ICS1|sha256(json)"
    ///   {slot}.meta.json small metadata for the slot list (no need to inflate full saves)
    ///   {slot}.jpg       256x144 thumbnail
    ///   {slot}.sav.bak   previous good version (restored automatically if .sav is corrupt)
    ///
    /// Writes are atomic: serialize on main thread (state must be consistent), then compress
    /// and write to .tmp on a worker thread, then swap. Android can kill the app at any time;
    /// a half-written .tmp is simply ignored.
    ///
    /// Auto-save triggers (via GameEvents): biome change, quest completion, building upgrade,
    /// and right BEFORE boss encounters. Throttled by <see cref="autoSaveCooldown"/>.
    /// </summary>
    public class SaveLoadManager : MonoBehaviour
    {
        public const int ManualSlotCount = 12;
        public const int AutoSlotCount = 3;
        const string Magic = "ICS1";
        const string ExportMagic = "ICSX1";

        public static SaveLoadManager Instance { get; private set; }

        [SerializeField] float autoSaveCooldown = 45f;
        [SerializeField] int thumbWidth = 256, thumbHeight = 144;
        [SerializeField] Camera thumbnailCamera;            // optional: render without HUD

        static readonly List<ISaveable> _saveables = new List<ISaveable>();
        public static void Register(ISaveable s) { if (!_saveables.Contains(s)) _saveables.Add(s); }
        public static void Unregister(ISaveable s) => _saveables.Remove(s);

        public bool IsBusy { get; private set; }
        public bool IsRestoring { get; private set; }
        public double PlaytimeSeconds { get; private set; }
        public string PlayerName { get; set; } = "Isekai'd Nobody";

        public event Action<string> SaveCompleted;          // slotId
        public event Action<string, string> SaveFailed;     // slotId, error
        public event Action<string> LoadCompleted;

        // Cached on the main thread: Application.persistentDataPath may not be read from worker threads.
        string SaveDir;
        IExternalSaveBridge _bridge;
        float _lastAutoSave = -999f;
        int _nextAutoIndex;
        string _pendingBossAutoSave;

        static readonly JsonSerializerSettings JsonSettings = new JsonSerializerSettings
        {
            Formatting = Formatting.None,
            NullValueHandling = NullValueHandling.Ignore,
            ObjectCreationHandling = ObjectCreationHandling.Replace,  // don't append to default lists
        };

        // ----------------------------------------------------------------------------
        #region Lifecycle
        void Awake()
        {
            if (Instance && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);
            SaveDir = Path.Combine(Application.persistentDataPath, "Saves");
            Directory.CreateDirectory(SaveDir);
            _bridge = ExternalSaveBridge.Create();
            _nextAutoIndex = FindOldestAutoIndex();

            GameEvents.BiomeChanged += (_, __) => RequestAutoSave("biome");
            GameEvents.QuestCompleted += _ => RequestAutoSave("quest");
            GameEvents.BuildingUpgraded += (_, __) => RequestAutoSave("base");
            GameEvents.BossEncounterStarting += id => RequestAutoSave("boss:" + id, force: true);
        }

        void Update()
        {
            if (!IsRestoring) PlaytimeSeconds += Time.unscaledDeltaTime;
        }

        /// <summary>Android: the app may be killed while backgrounded — quick auto-save.</summary>
        void OnApplicationPause(bool paused)
        {
            if (paused) RequestAutoSave("suspend", force: true);
        }
        #endregion

        // ----------------------------------------------------------------------------
        #region Slot ids
        public static string ManualSlot(int i) => $"manual_{i:00}";
        public const string QuickSlot = "quick";
        public static string AutoSlot(int i) => $"auto_{i}";

        public static SlotKind KindOf(string slotId) =>
            slotId == QuickSlot ? SlotKind.Quick : slotId.StartsWith("auto_") ? SlotKind.Auto : SlotKind.Manual;

        string PathFor(string slot, string ext) => Path.Combine(SaveDir, slot + ext);
        #endregion

        // ----------------------------------------------------------------------------
        #region Public API
        public void SaveManual(int index) => StartCoroutine(SaveRoutine(ManualSlot(Mathf.Clamp(index, 0, ManualSlotCount - 1))));
        public void QuickSave() => StartCoroutine(SaveRoutine(QuickSlot));
        public void QuickLoad() => StartCoroutine(LoadRoutine(QuickSlot));
        public void Load(string slotId) => StartCoroutine(LoadRoutine(slotId));

        public void RequestAutoSave(string reason, bool force = false)
        {
            if (IsRestoring || IsBusy) { if (force) _pendingBossAutoSave = reason; return; }
            if (!force && Time.unscaledTime - _lastAutoSave < autoSaveCooldown) return;
            _lastAutoSave = Time.unscaledTime;
            string slot = AutoSlot(_nextAutoIndex);
            _nextAutoIndex = (_nextAutoIndex + 1) % AutoSlotCount;
            Debug.Log($"[Save] Auto-save ({reason}) → {slot}");
            StartCoroutine(SaveRoutine(slot));
        }

        /// <summary>Slot list for the save/load menu. Reads only the small meta files.</summary>
        public List<SaveMetadata> ListSlots()
        {
            var list = new List<SaveMetadata>();
            foreach (var f in Directory.GetFiles(SaveDir, "*.meta.json"))
            {
                try { list.Add(JsonConvert.DeserializeObject<SaveMetadata>(File.ReadAllText(f))); }
                catch (Exception e) { Debug.LogWarning($"[Save] Bad meta {f}: {e.Message}"); }
            }
            return list.OrderByDescending(m => m.timestampUtcTicks).ToList();
        }

        public Texture2D LoadThumbnail(SaveMetadata meta)
        {
            if (meta == null || string.IsNullOrEmpty(meta.thumbnailFile)) return null;
            var p = Path.Combine(SaveDir, meta.thumbnailFile);
            if (!File.Exists(p)) return null;
            var tex = new Texture2D(2, 2, TextureFormat.RGB24, false);
            tex.LoadImage(File.ReadAllBytes(p));
            return tex;
        }

        public void Delete(string slotId)
        {
            foreach (var ext in new[] { ".sav", ".sav.bak", ".meta.json", ".jpg" })
            {
                var p = PathFor(slotId, ext);
                if (File.Exists(p)) File.Delete(p);
            }
        }
        #endregion

        // ----------------------------------------------------------------------------
        #region Save
        IEnumerator SaveRoutine(string slot)
        {
            while (IsBusy) yield return null;
            IsBusy = true;

            // 1) Thumbnail (must be at end of frame for a complete back buffer).
            yield return new WaitForEndOfFrame();
            byte[] jpg = null;
            try { jpg = CaptureThumbnail(); }
            catch (Exception e) { Debug.LogWarning($"[Save] Thumbnail failed: {e.Message}"); }

            // 2) Capture state on the main thread.
            SaveData data;
            string json;
            try
            {
                data = CaptureAll(slot, jpg != null);
                json = JsonConvert.SerializeObject(data, JsonSettings);
            }
            catch (Exception e)
            {
                IsBusy = false;
                SaveFailed?.Invoke(slot, e.Message);
                Debug.LogException(e);
                yield break;
            }

            // 3) Compress + write off the main thread.
            string savPath = PathFor(slot, ".sav"), metaJson = JsonConvert.SerializeObject(data.meta, Formatting.Indented);
            var task = Task.Run(() => WriteFiles(slot, savPath, json, metaJson, jpg));
            while (!task.IsCompleted) yield return null;

            IsBusy = false;
            if (task.IsFaulted)
            {
                var msg = task.Exception?.GetBaseException().Message;
                Debug.LogError($"[Save] Write failed for {slot}: {msg}");
                SaveFailed?.Invoke(slot, msg);
            }
            else SaveCompleted?.Invoke(slot);

            if (_pendingBossAutoSave != null) { var r = _pendingBossAutoSave; _pendingBossAutoSave = null; RequestAutoSave(r, true); }
        }

        SaveData CaptureAll(string slot, bool hasThumb)
        {
            var data = new SaveData { gameBuild = Application.version };
            data.player.name = PlayerName;
            foreach (var s in _saveables.ToList()) s.CaptureState(data);

            var m = data.meta;
            m.slotId = slot;
            m.playerName = PlayerName;
            m.biome = data.world.currentBiome;
            m.playtimeSeconds = PlaytimeSeconds;
            m.timestampUtcTicks = DateTime.UtcNow.Ticks;
            m.saveVersion = SaveData.CurrentVersion;
            m.thumbnailFile = hasThumb ? slot + ".jpg" : null;
            // act/chapter/gold are filled by StoryProgressManager / EconomyManager captures.
            return data;
        }

        void WriteFiles(string slot, string savPath, string json, string metaJson, byte[] jpg)
        {
            byte[] payload = Encode(json);
            string tmp = savPath + ".tmp";
            File.WriteAllBytes(tmp, payload);

            if (File.Exists(savPath))
            {
                string bak = savPath + ".bak";
                if (File.Exists(bak)) File.Delete(bak);
                File.Move(savPath, bak);
            }
            File.Move(tmp, savPath);

            File.WriteAllText(PathFor(slot, ".meta.json"), metaJson);
            if (jpg != null) File.WriteAllBytes(PathFor(slot, ".jpg"), jpg);
        }

        byte[] CaptureThumbnail()
        {
            Texture2D shot;
            if (thumbnailCamera)
            {
                var rt = RenderTexture.GetTemporary(thumbWidth, thumbHeight, 16);
                var prev = thumbnailCamera.targetTexture;
                thumbnailCamera.targetTexture = rt;
                thumbnailCamera.Render();
                thumbnailCamera.targetTexture = prev;
                shot = ReadRT(rt);
                RenderTexture.ReleaseTemporary(rt);
            }
            else
            {
                var full = ScreenCapture.CaptureScreenshotAsTexture();
                var rt = RenderTexture.GetTemporary(thumbWidth, thumbHeight, 0);
                Graphics.Blit(full, rt);                // GPU downscale — cheap on mobile
                Destroy(full);
                shot = ReadRT(rt);
                RenderTexture.ReleaseTemporary(rt);
            }
            var bytes = shot.EncodeToJPG(75);
            Destroy(shot);
            return bytes;
        }

        Texture2D ReadRT(RenderTexture rt)
        {
            var prev = RenderTexture.active;
            RenderTexture.active = rt;
            var tex = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
            tex.Apply(false);
            RenderTexture.active = prev;
            return tex;
        }
        #endregion

        // ----------------------------------------------------------------------------
        #region Load
        IEnumerator LoadRoutine(string slot)
        {
            while (IsBusy) yield return null;
            IsBusy = true;

            string savPath = PathFor(slot, ".sav");
            var task = Task.Run(() => ReadWithFallback(savPath));
            while (!task.IsCompleted) yield return null;

            IsBusy = false;
            if (task.IsFaulted || task.Result == null)
            {
                Debug.LogError($"[Save] Could not load {slot}: {task.Exception?.GetBaseException().Message ?? "no data"}");
                yield break;
            }
            yield return ApplyRoutine(task.Result);
            LoadCompleted?.Invoke(slot);
        }

        SaveData ReadWithFallback(string savPath)
        {
            foreach (var p in new[] { savPath, savPath + ".bak" })
            {
                if (!File.Exists(p)) continue;
                try { return ParseAndMigrate(Decode(File.ReadAllBytes(p))); }
                catch (Exception e) { Debug.LogWarning($"[Save] {Path.GetFileName(p)} unreadable ({e.Message}), trying backup."); }
            }
            return null;
        }

        static SaveData ParseAndMigrate(string json)
        {
            var root = JObject.Parse(json);
            SaveMigrator.Migrate(root);
            return root.ToObject<SaveData>(JsonSerializer.Create(JsonSettings));
        }

        IEnumerator ApplyRoutine(SaveData data)
        {
            IsRestoring = true;
            PlayerName = data.meta.playerName ?? data.player.name ?? PlayerName;
            PlaytimeSeconds = data.meta.playtimeSeconds;
            foreach (var s in _saveables.OrderBy(s => s.RestoreOrder).ToList())
            {
                try { s.RestoreState(data); }
                catch (Exception e) { Debug.LogException(e); }  // one bad system shouldn't brick the save
                yield return null;                               // spread work, keep UI responsive
            }
            // Let world streaming settle before re-enabling auto-saves.
            yield return new WaitForSecondsRealtime(1f);
            _lastAutoSave = Time.unscaledTime;
            IsRestoring = false;
        }
        #endregion

        // ----------------------------------------------------------------------------
        #region Encoding
        static byte[] Encode(string json)
        {
            string header = Magic + "|" + Sha256(json);
            using var ms = new MemoryStream();
            using (var gz = new GZipStream(ms, System.IO.Compression.CompressionLevel.Optimal, leaveOpen: true))
            using (var w = new StreamWriter(gz, new UTF8Encoding(false)))
            {
                w.Write(header);
                w.Write('\n');
                w.Write(json);
            }
            return ms.ToArray();
        }

        static string Decode(byte[] bytes)
        {
            using var ms = new MemoryStream(bytes);
            using var gz = new GZipStream(ms, CompressionMode.Decompress);
            using var r = new StreamReader(gz, Encoding.UTF8);
            string header = r.ReadLine();
            string json = r.ReadToEnd();
            var parts = header?.Split('|');
            if (parts == null || parts.Length != 2 || parts[0] != Magic) throw new InvalidDataException("Not a save file");
            if (parts[1] != Sha256(json)) throw new InvalidDataException("Checksum mismatch (corrupt save)");
            return json;
        }

        static string Sha256(string s)
        {
            using var sha = SHA256.Create();
            return BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(s))).Replace("-", "");
        }
        #endregion

        // ----------------------------------------------------------------------------
        #region Export / Import
        [Serializable]
        class ExportBundle
        {
            public string magic = ExportMagic;
            public SaveMetadata meta;
            public string thumbnailBase64;
            public string payloadBase64;     // the .sav bytes, already gzipped + checksummed
        }

        /// <summary>Packs one slot (save + meta + thumbnail) into a single portable .sav file.</summary>
        public void Export(string slotId, Action<bool, string> onDone)
        {
            string sav = PathFor(slotId, ".sav");
            if (!File.Exists(sav)) { onDone?.Invoke(false, "Empty slot"); return; }

            var meta = ListSlots().FirstOrDefault(m => m.slotId == slotId);
            var jpgPath = PathFor(slotId, ".jpg");
            var bundle = new ExportBundle
            {
                meta = meta,
                payloadBase64 = Convert.ToBase64String(File.ReadAllBytes(sav)),
                thumbnailBase64 = File.Exists(jpgPath) ? Convert.ToBase64String(File.ReadAllBytes(jpgPath)) : null,
            };
            string tmp = Path.Combine(Application.temporaryCachePath, "export.sav");
            File.WriteAllText(tmp, JsonConvert.SerializeObject(bundle));

            string safeName = string.Concat((meta?.playerName ?? "save").Where(char.IsLetterOrDigit));
            string name = $"IsekaiChaos_{safeName}_{DateTime.Now:yyyyMMdd_HHmm}.sav";
            _bridge.Export(tmp, name, onDone);
        }

        /// <summary>Imports a bundle into the given manual slot after validating it.</summary>
        public void Import(int targetManualSlot, Action<bool, string> onDone)
        {
            _bridge.Import(path =>
            {
                if (string.IsNullOrEmpty(path)) { onDone?.Invoke(false, "Cancelled"); return; }
                try
                {
                    var bundle = JsonConvert.DeserializeObject<ExportBundle>(File.ReadAllText(path));
                    if (bundle?.magic != ExportMagic) throw new InvalidDataException("Not an Isekai Chaos export");
                    var payload = Convert.FromBase64String(bundle.payloadBase64);
                    var data = ParseAndMigrate(Decode(payload));       // validates checksum + version

                    string slot = ManualSlot(Mathf.Clamp(targetManualSlot, 0, ManualSlotCount - 1));
                    var meta = data.meta;
                    meta.slotId = slot;
                    byte[] jpg = bundle.thumbnailBase64 != null ? Convert.FromBase64String(bundle.thumbnailBase64) : null;
                    meta.thumbnailFile = jpg != null ? slot + ".jpg" : null;
                    // Re-encode so the stored file matches the current version after migration.
                    data.saveVersion = SaveData.CurrentVersion;
                    WriteFiles(slot, PathFor(slot, ".sav"), JsonConvert.SerializeObject(data, JsonSettings),
                               JsonConvert.SerializeObject(meta, Formatting.Indented), jpg);
                    onDone?.Invoke(true, slot);
                }
                catch (Exception e) { onDone?.Invoke(false, e.Message); }
            });
        }
        #endregion

        int FindOldestAutoIndex()
        {
            int oldest = 0; DateTime t = DateTime.MaxValue;
            for (int i = 0; i < AutoSlotCount; i++)
            {
                var p = PathFor(AutoSlot(i), ".sav");
                if (!File.Exists(p)) return i;
                var w = File.GetLastWriteTimeUtc(p);
                if (w < t) { t = w; oldest = i; }
            }
            return oldest;
        }
    }
}
