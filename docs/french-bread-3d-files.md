# French-Bread's 3D files

This is the UNI / Melty / DFCI side of the story. Everything written here was measured on the files
the games actually ship, not guessed. When something is still uncertain, I say so.

The short version before the details: **a French-Bread stage is a 3D model in a native format of
theirs (`bg.fbx.bin`), a pile of loose DDS files next to it, and sometimes a 2D layer on top** (the
`object.txt` with a `.pat`). UNI2, UNI[st] and MBTL use exactly the same container. DFCI and UNIEL
are older cousins, and MBAACC is another world (it's all 2D sprites).

---

## `bg.fbx.bin` — the model

Despite the name, **there is nothing FBX in here**. It's French-Bread's "baked" format, which they
call `fbxex`. The game builds the path `bg\bgNNN\bg.fbx`, appends `.bin` and checks the magic. The
`bg.fbx.json` that sometimes shows up next to it is just a dump from when they built it: **the game
never opens it** (the word `json` appears nowhere in `uni2.exe`). To port a stage, only the `.bin`
matters.

There is no version field. The layout is the same in all three games.

### Header

```
'fbxex\0\0\0'   8 bytes
dword 0
dword 0
```

After that come **four blocks, always in this order**: textures, materials, nodes and animation.
Each block starts with:

```
dword size      (counting these 8 bytes)
dword count
...body
```

And the last block ends **exactly at the end of the file**. That's the cheapest sanity check there
is: if the sum doesn't land on EOF, the file is broken or you read it wrong.

### Block 1 — textures

One 128-byte name per texture, padded with zeros. **A texture's index is its position in the list**;
no number is stored. The name is that of the `.dds` that sits loose in the stage folder.

### Block 2 — materials

204 bytes each:

```
char  name[128]
dword index
dword textureindex
float values[17]
```

The confusing detail: **the material block is the flattened list of submeshes**. The total number of
submeshes across all nodes equals the material count, in all 28 UNI2 stages. So material *i* belongs
to the *i*-th submesh, in node order. That's why a stage has more materials than textures.

Nobody needs to touch the 17 floats to port a stage. I tried using them to control colour and it
didn't work.

### Block 3 — nodes

Each node:

```
dword size      (counting these 16 bytes)
dword type
dword child     (index of the first child, negative when there is none)
dword sibling   (index of the next sibling, negative when there is none)
...payload
```

**There are only two types.**

- **Type 0** is just a transform. It has no payload at all (size 16).
- **Type 1** is a mesh:

```
dword flag
dword flag                       (both are only ever 0 or 1)
float matrix[16]                 (4x4, the node's world)
dword vertexCount
float vertices[count * 12]
dword submeshCount
per submesh:
    dword material
    dword indexCount
    dword indices[count]         (triangle list, not strip)
```

**The 12 floats of a vertex are: position (3), normal (3), RGB colour (3), alpha (1), UV (2).** This
was measured on 419,827 vertices: floats 3 to 5 have length 1 in 100% of them, 6 to 9 never leave
`[0,1]` and 10 and 11 range from -31 to 52, which is how repeated UVs behave.

The vertex colour is where the stage's lighting lives. There is no dynamic light: the "lighting" comes
painted into the vertices.

**Node 0 has to be a root (type 0).** If a mesh lands in the first position, the stage shows up as an
empty green field. The Blender add-on already respects that.

### Block 4 — animation (`anime`)

One record per node (always `anime.count == node.count`):

```
dword n
float matrices[n * 16]
```

**It has no fixed size.** The first value is an **integer**, not a float (read as a float it gives
`1.4e-45`, which is the hint that it's wrong). `n` is the frame count: 1 for a still node, up to 4800
in stages with a lot moving.

In UNI2, UNI[st] and MBTL **each matrix is local to its parent**, and the game composes
`world = parentWorld * local` down the tree.

### The UNIEL pitfall

UNIEL (Exe:Late, 2012) stores **the already composed world matrix** in `anime`. If you drop a UNIEL
stage into UNI2 untouched, the game applies the parents' transform twice, and the stage shows up with
everything out of place, the wrong lighting and looking as if the camera moved.

How to detect it: in UNIEL, `anime[i][0]` equals the node's payload matrix on every mesh node. In a
UNI2 file that almost never happens. The fix is
`local[f] = inverse(parentWorld[f]) * world[f]`. FbxToMua does it by itself when it reads UNIEL
(`FbxExLocal`), and I checked it against UNI[st], which has seven of those stages re-exported by
French-Bread: it matches to 8e-6, which is float noise.

---

## The textures

Plain DDS, loose in the stage folder, DXT1/DXT3/DXT5 or uncompressed. D3D9 takes them as they are.

An annoying detail that really came up: **some texture names are in Shift-JIS** (BBCF's
`bg_halloween_4`, for example, has one). On a Japanese Windows, or a Windows with a different ANSI
code page, the name on disk changes. FbxToMua converts names through the system code page
(`DiskName`) to write them the way the game expects.

The three `.img` files (`stage_color.img`, `stage_specular.img`, `stage_bokashi_alpha.img`) are
per-stage colour, specular and blur maps. DFCI ships none of the three, and the stage works anyway.

---

## `object.txt` + `.pat` — the 2D layer

Some stages have things drawn in 2D over the model: grass, a glow in the sky, fireflies. That lives
in an `object.txt`, which is a Squirrel table:

```
BgObject <-
{
    panidata = "./bg/bgNNN/<name>.pat",
    data000 = [ { tag = "frm", name = "...", wait = 4 }, { tag = "prio", val = 10 }, ... ],
    ...
}
```

- `panidata` is a **path**, and it points at the stage's numbered folder. When the stage changes
  number, that path has to change with it (the importer does that).
- Each `dataNNN` is an object. The tags that matter: `frm` (which sprite and for how many frames it
  stays), `prio` (draw order), `startdelay` and `startpos` (x, y, z).

The `.pat` is the `PAniDataFile`: blocks with a 4-letter tag. `P_ST`/`PPST` are the parts (each
sprite cut-out, with `PPNM` the name, `PPUV` the rectangle, `PPCC` the colour and so on), `PGST` are
the texture pages (`PGNM` the name, `PGT2` the image), and `_END` closes it.

When exporting to BBCF/BBTAG this layer becomes **real 3D sprites**, up to 12 per mesh, because the
Arc games have no 2D stage layer.

---

## Where each game keeps its stages

FbxToMua reads straight from the install, without extracting anything first.

| game | where | how it's stored |
|---|---|---|
| **UNI2** | `d\` folder | index files + data files. The index lists folder, name, offset and size. No encryption. There is more than one index and two record sizes (80 and 64 bytes), which is why it has a dedicated reader. |
| **UNI[st]** / **[cl-r]** | `d\` folder | a listing file gives the names of the 526 files under `bg/` in plain text, with offset and size. |
| **UNIEL** | loose `bg\` folder | each file is RC4-encrypted with a fixed 16-byte key. Since the key is always the same, the keystream only depends on the byte's position. |
| **MBTL** | `data006.bin` | there is no index inside the file. FbxToMua carries a table with the 672 entries of `bg/`, and each one is decrypted with the same cipher the mod uses for the music. |
| **DFCI** | loose folder | **real FBX, in ASCII text**, plus the DDS files. FbxToMua converts the text FBX to `fbxex`. |
| **MBAACC** | `.p` files | 2D sprites, no model at all. See below. |

### MBTL's cipher depends on the file size

The keystream starts from a seed taken from the first two bytes and runs **backwards** from
`size - 1`. So if the size is wrong, the whole file comes out as garbage, but the first two bytes
come out right (which is quite misleading).

Three files under `bg/` grew after the version the table was taken from. Since only
`(seed + size - 1) & 0x3FF` matters, FbxToMua **tries all 1024 phases and keeps the one that gives
the right magic** (`DDS `, `fbxex`, `{`). Never trust the table alone.

### MBAACC is 2D

The old Melty has no 3D stage model: it's a pile of sprites (`bgmake` and the BMP Cutter3 format)
inside `.p` files. FbxToMua builds that as a flat 3D stage: each sprite becomes a flat card at the
place and depth the game uses, and the layer animation becomes keyframes. The 56 stages come out
identical to the mod's converter.

One thing I had to copy from MSVC to match: when drawing a particle position, the C++ evaluates the
function arguments **right to left**. So the draw is `y`, then `x`, then the slot. If you draw in the
written order, stage 16 comes out different.

---

## `BgList.txt` and `stage.txt`

`BgList.txt` is the Squirrel table that names the stage, places the camera and decides whether it
shows up in the picker. The fields that matter for the conversion:

- `Scale = [ x, y, z ]` — the stage's scale
- `Position = [ x, y, z ]` — position
- `ViewRotationX` / `ViewRotationY` — camera tilt and turn
- `FOV` and `VanishingPoint` — they only move UNI2's eye, which BBTAG has fixed. They are dropped on
  export.

For mod stages (the ones under `UNI2-IM\Mods\bg\bgNNN`), those fields go into a `stage.txt` next to
the model, with the `// UNI2 Improvement Mod` header, the identity lines (`Name`, `From`, `Source`)
and the lines the mod's runtime uses (`Flow`, `LampN`, `FlipN`, `Once`, `VertexAlpha`...). That's why
**a BBTAG/BBCF stage only runs in UNI2 with the IM installed**: the lamps, the flipbooks, the running
water and the particles are done by the mod's runtime, not the game.

The `Name` in `BgList.txt` is Japanese. The names FbxToMua shows are the games' English names
instead, taken from the UNI2 Improvement Mod's tables (`EnglishStageNames`): UNI2's come from its
`global_replace_word.csv`, UNI[st]'s pair with UNI2's by the Japanese name, and MBTL, MBAACC and DFCI
use their English releases' names.
