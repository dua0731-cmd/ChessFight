using System;
using System.Collections.Generic;
using ChessFight.Gameplay;
using UnityEngine;

namespace ChessFight.PawnRush
{
    // R5 the observing stage, the great telescope and the balcony (ArtSource/PawnRush/RoyalObservatory/PLAN.md;
    // ro_r5_telescope_model.py, the ro-hall session, 11-19): nine parts the user passed one by one, then put together
    // with the tube's glass ("투명한 경통 넣고"). Static models in the section's frame; the iris leaves ride their
    // devices. Walking faces and pivots stay the builder's - but for one exception the planning session agreed to (PLAN
    // 3): the fork mount's two arms are U-shaped now ("U자로 해서 판정도 모양에 맞추고"), so their colliders are the
    // convex pieces below, under the builder's "Fork arm W" / "Fork arm E", whose box colliders are switched off - only
    // when the fork's model has hung.
    public sealed partial class RoyalObservatoryDressing
    {
        static readonly Hang[] Telescope =
        {
            new Hang
            {
                zone = "Telescope", model = "RoStage", under = "4 Observing stage",
                covers = new[] { "Observing stage", "Observing stage brass rim", "Sun mark", "Sun rays", "Observing stage pillar", "Stage rails" },
            },
            new Hang { zone = "Telescope", model = "RoFork", under = "4 Observing stage", covers = new[] { "Fork arm W", "Fork arm E" } },
            new Hang
            {
                zone = "Telescope", model = "RoTube", under = "5 Great telescope",
                covers = new[] { "Objective ring", "Iris ring *", "Tube ring *", "Tube strut*", "Finder scope" },
            },
            new Hang { zone = "Telescope", model = "RoDeck", under = "5 Great telescope", covers = new[] { "Telescope deck" } },
            new Hang
            {
                zone = "Telescope", model = "RoBalcony", under = "6 Balcony",
                covers = new[] { "Balcony", "Balcony rail *", "Exit arch post *", "Exit arch lintel" },
            },
            Iris(1, "W"), Iris(1, "E"), Iris(2, "W"), Iris(2, "E"), Iris(3, "W"), Iris(3, "E"), Iris(4, "W"), Iris(4, "E"),
            // The flat warning bars across the deck stop drawing: the leaves' gold glows instead (IrisWarnings).
            new Hang { zone = "Telescope", model = "", under = "5 Great telescope", covers = new[] { "Iris 1 warning", "Iris 2 warning", "Iris 3 warning", "Iris 4 warning" } },
        };

        static Hang Iris(int n, string side)
        {
            string device = "Device_IrisLeaf " + n + " " + side;
            return new Hang { zone = "Telescope", model = "RoIrisLeaf", on = device, under = device, covers = new[] { "Leaf" } };
        }

        // While an iris warns (its "Iris N warning" object is on), both its leaves' gold glows ember red.
        // The tube's glass in the observatory's glass shader (RO_TubeGlass, Shaders/RO_Glass.shader - the user 11-20:
        // the glass skin looked odd, "좀 더 그래픽이 좋았으면"): the library's RO_Glass stays for every other glass.
        static readonly Action<RoyalObservatoryDressing> TubeGlass = dressing =>
        {
            var glass = Resources.Load<Material>(MaterialFolder + "RO_TubeGlass");
            if (glass == null) return;
            foreach (var model in dressing.FindAll("RoTube (model)"))
                foreach (var r in model.GetComponentsInChildren<Renderer>(true))
                {
                    var mats = r.sharedMaterials;
                    bool changed = false;
                    for (int i = 0; i < mats.Length; i++)
                        if (mats[i] != null && mats[i].name.StartsWith("RO_Glass", StringComparison.Ordinal)) { mats[i] = glass; changed = true; }
                    if (!changed) continue;
                    r.sharedMaterials = mats;
                    r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                }
        };

        static readonly Action<RoyalObservatoryDressing> IrisWarnings = dressing =>
        {
            for (int n = 1; n <= 4; n++)
            {
                var warnings = dressing.FindAll("Iris " + n + " warning");
                if (warnings.Count == 0) continue;
                foreach (var side in new[] { "W", "E" })
                    foreach (var device in dressing.FindAll("Device_IrisLeaf " + n + " " + side))
                    {
                        var model = device.Find("RoIrisLeaf (model)");
                        if (model == null) continue;
                        device.gameObject.AddComponent<DeviceWarningGlow>()
                              .Configure(warnings[0].gameObject, model.GetComponentsInChildren<Renderer>(true));
                    }
            }
        };

        // ---------------------------------------------------------------- LODs (the light pass, 11-19)

        // The balcony and the stage are seen close only on the way past them: beside each hung "<model> (model)" its
        // _LOD1 (about half) and _LOD2 (about a fifth) FBX hang too, and a LODGroup picks one by size on screen. The
        // last LOD stays to the end (never culled).
        static readonly string[] LodModels = { "RoBalcony", "RoStage" };
        public static float Lod1At = .45f, Lod2At = .18f;

        static readonly Action<RoyalObservatoryDressing> TelescopeLods = dressing =>
        {
            foreach (var model in LodModels)
            {
                var l1 = Resources.Load<GameObject>(ModelFolder + "Telescope/" + model + "_LOD1");
                var l2 = Resources.Load<GameObject>(ModelFolder + "Telescope/" + model + "_LOD2");
                if (l1 == null || l2 == null) continue;
                foreach (var pivot in dressing.FindAll(model + " (model)"))
                {
                    var lods = new List<Renderer[]> { pivot.GetComponentsInChildren<Renderer>(true) };
                    foreach (var fbx in new[] { l1, l2 })
                    {
                        var copy = Instantiate(fbx, pivot, false);
                        copy.name = fbx.name;
                        foreach (var c in copy.GetComponentsInChildren<Collider>(true)) Destroy(c);
                        dressing.PaintModel(copy);
                        lods.Add(copy.GetComponentsInChildren<Renderer>(true));
                    }
                    var group = pivot.gameObject.AddComponent<LODGroup>();
                    group.SetLODs(new[]
                    {
                        new LOD(Lod1At, lods[0]), new LOD(Lod2At, lods[1]), new LOD(0f, lods[2]),
                    });
                    group.RecalculateBounds();
                }
            }
        };

        // ---------------------------------------------------------------- the fork's colliders

        // The pieces, in the section's coordinates (ro_r5_telescope_model.py fork_colliders_cs): each an outline (x, y)
        // swept from z 95.4 to 97.4 (the builder's arm boxes' depth). Per arm: the blocker by the deck's edge (the old
        // box's lower part, 0.1 m off the deck and over the stage rail's end, so no new gap opens), the U from 45 deg,
        // the horn with its head, the leg.
        const float ForkZ0 = 95.4f, ForkZ1 = 97.4f;

        readonly struct Piece
        {
            public readonly string arm, name;
            public readonly Vector2[] outline;

            public Piece(string arm, string name, Vector2[] outline)
            {
                this.arm = arm;
                this.name = name;
                this.outline = outline;
            }
        }

        static readonly Piece[] ForkPieces =
        {
            new Piece("Fork arm W", "blocker", new[] { new Vector2(6.400f, 12.000f), new Vector2(4.800f, 12.000f), new Vector2(4.800f, 15.600f), new Vector2(6.400f, 15.600f) }),
            new Piece("Fork arm W", "U", new[] { new Vector2(6.172f, 12.857f), new Vector2(5.936f, 13.115f), new Vector2(5.723f, 13.392f), new Vector2(5.536f, 13.686f), new Vector2(5.375f, 13.995f), new Vector2(5.241f, 14.318f), new Vector2(5.136f, 14.651f), new Vector2(5.061f, 14.991f), new Vector2(5.015f, 15.337f), new Vector2(5.000f, 15.686f), new Vector2(4.150f, 15.686f), new Vector2(4.168f, 15.263f), new Vector2(4.224f, 14.844f), new Vector2(4.315f, 14.431f), new Vector2(4.442f, 14.027f), new Vector2(4.604f, 13.636f), new Vector2(4.800f, 13.261f), new Vector2(5.027f, 12.904f), new Vector2(5.285f, 12.568f), new Vector2(5.571f, 12.256f) }),
            new Piece("Fork arm W", "horn", new[] { new Vector2(5.000f, 15.686f), new Vector2(4.989f, 16.101f), new Vector2(4.993f, 16.518f), new Vector2(5.013f, 16.936f), new Vector2(5.048f, 17.355f), new Vector2(5.099f, 17.776f), new Vector2(5.166f, 18.199f), new Vector2(5.248f, 18.624f), new Vector2(5.347f, 19.050f), new Vector2(5.461f, 19.477f), new Vector2(5.592f, 19.906f), new Vector2(5.739f, 20.337f), new Vector2(5.901f, 20.769f), new Vector2(5.399f, 20.991f), new Vector2(5.194f, 20.558f), new Vector2(5.005f, 20.122f), new Vector2(4.833f, 19.686f), new Vector2(4.675f, 19.248f), new Vector2(4.534f, 18.808f), new Vector2(4.409f, 18.367f), new Vector2(4.300f, 17.924f), new Vector2(4.207f, 17.479f), new Vector2(4.131f, 17.033f), new Vector2(4.071f, 16.586f), new Vector2(4.027f, 16.136f), new Vector2(4.150f, 15.686f), new Vector2(6.310f, 20.880f), new Vector2(6.222f, 21.210f), new Vector2(5.980f, 21.452f), new Vector2(5.650f, 21.540f), new Vector2(5.320f, 21.452f), new Vector2(5.078f, 21.210f), new Vector2(4.990f, 20.880f), new Vector2(5.078f, 20.550f), new Vector2(5.320f, 20.308f), new Vector2(5.650f, 20.220f), new Vector2(5.980f, 20.308f), new Vector2(6.222f, 20.550f) }),
            new Piece("Fork arm W", "leg", new[] { new Vector2(5.570f, 12.400f), new Vector2(5.503f, 12.811f), new Vector2(5.443f, 13.221f), new Vector2(5.379f, 13.632f), new Vector2(5.311f, 14.043f), new Vector2(5.239f, 14.454f), new Vector2(5.163f, 14.864f), new Vector2(5.083f, 15.275f), new Vector2(5.000f, 15.686f), new Vector2(4.000f, 15.686f), new Vector2(4.044f, 15.275f), new Vector2(4.083f, 14.864f), new Vector2(4.115f, 14.454f), new Vector2(4.142f, 14.043f), new Vector2(4.160f, 13.632f), new Vector2(4.170f, 13.221f), new Vector2(4.166f, 12.811f), new Vector2(4.130f, 12.400f) }),
            new Piece("Fork arm E", "blocker", new[] { new Vector2(11.600f, 12.000f), new Vector2(13.200f, 12.000f), new Vector2(13.200f, 15.600f), new Vector2(11.600f, 15.600f) }),
            new Piece("Fork arm E", "U", new[] { new Vector2(11.828f, 12.857f), new Vector2(12.064f, 13.115f), new Vector2(12.277f, 13.392f), new Vector2(12.464f, 13.686f), new Vector2(12.625f, 13.995f), new Vector2(12.759f, 14.318f), new Vector2(12.864f, 14.651f), new Vector2(12.939f, 14.991f), new Vector2(12.985f, 15.337f), new Vector2(13.000f, 15.686f), new Vector2(13.850f, 15.686f), new Vector2(13.832f, 15.263f), new Vector2(13.776f, 14.844f), new Vector2(13.685f, 14.431f), new Vector2(13.558f, 14.027f), new Vector2(13.396f, 13.636f), new Vector2(13.200f, 13.261f), new Vector2(12.973f, 12.904f), new Vector2(12.715f, 12.568f), new Vector2(12.429f, 12.256f) }),
            new Piece("Fork arm E", "horn", new[] { new Vector2(13.000f, 15.686f), new Vector2(13.011f, 16.101f), new Vector2(13.007f, 16.518f), new Vector2(12.987f, 16.936f), new Vector2(12.952f, 17.355f), new Vector2(12.901f, 17.776f), new Vector2(12.834f, 18.199f), new Vector2(12.752f, 18.624f), new Vector2(12.653f, 19.050f), new Vector2(12.539f, 19.477f), new Vector2(12.408f, 19.906f), new Vector2(12.261f, 20.337f), new Vector2(12.099f, 20.769f), new Vector2(12.601f, 20.991f), new Vector2(12.806f, 20.558f), new Vector2(12.995f, 20.122f), new Vector2(13.167f, 19.686f), new Vector2(13.325f, 19.248f), new Vector2(13.466f, 18.808f), new Vector2(13.591f, 18.367f), new Vector2(13.700f, 17.924f), new Vector2(13.793f, 17.479f), new Vector2(13.869f, 17.033f), new Vector2(13.929f, 16.586f), new Vector2(13.973f, 16.136f), new Vector2(13.850f, 15.686f), new Vector2(13.010f, 20.880f), new Vector2(12.922f, 21.210f), new Vector2(12.680f, 21.452f), new Vector2(12.350f, 21.540f), new Vector2(12.020f, 21.452f), new Vector2(11.778f, 21.210f), new Vector2(11.690f, 20.880f), new Vector2(11.778f, 20.550f), new Vector2(12.020f, 20.308f), new Vector2(12.350f, 20.220f), new Vector2(12.680f, 20.308f), new Vector2(12.922f, 20.550f) }),
            new Piece("Fork arm E", "leg", new[] { new Vector2(12.430f, 12.400f), new Vector2(12.497f, 12.811f), new Vector2(12.557f, 13.221f), new Vector2(12.621f, 13.632f), new Vector2(12.689f, 14.043f), new Vector2(12.761f, 14.454f), new Vector2(12.837f, 14.864f), new Vector2(12.917f, 15.275f), new Vector2(13.000f, 15.686f), new Vector2(14.000f, 15.686f), new Vector2(13.956f, 15.275f), new Vector2(13.917f, 14.864f), new Vector2(13.885f, 14.454f), new Vector2(13.858f, 14.043f), new Vector2(13.840f, 13.632f), new Vector2(13.830f, 13.221f), new Vector2(13.834f, 12.811f), new Vector2(13.870f, 12.400f) }),
        };

        static readonly Action<RoyalObservatoryDressing> ForkColliders = dressing =>
        {
            // Only when the U-shaped model hangs: otherwise the builder's boxes stay as they are.
            if (dressing.FindAll("RoFork (model)").Count == 0) return;
            var content = dressing.transform;
            var done = new HashSet<Transform>();
            foreach (var piece in ForkPieces)
            {
                foreach (var arm in dressing.FindAll(piece.arm))
                {
                    var go = new GameObject("U collider " + piece.name);
                    go.layer = arm.gameObject.layer;
                    go.transform.SetParent(arm, false);
                    go.transform.SetPositionAndRotation(content.position, content.rotation);
                    var a = arm.lossyScale;
                    var c = content.lossyScale;
                    go.transform.localScale = new Vector3(c.x / Safe(a.x), c.y / Safe(a.y), c.z / Safe(a.z));
                    var mc = go.AddComponent<MeshCollider>();
                    mc.sharedMesh = Prism(piece.outline, ForkZ0, ForkZ1);
                    mc.convex = true;
                    go.AddComponent<NoClimbSurface>();
                    if (done.Add(arm) && arm.TryGetComponent<BoxCollider>(out var box)) box.enabled = false;
                }
            }
        };

        static Mesh Prism(Vector2[] outline, float z0, float z1)
        {
            int n = outline.Length;
            var v = new Vector3[n * 2];
            for (int i = 0; i < n; i++)
            {
                v[i] = new Vector3(outline[i].x, outline[i].y, z0);
                v[n + i] = new Vector3(outline[i].x, outline[i].y, z1);
            }
            var t = new List<int>();
            for (int i = 1; i < n - 1; i++)
            {
                t.Add(0); t.Add(i); t.Add(i + 1);
                t.Add(n); t.Add(n + i + 1); t.Add(n + i);
            }
            for (int i = 0; i < n; i++)
            {
                int j = (i + 1) % n;
                t.Add(i); t.Add(n + i); t.Add(j);
                t.Add(j); t.Add(n + i); t.Add(n + j);
            }
            var mesh = new Mesh { name = "Fork collider" };
            mesh.vertices = v;
            mesh.triangles = t.ToArray();
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
