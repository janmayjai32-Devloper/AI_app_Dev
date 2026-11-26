using UnityEngine;

namespace IsekaiChaos.Combat
{
    public enum AbilitySlot { Q = 0, W = 1, E = 2, R = 3, D = 4, F = 5 }
    public enum TargetingMode { Self, TargetUnit, GroundAoE, Skillshot, Dash }

    /// <summary>
    /// An Albion-style ability: "you are what you wear". Weapons grant Q/W/E, armor/outerwear
    /// grants R (chest), D (head), F (boots). Each WardrobeItem / weapon lists the abilities it
    /// can grant and the player picks one per slot in the gear screen.
    ///
    /// The animation side is an <see cref="AttackDefinition"/> (state name, hit window, feel),
    /// so the same data drives single-player action combat and networked ability casts.
    /// </summary>
    [CreateAssetMenu(menuName = "IsekaiChaos/Combat/Ability", fileName = "Ability")]
    public class AbilityDefinition : ScriptableObject
    {
        public string abilityId;
        public string displayName;
        [TextArea] public string description;
        public Sprite icon;
        public AbilitySlot slot = AbilitySlot.Q;

        [Header("Targeting")]
        public TargetingMode targeting = TargetingMode.TargetUnit;
        public float range = 8f;
        public float aoeRadius = 0f;
        [Tooltip("Skillshot width / dash distance.")]
        public float shapeSize = 1f;
        public bool canCastWhileMoving = false;
        public float castTime = 0f;                 // channel before the animation fires
        public bool interruptible = true;

        [Header("Execution")]
        public AttackDefinition attack;             // animation + hit window + damage feel
        public string projectilePrefabKey;          // for skillshots
        public float projectileSpeed = 18f;

        [Header("Cost")]
        public float cooldown = 6f;
        public float energyCost = 10f;

        [Header("Effects")]
        public float healAmount;
        public float shieldAmount;
        public string statusEffectId;               // "slow", "root", "stun", "embarrassed" (can't use items)
        public float statusDuration;
    }
}
