# Production Roadmap: v1.0 Vertical Slice

**Scope of the slice:** Starter Meadows and Guild Town, one base plot with road building,
4 core companions, Act 1 (chapters 1–5), Albion-style combat and gathering, interactive
animation, and the full save/load system. It runs offline on Android (ARM64, Android 10 or
later) and on Windows.

**Performance budget on a mid-range Android device** (Snapdragon 7-series or Dimensity 7xxx,
6 GB RAM):
- 60 FPS target with 30 FPS as the floor.
- Under 1.2 GB of RAM.
- Fewer than 150 draw calls and fewer than 300k visible triangles.
- Install size under 1.5 GB.

---

## Phase 1: Foundation (weeks 1–4)
**Goal:** move, fight and save in a grey-box level.

- [ ] Create the Unity 2022.3 LTS project with URP's Mobile renderer, IL2CPP, ARM64, Vulkan with a GLES3 fallback, and Active Input Handling set to "Both".
- [ ] Install the packages in `Packages/manifest.json`. Set up Addressables groups: `Chunks_Meadows`, `Characters`, `Wardrobe`.
- [ ] Bring up the core scripts: GameEvents, ContentSettings with the age-gate UI, and DeviceTier.
- [ ] Build a grey-box test arena with a NavMesh. Wire up ClickToMoveMotor, IsometricCameraRig and TouchInputController.
- [ ] Combat MVP: one sword ComboDefinition (3-hit chain), 2 abilities, Hitbox, Hurtbox, Health, a training dummy, and one EnemyAI (Giant Frog).
- [ ] SaveLoadManager with 12 manual slots, quick-save and 3 rolling auto-saves. Include a corrupt-file test and an Export/Import round trip on a real device.
- **Exit test:** on a mid-tier phone, walk, kill 5 frogs, quick-save, kill the app, relaunch, and quick-load into the same state.

## Phase 2: Characters and animation (weeks 5–10)
**Goal:** the look and feel of the game.

- [ ] Toon shader in URP: cel ramp, rim light, outline (inverted hull, disabled on the Low tier), and an anime eye/face shader with SDF face shadows.
- [ ] Player and 4 companions built in VRoid Studio and set up with `IsekaiChaos ▸ Characters ▸ Setup…` (see `VROID_PIPELINE.md`).
- [ ] Base humanoid body split into BodyRegion meshes. Male and female bases, plus race parts (elf ears, horns, wings, wolf/fox/cat ears and tails with spring-bone physics).
- [ ] CharacterAppearanceController sliders and the avatar creator UI.
- [ ] LayeredWardrobeController with 20 starter garments across the 5 layers, torn variants, and occlusion verified (no skin poke-through).
- [ ] Animator setup according to `ANIMATOR_SETUP.md`. Import and retarget the locomotion, combat, reaction and interaction clips.
- [ ] InteractionController: benches, chests, doors, crafting stations, and 3 paired companion emotes.
- [ ] HitStop, camera shake, slash VFX and the footstep system.
- **Exit test:** create a character, change outfits, fight with a full combo chain, sit on a bench and high-five a companion, all at 60 FPS on the mid tier.

## Phase 3: World, gathering and base (weeks 11–16)
**Goal:** Starter Meadows as a living sandbox.

- [ ] Block out the Starter Meadows map: Guild Town, the base plot, Frog Marsh, Slime Hills, and the start of the King's Debt Highway. Split it into 128 m chunks and set up WorldBiomeManager streaming.
- [ ] Highways with RoadSpeedZone, a NavMesh "Road" area, and waypoint shrines.
- [ ] Place T1–T3 ResourceNodes (wood, fiber, hide, stone) with depletion stages. Set up GatheringController tool tiers and the gather animations.
- [ ] BaseBuildingManager: shack, Forge, Tavern, Bathhouse and Storage, with grid placement UI. RoadBuilder with dirt and gravel auto-tiling.
- [ ] EconomyManager: Guild Town market, debt and fines UI, and passive base income.
- [ ] LOD groups, occlusion culling bakes, GPU instancing for foliage, lightmaps plus light probes, and impostors for distant trees.
- **Exit test:** a 30-minute play session with stable memory (checked in the Profiler), no streaming hitches over 50 ms, and a save/load that restores buildings and roads.

## Phase 4: Story, companions and romance (weeks 17–22)
**Goal:** Act 1 playable from start to finish.

- [ ] StoryProgressManager and the quest journal UI (Main, Companion, Bounties, Contracts, Completed) with map waypoints.
- [ ] Author Act 1 chapters 1–5 as QuestDefinitions with branching choices and alignment flags.
- [ ] 4 NPCProfiles (Bruni, Seraphine, Mireille, Corvin), their schedules, gifts, and companion arc stages 1–2.
- [ ] NPCRomanceController and the relationship UI (Affinity, Trust, Lust and Mood meters, plus "why is this locked?" reasons).
- [ ] IntimacySessionController and its menu, behind the content gate and consent checks. Theme filters in Settings.
- [ ] Companion AI: follow, use abilities, and assignment to base jobs. Flaw mechanics hooked into WorkerEfficiency.
- [ ] Slime King boss fight with the pre-fight auto-save.
- **Exit test:** play Act 1 on both alignment paths. Every quest state survives save/load. With mature content off, no gated content is reachable.

## Phase 5: Polish, optimization and release (weeks 23–26)
**Goal:** ship v1.0 of the slice.

- [ ] Device matrix testing: a low-tier device (4 GB RAM), a mid tier and a high tier, plus Windows. Tune the DeviceTier thresholds.
- [ ] Use Memory Profiler and Frame Debugger passes to hit the budgets above. Use ASTC texture compression, crunch the UI atlases, and stream the audio.
- [ ] HUD polish: ability cooldown radials, cast bar, damage numbers, minimap, and collapsible menus.
- [ ] Save migration test: load v1 fixture saves in the release build. Export/Import on Android 10, 13 and 14.
- [ ] Localization hooks (English first), accessibility (text size, colorblind-safe aim indicators), and audio mix.
- [ ] Build pipeline: a signed APK and AAB, a Windows build, and a crash reporter.
- **Exit test:** 2 hours of play with no crashes. 60 FPS on the mid tier with 30 FPS as the floor on the low tier. All saves load across a reinstall using Export/Import.

---

### After the slice (v1.x and beyond)
Next come the Beastkin Forest and Caravan Desert biomes, then Acts 2–3. After that: the 4
remaining biomes, the Demon Generals, mounts, caravan trading routes, and multiple endings.
