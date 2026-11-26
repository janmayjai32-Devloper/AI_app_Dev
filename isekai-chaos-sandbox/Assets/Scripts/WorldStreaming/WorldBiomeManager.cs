using System.Collections;
using System.Collections.Generic;
using System.Linq;
using IsekaiChaos.Core;
using IsekaiChaos.SaveSystem;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.ResourceManagement.ResourceProviders;
using UnityEngine.SceneManagement;

namespace IsekaiChaos.WorldStreaming
{
    /// <summary>
    /// Chunk-based world streaming for Android.
    ///
    ///  * World is a grid of square chunks (default 128 m). Chunks within <c>loadRadius</c> of
    ///    the focus (player) are loaded as additive Addressable scenes; chunks beyond
    ///    <c>unloadRadius</c> are released (hysteresis prevents thrashing at borders).
    ///  * Load requests are prioritised nearest-first and throttled to N concurrent ops so
    ///    streaming never spikes a frame; unloads are spread out too.
    ///  * Radii, LOD bias, shadow distance and texture mip limit come from the device tier.
    ///  * Memory pressure (Application.lowMemory) drops to the minimum radius and unloads
    ///    unused assets.
    ///  * Crossing into a chunk owned by another biome swaps the biome "shell" (lighting,
    ///    fog, music) and raises GameEvents.BiomeChanged → auto-save.
    /// </summary>
    public class WorldBiomeManager : MonoBehaviour, ISaveable
    {
        [System.Serializable]
        public class TierSettings
        {
            public int loadRadius = 2;
            public int unloadRadius = 3;
            public int maxConcurrentLoads = 1;
            public float lodBias = 0.8f;
            public float shadowDistance = 35f;
            public int globalTextureMipmapLimit = 1;
            public int maximumLODLevel = 0;
        }

        public static WorldBiomeManager Instance { get; private set; }

        [Header("World")]
        [SerializeField] float chunkSize = 128f;
        [SerializeField] List<BiomeDefinition> biomes = new List<BiomeDefinition>();
        [SerializeField] Transform focus;                         // usually the player

        [Header("Per-tier budgets (Low, Mid, High)")]
        [SerializeField] TierSettings[] tiers =
        {
            new TierSettings { loadRadius = 1, unloadRadius = 2, maxConcurrentLoads = 1, lodBias = 0.6f, shadowDistance = 20f, globalTextureMipmapLimit = 1, maximumLODLevel = 1 },
            new TierSettings { loadRadius = 2, unloadRadius = 3, maxConcurrentLoads = 1, lodBias = 0.9f, shadowDistance = 35f, globalTextureMipmapLimit = 0, maximumLODLevel = 0 },
            new TierSettings { loadRadius = 3, unloadRadius = 4, maxConcurrentLoads = 2, lodBias = 1.3f, shadowDistance = 60f, globalTextureMipmapLimit = 0, maximumLODLevel = 0 },
        };

        [SerializeField] float updateInterval = 0.25f;

        public BiomeId CurrentBiome { get; private set; } = BiomeId.None;
        public int RestoreOrder => 0;

        class ChunkHandle
        {
            public Vector2Int coord;
            public AsyncOperationHandle<SceneInstance> op;
            public bool unloading;
        }

        readonly Dictionary<Vector2Int, ChunkHandle> _loaded = new Dictionary<Vector2Int, ChunkHandle>();
        readonly HashSet<string> _knownKeys = new HashSet<string>();
        readonly HashSet<BiomeId> _discovered = new HashSet<BiomeId>();
        AsyncOperationHandle<SceneInstance> _shellOp;
        TierSettings _tier;
        int _activeLoads;
        bool _lowMemory;
        Vector2Int _lastFocusChunk = new Vector2Int(int.MinValue, int.MinValue);

        // ------------------------------------------------------------------------------
        void Awake()
        {
            if (Instance && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            ApplyTier(DeviceTier.Current);
            Application.lowMemory += OnLowMemory;
        }

        IEnumerator Start()
        {
            // Cache which chunk keys actually exist so we never request missing ones.
            var locOp = Addressables.LoadResourceLocationsAsync("WorldChunk");
            yield return locOp;
            if (locOp.Status == AsyncOperationStatus.Succeeded)
                foreach (var loc in locOp.Result) _knownKeys.Add(loc.PrimaryKey);
            Addressables.Release(locOp);

            var wait = new WaitForSecondsRealtime(updateInterval);
            while (true)
            {
                if (focus) Tick();
                yield return wait;
            }
        }

        void OnEnable() => SaveLoadManager.Register(this);
        void OnDisable() => SaveLoadManager.Unregister(this);

        void OnDestroy()
        {
            Application.lowMemory -= OnLowMemory;
            if (Instance == this) Instance = null;
        }

        public void SetFocus(Transform t) => focus = t;

        // ------------------------------------------------------------------------------
        #region Tier / quality
        public void ApplyTier(PerformanceTier tier)
        {
            _tier = tiers[Mathf.Clamp((int)tier, 0, tiers.Length - 1)];
            QualitySettings.lodBias = _tier.lodBias;
            QualitySettings.maximumLODLevel = _tier.maximumLODLevel;
            QualitySettings.shadowDistance = _tier.shadowDistance;
            QualitySettings.globalTextureMipmapLimit = _tier.globalTextureMipmapLimit; // Unity 2022.2+
        }

        void OnLowMemory()
        {
            if (_lowMemory) return;
            _lowMemory = true;
            Debug.LogWarning("[World] Low memory — shrinking stream radius.");
            _lastFocusChunk = new Vector2Int(int.MinValue, int.MinValue); // force re-evaluation
            Resources.UnloadUnusedAssets();
            StartCoroutine(RecoverFromLowMemory());
        }

        IEnumerator RecoverFromLowMemory()
        {
            yield return new WaitForSecondsRealtime(60f);
            _lowMemory = false;
        }

        int LoadRadius => _lowMemory ? 1 : _tier.loadRadius;
        int UnloadRadius => _lowMemory ? 1 : _tier.unloadRadius;
        #endregion

        // ------------------------------------------------------------------------------
        #region Streaming
        public Vector2Int WorldToChunk(Vector3 p) =>
            new Vector2Int(Mathf.FloorToInt(p.x / chunkSize), Mathf.FloorToInt(p.z / chunkSize));

        void Tick()
        {
            var center = WorldToChunk(focus.position);
            UpdateBiome(center);

            if (center != _lastFocusChunk)
            {
                _lastFocusChunk = center;
                ScheduleUnloads(center);
            }
            ScheduleLoads(center);
        }

        void ScheduleLoads(Vector2Int center)
        {
            if (_activeLoads >= _tier.maxConcurrentLoads) return;
            int r = LoadRadius;

            // Nearest-first: ring by ring.
            for (int ring = 0; ring <= r; ring++)
                for (int dx = -ring; dx <= ring; dx++)
                    for (int dz = -ring; dz <= ring; dz++)
                    {
                        if (Mathf.Max(Mathf.Abs(dx), Mathf.Abs(dz)) != ring) continue;
                        var c = new Vector2Int(center.x + dx, center.y + dz);
                        if (_loaded.ContainsKey(c)) continue;
                        if (!TryGetChunkKey(c, out var key)) continue;

                        BeginLoad(c, key);
                        if (_activeLoads >= _tier.maxConcurrentLoads) return;
                    }
        }

        void BeginLoad(Vector2Int c, string key)
        {
            _activeLoads++;
            var h = new ChunkHandle { coord = c };
            _loaded[c] = h;
            h.op = Addressables.LoadSceneAsync(key, LoadSceneMode.Additive, activateOnLoad: true, priority: 50);
            h.op.Completed += op =>
            {
                _activeLoads--;
                if (op.Status != AsyncOperationStatus.Succeeded)
                {
                    Debug.LogError($"[World] Failed to load chunk {key}");
                    _loaded.Remove(c);
                }
            };
        }

        void ScheduleUnloads(Vector2Int center)
        {
            int r = UnloadRadius;
            foreach (var h in _loaded.Values.ToList())
            {
                int dist = Mathf.Max(Mathf.Abs(h.coord.x - center.x), Mathf.Abs(h.coord.y - center.y));
                if (dist <= r || h.unloading || !h.op.IsDone) continue;
                h.unloading = true;
                Addressables.UnloadSceneAsync(h.op).Completed += _ => _loaded.Remove(h.coord);
            }
        }

        bool TryGetChunkKey(Vector2Int c, out string key)
        {
            foreach (var b in biomes)
            {
                if (!b.ContainsChunk(c)) continue;
                key = b.ChunkKey(c);
                // If the location catalog hasn't been read (or labels unused), trust the definition.
                return _knownKeys.Count == 0 || _knownKeys.Contains(key);
            }
            key = null;
            return false;
        }

        /// <summary>Used by fast travel / load game: drop everything and stream in at the target.</summary>
        public IEnumerator TeleportRoutine(Vector3 position)
        {
            foreach (var h in _loaded.Values.ToList())
                if (h.op.IsDone && !h.unloading) { h.unloading = true; yield return Addressables.UnloadSceneAsync(h.op); }
            _loaded.Clear();
            _activeLoads = 0;

            if (focus) focus.position = position;
            var center = WorldToChunk(position);
            UpdateBiome(center);

            // Synchronously wait for the 3x3 around the player before handing control back.
            var ops = new List<AsyncOperationHandle<SceneInstance>>();
            for (int dx = -1; dx <= 1; dx++)
                for (int dz = -1; dz <= 1; dz++)
                {
                    var c = new Vector2Int(center.x + dx, center.y + dz);
                    if (!TryGetChunkKey(c, out var key)) continue;
                    var h = new ChunkHandle { coord = c, op = Addressables.LoadSceneAsync(key, LoadSceneMode.Additive) };
                    _loaded[c] = h;
                    ops.Add(h.op);
                }
            foreach (var op in ops) yield return op;
            _lastFocusChunk = new Vector2Int(int.MinValue, int.MinValue);
        }
        #endregion

        // ------------------------------------------------------------------------------
        #region Biomes
        public BiomeDefinition GetBiome(BiomeId id) => biomes.FirstOrDefault(b => b.biomeId == id);

        void UpdateBiome(Vector2Int chunk)
        {
            var def = biomes.FirstOrDefault(b => b.ContainsChunk(chunk));
            if (def == null || def.biomeId == CurrentBiome) return;

            var from = CurrentBiome;
            CurrentBiome = def.biomeId;
            _discovered.Add(def.biomeId);

            RenderSettings.fogColor = def.fogColor;
            RenderSettings.fogDensity = def.fogDensity;
            SwapShell(def);

            GameEvents.RaiseBiomeChanged(from, CurrentBiome);
        }

        void SwapShell(BiomeDefinition def)
        {
            if (_shellOp.IsValid()) Addressables.UnloadSceneAsync(_shellOp);
            _shellOp = default;
            if (!string.IsNullOrEmpty(def.biomeShellSceneKey))
                _shellOp = Addressables.LoadSceneAsync(def.biomeShellSceneKey, LoadSceneMode.Additive);
        }

        public bool IsDiscovered(BiomeId id) => _discovered.Contains(id);
        #endregion

        // ------------------------------------------------------------------------------
        #region Save
        public void CaptureState(SaveData data)
        {
            data.world.currentBiome = CurrentBiome;
            data.world.discoveredBiomes = _discovered.ToList();
            if (focus)
            {
                var p = focus.position;
                data.player.position = new[] { p.x, p.y, p.z };
                data.player.yaw = focus.eulerAngles.y;
            }
        }

        public void RestoreState(SaveData data)
        {
            _discovered.Clear();
            _discovered.UnionWith(data.world.discoveredBiomes);
            CurrentBiome = BiomeId.None; // force shell reload in TeleportRoutine
            var p = data.player.position;
            if (p != null && p.Length == 3)
            {
                if (focus) focus.rotation = Quaternion.Euler(0, data.player.yaw, 0);
                StartCoroutine(TeleportRoutine(new Vector3(p[0], p[1], p[2])));
            }
        }
        #endregion
    }
}
