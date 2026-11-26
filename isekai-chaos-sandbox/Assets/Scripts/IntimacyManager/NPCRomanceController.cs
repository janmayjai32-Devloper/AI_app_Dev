using System;
using System.Collections.Generic;
using System.Linq;
using IsekaiChaos.Core;
using IsekaiChaos.Customizer;
using IsekaiChaos.SaveSystem;
using UnityEngine;

namespace IsekaiChaos.Intimacy
{
    public enum RelationshipStat { Affinity, Trust, Lust, Mood }

    public enum ConsentResult
    {
        Accepted,
        MatureContentDisabled,
        NotAdult,
        NotRomanceable,
        AffinityTooLow,
        TrustTooLow,
        BadMood,
        SceneLocked,
        ThemeFiltered,
        WrongLocation,
        Declined,
    }

    /// <summary>
    /// Per-NPC relationship state + intimacy entry point.
    ///
    /// Stats are 0..100:
    ///  * Affinity — how much they like you (gifts, quests, dialogue choices)
    ///  * Trust    — reliability (keeping promises, finishing their companion arc)
    ///  * Lust     — attraction; decays daily, raised by flirting/dates/charm gear
    ///  * Mood     — short-term emotional state; drives daily availability
    ///
    /// Design rule: every intimate scene is opt-in on BOTH sides. The player chooses it from
    /// the menu; the NPC agrees only when thresholds are met and their mood is good; either
    /// can end it at any time. Combat defeats and "capture" events never route here — they
    /// resolve as comedic, non-sexual setbacks (gold loss, embarrassing rescue scenes, etc).
    /// </summary>
    public class NPCRomanceController : MonoBehaviour, ISaveable
    {
        [SerializeField] NPCProfile profile;
        [SerializeField] LayeredWardrobeController wardrobe;
        [SerializeField] Animator animator;

        [Header("Daily decay")]
        [SerializeField] float lustDecayPerDay = 5f;
        [SerializeField] float moodRegressionPerDay = 10f;   // drifts toward profile.startMood

        public NPCProfile Profile => profile;
        public LayeredWardrobeController Wardrobe => wardrobe;
        public Animator Animator => animator;

        public float Affinity { get; private set; }
        public float Trust { get; private set; }
        public float Lust { get; private set; }
        public float Mood { get; private set; }

        readonly HashSet<string> _milestones = new HashSet<string>();
        readonly HashSet<string> _unlockedScenes = new HashSet<string>();
        readonly HashSet<string> _unlockedPoses = new HashSet<string>();
        long _lastGiftDay = -1;

        public event Action<RelationshipStat, float> StatChanged;

        public int RestoreOrder => 30;

        void Awake()
        {
            if (profile == null) { Debug.LogError($"[Romance] {name} has no NPCProfile."); enabled = false; return; }
            if (!profile.IsAdult) { Debug.LogError($"[Romance] {profile.npcId} is not flagged adult — romance disabled."); }
            Affinity = profile.startAffinity;
            Trust = profile.startTrust;
            Mood = profile.startMood;
        }

        void OnEnable() => SaveLoadManager.Register(this);
        void OnDisable() => SaveLoadManager.Unregister(this);

        // ----------------------------------------------------------------------------
        #region Stat changes
        public void Modify(RelationshipStat stat, float delta)
        {
            float v;
            switch (stat)
            {
                case RelationshipStat.Affinity: Affinity = v = Mathf.Clamp(Affinity + delta, 0, 100); break;
                case RelationshipStat.Trust:    Trust    = v = Mathf.Clamp(Trust + delta, 0, 100); break;
                case RelationshipStat.Lust:     Lust     = v = Mathf.Clamp(Lust + delta, 0, 100); break;
                default:                        Mood     = v = Mathf.Clamp(Mood + delta, 0, 100); break;
            }
            StatChanged?.Invoke(stat, v);
            CheckAutoMilestones();
        }

        /// <summary>Returns false if a gift was already given today.</summary>
        public bool GiveGift(string itemId, long currentDay)
        {
            if (_lastGiftDay == currentDay) return false;
            _lastGiftDay = currentDay;

            var pref = profile.gifts.FirstOrDefault(g => g.itemId == itemId);
            if (pref != null)
            {
                Modify(RelationshipStat.Affinity, pref.affinityDelta);
                Modify(RelationshipStat.Mood, pref.moodDelta);
            }
            else
            {
                Modify(RelationshipStat.Affinity, 1);   // "Oh... thanks, I guess?"
            }
            return true;
        }

        public void OnStoryMilestone(string milestoneId, float affinity = 0, float trust = 0)
        {
            UnlockMilestone(milestoneId);
            if (affinity != 0) Modify(RelationshipStat.Affinity, affinity);
            if (trust != 0) Modify(RelationshipStat.Trust, trust);
        }

        /// <summary>Called once per in-game day by the world clock.</summary>
        public void OnNewDay()
        {
            Lust = Mathf.Max(0, Lust - lustDecayPerDay);
            Mood = Mathf.MoveTowards(Mood, profile.startMood, moodRegressionPerDay);
        }

        public void UnlockMilestone(string id)
        {
            if (string.IsNullOrEmpty(id) || !_milestones.Add(id)) return;
            GameEvents.RaiseRelationshipMilestone(profile.npcId, id);

            foreach (var s in profile.scenes.Where(s => s.requiredMilestone == id))
            {
                _unlockedScenes.Add(s.sceneId);
                foreach (var p in s.poseIds) _unlockedPoses.Add(p);
            }
        }

        public bool HasMilestone(string id) => _milestones.Contains(id);

        void CheckAutoMilestones()
        {
            var t = profile.thresholds;
            if (Affinity >= t.romanceAffinity && Trust >= t.romanceTrust) UnlockMilestone("romance_open");
            if (Affinity >= t.intimacyAffinity && Trust >= t.intimacyTrust) UnlockMilestone("intimacy_open");
        }
        #endregion

        // ----------------------------------------------------------------------------
        #region Consent / scene gating
        public bool IsRomanceOpen => HasMilestone("romance_open");

        public IEnumerable<ScenePreset> AvailableScenes(string currentFacilityId) =>
            profile.scenes.Where(s => EvaluateConsent(s, currentFacilityId) == ConsentResult.Accepted);

        /// <summary>
        /// The single gate every intimate scene passes through. Deterministic given state,
        /// so UI can show the player *why* something is locked ("She's in a bad mood today").
        /// </summary>
        public ConsentResult EvaluateConsent(ScenePreset scene, string currentFacilityId)
        {
            if (!ContentSettings.MatureContentEnabled) return ConsentResult.MatureContentDisabled;
            if (!profile.IsAdult) return ConsentResult.NotAdult;
            if (!profile.romanceable) return ConsentResult.NotRomanceable;

            var t = profile.thresholds;
            if (Affinity < t.intimacyAffinity) return ConsentResult.AffinityTooLow;
            if (Trust < t.intimacyTrust) return ConsentResult.TrustTooLow;
            if (Mood < t.minimumMood) return ConsentResult.BadMood;

            if (scene != null)
            {
                if (!_unlockedScenes.Contains(scene.sceneId) && !string.IsNullOrEmpty(scene.requiredMilestone))
                    return ConsentResult.SceneLocked;
                if (!ContentSettings.AllThemesEnabled(scene.themeTags)) return ConsentResult.ThemeFiltered;
                if (!string.IsNullOrEmpty(scene.requiredFacilityId) && scene.requiredFacilityId != currentFacilityId)
                    return ConsentResult.WrongLocation;
            }
            return ConsentResult.Accepted;
        }

        /// <summary>
        /// Called by an active IntimacySession every tick. If the NPC's mood falls below the
        /// threshold (e.g. player picked something they dislike) they end the scene.
        /// </summary>
        public bool WantsToContinue() =>
            ContentSettings.MatureContentEnabled && Mood >= profile.thresholds.minimumMood;

        public bool IsPoseUnlocked(string poseId) => _unlockedPoses.Contains(poseId);
        #endregion

        // ----------------------------------------------------------------------------
        #region Save
        public void CaptureState(SaveData data)
        {
            data.relationships.RemoveAll(r => r.npcId == profile.npcId);
            data.relationships.Add(new RelationshipSaveState
            {
                npcId = profile.npcId,
                affinity = Affinity, trust = Trust, lust = Lust, mood = Mood,
                milestones = _milestones.ToList(),
                unlockedScenes = _unlockedScenes.ToList(),
                unlockedPoses = _unlockedPoses.ToList(),
                lastGiftDay = _lastGiftDay,
            });
        }

        public void RestoreState(SaveData data)
        {
            var r = data.relationships.FirstOrDefault(x => x.npcId == profile.npcId);
            if (r == null) return;
            Affinity = r.affinity; Trust = r.trust; Lust = r.lust; Mood = r.mood;
            _milestones.Clear(); _milestones.UnionWith(r.milestones);
            _unlockedScenes.Clear(); _unlockedScenes.UnionWith(r.unlockedScenes);
            _unlockedPoses.Clear(); _unlockedPoses.UnionWith(r.unlockedPoses);
            _lastGiftDay = r.lastGiftDay;
        }
        #endregion
    }
}
