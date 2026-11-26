using System;
using System.Collections.Generic;
using IsekaiChaos.Animation;
using UnityEngine;

namespace IsekaiChaos.World
{
    /// <summary>
    /// Player-side gathering: which tool tier is equipped per resource type, the yield bonus
    /// (from gear/companions), and the tool prop shown in hand during the gather animation
    /// (axe / pickaxe / sickle / skinning knife / hammer).
    /// </summary>
    [RequireComponent(typeof(InteractionController))]
    public class GatheringController : MonoBehaviour
    {
        [Serializable]
        public class ToolSlot
        {
            public ResourceType type;
            [Range(0, 8)] public int tier = 1;
            public GameObject propInHand;
            public string gatherAnimState = "Gather_Chop";
        }

        [SerializeField] List<ToolSlot> tools = new List<ToolSlot>();
        [SerializeField] float yieldMultiplier = 1f;

        public float YieldMultiplier { get => yieldMultiplier; set => yieldMultiplier = value; }
        public event Action<ResourceNode> Rejected;       // UI: "Your Tier 2 axe bounces off the Tier 5 ironwood."

        InteractionController _ic;

        void Awake()
        {
            _ic = GetComponent<InteractionController>();
            _ic.Started += OnStarted;
            _ic.Finished += _ => ShowProp(null);
        }

        public int ToolTier(ResourceType t)
        {
            foreach (var s in tools) if (s.type == t) return s.tier;
            return 0;
        }

        public void SetTool(ResourceType t, int tier)
        {
            foreach (var s in tools) if (s.type == t) { s.tier = tier; return; }
            tools.Add(new ToolSlot { type = t, tier = tier });
        }

        void OnStarted(Interactable it)
        {
            var node = it.GetComponent<ResourceNode>();
            if (!node) return;
            var slot = tools.Find(s => s.type == node.Type);
            ShowProp(slot);
            // Per-tool animation: designers set the loop state on the node; override with the tool's swing.
            if (slot != null && !string.IsNullOrEmpty(slot.gatherAnimState)) it.loopState = slot.gatherAnimState;
        }

        void ShowProp(ToolSlot active)
        {
            foreach (var s in tools) if (s.propInHand) s.propInHand.SetActive(s == active);
        }

        public void Reject(ResourceNode node)
        {
            _ic.Cancel();
            Rejected?.Invoke(node);
        }
    }
}
