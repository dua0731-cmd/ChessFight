# Royal Water Garden Blender work file

R269: `RoyalWaterGarden_v14.blend` adds two distant mountain ridges, four rounded cloud banks and a camera-visible sky gradient. Mountains stay outside the course; the established foreground lighting, player camera and layout are preserved. Source camera background is an art study, not a Unity sky implementation. See `plaza_backdrop_v14.png`. Unity transfer, performance and player review remain pending.

R268: `RoyalWaterGarden_v13.blend` refines the plaza's 20 low wall segments and four planter bases with staggered limestone blocks, recessed joints, rounded edges and projecting coping. Five warm neutral stone variations follow the war-room palette. Previous wall meshes are hidden in Blender and retained for reference. This is a visual art revision; Unity geometry/collision remain unchanged. See `masonry_detail_v13.png`.

R267: `RoyalWaterGarden_v12.blend` refines the plaza flower beds. Rounded six-petal flowers, pollen centres, stems and individual leaves replace the original flat plaza flower pieces. Pink, yellow, ivory, lilac and coral variations share the v11 camera and lighting for comparison. This remains a Blender art pass; Unity transfer and performance testing are pending.

R266: `RoyalWaterGarden_v11.blend` and `plaza_player_v11.png` are the first plaza art benchmark pass. Added staggered radial sandstone tiles with bevels and colour variation, refined metal roughness, warm sunlight/sky fill, and a perspective camera at 2.5 m. Existing terrain and water remain. This is a Blender art study; fine flowers, wall dressing, background art, and Unity transfer are still pending. The reference look is not yet achieved.

`RoyalWaterGarden_v10.blend` is the editable Blender 5.2 working file, based on the v06 FBX in `seoungtest` (R263). It incorporates the raised approach and landing, mirrored four-step C routes that descend and rise, war-room masonry panels, and animated water and waterfall previews. Play frames 1–120 at 30 fps. The water uses procedural materials and animated mesh shape keys; no external texture files are required for the water.

This is **not** the original v06 `.blend`: it was reconstructed by importing the v06 FBX. The original Blender animation drivers for the spinner, pendulums, and ferries are not present. The Unity FBX and game code are unchanged: Blender materials and water animation do not automatically transfer to Unity. Before any Unity replacement, rebuild and test the moving pivots, water shader/animation, collision exclusions, elevated respawn volumes, and route dimensions. Unity and player testing remain outstanding.

R265 adds a separate Unity water implementation on the existing v06 FBX: `GardenWaterLook` replaces G09/G10 material slots, using the existing URP water shader and a new world-space waterfall shader. Rebuild the test scene (version 10). This does not transfer the Blender layout, raised terrain or wall changes. Unity compilation and visual testing are pending.
