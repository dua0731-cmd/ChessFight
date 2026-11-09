using System.Collections.Generic;
using ChessFight.Gameplay;
using ChessFight.Network;
using UnityEngine;

namespace ChessFight.PawnRush
{
    // C, the drawbridge on one heavy rope (mini-games v1.0 §9): a winch by the moat (u 11.3) lets out a thick rope
    // south with four gold knots; pawns of the team grip a knot (GrabHandle) and walk back, and 7 m of rope lowers
    // the bridge over the moat (u 12..16). Pull F is one a pawn, 1.8 sprinting; the other team's pawns gripping a
    // knot and walking north hold back 0.5 each; the rope runs at DrawbridgeRope.Speed(F, E). A ratchet keeps
    // every centimetre: letting go stops the rope, it never winds back.
    //
    // Physics and the network (§1.2, §2.2): the knots are kinematic bodies moved along the rope by the host's
    // length, so a hand that grips one is dragged by it (and a pawn that walks off further than 1.3 m is pulled
    // back to it); nothing is pushed by physics. The host sends the length (2 bytes), the net pull, the ratchet
    // and which knots are pulled sprinting or held against (5 bytes); a client eases the knots, the deck and the
    // drum to the length and plays the clicks, creaks and dust from those numbers.
    public sealed class MgDrawbridge : MiniGameBase
    {
        public const float Moat0 = 12f, Moat1 = 16f, Hinge = 15.95f, Deck = 4.1f, DeckWidth = 4f;
        public const float Winch = 11.3f, DrumHeight = 1.3f, DrumRadius = .55f, RopeHeight = .8f, RopeStart = 10.9f;
        public const float FirstKnot = 10.2f, KnotGap = 1f, Tail = .8f, LeashLength = 1.3f, LeashPull = 8f, LeashMax = 3f;
        public const int Knots = 4;
        // R502 palace layout (the R311 concept): lane 1 m inside the bridge, the winch 3.1 m outside the lane at u 1.4,
        // the rope from the moat's edge (u 11.7) to the lane's south end, pylons 3.7 m (the art's rook pylon is 3.3 m).
        public const float PalaceLaneInset = 1f, PalaceWinchAside = 3.1f, PalaceWinchU = 1.4f, PalaceRopeNorth = 11.3f;
        public const float PalaceTowerTop = 3.7f;
        // R502 (user 11-07 "물 들어갈 공간 좀만 넓혀"): the palace moat is 5 m (u 11.5..16.5), the deck 5.1 m to span it.
        public const float PalaceMoat0 = 11.5f, PalaceMoat1 = 16.5f;
        // R502 (user 11-07, "앞에다 두고 지금은 밀어야 되잖아 당겨서 푸는 방식으로"): with the winch at the front the rope is
        // pulled away from it, north toward the moat, unwinding it from the drum. The knots start by the winch
        // (lead knot u 2.9, 0.8 m apart) and travel north with the metres pulled; the lead knot ends at u 9.9, so the
        // pawns holding on stay clear of the moat (u 11.5).
        public const float PalaceKnot0 = 2.9f, PalaceKnotGap = .8f;
        // R502 (user 11-07, "컨셉 아트를 보고 그거대로", then "줄 잡아 당기는 걸 앞에다 놔 ... l l 이런식으로", the pulley picked):
        // the winch at the front; the rope runs from the drum's top north along a two-plate iron track to a pulley on a
        // stone post by the moat, across the post to a second pulley and back south beside the track - two parallel
        // lines. All of it stands outside the bridge (x 10..14 from the middle). User 11-07 "왼쪽에 있는건 오른쪽 오른쪽에
        // 있는 건 왼쪽에다가": the returning line with the knots runs on the bridge's side (x 16), the track and the winch
        // outside it (x 18.4), the line clear of the winch's 3.2 m frame (pulled all the way the knots stay beside it). The pawns grab the knots on the returning line and walk south, toward the winch. The lead knot (the
        // southern one) starts at u 8.3, the knots 0.8 m apart behind it; 7 m pulled brings it to u 1.3.
        // User 11-08 (a sketch: "원리는 그대로 하고 위치만 이렇게"): the same winch, track, pulley post and returning line,
        // moved in beside the central aisle - the winch 3.8 m from the section's middle (its frame just clear of the
        // aisle's curb at 2), the returning line 2.4 m further out (6.2), clear of the bridge (10..14).
        public const float PalaceWinchFromMiddle = 3.8f, PalacePulleyAside = 2.4f, PalaceKnotU = 8.3f;
        public const float PalaceAnchorBack = .5f;     // the pulley post's south face, this far short of the moat (c_drawbridge_v2.py ANCHOR_Y)
        public const float PalacePulleyU = .08f;       // the pulleys' rope line, this far south of the post's face
        public const float PalacePylonX = 3.4f, PalacePylonHalf = 1.1f;
        public const float PalacePylonBack = 1.45f;   // the pylons' middle behind the moat's north edge (c_drawbridge_v2.py)   // the pylons' middle from the bridge's, the shaft's half width
        public const float PalaceCrownFloor = 2.81f;   // the pylon crown's middle floor (c_drawbridge_v2.py, the kit pylon at 3.3 m), the chain's eye

        [SerializeField] MissionStation station;
        [Tooltip("The hinge on the far bank; the deck turns about its x axis.")]
        [SerializeField] Rigidbody hinge;
        [SerializeField] Rigidbody[] knotBodies = new Rigidbody[Knots];
        [SerializeField] GrabHandle[] knots = new GrabHandle[Knots];
        [SerializeField] Transform rope, drum, pawl, deckLook;
        [SerializeField] Renderer[] lamps = new Renderer[3];
        [SerializeField] LineRenderer[] chains = new LineRenderer[2];
        [SerializeField] float ropeOffsetX;
        [SerializeField] Vector3 winchAt = new Vector3(0f, DrumHeight, Winch);   // the drum's centre (game frame)
        [SerializeField] bool palaceFront;
        [SerializeField] Vector3 pullDir = Vector3.back;      // the way the pawns walk to pull (game frame)
        [SerializeField] float pullX;                          // palace: the returning (pulled) line's x
        [SerializeField] float moat0 = Moat0, deckLen = Deck, towerU = 16.2f;

        float pulled;            // metres of rope pulled (host), or the host's (client)
        float shown;             // what is drawn: the host's own, a client's eased
        float net;               // F - E
        int knotBits;            // knots pulled sprinting (bits 0-3) and held against (bits 4-7)
        int clicks;              // whole metres pulled, for the ratchet's click
        float pawlKick, slamAt = -1f;
        readonly float[] strainAt = new float[Knots];
        // R502 (user 11-08, "줄이 처음 부터 이렇게 고정 되어 있으면 이상하잖아 바닥에 늘어져 있는데 자연스렙게 잡을 수 있게",
        // "좀 더 자연스럽게"): the returning rope lies slack on the floor until it is gripped or hauled; the rope's
        // tautness round each knot, eased (slow start and end), 0 = sagging and wandering on the floor, 1 = drawn
        // straight: it tightens in 0.3 s and slackens over 0.8 s once let go (RopeLine draws the sag from it). Worked out
        // the same on every PC from what each sees (Lift, every physics step).
        readonly float[] shownLift = new float[Knots];
        public const float PalaceKnotRest = .16f;          // a resting knot's middle: its 0.3 m box just clear of the floor
        public float LiftSum { get; private set; }
        // User 11-08 ("잡으면 기물이 올라가 버려", "바닥에서 끌리면 안돼", "아직도 이렇게 잡으면 붕 뜨는데", "아직도 떠"): a
        // kinematic knot counts as an environment grip (RagdollPawn.IsEnvironmentGrip), which switches the pawn's launch
        // guard off (GuardLaunch), and a kinematic body moving up or pressing on the pawn has no weight to give - so any
        // rise of a gripped knot, or its box against the pawn's chest, floated the pawn, whatever height it was carried
        // to. On the host a palace knot is therefore a light (KnotMass) weightless body: the game still sets where it is
        // along the rope (x, z, and its height while nobody holds it), but a held knot's height is left to the hands -
        // the pawn lifts it to where its holding arm puts it (forward, a little above the shoulder: RagdollPawn.ReachPose)
        // and nothing can lift the pawn. The other knots lie on the taut line from the second pulley through the held
        // ones while the rope is gripped or hauled (the free end past the last grip droops and stays off the floor),
        // or rest on the floor. Another PC, whose pawns are drawn, not simulated, keeps the knots kinematic and puts a
        // knot with a hand within 0.35 m of it at that hand.
        public const float KnotMass = .6f;
        const float FreeMin = .2f, Droop = .3f, GripNear = .35f;
        // User 11-08 ("그냥 폰이 다가가면 자동으로 올라가게 하자"): a knot rises off the floor to a pawn's hands
        // (ApproachHeight) in 0.3 s when a pawn comes within KnotNear of it, drawing its neighbours half-way with it,
        // and settles back over 0.8 s when nobody is near - so it is gripped at hand height and never has to rise in
        // a hand.
        public const float KnotNear = 1.3f, ApproachHeight = .42f;
        readonly float[] near01 = new float[Knots];
        // User 11-08 ("손잡이랑 줄이 같이 없고 줄이 잘 안잡혀"): a light knot left loose all the time was shoved off the
        // rope by the pawns' bodies and hands, and away from a reaching hand. Now only a gripped knot is the light free
        // body (Loosen); a free one is kinematic again and stays exactly on the rope (Tighten). And the pawns' arms,
        // hands, chest and legs never touch the knots (Physics.IgnoreCollision, renewed every second for pawns that came
        // or came back): a hand takes hold by being near a knot (PawnHand looks with an overlap query), not by touching
        // it, so a reaching arm no longer pushes it, nor the knot the pawn. Only the hips' first collider keeps touching
        // them: a hand ignores anything that one passes through (RagdollPawn.PassesThrough).
        float ignoreClock;
        readonly HashSet<Object> unhit = new HashSet<Object>();
        readonly float[] knotY = new float[Knots];       // each knot's height now (game frame)
        bool knotYReady;
        float taut;                                       // 0 slack on the floor .. 1 drawn taut
        readonly int[] gripCount = new int[Knots];
        readonly float[] gripY = new float[Knots];       // another PC: the gripping hands' heights, summed
        float dustAt;
        AudioSource creak, winch;
        float hum;               // the winch and rope loops' level, 0..1: eased in and out (user 11-08)
        // R519 (the user's list, 11-09, 1: "도개교 미니게임에서 끼기익거리는 사운드 음량 살짝 낮추기"): the two squeaky
        // sounds - the rope's creak loop while it runs (most of it at 0.8 to 3 kHz) and a held knot's strain every 2 s
        // (most at 1.5 to 6 kHz) - 3 dB lower (x 0.7, were 0.6 and 0.7). The winch's low rumble, the ratchet's click
        // and the deck's slam as before.
        const float CreakVolume = .42f, StrainVolume = .5f, WinchVolume = .6f;
        Material ropeMat, ropeHeld, lampOn, lampOff;
        bool slammed;

        public float Pulled => pulled;

        public static MgDrawbridge Build(CourseBuilder b, MissionStation station)
        {
            var k = b.kit;
            var V = (System.Func<float, float, float, Vector3>)MissionGames.V;
            // the moat, the hinge on its far edge and the deck spanning it (palace: the wider R502 moat)
            float Moat0 = station.PalaceStyle ? PalaceMoat0 : MgDrawbridge.Moat0, Moat1 = station.PalaceStyle ? PalaceMoat1 : MgDrawbridge.Moat1;
            // palace: the pylons stand wholly on the north bank (their 2.7 m crown's front 0.05 m behind the moat, user
            // 11-08 "기둥이 바닥 위에 있게 뒤로"), not 0.9 m out over the water; the chains run from there (ChainEnds)
            float Hinge = Moat1 - .05f, Deck = Moat1 - Moat0 + .1f, Tower = Moat1 + (station.PalaceStyle ? PalacePylonBack : .2f);
            MissionGames.Floor(b, 0f, Moat0);
            MissionGames.Floor(b, Moat1, 20f);
            // The moat: water at -0.9, its bottom at -1.6, walls to the floor; the ends closed down to the plaza
            // floor. Falling in counts as a fall (back to the plaza's checkpoint).
            var stone = Arena.Lit("M_MoatStone", Arena.Hex("#6E6A64"), 0f, .9f);
            // The S and N walls lie inside the floors' edges: their pictures stand off the floors' faces (R498).
            MissionGames.LinedWall(b, "Moat wall S", V(-10f, -1.6f, Moat0 - .2f), V(10f, 0f, Moat0), stone, 1f);
            MissionGames.LinedWall(b, "Moat wall N", V(-10f, -1.6f, Moat1), V(10f, 0f, Moat1 + .2f), stone, -1f);
            b.Box("Moat wall W", V(-10f, -1.6f, Moat0), V(-9.8f, -Arena.DaisHeight, Moat1), stone, true);
            b.Box("Moat wall E", V(9.8f, -1.6f, Moat0), V(10f, -Arena.DaisHeight, Moat1), stone, true);
            b.Box("Moat bottom", V(-10f, -2.6f, Moat0), V(10f, -1.6f, Moat1), stone, true);
            var moatWater = b.Box("Water", V(-9.8f, -.95f, Moat0), V(9.8f, -.9f, Moat1), k.glass, false, false);
            if (MapLook.Water != null) moatWater.GetComponent<Renderer>().sharedMaterial = MapLook.Water; // real water (10-22)
            b.Trigger<KillVolume>("Moat (fall)", V(-10f, -1.6f, Moat0), V(10f, -.7f, Moat1));

            // The bridge: 4 x 4.1 m of walnut with planks across, hinged at u 15.95, standing up until lowered.
            var hingeGo = new GameObject("Drawbridge hinge");
            hingeGo.transform.SetParent(b.parent, false);
            hingeGo.transform.localPosition = V(0f, 0f, Hinge);
            var body = hingeGo.AddComponent<Rigidbody>();
            body.isKinematic = true;
            body.useGravity = false;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            var before = b.parent;
            b.parent = hingeGo.transform;
            float hw = DeckWidth * .5f;
            var deck = b.Box("Deck", V(-hw, -.25f, -Deck), V(hw, 0f, 0f), Arena.Wood(false), true);
            deck.AddComponent<CourseFloor>();
            var look = new GameObject("Deck look").transform;
            look.SetParent(hingeGo.transform, false);
            for (int i = 0; i < Mathf.RoundToInt((Deck - .2f) / .52f); i++)
            {
                float z = -Deck + .25f + i * .52f;
                Arena.Picture(look, "Plank line", V(-hw, 0f, z - .02f), V(hw, .01f, z + .02f), Arena.Wood(true));
            }
            b.parent = before;
            hingeGo.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            var light = Arena.Lit("M_LightStone", Arena.Hex("#D8D0C2"), 0f, .8f);
            float towerTop = station.PalaceStyle ? PalaceTowerTop : 6f;
            // palace: the kit's rook pylons (c_drawbridge_v2.py, 2.2 m shaft, 2.7 m crown) stand just outside the 4 m
            // deck, their collision the shaft's size (user 11-07 "기둥 쪽 삐뚤어졌어 다시맞춰": squared and moved out of the walk)
            float towerX = station.PalaceStyle ? PalacePylonX : 2.4f, towerHalf = station.PalaceStyle ? PalacePylonHalf : .35f;
            foreach (float side in new[] { -1f, 1f })
                b.Box("Bridge tower", V(side * towerX - towerHalf, 0f, Tower - towerHalf), V(side * towerX + towerHalf, towerTop, Tower + towerHalf), light, true);

            // The winch: a walnut frame, the drum across it (axis along v), gold teeth and the pawl at its east end,
            // three latch lamps on the east post.
            // Palace sections (R502, the R311 concept, user 11-07 "그림처럼 앞으로 옮기기"): the rope lane runs just
            // inside the bridge (1 m toward the section's middle) and the winch stands at the lane's south end, outside
            // it, near the entrance; the knots, the pull and the ratchet are as before. The handles and rope move with
            // the lane, rather than decorating one place while judging a pull somewhere else.
            float postSide = station.PalaceStyle && !station.Mirrored ? -1f : 1f;      // the station's outer side
            // palace: the station's middle is 12 m out from the section's; the aisle is on its -postSide... side
            float winchX = station.PalaceStyle ? -postSide * (12f - PalaceWinchFromMiddle) : 0f;
            float ropeOffset = station.PalaceStyle ? winchX + postSide * PalacePulleyAside : 0f;   // palace: the returning (pulled) line, further out
            float winchU = station.PalaceStyle ? PalaceWinchU : Winch;
            var winchParent = b.parent;
            Transform laneParent = b.parent;
            if (station.PalaceStyle)
            {
                laneParent = b.Group("Rope lane look");
                laneParent.localPosition = Vector3.right * ropeOffset;
                b.parent = b.Group("Winch assembly");
                b.parent.localPosition = new Vector3(winchX, 0f, winchU - Winch);   // the parts below are drawn at u = Winch
            }
            b.Box("Winch frame", V(-1.6f, 0f, Winch - .5f), V(1.6f, 1f, Winch + .5f), Arena.Wood(false), true);
            var drumGo = new GameObject("Drum").transform;
            drumGo.SetParent(b.parent, false);
            drumGo.localPosition = V(0f, DrumHeight, Winch);
            var drumBody = MiniGameParts.Part(drumGo, PrimitiveType.Cylinder, Vector3.zero, new Vector3(DrumRadius * 2f, 1.1f, DrumRadius * 2f), Arena.Wood(true));
            drumBody.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
            var gold = Arena.Emissive(GrabHandle.Gold, .4f);
            for (int i = 0; i < 8; i++)
            {
                float a = i * 45f;
                var tooth = MiniGameParts.Part(drumGo, PrimitiveType.Cube, Quaternion.Euler(a, 0f, 0f) * new Vector3(1.12f, DrumRadius + .06f, 0f), new Vector3(.08f, .14f, .12f), gold);
                tooth.transform.localRotation = Quaternion.Euler(a, 0f, 0f);
            }
            var pawlGo = new GameObject("Pawl").transform;
            pawlGo.SetParent(b.parent, false);
            pawlGo.localPosition = V(postSide * 1.2f, DrumHeight + DrumRadius + .3f, Winch - .2f);
            MiniGameParts.Part(pawlGo, PrimitiveType.Cube, new Vector3(0f, -.15f, 0f), new Vector3(.12f, .6f, .12f), gold);
            b.Box("Winch post E", V(postSide * 1.9f, 0f, Winch - .1f), V(postSide * 2.1f, 2.5f, Winch + .1f), Arena.Wood(false), true);
            var lampOff = Arena.Lit("M_LampOff", new Color(.3f, .27f, .2f), 0f, .6f);
            var lamps = new Renderer[3];
            for (int i = 0; i < 3; i++)
                lamps[i] = MiniGameParts.Part(b.parent, PrimitiveType.Sphere, V(postSide * 2f, 1f + i * .6f, Winch - .16f), Vector3.one * .16f, lampOff).GetComponent<Renderer>();

            // The rope lane: dark slate, a gold tick every metre from u 10.2 down to 3.2, arrows pointing south.
            Arena.Picture(laneParent, "Rope lane", V(-1.2f, 0f, .2f), V(1.2f, .015f, RopeStart), Arena.Lit("M_RopeLane", Arena.Hex("#565C6B"), 0f, .8f));
            var tick = Arena.Emissive(GrabHandle.Gold, .5f);
            for (int i = 0; i < 8; i++)
            {
                float u = station.PalaceStyle ? PalaceKnotU - i : FirstKnot - i;
                Arena.Picture(laneParent, "Lane tick", V(-1.2f, .015f, u - .03f), V(1.2f, .025f, u + .03f), tick);
            }
            for (int i = 0; i < 4; i++)
            {
                float u = 9f - i * 2.2f;
                foreach (float side in new[] { -1f, 1f })
                {
                    var arm = Arena.Picture(laneParent, "Lane arrow", V(-.03f, .015f, -.22f), V(.03f, .025f, .22f), tick);
                    arm.transform.localPosition = V(-1.05f + side * .14f, .02f, u + .14f);
                    arm.transform.localRotation = Quaternion.Euler(0f, side * -40f, 0f);
                }
            }

            b.parent = winchParent;

            var game = b.parent.gameObject.AddComponent<MgDrawbridge>();
            game.station = station;
            game.ropeOffsetX = ropeOffset;
            game.winchAt = new Vector3(winchX, DrumHeight, winchU);
            game.pullDir = Vector3.back;                                        // both: the pawns walk south to pull
            game.pullX = ropeOffset;                                     // outside the winch, clear of its frame and the bridge
            game.palaceFront = station.PalaceStyle;
            game.moat0 = Moat0;
            game.deckLen = Deck;
            game.towerU = Tower;
            game.hinge = body;
            game.drum = drumGo;
            game.pawl = pawlGo;
            game.deckLook = look;
            game.lamps = lamps;
            game.lampOff = lampOff;
            game.lampOn = Arena.Emissive(Arena.Gold, 1f);
            // The rope (a picture from the drum to its tail) and the knots on it.
            game.ropeMat = Arena.Lit("M_Rope", Arena.Hex("#C9A57A"), 0f, .9f);
            game.ropeHeld = Arena.Lit("M_RopeHeld", station.Accent(station.Team == Teams.White ? Teams.Black : Teams.White), .4f, .7f);
            game.rope = MiniGameParts.Part(b.parent, PrimitiveType.Cylinder, Vector3.zero, Vector3.one, game.ropeMat).transform;
            game.rope.name = "Rope";
            if (station.PalaceStyle)
            {
                // the front winch: a short rope from the lane's south end across to the drum (fixed; the lane's rope moves)
                var a = new Vector3(ropeOffset, RopeHeight, winchU + .3f);
                var z = game.winchAt + Vector3.up * DrumRadius;
                var feed = MiniGameParts.Part(b.parent, PrimitiveType.Cylinder, (a + z) * .5f, Vector3.one, game.ropeMat).transform;
                feed.name = "Rope to drum";
                feed.localRotation = Quaternion.FromToRotation(Vector3.up, z - a);
                feed.localScale = new Vector3(.14f, (z - a).magnitude * .5f, .14f);
            }
            for (int i = 0; i < Knots; i++)
            {
                var h = MiniGameParts.Handle(b.parent, "Knot " + (i + 1), (station.PalaceStyle ? V(ropeOffset, RopeHeight, PalaceKnotU + i * PalaceKnotGap) : V(ropeOffset, RopeHeight, KnotU(0f, i))), new Vector3(.3f, .3f, .14f), game, (byte)i, GrabHandle.HandleKind.Pull);
                game.knots[i] = h;
                game.knotBodies[i] = h.GetComponent<Rigidbody>();
            }
            for (int i = 0; i < 2; i++)
            {
                var c = new GameObject("Chain").AddComponent<LineRenderer>();
                c.transform.SetParent(b.parent, false);
                c.useWorldSpace = !station.PalaceStyle;
                c.positionCount = 2;                // palace: deck end to the pylon's top (the winch is no longer by the moat)
                c.widthMultiplier = .05f;
                c.sharedMaterial = Arena.Lit("M_Chain", new Color(.45f, .45f, .47f), 0f, .4f);
                game.chains[i] = c;
            }
            game.Pose(0f);
            game.RopePose(0f, 0f);
            game.ChainPose();
            return game;
        }

        // Knot k's u when `metres` of rope have been pulled.
        public static float KnotU(float metres, int k) => FirstKnot - metres - k * KnotGap;

        // Knot k's position (game frame) when `metres` have been pulled (palace: on the returning line, pulled south
        // toward the front winch; knot 0 is the southern one).
        Vector3 KnotPos(float metres, int k)
        {
            if (!palaceFront) return new Vector3(ropeOffsetX, RopeHeight, KnotU(metres, k));
            float z = KnotZ(metres, k);
            // its height: Lift (a gripped knot at the waist, the others on the taut line, slack ones resting)
            return new Vector3(pullX, knotYReady ? knotY[k] : RestY(z), z);
        }

        float PulleyU => moat0 - PalaceAnchorBack - PalacePulleyU;

        Vector3 World(Vector3 local) => transform.TransformPoint(local);

        void FixedUpdate()
        {
            if (station == null) return;
            if (!Completed && !Remote) Pull();
            if (Remote) shown = Mathf.MoveTowards(shown, pulled, Time.fixedDeltaTime * .6f);
            else shown = pulled;
            if (palaceFront) Lift();
            Pose(shown);
        }

        void Lift()
        {
            float dt = Time.fixedDeltaTime;
            if (!knotYReady)
            {
                for (int k = 0; k < Knots; k++) knotY[k] = RestY(KnotZ(shown, k));
                knotYReady = true;
            }
            ignoreClock -= dt;
            if (ignoreClock <= 0f)
            {
                ignoreClock = 1f;
                unhit.Clear();
            }
            foreach (var p in station.Pawns())
                if (p.Driver is Component body && unhit.Add(body))
                {
                    // all but the hips' first collider: a hand will not take hold of anything that one passes
                    // through (RagdollPawn.PassesThrough reads ownColliders[0], the hips')
                    var hips = p.Driver.FollowTarget;
                    var keep = hips != null ? hips.GetComponentInChildren<Collider>(true) : null;
                    foreach (var c in body.GetComponentsInChildren<Collider>(true))
                    {
                        if (c == keep) continue;
                        for (int k = 0; k < Knots; k++)
                        {
                            var kc = knots[k] != null ? knots[k].GetComponent<Collider>() : null;
                            if (kc != null && c != kc) Physics.IgnoreCollision(c, kc, true);
                        }
                    }
                }
            // 1. the grips: which knots are held (and, on another PC, how high the hands on them are)
            for (int k = 0; k < Knots; k++) { gripCount[k] = 0; gripY[k] = 0f; }
            foreach (var p in station.Pawns())
            {
                var mg = p.Driver as IMiniGamePawn;
                if (mg == null) continue;
                for (int h = 0; h < 2; h++)
                {
                    int k = GripKnot(mg, h);
                    if (k < 0) continue;
                    gripCount[k]++;
                    gripY[k] += transform.InverseTransformPoint(mg.HandPosition(h)).y;
                }
            }
            bool anyGrip = false;
            for (int k = 0; k < Knots; k++) anyGrip |= gripCount[k] > 0;
            // a pawn near a knot: it rises to meet the hands
            for (int k = 0; k < Knots; k++)
            {
                bool near = false;
                if (knots[k] != null)
                {
                    var at = knots[k].transform.position;
                    foreach (var p in station.Pawns())
                    {
                        var hips = p.Driver != null ? p.Driver.FollowTarget : null;
                        if (hips == null) continue;
                        var d = hips.position - at;
                        d.y = 0f;
                        if (d.sqrMagnitude < KnotNear * KnotNear) { near = true; break; }
                    }
                }
                near01[k] = Mathf.MoveTowards(near01[k], near ? 1f : 0f, dt / (near ? .3f : .8f));
            }
            bool hauled = Mathf.Abs(net) > .05f && !Completed;
            taut = Mathf.MoveTowards(taut, hauled || anyGrip ? 1f : 0f, dt / (hauled || anyGrip ? .3f : .8f));
            float s01 = Mathf.SmoothStep(0f, 1f, taut);
            // 2. a held knot is where the hands carry it: its own body on the host, the hands on another PC
            for (int k = 0; k < Knots; k++)
            {
                if (gripCount[k] == 0) continue;
                var rb = knotBodies[k];
                if (!Remote && rb != null && !rb.isKinematic) knotY[k] = transform.InverseTransformPoint(rb.position).y;
                else knotY[k] = Mathf.MoveTowards(knotY[k], gripY[k] / gripCount[k], 3f * dt);
            }
            // 3. the others: on the taut line between the pulley and the held knots, drooping past the last one, or
            // (slack) resting on the floor
            float sum = s01;
            for (int k = 0; k < Knots; k++)
            {
                shownLift[k] = s01;
                if (gripCount[k] > 0) { sum += knotY[k]; continue; }
                float z = KnotZ(shown, k);
                float zN = PulleyU, yN = RopeHeight;                     // the nearest support toward the pulley
                for (int n = k + 1; n < Knots; n++)
                    if (gripCount[n] > 0) { zN = KnotZ(shown, n); yN = knotY[n]; break; }
                int sIdx = -1;                                            // and toward the tail
                for (int n = k - 1; n >= 0; n--)
                    if (gripCount[n] > 0) { sIdx = n; break; }
                float line = sIdx >= 0
                    ? Mathf.Lerp(yN, knotY[sIdx], Mathf.InverseLerp(zN, KnotZ(shown, sIdx), z))
                    : Mathf.Max(FreeMin, yN - Droop * Mathf.Max(0f, zN - z));
                float target = Mathf.Lerp(RestY(z), Mathf.Max(PalaceKnotRest, line), s01);
                // lifted to meet a pawn coming near it, or half-way by one coming near its neighbour
                float meet = Mathf.SmoothStep(0f, 1f, near01[k]);
                if (k > 0) meet = Mathf.Max(meet, .45f * Mathf.SmoothStep(0f, 1f, near01[k - 1]));
                if (k < Knots - 1) meet = Mathf.Max(meet, .45f * Mathf.SmoothStep(0f, 1f, near01[k + 1]));
                target = Mathf.Max(target, Mathf.Lerp(RestY(z), ApproachHeight, meet));
                knotY[k] = Mathf.MoveTowards(knotY[k], target, 1.5f * dt);
                sum += knotY[k];
            }
            LiftSum = sum;
        }

        // A gripped knot on the host: light and weightless, free to rise and fall in a hand, carried along the rope by
        // a hidden kinematic carrier the game moves (MovePosition, so the move has a speed) through a joint that locks
        // the knot to it across and along the rope and leaves only up and down free. User 11-08 ("잡고 땅기면 폰이
        // 미끄러져"): teleporting the knot along each step instead gave the grip joint a jump to make up every step,
        // which it did by dragging the pawn's hand, and the pawn slid over the floor.
        readonly Rigidbody[] carriers = new Rigidbody[Knots];

        void Loosen(int i)
        {
            var rb = knotBodies[i];
            if (carriers[i] == null)
            {
                var go = new GameObject("Knot carrier " + (i + 1));
                go.transform.SetParent(transform, false);
                var c = go.AddComponent<Rigidbody>();
                c.isKinematic = true;
                c.useGravity = false;
                carriers[i] = c;
            }
            carriers[i].position = rb.position;
            carriers[i].rotation = rb.rotation;
            rb.isKinematic = false;
            rb.useGravity = false;
            rb.mass = KnotMass;
            rb.linearDamping = 2f;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            rb.constraints = RigidbodyConstraints.FreezeRotation;
            var slide = rb.GetComponent<ConfigurableJoint>();
            if (slide == null) slide = rb.gameObject.AddComponent<ConfigurableJoint>();
            slide.connectedBody = carriers[i];
            slide.autoConfigureConnectedAnchor = false;
            slide.anchor = Vector3.zero;
            slide.connectedAnchor = Vector3.zero;
            // the joint's free axis is the world's up, in the knot's own frame (the knot is not turned: the hand
            // already holds it as it is)
            var up = rb.transform.InverseTransformDirection(Vector3.up);
            var across = rb.transform.InverseTransformDirection(transform.right);
            across = (across - up * Vector3.Dot(across, up)).normalized;
            slide.axis = across;
            slide.secondaryAxis = up;
            slide.xMotion = ConfigurableJointMotion.Locked;
            slide.yMotion = ConfigurableJointMotion.Free;
            slide.zMotion = ConfigurableJointMotion.Locked;
            slide.angularXMotion = slide.angularYMotion = slide.angularZMotion = ConfigurableJointMotion.Locked;
            slide.enableCollision = false;
        }

        // A free knot: kinematic, exactly where the game puts it on the rope (no carrier joint).
        void Tighten(int i)
        {
            var rb = knotBodies[i];
            var slide = rb.GetComponent<ConfigurableJoint>();
            if (slide != null) Destroy(slide);
            rb.isKinematic = true;
            rb.constraints = RigidbodyConstraints.None;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
        }

        // Which knot hand `h` of `mg` grips, or -1: the real grip on the host, a hand at a knot on another PC.
        int GripKnot(IMiniGamePawn mg, int h)
        {
            if (!Remote)
            {
                var c = mg.Held(h);
                if (c == null) return -1;
                var held = c.GetComponentInParent<GrabHandle>();
                for (int k = 0; k < Knots; k++) if (held != null && held == knots[k]) return k;
                return -1;
            }
            var at = mg.HandPosition(h);
            for (int k = 0; k < Knots; k++)
                if (knots[k] != null && (knots[k].transform.position - at).sqrMagnitude < GripNear * GripNear) return k;
            return -1;
        }

        float KnotZ(float metres, int k) => PalaceKnotU - metres + k * PalaceKnotGap;

        // Where knot k rests when nobody holds it: on the floor, except the one by the pulley, which hangs on the rope
        // coming down from it at no steeper than about 35 degrees.
        float RestY(float z) => Mathf.Max(PalaceKnotRest, RopeHeight - Mathf.Max(0f, PulleyU - z) * .7f);

        // §9.2, on the host every physics step.
        void Pull()
        {
            float force = 0f, hold = 0f;
            int pullers = 0, bits = 0;
            var south = transform.TransformDirection(palaceFront ? pullDir : Vector3.back);   // the pulling way
            foreach (var knot in knots) if (knot != null) knot.PullAxis = south;
            foreach (var p in station.Pawns())
            {
                var mg = p.Driver as IMiniGamePawn;
                var knot = GrabHandle.HeldBy(mg);
                if (knot == null || knot.Owner != this) continue;
                Leash(p, mg, knot);
                int effort = knot.Effort(p.Driver);
                if (p.Own && effort > 0 && pullers < PawnRushMissions.WorkerCap)
                {
                    pullers++;
                    // A sprint counts 1.8 while the pawn really sprints (it spends its own stamina for it).
                    bool sprint = mg.Sprinting;
                    force += sprint ? DrawbridgeRope.SprintForce : 1f;
                    if (sprint) bits |= 1 << knot.Id;
                }
                else if (!p.Own && effort < 0)
                {
                    hold += DrawbridgeRope.HoldForce;
                    bits |= 1 << (4 + knot.Id);
                }
            }
            net = force - hold;
            knotBits = bits;
            pulled = Mathf.Min(DrawbridgeRope.Length, pulled + DrawbridgeRope.Speed(force, hold) * Time.fixedDeltaTime);
            Progress01 = pulled / DrawbridgeRope.Length;
            if (pulled >= DrawbridgeRope.Length - 1e-4f)
            {
                pulled = DrawbridgeRope.Length;
                Complete();
            }
        }

        // §9.2.5: a pawn holding a knot cannot be more than 1.3 m from it; past that it is drawn back at 8 m/s a
        // metre over (3 m/s at most), along with what it already has toward the knot.
        void Leash(MissionStation.Pawn p, IMiniGamePawn mg, GrabHandle knot)
        {
            if (p.Driver.FollowTarget == null) return;
            var to = knot.transform.position - p.Driver.FollowTarget.position;
            to.y = 0f;
            float over = to.magnitude - LeashLength;
            if (over <= 0f) return;
            var dir = to.normalized;
            float want = Mathf.Min(LeashMax, over * LeashPull), has = Vector3.Dot(mg.HipsVelocity, dir);
            if (want > has) mg.AddVelocity(dir * (want - has));
        }

        void Pose(float metres)
        {
            float p = metres / DrawbridgeRope.Length;
            if (hinge != null)
            {
                var parent = hinge.transform.parent;
                var local = Quaternion.Euler(90f * (1f - p), 0f, 0f);
                hinge.MoveRotation(parent != null ? parent.rotation * local : local);
            }
            for (int i = 0; i < Knots; i++)
            {
                var rb = knotBodies[i];
                if (rb == null) continue;
                var at = KnotPos(metres, i);
                if (!palaceFront)
                {
                    rb.MovePosition(World(at));
                    continue;
                }
                var world = World(at);
                bool held = gripCount[i] > 0;
                if (!Remote && held)
                {
                    // the host, gripped: the carrier takes it along the rope, the hands keep its height
                    if (rb.isKinematic) Loosen(i);
                    carriers[i].MovePosition(new Vector3(world.x, rb.position.y, world.z));
                }
                else
                {
                    // free (or drawn on another PC): kinematic, exactly on the rope
                    if (!rb.isKinematic) Tighten(i);
                    rb.MovePosition(world);
                }
                // a gripped knot keeps the turn it was gripped at: turning it would twist the hands and the pawn
                if (!held)
                {
                    // along the rope through it: from the next knot toward the pulley to the one toward the tail
                    var up = i < Knots - 1 ? KnotPos(metres, i + 1) : new Vector3(pullX, RopeHeight, PulleyU);
                    var down = i > 0 ? KnotPos(metres, i - 1) : at + pullDir * Tail + Vector3.down * (at.y - .075f) * (1f - shownLift[0]);
                    var dir = up - down;
                    if (dir.sqrMagnitude > 1e-4f)
                    {
                        var q = transform.rotation * Quaternion.LookRotation(dir.normalized, Vector3.up);
                        if (rb.isKinematic) rb.MoveRotation(q); else rb.rotation = q;
                    }
                }
            }
        }

        void RopePose(float metres, float shiver)
        {
            float tail = KnotU(metres, Knots - 1) - Tail;
            var a = new Vector3(ropeOffsetX, RopeHeight + shiver, RopeStart);
            var z = new Vector3(ropeOffsetX, RopeHeight + shiver, tail);
            if (palaceFront)
            {
                // the returning line: from the second pulley south past the lead knot
                a = new Vector3(pullX, RopeHeight + shiver, PulleyU);
                z = KnotPos(metres, 0) + pullDir * Tail + Vector3.up * shiver;
            }
            rope.localPosition = (a + z) * .5f;
            rope.localRotation = Quaternion.Euler(90f, 0f, 0f);
            rope.localScale = new Vector3(.14f, Mathf.Abs(a.z - z.z) * .5f, .14f);
        }

        // R502: chain i's two ends in world space - the pylon's top and the deck's far corner (the deck end moves with
        // the hinge). MiniGameDrawbridgeChain lays its links between them.
        // For the pulling pictures (MiniGameDrawbridgeFx, palace only): where the rope leaves the drum, the two pulleys,
        // the knots and whether one is gripped, in world space.
        public bool PalaceFront => palaceFront;
        public Vector3 DrumTopWorld => transform.TransformPoint(new Vector3(winchAt.x, DrumHeight + PalaceCoilRadius, winchAt.z));
        public Vector3 DrumWorld => transform.TransformPoint(new Vector3(winchAt.x, DrumHeight, winchAt.z));
        public Vector3 PulleyWorld(int i) => transform.TransformPoint(new Vector3(i == 0 ? winchAt.x : pullX, RopeHeight, PulleyU));
        public Vector3 KnotWorld(int i) => transform.TransformPoint(KnotPos(shown, i));
        // Seconds since knot i's strain sound (a sprinting puller's tug, every 2 s); large when it never played. The rope
        // swells round that knot in time with it (MiniGameDrawbridgeRope).
        // How hard the rope is being hauled, 0..1, from the net pull every PC has (each puller 1, a sprinting one 1.8,
        // minus the rivals holding back; 5 = about three sprinting pawns): the rope looks ready to part
        // (MiniGameDrawbridgeRope). 0 once the bridge is down.
        public float Tension01 => Completed ? 0f : Mathf.Clamp01(net / 5f);
        public float StrainAge(int i) => strainAt[i] > 0f ? Time.time - strainAt[i] : 99f;
        public bool KnotHeld(int i) => (knotBits >> i & 1) != 0 || (knotBits >> (4 + i) & 1) != 0;

        // The deck's face normal in world space (the chain's deck eye stands on it).
        public Vector3 DeckUp => hinge != null ? hinge.transform.up : Vector3.up;

        public bool ChainEnds(int i, out Vector3 top, out Vector3 end)
        {
            top = end = Vector3.zero;
            if (hinge == null) return false;
            float x = i == 0 ? -1.6f : 1.6f;
            end = hinge.transform.TransformPoint(new Vector3(x, 0f, -deckLen));
            top = transform.TransformPoint(new Vector3(i == 0 ? -PalacePylonX : PalacePylonX, PalaceCrownFloor, towerU));   // the crown's floor
            return true;
        }

        // R502 (user 11-07, "로프를 당기고 쇠사슬이 그걸 푸는 방식으로"): chain i's whole run in world space, from the winch's
        // drum (the same drum the rope turns) to a guide at the lane's north end by the moat, up over the pylon's top
        // and down to the deck's far corner. Pulling the rope turns the drum and pays the chain out, so the deck lowers.
        public bool ChainPath(int i, List<Vector3> into)
        {
            into.Clear();
            if (!palaceFront || !ChainEnds(i, out var top, out var end)) return false;
            into.Add(top);       // the pylon's top (the concept: the chain hangs from the pylon to the deck)
            into.Add(end);       // the deck's far corner, moving with the hinge
            return true;
        }

        // R502 (user 11-07, "l l 이런식으로" / "튀어나오면 안돼" / "당길때 끊어져 이런 부분 하나하나 신경 써"): the rope's
        // whole centre line in the game frame, as one piece: wound on the drum (a helix of PalaceCoilTurns less what has
        // been pulled off), leaving the coil's top on the tangent toward the first pulley, round both pulleys on arcs in
        // their grooves, and south past the lead knot. `depart` is the line's index where it leaves the drum.
        public const float PalaceCoilRadius = .49f;    // the rope's centre on the drum (c_drawbridge_v2_export.py rope_coil_on_drum)
        public const float PalaceCoilHalf = .5f;       // half the drum's wound length
        public const float PalaceCoilTurns = 5.5f;     // turns on the drum before any is pulled off (7 m takes 2.3 turns); clear of the gear
        public const float RopeThick = .14f;

        public bool RopeLine(List<Vector3> into, out int depart)
        {
            into.Clear();
            depart = 0;
            if (!palaceFront) return false;
            var c = new Vector3(winchAt.x, DrumHeight, winchAt.z);                // the drum's axis (game x) centre
            var p1 = new Vector3(winchAt.x, RopeHeight, PulleyU);                 // the first pulley
            var p2 = new Vector3(pullX, RopeHeight, PulleyU);                     // the second pulley
            var tail = KnotPos(shown, 0) + pullDir * Tail;                        // past the lead knot
            float r = PalaceCoilRadius;
            float turns = Mathf.Max(1f, PalaceCoilTurns - shown / (2f * Mathf.PI * r));
            float pitch = RopeThick * 1.02f;
            float a0 = -PalaceCoilHalf + RopeThick * .5f;
            // the coil in the drum's cross-section (z north, y up): the line to the first pulley leaves it on the
            // tangent over the top; going back along the rope from there the angle grows (the top runs north as it unwinds)
            float dz = p1.z - c.z, dy = p1.y - c.y;
            float phiDep = Mathf.Atan2(dy, dz) + Mathf.Acos(Mathf.Clamp(r / Mathf.Sqrt(dz * dz + dy * dy), -1f, 1f));
            int steps = Mathf.CeilToInt(turns * 40f);
            for (int k = 0; k <= steps; k++)
            {
                float u = k / (float)steps;
                float phi = phiDep + 2f * Mathf.PI * turns * (1f - u);
                into.Add(new Vector3(c.x + a0 + turns * pitch * u, c.y + r * Mathf.Sin(phi), c.z + r * Mathf.Cos(phi)));
            }
            depart = into.Count - 1;
            Wrap(into, into[depart], p1, p2);
            Wrap(into, p1, p2, new Vector3(tail.x, RopeHeight, tail.z));
            // past the second pulley the rope runs through the knots, the pulley's side first; slack, it sags onto the
            // floor between them and its tail lies on the floor (user 11-08)
            const float Floor = .075f;
            var last = into[into.Count - 1];
            float lastLift = 1f;
            for (int k = Knots - 1; k >= -1; k--)
            {
                Vector3 next;
                float l;
                if (k >= 0) { next = KnotPos(shown, k); l = shownLift[k]; }
                else
                {
                    l = shownLift[0];
                    next = KnotPos(shown, 0) + pullDir * Tail;
                    next.y = Mathf.Lerp(Floor, Mathf.Max(FreeMin, next.y - Droop * Tail), l);   // taut, the tail droops off the floor
                }
                float slack = 1f - Mathf.Min(l, lastLift);
                if (slack > .01f)
                    for (int q = 1; q <= 3; q++)                     // the sag: a sine dip, never below the floor
                    {
                        float t = q / 4f;
                        var m = Vector3.Lerp(last, next, t);
                        m.y = Mathf.Max(Floor, m.y - .45f * slack * Mathf.Sin(Mathf.PI * t));
                        // lying slack it wanders a little to the sides, never quite straight (alternating per gap)
                        m.x += ((k & 1) == 0 ? 1f : -1f) * .13f * slack * Mathf.Sin(Mathf.PI * t);
                        into.Add(m);
                    }
                into.Add(next);
                last = next;
                lastLift = l;
            }
            return true;
        }

        const float PulleyGroove = .15f;     // the gold pulley wheels' radius (c_drawbridge_v2.py "pulley wheel")

        // The arc (game frame) that rounds the corner at b between the lines a-b and b-c, tangent to both.
        static void Wrap(List<Vector3> into, Vector3 a, Vector3 b, Vector3 c)
        {
            var d1 = b - a;
            var d2 = c - b;
            d1.y = d2.y = 0f;
            if (d1.sqrMagnitude < 1e-6f || d2.sqrMagnitude < 1e-6f) { into.Add(b); return; }
            d1.Normalize();
            d2.Normalize();
            float turn = Vector3.SignedAngle(d1, d2, Vector3.up);
            if (Mathf.Abs(turn) < 1f) { into.Add(b); return; }
            float r = PulleyGroove;
            float cut = r * Mathf.Tan(Mathf.Abs(turn) * .5f * Mathf.Deg2Rad);
            var t1 = b - d1 * cut;                           // where the rope meets the wheel
            var side = Vector3.Cross(Vector3.up, d1) * -Mathf.Sign(turn);
            var centre = t1 - side * r;
            const int steps = 10;
            for (int k = 0; k <= steps; k++)
                into.Add(centre + Quaternion.AngleAxis(turn * k / steps, Vector3.up) * side * r);
        }

        // Metres of rope pulled as drawn (the host's, eased on a client): how far the chain has been paid out.
        public float Shown => shown;

        void ChainPose()
        {
            if (hinge == null) return;
            for (int i = 0; i < chains.Length; i++)
            {
                if (chains[i] == null) continue;
                float x = i == 0 ? -1.6f : 1.6f;
                var end = hinge.transform.TransformPoint(new Vector3(x, 0f, -deckLen));
                if (station.PalaceStyle)
                {
                    // Local positions follow a placed prefab even before its first Play-mode Update.
                    chains[i].SetPosition(0, transform.InverseTransformPoint(end));
                    chains[i].SetPosition(1, new Vector3(i == 0 ? -2.4f : 2.4f, PalaceTowerTop - .3f, towerU));
                }
                else
                {
                    chains[i].SetPosition(0, end);
                    chains[i].SetPosition(1, World(new Vector3(x * .6f, DrumHeight + DrumRadius, Winch)));
                }
            }
        }

        // StateDrivenMover: the deck, the drum and the knots follow the rope's length (the host's game state), not
        // the shared obstacle clock.
        void Update()
        {
            if (station == null || rope == null) return;
            float p = shown / DrawbridgeRope.Length;
            bool moving = !Completed && net > 0f && shown < DrawbridgeRope.Length - .001f;
            // The rope from the drum to its tail, shivering 0.015 m at 40 Hz while it is pulled.
            float shiver = moving ? .015f * Mathf.Sin(Time.time * 40f * Mathf.PI * 2f) : 0f;
            RopePose(shown, shiver);
            var held = (knotBits >> 4) != 0 && Mathf.Repeat(Time.time * 4f, 1f) < .5f;
            var mat = held ? ropeHeld : ropeMat;
            var rr = rope.GetComponent<Renderer>();
            if (rr.sharedMaterial != mat) rr.sharedMaterial = mat;
            for (int i = 0; i < Knots; i++)
                if (knots[i] != null) knots[i].ShowHeld((knotBits >> (4 + i) & 1) != 0 ? (station.Team == Teams.White ? Teams.Black : Teams.White) : (knotBits >> i & 1) != 0 ? station.Team : -1);
            // The drum turns 0.3 of a turn a metre; the pawl kicks on every metre.
            // palace: the rope leaves the drum's top toward the moat, so its top turns north, a turn per 2 pi r of rope
            if (drum != null) drum.localRotation = Quaternion.Euler(palaceFront ? shown / (2f * Mathf.PI * PalaceCoilRadius) * 360f : -shown * .3f * 360f, 0f, 0f);
            int metre = Mathf.FloorToInt(shown + 1e-3f);
            if (metre > clicks)
            {
                clicks = metre;
                pawlKick = 1f;
                var at = pawl.position;
                ArenaFx.Burst(at, new[] { Arena.Gold }, 6, 1f, 1.2f, .4f, .05f);
                ArenaFx.Sfx("sfx_ratchet_click", at);
            }
            pawlKick = Mathf.MoveTowards(pawlKick, 0f, Time.deltaTime / .15f);
            if (pawl != null) pawl.localRotation = Quaternion.Euler(-28.6f * Mathf.Sin(pawlKick * Mathf.PI), 0f, 0f);
            // The latch lamps light at 25, 50 and 75 %.
            for (int i = 0; i < 3; i++)
            {
                var want = p >= (i + 1) * .25f - 1e-4f ? lampOn : lampOff;
                if (lamps[i] != null && lamps[i].sharedMaterial != want) lamps[i].sharedMaterial = want;
            }
            // The chains from the deck's end to the drum's top.
            ChainPose();
            Sounds(moving);
            if (moving) Dust();
            // Down: a thud and a 0.05 m bounce at the far bank's end.
            if (Completed && !slammed && shown >= DrawbridgeRope.Length - .01f)
            {
                slammed = true;
                slamAt = Time.time;
                var at = World(new Vector3(0f, .05f, moat0 + .2f));
                ArenaFx.Burst(at, new[] { new Color(.8f, .72f, .6f), new Color(.55f, .55f, .55f) }, 40, 3f, 2f, 1f, .1f);
                ArenaFx.Sfx("sfx_bridge_slam", at);
            }
            if (deckLook != null)
            {
                float age = slamAt < 0f ? 1f : (Time.time - slamAt) / .2f;
                deckLook.localPosition = new Vector3(0f, age < 1f ? .05f * Mathf.Sin(age * Mathf.PI) : 0f, 0f);
            }
        }

        void Sounds(bool moving)
        {
            var at = World(winchAt);
            float speed = DrawbridgeRope.Speed(Mathf.Max(0f, net), 0f);
            // user 11-08 ("줄을 당기다가 갑자기 안하면 갑자기 사운드가 멈춰"): the loops no longer stop dead when the
            // pulling stops - they swell in over 0.25 s and die away over 0.6 s (a short pause in the pulling keeps them on)
            hum = Mathf.MoveTowards(hum, moving ? 1f : 0f, Time.deltaTime / (moving ? .25f : .6f));
            if (hum > 0f)
            {
                if (creak == null || !creak.isPlaying) creak = ArenaFx.Hold("sfx_rope_creak", at, 1f, CreakVolume * hum);
                if (winch == null || !winch.isPlaying) winch = ArenaFx.Hold("sfx_winch", at, 1f, WinchVolume * hum);
                if (creak != null)
                {
                    creak.volume = CreakVolume * hum;
                    if (moving) creak.pitch = .8f + speed / DrawbridgeRope.MaxSpeed * .4f;
                }
                if (winch != null) winch.volume = WinchVolume * hum;
            }
            else
            {
                ArenaFx.Cut(creak);
                ArenaFx.Cut(winch);
                creak = winch = null;
            }
            // only while the rope runs: once the bridge is down the knots' last bits stay set (Pull no longer runs), and
            // the strain kept repeating from the rope (user 11-08, "다 줄을 땅겼는데 줄쪽에서 소리가 남아 있어")
            if (!moving) return;
            for (int i = 0; i < Knots; i++)
                if ((knotBits >> i & 1) != 0 && Time.time - strainAt[i] >= 2f)
                {
                    strainAt[i] = Time.time;
                    ArenaFx.Sfx("sfx_strain", World(KnotPos(shown, i) + (palaceFront ? pullDir : Vector3.back) * 1f + Vector3.up * .2f), volume: StrainVolume);
                }
        }

        // Sprinting pullers kick up 3 bits of dust behind their knot every 0.2 s.
        void Dust()
        {
            if ((knotBits & 15) == 0 || Time.time - dustAt < .2f) return;
            dustAt = Time.time;
            for (int i = 0; i < Knots; i++)
                if ((knotBits >> i & 1) != 0)
                    ArenaFx.Burst(World(KnotPos(shown, i) + (palaceFront ? pullDir : Vector3.back) * 1.1f + Vector3.down * (RopeHeight - .05f)), new[] { new Color(.8f, .72f, .6f) }, 3, .8f, .6f, .4f, .06f);
        }

        // ------------------------------------------------------------------ the wire (§9.6)

        public override void WriteNet(ref NetBlock block)
        {
            block.Unit16(pulled, DrawbridgeRope.Length);
            block.U8(Mathf.Clamp(Mathf.RoundToInt(Mathf.Max(0f, net) * 20f), 0, 255));
            int p = Mathf.FloorToInt(pulled / DrawbridgeRope.Length * 4f + 1e-4f);
            block.U8((Completed ? 1 : 0) | Mathf.Min(3, p) << 1);
            block.U8(knotBits);
        }

        public override void ReadNet(ref NetBlock block)
        {
            pulled = block.ReadUnit16(DrawbridgeRope.Length);
            net = block.ReadU8() / 20f;
            block.ReadU8();   // done and the latch: both follow from the length
            knotBits = block.ReadU8();
        }

        public override void ApplyRemote(float progress, bool completed)
        {
            base.ApplyRemote(progress, completed);
            if (completed) pulled = DrawbridgeRope.Length;
        }

        public override void ResetGame()
        {
            base.ResetGame();
            pulled = shown = net = 0f;
            knotBits = clicks = 0;
            slammed = false;
            slamAt = -1f;
            if (hinge != null) hinge.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            Pose(0f);
        }
    }
}
