using IsekaiChaos.Combat;
using UnityEngine;

namespace IsekaiChaos.Animation
{
    /// <summary>
    /// Genshin/NTE-style free orbit camera:
    ///  * Orbit with right-half-screen drag (mobile) or mouse (PC, cursor locked)
    ///  * Pinch / wheel zoom, from shoulder close-up to wide exploration
    ///  * Sphere-cast collision so walls and trees never block the view, with a smooth return
    ///  * Gentle auto-recenter behind the player while running (can be turned off)
    ///  * Combat framing: biases toward the soft-locked target
    ///  * Shake from CombatController hits
    /// </summary>
    public class ThirdPersonCameraRig : MonoBehaviour
    {
        [SerializeField] Transform target;
        [SerializeField] Camera cam;
        [SerializeField] Vector3 pivotOffset = new Vector3(0, 1.55f, 0);
        [SerializeField] float shoulderOffset = 0.35f;

        [Header("Orbit")]
        [SerializeField] float mouseSensitivity = 3f;
        [SerializeField] float touchSensitivity = 0.18f;
        [SerializeField] float minPitch = -30f, maxPitch = 70f;
        [SerializeField] float yaw = 0f, pitch = 15f;

        [Header("Zoom")]
        [SerializeField] float minDistance = 1.8f, maxDistance = 9f;
        [SerializeField] float distance = 5f;

        [Header("Collision")]
        [SerializeField] LayerMask collisionMask = ~0;
        [SerializeField] float collisionRadius = 0.25f;
        [SerializeField] float returnSpeed = 6f;

        [Header("Assist")]
        [SerializeField] bool autoRecenter = true;
        [SerializeField] float recenterDelay = 1.5f, recenterSpeed = 60f;
        [SerializeField] float combatFramingWeight = 0.25f;

        public Camera Camera => cam;
        public bool InputEnabled { get; set; } = true;

        CombatController _combat;
        ClickToMoveMotor _motor;
        float _currentDistance;
        float _lastManualInput;
        float _shake;
        int _orbitFingerId = -1;
        Vector2 _lastFingerPos;
        float _prevPinch = -1f;

        void Awake()
        {
            if (!cam) cam = GetComponentInChildren<Camera>();
            _currentDistance = distance;
        }

        void OnShake(float s) => _shake = Mathf.Max(_shake, s);

        public void SetTarget(Transform t)
        {
            if (_combat) _combat.CameraShakeRequested -= OnShake;
            target = t;
            _combat = t ? t.GetComponent<CombatController>() : null;
            _motor = t ? t.GetComponent<ClickToMoveMotor>() : null;
            if (_combat) _combat.CameraShakeRequested += OnShake;
            if (_motor) _motor.SetCamera(cam ? cam.transform : transform);
            if (t) yaw = t.eulerAngles.y;
        }

        void Start() { if (target) SetTarget(target); }

        void LateUpdate()
        {
            if (!target) return;
            if (InputEnabled) ReadInput();
            AutoRecenter();

            var rot = Quaternion.Euler(pitch, yaw, 0);
            Vector3 pivot = target.position + pivotOffset + rot * Vector3.right * shoulderOffset;

            // Bias the view toward the locked enemy so both stay in frame.
            if (_combat && _combat.Target)
                pivot = Vector3.Lerp(pivot, _combat.Target.transform.position + Vector3.up, combatFramingWeight * 0.5f);

            // Collision: pull in quickly, ease back out.
            float wanted = distance;
            if (Physics.SphereCast(pivot, collisionRadius, -(rot * Vector3.forward), out var hit, distance, collisionMask, QueryTriggerInteraction.Ignore)
                && !hit.transform.IsChildOf(target))
                wanted = Mathf.Max(minDistance * 0.5f, hit.distance - 0.05f);
            _currentDistance = wanted < _currentDistance ? wanted : Mathf.MoveTowards(_currentDistance, wanted, returnSpeed * Time.deltaTime);

            Vector3 pos = pivot - rot * Vector3.forward * _currentDistance;
            if (_shake > 0)
            {
                pos += Random.insideUnitSphere * _shake * 0.15f;
                _shake = Mathf.MoveTowards(_shake, 0, Time.unscaledDeltaTime * 2.5f);
            }
            transform.SetPositionAndRotation(pos, rot);
        }

        void ReadInput()
        {
            Vector2 delta = Vector2.zero;

#if UNITY_STANDALONE || UNITY_EDITOR
            if (Cursor.lockState == CursorLockMode.Locked)
                delta += new Vector2(Input.GetAxisRaw("Mouse X"), -Input.GetAxisRaw("Mouse Y")) * mouseSensitivity;
            float wheel = Input.mouseScrollDelta.y;
            if (Mathf.Abs(wheel) > 0.01f) distance = Mathf.Clamp(distance - wheel * 0.6f, minDistance, maxDistance);
#endif
            // Touch: the first finger that starts on the right half of the screen (and not on UI) orbits.
            int rightTouches = 0;
            Vector2 a = default, b = default;
            for (int i = 0; i < Input.touchCount; i++)
            {
                var t = Input.GetTouch(i);
                if (t.phase == TouchPhase.Began && _orbitFingerId < 0 && t.position.x > Screen.width * 0.45f
                    && !(UnityEngine.EventSystems.EventSystem.current?.IsPointerOverGameObject(t.fingerId) ?? false))
                { _orbitFingerId = t.fingerId; _lastFingerPos = t.position; }

                if (t.fingerId == _orbitFingerId)
                {
                    if (t.phase == TouchPhase.Moved) { delta += (t.position - _lastFingerPos) * touchSensitivity * new Vector2(1, -1); _lastFingerPos = t.position; }
                    if (t.phase == TouchPhase.Ended || t.phase == TouchPhase.Canceled) _orbitFingerId = -1;
                }
                if (t.position.x > Screen.width * 0.45f) { if (rightTouches == 0) a = t.position; else b = t.position; rightTouches++; }
            }
            // Two fingers on the right half = pinch zoom.
            if (rightTouches == 2)
            {
                float d = Vector2.Distance(a, b);
                if (_prevPinch > 0) distance = Mathf.Clamp(distance - (d - _prevPinch) * 0.01f, minDistance, maxDistance);
                _prevPinch = d;
                delta = Vector2.zero;
            }
            else _prevPinch = -1f;

            if (delta.sqrMagnitude > 0.0001f)
            {
                yaw += delta.x;
                pitch = Mathf.Clamp(pitch + delta.y, minPitch, maxPitch);
                _lastManualInput = Time.time;
            }
        }

        void AutoRecenter()
        {
            if (!autoRecenter || !_motor || !_motor.IsMoving || Time.time - _lastManualInput < recenterDelay) return;
            if (_combat && _combat.Target) return;
            yaw = Mathf.MoveTowardsAngle(yaw, target.eulerAngles.y, recenterSpeed * Time.deltaTime);
        }
    }
}
