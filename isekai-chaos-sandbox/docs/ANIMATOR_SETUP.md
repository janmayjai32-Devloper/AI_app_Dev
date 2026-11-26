# Animator Setup Contract

The code drives one shared humanoid Animator Controller, called `Humanoid_Base.controller`.
Each weapon swaps clips through an **Animator Override Controller**, which is referenced from
its `ComboDefinition`. Clips come from Mixamo or your own mocap, retargeted to the shared
Humanoid avatar.

## Layers

| Index | Name | Blend | Contents |
|---|---|---|---|
| 0 | Base | — | Locomotion blend tree (Speed: idle 0, walk 2.2, run 5.5, sprint 8), strafe 2D blend (MoveX/MoveY), hit reactions, death |
| 1 | Combat | Override, upper+lower body | Attack and ability states. Their names must equal `AttackDefinition.animatorState` |
| 2 | Interaction | Override, IK Pass ON | Interaction enter, loop and exit states, named in each `Interactable` |

## Parameters

| Name | Type | Set by |
|---|---|---|
| `Speed` | float | ClickToMoveMotor, EnemyAI |
| `MoveX`, `MoveY` | float | ClickToMoveMotor |
| `Grounded` | bool | ClickToMoveMotor |
| `Jump` | trigger | ClickToMoveMotor (jump start → air loop → land) |
| `VerticalSpeed` | float | ClickToMoveMotor (blends rising, falling and landing) |
| `AttackSpeed` | float | CombatController (use it as the speed multiplier on Combat states) |
| `Casting` | bool | CombatController (cast-bar idle loop) |
| `Hit` | trigger | Health |
| `HitReaction` | int (0 none, 1 flinch, 2 stagger, 3 knockdown, 4 launch) | Health |
| `HitDirX`, `HitDirY` | float | Health (2D blend tree of directional flinches) |
| `Dead` | bool | Health |
| `Intensity` | float | IntimacySessionController (scene loop speed/blend) |

## Animation events

Add these on the clip import tab. They are received by `AnimationEventRelay` on the model GameObject.

| Function | Param | Use |
|---|---|---|
| `OnFootstep` | "L"/"R" | Dust VFX, SFX by surface |
| `OnVfx` | addressable key | Slash trails, spell bursts |
| `OnSfx` | key | Whooshes, grunts |
| `OnContact` | id | Interaction sync point: axe hits the tree, lid opens, hands meet in a high-five |

Hit windows are **not** animation events. They come from normalized times in `AttackDefinition`,
so retiming a clip never desyncs damage.

## Starter clip list (vertical slice)

- **Locomotion:** idle (3 variants), walk, run, sprint, strafe ×4, turn-in-place, jump start, air loop, land (soft and hard), and a stop-skid.
- **Sword:** light ×4 chain, heavy ×2, a dash slash, a spin AoE, block, and a dodge roll.
- **Staff:** cast loop, projectile throw, ground AoE slam.
- **Reactions:** flinch F/B/L/R, stagger, knockdown and get-up, launch, death, and a comedic faint.
- **Interactions:** sit (bench, chair, ground), lie down, open a chest, open a door, pick up,
  chop, mine, sickle, skin, craft at an anvil, stir a cauldron, and enter, sit and exit a bath.
- **Paired:** high-five, hug, hand-hold walk, head-pat, ear-scratch, tavern dance, and a drinking-contest chug.
- **Emotes:** wave, facepalm, victory pose, sulk, and the dramatic isekai-protagonist point.
