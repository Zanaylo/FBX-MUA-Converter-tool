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
| `cammot.pac` | the intro camera (BBCF only) |

### BBCF always needs all four

This one really crashed BBCF. The first export had no `cammot.pac` (because some BBTAG test stages
don't have one) and the game closed while loading.

The reason: the loader looks up `mdl.pac`, `scr.pac`, `mot.pac` and `cammot.pac` by name, and **when
the name doesn't exist the lookup returns index 0, never -1**. So without `cammot.pac` it hands
`mdl.pac` to the camera reader, which reads the model as if it were an animation, finds a "2.0" where
it expected a count, tries to allocate 4 GB and dies with `bad_alloc`. All 57 BBCF stages have the
four.

The intro camera FbxToMua writes is still, on the fight framing: the eye at `(0, 100, -320)`, which
is where BBCF's shipped cameras end.

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

FbxToMua keeps only the keys it needs: it drops a key when linear interpolation of position and scale
and slerp of rotation stay within 1e-3 / 1e-5 without it, with at most 256 frames between keys.

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

The script of a moving mesh, the way FbxToMua writes it, is as simple as possible: it picks take 0 at
frame 0 and lets it run on the script's clock (`01 / 03 0 / 06 0 / 04 / 02`).

---

## The textures

Loose DDS files inside the `_img.pac`. A BBCF pitfall: **the DDS header has to carry the linear size
flag (`0x80000`)**. Without it BBCF can't measure the texture and draws it white. That's what left the
Magician's Night grass atlas white.

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
| `cammot.pac` | optional | **required** | — |
| FbxToMua exports to it | yes | yes | no |
| FbxToMua imports from it | yes | yes | yes |
