# How the conversion works

This explains **what changes when a stage moves from one game to the other**, and the pitfalls that came up while porting the mod's converter
from C++ to C#.

---

## The two directions

**French-Bread → BBCF / BBTAG (export).** Reads the `bg.fbx.bin` (or DFCI's text FBX, or MBAACC's
sprites), converts it to `MUA` + `MMOT` + `EVB`, packs it the way each game wants and **replaces a
stage that already exists**, because neither game takes a new stage. The first install over a stage
backs up its three files, and **Restore** puts them back byte for byte.

**BBTAG / BBCF / P4U2 → UNI2 (import).** Reads the three `.pac` files, takes the model apart and
writes a UNI2 Improvement Mod stage folder: `bg.fbx.bin`, the DDS files, the `stage.txt` and, when
there is one, the 2D layer. That folder goes into `UNI2-IM\Mods\bg\bgNNN` under the next free number,
and the mod picks it up when the game starts. **It doesn't run without the IM**, because the lamps,
the flipbooks, the running water and the particles are done by the mod's runtime.

---

## Units and framing

**A BBTAG character is 213 units tall.** BBTAG has a fixed fight camera: the eye 320 back, 100 up,
FOV 45. UNI2 has no fixed camera: each stage has `Scale`, `Position`, `FOV` and `ViewRotation` in the
`BgList`.

To export, FbxToMua matches the fight plane of the two games (half height `320 * tan(22.5°)` = 132.55
BBTAG units against 1 UNI2 world unit). Per vertex:

```
b = s * ( Sx*x + Px/a,  Sy*y + Py,  Pz - Sz*z )
s = 320 * tan(22.5°)
a = 16/9
```

`FOV` and `VanishingPoint` are dropped: they only move UNI2's eye, and BBTAG's eye is fixed.
`ViewRotationX` becomes the `0x28` of `base.evb`. `ViewRotationY` has nowhere to go and the program
warns about it.

Importing is the exact inverse, which is why a stage that goes there and back comes out the same.

### Reframing in the viewer

When that framing is wrong for a stage, the app's viewer lets you change it. BBTAG and BBCF move the
camera and the fighters together, so the viewer moves the stage around them instead, and the export
bakes it into the vertices (and the 2D layer) after the placement above:

```
b' = ( s*b - (side, height, -distance) ) * turnY(-turn)
```

`tilt` is added to `ViewRotationX` and goes into `0x28` in whole degrees, like before. The game
doesn't pitch the camera with it: it turns the whole stage about the world origin, and the viewer
does the same (see [`EVB`](arcsys-3d-files.md#evb--the-scripts)). With no reframe the output is the same,
byte for byte. The framing is kept per source stage in `%LOCALAPPDATA%\FbxToMua\reframes.json`.

## Mirror, UV, winding and colour

- **z is mirrored**, not rotated. BBTAG looks the opposite way from UNI2, and a 180° turn around Y
  swaps the stage's left and right, which is wrong.
- **V is flipped** (`v = 1 - v`). It once got flipped twice and every texture came out wrong in
  Blender.
- **Since mirroring swaps the face side**, triangles are rewritten as `(a, c, b)`.
- **Colour goes in BGRA.**
- **Normals go through the inverse transpose**, not through the matrix itself.
- Positions are computed in `double`: MBTL's `bg060` has a vertex at 36 thousand units cancelled by a
  node at -36.274, and in float the composition alone moved the stage by 1.4e-4.

## Moving nodes

A still node becomes a mesh with a single bone. A moving node becomes **a chain of bones, one per
animated ancestor**, each with the local matrix of its node.

Why not a single bone with the world matrix? Because some stages have **shear** in their animation:
`bg013` has an 800-frame non-uniform scale under a 1200-frame rotation, and an `S * R * T` bone can't
represent that. BBTAG composes `local * parent` per bone, so the chain reproduces it exactly. The
take's period becomes the LCM of the tracks (800 and 1200 give 2400), capped at 12,000 frames.

Moving nodes with the same period, blend and depth flags **share a skeleton**, its script and its
take, up to 32 bones; an animated ancestor they have in common is one bone. And meshes that come one
after the other in the draw order and use the same skeleton (or are still, with the same blend and
flags) become **one mesh**, so the draw order doesn't change. Both are there because the games keep
the stage's `.mmot` and `.evb` in fixed pools shared with the characters' effects; see
[Keep `scr.pac` and `mot.pac` small](arcsys-3d-files.md#keep-scrpac-and-motpac-small).

## The fighters' shadow

The games only draw the fighters' shadow on meshes whose skeleton doesn't have flag `0x100`, and the
shipped stages clear it on the floor alone. A still node loses it when it writes depth, isn't
additive, at least half of its area faces up, and an upward face passes under the fight line
(`z = 0`, 1500 units either side) within 10 units of the fighters' feet. Shining Colosseum's arena
is a bowl that climbs to 166 units at its rim, which is why the test looks under the fighters instead
of at the whole mesh. See
[The floor that takes the fighters' shadow](arcsys-3d-files.md#the-floor-that-takes-the-fighters-shadow).

BBTAG then tests each such mesh's box against the light, and a shadow leaning towards negative x
only reaches meshes that fit in a narrow strip of z. A floor that doesn't fit is cut, and only the
strip keeps the shadow; the viewer shows exactly that strip. See
[The loader's shadow test leans one way](arcsys-3d-files.md#the-loaders-shadow-test-leans-one-way).

The light is kept with the framing: the viewer's shadow **turn**, **length** and **strength** become
`base.evb`'s `0x1d` (`(-1000 sin turn, 1000 / length, -1000 cos turn)`) and `0x1f`
(`1000 * strength`). Left alone they write the shipped `(0, 900, -1000)` and `750`, as before, and the
viewer casts the stand-ins' shadows with the same light.

## Fog and bloom

UNI2 tints every stage with the `IsFog`, `FogStart`, `FogEnd` and `FogColor` of its `BgList`, and
adds a background bloom from its `BGBloom*` fields. BBTAG and BBCF have neither (their bloom is a
global debug setting, `LightBloomMode` and friends, next to `TextureViewer` in the debug menu), so
the export bakes both into the stage's textures, under their own names.

What UNI2 does with the fog was read from `uni2.exe` 1.40:

- `0x4ce506` builds the shader constant: with `IsFog` on, `g_FogCoord = (end / (end - start),
  -1 / (end - start))`, `end` being `start + 0.01` when the two are equal; with `IsFog` off,
  `(0, 0.01)` and a zeroed `FogColor`, which the shader reads as no fog.
- `fbxshader.txt` fogs when `FogColor.a >= 0.001`: `rgb = lerp(FogColor, rgb, x + w * y)`, alpha
  untouched, so the factor is `(end - w) / (end - start)` for a view depth `w`.
- The camera (`0x51b5ec`) sits at `(0, 0, 10)` looking at the origin, and the stage's world matrix is
  `Scaling(Scale) * Translation(Position)` (`0x51b862`), so `w = 10 - (Scale.z * z + Position.z)`.

Which depth UNI2 actually uses was settled against its own screenshots, not read from the code: the
stages that set `VanishingPoint` (UNI2's own, `bg012`, `bg014`, `bg016` to `bg027`) fog **by
distance**, which is what turns Moonlit Observatory's dark-blue sky purple (`w` about 178, factor
0.82); the ones that don't (the UNI1 ports, `bg001` to `bg011`, `bg013`, `bg015`, `bg090`) fog as
if every point were one unit away, so their factor is one constant, `(end - 1) / (end - start)` -
which is why `bg008`, factor 1.02, comes out warm instead of cyan. The fog value travels through a
`COLOR1` interpolator, which would clamp it to `[0, 1]`; why only one kind of stage gets clamped
isn't known.

So the export gives each texture one depth, the average `w` of the meshes that use it, and turns it
into `FogColor + (texel - FogColor) * factor`. That is exact for skies and far backgrounds, which are
what show the fog, and costs no extra textures. The 2D layer isn't fogged (UNI2 doesn't draw it with
`fbxshader`), nothing is done when the change stays under 6/255, and DFCI's fog and bloom are left
out because its `FogColor` means something else (see `GAMEDATA.md` in the UNI2 docs).

UNI2's bloom (`Shader/bg.txt`) is `pow(colour * BGBloomBlightness, BGBloomPower) * (BiassR, BiassG,
BiassB)`, blurred on a small buffer and added at `BGBloomAlpha`. The export approximates it inside
each texture: the glow is worked out on a quarter-size grid, blurred with the radius scaled the way
UNI2's 256-pixel buffer would, and added back. It can't spread from one mesh onto another, and it
works on the texture before the vertex colours that darken it in the game, so on stages like `bg008`
(power 0.8) and `bg014` (blue bias 3.5) the stage's own values came out far too strong. **The bloom
starts switched off**, with the stage's values filled in.

Both are set in the viewer, which shows them live and keeps them with the framing; **Reset fog** and
**Reset bloom** go back to the stage's own values. The import tab has the same fields for the other
direction, where UNI2 draws them itself: they go into the stage's `BgList` entry, and with nothing
changed it writes what it always did.

### Keeping the stage out of the game's bloom

BBTAG's own bloom switch blooms whatever is bright **and** writes alpha into the frame, and every
stage writes alpha 1 (see
[The frame's alpha is BBTAG's bloom mask](arcsys-3d-files.md#the-frames-alpha-is-bbtags-bloom-mask)).
Players who turn it off to calm the stage also lose the bloom palettes and the scripted bloom.
**Keep the stage out of the game's bloom**, in the viewer under the bloom fields and kept with the
framing, draws a node in mode 0 with vertex alpha 1/255 when all of this holds:

- it isn't additive;
- every vertex has alpha 255 and every texture it uses is opaque in every texel, so drawing it
  opaque changes nothing on screen;
- it writes depth, and its farthest point (where it was pulled to, if it was) is nearer than the
  nearest point of every node that doesn't write depth. Mode 0 draws before every blended item, so a
  pulled sky that doesn't write depth would otherwise paint over it.

Everything else stays as it was and keeps blooming: see-through and additive parts, cut-outs,
atlases with gaps, and stage parts behind a far node without depth. The export summary counts the
nodes it changed. It starts switched off, and it has only been reasoned from BBTAG's shaders, not
yet seen in the game or tried in BBCF.

## 2D layer

BBTAG/BBCF have no equivalent of UNI2's 2D layer. On export each `object.txt` object becomes a 3D
sprite, up to 12 per mesh, at the place and depth UNI2 draws it. On import it's the other way round:
script-animated sprites become mod flipbooks, and the particles are baked with the same seed the mod
uses.

---
