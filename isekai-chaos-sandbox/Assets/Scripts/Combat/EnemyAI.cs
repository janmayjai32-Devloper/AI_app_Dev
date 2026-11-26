using UnityEngine;
using UnityEngine.AI;

namespace IsekaiChaos.Combat
{
    /// <summary>
    /// Simple, cheap FSM for mobs (Giant Frogs, Slimes, bandits). Thinks at 5 Hz, not every
    /// frame; uses the same CombatController/abilities as the player so animations, hit
    /// windows and reactions are shared.
    /// </summary>
    [RequireComponent(typeof(NavMeshAgent), typeof(CombatController))]
    public class EnemyAI : MonoBehaviour
    {
        enum State { Idle, Patrol, Chase, Attack, Return, Dead }

        static readonly int SpeedParam = Animator.StringToHash("Speed");

        [SerializeField] float aggroRadius = 9f;
        [SerializeField] float leashRadius = 22f;
        [SerializeField] float attackRange = 2f;
        [SerializeField] float patrolRadius = 6f;
        [SerializeField] float thinkInterval = 0.2f;
        [SerializeField] AbilitySlot[] abilityRotation = { AbilitySlot.Q, AbilitySlot.W };
        [SerializeField] LayerMask playerMask;
        [SerializeField] Animator animator;

        NavMeshAgent _agent;
        CombatController _combat;
        Health _health;
        Vector3 _home;
        State _state = State.Idle;
        float _nextThink, _nextPatrol;
        int _rotationIndex;
        static readonly Collider[] Buffer = new Collider[8];

        void Awake()
        {
            _agent = GetComponent<NavMeshAgent>();
            _combat = GetComponent<CombatController>();
            _health = GetComponent<Health>();
            if (!animator) animator = GetComponentInChildren<Animator>();
            _home = transform.position;
            _health.Died += () => { _state = State.Dead; _agent.isStopped = true; };
            _health.Damaged += info => { if (!_combat.Target && info.source) _combat.SetTarget(info.source.GetComponent<Health>()); };
        }

        void Update()
        {
            if (animator) animator.SetFloat(SpeedParam, _agent.velocity.magnitude, 0.1f, Time.deltaTime);
            _agent.isStopped = _state == State.Dead || _health.IsStunned || _combat.IsBusy;
            if (Time.time < _nextThink || _state == State.Dead) return;
            _nextThink = Time.time + thinkInterval;
            Think();
        }

        void Think()
        {
            var target = _combat.Target;
            if (target && target.IsDead) { _combat.ClearTarget(); target = null; }
            if (!target) target = ScanForPlayer();

            if (Vector3.Distance(transform.position, _home) > leashRadius) { _combat.ClearTarget(); _state = State.Return; }

            switch (_state)
            {
                case State.Return:
                    _agent.SetDestination(_home);
                    if (Vector3.Distance(transform.position, _home) < 1.5f) { _health.Heal(_health.MaxHp); _state = State.Idle; }
                    return;
                default:
                    if (!target) { Patrol(); return; }
                    _combat.SetTarget(target);
                    float d = Vector3.Distance(transform.position, target.transform.position);
                    if (d > attackRange) { _state = State.Chase; _agent.SetDestination(target.transform.position); }
                    else
                    {
                        _state = State.Attack;
                        _agent.ResetPath();
                        // Basic attacks are automatic; sprinkle abilities in rotation.
                        var slot = abilityRotation.Length > 0 ? abilityRotation[_rotationIndex % abilityRotation.Length] : AbilitySlot.Q;
                        if (_combat.RequestCast(slot, target.transform.position, target)) _rotationIndex++;
                    }
                    return;
            }
        }

        Health ScanForPlayer()
        {
            int n = Physics.OverlapSphereNonAlloc(transform.position, aggroRadius, Buffer, playerMask);
            for (int i = 0; i < n; i++)
            {
                var h = Buffer[i].GetComponentInParent<Health>();
                if (h && !h.IsDead && h.Team != _health.Team) return h;
            }
            return null;
        }

        void Patrol()
        {
            _state = State.Patrol;
            if (Time.time < _nextPatrol) return;
            _nextPatrol = Time.time + Random.Range(3f, 7f);
            var p = _home + Random.insideUnitSphere * patrolRadius;
            if (NavMesh.SamplePosition(p, out var hit, 2f, NavMesh.AllAreas)) _agent.SetDestination(hit.position);
        }
    }
}
