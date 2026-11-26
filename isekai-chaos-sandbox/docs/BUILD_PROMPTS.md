# Build Prompts

These are copy-paste prompts for building the game with an AI coding assistant, such as
Claude Code, Cursor or Copilot Chat, run inside this repository.

**How to use:**
- **Run one prompt at a time, in order.** Each one is a single, testable step.
- **Paste the Master Context once** at the start of every new session, or keep it in a `CLAUDE.md` file at the repository root.
- **Don't move on until the step's "Done when" checks pass** in Unity. Paste errors back to the assistant word for word.
- **Never ask for "the whole game"** in one prompt. That produces broken, untestable code.

---

## 0. Master Context (paste first, every session)

```text
You are the lead Unity engineer on "Isekai Chaos Sandbox", an OFFLINE single-player 3D anime
open-world parody RPG for Android (primary) and Windows.

PROJECT FACTS (read before changing anything):
- Repo folder: isekai-chaos-sandbox/. Unity 2022.3 LTS, URP 14, Addressables,
  Newtonsoft JSON, AI Navigation. C# only. No networking.
- Read README.md, docs/GAME_DESIGN.md, docs/ART_STYLE_GUIDE.md, docs/ANIMATOR_SETUP.md,
  docs/VROID_PIPELINE.md and docs/ROADMAP.md before your first change.
- Existing systems live in Assets/Scripts/<Module>/ (Core, Customizer, IntimacyManager,
  WorldStreaming, StorySystem, SaveSystem, Combat, Animation, World, Economy, BaseBuilding,
  UI, Editor). EXTEND them; do not rewrite or duplicate them.
- Characters are VRoid/VRM humanoids set up via IsekaiChaos ▸ Characters ▸ Setup…
- Art target: 3D anime cel-shading in the Genshin Impact / Neverness to Everness family
  (style reference only: never copy their characters, names, UI or assets).
- Controls: Direct mode (default, joystick/WASD + orbit camera + attack/jump/dodge) and
  Tap-to-move mode (Albion-style). Both drive the same motor/combat code.
- Tone: comedic fantasy parody (debt, fines, flawed companions).

ENGINEERING RULES:
1. Data-driven: tunable numbers live in ScriptableObjects or [SerializeField], not literals.
2. Mobile performance: no per-frame allocations (use NonAlloc physics, cached arrays,
   StringToHash), no FindObjectOfType in Update, no LINQ in hot paths. Target 60 FPS on a
   mid-range phone (6 GB RAM, Snapdragon 7-series).
3. Every persistent state goes through ISaveable + SaveData. Never rename or remove a
   SaveData field without adding a SaveMigrator step and bumping SaveData.CurrentVersion.
4. Cross-system communication via GameEvents or C# events, not hard references between modules.
5. Each class: one responsibility, XML summary comment, file name equals class name.
   ScriptableObject classes get their own file.
6. Write EditMode tests (Unity Test Framework) for pure logic: economy pricing, quest
   flags, save encode/decode and migration, wardrobe occlusion masks.
7. After changes: list every file touched, what to set up in the Unity Editor (components,
   references, layers), and how to test it in Play mode.
8. If a requirement is ambiguous, state your assumption in one line and proceed.

CONTENT RULES (non-negotiable):
- All romanceable characters are adults (18+), enforced by NPCProfile.
- Mature content is OFF by default and gated behind ContentSettings (age confirmation).
- Every intimate scene requires mutual consent through NPCRomanceController.EvaluateConsent
  and can be stopped by either side. Defeat/capture events are never sexual; they are
  comedic setbacks (gold loss, infirmary bill).
- Keep generated text, code comments and asset names non-explicit.

Reply format for every task: (1) short plan, (2) code changes, (3) Editor setup steps,
(4) test steps, (5) known limitations.
```

---

## Phase 1: Make it compile and move

### Prompt 1.1: First compile
```text
Open isekai-chaos-sandbox in Unity 2022.3. Here is the full Console output from the first import:
<PASTE ERRORS>
Fix every compile error with minimal changes that preserve the documented design. Do not
delete systems to make errors go away. Explain each fix in one line.
```
**Done when:** the Console shows zero errors and zero script warnings in red.

### Prompt 1.2: Grey-box test scene
```text
Create an Editor menu tool "IsekaiChaos ▸ Scenes ▸ Build Prototype Meadows" that generates
Assets/Scenes/Prototype_Meadows.unity containing:
- a 200x200 m ground (layer Ground) with a NavMeshSurface, baked
- 10 primitive "trees" and 3 primitive "houses" with colliders
- a dirt road strip with RoadSpeedZone
- a directional light (warm noon), skybox, URP global volume (bloom low, tonemapping ACES)
- the managers GameObject: SaveLoadManager, EconomyManager, StoryProgressManager, NPCRegistry
- ThirdPersonCameraRig + IsometricCameraRig + ControlModeManager wired together
- a UI Canvas (scale with screen size 1920x1080, match 0.5) containing VirtualJoystick (left
  45%), Attack/Jump/Dodge buttons and an ability ring of 6 buttons wired to DirectControlInput
- a spawn point for the player
Make the tool idempotent (re-running replaces the scene).
```
**Done when:** the scene opens and every Inspector reference is filled in, with no "Missing" fields.

### Prompt 1.3: Player in the scene
```text
I imported Kazuya.vrm and ran Setup Selected As Player. Now:
1. Create Assets/Animation/Humanoid_Base.controller with an Editor script exactly per
   docs/ANIMATOR_SETUP.md (3 layers, all parameters, locomotion blend tree 0/2.2/5.5/8,
   jump start/air/land driven by Jump + VerticalSpeed + Grounded, hit reactions, death).
   Leave clip slots that I fill by name: Idle, Walk, Run, Sprint, JumpStart, InAir, Land,
   Light_1..3, Dodge, Hit_F/B/L/R, Stagger, Knockdown, Death.
2. Write a clip auto-binder: given a folder of Humanoid FBX clips, assign them to states by
   name match.
3. Create data assets: ComboSet_Sword (3 light attacks with sensible timings), Ability_Dash
   (slot F, Dash, i-frames), Ability_Spin (slot Q, GroundAoE around self).
Report any state names that had no clip.
```
**Done when:** in Play mode, WASD moves with blended walk/run/sprint, Space jumps, the mouse orbits the camera, left-click plays a 3-hit combo, and Shift-tap rolls.

### Prompt 1.4: Game feel pass
```text
Tune movement and combat feel to match modern anime action RPGs:
- acceleration/deceleration curves, turn speed while running vs. attacking
- attack magnetism: short root-motion lunge toward the soft-locked target (cap 1.5 m)
- input buffer 0.25 s, cancel windows from AttackDefinition
- hitstop 0.05–0.09 s by attack weight, camera shake curve, slash trail on the weapon
- dodge: 0.3 s i-frames, perfect-dodge window (0.1 s) triggers 0.5 s slow-motion on enemies only
Expose every value in a single "GameFeelSettings" ScriptableObject and explain each.
```

### Prompt 1.5: Save and load end to end
```text
Wire save/load for everything in the prototype scene and build a Save/Load UI:
- 12 manual slots in a scrollable grid showing thumbnail, player name, biome, act/chapter,
  playtime, gold, timestamp; Quick Save/Load buttons; auto-save list (3 rolling)
- Export/Import buttons using ExternalSaveBridge
- a confirmation dialog for overwrite/delete
Add EditMode tests: encode→decode round trip, checksum mismatch detection, .bak fallback,
SaveMigrator v0→v1 fixture.
```
**Done when:** you can kill the app mid-play, relaunch, quick-load, and get the same position, HP, gold and quest state back.

---

## Phase 2: Characters, look and animation

### Prompt 2.1: Toon look sign-off
```text
Create a "Look Dev" scene: Kazuya and Bruni on a turntable, 4 lighting presets (morning,
noon, sunset, night) switchable with keys 1–4, and a side-by-side toggle of MToon vs
AnimeToon. Tune AnimeToon defaults to the values in docs/ART_STYLE_GUIDE.md §3. Add a
per-tier URP setup (Low/Mid/High renderer + volume profiles) matching §4 and a debug
overlay showing FPS, draw calls, tier.
```

### Prompt 2.2: Character creator
```text
Build the in-game character creator (UI + logic) on top of CharacterAppearanceController:
tabs Body / Face / Hair / Race / Colors / Outfit; sliders bound to blendshape ids from a
"MorphCatalog" ScriptableObject; race parts (elf ears, horns, wolf/fox ears, tails, wings);
color pickers with palette presets; randomize and reset; orbit/zoom preview camera with
portrait framing; save to SaveData.player.appearance. Phone-friendly: large touch targets,
collapsible panels, works in landscape.
```

### Prompt 2.3: Wardrobe and occlusion
```text
Make LayeredWardrobeController work with VRoid bodies: write an Editor tool that splits a
VRoid "Body" mesh into BodyRegion sub-meshes by bone weights (majority bone → region map),
creating one renderer per region, and wires them into the controller. Add a wardrobe UI
(5 layer tabs, equip/unequip, durability bars, layer visibility toggles respecting
ContentSettings). Add an EditMode test for occlusion mask math.
```

### Prompt 2.4: Interactions and paired animations
```text
Implement 6 interactables in the prototype scene using Interactable + InteractionController:
bench sit, chest open (lid rotates on the OnContact frame), door, tree chop (ResourceNode),
anvil craft, and a paired high-five with a companion (both characters aligned via anchors,
synced by the same normalized time). Add the HUD interaction prompt that reads
DirectControlInput.NearestInteractable.
```

---

## Phase 3: World, gathering, base

### Prompt 3.1: Starter Meadows terrain
```text
Generate Starter Meadows as 128 m chunks (4x4) following docs/GAME_DESIGN.md §3 map: Guild
Town (NE), base plot (center), Frog Marsh (SW pond), Slime Hills (NW), the King's Debt
Highway crossing. Use Unity Terrain per chunk with 4 painted layers, a highway spline mesh,
GPU-instanced grass (density per tier), clustered trees, and a hero landmark (clocktower).
Make each chunk an Addressable scene with label "WorldChunk" named Meadows_x_z and register
a BiomeDefinition. Verify WorldBiomeManager streams them without hitches > 50 ms.
```

### Prompt 3.2: Enemies and encounters
```text
Create enemies with EnemyAI + CombatController: Giant Frog (tongue-pull ability that drags
the player 3 m, comedic "swallow" stun that the player escapes by mashing Attack; no
damage-over-time), 3 Slime colors (split into 2 small slimes on death), and a Slime King
boss (2 phases, telegraphed AoE circles, pre-fight auto-save via GameEvents). Add spawn
zones with respawn timers and loot tables (ScriptableObject).
```

### Prompt 3.3: Base building
```text
Build the base-building mode on BaseBuildingManager + RoadBuilder: enter/exit build mode,
grid overlay, ghost preview (green valid / red invalid), rotate, place, upgrade, demolish
(50% refund), drag-to-paint roads with auto-tiling, worker assignment panel showing each
companion's flaw effect on efficiency, and passive income collection. Persist everything.
```

### Prompt 3.4: Economy and gathering loop
```text
Connect gathering → inventory → crafting → market: Inventory system (stacks, weight limit),
3 crafting stations with recipes (ScriptableObjects), regional shop UI using
EconomyManager.GetPrice with supply/demand arrows, guild debt payment screen and the fines
log. Balance a 30-minute loop where the player can afford the Forge upgrade.
```

---

## Phase 4: Story and companions

### Prompt 4.1: Quest journal and dialogue
```text
Build a dialogue system (ScriptableObject graphs: lines, choices, conditions on story flags,
effects on flags/relationships/gold) with a visual-novel style dialogue box, portrait
expressions (VrmExpressionController emotions), auto/skip, and log. Build the Quest Journal
UI (tabs Main / Companion / Bounties / Contracts / Completed, chapter recap, track button,
world waypoint marker + minimap arrow).
```

### Prompt 4.2: Act 1 content
```text
Author Act 1 chapters 1–5 from docs/GAME_DESIGN.md §8 as QuestDefinitions + dialogue graphs.
Each chapter needs: objectives, at least one branching choice that changes a StoryFlag,
comedic dialogue that matches the parody tone, rewards, and the chapter recap text. Include
recruitment quests for Bruni (drinking contest mini-game: timing bar, best of 3), Seraphine,
Mireille and Corvin.
```

### Prompt 4.3: Companions
```text
Implement CompanionAI: follow formation, use abilities on cooldown with simple priorities,
revive the player once per fight (Seraphine), banter barks triggered by context (low HP,
big crit, fine received). Implement each companion's Fatal Flaw mechanically:
Bruni (forge efficiency 0 until drinking contest won that day), Seraphine (10% chance her
heal hits an enemy), Mireille (skims gold when loyalty < 40), Corvin (skeleton workers
strike for 1 in-game hour every 6 hours unless the union break room is built).
```

### Prompt 4.4: Relationships
```text
Build the Bonds screen for NPCRomanceController: Affinity/Trust/Mood meters, gift giving
with daily limit and reaction lines, milestones list, and a clear "why is this locked"
message from ConsentResult. Hook story milestones and companion arc stages. Keep all
mature-gated UI hidden when ContentSettings.MatureContentEnabled is false.
```

---

## Phase 5: Polish and ship

### Prompt 5.1: Performance pass
```text
Profile the prototype on <DEVICE NAME> using the attached Profiler capture/screenshots:
<PASTE>. Identify the top 5 costs and fix them (batching, LOD, shadow distance, texture
sizes/ASTC, animator culling, UI canvas splitting, GC allocs). Report before/after numbers.
```

### Prompt 5.2: HUD and UX polish
```text
Polish the HUD for phones: safe-area support (notches), button size ≥ 9 mm, cooldown
radials, damage numbers pooled (no Instantiate in combat), low-HP vignette, minimap with
fog of war, settings screen (graphics tier, control mode, camera sensitivity, text size,
colorblind-safe indicators, content settings with age gate), and haptics on hit.
```

### Prompt 5.3: Release build
```text
Create a build pipeline: Editor menu "IsekaiChaos ▸ Build ▸ Android Release" (IL2CPP, ARM64,
ASTC, Addressables build, versionCode from git commit count, keystore from environment
variables) and "Windows Release". Add a crash/exception logger to persistentDataPath and a
"Send logs" export button. Write a release checklist in docs/RELEASE.md.
```

---

## Debugging prompt (use any time)
```text
Bug: <what happens>. Expected: <what should happen>. Steps: <1,2,3>.
Console: <paste>. Relevant files: <paths>.
Find the root cause before changing code. Explain it in 2–3 sentences, then make the
smallest fix, and tell me how to verify it.
```

## Review prompt (after each phase)
```text
Review all code changed in this phase for: correctness bugs, per-frame allocations,
missing save/load coverage, broken GameEvents subscriptions (leaks on destroy), mobile
input edge cases, and violations of the Master Context rules. List findings by severity,
then fix the critical ones.
```

---

## Concept-art prompts (for Niji Journey, Midjourney or Stable Diffusion)

These are for mood boards and pitch images only. They are not game assets.

```text
Key art: 3D anime cel-shaded open-world fantasy RPG screenshot, third-person view from
behind a young adventurer with brown hair and a red cape on a dirt road through bright
green meadows, cherry blossom trees, a small fantasy town with a white clocktower on a hill,
a giant goofy green frog monster ahead, soft painterly clouds, warm sunlight with rim
light, vibrant saturated colors, polished gacha-game rendering quality --ar 16:9
```
```text
Character sheet: full-body turnaround (front, side, back) of an adult wolf-kin blacksmith
woman, tall athletic build, messy orange hair, wolf ears and fluffy tail, leather apron
over a work shirt, big forge hammer, confident grin, 3D anime cel-shaded style, clean
white background, game character design sheet --ar 16:9
```
```text
Environment: cozy fantasy base village at golden hour, timber-frame forge with smoking
chimney, tavern with purple roof, steaming hot-spring bathhouse, cobblestone paths, lanterns,
fireflies, stylized anime game environment art, soft volumetric light --ar 16:9
```
