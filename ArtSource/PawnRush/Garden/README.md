# Royal Water Garden Blender work file

`RoyalWaterGarden_v10.blend` is the editable Blender 5.2 working file, based on the v06 FBX in `seoungtest` (R263). It incorporates the raised approach and landing, mirrored four-step C routes that descend and rise, war-room masonry panels, and animated water and waterfall previews. Play frames 1–120 at 30 fps. The water uses procedural materials and animated mesh shape keys; no external texture files are required for the water.

This is **not** the original v06 `.blend`: it was reconstructed by importing the v06 FBX. The original Blender animation drivers for the spinner, pendulums, and ferries are not present. The Unity FBX and game code are unchanged: Blender materials and water animation do not automatically transfer to Unity. Before any Unity replacement, rebuild and test the moving pivots, water shader/animation, collision exclusions, elevated respawn volumes, and route dimensions. Unity and player testing remain outstanding.
