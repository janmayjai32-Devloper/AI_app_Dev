using System;
using System.Collections.Generic;
using UnityEngine;

namespace IsekaiChaos.Intimacy
{
    public enum IntimacyStyle { Romantic = 0, Seductive = 1, Playful = 2, Passionate = 3 }

    [Serializable]
    public class GiftPreference
    {
        public string itemId;
        [Range(-20, 20)] public int affinityDelta = 5;
        [Range(-10, 10)] public int moodDelta = 3;
    }

    [Serializable]
    public class ScenePreset
    {
        public string sceneId;
        public IntimacyStyle style;
        [Tooltip("Pose/animation preset ids (Animator state names) available in this scene.")]
        public List<string> poseIds = new List<string>();
        [Tooltip("Content theme tags; scenes with a theme the player disabled are hidden.")]
        public List<string> themeTags = new List<string>();
        [Tooltip("Relationship milestone required to unlock.")]
        public string requiredMilestone;
        [Tooltip("Optional location (base facility id) where this scene can occur.")]
        public string requiredFacilityId;
    }

    [Serializable]
    public class RelationshipThresholds
    {
        [Range(0, 100)] public float romanceAffinity = 50;
        [Range(0, 100)] public float romanceTrust = 40;
        [Range(0, 100)] public float intimacyAffinity = 70;
        [Range(0, 100)] public float intimacyTrust = 60;
        [Range(0, 100)] public float minimumMood = 40;
    }

    /// <summary>
    /// Authoring data for a recruitable NPC: personality, "Strength + Fatal Flaw" template,
    /// gift tastes, schedule, and romance content. One asset per NPC in Assets/Data/NPCs.
    /// </summary>
    [CreateAssetMenu(menuName = "IsekaiChaos/NPC Profile", fileName = "NPCProfile")]
    public class NPCProfile : ScriptableObject
    {
        public const int MinimumAge = 18;

        [Header("Identity")]
        public string npcId;
        public string displayName;
        public string raceId;
        [Tooltip("Every romanceable character is an adult. Validated in the editor and at runtime.")]
        [Min(MinimumAge)] public int age = 21;
        [TextArea] public string bio;

        [Header("Strength + Fatal Flaw")]
        [TextArea] public string strength;
        [TextArea] public string fatalFlaw;
        public string companionArcQuestId;

        [Header("Romance")]
        public bool romanceable = true;
        public RelationshipThresholds thresholds = new RelationshipThresholds();
        public List<GiftPreference> gifts = new List<GiftPreference>();
        public List<ScenePreset> scenes = new List<ScenePreset>();

        [Header("Starting values")]
        [Range(0, 100)] public float startAffinity = 10;
        [Range(0, 100)] public float startTrust = 10;
        [Range(0, 100)] public float startMood = 60;

        public bool IsAdult => age >= MinimumAge;

#if UNITY_EDITOR
        void OnValidate()
        {
            if (age < MinimumAge)
            {
                Debug.LogError($"[NPCProfile] {name}: age must be {MinimumAge}+. Clamping.", this);
                age = MinimumAge;
            }
        }
#endif
    }
}
