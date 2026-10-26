using System.Collections.Generic;
using ChessFight.Network;
using UnityEngine;

namespace ChessFight.RagdollLab
{
    public sealed partial class SwordFightPawn
    {
        // R48 click-swing timings and arc, kept separately for live A/B testing. Since 10-25 every piece swings its own way
        // (SwingStyle below); these stay the queen's timings - the pace the skill test bed times its dummies by.
        public const float Windup = .16f, ActiveTime = .20f, Recovery = .34f;
        public const float SwingDuration = Windup + ActiveTime + Recovery;
        public float SwingAge { get; private set; } = 99f;
        Vector3 swingDirection = Vector3.forward, bufferedAim, previousGrip, previousClickTip;
        readonly HashSet<SwordFightPawn> hitThisSwing = new HashSet<SwordFightPawn>();
        float inputBuffer;
        bool wasActive;

        void CancelClickSwing()
        { inputBuffer = 0; SwingAge = 99f; wasActive = false; hitThisSwing.Clear(); Pawn.BodyTurn = 0f; }

        /// <summary>A skill takes the sword: a click cut still running stops (without the step's input dropped, as StopCombat does).</summary>
        internal void CancelSwing() { if (ClassicControls) CancelClickSwing(); }

        void StepClickSwing(float dt)
        {
            if (Pawn.State != PawnState.Active) { CancelClickSwing(); return; }
            if (inputBuffer > 0 && !Attacking)
            {
                bufferedAim.y = 0;
                swingDirection = bufferedAim.sqrMagnitude > .01f ? bufferedAim.normalized : Pawn.Facing;
                style = StyleOf(Pawn.Piece);
                SwingAge = 0; Swings++; inputBuffer = 0; Protection = 0;
                hitThisSwing.Clear(); wasActive = false;
            }
            inputBuffer = Mathf.Max(0, inputBuffer - dt);
            if (!Attacking) { Pawn.BodyTurn = 0f; return; }
            SwingAge += dt;
            Pawn.BodyTurn = SwingPhase(style.BodyUp, style.BodyThrough);
            bool active = SwingAge >= style.Windup && SwingAge <= style.Windup + style.Active;
            Vector3 grip = ClickGrip(), tip = grip + ClickBladeDirection() * .88f;
            if (active)
            {
                if (!wasActive) { previousGrip = grip; previousClickTip = tip; }
                for (int s = 0; s <= 3; s++)
                    DetectClickHit(Vector3.Lerp(previousGrip, grip, s / 3f), Vector3.Lerp(previousClickTip, tip, s / 3f));
            }
            previousGrip = grip; previousClickTip = tip; wasActive = active;
        }

        void DetectClickHit(Vector3 start, Vector3 end)
        {
            if (!Attacking) return;   // stopped this step (a parried cut)
            int count = Physics.OverlapCapsuleNonAlloc(start, end, .15f, overlaps, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
            {
                var c = overlaps[i];
                if (!RagdollPawn.ColliderOwner.TryGetValue(c, out var target) || target == Pawn ||
                    target.Team == Pawn.Team || c.GetComponent<SwordBladeContact>() != null) continue;
                var victim = target.GetComponent<SwordFightPawn>();
                if (victim == null || !victim.Alive || victim.Protection > 0 || !hitThisSwing.Add(victim)) continue;
                // A king in its guard (the skill test scene) takes the cut and answers it.
                if (victim.Skills != null && victim.Skills.Parry(this, c.ClosestPoint(Hand.position))) { HitFlash = .1f; CancelClickSwing(); return; }
                Vector3 away = target.Hips.position - Pawn.Hips.position; away.y = 0;
                Vector3 push = (swingDirection * .6f + away.normalized * .4f).normalized;
                target.TakeHit(push * 3.4f + Vector3.up * 1.05f, .65f, 0, true);
                victim.StopCombat(); victim.HitFlash = .14f; HitFlash = .1f; HitsLanded++;
                RaiseStruck(this, victim);
            }
        }

        Vector3 ClickBladeDirection()
        {
            if (!Alive || Pawn.State != PawnState.Active) return Hand.rotation * Vector3.right;
            Vector3 dir = Attacking ? SwingLine(SwingAge) : IdleLine();
            // The posed blade has no collider: it is kept over the floor, not swept through it - and out of its own body.
            Vector3 grip = ClickGrip();
            return AboveFloor(grip, ClearOfBody(grip, dir));
        }

        Vector3 IdleLine() => (Quaternion.AngleAxis(-25f, Vector3.up) * Pawn.Facing + Vector3.up * .35f).normalized;

        /// <summary>The piece's own cut (SwingStyle) at <paramref name="age"/>: the blade brought from the guard to the cut's
        /// first line through the windup, along its lines through the cut (eased in, fast at the end), then back to the
        /// guard.</summary>
        Vector3 SwingLine(float age)
        {
            var keys = style.Keys;
            if (age < style.Windup) return Vector3.Slerp(IdleLine(), SwingWorld(keys[0]), Ease(Mathf.Max(0f, age) / style.Windup));
            if (age < style.Windup + style.Active)
            {
                float k = (age - style.Windup) / style.Active;
                // A snapping chop speeds up to its hit 60% through, then the draw slows out of it (same speed at the hit).
                float p = !style.Snap ? k * k * (3f - 2f * k) : k < .6f ? .6f * (k / .6f) * (k / .6f) : .6f + .4f * Ease((k - .6f) / .4f);
                float at = p * (keys.Length - 1);
                int i = Mathf.Min((int)at, keys.Length - 2);
                return Vector3.Slerp(SwingWorld(keys[i]), SwingWorld(keys[i + 1]), at - i);
            }
            return Vector3.Slerp(SwingWorld(keys[keys.Length - 1]), IdleLine(), Ease((age - style.Windup - style.Active) / style.Recovery));
        }

        /// <summary>Which way the blade's flat faces (its "up"; the swords' edges are their X). 10-25 (승규 님: "킹도 비슷하게
        /// 칼날로 베기끔"): held flat-up, an upright chop came down on the flat; through the windup and the cut the blade
        /// turns so an edge leads the cut, and back flat-up through the recovery.</summary>
        Vector3 bladeUp = Vector3.up;

        Vector3 BladeUp(Vector3 dir)
        {
            Vector3 want = Vector3.up;
            if (Attacking && SwingAge < style.Windup + style.Active)
            {
                // The way the cut moves (through the windup, the way it will start), across the blade.
                float at = Mathf.Max(SwingAge, style.Windup + .01f);
                Vector3 ahead = Vector3.ProjectOnPlane(SwingLine(at + .02f) - SwingLine(at - .01f), dir);
                if (ahead.sqrMagnitude > 1e-6f)
                {
                    // The kukri's edge is its inner curve, on its -X side; the straight swords cut with either edge.
                    Vector3 up = Vector3.Cross(dir, Pawn.Piece == PieceKind.Knight ? -ahead.normalized : ahead.normalized);
                    want = Pawn.Piece != PieceKind.Knight && Vector3.Dot(up, bladeUp) < 0f ? -up : up;
                }
            }
            bladeUp = Vector3.Slerp(bladeUp, want, 1f - Mathf.Exp(-60f * Mathf.Max(Time.deltaTime, 1e-4f)));
            return bladeUp;
        }

        static float Ease(float t) { t = Mathf.Clamp01(t); return 1f - (1f - t) * (1f - t); }

        /// <summary>A piece's swing (10-25, 승규 님: "기물별로 휘두르는 모션을 따로" - the table: king's overhead chop, queen's
        /// diagonal, rook's waist sweep, bishop's rising cut, knight's chop-and-hook, pawn's thrust). Where it hits is
        /// just where its blade goes (the cut sweeps the blade's line), so a wide sweep reaches wide and a thrust far -
        /// the pace is what evens them out.</summary>
        sealed class SwingStyle
        {
            public float Windup, Active, Recovery;
            /// <summary>The blade's lines through the cut, in the swing's frame: x to the sword hand's side, y up, z ahead.</summary>
            public Vector3[] Keys;
            /// <summary>The chest's turn (+ toward the sword hand) at the top of the windup and at the end of the cut, and
            /// its lean forward (+) into the cut and back (-) in the windup.</summary>
            public float TwistUp, TwistThrough, LeanIn, LeanBack;
            /// <summary>A thrust's arm (the swing's frame): drawn back here through the windup while the blade already
            /// points ahead, then driven out along the blade through the first part of the cut. Zero: the arm keeps to
            /// the blade's line, as for a cut.</summary>
            public Vector3 ArmDrawn;
            /// <summary>A draw cut's arm (the swing's frame): pulled back here after the hit (the last 40% of the cut) while
            /// the blade drags along its edge, back to the blade's line through the recovery. Zero: none.</summary>
            public Vector3 ArmPull;
            /// <summary>The whole body's turn (RagdollPawn.BodyTurn, + toward the sword hand) at the top of the windup and
            /// at the end of the cut: the rook's sweep carries its hand round its wide body with it.</summary>
            public float BodyUp, BodyThrough;
            /// <summary>The cut speeds up into its hit (a snapping chop, 60% through its lines) instead of easing in and out.</summary>
            public bool Snap;
            public float Length => Windup + Active + Recovery;
        }

        /// <summary>Lines round an arc in the plane of ahead and <paramref name="side"/>: 0 ahead, 90 along side.</summary>
        static Vector3[] Arc(Vector3 side, float from, float to, int steps)
        {
            var keys = new Vector3[steps + 1];
            side.Normalize();
            for (int i = 0; i <= steps; i++)
            {
                float r = Mathf.Lerp(from, to, i / (float)steps) * Mathf.Deg2Rad;
                keys[i] = (new Vector3(0f, 0f, Mathf.Cos(r)) + side * Mathf.Sin(r)).normalized;
            }
            return keys;
        }

        static readonly SwingStyle KingSwing = new SwingStyle
        {
            // Heavy and nearly upright: raised high over the head, brought straight down (15 degrees off upright).
            Windup = .26f, Active = .18f, Recovery = .40f,
            Keys = Arc(new Vector3(.27f, 1f, 0f), 115f, -55f, 6), TwistUp = 6f, TwistThrough = -6f, LeanIn = 18f, LeanBack = -6f,
        };
        static readonly SwingStyle QueenSwing = new SwingStyle
        {
            // Quick and wide: raised high out over the sword shoulder, cut down across the body to the far hip (45 degrees)
            // with the body turning through it. 10-25 (승규 님: "퀸 너무 까딱까딱 거려 좀 멋있게"): it was drawn back behind
            // the shoulder, into the head, and the body check threw it from side to side; now it is raised high and upright
            // beside the head (its pommel hangs down the body's side, clear of it) and the body turns through the cut.
            Windup = .18f, Active = .20f, Recovery = .36f,
            Keys = Arc(new Vector3(.6f, 1f, 0f), 100f, -65f, 8), TwistUp = 15f, TwistThrough = -20f, LeanIn = 12f, LeanBack = -4f,
            BodyUp = 0f, BodyThrough = -45f,
        };
        static readonly SwingStyle RookSwing = new SwingStyle
        {
            // Waist high, a half circle from the sword side round the front to the far side: slow, but it sweeps wide. The
            // hand sits against the wide body, so the body turns with the blade and carries the hand round.
            Windup = .22f, Active = .26f, Recovery = .40f,
            Keys = Arc(new Vector3(1f, -.08f, 0f), 95f, -95f, 8), TwistUp = 15f, TwistThrough = -15f, LeanIn = 6f, LeanBack = 0f,
            BodyUp = 80f, BodyThrough = -85f,
        };
        static readonly SwingStyle BishopSwing = new SwingStyle
        {
            // The queen's line the other way: drawn down to the far hip with the body turned away, then cut up across the
            // body to high and upright beside the head as the body turns back through it (10-25, 승규 님: "비숍도
            // 마찬가지로" - the queen's sweep and body turn).
            Windup = .18f, Active = .20f, Recovery = .36f,
            Keys = Arc(new Vector3(.6f, 1f, 0f), -65f, 100f, 8), TwistUp = -18f, TwistThrough = 15f, LeanIn = 4f, LeanBack = 8f,
            BodyUp = -40f, BodyThrough = 30f,
        };
        static readonly SwingStyle KnightSwing = new SwingStyle
        {
            // A kukri's chop (10-25, 승규 님: "나이트는 휘두르는 게 쿠크리 처럼"): the blade cocked back over the sword
            // shoulder with the elbow drawn back, a short chop that speeds up into the hit (the wrist snapping the heavy belly
            // through) down across the front, then a draw - the blade pulled back toward the far hip along its edge.
            Windup = .14f, Active = .24f, Recovery = .36f,
            Keys = new[]
            {
                new Vector3(.35f, .55f, -.75f), new Vector3(.35f, .9f, .1f), new Vector3(.2f, .5f, .85f), new Vector3(0f, -.15f, 1f),
                new Vector3(-.25f, -.55f, .8f), new Vector3(-.4f, -.65f, .4f),
            },
            TwistUp = 15f, TwistThrough = -25f, LeanIn = 20f, LeanBack = -6f,
            BodyUp = 30f, BodyThrough = -30f,
            ArmDrawn = new Vector3(.55f, .35f, -.6f), ArmPull = new Vector3(.3f, -.75f, -.35f), Snap = true,
        };
        static readonly SwingStyle PawnSwing = new SwingStyle
        {
            // A straight thrust ahead (the pawn only goes forward), lifting a little at its end.
            Windup = .12f, Active = .14f, Recovery = .30f,
            Keys = new[] { new Vector3(.05f, .12f, 1f), new Vector3(0f, .04f, 1f), new Vector3(0f, .1f, 1f), new Vector3(0f, .42f, .9f) },
            TwistUp = 20f, TwistThrough = -12f, LeanIn = 22f, LeanBack = -10f,
            ArmDrawn = new Vector3(.35f, -.7f, -.45f),
        };

        SwingStyle style = QueenSwing;

        static SwingStyle StyleOf(PieceKind piece) => piece switch
        {
            PieceKind.King => KingSwing,
            PieceKind.Rook => RookSwing,
            PieceKind.Bishop => BishopSwing,
            PieceKind.Knight => KnightSwing,
            PieceKind.Pawn => PawnSwing,
            _ => QueenSwing,
        };

        /// <summary>A value wound to <paramref name="up"/> through the windup, carried to <paramref name="through"/> by the end
        /// of the cut and eased back to zero through the recovery.</summary>
        float SwingPhase(float up, float through)
        {
            if (SwingAge < style.Windup) return up * Ease(SwingAge / style.Windup);
            if (SwingAge < style.Windup + style.Active)
            {
                float k = (SwingAge - style.Windup) / style.Active;
                return Mathf.Lerp(up, through, k * k * (3f - 2f * k));
            }
            return through * (1f - Ease((SwingAge - style.Windup - style.Active) / style.Recovery));
        }

        Vector3 SwingWorld(Vector3 local)
        {
            Vector3 fwd = swingDirection, right = Vector3.Cross(Vector3.up, fwd);
            return (right * local.x + Vector3.up * local.y + fwd * local.z).normalized;
        }

        // The body's drawn shape (the piece's skinned body), for keeping the posed blade out of it; how far the hilt reaches
        // back from the grip (the boxes' 0.1 m, a piece model's longer: SwordFightPawn.UpdateSwordModel).
        readonly List<Renderer> bodyLook = new List<Renderer>();
        int bodyLookPiece = -1;
        float hiltBack = .1f;
        Transform visualHand;
        // The middle of the drawn mitten, in its bone's space. 10-25 (승규 님: "폰처럼 손잡이 부분에 겹치게 … 다른 기물들은
        // 붕 떠져 있어"): the bone starts at the wrist and the pieces' mittens sit out past it on the stub's end, so a grip
        // held at the bone passed beside the mitten; it is held through the mitten's middle, as the pawn's is.
        Vector3 handMiddle;

        /// <summary>The middle of what <paramref name="skin"/> draws on its bone <paramref name="bone"/> (the vertices
        /// mostly on it), in that bone's space.</summary>
        static Vector3 HandMiddle(SkinnedMeshRenderer skin, int bone) => MittenBounds(skin, bone).center;

        /// <summary>The mitten in its bone's space: the bounds of the vertices mostly on <paramref name="bone"/> (through its
        /// bindpose); empty at the bone's origin (the wrist) if there are none. 10-25: also where the bishop's rescue rope is
        /// held (SwordFightSkillFx.RopeHand.cs).</summary>
        internal static Bounds MittenBounds(SkinnedMeshRenderer skin, int bone)
        {
            var mesh = skin.sharedMesh;
            if (mesh == null || bone < 0 || bone >= mesh.bindposes.Length) return default;
            var weights = mesh.boneWeights;
            var vertices = mesh.vertices;
            Matrix4x4 toBone = mesh.bindposes[bone];
            Bounds b = default;
            bool any = false;
            for (int i = 0; i < vertices.Length && i < weights.Length; i++)
            {
                if (weights[i].boneIndex0 != bone || weights[i].weight0 < .5f) continue;
                Vector3 p = toBone.MultiplyPoint3x4(vertices[i]);
                if (!any) { b = new Bounds(p, Vector3.zero); any = true; } else b.Encapsulate(p);
            }
            return any ? b : default;
        }

        /// <summary>The blade from <paramref name="grip"/> turned out round the body until it no longer passes through it
        /// (10-25, 승규 님: "칼을 들 때 몸에 관통이 되는 현상 수정해줘" — a wide piece such as the rook had the ready blade, turned
        /// 25° across the body, through its middle). The body is its drawn parts' upright box, as a round column.</summary>
        Vector3 ClearOfBody(Vector3 grip, Vector3 dir)
        {
            if (!BodyColumn(out Vector3 axis, out float radius, out float lo, out float hi)) return dir;
            radius += .03f;
            // The hand may itself be inside the drawn body: then only a blade (or a pommel) going in deeper than it counts.
            float gripOff = new Vector2(grip.x - axis.x, grip.z - axis.z).magnitude;
            // Held in the drawn hand: the body is no wider there than the hand is out from it (its box is the widest part).
            if (visualHand != null) radius = Mathf.Min(radius, gripOff - .01f);
            int Inside(Vector3 d)
            {
                if (bodyProfile != null) return InsideProfile(grip, d);
                int n = 0;
                // The blade out to its tip, and the hilt back to the pommel (the piece models' grips are long).
                for (int i = -6; i <= 8; i++)
                {
                    if (i == 0) continue;
                    Vector3 p = grip + d * (i > 0 ? .12f + .76f * i / 8f : hiltBack * i / 6f);
                    if (p.y < lo || p.y > hi) continue;
                    float off = new Vector2(p.x - axis.x, p.z - axis.z).magnitude;
                    // The pommel may just touch the body's side (a few cm, hidden by the hand): turning the whole blade
                    // away for it threw a raised blade about (10-25, the queen's "까딱까딱").
                    if (off < (i > 0 ? radius : radius - .05f) && off < gripOff + .02f) n++;
                }
                return n;
            }
            // 10-25 (승규 님: "칼을 휘두르면 이렇게 떨려 자연스럽게"): the turn that clears it was picked fresh each call in 15°
            // steps, so through a swing it jumped from side to side (and the arm, aimed along the blade, moved the drawn
            // hand and so the pick again). Now it is found once a frame to 2°, keeps to the side it is already turned to
            // unless the other is much nearer, and the blade eases to it over a few frames.
            if (clearFrame != Time.frameCount)
            {
                clearFrame = Time.frameCount;
                float target = 0f;
                if (Inside(dir) > 0)
                {
                    (float turn, bool clear) Need(float sign)
                    {
                        int best = int.MaxValue;
                        float at = 0f;
                        for (int a = 2; a <= 180; a += 2)
                        {
                            int n = Inside(Quaternion.AngleAxis(sign * a, Vector3.up) * dir);
                            if (n == 0) return (sign * a, true);
                            if (n < best) { best = n; at = sign * a; }
                        }
                        return (at, false);
                    }
                    var right = Need(1f);
                    var left = Need(-1f);
                    float rc = Mathf.Abs(right.turn) + (right.clear ? 0f : 360f) + (clearTurn < -1f ? 30f : 0f);
                    float lc = Mathf.Abs(left.turn) + (left.clear ? 0f : 360f) + (clearTurn > 1f ? 30f : 0f);
                    target = rc <= lc ? right.turn : left.turn;
                }
                clearTurn = Mathf.SmoothDampAngle(clearTurn, target, ref clearSpeed, .06f, Mathf.Infinity, Mathf.Max(Time.deltaTime, 1e-4f));
            }
            return Mathf.Abs(clearTurn) < .01f ? dir : Quaternion.AngleAxis(clearTurn, Vector3.up) * dir;
        }

        float clearTurn, clearSpeed, armOut;
        int clearFrame = -1, armOutFrame = -1;

        // 10-25 (승규 님: "나이트가 몸에 관통 돼"): the body as one round column missed what a piece has out in front of it
        // (the knight's neck and muzzle) and the blade as a straight line missed the kukri's bend. A piece model's body is
        // now its own shape - how far out it reaches, per height and per direction round its hips bone, from its mesh -
        // and the kukri's blade is followed along its bend.
        float[,] bodyProfile;
        Transform profileBone;
        float profileLo;
        const float ProfileStep = .03f;
        const int ProfileSectors = 32;
        // The kukri's centre line off its straight line, toward its edge (model -X), every 5 cm up the model (its tip 16 cm
        // over); the other swords are straight to under 1 cm.
        static readonly float[] KukriBend = { .004f, .006f, .002f, -.003f, -.003f, -.018f, -.021f, -.028f, -.036f, -.046f,
            -.057f, -.068f, -.083f, -.100f, -.121f, -.139f, -.158f, -.17f };
        float modelLift, modelScale = 1f;

        int InsideProfile(Vector3 grip, Vector3 d)
        {
            Vector3 up = Mathf.Abs(Vector3.Dot(bladeUp.normalized, d)) > .98f ? (sword != null ? sword.up : Vector3.up) : bladeUp;
            Vector3 side = Quaternion.LookRotation(d, up) * Vector3.right;
            bool kukri = Pawn.Piece == PieceKind.Knight && swordModel != null;
            int n = 0;
            for (int i = -6; i <= 8; i++)
            {
                if (i == 0) continue;
                float along = i > 0 ? .12f + .76f * i / 8f : hiltBack * i / 6f;
                Vector3 p = grip + d * along;
                if (kukri && i > 0)
                {
                    float y = Mathf.Clamp((along - modelLift) / modelScale / .05f, 0f, KukriBend.Length - 1.001f);
                    int k = (int)y;
                    p += side * Mathf.Lerp(KukriBend[k], KukriBend[k + 1], y - k) * modelScale;
                }
                // The blade's own half width; the pommel may just touch the body's side (hidden by the hand).
                if (InBody(p, i > 0 ? .03f : -.03f)) n++;
            }
            return n;
        }

        bool InBody(Vector3 p, float margin)
        {
            Vector3 q = profileBone.InverseTransformPoint(p);
            int h = Mathf.FloorToInt((q.y - profileLo) / ProfileStep);
            if (h < 0 || h >= bodyProfile.GetLength(0)) return false;
            float r = new Vector2(q.x, q.z).magnitude;
            int a = (int)((Mathf.Atan2(q.z, q.x) + Mathf.PI) / (2f * Mathf.PI) * ProfileSectors) % ProfileSectors;
            float reach = 0f;
            for (int dh = -1; dh <= 1; dh++)
                for (int da = -1; da <= 1; da++)
                {
                    int hh = h + dh;
                    if (hh < 0 || hh >= bodyProfile.GetLength(0)) continue;
                    reach = Mathf.Max(reach, bodyProfile[hh, (a + da + ProfileSectors) % ProfileSectors]);
                }
            return reach > 0f && r < reach + margin;
        }

        /// <summary>How far the piece's body (the vertices mostly on its hips, spine and head bones) reaches out from its hips
        /// bone, per height step and direction sector, in that bone's space.</summary>
        static float[,] BodyProfile(SkinnedMeshRenderer skin, int hips, out float lo)
        {
            lo = 0f;
            var mesh = skin.sharedMesh;
            if (mesh == null || !mesh.isReadable || hips >= mesh.bindposes.Length) return null;
            var body = new bool[skin.bones.Length];
            for (int b = 0; b < skin.bones.Length; b++)
            {
                string name = skin.bones[b] != null ? skin.bones[b].name : "";
                body[b] = name.EndsWith("Hips") || name.EndsWith("Spine") || name.EndsWith("Spine1") || name.EndsWith("Spine2")
                    || name.EndsWith("Neck") || name.EndsWith("Head");
            }
            var weights = mesh.boneWeights;
            var vertices = mesh.vertices;
            Matrix4x4 toHips = mesh.bindposes[hips];
            var points = new List<Vector3>();
            float top = float.MinValue;
            lo = float.MaxValue;
            for (int i = 0; i < vertices.Length && i < weights.Length; i++)
            {
                int b = weights[i].boneIndex0;
                if (b < 0 || b >= body.Length || !body[b] || weights[i].weight0 < .5f) continue;
                Vector3 q = toHips.MultiplyPoint3x4(vertices[i]);
                points.Add(q);
                lo = Mathf.Min(lo, q.y); top = Mathf.Max(top, q.y);
            }
            if (points.Count == 0) return null;
            var profile = new float[Mathf.FloorToInt((top - lo) / ProfileStep) + 1, ProfileSectors];
            foreach (var q in points)
            {
                int h = Mathf.FloorToInt((q.y - lo) / ProfileStep);
                int a = (int)((Mathf.Atan2(q.z, q.x) + Mathf.PI) / (2f * Mathf.PI) * ProfileSectors) % ProfileSectors;
                profile[h, a] = Mathf.Max(profile[h, a], new Vector2(q.x, q.z).magnitude);
            }
            return profile;
        }

        /// <summary>Where the sword is held: the hand, or - when the hand is buried in a wide piece's drawn body (the rook's
        /// short arms end inside its tower) - the point of the body's surface beside it, so neither the blade nor the long
        /// hilt has to pass through the body.</summary>
        Vector3 ClickGrip()
        {
            // A piece model's own drawn hand (its rig's right hand bone, outside the body where the mitt is), else the
            // physical hand - pushed to the body's surface if the body is drawn round it.
            BodyColumn(out _, out _, out _, out _);
            if (visualHand != null) return visualHand.TransformPoint(handMiddle);
            Vector3 hand = Hand.position;
            if (!BodyColumn(out Vector3 axis, out float radius, out float lo, out float hi) || hand.y < lo || hand.y > hi) return hand;
            Vector3 off = hand - axis; off.y = 0;
            float d = off.magnitude;
            if (d >= radius) return hand;
            Vector3 outward = d > 1e-3f ? off / d : Vector3.Cross(Vector3.up, Pawn.Facing).normalized;
            return new Vector3(axis.x, hand.y, axis.z) + outward * (radius + .02f);
        }

        bool BodyColumn(out Vector3 axis, out float radius, out float lo, out float hi)
        {
            axis = Pawn.Hips.position; radius = 0; lo = hi = 0;
            if ((int)Pawn.Piece != bodyLookPiece || bodyLook.Exists(r => r == null))
            {
                bodyLookPiece = (int)Pawn.Piece;
                bodyLook.Clear();
                visualHand = null;
                bodyProfile = null; profileBone = null;
                // The piece's skinned body (Visual), else the parts drawn on the hips, chest and head.
                var visual = transform.Find("Visual");
                if (visual != null)
                    foreach (var r in visual.GetComponentsInChildren<Renderer>())
                    {
                        bodyLook.Add(r);
                        if (r is SkinnedMeshRenderer skin && visualHand == null)
                            for (int k = 0; k < skin.bones.Length; k++)
                                if (skin.bones[k] != null && skin.bones[k].name.EndsWith("RightHand"))
                                {
                                    visualHand = skin.bones[k];
                                    handMiddle = HandMiddle(skin, k);
                                    int hips = System.Array.FindIndex(skin.bones, x => x != null && x.name.EndsWith("Hips"));
                                    if (hips >= 0)
                                    {
                                        bodyProfile = BodyProfile(skin, hips, out profileLo);
                                        profileBone = bodyProfile != null ? skin.bones[hips] : null;
                                    }
                                    break;
                                }
                    }
                if (bodyLook.Count == 0)
                {
                    var parts = new[] { Pawn.bodies[(int)BodyId.Hips].transform, Pawn.bodies[(int)BodyId.Chest].transform, Pawn.bodies[(int)BodyId.Head].transform };
                    foreach (var r in Pawn.GetComponentsInChildren<Renderer>())
                    {
                        if (sword != null && r.transform.IsChildOf(sword)) continue;
                        foreach (var t in parts)
                            if (r.transform == t || r.transform.IsChildOf(t)) { bodyLook.Add(r); break; }
                    }
                }
            }
            bool any = false;
            Bounds b = default;
            foreach (var r in bodyLook)
            {
                if (!r.enabled) continue;
                if (!any) { b = r.bounds; any = true; } else b.Encapsulate(r.bounds);
            }
            if (!any) return false;
            axis = b.center;
            radius = .5f * Mathf.Min(b.size.x, b.size.z);
            lo = b.min.y; hi = b.max.y;
            return true;
        }
        Quaternion? ClickSwordPose(int part)
        {
            if (!Alive || Pawn.State != PawnState.Active) return null;
            if (part == (int)BodyId.Chest && Attacking)
            {
                float turn = Mathf.Clamp(Vector3.SignedAngle(Pawn.Facing, swingDirection, Vector3.up), -65, 65);
                float sweep = Mathf.Sin(Mathf.Clamp01((SwingAge - style.Windup) / style.Active) * Mathf.PI);
                // The chest winds up its own way and turns through with the cut, leaning into it.
                float twist, back;
                if (SwingAge < style.Windup)
                {
                    float w = Ease(SwingAge / style.Windup);
                    twist = style.TwistUp * w; back = style.LeanBack * w;
                }
                else if (SwingAge < style.Windup + style.Active)
                {
                    float k = (SwingAge - style.Windup) / style.Active;
                    twist = Mathf.Lerp(style.TwistUp, style.TwistThrough, k); back = style.LeanBack * (1f - k);
                }
                else
                {
                    float r = Ease((SwingAge - style.Windup - style.Active) / style.Recovery);
                    twist = style.TwistThrough * (1f - r); back = 0f;
                }
                return Quaternion.Euler(style.LeanIn * sweep + back, turn * .65f + twist, -6f * sweep);
            }
            if (part != (int)BodyId.ArmR) return null;
            return Quaternion.FromToRotation(Vector3.right, Pawn.bodies[(int)BodyId.Chest].transform.InverseTransformDirection(ClickArmDirection()));
        }

        /// <summary>Where the sword arm points: along the blade, except through a thrust's windup and drive (ArmDrawn).</summary>
        Vector3 ClickArmDirection()
        {
            Vector3 blade = ClickBladeDirection();
            if (!Alive || Pawn.State != PawnState.Active) return blade;
            // 10-25 (승규 님: "이 모션이랑 팔 각도를 자연스럽게"): since a piece's sword arm is drawn all the way with its body
            // (RagdollPawn.RightArmFree), at rest it hangs forward and a little out, the sword held up from the wrist, not
            // the stub aimed along the blade; a cut takes it from there and gives it back.
            Vector3 guard = KeepArmOut(Pawn.Facing * .75f + Vector3.Cross(Vector3.up, Pawn.Facing) * .3f - Vector3.up * .55f);
            if (!Attacking) return guard;
            float end = style.Windup + style.Active;
            if (SwingAge >= end)
            {
                float r = (SwingAge - end) / style.Recovery;
                // A draw cut's arm stays pulled into the first third of the recovery (the arm takes that long to get
                // there), then comes back.
                Vector3 after = style.ArmPull == Vector3.zero ? blade
                    : Vector3.Slerp(SwingWorld(style.ArmPull), blade, Ease((r - .35f) / .65f));
                return KeepArmOut(Vector3.Slerp(after, guard, Ease(r)));
            }
            Vector3 drawn = style.ArmDrawn == Vector3.zero ? blade : SwingWorld(style.ArmDrawn);
            if (SwingAge < style.Windup) return KeepArmOut(Vector3.Slerp(guard, drawn, Ease(SwingAge / style.Windup)));
            if (style.ArmDrawn == Vector3.zero) return KeepArmOut(blade);
            // Out to full reach in the first 60% of the cut, the blade's line from then on - or, for a draw cut, pulled
            // back toward the body after it.
            float k = (SwingAge - style.Windup) / style.Active;
            Vector3 reach = Vector3.Slerp(drawn, blade, Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(k / .6f)));
            if (style.ArmPull == Vector3.zero || k <= .6f) return KeepArmOut(reach);
            return KeepArmOut(Vector3.Slerp(reach, SwingWorld(style.ArmPull), Ease((k - .6f) / .4f)));
        }

        /// <summary>The sword arm kept from pointing in across its own body (the stub would go into it): at most a little
        /// past straight ahead, measured on the body as it is turned (BodyTurn).</summary>
        Vector3 KeepArmOut(Vector3 dir)
        {
            Vector3 side = Quaternion.AngleAxis(Pawn.BodyTurn, Vector3.up) * Vector3.Cross(Vector3.up, Pawn.Facing);
            float across = Vector3.Dot(dir.normalized, side);
            if (across < -.3f) dir = dir.normalized + side * (-.3f - across);
            // 10-25 (승규 님: "나이트가 몸에 관통 돼"): a cut pulled in across the front took the hand and the guard into a
            // piece that has more of itself out in front (the knight's chest); turning the blade cannot move them, so the
            // arm is turned out round the body until they are clear (eased, like the blade's own turn).
            if (armOutFrame != Time.frameCount)
            {
                armOutFrame = Time.frameCount;
                float rate = 0f, dt = Mathf.Max(Time.deltaTime, 1e-4f);
                if (bodyProfile != null && sword != null)
                {
                    Vector3 grip = ClickGrip(), guardAt = sword.position + sword.forward * (modelLift + .09f * modelScale);
                    Vector3 quillon = sword.right * .1f * modelScale;
                    bool Touches(float extra) => InBody(grip, .02f + extra) || InBody(guardAt, .01f + extra)
                        || InBody(guardAt + quillon, .01f + extra) || InBody(guardAt - quillon, .01f + extra);
                    // Out while they touch, back in only once well clear (so it does not rock at the edge).
                    if (Touches(0f)) rate = 10f;
                    else if (!Touches(.04f)) rate = -3f;
                }
                else rate = -3f;
                armOut = Mathf.Clamp(armOut + rate * dt, 0f, 1.2f);
            }
            // Out toward the sword side as far as needed (a push, not a turn about up: an arm pointing down turned about
            // up does not move its hand).
            return (dir.normalized + side * armOut).normalized;
        }
        void PoseClickSword()
        {
            Vector3 dir = ClickBladeDirection(), up = BladeUp(dir);
            // An upright blade has no "up" of its own to roll by: it keeps its last roll.
            if (Mathf.Abs(Vector3.Dot(up.normalized, dir)) > .98f) up = sword.up;
            sword.SetPositionAndRotation(ClickGrip(), Quaternion.LookRotation(dir, up));
        }
    }
}
