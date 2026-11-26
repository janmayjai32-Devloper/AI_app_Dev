using System;
using UnityEngine;

namespace IsekaiChaos.Customizer
{
    /// <summary>Authoring asset for one garment. Lives in Assets/Data/Wardrobe.</summary>
    [CreateAssetMenu(menuName = "IsekaiChaos/Wardrobe Item", fileName = "WardrobeItem")]
    public class WardrobeItem : ScriptableObject
    {
        [Header("Identity")]
        public string itemId;
        public string displayName;
        public WardrobeLayer layer = WardrobeLayer.Clothing;

        [Tooltip("Mutually-exclusive slot within the layer (e.g. 'top', 'bottom', 'cloak', 'neck').")]
        public string slot = "top";

        [Header("Meshes (Addressable keys)")]
        [Tooltip("Skinned mesh prefab bound to the shared humanoid skeleton.")]
        public string intactPrefabKey;
        [Tooltip("Optional damaged variant. Falls back to intact mesh + torn shader mask.")]
        public string tornPrefabKey;

        [Header("Occlusion")]
        [Tooltip("Body regions fully hidden while this garment is intact.")]
        public BodyRegion occludesWhenIntact;
        [Tooltip("Body regions still hidden while torn (usually a subset).")]
        public BodyRegion occludesWhenTorn;

        [Header("Compatibility")]
        [Tooltip("Races whose silhouette needs a variant (tails, wings) — empty = universal.")]
        public string[] requiresRaceVariantFor = Array.Empty<string>();
        [Tooltip("Cut-out for tails/wings so they pass through.")]
        public BodyRegion passThrough;

        [Header("Durability")]
        [Min(1f)] public float maxDurability = 100f;
        [Range(0f, 1f)] public float tornThreshold = 0.5f;   // durability ratio at/below which it tears
        public bool canBeStrippedInCombat = true;

        [Header("Stats")]
        public int defense;
        public int charm;
    }
}
