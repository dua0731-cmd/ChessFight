using System.Collections.Generic;
using UnityEngine;

namespace ChessFight.PawnRush
{
    // A · 의장 광장 (R506, user 11-07: "Unity 배치 진행"): the user's Tripo models, trimmed and repaired in
    // ArtSource/PawnRush/MiniGames/Early/a_props_v1.py and installed by a_install.py, set around the shared chessboard.
    // Section frame: x across (stations at +-12, x 2..22 each), z from the entry (0) to the exit (36); the board covers
    // x -9..9, z 6..24 on the 0.4 m dais, the north block is z 24..30, the rear corridor z 30..36.
    //
    // Pictures only, like every mini-game model: the board, its squares and their paint, the gates, colliders and
    // ramps stay the game's. The facade and team arches stand by their front face (a_props_v1.py FRONT_ORIGIN), so no
    // stone pokes over the board. Made in code, not in the model (user rules, 11-07): the swaying banners.
    public static partial class MiniGameModels
    {
        const string ParadeName = "MODEL_ParadeCourt";
        const string ParadeFolder = "Early/A/";
        const float ParadeFacadeZ = 24f;                         // the board's north edge: the towers' front-most stone
        // The gatehouse stands on the dais's level, not the ground's: on the ground its foot sat 0.4 m under the board
        // with no floor in front of it, so it looked afloat over a sky-coloured strip (user, 11-07: "떠 보이지 않게").
        const float ParadeFacadeY = Arena.DaisHeight;
        const float ParadeArchZ = 23.8f;
        const float ParadeDoorZ = 20.45f;                        // the team door's centre, station frame (section z 24.45)                         // the shell's "Team portal front" (opening 4.5 m)
        // No gaps (user, 11-07: "난간이랑 벽 틈 있게 하지마"): Tripo's module ends are rounded, so modules that only touch
        // show a slit between them. Neighbours overlap by a fifth of a module, and each run reaches into what it meets
        // (the entry wall's back, the rear end wall, the entry and exit arches, the side walls).
        static readonly float[] ParadeSideZ = { 4f, 11f, 18f, 25f, 32f };          // A_Wall 9.21 m, 2.2 m overlaps: z -0.6..36.6
        // The entry and exit arches' jambs stand at |x| 6.26..6.84: a run starts behind a jamb, never in the opening.
        static readonly float[] ParadeEntryX = { 10.9f, 18f };                     // |x| 6.3..22.6: behind the entry jamb, into the side wall
        static readonly float[] ParadeCypressZ = { 5.2f, 10.3f, 16.2f, 21.6f };    // between the flagpoles (z 7.5, 13, 19)
        static readonly float[] ParadeGardenX = { 7.8f, 10f, 12.2f, 14.4f, 16.6f, 18.8f, 21f }; // A_GardenWall 2.81 m, 0.6 m overlaps: |x| 6.4..22.4
        static readonly Vector2 ParadeStagger = new Vector2(.012f, .01f);          // every other module: out, up (no shared planes)
        const float ParadeFootIn = .05f;                                           // wall foot this far inside the floor's edge

        static void ParadeCourt(MiniGameSection owner)
        {
            var content = owner.transform.Find(MiniGameSection.ContentName);
            if (content == null || Resources.Load<GameObject>(Folder + ParadeFolder + "A_Facade") == null)
            {
                if (content != null) Load(ParadeFolder + "A_Facade");     // logs the missing model once; the R312 art stays
                return;
            }
            var cache = new Dictionary<string, Material>();
            ParadeShell(content);
            if (content.Find(ParadeName) == null) ParadeBuild(owner, content, cache);
            foreach (var station in owner.Stations)
                if (station != null) ParadeStation(station, cache);
            ParadeFloors(owner, content);
        }

        // The shell's pictures the models replace (their colliders stay): the centre facade, the team portal arches, the
        // upper cornice that would float over the lower facade, the north block's outer wall masses, the side, entry and
        // rear passage walls, the rook gatehouse towers, the code-built threshold props and the garden edge's planters
        // and benches.
        static void ParadeShell(Transform content)
        {
            HideCentreFacade(content);
            var shell = content.Find(MiniGamePalaceArt.ShellName);
            if (shell == null) return;
            foreach (var t in shell.GetComponentsInChildren<Transform>(true))
                switch (t.name)
                {
                    case "Team portal front": case "ENV_SideWall": case "Rear passage side wall": case "Entry wing wall":
                    case "ENV_RookGatehouse": case "DEC_ThresholdProps":
                    // user, 11-08: "초록색 공 지워": the old planters' ball cypresses; the place test below skipped their
                    // top tier (3.5 m up). A has its own planters and cypresses (A_Planter, A_Cypress).
                    case "Planter foot": case "Planter bowl": case "Planter rim": case "Planter foliage": case "Planter flower":
                    case "Cypress tier":
                        Hide(t);
                        break;
                    case "Facade cornice":
                        if (t.localPosition.y > 5f) Hide(t);
                        break;
                }
            // The north block's outer wall masses (x 14.35..22, 11 m) with their ashlar, pilasters and sconces: picked by
            // place (wholly outside x +-14.3, front of the block), so the team portals' upper walls and the long cornice stay.
            var facade = Find(shell, "ENV_NorthFacade");
            if (facade != null)
                foreach (var renderer in facade.GetComponentsInChildren<Renderer>(true))
                {
                    var lo = content.InverseTransformPoint(renderer.bounds.min);
                    var hi = content.InverseTransformPoint(renderer.bounds.max);
                    float near = Mathf.Min(Mathf.Abs(lo.x), Mathf.Abs(hi.x));
                    if (lo.x * hi.x > 0f && near >= 14.3f && (lo.z + hi.z) * .5f < 27.5f && renderer.GetComponent<MiniGameBannerCloth>() == null)
                        renderer.enabled = false;
                }
            foreach (var renderer in shell.GetComponentsInChildren<Renderer>(true))
            {
                var c = content.InverseTransformPoint(renderer.bounds.center);
                if (c.y > 3f) continue;
                foreach (float z in new[] { 2f, 8f, 14f, 21f, 33f })
                    if (new Vector2(Mathf.Abs(c.x) - 20.8f, c.z - z).magnitude < 1.3f) renderer.enabled = false;
                foreach (float z in new[] { 2f, 33f })
                    if (new Vector2(Mathf.Abs(c.x) - 18.7f, c.z - z).magnitude < 1.5f) renderer.enabled = false;
            }
            ParadeBanners(shell);
            ParadePassages(shell);
        }

        // The passages the teams walk out through (user, 11-08: "출구 쪽 벽도 자연스럽게 이어지게 색 입혀"): the team portal
        // tunnels (linings, rear arch, the pocket over the gate) and the rear passage's end walls and common exit arch were
        // the shell's flat cream / grey boxes. Each now draws A's stone brick (PR_A_StoneBrick, the gatehouse plinth's
        // stone) as a tiled copy of the same box (MarbleBlock: 2 m repeats, not stretched); its own picture stops
        // drawing, colliders stay.
        static readonly string[] ParadePassageParts = { "Portal tunnel lining", "Gate pocket cover", "Rear passage end wall", "Arch jamb block", "Arch voussoir" };
        static readonly string[] ParadePassageGroups = { "Team portal rear", "ENV_CommonExitArch" };
        const string StoneCopy = " (A stone)";

        static void ParadePassages(Transform shell)
        {
            var brick = Resources.Load<Material>(OwnMaterials + "PR_A_StoneBrick");
            if (brick == null) return;
            var parts = new List<Transform>();
            foreach (var t in shell.GetComponentsInChildren<Transform>(true))
            {
                if (System.Array.IndexOf(ParadePassageParts, t.name) < 0 || t.GetComponent<MeshRenderer>() == null) continue;
                bool archBlock = t.name.StartsWith("Arch ");
                if (archBlock && (t.parent == null || System.Array.IndexOf(ParadePassageGroups, t.parent.name) < 0)) continue;
                parts.Add(t);
            }
            foreach (var t in parts)
            {
                if (t.parent.Find(t.name + StoneCopy) == null)
                {
                    // the copy shares the box's place and turn; its size is the box's scale (the shell's groups are unscaled)
                    // plus 2 cm on every face: at the box's own size its faces shared planes with the passage floors' sides
                    // (the gate passage, the rear corridor) and flickered along the wall's foot (user, 11-08: "뒤쪽 벽
                    // 하단에 깜빡깜빡"); 2 cm out, those floor sides sit inside the wall
                    var copy = MarbleBlock(t.parent, t.name + StoneCopy, t.localPosition, t.localScale + Vector3.one * .04f, brick);
                    copy.transform.localRotation = t.localRotation;
                }
                t.GetComponent<MeshRenderer>().enabled = false;
            }
        }

        static void ParadeBuild(MiniGameSection owner, Transform content, Dictionary<string, Material> cache)
        {
            var court = new GameObject(ParadeName).transform;
            court.SetParent(content, false);

            Transform Place(string model, Vector3 at, float yaw, float scale = 1f, string name = null)
            {
                var asset = Load(ParadeFolder + model);
                if (asset == null) return null;
                var holder = new GameObject(name ?? model).transform;
                holder.SetParent(court, false);
                holder.localPosition = at;
                holder.localRotation = Quaternion.Euler(0f, yaw, 0f);
                holder.localScale = Vector3.one * scale;
                Hang(holder, RideName, asset, cache);
                return holder;
            }

            // The closed ceremonial gatehouse (17.6 m, x +-8.8) in the centre block, its towers' front on the board's edge.
            Place("A_Facade", new Vector3(0f, ParadeFacadeY, ParadeFacadeZ), 0f);
            ParadeGatehouseDress(court);
            // The shell's centre wall is hidden for the gatehouse, which is lower and narrower and open at the back.
            // Stone fills what it leaves (no gaps): a block behind the gatehouse (z 27.6..30, 6.2 m: under its parapet's
            // merlon gaps at ~5.9-6.7 m) closes its open back and the view through to the rear corridor, and a block from
            // each round tower's middle (tower x +-7.3, radius ~1.5, a_props_v1.py) into the team arch's inner pier
            // (x 8.85..9.75) closes the wedge between the two; its front (z 25.3) is inside both, so only stone shows.
            var brick = Resources.Load<Material>(OwnMaterials + "PR_A_StoneBrick");
            if (brick != null)
            {
                MarbleBlock(court, "Centre block back", new Vector3(0f, 3.1f, 28.8f), new Vector3(19.4f, 6.2f, 2.4f), brick);
                // The gatehouse's floor at the dais's level (top 1 cm under the dais, no shared plane): under the
                // gatehouse and the recess before its doors, from the board's edge back to the rear corridor. Its back stops
                // 4 cm short of the centre block's back (z 30): sharing that plane, the floor's 0.4 m side flickered through
                // the wall's foot in the rear corridor (user, 11-08 recording: "여기 깜깜 거려").
                var paving = Resources.Load<Material>(OwnMaterials + "PR_A_CourtPaving");
                MarbleBlock(court, "Gatehouse floor", new Vector3(0f, (ParadeFacadeY - .01f - .6f) * .5f, 26.99f), new Vector3(20f, ParadeFacadeY - .01f + .6f, 5.94f),
                    paving != null ? paving : brick);
                foreach (float s in new[] { -1f, 1f })
                {
                    // 6.3 m: under the tower's slate roof (eaves ~6.6 m), so the block never hides the roof
                    MarbleBlock(court, s < 0f ? "Tower-arch fill W" : "Tower-arch fill E", new Vector3(s * 9f, 3.15f, 26.5f), new Vector3(1.4f, 6.3f, 2.4f), brick);
                    MarbleBlock(court, s < 0f ? "North wall footing W" : "North wall footing E", new Vector3(s * 18.9f, -.27f, 27f), new Vector3(9.4f, .5f, 6.3f), brick);
                    // Footings: the section's floor stops at the walls' line, and past its edge there is nothing, which
                    // showed as a sky-coloured strip at the wall's foot (user, 11-07: "틈이 있어 아직"). A stone slab
                    // under each wall run, its top 2 cm under the floor (no shared plane), so whatever shows there is stone.
                    MarbleBlock(court, s < 0f ? "Side wall footing W" : "Side wall footing E", new Vector3(s * 23.2f, -.27f, 17f), new Vector3(2.6f, .5f, 39.4f), brick);
                    MarbleBlock(court, s < 0f ? "Entry wall footing W" : "Entry wall footing E", new Vector3(s * 14.45f, -.27f, -1.15f), new Vector3(16.3f, .5f, 2.5f), brick);
                }
            }
            const float dais = Arena.DaisHeight;
            foreach (float s in new[] { -1f, 1f })
            {
                // Team arches where the shell's portal arches were; the body runs back into the portal passage.
                Place("A_TeamArch", new Vector3(s * 12f, 0f, ParadeArchZ), 0f, 1f, s < 0f ? "Team arch W" : "Team arch E");
                // Side walls facing the room (the module's front is the holder's -z), the foot's face (a_props_v1.py
                // BASE_FRONT) ParadeFootIn inside the floor's edge x +-22, so the wall rises out of the floor with no
                // slit; every other module 1 cm further out and up, so overlaps never share a plane (R498's flicker rule).
                for (int i = 0; i < ParadeSideZ.Length; i++)
                {
                    float k = i & 1;
                    Place("A_Wall", new Vector3(s * (22f - ParadeFootIn + k * ParadeStagger.x), k * ParadeStagger.y, ParadeSideZ[i]), s < 0f ? -90f : 90f);
                }
                // Entry walls facing the room (foot just inside the entry band's edge z 0), and a second one back to back
                // facing out: the camera starts outside the entry and Tripo models have open backs (as D's entry wall).
                for (int i = 0; i < ParadeEntryX.Length; i++)
                {
                    float k = i & 1;
                    Place("A_Wall", new Vector3(s * ParadeEntryX[i], k * ParadeStagger.y, ParadeFootIn - k * ParadeStagger.x), 180f);
                    Place("A_Wall", new Vector3(s * ParadeEntryX[i], (1f - k) * ParadeStagger.y + .005f, -2.1f - k * ParadeStagger.x), 0f, 1f, "A_Wall outside");
                }
                // The north block's outer parts (x 13.8..23, beside the team arch): the shell's 11 m wall mass hid the
                // corner tower's roof (user, 11-07: "벽 때문에 지붕이 가려져"), so it gives way to the same 5 m wall as the
                // other sides, one facing the court (foot on the dais edge z 24) and one facing the rear corridor (z 30).
                // |x| 14.3..23.5: clear of the portal passage (x 9.75..14.25) and into the side wall.
                Place("A_Wall", new Vector3(s * 18.9f, 0f, ParadeFacadeZ - ParadeFootIn), 0f, 1f, "North wall front");
                Place("A_Wall", new Vector3(s * 18.9f, ParadeStagger.y, 30f + ParadeFootIn), 180f, 1f, "North wall rear");
                // Towers: one at each outer entry corner, one in each corner of the north block (x1.5, 12 m tall, its
                // slate roof well over the 5 m walls; standing 1 m out of the wall line as the shell's rook gatehouse did).
                Place("A_Tower", new Vector3(s * 23.2f, 0f, -1.2f), 0f, 1f, s < 0f ? "Entry tower W" : "Entry tower E");
                Place("A_Tower", new Vector3(s * 19f, 0f, 26.2f), 0f, 1.5f, s < 0f ? "North tower W" : "North tower E");
                // Entrance posts either side of the entry arch.
                Place("A_GatePost", new Vector3(s * 6.7f, 0f, .7f), 0f);
                // Cypresses on the dais along each side wall, between the parade flagpoles.
                foreach (float z in ParadeCypressZ) Place("A_Cypress", new Vector3(s * 21.1f, dais, z), 0f);
                // The garden edge's places: planters at the entry and rear corners, benches facing the room.
                Place("A_Planter", new Vector3(s * 20.2f, 0f, 1.9f), 0f);
                Place("A_Planter", new Vector3(s * 18.3f, 0f, 33.4f), 0f);
                Place("A_Bench", new Vector3(s * 15.6f, 0f, 1.1f), 0f);
                Place("A_Bench", new Vector3(s * 20.8f, 0f, 32.8f), s < 0f ? -90f : 90f);
                // Low garden walls along the rear corridor's back, in front of its end wall.
                for (int i = 0; i < ParadeGardenX.Length; i++)
                {
                    float k = i & 1;
                    Place("A_GardenWall", new Vector3(s * ParadeGardenX[i], k * ParadeStagger.y, 35.4f + k * ParadeStagger.x), 0f);
                }
            }
            // The painters' corner at the entry (where the R312 threshold props were): brush rack and the two paint
            // pots on the east, the rolled ceremonial carpet on the west.
            Place("A_PaintRack", new Vector3(10.2f, 0f, 1.6f), 0f);
            Place("A_PotWhite", new Vector3(8.4f, 0f, 1.2f), 0f);
            Place("A_PotBlack", new Vector3(8.8f, 0f, 2.4f), 0f);
            Place("A_Carpet", new Vector3(-9.6f, 0f, 1.6f), 0f);
        }

        // Each station: the team door (the game's TeamGate box, moved by the game) wears the user's oak leaf, and the
        // parade flagpoles get the brass pole; their banner becomes a swaying cloth. Runs on every Apply.
        static void ParadeStation(MissionStation station, Dictionary<string, Material> cache)
        {
            // A's own 25 / 50 / 75 % gate moment, no ring (PaintGateFx; user 11-07 via the C session)
            if (station.GetComponent<PaintGateFx>() == null) station.gameObject.AddComponent<PaintGateFx>();
            var door = station.Gate != null ? station.Gate.transform : null;
            // The door in its arch (user, 11-08: "문 위치가 동떨어져 있어", chose "아치 바로 안쪽으로"): the game puts it 3.4 m
            // into the tunnel (station z 23.2, section 27.2), behind the A_TeamArch body (section z 23.8..26.7). It moves
            // to just inside the arch's mouth (section 24.45, its face 0.45 m behind the arch's front) with its team
            // barrier the same 0.5 m behind it; rising when open it stays inside the arch's 10.7 m stone.
            if (door != null)
            {
                float dz = ParadeDoorZ - door.localPosition.z;
                if (Mathf.Abs(dz) > .001f)
                {
                    station.Gate.Shift(door.parent.TransformVector(new Vector3(0f, 0f, dz)));
                    foreach (var t in station.GetComponentsInChildren<Transform>(true))
                        if (t.name == "Team barrier") t.localPosition = new Vector3(t.localPosition.x, t.localPosition.y, ParadeDoorZ + .5f);
                }
            }
            var leaf = Load(ParadeFolder + "A_TeamDoor");
            if (door != null && leaf != null)
            {
                Hide(door.Find("DEC_PalaceGate"));
                if (door.Find(RideName + " holder") == null)
                {
                    // The leaf model is the game's door size (3.85 x 4.55 x 0.3 m), centred: undo the box's scale.
                    var at = new GameObject(RideName + " holder").transform;
                    at.SetParent(door, false);
                    var s = door.localScale;
                    at.localScale = new Vector3(1f / Mathf.Max(.0001f, Mathf.Abs(s.x)), 1f / Mathf.Max(.0001f, Mathf.Abs(s.y)), 1f / Mathf.Max(.0001f, Mathf.Abs(s.z)));
                    Hang(at, RideName, leaf, cache);
                }
            }
            if (station.Game == null) return;
            var pole = Load(ParadeFolder + "A_Flagpole");
            foreach (var t in station.Game.GetComponentsInChildren<Transform>(true))
            {
                if (t.name != "Parade flagpole") continue;
                foreach (Transform part in t)
                    if (part.name == "Flag stone base" || part.name == "Flagpole" || part.name == "Banner rod") Hide(part);
                if (pole != null && t.Find(RideName) == null) Hang(t, RideName, pole, cache);
                ParadeFlag(t, Resources.Load<Material>(OwnMaterials + "PR_A_CrownBanner"));
            }
        }

        // A_Flagpole's crossbar (a_props_v1.py export, measured): 1.44 m wide along the model's x, its underside 3.77 m up,
        // 0.08 m deep either side of the pole. User, 11-08: "깃발 위치 제대로 맞추고": the old code banner hung 0.62 m to
        // one side of the pole from the old pole's arm, half off the new crossbar, and edge-on to the court (the cloth
        // in the bar's plane, the bar running along the side walls). Now the flag turns to face the court (the board),
        // and the cloth hangs centred under the crossbar, just in front of the pole. Every Apply: the old cloth goes.
        const float FlagBarUnder = 3.77f, FlagClothWidth = 1.3f, FlagClothLength = 1.9f, FlagClothFront = .12f;

        static void ParadeFlag(Transform flag, Material cloth)
        {
            var section = flag.GetComponentInParent<MiniGameSection>(true);
            if (section != null)
            {
                float side = section.transform.InverseTransformPoint(flag.position).x;
                var face = section.transform.TransformDirection(new Vector3(-Mathf.Sign(side), 0f, 0f));
                flag.rotation = Quaternion.LookRotation(-face, section.transform.up);     // the cloth faces local -z
            }
            foreach (Transform t in flag)
                if (t.name.StartsWith("Banner border") || t.name.StartsWith("Banner pawn")) Hide(t);
                else if (t.name.StartsWith("Banner cloth"))
                {
                    var r = t.GetComponent<Renderer>();
                    if (r != null) r.enabled = false;
                    if (!t.name.Contains(Swapped)) t.name += Swapped;
                }
            var old = new List<GameObject>();
            foreach (var c in flag.GetComponentsInChildren<MiniGameBannerCloth>(true)) old.Add(c.gameObject);
            foreach (var go in old) if (Application.isPlaying) Object.Destroy(go); else Object.DestroyImmediate(go);
            if (cloth != null)
                MiniGameBannerCloth.Hang(flag, new Vector3(0f, FlagBarUnder, -FlagClothFront), FlagClothWidth, FlagClothLength, cloth);
        }

        // Stiff banner boxes -> swaying cloth (MiniGameBannerCloth), as D's Banners: the brass border strips and the
        // pawn emblem cannot sway with the cloth and stop drawing. Marks what it has done. `pick` chooses the cloth's
        // material by the box's local centre (R506: the crown and team banner textures, a_banner_tex_v1.py, which draw
        // the border and the emblem on the cloth itself); null or no material keeps the box's own.
        static void SwayBanners(Transform parent, System.Func<Vector3, Material> pick = null)
        {
            var cloths = new List<Transform>();
            foreach (Transform t in parent)
                if (t.name.StartsWith("Banner cloth") && !t.name.Contains(Swapped) && t.GetComponent<MiniGameBannerCloth>() == null) cloths.Add(t);
            foreach (var box in cloths)
            {
                var r = box.GetComponent<Renderer>();
                if (r == null) continue;
                Vector3 c = box.localPosition, size = box.localScale;
                foreach (Transform t in box.parent)
                    if ((t.name.StartsWith("Banner border") || t.name.StartsWith("Banner pawn")) &&
                        Mathf.Abs(t.localPosition.x - c.x) < size.x * .5f + .1f && Mathf.Abs(t.localPosition.y - c.y) < size.y * .5f + .1f)
                        Hide(t);
                var chosen = pick != null ? pick(c) : null;
                MiniGameBannerCloth.Hang(box.parent, new Vector3(c.x, c.y + size.y * .5f, c.z - size.z * .5f - .02f), size.x, size.y,
                    chosen != null ? chosen : r.sharedMaterial);
                r.enabled = false;
                box.name += Swapped;
            }
        }

        // The shell's own banners (the team banners over the portals, the rear corridor's): swaying, in place. The team
        // banners hang 0.6 m further out, in front of the new arches' face (arch front z 23.8, banner z 23.35).
        static void ParadeBanners(Transform shell)
        {
            var facade = Find(shell, "ENV_NorthFacade");
            if (facade != null)
                foreach (Transform t in facade)
                {
                    if (!t.name.StartsWith("Banner") || t.name.Contains(Done) || t.GetComponent<MiniGameBannerCloth>() != null) continue;
                    var p = t.localPosition;
                    if (Mathf.Abs(Mathf.Abs(p.x) - 12f) < 1.6f && p.y > 5.5f) { t.localPosition = p + new Vector3(0f, 0f, -.6f); t.name += Done; }
                }
            // The team banners (over the portals, x +-12, high) wear their team's pawn-shield banner (white team west: sky
            // blue), every other shell banner the cream crown banner. The shell sits at the section's origin.
            var crown = Resources.Load<Material>(OwnMaterials + "PR_A_CrownBanner");
            var white = Resources.Load<Material>(OwnMaterials + "PR_A_TeamBannerWhite");
            var black = Resources.Load<Material>(OwnMaterials + "PR_A_TeamBannerBlack");
            Material Pick(Vector3 c) => Mathf.Abs(Mathf.Abs(c.x) - 12f) < 1.6f && c.y > 5f ? (c.x < 0f ? white : black) : crown;
            var parents = new HashSet<Transform>();
            foreach (var t in shell.GetComponentsInChildren<Transform>(true))
                if (t.name.StartsWith("Banner cloth") && t.parent != null) parents.Add(t.parent);
            foreach (var p in parents) SwayBanners(p, Pick);
        }

        // The gatehouse's dressing from the concept (A_01_overview), made in code by the user's rules (11-07: no
        // emblems in the Tripo photos, hanging cloth is code): a cream crown banner either side of the arch on the
        // wall between the arch's frame and the tower (wall face 1.47 m behind the towers' front, a_props_v1.py
        // measure), and a matte brass crown standing on the middle of the parapet (merlons ~7.4 m) on a stone plinth.
        static void ParadeGatehouseDress(Transform court)
        {
            var root = new GameObject("Gatehouse dressing").transform;
            root.SetParent(court, false);
            root.localPosition = new Vector3(0f, ParadeFacadeY, 0f);      // heights below are the gatehouse's own
            var brass = Resources.Load<Material>(OwnMaterials + "PR_Late_Brass");
            var brick = Resources.Load<Material>(OwnMaterials + "PR_A_StoneBrick");
            var cloth = Resources.Load<Material>(OwnMaterials + "PR_A_CrownBannerLong");
            float wall = ParadeFacadeZ + 1.47f;
            foreach (float s in new[] { -1f, 1f })
            {
                if (cloth != null) MiniGameBannerCloth.Hang(root, new Vector3(s * 4.45f, 5.75f, wall - .06f), .9f, 2.6f, cloth);
                Prim(PrimitiveType.Cylinder, root, "Banner rod", new Vector3(s * 4.45f, 5.8f, wall - .06f), new Vector3(.07f, .6f, .07f), 90f, brass);
            }
            // The emblem on the parapet (user, 11-08: "로고 퀸으로 제대로 해"): the game's own chess queen (the piece model
            // in Resources/Pieces, reused rather than made again), matte brass, 2.6 m tall, on the stone plinth. The
            // primitive crown it replaces read as boxes and balls.
            var crown = new GameObject("Gate queen").transform;
            crown.SetParent(root, false);
            crown.localPosition = new Vector3(0f, 7.42f, wall + .25f);
            if (brick != null) MarbleBlock(crown, "Crown plinth", new Vector3(0f, .15f, 0f), new Vector3(2.9f, .3f, 1.1f), brick);
            var queen = Resources.Load<GameObject>(QueenModel);
            if (queen == null) { Debug.LogWarning("[미니게임 A] 퀸 모델이 없습니다: Resources/" + QueenModel); return; }
            var copy = Object.Instantiate(queen, crown, false);
            copy.name = "Queen emblem";
            foreach (var c in copy.GetComponentsInChildren<Collider>(true))
                if (Application.isPlaying) Object.Destroy(c); else Object.DestroyImmediate(c);
            var renderers = copy.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0) return;
            foreach (var r in renderers)
            {
                var slots = r.sharedMaterials;
                for (int i = 0; i < slots.Length; i++) slots[i] = brass;
                r.sharedMaterials = slots;
            }
            // fit: 2.6 m tall, its foot on the plinth's top, centred on the plinth
            var bounds = renderers[0].bounds;
            foreach (var r in renderers) bounds.Encapsulate(r.bounds);
            float k = QueenHeight / Mathf.Max(.01f, bounds.size.y);
            copy.transform.localScale *= k;
            bounds = renderers[0].bounds;
            foreach (var r in renderers) bounds.Encapsulate(r.bounds);
            var foot = crown.InverseTransformPoint(new Vector3(bounds.center.x, bounds.min.y, bounds.center.z));
            copy.transform.localPosition -= foot - new Vector3(0f, .3f, 0f);
        }

        const string QueenModel = "Pieces/PR_Piece_Queen";
        const float QueenHeight = 2.6f;

        // A primitive without its collider (pictures only), turned `roll` degrees about local z.
        static GameObject Prim(PrimitiveType type, Transform parent, string name, Vector3 at, Vector3 scale, float roll, Material material)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            var collider = go.GetComponent<Collider>();
            if (Application.isPlaying) Object.Destroy(collider); else Object.DestroyImmediate(collider);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = at;
            go.transform.localRotation = Quaternion.Euler(0f, 0f, roll);
            go.transform.localScale = scale;
            if (material != null) go.GetComponent<Renderer>().sharedMaterial = material;
            return go;
        }

        // Floors: the court's cream flagstones (R506 swatch PR_A_CourtPaving, 2 m a tile) on the entry band, the rear
        // corridor, the shell's paving modules, the aisle under and in front of the board and each station's dais top,
        // ramp and gate passage, repeated by each box's size so the stones keep their size. The board's squares are
        // dressed by PaintBoard itself (R506 slate, ivory limestone and basalt). Colliders stay.
        static void ParadeFloors(MiniGameSection owner, Transform content)
        {
            var stone = Resources.Load<Material>(OwnMaterials + "PR_A_CourtPaving");
            if (stone == null) return;
            void Lay(Transform t)
            {
                if (t == null) return;
                var r = t.GetComponent<Renderer>();
                if (r == null) return;
                r.sharedMaterial = stone;
                var size = t.lossyScale;
                var block = new MaterialPropertyBlock();
                r.GetPropertyBlock(block);
                block.SetVector(BaseMapST, new Vector4(Mathf.Abs(size.x) / 2f, Mathf.Abs(size.z) / 2f, 0f, 0f));
                r.SetPropertyBlock(block);
            }
            var shell = content.Find(MiniGamePalaceArt.ShellName);
            if (shell != null)
                foreach (var r in shell.GetComponentsInChildren<Renderer>(true))
                    if (r.name == "Paving module") Lay(r.transform);
            foreach (var name in new[] { "Entry band", "Rear corridor", "Central aisle" }) Lay(content.Find(name));
            foreach (var t in content.GetComponentsInChildren<Transform>(true))
                if (t.name == "Aisle dais") Lay(t);
            foreach (var station in owner.Stations)
            {
                if (station == null) continue;
                foreach (var t in station.GetComponentsInChildren<Transform>(true))
                    if (t.name == "Station floor" || t.name == "Gate passage" || t.name == "Palace station ramp") Lay(t);
            }
        }
    }
}
