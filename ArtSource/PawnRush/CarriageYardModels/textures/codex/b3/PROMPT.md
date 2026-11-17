You are generating SURFACE TEXTURES (albedo / base colour maps) for a stylized 3D game level. Use your image generation tool. Do NOT write code. Generate exactly the images listed below, ONE image per material, then copy each generated PNG into the current working directory with the exact file name given. Reply with the file paths only.

## Every image must be

- A SQUARE image of one material that fills the WHOLE frame edge to edge (no object, no border, no background, no frame, no vignette).
- SEAMLESS / TILEABLE: the left edge continues into the right edge and the top into the bottom, so the image can repeat in a grid without a visible seam. No feature may be cut off at an edge in a way that would show when tiled.
- Seen perfectly flat, straight on from the front (orthographic, like a scanned material swatch), NOT in perspective, no depth of field.
- EVENLY LIT albedo: no cast shadows, no directional light, no highlights, no reflections, no sky, no gradient from one side to the other, no ambient-occlusion darkening at the image edges. Only the material's own colour and its small painted surface detail.
- In the style of the attached concept art (images 1 to 4): stylized, hand-painted-looking 3D game material, soft painterly colour variation, clean and readable, warm and sunny palette, NOT photographic, NOT noisy, NOT gritty.
- Absolutely NO text, letters, numbers, labels, logos, emblems, chess-piece icons, stamps, flags, banners, characters, people, animals, hands or UI.
- No blue anywhere (the client's colour rule), except where the material is naturally grey.

Attached image 1 is the concept's props-and-materials sheet: its bottom row has the material swatches (cream limestone, flagstone, shade stone, white marble, roof slate, light oak, walnut, dark iron, matte brass, functional gold, sand canvas) - match those colours and that painted look. Images 2 to 4 show the same materials in the level (the wagon yard, the sorting yard, the architecture modules).

IMPORTANT for the game's models: the stone blocks, paving slabs, wooden boards, roof tiles, rivets and planks are already MODELLED as separate 3D pieces. So each texture is the surface of ONE continuous piece of material: no joints, no mortar lines, no block or slab outlines, no plank seams, no tile shapes, no rivets, no nails, no bolts, no frames.

## The images
1. `rcy_sand_canvas.png` - Sand canvas (awnings, cargo covers). About 1 x 1 m of woven canvas: warm sand (#C9A273 average), visible fine weave, soft fold shading kept very subtle.
2. `rcy_burlap.png` - Burlap sack cloth. About 0.8 x 0.8 m of coarse jute weave, warm brown (#9E7A4C), slubby threads.
3. `rcy_straw.png` - Straw / hay bale surface. About 1 x 1 m of packed straw, stalks running HORIZONTALLY, golden yellow (#D2AE5A) with lighter and darker stalks.
4. `rcy_rope.png` - Hemp rope surface. About 0.4 x 0.4 m: twisted hemp strands running diagonally across the image as if a rope's surface were unrolled flat, warm tan (#9C7B4F).
5. `rcy_leather.png` - Saddle leather (harness, straps, coach seats). About 0.6 x 0.6 m: dark warm brown leather (#5B3320) with fine pebble grain and soft wear.
6. `rcy_velvet.png` - Deep bottle-green velvet (coach upholstery, cushions). About 0.6 x 0.6 m: very dark green velvet (#24361F) with soft crushed-pile variation. Not red.
7. `rcy_horse_coat.png` - Horse coat, NEUTRAL: about 0.6 x 0.6 m of short sleek horse hair, NEUTRAL LIGHT GREY (no hue, around #BDBDBD) with short hair strokes all running HORIZONTALLY; the game tints it brown, grey or black.
8. `rcy_horse_mane.png` - Horse mane/tail hair, NEUTRAL: about 0.6 x 0.6 m of long flowing hair strands running HORIZONTALLY, NEUTRAL GREY (no hue, around #9A9A9A) with lighter and darker strands.
9. `rcy_hoof.png` - Hoof horn: about 0.3 x 0.3 m of dark brown-grey horn (#3A302A) with fine vertical growth lines.
10. `rcy_plume.png` - Cream plume feathers: about 0.4 x 0.4 m of soft cream feather barbs (#F1E8D6) running HORIZONTALLY, fluffy, clean.
