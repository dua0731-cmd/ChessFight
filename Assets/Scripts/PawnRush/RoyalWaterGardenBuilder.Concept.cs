using System.Collections.Generic;
using UnityEngine;

namespace ChessFight.PawnRush
{
    // R412 (승규 11-05, "배치는 지금 대로 하고 컨셉 아트만 저렇게 입힐 거야"): the concept-art dressing laid over the v31
    // graybox, one beat at a time. Every position and collider stays the graybox's; the dressing draws only.
    //
    // Source: ArtSource/PawnRush/GardenConcept (a beat's *_sample_v*.py on gc_kit.py -> export_concept.py), exported to
    // Resources/PawnRushGardenConcept: one FBX per beat, Materials/GC_*.mat (one per Blender material, by name, shared).
    // Each FBX hangs under a pivot turned 180 degrees about y, as the garden's own FBX did (R276): Blender x, y, z =
    // section x, z, y.
    //
    //   GardenPlaza     beat 1 (R412-R420): the plaza, the start yard, the five spin bars (riding their movers)
    //   GardenCanal     beat 2 (R424, R426): the canal walk, the plaza's north apron, the ramp, the west wall, the
    //                   geysers as floor fountains (their water computed: GardenJet)
    //   GardenPlatforms beat 3 (R431): the west landing; P1..P8 as floating tubs, K1..K3, K4's spin bar and L1, L2
    //                   riding their movers; the platforms' piers gone (R430: the graybox stems too)
    //   GardenLockDyke  beats 4, 5 (R433): the east landing, the lock (bed, walls, gate frames; Sluice gate 1..3 riding
    //                   their movers), the dyke, the two robots (Robot pillar 1, 2 still; Robot arm 1, 2 on the arms)
    //   GardenBoats     beat 6 (R437): the boarding quay, the reservoir's south wall, the two boathouse tunnels, the
    //                   lane ropes; Boat 1-1 .. 4-2 riding the boats (R446: royal boats after 승규's photo, royal_boat.py)
    //   GardenGate      beat 7 (R462): the gate yard, the exit terrace and its medallion, 승규's reference gate at the
    //                   exit (gate_sample_v2.py), the steps and the band; R474: the north wall gone, the yard's north edge
    //                   a balustrade, the terraces dressed with the walls; R483: the exit gate 승규's Tripo gate
    //                   (gate_arch_asset.py, no flags or banners), its wing planters colliding ("Exit gate wing planter *")
    //   GardenWalls     the castle ring (R474, walls_sample_v2.py): twelve towers (R481: 승규's tower asset, tower_asset.py),
    //                   crenellated walls with big arches, cracked and chipped; the banks, the strip by the gate, the
    //                   terraces cut by the ring and the lawn round the castle
    //   GardenWater     the water in tiers (R440): the moat and the lake as PR_GardenWater sheets, the spillway and the
    //                   weirs, their falling water in the fall shader (PR_GardenFallUpper / Lower); the landing lines
    //                   for the water shader (GardenFallLines)
    //   GardenWatergate the east channel's water gate (R485, watergate_sample_v1.py: 승규's castle gate, gate_asset.py)
    //                   on its footings, and the lake's royal boats in their own colours (R486), which
    //                   GardenLakeBoat sails round the lake's wide waters (R487: four, two on each of two routes)
    //   GardenTrees     trees off the course (R491, trees_sample.py: 승규's two Tripo trees painted by tree_asset.py), in
    //                   groves on the reservoir terraces (3.8) and the waterside banks (0.6)
    public static partial class RoyalWaterGardenBuilder
    {
        const string ConceptFolder = "PawnRushGardenConcept/";
        const string ConceptMaterials = ConceptFolder + "Materials/";

        // A beat's dressing: its FBX; the graybox pieces it stands in for (drawn no more, still colliding); the moving
        // pieces whose dressing rides them (the FBX node of the same name is hung on the mover, which stops drawing its
        // own parts); and graybox pieces taken away outright (drawing and colliding). Names by group; a star in a name
        // stands for any run of characters.
        sealed class ConceptPiece
        {
            public string model;
            public (string group, string name)[] covered = new (string, string)[0];
            public (string group, string name)[] hung = new (string, string)[0];
            public (string group, string name)[] removed = new (string, string)[0];
        }

        static readonly ConceptPiece[] ConceptPieces =
        {
            new ConceptPiece
            {
                model = "GardenPlaza",
                covered = new[]
                {
                    ("B1_FountainPlaza", "Start yard"), ("B1_FountainPlaza", "Start yard rail W"), ("B1_FountainPlaza", "Start yard rail E"),
                    ("B1_FountainPlaza", "Fountain plaza"), ("B1_FountainPlaza", "Plaza kerb*"),
                },
                hung = new[]
                {
                    ("B1_FountainPlaza", "Spin bar 1"), ("B1_FountainPlaza", "Spin bar 2"), ("B1_FountainPlaza", "Spin bar 3"),
                    ("B1_FountainPlaza", "Spin bar 4"), ("B1_FountainPlaza", "Spin bar 5"),
                },
            },
            new ConceptPiece
            {
                model = "GardenCanal",
                covered = new[]
                {
                    ("B1_FountainPlaza", "Plaza north apron"),
                    ("B2_GeyserCanal", "Canal walk"), ("B2_GeyserCanal", "Canal walk rail S"), ("B2_GeyserCanal", "Canal walk rail N"),
                    ("B2_GeyserCanal", "Canal walk rail E"), ("B2_GeyserCanal", "Garden wall W (canal)"), ("B2_GeyserCanal", "West ramp"),
                    ("B2_GeyserCanal", "West ramp rail E"), ("B2_GeyserCanal", "Geyser rim"), ("B2_GeyserCanal", "Geyser grate"),
                },
            },
            new ConceptPiece
            {
                model = "GardenPlatforms",
                covered = new[]
                {
                    ("B3_Platforms", "West landing"), ("B3_Platforms", "West landing rail N"), ("B3_Platforms", "West landing rail S"),
                    ("B3_Shortcut", "Shortcut post K*"), ("B3_Shortcut", "Shortcut lotus L* stem"),
                },
                hung = new[]
                {
                    ("B3_Platforms", "Bobbing platform P1"), ("B3_Platforms", "Bobbing platform P2"), ("B3_Platforms", "Bobbing platform P3"),
                    ("B3_Platforms", "Bobbing platform P4"), ("B3_Platforms", "Bobbing platform P5"), ("B3_Platforms", "Bobbing platform P6"),
                    ("B3_Platforms", "Bobbing platform P7"), ("B3_Platforms", "Bobbing platform P8"),
                    ("B3_Shortcut", "Shortcut post K1"), ("B3_Shortcut", "Shortcut post K2"), ("B3_Shortcut", "Shortcut post K3"),
                    ("B3_Shortcut", "Shortcut post K4 spin bar"), ("B3_Shortcut", "Shortcut lotus L1"), ("B3_Shortcut", "Shortcut lotus L2"),
                },
                // R430 (승규 "기둥 없애주고"): the tubs float; their piers would only be unseen posts under them.
                removed = new[] { ("B3_Platforms", "Bobbing platform P* stem") },
            },
            // R433: the east lock and the robot dyke. The lock's glass water and current arrows stay (the channel bed
            // shows through them). The robots are 승규's ball-and-chain: the pillar (drawn wide over the gantry's two
            // posts, which still collide), its bracket and the hub drum stand still; the handle and ball ride the arm.
            new ConceptPiece
            {
                model = "GardenLockDyke",
                covered = new[]
                {
                    ("B4_EastLock", "East landing"), ("B4_EastLock", "East landing rail S"), ("B4_EastLock", "East landing rail N"),
                    ("B4_EastLock", "Lock floor"), ("B4_EastLock", "Lock wall W"), ("B4_EastLock", "Lock wall E"),
                    ("B4_EastLock", "Lock gate post *"), ("B4_EastLock", "Lock gate beam *"),
                    ("B5_RobotDyke", "Robot dyke"), ("B5_RobotDyke", "Dyke wall N"),
                    ("B5_RobotDyke", "Robot gantry *"), ("B5_RobotDyke", "Robot axle *"), ("B5_RobotDyke", "Robot pillar block *"),
                },
                hung = new[]
                {
                    ("B4_EastLock", "Sluice gate 1"), ("B4_EastLock", "Sluice gate 2"), ("B4_EastLock", "Sluice gate 3"),
                    ("B5_RobotDyke", "Robot arm 1"), ("B5_RobotDyke", "Robot arm 2"),
                },
                // R435: the robot's pillar and axle stand east of the swing; the gantry's west post and beam would be
                // unseen walls (the east ones are inside the pillar and under the axle).
                removed = new[] { ("B5_RobotDyke", "Robot gantry post W *"), ("B5_RobotDyke", "Robot gantry beam W *") },
            },
            // R437: the boat crossing. The tunnels' glass falls and the reservoir's water stay; the gondolas ride the
            // boats (under B6_BoatCrossing/Boats).
            new ConceptPiece
            {
                model = "GardenBoats",
                covered = new[]
                {
                    ("B6_BoatCrossing", "Boarding quay"), ("B6_BoatCrossing", "Boarding quay rail W"),
                    ("B6_BoatCrossing", "Boarding quay rail S"), ("B6_BoatCrossing", "Reservoir wall S*"),
                    ("B6_BoatCrossing", "Boat tunnel roof"), ("B6_BoatCrossing", "Boat tunnel eave"),
                    ("B6_BoatCrossing", "Boat tunnel wall"),
                },
                hung = new[]
                {
                    ("B6_BoatCrossing/Boats", "Boat 1-1"), ("B6_BoatCrossing/Boats", "Boat 1-2"),
                    ("B6_BoatCrossing/Boats", "Boat 2-1"), ("B6_BoatCrossing/Boats", "Boat 2-2"),
                    ("B6_BoatCrossing/Boats", "Boat 3-1"), ("B6_BoatCrossing/Boats", "Boat 3-2"),
                    ("B6_BoatCrossing/Boats", "Boat 4-1"), ("B6_BoatCrossing/Boats", "Boat 4-2"),
                },
            },
            // R462: the gate and the way out (gate_sample_v2.py): the yard, the exit terrace with its finish medallion (the
            // CP marker's picture), 승규's reference gate at the exit (its jambs, haunches, towers and crown), the steps and
            // the band. R474: the north wall by the opening is gone (the castle ring takes the gate in), the yard's north
            // edge is a balustrade, the wings (vine wall, urn pillar, end block) collide; the terraces went to GardenWalls.
            new ConceptPiece
            {
                model = "GardenGate",
                covered = new[]
                {
                    ("B7_Gate", "Gate yard"), ("B7_Gate", "Gate yard rail W"), ("B7_Gate", "Gate yard rail E"),
                    ("B7_Gate", "Gate yard rail N *"),
                    ("B7_Gate", "Exit terrace"), ("B7_Gate", "Exit terrace (CP 끝)"),
                    ("B7_Gate", "Exit terrace rail W"), ("B7_Gate", "Exit terrace rail E"),
                    ("B7_Gate", "Exit arch pillar *"), ("B7_Gate", "Exit arch haunch *"), ("B7_Gate", "Exit gate tower *"),
                    ("B7_Gate", "Exit gate wing *"), ("B7_Gate", "Exit gate urn pillar *"), ("B7_Gate", "Exit gate end block *"),
                    ("B7_Gate", "Exit arch lintel"), ("B7_Gate", "Step down *"), ("B7_Gate", "Mini-game entrance band"),
                    ("B7_Gate", "Band rail W"), ("B7_Gate", "Band rail E"),
                },
            },
            // R474: the castle ring round the garden (walls_sample_v2.py): its walls and towers, the banks along the sides,
            // the strip by the gate, the four terraces (the gate's cut by the ring) and the lawn outside, which has no
            // graybox.
            new ConceptPiece
            {
                model = "GardenWalls",
                covered = new[]
                {
                    ("Garden", "Castle wall *"), ("Garden", "Castle tower *"), ("Garden", "Castle bank *"),
                    ("Garden", "Castle strip *"), ("Garden", "Garden terrace *"),
                },
            },
            // R440: the water in three pools (R441: the moat -1.9, the lake -0.55; the reservoir is B6's picture at 3.0), the
            // spillway and the weirs with their falling water (water_tiers.py). Nothing to cover: the old sheet is gone
            // (Water); the landing lines go to the water shader (FallLines).
            new ConceptPiece { model = "GardenWater" },
            // R485: the east channel's water gate (watergate_sample_v1.py: 승규's castle gate asset, gate_asset.py, on two
            // footings); R486: the lake's three boats (LakeBoats). Out of reach: nothing covers or collides.
            new ConceptPiece { model = "GardenWatergate" },
            // R491: 승규's two trees (tree_asset.py, trees_sample.py) in groves on the reservoir terraces and the
            // waterside banks, off the course. Out of reach: nothing covers or collides.
            new ConceptPiece { model = "GardenTrees" },
        };

        // R440: Blender materials drawn with the garden's own shaders rather than a GC_ material of the same name.
        static readonly Dictionary<string, string> SharedMaterials = new Dictionary<string, string>
        {
            { "GC_basin_water", "PR_GardenWaterShallow" },   // R426: the fountains' basins (the mosaic shows through)
            { "GC_tier_water", "PR_GardenWater" },            // R440: the pools
            { "GC_fall_upper", "PR_GardenFallUpper" },        // R440: the spillway's falls (R441: 3.45 m)
            { "GC_fall_lower", "PR_GardenFallLower" },        // R440: the weirs' falls (R441: 1.31 m)
        };

        static void ConceptDressing(Transform content)
        {
            var pivot = new GameObject("Concept dressing").transform;
            pivot.SetParent(content, false);
            pivot.localRotation = Quaternion.Euler(0f, 180f, 0f);
            var cache = new Dictionary<string, Material>();
            foreach (var piece in ConceptPieces)
            {
                var model = Resources.Load<GameObject>(ConceptFolder + piece.model);
                if (model == null)
                {
                    Debug.LogWarning("[정원] 컨셉 장식을 불러오지 못했습니다: Resources/" + ConceptFolder + piece.model + ".fbx");
                    continue;
                }
                var dress = Object.Instantiate(model, pivot, false);
                dress.name = piece.model;
                foreach (var renderer in dress.GetComponentsInChildren<Renderer>(true))
                {
                    var slots = renderer.sharedMaterials;
                    for (int i = 0; i < slots.Length; i++)
                        if (slots[i] != null) slots[i] = ConceptMaterial(slots[i], cache);
                    renderer.sharedMaterials = slots;
                    // Water casts no shadow: the jets, the lily pads, the pools and the falls (R440).
                    string n = renderer.name;
                    bool water = n.Contains("jets") || n.Contains("lily") || n.StartsWith("Water ") || n.StartsWith("Fall ");
                    renderer.shadowCastingMode = water ? UnityEngine.Rendering.ShadowCastingMode.Off : UnityEngine.Rendering.ShadowCastingMode.On;
                }
                Cover(content, piece.covered);
                Remove(content, piece.removed);
                Hang(content, dress.transform, piece.hung);
                if (piece.model == "GardenCanal") RefitGeysers(content.Find("B2_GeyserCanal"));
                if (piece.model == "GardenWater") FallLines(content);
                if (piece.model == "GardenWatergate") LakeBoats(content, dress.transform);
            }
        }

        // R485 (승규 "보트 애니메이션을 둘거야 ... 그 구간에서 배가 떠 다니는 애니메이션 만들어줘", "성벽 안 수로 수문"); R486
        // ("총 3개가 나올 수 있게 하고 각자 택스쳐가 다르게 해줘 중반 구간 물 쪽 전체를 누릴 수 있게"): each of the lake's
        // three boats rides a pivot in the section's frame, put where the FBX left it (its route's start, bow north),
        // which GardenLakeBoat then moves along its route.
        static void LakeBoats(Transform content, Transform dress)
        {
            for (int i = 0; i < GardenLakeBoat.Names.Length; i++)
            {
                string name = "Lake boat " + GardenLakeBoat.Names[i];
                var node = dress.Find(name);
                if (node == null)
                {
                    Debug.LogWarning("[정원] 호수 보트가 없습니다: GardenWatergate/" + name);
                    continue;
                }
                var ride = new GameObject(name + " ride").transform;
                ride.SetParent(content, false);
                ride.localPosition = GardenLakeBoat.Rest(i, MiddleWaterY);
                ride.localRotation = Quaternion.identity;
                node.SetParent(ride, true);
                ride.gameObject.AddComponent<GardenLakeBoat>().Configure(i, MiddleWaterY);
            }
        }

        // R440: where each bay's falling water lands and the pool it lands in (written by export_water.py from
        // water_tiers.py), for the water shader's churn and rings (GardenFallLines on the content, section frame).
        [System.Serializable] sealed class LandingFile { public float early, middle, late; public LandingLine[] lines; }
        [System.Serializable] sealed class LandingLine { public string fall; public float x0, z0, x1, z1, y, strength; }

        static void FallLines(Transform content)
        {
            var text = Resources.Load<TextAsset>(ConceptFolder + "GardenWater_Landings");
            var file = text != null ? JsonUtility.FromJson<LandingFile>(text.text) : null;
            if (file == null || file.lines == null || file.lines.Length == 0)
            {
                Debug.LogWarning("[정원] 폭포 착수선이 없습니다: Resources/" + ConceptFolder + "GardenWater_Landings.json");
                return;
            }
            if (!Mathf.Approximately(file.early, EarlyWaterY) || !Mathf.Approximately(file.middle, MiddleWaterY) || !Mathf.Approximately(file.late, ReservoirY))
                Debug.LogWarning("[정원] 물 높이가 water_tiers.py와 다릅니다(빌더 " + EarlyWaterY + " / " + MiddleWaterY + " / " + ReservoirY + ")");
            int count = file.lines.Length;
            var from = new Vector3[count];
            var to = new Vector3[count];
            var strength = new float[count];
            for (int k = 0; k < count; k++)
            {
                var line = file.lines[k];
                from[k] = new Vector3(line.x0, line.y, line.z0);
                to[k] = new Vector3(line.x1, line.y, line.z1);
                strength[k] = line.strength;
            }
            var lines = content.GetComponent<GardenFallLines>();
            if (lines == null) lines = content.gameObject.AddComponent<GardenFallLines>();
            lines.Configure(from, to, strength);
        }

        static bool Matches(string name, string pattern)
        {
            int star = pattern.IndexOf('*');
            if (star < 0) return name == pattern;
            string head = pattern.Substring(0, star), tail = pattern.Substring(star + 1);
            return name.Length >= head.Length + tail.Length && name.StartsWith(head) && name.EndsWith(tail);
        }

        static List<Transform> Matching(Transform content, string group, string pattern)
        {
            var found = new List<Transform>();
            var root = content.Find(group);
            if (root == null) return found;
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
                if (t != root && Matches(t.name, pattern)) found.Add(t);
            return found;
        }

        // The graybox pieces under a dressing stop drawing; their colliders stay.
        static void Cover(Transform content, (string group, string name)[] covered)
        {
            foreach (var (group, name) in covered)
                foreach (var t in Matching(content, group, name))
                    if (t.TryGetComponent<Renderer>(out var drawn)) drawn.enabled = false;
        }

        static void Remove(Transform content, (string group, string name)[] removed)
        {
            foreach (var (group, name) in removed)
                foreach (var t in Matching(content, group, name))
                    if (t != null) Object.DestroyImmediate(t.gameObject);
        }

        // Each moving piece's dressing rides its mover, which is still at rest (the content is built inactive); the
        // mover's own parts stop drawing (they still collide).
        static void Hang(Transform content, Transform dress, (string group, string name)[] hung)
        {
            foreach (var (group, name) in hung)
            {
                var root = content.Find(group);
                var mover = root != null ? root.Find(name) : null;
                var node = dress.Find(name);
                if (mover == null || node == null)
                {
                    Debug.LogWarning("[정원] 움직이는 장식을 붙일 곳이 없습니다: " + group + "/" + name);
                    continue;
                }
                foreach (var renderer in mover.GetComponentsInChildren<Renderer>(true)) renderer.enabled = false;
                node.SetParent(mover, true);
            }
        }

        // R426: each geyser's water is computed (GardenJet, GC_jet) instead of the graybox's glass column and bubbles; the
        // six on the walk have a basin of water round the nozzle, the two on the ramp are dry-deck fountains.
        static void RefitGeysers(Transform canal)
        {
            if (canal == null) return;
            var water = Resources.Load<Material>(ConceptMaterials + "GC_jet");
            if (water == null) Debug.LogWarning("[정원] 물기둥 재질이 없습니다: " + ConceptMaterials + "GC_jet");
            foreach (var geyser in canal.GetComponentsInChildren<GardenGeyser>(true))
            {
                foreach (var old in new[] { "Geyser water", "Geyser bubbles" })
                {
                    var part = geyser.transform.Find(old);
                    if (part != null) Object.DestroyImmediate(part.gameObject);
                }
                var jet = geyser.GetComponent<GardenJet>();
                if (jet == null) jet = geyser.gameObject.AddComponent<GardenJet>();
                jet.Configure(geyser.Order, geyser.Height, geyser.transform.localPosition.z <= -12.5f, water);
            }
        }

        // "GC | cream stone" (as the FBX names it) -> Resources/PawnRushGardenConcept/Materials/GC_cream_stone.
        static Material ConceptMaterial(Material imported, Dictionary<string, Material> cache)
        {
            // R420: Unity imports "GC | cream stone" as "GC _ cream stone" (the | becomes _), so every run of characters
            // that are not letters or digits becomes one underscore: both spellings give GC_cream_stone.
            string name = System.Text.RegularExpressions.Regex.Replace(imported.name.Replace(" (Instance)", ""), "[^A-Za-z0-9]+", "_").Trim('_');
            if (!cache.TryGetValue(name, out var material))
            {
                string path = SharedMaterials.TryGetValue(name, out var shared) ? shared : ConceptMaterials + name;
                material = Resources.Load<Material>(path);
                if (material == null) Debug.LogWarning("[정원] 컨셉 재질이 없습니다: " + path);
                else if (Weathers(name)) material = Weathered(material);
                cache[name] = material;
            }
            return material != null ? material : imported;
        }

        // R480 (the hand-made pass, step 4): the stone, tiles and joints drawn weathered - their GC_ material's picture,
        // colour and smoothness in PR_GardenWeathered (ChessFight/Garden Weathered), which reads the grime, moss, water
        // streaks and each piece's tint that handmade_pass.py --step 4 baked into the FBX's vertex colours, and mixes the
        // picture with itself turned so its tiling does not show. Every garden FBX carries those colours (white would
        // read as all dirt), so only the GC_ materials of the garden dressing are swapped.
        static readonly string[] WeatheredKinds = { "stone", "tile", "joint", "grout", "paving" };

        static bool Weathers(string name)
        {
            string n = name.ToLowerInvariant();
            if (!n.StartsWith("gc_") || n.Contains("water") || n.Contains("shade")) return false;
            foreach (var kind in WeatheredKinds)
                if (n.Contains(kind)) return true;
            return false;
        }

        static Material weatheredTemplate;

        static Material Weathered(Material source)
        {
            if (weatheredTemplate == null) weatheredTemplate = Resources.Load<Material>("PR_GardenWeathered");
            if (weatheredTemplate == null)
            {
                Debug.LogWarning("[정원] 풍화 재질이 없습니다: Resources/PR_GardenWeathered");
                return source;
            }
            var m = new Material(weatheredTemplate) { name = source.name + " (weathered)" };
            if (source.HasProperty("_BaseMap"))
            {
                m.SetTexture("_BaseMap", source.GetTexture("_BaseMap"));
                m.SetTextureScale("_BaseMap", source.GetTextureScale("_BaseMap"));
                m.SetTextureOffset("_BaseMap", source.GetTextureOffset("_BaseMap"));
            }
            if (source.HasProperty("_BaseColor")) m.SetColor("_BaseColor", source.GetColor("_BaseColor"));
            if (source.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", source.GetFloat("_Smoothness"));
            return m;
        }
    }
}
