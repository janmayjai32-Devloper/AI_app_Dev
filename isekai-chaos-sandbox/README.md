# Isekai Chaos Sandbox: Parody RPG (working title)

This is an **offline, single-player** 3D open-world parody RPG for **Android and PC**. It is built
in **Unity 2022.3 LTS** with the **URP** render pipeline.

The controls follow the Albion Online model: a fixed isometric camera, tap or click to move,
tap an enemy to target it, and an ability bar where your gear decides your skills. Around that
sit an anime cel-shaded art style, KonoSuba-style comedy, deep character customization,
companion romance, base building with roads, gathering, and a branching story. Everything runs
on the device. There are no servers and no internet requirement.

> **Status:** this is the architecture and code scaffold. The C# prototypes below are written to
> compile together in one Unity project once the packages in `Packages/manifest.json` are
> installed. No compiler was available when they were written, so the first open in Unity is
> the first build. Art, animation clips, scenes and data assets come next, in the roadmap's
> phases.

## Docs

| Doc | Contents |
|---|---|
| [`docs/GAME_DESIGN.md`](docs/GAME_DESIGN.md) | Controls, combat, the world map and roads, gathering, the base, NPCs, Act 1, and the content policy |
| [`docs/ROADMAP.md`](docs/ROADMAP.md) | The 5-phase plan to a playable v1.0 vertical slice |
| [`docs/ANIMATOR_SETUP.md`](docs/ANIMATOR_SETUP.md) | Animator layers, parameters, state names and animation events the code expects |

## Folder structure

```
isekai-chaos-sandbox/
├── Packages/manifest.json            URP, Addressables, Newtonsoft JSON, AI Navigation
├── docs/
└── Assets/
    ├── Addressables/                 streamed content (world chunks, characters, garments)
    │   ├── Biomes/  Characters/  Wardrobe/
    ├── Art/  Characters/ Environments/ Shaders/ UI/
    ├── Audio/
    ├── Data/                         ScriptableObject assets (designers live here)
    │   ├── Biomes/ Buildings/ NPCs/ Quests/ Wardrobe/
    ├── Prefabs/  Scenes/  Settings/URP/
    └── Scripts/
        ├── Core/            GameEvents (event bus), ContentSettings (18+ gate), DeviceTier
        ├── Customizer/      LayeredWardrobeController ★, WardrobeItem, WardrobeTypes,
        │                    WardrobeDatabase, CharacterAppearanceController, CharacterStateBinder
        ├── IntimacyManager/ NPCRomanceController ★, NPCProfile, IntimacySessionController, NPCRegistry
        ├── WorldStreaming/  WorldBiomeManager ★, BiomeDefinition
        ├── StorySystem/     StoryProgressManager ★, QuestDefinition
        ├── SaveSystem/      SaveLoadManager ★, SaveData, SaveMigrator, ExternalSaveBridge
        ├── Combat/          CombatController, AbilityDefinition, AttackDefinition, ComboDefinition,
        │                    Hitbox, Hurtbox, Health, HitStopController, EnemyAI, PlayerDefeatHandler
        ├── Animation/       ClickToMoveMotor, IsometricCameraRig, InteractionController,
        │                    Interactable, AnimationEventRelay
        ├── World/           ResourceNode, GatheringController, RoadBuilder, RoadSpeedZone, RoadAwareSpeed
        ├── Economy/         EconomyManager, TradeGood
        ├── BaseBuilding/    BaseBuildingManager, BuildingDefinition
        └── UI/              TouchInputController, AimIndicator
```
★ = the five core classes the blueprint asked for.

## How the systems connect

```
 TouchInputController ──tap ground──▶ ClickToMoveMotor ──▶ CharacterController (+ root motion)
        │  tap enemy / ability             ▲ speed × RoadAwareSpeed
        ▼                                  │
 CombatController ──hit window──▶ Hitbox ─▶ Hurtbox ─▶ Health ─▶ LayeredWardrobeController
        │                                                   (clothing damage on hit region)
        ▼ HitLanded / shake
 HitStopController · IsometricCameraRig

 InteractionController ◀─ tap ── Interactable (bench, chest, forge, ResourceNode, NPC)

 GameEvents ──(biome change, quest done, building upgraded, boss incoming, app paused)──▶ SaveLoadManager auto-save
 SaveLoadManager ⇄ ISaveable: WorldBiomeManager, EconomyManager, StoryProgressManager,
                               CharacterStateBinder, BaseBuildingManager, RoadBuilder, NPCRomanceController
```

## Design decisions worth knowing

- **Offline-first.** Saves are local: gzip-compressed JSON with a SHA-256 checksum, a `saveVersion`
  header, atomic writes and a `.bak` fallback. Players back saves up with Export/Import, which
  matters for sideloaded APKs.
- **One combat system for everyone.** The player, companions and enemies all use
  `CombatController`. AI calls the same `SetTarget` and `RequestCast` methods that the touch UI calls.
- **Data-driven.** Attacks, abilities, quests, NPCs, garments, biomes and buildings are all
  ScriptableObjects. Designers tune them without touching code.
- **Mobile performance.** World chunks stream in additively through Addressables, nearest first
  and throttled. Radii and LOD come from the device tier, and the stream radius shrinks under low
  memory. Body regions covered by clothing stop rendering. Colors use MaterialPropertyBlocks so
  SRP batching keeps working. Hitboxes and AI allocate nothing per frame, and AI updates at 5 Hz.
- **Mature content is opt-in and adults only.** See *Content policy* in the design doc.
