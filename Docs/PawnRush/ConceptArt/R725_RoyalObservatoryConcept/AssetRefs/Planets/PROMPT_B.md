You are generating PLANET TEXTURES for a 3D game art team. Use your image generation tool; do NOT write code. Generate exactly FIVE final images, one at a time and in this order, and copy each generated PNG into the current working directory with these exact names:

- RO_P5_mars.png (1536x1024 landscape)
- RO_P6_venus.png (1536x1024 landscape)
- RO_P7_mercury.png (1536x1024 landscape)
- RO_P8_saturn_ring.png (1536x1024 landscape)
- RO_P9_moon_disc.png (1024x1024 square)

Images P5-P7 are each ONE EQUIRECTANGULAR (latitude-longitude) planet map filling the whole frame: top edge north pole, bottom edge south pole, and the LEFT AND RIGHT EDGES MUST MATCH so the map wraps seamlessly around a sphere. For each image build the prompt as: STYLE (word for word) + that image's line below + AVOID (word for word), except where the line says otherwise. Never resize, crop or convert a result. Call the tool at most twice per image: if the result breaks its rule, generate once more naming it first, and keep the better one. This prompt is complete: do not read any other file in the repository and change no other file. After saving, reply with the five file paths and one short line describing each.

## IMAGES

- RO_P5_mars: Mars - rusty red and ochre deserts, darker brown basalt plains, a few large craters and a long canyon scar, pale dusty highlands, small white polar caps along the top and bottom edges.
- RO_P6_venus: Venus - thick creamy yellow and pale amber cloud cover in soft broad swirling V-shaped bands, gentle and luminous, no surface visible.
- RO_P7_mercury: Mercury - warm grey and pale brown cratered rock, many craters of all sizes with bright rays from a few young ones, fine detail.
- RO_P8_saturn_ring: Saturn's RING as a flat straight strip for a texture: the ring's cross-section from the INNER edge at the LEFT to the OUTER edge at the RIGHT, drawn as many thin VERTICAL stripes running top to bottom (each stripe one ringlet), uniform from top to bottom so any single row of pixels holds the whole ring profile. Pale gold, cream and soft tan ringlets of varied width, a clear dark gap (the Cassini division) about two thirds of the way out, a fainter thin outer ring. Fill the whole frame; no ring curve, no planet, no space background.
- RO_P9_moon_disc: the MOON as a flat round DISC seen straight on, filling the square image edge to edge (the disc touches all four sides; the corners outside the disc pure white): creamy pale grey surface with many craters of all sizes and a few darker maria, painted in the same style, evenly lit with no shading toward the edge - like a painted medallion, not a lit sphere. (For this image, ignore the STYLE's "fills the whole image" and the AVOID's "no white margins" only as far as the four white corners outside the disc.)

## Attached references (in this order)

1. `RO_T4_dome_and_planets.png` - THE TARGET PAINTING STYLE for planet surfaces: its bottom row of six small planet maps (painterly, rich, slightly stylized, soft swirling bands and clean shapes, gold-tinged highlights). Copy that style exactly, at a much higher level of detail.
2. `ref1_style_concept.webp` - the game's art direction (a lavish royal observatory of cream marble, gold and navy). The planets hang inside a glass celestial globe in that hall.

## STYLE (paste word for word into every image)

A flat texture map for a stylized 3D game, painted in the style of reference 1's planet maps: painterly, rich colour, soft swirling bands and crisp readable shapes, fine detail that stays sharp when seen up close, gentle gold-tinged highlights within the paint. Flat and evenly lit: no sphere shading, no terminator, no shadows, no specular glare, no space background, no stars - the texture fills the whole image edge to edge.

## AVOID (paste word for word into every image)

No text, letters, numbers, logos, watermarks, labels or arrows. No sphere, no 3D rendering, no globe shape, no black space background, no stars, no frame or border, no white margins.
