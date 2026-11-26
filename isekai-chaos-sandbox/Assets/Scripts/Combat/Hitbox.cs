using System.Collections.Generic;
using UnityEngine;

namespace IsekaiChaos.Combat
{
    /// <summary>
    /// Weapon / limb hit volume. Active only during an attack's hit window. Uses a swept
    /// capsule between last and current frame positions so fast swings don't tunnel through
    /// targets at 30 fps on low-end phones. Zero allocations per frame.
    /// </summary>
    public class Hitbox : MonoBehaviour
    {
        public string hitboxId = "Weapon";
        [SerializeField] Transform tip;                // capsule end (blade tip); start = this transform
        [SerializeField] float radius = 0.25f;
        [SerializeField] LayerMask hurtboxMask;

        static readonly Collider[] Buffer = new Collider[16];
        readonly HashSet<Health> _hitThisSwing = new HashSet<Health>();

        Vector3 _prevBase, _prevTip;
        AttackDefinition _attack;
        CombatController _owner;
        bool _active;

        public void Begin(CombatController owner, AttackDefinition attack)
        {
            _owner = owner;
            _attack = attack;
            _active = true;
            _hitThisSwing.Clear();
            _prevBase = transform.position;
            _prevTip = tip ? tip.position : transform.position;
        }

        public void End() => _active = false;

        void FixedUpdate()
        {
            if (!_active) return;
            Vector3 basePos = transform.position;
            Vector3 tipPos = tip ? tip.position : basePos;

            // Two overlaps: current capsule + a capsule across the swept midpoints.
            Sweep(basePos, tipPos);
            Sweep((basePos + _prevBase) * 0.5f, (tipPos + _prevTip) * 0.5f);

            _prevBase = basePos;
            _prevTip = tipPos;
        }

        void Sweep(Vector3 a, Vector3 b)
        {
            int n = Physics.OverlapCapsuleNonAlloc(a, b, radius, Buffer, hurtboxMask, QueryTriggerInteraction.Collide);
            for (int i = 0; i < n && _hitThisSwing.Count < _attack.maxTargets; i++)
            {
                var hurt = Buffer[i].GetComponent<Hurtbox>();
                if (!hurt || hurt.Owner == null || hurt.Owner == _owner.Health) continue;
                if (hurt.Owner.Team == _owner.Health.Team) continue;
                if (!_hitThisSwing.Add(hurt.Owner)) continue;

                Vector3 point = Buffer[i].ClosestPoint(b);
                _owner.OnHitLanded(hurt, _attack, point);
            }
        }

#if UNITY_EDITOR
        void OnDrawGizmos()
        {
            Gizmos.color = _active ? Color.red : new Color(1, 0.5f, 0, 0.4f);
            Gizmos.DrawWireSphere(transform.position, radius);
            if (tip) { Gizmos.DrawWireSphere(tip.position, radius); Gizmos.DrawLine(transform.position, tip.position); }
        }
#endif
    }
}
