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

        [SerializeField] MissionStation station;
        [Tooltip("The hinge on the far bank; the deck turns about its x axis.")]
        [SerializeField] Rigidbody hinge;
        [SerializeField] Rigidbody[] knotBodies = new Rigidbody[Knots];
        [SerializeField] GrabHandle[] knots = new GrabHandle[Knots];
        [SerializeField] Transform rope, drum, pawl, deckLook;
        [SerializeField] Renderer[] lamps = new Renderer[3];
        [SerializeField] LineRenderer[] chains = new LineRenderer[2];

        float pulled;            // metres of rope pulled (host), or the host's (client)
        float shown;             // what is drawn: the host's own, a client's eased
        float net;               // F - E
        int knotBits;            // knots pulled sprinting (bits 0-3) and held against (bits 4-7)
        int clicks;              // whole metres pulled, for the ratchet's click
        float pawlKick, slamAt = -1f;
        readonly float[] strainAt = new float[Knots];
        float dustAt;
        AudioSource creak, winch;
        Material ropeMat, ropeHeld, lampOn, lampOff;
        bool slammed;

        public float Pulled => pulled;

        public static MgDrawbridge Build(CourseBuilder b, MissionStation station)
        {
            var k = b.kit;
            var V = (System.Func<float, float, float, Vector3>)MissionGames.V;
            MissionGames.Floor(b, 0f, Moat0);
            MissionGames.Floor(b, Moat1, 20f);
            // The moat: water at -0.9, its bottom at -1.6, walls to the floor; the ends closed down to the plaza
            // floor. Falling in counts as a fall (back to the plaza's checkpoint).
            var stone = Arena.Lit("M_MoatStone", Arena.Hex("#6E6A64"), 0f, .9f);
            b.Box("Moat wall S", V(-10f, -1.6f, Moat0 - .2f), V(10f, 0f, Moat0), stone, true);
            b.Box("Moat wall N", V(-10f, -1.6f, Moat1), V(10f, 0f, Moat1 + .2f), stone, true);
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
            for (int i = 0; i < 8; i++)
            {
                float z = -Deck + .25f + i * .52f;
                Arena.Picture(look, "Plank line", V(-hw, 0f, z - .02f), V(hw, .01f, z + .02f), Arena.Wood(true));
            }
            b.parent = before;
            hingeGo.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            var light = Arena.Lit("M_LightStone", Arena.Hex("#D8D0C2"), 0f, .8f);
            foreach (float side in new[] { -1f, 1f })
                b.Box("Bridge tower", V(side * 2.4f - .35f, 0f, 16.2f - .35f), V(side * 2.4f + .35f, 6f, 16.2f + .35f), light, true);

            // The winch: a walnut frame, the drum across it (axis along v), gold teeth and the pawl at its east end,
            // three latch lamps on the east post.
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
            pawlGo.localPosition = V(1.2f, DrumHeight + DrumRadius + .3f, Winch - .2f);
            MiniGameParts.Part(pawlGo, PrimitiveType.Cube, new Vector3(0f, -.15f, 0f), new Vector3(.12f, .6f, .12f), gold);
            b.Box("Winch post E", V(1.9f, 0f, Winch - .1f), V(2.1f, 2.5f, Winch + .1f), Arena.Wood(false), true);
            var lampOff = Arena.Lit("M_LampOff", new Color(.3f, .27f, .2f), 0f, .6f);
            var lamps = new Renderer[3];
            for (int i = 0; i < 3; i++)
                lamps[i] = MiniGameParts.Part(b.parent, PrimitiveType.Sphere, V(2f, 1f + i * .6f, Winch - .16f), Vector3.one * .16f, lampOff).GetComponent<Renderer>();

            // The rope lane: dark slate, a gold tick every metre from u 10.2 down to 3.2, arrows pointing south.
            Arena.Picture(b.parent, "Rope lane", V(-1.2f, 0f, .2f), V(1.2f, .015f, RopeStart), Arena.Lit("M_RopeLane", Arena.Hex("#565C6B"), 0f, .8f));
            var tick = Arena.Emissive(GrabHandle.Gold, .5f);
            for (int i = 0; i < 8; i++)
            {
                float u = FirstKnot - i;
                Arena.Picture(b.parent, "Lane tick", V(-1.2f, .015f, u - .03f), V(1.2f, .025f, u + .03f), tick);
            }
            for (int i = 0; i < 4; i++)
            {
                float u = 9f - i * 2.2f;
                foreach (float side in new[] { -1f, 1f })
                {
                    var arm = Arena.Picture(b.parent, "Lane arrow", V(-.03f, .015f, -.22f), V(.03f, .025f, .22f), tick);
                    arm.transform.localPosition = V(-1.05f + side * .14f, .02f, u + .14f);
                    arm.transform.localRotation = Quaternion.Euler(0f, side * -40f, 0f);
                }
            }

            var game = b.parent.gameObject.AddComponent<MgDrawbridge>();
            game.station = station;
            game.hinge = body;
            game.drum = drumGo;
            game.pawl = pawlGo;
            game.deckLook = look;
            game.lamps = lamps;
            game.lampOff = lampOff;
            game.lampOn = Arena.Emissive(Arena.Gold, 1f);
            // The rope (a picture from the drum to its tail) and the knots on it.
            game.ropeMat = Arena.Lit("M_Rope", Arena.Hex("#C9A57A"), 0f, .9f);
            game.ropeHeld = Arena.Lit("M_RopeHeld", Arena.TeamColor(station.Team == Teams.White ? Teams.Black : Teams.White), .4f, .7f);
            game.rope = MiniGameParts.Part(b.parent, PrimitiveType.Cylinder, Vector3.zero, Vector3.one, game.ropeMat).transform;
            for (int i = 0; i < Knots; i++)
            {
                var h = MiniGameParts.Handle(b.parent, "Knot " + (i + 1), V(0f, RopeHeight, KnotU(0f, i)), new Vector3(.3f, .3f, .14f), game, (byte)i, GrabHandle.HandleKind.Pull);
                game.knots[i] = h;
                game.knotBodies[i] = h.GetComponent<Rigidbody>();
            }
            for (int i = 0; i < 2; i++)
            {
                var c = new GameObject("Chain").AddComponent<LineRenderer>();
                c.transform.SetParent(b.parent, false);
                c.useWorldSpace = true;
                c.positionCount = 2;
                c.widthMultiplier = .05f;
                c.sharedMaterial = Arena.Lit("M_Chain", new Color(.45f, .45f, .47f), 0f, .4f);
                game.chains[i] = c;
            }
            game.Pose(0f);
            return game;
        }

        // Knot k's u when `metres` of rope have been pulled.
        public static float KnotU(float metres, int k) => FirstKnot - metres - k * KnotGap;

        Vector3 World(Vector3 local) => transform.TransformPoint(local);

        void FixedUpdate()
        {
            if (station == null) return;
            if (!Completed && !Remote) Pull();
            if (Remote) shown = Mathf.MoveTowards(shown, pulled, Time.fixedDeltaTime * .6f);
            else shown = pulled;
            Pose(shown);
        }

        // §9.2, on the host every physics step.
        void Pull()
        {
            float force = 0f, hold = 0f;
            int pullers = 0, bits = 0;
            var south = transform.TransformDirection(Vector3.back);
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
                if (knotBodies[i] != null) knotBodies[i].MovePosition(World(new Vector3(0f, RopeHeight, KnotU(metres, i))));
        }

        // StateDrivenMover: the deck, the drum and the knots follow the rope's length (the host's game state), not
        // the shared obstacle clock.
        void Update()
        {
            if (station == null || rope == null) return;
            float p = shown / DrawbridgeRope.Length;
            bool moving = !Completed && net > 0f && shown < DrawbridgeRope.Length - .001f;
            // The rope from the drum to its tail, shivering 0.015 m at 40 Hz while it is pulled.
            float tail = KnotU(shown, Knots - 1) - Tail;
            float shiver = moving ? .015f * Mathf.Sin(Time.time * 40f * Mathf.PI * 2f) : 0f;
            var a = new Vector3(0f, RopeHeight + shiver, RopeStart);
            var z = new Vector3(0f, RopeHeight + shiver, tail);
            rope.localPosition = (a + z) * .5f;
            rope.localRotation = Quaternion.Euler(90f, 0f, 0f);
            rope.localScale = new Vector3(.14f, (RopeStart - tail) * .5f, .14f);
            var held = (knotBits >> 4) != 0 && Mathf.Repeat(Time.time * 4f, 1f) < .5f;
            var mat = held ? ropeHeld : ropeMat;
            var rr = rope.GetComponent<Renderer>();
            if (rr.sharedMaterial != mat) rr.sharedMaterial = mat;
            for (int i = 0; i < Knots; i++)
                if (knots[i] != null) knots[i].ShowHeld((knotBits >> (4 + i) & 1) != 0 ? (station.Team == Teams.White ? Teams.Black : Teams.White) : (knotBits >> i & 1) != 0 ? station.Team : -1);
            // The drum turns 0.3 of a turn a metre; the pawl kicks on every metre.
            if (drum != null) drum.localRotation = Quaternion.Euler(-shown * .3f * 360f, 0f, 0f);
            int metre = Mathf.FloorToInt(shown + 1e-3f);
            if (metre > clicks)
            {
                clicks = metre;
                pawlKick = 1f;
                var at = World(pawl.localPosition);
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
            if (hinge != null)
                for (int i = 0; i < 2; i++)
                {
                    float x = i == 0 ? -1.6f : 1.6f;
                    chains[i].SetPosition(0, hinge.transform.TransformPoint(new Vector3(x, 0f, -Deck)));
                    chains[i].SetPosition(1, World(new Vector3(x * .6f, DrumHeight + DrumRadius, Winch)));
                }
            Sounds(moving);
            Dust();
            // Down: a thud and a 0.05 m bounce at the far bank's end.
            if (Completed && !slammed && shown >= DrawbridgeRope.Length - .01f)
            {
                slammed = true;
                slamAt = Time.time;
                var at = World(new Vector3(0f, .05f, Moat0 + .2f));
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
            var at = World(new Vector3(0f, DrumHeight, Winch));
            float speed = DrawbridgeRope.Speed(Mathf.Max(0f, net), 0f);
            if (moving)
            {
                if (creak == null || !creak.isPlaying) creak = ArenaFx.Hold("sfx_rope_creak", at, 1f, .6f);
                if (winch == null || !winch.isPlaying) winch = ArenaFx.Hold("sfx_winch", at, 1f, .6f);
                if (creak != null) creak.pitch = .8f + speed / DrawbridgeRope.MaxSpeed * .4f;
            }
            else
            {
                ArenaFx.Cut(creak);
                ArenaFx.Cut(winch);
                creak = winch = null;
            }
            for (int i = 0; i < Knots; i++)
                if ((knotBits >> i & 1) != 0 && Time.time - strainAt[i] >= 2f)
                {
                    strainAt[i] = Time.time;
                    ArenaFx.Sfx("sfx_strain", World(new Vector3(0f, 1f, KnotU(shown, i) - 1f)), volume: .7f);
                }
        }

        // Sprinting pullers kick up 3 bits of dust behind their knot every 0.2 s.
        void Dust()
        {
            if ((knotBits & 15) == 0 || Time.time - dustAt < .2f) return;
            dustAt = Time.time;
            for (int i = 0; i < Knots; i++)
                if ((knotBits >> i & 1) != 0)
                    ArenaFx.Burst(World(new Vector3(0f, .05f, KnotU(shown, i) - 1.1f)), new[] { new Color(.8f, .72f, .6f) }, 3, .8f, .6f, .4f, .06f);
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
