using System;
using UnityEngine;

namespace IsekaiChaos.Animation
{
    /// <summary>
    /// Lives on the model GameObject (the one with the Animator). Forwards root motion to the
    /// motor on the parent and exposes animation events (footsteps, VFX, SFX, interaction
    /// contact frames) as C# events. Add events in clip import settings by function name.
    /// </summary>
    [RequireComponent(typeof(Animator))]
    public class AnimationEventRelay : MonoBehaviour
    {
        [SerializeField] ClickToMoveMotor motor;
        [SerializeField] InteractionController interaction;

        public event Action<string> Footstep;          // "L"/"R"
        public event Action<string> PlayVfx;           // addressable key
        public event Action<string> PlaySfx;
        public event Action<string> ContactFrame;      // interaction sync point ("grab", "sit", "hug")

        void Awake()
        {
            if (!motor) motor = GetComponentInParent<ClickToMoveMotor>();
            if (!interaction) interaction = GetComponentInParent<InteractionController>();
        }

        // If the Animator sits on the same GameObject as the motor, Unity already calls the motor's own
        // OnAnimatorMove; forwarding again would apply root motion twice.
        void OnAnimatorMove() { if (motor && motor.gameObject != gameObject) motor.ApplyRootMotion(); }
        void OnAnimatorIK(int layerIndex) { if (interaction) interaction.ApplyIK(layerIndex); }

        // Called from animation events (names must match exactly).
        public void OnFootstep(string foot) => Footstep?.Invoke(foot);
        public void OnVfx(string key) => PlayVfx?.Invoke(key);
        public void OnSfx(string key) => PlaySfx?.Invoke(key);
        public void OnContact(string id) => ContactFrame?.Invoke(id);
    }
}
