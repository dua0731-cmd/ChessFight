# Unity garden v21 (R277)

R277 replaces the v20 export with `RoyalWaterGarden_v21.fbx` and `RoyalWaterGarden_v21_Motion.json` (444 meshes, same 15 tracks); zone version 12. Rebuild: `blender --background --python build_v21.py` (v20 → v21 .blend), then `export_unity_v21.py` and `check_unity_export_v21.py`. Collider roles: the decorative plaza and repaired pavement are `none`; `V21 | plaza collision proxy` is `proxy` (MeshCollider, renderer disabled in Unity); entry piers/coping/posts are `box`; trees are decoration only. Unity clay applies to `V1*`, `V20*` and `V21*` except the trees, which keep their trunk/leaf material colours. Unity was not connected: rendering, collision feel and performance remain unverified.

The v20 notes below are historical; the v20 FBX/motion files were removed from Resources (the v20 `.blend` remains).

## v20 (R276)

Pull `seoungtest` including Git LFS files. Open `Assets/Scenes/SeoungTest/SeoungTest_PawnRush.unity`, run **ChessFight > SeoungTest > Build Pawn Rush Test Scene**, then Play. Zone G is the royal garden beside the library; `]` selects the next zone. Zone version 11 also rebuilds an older saved layout on Play.

The course loads `Resources/PawnRushGarden/RoyalWaterGarden_v20.fbx` and `RoyalWaterGarden_v20_Motion.json`: 418 visible mesh objects and 15 sampled motion tracks, 721 samples at 30 Hz over a 24-second loop. `SampledGardenMotion` interpolates poses from `ObstacleClock.Now` in the physics step. Kinematic moving roots expose `IMovingSurface` point velocity and delta rotation for riders. Source rest poses and references are serialized, so an editor-built scene retains the setup after reload. Fixed pillars and decorative meshes do not receive animation.

Blender (x,y,z) maps to zone (x,z,y), using the existing 180-degree model pivot. Sampled rotation is applied as a reflected world-space delta to the FBX rest pose, preserving imported mesh-axis transforms. The supplied assets and derived structures use untextured clay; water/falls use the existing URP water and garden waterfall shaders. Ground modules get box colliders; the sloped ramp, open arch, plaza, rotor and moving detailed models use mesh colliders. The upper reservoir fall trigger follows the raised water level.

The Blender sky, lighting/compositor finish, distant mountains/clouds and curve-only foam strands are not exported into the shared multi-zone test scene. Unity uses the test scene's lighting and sky; water geometry is static and its shader animates the surface. This is a Unity view/play-test integration, not final collision or performance tuning. Test boarding, rotor/arm contacts, robot mechanical clearances and frame rate in Unity before treating this as gameplay-ready.

Checks: Core 133 passed (.NET 10); Blender FBX reimport recovered all 418 mesh IDs and 15 motion IDs, normalized quaternions, continuous loop endpoints. Non-water bounds differ by at most about 0.000009 m; water differs by up to 0.1 m because Blender morph waves are replaced by Unity shader animation. Shared-mesh FBX material-slot warnings are covered by the explicit Unity clay override. No Unity Editor was connected here: Unity C#/shader compilation, scene import, rendering, player interaction and Steam tests remain unverified.

Re-export with Blender 5.2: `blender --background --python ArtSource/PawnRush/Garden/export_unity_v20.py`; run `check_unity_export.py` with the same method for the FBX/data audit. Do not enable imported FBX clips over the sampled motion driver.
