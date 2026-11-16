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
    //   RcyCoach            the closed coach (승규 11-15 "닫힌 마차") on every YardKit.Carriage - the parked one and the
    //   RcyCoachWheel       roaming teams' - and a wheel on each of its Wheel pivots
    //   RcyHorse            every YardHorse (승규 11-15, a picture of chunky royal horses): one skinned horse in its
    //                       coat (bay, grey, black) with its bridle, plume, girth and breast collar, and the harness or
    //                       the saddle; YardHorseRig turns its bones with YardHorse's pivots (horse_model.py)
    //   RcyHitch            a team's pole, swingle-tree and traces
    //   RcyGateLeafW/E      the start gates' leaves (TimedGate)
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
    // Untextured for now: every slot is drawn in the hall's clay (승규: modelling without textures is one grey clay).
    // The castle kit's RcyCastleOuterGate, RcyCastleFinalGate and RcyCastleSectionEnd come with their textures and are
    // drawn in them (RoyalCarriageYard.Castle.cs, which also adds their colliders).
    public partial class RoyalCarriageYard
    {
        const string ModelFolder = "PawnRushCarriageYard/";

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
            hidden += DressSortingMachines();
            Debug.Log("[RoyalCarriageYard] 움직이는 모델 · 블록아웃 " + hidden + "개 그림 끔(충돌은 그대로)");
        }

        // Zone 3's machines (sorting_machines_model.py): each machine's standing part on its root, each moving part on
        // the pivot the code moves it by. Kept drawing: the gold trims, the lamps, the ropes and chains the code
        // stretches, the marks and rings that warn (the gap marks, the reach and sector, the slam circle and its wave and
        // dust, the well's boards and edge), and the handles a pawn holds on the lifting wheel.
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
                hidden += Unpaint(row, "Beam trim", "Trolley lamp", "Row rope", "Gap mark");
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
                hidden += Unpaint(crane, "Jib trim", "Jib lamp", "Slew reach", "Slew sector");
                Ride(crane.transform, "RcySlewCrane");
                foreach (var jib in crane.GetComponentsInChildren<SlewJib>(true)) Ride(jib.transform, "RcySlewJib");
            }
            foreach (var hoist in root.GetComponentsInChildren<PalletHoist>(true))
            {
                hidden += Unpaint(hoist, "Arm trim", "Hoist lamp", "Hoist rope", "Well board", "Well edge");
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
                hidden += Unpaint(wheel, "Handle", "Handle post");
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
                if (r.name == "Harness") r.gameObject.SetActive(!saddle);
                else if (r.name == "Saddle") r.gameObject.SetActive(saddle);
                var slots = r.sharedMaterials;
                for (int i = 0; i < slots.Length; i++) slots[i] = HorsePaint(slots[i] != null ? slots[i].name : "", coat);
                r.sharedMaterials = slots;
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
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
            slot = slot.ToLowerInvariant();
            int c = (int)coat;
            if (slot.Contains("coat")) return Paint("coat" + c, new[] { "#8B4A22", "#ECE8E1", "#2A2523" }[c], .35f);
            if (slot.Contains("hair")) return Paint("hair" + c, new[] { "#3B2216", "#BDB7AE", "#151211" }[c], .3f);
            if (slot.Contains("hoof")) return Paint("hoof" + c, new[] { "#2B221D", "#5E5852", "#1C1816" }[c], .5f);
            if (slot.Contains("eye")) return Paint("eye", "#0E0B0A", .85f);
            if (slot.Contains("leather")) return Paint("leather", "#5B3320", .45f);
            if (slot.Contains("gold")) return Paint("gold", "#D9A938", .65f, .85f);
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

        // A model under `parent` (a pivot turned 180 degrees about y), every slot in clay, casting shadows.
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
                for (int i = 0; i < slots.Length; i++) slots[i] = clay;
                r.sharedMaterials = slots;
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            }
            return pivot;
        }
    }
}
