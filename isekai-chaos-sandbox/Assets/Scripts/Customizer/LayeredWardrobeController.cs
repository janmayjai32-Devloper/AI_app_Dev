using System;
using System.Collections.Generic;
using System.Linq;
using IsekaiChaos.Core;
using IsekaiChaos.SaveSystem;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace IsekaiChaos.Customizer
{
    /// <summary>
    /// Manages the 5-layer equip / strip matrix for one character (player or NPC):
    ///  * equip / unequip per layer+slot, with garments skinned onto the shared skeleton
    ///  * per-layer visibility toggles (strip toggles) for menus and scenes
    ///  * durability: Intact -> Torn -> Stripped
    ///  * skin occlusion: hides body-region renderers fully covered by visible garments
    ///
    /// Mobile notes: garments are loaded via Addressables and released on unequip; bone
    /// remapping is done once per instance; occlusion rebuild is O(regions) and only runs
    /// when something actually changes (dirty flag), never per frame.
    /// </summary>
    [DisallowMultipleComponent]
    public class LayeredWardrobeController : MonoBehaviour
    {
        [Serializable]
        public class RegionRenderer
        {
            public BodyRegion region;
            public Renderer renderer;
        }

        [Header("Rig")]
        [SerializeField] SkinnedMeshRenderer bodyRootRenderer;       // provides the bone array
        [SerializeField] Transform garmentParent;
        [SerializeField] List<RegionRenderer> bodyRegions = new List<RegionRenderer>();

        [Header("Data")]
        [SerializeField] WardrobeDatabase database;

        [Header("Rules")]
        [Tooltip("NPCs/player always keep at least this layer visible when mature content is off.")]
        [SerializeField] WardrobeLayer minimumLayerWhenMatureOff = WardrobeLayer.Undergarments;

        public event Action Changed;

        class Equipped
        {
            public WardrobeItem item;
            public float durability;
            public GarmentCondition condition;
            public GameObject instance;
            public AsyncOperationHandle<GameObject> handle;
            public string loadedKey;
        }

        // key = layer + ":" + slot
        readonly Dictionary<string, Equipped> _equipped = new Dictionary<string, Equipped>();
        readonly HashSet<WardrobeLayer> _hiddenLayers = new HashSet<WardrobeLayer>();
        Dictionary<string, Transform> _boneLookup;
        bool _dirty;

        // -------------------------------------------------------------------------------
        #region Unity
        void Awake()
        {
            BuildBoneLookup();
            ContentSettings.Changed += OnContentSettingsChanged;
        }

        void OnDestroy()
        {
            ContentSettings.Changed -= OnContentSettingsChanged;
            foreach (var e in _equipped.Values) ReleaseInstance(e);
            _equipped.Clear();
        }

        void LateUpdate()
        {
            if (!_dirty) return;
            _dirty = false;
            ApplyVisibility();
            Changed?.Invoke();
        }
        #endregion

        // -------------------------------------------------------------------------------
        #region Public API — equip
        public bool Equip(WardrobeItem item, float durabilityRatio = 1f)
        {
            if (item == null || item.layer == WardrobeLayer.BaseSkin) return false;

            string key = SlotKey(item.layer, item.slot);
            if (_equipped.TryGetValue(key, out var existing))
            {
                if (existing.item == item) return true;
                ReleaseInstance(existing);
            }

            var e = new Equipped
            {
                item = item,
                durability = Mathf.Clamp01(durabilityRatio) * item.maxDurability,
            };
            e.condition = ConditionFor(e);
            _equipped[key] = e;
            LoadMeshFor(e);
            MarkDirty();
            return true;
        }

        public void Unequip(WardrobeLayer layer, string slot)
        {
            string key = SlotKey(layer, slot);
            if (!_equipped.TryGetValue(key, out var e)) return;
            ReleaseInstance(e);
            _equipped.Remove(key);
            MarkDirty();
        }

        public void UnequipLayer(WardrobeLayer layer)
        {
            foreach (var key in _equipped.Where(kv => kv.Value.item.layer == layer).Select(kv => kv.Key).ToList())
            {
                ReleaseInstance(_equipped[key]);
                _equipped.Remove(key);
            }
            MarkDirty();
        }

        public IEnumerable<WardrobeItem> GetEquipped(WardrobeLayer layer) =>
            _equipped.Values.Where(e => e.item.layer == layer).Select(e => e.item);

        public GarmentCondition GetCondition(WardrobeItem item) =>
            _equipped.Values.FirstOrDefault(e => e.item == item)?.condition ?? GarmentCondition.Stripped;
        #endregion

        // -------------------------------------------------------------------------------
        #region Public API — strip toggles
        /// <summary>
        /// Show/hide an entire layer without unequipping it (used by the wardrobe preview,
        /// hot springs, and scene clothing toggles). Respects the mature-content gate.
        /// </summary>
        public bool SetLayerVisible(WardrobeLayer layer, bool visible)
        {
            if (layer == WardrobeLayer.BaseSkin) return false;
            if (!visible && !CanHideLayer(layer)) return false;

            bool changed = visible ? _hiddenLayers.Remove(layer) : _hiddenLayers.Add(layer);
            if (changed) MarkDirty();
            return changed;
        }

        public bool IsLayerVisible(WardrobeLayer layer) => !_hiddenLayers.Contains(layer);

        /// <summary>
        /// Convenience for the 0..4 "Fully Clothed → minimal" slider in menus:
        /// everything above <paramref name="outermostVisible"/> is hidden.
        /// </summary>
        public void SetDressLevel(WardrobeLayer outermostVisible)
        {
            foreach (WardrobeLayer l in Enum.GetValues(typeof(WardrobeLayer)))
            {
                if (l == WardrobeLayer.BaseSkin) continue;
                // Accessories are independent of the dress slider.
                if (l == WardrobeLayer.Accessories) continue;
                SetLayerVisible(l, l <= outermostVisible);
            }
        }

        public void ShowAllLayers()
        {
            if (_hiddenLayers.Count == 0) return;
            _hiddenLayers.Clear();
            MarkDirty();
        }

        bool CanHideLayer(WardrobeLayer layer)
        {
            if (ContentSettings.MatureContentEnabled) return true;
            return layer > minimumLayerWhenMatureOff;
        }
        #endregion

        // -------------------------------------------------------------------------------
        #region Public API — durability
        /// <summary>Apply combat/scene damage to the outermost visible garment covering the region.</summary>
        public void ApplyDamage(BodyRegion hitRegion, float amount)
        {
            var target = _equipped.Values
                .Where(e => e.condition != GarmentCondition.Stripped
                            && IsLayerVisible(e.item.layer)
                            && e.item.canBeStrippedInCombat
                            && (e.item.occludesWhenIntact & hitRegion) != 0)
                .OrderByDescending(e => e.item.layer)
                .FirstOrDefault();
            if (target == null) return;
            ModifyDurability(target, -amount);
        }

        public void Repair(WardrobeItem item, float amount)
        {
            var e = _equipped.Values.FirstOrDefault(x => x.item == item);
            if (e != null) ModifyDurability(e, amount);
        }

        void ModifyDurability(Equipped e, float delta)
        {
            var before = e.condition;
            e.durability = Mathf.Clamp(e.durability + delta, 0f, e.item.maxDurability);
            var after = ConditionFor(e);

            // With mature content off, garments at/below the minimum layer can tear but never strip.
            if (after == GarmentCondition.Stripped && !ContentSettings.MatureContentEnabled
                && e.item.layer <= minimumLayerWhenMatureOff)
            {
                after = GarmentCondition.Torn;
                e.durability = Mathf.Max(e.durability, 1f);
            }

            e.condition = after;
            if (before != after)
            {
                // Swap intact <-> torn mesh (no-op if the key is unchanged); stripped just hides.
                if (after != GarmentCondition.Stripped) LoadMeshFor(e);
                MarkDirty();
            }
        }

        static GarmentCondition ConditionFor(Equipped e)
        {
            if (e.durability <= 0f) return GarmentCondition.Stripped;
            float ratio = e.durability / e.item.maxDurability;
            return ratio <= e.item.tornThreshold ? GarmentCondition.Torn : GarmentCondition.Intact;
        }
        #endregion

        // -------------------------------------------------------------------------------
        #region Visibility & skin occlusion
        void MarkDirty() => _dirty = true;

        void ApplyVisibility()
        {
            BodyRegion occluded = BodyRegion.None;

            foreach (var e in _equipped.Values)
            {
                bool show = IsLayerVisible(e.item.layer) && e.condition != GarmentCondition.Stripped;
                if (e.instance) e.instance.SetActive(show);
                if (!show) continue;

                var occ = e.condition == GarmentCondition.Intact ? e.item.occludesWhenIntact : e.item.occludesWhenTorn;
                occluded |= occ & ~e.item.passThrough;
            }

            foreach (var rr in bodyRegions)
                if (rr.renderer) rr.renderer.enabled = (occluded & rr.region) == 0;
        }

        void OnContentSettingsChanged()
        {
            if (ContentSettings.MatureContentEnabled) return;
            // Re-apply safety rules when mature content is switched off.
            _hiddenLayers.RemoveWhere(l => l <= minimumLayerWhenMatureOff);
            MarkDirty();
        }
        #endregion

        // -------------------------------------------------------------------------------
        #region Mesh loading / skinning
        void LoadMeshFor(Equipped e)
        {
            string key = e.condition == GarmentCondition.Torn && !string.IsNullOrEmpty(e.item.tornPrefabKey)
                ? e.item.tornPrefabKey
                : e.item.intactPrefabKey;
            if (string.IsNullOrEmpty(key) || key == e.loadedKey) return;

            ReleaseInstance(e);
            e.loadedKey = key;
            e.handle = Addressables.InstantiateAsync(key, garmentParent ? garmentParent : transform);
            e.handle.Completed += h =>
            {
                // Item may have been swapped while loading.
                if (e.loadedKey != key || h.Status != AsyncOperationStatus.Succeeded)
                {
                    if (h.Status == AsyncOperationStatus.Succeeded) Addressables.ReleaseInstance(h.Result);
                    return;
                }
                e.instance = h.Result;
                BindToSkeleton(e.instance);
                MarkDirty();
            };
        }

        void ReleaseInstance(Equipped e)
        {
            if (e.instance) Addressables.ReleaseInstance(e.instance);
            else if (e.handle.IsValid()) Addressables.Release(e.handle);
            e.instance = null;
            e.loadedKey = null;
        }

        void BuildBoneLookup()
        {
            _boneLookup = new Dictionary<string, Transform>();
            if (!bodyRootRenderer) return;
            foreach (var b in bodyRootRenderer.bones)
                if (b) _boneLookup[b.name] = b;
        }

        /// <summary>Remaps a garment's SkinnedMeshRenderers onto the body skeleton by bone name.</summary>
        void BindToSkeleton(GameObject garment)
        {
            foreach (var smr in garment.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                var src = smr.bones;
                var dst = new Transform[src.Length];
                for (int i = 0; i < src.Length; i++)
                    dst[i] = src[i] && _boneLookup.TryGetValue(src[i].name, out var t) ? t : null;
                smr.bones = dst;
                if (smr.rootBone && _boneLookup.TryGetValue(smr.rootBone.name, out var root)) smr.rootBone = root;
                smr.updateWhenOffscreen = false;
                // Garment shares the body's bounds — avoids per-garment culling mismatch.
                if (bodyRootRenderer) smr.localBounds = bodyRootRenderer.localBounds;
            }
            // Drop the garment's own duplicate armature.
            var armature = garment.transform.Find("Armature");
            if (armature) Destroy(armature.gameObject);
        }
        #endregion

        // -------------------------------------------------------------------------------
        #region Save / load
        public WardrobeState CaptureState()
        {
            var s = new WardrobeState();
            foreach (var e in _equipped.Values)
                s.equipped.Add(new WardrobeItemState
                {
                    itemId = e.item.itemId,
                    layer = (int)e.item.layer,
                    durability = e.durability / e.item.maxDurability,
                    condition = (int)e.condition,
                });
            s.hiddenLayers.AddRange(_hiddenLayers.Select(l => (int)l));
            return s;
        }

        public void RestoreState(WardrobeState s)
        {
            foreach (var e in _equipped.Values) ReleaseInstance(e);
            _equipped.Clear();
            _hiddenLayers.Clear();
            if (s == null) { MarkDirty(); return; }

            foreach (var it in s.equipped)
            {
                var item = database ? database.Get(it.itemId) : null;
                if (item == null)
                {
                    Debug.LogWarning($"[Wardrobe] Unknown item '{it.itemId}' in save — skipped.");
                    continue;
                }
                Equip(item, it.durability);
            }
            foreach (var l in s.hiddenLayers) SetLayerVisible((WardrobeLayer)l, false);
            MarkDirty();
        }
        #endregion

        static string SlotKey(WardrobeLayer layer, string slot) => $"{(int)layer}:{slot}";
    }
}
