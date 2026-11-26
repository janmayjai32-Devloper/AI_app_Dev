using System;
using System.Collections.Generic;
using IsekaiChaos.Core;

namespace IsekaiChaos.SaveSystem
{
    /// <summary>
    /// Anything that owns persistent state implements this and registers with
    /// <see cref="SaveLoadManager"/>. Capture writes into the shared SaveData; Restore reads it.
    /// </summary>
    public interface ISaveable
    {
        /// <summary>Restore order: lower runs first (world before NPCs before UI).</summary>
        int RestoreOrder { get; }
        void CaptureState(SaveData data);
        void RestoreState(SaveData data);
    }

    // ------------------------------------------------------------------------------------
    // Root document. Serialized with Newtonsoft.Json (com.unity.nuget.newtonsoft-json),
    // gzip-compressed and checksummed by SaveLoadManager.
    // RULE: never rename or remove a field without adding a SaveMigrator step.
    // ------------------------------------------------------------------------------------
    [Serializable]
    public class SaveData
    {
        public const int CurrentVersion = 1;

        public int saveVersion = CurrentVersion;
        public string gameBuild;                       // Application.version at write time
        public SaveMetadata meta = new SaveMetadata();

        public PlayerSaveState player = new PlayerSaveState();
        public List<NpcSaveState> npcs = new List<NpcSaveState>();
        public WorldSaveState world = new WorldSaveState();
        public StorySaveState story = new StorySaveState();
        public BaseSaveState baseState = new BaseSaveState();
        public EconomySaveState economy = new EconomySaveState();
        public List<RelationshipSaveState> relationships = new List<RelationshipSaveState>();
    }

    /// <summary>Small, separately-stored header so the slot list loads without parsing full saves.</summary>
    [Serializable]
    public class SaveMetadata
    {
        public string slotId;
        public string playerName;
        public BiomeId biome;
        public int act;
        public int chapter;
        public string chapterTitle;
        public double playtimeSeconds;
        public long gold;
        public long timestampUtcTicks;
        public int saveVersion;
        public string thumbnailFile;                   // relative file name, e.g. "manual_03.jpg"

        public DateTime TimestampLocal => new DateTime(timestampUtcTicks, DateTimeKind.Utc).ToLocalTime();
    }

    // ----------------------------- Appearance / wardrobe --------------------------------

    [Serializable]
    public class AppearanceState
    {
        public string raceId;                          // "human", "elf", "demon", "beastkin_wolf", ...
        public Dictionary<string, float> morphs = new Dictionary<string, float>();   // slider id -> 0..1
        public Dictionary<string, string> colors = new Dictionary<string, string>(); // channel -> "#RRGGBBAA"
        public List<string> skinDecals = new List<string>();                         // tattoos, scars, markings
        public string hairStyleId;
        public string tailId;
        public string earsId;
        public string hornsId;
        public string wingsId;
    }

    [Serializable]
    public class WardrobeItemState
    {
        public string itemId;
        public int layer;                              // WardrobeLayer as int
        public float durability;                       // 0..1
        public int condition;                          // GarmentCondition as int
    }

    [Serializable]
    public class WardrobeState
    {
        public List<WardrobeItemState> equipped = new List<WardrobeItemState>();
        public List<int> hiddenLayers = new List<int>(); // layers toggled off (strip toggles)
    }

    // ----------------------------- Actors ------------------------------------------------

    [Serializable]
    public class PlayerSaveState
    {
        public string name;
        public AppearanceState appearance = new AppearanceState();
        public WardrobeState wardrobe = new WardrobeState();
        public float[] position = new float[3];
        public float yaw;
        public int level = 1;
        public long xp;
        public float hp, mp;
        public List<string> inventory = new List<string>();      // item ids (stackables in economy)
        public Dictionary<string, int> stackables = new Dictionary<string, int>();
    }

    [Serializable]
    public class NpcSaveState
    {
        public string npcId;
        public bool recruited;
        public AppearanceState appearance = new AppearanceState(); // companions are customizable too
        public WardrobeState wardrobe = new WardrobeState();
        public string assignedBuildingId;
        public int skillTier;
        public int flawState;                          // 0 = untouched, 1 = cured, 2 = embraced
        public BiomeId currentBiome;
        public float[] position = new float[3];
    }

    [Serializable]
    public class RelationshipSaveState
    {
        public string npcId;
        public float affinity, trust, lust, mood;
        public List<string> milestones = new List<string>();
        public List<string> unlockedScenes = new List<string>();
        public List<string> unlockedPoses = new List<string>();
        public long lastGiftDay;
    }

    // ----------------------------- World / story / base / economy ------------------------

    [Serializable]
    public class WorldSaveState
    {
        public BiomeId currentBiome = BiomeId.StarterMeadows;
        public List<BiomeId> discoveredBiomes = new List<BiomeId>();
        public List<string> defeatedBosses = new List<string>();
        public List<string> openedChests = new List<string>();
        public float timeOfDay;                        // 0..24
        public long dayCount;
    }

    [Serializable]
    public class QuestSaveState
    {
        public string questId;
        public int stageIndex;
        public int status;                             // QuestStatus as int
        public Dictionary<string, int> counters = new Dictionary<string, int>();
        public string chosenBranch;
    }

    [Serializable]
    public class StorySaveState
    {
        public int act = 1;
        public int chapter = 1;
        public Dictionary<string, int> flags = new Dictionary<string, int>();
        public List<QuestSaveState> quests = new List<QuestSaveState>();
        public string trackedQuestId;
        public List<string> seenCutscenes = new List<string>();
    }

    [Serializable]
    public class BuildingSaveState
    {
        public string buildingId;                      // definition id, e.g. "forge"
        public string instanceId;
        public int gridX, gridY, rotation;
        public int tier;
        public List<string> assignedNpcIds = new List<string>();
        public double storedIncome;
    }

    [Serializable]
    public class BaseSaveState
    {
        public int plotLevel = 1;                      // unlocks grid size
        public List<BuildingSaveState> buildings = new List<BuildingSaveState>();
        public List<RoadCellSaveState> roads = new List<RoadCellSaveState>();
    }

    [Serializable]
    public class RoadCellSaveState
    {
        public int x, y;
        public string styleId;
    }

    [Serializable]
    public class RegionalMarketState
    {
        public BiomeId biome;
        public Dictionary<string, float> supply = new Dictionary<string, float>();
        public Dictionary<string, float> demand = new Dictionary<string, float>();
    }

    [Serializable]
    public class EconomySaveState
    {
        public long gold;
        public long guildDebt;
        public long unpaidFines;
        public List<RegionalMarketState> markets = new List<RegionalMarketState>();
    }
}
