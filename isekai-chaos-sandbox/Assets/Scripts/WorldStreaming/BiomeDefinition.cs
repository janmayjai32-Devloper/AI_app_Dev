using System;
using IsekaiChaos.Core;
using UnityEngine;

namespace IsekaiChaos.WorldStreaming
{
    /// <summary>
    /// One biome = a rectangle of square chunks in world space. Each chunk is an additive
    /// Addressable scene named "{addressPrefix}_{x}_{z}" (terrain, props, colliders, nav).
    /// Missing keys are treated as empty ocean/void and skipped.
    /// </summary>
    [CreateAssetMenu(menuName = "IsekaiChaos/Biome Definition", fileName = "Biome")]
    public class BiomeDefinition : ScriptableObject
    {
        public BiomeId biomeId;
        public string displayName;

        [Header("Chunk grid (world space)")]
        public Vector2Int gridOrigin;            // chunk coords of the biome's min corner
        public Vector2Int gridSize = new Vector2Int(8, 8);
        public string addressPrefix = "Meadows";

        [Header("Always-loaded content")]
        [Tooltip("Addressable scene with lighting, skybox, ambient audio, far-LOD terrain impostor.")]
        public string biomeShellSceneKey;

        [Header("Atmosphere")]
        public Color fogColor = new Color(0.75f, 0.85f, 1f);
        public float fogDensity = 0.004f;
        public string musicCue;

        [Header("Gameplay")]
        public int recommendedLevel = 1;
        [Tooltip("Boss ids whose encounters trigger a pre-fight auto-save.")]
        public string[] bossIds = Array.Empty<string>();

        public bool ContainsChunk(Vector2Int c) =>
            c.x >= gridOrigin.x && c.y >= gridOrigin.y &&
            c.x < gridOrigin.x + gridSize.x && c.y < gridOrigin.y + gridSize.y;

        public string ChunkKey(Vector2Int c) => $"{addressPrefix}_{c.x}_{c.y}";
    }
}
