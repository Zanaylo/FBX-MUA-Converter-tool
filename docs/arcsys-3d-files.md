# Arc System Works' 3D files (BBTAG, BBCF, P4U2)

Now the other side. BBTAG, BBCF and P4U2 run on the same Arc engine, and a stage is the same thing in
all three: **three `.pac` files, which are packed folders (`FPAC`), with a `MUA` model inside, the
animations in `.mmot`, the scripts in `.evb` and the textures in DDS**. What changes between the
games is the packaging (BBCF compresses, retail BBTAG encrypts) and a few details that, when missing,
crash the game in your face.

Everything here was read from the files the games ship or from the executable itself.

---

## Where a stage lives

```
<game>\data\bg\<group>\<stage>.pac        the scene: model without geometry, scripts, animations, camera
<game>\data\bg\<group>\<stage>_vtx.pac    the whole model, with the geometry
<game>\data\bg\<group>\<stage>_img.pac    the textures, loose DDS files
```

`<group>` is something like `main`, `main_cf`, `main_old` (BBCF) or `main_uni`, `main_bb`,
`main_p4u`, `main_rwby`, `main_arcana`, `main_kagura`, `cmn` (BBTAG).

**The stage ships the model twice.** The `<stage>.pac` has a copy of the `MUA` with empty vertex and
index sections, and the `_vtx.pac` has the whole model. If you're going to open something, open the
`_vtx`.

Inside the `<stage>.pac` there are more `.pac` files (FPAC nests freely):

| entry | what it holds |
|---|---|
| `mdl.pac` | the `.MUA` without geometry |
| `scr.pac` | the `.evb` scripts |
| `mot.pac` | the `.mmot` animations (can be an empty 32-byte FPAC) |
| `cammot.pac` | the intro camera (in menus and some `cmn` stages, an empty FPAC) |

### Both games always need all four

This one really crashed BBCF, and later BBTAG too. The first export had no `cammot.pac` and BBCF closed
while loading. BBTAG was left without it for a while, and installing UNI2's Altar of Light and Dark
over `main_arcana/bg_gyoenjogakuen` crashed it while loading, with the "requested the Runtime to
terminate it in an unusual way" box.

The reason: the loader looks up `mdl.pac`, `scr.pac`, `mot.pac` and `cammot.pac` by name, and **when
the name doesn't exist the lookup returns index 0, never -1**. So without `cammot.pac` it hands
`mdl.pac` to the camera reader, which reads the model as if it were an animation, finds a "2.0" where
it expected a count, tries to allocate 4 GB and dies with `bad_alloc`. All 57 BBCF stages have the
four, and so do all 101 BBTAG ones (the menu and astral stages carry an empty `cammot.pac`, never
none). The camera bone has no fixed name (BBTAG has `Bone_camera`, `Bone_kaimakucamera` and
`camera_Bone`), so it's the entry the game needs, not a particular bone.

The intro camera FbxToMua writes is still, on the fight framing: the eye at `(0, 100, -320)`, which
is where BBCF's shipped cameras end.

### Keep `scr.pac` and `mot.pac` small

This one crashed BBTAG and BBCF in the middle of a fight. MBTL's Shining Colosseum went out with
**372 `.mmot` and 373 `.evb`** (one per moving node) and the game died on a hit with
`Not Find Object Model Link Ex Motion Mesh Name Err - circle_L00`. `circle_L00` isn't in the stage at
all: it's a mesh of Ragna's hit effect (`Eff3D/eff3d_rg.pac`, `rgef_mc.MUA`).

The reason, read from `BBCF.exe`: the stage and the characters' 3D effects go through the **same
loader** (`0x479850`, called by the stage at `0x5d9501`), and it keeps one entry **per file** in
pools that are created once with **512 slots** (`0x473787` to `0x4737d7`) and only ever grow:

| pool | one entry per | adding to a full pool |
|---|---|---|
| models (`0x409850`, add `0x409ae0`) | `.MUA` in `mdl.pac` | fails quietly, the handle stays 0 |
| scripts (`0x40df50`, add `0x40e150`) | `.evb` in `scr.pac` | fails quietly, the handle stays 0 |
| motions (`0x409f30`, add `0x40a0a0`) | `.mmot` in `mot.pac` | **no check: writes past the end** |

The effects load on demand, so the stage ate most of the slots and the first hit spilled over: the
effect got handle 0 (somebody else's model) or the motion array overflowed into the heap. The error
comes from `0x47a955`, when the effect's motion can't find its mesh in the model it got.

What the shipped stages use, for comparison: at most **42 `.mmot`** and **127 `.evb`** (BBCF), and
character effects bring up to ~160 model files each. Meshes and bones don't go into these pools.

So FbxToMua shares: moving nodes with the same period, blend and depth flags go into **one skeleton,
one script and one take**, up to 32 bones per skeleton (the shipped stages go up to 37, the effects
up to 39). An ancestor that moves several nodes becomes one bone, not one per node. Shining Colosseum
now goes out with 37 takes and 38 scripts.

---

## `FPAC` — the packed file

```
'FPAC'
dword dataStart          where the data starts
dword totalSize
dword count
dword 1
dword nameBytes          size of the name field
8 zero bytes
count * ( char name[nameBytes], dword index, dword offset, dword size )
```

Offsets count from `dataStart`, and each file is aligned to 16.

**To read, the stride between entries is `(dataStart - 0x20) / count`**, not what you would deduce
from `nameBytes`. The two agree when the name is 12 or 24 bytes and diverge between 17 and 20, where
the real stride is 48. That made `bg_boss`'s `scr.pac` read the second entry from the middle of the
first name.

**To write**, FbxToMua uses `nameBytes = align4(longest name + 1)` and stride
`align16(nameBytes + 16)`, which is the rule that reproduces the game's files.

`totalSize` is 16 bytes short in roughly half of the files. Those 16 bytes at the end are a
signature and aren't part of the data.

### `DFAS` — BBCF's compressed FPAC

BBCF wraps its stage `.pac` files in zlib:

```
'DFAS'
4 bytes      the first 4 bytes of the original file ('FPAC')
dword        uncompressed size
dword        compressed size
...          zlib stream
```

Compressing again, the content comes back identical, but the compressed bytes can change with the
compression level. That's normal and the game doesn't care.

### Steam's BBTAG is encrypted

Retail BBTAG has no `data\bg`. Everything is in an `asset\` folder, with names that look like
garbage:

- **the file name is the MD5 of the path**, lowercase, with forward slashes:
  `md5("data/bg/main_uni/bg_odaiba_vtx.pac")`;
- **the content is XORed with a 43-byte key**, and the starting point in the key is
  `md5(fileName)[7] % 43`, advancing one byte at a time and wrapping around.

P4U2 is the same, just with another key. FbxToMua has both, and when installing into retail BBTAG it
**encrypts in place**, the way the game reads it. The executable needs no changes.

---

## `MUA` — the model

```
'MUA\0'
dword 0x3ee          version
dword 17             section count
24 zero bytes
17 * ( dword offset, dword count )
```

| # | section | stride | what it holds |
|---|---|---|---|
| 0 | skeleton | 0x20 | first bone, bone count, and at `+0xc` the **blend mode** |
| 1 | bone | 0x130 | name, type, T/R/S, parent at `+0x3c`, local matrix at `+0x48`, inverse at `+0x88` |
| 2 | mesh | 0xc0 | skeleton, parts, vertices, oriented collision box, name, bone |
| 3 | part | 0x20 | material, index count and start, scripts |
| 4 | material | 0x50 | texture assignments and 18 floats |
| 5 | assignment | 0x20 | material, texture |
| 6 | texture | 0x10 | name |
| 7 | — | — | not read |
| 8 | key list | 0x40 | length of the animation tracks per bone |
| 9 | keys | 0x20 | value (3 or 4 floats) and the frame at `+0x10` |
| 10 | — | — | empty |
| 11 | scripts | 0x10 | one `.evb` per record, in the order the game binds them |
| 12 | mesh/part order | 0x20 | **one record per part**: `(mesh, part of that mesh)` |
| 13 | vertex | 0x50 | see below |
| 14 | index | 2 | one triangle strip per part, joined with degenerate triangles |
| 15 | string info | 0x10 | offset and size |
| 16 | strings | — | Shift-JIS |

### The vertex (0x50 bytes)

```
+0x00  3 float   position, already in world space
+0x0c  3 float   normal
+0x18  3 float   tangent
+0x24  2 float   UV
+0x2c  2 float   UV2 (zero in every stage)
+0x34  4 byte    colour, in BGRA (not RGBA!)
+0x38  3 float   bone indices, -1 when there is none
+0x44  3 float   weights
```

Three things that have already cost me time:

- **The colour is BGRA.** It was read as RGBA for a long time, and that was what gave the Fountain
  Plaza fountain the wrong colour.
- **The vertices are already in world space.** The bones exist for the animations to move. If you
  apply the mesh's bone on top of the vertices, the stage comes out wrong without looking wrong
  (`bg_odaiba` came out 1.21x bigger and looked fine; only `bg_falling_blossoms`, which has things
  hanging off a bone with a 1200 scale, gave the problem away).
- **The bone's parent is local to the skeleton.** The index at `+0x3c` counts within the skeleton,
  not across the whole file.

### Section 12 has one record per PART

This one also crashed BBCF. The first version wrote one record per mesh. The game reads section 12
**using the part count** (section 3), so with more parts than meshes it read past the section, took a
piece of the string table as a mesh index, and crashed looking for a skeleton that didn't exist. In
the 141 MUAs the three games ship, section 12 has exactly one `(mesh, part)` pair per part.

### The blend mode is in the skeleton

`+0xc` of the skeleton record:

- `0` opaque (but the game still discards texels with alpha exactly 0, so cut-outs work)
- `2` additive
- `4` subtractive
- any other value (the `0x7fffffff` most use) is **alpha blend writing depth**, which is the game's
  default for models.

And flag `0x2000` in the skeleton flags turns depth writing off.

BBTAG's renderer puts every mode-0 item ahead of all the blended ones, and sorts only the blended
ones back to front (`UNI2-docs/BBTAG.md`, "The renderer sorts").

### The frame's alpha is BBTAG's bloom mask

BBTAG's bloom (the "Bloom" switch in the advanced graphics settings) starts with
`BBDX_CLightBloomPickupLuminance`, an effect compiled into `BBTAG.exe` (`0xb2f6f0`, `0x6d4` bytes).
Its pixel shader keeps a pixel of the frame when
`alpha * dot(rgb, (0.299, 0.587, 0.114)) * 2.3 >= threshold` and drops it otherwise. The model shader
is `In.color * tex + AddColor` on all four channels, so a mesh writes its vertex alpha times its
texture alpha into the frame, and that alpha decides whether its bright parts bloom.

The shipped stages write 1 nearly everywhere: their textures are mostly DXT1, and across 38 BBTAG
stage models 100% of mode-0 vertices, 84.5% of `0x7fffffff` vertices and 82.2% of additive vertices
carry alpha 255. So the bloom switch blooms every stage, shipped or exported. What makes a mesh
escape it is drawing it in mode 0 (`ONE, ZERO`, colour unaffected by alpha) with a near-zero alpha.
That the pickup reads the same target the stage is drawn into is inferred, not measured.

### The floor that takes the fighters' shadow

Flag `0x100` in the skeleton flags keeps the fighters' shadow off a mesh. In BBTAG, the stage loader
(`FUN_007d1fa0`) reads every mesh's skeleton flags (model slot 30, selector `0x12`): with `0x100` set
the mesh is marked 1, and without it the mesh is marked 2 when its bounding box falls inside the
shadow's area. The model draw (`FUN_004266d0`) only binds the shadow texture to meshes marked 2.

The shipped stages clear it on the floor and nothing else: across the BBCF and BBTAG stage models,
185 of the 5,153 opaque or alpha meshes go without it, and nearly all of them are the ground under
the fighters (`ground`, `floor`, `yuka`, `ground_skel`). The additive glow on the floor (`jimen_add`)
keeps it.

The first exports set `0x100` on every skeleton, which is why no custom stage showed the shadow.
FbxToMua now clears it on a still node that writes depth, isn't additive, faces up over at least half
of its area, and passes under the fight line within 10 units of the fighters' feet. That catches the
floor of 63 of the 65 MBTL and UNI2 stages. The two it misses (MBTL `bg028` and `bg033`) build the
floor and the buildings as one node, so most of their area faces sideways. MBAACC's 56 stages are 2D
sprites and get none. BBCF's shipped floors follow the same convention; its loader hasn't been read.

### The loader's shadow test leans one way

Clearing `0x100` is not enough. `FUN_007d1fa0` (`0x7d2740`) marks a mesh 2 only when its box, read
from the mesh record (corner 0 is the high corner, corner 6 the low one), passes a test built from
the `0x1d` light `(x, y, z)`, with `H = 2048 k` and `D = 3072 k` (`k` = 0.3682, the world units per
pixel at the fight plane, `FUN_00747f50` measuring `(0,0,0)` to `(1,0,0)` at 1280x720):

| light | the box must |
|---|---|
| always | have its lowest y at or below `D` (1131) |
| `z < 0` | have its lowest z at or below 0, `high z <= H - (z/x) high z` and `-(z/x) low z - H <= low z` |
| `z > 0` | have its highest z at or above 0, `high z <= H + (z/x) high z` and `(z/x) low z - H <= low z` |
| `z = 0` | `low z <= D - (y/z) low z` and `-(y/z) high z <= high z` |

A NaN from `0 * inf` passes, the way the x87 jumps take it. With `x = 0` (every shipped light but
three) or `x > 0` this only asks that the floor cross the fight line. With `x < 0` and `z < 0` it
asks that the **whole box** fit in `|z| <= H / (1 + z/x)`: 191 units for the turn 19, length 1.45
light that Gathering of Old Blood went out with, against a floor from -2772 to 1499. That is why its
shadow never showed. The shipped stages never lean that way, except the astral ones at
`(-1000, 1000, -1000)`, whose arenas fit in 377.

FbxToMua now runs the same test (`ShadowArea`). A floor that fails it whole is cut along z
(`DepthSlab`) and only the strip the game accepts loses `0x100`; the rest keeps it. The viewer casts
the stand-ins' shadows onto the same strip, so it shows the shadow where, and only where, the game
draws it.

### Draw order is in the pivot

The game sorts transparent things back to front using `-(int)pivot.z`. So to keep UNI2's draw order
(which is file order), FbxToMua writes pivot z = `meshes - i`.

---

## `MMOT` — the animation

```
'MMOT' ...
dword 0x3eb          version
10 sections (some have 11), table with a 0x10 stride
```

- bone: 0xe0 bytes. Local matrix, inverse world, inverse parent world, `frames` at `+0xc0` and the
  indices of the four key lists.
- key: 0x20 bytes, same idea as in the MUA.
- strings at the end: take name, root and the bound meshes.

**The animation's period is the `frames` at `+0xc0`.** The game only bakes frames `< frames`. If you
use "last key + 1" the loop gets one frame longer and keeps drifting.

Rotation is a quaternion and is interpolated with slerp. Something that went wrong on the way, while
converting the 2D layer to animation: **angles have to interpolate along the short way**, modulo one
turn, the way UNI2 does. The back grass of Magician's Night went from 0 to 0.95 of a turn and spun
342 degrees every loop, when in the game it just sways a little.

FbxToMua keeps only the keys it needs, **per track**: it drops a position key when linear
interpolation stays within 1e-3 without it, a scale key within 1e-5, and a rotation key (the euler
and the quaternion tracks together) when slerp stays within 1e-5, with at most 256 frames between
keys. Each bone has its own key list per track and the shipped takes already have tracks of
different lengths, so a lamp that only turns gets two position keys, not one per frame. That took
Shining Colosseum from 1.08 million keys to under 200 thousand.

---

## `EVB` — the scripts

An `EVT0` file. Each moving mesh has a script, and `base.evb` sets up the scene.

```
+0x00  'EVT0'
+0x10  offset of the command list
+0x14  record size (0x20 in every stage)
+0x20  seven u16 counts, one per block
+0x30  the blocks, one after the other
```

**The block whose count sits at `+0x22` is the name table** (32-byte ASCII names, like
`train_L_go_000.mmot`). Then come the commands, each with its opcode in the first dword.

The ones that matter:

| op | what it does |
|---|---|
| `0x01` | begin |
| `0x02` | wait for the next frame |
| `0x03 <frame>` | wait until the frame |
| `0x04` | close the block |
| `0x05 <label>` | jump to the label |
| `0x06 <n>` | **pick the n-th name**: switches the animation and resets the clock |
| `0x09 <n>` / `0x0a` | label / end of group |
| `0x0b <frames> <label>` | hold and continue at the label |
| `0x12 <target> <frames>` | **linear ramp** of the brightness (Central Station's lamps) |
| `0x13 <percent>` / `0x14` | roll: enters the block if `rand() % 100 < percent` |
| `0x15 <n>` | pauses the script's clock |

And `base.evb` uses another set (`0x1a` opens, `0x1b` closes). **Every size is divided by 1000** and
`0x28` is in degrees. `0x28` is where the camera tilt (`ViewRotationX`) goes on export, in whole
degrees, which is the precision the command has.

`0x1d`, `0x1e` and `0x1f` light the fighters' shadow. The stage object hands them out as vtable
slots 8, 9 and 10, and the shadow draw (`FUN_007920c0`) passes `(0x1d x, y, z, 0x1f)` as the light
and `(4096 * k, 3072 * k, 0x1e a, 0x1e b + 100)` as the area, where `k` is the camera's world units
per screen pixel (`FUN_00747f50`, about 0.37). The stage loader uses the same `0x1d` to decide which
floor meshes the shadow reaches. `0x1d`'s x and z point the way the shadow **falls**, at `y` per unit
of the fighter's height, so the shipped `(0, 0.9, -1)` casts it towards the camera, about 1.1 times as
long as the fighter is tall. This was first read the other way round (pointing at the light, so the
shipped light would cast it back); BBTAG settled it: `(0, 0.333, 1)` on Moonlit Observatory casts
Hakumen's shadow straight back, towards the vanishing point. That measured z. That x follows the same
rule is assumed, not yet measured.
`0x1f` is read as the shadow's strength (0.5 to 1.0 in the shipped stages); that it is the darkness
is inferred, not measured.

The shadow's sharpness isn't the stage's: the area is always 4096 by 3072 game pixels, and the
square render target the game creates first, next to the sprite shadow shader (`FUN_0066e800`), is
768 by 768. If that's the shadow's, each texel covers about five game pixels on every stage,
shipped or not. What a stage can do is keep the shadow short and straight, because a long or slanted
one stretches those texels further.

`0x28` doesn't move the camera. BBTAG passes the three angles as the stage model's rotation
(`FUN_007d0b00`, position from the stage, scale 1), and turns the stage's particle emitters by
`RotationX * RotationY * RotationZ` of the same angles about the world origin (`FUN_007d3350`). So a
positive angle lowers the far side of the stage, the way `D3DXMatrixRotationX` does, and the viewer
previews it that way. The battle camera itself stays at eye `(0, 100, -320)`, target `(0, 100, 0)`,
45 degree field of view, 16:9, far plane 100000 (`FUN_0067ef80`).

The script of a moving mesh, the way FbxToMua writes it, is as simple as possible: it picks take 0 at
frame 0 and lets it run on the script's clock (`01 / 03 0 / 06 0 / 04 / 02`).

---

## The textures

Loose DDS files inside the `_img.pac`. A BBCF pitfall: **the DDS header has to carry the linear size
flag (`0x80000`)**. Without it BBCF can't measure the texture and draws it white. That's what left the
Magician's Night grass atlas white.

The `fbxex` names the texture the artist painted, not the file the game ships: MBTL's `bg018` asks
for `church2_zimen_AlbedoTransparency.png`, `bg004` and `bg026` for `hunsui_touka.psd`, `bg027` and
UNI2's `bg020` for `.png` files, and `bg031` for `carpet light_2D_View.dds` while the archive holds
`carpetlight_2D_View.dds`. The DDS with the same name is always there. FbxToMua tries the name as a
`.dds`, then that without spaces, then the name as written, and writes the one it found into the
`MUA` and the `_img.pac`. Before, those textures went into the `MUA` under their `.png` names and
never reached the `_img.pac`.

---

## The far plane

BBCF's fight camera has its far plane at **100000**. UNI2 stages sometimes have a sky much farther
than that (Magician's Night's sky sat between 118 thousand and 121 thousand), and then it all
disappears.

FbxToMua pulls those meshes in with a uniform scale around the eye: each point stays on the same ray,
so the mesh covers the same pixels and only its depth changes. It only does that when the mesh goes
past 95% of the far plane and starts at least 20% into it (a floor that starts at the fighters is
never pulled, because that exaggerates the parallax when the camera moves). If the pulled mesh would
cover something that wasn't pulled, it stops writing depth.

---

## Stage names

The Arc games name their stages in `data/localize/eng_idlist.txt`, a UTF-16 list where each
`BG_<FOLDER>` line is followed by the English name (`BG_CASTLE` → "Crimson Throne"). FbxToMua reads it
from the install. BBCF writes `|` for the apostrophe and `^` for the colon (the font draws them that
way), squeezes the spaces out of two names that `jpn_idlist.txt` spells properly, and lists a few
internal stages under placeholder names that just repeat the id; all three are handled. Stages the
list doesn't name fall back to the UNI2 Improvement Mod's table, then to the folder name.

---

## BBTAG x BBCF x P4U2, quickly

| | BBTAG | BBCF | P4U2 |
|---|---|---|---|
| `.pac` | plain FPAC | FPAC inside DFAS (zlib) | plain FPAC |
| Steam install | encrypted under `asset\` | plain `data\bg` | encrypted, another key |
| `cammot.pac` | **required** | **required** | — |
| FbxToMua exports to it | yes | yes | no |
| FbxToMua imports from it | yes | yes | yes |
