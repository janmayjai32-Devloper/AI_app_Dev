using IsekaiChaos.Combat;
using IsekaiChaos.World;
using UnityEngine;
using UnityEngine.AI;

namespace IsekaiChaos.Animation
{
    /// <summary>
    /// Albion-style movement, offline:
    ///  * Tap/click ground → path there (NavMesh). Hold to keep walking toward the finger.
    ///  * Tap an enemy → target it and walk into attack range (auto-attack takes over).
    ///  * Optional floating joystick overrides pathing (direct steering).
    /// Drives a CharacterController via the agent's desired velocity (agent doesn't move the
    /// transform itself) so root-motion attacks, knockback and dashes all share one mover.
    ///
    /// Animator params: Speed (m/s, drives an Idle/Walk/Run blend tree), MoveX/MoveY (local
    /// velocity for strafe blends while attacking), Grounded.
    /// </summary>
    [RequireComponent(typeof(CharacterController), typeof(NavMeshAgent))]
    public class ClickToMoveMotor : MonoBehaviour
    {
        static readonly int SpeedParam = Animator.StringToHash("Speed");
        static readonly int MoveXParam = Animator.StringToHash("MoveX");
        static readonly int MoveYParam = Animator.StringToHash("MoveY");
        static readonly int GroundedParam = Animator.StringToHash("Grounded");

        [SerializeField] Animator animator;
        [SerializeField] float walkSpeed = 2.2f;
        [SerializeField] float runSpeed = 5.5f;
        [SerializeField] float acceleration = 20f;
        [SerializeField] float turnSpeed = 900f;
        [SerializeField] float gravity = -20f;
        [SerializeField] float stopDistance = 0.15f;

        CharacterController _cc;
        NavMeshAgent _agent;
        CombatController _combat;
        Health _health;
        RoadAwareSpeed _road;
        Vector3 _velocity;
        float _verticalVel;
        Vector2 _joystick;
        Transform _cameraYaw;
        bool _walkMode;

        public bool IsMoving => _velocity.sqrMagnitude > 0.05f;
        /// <summary>Set by InteractionController while a scripted motion owns the body.</summary>
        public bool Locked { get; set; }

        void Awake()
        {
            _cc = GetComponent<CharacterController>();
            _agent = GetComponent<NavMeshAgent>();
            _combat = GetComponent<CombatController>();
            _health = GetComponent<Health>();
            _road = GetComponent<RoadAwareSpeed>();
            if (!animator) animator = GetComponentInChildren<Animator>();
            _agent.updatePosition = false;
            _agent.updateRotation = false;
            _agent.speed = runSpeed;
            _cameraYaw = Camera.main ? Camera.main.transform : null;
        }

        // ------------------------------------------------------------- commands
        public void MoveTo(Vector3 point)
        {
            if (NavMesh.SamplePosition(point, out var hit, 2f, NavMesh.AllAreas))
            {
                _agent.nextPosition = transform.position;
                _agent.SetDestination(hit.position);
            }
        }

        public void Stop() { if (_agent.isOnNavMesh) _agent.ResetPath(); }
        public void SetJoystick(Vector2 v) { _joystick = Vector2.ClampMagnitude(v, 1f); if (v.sqrMagnitude > 0.01f) Stop(); }
        public void ToggleWalk() => _walkMode = !_walkMode;

        // ------------------------------------------------------------- update
        void Update()
        {
            bool canMove = !Locked && (_health == null || (!_health.IsDead && !_health.IsStunned))
                           && (_combat == null || !_combat.IsBusy);

            Vector3 desired = Vector3.zero;
            if (canMove)
            {
                if (_joystick.sqrMagnitude > 0.01f) desired = JoystickWorld() * (_walkMode ? walkSpeed : runSpeed) * _joystick.magnitude;
                else if (_agent.hasPath && _agent.remainingDistance > stopDistance)
                    desired = Vector3.ClampMagnitude(_agent.desiredVelocity, _walkMode ? walkSpeed : runSpeed);

                // Chase the combat target into basic-attack range.
                if (_combat && _combat.Target && desired == Vector3.zero)
                {
                    float d = Vector3.Distance(transform.position, _combat.Target.transform.position);
                    if (d > 2f) MoveTo(_combat.Target.transform.position);
                }
            }

            if (_road) desired *= _road.Multiplier;     // roads are faster
            _velocity = Vector3.MoveTowards(_velocity, desired, acceleration * Time.deltaTime);
            _verticalVel = _cc.isGrounded ? -2f : _verticalVel + gravity * Time.deltaTime;

            bool rootMotion = _combat && _combat.WantsRootMotion;
            if (!rootMotion) _cc.Move((_velocity + Vector3.up * _verticalVel) * Time.deltaTime);
            else _cc.Move(Vector3.up * _verticalVel * Time.deltaTime);

            if (_velocity.sqrMagnitude > 0.05f && !(_combat && _combat.IsBusy))
                transform.rotation = Quaternion.RotateTowards(transform.rotation,
                    Quaternion.LookRotation(new Vector3(_velocity.x, 0, _velocity.z)), turnSpeed * Time.deltaTime);

            _agent.nextPosition = transform.position;
            Animate();
        }

        Vector3 JoystickWorld()
        {
            Vector3 f = _cameraYaw ? Vector3.ProjectOnPlane(_cameraYaw.forward, Vector3.up).normalized : Vector3.forward;
            Vector3 r = Vector3.Cross(Vector3.up, f);
            return (f * _joystick.y + r * _joystick.x).normalized;
        }

        void Animate()
        {
            if (!animator) return;
            Vector3 local = transform.InverseTransformDirection(_velocity);
            animator.SetFloat(SpeedParam, _velocity.magnitude, 0.08f, Time.deltaTime);
            animator.SetFloat(MoveXParam, local.x, 0.1f, Time.deltaTime);
            animator.SetFloat(MoveYParam, local.z, 0.1f, Time.deltaTime);
            animator.SetBool(GroundedParam, _cc.isGrounded);
        }

        /// <summary>
        /// Root motion from attack/interaction clips is applied through the CharacterController.
        /// Unity only sends OnAnimatorMove to the Animator's own GameObject, so when the model is
        /// a child, AnimationEventRelay forwards it here.
        /// </summary>
        void OnAnimatorMove() => ApplyRootMotion();

        public void ApplyRootMotion()
        {
            if (!animator || !animator.applyRootMotion) return;
            Vector3 delta = animator.deltaPosition; delta.y = 0;
            _cc.Move(delta);
            transform.rotation *= animator.deltaRotation;
        }
    }
}
