You are generating PLANET SURFACE TEXTURES for a 3D game art team; they will be wrapped onto spheres. Use your image generation tool; do NOT write code. Generate exactly FOUR final images, one at a time and in this order, each 1536x1024 landscape, and copy each generated PNG into the current working directory with these exact names:

- RO_P1_jupiter.png
- RO_P2_saturn.png
- RO_P3_neptune.png
- RO_P4_earth.png

Each image is ONE EQUIRECTANGULAR (latitude-longitude) planet map filling the whole frame: the top edge is the north pole, the bottom edge the south pole, and the LEFT AND RIGHT EDGES MUST MATCH so the map wraps seamlessly around the sphere (no feature cut by the side edges; bands continue straight across). Near the poles, features stretch horizontally as an equirectangular map does. For each image build the prompt as: STYLE (word for word) + that image's PLANET line + AVOID (word for word). Ask the tool for 1536x1024. Never resize, crop or convert a result. Call the tool at most twice per image: if the result breaks the seamless or full-frame rule, generate once more naming it first, and keep the better one. This prompt is complete: do not read any other file in the repository and change no other file. After saving, reply with the four file paths and one short line describing each.

## PLANETS

- RO_P1_jupiter: Jupiter - horizontal cream, amber, rust and brown cloud bands with turbulent swirling edges and small white ovals, one large orange-red oval storm in the southern half.
- RO_P2_saturn: Saturn's body - soft pale gold, butterscotch and cream horizontal bands, gentle and smooth, faint fine banding, slightly darker poles.
- RO_P3_neptune: Neptune - deep saturated azure and cobalt blue with soft lighter blue bands, one dark navy oval storm and a few bright white streaky high clouds.
- RO_P4_earth: Earth - deep blue oceans, green and tan continents in invented (not real-world) shapes, swirling white cloud bands, white polar caps at the top and bottom edges.

## Attached references (in this order)

1. `RO_T4_dome_and_planets.png` - THE TARGET PAINTING STYLE for planet surfaces: its bottom row of six small planet maps (painterly, rich, slightly stylized, soft swirling bands and clean shapes, gold-tinged highlights). Copy that style exactly, at a much higher level of detail.
2. `ref1_style_concept.webp` - the game's art direction (a lavish royal observatory of cream marble, gold and navy). The planets hang inside a glass celestial globe in that hall.

## STYLE (paste word for word into every image)

A flat texture map for a stylized 3D game, painted in the style of reference 1's planet maps: painterly, rich colour, soft swirling bands and crisp readable shapes, fine detail that stays sharp when seen up close, gentle gold-tinged highlights within the paint. Flat and evenly lit: no sphere shading, no terminator, no shadows, no specular glare, no space background, no stars - the texture fills the whole image edge to edge.

## AVOID (paste word for word into every image)

No text, letters, numbers, logos, watermarks, labels or arrows. No sphere, no 3D rendering, no globe shape, no black space background, no stars, no frame or border, no white margins.
