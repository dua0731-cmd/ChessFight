using ChessFight.Gameplay;
using UnityEngine;

namespace ChessFight.RagdollLab
{
    /// <summary>
    /// The tutorial's three practice pawns (R834). Offline only, driven by a script, nothing sent; black (the other side),
    /// so the player may tackle and grab them - a Pawn Rush hand never takes a teammate (RagdollPawn.PawnRushTeammate).
    /// StateDrivenMover: they move by the player's state, which is fine offline (DECISIONS G2 is about obstacles).
    ///
    /// User 11-25 "봇 지능을 높게 해": each one sees, decides and reacts, and what it does follows the lesson (Mode):
    ///   Wander (always under the rest): walks to spots of its own choosing in the yard, rests, keeps clear of the
    ///           others, turns to watch the player when it comes near, jumps when it is stuck, walks back if pushed out.
    ///   Dodge  (7-1 tackle): a bot that SEES the player charging or diving at it (in front, within 4.5 m) steps aside
    ///           (0.18 s to react, 0.12 s to a dive). From behind or the side it sees nothing and goes down; after a
    ///           dodge it is out of breath for 3 s, so a second go straight away lands.
    ///   Evade  (7-2 grab): the player within 3 m: a 0.25 s start, then it runs (a little slower than the player's run,
    ///           along the yard's edge when it reaches one), out of breath after 3 s of it; held, it struggles like a player
    ///           (clicks with jumps, pulling away), so a hold must be kept up.
    ///   Hunt   (7-3 get free): the first bot reads where the player is going, sprints when far, takes hold with both
    ///           hands and hauls the player backwards; the other two stand off to the player's sides to cut the way out.
    /// Held by the player in any mode, a bot struggles. A knocked-down bot gets up by itself; one off the map comes home.
    /// </summary>
    public sealed class TutorialBots : MonoBehaviour
    {
        public enum Mode { Wander, Dodge, Evade, Hunt }

        const float WalkInput = 0.55f, JogInput = 0.85f, FleeInput = 0.8f, ChaseInput = 0.9f, FlankInput = 0.75f;
        const float SeeRange = 6f, SeeCos = 0.34f, FeelRange = 0.9f, WatchRange = 5f, WatchTurn = 360f;
        const float DodgeReaction = 0.18f, DiveReaction = 0.12f, DodgeTime = 0.55f, DodgeRest = 3f, DodgeRange = 4.5f;
        const float DiveSpeed = 2f, ChargeSpeed = 2f, DiveAim = 0.75f;
        const float FleeRange = 3f, FleeReaction = 0.25f, FleeFor = 3f, FleeRest = 1.8f;
        const float StruggleDelay = 0.7f, TapEvery = 0.25f;
        const float GrabReach = 0.95f, LetGo = 0.25f, Lead = 0.35f, LeadMax = 1.5f, SprintFrom = 3f, Haul = 0.6f;
        const float FlankDistance = 2.4f, FlankAngle = 70f, BackOff = 1.5f;
        const float Spacing = 1.4f, Leash = 14f, StuckCheck = 0.8f, StuckMove = 0.12f, Inset = 0.8f;

        sealed class Bot
        {
            public RagdollDriver driver;
            public Vector3 home, goal, lastPos, dodgeDir, pinFace;
            public float pauseUntil, letGoFor, dodgeAt = -1f, dodgeUntil = -1f, dodgeRestUntil, fleeSince = -1f, fleeRestUntil;
            public float heldSince = -1f, nextTap, stuckClock, pinnedUntil, backOffUntil;
            public int taps;
            public bool jog;
        }

        Bot[] bots = new Bot[0];
        readonly System.Random rng = new System.Random(834);

        /// <summary>What the bots are doing (the course sets it per lesson).</summary>
        public Mode Now { get; set; }

        /// <summary>The player: who they watch, dodge, run from and go after.</summary>
        public RagdollPawn Target { get; set; }

        public int Count => bots.Length;
        public RagdollPawn this[int i] => bots[i] != null && bots[i].driver != null ? bots[i].driver.Pawn : null;
        public RagdollPawn Grabber => bots.Length > 0 ? this[0] : null;

        /// <summary>The automatic check: stand bot i at a spot facing yaw for some seconds (it still dodges and struggles).</summary>
        public void PinForTest(int i, Vector3 at, float yaw, float seconds)
        {
            if (i < 0 || i >= bots.Length || bots[i] == null) return;
            var bot = bots[i];
            var face = Quaternion.Euler(0f, yaw, 0f);
            bot.driver.Teleport(at, face);
            bot.pinFace = face * Vector3.forward;
            bot.pinnedUntil = Time.time + seconds;
            bot.dodgeAt = bot.dodgeUntil = -1f;
            bot.dodgeRestUntil = 0f;
        }

        public void Spawn(GameObject prefab, Material skin)
        {
            var lanes = TutorialLayout.BotLanes;
            bots = new Bot[lanes.Length];
            for (int i = 0; i < lanes.Length; i++)
            {
                var bot = new Bot { home = Vector3.Lerp(lanes[i].a, lanes[i].b, i % 2 == 0 ? 0.2f : 0.8f) };
                bot.goal = bot.home;
                bot.lastPos = bot.home;
                var go = Instantiate(prefab, bot.home, Quaternion.identity, transform);
                go.name = "Practice bot " + (i + 1);
                bot.driver = go.GetComponentInChildren<RagdollDriver>();
                if (bot.driver == null) { Destroy(go); continue; }
                var pawn = bot.driver.Pawn;
                pawn.Team = Teams.Black;
                pawn.DisplayName = ChessFight.Network.Loc.T("연습 상대") + " " + (i + 1);
                pawn.IsSelf = false;
                if (skin != null && pawn.skin != null) pawn.skin.sharedMaterial = skin;
                MatchNameplate.Attach(pawn);
                bot.driver.Teleport(bot.home, Quaternion.Euler(0f, i % 2 == 0 ? 90f : 270f, 0f));
                bots[i] = bot;
            }
        }

        void Update()
        {
            float now = Time.time;
            for (int i = 0; i < bots.Length; i++)
            {
                var bot = bots[i];
                if (bot == null || bot.driver == null) continue;
                var pawn = bot.driver.Pawn;
                var hips = pawn.Hips.position;
                if (hips.y < -4f || Flat(hips - YardCenter).magnitude > Leash + 10f)
                {
                    bot.driver.Teleport(bot.home, Quaternion.identity);
                    bot.goal = bot.home;
                    continue;
                }
                pawn.FaceHoldRate = 0f;
                // Knocked down: nothing to do but get up (the pawn does that itself).
                if (pawn.State != PawnState.Active)
                {
                    bot.heldSince = -1f;
                    bot.driver.SetCommand(new CharacterCommand { Aim = pawn.Facing });
                    continue;
                }
                var command = Think(i, bot, pawn, hips, now);
                bot.driver.SetCommand(command);
            }
        }

        CharacterCommand Think(int i, Bot bot, RagdollPawn pawn, Vector3 hips, float now)
        {
            var me = Target;
            var command = new CharacterCommand { Aim = pawn.Facing };
            Vector3 toMe = me != null ? Flat(me.Hips.position - hips) : Vector3.zero;
            float d = toMe.magnitude;

            // Held by the player: struggle like a person - a click every quarter second, every other one with a jump,
            // pulling away from the hand - after a moment's surprise.
            if (pawn.BeingHeld)
            {
                if (bot.heldSince < 0f) { bot.heldSince = now; bot.nextTap = now + StruggleDelay; }
                command.Move = d > 0.01f ? -toMe / d : -pawn.Facing;
                if (now >= bot.nextTap)
                {
                    command.Shove = true;
                    command.Jump = (bot.taps++ & 1) == 0;
                    bot.nextTap = now + TapEvery;
                }
                return command;
            }
            bot.heldSince = -1f;

            Vector3 move = Vector3.zero;
            bool sprint = false, grab = false, decided = false;

            switch (Now)
            {
                case Mode.Dodge when me != null:
                    if (bot.dodgeUntil > now) { move = bot.dodgeDir; sprint = true; decided = true; }
                    else if (bot.dodgeAt > 0f && now >= bot.dodgeAt)
                    {
                        bot.dodgeAt = -1f;
                        bot.dodgeUntil = now + DodgeTime;
                        bot.dodgeRestUntil = now + DodgeTime + DodgeRest;
                        move = bot.dodgeDir; sprint = true; decided = true;
                    }
                    else if (bot.dodgeAt < 0f && now >= bot.dodgeRestUntil && Sees(pawn, hips, me))
                    {
                        // The player charging this way (seen), or diving: step off the line to the side the bot already
                        // stands. A dive alone is too fast to see in time (a sprint dive covers 3.7 m in about 0.4 s), so
                        // the run-up is what gives the player away; from behind there is nothing to see.
                        var v = Flat(me.Hips.linearVelocity);
                        var fromMe = -toMe;
                        bool atMe = d < DodgeRange && d > 0.01f && Vector3.Dot(v.normalized, fromMe / d) > DiveAim;
                        bool diving = me.Diving && v.magnitude > DiveSpeed;
                        bool charging = me.State == PawnState.Active && v.magnitude > ChargeSpeed;
                        if (atMe && (diving || charging))
                        {
                            var side = Vector3.Cross(Vector3.up, v.normalized);
                            float off = Vector3.Dot(side, fromMe);
                            if (off < 0f || (Mathf.Abs(off) < 0.1f && rng.NextDouble() < 0.5)) side = -side;
                            bot.dodgeDir = side;
                            bot.dodgeAt = now + (diving ? DiveReaction : DodgeReaction);
                        }
                    }
                    break;

                case Mode.Evade when me != null:
                    if (now < bot.fleeRestUntil)
                    {
                        Watch(pawn, toMe);   // out of breath: stands and stares
                        decided = true;
                    }
                    else if (d < FleeRange)
                    {
                        // A start (FleeReaction) - turns to look - then runs; out of breath after FleeFor of it.
                        if (bot.fleeSince < 0f) bot.fleeSince = now;
                        decided = true;
                        float running = now - bot.fleeSince - FleeReaction;
                        if (running < 0f) Watch(pawn, toMe);
                        else if (running > FleeFor) { bot.fleeRestUntil = now + FleeRest; bot.fleeSince = -1f; }
                        else move = FleeWay(hips, d > 0.01f ? -toMe / d : -pawn.Facing) * FleeInput;
                    }
                    else if (d > FleeRange + 1f) bot.fleeSince = -1f;
                    break;

                case Mode.Hunt when me != null:
                    decided = true;
                    if (i == 0) Hunt(bot, pawn, me, toMe, d, now, ref move, ref sprint, ref grab);
                    else Flank(i, pawn, hips, me, ref move);
                    break;
            }

            if (now < bot.pinnedUntil && !decided)
            {
                pawn.FaceHold = bot.pinFace;
                pawn.FaceHoldRate = WatchTurn;
                decided = true;
            }
            if (!decided) move = Wander(bot, pawn, hips, me, toMe, d, now);
            if (Now != Mode.Hunt || i != 0) move = KeepApart(i, hips, move);

            // Stuck against something while trying to go: hop.
            if (now - bot.stuckClock >= StuckCheck)
            {
                bool tried = move.sqrMagnitude > 0.09f && pawn.Grounded;
                command.Jump = tried && Flat(hips - bot.lastPos).magnitude < StuckMove;
                bot.lastPos = hips;
                bot.stuckClock = now;
            }
            command.Move = Vector3.ClampMagnitude(move, 1f);
            command.Sprint = sprint;
            command.Grab = grab;
            if (move.sqrMagnitude > 0.01f && pawn.FaceHoldRate <= 0f) command.Aim = move.normalized;
            return command;
        }

        // The grabber: lead the player, sprint from afar, take hold, haul backwards; give it room just after it got free.
        void Hunt(Bot bot, RagdollPawn pawn, RagdollPawn me, Vector3 toMe, float d, float now, ref Vector3 move, ref bool sprint, ref bool grab)
        {
            var dir = d > 0.01f ? toMe / d : pawn.Facing;
            if (pawn.HoldingPawn(out _))
            {
                grab = true;
                move = -dir * Haul;          // haul: walk backwards with the player in both hands
                pawn.FaceHold = dir;
                pawn.FaceHoldRate = WatchTurn;
                return;
            }
            // A hand takes whatever is nearest while the key is held (PawnHand), the floor included: let anything else go.
            if (pawn.Grabbing) { bot.letGoFor = LetGo; return; }
            bot.letGoFor -= Time.deltaTime;
            if (me.GrabImmune) bot.backOffUntil = now + BackOff;
            if (now < bot.backOffUntil) { Watch(pawn, toMe); return; }
            // Where the player will be a moment from now, not where it is.
            var lead = Vector3.ClampMagnitude(Flat(me.Hips.linearVelocity) * Lead, LeadMax);
            var aim = Flat(me.Hips.position + (d > 1.5f ? lead : Vector3.zero) - pawn.Hips.position);
            if (d > 0.6f) move = aim.normalized * ChaseInput;
            sprint = d > SprintFrom && pawn.Stamina > 0.3f;
            grab = d < GrabReach && bot.letGoFor <= 0f;
            if (d < 1.6f) { pawn.FaceHold = dir; pawn.FaceHoldRate = WatchTurn; }
        }

        // The other two: stand off to the player's sides, away from the grabber, to cut the way out.
        void Flank(int i, RagdollPawn pawn, Vector3 hips, RagdollPawn me, ref Vector3 move)
        {
            var grabber = Grabber;
            var away = grabber != null ? Flat(me.Hips.position - grabber.Hips.position) : -me.Facing;
            if (away.sqrMagnitude < 0.01f) away = -me.Facing;
            var spot = me.Hips.position + Quaternion.Euler(0f, i == 1 ? FlankAngle : -FlankAngle, 0f) * away.normalized * FlankDistance;
            var to = Flat(spot - hips);
            if (to.magnitude > 0.5f) move = to.normalized * FlankInput;
            else Watch(pawn, Flat(me.Hips.position - hips));
        }

        // Its own spots in the yard: walk (sometimes jog) there, rest a moment, pick another away from the others'.
        Vector3 Wander(Bot bot, RagdollPawn pawn, Vector3 hips, RagdollPawn me, Vector3 toMe, float d, float now)
        {
            if (Flat(hips - YardCenter).magnitude > Leash) bot.goal = bot.home;
            var to = Flat(bot.goal - hips);
            if (to.magnitude < 0.6f)
            {
                if (bot.pauseUntil <= 0f) bot.pauseUntil = now + Range(0.4f, 1.4f);
                if (now < bot.pauseUntil)
                {
                    if (me != null && d < WatchRange) Watch(pawn, toMe);
                    return Vector3.zero;
                }
                bot.pauseUntil = 0f;
                bot.goal = PickSpot(bot, hips);
                bot.jog = rng.NextDouble() < 0.25;
                to = Flat(bot.goal - hips);
            }
            return StayInYard(hips, to.normalized) * (bot.jog ? JogInput : WalkInput);
        }

        Vector3 PickSpot(Bot bot, Vector3 from)
        {
            var yard = TutorialLayout.BotYard;
            Vector3 best = bot.home;
            float bestScore = float.MinValue;
            for (int tries = 0; tries < 8; tries++)
            {
                var spot = new Vector3(Range(yard.xMin + Inset, yard.xMax - Inset), 0f, Range(yard.yMin + Inset, yard.yMax - Inset));
                float score = Mathf.Min(Flat(spot - from).magnitude, 4f);
                foreach (var other in bots)
                    if (other != null && other != bot) score = Mathf.Min(score, Flat(spot - other.goal).magnitude * 1.5f);
                if (score > bestScore) { bestScore = score; best = spot; }
            }
            return best;
        }

        // A bot that sees the player: in front (about 140 degrees) within 6 m, or touching close.
        static bool Sees(RagdollPawn pawn, Vector3 hips, RagdollPawn me)
        {
            var to = Flat(me.Hips.position - hips);
            float d = to.magnitude;
            if (d < FeelRange) return true;
            return d < SeeRange && Vector3.Dot(Flat(pawn.Facing).normalized, to / d) > SeeCos;
        }

        static void Watch(RagdollPawn pawn, Vector3 toMe)
        {
            if (toMe.sqrMagnitude < 0.01f) return;
            pawn.FaceHold = toMe.normalized;
            pawn.FaceHoldRate = WatchTurn;
        }

        // Spread out: a push away from any bot closer than Spacing.
        Vector3 KeepApart(int i, Vector3 hips, Vector3 move)
        {
            for (int j = 0; j < bots.Length; j++)
            {
                if (j == i || this[j] == null) continue;
                var off = Flat(hips - this[j].Hips.position);
                float d = off.magnitude;
                if (d > 0.01f && d < Spacing) move += off / d * ((Spacing - d) / Spacing);
            }
            return move;
        }

        // Inside the yard a heading that would leave it slides along the edge; outside, it heads back in.
        static Vector3 StayInYard(Vector3 at, Vector3 dir)
        {
            var yard = TutorialLayout.BotYard;
            bool inside = yard.Contains(new Vector2(at.x, at.z));
            if (!inside) return Flat(YardCenter - at).normalized;
            if ((at.x < yard.xMin + 0.5f && dir.x < 0f) || (at.x > yard.xMax - 0.5f && dir.x > 0f)) dir.x = 0f;
            if ((at.z < yard.yMin + 0.5f && dir.z < 0f) || (at.z > yard.yMax - 0.5f && dir.z > 0f)) dir.z = 0f;
            return dir.sqrMagnitude > 0.01f ? dir.normalized : Flat(YardCenter - at).normalized;
        }

        // Running away: straight from the player while there is room; against the yard's edge, along it to the side with
        // more room (only a corner traps a bot).
        static Vector3 FleeWay(Vector3 at, Vector3 away)
        {
            var yard = TutorialLayout.BotYard;
            if (!yard.Contains(new Vector2(at.x, at.z))) return Flat(YardCenter - at).normalized;
            var dir = away;
            if ((at.x < yard.xMin + 0.5f && dir.x < 0f) || (at.x > yard.xMax - 0.5f && dir.x > 0f)) dir.x = 0f;
            if ((at.z < yard.yMin + 0.5f && dir.z < 0f) || (at.z > yard.yMax - 0.5f && dir.z > 0f)) dir.z = 0f;
            if (dir.sqrMagnitude > 0.09f) return dir.normalized;
            var along = new Vector3(-away.z, 0f, away.x);
            if (Vector3.Dot(along, YardCenter - at) < 0f) along = -along;
            return along.normalized;
        }

        static Vector3 YardCenter
        {
            get
            {
                var c = TutorialLayout.BotYard.center;
                return new Vector3(c.x, 0f, c.y);
            }
        }

        float Range(float a, float b) => a + (float)rng.NextDouble() * (b - a);

        static Vector3 Flat(Vector3 v) => new Vector3(v.x, 0f, v.z);
    }
}
