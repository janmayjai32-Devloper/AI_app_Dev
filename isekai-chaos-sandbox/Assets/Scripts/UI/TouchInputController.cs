using IsekaiChaos.Animation;
using IsekaiChaos.Combat;
using UnityEngine;
using UnityEngine.EventSystems;

namespace IsekaiChaos.UI
{
    /// <summary>
    /// Albion-style input for touch AND mouse (PC build uses the same scheme):
    ///   * Tap ground          → move there (hold = keep following finger)
    ///   * Tap enemy           → select target (auto-attack + chase)
    ///   * Tap NPC/object      → walk over and interact (InteractionController)
    ///   * Ability buttons     → tap = smart-cast at current target / facing;
    ///                           drag from button = aim indicator, release to cast at point
    ///   * PC: Q W E R D F keys cast at the mouse cursor; right-click moves; Esc clears target
    /// </summary>
    public class TouchInputController : MonoBehaviour
    {
        [SerializeField] IsometricCameraRig cameraRig;
        [SerializeField] ClickToMoveMotor motor;
        [SerializeField] CombatController combat;
        [SerializeField] InteractionController interaction;
        [SerializeField] LayerMask groundMask, unitMask, interactableMask;
        [SerializeField] GameObject moveMarkerPrefab;
        [SerializeField] AimIndicator aimIndicator;

        GameObject _marker;
        bool _holdingMove;

        static readonly KeyCode[] SlotKeys = { KeyCode.Q, KeyCode.W, KeyCode.E, KeyCode.R, KeyCode.D, KeyCode.F };

        void Update()
        {
            HandlePointer();
#if UNITY_STANDALONE || UNITY_EDITOR
            for (int i = 0; i < SlotKeys.Length; i++)
                if (Input.GetKeyDown(SlotKeys[i]) && RaycastGround(Input.mousePosition, out var p))
                    combat.RequestCast((AbilitySlot)i, p, UnitUnder(Input.mousePosition));
            if (Input.GetKeyDown(KeyCode.Escape)) combat.ClearTarget();
#endif
        }

        void HandlePointer()
        {
            // Any new tap cancels a looping interaction (stand up from the bench, leave the bath...).
            if (interaction && interaction.IsInteracting && (Input.GetMouseButtonDown(0) || Input.GetMouseButtonDown(1)))
                interaction.Cancel();

            bool down, held;
            Vector2 pos;
            if (Input.touchCount == 1)
            {
                var t = Input.GetTouch(0);
                if (EventSystem.current && EventSystem.current.IsPointerOverGameObject(t.fingerId)) { _holdingMove = false; return; }
                down = t.phase == TouchPhase.Began;
                held = t.phase == TouchPhase.Moved || t.phase == TouchPhase.Stationary;
                pos = t.position;
            }
            else if (Input.touchCount == 0)
            {
                if (EventSystem.current && EventSystem.current.IsPointerOverGameObject()) return;
                int btn = Application.isMobilePlatform ? 0 : 1;   // PC: right-click to move, like Albion
                down = Input.GetMouseButtonDown(btn) || Input.GetMouseButtonDown(0);
                held = Input.GetMouseButton(btn);
                pos = Input.mousePosition;
            }
            else { _holdingMove = false; return; }                // 2 fingers = camera

            if (down)
            {
                var unit = UnitUnder(pos);
                if (unit && unit.Team != combat.Health.Team) { combat.SetTarget(unit); _holdingMove = false; return; }

                var interactable = InteractableUnder(pos);
                if (interactable) { combat.ClearTarget(); interaction.BeginApproach(interactable); _holdingMove = false; return; }

                if (RaycastGround(pos, out var p)) { combat.ClearTarget(); motor.MoveTo(p); ShowMarker(p); _holdingMove = true; }
            }
            else if (held && _holdingMove && RaycastGround(pos, out var p2)) motor.MoveTo(p2);
            else _holdingMove = false;
        }

        // ---------------------------------------------------------------- ability buttons
        /// <summary>Ability button: tap.</summary>
        public void OnAbilityTap(int slot)
        {
            var t = combat.Target;
            Vector3 p = t ? t.transform.position : combat.transform.position + combat.transform.forward * 4f;
            combat.RequestCast((AbilitySlot)slot, p, t);
        }

        /// <summary>Ability button: drag-aim. Screen delta from button maps to a ground offset.</summary>
        public void OnAbilityDrag(int slot, Vector2 dragFromButton)
        {
            var a = combat.GetAbility((AbilitySlot)slot);
            if (!a || !aimIndicator) return;
            aimIndicator.Show(a, combat.transform.position, AimPoint(a, dragFromButton));
        }

        public void OnAbilityRelease(int slot, Vector2 dragFromButton, bool cancelled)
        {
            if (aimIndicator) aimIndicator.Hide();
            if (cancelled) return;
            var a = combat.GetAbility((AbilitySlot)slot);
            if (!a) return;
            combat.RequestCast((AbilitySlot)slot, AimPoint(a, dragFromButton), combat.Target);
        }

        Vector3 AimPoint(AbilityDefinition a, Vector2 drag)
        {
            var camT = cameraRig.Camera.transform;
            Vector3 f = Vector3.ProjectOnPlane(camT.forward, Vector3.up).normalized;
            Vector3 r = Vector3.Cross(Vector3.up, f);
            Vector2 n = Vector2.ClampMagnitude(drag / 150f, 1f);              // 150 px = full range
            return combat.transform.position + (f * n.y + r * n.x) * a.range;
        }

        // ---------------------------------------------------------------- raycasts
        Ray RayAt(Vector2 screen) => cameraRig.Camera.ScreenPointToRay(screen);

        bool RaycastGround(Vector2 screen, out Vector3 point)
        {
            if (Physics.Raycast(RayAt(screen), out var hit, 200f, groundMask)) { point = hit.point; return true; }
            point = default; return false;
        }

        Health UnitUnder(Vector2 screen) =>
            Physics.SphereCast(RayAt(screen), 0.4f, out var hit, 200f, unitMask) ? hit.collider.GetComponentInParent<Health>() : null;

        Interactable InteractableUnder(Vector2 screen) =>
            Physics.SphereCast(RayAt(screen), 0.4f, out var hit, 200f, interactableMask) ? hit.collider.GetComponentInParent<Interactable>() : null;

        void ShowMarker(Vector3 p)
        {
            if (!moveMarkerPrefab) return;
            if (!_marker) _marker = Instantiate(moveMarkerPrefab);
            _marker.transform.position = p + Vector3.up * 0.05f;
            _marker.SetActive(false); _marker.SetActive(true);              // restart its pulse animation
        }
    }
}
