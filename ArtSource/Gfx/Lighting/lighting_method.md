# Interior Lighting Design Method: Pawn Rush castle halls

**Scope.** This method covers every indoor hall of courses 2 and 3. Each hall is redesigned one at a time, then implemented.

**Setup.** Unity 6000.3, URP 17.3 Forward+, realtime lights. Target look: Mario Kart World.

**Basis.** The method is built from four research digests: game lighting, architectural lighting, real palaces and spaces, and the URP engine. Every number comes from those digests or from arithmetic on them. Rules of thumb are marked as such, and so are this method's own syntheses.

---

## 0. Brief

**JY's request (11-24).** JY did not ask for "just brighter". JY asked for lights that are placed and composed well. The steps:
1. Learn how games and real interiors install light, how much light they put into a space, and how they make it beautiful.
2. Redesign each hall's lighting from its own concept.
3. Implement that design.

**JY's fixed rules**
1. **Interiors are bright**, like JY's daylight photos: a wood-panelled library, a long cream gallery with a gilt chandelier and golden floorboards, white carved Moorish halls with tiled floors, and pale dining rooms with tall windows. A closed room gets more lamps, never a dim mood. Drama comes from sun pools, gold and scale, never from darkness.
2. **One time of day: morning.** The sun and sky never change per room. An interior only receives less of them, through its own openings. Recorded exception: 3-2_RoyalObservatory is night by its concept (JY, 11-24 evening).
3. **Warm only at the light sources.** Shade, haze and grade never get a yellow or night cast.
4. **Each hall is lit from its own concept and architecture.** No single look per course, and no dusk.
5. **Pictures are judged with the joining corridors in place.** The route must read, and each section's goal (its exit door) must be lit.
6. **Course moods.** Course 2 has just entered the castle: lighter, but the feeling "I am inside a splendid castle" must land at once. Course 3 is solemn, overwhelming and grand: the game's ending.

**Why the current rig falls short, in research terms**
- **Brightness came from the wrong place.** It came from ambient light, exposure and modest lamps.
- **The lamps are too weak.** URP light falls off as 1/d². The rig's lamp and uplight intensities (about 2–9) give sun-comparable light only within about 1.5–2 m. That is roughly ten times too weak for halls many metres across.
- **Every map has its own sun, grade and sky tint.** The suns differ in intensity (1.4–3) and direction. This breaks rule 2.
- **Closed halls take their ambient from the outdoor sky probe.** Every surface gets the same sourceless, blue-leaning fill from every direction. It looks flat, and the shade looks like night.
- **Rig defaults are warm where they should be neutral.** Uplight is (1, .78, .52), lamp (1, .8, .58), goal (1, .86, .62). The heart light (1, .6, .32) defaults to a 10 m range. In the picture rounds, warm uplight on cream walls read yellow.
- **Several concept lines in GfxMapLights ask for what the research calls failure:**
  - "warm uplight washing every column and wall" (stair hall)
  - "great warm pools on the dance floor" (ballroom)
  - "the side aisles in cool shade" (procession)
  - "the hall's edges falling away into shade" (chess hall)
  - "the lasers bright in the dim" (bedchamber)
  - "a dark Gothic hall" (war room)

  Each hall's design sheet replaces its line.

---

## 1. Principles

**P1. A room looks bright when its walls and ceiling are bright.**
- Flynn's studies: brightness drives clarity, light on the walls (peripheral light) drives pleasantness, and spaciousness needs bright walls.
- Shepherd et al. found rooms rated gloomy when walls and corners were dim, even with the centre lit. Lighting the edges, at the same overall luminance, turned "gloomy" into "clear, pleasant, spacious".
- Cuttle: perceived brightness follows the light reflected by all room surfaces, not floor lux. He calls this mean room surface exitance (MRSE): 100 lm/m² looks acceptably bright, 300 bright, 1,000 distinctly bright.
- A race camera sees mostly walls, columns, vaults and the far end.
- **Rule:** a hall gets bright through its vertical surfaces and its vault, never through exposure or flat ambient.

Sources: https://nvlpubs.nist.gov/nistpubs/Legacy/IR/nistir5119.pdf ; https://www.cibse.org/media/5fymxkhw/c-cuttle-a-new-direction-for-general-lighting-practice-lighting-research-technology-volume-45-issue-1-2013.pdf

**P2. Surfaces are half the lighting.**
- Pale ceilings and walls multiply every lamp. Dark finishes swallow the light.
- EN 12464-1 useful reflectances: ceiling 0.7–0.9, walls 0.5–0.8, floor 0.2–0.4.
- In Mirror's Edge's global illumination, dark walls simply made dark rooms. The Level Design Book keeps main albedos at 50–100% brightness.
- JY's photos are bright because their big surfaces are cream, white, gilt and polished. The adopted floor swap (navy slate replaced by cream marble, parquet, white tile and walnut) is this principle at work.

Sources: https://www.fagerhult.com/knowledge-hub/EN-12464-1/luminance-ratios/ratios-for-working-areas--room-surfaces/ ; https://www.slideshare.net/slideshow/henrikgdc09-compat/3128065 ; https://book.leveldesignbook.com/process/lighting

**P3. All three kinds of light are present (Richard Kelly).**
- *Ambient luminescence:* the soft, bright base that makes the whole space visible.
- *Focal glow:* brighter, directed light that ranks what matters.
- *Play of brilliants:* light you look at for itself, such as flames, crystal, gilt and water.

Ambient alone is flat. Accents without a bright base look like stage lighting.

Source: https://www.fagerhult.com/knowledge/light-and-people/the-experience-of-light/the-three-basic-types-of-lighting/

**P4. Build in layers, from global to local.**
- The Level Design Book builds game lighting in passes, each with one job:
  1. Global: sun, sky, ambient and a first bounce.
  2. Wayfinding: the critical path.
  3. Gameplay: readability.
  4. Detail and mood, stopping before this pass undoes the earlier ones.
- Robert Yang adds an albedo pass first, because bounce depends on how bright the surfaces are.
- Naughty Dog roughs in sun, sky, fog and bounce before fixing spots that are too bright or too dark to play.
- Architectural practice runs in the same order: daylight, then ambient from the big surfaces, then focal glow, then the brilliants (ERCO).
- Section 5 follows this order.

Sources: https://book.leveldesignbook.com/process/lighting ; https://www.blog.radiator.debacle.us/2018/03/gdc-2018-how-to-light-level-slides-and.html ; https://80.lv/articles/learning-lighting-for-video-games/

**P5. Every light has a visible source.**
- The player can always point to where the light comes from: a window, skylight, chandelier, sconce, lantern or hearth.
- One source may own several engine lights (key, helper fill, bounce, rim), as long as the result still reads as coming from that source. The Level Design Book's example has 2 visible fixtures motivating 11 or more engine lights.
- Emission lights nothing in realtime. So every glowing fixture needs a matching light, and a light with no visible source reads fake.
- Hidden washes and uplights count as the room's daylight spreading around a pale room. Real buildings add electric light that looks like the same daylight where the windows don't reach (called PSALI). Because they stand in for daylight, these lights must look like daylight: neutral.

Sources: https://book.leveldesignbook.com/process/lighting ; https://80.lv/articles/looking-at-the-lighting-workflow-in-assassin-s-creed-valhalla

**P6. One key per space.**
- Indoors, the key is the daylight entering through the room's real openings. Set it first.
- Naughty Dog's Gabe Betancourt says a correct key lays out about 80% of the work. When a scene turns muddy, he deletes the lights and rebuilds from the key.
- The key-to-fill ratio sets the mood. TV comedy uses 2:1–3:1 and drama about 4:1. Steve Theodore suggests about 3:1 as the starting point for spaces; much lower flattens form.
- **Rule for bright halls:** 2:1–3:1 on the race line.

Sources: https://gamedeveloper.com/art/-i-last-of-us-i-lighting-artist-offers-devs-a-tip-set-the-mood-with-a-single-light ; https://www.gamedeveloper.com/art/lighting-design-fundamentals-using-contrast-in-your-game

**P7. Build hierarchy with ratios, and give each view one hero.**
- Perceived steps (Cuttle): 1.5:1 is noticeable, 3:1 distinct, 10:1 strong, 40:1 emphatic.
- ERCO: 2:1 is barely perceptible, an accent needs 5:1 or more, and 10:1 is dramatic.
- EN 12464-1 steps down from task to surround to background: 500, 300 and 100 lx, so the background gets a third.
- IES comfort limits: task to its surround up to 3:1, task to remote surfaces up to 10:1, a luminaire or window to its adjacent surface up to 20:1.
- Ten equal accents cancel each other out.
- **Game translation.** The Neutral tonemapper squeezes highlights: scene-linear 0.5 shows as 173/255, 1.0 as 208, 2.0 as 236, and 5.0 as 255. A large surface pushed to 10x its surroundings either clips to flat white or forces the surroundings dark.
- **Rule:** large heroes get 2–3x ("distinct"). Strong and emphatic ratios belong only to small things: sun glints, sky seen through an opening, flames and crystal.

Sources: Cuttle (above) ; https://www.erco.com/en/guide/outdoor-lighting/accentuation-1872/ ; https://sonel.pl/en/knowledge-centre/press-articles/illuminance-measurements/en-12464-12021-key-lighting-requirements-for-indoor-work-places ; https://complaints.lsu.edu/ehs/files/Section_V_Part_E_Illumination_for_Occupational_Tasks.pdf ; https://docs.unity.com/en-us/engine/6000.3/manual/post-processing-and-full-screen-effects/urp/effect-list/tonemapping

**P8. Fixtures follow the architecture.**
- The eye reads a layout of light fittings as a figure, so the layout must follow the building's bays, axes and symmetry (ERCO):
  - one fitting per bay or pilaster;
  - matching pairs flanking doors, windows, fireplaces, mirrors and beds;
  - corner fittings on the 45° line;
  - fittings on pillars, capitals and cornices, never cut into vaults;
  - grazing light only where the texture means something (carved stone, tile relief); washing everywhere else.
- Palaces put a light on every pier between windows. At Schönbrunn, the Great Gallery's two great chandeliers held only 144 of the 1,104 candles burned in its two galleries, so most of the rest were probably on the walls. (That last point is the researcher's inference.)
- Chandeliers go on the main axis, in one row. Versailles removed its extra rows because they formed a false ceiling, hid the painted vault and broke the rhythm of windows and mirrors. It kept 6 chandeliers in 73 m.

Sources: https://erco.com/en/designing-with-light/lighting-knowledge/lighting-design/arranging-luminaires-7645 ; https://en.chateauversailles.fr/press/restaurations/revealing-hall-mirrors ; https://www.schoenbrunn.at/en/about-schoenbrunn/the-palace/tour-of-the-palace/great-gallery

**P9. Light leads the race.**
- The goal is the brightest framed plane in view, lit from the side the player approaches.
- The main line is brighter than the side bays.
- Each hall shows a landmark from its entrance.
- Valve's Left 4 Dead playtesters reliably went where the light was.
- Flickering breadcrumbs and glowing everything turn into "a giant arrow" (Robert Yang).
- The D6 patterns: focal point; focal frame (a matching pair at a door); path/boundary (an even wash along the route); space; space with focal point; space with framed path.

Sources: https://book.leveldesignbook.com/process/lighting/d6-lighting ; https://gamedeveloper.com/art/in-depth-how-valve-makes-art-to-enhance-gameplay ; https://www.blog.radiator.debacle.us/2015/03/lighting-theory-for-3d-games-part-2.html

**P10. One daylight white balance; warmth only at the sources.**
- Mirror's Edge fixed its white balance early and kept its materials neutral.
- Real practice:
  - daylight and wall washes stay neutral;
  - electric light that supplements daylight gets a higher colour temperature;
  - different colour temperatures are not set side by side, because that makes coloured shadows;
  - about 2,700 K is kept for flames and filaments.
- Once eyes adapt, colour temperature barely changes how bright or pleasant a room seems; light level does (Fotios).
- Warm fill, warm haze or a warm grade turns the whole room yellow. Sky-blue fill makes the shade look like night.
- **Rule:** shade stays near neutral.

Sources: https://www.slideshare.net/slideshow/style-and-gameplay-in-the-mirrors-edge/127661765 ; https://buildingconservation.com/articles/lighting-historic-churches/lighting-historic-churches.htm ; https://eprints.whiterose.ac.uk/98531/17/Fotios_2017_a_new_Kruithof_graph.pdf

**P11. Chandeliers are jewellery; the windows were the light.**
- By the spaces researcher's arithmetic, more than 3,000 candles could put at most about 20–50 lux on the Hall of Mirrors' floor.
- Historic Royal Palaces lit 50 real wax candles in Kensington's Cupola Room and found the light very low.
- Candlelit splendour came from many bright points, multiplied by mirrors, crystal and gilding.
- Crystal sparkles only with small, clear points of light. Frosted sources, cool light and big bloom halos turn it into a blurry blob.

Sources: https://en.chateauversailles.fr/discover/estate/palace/hall-mirrors ; https://www.hrp.org.uk/blog/let-them-glow-lighting-kensington-palace/

**P12. The pieces always read.**
- Directional light shows form. Ambient-only light flattens shapes into silhouettes (Yang).
- TF2 edges its characters with rim light and sends shade to cool rather than black. VALORANT adds a fresnel rim and clamps the ambient.
- Good modelling means a vertical-to-horizontal light ratio (Ez/Eh) of 0.3–0.6.
- Our race camera sees the pieces' backs, with the sun and the goal ahead. So the pieces are backlit, which gives a free rim light, but the side we see is in shade. That side must stay cream; this is the job of the character fill light, on its own rendering layer.

Sources: https://wiki.teamfortress.com/wiki/Illustrative_Rendering_in_Team_Fortress_2 ; https://www.riotgames.com/en/news/valorant-shaders-and-gameplay-clarity

**P13. Daylight reaches only so far.**
- Side windows light a band about 1.5–2.5x the window-head height deep (3–3.5x under clear skies).
- Rooflights give about 2–3x the light of an equal vertical window, and they light both long walls equally. That is why long galleries got top light: the Louvre's Grande Galerie, the Galerie des Batailles, Dulwich and Labrouste's domes.
- Beyond the daylit band, add electric light that looks like the same daylight.

Sources: https://peer.asee.org/daylight-rules-of-thumb-experimentally-examined.pdf ; https://en.chateauversailles.fr/discover/estate/palace/gallery-great-battles

**P14. Copy the designed daylight, not today's conserved rooms.**
- Today's low levels have other causes:
  - museum limits: about 50 lux for sensitive objects, 150 for oil paintings, 300 for stone and metal;
  - the Sistine Chapel's 50–100 lux;
  - real hammams at 60 lux or less.
- These numbers protect objects or reflect old building technology.
- JY's photos show the rooms as they were designed.

Sources: https://www.canada.ca/en/conservation-institute/services/agents-deterioration/light.html ; https://orca.cardiff.ac.uk/119338/1/sustainability-10-03984-v2.pdf

---

## 2. Shared frame (set once, identical in every hall)

| Item | Setting | Why |
|---|---|---|
| Sun | One sun for the whole game: the round sun from ahead (SunWay -.5, -.6, -.62, about 37° high), colour (1, .98, .96). No per-map direction or intensity. | Rule 2. Nintendo says fixed time and weather made Mario Kart World's clearest route easier to build. |
| Sky | One sky, with no per-map tint. | Rule 2. |
| Tonemapper | Neutral. | ACES pulls mid-grey down about 0.7 EV and halves deep shade: a value of 0.05 lands at 33/255 instead of 65. |
| Exposure | One global value. The engine researcher suggests Post Exposure 0. | URP 17 has no auto exposure. Post Exposure runs after bloom and scales the outdoors too, so it cannot change ratios inside a hall. |
| Grade | One global grade: no per-hall grade, no tint, vignette 0.15 or less in halls. | Per-map grades break rule 2. Strong contrast and vignette darken shade. |
| Bloom | Threshold about 1.0–1.1, intensity 0.3–0.6. Lit walls and floors stay at scene-linear 1.2 or below. Fixture emissives sit at 2–6. | Only bulbs, flames, glass and sunlit highlights glow. These are the engine researcher's starting values. |
| SSAO | Intensity about 0.6–1.2, Direct Lighting Strength 0.1–0.2, falloff 40–60 m. | The default intensity of 3.0 leaves grey halos in bright rooms. |
| Haze and shafts | Neutral grey-white and thin. Shafts stay at the mild 0.6/0.015 already chosen. | Shafts at 1.4 turned the outdoors milky and the library orange. Dense haze hides silhouettes; Valve lightened Left 4 Dead's fog for this reason. |
| Rendering layers | Hall slots A, B and C, alternating along the course. One bit for characters. One "bounce" bit that Gfx Lit treats as diffuse-only. 8 layers or fewer. | Unshadowed lamps shine through walls into the next hall. Fill lights must not print highlights on polished floors. |
| Reflections | One realtime probe per hall. Box = the room, 64–128 px, blend distance 1–2 m. Render it once, after the hall spawns and its lights are final, with Individual Faces time slicing. Box Projection and Probe Blending stay on. | Polished floors, gilt and water must reflect the room, not the outdoor sky. That reflected sky is why Fortnite avoided indoor specular before Lumen. Mario Kart World also uses realtime cubemaps. |
| Baking | None. | See below. |

**Why there is no baking.**
- Halls are prefabs placed at run time. Adaptive Probe Volumes and lightmaps cannot follow them; only a single global offset exists.
- URP forces area lights to Baked mode, and a light's Indirect Multiplier only scales GI. So neither does anything here.
- Bounce light is therefore faked with lights (layer L6).
- Mario Kart World is mostly baked. If the halls ever get fixed positions, baking becomes the better route.

**Two notes.**
1. **Exposure.** The game researcher proposed a per-hall Post Exposure of +0.3 to +1 EV, blended at doorways, as a stand-in for eye adaptation. It is not part of this method by default, because JY rejected brightness that does not come from light. If it is ever used: the low end only, applied last, and only if JY asks for a stronger indoor/outdoor adaptation.
2. **Contrast.** Today's global grade has contrast about 30; the engine researcher suggests 0–10. Never change it per hall. If interior shade still falls below a third of the field once the fill lights are right, lower the contrast globally and re-check course 1 with JY.

Sources: https://www.nintendo.com/en-gb/News/2025/May/Ask-the-Developer-Vol-18-Mario-Kart-World-Chapter-2-2832717.html ; https://nintendoeverything.com/mario-kart-world-tech-analysis-including-frame-rate-and-resolution/ ; https://docs.unity.com/en-us/engine/6000.5/manual/render-pipelines/universal-render-pipeline/introduction/installing-and-configuring-urp/migrating-from-hdrp/convert-hdrp-lighting-to-urp ; https://docs.unity.com/en-us/engine/6000.3/manual/post-processing-and-full-screen-effects/urp/effect-list/bloom ; https://docs.unity.com/en-us/engine/6000.3/manual/lighting-overview/lighting/rendering-layers/introduction ; https://docs.unity.com/en-us/engine/6000.3/manual/lighting-overview/reflections/advanced-ref-probe ; https://docs.unity.com/en-us/engine/6000.3/manual/lighting-overview/lighting/probevolumes/concept ; https://docs.unity.com/en-us/engine/6000.3/manual/lighting-overview/lighting/light-component

---

## 3. Measure before placing lights

**Two currencies**
- **E (sun units):** the light a surface receives, relative to a directional light of intensity 1 striking it head-on. URP Lit's diffuse term is albedo x light colour x attenuation x N·L, with no 1/π. So a surface's scene-linear value is about albedo x E. Use E to size lights.
- **Screen value:** scene-linear before tonemapping, or 8-bit after it. Use it to judge pictures.

**Exterior reference, E_ext.** This is the E of a sunlit, level, pale exterior path.
- The engine researcher's example sun (intensity 2.2 at 50°) gives E_ext of about 1.7.
- The project's sun is about 37° high, where the same intensity gives about 1.3.
- Measure the real value in the game. Then scale every E target in section 4 by E_ext / 1.7.

**Tools.** These are the engine researcher's proposals; build them once.
- **Brightness meter.** Render a 64–128 px cubemap from the hall centre, with post-processing off and emissive fixtures hidden, and average it. This approximates Cuttle's MRSE. Target: at least 1/2–2/3 of the same measurement taken outdoors.
- **Grey-albedo view.** Force albedo to 0.5, so E = pixel value / 0.5.
- **Greyscale view** (Saturation -100) to judge the value hierarchy.
- **URP Rendering Debugger** albedo validation, for base colours outside the target range.
- **Frame Debugger**, for counting visible lights and shadow slices.

**Check this first:** confirm that Gfx Lit uses URP Lit's diffuse term. If it adds a ramp or wrap, the formulas in 4.3 are only approximate and the meter decides.

---

## 4. Quantitative targets

### 4.1 Real interiors (maintained illuminance)

Use these figures to rank rooms against each other and to set the ratios inside each room. Do not use them as an absolute look. Real rooms run about 10 stops below sunlit exteriors (EV 4 against EV 14), and the eye adapts; a URP camera does not. Fotios: below about 300 lx a room may feel unpleasant, and 500 lx is enough.

| Space (our hall) | Real reference | Ratios that transfer |
|---|---|---|
| Entrance hall, foyer, stairs (grand stair hall) | EN 12464-1:2021 entrance hall 100 lx (modified 200), walls 50, ceiling 30. IES lobby 200–300, stairs 50–100. | Foyers sit above the minimum because the eye arrives from outdoors (Glamox). Stair contrast 5:1 or less. |
| Workshop (royal workshop) | EN rough work 300, medium 500, fine 750, precision 1,000. IES 300–750. | Task 750, then at least 500 in a 0.5 m band around it, then a background of a third (about 170). Even light, no isolated pools. |
| Reading room, library | EN reading 500 (modified 750), walls 100, ceiling 50, uniformity 0.60, glare class UGR 19. Bookshelves 200 measured on the shelves (300). | Shelves at about 0.4x the desks. Desk:surround:wall 10:4:3, or the restful 10:5:2 (van Ooyen). |
| Ballroom, function room | No EN or IES value; lit for atmosphere. | Cuttle's MRSE: 300 (bright) to 1,000 (distinctly bright). |
| Pool hall (laundry, "bathhouse") | 200–300 lx at the water; about 300 average with uniformity 0.7 or more for recreation. | No fittings over the water; fittings 1–2 m to the side. Real hammams (60 lx or less) are not a model. |
| Church nave (procession) | CIBSE main body 100–200, nave about 150, chancel 200, pulpit and choir about 300. | Brightness rises up the axis to the altar, about 2x per zone. |
| State hall with a stage (chess hall) | No standard. Nearest analogue: IES museum exhibit 300–500. | In real practice the stage is the brightest horizontal plane, 3–10:1 over its surround. |
| Bedchamber | EN lounge 200 (modified 300), walls 75, ceiling 50. Bedroom 150–300 (Unity HDRP sheet). | Restful 10:5:2. Cove light reads as relaxing and private (Durak). |
| War council room | EN conference value not verified. Office 300–500 (Unity HDRP sheet) as an analogue. | Balance horizontal and vertical light; the table is the brightest surface. |
| Corridors (section joins) | EN 100 lx at the floor, walls 75, ceilings 50 (2011 edition). IES 50–100. | A little below the hall they lead into. |

**Transfer rule (this method's synthesis): compress, don't copy.**
- Every hall sits inside the game's field band (4.2).
- The table sets the order within that band: the entrance hall and the task rooms (workshop, reading room) at the top; the nave and the bedchamber lower.
- Inside a hall, keep the table's ratios (task:surround:background, shelves:desks, nave:choir:altar), compressed toward the game ladder in 4.2.

### 4.2 Game targets

These assume E_ext of about 1.7. Scale them by your measured E_ext.

| Quantity | Target | Basis |
|---|---|---|
| Lit field: walls and floor along the route | E 0.8–1.2. That is about 0.5–0.7x E_ext: interiors sit 1.5–2:1 below the sunlit outside, a "noticeable" step. | Engine researcher; Cuttle |
| Course split (this method's synthesis) | Course 2 at E 1.0–1.2; course 3 at E 0.8–1.0 | JY's course moods |
| Walls and vault on screen | At least as bright as the floor; wall E at 0.7–1.3x floor E | Flynn; van Ooyen; engine researcher |
| Shade anywhere in the playable view | At least a third of the field (E 0.35 or more); small contact shadows excepted | EN background rule; Shepherd's gloom studies; engine researcher |
| Key:fill on the race line | 2:1 (course 2) to 3:1 (course 3) | Game researcher; Theodore |
| Ambient base | E 0.3–0.5, neutral, saturation under about 10% | Engine researcher |
| Sky fill, measured at the surface it lights | E 0.2–0.4 | Engine researcher |
| Sun patches | Direct sun plus fills, about 1.5–2x the field | Arithmetic from the rows above |
| Secondary accents | 1.5–2x the field | Cuttle's "noticeable"; engine researcher |
| Hero (goal door, stage, board, table) | 2–3x the field over its whole area (focal E 1.5–2.5). The goal sits 0.5–1 stop above the route average. | Engine and game researchers |
| Zones toward the goal | Entry and aisles at 0.6–0.8x the field, then steps of about 1.5–2x up to the goal | Spaces researcher; church practice |
| Brilliants | 10x or more, up to clipping, always tiny | Spaces and architecture researchers |
| A window or opening against its lit wall | 20:1 at most | IES |
| Stairs | Contrast 5:1 or less | ERCO; IES stair guidance |
| Albedo | Walls and ceilings about sRGB 180–230; floors about 110–200; nothing large below about 60 | Game researcher; EN reflectances |

**Reading the screen under Neutral tonemapping:** scene-linear 0.35 shows as 153/255, 0.5 as 173, 0.7 as 191, 1.0 as 208 and 2.0 as 236. A cream wall (albedo 0.7) at E 1.0–1.4 reads about 190–210/255.

### 4.3 Sizing URP lights

URP has no physical light units. Intensity is a plain multiplier on the linear light colour. Attenuation is 1/d² times a smooth window W, so:

`E = I x W(d/R) x cosθ / d²`, where `W(x) = (1 - x⁴)²`. Solved for intensity: `I = E x d² / (cosθ x W)`.

Here d is the distance, R the light's Range, and θ the angle between the light's direction and the surface normal.

| d/R | 0.3 | 0.5 | 0.6 | 0.7 | 0.8 | 0.9 |
|---|---|---|---|---|---|---|
| W | 0.98 | 0.88 | 0.76 | 0.58 | 0.35 | 0.12 |

**Range.** Set Range to 1.5–2x the distance to the farthest surface the light must reach.
- Too short: hard circular pools, with dark walls just outside them.
- Too long: Forward+ pays for every pixel the light covers, and the light leaks into other halls.

**Intensity for E = 1**, face-on, with Range = 2d. For Range = 1.5d, multiply by about 1.36. Intensity scales linearly with E.

| d | 2 m | 3 m | 4 m | 5 m | 6 m | 8 m | 10 m | 12 m | 15 m |
|---|---|---|---|---|---|---|---|---|---|
| I | 4.6 | 10 | 18 | 28 | 41 | 73 | 114 | 164 | 256 |

The 5, 12 and 15 m values are computed with the same formula. A URP spot is not normalised by its cone size, so the table holds on the cone axis; the light fades between the inner and outer cone angles.

**Worked values** (from the engine researcher unless marked):
- **Wall-wash spot**, 5 m from the wall at 45°: I about 36 for E 1.
- **Column uplight**, 1–1.5 m off an 8 m shaft, aimed at mid-height. The light grazes the surface (cosθ 0.24–0.35), so I is about 60–80.
- **Chandelier room light**, 6 m above the floor: I about 40 with R 15 gives E about 1 directly below it, but only about 0.25–0.35 on walls 10 m away. The walls need their own lights.
- **Focal pool** from 8 m, R 16: each +1 E costs about I 73. Cuttle's rule for the light to add: E = field x (TAIR - 1), where TAIR is the target-to-ambient illuminance ratio. So TAIR 2 over a field of 1 needs about I 73, and TAIR 3 about I 146 (computed).
- **Decorative glow**: I 1–4 with R 1.5–3 m makes a warm halo about 1–2 m around the fixture. Today's sconce (R 6 m, I 1.4) already gives that halo: E 1.4 at 1 m and 0.34 at 2 m (computed). Its long range only costs time and leaks.
- **Sun-patch bounce**: a point light about 1.5x the patch radius below the floor, with I about albedo x E_sun x depth². Example: albedo 0.5, E_sun 1.7, radius 2 m and depth 3 m give I about 7.7 and R about 10 m.
- **Warm colours** deliver less brightness at the same Intensity, because Intensity multiplies the colour. Size warm lights by measurement.

Sources: https://raw.githubusercontent.com/Unity-Technologies/Graphics/master/Packages/com.unity.render-pipelines.universal/ShaderLibrary/RealtimeLights.hlsl ; https://docs.unity.com/en-us/engine/6000.3/script-reference/unityengine/rendering/graphicssettings/lightsuselinearintensity ; https://docs.godotengine.org/en/4.7/tutorials/3d/global_illumination/faking_global_illumination.html

### 4.4 Counts, shadows and cost (PC)

**Rig size per hall** (engine researcher):
- the shared sun;
- one sky fill per large opening;
- 2–4 bounce lights;
- 1–3 chandelier room lights, each with its warm glow;
- 6–12 washers and uplights;
- 1–2 focal spots;
- 8–16 short decorative glows. Long halls get more where the pier rhythm asks for them. Use one light per tight group, as Roblox's tutorial does per candle cluster.

That is about 20–40 lights per hall, and 40–80 visible when the next hall is in view. Forward+ allows 256 per camera, including the main light.

**Overlap.** About 8 lights or fewer over any pixel, and 12 or fewer at hot spots (engine researcher). Range is the main cost lever.

**Shadows.**
- The sun casts shadows.
- Add 0–2 shadowed spots per hall (2–4 per view), at the 512–1024 resolution tier, and only where piece contact shadows matter: the stage, the board, the goal.
- No shadowed point lights. Each one renders 6 atlas slices every frame, and URP has no shadow caching. At most one hero chandelier may be an exception.

**Patterns without shadows: use cookies.**
- Lattice, leaded-glass and window-grid patterns: a cookie on a spot aimed along the sun's direction.
- Chandelier frames: a cubemap cookie on a point light.
- Cookie atlas 2048 px in a Color format; 256–512 px per cookie. Spot outer angles 160° or less.

**Leaks.**
- Unshadowed lights shine through walls, so limit them with rendering layers or ranges.
- To stop sun leaks: use a shadow-only lid where needed, Two Sided shadow casting on single-faced walls, and glass that casts no shadow. Keep the main light's shadow distance (120 m today) at least as long as the hall's longest view.

**Distance fades.** Fade out the lights of halls that are not in view, with intensity fades so nothing pops, as Unity's Garden sample does.

Sources: https://docs.unity.com/en-us/engine/6000.3/manual/lighting-overview/lighting/light-limits-in-urp ; https://docs.unity.com/en-us/engine/6000.3/manual/lighting-overview/lighting/shadows-in-urp/shadow-resolution-urp ; https://docs.unity.com/ko-kr/engine/6000.5/manual/lighting-overview/lighting/shadows-in-urp/shadows-optimization ; https://create.roblox.com/docs/tutorials/use-case-tutorials/lighting/enhance-indoor-environments

---

## 5. The layer stack for one hall

Build in this order, and measure after each layer. Layer names match the GfxMapLights plan layers: daylight, general, wash, uplight, decorative, accent, bounce. Strengths are relative to the hall's lit field F (section 4.2).

| Layer | Kelly role | Job | Strength | Colour | Shadows |
|---|---|---|---|---|---|
| L0 Surfaces | (base) | Send the light back | Walls and ceiling sRGB 180–230; floors 110–200 | Material | – |
| L1 Daylight | Key, time of day | Direction, form, sun pools | Sun patches about 1.5–2F; sky fill E 0.2–0.4 | Sun (1, .98, .96); fill neutral | Sun only |
| L2 General | Ambient luminescence | No black; sets the shade floor | E 0.3–0.5 (1/3–1/2 F) | Neutral | None |
| L3 Wash, uplight | Ambient luminescence | Brightness of walls, vault, columns | Walls at 0.7–1.3x floor E | Daylight family, about 5,000–5,500 K | None |
| L4 Decorative | Play of brilliants | Identity, rhythm, sparkle, local warmth | Halo E about 1–2 within 1–2 m | Warm, at the source only | None (cookies) |
| L5 Accent | Focal glow | Goal, hero, a few secondaries | Hero 2–3F; secondaries 1.5–2F | Neutral, unless it is a warm fixture's own glow | 0–2 spots |
| L6 Bounce, fill | Ambient (faked GI) | Colour bleed, lift near bright areas, piece fill | 10–40% of the source it echoes | Albedo x source colour | None, diffuse-only |
| L7 Finish | Brilliants | Reflections, sparkle, haze, contact AO | – | Neutral haze | – |

### L0 Surfaces (before any light)

- Check every large surface with the albedo validation. Targets (game researcher): walls and ceilings about sRGB 180–230, floors about 110–200, nothing large below about 60.
- Polish where the archetype has marble or parquet. Use gilt and mirrors where the light should multiply.
- Dark large surfaces need far more light. Dark wood panelling (albedo about 0.25) needs about 2.5x the light of cream; navy slate (albedo 0.1–0.15) about 3–5x.
- For dark large surfaces, first propose a material change to JY, as was done with the floors. Otherwise, budget the extra light in L3.
- Relight whenever a material's colour or brightness changes (The Last of Us team).

### L1 Daylight (layer: daylight) – the key

**Job:** direction, form and time of day. The hall receives less of the same morning.

**Sun**
- Never rotate the sun per hall.
- Draw the sun's direction (about 37° high, from ahead) on the hall's plan and section. Let sun patches fall wherever the real openings put them.
- Patches that land on the route become route accents.
- A low sun from the far end never clears 13–17 m walls. Such halls get no sun patches; their key is the sky fill.

**Openings and lids**
- The sun enters only through openings.
- Roofed halls use the shadow-only lid with its openings. Single-faced walls cast Two Sided shadows. Glass casts no shadow.
- Course 2's roofless halls are top-lit galleries. A full lid made them too dark, while an open sky can flood them until the lamps vanish. Choose per hall between three options: open; a patterned shadow-only lid (beams, skylight frames, oculi); or closed. The goal is sun landing where wanted while the lamps still read everywhere else.

**Patterns.** Lattice, leaded-glass and window-grid patterns come from real shadows. Where the mesh is too coarse, use a cookie on a spot aimed along the sun's direction. Never add a second sun from another direction.

**Sky fill**
- One shadowless spot just inside each large window, skylight or roof opening.
- Cone 100–140°, aimed inward, giving E 0.2–0.4 on the surfaces it faces.
- Neutral to faintly cool: about 6,500 K, never the blue of shade at 8,000 K or more.
- URP has no portal light and no realtime area light, which is why this is a spot.

**Deep zones.** Beyond about 1.5–2.5x the window-head height from a side opening, add soft, broad, neutral helper fills that carry the daylight deeper (Naughty Dog's practice; PSALI).

**Shadows:** the sun only.

### L2 General (layer: general) – the ambient base

**Job:** a bright, nearly shadowless base that keeps every corner visible. It stands in for light bouncing between surfaces.

**Per-hall neutral ambient**
- Use a neutral ambient per hall instead of the outdoor sky probe.
- Today this is GfxHallLight, working through the team's ZoneLight. The engine researcher proposes per-pixel zones in Gfx Lit, keyed by each hall's rendering-layer bit or bounding box.
- Known failures:
  - sky ambient (royal-blue floors, lavender tiles); Fortnite's cold interiors before Lumen came from the same kind of sky leak;
  - a warm gradient (sepia);
  - a dim neutral (black floors).

**Strength:** E 0.3–0.5. The ambient must not be what makes the room look bright; on its own it is flat. Together with L6, no shaded face reads below E 0.35.

**Colour:** neutral, saturation under about 10%, never warmer than the walls, never sky-blue.

### L3 Architectural light (layers: wash, uplight) – what makes the hall bright

**Job:** light the big vertical surfaces and the ceiling: walls, piers, columns, vault, dome and coffers. This layer carries most of the hall's brightness (P1). Heritage practice hides these fittings on cornices, coves, triforium and clerestory walkways, window sills and the backs of columns (Salisbury, Norwich, Hampton Court).

**Wall wash**
- Light from the cornice line or a hidden ledge.
- Real wallwashers stand about a third of the room height out from the wall (about a fifth in rooms over 8 m). They are spaced at most 1.5x that distance (about 1.3x in tall rooms) and tilted about 35° (ERCO).
- URP spots can be wider than real washers. Keep the wall even: no scallops, and no bright lower half under a dark upper half.

**Columns and piers**
- One uplight per column, from the face turned away from the race camera (Norwich), 1–1.5 m off the shaft.
- Real practice puts up to four on a free-standing pillar; one usually reads in a game view.

**Vault, dome and ceiling**
- Uplight from cornices, coves, drum rings or clerestory ledges until the vault reads clearly lit.
- Never so strong that the roof outshines the floor; church guidance balances up and down light.
- Bright vaults and upper walls make a hall read as high and grand.

**Floor from above (tall halls)**
- Two downlights per bay, one on each side, from clerestory or gallery level (Salisbury's method).
- Use them only where sun and sky don't reach the route.

**Strength**
- Walls at 0.7–1.3x the floor's E. On screen, walls and vault are at least as bright as the floor.
- Meter check after this layer: the hall's average reaches at least 1/2–2/3 of the exterior reference.

**Colour**
- The daylight family, about 5,000–5,500 K. This is this method's pick from the upper part of the engine researcher's 4,000–5,500 K wash range; ERCO notes that supplementary light near daylight runs cooler.
- Gold columns, capitals and frames get their gold from their gilt material under neutral light.

**Shadows:** none. Washers facing polished floors carry the diffuse-only bit.

### L4 Decorative fixtures (layer: decorative)

**Job:** identity, rhythm, sparkle, and the only warmth in the room. Decorative fixtures are not the main light (P11).

**Placement: rhythm first**
- One sconce or girandole per pier between windows or arches, on both long walls, all at one height. Add a second, upper row when the wall is tall (Schönbrunn added one).
- Matching pairs flank doors (always the goal door), windows, fireplaces, mirrors and beds. Torchères flank stair feet and landings (Garnier).
- Chandeliers:
  - on the centre axis only, in one row;
  - about one per 2–3 bays (Versailles: 6 in 73 m, about every 12 m);
  - all at one height and at least 0.6 m apart;
  - in two-storey halls, the bottom hangs at about the upper-floor level;
  - never a second row: it becomes a false ceiling.
- Size chandeliers by rule of thumb (retail guides): diameter about 8.3 cm x (bay length + bay width, in m); height about 21–25 cm per metre of ceiling height. A 12 x 12 m bay under a 12 m ceiling gives about 2 m wide and 2.5–3 m tall.
- One task lamp per desk or bench, following the furniture's grid. Lanterns at doors, and one per bay along a bridge or arcade.
- A closed room gets more of these fixtures in rhythm, not brighter single lights.

**Light per fixture**
- An emissive at scene-linear 2–6 (above the bloom threshold), plus one short warm glow light: R 1.5–3 m, I 1–4.
- Colour: flames about 1,900–2,200 K; bulbs about 2,700–3,000 K.
- One glow per tight group, such as a candle cluster or a sconce pair within about 1 m.
- Only fixtures meant to light the room also get a room light, for example a chandelier 6 m up at I about 40, R 15. Keep it in the daylight family or barely warmer, so the floor below doesn't turn yellow. The warmth lives in the crystals and the glow.

**Shadows:** none. Chandelier frame patterns come from a point-light cookie.

**Brilliants:** small, clear points, never frosted blobs. Bloom appears only here and on sunlit highlights.

### L5 Accent (layer: accent) – focal glow

**Job:** rank what matters:
- the goal door, in every hall;
- one hero: the stair top, the bench grid, the desk grid, the clock, the pools, the far doors, the board, the bed alcove or the map table;
- a few secondaries: heraldry, paintings, side doors.

**Placement**
- From high, hidden positions on the approach side.
- Wall objects are lit from about 30° off vertical (the museum angle).
- The accent follows the object's shape: a table, a stair, a door.
- Frame the goal door with a matching pair (D6 focal frame). Best of all, let daylight or a bright window show beyond it.

**Strength**
- Hero 2–3F, secondaries 1.5–2F, one hero per view.
- Light to add: E = F x (TAIR - 1). From 8 m, each +1 E costs about I 73.

**Colour:** neutral pools. A "golden" door gets its gold from gilt material, emissive details and daylight beyond, not from a warm flood.

**Shadows:** the hall's 0–2 shadowed spots go here, if contact shadows on the stage, board or goal need them.

### L6 Bounce and fill (layer: bounce) – faked global illumination

**Job:** do what GI would do. Sunlit floors throw light onto walls, ceiling and pieces. Light pools tint their surroundings. Colour bleeds. Bright areas lift their neighbours.

**Sun-patch bounce**
- Under each large sunlit patch, place a point light about 1.5x the patch radius below the floor. It lights the walls, ceiling and pieces, but not the floor itself.
- I is about albedo x E_sun x depth².
- Colour = floor albedo x sun colour. Desaturate it if a warm floor starts colouring cream walls.

**Pool bounce** (Godot's recipe)
- A second light at a strong source's position, at 10–40% of its energy, with 25–50% more range, no shadows and no specular.
- For a spot, put it near where the beam lands.

**Character fill.** A soft fill or rim light on the character layer, working with CFCharacter's second light. It keeps the pieces' visible backs cream and separates them from the set in every hall.

**Limits:** a bounce light is never visible as a source, never prints highlights (diffuse-only bit), and is never brighter than the light it echoes.

### L7 Finish

- Render the hall's reflection probe after L1–L6 are final. Polished floors, water and gilt must reflect the room's own windows and chandeliers.
- Brilliants: crystal, gilt edges, brass and water glints as tiny points at 10x or more.
- Haze: thin and neutral, lit from above where there is steam or dust. Shafts draw only where the sun's shadow map blocks the sun.
- SSAO: check a lit area for grey halos.

---

## 6. Course moods

**Course 2**
- Halls: 2-1_GrandStairHall, MG_B_RepairWing (workshop), MG_E_RoyalLibrary (the reading room), MG_F_ClockworkBallroom, MG_J_PalaceLaundry.
- Mood: just inside the castle. Lighter, but splendid at once.
- How: high key; key:fill toward 2:1; field E 1.0–1.2; open top light; generous sparkle; white, cream and gold.
- The stair hall is where the eye adapts after outdoor course 1, so it is among the brightest interiors.

**Course 3**
- Halls: MG_G_RoyalProcession, MG_H_KingsChessHall, MG_I_KingsBedchamber, WarRoom. Also 3-1_RoyalLibrary and 3-2_RoyalObservatory, which keep their own lighting.
- Mood: solemn, overwhelming, an ending.
- Grandeur comes from:
  - scale (lit vaults and upper walls);
  - long symmetric axes;
  - the rhythm of bays;
  - brightness that rises in steps toward the goal;
  - sun from high openings;
  - white marble and gold.
- How: key:fill toward 3:1 with a clearer hero; field E 0.8–1.0; shade never below a third of the field.
- Never dusk, a night tint, a dark hall or cool shade. JY has rejected all of them.

---

## 7. Recipes by space type

Each recipe is a starting design. The hall's survey (section 8) decides the real positions and counts.

### 7.1 Grand stair hall (2-1_GrandStairHall, course 2 entry)

**Archetypes**
- The Winter Palace's Jordan Staircase: a climb from a shaded lower zone into a double-height hall lit by two tiers of windows, with mirrors opposite, white marble, gilt and a painted sky overhead.
- Würzburg: a low vestibule opens into Neumann's vault, and Tiepolo's fresco comes into view as the flights turn (from one design write-up).
- Versailles' lost Ambassadors' Staircase had a skylight in its painted ceiling.
- The Palais Garnier marks its stair with bronze torchères.

Sources: https://en.wikipedia.org/wiki/Jordan_Staircase_of_the_Winter_Palace ; https://en.wikipedia.org/wiki/Palais_Garnier

**Light story.** From a lower but still bright vestibule, the race climbs into the brightest volume in the castle, under daylight falling through the dome's lattice. D6 pattern: space with focal point.

- **Daylight**
  - Sun through the dome lattice, with real lattice shadows; use a lattice cookie on a sun-aligned spot if the mesh is too coarse.
  - Sun patches on the landings and the upper flight are the route accents.
  - Sky fill under the dome opening (top light gives 2–3x what an equal window would).
  - One bounce light under each landing patch. The red stair carpet bounces red light, so keep that bounce subtle or the walls turn pink.
- **Walls, dome and columns**
  - Wash the stairwell walls.
  - Uplight the dome and its painted or coffered surface from the drum or cornice ring.
  - One neutral uplight per column (I 60–80 for an 8 m shaft).
- **Stairs**
  - Light each flight from above its top, so the risers fall into shade and the nosings read.
  - Diffuse light on the landings.
  - Contrast on the stair 5:1 or less.
- **Fixtures**
  - The hub chandelier on the stairwell axis, its bottom at about the upper landing, sized by the bay rule, with a room light.
  - Torchère pairs at the stair foot and on each landing.
  - Sconce pairs on the landing walls, one pair per bay.
  - A mirror answering a window wall, where one exists.
- **Focal and goal**
  - The top landing and exit portal are the hero (2–3F), ideally with daylight beyond.
  - The vestibule sits at 0.6–0.8F, never dark, and then the hall opens out.
- **Targets:** field E about 1.2 (top of the band); walls at least as bright as the floor; the dome clearly lit but below the hero.
- **Replace** "warm uplight washing every column and wall": washes and uplights are neutral; warmth stays in the chandelier and the torchères.
- **Watch for:** the chandelier turning into a false ceiling over the stair; bare bright points at eye level on the climb.

### 7.2 Royal workshop (MG_B_RepairWing, course 2, roofless, walnut floor)

**Archetypes**
- North-light (saw-tooth) workshops: the glazing faces away from the sun, giving even, soft-shadowed light with no sun patches on the work.
- A lamp at every bench, placed at its front edge so the worker doesn't shadow the task.
- ERCO: work rooms read right with cooler, diffuse, even light.

Sources: https://en.wikipedia.org/wiki/Saw-tooth_roof ; https://www.finewoodworking.com/2002/02/01/lighting-for-the-workshop

**Light story.** An even, clean, daylit work hall. Every bench glows under its own lamp, and the tools gleam. D6 pattern: space with focal points in a grid.

- **Daylight**
  - Even, sky-dominated top light from the open roof.
  - Sun patches are allowed in the aisles but kept off the benches, using beams or a patterned lid.
  - Sky fill about E 0.3.
- **Walls:** wash the tool walls from top to bottom, so they read clearly; neutral uplights on the columns.
- **Fixtures**
  - One low pendant or work lamp per bench, in a grid matching the benches.
  - The warm bulb only inside the shade. The pool of light itself is 3,500–4,000 K at about E 2 on the bench (engine researcher).
  - Lamps over the pit.
- **Focal and goal:** the bench grid and the pit form the focal pattern (1.5–2F); the exit door is the hero.
- **Bounce and sparkle**
  - Walnut (albedo about 0.25–0.35) bounces brown light. Keep that bounce weak and desaturated so the walls don't turn orange.
  - Brass and steel tools sparkle through the probe.
  - The sawdust haze stays thin and neutral, never yellow.
- **Targets:** field E 1.0–1.2; uniform light following EN's steps (task 750, surround 500, background about 170), so no isolated moody pools; key:fill about 2:1.
- **Replace** "warm light washing up the columns" and "warm work-lamp pools": neutral column light, with the warmth only inside the lamp shades.

### 7.3 Reading room (MG_E_RoyalLibrary, course 2, roofless, parquet, bookshelf towers)

**Archetypes**
- Labrouste's reading room in Paris: nine domes, each with a glass oculus, lined with white and cream tile for shadowless light. Desk lamps came only in the 1920s.
- The New York Public Library's Rose Main Reading Room: tall windows high above the bookcases, chandeliers, and a brass lamp on every oak desk.
- The Library of Congress: a lamp at every desk.
- JY's photo of a wood-panelled library.

Sources: https://www.architecturalrecord.com/articles/12868-restoration-and-renovation-of-henri-labroustes-bibliotheque-nationale-richelieu ; https://www.nypl.org/press/press-release/october-5-2016/new-york-public-library-reopens-its-historic-rose-main-reading

**Light story.** Calm, even daylight from above; a strict grid of warm desk lamps; books lit along their spines.

- **Daylight**
  - Shadowless top light from the open roof, like Labrouste's oculi.
  - Sun patches between the towers are allowed as time-of-day accents, but never glaring on the desks.
- **Shelves and walls**
  - Wash the shelf faces from top to bottom.
  - Dark wood and book spines (albedo about 0.25) need about 2.5x the light of cream: washers from the cornice line at I 25–40, 4–5 m away.
  - A pale, uplit ceiling; neutral uplights on the towers and columns.
- **Fixtures**
  - A reading lamp on every desk, in a strict grid. This grid is the room's identity: a warm glow and a small pool at 1.5–2F.
  - Low-brightness sconces at shelf ends and flanking openings.
  - Chandeliers on the axis only.
  - Brass and glass shades for sparkle.
  - No bare bright points visible down the aisles (glare class UGR 19).
- **Ratios:** desk:surround:wall like 10:4:3, or the restful 10:5:2; never dark walls.
- **Focal and goal:** the desk grid is the pattern; the exit door is the hero.
- **Replace** "warm light up the columns": neutral.

### 7.4 Clockwork ballroom (MG_F_ClockworkBallroom, course 2, roofless, cream marble)

**Archetypes**
- Versailles' Hall of Mirrors: 17 garden windows face 17 mirrored arches. Since the restoration, a single central row of 6 chandeliers.
- Schönbrunn's Great Gallery: two large chandeliers, with most of the light points on the walls.
- The Catherine Palace Great Hall: windows on both sides, alternating with mirrors, so light crosses the room and plays over the gilt all day.
- Modern ballrooms hide cove light and wall wash, and hang chandeliers in uplit coffers so the chandeliers can be pure sparkle.
- No standard level exists. Aim between Cuttle's "bright" and "distinctly bright".

Sources: https://en.chateauversailles.fr/discover/estate/palace/hall-mirrors ; https://www.tzar.ru/en/objects/ekaterininsky/greathall

**Light story.** A cream-and-gold hall of daylight where every lamp is doubled. The walls carry the light, the chandeliers are jewellery, and the clockwork is the stage.

- **Daylight**
  - Top light from the open roof, with both long walls equally lit.
  - Where the art has mirrors, pier glasses or glazed panels, they answer the light. Otherwise the polished floor does the doubling.
  - A near-white bounce under each sun patch on the marble.
- **Walls and ceiling**
  - Wash the panels between pilasters, gently non-uneven, since Flynn found peripheral light pleasant.
  - Uplight the ceiling, beams or coffers from a cornice, so the roof is not a dark lid.
  - Neutral uplight on the gold columns; the gold comes from the material.
- **Fixtures**
  - One axis row of 2–6 chandeliers, depending on length: one per 2–3 bays, all at one height, each sized to its bay.
  - Each chandelier has an emissive crystal and a warm glow. One to three of them also carry a near-neutral room light (I 35–45, R 14–18 m).
  - Two rows of sconces on the piers hold most of the light points.
- **Focal and goal:** the great clockwork or clock face at the end is the hero (2–3F). A sconce pair frames the exit door.
- **Sparkle:** the polished cream marble reflects the chandeliers through the box-projected probe; this reflection is the play of brilliants. The gilded clockwork glints. Crystal shows as small clear points.
- **Targets:** field E 1.0–1.2; key:fill about 2:1.
- **Replace** "great warm pools on the dance floor" and "cool daylight from above": a near-neutral room light, with the warmth in the crystals; neutral daylight.

### 7.5 Palace laundry, the "bathhouse" (MG_J_PalaceLaundry, course 2, roofless, white tile, pools)

**Archetypes**
- JY's white carved Moorish halls. In the Alhambra, high lattice windows rake light across carved plaster, small windows at the base of the muqarnas domes light them from below, and courtyards hold still pools.
- Roman caldaria had large glazed windows and the semicircular "thermal" window high under the vault.
- Victoria Baths in Manchester has over 500 m² of patent roof glazing over galleried pools; the Gellért in Budapest has a glass roof over its pool hall.
- Pool-hall practice:
  - no fittings over the water, because their reflections glare;
  - fittings 1–2 m to the side;
  - wall washers and bright matt surfaces;
  - underwater lights;
  - 200–300 lx at the water, with uniformity about 0.7.
- Real hammam darkness (60 lx or less) is not the model.

Sources: https://www.alhambra-patronato.es/en/edificios-lugares/the-hall-of-the-two-sisters ; https://fagerhult.com/knowledge/light-guides/sports-facilities/swimming-hall

**Light story.** Clean white daylight from above onto white tile. The water throws light up onto the walls and vault, lanterns line the arcades, and the steam glows.

- **Daylight**
  - Top light from the open roof, or a pattern of oculi or thermal windows in a lid.
  - Sun on the pool edges and the water.
  - Neutral to faintly cool sky fill.
- **Walls and vault**
  - White tile (albedo about 0.8) reads bright with modest light. Wash the walls and vault.
  - Graze the carved plaster or tile relief from high positions; here the texture means something.
- **Fixtures**
  - Rows of lanterns on the arcade piers (a closed room gets many lamps).
  - Warm sconces at the doors.
  - Star-shaped oculi as sparkle accents only.
- **Pools**
  - Pool lights 1–2 m back from the edge, aimed at the walls and vault.
  - Underwater lights so the pools glow.
  - A caustic pattern (spot cookie or shader) on the shaded walls and vault above the pools. Never put it inside sun patches, where it washes out.
- **Steam:** neutral white, thin, lit from above, never yellow.
- **Reflection probe:** mandatory, because the water is a mirror.
- **Targets:** field E 1.0–1.2, very even.
- **Keep** "warm sconces by the doors". **Replace** "clean cool daylight" with neutral daylight.

### 7.6 Ceremonial procession nave (MG_G_RoyalProcession, course 3, skylit marble)

**Archetypes**
- The Gothic or basilica nave: clerestory windows exist because aisle windows can't reach the middle. So the upper nave and vault are bright, and the aisles darker.
- Salisbury's relighting hides uplights on the triforium and clerestory walkways for the vaults, and puts a downlight projector on each side of every bay at clerestory level for the floor.
- Norwich hides lights in the column flutes, on the faces away from the main views, and uplights the carved vault bosses.
- Notre-Dame (2024) keeps 13 chandeliers in the arcades.
- CIBSE levels: nave about 150 lx, chancel 200, choir about 300, the altar brightest.

Sources: https://buildingconservation.com/articles/lowcarbonlight/lowcarbonlight.htm ; https://www.architectmagazine.com/awards/al-design-awards/relighting-norwich-cathedrals-interior_o

**Light story.** A march of daylight down the axis: bay after bay of sun pools on the carpet and lit columns, rising to the far doors. D6 pattern: space with framed path.

- **Daylight:** high skylights or clerestory windows. One sun pool per bay on the carpet, with one bounce light under each.
- **Vault and columns**
  - One neutral uplight per column, from the face away from the camera.
  - Vault ribs washed from the triforium or clerestory ledges: readable, but never outshining the floor.
  - Wall panels between columns washed, so the nave is not a dark tunnel.
  - Floor light from above where the sun misses: two downlights per bay at clerestory level. From 12 m, each +1 E costs about I 164.
- **Fixtures**
  - A crown of light or a lantern in the arcade of every bay.
  - Candelabra pairs along the procession.
  - Gilded capitals as emissive sparkle.
- **Hierarchy**
  - Aisles at 0.6–0.8F, in neutral light.
  - Brightness rises in steps of about 1.5–2x toward the goal.
  - The far doors are the brightest step (about 3F) and the only distinct jump.
- **Course 3:** the rhythm and the rise give the solemn grandeur; key:fill toward 3:1.
- **Replace** "gold light rising up every column" and "the side aisles in cool shade": neutral uplight, with the gold in the capitals; aisles at 0.6–0.8F in neutral light.

### 7.7 King's chess hall (MG_H_KingsChessHall, course 3, lidded, window grids)

**Archetypes**
- White-marble state halls. The Winter Palace's St George's Hall pairs white Carrara marble with gilded bronze under chandeliers at regular intervals; the Small Hermitage's Pavilion Hall reportedly hangs 28 of them. Their brilliance comes from many identical fittings in a strict grid over a pale floor.
- The stage: the board as a lit plane.

Source: https://en.wikipedia.org/wiki/St_George%27s_Hall_and_Apollo_Room_of_the_Winter_Palace

**Light story.** The board is the stage: an even, bright plane under one great overhead light, framed by window grids and white-and-gold walls. D6 pattern: space with focal point.

- **Daylight**
  - The lid's window patterns, which already work, with tall windows in tiers.
  - Sun grids across the board, from shadows or from cookies aligned with the sun, with a bounce light under each.
- **The board**
  - The brightest horizontal plane: even and nearly shadowless, about 2x its surround. Real practice would go to 3–10:1; it is capped here for a bright hall.
  - Light it with an overhead pool, a spot cluster, or a ring of chandelier lights aligned with the board's files and ranks.
  - Balance the vertical light on the pieces (Ez/Eh 0.3–0.6) so the cute pieces read as round forms.
  - The hall's one shadowed spot goes here, if contact shadows are needed.
- **Walls:** washed bright, with no edge falloff and no vignette darkness.
- **Fixtures:** chandeliers in a regular grid aligned with the board, on its axes; sconces on the piers.
- **Goal:** the throne end or exit door is the brightest framed vertical plane at the end of the axis. The board is the brightest floor and the door the brightest frame; nothing else reaches their level.
- **Sparkle:** crowns, finials and the polished board.
- **Replace** "the hall's edges falling away into shade": edges washed bright.

### 7.8 King's bedchamber (MG_I_KingsBedchamber, course 3, lidded, curtained windows, lasers)

**Archetypes**
- Louis XIV's bedchamber at Versailles sits at the centre of the east façade, facing the rising sun. The bed stands in an alcove behind a gilded balustrade on the opposite wall, so morning light crosses the room to it. Candelabra stood on the mantels, with pier glasses between the windows.
- Heritage relighting uses decorative sconces with warm, dimmable candle lamps, uplights in the window sills, and small picture lights.
- Durak: people chose cove light for relaxation and privacy.

Sources: https://en.chateauversailles.fr/discover/estate/kings-apartments ; https://www.arc-magazine.com/cumberland-art-gallery-london-uk/

**Light story.** Morning light crosses from the window wall to the bed alcove: the warm heart of a bright, calm room.

- **Daylight:** sun through the window wall, with leaded-glass patterns, crossing toward the bed; sky fill at each window.
- **Walls and ceiling**
  - Uplights in the window sills, or a cove, lighting the painted or gilded ceiling.
  - A restful wall pattern (10:5:2), but walls at E 0.7 or more and no dark corners.
- **Fixtures**
  - Pier glasses between the windows, with candelabra or girandoles.
  - A matching pair of bedside sconces.
  - One central chandelier sized to the room.
  - Candelabra on the mantel.
- **Focal:** the bed alcove is the hero, with a warm glow inside the canopy as the room's heart. Its range is 3–4 m, not the heart light's default 10 m. The exit door is the goal.
- **Materials:** deep plum and red fabrics are dark albedos. Lift their tone in the albedo pass, or budget more wash light. Never accept a dim room.
- **Gameplay:** the lasers must read through saturated colour and emissive contrast against a bright room, not through darkness. Valve and Riot keep the set calmer than whatever the player must read.
- **Intimacy** comes from the warm heart and a lower ratio, not from darkness.
- **Replace** "an intimate room in deep plum and red ... the lasers bright in the dim".

### 7.9 War council room (WarRoom, course 3 finale: bridge, lancets, final golden door)

**Archetypes**
- The Doge's Palace in Venice raised its great council hall's windows so daylight reached the painted, gilded ceiling; Ruskin praised the hall for taking its light in a few broad masses.
- The Reichstag chamber takes daylight from above, with a shield against direct sun.
- Churchill's Map Room kept constant, even light on its maps.
- ERCO's conference recipe balances horizontal and vertical light, takes the ambient from ceiling washlights, and puts prestigious light on the table.

Sources: https://books.openbookpublishers.com/10.11647/obp.0151/ch6.xhtml ; https://www.iwm.org.uk/history/the-map-room-at-the-cabinet-war-rooms

**Light story.** Light falls in a few broad masses from the high lancet windows onto the vault and the great table. At the end of the bridge, the final golden door is the brightest thing in the castle's last view. D6 pattern: space with framed path.

- **Daylight:** sun stripes through the lancets plus sky fill. Light the vault and the ceiling.
- **Walls:** wash the end walls, with their maps and banners, evenly; picture lights from about 30°. Bright stone and a lit vault.
- **Fixtures**
  - Lanterns along the bridge as short warm glows, with one neutral fill per 2–3 lanterns.
  - One big chandelier or a row of downlights over the map table, the fixture about 1/2–2/3 of the table's width.
  - Team lanterns stay small, coloured sparkle; boosting them turned the room red.
- **Focal:** the map table at 1.5–2F. The final door is the hero (about 3F) and the only distinct jump: the course's finale.
- **Replace**:
  - "a dark Gothic hall" with a bright, solemn hall;
  - "cool light high through the lancets" with neutral daylight;
  - "the final golden door blazing" with a door made golden by its gilt, emissive details and daylight beyond, not by a warm flood over the bridge.

### 7.10 Sections with their own lighting, and corridors

- **3-1_RoyalLibrary** (the team's RoyalLibraryLighting, shadowless). It already matches Labrouste's shadowless ideal. Review it against this method before changing anything: walls and vault at least as bright as the floor; neutral fill; a lit goal; a lamp and a window per bay, as in Admont's white-and-gold hall under 48 windows or Trinity's 40 alcoves.
- **3-2_RoyalObservatory** (the team's RoyalObservatoryLighting). It is night by JY's recorded concept; two digests' morning recipes for it are superseded. If it is ever redesigned, use the same layers with the night sky as its key, kept readable by interior lamps around the windows. Greenwich's Octagon Room has tall windows on all sides.
- **Corridors** (SectionCorridor, 10 m). A little below the hall they lead into, with no dark mouths. Each hall's goal door is the brightest framed plane at the end of its axis.

---

## 8. The lighting design sheet (one per hall, written before building)

1. **Identity.** Map key, course, and the new concept line: archetype plus a one-sentence light story.
2. **Survey.**
   - Size (L x W x H), bay and pier spacing, axes.
   - Openings: type, size, head height, which ones face the sun; roof open, patterned lid or closed.
   - Hidden ledges: cornice, cove, clerestory, sills, column backs.
   - Materials, with measured albedo.
   - Route: entry, main line, side bays.
   - The goal door, the hero, the landmark visible from the entrance, and the gameplay elements that must stay readable.
3. **Sun plan.** Where the fixed sun lands, in plan and in section; leaks to seal.
4. **Hierarchy.** The hero and its ratio; secondaries; field E; shade minimum; key:fill; D6 pattern; course mood.
5. **Light table.** One row per light group. Each row becomes a GfxMapLights plan entry (Layer, Note, At/To, Anchor, Each, Spacing, Count, Max, Aim, Angle/Inner, Intensity, Range, Color, Shadows). Example rows:

| Layer | Kelly role | Motivating source | Type | Count and rhythm | Position | Aim, cone | Target E at d, then I | Range | Colour | Shadows | Render layer |
|---|---|---|---|---|---|---|---|---|---|---|---|
| wash | ambient | daylight spread in the room | spot | 3 per long wall, every 2nd pier | cornice line, 3 m off the wall | wall mid-height | E 1.0 at 6 m, so I about 41 | 12 m | about 5,300 K | off | hall A, diffuse-only |
| decorative | brilliants | sconce pair | point | 1 per pier pair | at the sconce | – | halo | 2.5 m | 2,700 K | off | hall A |

6. **Targets and budget.** Measured E_ext; meter target; lights per hall; shadowed spots; per-pixel overlap.
7. **Changes and risks.** The concept line it replaces; material proposals for JY; open questions.
8. **Review.** Checklist results; race-camera pictures with the corridors joined; greyscale views; before and after.

---

## 9. Workflow per hall

1. **Survey** in Play with the corridors joined, from the race camera and a free camera. Use GfxShots for the pictures.
2. **Write the design sheet.** Its story and hierarchy can go to JY before building.
3. **Albedo pass**, with material proposals for dark large surfaces.
4. **Confirm the shared frame** is in place: one sun, the global grade, a neutral hall ambient, the probe.
5. **Build the layers.**
   - Build L1, then take pictures (lit and greyscale).
   - Then L2; then L3, with a meter check; then L4, L5, L6 and L7, measuring after each.
   - Tune ratios with the per-layer scales (DaylightScale through BounceScale), not by adding lamps.
6. **Review** with section 10, from the race camera, both moving and still.
   - Fix ratios with lights, never with exposure, contrast or tint.
   - When a hall turns muddy, strip it back to the key and rebuild.
7. **Before-and-after pictures** for JY. The change has to be big enough to see.

---

## 10. Review checklist

**What to look at:** race-camera stills and a full run with the corridors joined; the same views in greyscale and in the grey-albedo view; the meter value; the Frame Debugger counts. Any one of the failures below fails the hall.

**Sources**
1. A lit area you cannot trace to a visible source, or a glowing fixture that lights nothing.
2. A sun direction, colour or strength different from the other halls, or a second "sun".

**Brightness**

3. Upper walls, vault, corners or far end dark (the cave effect), or walls darker than the floor on screen.
4. Anything in the playable view below a third of the field (small contact shadows aside), or any black pocket.
5. A meter average below about half of the outdoor reference.
6. Flat light: key:fill under about 2:1 along the route, with pieces and columns reading as cut-outs.

**Hierarchy and composition**

7. No single brightest framed element in a forward view, or the goal door is not that element; accents competing at the hero's level.
8. A large surface clipped to flat white, or a theatre spotlight in a dim room.
9. Fixtures off the axis or off the bay rhythm; chandeliers in more than one row; unmatched pairs at doors; fixtures out of scale with their bay.
10. Side bays brighter than the main line; a dark mouth at a hall entrance; a corridor so bright that the next hall looks closed.

**Colour**

11. Cream or white walls turning yellow; warmth spreading past a fixture's own halo; warm haze or a warm grade.
12. Shade reading blue, violet or night-grey; lavender tiles or royal-blue floors (sky ambient leaking in).
13. Coloured accents flooding the room: team lanterns turning it red, or stained-glass colour everywhere.

**Sparkle and reflections**

14. Crystal and gilt reading as blurry blobs; big bloom halos; bloom on walls or floors.
15. Polished floors, water or metal reflecting the outdoor sky or a room at the wrong scale; highlights on the floor where no lamp is.

**Readability**

16. The visible back of a piece in grey or black shade; pieces merging into the floor or walls; hazards (lasers) not reading against the room.
17. Stair nosings unreadable; a bare bright point at eye level on the race line.

**Technical**

18. Sun or lamp light leaking through walls or roofs, or into the next hall; hard circular rims on pools of light.
19. More than about 12 lights over a pixel; more than 2 shadowed spots in a hall; a shadowed point light; shadow-atlas warnings; lights popping on or off.

**Against the references**

20. Next to JY's photos at the same exposure, the hall reads dimmer or greyer; or, next to the previous build, the change is too small to see.

---

## 11. Limits and open items

- **Measure E_ext in the game.** The engine researcher's sun (2.2 at 50°) is an example, not the project's sun, which is about 37° high and comes from ahead.
- **Confirm Gfx Lit's diffuse term** against URP Lit before trusting the formulas.
- **Syntheses to confirm in pictures with JY.** These are this method's own judgements, not sourced facts: the course split of the field band, the 5,000–5,500 K wash colour, the 2–3x cap on large heroes, and the transfer rule in 4.1.
- **Weakly sourced points.** Seen only through search excerpts or single sources:
  - the Fortnite Lumen blog, Seif El-Nasr, Klevestav, Flynn's original paper, BeamNG, and some Historic England and ERCO pages;
  - Pavilion Hall's 28 chandeliers and Würzburg's dim vestibule;
  - the chandelier sizing rule (retail guides) and the accent-ratio labels (rules of thumb).
- **Missing standards.** The EN conference-room value is unverified. No EN or IES value exists for ballrooms. No Nintendo source on Mario Kart's interior lighting was found.
- **The observatory exception.** The brief for this method said "morning everywhere". The observatory exception comes from JY's recorded words (11-24 evening) and the team's RoyalObservatoryLighting.
- **Area lights.** URP forces area lights to Baked mode; this was checked on Unity's 6000.3 Light component page for this method. So area lights cannot carry window light here.