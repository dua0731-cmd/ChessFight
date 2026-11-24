# Gfx: what made the graphics upgrade's pictures (JY-gfx, 2026-11-24)

The graphics upgrade renders the same game (same sections, same pieces) better in URP: a real cloud sky, a warm sun,
longer and finer shadows, a clean vivid grade, and real material detail (surface sets) on the course's surfaces.
The Unity side is in `Assets/Scripts/Game/GameSky.cs`, `GameGrade.cs`, `GameSurfaces.cs` and
`Assets/Shaders/URP/CFSurface.hlsl`, `CFLit.hlsl`, `GfxLit.shader`, `GfxSky.shader`. These scripts made its
pictures. Blender 5.x, headless:

| Script | Makes | Command used |
| --- | --- | --- |
| `textures.py` | the nine surface sets (limestone, sand, wood, iron, brass, paint, canvas, leaf, slate): `<set>_mask.png` (R roughness, G cavity, B height, A colour variation, linear) and `<set>_normal.png`, 1024 px, tiling | `blender -b --factory-startup --python textures.py -- --out <folder> --n 1024` then copy into `Assets/Resources/Gfx/Sets` |
| `sky.py` | the sky panorama: volumetric cumulus path traced over a drawn blue, the lower half a warm far land, the sun where the game's sun is | `blender -b --factory-startup --python sky.py -- --out <folder>/sky --sun-dir -0.501,-0.622,-0.602 --res 4096 --samples 128` then `sky.exr` into `Assets/Resources/Gfx/Sky` |
| `kit.py`, `surface.py` | helpers the two above import | |
| `make_gfxlit.mjs` | `GfxLit.shader` and `GfxLitInput.hlsl` from the project's own URP Lit (run again when URP is updated) | `node ArtSource/Gfx/make_gfxlit.mjs <project folder>` |

`--sun-dir` is the way the light travels in Blender's axes (Unity x, y, z = Blender x, z, y): the round's sun
(GameSky.SunWay -.5, -.6, -.62: from ahead and a little right, about 37 degrees up). Another sun angle wants the sky rendered again so the bright side of the clouds stays on the
sun's side.

Pictures to compare: `Assets/Scripts/PawnRushEditor/GfxShots.cs` (menu-less, batch):
`Unity.exe -batchmode -projectPath <copy> -executeMethod ChessFight.PawnRush.Editor.GfxShots.Run -gfxOut <folder> -gfxTag <name>`
plays each default section, puts a camera where the game's camera would be, and writes PNGs; `-gfxTune
"a:exp=0,con=20;b:lit=0"` compares looks (keys in GfxShots.Look). `-gfxFloors` writes, instead of pictures, which
material draws the floor the game's camera sees and how much of it (every material drawn once in an id colour and
read back); `-gfxTime 60` and `-gfxScan all` measure what the upgrade costs.

## Floor relief (2026-11-24 afternoon)

Floors fill half of the game camera's view, and half of that floor is the mini-game swatches (the floor census). The
swatch scripts (`ArtSource/PawnRush/MiniGames/*/Materials`, r502-r509) baked height and ambient occlusion from real
geometry, but only colour and normals came into the game. In Play, GameSurfaces gives a Gfx Lit twin whose base map
is `<swatch>_BaseColor` the packed `Assets/Resources/Gfx/Height/<swatch>_HeightAO.png` (R height, G occlusion) as its
`_ParallaxMap` and `_OcclusionMap`. Gfx Lit then does parallax occlusion mapping with it (Unity's
ParallaxOcclusionMapping: more steps the flatter the view, faded out by mip level), the relief's own sun shadow, and
dust in its joints. Depths come from the swatch's name (GameSurfaces.Depth: stone 3 cm, marble, tile and wood 8 mm,
cloth and metal 4 mm, over a 2 m tile).

| Script | Makes | Command used |
| --- | --- | --- |
| `pack_height.py` | `<swatch>_HeightAO.png`, 1024 px, from the swatches' `_Height` and `_AO` | `blender -b --factory-startup --python pack_height.py` (writes to `OUT`), then copy them into `Assets/Resources/Gfx/Height`, leaving out the flat ones (BallroomMarble, CreamMarble, DarkIron, MatteBrass, SoftMirror) |
| `r505_paving_height.py` | `PR_Early_Paving_HeightAO.png`: r505_paving.py's own stones (same seed), whose height it computed but never saved | `blender -b --factory-startup --python r505_paving_height.py` |

Tried and dropped: the moat floor's height integrated back from its normal map (the Tripo stone block's cracks came
out black and gritty).
