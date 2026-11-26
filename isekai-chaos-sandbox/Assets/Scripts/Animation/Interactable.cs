using System;
using UnityEngine;
using UnityEngine.Events;

namespace IsekaiChaos.Animation
{
    public enum InteractionKind
    {
        Talk, Sit, Lie, OpenChest, OpenDoor, PickUp, Gather, Craft, Bathe,
        PairedEmote,        // two-character synced animation: high-five, hug, hand-hold, head-pat, ear-scratch, dance
        Mount, Climb, Push,
    }

    /// <summary>
    /// Anything the player can tap to interact with: benches, chests, doors, forges, hot
    /// springs, resource nodes, NPCs. Defines WHERE the character must stand (alignment
    /// anchor), WHICH animation plays, optional IK targets for hands/look, and the partner
    /// animation for paired interactions.
    /// </summary>
    public class Interactable : MonoBehaviour
    {
        [Serializable]
        public class IkTargets
        {
            public Transform leftHand, rightHand, lookAt;
            [Range(0, 1)] public float weight = 1f;
        }

        public string interactionId = "bench_sit";
        public InteractionKind kind = InteractionKind.Sit;
        public string prompt = "Sit";

        [Header("Alignment")]
        [Tooltip("Where the character's root must be (position + facing) before the animation starts.")]
        public Transform anchor;
        public float approachDistance = 1.2f;
        [Tooltip("Blend the last few cm into place with MatchTarget instead of snapping.")]
        public bool matchTarget = true;

        [Header("Animation")]
        public string enterState = "Sit_Enter";
        public string loopState = "Sit_Loop";
        public string exitState = "Sit_Exit";
        [Tooltip("0 = loop until cancelled (sit, bathe); >0 = one-shot length in seconds.")]
        public float duration = 0f;
        public IkTargets ik = new IkTargets();

        [Header("Paired (two-character) interactions")]
        public Animator partnerAnimator;           // e.g. the NPC
        public Transform partnerAnchor;
        public string partnerState;

        [Header("Gating")]
        public bool singleUser = true;
        public string requiredFlag;                // StoryFlag that must be set

        [Header("Hooks")]
        public UnityEvent<GameObject> onContact;   // fired on animation contact frame ("open lid now")
        public UnityEvent<GameObject> onComplete;

        public bool InUse { get; set; }

        public Vector3 ApproachPoint =>
            anchor ? anchor.position : transform.position + (transform.forward * approachDistance);

        public Quaternion ApproachRotation => anchor ? anchor.rotation : Quaternion.LookRotation(-transform.forward);

#if UNITY_EDITOR
        void OnDrawGizmos()
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(ApproachPoint, 0.2f);
            Gizmos.DrawRay(ApproachPoint, ApproachRotation * Vector3.forward * 0.6f);
        }
#endif
    }
}
