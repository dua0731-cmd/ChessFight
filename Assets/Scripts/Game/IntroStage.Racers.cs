using ChessFight.Network;
using UnityEngine;
using UnityEngine.Rendering;

namespace ChessFight.Game
{
    // The title screen's moving parts: the runners and the shoving pairs (the
    // match's own models, posed every frame), the pennants' cloth, and the dust
    // and speed streaks the runners leave behind.
    public sealed partial class IntroStage
    {
        // ---------- figures ----------

        // One piece model with a home spot it jostles around, and a stumble a click sets off.
        sealed class Figure
        {
            public readonly LastSceneFigure Body;
            float stumble = -10f, stumbleSide = 1f;

            public Figure(Transform parent, PieceKind kind, int team, Vector3 at)
            {
                Body = LastSceneFigure.Build(kind, team, parent);
                // Facing the camera (on +Z); each pose turns it from there.
                Body.Place(at, at + new Vector3(0, 0, 10f));
                foreach (var r in Body.Root.GetComponentsInChildren<Renderer>()) r.shadowCastingMode = ShadowCastingMode.On;
            }

            public void Stumble()
            {
                if (Time.unscaledTime - stumble < .5f) return;
                stumble = Time.unscaledTime;
                stumbleSide = -stumbleSide;
            }

            // Adds the stumble (a hop, a wobble to one side, arms thrown up) to a pose.
            public FigurePose Shake(FigurePose p)
            {
                float since = Time.unscaledTime - stumble;
                if (since < 0 || since > 1f) return p;
                float k = Mathf.Exp(-since * 4f);
                p.Jump += Mathf.Sin(Mathf.Min(since, .35f) / .35f * Mathf.PI) * .35f;
                p.Tip += stumbleSide * .55f * k * Mathf.Cos(since * 14f);
                p.ArmLeft = Mathf.Lerp(p.ArmLeft ?? p.Arm, 2.3f, k);
                p.ArmRight = Mathf.Lerp(p.ArmRight ?? p.Arm, 2.1f, k);
                return p;
            }
        }

        // A runner: the lean-and-stride run to screen right, drifting back and forth in the pack.
        sealed class Runner
        {
            public readonly Figure Body;
            readonly float seed, drift;

            public Runner(Transform parent, PieceKind kind, int team, Vector3 at, float seed, float drift)
            {
                Body = new Figure(parent, kind, team, at);
                this.seed = seed;
                this.drift = drift;
            }

            public void Tick(float t, float dt, float pace)
            {
                var p = Stride(t * pace, seed);
                p.Dx = Mathf.Sin(t * .45f + seed * 2.1f) * drift;
                p.Dz = Mathf.Sin(t * .31f + seed) * drift * .4f;
                Body.Body.Pose(Body.Shake(p));
            }
        }

        // A real run (10-17, 승규 님: they waddled — the old stride rolled the body from side to side and
        // flapped the arms out sideways): leaning forward into the run, legs driving in a long stride,
        // arms pumping forward and back against the legs, the body highest in the flight between steps
        // and squashing a little as each foot lands, a slight twist of the shoulders and no side roll.
        // Turned 40 degrees to screen right so the stride reads from the camera.
        static FigurePose Stride(float t, float seed)
        {
            float ph = t * 12f + seed * 1.7f, s = Mathf.Sin(ph), a = Mathf.Abs(s);
            float leg = Mathf.Sign(s) * Mathf.Pow(a, .7f);
            return new FigurePose
            {
                Yaw = -40f * Mathf.Deg2Rad + s * .07f,
                Lean = .22f + .06f * (1f - a),
                Tip = .025f * s,
                Leg = leg * 1.0f,
                ArmSwing = -leg * 1.05f,
                ArmLeft = .3f, ArmRight = .3f,
                // Lowest as a foot passes under the body, highest in the flight with the legs spread.
                Jump = .15f * Mathf.Pow(a, 1.5f),
                Squash = .07f * Mathf.Pow(1f - a, 6f) - .03f * a
            };
        }

        // Two runners side by side: every few seconds the pusher shoves the other off
        // balance with an arm; it tips away, staggers and comes back into the pack.
        // Numbers from the shove the mockup was rendered with (Temp/Captures/r150_cut5).
        sealed class Shove
        {
            public readonly Figure Pusher, Pushed;
            readonly float seed, period;

            public Shove(Transform parent, PieceKind pusher, int pusherTeam, PieceKind pushed, int pushedTeam, Vector3 at, float seed, float period)
            {
                // The pusher on screen right of the pushed (camera on +Z: +X is screen left).
                // Wider apart than the mockup render (.82 m): closer, the bodies sank into each other.
                Pusher = new Figure(parent, pusher, pusherTeam, at + new Vector3(-.5f, 0, 0));
                Pushed = new Figure(parent, pushed, pushedTeam, at + new Vector3(.62f, 0, 0));
                this.seed = seed;
                this.period = period;
            }

            public void Tick(float t, float dt, float pace)
            {
                float cycle = ((t * pace + seed * period) % period) / period;
                // 0..1 how far into the shove: arm out .25-.4, the other tips .32-.5, all back by .9.
                float reach = Smooth(.22f, .38f, cycle) * (1f - Smooth(.6f, .85f, cycle));
                float tip = Smooth(.3f, .48f, cycle) * (1f - Smooth(.58f, .9f, cycle));

                var a = Stride(t * pace, seed);
                a.Yaw = Mathf.Lerp(a.Yaw, -35f * Mathf.Deg2Rad, reach);
                a.ArmLeft = Mathf.Lerp(a.ArmLeft ?? a.Arm, 2.0f, reach);
                a.Tip = Mathf.Lerp(a.Tip, .15f, reach);
                a.Dx = .14f * reach;
                Pusher.Body.Pose(Pusher.Shake(a));

                var b = Stride(t * pace, seed + 1.3f);
                // No lean towards the pusher: it tipped its head into the other body.
                b.Tip = 0f;
                b.Yaw = Mathf.Lerp(b.Yaw, 15f * Mathf.Deg2Rad, tip);
                b.Tip = Mathf.Lerp(b.Tip, .6f, tip);
                b.ArmLeft = Mathf.Lerp(b.ArmLeft ?? b.Arm, 2.3f, tip);
                b.ArmRight = Mathf.Lerp(b.ArmRight ?? b.Arm, 1.4f, tip);
                b.Leg = Mathf.Lerp(b.Leg, .7f, tip);
                b.Dx = .35f * tip;
                Pushed.Body.Pose(Pushed.Shake(b));
            }
        }

        static float Smooth(float a, float b, float x) => Mathf.SmoothStep(0, 1, Mathf.InverseLerp(a, b, x));

        // Where the mockup (BrawlShove) has them, less the white pawn in front and the black
        // rook at the back (승규 님 asked for both gone): a white rook shoving a black pawn,
        // a black knight and a white queen behind, a white bishop at the back. Depth from the
        // camera picks their size on screen.
        void Racers()
        {
            runners.Add(new Runner(transform, PieceKind.Knight, 1, new Vector3(Sx(.15f), 0, -3.4f), .3f, .3f));
            runners.Add(new Runner(transform, PieceKind.Queen, 0, new Vector3(Sx(3.05f), 0, -2.2f), 1.1f, .25f));
            // R150 follow-up: the black rook that shoved the bishop is gone; the bishop runs on its own.
            runners.Add(new Runner(transform, PieceKind.Bishop, 0, new Vector3(Sx(4.5f), 0, -3.6f), 3.4f, .25f));
            shoves.Add(new Shove(transform, PieceKind.Rook, 0, PieceKind.Pawn, 1, new Vector3(Sx(1.75f), 0, -1f), 0f, 2.7f));
        }

        // ---------- dust and streaks ----------

        sealed class Puff
        {
            public Transform Shape;
            public Material Material;
            public float Born = -10f, Life, Size, Rise;
            public Vector3 Velocity;
            public bool Streak;
        }

        void Effects()
        {
            puffMaterial = MenuArt.Glow("intro dust", MenuArt.RadialTexture(), new Color(.92f, .84f, .7f, .5f), false);
            streakMaterial = MenuArt.Glow("intro streak", null, new Color(1f, .97f, .9f, .55f), false);
            for (int i = 0; i < 40; i++)
            {
                bool streak = i >= 28;
                var m = new Material(streak ? streakMaterial : puffMaterial);
                var go = LastSceneArt.Part(transform, streak ? "Streak" : "Dust", LastSceneArt.Panel(1f, 1f), m, new Vector3(0, -50f, 0), null, null, false);
                puffs.Add(new Puff { Shape = go.transform, Material = m, Streak = streak });
            }
        }

        void Dust(float t, float dt, float speed)
        {
            if (t >= nextPuff)
            {
                nextPuff = t + .07f;
                // Kick dust from a random runner's feet, and now and then a streak behind it.
                var figures = new System.Collections.Generic.List<Figure>(AllFigures());
                var f = figures[Random.Range(0, figures.Count)];
                Spawn(false, f.Body.Position + new Vector3(Random.Range(.1f, .5f), .12f, Random.Range(-.2f, .2f)), speed);
                if (Random.value < .45f)
                    Spawn(true, f.Body.Position + new Vector3(.6f + Random.value * .5f, .55f + Random.value * .65f, -.35f), speed);
            }
            var face = view != null ? view.transform.rotation : Quaternion.identity;
            foreach (var p in puffs)
            {
                float age = t - p.Born;
                if (age > p.Life) { if (p.Shape.localPosition.y > -40f) p.Shape.localPosition = new Vector3(0, -50f, 0); continue; }
                float k = age / p.Life;
                p.Shape.localPosition += p.Velocity * dt + Vector3.up * p.Rise * dt;
                p.Shape.rotation = face;
                if (p.Streak)
                {
                    p.Shape.localScale = new Vector3(p.Size * (1f + k), .022f, 1f);
                    var c = p.Material.color; c.a = .28f * (1f - k) * Mathf.Min(1f, k * 8f); p.Material.color = c;
                }
                else
                {
                    float s = p.Size * (.5f + k * 1.4f);
                    p.Shape.localScale = new Vector3(s * 1.4f, s, 1f);
                    var c = p.Material.color; c.a = .45f * (1f - k) * Mathf.Min(1f, k * 6f); p.Material.color = c;
                }
            }
        }

        void Spawn(bool streak, Vector3 at, float speed)
        {
            foreach (var p in puffs)
            {
                if (p.Streak != streak || Time.unscaledTime - p.Born < p.Life) continue;
                p.Born = Time.unscaledTime;
                p.Shape.localPosition = at;
                if (streak)
                {
                    p.Life = .28f; p.Size = .35f + Random.value * .35f; p.Rise = 0;
                    p.Velocity = new Vector3(speed * 1.2f, 0, 0);
                }
                else
                {
                    p.Life = .7f + Random.value * .4f; p.Size = .25f + Random.value * .25f; p.Rise = .25f;
                    // The floor carries it towards screen left.
                    p.Velocity = new Vector3(speed * .9f, 0, Random.Range(-.2f, .2f));
                }
                return;
            }
        }

        // ---------- pennants ----------

        // A swallowtail pennant hung from the rod: a grid of cloth that ripples down its
        // length, more so further from the rod. The mouse passing over it whips up a gust.
        // Also hung in the lobby castle (LobbyStage.Castle.cs, 10-17).
        internal sealed class Pennant
        {
            const int Cols = 8, Rows = 18;
            const float Width = 1.25f, Height = 2.4f, WallGap = .26f;
            readonly Mesh mesh;
            readonly Vector3[] rest, moved;
            readonly Transform shape;
            public Transform Shape => shape;
            readonly Material material;
            readonly Texture2D texture;
            readonly float phase;
            float gust;

            public Pennant(Transform parent, Vector3 top, Texture2D texture, int index)
            {
                this.texture = texture;
                phase = index * 1.37f;
                rest = new Vector3[(Cols + 1) * (Rows + 1)];
                moved = new Vector3[rest.Length];
                var uv = new Vector2[rest.Length];
                var tris = new int[Cols * Rows * 6];
                for (int r = 0; r <= Rows; r++)
                    for (int c = 0; c <= Cols; c++)
                    {
                        int i = r * (Cols + 1) + c;
                        float u = c / (float)Cols, v = r / (float)Rows;   // v 0 at the rod
                        // Facing the camera (+Z): u runs to screen right, which is -X.
                        rest[i] = new Vector3((.5f - u) * Width, -v * Height, 0);
                        uv[i] = new Vector2(u, 1f - v);
                    }
                int k = 0;
                for (int r = 0; r < Rows; r++)
                    for (int c = 0; c < Cols; c++)
                    {
                        int a = r * (Cols + 1) + c, b = a + 1, d = a + Cols + 1, e = d + 1;
                        tris[k++] = a; tris[k++] = b; tris[k++] = d;
                        tris[k++] = b; tris[k++] = e; tris[k++] = d;
                    }
                mesh = new Mesh { name = "Intro pennant" };
                mesh.vertices = rest;
                mesh.uv = uv;
                mesh.triangles = tris;
                mesh.RecalculateNormals();
                mesh.MarkDynamic();
                material = new Material(LastSceneArt.Cutout("intro pennant " + index, texture, .2f));
                // The shared template keeps the first pennant's texture (the title screen's, destroyed with it):
                // every pennant sets its own, or the lobby's lost their cloth after the title screen.
                material.mainTexture = texture;
                if (material.HasProperty("_Cull")) material.SetInt("_Cull", (int)CullMode.Off);
                var go = LastSceneArt.Part(parent, "Pennant", mesh, material, top, null, null, true);
                shape = go.transform;
            }

            public void Tick(float t, float dt, Camera view, Vector2 mouse)
            {
                if (view != null)
                {
                    // A gust while the mouse is over the cloth, dying away after.
                    var a = view.WorldToScreenPoint(shape.position + new Vector3(Width / 2, 0, 0));
                    var b = view.WorldToScreenPoint(shape.position + new Vector3(-Width / 2, -Height, 0));
                    var box = Rect.MinMaxRect(Mathf.Min(a.x, b.x), Mathf.Min(a.y, b.y), Mathf.Max(a.x, b.x), Mathf.Max(a.y, b.y));
                    bool over = a.z > 0 && box.Contains(mouse);
                    gust = over ? Mathf.MoveTowards(gust, 1f, dt * 1.2f) : Mathf.MoveTowards(gust, 0f, dt * .5f);
                }
                float amp = .1f + gust * .1f, speed = 2f + gust * .6f;
                for (int i = 0; i < rest.Length; i++)
                {
                    var p = rest[i];
                    float v = -p.y / Height, u = .5f - p.x / Width;
                    float hang = Mathf.Pow(v, 1.2f);
                    float wave = Mathf.Sin(t * speed + phase - v * 4.2f + u * 1.6f);
                    float wave2 = Mathf.Sin(t * speed * 1.7f + phase * 2f - v * 7f);
                    p.z += hang * amp * (wave * 1.2f + wave2 * .4f);
                    // Sideways too, so the flapping shows in the outline, not only in the shading.
                    p.x += hang * amp * .9f * Mathf.Sin(t * speed * .8f + phase - v * 3.2f);
                    p.y += hang * amp * .12f * Mathf.Abs(wave);
                    // Never back through the wall behind the rod.
                    p.z = Mathf.Max(p.z, -WallGap);
                    moved[i] = p;
                }
                mesh.vertices = moved;
                mesh.RecalculateNormals();
                mesh.RecalculateBounds();
            }

            public void Dispose()
            {
                if (mesh != null) Object.Destroy(mesh);
                if (material != null) Object.Destroy(material);
                if (texture != null) Object.Destroy(texture);
            }
        }
    }
}
