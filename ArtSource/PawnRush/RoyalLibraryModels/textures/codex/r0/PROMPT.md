You are generating SURFACE TEXTURES (albedo / base colour maps) for a stylized 3D game level. Use your image generation tool. Do NOT write code. Generate exactly the EIGHT images listed below, ONE image per material, each a SQUARE 1024x1024 image (ask the tool for the largest square size it offers), then copy each generated PNG into the current working directory with the exact file name given. Never resize, crop or convert a result. Call the tool at most twice per image. This prompt is complete: do not read any other file and change no other file. Reply with the file paths only.

## The target

Attached images 1 to 8 (`swatch_1.png` ... `swatch_8.png`) are the eight material swatches the client picked, in order. Each texture must look EXACTLY like its swatch's material, colour and painted style, only seen as a large flat close-up of the surface: same hue, same brightness, same saturation, same softness of the painting, same kind and scale of detail. Image 9 (`GC_D6_props_materials.png`) is the sheet they come from (bottom row) and shows the same materials on the props.

## Every image must be

- A SQUARE image of one material that fills the WHOLE frame edge to edge (no object, no rounded tile, no border, no background, no frame, no bevel, no vignette, no drop shadow).
- SEAMLESS / TILEABLE: the left edge continues into the right edge and the top into the bottom, so it can repeat in a grid without a visible seam.
- Seen perfectly flat, straight on from the front (orthographic, like a scanned swatch), NOT in perspective, no depth of field.
- EVENLY LIT albedo: no cast shadows, no directional light, no glare, no highlights, no reflections, no gradient from one side to the other, no darkening at the edges. Only the material's own colour and its painted surface detail. (The swatches show a highlight and a bevel because they are lit tiles; leave that lighting out, keep the material.)
- Stylized, hand-painted-looking 3D game material like the swatches: soft painterly colour variation, clean and readable, warm. NOT photographic, NOT noisy, NOT gritty.
- Absolutely NO text, letters, numbers, labels, logos, emblems, stamps, icons, characters or UI.

The props are modelled in 3D, so each texture is ONE continuous piece of material: no joints, no plank seams, no tile outlines, no frames, no rivets, no nails, no stitching borders.

## The images

1. `rl_cream_stone.png` - swatch 1: cream stone, the light squares of a giant chessboard and the stone walls. Warm cream (#E8C397 average), soft large cloudy patches a little peachier and a little paler, a few tiny shallow pits and speckles, very clean. Covers about 2 x 2 m.
2. `rl_walnut.png` - swatch 2: walnut wood, the dark squares of the chessboard, tables and bookshelves. Rich red-brown (#7D4323 average) with flowing, gently wavy grain running left to right, soft darker grain lines and lighter figure, satin. No planks. Covers about 2 x 2 m.
3. `rl_varnished_walnut.png` - swatch 3: varnished dark walnut, the polished frames and furniture trim. Deeper, darker, slightly redder than image 2 (#69371C average), finer straight grain running left to right, smoother and more even, a lacquered look but still no highlight. Only the wood surface, NOT the frame shape of the swatch.
4. `rl_polished_brass.png` - swatch 4: polished brass, corner caps, rims, rails, keys. Warm golden brass (#C58B3C average), soft even brushed sheen painted as gentle broad tone variation in NO particular direction, a few faint tiny scratches. No highlight streak.
5. `rl_red_leather.png` - swatch 5: red leather, giant chair upholstery and book covers. Deep brick red (#852B20 average), fine pebbled leather grain, soft slightly darker creases. No stitching, no tufting.
6. `rl_parchment.png` - swatch 6: parchment, book pages, paper notes, the typewriter paper. Warm pale parchment (#F5D19D average), soft blotchy fibres and very faint darker mottling, a few faint fibre streaks. No writing, no lines, no torn edges.
7. `rl_ink_black_glass.png` - swatch 7: ink-black glass / black enamel, the typewriter body and the inkwell. Very dark warm black (#231919 average), almost uniform, only a very subtle soft deep-plum and warm-brown variation. No reflections, no highlights.
8. `rl_cream_cloth.png` - swatch 8: cream robe cloth, the white grandmaster's robe. Warm cream (#DDB88B average), soft fine woven wool texture with a gentle diagonal twill, soft fabric shading variation. No folds, no drapes, no hems.
