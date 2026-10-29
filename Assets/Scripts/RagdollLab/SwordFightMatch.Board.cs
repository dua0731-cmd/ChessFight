using System.Collections.Generic;
using ChessFight.Network;
using UnityEngine;

namespace ChessFight.RagdollLab
{
    /// <summary>
    /// The prototype match's pictures, as the demo plays them (R149, Docs/GameModes/SwordFight/DEMO.md):
    /// <list type="bullet">
    /// <item>the stage (<see cref="SwordFightArena"/>: separate squares over a desk, pedestals, rails, the study);</item>
    /// <item>the ghost pieces: from 2:00, every 30 s, a big see-through turned piece fades in over the board, glides along
    /// its squares while they glow faster and faster, then slams down as they drop onto the desk;</item>
    /// <item>a fallen piece goes on falling as a turned piece of its colour, lands on the desk, slides, and is laid on its
    /// team's brass tray until it comes back;</item>
    /// <item>coming back: a big hand in a blue sleeve lowers a pawn onto the square (0.9 s), lets go and rises (0.5 s);
    /// the protection is a pale bubble;</item>
    /// <item>a steal is a gold pillar of light; hits flash a ring of sparks; heavy hits shake the camera a little;</item>
    /// <item>the camera circles the board while pieces are picked and after the result, as the demo's wide shots.</item>
    /// </list>
    /// Every PC draws these from the match state and the bodies, nothing extra on the wire.
    /// </summary>
    public sealed partial class SwordFightMatch
    {
        List<SwordGhost> plan;
        int planSeed;
        readonly bool[] dropped = new bool[4];
        readonly bool[] erased = new bool[SwordBoard.Count];
        SwordFightArena arena;
        GameObject[] ghostShapes = new GameObject[4];
        Material[] ghostMats = new Material[4];

        bool[] ErasedNow => erased;
        bool QueenDone => dropped[3];
        float K => Cell / 2f;

        void SizeBoard(float cell)
        {
            Cell = cell;
            var platform = floor.Platform;
            if (platform == null) { Debug.LogWarning("[SwordFightMatch] no platform found under the scene's centre"); return; }
            float size = SwordBoard.Size * cell;
            var scale = platform.localScale;
            platform.localScale = new Vector3(size, scale.y, size);
            platform.position = new Vector3(0f, platform.position.y, 0f);
            // The arena draws the squares; the platform is what stands on (and what breaks).
            floor.Hidden = true;
            floor.Snap = cell;
            var r = platform.GetComponent<Renderer>();
            if (r != null) r.enabled = false;
            Physics.SyncTransforms();
            floor.Refind();
            if (arena == null) arena = gameObject.AddComponent<SwordFightArena>();
            arena.Build(cell, floor);
            plan = null;   // the squares are new: the plan goes on them again
        }

        void ResetBoard()
        {
            plan = null;
            planSeed = 0;
            for (int i = 0; i < 4; i++) dropped[i] = false;
            for (int c = 0; c < erased.Length; c++) erased[c] = false;
            if (arena != null) arena.ResetPlan();
            if (floor != null) floor.ClearAll();
        }

        void StepBoard(double clock, int seed)
        {
            if (seed == 0 || arena == null) return;
            if (plan == null || planSeed != seed)
            {
                int keepSeed = seed;
                ResetBoard();
                plan = SwordGhosts.Plan(keepSeed);
                planSeed = keepSeed;
                foreach (var g in plan) foreach (int c in g.Cells) arena.Plan(c, g.At, g.DropAt);
            }
            var phase = view.Phase;
            bool started = phase != SwordPhase.Pick && phase != SwordPhase.Countdown;
            arena.Step(started ? clock : 0d);
            if (!started) return;
            for (int i = 0; i < plan.Count; i++)
            {
                var g = plan[i];
                StepGhost(i, g, clock);
                if (dropped[i] || clock < g.DropAt) continue;
                dropped[i] = true;
                foreach (int c in g.Cells)
                {
                    erased[c] = true;
                    floor.Erase(CellRect(c), game.Authority);
                }
                if (g.Cells.Length > 0) shake = Mathf.Max(shake, 0.12f);
            }
        }

        /// <summary>The ghost piece: fades in 0.35 s before its warning, glides over its squares (eased) at 3.4 m, slams
        /// 2.8 m down as they drop, fades out in 0.5 s; turning slowly, bobbing.</summary>
        void StepGhost(int i, SwordGhost g, double clock)
        {
            double t0 = g.At - 0.35, t1 = g.DropAt + 0.5;
            bool on = g.Cells.Length > 0 && clock >= t0 && clock <= t1;
            if (!on) { if (ghostShapes[i] != null) ghostShapes[i].SetActive(false); return; }
            if (ghostShapes[i] == null)
            {
                ghostMats[i] = SwordFightShapes.Glow("#fff1c6", 0f);
                ghostShapes[i] = SwordFightShapes.Piece(g.Piece, ghostMats[i]);
                ghostShapes[i].transform.localScale = Vector3.one * 3.4f * K;
                foreach (var r in ghostShapes[i].GetComponentsInChildren<Renderer>()) r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }
            var go = ghostShapes[i];
            go.SetActive(true);
            float u = Mathf.Clamp01((float)((clock - g.At) / SwordGhost.Warning));
            Vector3 p = PathPoint(GhostPath(g), Ease(u));
            float o = Mathf.Min(Mathf.Clamp01((float)((clock - t0) / 0.35)), clock > g.DropAt ? 1f - (float)((clock - g.DropAt) / 0.5) : 1f);
            var c = ghostMats[i].color;
            c.a = 0.5f * Mathf.Clamp01(o);
            ghostMats[i].color = c;
            float slam = clock > g.DropAt ? Mathf.Min(1f, (float)((clock - g.DropAt) / 0.18)) : 0f;
            go.transform.position = new Vector3(p.x, 3.4f - slam * 2.8f + Mathf.Sin(Time.time * 3f) * 0.12f, p.z);
            go.transform.rotation = Quaternion.Euler(0f, Time.time * 0.6f * Mathf.Rad2Deg, 0f);
        }

        /// <summary>The ghost's way over the board: a line from a square before its first to one past its last (the rook,
        /// the bishop), the knight's three squares, the queen round the rim's corners.</summary>
        List<Vector3> GhostPath(SwordGhost g)
        {
            var path = new List<Vector3>();
            if (g.Piece == PieceKind.Queen)
            {
                float e = (SwordBoard.Size / 2f - 0.5f) * Cell;
                path.Add(new Vector3(-e, 0f, -e)); path.Add(new Vector3(e, 0f, -e)); path.Add(new Vector3(e, 0f, e)); path.Add(new Vector3(-e, 0f, e)); path.Add(new Vector3(-e, 0f, -e));
                return path;
            }
            foreach (int c in g.Cells) path.Add(CellCentre(c));
            if (g.Piece != PieceKind.Knight && path.Count >= 2)
            {
                Vector3 d = (path[1] - path[0]).normalized * Cell;
                path.Insert(0, path[0] - d);
                path.Add(path[path.Count - 1] + d);
            }
            return path;
        }

        static Vector3 PathPoint(List<Vector3> path, float f)
        {
            if (path.Count == 0) return Vector3.zero;
            if (path.Count == 1) return path[0];
            float total = 0f;
            for (int k = 0; k + 1 < path.Count; k++) total += Vector3.Distance(path[k], path[k + 1]);
            float d = f * total;
            for (int k = 0; k + 1 < path.Count; k++)
            {
                float l = Vector3.Distance(path[k], path[k + 1]);
                if (d <= l || k == path.Count - 2) return Vector3.Lerp(path[k], path[k + 1], l > 0f ? Mathf.Clamp01(d / l) : 0f);
                d -= l;
            }
            return path[0];
        }

        static float Ease(float u) => u < 0.5f ? 2f * u * u : 1f - Mathf.Pow(-2f * u + 2f, 2f) / 2f;

        void DestroyBoardPictures()
        {
            foreach (var g in ghostShapes) if (g != null) Destroy(g);
            foreach (var m in ghostMats) if (m != null) Destroy(m);
            foreach (var f in figures.Values) if (f.go != null) Destroy(f.go);
            figures.Clear();
            foreach (var b in bubbles.Values) if (b != null) Destroy(b);
            bubbles.Clear();
            if (hand != null) Destroy(hand);
            foreach (var p in handPawns) if (p != null) Destroy(p);
            foreach (var f in fxPool) if (f.go != null) Destroy(f.go);
            foreach (var m in made) if (m != null) Destroy(m);
            if (pillar != null) Destroy(pillar);
        }

        // ---------------------------------------------------------------- the fallen, the hand, the bubble, the pillar, the rings

        sealed class Figure
        {
            public GameObject go;
            public Vector3 v;
            public float landed = -1f, trayFrom = -1f;
            public int team, slot;
            public Vector3 trayFrom3;
            public Quaternion lieTurn;
        }

        readonly Dictionary<ulong, Figure> figures = new Dictionary<ulong, Figure>();
        readonly Dictionary<ulong, bool> wasAlive = new Dictionary<ulong, bool>();
        readonly Dictionary<ulong, Vector3> lastHips = new Dictionary<ulong, Vector3>();
        readonly Dictionary<ulong, float> lastFlash = new Dictionary<ulong, float>();
        readonly Dictionary<ulong, float> cameBack = new Dictionary<ulong, float>();
        readonly Dictionary<ulong, Vector3> cameBackAt = new Dictionary<ulong, Vector3>();
        readonly Dictionary<SwordFightPawn, GameObject> bubbles = new Dictionary<SwordFightPawn, GameObject>();
        readonly int[] traySlots = new int[2];
        readonly List<Object> made = new List<Object>();
        GameObject hand, pillar;
        readonly GameObject[] handPawns = new GameObject[2];
        Material whiteMat, blackMat, bubbleMat, pillarMat, ringTexMat;
        float pillarAt = -10f;
        Vector3 pillarPos;
        float shake;

        T Made<T>(T o) where T : Object { made.Add(o); return o; }

        void EnsureMaterials()
        {
            if (whiteMat != null) return;
            whiteMat = Made(SwordFightShapes.Lit("#f3ecdd", 0.48f));
            blackMat = Made(SwordFightShapes.Lit("#2b2522", 0.32f));
            bubbleMat = Made(SwordFightShapes.Glow("#cfe8ff", 0.18f));
            pillarMat = Made(SwordFightShapes.Glow("#ffe39a", 0f));
        }

        void StepPictures()
        {
            EnsureMaterials();
            float dt = Time.deltaTime;
            foreach (var f in game.Fighters.Values)
            {
                if (f == null) continue;
                bool alive = f.Alive;
                wasAlive.TryGetValue(f.Id, out bool was);
                if (!wasAlive.ContainsKey(f.Id)) was = alive;
                Vector3 hips = f.Pawn.Hips.position;
                if (was && !alive) Fell(f, hips, lastHips.TryGetValue(f.Id, out var before) ? (hips - before) / Mathf.Max(1e-3f, dt) : Vector3.zero);
                if (!was && alive)
                {
                    if (figures.TryGetValue(f.Id, out var fig)) { Destroy(fig.go); figures.Remove(f.Id); }
                    cameBack[f.Id] = Time.time;
                    cameBackAt[f.Id] = new Vector3(hips.x, floor.Top, hips.z);
                    Ring(hips + Vector3.up * 0.3f, 0.8f, "#ffffff");
                }
                wasAlive[f.Id] = alive;
                lastHips[f.Id] = hips;
                // A sword's hit (its victim's flash jumps up: the host's own and every client's from the wire).
                lastFlash.TryGetValue(f.Id, out float flashBefore);
                // 10-29: the sword-hit effect (SwordFightSkillFx.SwordHit) takes its place: no ring of glowing balls.
                if (alive && f.HitFlash >= 0.13f && f.HitFlash > flashBefore + 0.02f && !SwordFightSkillFx.SwordHitVfxOn)
                    Ring(f.Pawn.bodies[(int)BodyId.Chest].position, 0.7f, "#fff3c8");
                lastFlash[f.Id] = f.HitFlash;
                Bubble(f, alive && f.Protection > 0f);
            }
            StepFigures(dt);
            StepHand();
            StepPillar();
            StepRings();
        }

        void Fell(SwordFightPawn f, Vector3 at, Vector3 v)
        {
            var piece = f.Pawn.Piece;
            var go = SwordFightShapes.Piece(piece, f.Pawn.Team == 0 ? whiteMat : blackMat);
            go.transform.localScale = Vector3.one * K;
            go.transform.position = at - Vector3.up * 0.5f;
            int team = Mathf.Clamp(f.Pawn.Team, 0, 1);
            Vector3 flat = new Vector3(v.x, 0f, v.z);
            Vector3 axis = flat.sqrMagnitude > 1e-3f ? Vector3.Cross(Vector3.up, flat.normalized) : Vector3.right;
            figures[f.Id] = new Figure { go = go, v = Vector3.ClampMagnitude(v, 14f), team = team, slot = traySlots[team]++ % 6,
                lieTurn = Quaternion.AngleAxis(piece == PieceKind.King ? 80f : 90f, axis) };
        }

        void StepFigures(float dt)
        {
            foreach (var fig in figures.Values)
            {
                if (fig.go == null) continue;
                var t = fig.go.transform;
                if (fig.trayFrom >= 0f)
                {
                    // Laid on the tray, sideways, swaying a hair.
                    float u = Mathf.Clamp01((Time.time - fig.trayFrom) / 0.4f);
                    Vector3 tray = arena != null ? arena.TrayPoint(fig.team, fig.slot) : Vector3.zero;
                    t.position = Vector3.Lerp(fig.trayFrom3, tray, Ease(u)) + Vector3.up * Mathf.Sin(u * Mathf.PI) * 0.8f;
                    t.rotation = Quaternion.Euler(Mathf.Sin(Time.time * 2f + fig.slot) * 2.3f, fig.team == 0 ? 90f : -90f, 80f + Mathf.Sin(Time.time * 1.3f + fig.slot) * 3f);
                    continue;
                }
                if (fig.landed < 0f)
                {
                    fig.v += Vector3.down * 9.81f * dt;
                    t.position += fig.v * dt;
                    t.rotation = Quaternion.Slerp(t.rotation, fig.lieTurn, dt * 3f);
                    if (t.position.y <= SwordFightArena.DeskY)
                    {
                        t.position = new Vector3(t.position.x, SwordFightArena.DeskY, t.position.z);
                        fig.v.y = 0f;
                        fig.landed = Time.time;
                        Ring(t.position + Vector3.up * 0.2f, 0.6f, "#fff3c8");
                    }
                }
                else
                {
                    float kk = Mathf.Exp(-3f * dt);
                    fig.v *= kk;
                    t.position += fig.v * dt;
                    t.rotation = Quaternion.Slerp(t.rotation, fig.lieTurn, dt * 8f);
                    if (Time.time - fig.landed > 0.9f) { fig.trayFrom = Time.time; fig.trayFrom3 = t.position; }
                }
            }
        }

        /// <summary>The hand lowering the pawn onto its square in the 0.9 s before it comes back, then rising away.</summary>
        void StepHand()
        {
            if (hand == null) hand = BuildHand();
            bool shown = false;
            for (int i = 0; i < 2; i++) if (handPawns[i] != null) handPawns[i].SetActive(false);
            foreach (var v in view.Fighters)
            {
                if (!game.Fighters.TryGetValue(v.Id, out var f) || f == null) continue;
                Vector3 at;
                float s;
                if (!v.Alive && v.SpawnCell >= 0 && v.SpawnCell < SwordBoard.Count && v.Respawn <= (float)settings.RingLead)
                {
                    at = CellCentre(v.SpawnCell);
                    s = -Mathf.Max(0f, LeftFor(v));
                }
                else if (f.Alive && cameBack.TryGetValue(v.Id, out float back) && Time.time - back < 0.5f)
                {
                    at = cameBackAt[v.Id];
                    s = Time.time - back;
                }
                else continue;
                shown = true;
                float yH = s < 0f ? Mathf.Lerp(7f, 1.55f, Ease((s + (float)settings.RingLead) / (float)settings.RingLead)) : Mathf.Lerp(1.55f, 8f, Ease(s / 0.5f));
                hand.transform.position = new Vector3(at.x, at.y + yH * K, at.z - 0.1f);
                if (s < 0f)
                {
                    int team = Mathf.Clamp(f.Pawn.Team, 0, 1);
                    if (handPawns[team] == null)
                    {
                        handPawns[team] = SwordFightShapes.Piece(PieceKind.Pawn, team == 0 ? whiteMat : blackMat);
                        handPawns[team].transform.localScale = Vector3.one * K;
                    }
                    handPawns[team].SetActive(true);
                    handPawns[team].transform.position = new Vector3(at.x, at.y + (yH - 1.25f) * K, at.z);
                }
                break;   // one hand (the demo's)
            }
            hand.SetActive(shown);
        }

        /// <summary>A client's respawn time runs down between the host's states.</summary>
        float LeftFor(SwordMatchFighter v) => Authority ? v.Respawn : v.Respawn - (Time.realtimeSinceStartup - viewAt);

        GameObject BuildHand()
        {
            var skin = Made(SwordFightShapes.Lit("#e9d8c4", 0.7f));
            var sleeve = Made(SwordFightShapes.Lit("#2c3b5e", 0.8f));
            var g = new GameObject("Respawn hand");
            var t = g.transform;
            SwordFightShapes.Part(PrimitiveType.Cube, skin, t, Vector3.zero, new Vector3(1.4f, 0.4f, 1.6f));
            for (int k = 0; k < 4; k++)
                SwordFightShapes.Part(PrimitiveType.Cylinder, skin, t, new Vector3(-0.5f + k * 0.34f, -0.45f, 0.95f), new Vector3(0.33f, 0.65f, 0.33f), Quaternion.Euler(0.4f * Mathf.Rad2Deg, 0f, 0f));
            SwordFightShapes.Part(PrimitiveType.Cylinder, skin, t, new Vector3(0.85f, -0.4f, 0.1f), new Vector3(0.34f, 0.5f, 0.34f), Quaternion.Euler(0f, 0f, 1f * Mathf.Rad2Deg));
            SwordFightShapes.Part(PrimitiveType.Cylinder, sleeve, t, new Vector3(0f, 3.2f, -0.4f), new Vector3(1.55f, 3f, 1.55f));
            t.localScale = Vector3.one * K;
            g.SetActive(false);
            return g;
        }

        void Bubble(SwordFightPawn f, bool on)
        {
            bubbles.TryGetValue(f, out var b);
            if (!on) { if (b != null) b.SetActive(false); return; }
            if (b == null)
            {
                b = SwordFightShapes.Part(PrimitiveType.Sphere, bubbleMat, null, Vector3.zero, Vector3.one * 1.5f);
                b.name = "Protection bubble";
                b.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                bubbles[f] = b;
            }
            b.SetActive(true);
            b.transform.position = f.Pawn.Hips.position + Vector3.up * 0.15f;
            var c = bubbleMat.color;
            c.a = 0.18f + 0.08f * Mathf.Sin(Time.time * 12f);
            bubbleMat.color = c;
        }

        void StealPillar(SwordFightPawn at)
        {
            if (at == null) return;
            pillarAt = Time.time;
            pillarPos = new Vector3(at.Pawn.Hips.position.x, floor.Top + 3.5f, at.Pawn.Hips.position.z);
            Ring(at.Pawn.Hips.position, 1.7f, "#ffd56b");
        }

        void StepPillar()
        {
            float s = Time.time - pillarAt;
            if (s < 0f || s >= 1f) { if (pillar != null) pillar.SetActive(false); return; }
            if (pillar == null)
            {
                pillar = SwordFightShapes.Part(PrimitiveType.Cylinder, pillarMat, null, Vector3.zero, Vector3.one);
                pillar.name = "Steal pillar";
                pillar.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }
            pillar.SetActive(true);
            pillar.transform.position = pillarPos;
            pillar.transform.localScale = new Vector3(1.2f * (1f + s), 4f, 1.2f * (1f + s));
            var c = pillarMat.color;
            c.a = (1f - s) * 0.55f;
            pillarMat.color = c;
        }

        // The demo's hit rings: a ring facing the camera that grows and fades in 0.5 s, ten sparks flying out round it.
        sealed class Fx { public GameObject go; public Transform ring; public Material ringMat, sparkMat; public Transform[] sparks; public float at, size; public Vector3 pos; public bool used; }
        readonly List<Fx> fxPool = new List<Fx>();

        void Ring(Vector3 at, float size, string hex)
        {
            Fx fx = null;
            foreach (var f in fxPool) if (!f.used) { fx = f; break; }
            if (fx == null)
            {
                if (fxPool.Count >= 12) return;
                fx = new Fx { go = new GameObject("Hit ring"), ringMat = Made(new Material(RingMaterial())), sparkMat = Made(SwordFightShapes.Glow("#ffd16a", 1f)) };
                fx.ring = SwordFightShapes.Part(PrimitiveType.Quad, fx.ringMat, fx.go.transform, Vector3.zero, Vector3.one * 2f).transform;
                fx.sparks = new Transform[10];
                for (int q = 0; q < 10; q++) fx.sparks[q] = SwordFightShapes.Part(PrimitiveType.Sphere, fx.sparkMat, fx.go.transform, Vector3.zero, Vector3.one * 0.1f).transform;
                foreach (var r in fx.go.GetComponentsInChildren<Renderer>()) r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                fxPool.Add(fx);
            }
            var c = SwordFightShapes.Hex(hex);
            fx.ringMat.color = c;
            fx.sparkMat.color = c;
            fx.used = true;
            fx.at = Time.time;
            fx.size = size;
            fx.pos = at;
            fx.go.SetActive(true);
        }

        void StepRings()
        {
            var cam = game.CameraRig != null ? game.CameraRig.Cam : Camera.main;
            foreach (var f in fxPool)
            {
                if (!f.used) continue;
                float s = Time.time - f.at;
                if (s > 0.5f) { f.used = false; f.go.SetActive(false); continue; }
                float u = s / 0.5f, sc = (0.25f + u * 1.5f) * f.size;
                f.go.transform.position = f.pos;
                if (cam != null) f.go.transform.rotation = Quaternion.LookRotation(f.pos - cam.transform.position);
                f.ring.localScale = Vector3.one * 2f * sc;
                var c = f.ringMat.color; c.a = (1f - u) * 0.85f; f.ringMat.color = c;
                var sc2 = f.sparkMat.color; sc2.a = 1f - u; f.sparkMat.color = sc2;
                for (int q = 0; q < f.sparks.Length; q++)
                {
                    float an = q / 10f * Mathf.PI * 2f + f.at;
                    float r = (0.25f + u * 1.3f) * f.size;
                    f.sparks[q].localPosition = new Vector3(Mathf.Cos(an) * r, Mathf.Sin(an) * r, 0f);
                }
            }
        }

        Material RingMaterial()
        {
            if (ringTexMat != null) return ringTexMat;
            const int n = 64;
            var tex = Made(new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp });
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float d = new Vector2(x - n / 2f + 0.5f, y - n / 2f + 0.5f).magnitude / (n / 2f);
                    float a = Mathf.Clamp01(1f - Mathf.Abs(d - 0.85f) / 0.15f);
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
                }
            tex.Apply();
            ringTexMat = Made(new Material(Shader.Find("Sprites/Default")) { mainTexture = tex, renderQueue = 3100 });
            return ringTexMat;
        }

        // ---------------------------------------------------------------- the camera

        Vector3 camPos, camLook;
        bool camHeld;

        /// <summary>The demo's wide shots: circling the board while the pieces are picked (27 m out, 15 up) and once the
        /// result is in (25 m, 11 up); a small shake after heavy hits. Runs after the player's camera (LabCamera, 150).</summary>
        void LateUpdate()
        {
            var rig = game != null ? game.CameraRig : null;
            if (rig == null) return;
            var cam = rig.transform;
            float k = K;
            bool orbit = view.Phase == SwordPhase.Pick || (view.Phase == SwordPhase.Result && PhaseTime > 1.5);
            if (orbit)
            {
                bool pick = view.Phase == SwordPhase.Pick;
                float dist = (pick ? 27f : 25f) * k, h = pick ? 15f : 11f, an = (pick ? -2.6f : -1.3f) + (float)PhaseTime * (pick ? 0.14f : 0.12f);
                Vector3 look = new Vector3(0f, pick ? -1.5f : -3f, 0f);
                Vector3 pos = look + new Vector3(Mathf.Sin(an) * dist, 0f, Mathf.Cos(an) * dist);
                pos.y = h;
                if (!camHeld) { camPos = cam.position; camLook = cam.position + cam.forward * 10f; camHeld = true; }
                float dt = Time.unscaledDeltaTime;
                camPos = Vector3.Lerp(camPos, pos, 1f - Mathf.Exp(-dt * 4.5f));
                camLook = Vector3.Lerp(camLook, look, 1f - Mathf.Exp(-dt * 7f));
                cam.position = camPos;
                cam.LookAt(camLook);
            }
            else camHeld = false;
            if (shake > 0f)
            {
                float q = Time.time * 97f;
                cam.position += new Vector3(Mathf.Sin(q * 1.3f), Mathf.Sin(q * 1.7f + 1f), Mathf.Sin(q * 2.1f + 2f)) * shake;
                shake = Mathf.Max(0f, shake - Time.unscaledDeltaTime * 0.2f / 0.35f);
            }
        }

        /// <summary>The demo's heavy hits (its hit stop list): a short shake on this screen.</summary>
        void ShakeFor(SfFxKind kind)
        {
            switch (kind)
            {
                case SfFxKind.KingCounterHit: case SfFxKind.RookHit: case SfFxKind.QueenHit: case SfFxKind.KnightHit:
                case SfFxKind.QueenCut: case SfFxKind.KnightKickHit: case SfFxKind.BishopPin:
                    shake = Mathf.Max(shake, 0.2f);
                    break;
            }
        }
    }
}
