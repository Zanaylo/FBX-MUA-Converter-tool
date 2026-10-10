# FbxToMua

Takes stages from the French-Bread games to BBCF and BBTAG, and brings stages from BBTAG, BBCF and
P4U2 to UNI2. **Exporting doesn't need the UNI2 Improvement Mod.** Importing into UNI2 does, because
the lamps, the water and the particles of those stages are run by the mod's runtime.

It's a C# port of the converter that lives inside the mod, and its output matches it byte for byte.

| from | to |
|---|---|
| UNI2, UNI[st], UNIEL, MBTL, MBAACC, DFCI, or a stage folder with `bg.fbx.bin` | BBCF or BBTAG |
| BBTAG, BBCF or P4U2 (decrypted or the Steam release) | UNI2, through the IM stage library |

Stages are listed by the English names the games themselves use (UNI2's "Metropolitan Center:
Intersection", BBCF's "Crimson Throne"), not by their folder or their Japanese name.

---

## The app (`FbxToMuaApp.exe`)

It opens and looks for the games through Steam right away. Two tabs:

**French-Bread → BBCF / BBTAG**

1. Pick the source game (or **Browse...** for a loose stage folder).
2. Pick the stage.
3. Pick the target game. If it can't find the folder, press **Game folder...**.
4. Pick which of the game's stages gets replaced and press **Install in game**.

The first time you install over a stage, it backs up the original files. **Restore original** puts
them back. **Export files only...** just writes the files into a folder without touching the game
(you get the loose `.MUA` to open in Blender, and the `BBTAG\` and `BBCF\` folders with the `.pac`
files).

**Viewer...** opens the stage the way BBTAG and BBCF's fight camera sees it, with two stand-in fighters.
When a stage comes out with the wrong angle, move the camera and the fighters around it (side, height,
distance, turn, tilt, stage scale) and press **Keep this framing**. The framing is saved per stage and
used by every **Install in game** and **Export files only...** after that; **Reset framing** goes back
to the converted one. The fighters' positions are only for looking: the camera follows the middle of
the two, the way it does in a fight, and **Free look** lets you orbit around the stage.

On the Steam release of BBTAG the files are encrypted under `asset\`, and FbxToMua writes them
encrypted the same way. The executable needs no patch.

**BBTAG / BBCF / P4U2 → UNI2**

1. Pick the source game.
2. Pick the stage.
3. Check the UNI2 folder (it must have `UNI2-IM` inside).
4. **Install into UNI2-IM** and start the game. The stage shows up in the stage list.

**Stage folder only...** writes the stage folder wherever you want, to import later through the mod.

---

## The command line (`FbxToMua.exe`)

```
FbxToMua stages <game folder>
FbxToMua export <game folder | stage folder> [stage] --out <folder> [--name text]
FbxToMua extract <game folder> <stage> --out <folder>
FbxToMua targets <bbcf|bbtag>
FbxToMua game-folder <bbcf|bbtag> <folder>
FbxToMua install <game folder | stage folder> [stage] --to <bbcf|bbtag> --replace <group/bg_name> [--name text]
FbxToMua restore <bbcf|bbtag> <group/bg_name>
FbxToMua import <BBTAG|BBCF|P4U2 folder> <bg_stage> (--out <folder> | --uni2 <UNI2 folder>) [--name text]
```

Examples:

```
FbxToMua stages "D:\Steam\steamapps\common\MELTY BLOOD TYPE LUMINA"
FbxToMua targets bbcf
FbxToMua install "D:\Steam\steamapps\common\MELTY BLOOD TYPE LUMINA" bg001 --to bbcf --replace main/bg_snowtown
FbxToMua restore bbcf main/bg_snowtown
FbxToMua import "D:\Steam\steamapps\common\BBTAG" bg_snowtown --uni2 "D:\Steam\steamapps\common\UNDER NIGHT IN-BIRTH II Sys Celes"
```

`extract` pulls a stage's raw files (the `bg.fbx.bin`, the DDS...) straight out of the install, to
open in Blender.

Where it keeps its data: `%LOCALAPPDATA%\FbxToMua\` (`installs.json` with what was installed where,
`reframes.json` with the framing kept in the viewer, and the `Backup\` folder with the originals).

---

## Blender

The [`blender/`](blender/) folder holds the two add-ons:

- **FbxExp model** opens and saves `bg.fbx.bin` (UNI2, MBTL, UNI[st], converted DFCI).
- **Mua model** opens the `.pac`/`.MUA` files of BBTAG, BBCF and P4U2, with skeleton, animation,
  scripts and particles, and saves them back.

To build the zips: `python blender/package.py`. Then, in Blender, `Edit > Preferences > Add-ons >
Install from Disk...` and pick the zip. It needs Blender 4.2 or newer.

Opening and saving without changes, both give the file back unchanged (tested on Blender 5.2:
`bg.fbx.bin` and the BBTAG `.pac` come back byte for byte; the BBCF one comes back with the same
content and only a different zlib compression).

---

## Building

It needs the .NET 10 SDK.

```
dotnet test tests/FbxToMua.Core.Tests
dotnet publish src/FbxToMua.App -c Release -o publish
dotnet publish src/FbxToMua.Cli -c Release -o publish
```

Each one comes out as a single `.exe` that doesn't need .NET installed on the user's machine.

| project | what it is |
|---|---|
| `src/FbxToMua.Core` | all the logic: formats, reading the games, conversion, installing |
| `src/FbxToMua.App` | the WPF app |
| `src/FbxToMua.Cli` | the command line |
| `tests/FbxToMua.Core.Tests` | unit tests (xUnit) |
| `blender/` | the add-ons |

---

## Credits

- [UNI2-Improvement-Mod](https://github.com/Zanaylo/UNI2-Improvement-Mod) - someone made it :)
- [GeoArcSysAIOCLITool](https://github.com/Geordan9/GeoArcSysAIOCLITool) by Geordan9 - the
  ArcSys MD5 crypt keys of BBTAG and P4U2, and the `FPAC` extractions the archive reader was checked
  against
- [MBTL.BIN.Tool](https://github.com/Ekey/MBTL.BIN.Tool) by Ekey - MBTL's `dataNNN.bin` archives and
  their key
- [UNIB.Data.Tool](https://github.com/Ekey/UNIB.Data.Tool) by Ekey - UNI2's `d` archive
- [undernightinbirth](https://github.com/Fatih120/undernightinbirth) - the community documentation
  of UNI's files
- [Blender-MBTL-BG-IO](https://github.com/Eiton/Blender-MBTL-BG-IO) by Eiton - its `fbx.bin.hexpat`
  confirmed the `bg.fbx.bin` layout
- The community BlazBlue MUA plugin for [Noesis](https://github.com/tl000000/NoesisMuaPlugin)
- **Hikari** - for all the help with stages
- [Under Night BR](https://discord.gg/Az7uQUU)

UNDER NIGHT IN-BIRTH, MELTY BLOOD and DFCI belong to French-Bread and their publishers; BLAZBLUE and
Persona 4 Arena belong to Arc System Works and ATLUS. This is just a fan tool... have fun :)