using IsekaiChaos.Animation;
using IsekaiChaos.Combat;
using UnityEngine;

namespace IsekaiChaos.UI
{
    public enum ControlMode { Direct = 0, TapToMove = 1 }

    /// <summary>
    /// Switches between the two control schemes (Settings > Controls):
    ///  * Direct (default): Genshin/NTE style. Joystick/WASD, free 3rd-person camera, jump,
    ///    sprint, dodge, attack button with soft lock-on.
    ///  * TapToMove: Albion style. Tap to move or target, auto-attack, isometric camera,
    ///    drag-to-aim abilities.
    /// Both drive the same motor, combat and interaction components, so gameplay and saves are identical.
    /// </summary>
    public class ControlModeManager : MonoBehaviour
    {
        const string PrefKey = "controls.mode";

        [SerializeField] ClickToMoveMotor motor;
        [SerializeField] CombatController combat;

        [Header("Direct")]
        [SerializeField] DirectControlInput directInput;
        [SerializeField] ThirdPersonCameraRig thirdPersonCamera;
        [SerializeField] GameObject directHud;          // joystick, attack/jump/dodge buttons

        [Header("Tap to move")]
        [SerializeField] TouchInputController tapInput;
        [SerializeField] IsometricCameraRig isometricCamera;
        [SerializeField] GameObject tapHud;             // ability bar with drag-aim, move marker

        public ControlMode Mode { get; private set; }
        public event System.Action<ControlMode> Changed;

        void Start() => Apply((ControlMode)PlayerPrefs.GetInt(PrefKey, (int)ControlMode.Direct));

        public void SetMode(ControlMode mode)
        {
            PlayerPrefs.SetInt(PrefKey, (int)mode);
            PlayerPrefs.Save();
            Apply(mode);
        }

        public void Toggle() => SetMode(Mode == ControlMode.Direct ? ControlMode.TapToMove : ControlMode.Direct);

        void Apply(ControlMode mode)
        {
            Mode = mode;
            bool direct = mode == ControlMode.Direct;

            motor.Stop();
            motor.SetJoystick(Vector2.zero);
            motor.AutoChaseTarget = !direct;
            combat.AutoAttackEnabled = !direct;

            if (directInput) directInput.enabled = direct;
            if (thirdPersonCamera) thirdPersonCamera.gameObject.SetActive(direct);
            if (directHud) directHud.SetActive(direct);

            if (tapInput) tapInput.enabled = !direct;
            if (isometricCamera) isometricCamera.gameObject.SetActive(!direct);
            if (tapHud) tapHud.SetActive(!direct);

            var cam = direct ? (thirdPersonCamera ? thirdPersonCamera.Camera : null) : (isometricCamera ? isometricCamera.Camera : null);
            if (cam) motor.SetCamera(cam.transform);
            if (direct && thirdPersonCamera) thirdPersonCamera.SetTarget(motor.transform);

            Changed?.Invoke(mode);
        }
    }
}
