using System.Collections.Generic;
using UnityEngine;

namespace IsekaiChaos.Combat
{
    /// <summary>
    /// A weapon's move-set: light chain, heavy chain, branch-from-light finishers,
    /// dodge attack and air attack. Swapping weapons swaps the ComboDefinition and the
    /// Animator Override Controller (clips), not code.
    /// </summary>
    [CreateAssetMenu(menuName = "IsekaiChaos/Combat/Combo Set", fileName = "ComboSet")]
    public class ComboDefinition : ScriptableObject
    {
        public string weaponType = "sword";
        public AnimatorOverrideController animatorOverrides;

        [Tooltip("L, L, L, L ...")]
        public List<AttackDefinition> lightChain = new List<AttackDefinition>();
        [Tooltip("H, H ...")]
        public List<AttackDefinition> heavyChain = new List<AttackDefinition>();
        [Tooltip("Heavy pressed after light #i uses heavyFinishers[i] (L,L,H = finisher 1).")]
        public List<AttackDefinition> heavyFinishers = new List<AttackDefinition>();

        public AttackDefinition dodgeAttack;     // attack pressed during dodge recovery
        public AttackDefinition airAttack;
        public AttackDefinition chargedHeavy;    // hold heavy
        public float chargeTime = 0.6f;
    }
}
