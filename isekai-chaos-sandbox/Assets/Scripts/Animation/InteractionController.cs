using System;
using System.Collections;
using IsekaiChaos.Combat;
using UnityEngine;

namespace IsekaiChaos.Animation
{
    /// <summary>
    /// Plays interactive motion on the player (and partner): walk to the interactable,
    /// align precisely, play enter → loop → exit states, drive hand/look IK, fire contact
    /// events from animation, and release control. Any movement input or taking damage
    /// cancels a looping interaction with its exit animation.
    ///
    /// Animator setup: an "Interaction" layer (index 2, override, weight driven here)
    /// containing the enter/loop/exit states; IK Pass enabled on that layer.
    /// </summary>
    public class InteractionController : MonoBehaviour
    {
        const int InteractionLayer = 2;

        [SerializeField] Animator animator;
        [SerializeField] ClickToMoveMotor motor;
        [SerializeField] AnimationEventRelay relay;
        [SerializeField] float alignTime = 0.25f;
        [SerializeField] float layerBlendSpeed = 6f;

        public Interactable Current { get; private set; }
        public bool IsInteracting => Current != null;
        public event Action<Interactable> Started, Finished;

        Interactable _approaching;
        float _layerWeight, _ikWeight;
        bool _cancelRequested;
        Coroutine _routine;

        void Awake()
        {
            if (!animator) animator = GetComponentInChildren<Animator>();
            if (!motor) motor = GetComponent<ClickToMoveMotor>();
            if (!relay) relay = GetComponentInChildren<AnimationEventRelay>();
            if (relay) relay.ContactFrame += OnContact;
            var h = GetComponent<Health>();
            if (h) h.Damaged += _ => Cancel();
        }

        public void BeginApproach(Interactable target)
        {
            if (target == null || (target.singleUser && target.InUse)) return;
            if (IsInteracting) Cancel();
            _approaching = target;
            motor.MoveTo(target.ApproachPoint);
        }

        public void Cancel() => _cancelRequested = true;

        void Update()
        {
            if (_approaching && !IsInteracting)
            {
                if (Vector3.Distance(Flat(transform.position), Flat(_approaching.ApproachPoint)) < 0.35f)
                {
                    var t = _approaching; _approaching = null;
                    _routine = StartCoroutine(Run(t));
                }
            }

            float targetW = IsInteracting ? 1f : 0f;
            _layerWeight = Mathf.MoveTowards(_layerWeight, targetW, layerBlendSpeed * Time.deltaTime);
            if (animator) animator.SetLayerWeight(InteractionLayer, _layerWeight);
        }

        IEnumerator Run(Interactable it)
        {
            Current = it;
            it.InUse = true;
            _cancelRequested = false;
            motor.Stop();
            motor.Locked = true;
            Started?.Invoke(it);

            // 1) Align root to the anchor (short lerp — hides tiny pathing error).
            Vector3 p0 = transform.position; Quaternion r0 = transform.rotation;
            for (float t = 0; t < 1f; t += Time.deltaTime / alignTime)
            {
                transform.SetPositionAndRotation(Vector3.Lerp(p0, it.ApproachPoint, t), Quaternion.Slerp(r0, it.ApproachRotation, t));
                yield return null;
            }
            transform.SetPositionAndRotation(it.ApproachPoint, it.ApproachRotation);

            // 2) Enter (+ partner in sync).
            animator.applyRootMotion = true;
            animator.CrossFadeInFixedTime(it.enterState, 0.15f, InteractionLayer);
            if (it.partnerAnimator && !string.IsNullOrEmpty(it.partnerState))
            {
                if (it.partnerAnchor)
                    it.partnerAnimator.transform.root.SetPositionAndRotation(it.partnerAnchor.position, it.partnerAnchor.rotation);
                it.partnerAnimator.CrossFadeInFixedTime(it.partnerState, 0.15f);
            }
            yield return WaitForState(it.enterState);

            // 3) Loop / one-shot.
            if (!string.IsNullOrEmpty(it.loopState)) animator.CrossFadeInFixedTime(it.loopState, 0.2f, InteractionLayer);
            float end = it.duration > 0 ? Time.time + it.duration : float.MaxValue;
            while (!_cancelRequested && Time.time < end) yield return null;

            // 4) Exit.
            if (!string.IsNullOrEmpty(it.exitState))
            {
                animator.CrossFadeInFixedTime(it.exitState, 0.15f, InteractionLayer);
                yield return WaitForState(it.exitState);
            }

            animator.applyRootMotion = false;
            it.InUse = false;
            it.onComplete?.Invoke(gameObject);
            Current = null;
            motor.Locked = false;
            Finished?.Invoke(it);
        }

        IEnumerator WaitForState(string state)
        {
            // wait for the crossfade to land, then for the clip to finish
            float timeout = Time.time + 5f;
            while (Time.time < timeout && !animator.GetCurrentAnimatorStateInfo(InteractionLayer).IsName(state)) yield return null;
            while (Time.time < timeout && animator.GetCurrentAnimatorStateInfo(InteractionLayer).IsName(state)
                   && animator.GetCurrentAnimatorStateInfo(InteractionLayer).normalizedTime < 0.98f) yield return null;
        }

        void OnContact(string id)
        {
            if (Current) Current.onContact?.Invoke(gameObject);
        }

        /// <summary>Hand + look IK toward interactable targets (chest lid, door handle, partner's hand).</summary>
        /// <remarks>Unity sends OnAnimatorIK to the Animator's GameObject; AnimationEventRelay forwards it.</remarks>
        public void ApplyIK(int layerIndex)
        {
            var ik = Current ? Current.ik : null;
            _ikWeight = Mathf.MoveTowards(_ikWeight, ik != null ? ik.weight : 0f, Time.deltaTime * 4f);
            if (ik == null && _ikWeight <= 0f) return;

            SetHand(AvatarIKGoal.LeftHand, ik?.leftHand);
            SetHand(AvatarIKGoal.RightHand, ik?.rightHand);
            if (ik?.lookAt) { animator.SetLookAtWeight(_ikWeight, 0.2f, 0.8f, 1f, 0.5f); animator.SetLookAtPosition(ik.lookAt.position); }
            else animator.SetLookAtWeight(0);
        }

        void SetHand(AvatarIKGoal goal, Transform t)
        {
            float w = t ? _ikWeight : 0f;
            animator.SetIKPositionWeight(goal, w);
            animator.SetIKRotationWeight(goal, w);
            if (t) { animator.SetIKPosition(goal, t.position); animator.SetIKRotation(goal, t.rotation); }
        }

        static Vector3 Flat(Vector3 v) => new Vector3(v.x, 0, v.z);
    }
}
