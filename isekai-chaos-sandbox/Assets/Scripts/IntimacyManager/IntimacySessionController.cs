using System;
using System.Collections.Generic;
using System.Linq;
using IsekaiChaos.Core;
using IsekaiChaos.Customizer;
using UnityEngine;

namespace IsekaiChaos.Intimacy
{
    public enum SceneCameraMode { FreeCam = 0, FirstPerson = 1, Focus = 2 }

    /// <summary>
    /// Runs one interactive scene: holds participants, current pose preset, intensity,
    /// camera mode and per-participant clothing level. The touch UI (IntimacyMenuView) only
    /// talks to this class. Content itself lives in animation clips / data assets — this is
    /// pure state + orchestration.
    /// </summary>
    public class IntimacySessionController : MonoBehaviour
    {
        static readonly int IntensityParam = Animator.StringToHash("Intensity");

        [SerializeField] LayeredWardrobeController playerWardrobe;
        [SerializeField] Animator playerAnimator;
        [SerializeField] Transform sceneAnchor;
        [Range(1, 4)] [SerializeField] int maxPartners = 3;
        [SerializeField] float consentCheckInterval = 1f;

        public bool IsActive { get; private set; }
        public ScenePreset Scene { get; private set; }
        public string CurrentPose { get; private set; }
        public float Intensity { get; private set; } = 0.5f;
        public SceneCameraMode CameraMode { get; private set; } = SceneCameraMode.FreeCam;
        public IReadOnlyList<NPCRomanceController> Partners => _partners;

        public event Action Started;
        public event Action<string> Ended;          // reason
        public event Action StateChanged;

        readonly List<NPCRomanceController> _partners = new List<NPCRomanceController>();
        float _nextConsentCheck;

        // ---------------------------------------------------------------------------
        /// <summary>
        /// Attempts to start a scene. All partners must independently consent; the result
        /// list tells the UI who declined and why.
        /// </summary>
        public bool TryStart(ScenePreset scene, IList<NPCRomanceController> partners, string facilityId,
                             out List<(NPCRomanceController npc, ConsentResult result)> results)
        {
            results = new List<(NPCRomanceController, ConsentResult)>();
            if (IsActive || scene == null || partners == null || partners.Count == 0 || partners.Count > maxPartners)
                return false;

            foreach (var p in partners)
            {
                var r = p.EvaluateConsent(scene, facilityId);
                // Group scenes additionally require each partner to have unlocked the group milestone.
                if (r == ConsentResult.Accepted && partners.Count > 1 && !p.HasMilestone("group_ok"))
                    r = ConsentResult.Declined;
                results.Add((p, r));
            }
            if (results.Any(x => x.result != ConsentResult.Accepted)) return false;

            Scene = scene;
            _partners.Clear();
            _partners.AddRange(partners);
            IsActive = true;
            Intensity = 0.5f;
            SetPose(scene.poseIds.FirstOrDefault());
            _nextConsentCheck = Time.unscaledTime + consentCheckInterval;
            Started?.Invoke();
            return true;
        }

        public void End(string reason = "player")
        {
            if (!IsActive) return;
            IsActive = false;
            playerWardrobe?.ShowAllLayers();
            foreach (var p in _partners) p.Wardrobe?.ShowAllLayers();
            foreach (var p in _partners) p.Modify(RelationshipStat.Mood, reason == "player" ? 5 : -5);
            _partners.Clear();
            Scene = null;
            Ended?.Invoke(reason);
        }

        void Update()
        {
            if (!IsActive || Time.unscaledTime < _nextConsentCheck) return;
            _nextConsentCheck = Time.unscaledTime + consentCheckInterval;

            if (!ContentSettings.MatureContentEnabled) { End("content_disabled"); return; }
            var leaving = _partners.FirstOrDefault(p => !p.WantsToContinue());
            if (leaving != null) End($"partner_stopped:{leaving.Profile.npcId}");
        }

        // ---------------------------------------------------------------------------
        public bool SetPose(string poseId)
        {
            if (!IsActive || string.IsNullOrEmpty(poseId) || !Scene.poseIds.Contains(poseId)) return false;
            if (_partners.Any(p => !p.IsPoseUnlocked(poseId)) && poseId != Scene.poseIds[0]) return false;

            CurrentPose = poseId;
            playerAnimator?.CrossFadeInFixedTime(poseId, 0.35f);
            foreach (var p in _partners) p.Animator?.CrossFadeInFixedTime(poseId, 0.35f);
            StateChanged?.Invoke();
            return true;
        }

        public void SetIntensity(float value01)
        {
            Intensity = Mathf.Clamp01(value01);
            playerAnimator?.SetFloat(IntensityParam, Intensity);
            foreach (var p in _partners) p.Animator?.SetFloat(IntensityParam, Intensity);
            StateChanged?.Invoke();
        }

        public void SetCameraMode(SceneCameraMode mode)
        {
            CameraMode = mode;
            StateChanged?.Invoke();
        }

        /// <summary>Clothing slider for one participant (null = player).</summary>
        public void SetDressLevel(NPCRomanceController participant, WardrobeLayer outermostVisible)
        {
            var w = participant ? participant.Wardrobe : playerWardrobe;
            w?.SetDressLevel(outermostVisible);
            StateChanged?.Invoke();
        }
    }
}
