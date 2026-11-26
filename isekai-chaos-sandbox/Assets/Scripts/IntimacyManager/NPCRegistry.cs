using System.Collections.Generic;
using IsekaiChaos.StorySystem;
using UnityEngine;

namespace IsekaiChaos.Intimacy
{
    /// <summary>Looks up live NPC romance controllers by id and wires story → relationship hooks.</summary>
    public class NPCRegistry : MonoBehaviour
    {
        public static NPCRegistry Instance { get; private set; }
        readonly Dictionary<string, NPCRomanceController> _npcs = new Dictionary<string, NPCRomanceController>();

        void Awake()
        {
            Instance = this;
            RelationshipHooks.Handler = OnStoryRelationshipEvent;
            foreach (var npc in FindObjectsByType<NPCRomanceController>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                Register(npc);
        }

        void OnDestroy()
        {
            if (Instance == this) { Instance = null; RelationshipHooks.Handler = null; }
        }

        public void Register(NPCRomanceController npc)
        {
            if (npc && npc.Profile) _npcs[npc.Profile.npcId] = npc;
        }

        public NPCRomanceController Get(string npcId) => _npcs.TryGetValue(npcId, out var n) ? n : null;
        public IEnumerable<NPCRomanceController> All => _npcs.Values;

        public void BroadcastNewDay()
        {
            foreach (var n in _npcs.Values) n.OnNewDay();
        }

        void OnStoryRelationshipEvent(string npcId, float affinity, float trust, string milestone)
        {
            var npc = Get(npcId);
            if (npc == null) return;
            if (!string.IsNullOrEmpty(milestone)) npc.UnlockMilestone(milestone);
            if (affinity != 0) npc.Modify(RelationshipStat.Affinity, affinity);
            if (trust != 0) npc.Modify(RelationshipStat.Trust, trust);
        }
    }
}
