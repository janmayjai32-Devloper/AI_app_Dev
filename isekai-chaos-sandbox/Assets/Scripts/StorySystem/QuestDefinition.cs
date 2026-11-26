using System;
using System.Collections.Generic;
using IsekaiChaos.Core;
using UnityEngine;

namespace IsekaiChaos.StorySystem
{
    public enum QuestCategory { MainStory = 0, CompanionArc = 1, GuildBounty = 2, TradeContract = 3 }
    public enum QuestStatus { Locked = 0, Available = 1, Active = 2, Completed = 3, Failed = 4 }
    public enum FlagOp { Set = 0, Add = 1 }
    public enum Compare { Equal, NotEqual, GreaterOrEqual, LessOrEqual }

    [Serializable]
    public class FlagCondition
    {
        public string flag;
        public Compare op = Compare.GreaterOrEqual;
        public int value = 1;

        public bool Evaluate(int current) => op switch
        {
            Compare.Equal => current == value,
            Compare.NotEqual => current != value,
            Compare.GreaterOrEqual => current >= value,
            _ => current <= value,
        };
    }

    [Serializable]
    public class FlagEffect
    {
        public string flag;
        public FlagOp op = FlagOp.Add;
        public int value = 1;
    }

    [Serializable]
    public class QuestObjective
    {
        public string counterId;          // e.g. "kill_giant_frog", "deliver_lumber", "talk_bruni"
        public int required = 1;
        public string description;
    }

    /// <summary>A branch offered at the end of a stage (dialogue choice, moral decision).</summary>
    [Serializable]
    public class QuestChoice
    {
        public string choiceId;
        public string label;
        public List<FlagCondition> requires = new List<FlagCondition>();
        public List<FlagEffect> effects = new List<FlagEffect>();
        [Tooltip("Stage index to jump to; -1 = complete quest.")]
        public int nextStage = -1;
        public long goldDelta;
        public string npcId;              // optional relationship impact
        public float affinityDelta;
        public float trustDelta;
    }

    [Serializable]
    public class QuestStage
    {
        public string stageId;
        [TextArea] public string journalText;
        public List<QuestObjective> objectives = new List<QuestObjective>();
        public List<QuestChoice> choices = new List<QuestChoice>();   // empty = linear
        public BiomeId waypointBiome;
        public Vector3 waypointPosition;
        public List<FlagEffect> onEnterEffects = new List<FlagEffect>();
    }

    [CreateAssetMenu(menuName = "IsekaiChaos/Quest", fileName = "Quest")]
    public class QuestDefinition : ScriptableObject
    {
        public string questId;
        public string title;
        public QuestCategory category;
        [Tooltip("Main story: act/chapter this quest belongs to.")]
        public int act = 1, chapter = 1;
        [Tooltip("Companion arcs: the NPC whose arc this is.")]
        public string companionNpcId;
        [TextArea] public string chapterRecap;

        public List<FlagCondition> unlockConditions = new List<FlagCondition>();
        public List<string> prerequisiteQuests = new List<string>();
        public List<QuestStage> stages = new List<QuestStage>();

        [Header("Rewards on completion")]
        public long goldReward;
        public long xpReward;
        public List<FlagEffect> completionEffects = new List<FlagEffect>();
        public string unlockMilestoneForNpc;           // e.g. companion arc stage 3 → romance milestone
        public bool advancesChapter;
        public bool isBossQuest;                        // triggers pre-fight auto-save on final stage
        public string bossId;
    }
}
