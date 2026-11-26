using IsekaiChaos.Combat;
using UnityEngine;

namespace IsekaiChaos.Animation
{
    /// <summary>
    /// Albion-style camera: fixed high-angle 3/4 view following the player, pinch / scroll
    /// zoom, optional two-finger rotate, hit-stop-safe screen shake. Zoom changes pitch so
    /// close-ups (towns, dialogue) feel cinematic while far zoom reads combat clearly.
    /// </summary>
    public class IsometricCameraRig : MonoBehaviour
    {
        [SerializeField] Transform target;
        [SerializeField] Camera cam;
        [SerializeField] float minDistance = 7f, maxDistance = 22f;
        [SerializeField] float minPitch = 35f, maxPitch = 55f;
        [SerializeField] float yaw = 45f;
        [SerializeField] bool allowRotate = true;
        [SerializeField] float followSharpness = 12f;
        [SerializeField] float zoomSensitivity = 0.02f;
        [SerializeField, Range(0, 1)] float zoom = 0.5f;

        Vector3 _focus;
        float _shake;
        float _prevPinch = -1f, _prevAngle;

        public Camera Camera => cam;

        void Awake()
        {
            if (!cam) cam = GetComponentInChildren<Camera>();
            if (target) _focus = target.position;
        }

        public void SetTarget(Transform t) { target = t; if (t) _focus = t.position; }

        public void BindCombat(CombatController combat)
        {
            combat.CameraShakeRequested += s => _shake = Mathf.Max(_shake, s);
        }

        void LateUpdate()
        {
            HandleZoomAndRotate();
            if (target) _focus = Vector3.Lerp(_focus, target.position, 1f - Mathf.Exp(-followSharpness * Time.deltaTime));

            float dist = Mathf.Lerp(minDistance, maxDistance, zoom);
            float pitch = Mathf.Lerp(minPitch, maxPitch, zoom);
            var rot = Quaternion.Euler(pitch, yaw, 0);
            Vector3 pos = _focus + Vector3.up * 1.2f - rot * Vector3.forward * dist;

            if (_shake > 0)
            {
                pos += Random.insideUnitSphere * _shake * 0.3f;
                _shake = Mathf.MoveTowards(_shake, 0, Time.unscaledDeltaTime * 2f);
            }
            transform.SetPositionAndRotation(pos, rot);
        }

        void HandleZoomAndRotate()
        {
            float scroll = Input.mouseScrollDelta.y;
            if (Mathf.Abs(scroll) > 0.01f) zoom = Mathf.Clamp01(zoom - scroll * 0.08f);

            if (Input.touchCount == 2)
            {
                var a = Input.GetTouch(0); var b = Input.GetTouch(1);
                float d = Vector2.Distance(a.position, b.position);
                float ang = Mathf.Atan2(b.position.y - a.position.y, b.position.x - a.position.x) * Mathf.Rad2Deg;
                if (_prevPinch > 0)
                {
                    zoom = Mathf.Clamp01(zoom - (d - _prevPinch) * zoomSensitivity * 0.1f);
                    if (allowRotate) yaw += Mathf.DeltaAngle(_prevAngle, ang);
                }
                _prevPinch = d; _prevAngle = ang;
            }
            else _prevPinch = -1f;
        }
    }
}
