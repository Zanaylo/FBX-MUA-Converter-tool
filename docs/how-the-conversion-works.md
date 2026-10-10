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

`tilt` is added to `ViewRotationX` and goes into `0x28` in whole degrees, like before. The viewer
previews it as the camera pitching down around the eye; what the game does with `0x28` hasn't been
measured, so that part of the preview is an approximation. With no reframe the output is the same,
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

## 2D layer

BBTAG/BBCF have no equivalent of UNI2's 2D layer. On export each `object.txt` object becomes a 3D
sprite, up to 12 per mesh, at the place and depth UNI2 draws it. On import it's the other way round:
script-animated sprites become mod flipbooks, and the particles are baked with the same seed the mod
uses.

---
