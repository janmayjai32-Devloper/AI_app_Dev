using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace IsekaiChaos.Combat
{
    /// <summary>
    /// Albion-style combat on one character (player, companion or enemy):
    ///
    ///  * Auto-attack: when a hostile target is selected and in range, the character faces it
    ///    and loops the weapon's basic-attack chain (ComboDefinition.lightChain), playing
    ///    each attack's animation with real hit windows, hitstop and reactions.
    ///  * Ability slots Q/W/E/R/D/F granted by equipped gear (see AbilityDefinition).
    ///    Cast → (optional cast time) → animation → hit window opens the named Hitbox,
    ///    or a projectile / AoE is resolved at the hit frame.
    ///  * Dodge (a Dash-type ability in slot F by default) grants i-frames.
    ///  * Input buffering: a cast pressed during the current animation's buffer window fires
    ///    as soon as its cancel window opens, so play feels responsive on touch screens.
    ///
    /// Fully offline: the same component drives the player, companions (CompanionAI) and
    /// enemies (EnemyAI) — AI just calls SetTarget/RequestCast like the touch UI does.
    /// </summary>
    [RequireComponent(typeof(Health))]
    public class CombatController : MonoBehaviour
    {
        static readonly int AttackSpeedParam = Animator.StringToHash("AttackSpeed");
        static readonly int CastingParam = Animator.StringToHash("Casting");

        [SerializeField] Animator animator;
        [SerializeField] ComboDefinition weapon;
        [SerializeField] List<Hitbox> hitboxes = new List<Hitbox>();
        [SerializeField] float maxEnergy = 100f;
        [SerializeField] float energyRegen = 6f;
        [SerializeField] float turnSpeed = 720f;
        [SerializeField] float basicAttackRange = 2.2f;
        [Header("Direct control (Genshin/NTE style)")]
        [SerializeField] float softLockRange = 6f;
        [SerializeField, Range(0, 180)] float softLockHalfAngle = 70f;
        [SerializeField] LayerMask softLockMask = ~0;

        /// <summary>Tap-to-move mode auto-attacks the selected target; direct mode uses the Attack button.</summary>
        public bool AutoAttackEnabled { get; set; } = true;

        public Health Health { get; private set; }
        public Health Target { get; private set; }
        public float Energy { get; private set; }
        public bool IsBusy => _current != null && _currentNormalized < (_current.cancelFrom);
        public bool IsCasting => _castTimer > 0f;
        public ComboDefinition Weapon => weapon;

        /// <summary>Movement components read this to stop walking during attacks.</summary>
        public bool WantsRootMotion => _current != null && _current.useRootMotion;

        public event Action<Health, AttackDefinition, Vector3> HitLanded;
        public event Action<AbilityDefinition> AbilityCast;
        public event Action<float> HitStopRequested;          // seconds — CameraRig/TimeManager handles it
        public event Action<float> CameraShakeRequested;

        readonly AbilityDefinition[] _slots = new AbilityDefinition[6];
        readonly float[] _cooldownUntil = new float[6];

        AttackDefinition _current;
        bool _bufferedBasic;
        float _currentNormalized;
        int _chainIndex;
        float _chainResetAt;
        Hitbox _activeHitbox;
        bool _hitWindowOpen;

        // cast / buffer
        AbilitySlot? _bufferedSlot;
        Vector3 _bufferedPoint;
        Health _bufferedTarget;
        AbilityDefinition _casting;
        float _castTimer;
        Vector3 _castPoint;
        Health _castTarget;

        void Awake()
        {
            Health = GetComponent<Health>();
            if (!animator) animator = GetComponentInChildren<Animator>();
            Energy = maxEnergy;
            Health.Died += CancelAll;
            if (weapon && weapon.animatorOverrides) animator.runtimeAnimatorController = weapon.animatorOverrides;
        }

        // ------------------------------------------------------------------------------
        #region Loadout
        public void EquipWeapon(ComboDefinition combo)
        {
            weapon = combo;
            if (combo && combo.animatorOverrides) animator.runtimeAnimatorController = combo.animatorOverrides;
            _chainIndex = 0;
        }

        public void SetAbility(AbilitySlot slot, AbilityDefinition ability) => _slots[(int)slot] = ability;
        public AbilityDefinition GetAbility(AbilitySlot slot) => _slots[(int)slot];

        public float CooldownRemaining(AbilitySlot slot) => Mathf.Max(0, _cooldownUntil[(int)slot] - Time.time);
        #endregion

        // ------------------------------------------------------------------------------
        #region Targeting / input
        public void SetTarget(Health target) => Target = target && target.Team != Health.Team ? target : null;
        public void ClearTarget() => Target = null;

        /// <summary>UI / input entry point. Point = ground aim; target = unit under cursor (optional).</summary>
        public bool RequestCast(AbilitySlot slot, Vector3 aimPoint, Health target = null) =>
            TryExecute(slot, aimPoint, target ?? Target);

        /// <summary>Validates cooldown, energy, range, stun; buffers if mid-animation.</summary>
        bool TryExecute(AbilitySlot slot, Vector3 aimPoint, Health target)
        {
            var a = _slots[(int)slot];
            if (a == null || Health.IsDead || Health.IsStunned) return false;
            if (Time.time < _cooldownUntil[(int)slot] || Energy < a.energyCost) return false;
            if (a.targeting == TargetingMode.TargetUnit && (target == null || target.IsDead)) return false;

            Vector3 focus = a.targeting == TargetingMode.TargetUnit ? target.transform.position : aimPoint;
            if (a.targeting != TargetingMode.Self &&
                Vector3.Distance(transform.position, focus) > a.range + 0.5f) return false;

            if (IsBusy || IsCasting)
            {
                if (_current != null && _currentNormalized >= _current.inputBufferFrom)
                { _bufferedSlot = slot; _bufferedPoint = aimPoint; _bufferedTarget = target; }
                return false;
            }

            Energy -= a.energyCost;
            _cooldownUntil[(int)slot] = Time.time + a.cooldown;
            _casting = a; _castPoint = aimPoint; _castTarget = target;
            _castTimer = Mathf.Max(0.0001f, a.castTime);
            animator.SetBool(CastingParam, a.castTime > 0);
            AbilityCast?.Invoke(a);
            return true;
        }
        #endregion

        // ------------------------------------------------------------------------------
        void Update()
        {
            Energy = Mathf.Min(maxEnergy, Energy + energyRegen * Time.deltaTime);
            if (Health.IsDead) return;

            if (Health.IsStunned) { if (_casting && _casting.interruptible) CancelAll(); return; }

            TickCast();
            TickCurrentAttack();
            TickAutoAttack();
            FaceTarget();
        }

        void TickCast()
        {
            if (_casting == null) return;
            _castTimer -= Time.deltaTime;
            if (_castTimer > 0f) return;

            animator.SetBool(CastingParam, false);
            var a = _casting;
            _casting = null;

            if (a.targeting == TargetingMode.Dash) StartCoroutine(DashRoutine(a));
            if (a.healAmount > 0) Health.Heal(a.healAmount);
            if (a.attack) StartAttack(a.attack);
        }

        System.Collections.IEnumerator DashRoutine(AbilityDefinition a)
        {
            Health.Invulnerable = true;
            var cc = GetComponent<CharacterController>();
            Vector3 dir = _castPoint - transform.position; dir.y = 0;
            if (dir.sqrMagnitude < 0.01f) dir = transform.forward;
            dir.Normalize();
            transform.rotation = Quaternion.LookRotation(dir);
            float t = 0, dur = 0.25f;
            while (t < dur)
            {
                var step = dir * (a.shapeSize / dur) * Time.deltaTime;
                if (cc && cc.enabled) cc.Move(step); else transform.position += step;
                t += Time.deltaTime;
                yield return null;
            }
            Health.Invulnerable = false;
        }

        // ------------------------------------------------------------------------------
        #region Direct-control attack button
        /// <summary>
        /// Attack button (or left mouse button in direct mode). Soft lock-on: snaps to the best enemy
        /// in a cone in front of the character (or along the stick direction), then plays the next
        /// hit of the light chain. Pressing during an attack buffers the next hit until the cancel window.
        /// </summary>
        public bool BasicAttack(Vector3 preferredDirection = default)
        {
            if (Health.IsDead || Health.IsStunned || weapon == null || weapon.lightChain.Count == 0) return false;
            if (IsBusy || IsCasting)
            {
                if (_current != null && _currentNormalized >= _current.inputBufferFrom) _bufferedBasic = true;
                return false;
            }

            var target = FindSoftTarget(preferredDirection.sqrMagnitude > 0.01f ? preferredDirection : transform.forward);
            if (target) Target = target;
            if (Target)
            {
                Vector3 d = Target.transform.position - transform.position; d.y = 0;
                if (d.sqrMagnitude > 0.001f) transform.rotation = Quaternion.LookRotation(d);
            }
            else if (preferredDirection.sqrMagnitude > 0.01f)
                transform.rotation = Quaternion.LookRotation(new Vector3(preferredDirection.x, 0, preferredDirection.z));

            NextChainAttack();
            return true;
        }

        static readonly Collider[] SoftLockBuffer = new Collider[24];

        Health FindSoftTarget(Vector3 dir)
        {
            dir.y = 0; dir.Normalize();
            int n = Physics.OverlapSphereNonAlloc(transform.position, softLockRange, SoftLockBuffer, softLockMask, QueryTriggerInteraction.Collide);
            Health best = null; float bestScore = float.MaxValue;
            for (int i = 0; i < n; i++)
            {
                var h = SoftLockBuffer[i].GetComponentInParent<Health>();
                if (!h || h == Health || h.IsDead || h.Team == Health.Team) continue;
                Vector3 to = h.transform.position - transform.position; to.y = 0;
                float angle = Vector3.Angle(dir, to);
                if (angle > softLockHalfAngle) continue;
                float score = to.magnitude + angle * 0.05f;      // prefer close and centered
                if (score < bestScore) { bestScore = score; best = h; }
            }
            return best;
        }

        void NextChainAttack()
        {
            if (Time.time > _chainResetAt) _chainIndex = 0;
            var atk = weapon.lightChain[_chainIndex % weapon.lightChain.Count];
            _chainIndex++;
            StartAttack(atk);
        }
        #endregion

        void TickAutoAttack()
        {
            if (!AutoAttackEnabled) return;
            if (Target == null || Target.IsDead || IsBusy || IsCasting || weapon == null || weapon.lightChain.Count == 0) return;
            if (Vector3.Distance(transform.position, Target.transform.position) > basicAttackRange) return;

            NextChainAttack();
        }

        void StartAttack(AttackDefinition atk)
        {
            EndHitWindow();
            _current = atk;
            _currentNormalized = 0f;
            animator.SetFloat(AttackSpeedParam, atk.animationSpeed);
            animator.applyRootMotion = atk.useRootMotion;
            animator.CrossFadeInFixedTime(atk.animatorState, atk.crossFade, 1 /* Combat layer */);
            _activeHitbox = hitboxes.FirstOrDefault(h => h.hitboxId == atk.hitboxId);
        }

        void TickCurrentAttack()
        {
            if (_current == null) return;

            var info = animator.GetCurrentAnimatorStateInfo(1);
            if (!info.IsName(_current.animatorState))
            {
                // Still crossfading in, or the state was interrupted.
                if (animator.IsInTransition(1)) return;
                FinishAttack();
                return;
            }
            _currentNormalized = info.normalizedTime;

            if (!_hitWindowOpen && _currentNormalized >= _current.hitStart && _currentNormalized < _current.hitEnd)
                BeginHitWindow();
            else if (_hitWindowOpen && _currentNormalized >= _current.hitEnd)
                EndHitWindow();

            if (_currentNormalized >= _current.cancelFrom && _bufferedSlot.HasValue)
            {
                var s = _bufferedSlot.Value; _bufferedSlot = null;
                var c = _current; _current = null;            // allow the buffered cast through
                if (!TryExecute(s, _bufferedPoint, _bufferedTarget)) _current = c;
            }

            if (_currentNormalized >= _current.cancelFrom && _bufferedBasic)
            {
                _bufferedBasic = false;
                NextChainAttack();                                 // combo continues seamlessly
                return;
            }

            if (_currentNormalized >= 1f) FinishAttack();
        }

        void BeginHitWindow()
        {
            _hitWindowOpen = true;
            if (_activeHitbox) _activeHitbox.Begin(this, _current);
            else ResolveAreaHit(_current);                      // no hitbox → instant AoE in front
        }

        void EndHitWindow()
        {
            if (_hitWindowOpen && _activeHitbox) _activeHitbox.End();
            _hitWindowOpen = false;
        }

        void FinishAttack()
        {
            EndHitWindow();
            _current = null;
            animator.applyRootMotion = false;
            _chainResetAt = Time.time + 1.2f;
        }

        void CancelAll()
        {
            EndHitWindow();
            _current = null;
            _casting = null;
            _bufferedSlot = null;
            _bufferedBasic = false;
            if (animator) { animator.SetBool(CastingParam, false); animator.applyRootMotion = false; }
        }

        static readonly Collider[] AoeBuffer = new Collider[24];
        void ResolveAreaHit(AttackDefinition atk)
        {
            Vector3 center = transform.position + transform.forward * 1.2f + Vector3.up;
            int n = Physics.OverlapSphereNonAlloc(center, 1.4f, AoeBuffer, ~0, QueryTriggerInteraction.Collide);
            var seen = new HashSet<Health>();
            for (int i = 0; i < n; i++)
            {
                var hb = AoeBuffer[i].GetComponent<Hurtbox>();
                if (hb && hb.Owner && hb.Owner.Team != Health.Team && seen.Add(hb.Owner))
                    OnHitLanded(hb, atk, AoeBuffer[i].ClosestPoint(center));
            }
        }

        /// <summary>Called by Hitbox (or AoE) when a hurtbox is struck during the window.</summary>
        public void OnHitLanded(Hurtbox hurt, AttackDefinition atk, Vector3 point)
        {
            Vector3 dir = hurt.Owner.transform.position - transform.position; dir.y = 0;
            float mult = hurt.damageMultiplier * (hurt.isWeakSpot ? 1.5f : 1f);
            bool landed = hurt.Owner.TakeDamage(new DamageInfo
            {
                amount = atk.element == DamageElement.Comedy ? 1f : atk.damage * mult,
                poiseDamage = atk.poiseDamage,
                element = atk.element,
                reaction = atk.reaction,
                point = point,
                direction = dir.normalized,
                knockback = atk.knockback,
                region = hurt.region,
                clothingDamage = atk.clothingDamage,
                source = gameObject,
            });
            if (!landed) return;
            HitLanded?.Invoke(hurt.Owner, atk, point);
            if (atk.hitStop > 0) HitStopRequested?.Invoke(atk.hitStop);
            if (atk.cameraShake > 0) CameraShakeRequested?.Invoke(atk.cameraShake);
        }

        void FaceTarget()
        {
            Vector3? look = null;
            if (_current != null && Target) look = Target.transform.position;
            else if (_casting != null) look = _castTarget ? _castTarget.transform.position : _castPoint;
            if (!look.HasValue) return;

            Vector3 d = look.Value - transform.position; d.y = 0;
            if (d.sqrMagnitude < 0.001f) return;
            transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(d), turnSpeed * Time.deltaTime);
        }
    }
}
