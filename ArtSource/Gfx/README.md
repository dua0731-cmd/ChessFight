# Gfx: what made the graphics upgrade's pictures (JY-gfx, 2026-11-24)

The graphics upgrade renders the same game (same sections, same pieces) better in URP: a real cloud sky, a warm sun,
longer and finer shadows, a clean vivid grade, and real material detail (surface sets) on the course's surfaces.
The Unity side is in `Assets/Scripts/Game/GameSky.cs`, `GameGrade.cs`, `GameSurfaces.cs` and
`Assets/Shaders/URP/CFSurface.hlsl`, `CFLit.hlsl`, `GfxLit.shader`, `GfxSky.shader`. These scripts made its
pictures. Blender 5.x, headless:

| Script | Makes | Command used |
| --- | --- | --- |
| `textures.py` | the nine surface sets (limestone, sand, wood, iron, brass, paint, canvas, leaf, slate): `<set>_mask.png` (R roughness, G cavity, B height, A colour variation, linear) and `<set>_normal.png`, 1024 px, tiling | `blender -b --factory-startup --python textures.py -- --out <folder> --n 1024` then copy into `Assets/Resources/Gfx/Sets` |
| `sky.py` | the sky panorama: volumetric cumulus path traced over a drawn blue, the lower half a warm far land, the sun where the game's sun is | `blender -b --factory-startup --python sky.py -- --out <folder>/sky --sun-dir -0.321,0.557,-0.766 --res 4096 --samples 128` then `sky.exr` into `Assets/Resources/Gfx/Sky` |
| `kit.py`, `surface.py` | helpers the two above import | |
| `make_gfxlit.mjs` | `GfxLit.shader` and `GfxLitInput.hlsl` from the project's own URP Lit (run again when URP is updated) | `node ArtSource/Gfx/make_gfxlit.mjs <project folder>` |

`--sun-dir` is the way the light travels in Blender's axes (Unity x, y, z = Blender x, z, y): the round's sun
(rotation 50, 330, 0). Another sun angle wants the sky rendered again so the bright side of the clouds stays on the
sun's side.

Pictures to compare: `Assets/Scripts/PawnRushEditor/GfxShots.cs` (menu-less, batch):
`Unity.exe -batchmode -projectPath <copy> -executeMethod ChessFight.PawnRush.Editor.GfxShots.Run -gfxOut <folder> -gfxTag <name>`
plays each default section, puts a camera where the game's camera would be, and writes PNGs; `-gfxTune
"a:exp=0,con=20;b:lit=0"` compares looks (keys in GfxShots.Look).
