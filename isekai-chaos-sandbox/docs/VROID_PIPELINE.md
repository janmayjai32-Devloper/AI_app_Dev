# VRoid → Unity Pipeline: real 3D anime characters

This guide gets real, animated 3D anime characters running in the project, using free tools.
At the end you'll have Kazuya walking, jumping and attacking with the direct controls (WASD or
joystick, orbit camera). You'll also have a real screenshot instead of a vector mockup.

**Time:** about 1–2 days the first time. After that, roughly 30 minutes per new character.

---

## 0. Tools (all free)

| Tool | What for | Where |
|---|---|---|
| **Unity 2022.3 LTS** | The engine. Install the Android Build Support module too | Unity Hub |
| **VRoid Studio** | Anime character creator: hair, face, body sliders, outfits | vroid.com/en/studio (Windows / macOS / Steam) |
| **UniVRM** (VRM 1.0 package) | Imports `.vrm` files into Unity, with spring-bone physics and expressions | github.com/vrm-c/UniVRM → Releases |
| **Mixamo** | Free humanoid animations: walk, run, attacks, reactions | mixamo.com (Adobe account) |
| **Unity Starter Assets: Third Person** | Free locomotion clips (idle, walk, run, jump, land) | Unity Asset Store |
| Blender (optional) | Custom clothing layers, props and weapons later on | blender.org |

## 1. Open the project in Unity

1. In Unity Hub: **Add → select the `isekai-chaos-sandbox` folder**, then open it with 2022.3 LTS.
2. When it asks about the input system, choose **Both**. If it doesn't ask, set it in Project Settings ▸ Player ▸ Active Input Handling.
3. Let the packages in `Packages/manifest.json` resolve: URP, Addressables, Newtonsoft JSON and AI Navigation.
4. **Set up URP:**
   1. Create ▸ Rendering ▸ URP Asset (with Universal Renderer).
   2. Assign it in Project Settings ▸ Graphics and in ▸ Quality.
5. **Add layers** in Project Settings ▸ Tags and Layers: `Character`, `Hurtbox`, `Ground`, `Interactable`.
6. **Expect compile errors on first open.** No compiler was available when this code was written, so this is its first build. Fix them now or send me the Console output.

## 2. Make the characters in VRoid Studio

Make **Kazuya** (the player), **Bruni**, **Seraphine**, **Mireille** and **Corvin**, using the
descriptions in `GAME_DESIGN.md` §7.

Tips to get closer to the Genshin/NTE look:
- **Body:** use about 7 heads tall. Raise leg length and lower head size a little from the defaults.
- **Eyes:** pick a larger iris with a two-tone gradient and a strong highlight.
- **Hair:** use big clumped strands and add 1–2 long locks (bangs, side tails), which become spring-bone physics.
- **Beastkin (Bruni):**
  - Ears: use the hair editor's "animal ears" accessory, or model them in Blender.
  - Tail: VRoid has no tail, so make it in Blender and parent it to the Hips bone. Give it a spring bone chain.
- **Mobile export settings** (Camera/Export ▸ Export ▸ VRM 1.0):

  | Setting | Player / companions | Background NPCs |
  |---|---|---|
  | Reduce polygons | ~25k triangles | ~12k triangles |
  | Material count reduction | 8 materials | 4 materials |
  | Texture atlas | 2048 | 1024 |
  | Reduce blend shapes | Keep only the expression presets | Same |

- **Save to** `isekai-chaos-sandbox/Assets/Art/Characters/VRoid/<Name>.vrm`.

## 3. Import into Unity

1. Import the UniVRM `.unitypackage` (VRM-1.0 version) from its GitHub Releases page.
2. Add **`ICS_UNIVRM`** to Project Settings ▸ Player ▸ Scripting Define Symbols. This turns on `VrmExpressionController`.
3. Unity auto-imports each `.vrm` into a prefab. If the material looks pink, set UniVRM's material generation to URP. The import settings and recent UniVRM versions support this.
4. Drag `Kazuya.prefab` into a new scene, `Assets/Scenes/Prototype_Meadows.unity`.

## 4. One-click game setup

1. Select the Kazuya instance and run **IsekaiChaos ▸ Characters ▸ Setup Selected As Player**.

   The tool:
   - wraps the model in a root object with a CharacterController sized to the skeleton, and a NavMeshAgent
   - adds Health, CombatController, HitStop, the motor, interaction, gathering, road speed, the defeat handler and the save binder
   - builds 12 hurtbox capsules on the bones, mapped to body regions so clothing takes damage where it's hit
   - adds a weapon socket and a sword hitbox on the right hand (move the `Tip` child to your blade length)
   - adds AnimationEventRelay and assigns `Assets/Animation/Humanoid_Base.controller`, if that file exists
2. Do the same for companions (**Setup Selected As Companion**) and for a test enemy (**Setup Selected As Enemy**).
3. **Optional: switch to our anime shader.** Use **IsekaiChaos ▸ Characters ▸ Convert Selected Character Materials To AnimeToon**.
   - It converts VRoid's MToon materials to `AnimeToon`. Hair gets the shine band, and eyes never shade.
   - It keeps transparent parts (eye highlights, lashes) on MToon.
   - Compare both shaders and keep whichever looks better. MToon is already good.
4. **Expressions:** add `VrmExpressionController` to the model, the object that has `Vrm10Instance`. It then blinks, makes a pain face when hit, and a happy face on finishers.

## 5. Animations

1. **Locomotion:** import *Starter Assets – Third Person* from the Asset Store and copy its humanoid clips (Idle, Walk, Run, JumpStart, InAir, Land).
2. **Combat and interactions:** from Mixamo, download each clip as **FBX for Unity**, **Without Skin**, at **30 fps**:
   - **Sword:** "Sword And Shield Slash" ×3, "Great Sword Slash", "Stand To Roll" (for the dodge)
   - **Reactions:** "Hit Reaction", "Standing React Large", "Dying"
   - **Interactions:** "Sitting", "Opening", "Picking Up", "Hammering" (Bruni's forge), "Chopping"
   - **Paired:** "High Five" (×2 sides), "Hug" and "Waving"
3. Select all the clip FBXs and set **Rig ▸ Animation Type: Humanoid**, Avatar: *Create From This Model*. Retargeting onto VRoid bodies is automatic.
4. **Build `Assets/Animation/Humanoid_Base.controller`** exactly as described in `ANIMATOR_SETUP.md`:
   - layers Base, Combat and Interaction
   - parameters `Speed`, `MoveX`, `MoveY`, `Grounded`, `Jump`, `VerticalSpeed`, `Hit`, `HitReaction`, `Dead` and so on
   - a locomotion blend tree on Speed at 0, 2.2, 5.5 and 8
   - Combat layer state names that match each `AttackDefinition.animatorState` (for example `Light_1`, `Light_2`, `Light_3`)
5. Create the data assets in `Assets/Data`:
   - `ComboSet_Sword` (3 light attacks)
   - 2 `AbilityDefinition`s, one of them a Dash in slot F for the dodge
   - assign them to Kazuya's CombatController

## 6. Grey-box level and controls

1. **Ground:** add a Plane (scale 20) on the `Ground` layer. Add a few cubes and capsules as trees and houses.
2. **NavMesh:** add a `NavMeshSurface` component and click **Bake**. AI Navigation needs this for enemies and tap-to-move.
3. **Cameras:** create two camera rigs.
   - `ThirdPersonCameraRig`: an empty object with a Camera child. Set its target to Kazuya.
   - `IsometricCameraRig`, for the Albion mode.
4. **Controls:** create a Canvas with `VirtualJoystick` on the left 45% of the screen, plus Attack, Jump and Dodge buttons wired to `DirectControlInput`.
5. **Mode manager:** add `ControlModeManager` and drag in the references.
6. **Core managers:** add one GameObject holding `SaveLoadManager`, `EconomyManager`, `StoryProgressManager` and `NPCRegistry`.
7. **Press Play.** WASD moves, the mouse orbits, Space jumps, left-click attacks and Shift dodges or sprints.

## 7. Android test build

1. **File ▸ Build Settings ▸ Android ▸ Switch Platform.**
2. **Player settings:**
   - Scripting backend: IL2CPP, ARM64 only, minimum API level 29 (Android 10)
   - Graphics APIs: Vulkan, then OpenGLES3
   - Texture compression: ASTC
3. **Build & Run** to a phone with USB debugging on. The touch joystick and camera drag work on the device.
4. **Profile** with Window ▸ Analysis ▸ Profiler and Autoconnect. Targets for the vertical slice:
   - 60 FPS
   - under 150 draw calls
   - one VRoid character costs about 3–6 draw calls after material reduction

## 8. What the characters can and can't do

| Supported in this pipeline | Needs extra work later |
|---|---|
| Anime face and body, hair physics, expressions, blinking | Tails (Blender, then a spring bone) |
| All Humanoid animations: Mixamo, Starter Assets, your own mocap | Custom outfits as separate **layers** for the 5-layer wardrobe. VRoid bakes most clothing onto the body mesh, so a full outfit counts as one "Clothing" set for now. Real separable layers need garments modeled in Blender on the VRoid skeleton |
| Combat hitboxes and hurtboxes, interactions with IK | SDF face shadows (paint or generate one face SDF texture) |
| Day/night lighting with MToon or AnimeToon | NTE-level fidelity on PC (high-poly hair cards, hand-painted textures from an artist) |

## 9. Once the first screenshot works

Send me any of these and I'll fix the problem or tune the settings:
- **the Console errors** from the first compile
- **a screenshot** of Kazuya in the grey-box scene
- **how it feels:** camera too close? turning too slow? dodge too short?
