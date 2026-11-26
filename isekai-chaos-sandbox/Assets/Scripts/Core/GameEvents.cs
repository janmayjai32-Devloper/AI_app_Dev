using System;

namespace IsekaiChaos.Core
{
    /// <summary>
    /// Lightweight static event bus. Systems publish gameplay milestones here so that
    /// cross-cutting listeners (auto-save, analytics, achievements, UI toasts) never need
    /// hard references to the publishers.
    /// </summary>
    public static class GameEvents
    {
        // World
        public static event Action<BiomeId, BiomeId> BiomeChanged;          // (from, to)
        public static event Action<string> BossEncounterStarting;           // bossId
        public static event Action<string> BossDefeated;                    // bossId

        // Story
        public static event Action<string> QuestCompleted;                  // questId
        public static event Action<int, int> ChapterStarted;                // (act, chapter)
        public static event Action<string, int> StoryFlagChanged;           // (flag, newValue)

        // Base
        public static event Action<string, int> BuildingUpgraded;           // (buildingId, tier)

        // Economy
        public static event Action<long> GoldChanged;                       // new total

        // Relationships
        public static event Action<string, string> RelationshipMilestone;   // (npcId, milestoneId)

        public static void RaiseBiomeChanged(BiomeId from, BiomeId to) => BiomeChanged?.Invoke(from, to);
        public static void RaiseBossEncounterStarting(string bossId) => BossEncounterStarting?.Invoke(bossId);
        public static void RaiseBossDefeated(string bossId) => BossDefeated?.Invoke(bossId);
        public static void RaiseQuestCompleted(string questId) => QuestCompleted?.Invoke(questId);
        public static void RaiseChapterStarted(int act, int chapter) => ChapterStarted?.Invoke(act, chapter);
        public static void RaiseStoryFlagChanged(string flag, int value) => StoryFlagChanged?.Invoke(flag, value);
        public static void RaiseBuildingUpgraded(string buildingId, int tier) => BuildingUpgraded?.Invoke(buildingId, tier);
        public static void RaiseGoldChanged(long total) => GoldChanged?.Invoke(total);
        public static void RaiseRelationshipMilestone(string npcId, string milestoneId) => RelationshipMilestone?.Invoke(npcId, milestoneId);
    }

    /// <summary>The six zones of Gantia. Values are persisted in saves — append only, never reorder.</summary>
    public enum BiomeId
    {
        None = 0,
        StarterMeadows = 1,
        BeastkinForest = 2,
        CaravanDesert = 3,
        HotSpringsCoast = 4,
        FrostpeakMountains = 5,
        VolcanicWastes = 6,
    }
}
