using System;
using System.Collections.Generic;
using ChessFight.Gameplay;
using UnityEngine;

namespace ChessFight.PawnRush
{
    // The stage's show on the dressed hall (대연회장 연출 R845; the user 11-25 of a picture of the curtain: "극장 가림막 좀 더
    // 자연스럽게 움직이고 연출 효과"). The curtain's twenty pleats (CurtainPleat, each a wall a pawn hangs on) keep their
    // places, colliders and timing; only what is drawn changes:
    //   - a pleat's picture folds up as its neighbours come close (narrower, its folds deeper), so the bunched curtain is
    //     a gathered curtain and not twenty 1.05 m boards through one another 0.3 m apart; it spreads again as they part;
    //   - its hem lags behind the rail as it is drawn, swings past when it stops and settles (a damped swing worked out
    //     from the clock), and a slow wave of air runs through the half while it moves;
    //   - the gold tassels under the pelmet swing with the air the curtain drags;
    //   - shortly before it opens, light leaks out of the seam between the two halves onto the dance floor; as it opens
    //     the stage lights come up on the orchestra, and they go down as it shuts.
    // Every motion is a pure function of the shared obstacle clock (HANDOFF rule 7): nothing is sent, nothing adds up.
    [DefaultExecutionOrder(205)]
    public sealed class BanquetStageShow : MonoBehaviour
    {
        // The hem's swing: a heavy velvet's (Hz, damping), the most it lags (m); how much deeper the folds go gathered
        // (the depth times 1 + Deepen at no width); the narrowest a pleat is drawn (the bunched curtain's 0.3 m spacing).
        const float HemHertz = .55f, HemDamping = .2f, HemMost = .75f, Deepen = .55f, Gathered = .3f, Taut = 1.35f, SeamPast = .15f;
        const float Air = .05f, AirWave = 3.2f, AirFull = 6f;                  // the wave of air: m at AirFull m/s, m long
        const float TasselHertz = 1.05f, TasselDamping = .12f, TasselLength = .9f, TasselMost = .4f, TasselPush = 1.2f;
        const float LeakFrom = 1.6f;                                            // s before the curtain opens
        const float StageLight = 3.2f, LeakLight = 5f;

        sealed class Pleat
        {
            public Transform root;
            public CurtainPleat mover;
            public int side, index;                                             // -1 south, +1 north; 0 the leading
            public Transform[] rows = Array.Empty<Transform>();          // its bones, rail (0) to hem
            public float shut;                                                  // where it hangs in the shut curtain (z)
            public float z, hem, top, low;                                      // now: its middle, its hem's lag; drawn widths (0..1)
            public float nudge;
        }

        sealed class Tassel
        {
            public Transform bone;
            public float side;                                                  // the half under it
        }

        readonly List<Pleat> pleats = new List<Pleat>();
        readonly List<Tassel> tassels = new List<Tassel>();
        readonly List<UnityEngine.Object> owned = new List<UnityEngine.Object>();
        Transform frame;
        CurtainPleat clock;
        float top = GrandBanquetHallBuilder.RailY, bottom = GrandBanquetHallBuilder.PleatBottom, southTravel, northTravel;
        Light leak;
        Light[] stage = Array.Empty<Light>();
        Transform blade, halo;

        public int Pleats => pleats.Count;
        public int Meshes { get; private set; }
        // The time this show takes a frame (ms, smoothed) and how many bones it sets (the graphics card moves the vertices).
        public float Cost { get; private set; }
        public int Bones { get; private set; }
        readonly System.Diagnostics.Stopwatch watch = new System.Diagnostics.Stopwatch();
        public int TasselCount => tassels.Count;
        // As last drawn: the most a tassel swings (degrees).
        public float Swung { get; private set; }
        // As last drawn: the curtain open (0..1), the most two neighbouring pleats' pictures overlap at the rail and at
        // the hem (m), the narrowest pleat (its width over 1.05), the most a hem lags (m), the leak and the stage lights.
        public float Opened { get; private set; }
        public float TopOverlap { get; private set; }
        public float HemOverlap { get; private set; }
        public float Narrowest { get; private set; }
        public float HemLag { get; private set; }
        public float Leak { get; private set; }
        public float Lit { get; private set; }
        public int Musicians { get; private set; }
        // As last drawn: how far the players lean from where they stand (degrees, the most of them).
        public float Playing { get; private set; }

        public void Configure(GrandBanquetHallDressing dressing)
        {
            frame = dressing.transform;
            foreach (int side in new[] { -1, 1 })
                for (int k = 0; k < GrandBanquetHallBuilder.Pleats; k++)
                {
                    string name = "BH pleat " + (side < 0 ? "S" : "N") + (k + 1);
                    foreach (var root in dressing.FindAll(name))
                    {
                        if (!root.TryGetComponent<CurtainPleat>(out var mover)) continue;
                        var p = new Pleat
                        {
                            root = root, mover = mover, side = side, index = k,
                            shut = GrandBanquetHallBuilder.CurtainZ + side * (.55f + GrandBanquetHallBuilder.PleatW * k),
                        };
                        var model = GrandBanquetHallDressing.ModelOn(root, "bh_curtain_pleat");
                        if (model != null) Rig(p, model);
                        pleats.Add(p);
                        if (clock == null) clock = mover;
                        if (k == 0) { if (side < 0) southTravel = mover.Travel; else northTravel = mover.Travel; }
                    }
                }
            SetUpTassels();
            SetUpLights();
            SetUpMusicians();
            Debug.Log("[대연회장 무대 연출] 막 주름 " + pleats.Count + "개(메시 " + Meshes + ") · 가리개 술 " + tassels.Count + "개 · 뼈 " + Bones +
                      " · 무대 조명 " + stage.Length + "개");
        }

        // A pleat's bones: Rows of them from the rail to the hem, on its root; its meshes (every LOD) skinned to them,
        // each vertex between the two rows over and under it.
        const int Rows = 6;

        void Rig(Pleat p, Transform model)
        {
            p.rows = new Transform[Rows];
            for (int r = 0; r < Rows; r++)
                p.rows[r] = BanquetShowKit.Bone("BH pleat row " + r, p.root, new Vector3(0f, RowY(r), 0f));
            Bones += Rows;
            foreach (var filter in model.GetComponentsInChildren<MeshFilter>(true))
                if (BanquetShowKit.Skin(filter, p.root, p.rows, WeighRow, 1f, owned) != null) Meshes++;
        }

        float RowY(int r) => top - (top - bottom) * r / (Rows - 1);

        void WeighRow(Vector3 v, out int a, out int b, out float wa)
        {
            float q = Mathf.Clamp01((top - v.y) / Mathf.Max(.1f, top - bottom)) * (Rows - 1);
            a = Mathf.Min(Rows - 2, Mathf.FloorToInt(q));
            b = a + 1;
            wa = 1f - (q - a);
        }

        // The pelmet's tassels (bh_b5_stage_model.pelmet: one under every junction of its swags, 2.41 m apart, hung from
        // 0.9 over the pelmet's foot, in front of its face): a bone at each tassel's top; the vertices in front of the
        // face, under the swags' ends, near a junction follow it (the cord's top blending into the still pelmet).
        void SetUpTassels()
        {
            var rail = GrandBanquetHallDressing.ModelOn(frame, "bh_curtain_rail");
            if (rail == null) return;
            const float runZ0 = 12.5f, runZ1 = 51.05f, span = 2.4f;
            int n = Mathf.Max(1, Mathf.RoundToInt((runZ1 - runZ0) / span));
            float face = GrandBanquetHallBuilder.CurtainX + .5f, foot = GrandBanquetHallBuilder.RailY + .3f, hung = foot + .9f;
            var bones = new Transform[n + 2];
            bones[0] = BanquetShowKit.Bone("BH pelmet still", frame, Vector3.zero);
            for (int j = 0; j <= n; j++)
            {
                float jz = runZ0 + (runZ1 - runZ0) * j / n + (j == 0 ? .15f : j == n ? -.15f : 0f);
                bones[j + 1] = BanquetShowKit.Bone("BH pelmet tassel " + j, frame, new Vector3(face + .14f, hung, jz));
                tassels.Add(new Tassel { bone = bones[j + 1], side = jz < GrandBanquetHallBuilder.CurtainZ ? -1f : 1f });
            }
            Bones += bones.Length;
            void Weigh(Vector3 v, out int a, out int b, out float wa)
            {
                a = b = 0;
                wa = 1f;
                if (v.x < face + .01f || v.y > foot + .75f) return;
                int nearest = Mathf.Clamp(Mathf.RoundToInt((v.z - runZ0) / ((runZ1 - runZ0) / n)), 0, n);
                if (Mathf.Abs(v.z - bones[nearest + 1].localPosition.z) > .17f) return;
                a = nearest + 1;
                wa = Mathf.Clamp01((foot + .75f - v.y) / .2f);
            }
            foreach (var filter in rail.GetComponentsInChildren<MeshFilter>(true))
                if (filter.name.IndexOf("pelmet", StringComparison.OrdinalIgnoreCase) >= 0)
                    BanquetShowKit.Skin(filter, frame, bones, Weigh, .5f, owned);
        }

        // The light out of the seam (a blade of warm light on the seam and a spot through it onto the dance floor) and
        // the stage's lights (spots from under the pelmet's line down onto the orchestra).
        void SetUpLights()
        {
            var lights = new GameObject("BH stage show lights").transform;
            lights.SetParent(frame, false);
            var glow = BanquetShowKit.GlowMaterial(new Color(1f, .78f, .5f, 1f), owned);
            if (glow != null)
            {
                blade = BanquetShowKit.Drawn("BH curtain seam light", lights, BanquetShowKit.BladeMesh(), glow);
                halo = BanquetShowKit.Drawn("BH curtain seam glow", lights, BanquetShowKit.BladeMesh(), glow);
                blade.gameObject.SetActive(false);
                halo.gameObject.SetActive(false);
            }
            leak = new GameObject("BH curtain seam spill").AddComponent<Light>();
            leak.transform.SetParent(lights, false);
            leak.type = LightType.Spot;
            leak.color = new Color(1f, .8f, .55f);
            leak.range = 48f;
            leak.spotAngle = 13f;
            leak.innerSpotAngle = 4f;
            leak.shadows = LightShadows.None;
            leak.enabled = false;
            float x = GrandBanquetHallBuilder.StageX1 - 1.5f, y = GrandBanquetHallBuilder.RailY - .4f, floor = GrandBanquetHallBuilder.StageY;
            var at = new[] { GrandBanquetHallBuilder.StageZ0 + 3.5f, GrandBanquetHallBuilder.CurtainZ - 1.5f, GrandBanquetHallBuilder.StageZ1 - 3.5f };
            stage = new Light[at.Length];
            for (int i = 0; i < at.Length; i++)
            {
                var s = new GameObject("BH stage light " + (i + 1)).AddComponent<Light>();
                s.transform.SetParent(lights, false);
                s.type = LightType.Spot;
                s.color = new Color(1f, .86f, .66f);
                s.range = 22f;
                s.spotAngle = 62f;
                s.innerSpotAngle = 30f;
                s.shadows = LightShadows.None;
                s.transform.localPosition = new Vector3(x, y, at[i]);
                s.transform.localRotation = Quaternion.LookRotation(new Vector3(-28.4f, floor + 1.5f, at[i]) - s.transform.localPosition);
                s.enabled = false;
                stage[i] = s;
            }
        }

        void OnDestroy()
        {
            foreach (var o in owned)
                if (o != null) Destroy(o);
        }

        // ------------------------------------------------------------------ every frame

        void LateUpdate()
        {
            if (clock == null || frame == null) return;
            watch.Restart();
            double now = ObstacleClock.Now;
            float opened = clock.Open(now);
            Opened = opened;
            // The hem's lag, as a share of a pleat's own travel (every pleat runs the same eased course).
            float lag = BanquetShowKit.Lag(t => clock.Open(t), now, HemHertz, HemDamping);
            float speed = Mathf.Abs(clock.Open(now + .02) - clock.Open(now - .02)) / .04f;
            Curtain(lag, speed, (float)(now % 1000.0));
            Tassels(now);
            Lights(now, opened);
            Play(now, Lit);
            watch.Stop();
            Cost = Mathf.Lerp(Cost, (float)watch.Elapsed.TotalMilliseconds, Cost == 0f ? 1f : .05f);
        }

        void Curtain(float lag, float speed, float time)
        {
            float topOverlap = 0f, hemOverlap = 0f, narrowest = 1f, most = 0f;
            foreach (int side in new[] { -1, 1 })
            {
                var half = new List<Pleat>();
                foreach (var p in pleats)
                {
                    if (p.side != side || p.root == null) continue;
                    p.z = frame.InverseTransformPoint(p.root.position).z;
                    p.hem = BanquetShowKit.Soft(p.mover.Travel * lag, HemMost);
                    // The halves meet at the seam: a hem swinging in toward the middle is stopped there (a hand's
                    // breadth past it) instead of passing through the other half's; the pleats behind it crowd up.
                    float inward = -side * p.hem;
                    float room = -side * (GrandBanquetHallBuilder.CurtainZ - p.z) + SeamPast - .5f * GrandBanquetHallBuilder.PleatW;
                    if (inward > 0f) p.hem = -side * (room > .01f ? BanquetShowKit.Soft(inward, room) : 0f);
                    most = Mathf.Max(most, Mathf.Abs(p.hem));
                    half.Add(p);
                }
                if (half.Count == 0) continue;
                // Widths by who is next to whom now (the north half's pleats pass one another as it opens).
                half.Sort((a, b) => a.z.CompareTo(b.z));
                for (int i = 0; i < half.Count; i++)
                {
                    var p = half[i];
                    p.top = Width(half, i, false);
                    p.low = Width(half, i, true);
                    narrowest = Mathf.Min(narrowest, Mathf.Min(p.top, p.low));
                }
                for (int i = 0; i + 1 < half.Count; i++)
                {
                    Pleat a = half[i], b = half[i + 1];
                    float w = GrandBanquetHallBuilder.PleatW * .5f;
                    topOverlap = Mathf.Max(topOverlap, w * (a.top + b.top) - (b.z - a.z));
                    hemOverlap = Mathf.Max(hemOverlap, w * (a.low + b.low) - (b.z + b.hem - a.z - a.hem));
                }
                // Gathered so close that the pictures must lie over one another: they stand a little in front of and
                // behind one another rather than in one plane.
                for (int i = 0; i < half.Count; i++)
                {
                    var p = half[i];
                    float crowd = Mathf.Clamp01(1f - Mathf.Min(p.top, p.low) / .45f);
                    p.nudge = ((i % 3) - 1) * .035f * crowd;
                }
            }
            TopOverlap = topOverlap;
            HemOverlap = hemOverlap;
            Narrowest = narrowest;
            HemLag = most;
            float air = Air * Mathf.Clamp01(speed * Mathf.Max(southTravel, northTravel) / AirFull);
            foreach (var p in pleats) Draw(p, air, time);
        }

        // A pleat's drawn width (its share of 1.05) at its rail or its hem: half the room to each neighbour (a half's end
        // pleats: the room to their one neighbour), never wider than modelled nor narrower than the bunched spacing.
        static float Width(List<Pleat> half, int i, bool hem)
        {
            float At(int k) => half[k].z + (hem ? half[k].hem : 0f);
            float room;
            if (half.Count == 1) room = GrandBanquetHallBuilder.PleatW;
            else if (i == 0) room = At(1) - At(0);
            else if (i == half.Count - 1) room = At(i) - At(i - 1);
            else room = .5f * (At(i + 1) - At(i - 1));
            // at the hem a pleat may be drawn a little wider, pulled taut, so neighbours that swing apart leave no slit
            return Mathf.Clamp(Mathf.Abs(room) / GrandBanquetHallBuilder.PleatW, Gathered, hem ? Taut : 1f);
        }

        // The rows' bones: each row as wide as its share of the way from the rail's width to the hem's, its folds deeper
        // the narrower it is drawn; lagging by the hem's lag times (its depth)^1.6; the wave of air across it.
        void Draw(Pleat p, float air, float time)
        {
            float wave = 2f * Mathf.PI / AirWave, beat = 2.4f * time;
            for (int r = 0; r < p.rows.Length; r++)
            {
                var bone = p.rows[r];
                if (bone == null) continue;
                float h = (float)r / (Rows - 1), shape = Mathf.Pow(h, 1.6f);
                float f = Mathf.Lerp(p.top, p.low, h), deep = 1f + Deepen * (1f - f);
                float z = p.hem * shape;
                float x = p.nudge + air * shape * Mathf.Sin(wave * (p.z + z) - beat);
                bone.localPosition = new Vector3(x, RowY(r), z);
                bone.localScale = new Vector3(deep, 1f, f);
            }
        }

        // The tassels swing about their tops along the rail with the air the half under them drags, and turn a little.
        void Tassels(double now)
        {
            if (tassels.Count == 0) return;
            // pushed along by the air: the curtain's speed (a share of its travel per second) times the half's travel
            float swing = BanquetShowKit.Response(t => TasselPush * BanquetShowKit.Speed(clock.Open, t), now, TasselHertz, TasselDamping);
            float south = BanquetShowKit.Soft(southTravel * swing / TasselLength, TasselMost);
            float north = BanquetShowKit.Soft(northTravel * swing / TasselLength, TasselMost);
            Swung = Mathf.Max(Mathf.Abs(south), Mathf.Abs(north)) * Mathf.Rad2Deg;
            foreach (var t in tassels)
            {
                if (t.bone == null) continue;
                // a little different for every tassel, so they do not swing as one
                float a = (t.side < 0f ? south : north) * (1f + .18f * Mathf.Sin(t.bone.localPosition.z * 2.7f));
                t.bone.localRotation = Quaternion.AngleAxis(-a * Mathf.Rad2Deg, Vector3.right) * Quaternion.AngleAxis(a * 25f, Vector3.up);
            }
        }

        // The seam's light grows in the last moments before the curtain opens and is lost as the stage lights come up;
        // the stage lights follow the curtain open and shut.
        void Lights(double now, float opened)
        {
            float period = clock.Period, shutFor = GrandBanquetHallBuilder.CurtainShut;
            float u = (float)(now % period);
            if (u < 0f) u += period;
            float before = BanquetShowKit.Smooth((u - (shutFor - LeakFrom)) / LeakFrom);
            float leakNow = (u < shutFor ? before : 1f) * (1f - BanquetShowKit.Smooth(opened / .3f));
            float lit = BanquetShowKit.Smooth((opened - .08f) / .55f);
            float flicker = .94f + .06f * Mathf.PerlinNoise(Time.time * 1.3f, .3f);
            Leak = leakNow;
            Lit = lit;
            // where the halves part: the leading pleats' drawn edges
            float south = float.MinValue, north = float.MaxValue;
            foreach (var p in pleats)
            {
                if (p.index != 0) continue;
                float edge = .5f * GrandBanquetHallBuilder.PleatW * p.top;
                if (p.side < 0) south = Mathf.Max(south, p.z + edge); else north = Mathf.Min(north, p.z - edge);
            }
            if (south == float.MinValue || north == float.MaxValue) south = north = GrandBanquetHallBuilder.CurtainZ;
            float middle = .5f * (south + north), gap = Mathf.Max(0f, north - south);
            bool showLeak = leakNow > .01f;
            if (blade != null)
            {
                if (blade.gameObject.activeSelf != showLeak) { blade.gameObject.SetActive(showLeak); halo.gameObject.SetActive(showLeak); }
                if (showLeak)
                {
                    float x = GrandBanquetHallBuilder.CurtainX + .17f, h = top - bottom;
                    var turn = Quaternion.Euler(0f, 90f, 0f);
                    blade.localPosition = new Vector3(x, bottom, middle);
                    blade.localRotation = turn;
                    blade.localScale = new Vector3(Mathf.Max(.14f, gap + .1f), h, 1f);
                    halo.localPosition = new Vector3(x + .03f, bottom - .1f, middle);
                    halo.localRotation = turn;
                    halo.localScale = new Vector3(Mathf.Max(1.3f, gap + 1.4f), h + .3f, 1f);
                    SetGlow(blade, new Color(1f, .8f, .55f, .9f * leakNow * flicker));
                    SetGlow(halo, new Color(1f, .7f, .42f, .22f * leakNow * flicker));
                }
            }
            if (leak != null)
            {
                if (leak.enabled != showLeak) leak.enabled = showLeak;
                leak.transform.localPosition = new Vector3(GrandBanquetHallBuilder.CurtainX - 5f, GrandBanquetHallBuilder.RailY - 2.5f, middle);
                leak.transform.localRotation = Quaternion.LookRotation(new Vector3(-6f, 0f, middle) - leak.transform.localPosition);
                leak.spotAngle = 9f + Mathf.Min(gap, 3f) * 6f;
                leak.intensity = LeakLight * leakNow * flicker;
            }
            bool on = lit > .005f;
            foreach (var s in stage)
            {
                if (s.enabled != on) s.enabled = on;
                s.intensity = StageLight * lit * flicker;
            }
        }

        // ------------------------------------------------------------------ the orchestra

        // The players (bh_musicians: four violinists on the stage, the two bassists in the pit; each figurine one piece
        // with its LODs, facing the hall, +x): they play while the curtain is open and the stage lit - a violinist sways
        // with the bar and turns with every bow stroke, a bassist leans into his long strokes - and stand still shut.
        // A waltz's three beats to a bar at 1.62 s (the dance's own: four bars to a queen's 4.8 s stop... near enough).
        const float Bar = 1.6f;

        sealed class Player
        {
            public Transform[] parts;
            public Vector3[] at;
            public Quaternion[] turn;
            public Vector3 foot;
            public bool bass;
            public float seed;
        }

        readonly List<Player> players = new List<Player>();

        void SetUpMusicians()
        {
            var model = GrandBanquetHallDressing.ModelOn(frame, "bh_musicians");
            var turnBack = Quaternion.Inverse(frame.rotation);
            foreach (var part in BanquetShowKit.Parts(model))
            {
                bool bass = part.Key.IndexOf("bassist", StringComparison.OrdinalIgnoreCase) >= 0;
                if (!bass && part.Key.IndexOf("violinist", StringComparison.OrdinalIgnoreCase) < 0) continue;
                var p = new Player { bass = bass, parts = new Transform[part.Value.Count] };
                p.at = new Vector3[p.parts.Length];
                p.turn = new Quaternion[p.parts.Length];
                var bounds = new Bounds();
                bool any = false;
                for (int i = 0; i < p.parts.Length; i++)
                {
                    var t = part.Value[i].transform;
                    p.parts[i] = t;
                    p.at[i] = frame.InverseTransformPoint(t.position);
                    p.turn[i] = turnBack * t.rotation;
                    if (t.TryGetComponent<Renderer>(out var r))
                    {
                        if (!any) { bounds = r.bounds; any = true; } else bounds.Encapsulate(r.bounds);
                    }
                }
                // its foot: under the middle of what is drawn
                var middle = frame.InverseTransformPoint(bounds.center);
                p.foot = new Vector3(middle.x, frame.InverseTransformPoint(bounds.min).y, middle.z);
                p.seed = Mathf.Repeat(p.foot.z * .173f, 1f);
                players.Add(p);
            }
            Musicians = players.Count;
        }

        void Play(double now, float lit)
        {
            float most = 0f;
            float beat = (float)(now % (Bar * 1000.0)) / Bar;                     // bars
            foreach (var p in players)
            {
                float b = beat + (p.bass ? 0f : p.seed * .08f);
                float bar = 2f * Mathf.PI * b, stroke = 2f * Mathf.PI * (p.bass ? b * .5f : b * 1.5f);   // a stroke a beat and a half / two bars
                float roll, pitch, yaw, bob;
                if (p.bass)
                {
                    // a slow lean into the long stroke and back, a dip on each bar's first beat
                    roll = 3.2f * Mathf.Sin(stroke);
                    pitch = -2.2f - 1.6f * Mathf.Pow(.5f + .5f * Mathf.Cos(bar), 3f);
                    yaw = 4.5f * Mathf.Sin(stroke + .6f);
                    bob = -.03f * Mathf.Pow(.5f + .5f * Mathf.Cos(bar), 4f);
                }
                else
                {
                    roll = 2.6f * Mathf.Sin(bar + p.seed * 2f);
                    pitch = -1.4f - 1.2f * Mathf.Pow(.5f + .5f * Mathf.Cos(bar), 3f);
                    yaw = 5f * Mathf.Sin(stroke);
                    bob = -.025f * Mathf.Pow(.5f + .5f * Mathf.Cos(bar), 4f);
                }
                float k = lit;
                // x is the hall (forward): pitch about z leans toward it, roll about x sways, yaw about y turns
                var lean = Quaternion.AngleAxis(yaw * k, Vector3.up) * Quaternion.AngleAxis(pitch * k, Vector3.forward) * Quaternion.AngleAxis(roll * k, Vector3.right);
                var foot = p.foot + new Vector3(0f, bob * k, 0f);
                most = Mathf.Max(most, Quaternion.Angle(Quaternion.identity, lean));
                for (int i = 0; i < p.parts.Length; i++)
                {
                    if (p.parts[i] == null) continue;
                    p.parts[i].SetPositionAndRotation(frame.TransformPoint(foot + lean * (p.at[i] - p.foot)), frame.rotation * (lean * p.turn[i]));
                }
            }
            Playing = most;
        }

        MaterialPropertyBlock block;

        void SetGlow(Transform t, Color c)
        {
            if (block == null) block = new MaterialPropertyBlock();
            if (!t.TryGetComponent<Renderer>(out var r)) return;
            block.Clear();
            ChessFight.Game.LitMaterials.SetColor(block, c);
            r.SetPropertyBlock(block);
        }
    }
}
