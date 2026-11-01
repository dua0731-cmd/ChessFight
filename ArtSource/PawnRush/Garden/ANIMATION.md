# Garden animation study (R275)

Open `RoyalWaterGarden_v20.blend` in Blender 5.2 and press Space in the timeline. Frames 1–720, 30 fps, form a 24-second review loop. Frame 1 is the saved starting view.

| Section | Motion |
|---|---|
| Plaza | Separated raised spinner arms turn clockwise from above, one turn every 8 seconds; floor and flower beds stay fixed. Three integrated pawn props were removed and their pavement repaired. |
| C platforms | Each mirrored pair rises 0–0.7 m on a 6-second cycle, with successive pads phase shifted. The original C layout and top slopes remain. |
| Robot obstacles | Two shoulder/elbow assemblies use repeating target poses with holds, on a 6-second cycle. Support pillars remain fixed. |
| Team boats | Separate lanes x ±6 m; y 38–45 m on a 12-second shuttle. They start at opposite ends and pass in the middle. |

The final-section lamp/candle-stand mesh (`V04_후반_가로등`) was removed. Water mesh/material previews repeat across the review range. Spinner/platform/boat drivers and robot keyframes are Blender animation, not Unity gameplay implementations. Unity export/baking, pivot axes, rider transport, collision, contact heights, and mechanical clearances still need implementation/testing before play. The data report samples transforms; it does not establish gameplay safety.
