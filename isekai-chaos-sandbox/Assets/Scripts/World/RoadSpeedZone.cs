using UnityEngine;

namespace IsekaiChaos.World
{
    /// <summary>
    /// Trigger volume along designer-authored world roads (the highways between towns).
    /// Characters on a road move faster, and mounts/caravans prefer them via NavMesh area
    /// cost (set the road mesh's NavMesh area to "Road" with cost 0.5).
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class RoadSpeedZone : MonoBehaviour
    {
        [Range(0f, 1f)] public float speedBonus = 0.2f;
        public string roadName = "King's Debt Highway";

        void Reset() => GetComponent<Collider>().isTrigger = true;

        void OnTriggerEnter(Collider other)
        {
            var r = other.GetComponentInParent<RoadAwareSpeed>();
            if (r) r.Enter(this);
        }

        void OnTriggerExit(Collider other)
        {
            var r = other.GetComponentInParent<RoadAwareSpeed>();
            if (r) r.Exit(this);
        }
    }
}
