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
1. `rcy_cream_limestone.png` - Cream limestone (the walls of a royal castle yard). About 2 x 2 m of one smooth dressed stone face. Warm cream (#E5C8A5 on average), soft large cloudy patches slightly peachier and slightly paler, a few tiny shallow pits, faint chalky bloom. Very clean.
2. `rcy_flagstone.png` - Flagstone (the yard's paving). About 2.4 x 2.4 m of one worn paving stone surface. Warm sandy cream a little pinker and darker than the limestone (#D8B797 average), smooth walked-on, soft tone patches, two or three faint hairline cracks, very slight dusty warmth. No slab outlines.
3. `rcy_shade_stone.png` - Shade stone (plinths, footings, stone under water). About 2 x 2 m of one dressed stone face. Warm mid grey (#A39790 average) with soft cloudy variation and a few tiny pits.
4. `rcy_white_marble.png` - White marble (balustrades, the cliff). About 3 x 3 m. Soft white (#E8DEDA) with gentle soft grey veins running diagonally, a few fine branching veins. Polished but evenly lit.
5. `rcy_roof_slate.png` - Roof slate surface (NOT individual tiles). About 1.5 x 1.5 m of one slate surface: warm mid grey (#756E6B), subtle layered flaky texture, a few lighter worn flake edges, slight warm brown cast in places. No blue.
6. `rcy_soil.png` - Packed earth / dusty ground at the yard's edges. About 2 x 2 m. Warm light brown dusty earth with tiny pebbles, a little dry grass at random, mostly flat.
