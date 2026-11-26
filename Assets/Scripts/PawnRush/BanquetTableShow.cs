using System;
using System.Collections.Generic;
using System.Globalization;
using ChessFight.Game;
using ChessFight.Gameplay;
using UnityEngine;

namespace ChessFight.PawnRush
{
    // The long table's show on the dressed hall (대연회장 연출, the user 11-25: "천을 뺄 때 그릇이나 위에 소품들이 움직이며
    // 그릇은 움직이는 동안(제자리에서) 콜라이더 판정이 바뀌면서 사용자가 약간의 wasd로 조작 할 수 있는 정도 ... 자연스럽게
    // 빼는 것처럼 ... 천이 빠질 때 순서대로 촛불이 꺼지면서 천이 올라갈 때 촛불이 순서로 켜진다").
    //
    // The magician's tablecloth trick (BanquetDevices.TableclothTrick) keeps its timing and its throw. What is seen changes:
    //   - the cloth is drawn south out from under the ware, over the table's south end into the magician's hands, and
    //     flung back out of them over the table (the model's vertices are moved; the graybox slid it away in 0.3 s and
    //     stretched it back);
    //   - the plates rock where they stand as the cloth leaves and as it comes back under them - their colliders rock
    //     with them (BanquetPlate), so whoever stands on one has to hold a little against it;
    //   - the goblets and candelabra are dragged a little, rock and settle, and hop as the cloth comes back;
    //   - the candles go out one candelabrum after another as the cloth's end passes them (north to south) and are lit
    //     again as it comes back (south to north);
    //   - the magician leans into the roll, back with the pull and forward with the throw.
    // Everything is a pure function of the shared obstacle clock (HANDOFF rule 7): every PC sees the same, nothing is sent.

    // The cloth's run: how far south it has been drawn at a phase of the trick, and when its north edge passes a place.
    public readonly struct BanquetClothRun
    {
        // Past the table's south end the cloth is gathered into the hands over this length.
        public const float Gather = 3f;
        const float PullPower = 1.7f, LayPower = 2.6f;

        public readonly float Length, Period, RollAt, PullAt, PullTime, LayAt, LayTime;

        public BanquetClothRun(float clothLength, TableclothTrick trick)
        {
            Length = clothLength;
            Period = trick.Period;
            RollAt = trick.RollAt;
            PullAt = trick.PullAt;
            LayAt = trick.LayAt;
            LayTime = Mathf.Max(.1f, trick.LayTime);
            // The pull is seen for longer than the trick's instant (0.3 s): the throw is at its start, as before.
            PullTime = Mathf.Min(.8f, (LayAt - PullAt) * .9f);
        }

        public bool Set => Period > 0f;
        // Drawn this far, the whole cloth is in the hands.
        public float Away => Length + .6f + Gather;

        public float Phase(double time)
        {
            double u = time % Period;
            return (float)(u < 0.0 ? u + Period : u);
        }

        // Metres the cloth has been drawn south at phase `u`.
        public float Slide(float u)
        {
            if (u < PullAt) return 0f;
            if (u < PullAt + PullTime) return Away * Mathf.Pow((u - PullAt) / PullTime, PullPower);
            if (u < LayAt) return Away;
            return Away * Mathf.Pow(Mathf.Clamp01(1f - (u - LayAt) / LayTime), LayPower);
        }

        // Its speed, m/s (either way).
        public float Speed(float u)
        {
            if (u < PullAt) return 0f;
            if (u < PullAt + PullTime) return Away * PullPower * Mathf.Pow((u - PullAt) / PullTime, PullPower - 1f) / PullTime;
            if (u < LayAt) return 0f;
            return Away * LayPower * Mathf.Pow(Mathf.Clamp01(1f - (u - LayAt) / LayTime), LayPower - 1f) / LayTime;
        }

        // The phase at which the cloth's north edge passes `along` metres up the table from its south end: going away
        // in the pull, and coming back in the laying.
        public float Leaves(float along) =>
            PullAt + PullTime * Mathf.Pow(Mathf.Clamp01((Length - along) / Away), 1f / PullPower);

        public float Arrives(float along) =>
            LayAt + LayTime * (1f - Mathf.Pow(Mathf.Clamp01((Length - along) / Away), 1f / LayPower));
    }

    // A plate of the long table: a floor that rocks where it stands. When the cloth is pulled from under it, it tips a
    // few degrees and the tip goes round (a plate ringing down on a table), dying out in about 1.5 s; it turns a little
    // with the cloth; when the cloth comes back under it, it hops, rocks again and is turned back. Never off its place:
    // the trick's "on a plate" is still the plate's circle. Tipped, it stands on its low rim (its middle up to 0.115 m
    // higher), so no part of it goes under the table.
    //
    // The numbers are kept under what throws a pawn: the rim (1.2 m out) never moves faster than 0.7 m/s nor at more
    // than 5 m/s2, under gravity, so feet stay on the plate (the cloche's first lift threw pawns: "슈퍼점프").
    public sealed class BanquetPlate : Obstacle
    {
        public const float PullTip = 5.5f, LayTip = 3.5f, Turn = 18f, Hop = .04f, Radius = GrandBanquetHallBuilder.PlateR;

        BanquetClothRun run;
        float along, seed;

        public float Along => along;

        // Call on an INACTIVE object. `alongCloth`: metres from the cloth's south end; `seedTurns`: where round the
        // plate the first tip is (0..1), so the five plates do not rock alike.
        public void Configure(BanquetClothRun clothRun, float alongCloth, float seedTurns)
        {
            run = clothRun;
            along = alongCloth;
            seed = seedTurns;
        }

        // The tip (degrees about the plate's x and z), the turn about its axis (degrees) and the lift (metres) at `time`.
        public static void Rock(in BanquetClothRun run, float along, float seed, double time, out Vector2 tip, out float turn, out float lift)
        {
            tip = Vector2.zero;
            turn = 0f;
            lift = 0f;
            if (!run.Set) return;
            float u = run.Phase(time);
            float pulled = u - run.PullAt;                 // since the pull
            float back = u - run.Arrives(along);           // since the cloth came back under it: this cycle's ...
            float backBefore = back + run.Period;          // ... and the last cycle's, still dying out
            if (pulled >= 0f)
            {
                float e = Ramp(pulled / .35f) * Mathf.Exp(-pulled / .75f), a = 2f * Mathf.PI * (seed + .85f * pulled);
                tip += PullTip * e * new Vector2(Mathf.Cos(a), Mathf.Sin(a));
            }
            Laid(back, seed, ref tip, ref lift);
            Laid(backBefore, seed, ref tip, ref lift);
            // Tipped, it stands on its low rim: lifted by what would otherwise sink that rim into the table.
            lift += Radius * Mathf.Sin(Mathf.Min(tip.magnitude, 20f) * Mathf.Deg2Rad);
            turn = pulled >= 0f ? Turn * (1f - Mathf.Exp(-pulled / .25f) - (back >= 0f ? 1f - Mathf.Exp(-back / .3f) : 0f))
                                : Turn * Mathf.Exp(-backBefore / .3f);
        }

        static void Laid(float since, float seed, ref Vector2 tip, ref float lift)
        {
            if (since < 0f) return;
            float e = Ramp(since / .3f) * Mathf.Exp(-since / .5f), a = 2f * Mathf.PI * (seed * 1.7f + .31f + since);
            tip += LayTip * e * new Vector2(Mathf.Cos(a), Mathf.Sin(a));
            if (since < .4f)
            {
                float s = Mathf.Sin(Mathf.PI * since / .4f);
                lift += Hop * s * s;
            }
        }

        static float Ramp(float x)
        {
            x = Mathf.Clamp01(x);
            return x * x * (3f - 2f * x);
        }

        protected override void Evaluate(double time, out Vector3 position, out Quaternion rotation)
        {
            Rock(run, along, seed, time, out var tip, out float turn, out float lift);
            float degrees = tip.magnitude;
            var lean = degrees > 1e-4f ? Quaternion.AngleAxis(degrees, new Vector3(tip.x, 0f, tip.y) / degrees) : Quaternion.identity;
            rotation = StartRotation * (lean * Quaternion.AngleAxis(turn, Vector3.up));
            position = StartPosition + StartRotation * new Vector3(0f, lift, 0f);
        }
    }

    // The pictures: the cloth's vertices, the ware's models, the candles' flames and the magician. Sits on the hall's
    // Content (the section's frame) beside GrandBanquetHallDressing, which makes it once the models hang.
    [DefaultExecutionOrder(205)]
    public sealed class BanquetTableShow : MonoBehaviour
    {
        // The cloth's own frame: x across the table from its middle, y up from its top, z north from its south end.
        const float SliceStep = .5f;                        // no triangle is longer than this along the table
        static readonly Vector3 Hands = new Vector3(0f, .35f, -.45f);
        const float Billow = .16f, Ripple = .06f, RippleWave = 2.2f, Flutter = .12f, FullSpeed = 18f;
        // (11-26, the user: "예고나 천의 주름") the cloth is cut across the table too, so it can fold; the folds: the
        // magician's tug as the drum rolls (the near end creeps toward his hands in jerks, folds fanning out from them),
        // the long folds the pull drags along the cloth, and the soft folds it settles out of once laid.
        const float SliceAcross = .3f, TugCreep = .07f, TugReach = 2.6f, TugFold = .15f, PullFold = .16f, FoldPitch = .62f,
                    SettleFold = .09f, SettleTime = 1.1f;
        static readonly Color RollTint = new Color(1f, .55f, .5f, 1f);
        static readonly int BaseColor = Shader.PropertyToID("_BaseColor");

        sealed class Sheet
        {
            public Renderer renderer;
            public Mesh live;
            public Vector3[] rest, restNormal, moved, movedNormal;
            public Matrix4x4 back;
            public Quaternion backTurn;
            public bool linen, runner;
            public float at = float.NaN;                    // the slide its vertices show
        }

        enum Kind { Plate, Goblet, Candelabrum }

        sealed class Candle
        {
            public Transform flame, smoke;
            public Vector3 wick;                            // from the candelabrum's foot, in the frame
            public float outAfter, onAfter, seed;           // seconds after the cloth's edge passes
        }

        sealed class Ware
        {
            public Kind kind;
            public Transform[] parts;                       // its LODs
            public Vector3[] at;
            public Quaternion[] turn;
            public Vector3 foot;                            // what it rocks about, in the frame
            public float along, seed;
            public Transform plate;                         // a plate's collider (BanquetPlate), followed
            public Quaternion plateTurn;
            public Candle[] candles = Array.Empty<Candle>();
            public Light glow;
            public Vector3 glowAt;
        }

        TableclothTrick trick;
        BanquetClothRun run;
        Transform frame, magician, flames;
        Vector3 magicianFoot;
        readonly List<Sheet> sheets = new List<Sheet>();
        readonly List<Ware> ware = new List<Ware>();
        readonly List<UnityEngine.Object> owned = new List<UnityEngine.Object>();
        MaterialPropertyBlock block;
        bool tinted, shown = true;

        public int Sheets => sheets.Count;
        public int WarePieces => ware.Count;
        public int Candles { get; private set; }
        public int Plates { get; private set; }
        // As last drawn: the candles burning, and the cloth wholly in the hands.
        public int Lit { get; private set; }
        public bool ClothAway => !shown;
        public BanquetClothRun Run => run;

        // ------------------------------------------------------------------ setting up

        public void Configure(GrandBanquetHallDressing dressing, TableclothTrick tableclothTrick)
        {
            trick = tableclothTrick;
            frame = dressing.transform;
            run = new BanquetClothRun(GrandBanquetHallBuilder.ClothZ1 - GrandBanquetHallBuilder.TableZ0, trick);
            block = new MaterialPropertyBlock();
            SetUpCloth(dressing);
            // (11-26) the runner lies on the linen a hair over it: the folds stay off it and the linen under it (a stiff
            // band; folded apart, one would show through the other)
            foreach (var s in sheets)
                if (s.runner)
                    foreach (var p in s.rest) if (p.y > -.06f) runnerHalf = Mathf.Max(runnerHalf, Mathf.Abs(p.x));
            SetUpWare(dressing);
            // (11-26) where the ware stands on the cloth (the cloth's frame: x across from the table's middle, z north from
            // its south end): the folds go round them, so the linen never rises through a candelabrum's or a goblet's foot
            var feet = new List<Vector2>();
            foreach (var w in ware)
                feet.Add(new Vector2(w.foot.x - (GrandBanquetHallBuilder.TableX0 + GrandBanquetHallBuilder.TableX1) * .5f, w.foot.z - GrandBanquetHallBuilder.TableZ0));
            wareFeet = feet.ToArray();
            SetUpCandles();
            magician = GrandBanquetHallDressing.ModelOn(frame, "bh_magician");
            magicianFoot = new Vector3(GrandBanquetHallBuilder.RunnerX, 0f, 15.2f);
            Debug.Log("[대연회장 식탁 연출] 식탁보 메시 " + sheets.Count + "개 · 식기 " + ware.Count + "개(움직이는 접시 판정 " + Plates +
                      "개) · 촛불 " + Candles + "개" + (magician != null ? " · 마술사" : ""));
        }

        // The cloth's model is taken off the trick's picture (which the trick slides and stretches) and stands in the
        // frame where the cloth lies; its meshes are copied, cut so that no triangle spans more than SliceStep along
        // the table (the cloth's top was two triangles 32 m long), and moved from then on by Move.
        void SetUpCloth(GrandBanquetHallDressing dressing)
        {
            Transform model = null;
            foreach (var host in dressing.FindAll("BH tablecloth"))
            {
                model = GrandBanquetHallDressing.ModelOn(host, "bh_table_cloth");
                if (model != null) break;
            }
            if (model == null) return;
            var origin = new Vector3((GrandBanquetHallBuilder.TableX0 + GrandBanquetHallBuilder.TableX1) * .5f,
                                     GrandBanquetHallBuilder.TableY + .005f, GrandBanquetHallBuilder.TableZ0);
            model.SetParent(frame, false);
            model.localPosition = origin;
            model.localRotation = Quaternion.Euler(0f, 180f, 0f);
            model.localScale = Vector3.one;
            var toFrame = frame.worldToLocalMatrix;
            foreach (var filter in model.GetComponentsInChildren<MeshFilter>(true))
            {
                var source = filter.sharedMesh;
                if (source == null || !source.isReadable || !filter.TryGetComponent<Renderer>(out var renderer)) continue;
                var toCloth = Matrix4x4.Translate(-origin) * toFrame * filter.transform.localToWorldMatrix;
                var sheet = Cut(source, toCloth);
                if (sheet == null) continue;
                sheet.renderer = renderer;
                string n = filter.name;
                sheet.linen = n.IndexOf("cloth", StringComparison.Ordinal) >= 0 && n.IndexOf("border", StringComparison.Ordinal) < 0;
                sheet.runner = n.IndexOf("runner", StringComparison.Ordinal) >= 0;
                filter.sharedMesh = sheet.live;
                owned.Add(sheet.live);
                sheets.Add(sheet);
            }
        }

        struct Corner
        {
            public Vector3 p, n;
            public Vector4 t;
            public Vector2 uv;
            public int index;
        }

        static Sheet Cut(Mesh source, Matrix4x4 toCloth)
        {
            var v = new List<Vector3>();
            var n = new List<Vector3>();
            var tan = new List<Vector4>();
            var uv = new List<Vector2>();
            source.GetVertices(v);
            source.GetNormals(n);
            source.GetTangents(tan);
            source.GetUVs(0, uv);
            if (v.Count == 0 || n.Count != v.Count) return null;
            bool hasTan = tan.Count == v.Count, hasUv = uv.Count == v.Count;
            for (int i = 0; i < v.Count; i++)
            {
                v[i] = toCloth.MultiplyPoint3x4(v[i]);
                n[i] = toCloth.MultiplyVector(n[i]).normalized;
            }

            var poly = new List<Corner>(8);
            var clipped = new List<Corner>(8);
            var subs = new List<int>[source.subMeshCount];
            var tris = new List<int>();
            for (int s = 0; s < subs.Length; s++)
            {
                source.GetTriangles(tris, s);
                var into = subs[s] = new List<int>(tris.Count + tris.Count / 2);
                for (int i = 0; i + 2 < tris.Count; i += 3)
                {
                    int a = tris[i], b = tris[i + 1], c = tris[i + 2];
                    float lo = Mathf.Min(v[a].z, Mathf.Min(v[b].z, v[c].z)), hi = Mathf.Max(v[a].z, Mathf.Max(v[b].z, v[c].z));
                    int k0 = Mathf.FloorToInt(lo / SliceStep), k1 = Mathf.FloorToInt(hi / SliceStep);
                    if (k1 * SliceStep >= hi) k1--;         // its top on a cut: nothing over it
                    if (k0 >= k1) { into.Add(a); into.Add(b); into.Add(c); continue; }
                    for (int k = k0; k <= k1; k++)
                    {
                        poly.Clear();
                        poly.Add(At(a)); poly.Add(At(b)); poly.Add(At(c));
                        Clip(poly, clipped, k * SliceStep, true);
                        Clip(clipped, poly, (k + 1) * SliceStep, false);
                        if (poly.Count < 3) continue;
                        for (int j = 0; j < poly.Count; j++)
                        {
                            var corner = poly[j];
                            if (corner.index >= 0) continue;
                            corner.index = v.Count;
                            v.Add(corner.p);
                            n.Add(corner.n.sqrMagnitude > 1e-8f ? corner.n.normalized : Vector3.up);
                            if (hasTan) tan.Add(corner.t);
                            if (hasUv) uv.Add(corner.uv);
                            poly[j] = corner;
                        }
                        for (int j = 1; j + 1 < poly.Count; j++)
                        {
                            into.Add(poly[0].index); into.Add(poly[j].index); into.Add(poly[j + 1].index);
                        }
                    }
                }
            }

            Corner At(int i) => new Corner { p = v[i], n = n[i], t = hasTan ? tan[i] : default, uv = hasUv ? uv[i] : default, index = i };

            // (11-26) and across it, every SliceAcross, so the top can fold along and across
            for (int s = 0; s < subs.Length; s++)
            {
                var along = subs[s];
                var into = subs[s] = new List<int>(along.Count * 2);
                for (int i = 0; i + 2 < along.Count; i += 3)
                {
                    int a = along[i], b = along[i + 1], c = along[i + 2];
                    float lo = Mathf.Min(v[a].x, Mathf.Min(v[b].x, v[c].x)), hi = Mathf.Max(v[a].x, Mathf.Max(v[b].x, v[c].x));
                    int k0 = Mathf.FloorToInt(lo / SliceAcross), k1 = Mathf.FloorToInt(hi / SliceAcross);
                    if (k1 * SliceAcross >= hi) k1--;
                    if (k0 >= k1) { into.Add(a); into.Add(b); into.Add(c); continue; }
                    for (int k = k0; k <= k1; k++)
                    {
                        poly.Clear();
                        poly.Add(At(a)); poly.Add(At(b)); poly.Add(At(c));
                        Clip(poly, clipped, k * SliceAcross, true, 0);
                        Clip(clipped, poly, (k + 1) * SliceAcross, false, 0);
                        if (poly.Count < 3) continue;
                        for (int j = 0; j < poly.Count; j++)
                        {
                            var corner = poly[j];
                            if (corner.index >= 0) continue;
                            corner.index = v.Count;
                            v.Add(corner.p);
                            n.Add(corner.n.sqrMagnitude > 1e-8f ? corner.n.normalized : Vector3.up);
                            if (hasTan) tan.Add(corner.t);
                            if (hasUv) uv.Add(corner.uv);
                            poly[j] = corner;
                        }
                        for (int j = 1; j + 1 < poly.Count; j++)
                        {
                            into.Add(poly[0].index); into.Add(poly[j].index); into.Add(poly[j + 1].index);
                        }
                    }
                }
            }

            var sheet = new Sheet
            {
                rest = v.ToArray(), restNormal = n.ToArray(), moved = new Vector3[v.Count], movedNormal = new Vector3[v.Count],
                back = toCloth.inverse,
            };
            sheet.backTurn = Quaternion.LookRotation(sheet.back.MultiplyVector(Vector3.forward), sheet.back.MultiplyVector(Vector3.up));
            for (int i = 0; i < v.Count; i++)
            {
                sheet.moved[i] = sheet.back.MultiplyPoint3x4(sheet.rest[i]);
                sheet.movedNormal[i] = sheet.backTurn * sheet.restNormal[i];
            }
            var live = new Mesh { name = source.name + " (식탁보 연출)" };
            if (v.Count > 65000) live.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            live.MarkDynamic();
            live.SetVertices(sheet.moved);
            live.SetNormals(sheet.movedNormal);
            if (hasTan) live.SetTangents(tan);
            if (hasUv) live.SetUVs(0, uv);
            live.subMeshCount = subs.Length;
            for (int s = 0; s < subs.Length; s++) live.SetTriangles(subs[s], s, false);
            // Where it lies, and on to the hands over the table's south end.
            live.RecalculateBounds();
            var bounds = live.bounds;
            bounds.Encapsulate(sheet.back.MultiplyPoint3x4(Hands));
            bounds.Expand(1.2f);
            live.bounds = bounds;
            sheet.live = live;
            sheet.at = 0f;
            return sheet;
        }

        // Sutherland-Hodgman against a plane across the table: keeps z >= plane (above) or z <= plane.
        static void Clip(List<Corner> from, List<Corner> into, float plane, bool above, int axis = 2)
        {
            into.Clear();
            for (int i = 0; i < from.Count; i++)
            {
                Corner p = from[i], q = from[(i + 1) % from.Count];
                bool pin = above ? p.p[axis] >= plane : p.p[axis] <= plane, qin = above ? q.p[axis] >= plane : q.p[axis] <= plane;
                if (pin) into.Add(p);
                if (pin != qin) into.Add(Cross(p, q, plane, axis));
            }
        }

        static bool Before(Vector3 a, Vector3 b) => a.x != b.x ? a.x < b.x : a.y != b.y ? a.y < b.y : a.z < b.z;

        // The same point whichever way round the edge is taken, so two triangles that share it stay shut.
        static Corner Cross(Corner p, Corner q, float plane, int axis = 2)
        {
            if (p.p[axis] > q.p[axis] || (p.p[axis] == q.p[axis] && Before(q.p, p.p))) (p, q) = (q, p);
            float f = (plane - p.p[axis]) / (q.p[axis] - p.p[axis]);
            var at = Vector3.LerpUnclamped(p.p, q.p, f);
            at[axis] = plane;
            return new Corner
            {
                p = at, n = Vector3.LerpUnclamped(p.n, q.n, f), t = Vector4.LerpUnclamped(p.t, q.t, f),
                uv = Vector2.LerpUnclamped(p.uv, q.uv, f), index = -1,
            };
        }

        // The ware's models (bh_table_ware: each plate, goblet and candelabrum an object with its LODs, its origin at its
        // foot) and the plates' colliders, which become BanquetPlate.
        void SetUpWare(GrandBanquetHallDressing dressing)
        {
            var model = GrandBanquetHallDressing.ModelOn(frame, "bh_table_ware");
            var byName = new Dictionary<string, List<Transform>>();
            if (model != null)
                foreach (var filter in model.GetComponentsInChildren<MeshFilter>(true))
                {
                    string n = filter.name;
                    int lod = n.LastIndexOf("_LOD", StringComparison.Ordinal);
                    if (lod >= 0) n = n.Substring(0, lod);
                    if (!byName.TryGetValue(n, out var list)) byName[n] = list = new List<Transform>();
                    list.Add(filter.transform);
                }
            var turnBack = Quaternion.Inverse(frame.rotation);
            foreach (var pair in byName)
            {
                Kind kind;
                if (pair.Key.IndexOf("plate", StringComparison.Ordinal) >= 0) kind = Kind.Plate;
                else if (pair.Key.IndexOf("goblet", StringComparison.Ordinal) >= 0) kind = Kind.Goblet;
                else if (pair.Key.IndexOf("candelabra", StringComparison.Ordinal) >= 0) kind = Kind.Candelabrum;
                else continue;
                var w = new Ware { kind = kind, parts = pair.Value.ToArray() };
                w.at = new Vector3[w.parts.Length];
                w.turn = new Quaternion[w.parts.Length];
                for (int i = 0; i < w.parts.Length; i++)
                {
                    w.at[i] = frame.InverseTransformPoint(w.parts[i].position);
                    w.turn[i] = turnBack * w.parts[i].rotation;
                }
                // Its foot: the object's origin (bh_parts builds each from its foot up); were the origins baked away,
                // under the middle of what is drawn.
                w.foot = w.at[0];
                if (w.parts[0].TryGetComponent<Renderer>(out var r))
                {
                    var middle = frame.InverseTransformPoint(r.bounds.center);
                    if (new Vector2(middle.x - w.foot.x, middle.z - w.foot.z).magnitude > 1.5f)
                        w.foot = new Vector3(middle.x, frame.InverseTransformPoint(r.bounds.min).y, middle.z);
                }
                w.along = w.foot.z - GrandBanquetHallBuilder.TableZ0;
                w.seed = Mathf.Repeat(w.foot.x * .37f + w.foot.z * .113f, 1f);
                ware.Add(w);
            }

            // The plates' colliders: "BH plate 1" .. on the runner (the waiters' "BH plate stack" is not one).
            var colliders = new List<Transform>();
            for (int i = 1; i <= GrandBanquetHallBuilder.PlateZ.Length; i++)
                foreach (var t in dressing.FindAll("BH plate " + i))
                    if (t.GetComponent<Collider>() != null) colliders.Add(t);
            foreach (var plate in colliders)
            {
                if (plate.GetComponent<Obstacle>() != null) continue;
                var at = frame.InverseTransformPoint(plate.position);
                float along = at.z - GrandBanquetHallBuilder.TableZ0;
                // Its picture, taken while it still stands as built.
                Ware picture = null;
                foreach (var w in ware)
                    if (w.kind == Kind.Plate && w.plate == null && Mathf.Abs(w.foot.z - at.z) < 1f && Mathf.Abs(w.foot.x - at.x) < 1f) picture = w;
                if (picture != null)
                {
                    picture.plate = plate;
                    picture.plateTurn = turnBack * plate.rotation;
                    picture.foot = at;
                    picture.along = along;
                }
                bool active = plate.gameObject.activeSelf;
                plate.gameObject.SetActive(false);
                plate.gameObject.AddComponent<BanquetPlate>().Configure(run, along, Mathf.Repeat(.21f + along * .137f, 1f));
                plate.gameObject.SetActive(active);
                Plates++;
            }
        }

        // The candles' flames (the user's rule, 11-07: flames and light are code): a glowing drop on every wick the
        // model's script wrote beside the FBX (bh_table_ware_lamps.txt, the frame's coordinates) and a warm light on
        // each candelabrum, put out and lit with the cloth.
        void SetUpCandles()
        {
            var text = Resources.Load<TextAsset>("BanquetHall/Models/B2/bh_table_ware_lamps");
            if (text == null) return;
            var wicks = new List<Vector3>();
            foreach (var line in text.text.Split('\n'))
            {
                var parts = line.Trim().Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length < 3) continue;
                if (float.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out float x)
                    && float.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out float y)
                    && float.TryParse(parts[2], NumberStyles.Float, CultureInfo.InvariantCulture, out float z))
                    wicks.Add(new Vector3(x, y, z));
            }
            if (wicks.Count == 0) return;

            flames = new GameObject("BH table candle flames").transform;
            flames.SetParent(frame, false);
            // The chandeliers' flame (R843, GrandBanquetHallDressing.Chandeliers.cs): its colours are in the mesh.
            var shader = Shader.Find(LitMaterials.ParticlesUnlitName);
            if (shader == null) return;
            var fire = new Material(shader) { name = "BH table candle flame", mainTexture = Texture2D.whiteTexture, color = new Color(1.9f, 1.75f, 1.5f, 1f) };
            LitMaterials.MakeTransparent(fire);
            owned.Add(fire);
            var haze = LitMaterials.NewUnlit(new Color(.82f, .8f, .76f, .22f));
            haze.name = "M_BanquetCandleSmoke";
            LitMaterials.MakeTransparent(haze);
            owned.Add(haze);

            var own = new Dictionary<Ware, List<Vector3>>();
            foreach (var wick in wicks)
            {
                Ware nearest = null;
                float best = 2.2f * 2.2f;
                foreach (var w in ware)
                {
                    if (w.kind != Kind.Candelabrum) continue;
                    float d = new Vector2(wick.x - w.foot.x, wick.z - w.foot.z).sqrMagnitude;
                    if (d < best) { best = d; nearest = w; }
                }
                if (nearest == null) continue;
                if (!own.TryGetValue(nearest, out var list)) own[nearest] = list = new List<Vector3>();
                list.Add(wick);
            }
            foreach (var pair in own)
            {
                var w = pair.Key;
                var list = pair.Value;
                // Along the table, so a candelabrum's candles go out one after the other the way the cloth goes.
                list.Sort((p, q) => p.z.CompareTo(q.z));
                w.candles = new Candle[list.Count];
                var middle = Vector3.zero;
                for (int i = 0; i < list.Count; i++)
                {
                    middle += list[i];
                    var c = new Candle
                    {
                        wick = list[i] - w.foot, seed = Mathf.Repeat(list[i].x * 1.31f + list[i].z * .77f + list[i].y, 1f) * 50f,
                        outAfter = .03f * (list.Count - 1 - i), onAfter = .12f + .06f * i,
                        flame = Flame(fire), smoke = Drop("BH table candle smoke", haze),
                    };
                    c.smoke.gameObject.SetActive(false);
                    w.candles[i] = c;
                    Candles++;
                }
                middle /= list.Count;
                var glow = new GameObject("BH table candle glow").AddComponent<Light>();
                glow.transform.SetParent(flames, false);
                glow.type = LightType.Point;
                glow.color = new Color(1f, .7f, .4f);
                glow.range = 9f;
                glow.intensity = GlowIntensity;
                glow.shadows = LightShadows.None;
                w.glow = glow;
                w.glowAt = middle + new Vector3(0f, .35f, 0f) - w.foot;
            }
        }

        const float GlowIntensity = 2.4f, FlameSize = 1.5f;      // the flame of a size-1 candle is 0.2 m high
        static Mesh flameMesh;

        // (11-26) the hall's candle flame, BanquetShowKit.NewFlame: painted cards turned to the camera, a halo, an ember;
        // this code still poses it (on the wick, its lean, its size)
        Transform Flame(Material material) => BanquetShowKit.NewFlame("BH table candle flame", flames);

        // The chandeliers' candle flame (BanquetChandelierLights.FlameMesh, R843), made again here so the table's
        // candles burn alike: a teardrop 0.2 m high and 0.09 m across standing on the wick, its colours in its
        // vertices - a dim blue foot, a yellow-white body, an orange tip that thins out.
        static Mesh FlameMesh()
        {
            if (flameMesh != null) return flameMesh;
            const int rings = 12, sides = 12;
            const float height = .2f, wide = .045f;
            Color foot = new Color(.3f, .45f, 1f, .35f), body = new Color(1f, .93f, .66f, 1f), high = new Color(1f, .74f, .3f, .95f), tip = new Color(1f, .5f, .12f, 0f);
            var v = new List<Vector3>();
            var c = new List<Color>();
            var uv = new List<Vector2>();
            var tris = new List<int>();
            for (int j = 0; j <= rings; j++)
            {
                float u = (float)j / rings, r = wide * Mathf.Sin(Mathf.PI * Mathf.Pow(u, .55f));
                Color col = u < .16f ? Color.Lerp(foot, body, u / .16f) : u < .55f ? Color.Lerp(body, high, (u - .16f) / .39f) : Color.Lerp(high, tip, (u - .55f) / .45f);
                for (int i = 0; i < sides; i++)
                {
                    float a = 2f * Mathf.PI * i / sides;
                    v.Add(new Vector3(Mathf.Cos(a) * r, u * height, Mathf.Sin(a) * r));
                    c.Add(col);
                    uv.Add(new Vector2(.5f, .5f));
                }
            }
            for (int j = 0; j < rings; j++)
                for (int i = 0; i < sides; i++)
                {
                    int p = j * sides + i, q = j * sides + (i + 1) % sides;
                    tris.Add(p); tris.Add(p + sides); tris.Add(q);
                    tris.Add(q); tris.Add(p + sides); tris.Add(q + sides);
                }
            flameMesh = new Mesh { name = "BH table candle flame" };
            flameMesh.SetVertices(v);
            flameMesh.SetColors(c);
            flameMesh.SetUVs(0, uv);
            flameMesh.SetTriangles(tris, 0);
            flameMesh.RecalculateNormals();
            flameMesh.RecalculateBounds();
            return flameMesh;
        }

        Transform Drop(string name, Material material)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            if (go.TryGetComponent<Collider>(out var collider)) Destroy(collider);
            go.name = name;
            go.transform.SetParent(flames, false);
            var r = go.GetComponent<Renderer>();
            r.sharedMaterial = material;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            return go.transform;
        }

        void OnDestroy()
        {
            foreach (var o in owned)
                if (o != null) Destroy(o);
        }

        // ------------------------------------------------------------------ every frame

        void LateUpdate()
        {
            if (trick == null || frame == null || !run.Set) return;
            double now = ObstacleClock.Now;
            float u = run.Phase(now);
            Folds(u);
            float slide = run.Slide(u), wave = Mathf.Clamp01(run.Speed(u) / FullSpeed);
            Cloth(u, slide, wave, trick.DrumRolling(now));
            MoveWare(u, wave);
            Magician(u);
        }

        float tug, settle, nerves, runnerHalf;
        Vector2[] wareFeet = Array.Empty<Vector2>();

        // 1 clear of the ware, down to 0 under a foot (within 0.3 m of it, rising over the next 0.45 m).
        float Clear(float x, float z)
        {
            float c = 1f;
            foreach (var f in wareFeet)
            {
                float dz = z - f.y;
                if (dz > .8f || dz < -.8f) continue;
                float dx = x - f.x;
                if (dx > .8f || dx < -.8f) continue;
                c = Mathf.Min(c, Mathf.Clamp01((Mathf.Sqrt(dx * dx + dz * dz) - .3f) / .45f));
            }
            return c;
        }

        float RollShare(float u) => Mathf.Clamp01((u - run.RollAt) / Mathf.Max(.01f, run.PullAt - run.RollAt));

        // (11-26) How hard the magician tugs (0 .. 1, in jerks with the drum), how much of the laid cloth's folds are
        // left, how nervous the candles are, at phase `u`.
        void Folds(float u)
        {
            tug = settle = nerves = 0f;
            if (u >= run.RollAt && u < run.PullAt)
            {
                float r = RollShare(u), jerk = Mathf.Pow(.5f + .5f * Mathf.Sin(2f * Mathf.PI * (2f + 3f * r) * (u - run.RollAt)), 6f);
                tug = r * r * (.65f + .35f * jerk);
                nerves = r;
            }
            float laid = u - (run.LayAt + run.LayTime);
            if (laid >= 0f && laid < SettleTime) settle = Mathf.Exp(-laid / .35f) * (1f - laid / SettleTime);
        }

        void Cloth(float u, float slide, float wave, bool rolling)
        {
            bool away = slide >= run.Away - 1e-3f, lying = slide <= 0f && tug <= 0f && settle <= 0f;
            bool appearing = !away && !shown;
            shown = !away;
            foreach (var s in sheets)
            {
                if (s.renderer == null) continue;
                if (s.renderer.enabled != shown) s.renderer.enabled = shown;
                if (away) continue;
                if (lying)
                {
                    if (s.at != 0f) Move(s, 0f, 0f, u);
                    continue;
                }
                // Only what is drawn is moved (the LODs not shown wait, and are a frame late when they are shown), but
                // never a frame late out of the hands; a batch run draws by hand, so there all of them.
                if (s.renderer.isVisible || appearing || Application.isBatchMode) Move(s, slide, wave, u);
                else s.at = float.NaN;
            }
            if (!rolling && !tinted) return;
            tinted = rolling;
            // The cue: the linen warms toward red while the drum rolls (the graybox's cloth turned red) - (11-26) beating
            // with the drum, faster and deeper as the pull comes
            var tint = Color.white;
            if (rolling)
            {
                float r = RollShare(u), beat = .5f + .5f * Mathf.Sin(2f * Mathf.PI * (3f + 6f * r) * (u - run.RollAt));
                tint = Color.Lerp(Color.white, RollTint, (.3f + .7f * r) * (.6f + .4f * beat));
            }
            foreach (var s in sheets)
            {
                if (!s.linen || s.renderer == null) continue;
                if (rolling)
                {
                    block.Clear();
                    block.SetColor(BaseColor, tint);
                    s.renderer.SetPropertyBlock(block);
                }
                else s.renderer.SetPropertyBlock(null);
            }
        }

        // The cloth drawn `slide` metres south. On the table it only slides (with a billow under its north end and
        // ripples behind it while it is fast, and its skirts fluttering); what has passed the south end is gathered
        // into the hands, where it ends as nothing.
        void Move(Sheet s, float slide, float wave, float u)
        {
            Vector3[] rest = s.rest, restNormal = s.restNormal, moved = s.moved, movedNormal = s.movedNormal;
            var back = s.back;
            var backTurn = s.backTurn;
            float length = run.Length, k = 2f * Mathf.PI / RippleWave, beat = 14f * u, flap = 9f * u;
            bool still = slide <= 0f && wave <= 0f && tug <= 0f && settle <= 0f;
            float pulled = slide > 0f && slide < run.Away ? Mathf.Clamp01(slide / 1.5f) : 0f, k2 = 2f * Mathf.PI / FoldPitch;
            for (int i = 0; i < rest.Length; i++)
            {
                Vector3 p = rest[i], n = restNormal[i];
                if (!still)
                {
                    float x = p.x, y = p.y, z = p.z;
                    float drawn = z - slide;
                    float stop = Mathf.Max(drawn, Mathf.Min(z, 0f));   // it stops at the table's end (or where it hung over it)
                    float past = stop - drawn;                          // and this much of it has gone over
                    float px = x, py = y, pz = stop;
                    float onTop = y > -.06f ? 1f : y > -.6f ? (y + .6f) / .54f : 0f;
                    if (runnerHalf > 0f) onTop *= BanquetShowKit.Smooth((Mathf.Abs(x) - runnerHalf - .05f) / .55f);
                    if (onTop > 0f && (tug > 0f || pulled > 0f || settle > 0f)) onTop *= BanquetShowKit.Smooth(Clear(px, pz));
                    if (onTop > 0f)
                    {
                        // the tug: the near end crept toward the hands, folds fanning out from them
                        if (tug > 0f)
                        {
                            pz -= tug * TugCreep * Mathf.Exp(-z / TugReach);
                            float fan = Mathf.Sin(9f * Mathf.Atan2(x, z + 1f));
                            py += onTop * tug * TugFold * Mathf.Exp(-z / 2.2f) * (.5f + .5f * fan);
                        }
                        // the pull: long folds dragged along it, deepest where it is about to go over the end, the cloth
                        // drawn in a little toward the hands there
                        if (pulled > 0f)
                        {
                            float end = Mathf.Exp(-Mathf.Max(0f, drawn) / 5f), phase = k2 * x + .8f * Mathf.Sin(.7f * z);
                            float amp = PullFold * pulled * (.35f + .65f * end) * Mathf.Max(wave, .35f);
                            py += onTop * amp * (.5f + .5f * Mathf.Cos(phase));
                            px *= 1f - .07f * pulled * Mathf.Exp(-Mathf.Max(0f, drawn) / 2f);
                            n = new Vector3(n.x + onTop * amp * .5f * k2 * Mathf.Sin(phase) * n.y, n.y, n.z).normalized;
                        }
                        // laid again: soft folds it settles out of
                        if (settle > 0f && slide <= 0f)
                        {
                            float phase = k2 * .8f * x - 1.3f * Mathf.Sin(.45f * z + 1f);
                            py += onTop * SettleFold * settle * (.5f + .5f * Mathf.Cos(phase));
                        }
                    }
                    if (wave > 0f)
                    {
                        float top = y > -.06f ? 1f : y > -.6f ? (y + .6f) / .54f : 0f;
                        if (top > 0f)
                        {
                            float behind = Mathf.Max(0f, length - z);
                            float billow = Billow * Mathf.Exp(-behind / 1.4f), ripple = Ripple * Mathf.Exp(-behind / 9f);
                            float swing = .5f + .5f * Mathf.Sin(k * behind - beat);
                            py += wave * top * (billow + ripple * swing);
                            // dy/dz (behind = length - z), to light the ripples
                            float slope = wave * top * (billow / 1.4f - ripple * (.5f * k * Mathf.Cos(k * behind - beat) - swing / 9f));
                            n = new Vector3(n.x, n.y, n.z - slope * n.y).normalized;
                        }
                        else if (x > 5.9f || x < -5.9f)
                        {
                            float drop = Mathf.Clamp01(-y / 4.2f);
                            px += (x > 0f ? 1f : -1f) * wave * Flutter * drop * (.5f + .5f * Mathf.Sin(1.9f * z - flap));
                        }
                    }
                    if (past > 0f)
                    {
                        float f = past >= BanquetClothRun.Gather ? 1f : past / BanquetClothRun.Gather;
                        f = f * f * (3f - 2f * f);
                        px = Mathf.LerpUnclamped(px, Hands.x + x * .06f, f);
                        py = Mathf.LerpUnclamped(py, Hands.y + y * .04f, f);
                        pz = Mathf.LerpUnclamped(pz, Hands.z, f);
                    }
                    p = new Vector3(px, py, pz);
                }
                moved[i] = back.MultiplyPoint3x4(p);
                movedNormal[i] = backTurn * n;
            }
            s.live.SetVertices(moved);
            s.live.SetNormals(movedNormal);
            // (folded while lying: shown as not at rest, so it is laid flat again once the folds are gone)
            s.at = tug > 0f || settle > 0f ? -1f : slide;
        }

        // The ware: a plate's picture follows its collider; a goblet or a candelabrum is dragged south a little while
        // the cloth runs out from under it, rocks on its foot and stays where it was left, and is carried back, hops and
        // rocks again when the cloth comes back under it. They tremble with the drum roll.
        void MoveWare(float u, float wave)
        {
            float flick = Time.time * 3.1f;
            int burning = 0;
            foreach (var w in ware)
            {
                Vector3 footNow;
                Quaternion lean;
                if (w.kind == Kind.Plate)
                {
                    if (w.plate == null) continue;
                    footNow = frame.InverseTransformPoint(w.plate.position);
                    lean = Quaternion.Inverse(frame.rotation) * w.plate.rotation * Quaternion.Inverse(w.plateTurn);
                }
                else Rock(w, u, out footNow, out lean);
                for (int i = 0; i < w.parts.Length; i++)
                {
                    if (w.parts[i] == null) continue;
                    w.parts[i].SetPositionAndRotation(frame.TransformPoint(footNow + lean * (w.at[i] - w.foot)), frame.rotation * (lean * w.turn[i]));
                }
                if (w.candles.Length == 0) continue;

                float leaves = run.Leaves(w.along), arrives = run.Arrives(w.along), lit = 0f;
                // Flames lean with the rush of air the cloth drags south.
                var draught = Quaternion.Euler(-38f * wave, 0f, 0f);
                foreach (var c in w.candles)
                {
                    float burn = Burning(u, leaves + c.outAfter, arrives + c.onAfter);
                    lit += Mathf.Min(1f, burn);
                    bool on = burn > .02f;
                    if (burn > .5f) burning++;
                    if (c.flame.gameObject.activeSelf != on) c.flame.gameObject.SetActive(on);
                    var wick = footNow + lean * c.wick;
                    if (on)
                    {
                        float f = Mathf.PerlinNoise(flick * 2.3f + c.seed, c.seed * .37f) - .5f, g = Mathf.PerlinNoise(c.seed * .61f, flick * 1.7f + c.seed) - .5f;
                        // Upright whatever its candelabrum's lean, wavering, laid over by the draught.
                        c.flame.localRotation = draught * Quaternion.Euler((14f + 16f * nerves) * f, 0f, (14f + 16f * nerves) * g);
                        float wide = FlameSize * Mathf.Min(1f, burn + .25f) * (1f - .12f * f), high = FlameSize * burn * (1f + .3f * f + .1f * g);
                        c.flame.localScale = new Vector3(wide, high, wide);
                        c.flame.localPosition = wick;
                    }
                    // A wisp of smoke off the wick that has just gone out.
                    float smoked = u - (leaves + c.outAfter) - .1f;
                    bool wisp = smoked >= 0f && smoked < 1.1f && u < arrives + c.onAfter;
                    if (c.smoke.gameObject.activeSelf != wisp) c.smoke.gameObject.SetActive(wisp);
                    if (wisp)
                    {
                        float a = smoked / 1.1f, thick = Mathf.Sin(Mathf.PI * Mathf.Sqrt(a));
                        c.smoke.localPosition = wick + new Vector3(.08f * Mathf.Sin(5f * a + c.seed), .1f + .9f * a, -.25f * a);
                        c.smoke.localScale = new Vector3(.05f + .14f * a, .18f + .4f * a, .05f + .14f * a) * thick;
                    }
                }
                if (w.glow != null)
                {
                    float level = lit / w.candles.Length;
                    if (w.glow.enabled != level > .01f) w.glow.enabled = level > .01f;
                    w.glow.intensity = GlowIntensity * level * (.88f + .24f * Mathf.PerlinNoise(flick * .8f + w.seed * 9f, .5f));
                    w.glow.transform.localPosition = footNow + lean * w.glowAt;
                }
            }
            Lit = burning;
        }

        // 1 burning; down to 0 in 0.14 s when put out; flaring up past 1 and settling in 0.3 s when lit.
        static float Burning(float u, float outAt, float onAt)
        {
            if (u < outAt) return 1f;
            if (u < onAt) return Mathf.Max(0f, 1f - (u - outAt) / .14f);
            float g = (u - onAt) / .3f;
            return g >= 1f ? 1f : Mathf.Sin(g * Mathf.PI * .5f) * (1f + .35f * Mathf.Sin(g * Mathf.PI));
        }

        void Rock(Ware w, float u, out Vector3 foot, out Quaternion lean)
        {
            bool goblet = w.kind == Kind.Goblet;
            float drag = goblet ? .3f : .14f, tip = goblet ? 13f : 6f, rate = 2f * Mathf.PI * (goblet ? 3.2f : 1.9f);
            float fade = goblet ? .45f : .7f, hop = goblet ? .12f : .06f;
            float leaves = run.Leaves(w.along), arrives = run.Arrives(w.along);
            float pulled = u - run.PullAt, back = u - arrives, backBefore = back + run.Period;
            float south = 0f, up = 0f, x = 0f, z = 0f;      // x: degrees about x (its top to the north), z: about z

            // The drum roll: a tremble that grows.
            if (u >= run.RollAt && u < run.PullAt)
            {
                float r = (u - run.RollAt) / Mathf.Max(.01f, run.PullAt - run.RollAt), amp = (goblet ? .9f : .45f) * r * r;
                x += amp * Mathf.Sin(2f * Mathf.PI * 13f * u + w.seed * 40f);
                z += amp * Mathf.Sin(2f * Mathf.PI * 17f * u + w.seed * 71f);
            }
            if (pulled >= 0f)
            {
                // Dragged while the cloth is still under it; its foot goes south, so its top is left to the north.
                south = drag * (1f - Mathf.Exp(-Mathf.Min(pulled, leaves - run.PullAt) / .15f));
                float e = Mathf.Clamp01(pulled / .06f) * Mathf.Exp(-pulled / fade);
                x += tip * e * Mathf.Cos(rate * pulled);
                z += tip * .35f * e * Mathf.Sin(rate * .83f * pulled + w.seed * 6f);
                if (back >= 0f) south *= Mathf.Exp(-back / .12f);
            }
            // The cloth back under it: this cycle's, and the last cycle's still dying out.
            for (int cycle = 0; cycle < 2; cycle++)
            {
                float since = cycle == 0 ? back : backBefore;
                if (since < 0f) continue;
                float e = Mathf.Clamp01(since / .05f) * Mathf.Exp(-since / fade);
                x -= tip * .6f * e * Mathf.Cos(rate * since);
                z += tip * .2f * e * Mathf.Sin(rate * .83f * since + w.seed * 6f);
                if (since < .3f)
                {
                    float s = Mathf.Sin(Mathf.PI * since / .3f);
                    up += hop * s * s;
                }
            }
            foot = w.foot + new Vector3(0f, up, -south);
            lean = Quaternion.Euler(x, 0f, z);
        }

        // The magician (a figurine: it leans as one piece about its foot): into the table as the drum rolls, back with
        // the pull, forward with the throw.
        void Magician(float u)
        {
            if (magician == null) return;
            float lean = 0f;
            if (u >= run.RollAt && u < run.PullAt) lean = 3f * (u - run.RollAt) / Mathf.Max(.01f, run.PullAt - run.RollAt);
            else if (u >= run.PullAt && u < run.LayAt)
            {
                float t = u - run.PullAt;
                lean = 3f - 15f * (1f - Mathf.Exp(-t / .09f)) + 9f * (1f - Mathf.Exp(-t / .5f));
            }
            else if (u >= run.LayAt)
            {
                float t = u - run.LayAt, held = run.LayAt - run.PullAt, x = t / .25f;
                float before = 3f - 15f * (1f - Mathf.Exp(-held / .09f)) + 9f * (1f - Mathf.Exp(-held / .5f));
                lean = before * Mathf.Exp(-t / .1f) + 9f * x * Mathf.Exp(1f - x);
            }
            var turn = Quaternion.Euler(lean, 0f, 0f);
            magician.localPosition = magicianFoot - turn * magicianFoot;
            magician.localRotation = turn * Quaternion.Euler(0f, 180f, 0f);
        }
    }
}
