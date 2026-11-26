using System;
using System.Collections.Generic;
using System.Linq;
using IsekaiChaos.Core;
using IsekaiChaos.SaveSystem;
using UnityEngine;

namespace IsekaiChaos.Economy
{

    /// <summary>
    /// Gold, Guild Debt, Collateral Damage Fines, and regional supply/demand pricing.
    ///
    /// Price = base * clamp((demand / supply) ^ elasticity, 0.25, 4) * regionalModifier.
    /// Supply and demand drift back toward 1.0 daily; quest outcomes and trade shift them
    /// (e.g. clearing bandits from the caravan route raises Desert silk supply).
    /// </summary>
    public class EconomyManager : MonoBehaviour, ISaveable
    {
        public static EconomyManager Instance { get; private set; }

        [SerializeField] List<TradeGood> goods = new List<TradeGood>();
        [SerializeField] long startingDebt = 1_000_000;    // the "summoning processing fee"
        [SerializeField] float dailyDebtInterest = 0.001f;
        [SerializeField] float dailyMarketRecovery = 0.1f;
        [SerializeField] float producerDiscount = 0.6f;

        public long Gold { get; private set; }
        public long GuildDebt { get; private set; }
        public long UnpaidFines { get; private set; }
        public int RestoreOrder => 5;

        class Market
        {
            public readonly Dictionary<string, float> supply = new Dictionary<string, float>();
            public readonly Dictionary<string, float> demand = new Dictionary<string, float>();
        }
        readonly Dictionary<BiomeId, Market> _markets = new Dictionary<BiomeId, Market>();

        public event Action<string> LedgerEntry;            // human-readable log for the UI

        void Awake()
        {
            if (Instance && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            GuildDebt = startingDebt;
        }

        void OnEnable() => SaveLoadManager.Register(this);
        void OnDisable() => SaveLoadManager.Unregister(this);

        // --------------------------------------------------------------------------
        public void AddGold(long amount, string reason)
        {
            Gold = Math.Max(0, Gold + amount);
            LedgerEntry?.Invoke($"{(amount >= 0 ? "+" : "")}{amount}g  {reason}");
            GameEvents.RaiseGoldChanged(Gold);
        }

        public bool TrySpend(long amount, string reason)
        {
            if (amount > Gold) return false;
            AddGold(-amount, reason);
            return true;
        }

        public void PayDebt(long amount)
        {
            amount = Math.Min(amount, Math.Min(Gold, GuildDebt));
            if (amount <= 0) return;
            AddGold(-amount, "Guild debt repayment");
            GuildDebt -= amount;
        }

        /// <summary>"You blew up the town fountain. Again." — the comedic wealth sink.</summary>
        public void IssueFine(long amount, string reason)
        {
            UnpaidFines += amount;
            LedgerEntry?.Invoke($"FINE {amount}g  {reason}");
        }

        public void PayFines()
        {
            long pay = Math.Min(Gold, UnpaidFines);
            if (pay <= 0) return;
            AddGold(-pay, "Collateral damage fines");
            UnpaidFines -= pay;
        }

        // --------------------------------------------------------------------------
        Market GetMarket(BiomeId b)
        {
            if (!_markets.TryGetValue(b, out var m)) _markets[b] = m = new Market();
            return m;
        }

        public long GetPrice(string goodId, BiomeId biome, bool selling)
        {
            var g = goods.FirstOrDefault(x => x.goodId == goodId);
            if (g == null) return 0;
            var m = GetMarket(biome);
            float s = m.supply.TryGetValue(goodId, out var sv) ? sv : 1f;
            float d = m.demand.TryGetValue(goodId, out var dv) ? dv : 1f;
            float ratio = Mathf.Clamp(Mathf.Pow(d / Mathf.Max(0.05f, s), g.elasticity), 0.25f, 4f);
            float regional = g.producedIn.Contains(biome) ? producerDiscount : 1f;
            float spread = selling ? 0.8f : 1f;                 // merchants buy low
            return Math.Max(1, (long)Mathf.Round(g.basePrice * ratio * regional * spread));
        }

        public void ShiftSupply(BiomeId biome, string goodId, float delta)
        {
            var m = GetMarket(biome);
            m.supply[goodId] = Mathf.Max(0.05f, (m.supply.TryGetValue(goodId, out var v) ? v : 1f) + delta);
        }

        public void ShiftDemand(BiomeId biome, string goodId, float delta)
        {
            var m = GetMarket(biome);
            m.demand[goodId] = Mathf.Max(0.05f, (m.demand.TryGetValue(goodId, out var v) ? v : 1f) + delta);
        }

        public void OnNewDay()
        {
            GuildDebt += (long)(GuildDebt * dailyDebtInterest);
            foreach (var m in _markets.Values)
            {
                foreach (var k in m.supply.Keys.ToList()) m.supply[k] = Mathf.MoveTowards(m.supply[k], 1f, dailyMarketRecovery);
                foreach (var k in m.demand.Keys.ToList()) m.demand[k] = Mathf.MoveTowards(m.demand[k], 1f, dailyMarketRecovery);
            }
        }

        // --------------------------------------------------------------------------
        public void CaptureState(SaveData data)
        {
            var e = data.economy;
            e.gold = Gold; e.guildDebt = GuildDebt; e.unpaidFines = UnpaidFines;
            e.markets = _markets.Select(kv => new RegionalMarketState
            {
                biome = kv.Key,
                supply = new Dictionary<string, float>(kv.Value.supply),
                demand = new Dictionary<string, float>(kv.Value.demand),
            }).ToList();
            data.meta.gold = Gold;
        }

        public void RestoreState(SaveData data)
        {
            var e = data.economy;
            Gold = e.gold; GuildDebt = e.guildDebt; UnpaidFines = e.unpaidFines;
            _markets.Clear();
            foreach (var ms in e.markets)
            {
                var m = GetMarket(ms.biome);
                foreach (var kv in ms.supply) m.supply[kv.Key] = kv.Value;
                foreach (var kv in ms.demand) m.demand[kv.Key] = kv.Value;
            }
            GameEvents.RaiseGoldChanged(Gold);
        }
    }
}
