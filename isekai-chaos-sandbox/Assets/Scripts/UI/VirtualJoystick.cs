using UnityEngine;
using UnityEngine.EventSystems;

namespace IsekaiChaos.UI
{
    /// <summary>
    /// Floating joystick for the left half of the screen. Put it on a transparent full-height
    /// UI panel covering the left 45% of the screen; the knob appears where the thumb lands.
    /// Pushing past 90% of the radius = run; a light push = walk (analog speed).
    /// </summary>
    public class VirtualJoystick : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
    {
        [SerializeField] RectTransform background;
        [SerializeField] RectTransform knob;
        [SerializeField] float radius = 110f;
        [SerializeField] float deadZone = 0.08f;
        [SerializeField] CanvasGroup visuals;

        public Vector2 Value { get; private set; }

        Vector2 _origin;
        RectTransform _area;

        void Awake()
        {
            _area = (RectTransform)transform;
            if (visuals) visuals.alpha = 0.35f;
        }

        public void OnPointerDown(PointerEventData e)
        {
            RectTransformUtility.ScreenPointToLocalPointInRectangle(_area, e.position, e.pressEventCamera, out _origin);
            background.anchoredPosition = _origin;
            knob.anchoredPosition = _origin;
            if (visuals) visuals.alpha = 1f;
            OnDrag(e);
        }

        public void OnDrag(PointerEventData e)
        {
            RectTransformUtility.ScreenPointToLocalPointInRectangle(_area, e.position, e.pressEventCamera, out var p);
            Vector2 d = Vector2.ClampMagnitude(p - _origin, radius);
            knob.anchoredPosition = _origin + d;
            Vector2 v = d / radius;
            Value = v.magnitude < deadZone ? Vector2.zero : v;
        }

        public void OnPointerUp(PointerEventData e)
        {
            Value = Vector2.zero;
            knob.anchoredPosition = background.anchoredPosition;
            if (visuals) visuals.alpha = 0.35f;
        }
    }
}
