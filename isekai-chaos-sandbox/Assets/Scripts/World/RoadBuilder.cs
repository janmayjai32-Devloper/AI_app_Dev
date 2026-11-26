using System;
using System.Collections.Generic;
using IsekaiChaos.SaveSystem;
using UnityEngine;

namespace IsekaiChaos.World
{
    /// <summary>
    /// Player road building on the base plot (and in claimable outposts): drag across grid
    /// cells to lay road; each cell auto-tiles from its 4 neighbours (end / straight / corner /
    /// T / cross) and rebuilds the NavMesh speed area. Roads boost movement speed
    /// (RoadSpeedZone), raise the base's "Charm" (visitor events) and connect buildings.
    ///
    /// Road materials are tiers: dirt path → gravel → cobblestone → enchanted marble.
    /// </summary>
    public class RoadBuilder : MonoBehaviour, ISaveable
    {
        [Serializable]
        public class RoadStyle
        {
            public string styleId = "dirt";
            public long costPerCell = 5;
            public float speedBonus = 0.15f;
            [Tooltip("Indexed by shape: 0=single,1=end,2=straight,3=corner,4=T,5=cross")]
            public GameObject[] shapePrefabs = new GameObject[6];
        }

        [SerializeField] float cellSize = 2f;
        [SerializeField] Transform gridOrigin;
        [SerializeField] List<RoadStyle> styles = new List<RoadStyle>();

        public int RestoreOrder => 25;
        public event Action Changed;

        readonly Dictionary<Vector2Int, string> _cells = new Dictionary<Vector2Int, string>();
        readonly Dictionary<Vector2Int, GameObject> _instances = new Dictionary<Vector2Int, GameObject>();

        void OnEnable() => SaveLoadManager.Register(this);
        void OnDisable() => SaveLoadManager.Unregister(this);

        // ------------------------------------------------------------------- API
        public Vector2Int WorldToCell(Vector3 p)
        {
            Vector3 l = gridOrigin ? gridOrigin.InverseTransformPoint(p) : p;
            return new Vector2Int(Mathf.FloorToInt(l.x / cellSize), Mathf.FloorToInt(l.z / cellSize));
        }

        Vector3 CellToWorld(Vector2Int c)
        {
            var l = new Vector3((c.x + 0.5f) * cellSize, 0, (c.y + 0.5f) * cellSize);
            return gridOrigin ? gridOrigin.TransformPoint(l) : l;
        }

        /// <summary>Lays an L-shaped path between two cells (drag start → drag end).</summary>
        public int PaintPath(Vector2Int from, Vector2Int to, string styleId, Func<Vector2Int, bool> isBlocked, Func<long, bool> trySpend)
        {
            var style = styles.Find(s => s.styleId == styleId);
            if (style == null) return 0;

            var path = new List<Vector2Int>();
            var c = from;
            path.Add(c);
            while (c.x != to.x) { c.x += Math.Sign(to.x - c.x); path.Add(c); }
            while (c.y != to.y) { c.y += Math.Sign(to.y - c.y); path.Add(c); }

            int placed = 0;
            foreach (var cell in path)
            {
                if (_cells.TryGetValue(cell, out var existing) && existing == styleId) continue;
                if (isBlocked != null && isBlocked(cell)) continue;
                if (trySpend != null && !trySpend(style.costPerCell)) break;
                _cells[cell] = styleId;
                placed++;
            }
            if (placed > 0) RebuildAround(path);
            return placed;
        }

        public bool Remove(Vector2Int cell)
        {
            if (!_cells.Remove(cell)) return false;
            RebuildAround(new[] { cell });
            return true;
        }

        public bool HasRoad(Vector2Int cell) => _cells.ContainsKey(cell);

        public float SpeedBonusAt(Vector3 world)
        {
            if (!_cells.TryGetValue(WorldToCell(world), out var id)) return 0f;
            var s = styles.Find(x => x.styleId == id);
            return s?.speedBonus ?? 0f;
        }

        // ------------------------------------------------------------------- auto-tiling
        static readonly Vector2Int[] Dirs = { Vector2Int.up, Vector2Int.right, Vector2Int.down, Vector2Int.left };

        void RebuildAround(IEnumerable<Vector2Int> cells)
        {
            var dirty = new HashSet<Vector2Int>();
            foreach (var c in cells) { dirty.Add(c); foreach (var d in Dirs) dirty.Add(c + d); }
            foreach (var c in dirty) RebuildCell(c);
            Changed?.Invoke();
        }

        void RebuildCell(Vector2Int c)
        {
            if (_instances.TryGetValue(c, out var old)) { Destroy(old); _instances.Remove(c); }
            if (!_cells.TryGetValue(c, out var styleId)) return;
            var style = styles.Find(s => s.styleId == styleId);
            if (style == null) return;

            int mask = 0;                                  // bit0=N, bit1=E, bit2=S, bit3=W
            for (int i = 0; i < 4; i++) if (_cells.ContainsKey(c + Dirs[i])) mask |= 1 << i;
            var (shape, rotSteps) = Classify(mask);
            var prefab = style.shapePrefabs.Length > shape ? style.shapePrefabs[shape] : null;
            if (!prefab) return;

            var rot = Quaternion.Euler(0, rotSteps * 90f, 0) * (gridOrigin ? gridOrigin.rotation : Quaternion.identity);
            _instances[c] = Instantiate(prefab, CellToWorld(c), rot, transform);
        }

        /// <summary>Maps the 4-bit neighbour mask to (shape index, 90° rotation steps). Prefabs are authored facing N.</summary>
        static (int shape, int rot) Classify(int m)
        {
            switch (m)
            {
                case 0: return (0, 0);
                case 1: return (1, 0); case 2: return (1, 1); case 4: return (1, 2); case 8: return (1, 3);
                case 5: return (2, 0); case 10: return (2, 1);
                case 3: return (3, 0); case 6: return (3, 1); case 12: return (3, 2); case 9: return (3, 3);
                case 7: return (4, 0); case 14: return (4, 1); case 13: return (4, 2); case 11: return (4, 3);
                default: return (5, 0);
            }
        }

        // ------------------------------------------------------------------- save
        public void CaptureState(SaveData data)
        {
            data.baseState.roads = new List<RoadCellSaveState>();
            foreach (var kv in _cells) data.baseState.roads.Add(new RoadCellSaveState { x = kv.Key.x, y = kv.Key.y, styleId = kv.Value });
        }

        public void RestoreState(SaveData data)
        {
            foreach (var go in _instances.Values) if (go) Destroy(go);
            _instances.Clear();
            _cells.Clear();
            if (data.baseState.roads == null) return;
            foreach (var r in data.baseState.roads) _cells[new Vector2Int(r.x, r.y)] = r.styleId;
            foreach (var c in new List<Vector2Int>(_cells.Keys)) RebuildCell(c);
            Changed?.Invoke();
        }
    }
}
