using System;
using UnityEngine;

namespace IsekaiChaos.BaseBuilding
{
    [CreateAssetMenu(menuName = "IsekaiChaos/Building Definition", fileName = "Building")]
    public class BuildingDefinition : ScriptableObject
    {
        public string buildingId;                 // "shack", "forge", "alchemy_lab", "tavern", "bathhouse", "suites"
        public string displayName;
        public Vector2Int footprint = new Vector2Int(2, 2);
        public long[] upgradeCosts = { 500, 2000, 8000 };    // index = tier being bought (tier 1 = build)
        public int requiredPlotLevel = 1;
        public int maxWorkers = 1;
        [Tooltip("Gold per in-game hour per tier, before worker bonuses.")]
        public float[] incomePerHour = { 0, 5, 12, 25 };
        [Tooltip("Facility id used by romance scenes / events (e.g. hot spring dates).")]
        public string facilityId;
        public string[] prefabKeysPerTier = Array.Empty<string>();
    }
}
