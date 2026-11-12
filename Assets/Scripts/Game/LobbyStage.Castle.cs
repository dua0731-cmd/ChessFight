using UnityEngine;
using UnityEngine.Rendering;

namespace ChessFight.Game
{
    // The lobby's chess castle (2026-10-17): a cream stone castle courtyard after
    // the 10-12 concept art, made in Blender (ArtSource/Lobby, export_castle.py)
    // and loaded from Resources/Lobby/Castle. Glossy cream and slate-blue tiles,
    // red and cream block curbs, the red carpet stair up to the great gate, the
    // title screen's pennants, knight and king statues. The stage and the party
    // stay LobbyStage's own. Blender's Y (away from the camera) is Unity's +Z.
    //
    // The gate's two leaves are separate objects pivoted on their hinges, so the
    // gate can open when the party walks in (SetGate).
    public sealed partial class LobbyStage
    {
        GameObject castle;
        Transform leafLeft, leafRight;
        Light gateGlow;
        Material gateLightMaterial;
        float gateOpen, gateTarget;
        const float GateOpenDegrees = 96f;

        bool Castle(Transform root)
        {
            var prefab = Resources.Load<GameObject>("Lobby/Castle/Castle");
            if (prefab == null) return false;
            castle = Instantiate(prefab, root);
            castle.name = "Lobby Castle";
            // The FBX comes in as (-x, z, -y) of Blender; half a turn puts Blender's
            // +Y (away from the camera) on Unity's +Z and its +X on the right.
            castle.transform.localRotation = Quaternion.Euler(0, 180f, 0);
            foreach (var r in castle.GetComponentsInChildren<Renderer>())
            {
                r.shadowCastingMode = ShadowCastingMode.On;
                r.receiveShadows = true;
            }
            // The stone floor (10-17, cut stones in a chess pattern) comes as its own
            // FBX; the castle's first, glossy tiles hide under it until the next full export.
            var floor = Resources.Load<GameObject>("Lobby/Castle/Floor");
            if (floor != null)
            {
                var stones = Instantiate(floor, castle.transform);
                stones.name = "Stone Floor";
                foreach (string old in new[] { "Castle_Tile_Cream", "Castle_Tile_Slate", "Castle_Grout" })
                {
                    var t = castle.transform.Find(old);
                    if (t != null) t.gameObject.SetActive(false);
                }
            }
            // 승규 2026-10-17: no topiaries and no red / cream block curbs (still in the Blender file and the FBX for now).
            foreach (string gone in new[] { "Castle_Topiary", "Castle_Red_Block", "Castle_Cream_Block" })
            {
                var t = castle.transform.Find(gone);
                if (t != null) t.gameObject.SetActive(false);
            }
            leafLeft = castle.transform.Find("GateLeaf_L");
            leafRight = castle.transform.Find("GateLeaf_R");
            // the far wall of the gateway glows when the gate opens
            var lightWall = castle.transform.Find("Castle_Gate_Light");
            if (lightWall != null)
            {
                var r = lightWall.GetComponent<Renderer>();
                gateLightMaterial = r.material;   // an instance: only this wall changes
                gateLightMaterial.EnableKeyword("_EMISSION");
                gateLightMaterial.SetColor("_EmissionColor", Color.black);
            }
            if (leafLeft != null) leafBaseLeft = leafLeft.localRotation;
            if (leafRight != null) leafBaseRight = leafRight.localRotation;
            // The title screen's own pennants (cloth that flaps, a gust under the mouse) on the castle's
            // iron rods; the baked flat ones are hidden.
            // The castle's own rods go too (the two at the far ends hung pennants 승규 took out, 10-17);
            // each pennant left gets its rod from Pennants().
            foreach (string flat in new[] { "Castle_Pennant_1C1612", "Castle_Pennant_7A1F24", "Castle_Iron_Rod" })
            {
                var t = castle.transform.Find(flat);
                if (t != null) t.gameObject.SetActive(false);
            }
            Pennants(root);
            Daylight(root);
            Fires(root);
            Blocks(root);
            return true;
        }

        // Rod top (x, height, depth), cloth size, colour and glyph, as in the Blender castle (build_castle.py),
        // 20 cm further from the wall since the stone blocks (LobbyStage.Blocks.cs) stand proud of it: the
        // cloth hangs a little behind its rod and had sunk into them.
        static readonly (Vector3 top, Vector2 size, int cloth, string glyph)[] PennantSpots =
        {
            (new Vector3(-9.0f, 4.7f, 17.56f), new Vector2(1.45f, 3.0f), 0x7A1F24, "♕"),
            (new Vector3(9.0f, 4.7f, 17.56f), new Vector2(1.45f, 3.0f), 0x1C1612, "♘"),
            (new Vector3(-5.6f, 5.2f, 16.15f), new Vector2(1.25f, 2.5f), 0x1C1612, "♘"),
            (new Vector3(5.6f, 5.2f, 16.15f), new Vector2(1.25f, 2.5f), 0x7A1F24, "♕"),
        };
        readonly System.Collections.Generic.List<IntroStage.Pennant> pennants = new System.Collections.Generic.List<IntroStage.Pennant>();

        void Pennants(Transform root)
        {
            var rodIron = LastSceneArt.Lit(0x3A3530, .5f, .6f);
            for (int i = 0; i < PennantSpots.Length; i++)
            {
                var spot = PennantSpots[i];
                var pennant = new IntroStage.Pennant(root, spot.top, IntroStage.PennantTexture(spot.glyph, spot.cloth), i);
                // The title looks down -Z; here the camera looks down +Z, so the cloth turns half round.
                pennant.Shape.localRotation = Quaternion.Euler(0, 180f, 0);
                pennant.Shape.localScale = new Vector3(spot.size.x / 1.25f, spot.size.y / 2.4f, 1f);
                LastSceneArt.Part(root, "Pennant Rod", LastSceneArt.Cylinder(.035f, .035f, spot.size.x + .35f, 12), rodIron,
                                  spot.top + new Vector3(0, .04f, -.03f), Quaternion.Euler(0, 0, 90f), null, true);
                pennants.Add(pennant);
            }
        }

        void TickPennants(float t, float dt)
        {
            if (pennants.Count == 0) return;
            // 10-17 (승규): the cursor does nothing to the lobby's pennants; they only sway in the wind.
            var nowhere = new Vector2(-10000f, -10000f);
            foreach (var p in pennants) p.Tick(t, dt, view, nowhere);
        }

        // A bright castle day: a warm sun from the front left, a blue sky, soft
        // blue-and-stone ambient light, and the warm points the props carry.
        void Daylight(Transform root)
        {
            view.clearFlags = CameraClearFlags.SolidColor;
            view.backgroundColor = MenuArt.Hex(0x9CC6EE);
            view.farClipPlane = 300f;
            RenderSettings.fog = false;
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(.62f, .68f, .80f);
            RenderSettings.ambientEquatorColor = new Color(.60f, .56f, .50f);
            RenderSettings.ambientGroundColor = new Color(.30f, .27f, .24f);
            RenderSettings.reflectionIntensity = .8f;
            foreach (var light in FindObjectsByType<Light>(FindObjectsSortMode.None))
                if (light.type == LightType.Directional && light.transform.parent == null) light.enabled = false;

            var sun = new GameObject("Castle Sun").AddComponent<Light>();
            sun.transform.SetParent(root, false);
            sun.type = LightType.Directional;
            sun.color = MenuArt.Hex(0xFFF0D8);
            sun.intensity = 1.5f;
            sun.shadows = LightShadows.Soft;
            sun.transform.rotation = Quaternion.LookRotation(new Vector3(.314f, -.743f, .591f));

            for (int s = -1; s <= 1; s += 2)
            {
                Point(root, "Brazier Glow", new Vector3(s * 7.6f, 1.1f, 7.6f), 0xFF7A2A, .7f, 3.2f);
                Point(root, "Lantern Glow", new Vector3(s * 7.6f, 2.7f, 17.3f), 0xFFB45E, 1.2f, 3.5f);
                Point(root, "Candle Glow", new Vector3(s * 2.5f, 3.8f, 15.6f), 0xFFC27A, 1.3f, 3.5f);
            }
            gateGlow = Point(root, "Gate Glow", new Vector3(0, 3.3f, 16.4f), 0xFFD890, 0f, 7f);
        }

        Light Point(Transform root, string name, Vector3 at, int color, float intensity, float range)
        {
            var light = new GameObject(name).AddComponent<Light>();
            light.transform.SetParent(root, false);
            light.transform.localPosition = at;
            light.type = LightType.Point;
            light.color = MenuArt.Hex(color);
            light.range = range;
            light.intensity = PipelineSettings.PointIntensity(intensity, range);
            light.shadows = LightShadows.None;
            return light;
        }

        // The key light on the party (the castle's sun alone leaves the pawns' faces flat).
        void KeyLight(Transform root)
        {
            MenuArt.Spot(root, MenuArt.Web(0, 15, 5), MenuArt.Web(0, Top, .5f), 0xFFF1DC, 1.1f, .3f, .55f, true);
        }

        // Opens (true) or closes the great gate; the leaves swing in over about
        // half a second with a little overshoot, and the light beyond comes up.
        public void SetGate(bool open) => gateTarget = open ? 1f : 0f;

        void TickGate(float dt)
        {
            if (leafLeft == null || leafRight == null) return;
            if (Mathf.Approximately(gateOpen, gateTarget)) return;
            gateOpen = Mathf.MoveTowards(gateOpen, gateTarget, dt * 2f);
            float u = gateOpen < 1f ? 1f - Mathf.Pow(1f - gateOpen, 3f) : 1f;
            float angle = u * GateOpenDegrees;
            if (hall) HallLeaves(angle);
            else
            {
                leafLeft.localRotation = leafBaseLeft * Quaternion.Euler(0, angle, 0);   // the left leaf is mirrored in the FBX
                leafRight.localRotation = leafBaseRight * Quaternion.Euler(0, angle, 0);
            }
            if (gateGlow != null) gateGlow.intensity = PipelineSettings.PointIntensity(2.4f * u, gateGlow.range);
            if (gateLightMaterial != null) gateLightMaterial.SetColor("_EmissionColor", MenuArt.Hex(0xFFD9A0) * (3.2f * u));
        }

        Quaternion leafBaseLeft = Quaternion.identity, leafBaseRight = Quaternion.identity;

        // ---- The walk in. "게임 시작" on a public search: the gate swings open and
        // the party runs off the back of the stage, down its steps, along the red
        // carpet and up the stair into the light. MatchLoader holds the matchmaking
        // screen until GateEntered (or a short timeout), so the search itself is
        // never delayed. Each PC plays it for itself; nothing goes over the network.
        // A cancelled search before the screen comes up puts everyone back (LeaveGate).

        public static LobbyStage Current { get; private set; }
        public bool HasGate => leafLeft != null && leafRight != null;
        public bool GateEntered => walking && Time.unscaledTime - walkStart >= walkSeconds;
        // The longest the walk can take; MatchLoader's timeout.
        public const float MaxWalkSeconds = 3f;
        const float RunSpeed = 9f, FirstStep = .12f, Stagger = .07f;   // about 2.3 s (10-17: was 4.5 s, too long a wait)
        float Speed => hall ? HallRunSpeed : RunSpeed;

        readonly (Vector3 at, Vector3 look)[] homes = new (Vector3, Vector3)[Spots];
        readonly Vector3[][] paths = new Vector3[Spots][];
        readonly float[] delays = new float[Spots];
        float walkStart, walkSeconds;
        bool walking;

        void Awake() => Current = this;
        void OnDestroy()
        {
            if (Current == this) Current = null;
            if (gateLightMaterial != null) Destroy(gateLightMaterial);
            foreach (var p in pennants) p.Dispose();
            foreach (var m in fireMaterials) if (m != null) Destroy(m);
            DisposeHall();
            mason?.Dispose();
        }

        public void EnterGate()
        {
            if (walking || !HasGate) return;
            walking = true;
            walkStart = Time.unscaledTime;
            walkSeconds = FirstStep;
            SetGate(true);
            int order = 0;
            // Lanes: the front-row middle first, then outwards, the back row last.
            float[] lane = { 0f, -1.1f, 1.1f, -.55f, .55f, -1.1f };
            for (int i = 0; i < Spots; i++)
            {
                paths[i] = null;
                if (bodies[i] == null || shown[i].Invite || figures[i] == null) continue;
                float x = lane[i];
                var home = homes[i].at;
                paths[i] = hall ? HallPath(x, home) : new[]
                {
                    home,
                    new Vector3(x, Top, 5.0f),            // the stage's back edge
                    new Vector3(x * .9f, 0f, 7.6f),       // down the stage's back steps
                    new Vector3(x * .9f, 0f, 10.0f),      // the carpet to the stair
                    new Vector3(x * .9f, 1.6f, 13.6f),    // up the stair
                    new Vector3(x * .5f, 1.68f, 17.2f),   // across the landing to the gate
                    new Vector3(0f, 1.68f, 19.6f),        // through it, into the light
                };
                delays[i] = FirstStep + Stagger * order++;
                walkSeconds = Mathf.Max(walkSeconds, delays[i] + RunUp / 2f + Length(paths[i]) / Speed);
            }
            walkSeconds = Mathf.Min(walkSeconds + .15f, MaxWalkSeconds);
        }

        public void LeaveGate()
        {
            if (!walking) return;
            walking = false;
            SetGate(false);
            for (int i = 0; i < Spots; i++)
            {
                if (bodies[i] == null) continue;
                bodies[i].SetActive(true);
                if (figures[i] != null) figures[i].Place(homes[i].at, homes[i].look);
            }
        }

        // True when spot i is walking (or hidden for the walk) this frame.
        // R603 (승규 "기물 걸어가는 동작을 자연스럽게 해줘"): the old run (MenuArt.Run) went from standing to full speed
        // at once, took about three steps a second at 9 to 10 m/s so the feet slid along the floor, flapped the arms
        // out sideways and turned the body round in one frame at every bend of the path. Now each runner speeds up over
        // its first steps, its stride is counted from the ground it covers (a step every StepLength metres), the arms
        // pump forward and back against the legs, the body leans into the run, bounces in the flight between steps
        // and squashes as a foot lands (as the intro's runners, IntroStage.Racers.cs Stride), and it faces a point a
        // little further along the path so it turns smoothly round the bends.
        const float RunUp = .35f;          // seconds to full speed
        const float StepLength = 1.5f;     // metres a step at full speed: about 6.5 steps a second at 10 m/s
        const float LookAhead = 1.1f;      // metres along the path the runner faces

        bool TickWalk(int i, float t)
        {
            if (!walking) return false;
            if (shown[i].Invite || paths[i] == null) { bodies[i].SetActive(false); return true; }
            float age = t - walkStart - delays[i];
            if (age < 0) { MenuArt.Idle(figures[i], t, i, .6f); return true; }
            // speeding up: the distance under a speed that rises evenly to full over RunUp
            float s = (age < RunUp ? age * age / (2f * RunUp) : age - RunUp / 2f) * Speed;
            float pace = Mathf.Clamp01(age / RunUp);
            var path = paths[i];
            if (s >= Length(path)) { bodies[i].SetActive(false); return true; }   // through the gate
            var at = PathPoint(path, s);
            var ahead = PathPoint(path, s + LookAhead) - at; ahead.y = 0;
            figures[i].Place(at, at + (ahead.sqrMagnitude > 1e-4f ? ahead : Vector3.forward));
            figures[i].Pose(Stride(s / StepLength * Mathf.PI, pace));
            return true;
        }

        static Vector3 PathPoint(Vector3[] path, float s)
        {
            for (int k = 1; k < path.Length; k++)
            {
                float seg = Vector3.Distance(path[k - 1], path[k]);
                if (s <= seg) return Vector3.Lerp(path[k - 1], path[k], seg > 0 ? s / seg : 1f);
                s -= seg;
            }
            return path[path.Length - 1];
        }

        // One step every pi of phase. pace 0..1 grows the stride from standing to a full run.
        static FigurePose Stride(float phase, float pace)
        {
            float sn = Mathf.Sin(phase), a = Mathf.Abs(sn);
            float leg = Mathf.Sign(sn) * Mathf.Pow(a, .7f) * pace;
            return new FigurePose
            {
                Yaw = sn * .06f * pace,                        // the shoulders turn a little against the legs
                Lean = .08f + .16f * pace + .05f * (1f - a) * pace,
                Leg = leg * .95f,
                ArmSwing = -leg * 1.0f,
                ArmLeft = .3f, ArmRight = .3f,                 // arms down at the sides, pumping
                Jump = .12f * Mathf.Pow(a, 1.5f) * pace,       // highest in the flight with the legs spread
                Squash = (.06f * Mathf.Pow(1f - a, 6f) - .025f * a) * pace
            };
        }

        // While the party walks in, the camera follows it from behind and closes in
        // on the gate: it eases from the lobby view to a low shot up the carpet,
        // looking at the party and, by the end, into the open gate.
        static readonly Vector3 CloseAt = new Vector3(0f, 3.8f, 9.0f), GateLook = new Vector3(0f, 2.9f, 17.6f);
        float cameraBlend;

        void WalkCamera(ref Vector3 at, ref Vector3 look)
        {
            float target = 0f;
            if (walking) target = Mathf.Clamp01((Time.unscaledTime - walkStart - .15f) / Mathf.Max(.5f, walkSeconds - .5f));
            // in: follows the walk; out (cancelled): a quick ease back
            cameraBlend = walking ? target : Mathf.MoveTowards(cameraBlend, 0f, Time.unscaledDeltaTime * 1.5f);
            if (cameraBlend <= 0f) return;
            if (hall) { HallCamera(ref at, ref look, cameraBlend); return; }   // the gate is behind the camera there
            float u = cameraBlend * cameraBlend * (3f - 2f * cameraBlend);
            var party = GateLook; int count = 0; var sum = Vector3.zero;
            for (int i = 0; i < Spots; i++)
                if (bodies[i] != null && bodies[i].activeSelf && figures[i] != null && !shown[i].Invite) { sum += figures[i].Position; count++; }
            if (count > 0) party = sum / count + Vector3.up * 1.0f;
            // an arc over the stage (it would fill the frame on a straight line), then down the carpet
            at = Vector3.Lerp(at, CloseAt, u) + Vector3.up * (Mathf.Sin(u * Mathf.PI) * 2.2f);
            look = Vector3.Lerp(look, Vector3.Lerp(party, GateLook, .35f + .65f * u), u);
        }

        static float Length(Vector3[] path)
        {
            float l = 0;
            for (int k = 1; k < path.Length; k++) l += Vector3.Distance(path[k - 1], path[k]);
            return l;
        }
    }
}
