You are making input images for an AI image-to-3D tool (like Tripo). Each image will become ONE static 3D game model of the "Royal Carriage Yard" course of ChessFight, a cute toy chess-kingdom party race. Use your image generation tool; do NOT write code and do not change any file except the PNGs you save here. Generate exactly NINE images, one at a time, in this order, and save each as PNG in the current working directory with these exact names. If the tool saves somewhere else (for example ~/.codex/generated_images), copy each file here under its name. Never resize, crop or convert a result.

## Attached images

1. ref_gate_towers_walls.png - the approved look of items 1 (outer gatehouse), 2 (rook tower family) and 3 (curtain wall kit)
2. ref_final_gate_section_end.png - the approved look of items 9 (final gatehouse) and 10 (section end)
3. RCY_07_architecture_modules.png - the whole module sheet, for style

Copy their shapes, proportions, stone, brass and wood exactly. Leave out everything this file says to leave out.

## Common prompt (start every image with this, then add that image's own lines)

A single stylized 3D game asset of a cute toy chess-kingdom castle, standing alone and centred on a plain pure white background, the whole object in frame with a wide empty margin on every side, seen from a front three-quarter view (about 30 degrees from the front, camera a little above the middle of the object). Chunky pale cream limestone in big soft-bevelled blocks with shallow joints, flat rook battlements (square merlons with gaps, like the crown of a chess rook), a matte dark brass band around the foot of every tower, smooth matte finish. Soft even white studio light, very light ambient occlusion, no cast shadow, no floor, no ground plane, no reflection.

Things that the game draws with code are NOT part of the model and must be left out: no door leaves, no gates, no portcullis, no signs, panels, progress bars or pictograms, no lamps, no gold handles or rings, no hinges hanging in the opening. A gate opening is an empty rectangular passage you can see straight through to the white background.

AVOID (every image): any background, scenery, sky, floor, ground shadow, vignette or gradient; flags, banners, pennants; windows, arrow slits, holes or openings in towers and walls other than the gate passage asked for; any text, letters, numbers, labels, logos, watermarks, frames or borders; characters, pawns, chess pieces, horses, statues, plants, props, crates; arches over the gate openings (lintels are flat); cone or pyramid roofs; shiny gold; glass; flames; damage or ruins; photorealism, sketch lines, outlines, cel shading.

## The nine images

1. CASTLE_01_outer_gatehouse.png - 1536x1024. The outer gatehouse of reference 1 item 1: a gate wall with flat rook battlements between two round rook towers, one wide rectangular gate passage in the middle under a flat stone lintel, the passage EMPTY (no door leaves) and seen through. The crowned-knight crest shield above the lintel stays as a carved stone shield with a matte brass relief of a crowned knight's head (no text). Towers solid, no arrow slits.
2. CASTLE_02_rook_tower_short.png - 1024x1024. The short, thick round rook tower of reference 1 item 2 (left one): a round shaft, a corbelled overhang and a rook crown of square merlons, brass foot band. Solid, no arrow slits.
3. CASTLE_03_rook_tower_tall.png - 1024x1024. The tall slim round rook tower of reference 1 item 2 (middle one), same parts. Solid, no arrow slits.
4. CASTLE_04_rook_tower_wide.png - 1024x1024. The wide round rook tower of reference 1 item 2 (right one), same parts. Solid, no arrow slits.
5. CASTLE_05_wall_low.png - 1536x1024. The low curtain wall segment of reference 1 item 3 (left one): a straight stone wall with flat square pilasters at both ends and rook battlements on top. Solid, no arrow slits. Both ends flat and clean so segments can be placed end to end.
6. CASTLE_06_wall_tall.png - 1536x1024. The tall curtain wall segment of reference 1 item 3 (middle one), same parts, much taller. Solid, no arrow slits. Both ends flat and clean.
7. CASTLE_07_wall_corner.png - 1024x1024. The corner piece of reference 1 item 3 (right one): a square corner block where two wall runs meet at 90 degrees, rook battlements on top, both open wall ends flat and clean. Solid, no arrow slits.
8. CASTLE_08_final_gatehouse.png - 1536x1024. The final gatehouse of reference 2 item 9: a tall gate wall between two round rook towers, the wall clearly taller than the gate passage, one tall rectangular passage under a flat lintel, the passage EMPTY (no door leaves, no gold rings, no progress-bar sign panel above it) and seen through. The crowned-knight crest shield on top of the wall stays as carved stone with a matte brass relief (no text). Towers solid, no arrow slits.
9. CASTLE_09_section_end.png - 1536x1024. The section end of reference 2 item 10: an open stone gateway of two square rook-crowned piers joined by a flat stone beam carrying a small matte brass crown relief, a short flight of four wide low stone steps up through it, and a white marble balustrade (balusters and a handrail) running down each side of the steps. The gateway is open (nothing in it).

## CHECK (never send to the tool; call the tool at most twice per image; if a check fails, generate once more with the failed checks named first and keep the better one)

- one asset, whole object, pure white background with nothing else;
- no door leaves, signs, panels, lamps, rings, flags, windows, arrow slits or text;
- gate passages are empty, rectangular and see-through, with flat lintels;
- the shape matches the reference item named.

## When done

Reply with the nine file paths, each file's size and resolution, and one short line describing each image. If a generation fails (for example a usage limit), say which images are missing and why.
