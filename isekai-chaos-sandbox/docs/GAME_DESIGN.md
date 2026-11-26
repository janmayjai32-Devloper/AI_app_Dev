# Game Design: Isekai Chaos Sandbox

This is an offline, single-player game. The controls and the interactive world follow the
Albion Online model. The tone is comedy parody.

---

## 1. Controls: two modes that share the same gameplay

Players switch between the modes in **Settings → Controls** (`ControlModeManager`). Both modes drive the same movement, combat and interaction code, so saves, balance and animation are identical.

### A. Direct control (default): Genshin / NTE style
| Action | Touch (Android) | PC |
|---|---|---|
| Move | Floating joystick on the left half of the screen (push lightly to walk) | WASD (Ctrl toggles walking) |
| Camera | Drag on the right half of the screen. Pinch with two fingers to zoom | Mouse (cursor locked), wheel to zoom |
| Attack | Big ⚔ button. Tap repeatedly to combo. It locks on to the nearest enemy in front | Left click |
| Jump | ⤒ button | Space |
| Dodge / Sprint | 💨 button: tap to dodge (i-frames), hold to sprint | Shift: tap to dodge, hold to sprint |
| Abilities | 6 buttons in a ring around Attack | 1 – 6 |
| Interact | A "Sit / Open / Gather / Talk" prompt appears when you're near something | F |
| Menus | HUD buttons | Hold Alt to free the cursor |

`ThirdPersonCameraRig` follows over the shoulder. It avoids walls with collision, recenters behind you while you run, and frames your lock-on target during fights.

### B. Tap-to-move: Albion Online style
| Action | Touch (Android) | PC |
|---|---|---|
| Move | Tap the ground. Hold to keep walking toward your finger | Right-click |
| Target an enemy | Tap the enemy. Your character chases it and auto-attacks | Left-click |
| Interact | Tap it. Your character walks over, lines up and plays the animation | Left-click |
| Abilities | Tap to smart-cast, or drag from the button to aim | Q W E R D F at the cursor |
| Camera | Fixed isometric view. Pinch to zoom, two-finger twist to rotate | Mouse wheel |

**Your gear is your build.** In both modes, a weapon grants a pool of abilities for slots 1–3 (Q, W, E), and each armor
piece grants its own slot. Players choose one ability per slot on the gear screen. Changing
outfits in the 5-layer wardrobe therefore changes your build as well as your look.

## 2. Combat and animation

The code for this section is in `CombatController`, `AttackDefinition`, `AbilityDefinition`,
`Hitbox`, `Health`, `HitStopController` and `EnemyAI`.

- **Auto-attack chains.** Each weapon has a `ComboDefinition` with a basic-attack chain of
  3–4 animated swings. Each swing has its own hit window, root-motion lunge, hitstop and
  camera shake.
- **Ability flow.** An optional cast time is shown on a cast bar. Then the animation plays,
  the hit window opens the right hitbox (weapon, fist, tail), and reactions follow.
  Targeting modes are unit, ground AoE, skillshot, dash (with i-frames) and self.
- **Input buffering.** An ability pressed late in an animation fires at that animation's
  cancel frame, so combat feels responsive on touch screens.
- **Hit reactions.** Directional flinches use a 2D blend tree. Breaking poise causes a stagger.
  Heavy attacks cause a knockdown or launch, with knockback.
- **Clothing damage.** Hits hurt the outermost garment on the body region that was struck,
  moving it from Intact to Torn to Stripped. The mature-content gate caps this so it never goes
  below the undergarment layer unless the player has opted in.
- **Comedic defeat.** You wake up in the Guild Infirmary, lose 10% of the gold you carried, and
  get billed for the rescue. Defeat never leads to romance or intimacy content.
- **Enemies** run a cheap 5 Hz state machine (patrol, chase, attack, leash back home) and use
  the same combat code as the player.

**Interactive motion.** This is handled by `InteractionController` and `Interactable`.
Sit, lie down, open a chest or door, pick up, gather, craft at a station, and bathe in a hot
spring. There are also **paired animations** with companions: high-five, hug, hand-hold,
head-pat, ear-scratch for beastkin, and dancing at the tavern. The flow is: walk to the anchor,
line up, play the enter → loop → exit animations, use hand and look IK on targets, and trigger
gameplay at the animation's contact frame.

## 3. The world of Gantia: map and roads

The world is a hand-designed open world, streamed in 128 m chunks. Towns are connected by
**highways** (`RoadSpeedZone`, +20% move speed, preferred by NavMesh). Off-road areas are
slower but hold the richer resource nodes and the dungeons.

```
                          ❄ FROSTPEAK MOUNTAINS (T5–T7)
                     Dwarfhold Mines ─── Ice Giant's Throne (boss)
                              │  Switchback Pass
   🌲 BEASTKIN FOREST (T2–T5) │                       🌋 CRIMSON VOLCANIC WASTES (T6–T8)
   Treehouse Village ─────────┼───── Ashgate Outpost ── Demon General's Keep ── Dragon Caldera
         │  Mossroad          │            │ Obsidian Bridge
         │                    │            │
   ══════╪═══ KING'S DEBT HIGHWAY ═════════╪════════════════════
         │                    │            │
   🌾 STARTER MEADOWS (T1–T3)  │       🏜 GOLDEN CARAVAN DESERT (T3–T5)
   Guild Town ◉ ── Your Base Plot          Oasis Bazaar ── Bandit Gulch
   Frog Marsh · Slime Hills    │           Silk Road (caravan escort route)
                              │
              Seaside Road ───┘
                     🌊 AZURE HOT SPRINGS COAST (T3–T6)
                     Port Lumière ── Volcanic Resort Springs ── Siren Cove
```

| Biome | Tiers | Signature resources | Interactive set pieces |
|---|---|---|---|
| Starter Meadows | T1–T3 | Wood, fiber, hide (frogs) | Guild board, training dummies, frog marsh, the base plot |
| Beastkin Forest | T2–T5 | Glowwood, herbs | Treehouse lifts, rope bridges, beastkin festivals |
| Caravan Desert | T3–T5 | Silk, stone, sand glass | Caravan escorts, a bazaar with haggling, sandstorms |
| Hot Springs Coast | T3–T6 | Pearls, salt, fish | Hot springs (bathe interaction), docks, a ferry |
| Frostpeak | T5–T7 | Ore, crystal | Mine carts, ice slides, avalanche hazards |
| Volcanic Wastes | T6–T8 | Obsidian, dragonbone | Lava geysers, crumbling bridges, Demon General keeps |

**Map design rules for level designers:**
1. Every town sits on a highway. Every highway crossing has a waypoint shrine (fast travel and auto-save).
2. Resource tiers rise the farther a spot is from a road, so players trade safety and speed for value.
3. One landmark should be visible from every chunk (volcano, Frostpeak, the Guild Town clocktower) so players can orient without the map.
4. Chunk borders never cut through buildings, and every chunk carries its own NavMesh.
5. Small comedy props and interactables go everywhere: suspicious barrels, a bench that collapses, a cursed signpost.

## 4. Gathering and crafting

This is handled by `ResourceNode` and `GatheringController`.

- Resource types are wood, ore, fiber, hide, stone, herbs, crystal and silk, in tiers **T1–T8**.
- Your tool tier must be at least the node tier minus one, or the swing bounces off with a funny line.
- Each swing animation's contact frame removes one charge. The node's mesh shrinks through
  full → half → stump, and it respawns on a timer.
- Gathered materials feed the Forge, Alchemy Lab and Tailor at your base, and supply levels in
  the regional markets.

## 5. Base building and roads

This is handled by `BaseBuildingManager` and `RoadBuilder`.

- You start on a grid plot with a rundown shack. Plot upgrades grow the grid from 6×6 to 10×10, 14×14 and 20×20.
- Buildings: Forge, Alchemy Lab, Tailor, Tavern, Bathhouse, Private Suites, Storage and Stables.
  Each has upgrade tiers and worker slots for recruited NPCs, who generate passive income.
  Their flaws modify how efficient they are.
- **Road building.** Drag across the grid to lay a path. Each cell automatically picks its shape
  (end, straight, corner, T-junction or crossroads). Material tiers are dirt, gravel, cobblestone
  and enchanted marble. Roads add movement speed and base charm, and charm brings visitor events.
- Placement, upgrades, workers and roads are all saved.

## 6. Economy

This is handled by `EconomyManager`.

- Every region has its own supply and demand for each good. Prices follow
  `base × (demand/supply)^elasticity`, so goods are cheap in the region that produces them.
  Markets drift back toward normal each day.
- Quest outcomes change supply. For example, clearing Bandit Gulch raises silk supply in the Desert.
- **Guild Debt.** You start owing 1,000,000 g as the summoning processing fee, with daily
  interest. **Collateral Damage Fines** are the comedic money sink.

## 7. Core companions (vertical slice)

All companions are adults. `NPCProfile` enforces a minimum age of 18 in the editor and at
runtime. Each one follows the Strength + Fatal Flaw template.

| NPC | Race / age | Strength | Fatal flaw | Base job |
|---|---|---|---|---|
| **Brunhilde "Bruni" Ashfang** | Wolf-kin, 27 | Forges legendary gear | Only works after winning a drinking contest. Her efficiency is 0 until you beat her. | Forge |
| **Seraphine Lux** | Human paladin, 24 | Unbreakable shield, big heals | Apologizes to enemies and heals them too | Infirmary / Bathhouse |
| **Mireille Goldtail** | Succubus merchant, 230 (looks 25) | +30% shop profits | Skims gold when loyalty falls below 40 | Tavern / Market stall |
| **Corvin Ashmoor** | Half-elf necromancer, 31 | Summons skeleton workers | His skeletons unionize and demand breaks | Storage / Mine |

Each companion has a 4-stage arc. It reveals their backstory and ends with a choice to
**cure** or **embrace** the flaw, which changes their skills. It also unlocks relationship
milestones.

## 8. Act 1 outline: "Debt on Arrival"

| Ch. | Title | Beats | Branch flags |
|---|---|---|---|
| 1 | Form 27-B Was Stamped Wrong | A goddess of bureaucracy summons you by clerical error. You arrive in debt. You register at the Guild. | — |
| 2 | Frog Season | Your first bounty: giant frogs. Tutorial for combat, gathering and roads. | Heroic (free the swallowed adventurer) or Greedy (loot them first) |
| 3 | Fixer-Upper | Claim the shack, build the Forge, recruit Bruni through a drinking contest. | — |
| 4 | Assemble the Disaster Squad | Recruit Seraphine, Mireille and Corvin. Each recruitment is a short quest. | Chaos Gremlin (blow up the bridge) or a careful approach |
| 5 | The Slime King's Herald | A boss fight with a pre-fight auto-save. A messenger from the first Demon General arrives. | The ending meters start to matter |

## 9. Content policy (built into the code)

- **Off by default.** Mature content requires the age-gate confirmation (`ContentSettings`).
  That setting is stored on the device and is never included in a save file.
- **Adults only.** Every romanceable character is 18 or older, enforced in `NPCProfile`.
- **Mutual, revocable consent.** Every intimate scene goes through
  `NPCRomanceController.EvaluateConsent` (affinity, trust, mood, unlocked milestone, location,
  and the player's theme filters). The NPC can stop a scene if their mood drops, and the player
  can stop it at any time.
- **No non-consent.** Defeat and capture events are always non-sexual comedy beats.
- **Theme filters.** Players can turn individual content tags off. A disabled tag hides the scene.
- With mature content off, garments never strip below the undergarment layer.
