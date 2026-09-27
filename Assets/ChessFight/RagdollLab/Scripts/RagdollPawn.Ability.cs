using System.Collections.Generic;
using ChessFight.Gameplay;
using ChessFight.Network;
using UnityEngine;

namespace ChessFight.RagdollLab
{
    /// <summary>
    /// The pieces' abilities (Queen of the Hill M12, DESIGN §6.2; the numbers are PieceAbilities in Core).
    /// E is a piece's main ability - the pawn keeps its grappling hook on E - and Q the king's castling.
    ///
    ///   Knight  E  the L-shaped leap, drawn as the letter: 6.6 m straight up, 3 m on along the aim, then it
    ///              lets go and comes down - over a wall or onto a ledge right in front of it. Its landing spot
    ///              is shown to everyone from the start. Passive: coming down on an enemy's head squashes it
    ///              (M13) and bounces the knight up.
    ///   Rook    E  the straight charge: a warning line for 0.6 s, then 14 m along the nearest of the
    ///              board's four directions at 18 m/s - or 8 m straight up a wall in front of it when aimed
    ///              up, or straight down when aimed down - gravity ignored, through bodies. Enemies it
    ///              passes on the ground or on a wall are knocked down and thrown aside; those in the air
    ///              are missed.
    ///   King    E  "check!": the sceptre up for 0.5 s, then a shockwave that knocks down enemies within
    ///              4 m and throws them out. Q castling: swaps places with its own team's rook. Passive:
    ///              teammates within 6 m are harder to push and get their stamina back faster.
    ///   Bishop  E  the hover: 5 s afloat moving at 3 m/s, up to 3 m higher (Space a metre up, Shift down),
    ///              the left button throwing stones (PieceShard) every 0.7 s, three at once at most. E
    ///              again, a hit or a grab ends it; the cooldown starts when it ends. Space in the air:
    ///              the diagonal glide, 45 degrees down along the way it faces, until it lands or Space
    ///              again.
    ///
    /// The leap's L, the charge, the hover and the glide carry the body along a path the way the hook's
    /// rope does: every bone kinematic and placed around the anchor (PlaceHanging), then physics again
    /// with the path's speed (EndClimbPose). Only the machine that simulates the pawn runs any of it; a
    /// network puppet shows the move and its target off the wire.
    /// </summary>
    public partial class RagdollPawn
    {
        readonly PieceCooldowns cooldowns = new PieceCooldowns();
        readonly ShardTally shardTally = new ShardTally();
        readonly List<RagdollPawn> struck = new List<RagdollPawn>();
        readonly Collider[] pieceOverlap = new Collider[64];
        PieceMove move, netMove;
        float moveTime, moveLeft, hoverBase, hoverHeight, shardWait, auraWait;
        Vector3 moveDir, moveTarget, netMoveTarget;
        int rookVertical, leapPhase;
        bool ghost, inAura, pieceJump, pieceShove, pieceSprint;
        bool shardPending;
        Vector3 shardPush;

        /// <summary>What the piece's ability is doing. Remote pawns read it off the wire.</summary>
        public PieceMove Move => NetworkPuppet ? netMove : move;
        /// <summary>The knight's landing spot, the end of the rook's line, the king's centre; world space.</summary>
        public Vector3 MoveTarget => NetworkPuppet ? netMoveTarget : moveTarget;
        public Vector3 MoveDirection => moveDir;
        public float MoveTime => moveTime;
        /// <summary>Seconds until E (0) or Q (1) can be used again.</summary>
        public float CooldownLeft(int slot) => cooldowns.Left(slot);
        /// <summary>A king of its own team stands within 6 m (the king's grace).</summary>
        public bool InKingsGrace => inAura;
        public int AbilityUses { get; private set; }
        public int Stomps { get; private set; }
        public int RookHits { get; private set; }
        public int CheckHits { get; private set; }
        public int Castles { get; private set; }
        public int ShardsThrown { get; private set; }
        public int ShardHitsTaken { get; private set; }
        public string LastAbilityEvent { get; private set; } = "-";

        /// <summary>The body is being carried along a path (the charge, the hover, the glide): placed, not simulated.</summary>
        bool Carried => move == PieceMove.RookCharge || move == PieceMove.BishopHover || move == PieceMove.BishopGlide
                        || (move == PieceMove.KnightLeap && leapPhase < 2);

        /// <summary>Standing still while it winds up: the rook's line, the king's sceptre.</summary>
        bool WindingUp => move == PieceMove.RookWindup || move == PieceMove.KingCheck;

        /// <summary>Early in the step, before the controls are used: an ability takes the keys it needs.</summary>
        void PieceInputs()
        {
            pieceJump = input.jump;
            pieceShove = input.shove;
            pieceSprint = input.sprint;
            if (!Carried && !WindingUp) return;
            input.jump = false;
            input.shove = false;
            input.sprint = false;
            // Winding up on a wall it keeps its hold; carried, the hands have nothing to do.
            if (Carried) input.grab = false;
            if (WindingUp) input.move = Vector3.zero;
        }

        void UpdatePiece(RagdollParams p, float dt)
        {
            cooldowns.Tick(dt);
            UpdateGrace(dt);
            TakeShard();
            if (piece == PieceKind.Pawn)
            {
                if (move != PieceMove.None) EndMove(false);
                return;
            }
            // Knocked down, in the water, in someone's hands, squashed or staggered: whatever it was doing stops.
            if (move != PieceMove.None && (State != PawnState.Active || Floating || BeingHeld || Squashed || Staggered))
                EndMove(false);
            moveTime += dt;
            switch (move)
            {
                case PieceMove.None:
                    if (input.ability) StartAbility(p, 0);
                    else if (input.ability2) StartAbility(p, 1);
                    else if (piece == PieceKind.Bishop && pieceJump) TryGlide(p);
                    break;
                case PieceMove.KnightLeap:
                    LeapStep(p, dt);
                    break;
                case PieceMove.RookWindup:
                    if (moveTime >= PieceAbilities.RookWindup) BeginCharge(p);
                    break;
                case PieceMove.RookCharge:
                    Charge(p, dt);
                    break;
                case PieceMove.KingCheck:
                    if (moveTime >= PieceAbilities.CheckWindup) Shockwave();
                    break;
                case PieceMove.BishopHover:
                    Hover(p, dt);
                    break;
                case PieceMove.BishopGlide:
                    Glide(p, dt);
                    break;
            }
            Stomp();
            // With an ability going, the left button is its own: no dive.
            if (move != PieceMove.None) input.shove = false;
        }

        bool CanUseAbility => State == PawnState.Active && !Floating && !BeingHeld && !Squashed && !Staggered;

        void StartAbility(RagdollParams p, int slot)
        {
            if (!PieceAbilities.Has(piece, slot)) return;
            if (!cooldowns.Ready(slot))
            {
                LastAbilityEvent = $"{PieceAbilities.Name(piece, slot)}: {cooldowns.Left(slot):0.0}초 남음";
                return;
            }
            if (!CanUseAbility) return;
            switch (piece)
            {
                case PieceKind.Knight: Leap(p); break;
                case PieceKind.Rook: WindUpCharge(p); break;
                case PieceKind.King:
                    if (slot == 0) RaiseSceptre();
                    else Castle();
                    break;
                case PieceKind.Bishop: StartHover(p); break;
            }
        }

        /// <summary>Stop whatever the ability was doing. Carried: back to physics now, with the path's speed.</summary>
        void EndMove(bool keepCooldown)
        {
            if (move == PieceMove.None) return;
            bool wasHover = move == PieceMove.BishopHover;
            move = PieceMove.None;
            moveTime = 0f;
            SetGhost(false);
            if (wasHover && !keepCooldown) cooldowns.Start(0, PieceAbilities.HoverCooldown);
            if (climbKinematic && !Climbing && hookPhase != HookPhase.Pulling && rope == null) EndClimbPose();
        }

        /// <summary>Promoted, demoted or teleported: nothing carries over, the cooldowns start afresh.</summary>
        void ResetPiece()
        {
            EndMove(true);
            cooldowns.Clear();
            struck.Clear();
            inAura = false;
        }

        // ---------------------------------------------------------------- knight

        void Leap(RagdollParams p)
        {
            Vector3 hips = bodies[0].position;
            Vector3 on = FlatDir(input.aim.sqrMagnitude > 0.25f ? input.aim : facing, facing);
            float up = PieceAbilities.KnightUp + PieceAbilities.KnightLift;
            // Where it comes down: the floor under the end of the L (or as far below as the leap rose).
            Vector3 end = hips + Vector3.up * up + on * PieceAbilities.KnightOn;
            moveTarget = SolidRay(end, Vector3.down, up + 3f, out var ground) && ground.normal.y > 0.5f
                ? ground.point : end - Vector3.up * (up + standHeight);
            LetGo(0.3f);
            ReleaseHolders(0.5f);
            facing = on;
            moveDir = on;
            move = PieceMove.KnightLeap;
            moveTime = 0f;
            leapPhase = 0;
            moveLeft = up;
            anchorPos = hips;
            anchorVel = Vector3.zero;
            cooldowns.Start(0, PieceAbilities.KnightCooldown);
            AbilityUses++;
            LastAbilityEvent = $"L자 도약: 착지 {moveTarget.y - (hips.y - standHeight):+0.0;-0.0} m";
        }

        /// <summary>The L: straight up, then along the aim, then it lets go and falls onto the spot.</summary>
        void LeapStep(RagdollParams p, float dt)
        {
            if (leapPhase >= 2)
            {
                if ((Grounded && moveTime > 0.2f) || Climbing || rope != null || moveTime > 5f) EndMove(true);
                return;
            }
            Vector3 delta;
            if (leapPhase == 0)
            {
                float step = Mathf.Min(moveLeft, PieceAbilities.KnightUpSpeed * dt);
                if (RopeBodyCast(anchorPos, Vector3.up, step, out var roof))
                {
                    // A ceiling: the L turns under it.
                    step = Mathf.Max(0f, roof.distance - 0.02f);
                    moveLeft = step;
                }
                delta = Vector3.up * step;
                moveLeft -= step;
                if (moveLeft <= 1e-3f)
                {
                    leapPhase = 1;
                    moveLeft = PieceAbilities.KnightOn;
                }
            }
            else
            {
                float step = Mathf.Min(moveLeft, PieceAbilities.KnightOnSpeed * dt);
                delta = CarrySlide(moveDir * step);
                moveLeft -= step;
                // Against a wall higher than the leap it gets no further: let go there.
                if (delta.magnitude < step * 0.3f) moveLeft = 0f;
            }
            anchorVel = delta / Mathf.Max(dt, 1e-4f);
            anchorPos += delta;
            anchor.MovePosition(anchorPos);
            anchor.MoveRotation(Quaternion.LookRotation(facing, Vector3.up));
            if (leapPhase == 1 && moveLeft <= 1e-3f)
            {
                // Let go: physics again, a little speed on, down onto the spot.
                leapPhase = 2;
                moveTime = 0f;
                anchorVel = moveDir * PieceAbilities.KnightLetGo;
                wallPoint = anchorPos;
                wallNormal = Vector3.up;
                EndClimbPose();
            }
        }

        /// <summary>The knight's passive: coming down on an enemy's head or shoulders squashes it.</summary>
        void Stomp()
        {
            if (piece != PieceKind.Knight || State != PawnState.Active || Grounded || Climbing || Carried || rope != null) return;
            if (bodies[0].linearVelocity.y > -1f) return;
            Vector3 feet = bodies[0].position - Vector3.up * standHeight;
            int n = Physics.OverlapSphereNonAlloc(feet, 0.35f, pieceOverlap, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < n; i++)
            {
                if (!ColliderOwner.TryGetValue(pieceOverlap[i], out var other) || other == this || other.NetworkPuppet) continue;
                if (!Teams.AreEnemies(Team, other.Team)) continue;
                var rb = pieceOverlap[i].attachedRigidbody;
                if (rb != other.bodies[(int)BodyId.Head] && rb != other.bodies[(int)BodyId.Chest]) continue;
                // On top of it, not beside it.
                if (other.bodies[(int)BodyId.Head].position.y > feet.y + 0.15f) continue;
                if (!other.Squash(PieceAbilities.StompSquash, PieceAbilities.StompImmunity)) continue;
                Stomps++;
                LastAbilityEvent = $"밟기: {other.DisplayName} 찌그러짐 {PieceAbilities.StompSquash:0.0}초";
                foreach (var body in bodies)
                    body.linearVelocity = new Vector3(body.linearVelocity.x, PieceAbilities.StompBounce, body.linearVelocity.z);
                freeFlight = Mathf.Max(freeFlight, 0.4f);
                jumpTimer = Mathf.Max(jumpTimer, 0.3f);
                return;
            }
        }

        // ---------------------------------------------------------------- rook

        void WindUpCharge(RagdollParams p)
        {
            Vector3 aim = input.aim.sqrMagnitude > 0.25f ? input.aim.normalized : facing;
            Vector3 chest = bodies[(int)BodyId.Chest].position;
            bool wall = WallRay(p, chest, facing, ClimbHug(p) + 0.9f, out var face)
                        || (Climbing && WallRay(p, chest, -wallNormal, ClimbHug(p) + 0.9f, out face));
            rookVertical = PieceAbilities.RookVertical(aim.y, wall);
            // Straight down with a floor right under it is no charge at all: along the board instead.
            if (rookVertical < 0 && SolidRay(bodies[0].position, Vector3.down, standHeight + 1f, out _)) rookVertical = 0;
            if (rookVertical > 0)
            {
                moveDir = Vector3.up;
                wallPoint = face.point;
                wallNormal = face.normal;
                Vector3 into = Flat(-face.normal);
                if (into.sqrMagnitude > 1e-4f) facing = into.normalized;
            }
            else if (rookVertical < 0) moveDir = Vector3.down;
            else
            {
                PieceAbilities.RookAxis(aim.x, aim.z, out float ax, out float az);
                moveDir = new Vector3(ax, 0f, az);
                facing = moveDir;
            }
            moveLeft = PieceAbilities.RookLength(rookVertical);
            moveTarget = bodies[0].position + moveDir * RookReachable(moveLeft);
            move = PieceMove.RookWindup;
            moveTime = 0f;
            cooldowns.Start(0, PieceAbilities.RookCooldown);
            AbilityUses++;
            LastAbilityEvent = rookVertical > 0 ? "직선 돌진: 벽 위로" : rookVertical < 0 ? "직선 돌진: 아래로" : "직선 돌진";
        }

        /// <summary>How far the line runs before something stops it: the warning shows that much.</summary>
        float RookReachable(float length)
        {
            if (rookVertical > 0) return length;
            if (rookVertical < 0)
                return SolidRay(bodies[0].position, Vector3.down, length + standHeight, out var floor)
                    ? Mathf.Max(0f, floor.distance - standHeight) : length;
            return RopeBodyCast(bodies[0].position, moveDir, length, out var hit) && hit.normal.y <= 0.6f
                ? Mathf.Max(0f, hit.distance - 0.05f) : length;
        }

        void BeginCharge(RagdollParams p)
        {
            LetGo(0.3f);
            ReleaseHolders(0.5f);
            SetGhost(true);
            struck.Clear();
            anchorPos = bodies[0].position;
            anchorVel = Vector3.zero;
            move = PieceMove.RookCharge;
            moveTime = 0f;
        }

        void Charge(RagdollParams p, float dt)
        {
            float step = Mathf.Min(moveLeft, PieceAbilities.RookSpeed * dt);
            Vector3 from = anchorPos;
            Vector3 delta = moveDir * step;
            bool stop = false, topped = false;
            RaycastHit blocked = default;
            if (rookVertical > 0)
            {
                // Up the wall; above its lip, over onto the top and done.
                Vector3 chest = anchorPos + Vector3.up * 0.35f;
                if (!WallRay(p, chest, -wallNormal, ClimbHug(p) + 0.8f, out var face))
                {
                    Vector3 onTop = anchorPos + Flat(-wallNormal).normalized * 0.7f + Vector3.up * 0.25f;
                    if (!InsideSolid(onTop, 0.2f)) delta = onTop - anchorPos;
                    else delta = Vector3.zero;
                    stop = topped = true;
                }
                else
                {
                    wallPoint = face.point;
                    wallNormal = face.normal;
                    if (RopeBodyCast(anchorPos, Vector3.up, step, out blocked))
                    {
                        delta = Vector3.up * Mathf.Max(0f, blocked.distance - 0.05f);
                        stop = true;
                    }
                }
            }
            else if (rookVertical < 0)
            {
                // Down until the feet meet a floor.
                if (SolidRay(anchorPos, Vector3.down, standHeight + step + 0.05f, out var floor))
                {
                    delta = Vector3.up * (floor.point.y + standHeight + 0.02f - anchorPos.y);
                    stop = true;
                }
            }
            else if (RopeBodyCast(anchorPos, moveDir, step, out blocked))
            {
                Vector3 before = moveDir * Mathf.Max(0f, blocked.distance - 0.02f);
                if (blocked.normal.y > 0.6f)
                {
                    // A slope or a step up: along it, still at full speed.
                    Vector3 along = Vector3.ProjectOnPlane(moveDir, blocked.normal).normalized;
                    Vector3 rest = along * (step - before.magnitude);
                    if (RopeBodyCast(anchorPos + before, along, rest.magnitude, out var second))
                        rest = along * Mathf.Max(0f, second.distance - 0.02f);
                    delta = before + rest;
                }
                else if (!RopeBodyCast(anchorPos + Vector3.up * 0.45f, moveDir, step, out _))
                {
                    // A step or a kerb: up it and on.
                    delta = Vector3.up * 0.45f + moveDir * step;
                }
                else
                {
                    // A wall: the charge ends against it.
                    delta = before;
                    stop = true;
                }
            }
            anchorVel = delta / Mathf.Max(dt, 1e-4f);
            anchorPos += delta;
            moveLeft -= step;
            anchor.MovePosition(anchorPos);
            anchor.MoveRotation(Quaternion.LookRotation(facing, Vector3.up));
            Strike(from, anchorPos);
            if (!stop && moveLeft > 1e-3f) return;
            EndCharge(p, stop, topped, blocked);
        }

        void EndCharge(RagdollParams p, bool stopped, bool topped, RaycastHit blocked)
        {
            if (topped)
            {
                anchorVel = facing * 1f;
                wallNormal = Vector3.up;
                wallPoint = anchorPos - Vector3.up * standHeight;
                LastAbilityEvent = "직선 돌진: 벽을 올라 위에 섬";
            }
            else if (rookVertical < 0)
            {
                anchorVel = Vector3.zero;
                wallNormal = Vector3.up;
                wallPoint = anchorPos - Vector3.up * standHeight;
            }
            else if (stopped)
            {
                anchorVel = Vector3.zero;
                if (blocked.collider != null)
                {
                    wallPoint = blocked.point;
                    wallNormal = blocked.normal;
                }
            }
            else
            {
                // Out of distance in the open: it runs on with some of the speed.
                anchorVel = moveDir * (rookVertical > 0 ? 0f : 6f);
                wallPoint = anchorPos;
                wallNormal = Vector3.up;
            }
            bool onWall = rookVertical > 0 && !topped;
            move = PieceMove.None;
            moveTime = 0f;
            SetGhost(false);
            if (onWall && WallRay(p, anchorPos + Vector3.up * 0.35f, -wallNormal, ClimbHug(p) + 0.8f, out var face))
            {
                // Still on the wall after 8 m: it takes hold (and keeps it while the right button is held).
                StartClimb(p, face);
                return;
            }
            EndClimbPose();
        }

        /// <summary>Enemies on the rook's way since the last step: knocked down and thrown aside, once each.</summary>
        void Strike(Vector3 from, Vector3 to)
        {
            Vector3 top = Vector3.up * headRise;
            int n = Physics.OverlapCapsuleNonAlloc(from, to + top, PieceAbilities.RookReach, pieceOverlap, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < n; i++)
            {
                if (!ColliderOwner.TryGetValue(pieceOverlap[i], out var other) || other == this || other.NetworkPuppet) continue;
                if (struck.Contains(other) || !Teams.AreEnemies(Team, other.Team)) continue;
                // In the air it cannot be hit (DESIGN §6.2: the rook's weakness).
                if (!other.Grounded && !other.Climbing && other.State == PawnState.Active) continue;
                struck.Add(other);
                Vector3 aside;
                if (rookVertical != 0) aside = other.Climbing ? other.wallNormal : Flat(other.bodies[0].position - to);
                else
                {
                    Vector3 side = Vector3.Cross(Vector3.up, moveDir);
                    aside = Vector3.Dot(other.bodies[0].position - to, side) >= 0f ? side : -side;
                }
                aside = FlatDir(aside, Vector3.Cross(Vector3.up, facing));
                other.TakeHit(aside * PieceAbilities.RookAside + Vector3.up * PieceAbilities.RookLift, PieceAbilities.RookKnockdown, 0f, true);
                RookHits++;
                LastAbilityEvent = $"직선 돌진: {other.DisplayName} 넘어뜨림";
            }
        }

        /// <summary>Through bodies: none of this pawn's colliders touch any other pawn's while it charges.</summary>
        void SetGhost(bool on)
        {
            if (ghost == on || ownColliders == null) return;
            ghost = on;
            foreach (var other in All)
            {
                if (other == null || other == this || other.ownColliders == null) continue;
                foreach (var a in ownColliders)
                    foreach (var b in other.ownColliders)
                        if (a != null && b != null) Physics.IgnoreCollision(a, b, on);
            }
        }

        /// <summary>This pawn's own colliders (PieceShard keeps clear of its thrower).</summary>
        internal Collider[] Colliders => ownColliders;

        // ---------------------------------------------------------------- king

        void RaiseSceptre()
        {
            move = PieceMove.KingCheck;
            moveTime = 0f;
            moveTarget = bodies[0].position;
            cooldowns.Start(0, PieceAbilities.CheckCooldown);
            AbilityUses++;
            LastAbilityEvent = "체크!";
        }

        void Shockwave()
        {
            Vector3 centre = bodies[0].position;
            moveTarget = centre;
            int hit = 0;
            foreach (var other in All)
            {
                if (other == null || other == this || other.NetworkPuppet || !Teams.AreEnemies(Team, other.Team)) continue;
                Vector3 to = other.bodies[0].position - centre;
                if (to.sqrMagnitude > PieceAbilities.CheckRadius * PieceAbilities.CheckRadius) continue;
                Vector3 away = FlatDir(to, facing);
                other.TakeHit(away * PieceAbilities.CheckPush + Vector3.up * PieceAbilities.CheckLift, PieceAbilities.CheckKnockdown, 0f, true);
                hit++;
            }
            CheckHits += hit;
            LastAbilityEvent = hit > 0 ? $"체크!: {hit}명 넘어뜨림" : "체크!: 4 m 안에 적 없음";
            move = PieceMove.None;
            moveTime = 0f;
        }

        /// <summary>Castling: swaps places with the nearest rook of its own team, however far away.</summary>
        void Castle()
        {
            RagdollPawn rook = null;
            float best = float.MaxValue;
            foreach (var other in All)
            {
                if (other == null || other == this || other.NetworkPuppet || other.piece != PieceKind.Rook) continue;
                if (Team == Teams.None || other.Team != Team) continue;
                float d = (other.bodies[0].position - bodies[0].position).sqrMagnitude;
                if (d >= best) continue;
                best = d;
                rook = other;
            }
            if (rook == null)
            {
                LastAbilityEvent = "캐슬링: 같은 팀 룩이 없음";
                return;
            }
            Vector3 mine = StandingHips(bodies[0].position), theirs = StandingHips(rook.bodies[0].position);
            Vector3 myFace = facing, theirFace = rook.facing;
            Teleport(theirs, theirFace);
            rook.Teleport(mine, myFace);
            cooldowns.Start(1, PieceAbilities.CastleCooldown);
            AbilityUses++;
            Castles++;
            LastAbilityEvent = $"캐슬링: {rook.DisplayName}와 자리 바꿈";
            rook.LastAbilityEvent = $"캐슬링: {DisplayName}와 자리 바꿈";
        }

        /// <summary>Where the hips go to stand over the floor under <paramref name="hips"/> (lying, climbing or
        /// standing, the swap puts each one on its feet).</summary>
        Vector3 StandingHips(Vector3 hips)
        {
            return SolidRay(hips + Vector3.up * 0.5f, Vector3.down, 3f, out var floor) && floor.normal.y > 0.5f
                ? new Vector3(hips.x, floor.point.y + standHeight + 0.02f, hips.z)
                : hips;
        }

        /// <summary>The king's grace: a king of its own team within 6 m. Looked up four times a second.</summary>
        void UpdateGrace(float dt)
        {
            auraWait -= dt;
            if (auraWait > 0f) return;
            auraWait = 0.25f;
            inAura = false;
            if (Team == Teams.None) return;
            Vector3 at = bodies[0].position;
            foreach (var other in All)
            {
                if (other == null || other == this || other.piece != PieceKind.King || other.Team != Team) continue;
                Vector3 d = other.bodies[0].position - at;
                if (!PieceAbilities.InAura(d.x, d.y, d.z)) continue;
                inAura = true;
                return;
            }
        }

        // ---------------------------------------------------------------- bishop

        void StartHover(RagdollParams p)
        {
            LetGo(0.3f);
            ReleaseHolders(0.5f);
            move = PieceMove.BishopHover;
            moveTime = 0f;
            hoverBase = bodies[0].position.y;
            hoverHeight = PieceAbilities.HoverStartRise;
            anchorPos = bodies[0].position;
            anchorVel = Vector3.zero;
            moveTarget = anchorPos;
            shardWait = 0f;
            AbilityUses++;
            LastAbilityEvent = "호버: 좌클릭 돌조각, Space 위로, Shift 아래로";
        }

        void Hover(RagdollParams p, float dt)
        {
            shardWait -= dt;
            if (moveTime >= PieceAbilities.HoverSeconds || input.ability)
            {
                LastAbilityEvent = moveTime >= PieceAbilities.HoverSeconds ? "호버 끝 (5초)" : "호버 끝 (E)";
                EndMove(false);
                return;
            }
            if (pieceJump) hoverHeight = Mathf.Min(PieceAbilities.HoverRise, hoverHeight + PieceAbilities.HoverStep);
            if (pieceSprint) hoverHeight -= 2f * dt;
            Vector3 flat = Flat(input.move);
            if (flat.sqrMagnitude > 1f) flat.Normalize();
            float vy = Mathf.Clamp((hoverBase + hoverHeight - anchorPos.y) * 3f, -2.5f, 2.5f);
            if (vy < 0f && SolidRay(anchorPos, Vector3.down, standHeight + 0.1f, out _))
            {
                // Down to standing height over a floor, no further.
                vy = 0f;
                hoverHeight = anchorPos.y - hoverBase;
            }
            Vector3 delta = CarrySlide((flat * PieceAbilities.HoverSpeed + Vector3.up * vy) * dt);
            anchorVel = delta / Mathf.Max(dt, 1e-4f);
            anchorPos += delta;
            Vector3 aim = Flat(input.aim);
            Vector3 turnTo = aim.sqrMagnitude > 1e-4f ? aim : flat;
            if (turnTo.sqrMagnitude > 1e-4f) facing = Vector3.RotateTowards(facing, turnTo.normalized, 8f * dt, 0f);
            anchor.MovePosition(anchorPos);
            anchor.MoveRotation(Quaternion.LookRotation(facing, Vector3.up));
            moveTarget = anchorPos;
            if (pieceShove && shardWait <= 0f) ThrowShard();
        }

        void ThrowShard()
        {
            if (PieceShard.LiveFrom(this) >= PieceAbilities.ShardsAtOnce)
            {
                LastAbilityEvent = "돌조각: 이미 셋이 날고 있음";
                return;
            }
            Vector3 aim = input.aim.sqrMagnitude > 0.25f ? input.aim.normalized : facing;
            Vector3 from = bodies[(int)BodyId.Chest].position + aim * 0.7f + Vector3.up * 0.2f;
            PieceShard.Throw(this, from, aim * PieceAbilities.ShardSpeed + Vector3.up * 2f);
            shardWait = PieceAbilities.ShardInterval;
            ShardsThrown++;
            LastAbilityEvent = $"돌조각 {ShardsThrown}";
        }

        /// <summary>The carried body along <paramref name="delta"/>, sliding along whatever is in the way.</summary>
        Vector3 CarrySlide(Vector3 delta)
        {
            float length = delta.magnitude;
            if (length < 1e-6f || !RopeBodyCast(anchorPos, delta / length, length, out var hit)) return delta;
            Vector3 before = delta / length * Mathf.Max(0f, hit.distance - 0.02f);
            Vector3 along = Vector3.ProjectOnPlane(delta - before, hit.normal);
            if (along.sqrMagnitude > 1e-8f && RopeBodyCast(anchorPos + before, along.normalized, along.magnitude, out var second))
                along = along.normalized * Mathf.Max(0f, second.distance - 0.02f);
            return before + along;
        }

        /// <summary>Space in the air: the diagonal glide, 45 degrees down the way the keys (or the body) point.</summary>
        void TryGlide(RagdollParams p)
        {
            if (!CanUseAbility || Grounded || coyote > 0f || Climbing || rope != null || hookPhase != HookPhase.None
                || handL.HoldingLedge || handR.HoldingLedge || jumpTimer > 0.2f) return;
            Vector3 flat = FlatDir(input.move.sqrMagnitude > 0.01f ? input.move : facing, facing);
            LetGo(0.3f);
            facing = flat;
            move = PieceMove.BishopGlide;
            moveTime = 0f;
            anchorPos = bodies[0].position;
            anchorVel = Vector3.zero;
            AbilityUses++;
            LastAbilityEvent = "대각 활공 (Space로 멈춤)";
        }

        void Glide(RagdollParams p, float dt)
        {
            if (pieceJump || moveTime > 12f)
            {
                LastAbilityEvent = "대각 활공 끝";
                EndMove(true);
                return;
            }
            // Steering: toward the keys, a quarter turn a second.
            Vector3 want = Flat(input.move);
            if (want.sqrMagnitude > 0.01f) facing = Vector3.RotateTowards(facing, want.normalized, Mathf.PI * 0.5f * dt, 0f);
            float down = Mathf.Sin(PieceAbilities.GlideAngle * Mathf.Deg2Rad), on = Mathf.Cos(PieceAbilities.GlideAngle * Mathf.Deg2Rad);
            moveDir = facing * on + Vector3.down * down;
            float step = PieceAbilities.GlideSpeed * dt;
            moveTarget = anchorPos;
            if (SolidRay(anchorPos, Vector3.down, standHeight + step * down + 0.05f, out var floor) && floor.normal.y > 0.5f)
            {
                // Down onto a floor: on its feet, running on with some of the speed.
                anchorPos.y = floor.point.y + standHeight + 0.02f;
                anchorVel = facing * (PieceAbilities.GlideSpeed * on * 0.6f);
                wallPoint = floor.point;
                wallNormal = floor.normal;
                anchor.MovePosition(anchorPos);
                LastAbilityEvent = "대각 활공: 착지";
                EndMove(true);
                return;
            }
            if (RopeBodyCast(anchorPos, moveDir, step, out var hit))
            {
                // Into a wall: it drops off it.
                anchorPos += moveDir * Mathf.Max(0f, hit.distance - 0.02f);
                anchorVel = Vector3.zero;
                wallPoint = hit.point;
                wallNormal = hit.normal;
                anchor.MovePosition(anchorPos);
                LastAbilityEvent = "대각 활공: 부딪힘";
                EndMove(true);
                return;
            }
            anchorVel = moveDir * PieceAbilities.GlideSpeed;
            anchorPos += moveDir * step;
            anchor.MovePosition(anchorPos);
            anchor.MoveRotation(Quaternion.LookRotation(facing, Vector3.up));
        }

        /// <summary>
        /// A bishop's stone hit this pawn (PieceShard, from its FixedUpdate): on a wall it bites stamina; riding
        /// a moving platform, the second stone within 2 s knocks it off; anywhere else a stagger and a push.
        /// Applied at this pawn's next step, not inside the physics callback.
        /// </summary>
        public void ShardHit(Vector3 velocity)
        {
            if (NetworkPuppet) return;
            shardPending = true;
            shardPush = FlatDir(velocity, -facing);
        }

        void TakeShard()
        {
            if (!shardPending) return;
            shardPending = false;
            ShardHitsTaken++;
            if (Carried) EndMove(false);
            Vector3 push = shardPush * PieceAbilities.ShardPush;
            if (Climbing)
            {
                TakeHit(Vector3.zero, 0f, PieceAbilities.ShardStamina, false);
                return;
            }
            if (Riding && shardTally.Hit(Time.timeAsDouble, PieceAbilities.ShardRideWindow))
            {
                TakeHit(push * 1.5f + Vector3.up * 2f, PieceAbilities.ShardRideKnockdown, 0f, true);
                return;
            }
            Stagger(PieceAbilities.ShardStagger);
            TakeHit(push, 0f, 0f, false);
        }

        // ---------------------------------------------------------------- poses and the wire

        /// <summary>The carried body: upright, leaning into the charge or the glide, placed on the anchor.</summary>
        void ApplyCarryPose(RagdollParams p)
        {
            Vector3 up = Vector3.up;
            if (move == PieceMove.RookCharge && rookVertical == 0) up = (Vector3.up + moveDir * 0.45f).normalized;
            else if (move == PieceMove.BishopGlide) up = (Vector3.up + facing * 0.7f).normalized;
            PlaceHanging(p, anchorPos, Vector3.up, up, 0f, false);
        }

        /// <summary>The ability's part of the puppet pose: the rook's shoulder charge, the king's raised sceptre,
        /// the bishop's arms out, the knight's legs tucked.</summary>
        void AbilityPose(ref Quaternion armL, ref Quaternion armR, ref Quaternion chest, ref Quaternion head,
                         ref Quaternion thighL, ref Quaternion thighR, ref Quaternion footL, ref Quaternion footR)
        {
            switch (move)
            {
                case PieceMove.RookWindup:
                case PieceMove.RookCharge:
                    chest = Quaternion.Euler(rookVertical == 0 ? 28f : 8f, 0f, 0f);
                    head = Quaternion.Euler(-12f, 0f, 0f);
                    armL = Quaternion.Euler(0f, -70f, -20f);
                    armR = Quaternion.Euler(0f, 70f, 20f);
                    break;
                case PieceMove.KingCheck:
                    chest = Quaternion.Euler(-6f, 0f, 0f);
                    armR = Quaternion.Euler(0f, 0f, 80f);
                    break;
                case PieceMove.BishopHover:
                    armL = Quaternion.Euler(0f, 0f, -25f);
                    armR = Quaternion.Euler(0f, 0f, 25f);
                    thighL = thighR = Quaternion.identity;
                    footL = footR = Quaternion.identity;
                    break;
                case PieceMove.BishopGlide:
                    chest = Quaternion.Euler(20f, 0f, 0f);
                    armL = Quaternion.Euler(0f, -30f, -10f);
                    armR = Quaternion.Euler(0f, 30f, 10f);
                    break;
                case PieceMove.KnightLeap:
                    thighL = thighR = Quaternion.Euler(-55f, 0f, 0f);
                    footL = footR = Quaternion.Euler(55f, 0f, 0f);
                    break;
            }
        }

        void CapturePiece(RagdollPose pose)
        {
            pose.move = (byte)move;
            // A piece has no hook: the hook's point carries the move's target instead.
            if (piece != PieceKind.Pawn) pose.hookPoint = moveTarget;
        }

        void ApplyPiece(RagdollPose pose)
        {
            netMove = pose.move <= (byte)PieceMove.BishopGlide ? (PieceMove)pose.move : PieceMove.None;
            netMoveTarget = pose.hookPoint;
        }
    }
}
