using System;
using IsekaiChaos.Core;
using UnityEngine;

namespace IsekaiChaos.Economy
{
    [CreateAssetMenu(menuName = "IsekaiChaos/Trade Good", fileName = "TradeGood")]
    public class TradeGood : ScriptableObject
    {
        public string goodId;
        public string displayName;
        public long basePrice = 10;
        [Tooltip("How strongly price reacts to supply/demand imbalance.")]
        [Range(0f, 2f)] public float elasticity = 0.6f;
        [Tooltip("Biomes that produce this good cheaply.")]
        public BiomeId[] producedIn = Array.Empty<BiomeId>();
    }
}
