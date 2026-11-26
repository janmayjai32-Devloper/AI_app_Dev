using System;
using System.Collections.Generic;
using System.Linq;
using IsekaiChaos.Core;
using IsekaiChaos.Economy;
using IsekaiChaos.SaveSystem;
using UnityEngine;

namespace IsekaiChaos.StorySystem
{
    /// <summary>
    /// Owns acts/chapters, quest state, and the global StoryFlag dictionary.
    ///
    /// Flags are plain string → int. Alignment meters (Heroic, GreedyMerchant, ChaosGremlin,
    /// HaremLord) are just well-known flags that choices add to; other systems read them
    /// (dialogue variants, shop prices, recruitment outcomes, ending selection).
    /// </summary>
    public class StoryProgressManager : MonoBehaviour, ISaveable
    {
        public static class Flags
        {
            public const string Heroic = "align.heroic";
            public const string GreedyMerchant = "align.greedy";
            public const string ChaosGremlin = "align.chaos";
            public const string HaremLord = "align.harem";
            public const string CollateralDamage = "stat.collateral";
        }

        [Serializable]
        public class EndingRule
        {
            public string endingId;
            public int priority;                          // higher wins
            public List<FlagCondition> conditions = new List<FlagCondition>();
        }

        public static StoryProgressManager Instance { get; private set; }

        [SerializeField] List<QuestDefinition> allQuests = new List<QuestDefinition>();
        [SerializeField] List<EndingRule> endings = new List<EndingRule>();

        public int Act { get; private set; } = 1;
        public int Chapter { get; private set; } = 1;
        public string TrackedQuestId { get; set; }
        public int RestoreOrder => 10;

        class QuestRuntime
        {
            public QuestDefinition def;
            public QuestStatus status;
            public int stage;
            public Dictionary<string, int> counters = new Dictionary<string, int>();
            public string chosenBranch;
        }

        readonly Dictionary<string, int> _flags = new Dictionary<string, int>();
        readonly Dictionary<string, QuestRuntime> _quests = new Dictionary<string, QuestRuntime>();
        readonly HashSet<string> _seenCutscenes = new HashSet<string>();

        public event Action<string, QuestStatus> QuestStatusChanged;
        public event Action<string, int> QuestStageChanged;

        void Awake()
        {
            if (Instance && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            ResetToNewGame();
        }

        void OnEnable() => SaveLoadManager.Register(this);
        void OnDisable() => SaveLoadManager.Unregister(this);

        public void ResetToNewGame()
        {
            _flags.Clear();
            _quests.Clear();
            _seenCutscenes.Clear();
            Act = 1; Chapter = 1;
            foreach (var q in allQuests)
                _quests[q.questId] = new QuestRuntime { def = q, status = QuestStatus.Locked };
            RefreshAvailability();
        }

        // ------------------------------------------------------------------------------
        #region Flags
        public int GetFlag(string flag) => _flags.TryGetValue(flag, out var v) ? v : 0;
        public bool HasFlag(string flag) => GetFlag(flag) != 0;

        public void SetFlag(string flag, int value)
        {
            if (GetFlag(flag) == value) return;
            _flags[flag] = value;
            GameEvents.RaiseStoryFlagChanged(flag, value);
        }

        public void AddFlag(string flag, int delta) => SetFlag(flag, GetFlag(flag) + delta);

        public bool Check(IEnumerable<FlagCondition> conds) =>
            conds == null || conds.All(c => c.Evaluate(GetFlag(c.flag)));

        void Apply(IEnumerable<FlagEffect> effects)
        {
            if (effects == null) return;
            foreach (var e in effects)
                if (e.op == FlagOp.Set) SetFlag(e.flag, e.value); else AddFlag(e.flag, e.value);
        }

        /// <summary>The dominant alignment, used by dialogue to pick reaction variants.</summary>
        public string DominantAlignment()
        {
            var meters = new[] { Flags.Heroic, Flags.GreedyMerchant, Flags.ChaosGremlin, Flags.HaremLord };
            var best = meters.OrderByDescending(GetFlag).First();
            return GetFlag(best) > 0 ? best : null;
        }
        #endregion

        // ------------------------------------------------------------------------------
        #region Quests
        public QuestStatus GetStatus(string questId) =>
            _quests.TryGetValue(questId, out var q) ? q.status : QuestStatus.Locked;

        public IEnumerable<QuestDefinition> GetQuests(QuestCategory cat, QuestStatus status) =>
            _quests.Values.Where(q => q.def.category == cat && q.status == status).Select(q => q.def);

        public IEnumerable<QuestDefinition> Completed() =>
            _quests.Values.Where(q => q.status == QuestStatus.Completed).Select(q => q.def);

        public QuestStage CurrentStage(string questId) =>
            _quests.TryGetValue(questId, out var q) && q.stage < q.def.stages.Count ? q.def.stages[q.stage] : null;

        public int GetCounter(string questId, string counterId) =>
            _quests.TryGetValue(questId, out var q) && q.counters.TryGetValue(counterId, out var v) ? v : 0;

        void RefreshAvailability()
        {
            foreach (var q in _quests.Values)
            {
                if (q.status != QuestStatus.Locked) continue;
                if (q.def.category == QuestCategory.MainStory &&
                    (q.def.act > Act || (q.def.act == Act && q.def.chapter > Chapter))) continue;
                if (!q.def.prerequisiteQuests.All(p => GetStatus(p) == QuestStatus.Completed)) continue;
                if (!Check(q.def.unlockConditions)) continue;
                SetStatus(q, QuestStatus.Available);
            }
        }

        public bool StartQuest(string questId)
        {
            if (!_quests.TryGetValue(questId, out var q) || q.status != QuestStatus.Available) return false;
            SetStatus(q, QuestStatus.Active);
            EnterStage(q, 0);
            if (string.IsNullOrEmpty(TrackedQuestId)) TrackedQuestId = questId;
            return true;
        }

        /// <summary>
        /// Global progress hook — combat, crafting, dialogue and delivery systems call this
        /// with a counter id. Every active quest whose current stage cares about it advances.
        /// </summary>
        public void ReportProgress(string counterId, int amount = 1)
        {
            foreach (var q in _quests.Values.Where(x => x.status == QuestStatus.Active).ToList())
            {
                var stage = q.def.stages[q.stage];
                var obj = stage.objectives.FirstOrDefault(o => o.counterId == counterId);
                if (obj == null) continue;

                q.counters.TryGetValue(counterId, out var cur);
                q.counters[counterId] = Mathf.Min(cur + amount, obj.required);
                if (stage.choices.Count == 0 && StageObjectivesMet(q)) AdvanceLinear(q);
            }
        }

        bool StageObjectivesMet(QuestRuntime q) =>
            q.def.stages[q.stage].objectives.All(o => q.counters.TryGetValue(o.counterId, out var v) && v >= o.required);

        public IEnumerable<QuestChoice> AvailableChoices(string questId)
        {
            if (!_quests.TryGetValue(questId, out var q) || q.status != QuestStatus.Active) return Enumerable.Empty<QuestChoice>();
            if (!StageObjectivesMet(q)) return Enumerable.Empty<QuestChoice>();
            return q.def.stages[q.stage].choices.Where(c => Check(c.requires));
        }

        /// <summary>Resolve a branching choice. Applies flags, gold and relationship deltas.</summary>
        public bool MakeChoice(string questId, string choiceId)
        {
            var choice = AvailableChoices(questId).FirstOrDefault(c => c.choiceId == choiceId);
            if (choice == null) return false;
            var q = _quests[questId];

            q.chosenBranch = choiceId;
            Apply(choice.effects);
            if (choice.goldDelta != 0) EconomyManager.Instance?.AddGold(choice.goldDelta, $"choice:{choiceId}");
            if (!string.IsNullOrEmpty(choice.npcId))
                RelationshipHooks.Apply(choice.npcId, choice.affinityDelta, choice.trustDelta, null);

            if (choice.nextStage < 0 || choice.nextStage >= q.def.stages.Count) CompleteQuest(q);
            else EnterStage(q, choice.nextStage);
            return true;
        }

        void AdvanceLinear(QuestRuntime q)
        {
            if (q.stage + 1 >= q.def.stages.Count) CompleteQuest(q);
            else EnterStage(q, q.stage + 1);
        }

        void EnterStage(QuestRuntime q, int index)
        {
            q.stage = index;
            q.counters.Clear();
            Apply(q.def.stages[index].onEnterEffects);
            QuestStageChanged?.Invoke(q.def.questId, index);

            if (q.def.isBossQuest && index == q.def.stages.Count - 1)
                GameEvents.RaiseBossEncounterStarting(q.def.bossId);

            if (q.def.stages[index].objectives.Count == 0 && q.def.stages[index].choices.Count == 0)
                AdvanceLinear(q);
        }

        void CompleteQuest(QuestRuntime q)
        {
            SetStatus(q, QuestStatus.Completed);
            Apply(q.def.completionEffects);
            if (q.def.goldReward != 0) EconomyManager.Instance?.AddGold(q.def.goldReward, $"quest:{q.def.questId}");
            if (!string.IsNullOrEmpty(q.def.companionNpcId) && !string.IsNullOrEmpty(q.def.unlockMilestoneForNpc))
                RelationshipHooks.Apply(q.def.companionNpcId, 10, 15, q.def.unlockMilestoneForNpc);

            if (TrackedQuestId == q.def.questId) TrackedQuestId = null;
            if (q.def.advancesChapter) AdvanceChapter();

            GameEvents.RaiseQuestCompleted(q.def.questId);
            RefreshAvailability();
        }

        public void FailQuest(string questId)
        {
            if (_quests.TryGetValue(questId, out var q) && q.status == QuestStatus.Active)
                SetStatus(q, QuestStatus.Failed);
        }

        void SetStatus(QuestRuntime q, QuestStatus s)
        {
            q.status = s;
            QuestStatusChanged?.Invoke(q.def.questId, s);
        }
        #endregion

        // ------------------------------------------------------------------------------
        #region Acts / chapters / endings
        public void AdvanceChapter()
        {
            // Chapter → next; act rolls over when no main quest exists for the next chapter.
            int nextChapter = Chapter + 1;
            bool chapterExists = allQuests.Any(q => q.category == QuestCategory.MainStory && q.act == Act && q.chapter == nextChapter);
            if (chapterExists) Chapter = nextChapter;
            else { Act++; Chapter = 1; }
            GameEvents.RaiseChapterStarted(Act, Chapter);
            RefreshAvailability();
        }

        public string ChapterTitle()
        {
            var q = allQuests.FirstOrDefault(x => x.category == QuestCategory.MainStory && x.act == Act && x.chapter == Chapter);
            return q ? q.title : $"Act {Act} – Chapter {Chapter}";
        }

        public string EvaluateEnding() =>
            endings.Where(e => Check(e.conditions)).OrderByDescending(e => e.priority).FirstOrDefault()?.endingId
            ?? "ending_default_debt_forever";

        public bool MarkCutsceneSeen(string id) => _seenCutscenes.Add(id);
        #endregion

        // ------------------------------------------------------------------------------
        #region Save
        public void CaptureState(SaveData data)
        {
            var s = data.story;
            s.act = Act; s.chapter = Chapter;
            s.flags = new Dictionary<string, int>(_flags);
            s.trackedQuestId = TrackedQuestId;
            s.seenCutscenes = _seenCutscenes.ToList();
            s.quests = _quests.Values
                .Where(q => q.status != QuestStatus.Locked)
                .Select(q => new QuestSaveState
                {
                    questId = q.def.questId,
                    status = (int)q.status,
                    stageIndex = q.stage,
                    counters = new Dictionary<string, int>(q.counters),
                    chosenBranch = q.chosenBranch,
                }).ToList();

            data.meta.act = Act;
            data.meta.chapter = Chapter;
            data.meta.chapterTitle = ChapterTitle();
        }

        public void RestoreState(SaveData data)
        {
            ResetToNewGame();
            var s = data.story;
            Act = s.act; Chapter = s.chapter;
            foreach (var kv in s.flags) _flags[kv.Key] = kv.Value;
            TrackedQuestId = s.trackedQuestId;
            _seenCutscenes.UnionWith(s.seenCutscenes);

            foreach (var qs in s.quests)
            {
                if (!_quests.TryGetValue(qs.questId, out var q))
                {
                    Debug.LogWarning($"[Story] Save references unknown quest '{qs.questId}' — dropped.");
                    continue;
                }
                q.status = (QuestStatus)qs.status;
                q.stage = Mathf.Clamp(qs.stageIndex, 0, Mathf.Max(0, q.def.stages.Count - 1));
                q.counters = new Dictionary<string, int>(qs.counters ?? new Dictionary<string, int>());
                q.chosenBranch = qs.chosenBranch;
            }
            // New quests added by a patch become available if their conditions are now met.
            RefreshAvailability();
        }
        #endregion
    }

    /// <summary>
    /// Decouples story from the romance system: the NPC registry assigns this delegate at boot
    /// so StorySystem doesn't need a compile-time reference to Intimacy.
    /// </summary>
    public static class RelationshipHooks
    {
        public static Action<string, float, float, string> Handler;   // (npcId, affinity, trust, milestone)
        public static void Apply(string npcId, float affinity, float trust, string milestone) =>
            Handler?.Invoke(npcId, affinity, trust, milestone);
    }
}
