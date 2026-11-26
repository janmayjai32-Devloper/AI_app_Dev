using System;
using System.Collections.Generic;
using System.Linq;
using IsekaiChaos.Core;
using IsekaiChaos.Economy;
using IsekaiChaos.SaveSystem;
using UnityEngine;

namespace IsekaiChaos.BaseBuilding
{

    /// <summary>
    /// Grid-plot base: place/upgrade buildings, assign recruited NPCs as workers, accrue
    /// passive income. Worker efficiency comes from a delegate so NPC flaws (e.g. the
    /// blacksmith who only works after a drinking contest) can modify output.
    /// </summary>
    public class BaseBuildingManager : MonoBehaviour, ISaveable
    {
        public static BaseBuildingManager Instance { get; private set; }

        [SerializeField] List<BuildingDefinition> definitions = new List<BuildingDefinition>();
        [SerializeField] Vector2Int[] gridSizePerPlotLevel = { new Vector2Int(6, 6), new Vector2Int(10, 10), new Vector2Int(14, 14), new Vector2Int(20, 20) };

        /// <summary>(npcId, buildingId) → efficiency multiplier. Set by companion systems.</summary>
        public static Func<string, string, float> WorkerEfficiency = (_, __) => 1f;

        public int PlotLevel { get; private set; } = 1;
        public int RestoreOrder => 20;
        public Vector2Int GridSize => gridSizePerPlotLevel[Mathf.Clamp(PlotLevel - 1, 0, gridSizePerPlotLevel.Length - 1)];

        readonly List<BuildingSaveState> _buildings = new List<BuildingSaveState>();
        public IReadOnlyList<BuildingSaveState> Buildings => _buildings;

        void Awake()
        {
            if (Instance && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        void OnEnable() => SaveLoadManager.Register(this);
        void OnDisable() => SaveLoadManager.Unregister(this);

        BuildingDefinition Def(string id) => definitions.FirstOrDefault(d => d.buildingId == id);

        public bool CanPlace(BuildingDefinition def, int x, int y, int rot, string ignoreInstance = null)
        {
            var size = rot % 2 == 0 ? def.footprint : new Vector2Int(def.footprint.y, def.footprint.x);
            if (x < 0 || y < 0 || x + size.x > GridSize.x || y + size.y > GridSize.y) return false;
            if (def.requiredPlotLevel > PlotLevel) return false;
            var rect = new RectInt(x, y, size.x, size.y);
            foreach (var b in _buildings)
            {
                if (b.instanceId == ignoreInstance) continue;
                var d = Def(b.buildingId);
                if (d == null) continue;
                var bs = b.rotation % 2 == 0 ? d.footprint : new Vector2Int(d.footprint.y, d.footprint.x);
                if (rect.Overlaps(new RectInt(b.gridX, b.gridY, bs.x, bs.y))) return false;
            }
            return true;
        }

        public BuildingSaveState Build(string buildingId, int x, int y, int rot)
        {
            var def = Def(buildingId);
            if (def == null || !CanPlace(def, x, y, rot)) return null;
            if (!EconomyManager.Instance.TrySpend(def.upgradeCosts[0], $"Build {def.displayName}")) return null;

            var b = new BuildingSaveState
            {
                buildingId = buildingId, instanceId = Guid.NewGuid().ToString("N"),
                gridX = x, gridY = y, rotation = rot, tier = 1,
            };
            _buildings.Add(b);
            GameEvents.RaiseBuildingUpgraded(buildingId, 1);
            return b;
        }

        public bool Upgrade(string instanceId)
        {
            var b = _buildings.FirstOrDefault(x => x.instanceId == instanceId);
            var def = b != null ? Def(b.buildingId) : null;
            if (def == null || b.tier >= def.upgradeCosts.Length) return false;
            if (!EconomyManager.Instance.TrySpend(def.upgradeCosts[b.tier], $"Upgrade {def.displayName}")) return false;
            b.tier++;
            GameEvents.RaiseBuildingUpgraded(b.buildingId, b.tier);
            return true;
        }

        public bool UpgradePlot(long cost)
        {
            if (PlotLevel >= gridSizePerPlotLevel.Length || !EconomyManager.Instance.TrySpend(cost, "Expand base plot")) return false;
            PlotLevel++;
            GameEvents.RaiseBuildingUpgraded("plot", PlotLevel);
            return true;
        }

        public bool AssignWorker(string instanceId, string npcId)
        {
            var b = _buildings.FirstOrDefault(x => x.instanceId == instanceId);
            var def = b != null ? Def(b.buildingId) : null;
            if (def == null || b.assignedNpcIds.Count >= def.maxWorkers) return false;
            foreach (var other in _buildings) other.assignedNpcIds.Remove(npcId);
            b.assignedNpcIds.Add(npcId);
            return true;
        }

        /// <summary>Called by the world clock each in-game hour.</summary>
        public void TickHour()
        {
            foreach (var b in _buildings)
            {
                var def = Def(b.buildingId);
                if (def == null) continue;
                float baseIncome = def.incomePerHour[Mathf.Clamp(b.tier, 0, def.incomePerHour.Length - 1)];
                float workerMult = b.assignedNpcIds.Count == 0 ? 0.25f
                    : b.assignedNpcIds.Sum(n => WorkerEfficiency(n, b.buildingId));
                b.storedIncome += baseIncome * workerMult;
            }
        }

        public long CollectIncome()
        {
            long total = 0;
            foreach (var b in _buildings) { total += (long)b.storedIncome; b.storedIncome -= (long)b.storedIncome; }
            if (total > 0) EconomyManager.Instance.AddGold(total, "Base income");
            return total;
        }

        public void CaptureState(SaveData data)
        {
            data.baseState.plotLevel = PlotLevel;
            data.baseState.buildings = _buildings.Select(b => new BuildingSaveState
            {
                buildingId = b.buildingId, instanceId = b.instanceId, gridX = b.gridX, gridY = b.gridY,
                rotation = b.rotation, tier = b.tier, assignedNpcIds = new List<string>(b.assignedNpcIds),
                storedIncome = b.storedIncome,
            }).ToList();
        }

        public void RestoreState(SaveData data)
        {
            PlotLevel = Mathf.Max(1, data.baseState.plotLevel);
            _buildings.Clear();
            _buildings.AddRange(data.baseState.buildings.Where(b => Def(b.buildingId) != null));
        }
    }
}
