using System;
using ChessFight.Network;
using UnityEngine;

namespace ChessFight.RagdollLab
{
    /// <summary>
    /// The Sword Fight skills online (R121, the skill test scene only; Docs/Network/SKILLS_ONLINE.md §15). The host runs
    /// every fighter's F and E skills as offline. With every snapshot it sends what the effects, the sounds and the key
    /// cards read (SwordWire, <see cref="CaptureNet"/>), and a client writes that into its copy of each piece
    /// (<see cref="ApplyNet"/>), so SwordFightSkillFx and SwordFightSkillSfx draw and play the same things on every screen
    /// without knowing about the network. The moments (casts, hits, landings) come numbered in the skills packet and are
    /// raised on a client in step with the bodies (<see cref="RaiseNet"/>).
    /// As in the other two modes the state is written when it arrives (D-S4: a warning as early as it can be), and the
    /// player's own aim is drawn from this PC's camera and its own press at once (D-S3, <see cref="NetViewAim"/>,
    /// <see cref="PredictPress"/>); the host's word takes over when it comes.
    /// </summary>
    public partial class SwordFightSkills
    {
        /// <summary>A client's copy: the host runs this piece, this PC only draws what it sends (its own step is off).</summary>
        public bool NetPuppet { get; set; }
        /// <summary>This client's own piece: its aim follows this PC's camera instead of the host's word.</summary>
        public bool NetOwn { get; set; }

        // The detail lines the key cards and the panel show, as codes on the wire (the texts set in the two skill files).
        static readonly string[] DetailCodes =
        {
            "", "조준 (좌클릭 발동 · 우클릭 취소)", "예고", "예고 · 고정", "예고 · 적 조준", "도약", "받아내기", "반격", "빈틈", "후딜", "돌진", "충격파", "손",
            "막힘",   // R130: the queen's thrust stopped by a king's guard (appended: the codes before it stay)
        };
        static readonly string[] EdgeDetailCodes =
        {
            "", "칼 박기", "매달림", "귀환", "착지", "금색 선", "막힘", "헛벰", "후딜", "내려치기", "손", "웅크림", "공중제비", "뒷발차기",
        };
        string[] whyTexts;

        // The host's last word on the F skill, under a guess of this client's own press (D-S3) with its own clock.
        SfStage netStage;
        float netStageTime;
        string netDetail = "", predictDetail = "";
        SfStage predictStage;
        float predictLeft, predictTime;
        // A pin or a slow comes with the bishop's moment on a client (so the hands fly from it); the state only cuts it short.
        float pinSetAt = -10f, slowSetAt = -10f;

        /// <summary>The host's state of this piece's F skill (not this client's guess over it).</summary>
        public SfStage HostStage => NetPuppet ? netStage : Stage;

        /// <summary>A moment a client was sent, raised for the effects and the sounds as if it had happened here.</summary>
        public static void RaiseNet(SfFxEvent e) => Fx?.Invoke(e);

        // ---------------------------------------------------------------- host: what the others need

        public void CaptureNet(ref SwordWireFighter w, Func<RagdollPawn, byte> refOf)
        {
            w.EdgeCondition = EdgeCondition;
            w.Why = Code(WhyTexts(), EdgeWhy);
            w.EdgeTarget = EdgeTarget != null ? refOf(EdgeTarget) : SkillWire.None;

            w.Skill = Stage != SfStage.None || cooldownLeft > 0f || slowLeft > 0f || pinLeft > 0f;
            if (w.Skill)
            {
                bool aim = Stage == SfStage.Aim;
                Vector3 d = aim ? AimDir : Dir, p = aim ? AimPoint : Point;
                w.Stage = (byte)Stage;
                w.Detail = Code(DetailCodes, Detail);
                w.SkillFlags = (byte)((WaveBlocked ? 1 : 0) | (KingCountered ? 2 : 0));
                w.StageTime = StageTime;
                w.Cooldown = Mathf.Max(0f, cooldownLeft);
                w.CooldownTotal = CooldownTotal;
                w.Dx = d.x; w.Dy = d.y; w.Dz = d.z;
                w.Px = p.x; w.Py = p.y; w.Pz = p.z;
                w.Ox = Origin.x; w.Oy = Origin.y; w.Oz = Origin.z;
                w.Reach = aim ? AimReach : Reach;
                w.Wave = WaveFront;
                w.Dash = DashProgress;
                w.PinCandidate = PinCandidate != null ? refOf(PinCandidate) : SkillWire.None;
                w.Slow = Mathf.Max(0f, slowLeft);
                w.Pin = Mathf.Max(0f, pinLeft);
            }

            w.Edge = EdgeStage != SfEdge.None || edgeCooldownLeft > 0f;
            if (w.Edge)
            {
                w.EdgeStage = (byte)EdgeStage;
                w.EdgeDetail = Code(EdgeDetailCodes, EdgeDetail);
                w.EdgeFlags = (byte)((held ? 1 : 0) | (vaulted ? 2 : 0));
                w.EdgeTime = EdgeTime;
                w.EdgeCooldown = Mathf.Max(0f, edgeCooldownLeft);
                w.EdgeCooldownTotal = EdgeCooldownTotal;
                w.Carried = carried != null ? refOf(carried) : SkillWire.None;
                w.Ax = EdgeAt.x; w.Ay = EdgeAt.y; w.Az = EdgeAt.z;
                w.Outx = EdgeOut.x; w.Outy = EdgeOut.y; w.Outz = EdgeOut.z;
                w.Lx = EdgeLand.x; w.Ly = EdgeLand.y; w.Lz = EdgeLand.z;
                w.Sx = EdgeStart.x; w.Sy = EdgeStart.y; w.Sz = EdgeStart.z;
            }

            w.Glow = HasEdgeGlow;
            if (w.Glow)
            {
                var g = EdgeGlow;
                w.Gx = g.point.x; w.Gy = g.point.y; w.Gz = g.point.z;
                w.GOx = g.outward.x; w.GOy = g.outward.y; w.GOz = g.outward.z;
                w.GAx = g.along.x; w.GAy = g.along.y; w.GAz = g.along.z;
                w.GAt = g.at;
                w.GTo = g.to;
            }
        }

        // ---------------------------------------------------------------- client: the copy's fields

        /// <summary>The host's state for this piece arrived (a client's copy). <paramref name="pawnOf"/> turns a roster
        /// place back into a piece.</summary>
        public void ApplyNet(in SwordWireFighter w, Func<byte, RagdollPawn> pawnOf)
        {
            // The F skill.
            var stage = w.Skill ? (SfStage)w.Stage : SfStage.None;
            netStage = stage;
            netStageTime = w.Skill ? w.StageTime : 0f;
            Stage = stage;
            StageTime = netStageTime;
            cooldownLeft = w.Skill ? w.Cooldown : 0f;
            if (w.Skill) CooldownTotal = w.CooldownTotal;
            netDetail = w.Skill ? Text(DetailCodes, w.Detail) : "";
            Detail = netDetail;
            WaveBlocked = w.Skill && (w.SkillFlags & 1) != 0;
            KingCountered = w.Skill && (w.SkillFlags & 2) != 0;
            if (w.Skill)
            {
                var d = new Vector3(w.Dx, w.Dy, w.Dz);
                var p = new Vector3(w.Px, w.Py, w.Pz);
                if (stage == SfStage.Aim)
                {
                    // This client's own aim is its camera's (NetViewAim), not the host's of a round trip ago.
                    if (!NetOwn)
                    {
                        if (d.sqrMagnitude > 0.5f) AimDir = d;
                        AimPoint = p;
                        AimReach = w.Reach;
                        Origin = new Vector3(w.Ox, w.Oy, w.Oz);
                        PinCandidate = pawnOf(w.PinCandidate);
                    }
                }
                else
                {
                    if (d.sqrMagnitude > 0.5f) Dir = d;
                    Point = p;
                    Reach = w.Reach;
                    Origin = new Vector3(w.Ox, w.Oy, w.Oz);
                    PinCandidate = pawnOf(w.PinCandidate);
                }
                WaveFront = w.Wave;
                DashProgress = w.Dash;
            }
            else
            {
                WaveFront = DashProgress = 0f;
                if (!NetOwn) PinCandidate = null;
            }
            if (Piece == PieceKind.Knight && ((stage == SfStage.Aim && !NetOwn) || stage == SfStage.Active))
                KnightSquaresAt(stage == SfStage.Aim ? AimPoint : Point, stage == SfStage.Aim ? AimDir : Dir);
            float pin = w.Skill ? w.Pin : 0f, slow = w.Skill ? w.Slow : 0f;
            if (pin <= 0f && pinLeft > 0f && Time.unscaledTime - pinSetAt > 0.3f) pinLeft = 0f;
            if (slow <= 0f && slowLeft > 0f && Time.unscaledTime - slowSetAt > 0.3f) slowLeft = 0f;

            // The E skill.
            EdgeStage = w.Edge ? (SfEdge)w.EdgeStage : SfEdge.None;
            EdgeTime = w.Edge ? w.EdgeTime : 0f;
            edgeCooldownLeft = w.Edge ? w.EdgeCooldown : 0f;
            if (w.Edge) EdgeCooldownTotal = w.EdgeCooldownTotal;
            EdgeDetail = w.Edge ? Text(EdgeDetailCodes, w.EdgeDetail) : "";
            held = w.Edge && (w.EdgeFlags & 1) != 0;
            vaulted = w.Edge && (w.EdgeFlags & 2) != 0;
            carried = w.Edge ? pawnOf(w.Carried) : null;
            if (w.Edge)
            {
                EdgeAt = new Vector3(w.Ax, w.Ay, w.Az);
                var o = new Vector3(w.Outx, w.Outy, w.Outz);
                if (o.sqrMagnitude > 0.5f) EdgeOut = o;
                EdgeLand = new Vector3(w.Lx, w.Ly, w.Lz);
                EdgeStart = new Vector3(w.Sx, w.Sy, w.Sz);
            }
            EdgeCondition = w.EdgeCondition;
            EdgeWhy = Text(WhyTexts(), w.Why);
            EdgeTarget = pawnOf(w.EdgeTarget);
            HasEdgeGlow = w.Glow;
            if (w.Glow)
            {
                var point = new Vector3(w.Gx, w.Gy, w.Gz);
                var along = new Vector3(w.GAx, w.GAy, w.GAz);
                EdgeGlow = new EdgeHit
                {
                    point = point, outward = new Vector3(w.GOx, w.GOy, w.GOz), along = along,
                    from = 0f, to = w.GTo, at = w.GAt, start = point - along * w.GAt,
                };
                // The rook's chunk as it would break, from the edge it would use (the same arithmetic as the host's).
                var floor = SwordFightEdgeFloor.Current;
                if (Piece == PieceKind.Rook && EdgeParams != null && floor != null && floor.Find())
                    EdgeChunk = floor.ChunkAt(EdgeGlow, EdgeParams.rookChunkWidth, EdgeParams.rookChunkDepth, out rookLip);
            }
            ApplyPrediction();
        }

        /// <summary>Between two of the host's packets a client runs the timers on itself, so a warning fills smoothly at
        /// the screen's rate rather than in 30 steps a second.</summary>
        public void AdvanceNetView(float dt)
        {
            if (Stage != SfStage.None) StageTime += dt;
            if (netStage != SfStage.None) netStageTime += dt;
            cooldownLeft -= dt;
            if (EdgeStage != SfEdge.None) EdgeTime += dt;
            edgeCooldownLeft -= dt;
            slowLeft -= dt;
            pinLeft -= dt;
            if (predictLeft > 0f)
            {
                predictLeft -= dt;
                predictTime += dt;
                if (predictLeft <= 0f)
                {
                    // The host never took the press (a cooldown, knocked down meanwhile): back to what it says.
                    Stage = netStage;
                    StageTime = netStageTime;
                    Detail = netDetail;
                    RagdollPawn.NetStats.GuessesDropped++;
                }
            }
        }

        /// <summary>This client's own piece: while it may aim, the aim follows this PC's camera at once (D-S3), the same
        /// arithmetic the host runs (its line, its point, the enemy on the bishop's point).</summary>
        public void NetViewAim(Vector3 aim, Camera cam)
        {
            if (aim.sqrMagnitude > 0.01f) rawAim = aim;
            if (cam != null) aimCamera = cam;
            if (!HasSkill || Pawn == null || S == null) return;
            if (Stage == SfStage.Aim) UpdateAim();
        }

        /// <summary>
        /// This client pressed something (D-S3): draw what the host will most likely do with it at once, for at most
        /// <paramref name="hold"/> seconds (about a round trip) until the host's own state says the same.
        /// <paramref name="key"/> 0 = the skill key, 1 = the left click while aiming (go), 2 = the right click (call it off).
        /// The edge skill (E) is not guessed: whether its condition holds is the host's to judge.
        /// </summary>
        public void PredictPress(int key, float hold)
        {
            if (!HasSkill || Pawn == null || S == null || Pawn.State != PawnState.Active || EdgeStage != SfEdge.None) return;
            switch (key)
            {
                case 0:
                    if (Stage == SfStage.None && Cooldown <= 0.05f)
                    {
                        if (Piece == PieceKind.King)
                        {
                            Dir = Flat(Pawn.Facing).normalized;
                            Predict(SfStage.Windup, hold);
                        }
                        else Predict(SfStage.Aim, hold);
                    }
                    else if (Stage == SfStage.Aim) Predict(SfStage.None, hold);
                    break;
                case 1:
                    if (Stage != SfStage.Aim) break;
                    // What Go() does with the aim.
                    Dir = AimDir;
                    Point = AimPoint;
                    bool wall = false;
                    Reach = Piece == PieceKind.Queen ? DashDistance(Origin, Dir, S.queenLength)
                          : Piece == PieceKind.Rook ? RookReach(Origin, Dir, out wall) : AimReach;
                    WaveBlocked = Piece == PieceKind.Rook && wall;
                    if (Piece == PieceKind.Knight) KnightSquaresAt(Point, Dir);
                    Predict(Piece == PieceKind.Knight ? SfStage.Active : SfStage.Windup, hold);
                    break;
                case 2:
                    if (Stage == SfStage.Aim) Predict(SfStage.None, hold);
                    break;
            }
        }

        void Predict(SfStage stage, float hold)
        {
            RagdollPawn.NetStats.Guesses++;
            predictStage = stage;
            predictLeft = Mathf.Max(0.1f, hold);
            predictTime = 0f;
            // The line the host will write for it (SetStage's), so the key card does not show the stage's name meanwhile.
            predictDetail = stage == SfStage.Aim ? DetailCodes[1]
                : stage == SfStage.Active ? "도약"
                : stage != SfStage.Windup ? ""
                : Piece == PieceKind.King ? "받아내기"
                : Piece == PieceKind.Rook ? "예고 · 고정"
                : Piece == PieceKind.Bishop && PinCandidate != null ? "예고 · 적 조준" : "예고";
            Stage = stage;
            StageTime = 0f;
            Detail = predictDetail;
        }

        /// <summary>The host's state has caught up with the guess (the same step or one past it), or the guess holds on.</summary>
        void ApplyPrediction()
        {
            if (predictLeft <= 0f) return;
            bool confirmed = predictStage == SfStage.None
                ? netStage == SfStage.None
                : netStage != SfStage.None && (int)netStage >= (int)predictStage;
            if (confirmed)
            {
                predictLeft = 0f;
                RagdollPawn.NetStats.GuessesMatched++;
                return;
            }
            Stage = predictStage;
            StageTime = predictTime;
            Detail = predictDetail;
        }

        /// <summary>A bishop's hands took this piece (its moment, on a client): held and slowed from now, so the effect
        /// that flies the hands from the bishop starts with the moment instead of the state that came before it.</summary>
        public void NetPinned(float seconds)
        {
            pinLeft = Mathf.Max(pinLeft, seconds);
            pinSetAt = Time.unscaledTime;
        }

        public void NetSlowed(float seconds)
        {
            slowLeft = Mathf.Max(slowLeft, seconds);
            slowSetAt = Time.unscaledTime;
        }

        /// <summary>This PC becomes the host mid-match: what it was drawing becomes what it runs. Cooldowns go on; a skill
        /// caught half way (an aim, a dash, a hang) is called off and its cooldown starts, as if it had ended there.</summary>
        public void AdoptNet()
        {
            NetPuppet = NetOwn = false;
            predictLeft = 0f;
            bool midF = Stage != SfStage.None && Stage != SfStage.Aim, midE = EdgeStage != SfEdge.None;
            float cd = cooldownLeft, total = CooldownTotal, ecd = edgeCooldownLeft, etotal = EdgeCooldownTotal;
            Clear();
            ClearEdge();
            cooldownLeft = Mathf.Max(0f, cd);
            CooldownTotal = total;
            edgeCooldownLeft = Mathf.Max(0f, ecd);
            EdgeCooldownTotal = etotal;
            if (midF && cooldownLeft <= 0f) StartCooldown();
            if (midE && edgeCooldownLeft <= 0f && EdgeParams != null) StartEdgeCooldown(1f);
            FallTime = -1f;
        }

        /// <summary>This PC hands the host role on: whatever it was carrying (a flight, a dash, pieces passing through)
        /// lets go, and from now on it draws what the new host sends.</summary>
        public void YieldNet()
        {
            if (Pawn != null)
            {
                if (Pawn.OuterFlying) Pawn.EndFlight();
                Pawn.StopDash();
            }
            PassThrough(false);
            Clearance(null, false);
            if (carried != null && carried.OuterFlying) carried.EndFlight();
            NetPuppet = true;
        }

        // ---------------------------------------------------------------- the codes

        /// <summary>Why the edge skill cannot be used, every line <see cref="CheckEdge"/> writes (with this build's numbers).</summary>
        string[] WhyTexts()
        {
            if (whyTexts != null) return whyTexts;
            var X = EdgeParams ?? new SwordFightEdgeParams();
            whyTexts = new[]
            {
                "",
                "발판을 못 찾음",
                "밀려 떨어지는 중에만",
                "너무 늦음",
                "가장자리가 멂",
                $"가장자리 구역({X.edgeZone:0.#} m)에 있는 적이 {X.queenRange:0.#} m 안에 없음",
                $"가장자리 구역({X.edgeZone:0.#} m)에 서야 함",
                "그 자리는 이미 무너지는 중",
                $"{X.bishopRange:0.#} m 안에 떨어지는 아군이 없음",
                $"가장자리를 등지고({X.knightBackEdge:0.#} m 안) 서야 함",
                "떨어진 지 너무 오래됨",
                "넘어져 있음",
                "발판 위에서만",
                $"앞 {X.knightFront:0.#} m 안에 적이 없음",
                "폰은 스킬이 없어요",
            };
            return whyTexts;
        }

        static byte Code(string[] table, string text)
        {
            int i = Array.IndexOf(table, text ?? "");
            return i < 0 ? (byte)255 : (byte)i;
        }

        static string Text(string[] table, byte code) => code < table.Length ? table[code] : "";
    }
}
