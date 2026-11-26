using System.Collections.Generic;
using UnityEngine;

namespace IsekaiChaos.World
{
    /// <summary>
    /// Sums the speed bonus from world road zones and player-built base roads.
    /// ClickToMoveMotor multiplies its speed by <see cref="Multiplier"/>.
    /// </summary>
    public class RoadAwareSpeed : MonoBehaviour
    {
        [SerializeField] RoadBuilder baseRoads;
        readonly HashSet<RoadSpeedZone> _zones = new HashSet<RoadSpeedZone>();

        public string CurrentRoadName { get; private set; }

        public float Multiplier
        {
            get
            {
                float best = 0f;
                foreach (var z in _zones) if (z && z.speedBonus > best) best = z.speedBonus;
                if (baseRoads) best = Mathf.Max(best, baseRoads.SpeedBonusAt(transform.position));
                return 1f + best;
            }
        }

        public void Enter(RoadSpeedZone z) { _zones.Add(z); CurrentRoadName = z.roadName; }
        public void Exit(RoadSpeedZone z) { _zones.Remove(z); if (_zones.Count == 0) CurrentRoadName = null; }
    }
}
