using IsekaiChaos.Animation;
using IsekaiChaos.Combat;
using UnityEngine;

namespace IsekaiChaos.UI
{
    /// <summary>
    /// Direct player control in the style of Genshin Impact / NTE.
    ///
    /// PC (keyboard + mouse, cursor locked):
    ///   WASD move · Shift tap = dodge, hold = sprint · Space jump · Left click attack
    ///   1–6 abilities (Q W E R D F slots) · F interact · Ctrl walk toggle · Alt free cursor (menus)
    ///
    /// Mobile (touch):
    ///   Left half: floating joystick (VirtualJoystick)
    ///   Right half: drag to orbit the camera, pinch to zoom (ThirdPersonCameraRig)
    ///   Buttons (wire the UI Buttons to the public methods): Attack (big), Jump, Dodge/Sprint,
    ///   ability ring around Attack, Interact prompt when something is near
    /// </summary>
    public class DirectControlInput : MonoBehaviour
    {
        [SerializeField] ClickToMoveMotor motor;
        [SerializeField] CombatController combat;
        [SerializeField] InteractionController interaction;
        [SerializeField] ThirdPersonCameraRig cameraRig;
        [SerializeField] VirtualJoystick joystick;
        [SerializeField] float interactRadius = 2.2f;
        [SerializeField] LayerMask interactableMask;
        [SerializeField] float dodgeTapMaxTime = 0.2f;
        [SerializeField] AbilitySlot dodgeSlot = AbilitySlot.F;

        public Interactable NearestInteractable { get; private set; }   // HUD shows "[F] Sit" prompt

        static readonly KeyCode[] AbilityKeys = { KeyCode.Alpha1, KeyCode.Alpha2, KeyCode.Alpha3, KeyCode.Alpha4, KeyCode.Alpha5, KeyCode.Alpha6 };
        static readonly Collider[] Buffer = new Collider[12];
        float _shiftDownAt = -1f;
        bool _sprintButtonHeld;

        void OnEnable()
        {
#if UNITY_STANDALONE || UNITY_EDITOR
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
#endif
        }

        void OnDisable()
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            if (motor) { motor.SetJoystick(Vector2.zero); motor.SetSprint(false); }
        }

        void Update()
        {
            Vector2 move = joystick ? joystick.Value : Vector2.zero;

#if UNITY_STANDALONE || UNITY_EDITOR
            // Alt frees the cursor for menus, like most PC action RPGs.
            bool alt = Input.GetKey(KeyCode.LeftAlt);
            Cursor.lockState = alt ? CursorLockMode.None : CursorLockMode.Locked;
            Cursor.visible = alt;

            Vector2 keys = new Vector2((Input.GetKey(KeyCode.D) ? 1 : 0) - (Input.GetKey(KeyCode.A) ? 1 : 0),
                                       (Input.GetKey(KeyCode.W) ? 1 : 0) - (Input.GetKey(KeyCode.S) ? 1 : 0));
            if (keys.sqrMagnitude > 0) move = keys.normalized;

            if (Input.GetKeyDown(KeyCode.Space)) Jump();
            if (Input.GetKeyDown(KeyCode.LeftControl)) motor.ToggleWalk();
            if (!alt && Input.GetMouseButtonDown(0)) Attack();
            if (Input.GetKeyDown(KeyCode.F)) Interact();
            for (int i = 0; i < AbilityKeys.Length; i++) if (Input.GetKeyDown(AbilityKeys[i])) Ability(i);

            // Shift: tap = dodge, hold = sprint.
            if (Input.GetKeyDown(KeyCode.LeftShift)) _shiftDownAt = Time.time;
            bool shiftHeld = Input.GetKey(KeyCode.LeftShift) && Time.time - _shiftDownAt > dodgeTapMaxTime;
            if (Input.GetKeyUp(KeyCode.LeftShift) && Time.time - _shiftDownAt <= dodgeTapMaxTime) Dodge();
            motor.SetSprint(shiftHeld || _sprintButtonHeld);
#else
            motor.SetSprint(_sprintButtonHeld);
#endif
            // Moving cancels looping interactions (stand up from benches, leave the bath).
            if (move.sqrMagnitude > 0.04f && interaction && interaction.IsInteracting) interaction.Cancel();
            motor.SetJoystick(move);

            FindNearestInteractable();
        }

        // ----------------------------------------------------------- button hooks (mobile UI)
        public void Attack() => combat.BasicAttack(MoveDirection());
        public void Jump() => motor.Jump();
        public void Dodge() => combat.RequestCast(dodgeSlot, transform.position + MoveDirectionOrForward() * 4f);
        public void SprintButton(bool held) => _sprintButtonHeld = held;

        public void Ability(int slot)
        {
            var target = combat.Target;
            Vector3 aim = target ? target.transform.position : combat.transform.position + MoveDirectionOrForward() * 5f;
            combat.RequestCast((AbilitySlot)slot, aim, target);
        }

        public void Interact()
        {
            if (NearestInteractable && interaction) interaction.BeginApproach(NearestInteractable);
        }

        // ----------------------------------------------------------- helpers
        Vector3 MoveDirection()
        {
            var cam = cameraRig ? cameraRig.Camera.transform : Camera.main.transform;
            Vector2 j = joystick ? joystick.Value : Vector2.zero;
#if UNITY_STANDALONE || UNITY_EDITOR
            Vector2 k = new Vector2((Input.GetKey(KeyCode.D) ? 1 : 0) - (Input.GetKey(KeyCode.A) ? 1 : 0),
                                    (Input.GetKey(KeyCode.W) ? 1 : 0) - (Input.GetKey(KeyCode.S) ? 1 : 0));
            if (k.sqrMagnitude > 0) j = k.normalized;
#endif
            if (j.sqrMagnitude < 0.01f) return Vector3.zero;
            Vector3 f = Vector3.ProjectOnPlane(cam.forward, Vector3.up).normalized;
            Vector3 r = Vector3.Cross(Vector3.up, f);
            return (f * j.y + r * j.x).normalized;
        }

        Vector3 MoveDirectionOrForward()
        {
            var d = MoveDirection();
            return d.sqrMagnitude > 0.01f ? d : motor.transform.forward;
        }

        void FindNearestInteractable()
        {
            NearestInteractable = null;
            int n = Physics.OverlapSphereNonAlloc(motor.transform.position, interactRadius, Buffer, interactableMask, QueryTriggerInteraction.Collide);
            float best = float.MaxValue;
            for (int i = 0; i < n; i++)
            {
                var it = Buffer[i].GetComponentInParent<Interactable>();
                if (!it || !it.enabled || (it.singleUser && it.InUse)) continue;
                float d = (it.ApproachPoint - motor.transform.position).sqrMagnitude;
                if (d < best) { best = d; NearestInteractable = it; }
            }
        }
    }
}
