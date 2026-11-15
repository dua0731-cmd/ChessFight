You are generating concept art for a 3D modeller. Use your image generation tool; do NOT write code. Generate exactly EIGHT final images, one at a time and in this order, and copy each generated PNG into the current working directory with these exact names:

- RCY_01_overview.png - 1024x1536 portrait (2:3)
- RCY_02_wagon_yard.png - 1536x1024 landscape (3:2)
- RCY_03_waterwheel_crossing.png - 1536x1024 landscape (3:2)
- RCY_04_sorting_yard_cranes.png - 1536x1024 landscape (3:2)
- RCY_05_hoist_loft_and_berth.png - 1536x1024 landscape (3:2)
- RCY_06_pond_cliff_and_gate.png - 1536x1024 landscape (3:2)
- RCY_07_architecture_modules.png - 1536x1024 landscape (3:2)
- RCY_08_props_materials.png - 1536x1024 landscape (3:2)

Build each image prompt from five blocks, copied word for word in this order: the REFS block for its kind (scenes 1-6 or sheets 7-8), STYLE, CUES, that image's entry under "The images" without its CHECK line, and AVOID. Do not shorten, summarise or add to them. (The files RCY_01_overview_prompt.txt ... RCY_08_props_materials_prompt.txt beside this file are exactly these eight prompts.) Every other section is the full brief behind those blocks: use it to check each result, and never paste it into an image prompt. Ask the tool for the size shown (or its aspect ratio if it takes no size), and never resize, crop or convert a result. Call the tool at most twice per image: generate it and compare the result with the entry's CHECK line; if a check fails, generate once more with the failed checks named first. Keep the better of the two: a retry replaces its image and is not a ninth. The attachments are "reference 1-9" (9 is the image's own layout map, none for images 7-8; the paintings below are optional), the outputs "image 1-8". This prompt is complete: do not read HANDOFF.md or any Docs file (the yard's documents there describe older versions of this place), and change no other file. After saving, reply with the eight file paths and one short line describing each image.

## Attached references

Attached in this order, for every image:

1. `Docs/PawnRush/ConceptArt/R502_DrawbridgeSheets/01_mechanical_assets.webp`
2. `Docs/PawnRush/ConceptArt/R502_DrawbridgeSheets/02_architecture_modules.webp`
3. `Docs/PawnRush/ConceptArt/R502_DrawbridgeSheets/03_props_materials.webp`

References 1-3 are THE TARGET RENDER STYLE for all eight images: clean stylized 3D game-asset renders, chunky rounded bevels, worn edges, cream stone blocks, warm wood with visible grain, dark riveted iron. Images 7-8 also take their sheet format and soft warm studio light (reference 2 for image 7; references 1 and 3 for image 8 and its swatch row). Take nothing else from them:
- not their blue and orange team banners or their gate layout;
- not their slate pyramid tower roofs and gold ball finials (our towers end in flat rook crowns);
- not their polished gold (every crown, plaque, trim and finial here is dull matte brass like old bronze, clearly darker than the bright functional gold);
- not their lit glass lantern, their chains, drawbridges and winches, or their benches, planters and bollards.

4. `Docs/PawnRush/ConceptArt/R506_CeremonialCourt/A_01_overview.png` - a court of the same stage. Copy its scene format (serif title, ribbon "Stage 1 - Outer Castle"), its sunny sky and hills, and its facade for the far palace's main facade: cream stone, grey slate roofs and turrets, an arched central gate under a crown (in matte brass here). Do not copy its chessboard, team gates and banners, layout, scarf pawns or bright gold.
5. `Docs/PawnRush/ConceptArt/R505_PortcullisCourt/D_01_overview.png` - same stage. Copy its crenellated cream walls, flat rook-topped towers, grey slate roofs, oak-and-iron machinery and cream banners with a brass crown. Do not copy its capstans, portcullises, overhead chains, team banners, lit glass lanterns or scarf pawns.
6. `ArtSource/Loading/CourseReveal/MiniGames/Early_MG_C_MoatDrawbridge.png` - a real in-game capture of a court that can follow this course. Copy only its stone block shapes (soft rounded bevels, per-block tone variation, recessed mortar) and its turquoise water. Its honey tint comes from the game's colour grading: our stone stays pale cream #DDD1B9, never honey, orange or sandy. Do not copy its team banners, drawbridges, moat or bright gold.
7. `Docs/PawnRush/ConceptArt/R676_RoyalCarriageYard/RCY_ref_pawns_in_game.png` - the players as they look in the game, charcoal and cream (the two pawns cut from `RigWork/pieces/tripo_rigged/unity_look.png`). The frame cuts off the cream pawn's right hand; it mirrors the left.
8. `Docs/PawnRush/ConceptArt/R676_RoyalCarriageYard/RCY_ref_pawn_front.png` - the pawn's proportions, face, stub arms and ball hands (cut from `RigWork/pieces/tripo_rigged/lineup_arms.png`). Its grey is untextured clay, not a team colour.

No rook, knight, bishop, queen or king appears anywhere in this course, as a character or as a statue.

9. For images 1-6 only, the layout map of the image's own part, from `Docs/PawnRush/ConceptArt/R676_RoyalCarriageYard/`:
- `RCY_map_course.png` for image 1
- `RCY_map_wagon_yard.png` for image 2
- `RCY_map_canal.png` for image 3
- `RCY_map_warehouse.png` for images 4-5
- `RCY_map_pond_gate.png` for image 6

The maps are flat top-down plans drawn from the game's coordinates (`ArtSource/PawnRush/CarriageYardConcept/rcy_concept_refs.ps1`), far end at the top, left = west. Colours: cream = floor; darker stone = walls and towers (circles); turquoise = water; light oak = decks, walkways, pallets and wheels; brown = goods and crates; dark grey = roofs; white = marble; translucent red = hazard areas (the crane sweeps, the berth circle); bright green = the open gaps in the stack rows; gold = jump and step edges; light and darker stone squares = rank bands; green = lawn outside the walls; green or brown boxes with a pair of horses = the coach rigs. A map is the exact layout of its part: copy its counts and positions into the 3D view. It shows places, not the image's moment: both cranes at rest pointing left, all four hoist pallets drawn, the coffer over the second berth from the left, the outer gate open and the final gate shut.

Optional, only if attached as references 10-12 (they are not in the repository): the user's own paintings of this yard. They are the R532 royal unloading-yard painting and the two R659 paintings (the wagon yard; the crane-worked warehouse); each is for the images of the part it shows. Take from them only the mood, the crate, barrel and sack stacks, the lanterns and the timber cranes. Where they differ from this text in layout, counts or colours, this text wins: their blue roofs and blue cloth are grey slate and cream or sand cloth here. Never attach the R532 picture of the old yard.

## The place

"Royal Carriage Yard" - stage 1 of the chess kingdom's castle: outside the palace, open air, a clear day.

This is the service bailey of the chess castle, just inside its outer curtain wall.
- The king's supply coaches roll in through the outer gate and unload into granary sheds.
- The goods cross a canal past a giant waterwheel into the royal warehouse, where they are sorted. They go in at an intake door, along a timber roller line and out at a sorting door. Then they go into crate rows shifted by beam cranes; timber is swung by jib cranes, a loft is served by rope hoists, and the king's coffer is lowered on chains at the heavy berth.
- At the mill pond an upright paddle wheel lifts the runners up a white marble cliff to the palace terrace, and the final gate opens toward the palace.

It is the opening course of a cute chess party game: twelve small pawns race along one shared route, and every machine is an obstacle with clear warnings. Busy, royal and orderly; lived-in, never a ruin.

The chess identity lives in silhouettes and craft details that read at game distance. It is coherent as one kingdom, always on architecture, machines and props, never clutter on the paths and never confusable with the players:
- The castle is built of rooks: every round tower ends in a classic rook crown (flared corbelled collar, a ring of chunky merlons, flat top) instead of a cone, and every wall top carries chunky rook merlons.
- The yard's crest is a chess knight's head in profile under a small three-point crown, flat matte brass on a cream shield. It sits on the gate plaques, the banners, the royal canopies and the coach doors.
- Goods are sorted by piece: crates, sacks and barrels carry small stencilled piece icons (pawn, rook, knight, bishop, queen, king) in dark brown paint, and every warehouse bay has a small matte brass plaque with its icon.
- Carved knight heads end the hoist arms and the jib tips and carry the crane beams as wall corbels, always 6 m or more above the floor; nothing knight-shaped stands at pawn height. The horses are horses, not chess pieces: their only tie to the knight is a small brass knight badge on the brow band. Every carving is plain stone, wood, iron or brass, with no eyes or face.
- Crane counterweights are squat iron blocks with crenellated tops; lamp posts wear small bishop-mitre caps; the giant wheel's hub and the king's coffer carry matte brass crowns.
- The floor is plain flagstone. Its only chess pattern is the rank band: one flush row of 1 m squares, 1 m deep, alternating cream limestone and the darker shade stone, under each gate opening and at each checkpoint.
- Far beyond the final gate stands the palace. First comes its cream main facade, with grey slate roofs and turrets and a matte brass crown over its arched central gate (as in reference 4; the next court stands in front of it). Behind it the keep's skyline stands as one back rank: rook corner towers, two bishop-mitre wing towers, one queen-coronet tower, and the tallest central tower crowned like a king (crown and cross).

The players are the only chess pieces at pawn size anywhere.

Light, images 1-6: bright clear midday, blue sky with a few soft clouds, short soft shadows, warm white light, turquoise reflections on the water and a slight bloom on the glowing cues. Nothing roofs the play space. Show worn edges and small corner chips, and a little moss, dirt and water staining low on the walls and near the water.

Light, images 7-8: soft warm studio light on cream parchment, no sky or scenery.

## Players and scale

- The only characters are the player pawns. This is the first course of a match and promotion comes later, so all twelve runners are pawns: up to six cream and six charcoal. The teams differ only by body colour, like chess: no scarves, neckerchiefs, coloured collar bands or team-colour accents. Name tags are UI and are not drawn.
- Look (references 7-8): about 1.0 m tall, a low-poly chess pawn with clear facets and a soft cel-shaded toy look:
  - a round faceted head with two glossy black oval eyes, each with a white glint, and soft blush cheeks;
  - a thin neck ring in the body's own colour over a flared conical body with stepped base rings;
  - short stub arms sticking straight out at mid-height, ending in black ball mitten hands;
  - two black rounded shoe-feet.
- Cream team: pale peach-cream body #F5E0C2, dark warm-grey hands and feet #302B2A, pink blush #FA9E94. Charcoal team: soft charcoal body #59595F, near-black hands and feet #121111, dusty-pink blush #B36666.
- Scale everything against the 1 m pawn: a coach rig is 7 m long and 2.75 m tall; the shed and stall fronts and the crate stacks are 2.4 m; the loft deck is 2.4 m up; the giant waterwheel is 18 m across; the marble cliff is 8 m; the final gate is 8 m wide in an 11 m wall. The pawns look small; the yard is big.
- Pawns run, jump, dive, grab with their ball hands and push, both teams mixed, spread out, never a crowd. A pawn hit by a hazard tumbles over, toy-like and unhurt, with a little dust; one that misses a jump splashes into the water.

## Colour and material rules

Paint base colours only; light and shadow come from the scene. Use toy-like big masses with large rounded bevels, silhouette first; nails, grain and cracks are texture.

- Cream limestone #DDD1B9: walls, towers, gatehouses, parapets, piers. Chunky, slightly swollen blocks with big bevels, worn edges, per-block tone variation and recessed mortar (block shapes as in reference 6; colour pale cream, never orange or sandy).
- Shade stone #B7AA92: plinths, copings, string courses and the darker squares of the rank bands.
- Flagstones #D6C7A8: square slabs about 2 m across, all one pale sandstone, with gentle per-stone tone variation and thin darker joints (texture only). Flush, no curbs, no relief, no checkerboard.
- White marble #EEE7DA: smooth, plain, unveined, satin. Used only on the marble cliff and on the railings and side walls of the landings, the cliff top and the section end. It always means "cannot be climbed".
- Roof slate #5A5B5E, neutral dark grey: every roof (shed and stall lean-tos, crane houses, hoist hoods, the palace).
- Light oak #BC9665 and walnut #65432D: decks, crates, pallets, machinery, gate leaves, beams, coach chassis.
- Dark iron #393D43: straps, rails, chains, tyres, lamp posts, bearings, counterweights.
- Matte brass #9C8354: all decorative metal - crowns, crests, crown plates, finials, hub bosses, keystones, trims, coach mouldings. Dull, never shiny.
- Cloth and goods: cream banner wool #E6D9BC with a matte brass border and the crest; sand canvas #CDBE9C tied over covered loads; burlap sacks #CDB48A; straw #D9B65E.
- Coaches: bottle-green lacquer #2E5D4B or walnut-brown lacquer #4F3424, with matte brass mouldings. Horses: bay #7A4A2A, grey #D8D1C4, black #2E2925; walnut leather harness with matte brass fittings; cream plumes.
- Water: open-air turquoise (shallow #3D9EBD, deep #0A3861) with sky reflections, and white foam only at edges and under falls. Falling water is a white-blue streaked sheet #DBF0FF.
- Trees: cypress and round trees #425B42, only outside the walls.

What the colours mean. Keep every cue; style it, never remove it:
- Bright functional gold #E2B23C with a faint glow = grab here, or step or jump from here. It appears only on:
  - the four jump lines at the giant-wheel gaps;
  - the conveyor's two top edges;
  - the four hoist-well boarding edges;
  - the loft deck's south lip and north edge;
  - the pond-edge strip and the wheel landing's front edge;
  - the paddle wheel's eight handle bars;
  - on the final gate, the two ring handles and the progress-bar fill.

  All other metal, including both gates' crown plates, is matte brass or iron.
- Red glow = a hazard is about to move or is moving. It appears on:
  - coach lanterns in warning #FF4A3A and the translucent charge stripes #FF3829;
  - the row-gap pads when they flash #FF3824;
  - the jib-tip lamps and the cranes' floor fans #FF331F;
  - the hoist lamps and well stripes;
  - the berth's warning circle #FF331F.

  Nothing else in the yard is red, crimson or rust.
- Green glow #59FF73 = go now: row-gap pads as a gap opens, jib-tip lamps as a rest begins, hoist lamps over a boardable pallet. No other bright green.
- Amber glow: coach lanterns at rest #FFC86A (calm), hoist lamps in their last moment #FFB833 (hurry), thin crane reach arcs on the floor #FFC74D. Every other lantern is unlit by day.
- Pale gold #FFDB9E: the shock ring where the coffer lands.
- No sky-blue, royal-blue, navy or orange decoration (the sky and the water keep their natural colours) and no team colours: roofs are slate, cloth is cream or sand, coaches are green or brown.
- Checker patterns appear only in stone, as the rank bands.

## Layout, south to north

Real game sizes; draw the match version.
- Axes: x runs across the course (negative = west = left when walking north), z runs along it (north = forward), and y is absolute height. Units are metres and seconds.
- "m up" means above the floor you stand on. The floor is y 0 from the start court to the pond, y 8 on the platform and y 7 on the entrance band.
- Art is a skin over existing solid blocks: keep every size and position below. Add decoration only on walls, roofs, beyond the walls or on the picture parts named here.
- Timings say what moves and what warns; each image shows one moment of them.

**Zone 1 - Wagon Yard**

1. **Start court** (z -16..0, y 0).
   - A paved court 24 m wide and 16 m deep inside plain 5 m stone walls on the west, east and south (add rook merlons). The gate wall closes its north side, and its floor runs on under the gate.
   - The twelve pawns wait in two blocks facing north: cream on the west half (x -1.5, -3, -4.5), charcoal on the east half (x 1.5, 3, 4.5), in rows at z -8 and -12.
   - Two solid props stay as they are. A parked royal coach without horses, like the rigs' coaches, stands in the south-west corner (box 2.3 x 2.75 x 4.2 m centred at x -9.8, z -12.5, nose north). Three crates stand in the south-east corner: two 1.4 m crates in a row at x 9.1..10.5, z -15.3..-12.3, and a 1.1 m crate on the south one. Nothing else.
2. **Outer gate** (z 0..2).
   - A flat-topped curtain wall 46 m wide (x -23..23), 7 m tall and 2 m thick; add rook merlons on top.
   - Its opening is a plain rectangle 6 m wide and 5.6 m clear (x -3..3) under a flat stone lintel 1.4 m deep. The lintel is the wall's top course: draw no arch. Its court face stays plain; the game writes its 3-2-1 countdown there before the start, but draw no digits.
   - Two solid round towers 3 m across and 10 m tall stand at x +/-4.8, 0.5 m proud of both faces, with rook crowns on top. Picture corner towers 4 m across and 12 m tall stand at x +/-24.
   - Two walnut leaves 3 x 5.4 x 0.35 m are hinged at x +/-3, with three iron straps and a matte brass crown plate on their court side. They stay shut until 3 s after the start, then swing 90 deg north into the yard over 2.5 s and stay open along the opening's sides (to z 4).
   - The crest plaque crowns the wall over the opening, and a rank band lies in the opening. Four banners hang on the yard face at x +/-10 and +/-17 (3.1-5.7 m up); there are none on the court face.
3. **Wagon yard** (z 2..50, y 0).
   - One flat paved floor 44 m wall to wall, inside 5 m side walls with rook merlons (1 m blocks every 2 m).
   - Along each side wall runs a granary strip at |x| 19..22, z 2.6..47.6: a solid goods block 2.4 m high with a flat front at |x| 19. Draw the goods packed to that front and up to 2.4 m, with no open bay, gap, ledge or pocket.
   - Each side has:
     - ten 4.5 m bays between eleven walnut posts just inside the front (z 2.6, 7.1 ... 47.6);
     - a slate lean-to roof from 4.6 m at the wall down to a 3.4 m eave at |x| 18.65, its fascia an oak board cut in rook crenels with a matte brass edge;
     - an unlit lantern under the eave on every second post (z 7.1, 16.1, 25.1, 34.1, 43.1);
     - a plank floor.
   - Bay goods, south to north. West: crates, barrels, hay, sacks, covered load, then the same again. East: covered load, crates, barrels, hay, sacks, then the same again.
   - Mid-row on each side (centred z 25.1, 5 m long, 3.55-3.75 m up), a raised royal canopy carries the crest, facing the yard.
   - The 38 m between the shed fronts belongs to the coaches: no lamps, banners, props or trees there, and nothing hanging over it.
   - Never swept, so safe: the 3.3 m strip inside the gate (z 2..5.3) and a 0.8 m strip along each shed front (|x| 18.2..19). The corner nooks beside the sheds are bare floor.
4. **The five coach rigs** (the zone's obstacle).
   - Each rig is ONE rigid unit 6.95 m long, 2.3 m wide and 2.75 m tall: a closed royal coach (4.2 m) at the back and a harnessed pair of horses side by side (2.6 m, at x +/-0.55 of the rig's centre line) in front, with pole and traces between. Only the horses' heads (about 0.6 m) and the crown finial leave that box.
   - Coach:
     - a walnut chassis 0.6-0.84 m up;
     - a closed boxy cabin 2.1 m wide, 1.64 m tall and 2.9 m long, with wooden shutters instead of windows and the crest on each door;
     - a roof slab to 2.66 m with a matte brass crown finial (top under 3 m);
     - an empty coachman's bench and footboard in front;
     - four 1.1 m spoked wheels with iron tyres;
     - two big lanterns at the cabin's front corners, about 1.8-2.3 m up.
   - Horses: back 1.7 m, head about 2.2 m, ears 2.5 m.
   - Bodies: rigs 1, 3 and 5 bottle green, rigs 2 and 4 walnut brown. Coats run bay, grey, black in turn; a pair always matches.
   - Starting places (x, z): (-7.3, 15.1) facing east, (7.3, 15.1) facing west, (-7.3, 27.3) east, (7.3, 27.3) west, (0, 39.4) north.
   - They roam the whole yard (bodies within |x| <= 18.2, z 5.3..49.2), never touching each other, in a loop:
     - turn on the spot (a half turn takes 3.4 s);
     - aim 0.6-1.2 s (turn plus aim at least 1.8 s);
     - charge straight 10-30 m at 9.3-10.5 m/s, or dart 4-10 m;
     - rest 0.3-0.9 s.
   - Cues:
     - The lanterns glow amber at rest and red from the start of the turn to the end of the charge.
     - A translucent red stripe 2.8 m wide lies on the floor from under the rig's centre to where its nose will stop. It is faint while turning, pulsing while aiming, shrinks behind the rig as it charges, and is gone at rest.
     - While aiming, the horses toss their heads and lift their forelegs a little (the body tips up about 10 deg; no rearing).
   - A charge knocks pawns sideways and over.

**Zone 2 - Giant Waterwheel**

5. **Entry landing** (z 50..58).
   - At the yard's north edge, 2 m stone parapets (0.4 m thick) close the canal at |x| 10..22.
   - Between them a 20 x 8 m stone landing (x -10..10, y 0) juts over the water on two stone piers (x +/-7). It has smooth white marble side walls 2 m tall and 0.4 m thick, and a 0.9 m marble railing along its north edge, open only at the two walkway mouths (x -6.2..-3 and 3..6.2).
   - Iron lamp posts stand at (+/-9, 51); checkpoint 1 and its rank band are at z 53.
6. **Canal** (z 50..82). Stone-lined, 44 m wall to wall, the water 1.2 m below the decks (y -1.2) and 3.3 m deep; the 5 m walls with rook merlons continue from the yard. Water lies beside both landings (|x| 10..22) and fills the whole width from z 58 to 74. Picture towers 4 m across and 12 m tall stand outside the far corners at (+/-28, 82).
7. **Giant waterwheel crossing** (z 58..74).
   - Two railing-less wooden walkways 3.2 m wide (x -6.2..-3.0 and 3.0..6.2), deck top y 0, oak with walnut planks, stand on thin walnut posts down to the canal bed, with 6 m of open water between them. Each breaks for 3.4 m at z 64.3..67.7; a bright gold jump line crosses the deck at both edges of each break (four lines).
   - The wheel stands upright in the canal facing the runners: its axle runs along the route at (0, 5.27, 66). It is 18 m across (top 14.3 m above the decks, bottom 2.5 m under water) and 1 m deep (z 65.5..66.5). It has:
     - two rims (radius 8.4-9.0) joined by 28 bucket boards alternating light oak and walnut;
     - six spoke pairs (oak, 0.45 m wide, radius 1 to 8.5) with walnut ties and iron bands;
     - a walnut hub 2.2 m across with a matte brass crown boss;
     - the iron axle in iron bearings on two walnut posts standing in the water between the walkways (z 64.8 and 67.2).
   - The walkways pass through the wheel's face inside its rim (the rim's inside passes 0.4 m under each walkway's outer edge). The six wedge-shaped holes between the spokes are the way through, and nothing may be added in them.
   - It turns clockwise seen walking north (rising on the west, falling on the east) at 15 deg/s: a hole reaches each walkway every 4 s and stays clear about 1.5 s. Pawns sprint, jump from the gold line and dive through; a short jump ends in the canal.
   - A timber flume 1.6 m wide runs about 14.6 m up, from over the wheel's top north to a cistern at z 72.4..73.8. The cistern is 2.4 x 1.6 x 1.4 m, shaped as a small rook water tower, on two tall timber trestles standing in the water between the walkways (z 70 and 73). Water pours from the flume's mouth onto the wheel's top, and a 1 m sheet spills down the wheel's east side into foam.
8. **Far landing** (z 74..82). The entry landing's twin, with 0.9 m marble railings on its sides and its south edge (open at the walkway mouths), lamp posts at (+/-9, 81), and checkpoint 2 with its rank band at z 78.5. 1.5 m stone parapets stand at |x| 10..22 on its north side. It opens straight into the warehouse.

**Zone 3 - Royal Warehouse**

9. **Warehouse mouth** (z 82..87).
   - The bailey widens to 52 m (x -26..26) behind 6 m corner walls (|x| 22..27). From here 11 m walls with rook merlons run to the final gate.
   - Along both walls, sorting stalls stand at |x| 23.5..26 from z 82 to 129.5:
     - a solid goods block 2.4 m high with a flat front at |x| 23.5 (goods packed to the front, no gaps);
     - ten 4.75 m bays a side, each under its own slate lean-to (4.6 m at the wall to a 3.4 m eave at |x| 23.15) on walnut posts, each with a small matte brass plaque bearing a piece icon;
     - a balance scale on an iron post that shows above each stall row at z 105.75.
   - Play width 47 m. Lamp posts stand at (+/-11.2, 82.8). Cream banners hang on the inner wall faces at z 88, 100, 112 and 136 (7.0-9.6 m up), none near the berth gantry.
10. **Conveyor** (z 87.1..88.9).
    - A 47 m cargo line from stall front to stall front (x -23.5..23.5), 1.6 m wide, its top 0.45 m up (half a pawn), and boxed down to the floor so nothing can crawl under it.
    - It is a timber roller bed in a walnut box frame, with close-set oak rollers or an endless run of oak slats on top so the movement reads, and iron axle caps along its sides. Bright gold strips run only along its two top edges. Never a rubber or steel belt.
    - It runs west -> east at 3.6 m/s, carrying 11 loads: nine single oak crates 1.3 x 1.2 x 1.3 m and two two-crate stacks 2.4 m tall (walnut top crate), back to back. About nine are on the line at once, 2.9-4.6 m apart.
    - The loads come out of a dark doorway in the west stall (intake) and go into a dark doorway in the east stall (sorting). Each doorway is 2.3 m wide and 3.2 m tall, with iron jambs, a lintel and a pictogram board above (a crate and an outward arrow on the west, a crate and an inward arrow on the east), and a stored two-crate stack beside it.
    - Pawns hop on through a gap and ride; a crate's front face shoves them off. Then comes 3.7 m of open floor.
11. **Stack row 1** (z 92.6..94.4).
    - A wall of two-crate stacks 2.0 m wide, 2.4 m tall and 1.8 m deep (oak and walnut crates, iron bands, piece icons) runs from stall to stall. Each half has nine slots on a 2.2 m pitch (centres |x| 1.1, 3.3 ... 18.7), eight stacks and one gap; fixed stock stands beyond |x| 19.8.
    - Overhead, wall to wall at 6.5-7.0 m, runs a walnut beam with an iron rail, its ends on carved knight-head stone corbels in the walls. Two iron trolleys (one a half) hang from it, each with a small unlit lantern and two ropes.
    - Every 2.4 s a trolley lifts the stack beside its half's gap 0.25 m and swings it into the gap. So each gap walks one slot at a time from the middle to the wall and back.
    - The halves are exact mirror images in time: both gaps stand at the same slot and are always in the same state. When they meet in the middle they make one 4.6 m opening.
    - Cues: a floor pad 1.9 x 2.4 m in each gap glows green as the gap opens (its first 0.3 s), then flashes red from 0.8 s before the next stack swings in until it is set down. The trolley parks over the stack that moves next.
    - Then comes 4.1 m of open floor.
12. **Timber crane yard** (z 97.8..109).
    - Timber stores along both walls (|x| 20.11..23.5, z 97.8..113.4) narrow the way to 40.2 m. They are solid 2.4 m walls of squared beams in layers, with iron bands and flat faces.
    - Two slewing jib cranes stand on stone drums 2.4 m across and 2.4 m tall (a carved rook band near the top, no ledges) at (+/-9.5, 99). Each has:
      - a walnut mast to 7.6 m;
      - a small crane house on the drum (2.0 x 1.8 x 1.8 m, slate roof, an oak treadwheel on its side), turning with the jib;
      - a walnut brace at 45 deg from the house roof (4.3 m) up to the jib (7.5 m), 3.2 m out;
      - a jib 7.4-7.85 m up reaching 10.4 m out, with a crenellated iron counterweight under its short tail (6.5-7.5 m up);
      - two slings holding an 8 m bundle of squared timber (two beams under one, iron straps) from 2 m to 10 m out, riding 0.13-0.55 m above the floor - ankle to hip of a pawn.
    - Both jibs always point the same way: they rest pointing west 1.4 s, slew 6 s through north to east, rest east 1.4 s and slew back (a 14.8 s cycle). Their north half-circles overlap in the middle, so no lane is safe from them. Dark rest boards with iron chocks mark the timber's rest line across the yard at z 99.
    - Cues:
      - a lamp under each jib tip: green only as a rest begins, red flashing from a second before each slew and through it;
      - a red fan on the floor showing where the timber will be in the next second;
      - a thin amber arc on the floor at its reach;
      - a bell rung three times before each lift (sound only: no bell is modelled, draw none).
    - Refuges: the 0.8 m ring round each drum and the 0.61 m strip beside each timber store.
13. **Loft hoists** (z 109..113.4).
    - Two mirrored hoists stand on posts at x +/-3.5 (solid timber posts 0.4 m wide and 3.6 m tall). Each serves two pallet wells, inner |x| 0.9..3.27 and outer |x| 3.73..6.10, at z 110.97..113.37. Between the inner wells a 1.8 m aisle dead-ends at the loft.
    - Above each post stands a lucam (hoist) frame: a post up to 6.4 m, a 5.5 m walnut arm at 6 m with carved knight-head ends and a small slate hood, an iron pulley over each well, and four ropes to the pallet's corners.
    - Pallets: oak plates 2.37 x 0.22 x 2.4 m with iron corner shoes and two sacks each, travelling between 0.22 m (down) and 2.40 m (flush with the loft deck).
    - Each pallet loops in 7.4 s: down 2.2 s (board), rise 1.5 s, up 2.2 s (step off), lower 1.5 s. In each hoist one pallet is down while its partner is up (3.7 s apart), and both hoists move together, mirrored.
    - Cues:
      - a lamp per pallet on the arm: green while boardable, amber flashing in its last 0.8 s, red while moving or up;
      - red stripes on the well floor: dim while the pallet is up, flashing from 1 s before it comes down;
      - a bright gold boarding edge on each well's south side.
14. **Loft** (z 113.4..116.6).
    - A solid timber block with no railings and no doors or arches in its face; twenty vertical oak boards cover the face.
    - Its middle (|x| <= 12) is a plank deck 2.4 m up with a bright gold south lip and north edge. Its ends (|x| 12..23.5) are 4.4 m tall, each topped with five two-crate stacks (up to 6.0 m).
    - The hoists are the only way up; the only way off is the 2.4 m drop from the open north edge.
    - Two pictogram boards hang one above the other between the two lucam posts, over the deck's south lip (|x| < 3.3, about 3.5-4.5 m up). They are pictures only, with nothing below them, because the loft is only 2.4 m high there and pawns step off the raised inner pallets right under them. One shows a pawn stepping onto a pallet under a green lamp; the other shows a crate hanging over a crossed-out pawn.
    - Then comes 4.5 m of open floor to land on.
15. **Stack row 2** (z 121.1..122.9). The same as row 1 but on its own clock: each gap stays open a little longer (a step every 2.7 s, green for its first 0.6 s), so rows 1 and 2 are seldom in the same state.
16. **Heavy berth** (z 123..127).
    - Twin walnut gantry beams 54 m long run at 9.0-9.8 m (z 124.2 and 126.6), with matte brass bands and an iron rail between them. Their posts are hidden in the walls, with knee braces and 2.2 m winch wheels on the wall faces above the stall roofs.
    - An iron trolley with a winch drum carries the king's coffer on four chains. The coffer is a chest 3.2 x 2.4 x 2.4 m: oak body, walnut lid, iron corners, matte brass straps and lock, and a matte brass crown on the lid.
    - Four berth marks lie flush on the floor at x -13.5, -4.5, 4.5 and 13.5 (centred z 125.4): dark boards 3.9 x 3.1 m with iron corner plates.
    - The loop takes 10.4 s: carried along the beam 2.4 s (at most two berths over), held 1.0 s with its underside 3.6 m up, lowered 1.1 s, left on the floor 4.5 s as a wall, hoisted 1.4 s.
    - Cues: a red circle of 6 m radius on the floor round the target berth, dim while the coffer is carried and bright and pulsing while it is held and lowered; it reaches south over the landing strip to z 119.4. As the coffer lands, a pale-gold shock ring runs out to 6 m with dust; pawns jump it.
17. **Apron** (z 127..132.85). The stalls end at z 129.5, so the last strip is 52 m wide. Lamp posts stand at x +/-8.5 and +/-21.5 (z 131.95). Checkpoint 3 lies at z 131.8 across the full width, with its rank band at z 131.0..132.0. A bright gold strip runs along the pond's edge (z 132.6..132.85), with the water 0.5 m below.
18. **Mill pond and paddle wheel** (z 132.85..143).
    - One pond from wall to wall, 2.5 m deep.
    - In its middle stands the upright paddle wheel, turned a quarter turn from the giant wheel: its axle (0, 4.6, 137) runs across the way, so a runner faces its paddles. It is 10.4 m across and 3 m wide, with:
      - eight oak paddles (2.9 x 1.0 m) on walnut spokes and rims, each with a bright gold handle bar on its leading face;
      - matte brass hub caps;
      - an iron axle on two stone piers with iron bearings (x +/-2..3).
    - Its disc (z 131.8..142.2) is as long as the pond. The rising south paddles reach out over the last metre of the apron above head height, and at the top they pass about half a metre short of the landing's gold edge.
    - It turns 28 deg/s (12.9 s a turn) with its near (south) side rising. A pawn jumps, holds a paddle or a handle, rides over the top and is let go 30 deg past it onto a 7 m oak landing at the cliff top (x -3.5..3.5, z 141.4..143, y 8) with a bright gold front edge.
    - A 10 m waterfall sheet (x -5..5) runs down the cliff behind the wheel.
    - Dressing, mirrored at x +/-15.5, all pictures out of every reach:
      - A sluice portal in the cliff, about 6 x 6 m: rook-gate jambs, a lintel with a matte brass crown keystone, a dark tunnel, and a raised solid oak sluice board with three flat iron straps in iron guide slots, with nothing hanging below it. A 1.6 m wooden gear wheel sits on the outer jamb. The portal pours a 4 m sheet of water into foam.
      - A moored cargo barge 9.2 x 3.2 m (x +/-10.9..20.1, z 138.2..141.4), roped to iron rings on the cliff, with a curled bow post carrying a small matte brass crest shield, and crates, barrels and a covered load on deck.
      - A timber jib crane on the platform (post at x +/-17.7, z 144.4, from y 8 to 12.8, with a proper knee brace from the post up to the arm), reaching about 5 m out over the pond and lowering a net of sacks onto the barge.
      - Cream banners on the cliff at x +/-11.1 and +/-19.9 (y 5.1-7.7).
    - No bridge, stepping stones or reachable boat.

**Zone 4 - Final Gate**

19. **Marble cliff** (z 143..143.6). 52 m wide, smooth plain white marble from the pond bed up to y 8 (8.5 m above the water), with no ledges, holds, carvings, vines, pipes or ladders; the wheel is the only way up. A 1 m marble railing runs along its top, except for the 7 m gap at the wheel landing.
20. **Platform** (z 143.6..155, y 8). A 52 m wide paved terrace with checkpoint 4 and its rank band at z 146, lamp posts at (+/-8, 144.5), and the dock-crane posts at x +/-17.7 by the cliff edge. The side walls stand 3 m above it.
21. **Final gate** (z 155..157).
    - An 11 m flat-topped curtain wall (y 8..19, 2 m thick, rook merlons on top) runs across the platform. Its rectangular opening is 8 m wide and 7.3 m clear (x -4..4), under a flat lintel (y 15.3..19), with a rank band in it.
    - Two solid round towers 3.6 m across rise 14 m above the platform at x +/-6.6, with rook crowns. Picture towers 4 m across stand outside the corners at (+/-28, 159), 11 m above the platform.
    - Four cream banners hang on the south face at x +/-12 and +/-18, 7.0-9.6 m above the platform.
    - The push gate: two walnut leaves 4 x 7.2 x 0.5 m hinged at x +/-4. Each has four iron straps, a matte brass crown plate (1.2 m, at x +/-1.8, 4.3 m up) and a bright gold ring handle (0.55 m, at x +/-0.7, 0.95 m up - a pawn's chest).
    - The leaves swing north, away from the pushers, up to 100 deg (slow at first, soft at the end), and only while pawns grip them. One pusher takes 10.5 s; more go faster (at most three times as fast); they never close. A third of the way through, the leaves stand about 26 deg open, just enough for a pawn to squeeze through the middle.
    - On the wall face over the opening: an iron progress-bar frame 5 x 0.5 m, 8.1 m above the platform (y 16.1), whose bright gold fill grows from the left; a push pictogram above it (8.9 m up, y 16.9); the crest higher up.
22. **Beyond the gate, match version** (z 157..181).
    - A 2 m strip of platform lies behind the wall.
    - A 12 m wide causeway (z 159..169, y 8) has 1.1 m marble railings and open air on both sides.
    - A 12 x 6 m terrace (z 169..175) holds checkpoint 5 and its rank band at z 172.
    - An open stone portal stands at z 175: 1 m piers at |x| 6..7 with rook crowns, and a lintel 6-7 m above the terrace (y 14..15) with a crown keystone; 12 m clear.
    - Four 0.25 m steps go down (z 175..177) to a 12 x 4 m entrance band at y 7 (z 177..181) with marble railings, ending at the course exit (0, 7, 181).
    - Beyond it, a short open-air walled passage with plain walls and no banners leads to the next court, in front of the palace's main facade.
    - Do not draw the map-test finish: no walled finish court, no checkered finish line, no crown on a pedestal.
23. **Around the course** (pictures only, outside the walls and below the floors; nothing is modelled there yet). Ground the yard in the castle: the outer faces of the walls with plinths, pilasters and arrow slits, further curtain walls and rook towers, slate roofs of lower castle buildings, lawns, cypress and round trees, distant green hills, and the palace far to the north. The yard never floats.

## The images

In images 1-6 the camera looks along the course: forward (north) is away from the viewer and up the frame, left is west and right is east. The entries use only left, right, near and far. Images 1-6 are scenes; images 7-8 are asset sheets.

Copy the REFS block for the image's kind, then these three blocks, word for word into every image prompt:

**REFS (images 1-6):** Attached references, in order: 1-3, asset sheets - take only their render style; 4-5, two courts of the same castle - their title format, sunny sky, crenellated cream walls, rook-topped towers, grey slate roofs and the far palace facade, never their layouts, machines, team banners or bright gold; 6, an in-game capture - only its stone block shapes and turquoise water, never its honey tint; 7-8, the player pawns; 9, the top-down layout map of this part, far end at the top and left = west (cream floor, darker stone walls and round towers, turquoise water, light oak decks, brown goods, dark grey roofs, white marble, translucent red hazard areas, bright green open gaps, gold jump and step edges, stone-square rank bands, green lawn outside): copy its counts and positions exactly; it shows places, not this image's moment.

**REFS (images 7-8):** Attached references, in order: 1-3, asset sheets - their render style, sheet format and soft studio light; 4-6, courts of the same castle - materials only; 7-8, the player pawns, for the one scale pawn.

**STYLE:** Stylized 3D game concept art in the clean toy-like render style of references 1-3: big chunky shapes, large rounded bevels, worn edges, silhouette first. The castle of a cute chess kingdom, stage 1, outside the palace: pale cream limestone in big soft-edged blocks (never orange, honey or sandy), plain pale sandstone flagstones, grey slate roofs, light oak and dark walnut with visible grain, dark riveted iron, and dull brownish matte brass like old bronze on every crown, crest and trim. Chess identity: every round tower ends in a flat chess-rook crown (flared corbelled collar, ring of chunky merlons), never a cone; every wall top has chunky rook merlons; the yard's crest, a chess knight's head in profile under a small crown, flat matte brass on a cream shield, sits on plaques, cream banners and coach doors; crates, sacks and barrels carry small dark stencilled chess-piece icons; carved knight heads appear only high up, on crane and hoist arm ends. The only characters are the players: small chess pawns about 1 m tall exactly as in references 7-8 - low-poly and faceted, pale peach-cream or soft charcoal bodies, a round head with two glossy black oval eyes with a white glint and pink blush, a thin neck ring in the body's own colour, short stub arms with black ball hands, black rounded feet, no scarves or team colours. Everything else is big next to them.

**CUES:** The game's colour code, only where this entry names it: bright glowing gold only where a pawn grabs, steps or jumps; translucent glowing red only on hazard warnings; glowing green only on go signals; glowing amber only on resting coach lanterns, a hoist lamp's last moment and thin crane reach lines. Every other lamp and lantern is unlit. No blue, orange or red decoration anywhere: cloth is cream or sand, banners cream with the brass crest, coaches bottle green or walnut brown, roofs grey slate.

**AVOID:** any text except the title, ribbon and labels this entry asks for - no numbers, sizes, letters or words on signs, crates, boards or floors (signs are pictograms only); faces, eyes or blush on buildings, carvings, machines or horses; any chess piece other than the pawn players, and no statues of pieces; people or extra figures; cone or pyramid roofs on the yard's towers; shiny gold trim; glass and flames; a chessboard or checker over a floor; painted arrows on floors; posts, props or railings added on the paths; portcullises, drawbridges, capstans or overhead chains; ruins, modern objects, photorealism.

### 1. RCY_01_overview.png

Portrait 2:3 (1024x1536). Title at the top in a serif face, "Royal Carriage Yard - Overview", with the small ribbon "Stage 1 - Outer Castle" under it, as in reference 4. Bright clear midday, blue sky, short soft shadows, turquoise water.
- Camera: very high behind the start, on the course's centre line, about 300 m up over a point 55 m behind the start court (x 0, z -70), looking straight along the course and down about 60 deg, normal lens.
- Framing, by height from the bottom edge:
  - start court and outer gatehouse in the bottom tenth;
  - the wagon yard up to about 30%;
  - the canal and giant wheel at about 30-43%;
  - the warehouse at about 43-60%;
  - the mill pond, white cliff and platform at about 60-68%;
  - the final gatehouse at about 70%, with the section's end just above it;
  - the top quarter shows what lies beyond: a short walled passage, the next court and the palace, its crowned skyline under the title.

  The course is one straight walled strip of even width filling the middle third of the frame's width; castle roofs, rook towers, lawns, cypresses and green hills fill both sides.
- Moment: mid-race; about twelve pawns, tiny specks, spread from the outer gate to the cliff. Do not enlarge them.
- Must show (keep these if anything has to go):
  1. Flat rook-crowned towers and rook merlons along both sides of the whole strip.
  2. Wagon yard: slate-roofed granary sheds along both side walls; five closed coaches, each drawn by a side-by-side pair of horses, scattered in the open yard, two of them with long translucent red stripes on the floor ahead.
  3. Canal: turquoise water across the full width; two plank walkways; the giant waterwheel standing upright in the canal like a huge clock face turned toward the viewer, the walkways running into its lower half; a flume on tall stilts above it.
  4. Warehouse, wider and with taller walls: a long cargo line across its whole width; two walls of crate stacks across the yard; two timber cranes over red floor fans; a timber loft with two hoist frames; a high gantry with a chest hanging on chains over a red floor circle.
  5. The mill pond with one upright paddle wheel in front of a smooth white marble cliff with a waterfall; sluice portals, moored barges and dock cranes on either side.
  6. The platform and the final gatehouse, its two leaves part open; behind it the terrace, the open stone portal and the steps.
  7. The palace far beyond.
  8. Four small cream pills overlaid along the right edge, each with a thin leader line to its zone: "1 Wagon Yard", "2 Giant Waterwheel", "3 Royal Warehouse", "4 Final Gate".
- If there is room: the open outer-gate leaves, rank bands, cream banners, iron lamp posts, and the parked coach and crates in the start court.
- CHECK: five coaches; the giant wheel face-on with both walkways entering it; one paddle wheel at a white cliff; every yard tower a flat rook crown; no text but the title, ribbon and four pills.

### 2. RCY_02_wagon_yard.png

Landscape 3:2 (1536x1024). Title at the top in a serif face, "Royal Carriage Yard - Wagon Yard", with the small ribbon "Stage 1 - Outer Castle" under it, as in reference 4. Bright clear midday, blue sky, short soft shadows.
- Camera: about 40 m up just behind the start court's back wall, on the centre line (x 0, z -22), looking straight along the course and down about 45 deg, wide lens.
- Framing: the front half of the start court across the bottom fifth; the outer gatehouse up to about a third; the wagon yard, wall to wall, filling the middle half of the frame; the canal's stone parapets and the entry landing along the top, under the title.
- Moment: a few seconds after the gate opened. The leading pawns are spread through the yard; the last are still crossing the start court.
- Must show (keep these if anything has to go):
  1. Five closed royal coaches spread out in the open yard, each drawn by a side-by-side pair of horses (bodies bottle green or walnut brown; horses bay, grey or black, a pair always matching):
     - one turning on the spot: red lanterns, and a faint red stripe on the floor at an angle to its body, where it will charge next;
     - one about to charge: red lanterns, a bright translucent red stripe straight ahead to where it will stop, its horses tossing their heads and lifting their forelegs a little;
     - one charging: red lanterns, only the part of its stripe ahead of it still showing, a little dust, a pawn tumbling off its flank;
     - two resting: amber lanterns, no stripe.
  2. Both granary sheds along the side walls: a long row of equal bays under slate lean-to roofs with crenel-cut oak fascias, walnut posts and unlit lanterns. Goods are packed solid to the front (crates, barrels, hay, sacks and covered loads with chess-piece stencils), with a raised royal canopy and the crest at the middle of each row.
  3. The outer gatehouse seen from the court: a flat-topped wall with rook merlons and a plain rectangular opening under a flat stone lintel (no arch). Two flat rook-crowned towers stand beside it, with the crest shield on top. A rank band (one flush row of light and darker stone squares) lies under the opening, and the two walnut leaves are swung open into the yard. No banners on this face.
  4. Pawns: about four in the start court heading for the gate, about six spread through the yard dodging, and one walking the narrow strip along a shed front.
  5. Side walls with rook merlons; the stone parapets at the far end, with the landing between them and its low white marble railing.
  6. Nothing else on the yard floor: no lamps, banners, props or trees in the yard, and nothing hanging over it.
- If there is room: castle roofs and rook towers beyond the walls; the bottom of the giant wheel rising behind the landing.
- CHECK: five coaches with two horses each; red only on the moving coaches' lanterns and stripes; the yard floor holds only coaches, pawns and stripes; the gate opening is rectangular.

### 3. RCY_03_waterwheel_crossing.png

Landscape 3:2 (1536x1024). Title at the top in a serif face, "Royal Carriage Yard - Giant Waterwheel Crossing", with the small ribbon "Stage 1 - Outer Castle" under it, as in reference 4. Bright clear midday, blue sky, turquoise water with sky reflections.
- Camera: about 9 m up over the left half of the wagon yard, about 10 m short of the canal (x -12, z 40), looking forward and a little right at the wheel's hub, down about 8 deg.
- Framing: the far half of the entry landing along the bottom edge; the wheel filling the upper middle, its top just under the title; both walkways and both gaps clearly visible; the far landing and the warehouse walls behind.
- Moment: an open wedge between two spokes is passing the left walkway.
- Must show (keep these if anything has to go):
  1. The giant waterwheel standing upright in the middle of the canal like a huge clock face turned toward the viewer, its lower edge in the water, about one pawn thick. It has two rims with alternating light oak and walnut bucket boards, six oak spoke pairs with walnut ties, and a walnut hub with a matte brass crown boss. Its iron axle runs toward the viewer on two tall posts standing in the water between the walkways. It turns clockwise: rising on the left, sinking on the right.
  2. Two railing-less plank walkways running straight at the wheel, one left and one right of its posts, entering its lower half. Each breaks for a short gap exactly where the thin wheel turns, with a bright gold line across the deck at both edges of each gap (four lines).
  3. Pawns: one diving through the open wedge between two spokes on the left walkway; one waiting at the right walkway's near gold line while a spoke sweeps down through the right gap; one landing on the far left walkway; one splashing into the canal below a gap.
  4. High above, a timber flume on tall stilts standing in the water, pouring onto the wheel's top, with a small rook-shaped cistern at its far end; a thin sheet of water spilling down the wheel's right side into white foam.
  5. In the foreground, the landing: stone deck, a low smooth white marble railing with two openings at the walkways, and a rank band (one flush row of light and darker stone squares).
  6. Canal walls with rook merlons; the far landing; the warehouse's taller walls and a corner rook tower behind.
- If there is room: iron lamp posts with bishop-mitre caps on the landings.
- CHECK: the wheel's round face toward the camera; railing-less walkways passing through it; four gold lines; nothing in the spoke holes.

### 4. RCY_04_sorting_yard_cranes.png

Landscape 3:2 (1536x1024). Title at the top in a serif face, "Royal Carriage Yard - Sorting Yard and Timber Cranes", with the small ribbon "Stage 1 - Outer Castle" under it, as in reference 4. Bright clear midday, short soft shadows.
- Camera: about 34 m up over the canal's far landing, on the centre line (x 0, z 76), looking straight along the course and down about 50 deg, wide lens.
- Framing:
  - the warehouse mouth's floor along the bottom edge;
  - the cargo line just above it across the full width, its two doorways at the left and right edges;
  - stack row 1 about a quarter up;
  - the two crane drums a little below the middle, the jibs and their loads sweeping the middle;
  - the loft and its two hoists across about two-thirds up;
  - stack row 2 and the berth gantry beyond, under the title.
- Moment: both cranes mid-swing, jibs parallel, pointing forward and to the right, still swinging right; both gaps of row 1 open.
- Must show (keep these if anything has to go):
  1. The cargo line: a long, low timber roller bed in a walnut box frame, with oak rollers on top and bright gold strips along its two top edges, running wall to wall. Oak crates ride it from the dark intake doorway at the left edge to the dark sorting doorway at the right edge, each doorway under a pictogram board (a crate and an arrow). About nine loads, two of them two-crate stacks; one pawn riding the line, one hopping on through a gap.
  2. Stack row 1: one wall of two-crate stacks across the yard under a beam crane, the beam's ends on carved knight-head corbels in the walls, with two iron trolleys. Its two gaps mirror each other, both open on green floor pads, with a pawn slipping through each.
  3. Two timber cranes on stone drums with a carved rook band: small treadwheel houses with slate roofs, jibs with crenellated iron counterweights, each jib's slings holding a long bundle of squared beams just above the floor (ankle to hip of a pawn). A translucent red fan lies on the floor ahead of each bundle and a thin amber arc on the floor at its reach, with a red lamp under each jib tip.
  4. Pawns: one hopping over a bundle, one following behind one, one standing in the clear ring round a drum, one edging along the narrow strip beside a timber store.
  5. Timber stores along both walls (solid walls of stacked squared beams); the sorting stalls along the walls with slate lean-tos, matte brass piece-icon plaques and a balance scale.
  6. Tall side walls with rook merlons and cream banners.
  7. The loft and its two hoist frames across the back.
- If there is room: dark rest boards with iron chocks in a line across the yard through both drums; the trolleys parked over the stacks that move next; the stored stacks beside the doorways.
- CHECK: row 1's two gaps in the same state; jibs parallel; bundles low, never in the air; the cargo line ends in the two doorways at the side walls.

### 5. RCY_05_hoist_loft_and_berth.png

Landscape 3:2 (1536x1024). Title at the top in a serif face, "Royal Carriage Yard - Hoist Loft and Heavy Berth", with the small ribbon "Stage 1 - Outer Castle" under it, as in reference 4. Bright clear midday, short soft shadows.
- Camera: about 26 m up over the north end of the crane yard, a little right of the middle (x 4, z 100), looking straight along the course and down about 42 deg, wide lens.
- Framing:
  - the hoists and the loft's face across the bottom fifth;
  - stack row 2 at about 40%;
  - the hanging coffer around the middle, with the gantry beams high above it at about two-thirds;
  - the apron, its lamp posts and the gold pond edge just above the middle;
  - the paddle wheel and the white cliff at the top, under the title.
- Moment: in each hoist the outer pallet is down and the inner one up; both gaps of row 2 are closing; the coffer is being lowered over the second berth mark from the left.
- Must show (keep these if anything has to go):
  1. Two mirrored timber hoist frames either side of the middle, each a post with an arm on top (carved knight-head ends, a small slate hood), two pulleys and four-rope bridles.
     - The outer pallets (nearer the walls) are down under green lamps, with two pawns boarding them over bright gold boarding edges.
     - The inner pallets are up, flush with the deck, under red lamps, with a pawn stepping off one onto the deck.
     - Dim red stripes lie on the floors of the two empty inner wells.
  2. The loft: a solid timber block faced with vertical oak boards, its plank deck with a bright gold front lip and far edge and no railings, its high ends topped with crate stacks. A pawn crosses the deck; another drops off its far edge.
  3. Two small pictogram boards hung one above the other between the two hoist frames' posts, over the deck's front lip. Icons only: a pawn stepping onto a pallet under a green lamp; a crate hanging over a crossed-out pawn.
  4. Stack row 2: a wall of two-crate stacks under its beam crane. In both gaps a trolley swings a stack in on its ropes over a bright red floor pad; a pawn hops back out of the left gap.
  5. The heavy berth: twin gantry beams high above with a rail and an iron trolley. The king's coffer (an oak chest with a walnut lid, matte brass straps and a brass crown) hangs on four chains halfway down over the second berth mark from the left, with a bright translucent red circle round it on the floor and two pawns running out of it. The other three berth marks (dark boards with iron corners) are empty.
  6. Winch wheels and knee braces on the side walls above the stall roofs; the sorting stalls along both walls.
  7. The apron with its four lamp posts and a rank band, and the gold pond edge; the paddle wheel and the white cliff beyond.
- If there is room: cream banners on the walls; the ends of the timber stores in the bottom corners.
- CHECK: four pallets, the two outer down with green lamps and the two inner up with red, mirrored; row 2's two gaps in the same state; the coffer hangs on four chains; no railings on the loft deck.

### 6. RCY_06_pond_cliff_and_gate.png

Landscape 3:2 (1536x1024). Title at the top in a serif face, "Royal Carriage Yard - Mill Pond and Final Gate", with the small ribbon "Stage 1 - Outer Castle" under it, as in reference 4. Bright clear midday, light haze far away.
- Camera: about 55 m up over the north end of the warehouse, a third of the way toward the right wall (x 12, z 108), looking forward and a little left, down about 42 deg.
- Framing:
  - the gold pond edge along the bottom;
  - the paddle wheel in the lower fifth;
  - the cliff top at about 30%;
  - the platform and the pushers at about 40%;
  - the final gatehouse from about 45% to 60%;
  - just over its wall, the terrace's far end, the portal, the steps and the entrance band;
  - beyond, in light haze toward the top, a short walled passage and the palace.
- Moment: three pawns push the gate. The progress bar is a third gold, and the leaves stand about a quarter open, just wide enough for a pawn to squeeze through the middle.
- Must show (keep these if anything has to go):
  1. The paddle wheel in the pond, turned a quarter turn from the giant wheel: its axle runs left-right across the course, so the camera sees its rising paddles and, a little from the right, its round side. It has eight oak paddles with bright gold handle bars, matte brass hub caps and two stone piers. Its near side rises: a pawn hangs from a gold handle being carried up, and another has just been let off onto the small oak landing at the cliff top with its bright gold front edge.
  2. The smooth white marble cliff across the full width, a waterfall behind the wheel, its top railing broken only at the landing.
  3. Both side docks: a sluice portal in the cliff (rook-gate jambs, a raised solid oak sluice board, a gear wheel) pouring water into foam; a moored cargo barge with a curled bow post and cargo; a timber jib crane on the platform lowering a net of sacks; cream banners on the cliff.
  4. The platform with two lamp posts and a rank band. Three pawns of both teams push: two grip the bright gold ring handles and one presses against a leaf. The two walnut leaves are swung away from them, and a fourth pawn squeezes through.
  5. The final gatehouse: a tall flat-topped wall with rook merlons, a rectangular opening under a flat lintel, two flat rook-crowned towers, cream banners, an iron progress bar a third filled with bright gold, and a push pictogram with the crest above it.
  6. Over the wall: the terrace's far end, the open stone portal with rook-crowned piers and a crown keystone, four shallow steps, and the entrance band with white marble railings. The causeway right behind the wall stays hidden, glimpsed only through the gate.
  7. Far behind in light haze: a short walled passage with plain walls, then the palace - its cream facade with grey slate roofs and turrets and a matte brass crown over the arched gate, with the keep's chess skyline behind it.
- If there is room: the apron's lamp posts and rank band at the bottom edge.
- CHECK: one paddle wheel, its axle across the course; a smooth cliff with no holds; the gate wall clearly taller than its leaves; no finish line, crown pedestal or walled finish court.

### 7. RCY_07_architecture_modules.png

Landscape 3:2 (1536x1024). Title "Royal Carriage Yard - Asset Breakdown / Architecture Modules" with the small ribbon "Stage 1 - Outer Castle".
- Sheet layout like reference 2: cream parchment background and the serif title at the top. Each module stands on its own with a numbered rounded caption label under it: "1. Name" plus one short line of words, with no sizes, units or other numbers.
- Soft warm studio light and soft contact shadows; no sky, scenery or dimension lines.
- One small cream pawn beside module 1 for scale only; no other characters.

Modules:
1. Outer gatehouse, court side: a flat-topped wall with rook merlons and a plain rectangular opening under a flat lintel (no arch; the lintel's face plain). The walnut leaves are closed, with three iron straps and matte brass crown plates. Two slender flat rook-crowned towers stand beside it, with the crest shield on top and a rank band at the threshold.
2. Rook tower family: a slender gate tower, a taller final-gate tower and a thicker corner tower, each three to four times as tall as wide, with a flared corbelled collar and a crenellated rook crown; no cones.
3. Curtain wall kit: a low wall and a tall wall with rook merlons, and a corner piece. The inner face is plain and flat; the outer face has a plinth, pilasters and arrow slits.
4. Granary shed bay: a slate lean-to on walnut posts with a crenel-cut oak fascia and a matte brass edge, an unlit lantern, and goods packed solid to the flat front. Beside it, the bay with the raised royal canopy and crest.
5. Sorting stall bay with its matte brass piece-icon plaque, and the cargo-line doorway bay: a dark doorway, iron jambs, a lintel and a pictogram board with a crate and an arrow.
6. Canal crossing piece: the stone landing edge with a low white marble railing and one walkway opening; a stone pier; a railing-less plank walkway on walnut posts ending in a bright gold jump line; a canal wall with its stone parapet and the water line.
7. Loft and hoist: the solid loft front of vertical oak boards, the plank deck's edge with its bright gold lip, and a high loft end with crate stacks; a timber hoist frame (post, arm with carved knight-head ends, slate hood, two pulleys) with one pallet down and one up.
8. Marble cliff section: a smooth white marble face with one sluice portal (rook-gate jambs, crown keystone, raised solid oak sluice board, gear wheel), the top railing ending at the landing gap, and the small oak landing with its bright gold edge.
9. Final gatehouse, platform side: a tall flat-topped wall with rook merlons, a rectangular opening under a flat lintel, and two flat rook-crowned towers. The walnut push leaves are closed, with four iron straps, matte brass crown plates and bright gold ring handles at a pawn's chest height. The iron progress-bar frame and a push pictogram sit over the opening, with the crest above.
10. Section end: a causeway piece with white marble railings, the open stone portal (rook-crowned piers, crown keystone), four shallow steps and the entrance band.
- CHECK: ten modules, each numbered once; no numbers but the item numbers; no cones; no arch over either gate opening.

### 8. RCY_08_props_materials.png

Landscape 3:2 (1536x1024). Title "Royal Carriage Yard - Asset Breakdown / Props & Materials" with the small ribbon "Stage 1 - Outer Castle".
- Sheet layout like references 1 and 3: cream parchment background and the serif title at the top. Each prop stands on its own with a numbered rounded caption label under it: "1. Name" plus one short line of words, with no sizes, units or other numbers.
- Small pale arrows may show how moving parts move.
- Soft warm studio light and soft contact shadows; no sky or scenery.
- One small cream pawn beside prop 1 for scale only.

Props:
1. Royal coach rig at rest, side view: a closed boxy coach drawn by a pair of horses side by side. The coach has a walnut chassis, a bottle-green lacquered cabin with wooden shutters and the crest on the door, a roof slab with a matte brass crown finial, an empty coachman's bench, four spoked wheels with iron tyres, and two lanterns glowing amber at the front corners. Beside it, the same coach exploded: chassis, cabin, roof and finial, bench and footboard, wheels, pole and traces, lanterns.
2. Carriage horses in bay, grey and black: chunky toy horses with arched necks, short upright manes and plain small dark eyes (no glint, blush or brows), walnut harness with a small matte brass knight badge on the brow band, and cream plumes. One standing, one tossing its head.
3. Goods: a single crate and a two-crate stack from the cargo line, granary crates, barrels with iron hoops, sacks on a pallet, straw bales and a load under tied sand canvas, all with small dark chess-piece stencils.
4. Timber: a long bundle of squared beams (two under one, iron straps, branded ends) on a dark rest board with iron chocks.
5. Timber crane parts, assembled: a stone drum with a carved rook band, a walnut mast, a small treadwheel house with a slate roof, a diagonal brace from the house roof up to the jib, the jib with a carved knight-head tip and its lamp, a crenellated iron counterweight under the short tail, and two slings holding the bundle low.
6. Row crane: a beam end on a carved knight-head stone corbel, the iron rail and trolley, and two ropes lifting a two-crate stack.
7. Hoist pallet: an oak plate with iron corner shoes and two sacks on a four-rope bridle under a pulley; the arm lamp shown three times - green, amber, red.
8. The king's coffer: an oak chest with a walnut lid, iron corners and matte brass straps, lock and crown, hanging on four chains from a trolley with a winch drum; a berth mark of dark boards with iron corner plates.
9. Waterwheel parts: a giant-wheel rim segment with light and dark bucket boards, a spoke pair with walnut ties and an iron band, and the hub with its matte brass crown boss; a paddle-wheel paddle with its bright gold handle bar.
10. Water pieces: the flume trough and the small rook-shaped cistern on a trestle; a cargo barge with a curled bow post carrying a small matte brass crest shield, loaded with crates, barrels and a covered load; the dock jib crane with a proper knee brace and a net of sacks.
11. Lamps, signs and heraldry: an iron lamp post with a bishop-mitre cap (unlit, open iron frame, no glass), a shed lantern, a cream banner with the crowned-knight crest, and a stone crest plaque. Also the pictogram boards, icons only: a crate with an outward arrow, a crate with an inward arrow, a pawn stepping onto a pallet under a green lamp, a crate hanging over a crossed-out pawn, and a pawn pushing a door.

Bottom row, labelled material swatches: cream limestone, flagstone, shade stone, white marble (no climb), roof slate, light oak, walnut, dark iron, matte brass, functional gold, sand canvas, canal water.
- CHECK: eleven props and twelve labelled swatches; the horses are plain horses, not chess pieces; no glass lamps; no numbers but the item numbers.

## Never

- Never change the play space:
  - no new obstacles, props, posts, bollards, curbs, steps, ramps, ledges, ladders or handholds on any floor, wall, cliff, stall, shed, store or loft face;
  - no railings on the walkways or the loft deck;
  - no bridge, stepping stones or reachable boat in the pond;
  - nothing hanging into the wagon yard, the giant wheel's holes, the hoist wells or the crane sweeps;
  - no open bays or gaps in the shed, stall and store fronts.
- Never a chessboard: no checker over a whole floor, no 8 x 8 board, no checker on cloth, wood or marble. The rank bands are the only floor checker.
- Never sky-blue, royal-blue, navy or orange decoration (the sky and the water keep their natural colours): no blue roofs, awnings, cloth, banners, plumes, coaches or lamps; no team banners, team gates or team colours anywhere.
- Never red, crimson or rust as decoration (no red coach, cloth, carpet, flag or flower). Never bright green outside the go cues, and never shiny gold outside the functional spots listed.
- Never motifs of the other stages: ceilings, chandeliers, wall sconces, wood panelling, veined marble, marble or wooden checker floors, parquet, bookcases, laundry, purple, velvet, ermine, skylights, statues, a palanquin, a king figure, canopy beds, cannons or cannon turrets, an iron or rubber conveyor belt.
- Never the next rooms' identities: no moat with a drawbridge, no capstans, no portcullis or barred gate, no runs of overhead chain (the coffer's four chains are the only chains), no painted chessboard, no paint pots.
- Never people or extra figures:
  - no humans, coachmen, porters, guards or crowds;
  - no chess piece other than the pawn players;
  - no statues, finials, balusters or carvings shaped like chess pieces at pawn size, and no knight-head carving below 6 m (flat crests and badges are fine);
  - no faces, eyes, brows or blush on buildings, carvings, machines or horses;
  - no pawns with scarves, neckerchiefs, coloured collar bands or team-colour accents.
- Never glass or glazing (shutters and open frames instead) and never flames; only the cue lamps and the coach lanterns glow.
- Never cone or pyramid roofs on the yard's towers: they end in rook crowns. Only the far palace facade keeps its slate turret roofs.
- Never text in the scenes: no words, letters, numbers, countdown digits or dimension lines; in-world signs are pictograms only. Text appears only in the titles, the ribbons, image 1's four pills and the sheets' caption labels, in English, with no sizes or units.
- Never real-world brands or logos, modern or sci-fi objects, gore, photorealism, a ruined or abandoned look, or a floating island.
- For checking only, never named in an image prompt: nothing from older versions of this yard. That means:
  - no carousel of coaches round a central post, no flowerbeds or partition walls in the yard, no crate-wall gate;
  - no stables or loose horses, no cranes dropping crates from hooks, no third row of crate stacks;
  - no side waterwheels, sorting plinths, hammering chests or ramps beside the cliff;
  - no walled finish court, checkered finish line or crown on a pedestal.

## Short version (any image tool)

Stylized 3D game concept art, portrait 2:3: a high bird's-eye view straight along the long, walled service yard of a cute toy chess kingdom's castle on a sunny day, entrance at the bottom, far end at the top. Chunky pale cream limestone walls with rook battlements, every round tower crowned like a chess rook, grey slate roofs, matte brass crests of a crowned knight's head, big rounded bevels. Bottom to top: a start court and an open walnut gate between rook towers; a paved wagon yard between slate-roofed sheds full of crates, barrels, sacks and hay, where five green and brown royal coaches, each drawn by two horses, charge about with red lanterns and translucent red floor stripes ahead; a turquoise canal whose two plank walkways run through a giant wooden waterwheel standing upright, facing the viewer; a wider timber warehouse with a cargo roller line, walls of crate stacks under beam cranes, two timber cranes swinging low bundles of squared beams over red floor fans, a plank loft with rope hoists and a chest hanging on chains over a red circle; a mill pond with an upright paddle wheel before a smooth white marble cliff and waterfall; a final walnut gate between rook towers; the palace far beyond. Tiny cream and charcoal chess-pawn characters race along it.

Negative prompt: blue roofs, blue banners or blue cloth, orange, red banners or red decoration, cone roofs on the towers, shiny gold trim, people, coachmen, chess statues, scarves, text, letters, numbers, logos, glass windows, modern objects, ruins, floating island, photorealism, props on the paths (Midjourney: put this list after --no and add --ar 2:3).
