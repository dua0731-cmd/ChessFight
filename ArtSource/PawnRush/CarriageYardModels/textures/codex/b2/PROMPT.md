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
1. `rcy_light_oak.png` - Light oak (crates, barrels, fascia boards, hoist frames). About 2 x 2 m of ONE wide board surface with the wood grain running HORIZONTALLY (left to right) across the whole image, flowing painted grain lines, one or two small knots, honey colour (#D6995C average). No board edges.
2. `rcy_walnut.png` - Walnut (posts, beams, floorboards, coach body). About 2 x 2 m of one board surface, grain running HORIZONTALLY, rich warm red-brown (#824928 average) with darker flowing grain lines and one small knot.
3. `rcy_pale_timber.png` - Pale sawn timber (crane timber bundles, joists, trestles). About 2 x 2 m, grain HORIZONTAL, raw pale straw-beige sawn wood (#C9A97A) with faint saw marks and soft grain.
4. `rcy_dark_iron.png` - Dark iron (straps, brackets, lantern frames, chains). About 1 x 1 m of one hammered iron plate surface (no rivets): dark warm grey (#60524C), soft hammer dents, slight warm brown rust tint in places, a few fine scratches.
5. `rcy_matte_brass.png` - Matte brass (decorative metal, trims, bands). About 1 x 1 m of brushed matte brass: warm orange-brown brass (#A37235), soft mottling, faint fine brushing, a little darker patina in places. Evenly lit, no shine spot.
6. `rcy_gold.png` - Gold leaf / gilded metal (crowns, royal ornament). About 1 x 1 m: warm rich gold (#C9963A), soft mottled gilding, fine scratches. Evenly lit, no shine spot.
7. `rcy_coach_lacquer.png` - Bottle-green lacquered coach paint over wood. About 1.5 x 1.5 m: deep bottle green (#375231), faint horizontal brush strokes, very slight wear. Evenly lit.
