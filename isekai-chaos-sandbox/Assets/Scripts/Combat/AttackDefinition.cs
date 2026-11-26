using IsekaiChaos.Customizer;
using UnityEngine;

namespace IsekaiChaos.Combat
{
    public enum HitReaction { None = 0, Flinch = 1, Stagger = 2, Knockdown = 3, Launch = 4 }
    public enum DamageElement { Physical, Fire, Ice, Lightning, Holy, Dark, Comedy }  // Comedy: guaranteed pratfall, 1 dmg

    /// <summary>
    /// One attack in a combo or skill. Timings are normalized (0..1) over the animation state
    /// so designers can retime clips without touching data.
    ///
    ///   0 ─── windup ───[hitStart ── active ── hitEnd]─── recovery ───[cancelFrom ─ 1]
    ///                                                                 └ next input allowed
    /// </summary>
    [CreateAssetMenu(menuName = "IsekaiChaos/Combat/Attack", fileName = "Attack")]
    public class AttackDefinition : ScriptableObject
    {
        [Header("Animation")]
        [Tooltip("Animator state name (in the Combat layer).")]
        public string animatorState = "Light_1";
        public float crossFade = 0.08f;
        [Tooltip("Use the clip's root motion for lunges/dashes.")]
        public bool useRootMotion = true;
        public float animationSpeed = 1f;

        [Header("Timing (normalized)")]
        [Range(0, 1)] public float hitStart = 0.30f;
        [Range(0, 1)] public float hitEnd = 0.45f;
        [Range(0, 1)] public float cancelFrom = 0.60f;      // chain / dodge allowed after this
        [Range(0, 1)] public float inputBufferFrom = 0.25f; // inputs after this queue the next attack

        [Header("Hitbox")]
        [Tooltip("Name of a Hitbox component on the attacker (e.g. 'RightHand', 'Weapon', 'Tail').")]
        public string hitboxId = "Weapon";
        public int maxTargets = 4;

        [Header("Damage")]
        public float damage = 10f;
        public DamageElement element = DamageElement.Physical;
        public float poiseDamage = 10f;
        public HitReaction reaction = HitReaction.Flinch;
        public float knockback = 2f;
        [Tooltip("Durability damage to the target's outermost garment on that region.")]
        public float clothingDamage = 8f;
        public BodyRegion preferredRegion = BodyRegion.Chest;

        [Header("Feel")]
        [Tooltip("Freeze-frame on hit (seconds). Sells impact; keep 0.03–0.12.")]
        public float hitStop = 0.06f;
        public float cameraShake = 0.15f;
        public string hitVfxKey = "VFX_HitSpark";
        public string swingSfx = "sfx_swing_light";

        [Header("Cost")]
        public float staminaCost = 8f;
        public float manaCost = 0f;
        public float cooldown = 0f;                          // skills only
    }
}
