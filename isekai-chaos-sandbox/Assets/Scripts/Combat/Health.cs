using System;
using System.Collections;
using IsekaiChaos.Customizer;
using UnityEngine;

namespace IsekaiChaos.Combat
{
    public enum Team { Player = 0, Companion = 0, Enemy = 1, Neutral = 2 }

    public struct DamageInfo
    {
        public float amount;
        public float poiseDamage;
        public DamageElement element;
        public HitReaction reaction;
        public Vector3 point;
        public Vector3 direction;         // attacker → target, flattened
        public float knockback;
        public BodyRegion region;
        public float clothingDamage;
        public GameObject source;
    }

    /// <summary>
    /// HP + poise + hit reactions. Poise absorbs flinches; when broken the character plays the
    /// attack's reaction (directional flinch / stagger / knockdown / launch) and briefly can't act.
    /// Clothing damage is forwarded to the LayeredWardrobeController on the hit region.
    /// </summary>
    public class Health : MonoBehaviour
    {
        static readonly int HitDirX = Animator.StringToHash("HitDirX");
        static readonly int HitDirY = Animator.StringToHash("HitDirY");
        static readonly int HitReactionParam = Animator.StringToHash("HitReaction");
        static readonly int HitTrigger = Animator.StringToHash("Hit");
        static readonly int DeadParam = Animator.StringToHash("Dead");

        [SerializeField] Team team = Team.Enemy;
        [SerializeField] float maxHp = 100f;
        [SerializeField] float maxPoise = 30f;
        [SerializeField] float poiseRegenPerSecond = 15f;
        [SerializeField] float poiseRegenDelay = 1.5f;
        [SerializeField] Animator animator;
        [SerializeField] LayeredWardrobeController wardrobe;
        [SerializeField] CharacterController characterController;
        [SerializeField] float[] reactionLockSeconds = { 0f, 0.35f, 0.8f, 1.6f, 1.9f };

        public Team Team => team;
        public float Hp { get; private set; }
        public float MaxHp => maxHp;
        public float Poise { get; private set; }
        public bool IsDead { get; private set; }
        public bool Invulnerable { get; set; }          // dodge i-frames, cutscenes
        public bool IsStunned => Time.time < _stunUntil;

        public event Action<DamageInfo> Damaged;
        public event Action<HitReaction> Reacted;
        public event Action Died;

        float _stunUntil, _lastHitTime;

        void Awake()
        {
            Hp = maxHp;
            Poise = maxPoise;
            if (!animator) animator = GetComponentInChildren<Animator>();
            if (!wardrobe) wardrobe = GetComponentInChildren<LayeredWardrobeController>();
            if (!characterController) characterController = GetComponent<CharacterController>();
        }

        void Update()
        {
            if (Time.time - _lastHitTime > poiseRegenDelay)
                Poise = Mathf.Min(maxPoise, Poise + poiseRegenPerSecond * Time.deltaTime);
        }

        public bool TakeDamage(DamageInfo info)
        {
            if (IsDead || Invulnerable) return false;
            _lastHitTime = Time.time;

            Hp = Mathf.Max(0, Hp - info.amount);
            Poise -= info.poiseDamage;
            Damaged?.Invoke(info);

            if (wardrobe && info.clothingDamage > 0) wardrobe.ApplyDamage(info.region, info.clothingDamage);

            if (Hp <= 0) { Die(); return true; }

            var reaction = Poise <= 0 ? (HitReaction)Mathf.Max((int)info.reaction, (int)HitReaction.Stagger) : info.reaction;
            if (Poise <= 0) Poise = maxPoise;                 // poise break resets
            if (reaction != HitReaction.None && (Poise < maxPoise || info.reaction >= HitReaction.Knockdown))
                PlayReaction(reaction, info);
            return true;
        }

        void PlayReaction(HitReaction reaction, DamageInfo info)
        {
            _stunUntil = Time.time + reactionLockSeconds[(int)reaction];
            if (animator)
            {
                // Directional blend: local-space hit direction drives a 2D blend tree of flinches.
                Vector3 local = transform.InverseTransformDirection(-info.direction);
                animator.SetFloat(HitDirX, local.x);
                animator.SetFloat(HitDirY, local.z);
                animator.SetInteger(HitReactionParam, (int)reaction);
                animator.SetTrigger(HitTrigger);
            }
            if (info.knockback > 0 && characterController) StartCoroutine(Knockback(info.direction, info.knockback));
            Reacted?.Invoke(reaction);
        }

        IEnumerator Knockback(Vector3 dir, float distance)
        {
            const float duration = 0.18f;
            float t = 0;
            dir.y = 0; dir.Normalize();
            while (t < duration && characterController && characterController.enabled)
            {
                float dt = Time.deltaTime;
                characterController.Move(dir * (distance / duration) * dt);
                t += dt;
                yield return null;
            }
        }

        void Die()
        {
            IsDead = true;
            if (animator) animator.SetBool(DeadParam, true);
            Died?.Invoke();
        }

        public void Heal(float amount) { if (!IsDead) Hp = Mathf.Min(maxHp, Hp + amount); }

        public void Revive(float hpFraction = 0.5f)
        {
            IsDead = false;
            Hp = maxHp * hpFraction;
            Poise = maxPoise;
            if (animator) animator.SetBool(DeadParam, false);
        }

        public void SetHp(float hp) => Hp = Mathf.Clamp(hp, 0, maxHp);
    }
}
