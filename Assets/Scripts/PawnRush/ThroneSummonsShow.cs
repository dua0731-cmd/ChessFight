using System.Collections.Generic;
using ChessFight.Game;
using ChessFight.Gameplay;
using ChessFight.Network;
using UnityEngine;
using UnityEngine.Rendering;

namespace ChessFight.PawnRush
{
    // The two kings' summons (JY 2026-11-22; the previz Tools/Previz/PawnRushEndingSummon clip 'king', the staging doc §7).
    // On an ending hall with a board (IThroneStage.Board) the white king stands on e1 and the black king on e8 - no
    // player's pieces in this mode - each with his staff, from the moment the hall is built. When the throne contest
    // begins (ThroneContest.OnBegan, on every PC from when it heard it) the hall dims, the kings' eyes light, white strikes
    // his staff, then black: the board lights from each king's square, an aurora wraps each king, a pillar rises on each
    // square a player is called to (ChessSummons: its own starting square); the host carries everyone onto them
    // (ThroneContest.Call: pulled forward, fast, along the way, held there); a pillar flares and goes once its piece is in;
    // "양 팀 집결!"; both staffs light the crown; 3 · 2 · 1; both strike, the lights come back and the platform counts.
    // Every effect is yellow, the same for both teams ("이펙트 빛 색은 두 팀 다 노란색으로").
    public sealed class ThroneSummonsShow : MonoBehaviour
    {
        // Seconds from the beginning. The platform counts from Lead + the hall's standoff (ThroneContest.Waiting).
        public const float StrikeWhite = .8f, StrikeBlack = 1.25f, Pull = 2.4f, Muster = 4.2f, Beam = 4.6f, Lead = 5.4f;
        static readonly Color Yellow = new Color(1f, .82f, .28f), Pale = new Color(1f, .95f, .75f);

        Transform hall;
        ThroneBoard board;
        CrownFinish finish;
        readonly King[] kings = new King[2];
        float start = -1f, go;
        bool playing;
        Transform crown;   // the hall's crown over the platform (HallParts.Crown's band)
        Camera eyeCam;     // the pictures' camera (else the main one)

        // What the show builds when it plays, and throws away when it stops.
        GameObject fx;
        Material add, addGrad, addRays, addDot;
        Texture2D grad, rays, dot;
        readonly List<Renderer> cells = new List<Renderer>();
        readonly List<Call> calls = new List<Call>();
        readonly List<Light> lights = new List<Light>();
        readonly List<float> lightWas = new List<float>();
        float ambientWas = -1f;
        MaterialPropertyBlock mpb;
        GUIStyle big;

        sealed class King
        {
            public Transform root, staff;
            public Renderer[] eyes;
            public Material eye;
            public Vector3 foot;       // the staff's foot (world)
            public Transform[] aurora;
            public Renderer[] auroraR;
            public Transform ray, flash;
            public Renderer rayR, flashR;
        }

        sealed class Call
        {
            public int Team, Runner, File, Rank;
            public PieceKind Kind;
            public Vector3 Spot;
            public float Rise, In = -1f;
            public Transform Pillar, Flare;
            public Renderer PillarR, FlareR;
        }

        // The show of an ending hall with a board, its kings built (on every PC, when the contest is set up).
        public static ThroneSummonsShow Ready(PawnRushSection ending, ThroneBoard board)
        {
            var show = ending.GetComponent<ThroneSummonsShow>();
            if (show == null) show = ending.gameObject.AddComponent<ThroneSummonsShow>();
            show.Build(ending.transform, board);
            return show;
        }

        // Each team's players and the squares they are called to (ChessSummons), the same on every PC for the same runners.
        public static void Squares(IReadOnlyList<CrownFinish.Runner> runners, List<(int team, int runner, int file, int rank)> into)
        {
            into.Clear();
            var kinds = new List<PieceKind>();
            var who = new List<int>();
            for (int team = 0; team < 2; team++)
            {
                kinds.Clear();
                who.Clear();
                for (int k = 0; k < runners.Count; k++)
                {
                    if (runners[k].Team != team || runners[k].Driver == null) continue;
                    who.Add(k);
                    kinds.Add(runners[k].Driver is IPromotable p ? p.Piece : PieceKind.Pawn);
                }
                var file = new int[who.Count];
                var rank = new int[who.Count];
                ChessSummons.Assign(kinds, file, rank);
                for (int i = 0; i < who.Count; i++) into.Add((team, who[i], file[i], rank[i]));
            }
        }

        // ------------------------------------------------------------------ the kings

        void Build(Transform ofHall, ThroneBoard ofBoard)
        {
            hall = ofHall;
            board = ofBoard;
            for (int team = 0; team < 2; team++)
            {
                if (kings[team] != null && kings[team].root != null) Kill(kings[team].root.gameObject);
                kings[team] = MakeKing(team);
            }
        }

        King MakeKing(int team)
        {
            float h = board.KingHeight;
            var k = new King();
            var root = new GameObject(team == Teams.White ? "King white (e1)" : "King black (e8)").transform;
            root.SetParent(hall, false);
            root.localPosition = board.King(team);
            root.localRotation = Quaternion.LookRotation(board.Facing(team), Vector3.up);
            k.root = root;
            var body = LitMaterials.NewLit(team == Teams.White ? new Color(.95f, .92f, .85f) : new Color(.15f, .13f, .12f), .55f);
            if (MapLook.PieceBody(root, "Body", PieceKind.King, h, body) == null)
            {
                var c = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                Kill(c.GetComponent<Collider>());
                c.transform.SetParent(root, false);
                c.transform.localPosition = Vector3.up * h * .5f;
                c.transform.localScale = new Vector3(.45f * h, .5f * h, .45f * h);
                c.GetComponent<Renderer>().sharedMaterial = body;
            }
            if (MapLook.RingMesh != null)
            {
                var ring = new GameObject("Team ring", typeof(MeshFilter), typeof(MeshRenderer));
                ring.transform.SetParent(root, false);
                ring.transform.localPosition = Vector3.up * .03f;
                ring.transform.localScale = Vector3.one * MapLook.FootRadius(PieceKind.King) * h * 1.1f;
                ring.GetComponent<MeshFilter>().sharedMesh = MapLook.RingMesh;
                ring.GetComponent<MeshRenderer>().sharedMaterial = Arena.Emissive(Arena.TeamColor(team, true), 1.2f);
            }
            // Eyes (they light at the call), white with a dark middle.
            k.eye = LitMaterials.NewLit(Color.white, .7f);
            var pupil = LitMaterials.NewLit(new Color(.08f, .08f, .08f), .7f);
            k.eyes = new Renderer[2];
            for (int s = 0; s < 2; s++)
            {
                var e = Ball(root, "Eye", new Vector3((s == 0 ? -.05f : .05f) * h, .68f * h, .11f * h), .055f * h, k.eye);
                k.eyes[s] = e.GetComponent<Renderer>();
                Ball(e.transform, "Pupil", new Vector3(0f, -.05f, .32f), .5f, pupil);
            }
            // The staff (the Queen of the Hill king's sceptre, grown to him): gold rod, collars, a little crown, a light.
            var staff = new GameObject("Staff").transform;
            staff.SetParent(root, false);
            staff.localPosition = new Vector3(-.42f * h, 0f, .12f * h);
            var gold = MapLook.Gold;
            Rod(staff, "Rod", .02f * h, 1.2f * h, gold);
            foreach (float y in new[] { .1f, .55f, 1f })
            {
                var collar = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                Kill(collar.GetComponent<Collider>());
                collar.name = "Collar";
                collar.transform.SetParent(staff, false);
                collar.transform.localPosition = Vector3.up * y * h;
                collar.transform.localScale = new Vector3(.06f * h, .012f * h, .06f * h);
                collar.GetComponent<Renderer>().sharedMaterial = gold;
            }
            Ball(staff, "Crown", Vector3.up * 1.24f * h, .1f * h, gold);
            Ball(staff, "Light", Vector3.up * 1.32f * h, .06f * h, k.eye);
            k.staff = staff;
            // He is solid: nobody walks through a king.
            var cap = root.gameObject.AddComponent<CapsuleCollider>();
            cap.center = Vector3.up * h * .5f;
            cap.height = h;
            cap.radius = MapLook.FootRadius(PieceKind.King) * h * .9f;
            return k;
        }

        static void Kill(Object o)
        {
            if (o == null) return;
            if (Application.isPlaying) Destroy(o);
            else DestroyImmediate(o);
        }

        static GameObject Ball(Transform parent, string name, Vector3 at, float size, Material m)
        {
            var b = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            Kill(b.GetComponent<Collider>());
            b.name = name;
            b.transform.SetParent(parent, false);
            b.transform.localPosition = at;
            b.transform.localScale = Vector3.one * size;
            b.GetComponent<Renderer>().sharedMaterial = m;
            return b;
        }

        static void Rod(Transform parent, string name, float radius, float length, Material m)
        {
            var r = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            Kill(r.GetComponent<Collider>());
            r.name = name;
            r.transform.SetParent(parent, false);
            r.transform.localPosition = Vector3.up * length * .5f;
            r.transform.localScale = new Vector3(2f * radius, length * .5f, 2f * radius);
            r.GetComponent<Renderer>().sharedMaterial = m;
        }

        // ------------------------------------------------------------------ playing

        // From now (when this PC heard the beginning); the platform counts at `startAt` seconds.
        public void Play(CrownFinish ofFinish, float startAt)
        {
            Stop();
            finish = ofFinish;
            start = Time.time;
            go = startAt;
            playing = true;
            MakeFx();
            crown = FindDeep(hall, "Crown band");
            var squares = new List<(int team, int runner, int file, int rank)>();
            Squares(finish.Runners, squares);
            int[] count = new int[2];
            foreach (var s in squares) AddCall(s.team, s.runner, s.file, s.rank, PieceKind.Pawn, count);
            KeepLights();
        }

        void AddCall(int team, int runner, int file, int rank, PieceKind kind, int[] count)
        {
            var c = new Call { Team = team, Runner = runner, File = file, Rank = rank, Kind = kind };
            c.Spot = hall.TransformPoint(board.Of(team, file, rank));
            c.Rise = (team == Teams.White ? StrikeWhite : StrikeBlack) + .2f + count[team]++ * .06f;
            c.Pillar = Shape(PrimitiveType.Cylinder, "Pillar", addGrad, out c.PillarR);
            c.Flare = Shape(PrimitiveType.Quad, "Flare", addDot, out c.FlareR);
            calls.Add(c);
        }

        // The hall's lights and the sky's, to dim (kept as they were, to put back).
        void KeepLights()
        {
            lights.Clear();
            lightWas.Clear();
            foreach (var l in hall.GetComponentsInChildren<Light>(true)) { lights.Add(l); lightWas.Add(l.intensity); }
            if (RenderSettings.sun != null && !lights.Contains(RenderSettings.sun)) { lights.Add(RenderSettings.sun); lightWas.Add(RenderSettings.sun.intensity); }
            ambientWas = RenderSettings.ambientIntensity;
        }

        // For pictures (the editor's AudienceHallPictures): the show `t` seconds in, without runners. These pieces are
        // called (white's and black's kinds) and taken as coming in one after another from Pull + .5 s; what comes back
        // is each one's square and whether it is in by then, for the pictures' stand-ins.
        public List<(int team, PieceKind kind, Vector3 spot, bool arrived)> Preview(float t, IReadOnlyList<PieceKind> white, IReadOnlyList<PieceKind> black, Camera eye)
        {
            eyeCam = eye;
            if (!playing)
            {
                playing = true;
                finish = null;
                start = 0f;
                go = Lead + 3f;
                MakeFx();
                crown = FindDeep(hall, "Crown band");
                int[] count = new int[2];
                for (int team = 0; team < 2; team++)
                {
                    var kinds = team == Teams.White ? white : black;
                    var file = new int[kinds.Count];
                    var rank = new int[kinds.Count];
                    ChessSummons.Assign(kinds, file, rank);
                    for (int i = 0; i < kinds.Count; i++) AddCall(team, -1, file[i], rank[i], kinds[i], count);
                }
                KeepLights();
            }
            var at = new List<(int team, PieceKind kind, Vector3 spot, bool arrived)>();
            for (int i = 0; i < calls.Count; i++)
            {
                float arrive = Pull + .5f + i * .1f;
                calls[i].In = t >= arrive ? arrive : -1f;
                at.Add((calls[i].Team, calls[i].Kind, calls[i].Spot, calls[i].In >= 0f));
            }
            Pose(t);
            Dim(t);
            Board(t);
            Pillars(t);
            for (int team = 0; team < 2; team++) Aurora(team, t);
            Rays(t);
            return at;
        }

        public Vector3 CrownAt => crown != null ? crown.position : hall.TransformPoint(board.Middle) + Vector3.up * 6f;

        public void Stop()
        {
            if (!playing) return;
            playing = false;
            for (int i = 0; i < lights.Count; i++) if (lights[i] != null) lights[i].intensity = lightWas[i];
            if (ambientWas >= 0f) RenderSettings.ambientIntensity = ambientWas;
            ambientWas = -1f;
            lights.Clear();
            lightWas.Clear();
            calls.Clear();
            cells.Clear();
            if (fx != null) Kill(fx);
            fx = null;
            foreach (var k in kings) if (k != null) { k.aurora = null; k.ray = k.flash = null; }
            Pose(-1f);
        }

        void OnDestroy()
        {
            Stop();
            foreach (var m in new[] { add, addGrad, addRays, addDot }) if (m != null) Kill(m);
            foreach (var t in new[] { grad, rays, dot }) if (t != null) Kill(t);
        }

        void Update()
        {
            if (!playing) return;
            float t = Time.time - start;
            if (t > go + 2f || finish == null) { Stop(); return; }
            Pose(t);
            Dim(t);
            Board(t);
            Pillars(t);
            for (int team = 0; team < 2; team++) Aurora(team, t);
            Rays(t);
        }

        // ------------------------------------------------------------------ what each part does

        static float Smooth(float x) { x = Mathf.Clamp01(x); return x * x * (3f - 2f * x); }
        float Strike(int team) => team == Teams.White ? StrikeWhite : StrikeBlack;

        // The staff: raised and struck at his call, raised to call his army in, raised high once they are in, struck
        // again at the start. The eyes light at the call. t < 0: at rest.
        void Pose(float t)
        {
            float h = board.KingHeight;
            for (int team = 0; team < 2; team++)
            {
                var k = kings[team];
                if (k == null || k.root == null) continue;
                float s = Strike(team), lift = 0f;
                if (t >= 0f)
                {
                    if (t < s) lift = .3f * Smooth((t - (s - .6f)) / .45f);
                    else if (t < Pull - .7f) lift = 0f;
                    else if (t < Pull + .3f) lift = .2f * Smooth((t - (Pull - .7f)) / .3f) * (1f - Smooth((t - Pull) / .3f));
                    else if (t < Muster) lift = 0f;
                    else if (t < go - .06f) lift = .35f * Smooth((t - Muster) / .4f);
                    else if (t < go) lift = .35f * (go - t) / .06f;
                }
                k.staff.localPosition = new Vector3(-.42f * h, lift * h, .12f * h);
                float hit = Hit(t, s) + Hit(t, go);
                k.root.localRotation = Quaternion.LookRotation(board.Facing(team), Vector3.up) * Quaternion.Euler(6f * hit, 0f, 0f);
                float eye = t < 0f ? 0f : .5f * Smooth(t / .8f) + .9f * (t >= s ? Mathf.Exp(-(t - s) / .5f) : 0f) + (t >= Muster ? .5f : 0f)
                                         - (t > go + .5f ? 1.6f * Smooth((t - go - .5f) / .6f) : 0f);
                LitMaterials.SetEmission(k.eye, Yellow * 4f * Mathf.Max(0f, eye));
                k.foot = k.staff.position;
            }
        }

        static float Hit(float t, float at) => t >= at ? Mathf.Exp(-(t - at) / .12f) : 0f;

        // The hall goes dark for the call and its lights come back at the start.
        void Dim(float t)
        {
            float k = 1f - .65f * Smooth(t / .8f) * (1f - Smooth((t - go) / .3f));
            for (int i = 0; i < lights.Count; i++) if (lights[i] != null) lights[i].intensity = lightWas[i] * k;
            RenderSettings.ambientIntensity = ambientWas * Mathf.Lerp(1f, .55f, 1f - k);
        }

        // Each square's light: a wave from each king's square at his strike, his half keeping a little light, the squares
        // someone is called to burning until they come, a pulse each second of the count, a wave from the middle at the start.
        void Board(float t)
        {
            float v = 11f * board.Square, width = .9f * board.Square, keep = Smooth((t - .3f) / .4f) * (1f - Smooth((t - go - .2f) / .6f));
            float pulse = 0f;
            for (int i = 1; i <= 3; i++) if (t >= go - i) pulse += Mathf.Exp(-(t - (go - i)) / .25f);
            for (int n = 0; n < cells.Count; n++)
            {
                int f = n % 8 + 1, r = n / 8 + 1;
                Vector3 p = board.At(f, r);
                float light = 0f;
                for (int team = 0; team < 2; team++)
                {
                    float a = t - Strike(team);
                    if (a < 0f) continue;
                    float d = Vector3.Distance(p, board.King(team)), e = (d - v * a) / width;
                    light += .9f * Mathf.Exp(-e * e) * Mathf.Max(0f, 1f - a / 1.6f);
                    bool half = team == Teams.White ? r <= 4 : r >= 5;
                    if (half && d < v * a) light += (.05f + .03f * ((f + r) & 1) + .18f * pulse) * keep;
                }
                foreach (var c in calls)
                    if (c.File == f && (c.Team == Teams.White ? c.Rank : 9 - c.Rank) == r && t >= c.Rise)
                        light += c.In < 0f ? .3f * Smooth((t - c.Rise) / .3f) * (.8f + .2f * Mathf.Sin(t * 10f)) : .45f * Mathf.Exp(-(t - c.In) / .25f) + .08f * keep;
                float a2 = t - go;
                if (a2 >= 0f)
                {
                    float d = Vector3.Distance(p, board.Middle), e = (d - v * a2) / width;
                    light += 1.2f * Mathf.Exp(-e * e) * Mathf.Max(0f, 1f - a2 / 1.4f);
                }
                Tint(cells[n], Yellow, light);
            }
        }

        // A pillar on each called square: up after its king's strike, a flare and gone once its piece stands there.
        void Pillars(float t)
        {
            float s = board.Square, h = Mathf.Min(12f, 5f * s);
            var runners = finish != null ? finish.Runners : null;
            var cam = eyeCam != null ? eyeCam : Camera.main;
            foreach (var c in calls)
            {
                if (runners != null && c.In < 0f && t >= Pull && c.Runner >= 0 && c.Runner < runners.Count)
                {
                    var hips = runners[c.Runner].Driver != null ? runners[c.Runner].Driver.FollowTarget : null;
                    if (hips != null)
                    {
                        Vector3 d = hips.position - c.Spot;
                        d.y = 0f;
                        if (d.magnitude < .45f * s && Mathf.Abs(hips.position.y - c.Spot.y) < 1.5f) c.In = t;
                    }
                }
                if (runners != null && c.In < 0f && t > go) c.In = t;   // nobody came (left the game): it goes at the start
                float up = Smooth((t - c.Rise) / .35f), after = c.In < 0f ? -1f : t - c.In;
                float keep = after > 0f ? 1f - Smooth((after - .05f) / .3f) : 1f, flare = after > 0f && after < .25f ? 1f - after / .25f : 0f;
                bool on = t >= c.Rise && keep > 0f;
                c.Pillar.gameObject.SetActive(on);
                if (on)
                {
                    float w = (.3f + .15f * flare) * s, hh = h * up;
                    c.Pillar.position = c.Spot + Vector3.up * hh * .5f;
                    c.Pillar.localScale = new Vector3(w, hh * .5f, w);
                    Tint(c.PillarR, Yellow, (.32f + .08f * Mathf.Sin(t * 9f + c.Spot.z)) * keep + .3f * flare);
                }
                bool flash = after >= 0f && after < .35f;
                c.Flare.gameObject.SetActive(flash);
                if (flash)
                {
                    c.Flare.position = c.Spot + Vector3.up * .5f;
                    if (cam != null) c.Flare.rotation = cam.transform.rotation;
                    c.Flare.localScale = Vector3.one * s * (.6f + 1.6f * (1f - after / .35f));
                    Tint(c.FlareR, Pale, 1f - after / .35f);
                }
            }
        }

        // The aurora: three curtains of light turning round each king from his strike to the start.
        void Aurora(int team, float t)
        {
            var k = kings[team];
            if (k == null || k.aurora == null) return;
            float s = Strike(team), on = Smooth((t - s) / .3f) * (1f - Smooth((t - go) / .6f));
            float pulse = 1.4f * Mathf.Exp(-Mathf.Max(0f, t - s) / .16f) + (t >= Muster ? .8f * Mathf.Exp(-(t - Muster) / .3f) : 0f);
            float grow = .6f + .4f * Smooth((t - s) / .35f), h = board.KingHeight;
            for (int i = 0; i < k.aurora.Length; i++)
            {
                bool vis = on > .01f;
                k.aurora[i].gameObject.SetActive(vis);
                if (!vis) continue;
                k.aurora[i].position = k.root.position;
                k.aurora[i].rotation = Quaternion.Euler(0f, i * 120f + (i % 2 == 0 ? 50f : -35f) * t, 0f);
                k.aurora[i].localScale = new Vector3(grow, grow, grow);
                Tint(k.auroraR[i], Yellow, .7f * on * (.9f + .5f * pulse));
            }
            // The strike's flash at the staff's foot.
            float since = Mathf.Min(t - s >= 0f ? t - s : 99f, t - go >= 0f ? t - go : 99f);
            bool flash = since < .35f;
            k.flash.gameObject.SetActive(flash);
            if (flash)
            {
                var cam = eyeCam != null ? eyeCam : Camera.main;
                k.flash.position = new Vector3(k.foot.x, k.root.position.y + .2f * h, k.foot.z);
                if (cam != null) k.flash.rotation = cam.transform.rotation;
                k.flash.localScale = Vector3.one * h * (.6f + 2.4f * (1f - since / .35f));
                Tint(k.flashR, Pale, 1f - since / .35f);
            }
        }

        // Both staffs light the crown over the platform from Beam to the start.
        void Rays(float t)
        {
            float on = Smooth((t - Beam) / .12f) * (1f - Smooth((t - go) / .15f)), grow = Smooth((t - Beam) / .3f);
            for (int team = 0; team < 2; team++)
            {
                var k = kings[team];
                if (k == null || k.ray == null) continue;
                bool vis = crown != null && on > .01f;
                k.ray.gameObject.SetActive(vis);
                if (!vis) continue;
                Vector3 from = k.staff.position + Vector3.up * 1.32f * board.KingHeight, to = Vector3.Lerp(from, crown.position + Vector3.up * .6f, grow), d = to - from;
                k.ray.position = from + d * .5f;
                k.ray.rotation = Quaternion.FromToRotation(Vector3.up, d.normalized);
                k.ray.localScale = new Vector3(.18f, d.magnitude * .5f, .18f);
                Tint(k.rayR, Yellow, .9f * on * (.8f + .2f * Mathf.Sin(t * 14f)));
            }
        }

        static Transform FindDeep(Transform under, string name)
        {
            foreach (var t in under.GetComponentsInChildren<Transform>(true)) if (t.name == name) return t;
            return null;
        }

        // ------------------------------------------------------------------ the pieces of light

        void MakeFx()
        {
            if (grad == null)
            {
                grad = new Texture2D(4, 64, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, name = "Summons fade" };
                for (int y = 0; y < 64; y++) for (int x = 0; x < 4; x++) grad.SetPixel(x, y, new Color(1f, 1f, 1f, Mathf.Pow(1f - y / 63f, 1.6f)));
                grad.Apply();
                rays = new Texture2D(64, 64, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Repeat, name = "Summons rays" };
                for (int y = 0; y < 64; y++)
                    for (int x = 0; x < 64; x++)
                    {
                        float u = x / 64f, v = y / 63f, ray = Mathf.Pow(.5f + .5f * Mathf.Sin(u * 6.2832f * 7f + 2f * Mathf.Sin(u * 6.2832f * 2f)), 6f);
                        float fadeV = Smooth(v / .12f) * (1f - Smooth((v - .2f) / .8f)), ends = Smooth(u / .14f) * (1f - Smooth((u - .86f) / .14f));
                        rays.SetPixel(x, y, new Color(1f, 1f, 1f, (.08f + ray) * fadeV * ends));
                    }
                rays.Apply();
                dot = new Texture2D(64, 64, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, name = "Summons dot" };
                for (int y = 0; y < 64; y++)
                    for (int x = 0; x < 64; x++)
                    {
                        float d = Vector2.Distance(new Vector2(x, y), new Vector2(31.5f, 31.5f)) / 32f;
                        dot.SetPixel(x, y, new Color(1f, 1f, 1f, Mathf.Pow(Mathf.Clamp01(1f - d), 2f)));
                    }
                dot.Apply();
                add = Additive("Summons light", null);
                addGrad = Additive("Summons pillar", grad);
                addRays = Additive("Summons aurora", rays);
                addDot = Additive("Summons flare", dot);
                mpb = new MaterialPropertyBlock();
            }
            fx = new GameObject("Summons show");
            fx.transform.SetParent(hall, false);
            for (int n = 0; n < 64; n++)
            {
                var q = Shape(PrimitiveType.Quad, "Square light", add, out var r);
                q.SetParent(hall, false);
                q.localPosition = board.At(n % 8 + 1, n / 8 + 1) + Vector3.up * .04f;
                q.localRotation = Quaternion.LookRotation(Vector3.down, board.File);
                q.localScale = Vector3.one * board.Square * .94f;
                q.SetParent(fx.transform, true);
                cells.Add(r);
                Tint(r, Yellow, 0f);
            }
            float h = board.KingHeight;
            for (int team = 0; team < 2; team++)
            {
                var k = kings[team];
                if (k == null) continue;
                k.aurora = new Transform[3];
                k.auroraR = new Renderer[3];
                float[] radius = { .5f, .66f, .82f }, height = { 1.5f, 2f, 2.5f }, arc = { 1.3f, 1.1f, 1.5f };
                for (int i = 0; i < 3; i++)
                {
                    var curtain = new GameObject("Aurora", typeof(MeshFilter), typeof(MeshRenderer));
                    curtain.transform.SetParent(fx.transform, false);
                    curtain.GetComponent<MeshFilter>().sharedMesh = Curtain(radius[i] * h, height[i] * h, arc[i] * Mathf.PI);
                    var r = curtain.GetComponent<MeshRenderer>();
                    r.sharedMaterial = addRays;
                    r.shadowCastingMode = ShadowCastingMode.Off;
                    k.aurora[i] = curtain.transform;
                    k.auroraR[i] = r;
                }
                k.ray = Shape(PrimitiveType.Cylinder, "Ray to the crown", add, out k.rayR);
                k.flash = Shape(PrimitiveType.Quad, "Strike flash", addDot, out k.flashR);
            }
        }

        Transform Shape(PrimitiveType type, string name, Material m, out Renderer r)
        {
            var go = GameObject.CreatePrimitive(type);
            Kill(go.GetComponent<Collider>());
            go.name = name;
            go.transform.SetParent(fx.transform, false);
            r = go.GetComponent<Renderer>();
            r.sharedMaterial = m;
            r.shadowCastingMode = ShadowCastingMode.Off;
            r.receiveShadows = false;
            go.SetActive(type == PrimitiveType.Quad && name == "Square light");
            return go.transform;
        }

        // An open arc of a cylinder, u round it and v up it (the aurora's texture fades towards its top and its ends).
        static Mesh Curtain(float radius, float height, float arc)
        {
            const int n = 32;
            var v = new Vector3[(n + 1) * 2];
            var uv = new Vector2[v.Length];
            var tri = new int[n * 12];
            for (int i = 0; i <= n; i++)
            {
                float a = arc * i / n, r0 = radius, r1 = radius * 1.25f;
                v[2 * i] = new Vector3(Mathf.Cos(a) * r0, 0f, Mathf.Sin(a) * r0);
                v[2 * i + 1] = new Vector3(Mathf.Cos(a) * r1, height, Mathf.Sin(a) * r1);
                uv[2 * i] = new Vector2((float)i / n, 0f);
                uv[2 * i + 1] = new Vector2((float)i / n, 1f);
            }
            for (int i = 0, k = 0; i < n; i++)
            {
                int a = 2 * i, b = a + 1, c = a + 2, d = a + 3;
                tri[k++] = a; tri[k++] = b; tri[k++] = c; tri[k++] = c; tri[k++] = b; tri[k++] = d;   // both faces
                tri[k++] = a; tri[k++] = c; tri[k++] = b; tri[k++] = c; tri[k++] = d; tri[k++] = b;
            }
            var mesh = new Mesh { name = "Aurora curtain", vertices = v, uv = uv, triangles = tri };
            mesh.RecalculateBounds();
            return mesh;
        }

        static Material Additive(string name, Texture texture)
        {
            var shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
            if (shader == null) return Arena.Glow(new Color(Yellow.r, Yellow.g, Yellow.b, .5f));
            var m = new Material(shader) { name = name };
            m.SetFloat("_Surface", 1f);
            m.SetFloat("_Blend", 2f);   // additive
            m.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            m.SetFloat("_DstBlend", (float)BlendMode.One);
            m.SetFloat("_ZWrite", 0f);
            m.SetFloat("_Cull", 0f);
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            if (texture != null) m.SetTexture("_BaseMap", texture);
            m.SetColor("_BaseColor", Yellow);
            m.renderQueue = (int)RenderQueue.Transparent;
            return m;
        }

        void Tint(Renderer r, Color c, float strength)
        {
            if (r == null) return;
            r.GetPropertyBlock(mpb);
            mpb.SetColor("_BaseColor", new Color(c.r, c.g, c.b, Mathf.Clamp01(strength)));
            mpb.SetColor("_Color", new Color(c.r, c.g, c.b, Mathf.Clamp01(strength)));
            r.SetPropertyBlock(mpb);
        }

        // ------------------------------------------------------------------ on screen

        void OnGUI()
        {
            if (!playing || Event.current.type != EventType.Repaint || LoadingScreenView.Showing) return;
            float t = Time.time - start, a = Mathf.Clamp01((t - Muster) / .05f) * (1f - Mathf.Clamp01((t - Muster - .6f) / .3f));
            if (a <= 0f) return;
            float unit = Mathf.Clamp(Screen.height / 1080f, .6f, 2f);
            if (big == null) big = new GUIStyle(GUI.skin.label) { font = RuntimePanels.KoreanFont, alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold };
            var was = GUI.color;
            big.fontSize = Mathf.RoundToInt(72f * unit * (1f + .3f * (1f - Mathf.Clamp01((t - Muster) / .2f))));
            var at = new Rect(0f, Screen.height * .26f, Screen.width, 110f * unit);
            GUI.color = new Color(.1f, .07f, 0f, .85f * a);
            string muster = Loc.T("양 팀 집결!");
            GUI.Label(new Rect(at.x + 4f * unit, at.y + 4f * unit, at.width, at.height), muster, big);
            GUI.color = new Color(Yellow.r, Yellow.g, Yellow.b, a);
            GUI.Label(at, muster, big);
            GUI.color = was;
        }
    }
}
