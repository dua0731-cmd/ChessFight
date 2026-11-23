using System.Collections.Generic;
using ChessFight.Gameplay;
using UnityEngine;

namespace ChessFight.PawnRush
{
    // R678 (승규 11-15 "마차뜰 블록아웃을 모델로 교체", "그래픽을 완전 좋게 할건데"): the Blender models laid over the code's
    // blockout, zone by zone, the way the garden's concept dressing was (RoyalWaterGardenBuilder.Concept.cs). Every
    // position, collider, pivot and timing stays the blockout's: a covered piece only stops drawing.
    //
    // Source: ArtSource/PawnRush/CarriageYardModels (rcy_kit.py, one <zone>_model.py each, export_rcy.py), exported to
    // Resources/PawnRushCarriageYard. Each FBX hangs under a pivot turned 180 degrees about y (Blender x, y, z = Unity x,
    // z, y): a zone's FBX in the map's frame under the built root, a moving piece's FBX on the pivot the code turns it on.
    //
    //   RcyEntrance         zone 1: the start court, the tablet, the yard's paving and walls, the storage sheds and their
    //                       loads, the court's crates (the gate wall and its towers are the castle kit's, below)
    //                       (RcyEntrance, RcyCanal and RcySorting are each cut into ~10 m cells along z since R681, one
    //                       child renderer per cell, so what is off screen is culled; Hang dresses every child alike)
    //   RcyCoach            the closed coach (승규 11-15 "닫힌 마차") on every YardKit.Carriage - the parked one and the
    //   RcyCoachWheel       roaming teams' - and a wheel on each of its Wheel pivots (R681: a locker between the wheels
    //                       and a boot and trunk behind fill the carriage's collider box to a pawn's height)
    //   RcyHorse            every YardHorse (승규 11-15, a picture of chunky royal horses): one skinned horse in its
    //                       coat (bay, grey, black) with its bridle, plume, girth and breast collar, and the harness or
    //                       the saddle; YardHorseRig turns its bones with YardHorse's pivots (horse_model.py)
    //   RcyHitch            a team's pole, swingle-tree and traces
    //   RcyGateLeafW/E      the start gates' leaves (TimedGate)
    //   RcyFinalGateLeafW/E the final gate's leaves (PushGate), old and scratched in their own slots RCY_Oldwalnut and
    //                       RCY_Oldiron (textures/rcy_worn.py, 11-23)
    //   RcyCanal            zone 2 (the pilot): landings and balustrades, parapets, canal walls, the four walkways, the
    //                       wheel's axle posts and bearings, the flume, cistern and trestles
    //   RcyGiantWaterwheel  the giant waterwheel (HorizontalWaterwheel): rims, buckets, hub, six spoke pairs, the crowned
    //                       knight on both faces
    //   RcySorting          zone 3 (sorting_model.py): the yard's paving and walls, the sorting stalls (cloth awnings,
    //                       goods, bay numbers, scales, the conveyor's hatches), the timber stores, the loft, the berths'
    //                       boards, the lamp posts, the basin, the lifting wheel's piers, bearings, axle and landing, the
    //                       basin docks (sluice, barge, jib crane, banners)
    //   zone 3's machines   (sorting_machines_model.py) RcyConveyor and its loads RcyConveyorCrate / RcyConveyorStack;
    //                       RcyStackRow, RcyStackCrate, RcyRowTrolley; the heavy berth's RcySlamGantry, RcySlamChest,
    //                       RcySlamTrolley; the timber cranes' RcySlewCrane and RcySlewJib; the loft hoists'
    //                       RcyHoistFrame and RcyHoistPalletP / N; the lifting wheel's RcyLiftWheel (DressSortingMachines)
    //   RcyPlatform         zone 4 (platform_model.py, R680): the cliff's marble face over the basin (flush with or set
    //                       back from its collider's face), its coping, the platform's paving, the edge balustrades
    //   RcyFinishCourt      the whole map only: the finish court's paving, its walls, the crown's pedestal and cushion
    //   RcySectionFloors    as a section only: the runout's, the terrace's and the entrance band's paving
    //   RcyScenery          (scenery_model.py, R680) the merlons on the yard's and canal's side walls and the canal
    //   RcySceneryOwn       landings' lamp posts; the sorting yard's and platform's merlons, the platform's lamp posts and a
    //                       corbel table under the sorting yard's wall copings (over every machine there, 10.15 m up)
    //
    // Never covered: what tells a player something (water, the pour and the spill, the gold jump lines, signs and the
    // countdown, the coaches' lamps that go red through a charge, the charge lines) and the lamps' glow.
    // Materials (R686, 승규 11-17 "재질 텍스쳐 컨셉 아트대로 … 구워"): each slot "RCY | <name>" is drawn in the baked library
    // material Resources/PawnRushCarriageYard/Materials/RCY_<Name> (URP Lit, a seamless tile in metres - the models'
    // UVs are in metres; ArtSource/PawnRush/CarriageYardModels/textures/PLAN.md), or in Materials/<model>/RCY_<Name>
    // when a model has its own; a slot with neither stays in the hall's clay. The castle kit's RcyCastleOuterGate,
    // RcyCastleFinalGate and RcyCastleSectionEnd come with their textures and are drawn in them
    // (RoyalCarriageYard.Castle.cs, which also adds their colliders).
    public partial class RoyalCarriageYard
    {
        const string ModelFolder = "PawnRushCarriageYard/";
        const string MaterialFolder = ModelFolder + "Materials/";

        [Tooltip("R686: draw the models in the baked material library (Resources/PawnRushCarriageYard/Materials); off = all clay.")]
        public bool bakedMaterials = true;

        // A zone's dressing: its FBX and the blockout group it covers, but for the pieces named in `keep` (a name, or a
        // name's start followed by '*').
        sealed class ZoneModel
        {
            public string model;
            public string group;
            public string[] keep = new string[0];
            public bool mapOnly, sectionOnly;   // hung only when the whole map is built / only as a section
            public string[] only;               // when set, only these pieces stop drawing (a group covered in part)
        }

        // Zone 4's pieces the code still draws: the finish line (where the race ends) and the crown over it.
        static readonly string[] FinalKeep = { "Finish line", "Crown", "Crown point" };

        // The scenery's pieces the models stand in for (declared before the table, which reads it as it is built).
        static readonly string[] SceneryCovered = { "Merlon", "Lamp post", "Lamp cap" };

        static readonly ZoneModel[] ZoneModels =
        {
            new ZoneModel
            {
                model = "RcyEntrance",
                group = "1 Entrance",
                keep = new[] { "Shed lamp", "Charge line*" },
            },
            new ZoneModel
            {
                model = "RcyCanal",
                group = "2 Canal",
                keep = new[] { "Water", "Flume water", "Pour", "Spill", "Foam", "Gap edge" },
            },
            // Zone 3 is built in its own frame (under "3-4 (own frame)"); its FBX is in the map's like the others.
            new ZoneModel
            {
                model = "RcySorting",
                group = "3-4 (own frame)/3 Loading and sorting yard",
                keep = new[]
                {
                    "Pool water", "Waterfall", "Foam", "Sluice water", "Sluice foam", "Sluice tunnel", "Hatch tunnel", "Lamp",
                    "Landing edge trim", "Landing trim", "Loft edge", "Loft lip",
                },
            },
            // Zone 4 (platform_model.py, R680): the cliff and the platform either way, the finish court on the whole map,
            // the runout's, terrace's and band's floors as a section (their arch, railings and steps are the castle kit's).
            new ZoneModel { model = "RcyPlatform", group = "3-4 (own frame)/4 Final gate", keep = FinalKeep },
            new ZoneModel { model = "RcyFinishCourt", group = "3-4 (own frame)/4 Final gate", keep = FinalKeep, mapOnly = true },
            new ZoneModel { model = "RcySectionFloors", group = "3-4 (own frame)/4 Final gate", keep = FinalKeep, sectionOnly = true },
            // The scenery inside the walls (scenery_model.py, R680): the side walls' merlons and the lamp posts, the lamps'
            // glow kept; the banners and what stands outside the walls stay the code's (the castle kit covers the gates' towers).
            new ZoneModel { model = "RcyScenery", group = "Scenery", only = SceneryCovered },
            new ZoneModel { model = "RcySceneryOwn", group = "3-4 (own frame)/Scenery (own frame)", only = SceneryCovered },
        };

        // A carriage's lamps: kept drawing (a roaming one's go red through a charge), at the size the roaming ones are
        // drawn at, which the model's housings are built round.
        static readonly Vector3 CoachLamp = new Vector3(.18f, .3f, .18f) * 1.8f;

        Material clay;
        readonly Dictionary<string, GameObject> loaded = new Dictionary<string, GameObject>();

        void Dress()
        {
            clay = Course01v4Builder.HallClay;
            int shown = 0, hidden = 0;
            foreach (var zone in ZoneModels)
            {
                if (zone.mapOnly && asSection || zone.sectionOnly && !asSection) continue;
                var group = root.Find(zone.group);
                var model = Model(zone.model);
                if (group == null || model == null)
                {
                    // (No scenery groups when the yard is built without its scenery.)
                    if (group == null && zone.only == null) Debug.LogWarning("[RoyalCarriageYard] 모델을 붙일 구역이 없습니다: " + zone.group);
                    continue;
                }
                foreach (var r in group.GetComponentsInChildren<Renderer>(true))
                {
                    // Moving obstacles get their own models (DressMovers); text (signs) always stays.
                    if (Moving(r) || r.GetComponent<TextMesh>() != null || Kept(r.name, zone.keep)) continue;
                    if (zone.only != null && !Kept(r.name, zone.only)) continue;
                    r.enabled = false;
                    hidden++;
                }
                Hang(model, root, zone.model);
                shown++;
            }
            // The parked carriage in the start court (a picture over its own solid box).
            var parked = root.Find("1 Entrance/Parked carriage/Carriage");
            if (parked != null) DressCarriage(parked);
            Debug.Log("[RoyalCarriageYard] 모델 " + shown + "구역 · 블록아웃 " + hidden + "개 그림 끔(충돌은 그대로)");
            // The castle kit over both gates (and the section's end), in its own textures (RoyalCarriageYard.Castle.cs).
            DressCastle();
            // R809: the big still models' far halves, once their materials are all set.
            AttachLods();
        }

        // R809 (the 11-23 build test: the yard drew at 52 fps on an RTX 3060, the grand stair hall at 77 - the yard is open
        // and nearly all of it is in view at once; the user: "마차뜰 LOD 부터 해줘 너무 많이는 줄이지 말고"): a model with a
        // Resources/PawnRushCarriageYard/LOD/<model>_LOD1 (lod_rcy.py: half its triangles, nothing else changed) gets it
        // hung beside each of its pieces, the two in one LODGroup: the piece as it is nearer than LodSwitch metres (at
        // LOD bias 1 - the PC "높음" preset's 1.6 makes it about 64 m, "중간" 40, "낮음" 24), its half beyond. Nothing is
        // culled. Pictures only - the models have no colliders - and the same materials slot for slot.
        const string LodFolder = ModelFolder + "LOD/";
        const float LodSwitch = 40f, LodFov = 60f;
        readonly List<(string name, Transform pivot, GameObject model)> hung = new List<(string, Transform, GameObject)>();

        void AttachLods()
        {
            int models = 0, pieces = 0;
            long near = 0, far = 0;
            foreach (var (name, pivot, model) in hung)
            {
                if (pivot == null) continue;
                var half = Resources.Load<GameObject>(LodFolder + name + "_LOD1");
                if (half == null) continue;
                // The piece and the FBX renderer it came from, by name (Hang instantiated the model under the pivot).
                var source = model.GetComponentsInChildren<Renderer>(true);
                var views = pivot.GetComponentsInChildren<Renderer>(true);
                var byName = new Dictionary<string, (Renderer view, Renderer source)>();
                for (int k = 0; k < views.Length && k < source.Length; k++) byName[views[k].name] = (views[k], source[k]);
                var farView = Object.Instantiate(half, pivot, false);
                farView.name = name + " LOD1";
                bool used = false;
                foreach (var r in farView.GetComponentsInChildren<Renderer>(true))
                {
                    if (!byName.TryGetValue(r.name, out var pair) || pair.view.GetComponent<LODGroup>() != null) { r.enabled = false; continue; }
                    // The materials the piece drew with, matched by the FBX slot names (a slot may have lost its faces).
                    var given = new Dictionary<string, Material>();
                    var from = pair.source.sharedMaterials;
                    var drawn = pair.view.sharedMaterials;
                    for (int i = 0; i < from.Length && i < drawn.Length; i++)
                        if (from[i] != null && !given.ContainsKey(from[i].name)) given[from[i].name] = drawn[i];
                    var slots = r.sharedMaterials;
                    for (int i = 0; i < slots.Length; i++)
                        slots[i] = slots[i] != null && given.TryGetValue(slots[i].name, out var m) ? m : (drawn.Length > 0 ? drawn[0] : clay);
                    r.sharedMaterials = slots;
                    r.shadowCastingMode = pair.view.shadowCastingMode;
                    r.transform.SetParent(pair.view.transform, true);
                    var group = pair.view.gameObject.AddComponent<LODGroup>();
                    group.SetLODs(new[] { new LOD(.5f, new[] { pair.view }), new LOD(0f, new[] { r }) });
                    group.RecalculateBounds();
                    // The screen height at which the piece is LodSwitch metres away (its reference point, LOD bias 1).
                    float height = group.size / (2f * LodSwitch * Mathf.Tan(LodFov * .5f * Mathf.Deg2Rad));
                    group.SetLODs(new[] { new LOD(Mathf.Clamp(height, .002f, .99f), new[] { pair.view }), new LOD(0f, new[] { r }) });
                    near += Triangles(pair.view);
                    far += Triangles(r);
                    pieces++;
                    used = true;
                }
                // A many-piece FBX leaves its emptied root behind (a one-piece FBX's root is its piece, now moved).
                if (farView != null && farView.GetComponent<Renderer>() == null) Object.Destroy(farView);
                if (used) models++;
            }
            if (pieces > 0)
                Debug.Log($"[RoyalCarriageYard] 먼 거리 LOD: 모델 {models}개 · 조각 {pieces}개, 삼각형 {near:N0} → {far:N0} " +
                          $"(LOD 배율 1에서 {LodSwitch:0} m 넘어서, 지금 배율 {QualitySettings.lodBias:0.##})");
        }

        static long Triangles(Renderer r)
        {
            Mesh mesh = null;
            if (r.TryGetComponent<MeshFilter>(out var filter)) mesh = filter.sharedMesh;
            if (mesh == null) return 0;
            long n = 0;
            for (int i = 0; i < mesh.subMeshCount; i++) n += (long)mesh.GetIndexCount(i) / 3;
            return n;
        }

        // After the map is switched on: a moving obstacle builds its own parts in its Awake, so only now are there parts
        // to stop drawing.
        void DressMovers()
        {
            if (clay == null) clay = Course01v4Builder.HallClay;
            int hidden = 0;
            foreach (var wheel in root.GetComponentsInChildren<HorizontalWaterwheel>(true))
            {
                hidden += Unpaint(wheel);
                Ride(wheel.transform, "RcyGiantWaterwheel");
            }
            foreach (var carriage in root.GetComponentsInChildren<RoamingCarriage>(true))
            {
                hidden += Unpaint(carriage, "Lamp");
                foreach (var t in carriage.GetComponentsInChildren<Transform>(true))
                    if (t.name == "Carriage") DressCarriage(t);
                if (carriage.transform.Find("Team carriage") != null) DressHitch(carriage.transform);
            }
            // Every horse in the yard: the carriages' pairs, the loose ones, the stable's charger.
            int horses = 0;
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
                if (t.name == "Body" && t.Find("Neck") != null && t.Find("Leg FL") != null)
                {
                    hidden += DressHorse(t);
                    horses++;
                }
            if (horses > 0) Debug.Log("[RoyalCarriageYard] 말 모델 " + horses + "마리");
            // The start gates (later zones' gates get their own leaves with their zones).
            var entrance = root.Find("1 Entrance");
            if (entrance != null)
                foreach (var gate in entrance.GetComponentsInChildren<TimedGate>(true))
                {
                    var leaf = gate.transform.Find("Leaf");
                    if (leaf == null) continue;
                    hidden += Unpaint(gate);
                    Ride(gate.transform, leaf.localPosition.x >= 0f ? "RcyGateLeafW" : "RcyGateLeafE");
                }
            // The final gate's leaves (11-23, movers_model.py final_gate_leaf: old walnut planks, rusty strap hinges, a
            // knocker on the code's ring): each leaf's own boxes stop drawing (their colliders stay - what a hand holds),
            // the progress bar and its sign over the gate keep drawing.
            foreach (var push in root.GetComponentsInChildren<PushGate>(true))
                foreach (var leaf in push.GetComponentsInChildren<Rigidbody>(true))
                {
                    var door = leaf.transform.Find("Door");
                    if (door == null) continue;
                    hidden += Unpaint(leaf);
                    Ride(leaf.transform, door.localPosition.x >= 0f ? "RcyFinalGateLeafW" : "RcyFinalGateLeafE");
                }
            hidden += DressSortingMachines();
            Debug.Log("[RoyalCarriageYard] 움직이는 모델 · 블록아웃 " + hidden + "개 그림 끔(충돌은 그대로)");
            // R686: how many slots got the baked library (Resources/PawnRushCarriageYard/Materials) and which names did not.
            Debug.Log("[RoyalCarriageYard] 구운 재질 " + bakedSlots + "칸 · 클레이 " + claySlots + "칸" + (bakedMaterials ? "" : " (bakedMaterials 꺼짐)")
                      + (missedSlots.Count > 0 ? " · 재질 못 찾은 칸 이름: " + string.Join(", ", missedSlots) : ""));
        }

        // Zone 3's machines (sorting_machines_model.py): each machine's standing part on its root, each moving part on
        // the pivot the code moves it by. Kept drawing: the gold trims, the lamps, the ropes and chains the code
        // stretches, the marks and rings that warn (the gap marks, the reach and sector, the slam circle and its wave and
        // dust, the well's boards and edge). The lifting wheel's handles are the model's since 11-23.
        int DressSortingMachines()
        {
            int hidden = 0;
            foreach (var conveyor in root.GetComponentsInChildren<CargoConveyor>(true))
            {
                hidden += Unpaint(conveyor, "Conveyor trim");
                Ride(conveyor.transform, "RcyConveyor");
                foreach (var load in conveyor.GetComponentsInChildren<ConveyorCrate>(true))
                {
                    int crates = 0;
                    foreach (Transform t in load.transform)
                        if (t.name == "Crate") crates++;
                    Ride(load.transform, crates > 1 ? "RcyConveyorStack" : "RcyConveyorCrate");
                }
            }
            foreach (var row in root.GetComponentsInChildren<StackRow>(true))
            {
                hidden += Unpaint(row, "Beam trim", "Trolley lamp", "Row rope*", "Gap mark");
                Ride(row.transform, "RcyStackRow");
                foreach (var stack in row.GetComponentsInChildren<StackCrate>(true)) Ride(stack.transform, "RcyStackCrate");
                foreach (Transform t in row.transform)
                    if (t.name == "Row trolley") RideUnscaled(t, "RcyRowTrolley");
            }
            foreach (var slam in root.GetComponentsInChildren<CargoSlam>(true))
            {
                hidden += Unpaint(slam, "Beam band", "Slam chain", "Slam circle", "Slam circle rim", "Slam wave", "Slam dust");
                Ride(slam.transform, "RcySlamGantry");
                foreach (var chest in slam.GetComponentsInChildren<SlamChest>(true)) Ride(chest.transform, "RcySlamChest");
                var trolley = slam.transform.Find("Slam trolley");
                if (trolley != null) RideUnscaled(trolley, "RcySlamTrolley");
            }
            foreach (var crane in root.GetComponentsInChildren<SlewCrane>(true))
            {
                hidden += Unpaint(crane, "Jib trim", "Jib lamp", "Lantern*", "Slew reach", "Slew sector");
                Ride(crane.transform, "RcySlewCrane");
                foreach (var jib in crane.GetComponentsInChildren<SlewJib>(true)) Ride(jib.transform, "RcySlewJib");
            }
            foreach (var hoist in root.GetComponentsInChildren<PalletHoist>(true))
            {
                hidden += Unpaint(hoist, "Arm trim", "Hoist lamp", "Lantern*", "Hoist rope", "Well board", "Well edge");
                Ride(hoist.transform, "RcyHoistFrame");
                // The pallet's sacks are at its outer side: the model with them on that side.
                foreach (var pallet in hoist.GetComponentsInChildren<HoistPallet>(true))
                {
                    var sack = pallet.transform.Find("Pallet sack");
                    Ride(pallet.transform, sack != null && sack.localPosition.x < 0f ? "RcyHoistPalletN" : "RcyHoistPalletP");
                }
            }
            foreach (var wheel in root.GetComponentsInChildren<VerticalWaterwheel>(true))
            {
                // 11-23 (승규 "물레방아 손잡이 부분도 노란색 말고 다른 걸로 모델링 해줘"): the model has its own handles
                // (rope-wound walnut rungs on iron brackets, sorting_machines_model.py lift_handle) where the code's gold
                // bars are, so those stop drawing too; their grab colliders stay.
                hidden += Unpaint(wheel);
                Ride(wheel.transform, "RcyLiftWheel");
            }
            return hidden;
        }

        // A YardKit.Carriage: the coach on its frame, a wheel on each Wheel pivot, its lamps kept (at the roaming size).
        void DressCarriage(Transform carriage)
        {
            Ride(carriage, "RcyCoach");
            foreach (Transform child in carriage)
                if (child.name == "Wheel") Ride(child, "RcyCoachWheel");
            foreach (var r in carriage.GetComponentsInChildren<Renderer>(true))
                if (r.name == "Lamp")
                {
                    r.enabled = true;
                    if (r.transform.localScale.y < .5f) r.transform.localScale = CoachLamp;
                }
        }

        // A YardHorse from its Body pivot: its blockout stops drawing, the skinned horse hangs where the body's pivot
        // leaves the root (YardHorse's 1.05 up, 0.75 back), in its coat (read off the blockout's barrel), with the
        // harness (a carriage horse, wearing a collar) or the saddle (a ridden one), and YardHorseRig turns its bones
        // with the pivots. Returns how many renderers went dark.
        int DressHorse(Transform body)
        {
            var model = Model("RcyHorse");
            if (model == null) return 0;
            // The pivots by name, before the model's bones (some of the same names) are under the body too.
            var pivots = new Dictionary<string, Transform>();
            foreach (var t in body.GetComponentsInChildren<Transform>(true))
                if (!pivots.ContainsKey(t.name)) pivots[t.name] = t;
            var barrel = body.Find("Barrel");
            string coatName = barrel != null && barrel.GetComponent<Renderer>() != null ? barrel.GetComponent<Renderer>().sharedMaterial.name : "";
            var coat = coatName.Contains("Grey") ? YardHorse.Coat.Grey : coatName.Contains("Black") ? YardHorse.Coat.Black : YardHorse.Coat.Bay;
            bool saddle = body.Find("Saddle cloth") != null;
            int hidden = 0;
            foreach (var r in body.GetComponentsInChildren<Renderer>(true))
                if (r.enabled)
                {
                    r.enabled = false;
                    hidden++;
                }
            var pivot = new GameObject("RcyHorse (model)").transform;
            pivot.SetParent(body, false);
            pivot.localPosition = new Vector3(0f, -1.05f, .75f);
            pivot.localRotation = Quaternion.Euler(0f, 180f, 0f);
            var view = Object.Instantiate(model, pivot, false);
            view.name = "RcyHorse";
            foreach (var r in view.GetComponentsInChildren<Renderer>(true))
            {
                // Each mesh comes as M_LOD0 and M_LOD1 (R680 QA: the FBX's LOD names make Unity's LOD Group).
                if (r.name.StartsWith("Harness")) r.gameObject.SetActive(!saddle);
                else if (r.name.StartsWith("Saddle")) r.gameObject.SetActive(saddle);
                var slots = r.sharedMaterials;
                for (int i = 0; i < slots.Length; i++) slots[i] = HorsePaint(slots[i] != null ? slots[i].name : "", coat);
                r.sharedMaterials = slots;
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
                // The legs and tail at full stride reach 0.55 m past the rest pose's bounds: grown so the horse is
                // not culled at the screen's edges.
                if (r is SkinnedMeshRenderer skin)
                {
                    var b = skin.localBounds;
                    b.Expand(1.2f);
                    skin.localBounds = b;
                }
            }
            // LOD1 (the *_LOD1 meshes) from about 14 m (the horse under 12% of the screen's height, the camera 6 m
            // behind the pawn) and never culled (the importer's default would drop a far horse). Set here whether or not
            // the importer made the group, so the two levels never draw at once.
            var near = new List<Renderer>();
            var far = new List<Renderer>();
            foreach (var r in view.GetComponentsInChildren<Renderer>(true)) (r.name.EndsWith("_LOD1") ? far : near).Add(r);
            if (far.Count > 0)
            {
                var lodGroup = view.GetComponentInChildren<LODGroup>(true);
                if (lodGroup == null) lodGroup = view.AddComponent<LODGroup>();
                lodGroup.SetLODs(new[] { new LOD(.12f, near.ToArray()), new LOD(0f, far.ToArray()) });
                lodGroup.RecalculateBounds();
            }
            view.AddComponent<YardHorseRig>().Bind(body, view.transform, pivots);
            return hidden;
        }

        // A team's hitch on its frame, in clay but its traces and pole straps: the horses wear them, so they are in the
        // horses' leather (the pole and swingle-tree are the carriage's).
        void DressHitch(Transform team)
        {
            var model = Model("RcyHitch");
            if (model == null) return;
            var pivot = Hang(model, team, "RcyHitch");
            var source = model.GetComponentsInChildren<Renderer>(true);
            var views = pivot.GetComponentsInChildren<Renderer>(true);
            for (int k = 0; k < views.Length && k < source.Length; k++)
            {
                var from = source[k].sharedMaterials;
                var slots = views[k].sharedMaterials;
                for (int i = 0; i < slots.Length && i < from.Length; i++)
                    if (from[i] != null && from[i].name.ToLowerInvariant().Contains("leather"))
                        slots[i] = HorsePaint(from[i].name, YardHorse.Coat.Bay);
                views[k].sharedMaterials = slots;
            }
        }

        // The horse's colours by slot (horse_model.py's material names): the coat, mane and tail, hooves for each coat
        // (승규 11-15 "백 말 흑말 갈색 말"); the eyes, brown leather, gold, the cream plume and the red cloth for all.
        static readonly Dictionary<string, Material> horsePaints = new Dictionary<string, Material>();

        Material HorsePaint(string slot, YardHorse.Coat coat)
        {
            int c = (int)coat;
            // R686: the baked coat and hair are neutral grey, tinted per coat; the tack and hooves straight from the library.
            var key = SlotKey(slot);
            if (key == "RCY_Coat" || key == "RCY_Hair")
            {
                // The grey coat a warm light grey-beige (concept sheet 08; #ECE8E1 over the neutral tile read white and blue).
                var tint = key == "RCY_Coat" ? new[] { "#8B4A22", "#D6CFC4", "#2A2523" }[c] : new[] { "#3B2216", "#BDB7AE", "#151211" }[c];
                var tinted = Tinted(Baked(slot, "RcyHorse"), key + c, tint);
                if (tinted != null) return tinted;
            }
            else if (key != null && key != "RCY_Eye")
            {
                var m = Baked(slot, "RcyHorse");
                if (m != null) return m;
            }
            slot = slot.ToLowerInvariant();
            if (slot.Contains("coat")) return Paint("coat" + c, new[] { "#8B4A22", "#ECE8E1", "#2A2523" }[c], .35f);
            if (slot.Contains("hair")) return Paint("hair" + c, new[] { "#3B2216", "#BDB7AE", "#151211" }[c], .3f);
            if (slot.Contains("hoof")) return Paint("hoof" + c, new[] { "#2B221D", "#5E5852", "#1C1816" }[c], .5f);
            if (slot.Contains("eye")) return Paint("eye", "#0E0B0A", .85f);
            if (slot.Contains("leather")) return Paint("leather", "#5B3320", .45f);
            if (slot.Contains("gold")) return Paint("gold", "#D9A938", .65f, .85f);
            if (slot.Contains("brass")) return Paint("brass", "#A07234", .45f, .9f);   // R686: the tack's studs and rosettes
            if (slot.Contains("plume")) return Paint("plume", "#F3ECDB", .2f);
            if (slot.Contains("cloth")) return Paint("cloth", "#8F1D2A", .3f);
            return clay;
        }

        static Material Paint(string key, string hex, float smoothness, float metallic = 0f)
        {
            if (horsePaints.TryGetValue(key, out var m) && m != null) return m;
            m = ChessFight.Game.LitMaterials.NewLit(Arena.Hex(hex), smoothness, metallic);
            m.name = "M_RcyHorse_" + key;
            return horsePaints[key] = m;
        }

        // Stops a moving thing's blockout drawing (its colliders and hit shells stay), but its text and the pieces named
        // in `keep`. Returns how many renderers went dark.
        static int Unpaint(Component thing, params string[] keep)
        {
            int hidden = 0;
            foreach (var r in thing.GetComponentsInChildren<Renderer>(true))
            {
                if (r.GetComponent<TextMesh>() != null || Kept(r.name, keep) || !r.enabled) continue;
                r.enabled = false;
                hidden++;
            }
            return hidden;
        }

        // A part of a moving obstacle, or of the machine that drives one (the conveyor's belt and frame, a storage row's
        // beam and crane, a timber crane's drum, a hoist's posts, the heavy berth's gantry): those get their own models
        // with their machines, so a zone's dressing leaves them drawing.
        static bool Moving(Renderer r) =>
            r.GetComponentInParent<Obstacle>(true) != null || r.GetComponentInParent<CargoConveyor>(true) != null
            || r.GetComponentInParent<StackRow>(true) != null || r.GetComponentInParent<SlewCrane>(true) != null
            || r.GetComponentInParent<PalletHoist>(true) != null || r.GetComponentInParent<CargoSlam>(true) != null;

        static bool Kept(string name, string[] keep)
        {
            foreach (var k in keep)
                if (k.EndsWith("*") ? name.StartsWith(k.Substring(0, k.Length - 1)) : name == k) return true;
            return false;
        }

        // A copy of one model on a pivot (the pivot moves it); nothing when the model is missing.
        void Ride(Transform pivot, string modelName)
        {
            var model = Model(modelName);
            if (model != null) Hang(model, pivot, modelName);
        }

        // A model on a pivot the code draws as a scaled box (a trolley): a holder undoes the pivot's scale, so the
        // model, built full size in the pivot's frame, is not stretched.
        void RideUnscaled(Transform pivot, string modelName)
        {
            var model = Model(modelName);
            if (model == null) return;
            var holder = new GameObject(modelName + " (holder)").transform;
            holder.SetParent(pivot, false);
            var s = pivot.localScale;
            holder.localScale = new Vector3(1f / s.x, 1f / s.y, 1f / s.z);
            Hang(model, holder, modelName);
        }

        GameObject Model(string modelName)
        {
            if (loaded.TryGetValue(modelName, out var model)) return model;
            model = Resources.Load<GameObject>(ModelFolder + modelName);
            if (model == null) Debug.LogWarning("[RoyalCarriageYard] 모델이 없습니다: Resources/" + ModelFolder + modelName + ".fbx");
            loaded[modelName] = model;
            return model;
        }

        // A model under `parent` (a pivot turned 180 degrees about y), each slot in its baked material (clay without
        // one), casting shadows.
        Transform Hang(GameObject model, Transform parent, string name)
        {
            var pivot = new GameObject(name + " (model)").transform;
            pivot.SetParent(parent, false);
            pivot.localRotation = Quaternion.Euler(0f, 180f, 0f);
            var view = Object.Instantiate(model, pivot, false);
            view.name = name;
            foreach (var r in view.GetComponentsInChildren<Renderer>(true))
            {
                var slots = r.sharedMaterials;
                for (int i = 0; i < slots.Length; i++)
                {
                    var m = slots[i] != null ? Baked(slots[i].name, name) : null;
                    if (m != null) bakedSlots++;
                    else if (slots[i] == null || !slots[i].name.StartsWith("RcyCastle_"))   // the kit's slots: CastlePaint, just after
                    {
                        claySlots++;
                        if (slots[i] != null) missedSlots.Add(slots[i].name);
                    }
                    slots[i] = m != null ? m : clay;
                }
                r.sharedMaterials = slots;
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            }
            hung.Add((name, pivot, model));   // R809: AttachLods
            return pivot;
        }

        // R686: the baked material for an FBX slot ("RCY | stone", Unity may add " (Instance)" and the like): the model's
        // own Materials/<model>/RCY_Stone first, then the library's Materials/RCY_Stone; null when there is neither (or
        // bakedMaterials is off). Loaded once each.
        readonly Dictionary<string, Material> baked = new Dictionary<string, Material>();

        Material Baked(string slot, string modelName)
        {
            if (!bakedMaterials) return null;
            var key = SlotKey(slot);
            if (key == null) return null;
            var cache = modelName + "/" + key;
            if (baked.TryGetValue(cache, out var m)) return m;
            m = Resources.Load<Material>(MaterialFolder + modelName + "/" + key);
            if (m == null) m = Resources.Load<Material>(MaterialFolder + key);
            return baked[cache] = m;
        }

        // "RCY | stone" -> "RCY_Stone"; null for a name that is not a library slot.
        // Whatever the importer made of the '|' and the spaces ("RCY | stone", "RCY _ stone", "RCY___stone",
        // "RCY | stone (Instance)"): the first word after "RCY".
        static string SlotKey(string slot)
        {
            if (string.IsNullOrEmpty(slot) || slot.Length < 4 || !slot.StartsWith("RCY", System.StringComparison.OrdinalIgnoreCase)) return null;
            int a = 3;
            while (a < slot.Length && !char.IsLetter(slot[a])) a++;
            int b = a;
            while (b < slot.Length && char.IsLetter(slot[b])) b++;
            if (b == a) return null;
            return "RCY_" + char.ToUpperInvariant(slot[a]) + slot.Substring(a + 1, b - a - 1).ToLowerInvariant();
        }

        // What Hang did, for the console: slots drawn baked, in clay, and the slot names that found no material.
        int bakedSlots, claySlots;
        readonly HashSet<string> missedSlots = new HashSet<string>();

        // A copy of a baked material with its base colour multiplied by `hex` (the neutral coat and hair per coat), cached.
        readonly Dictionary<string, Material> tintedMaterials = new Dictionary<string, Material>();

        Material Tinted(Material source, string key, string hex)
        {
            if (source == null) return null;
            if (tintedMaterials.TryGetValue(key, out var m) && m != null) return m;
            m = new Material(source) { name = source.name + " " + key };
            // The neutral tile averages about 0.8 grey: lift the tint so the coat's mean matches the old flat colour.
            var c = Arena.Hex(hex) * 1.25f;
            m.SetColor("_BaseColor", new Color(Mathf.Min(c.r, 1f), Mathf.Min(c.g, 1f), Mathf.Min(c.b, 1f), 1f));
            return tintedMaterials[key] = m;
        }
    }
}
