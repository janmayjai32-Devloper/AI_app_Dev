using System;
using IsekaiChaos.Animation;
using UnityEngine;

namespace IsekaiChaos.World
{
    public enum ResourceType { Wood = 0, Ore = 1, Fiber = 2, Hide = 3, Stone = 4, Herb = 5, Crystal = 6, Silk = 7 }

    /// <summary>
    /// Albion-style gatherable: a tiered node (T1–T8) with several charges. Each gather
    /// "swing" (animation contact frame) removes one charge and yields items. The mesh steps
    /// through visual stages (full → half → stump) and the node respawns after a timer.
    ///
    /// Biomes place nodes by tier band so higher zones are worth the trip:
    ///   Meadows T1–T3 · Forest T2–T5 · Desert T3–T5 · Coast T3–T6 · Frostpeak T5–T7 · Volcanic T6–T8
    /// </summary>
    [RequireComponent(typeof(Interactable))]
    public class ResourceNode : MonoBehaviour
    {
        [SerializeField] ResourceType type = ResourceType.Wood;
        [SerializeField, Range(1, 8)] int tier = 1;
        [SerializeField] int maxCharges = 6;
        [SerializeField] Vector2Int yieldPerCharge = new Vector2Int(1, 2);
        [SerializeField] float respawnSeconds = 180f;
        [SerializeField] float secondsPerSwing = 1.1f;
        [Tooltip("Visual stages from full to depleted. Index chosen by remaining charge ratio.")]
        [SerializeField] GameObject[] stages;
        [SerializeField] ParticleSystem hitVfx;
        [SerializeField] string itemIdFormat = "{0}_t{1}";     // e.g. "wood_t3"

        public ResourceType Type => type;
        public int Tier => tier;
        public int Charges { get; private set; }
        public bool Depleted => Charges <= 0;
        public float SecondsPerSwing => secondsPerSwing;
        public string ItemId => string.Format(itemIdFormat, type.ToString().ToLowerInvariant(), tier);

        /// <summary>(itemId, amount) — inventory listens.</summary>
        public static event Action<string, int> Gathered;

        Interactable _interactable;
        float _respawnAt;

        void Awake()
        {
            _interactable = GetComponent<Interactable>();
            _interactable.kind = InteractionKind.Gather;
            _interactable.onContact.AddListener(OnSwingContact);
            Charges = maxCharges;
            RefreshVisual();
        }

        void Update()
        {
            if (Depleted && Time.time >= _respawnAt)
            {
                Charges = maxCharges;
                _interactable.enabled = true;
                RefreshVisual();
            }
        }

        public bool CanGather(int toolTier) => !Depleted && toolTier >= tier - 1;   // one tier of leeway

        /// <summary>Called on the animation's contact frame (axe hits trunk, pick hits ore).</summary>
        void OnSwingContact(GameObject gatherer)
        {
            if (Depleted) return;
            var g = gatherer.GetComponent<GatheringController>();
            if (g && !CanGather(g.ToolTier(type))) { g.Reject(this); return; }

            Charges--;
            int amount = UnityEngine.Random.Range(yieldPerCharge.x, yieldPerCharge.y + 1);
            if (g) amount = Mathf.RoundToInt(amount * g.YieldMultiplier);
            Gathered?.Invoke(ItemId, amount);
            if (hitVfx) hitVfx.Play();
            RefreshVisual();

            if (Depleted)
            {
                _respawnAt = Time.time + respawnSeconds;
                var ic = gatherer.GetComponent<InteractionController>();
                if (ic) ic.Cancel();                              // play the exit animation
                _interactable.enabled = false;
            }
        }

        void RefreshVisual()
        {
            if (stages == null || stages.Length == 0) return;
            float ratio = maxCharges > 0 ? (float)Charges / maxCharges : 0;
            int idx = Mathf.Clamp(Mathf.FloorToInt((1f - ratio) * (stages.Length - 1) + 0.0001f), 0, stages.Length - 1);
            if (Depleted) idx = stages.Length - 1;
            for (int i = 0; i < stages.Length; i++) if (stages[i]) stages[i].SetActive(i == idx);
        }
    }
}
