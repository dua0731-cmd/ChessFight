# Conservative garden and hall mesh pass (2026-11-13)

User scope: preserve appearance, optimize garden and hall, leave war room for later.
Do not commit or push this work without a later user instruction.

The final result and actual Unity before/after images are in:
Docs/PawnRush/COURSE_OPTIMIZATION_2026-11-13.md

## Reproduce candidates

Use Blender 5.2.2 LTS. Keep the output directory outside Assets.
Set CHESSFIGHT_ROOT to the root containing the ORIGINAL Assets directory, not an already optimized copy.
On this machine that original input is:
C:/Work/ChessFight/Temp/ModelOptimization20261113/original

Run:
blender --background --factory-startup --python course_mesh_pass.py -- <separate-output-directory>
blender --background --factory-startup --python validate_sources.py -- <same-output-directory>
blender --background --factory-startup --python image_metrics.py -- <same-output-directory>

MESH_PASS_ONLY is an optional comma-separated list of keys.
The script writes candidates and screenshots; it never automatically replaces Assets.
Applying the pass repeatedly to already optimized input is not supported.

The knight/pad use protected collapse (material borders, open borders and hard edges plus an adjacent ring).
Architectural models use 0.05-degree planar dissolve, delimited by material, UV and normal boundaries.
Objects below 1,000 triangles are left alone; only flower/planter objects are considered in the plaza.
The flower mesh cannot safely shrink under these restrictions and is unchanged.
The tree mesh is unchanged. Direct reduction and a Unity Mesh LOD trial were rejected after visual comparison.
Native tree LOD settings were restored: generateMeshLods=false, maximumMeshLod=-1.

The Unity JSON files record active LOD0 geometry and collider-bound signatures in isolated preview scenes.
The images are static preview renders, not a performance benchmark or human validation.
unity_visual_metrics.json includes a REJECTED native tree LOD0/1 far comparison.
No C# runtime/scene/prefab/material/texture changes were made by this pass.

Restore from the original input directory by copying the individual FBX and its meta to the same relative Assets path.
Existing GUIDs must be retained.
