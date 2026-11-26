# Art Style Guide

**Target look:** 3D anime cel-shading in the family of *Genshin Impact*, with the cinematic
lighting and material detail of *Neverness to Everness* (NTE) on high-end devices.

These games are **style references only**. Do not copy their characters, outfits, logos,
UI layouts, assets or music. All designs must be original.

Visual summary: [`previews/style-board.png`](previews/style-board.png).

---

## 1. Pillars
1. **Readable silhouettes.** Each character should be recognizable in black silhouette at 64 px tall, which is our isometric camera distance.
2. **Bright, saturated, optimistic.** Shadows are tinted lilac or blue, never grey or black. This is a comedy, so the world should look like a postcard.
3. **Anime faces, grounded worlds.** Characters are fully cel-shaded. Environments are stylized but solid, with painterly textures and soft volumetric light.
4. **Same style on every tier.** Lower tiers drop effects (bloom, AO, outlines on props) but never change the character shading.

## 2. Character proportions

| Type | Head-to-body | Notes |
|---|---|---|
| Adult heroine | 1 : 6.5–7 | Long legs (≈ 3.5 heads), slim neck, slightly large hands for readability |
| Adult hero | 1 : 7–7.5 | Broader shoulders (≈ 2 heads wide), defined jaw |
| Tall / imposing (Bruni, ogres) | 1 : 7.5–8 | Wolf-kin are taller and bulkier |
| Chibi | 1 : 2.5 | **Only** for UI emotes and stickers, never in-world |

- **Eyes:** large, about 1/5 of face height. They use a layered iris (dark top gradient, bright lower highlight, two specular dots), and an eyelash shape drawn as one mesh piece.
- **Mouth and nose:** minimal. The nose is a single shading cue from the SDF face shadow. The mouth is a line plus blendshapes.
- **Hair:** large clumped strands, an "angel ring" specular band, and 2–3 physics chains (bangs, side locks, ponytail).
- **Every character is an adult.** Facial design and body proportions must read as adult.

## 3. Shading spec (`Assets/Art/Shaders/AnimeToon.shader`)

| Feature | Setting | Notes |
|---|---|---|
| Cel ramp | 2 bands, threshold 0.05, softness 0.03–0.06 | Skin can go softer (0.08) |
| Shadow tint | Skin `#C9A9C9`, cloth `#B7AFD6`, hair `#9C8FC8` (multiplied) | Shadows lean lilac in day and blue at night |
| Face shadow | SDF texture per face (`_FACE_SDF`) plus `FaceShadowDriver` | No faceted shadows on faces |
| Light map | R = AO bias, G = spec mask, B = ramp offset | Painted per texture set |
| Rim light | Power 4, strength 0.5–0.7, warm `#FFEED9` | Stronger on the side the sun lights |
| Hair spec | Band size 0.10–0.14, masked by the light map G channel | |
| Outline | Characters 1.0–1.4, props 0.6, terrain none | Color is the darkened albedo, never pure black. Vertex alpha thins lines at fingers and hair tips |
| Emission | Runes, magic, eyes during skills | HDR, feeds bloom on Mid and High |

## 4. Per-tier rendering

| Feature | Low (Genshin-lite) | Mid (Genshin) | High (NTE-like) |
|---|---|---|---|
| Resolution scale | 0.75 | 0.9 | 1.0 + MSAA 2× / TAA on PC |
| Character outline | ✓ | ✓ | ✓ |
| Prop outlines | ✗ | ✓ | ✓ |
| Main light shadows | 1 cascade, 20 m | 2 cascades, 35 m | 4 cascades, 60 m, soft |
| Bloom | ✗ | Low quality | HDR, with lens dirt |
| SSAO | ✗ | ✗ | ✓ |
| Color grading LUT | ✓ (baked) | ✓ | ✓ plus per-biome volumes |
| Depth of field (dialogue/cutscenes) | ✗ | Gaussian | Bokeh |
| Volumetric light shafts | Sprite cards | Sprite cards | Raymarched (PC) |
| Water | Flat with scrolling normals | Plus depth fade and foam | Plus SSR on PC |
| Grass | Instanced cards, 25 m | 40 m with wind | 60 m, interactive bending |

## 5. Color palettes

**UI:** ink `#1B1630`, panel `#221B47`, gold accent `#FFD25E`, HP `#FF4F6D`, energy `#4FC3FF`, heal `#9EFFA5`.

| Biome | Key colors | Sky / light mood |
|---|---|---|
| Starter Meadows | `#7CC35E` `#B9E58A` `#F7A8C8` `#E6D9C2` | Clear blue, warm noon sun |
| Beastkin Forest | `#2F7D5B` `#78E0B8` `#B4FFE0` `#6B4A31` | Dappled green light, glowing spores at dusk |
| Caravan Desert | `#E8C27A` `#D9874A` `#FFE6B0` `#3FA9C8` | Hot white sky, long magenta sunsets |
| Hot Springs Coast | `#3FD0E8` `#FFF1D6` `#FF9F7A` `#2F8FD6` | Turquoise water, steamy soft haze |
| Frostpeak | `#DDEBFF` `#8FB7E8` `#5A6FA8` `#FFFFFF` | Low blue-white sun, aurora at night |
| Volcanic Wastes | `#2A1A2E` `#FF5A3C` `#FFB13D` `#6D2A3A` | Smoky red sky, lava glow from below |

## 6. Environment rules
- **Textures:** hand-painted albedo with gradients baked in and minimal noise. One 2K atlas per building kit, and 512px tiling terrain layers.
- **Shapes:** chunky and slightly exaggerated, such as fat roof eaves, tilted chimneys and oversized lanterns. The comedy should show in the props.
- **Foliage:** clumpy canopy cards with normals pointed outward (spherical normals) so trees cel-shade as soft blobs.
- **Skies:** a painted gradient skybox with 2–3 scrolling cloud layers and a stylized sun disk. Time of day blends between 4 key palettes.
- **Landmarks:** one hero landmark per biome, visible from far away, such as the Guild clocktower, the volcano or the Frostpeak spire.

## 7. VFX
- **Hit sparks:** white core, color fringe by element, and 3–5 frame hitstop. Every damage type has its own color.
- **Slash trails:** a mesh trail with a gradient from white to the element color, fading out over 0.15 s.
- **Comedy effects:** sweat drops, anger marks, sparkles and "doki-doki" hearts appear as billboard sprites over heads.

## 8. What to commission first
1. A heroine and hero base body (1:7) with BodyRegion splits, plus 3 face presets with SDF maps.
2. Bruni turnaround sheet (front, side, back) and her expression sheet (6 emotions).
3. The Starter Meadows building kit: walls, roofs, doors, windows and fences, from the Guild Hall down.
4. One biome lighting test scene to sign off on all 3 tiers before any mass production.
