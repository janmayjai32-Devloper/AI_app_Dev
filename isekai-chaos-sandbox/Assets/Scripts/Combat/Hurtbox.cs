using IsekaiChaos.Customizer;
using UnityEngine;

namespace IsekaiChaos.Combat
{
    /// <summary>
    /// Trigger collider on a ragdoll bone. Maps hits to a body region (for clothing damage
    /// and directional hit reactions) and a damage multiplier (weak spots).
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class Hurtbox : MonoBehaviour
    {
        public BodyRegion region = BodyRegion.Chest;
        public float damageMultiplier = 1f;
        public bool isWeakSpot;

        public Health Owner { get; private set; }

        void Awake()
        {
            Owner = GetComponentInParent<Health>();
            GetComponent<Collider>().isTrigger = true;
        }
    }
}
