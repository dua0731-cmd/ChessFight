using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using ChessFight.Network;
using UnityEngine;

namespace ChessFight.RagdollLab
{
    /// <summary>
    /// R808 (승규 님 11-23, after the online play's "피격을 해도 허공을 가로지르는 것 같음" and the fixes that followed it): the
    /// automated check of the click cut's hit feel in the real match scene played alone (SwordFight.unity: this player,
    /// an ally dummy and two enemy dummies). Put on by the Editor's batch check (RagdollLabEditor/SwordHitFeelCheck,
    /// Tools/Run-SwordHitFeel.ps1); nothing in the game adds it. Each check is a PASS / FAIL / SKIP line with its numbers
    /// in Logs/SwordHitFeel/report.txt:
    /// <list type="number">
    /// <item>every piece's first cut, missing: its windup (the cut starting → its blade starting) and the whole cut;</item>
    /// <item>a queen's combo beat: four clicks 0.15 s apart, the time between the blades starting;</item>
    /// <item>this player's queen cutting an enemy: the hit, the blade caught (its pace for BiteTime), the cut piece's
    /// picture held, the cut's cutter, the hit mark, the sounds (this screen's own: the body layer);</item>
    /// <item>the ally dummy cutting the other enemy far from this player: someone else's hit (no layer, quieter, no mark);</item>
    /// <item>a cut on a piece under its respawn protection: stopped on the shell, the moment raised, the bubble's flare;</item>
    /// <item>a client's side: two pieces turned into puppets and fed made-up host frames - the cut waits for the bodies
    /// drawn to reach it, finds its cutter by the strike's time, falls back to the nearest blade, waits at most 0.1 s.</item>
    /// </list>
    /// Pictures of the queen's hit from the side. Not a play test with people, and not online (6 is the client's code
    /// fed by hand, not packets).
    /// </summary>
    [DefaultExecutionOrder(1000)]   // after the pawns' pictures and the sword (LateUpdate captures)
    public sealed class SwordHitFeelProbe : MonoBehaviour
    {
        public static bool Done { get; private set; }
        public static int Failures { get; private set; }
        public const string Folder = "Logs/SwordHitFeel";

        const BindingFlags Hidden = BindingFlags.NonPublic | BindingFlags.Instance;
        static readonly FieldInfo WindupField = typeof(SwordFightPawn).GetField("windup", Hidden);
        static readonly FieldInfo ActiveField = typeof(SwordFightPawn).GetField("wasActive", Hidden);
        static readonly FieldInfo MarksField = typeof(SwordFightMatch).GetField("hitMarks", Hidden);
        static readonly FieldInfo ShellField = typeof(SwordFightMatch).GetField("shellStruck", Hidden);
        static readonly FieldInfo RulesField = typeof(SwordFightMatch).GetField("rules", Hidden);
        static readonly FieldInfo HoldField = typeof(RagdollPawn).GetField("visualHoldUntil", Hidden);

        readonly StringBuilder report = new StringBuilder();
        int passed, failed, skipped;
        SwordFightGame game;
        SwordFightMatch match;
        SwordFightPawn me, ally, enemyA, enemyB;
        float top;
        string folder;

        // What the game said while the checks ran.
        readonly List<(SwordFightPawn victim, SwordFightPawn by, float at)> cuts = new List<(SwordFightPawn, SwordFightPawn, float)>();
        readonly List<SfFxEvent> moments = new List<SfFxEvent>();
        readonly List<(string name, float gain)> sounds = new List<(string, float)>();
        // The cut piece's picture held (RagdollPawn.VisualHeld), watched every frame: how long the cut asked (set at the
        // cut), how long it was seen held, and the longest frame meanwhile (a held picture is let go at the first frame
        // after its time, so a long frame - a batch Editor's - lengthens what is seen).
        SwordFightPawn watched;
        float heldFrom = -1f, heldFor = -1f, holdSet = -1f, longestFrame;
        // Pictures asked for: the frame they are taken on and the file.
        readonly List<(int frame, string file)> shots = new List<(int, string)>();
        Camera shooter;

        void Start()
        {
            Done = false;
            Failures = 0;
            folder = Path.GetFullPath(Path.Combine(Application.dataPath, "..", Folder));
            Directory.CreateDirectory(folder);
            foreach (var old in Directory.GetFiles(folder, "*.png")) File.Delete(old);
            StartCoroutine(Run());
        }

        void OnDestroy() => Unhook();

        void Hook()
        {
            SwordFightPawn.Cut += OnCut;
            SwordFightSkills.Fx += OnFx;
            SwordFightSkillSfx.Played += OnPlayed;
        }

        void Unhook()
        {
            SwordFightPawn.Cut -= OnCut;
            SwordFightSkills.Fx -= OnFx;
            SwordFightSkillSfx.Played -= OnPlayed;
        }

        void OnCut(SwordFightPawn victim, SwordFightPawn by)
        {
            cuts.Add((victim, by, Time.unscaledTime));
            // The hit effect's own handler (SwordFightSkillFx.OnCut, hooked when the scene started) has held it by now.
            if (victim == watched && holdSet < 0f && HoldField != null)
                holdSet = (float)HoldField.GetValue(victim.Pawn) - Time.unscaledTime;
        }
        void OnFx(SfFxEvent e) => moments.Add(e);
        void OnPlayed(SwordFightSkillSfx.Sound s, float gain, float pan) => sounds.Add((s != null ? s.name : "?", gain));

        void Update()
        {
            if (watched == null || watched.Pawn == null) return;
            bool held = watched.Pawn.VisualHeld;
            if (held && heldFrom < 0f) heldFrom = Time.unscaledTime;
            if (heldFrom >= 0f && heldFor < 0f) longestFrame = Mathf.Max(longestFrame, Time.unscaledDeltaTime);
            if (!held && heldFrom >= 0f && heldFor < 0f) heldFor = Time.unscaledTime - heldFrom;
        }

        void LateUpdate()
        {
            for (int i = shots.Count - 1; i >= 0; i--)
            {
                if (shots[i].frame > Time.frameCount) continue;
                Shoot(shots[i].file);
                shots.RemoveAt(i);
            }
        }

        // ---------------------------------------------------------------- the run

        IEnumerator Run()
        {
            Line("# R808 sword hit feel check (" + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + ")");
            Line("Scene: the match played alone. Physics step " + (Time.fixedDeltaTime * 1000f).ToString("0.0") + " ms.");
            float until = Time.realtimeSinceStartup + 40f;
            while ((game = FindFirstObjectByType<SwordFightGame>()) == null || game.Match == null || game.Local == null || game.Fighters.Count < 4)
            {
                if (Time.realtimeSinceStartup > until) { Check(false, "the match scene starts with four fighters", "it did not"); Finish(); yield break; }
                yield return null;
            }
            match = game.Match;
            Hook();
            me = game.Local;
            foreach (var f in game.Fighters.Values)
            {
                if (f == null || f == me) continue;
                if (f.Pawn.Team == me.Pawn.Team) { if (ally == null) ally = f; }
                else if (enemyA == null) enemyA = f;
                else if (enemyB == null) enemyB = f;
            }
            if (ally == null || enemyA == null || enemyB == null) { Check(false, "an ally and two enemies", "missing"); Finish(); yield break; }
            game.PieceRequest?.Invoke(me.Id, PieceKind.Queen);
            until = Time.realtimeSinceStartup + 40f;
            while (!(RulesField != null && RulesField.GetValue(match) is SwordMatchRules rules && rules.Phase == SwordPhase.Playing) || SwordFightGame.InputLocked)
            {
                if (Time.realtimeSinceStartup > until) { Check(false, "the match reaches play", "it did not"); Finish(); yield break; }
                yield return null;
            }
            yield return Wait(.5f);
            var floor = SwordFightEdgeFloor.Current;
            top = floor != null && floor.Found ? floor.Top : 0f;
            Line($"Playing. Floor top {top:0.00} m. Me = {me.Pawn.DisplayName} (team {me.Pawn.Team}), ally {ally.Pawn.DisplayName}, enemies {enemyA.Pawn.DisplayName} · {enemyB.Pawn.DisplayName}.");
            bool sound = SwordFightSkillSfx.Current != null && SwordFightSkillSfx.Current.Sounds.Count > 0;
            Line(sound ? $"Sounds loaded: {SwordFightSkillSfx.Current.Sounds.Count}." : "Sounds: none loaded here (batch mode) - the sound checks are skipped; Heard() is checked by itself.");
            MakeShooter();

            yield return Windups();
            yield return ComboBeat();
            yield return MyHit(sound);
            yield return OthersHit(sound);
            yield return ShellBlock(sound);
            yield return ClientSide();
            Finish();
        }

        // ---------------------------------------------------------------- 1. windups and whole cuts

        static readonly (PieceKind piece, float windup, float length)[] Expected =
        {
            (PieceKind.King, .14f, .68f), (PieceKind.Queen, .12f, .68f), (PieceKind.Rook, .12f, .68f),
            (PieceKind.Bishop, .12f, .68f), (PieceKind.Knight, .10f, .68f), (PieceKind.Pawn, .10f, .56f),
        };

        IEnumerator Windups()
        {
            Line("\n## 1. First cut of every piece, missing (windup = the cut starting → its blade starting)");
            float dt = Time.fixedDeltaTime;
            foreach (var (piece, windup, length) in Expected)
            {
                game.PieceRequest?.Invoke(me.Id, piece);
                yield return Wait(.3f);
                Clear();
                yield return Wait(.7f);
                float startAt = -1f, activeAt = -1f, endAt = -1f, ageAtActive = 0f, field = -1f;
                Click(me, Vector3.forward);
                float limit = Time.time + 2f;
                while (Time.time < limit && endAt < 0f)
                {
                    yield return new WaitForFixedUpdate();
                    if (startAt < 0f && me.Attacking) { startAt = Time.fixedTime - dt; field = WindupField != null ? (float)WindupField.GetValue(me) : -1f; }
                    if (startAt >= 0f && activeAt < 0f && Active(me)) { activeAt = Time.fixedTime; ageAtActive = me.SwingAge; }
                    if (startAt >= 0f && !me.Attacking) endAt = Time.fixedTime - dt;
                    if (startAt < 0f) Release(me, Vector3.forward);
                }
                Release(me, Vector3.forward);
                float whole = endAt - startAt;
                bool ok = startAt >= 0f && activeAt >= 0f && Mathf.Abs(ageAtActive - windup) <= dt + .001f && Mathf.Abs(field - windup) < .001f
                          && Mathf.Abs(whole - length) <= dt + .001f && me.HitsLanded == 0;
                Check(ok, $"{piece} windup {windup:0.00} s · whole cut {length:0.00} s",
                      $"table {field:0.000} s, blade started at a swing age of {ageAtActive:0.000} s ({(activeAt - startAt):0.000} s after the cut started), whole cut {whole:0.000} s, hits {me.HitsLanded}");
            }
            game.PieceRequest?.Invoke(me.Id, PieceKind.Queen);
            yield return Wait(.3f);
        }

        // ---------------------------------------------------------------- 2. the combo's beat

        IEnumerator ComboBeat()
        {
            Line("\n## 2. A queen's combo: a click every 0.15 s (the beat between the first four blades starting; 0.68 s before and after R808)");
            Clear();
            yield return Wait(.7f);
            var starts = new List<float>();
            bool was = false;
            // Clicking on as R670 did: a click while a cut runs waits for its blade (two in that time are one).
            float next = Time.time, stopClicking = Time.time + 2.3f, end = Time.time + 2.7f;
            while (Time.time < end && starts.Count < 4)
            {
                if (Time.time < stopClicking && Time.time >= next) { Click(me, Vector3.forward); next += .15f; }
                yield return new WaitForFixedUpdate();
                Release(me, Vector3.forward);
                bool now = Active(me);
                if (now && !was) starts.Add(Time.fixedTime);
                was = now;
            }
            var gaps = new List<string>();
            bool ok = starts.Count == 4;
            for (int i = 1; i < starts.Count; i++)
            {
                float gap = starts[i] - starts[i - 1];
                gaps.Add(gap.ToString("0.000"));
                ok &= Mathf.Abs(gap - .68f) <= Time.fixedDeltaTime + .001f;
            }
            Check(ok, "four chained cuts, 0.68 s between their blades", $"{starts.Count} blades, gaps {string.Join(" · ", gaps)} s");
            yield return Wait(.5f);
        }

        // ---------------------------------------------------------------- 3. this player's hit

        IEnumerator MyHit(bool sound)
        {
            Line("\n## 3. My queen cuts an enemy (this screen's own hit)");
            Clear();
            Place(enemyA, new Vector3(0f, 0f, .5f), Vector3.back, true);
            yield return Wait(.8f);
            int hits = me.HitsLanded, bites = me.Bites, taken = enemyA.Pawn.Hits;
            int marks = Marks(), body = Count("SwordHitBody"), hit = Count("SwordHit");
            cuts.Clear(); sounds.Clear();
            watched = enemyA; heldFrom = heldFor = holdSet = -1f; longestFrame = 0f;
            float clickAt = Time.time, hitAt = -1f, ageAtHit = 0f;
            int marksAtHit = marks;
            var ages = new List<float>();
            Click(me, Vector3.forward);
            float limit = Time.time + 1.2f;
            while (Time.time < limit)
            {
                yield return new WaitForFixedUpdate();
                Release(me, Vector3.forward);
                if (hitAt < 0f && me.HitsLanded > hits)
                {
                    hitAt = Time.fixedTime; ageAtHit = me.SwingAge;
                    marksAtHit = Marks();   // now: the HUD lets a mark go after 0.3 s
                }
                else if (hitAt >= 0f && ages.Count < 24) ages.Add(me.SwingAge);
            }
            bool landed = hitAt >= 0f && me.HitsLanded == hits + 1 && enemyA.Pawn.Hits == taken + 1;
            Check(landed, "the cut lands once", landed ? $"{hitAt - clickAt:0.000} s after the click (swing age {ageAtHit:0.000} s)" : $"hits landed {me.HitsLanded - hits}, taken {enemyA.Pawn.Hits - taken}");
            if (!landed) yield break;
            Check(me.Bites == bites + 1, "the blade is caught (a bite)", $"bites {me.Bites - bites}");
            // The pace over the bite: BiteTime / step steps after the hit's step.
            float dt = Time.fixedDeltaTime;
            int steps = Mathf.RoundToInt(SwordFightPawn.BiteTime / dt);
            float pace = ages.Count > steps ? (ages[steps - 1] - ageAtHit) / (steps * dt) : -1f;
            // Two steps' margin past the bite (its last step can fall either side of BiteTime).
            float after = ages.Count > steps + 5 ? (ages[steps + 5] - ages[steps + 2]) / (3 * dt) : -1f;
            Check(pace >= 0f && Mathf.Abs(pace - SwordFightPawn.BitePace) < .02f && after > .9f,
                  $"the cut goes on at {SwordFightPawn.BitePace:0.00} of its pace for {SwordFightPawn.BiteTime:0.00} s, then at full pace",
                  $"pace over the {steps} steps after the hit {pace:0.000}, after them {after:0.00}");
            float hold = SwordFightSkillFx.CutHoldFrames / 60f;
            Check(Mathf.Abs(holdSet - hold) < .005f && heldFor > 0f && heldFor <= holdSet + longestFrame + .02f,
                  $"the cut piece's picture is held {SwordFightSkillFx.CutHoldFrames} frames ({hold:0.000} s)",
                  $"asked {holdSet:0.000} s, seen held {heldFor:0.000} s, longest frame meanwhile {longestFrame:0.000} s");
            var mine = cuts.Find(c => c.victim == enemyA);
            Check(mine.victim != null && mine.by == me, "the cut's cutter is known where it is run (Cut(victim, by))",
                  mine.victim != null ? $"by {(mine.by != null ? mine.by.Pawn.DisplayName : "null")}" : "no Cut event");
            Check(marksAtHit == marks + 1, "a hit mark on my screen", $"marks +{marksAtHit - marks} at the hit");
            Check(SwordFightSkillSfx.Viewer(me.Pawn) && !SwordFightSkillSfx.Viewer(ally.Pawn), "this screen's own player is me, not the ally",
                  $"me {SwordFightSkillSfx.Viewer(me.Pawn)}, ally {SwordFightSkillSfx.Viewer(ally.Pawn)}");
            if (sound)
            {
                float gain = Gain("SwordHit");
                Check(Count("SwordHit") == hit + 1 && Count("SwordHitBody") == body + 1, "my hit: the hit sound with its body layer",
                      $"SwordHit +{Count("SwordHit") - hit}, SwordHitBody +{Count("SwordHitBody") - body}, gain {gain:0.000}");
                myHitGain = gain;
            }
            else Skip("my hit's sounds (none loaded in batch mode)");
            watched = null;
            yield return Wait(1.2f);

            // The same cut again for its pictures only (taking one makes a long frame, which would have lengthened the
            // held picture measured above): the moment it bites, and a quarter of a second on.
            Clear();
            Place(enemyA, new Vector3(0f, 0f, .5f), Vector3.back, true);
            yield return Wait(.8f);
            hits = me.HitsLanded;
            Click(me, Vector3.forward);
            limit = Time.time + 1.2f;
            float bitAt = -1f;
            bool later = false;
            while (Time.time < limit)
            {
                yield return new WaitForFixedUpdate();
                Release(me, Vector3.forward);
                if (bitAt < 0f && me.HitsLanded > hits) { bitAt = Time.time; shots.Add((Time.frameCount, Path.Combine(folder, "3_hit_bite.png"))); }
                if (bitAt >= 0f && !later && Time.time >= bitAt + .25f) { later = true; shots.Add((Time.frameCount, Path.Combine(folder, "3_hit_0.25s_on.png"))); }
            }
            yield return Wait(1f);
        }

        float myHitGain = -1f;

        // ---------------------------------------------------------------- 4. someone else's hit

        IEnumerator OthersHit(bool sound)
        {
            Line("\n## 4. The ally dummy cuts the other enemy, far from me (someone else's hit)");
            Clear();
            Place(me, new Vector3(-3.5f, 0f, -3f), Vector3.forward, true);
            Place(ally, new Vector3(3.5f, 0f, -.6f), Vector3.forward, true);
            Place(enemyB, new Vector3(3.5f, 0f, .5f), Vector3.back, true);
            yield return Wait(.8f);
            int marks = Marks(), body = Count("SwordHitBody"), hit = Count("SwordHit"), taken = enemyB.Pawn.Hits;
            cuts.Clear(); sounds.Clear();
            Click(ally, Vector3.forward);
            float limit = Time.time + 1.2f;
            while (Time.time < limit)
            {
                yield return new WaitForFixedUpdate();
                Release(ally, Vector3.forward);
            }
            var theirs = cuts.Find(c => c.victim == enemyB);
            Check(enemyB.Pawn.Hits == taken + 1 && theirs.victim != null && theirs.by == ally, "the ally's cut lands, its cutter known",
                  theirs.victim != null ? $"by {(theirs.by != null ? theirs.by.Pawn.DisplayName : "null")}" : $"no Cut event (taken {enemyB.Pawn.Hits - taken})");
            Check(Marks() <= marks, "no hit mark on my screen for it", $"marks +{Mathf.Max(0, Marks() - marks)}");
            float d = Vector3.Distance(me.Pawn.Hips.position, enemyB.Pawn.bodies[(int)BodyId.Chest].position);
            float heard = SwordFightSkillSfx.Heard(enemyB.Pawn.bodies[(int)BodyId.Chest].position, false);
            float want = Mathf.Lerp(SwordFightSkillSfx.OthersNear, SwordFightSkillSfx.OthersFar,
                Mathf.InverseLerp(SwordFightSkillSfx.OthersNearTo, SwordFightSkillSfx.OthersFarFrom, d));
            Check(Mathf.Abs(heard - want) < .01f && SwordFightSkillSfx.Heard(Vector3.zero, true) == 1f,
                  "someone else's sounds are quieter by how far they are (mine at full level)", $"{d:0.0} m away → {heard:0.00} of the level (mine 1.00)");
            if (sound)
            {
                float gain = Gain("SwordHit");
                Check(Count("SwordHit") == hit + 1 && Count("SwordHitBody") == body && myHitGain > 0f && Mathf.Abs(gain / myHitGain - heard) < .02f,
                      "someone else's hit: no body layer, quieter", $"SwordHit +{Count("SwordHit") - hit}, SwordHitBody +{Count("SwordHitBody") - body}, gain {gain:0.000} = {(myHitGain > 0f ? gain / myHitGain : 0f):0.00} of mine");
            }
            else Skip("someone else's hit sound (none loaded in batch mode)");
            yield return Wait(1.2f);
        }

        // ---------------------------------------------------------------- 5. the protection shell

        IEnumerator ShellBlock(bool sound)
        {
            Line("\n## 5. My queen cuts an enemy under its respawn protection (the shell)");
            Clear();
            Place(enemyA, new Vector3(0f, 0f, .5f), Vector3.back, false);   // protected: SwordFightPawn.Respawn's 1.25 s
            yield return Wait(.25f);
            int blocks = me.Blocks, taken = enemyA.Pawn.Hits, rang = Count("SwordBlocked");
            moments.Clear();
            Check(enemyA.Protection > .5f, "the enemy is under its protection", $"protection {enemyA.Protection:0.00} s");
            float blockedAt = -1f, ageAtBlock = 0f;
            var ages = new List<float>();
            Click(me, Vector3.forward);
            float limit = Time.time + .8f;
            while (Time.time < limit)
            {
                yield return new WaitForFixedUpdate();
                Release(me, Vector3.forward);
                if (blockedAt < 0f && me.Blocks > blocks)
                {
                    blockedAt = Time.fixedTime; ageAtBlock = me.SwingAge;
                    shots.Add((Time.frameCount, Path.Combine(folder, "5_shell_0f.png")));
                    shots.Add((Time.frameCount + 4, Path.Combine(folder, "5_shell_4f.png")));
                }
                else if (blockedAt >= 0f && ages.Count < 10) ages.Add(me.SwingAge);
            }
            Check(me.Blocks == blocks + 1 && enemyA.Pawn.Hits == taken, "the cut is stopped on the shell once and the piece is not hit",
                  $"blocks +{me.Blocks - blocks}, hits taken +{enemyA.Pawn.Hits - taken}");
            int moment = moments.FindIndex(e => e.kind == SfFxKind.SwordBlocked);
            if (moment >= 0)
            {
                var e = moments[moment];
                Vector3 centre = enemyA.Pawn.Hips.position + Vector3.up * SwordFightPawn.ShellLift;
                float r = Vector3.Distance(e.at, centre);
                Check(e.target == enemyA.Pawn && e.by != null && e.by.Fighter == me && Mathf.Abs(r - SwordFightPawn.ShellRadius) < .25f,
                      "the moment SwordBlocked is raised (sent to clients like a skill's), on the shell",
                      $"target {(e.target != null ? e.target.DisplayName : "null")}, by {(e.by != null && e.by.Fighter != null ? e.by.Fighter.Pawn.DisplayName : "null")}, {r:0.00} m from the shell's centre now");
            }
            else Check(false, "the moment SwordBlocked is raised", "none");
            float dt = Time.fixedDeltaTime;
            int steps = Mathf.RoundToInt(SwordFightPawn.BlockBiteTime / dt);
            float pace = ages.Count >= steps ? (ages[steps - 1] - ageAtBlock) / (steps * dt) : -1f;
            Check(pace >= 0f && Mathf.Abs(pace - SwordFightPawn.BitePace) < .03f, $"the blade is caught {SwordFightPawn.BlockBiteTime:0.00} s on it",
                  $"pace over {steps} steps {pace:0.000}");
            var shells = ShellField != null ? ShellField.GetValue(match) as IDictionary : null;
            Check(shells != null && shells.Contains(enemyA), "its bubble flares (SwordFightMatch.Board)", shells != null ? $"{shells.Count} shell(s) struck" : "no field");
            if (sound) Check(Count("SwordBlocked") == rang + 1, "the shell rings", $"SwordBlocked +{Count("SwordBlocked") - rang}");
            else Skip("the shell's ring (none loaded in batch mode)");
            enemyA.EndProtection();
            yield return Wait(1.2f);
        }

        // ---------------------------------------------------------------- 6. a client's side

        IEnumerator ClientSide()
        {
            Line("\n## 6. A client's side: the ally (cutter) and the second enemy (cut) made puppets, fed made-up host frames");
            Clear();
            Place(ally, new Vector3(2f, 0f, 2f), Vector3.forward, true);
            Place(enemyB, new Vector3(2f, 0f, 3f), Vector3.back, true);
            yield return Wait(.6f);
            var c = ally; var d = enemyB;

            // a) the strike and the cut in one frame, the cut 20 ms ahead of the bodies drawn (the cut's own fighter first).
            yield return Quiet(c, d, 10000);
            Frame(d, .29f, 10033, 10003);
            Frame(c, .09f, 10033, 10003);
            float sent = Time.unscaledTime;
            bool heldEarly = d.Pawn.VisualHeld;
            yield return CutOn(d, .5f);
            bool heldAfter = d.Pawn.VisualHeld;
            var a = cuts.Find(x => x.victim == d);
            float waited = a.victim != null ? a.at - sent : -1f;
            Check(a.victim != null && a.by == c && waited >= .018f && waited <= .07f,
                  "a cut waits until the bodies drawn here reach it (20 ms on), its cutter found by the strike's time",
                  a.victim != null ? $"played {waited * 1000f:0} ms after its frame came, by {Name(a.by)}" : "no Cut event");
            Check(!heldEarly && heldAfter, "the cut piece's picture is held from that moment, not from the frame's coming",
                  $"held when the frame came: {heldEarly}, just after the cut played: {heldAfter}");
            yield return Wait(.3f);

            // b) a cut whose strike was not seen (its state packets lost): the nearest foe's blade (the ally's, 1 m off).
            yield return Quiet(c, d, 20000);
            Frame(d, .29f, 20033, 20003);
            yield return CutOn(d, .5f);
            var b = cuts.Find(x => x.victim == d);
            Check(b.victim != null && b.by == c, "a cut without its strike seen falls back to the nearest blade",
                  b.victim != null ? $"by {Name(b.by)}" : "no Cut event");
            yield return Wait(.3f);

            // c) a cut far ahead of the bodies (300 ms): it waits at most 0.1 s.
            yield return Quiet(c, d, 30000);
            Frame(d, .29f, 30333, 30033);
            Frame(c, .09f, 30333, 30033);
            sent = Time.unscaledTime;
            yield return CutOn(d, .6f);
            var cc = cuts.Find(x => x.victim == d);
            waited = cc.victim != null ? cc.at - sent : -1f;
            Check(cc.victim != null && waited >= .095f && waited <= .15f && cc.by == c, "a cut never waits more than 0.1 s",
                  cc.victim != null ? $"played {waited * 1000f:0} ms on, by {Name(cc.by)}" : "no Cut event");
            yield return Wait(.3f);

            // d) no times (the old lobby link, a host change): this frame, as before.
            yield return Quiet(c, d, 0);
            Frame(d, .29f, 0, 0);
            sent = Time.unscaledTime;
            int frame = Time.frameCount;
            yield return CutOn(d, .5f);
            var dd = cuts.Find(x => x.victim == d);
            Check(dd.victim != null && Time.frameCount - frame <= 1, "without times a cut plays the same frame",
                  dd.victim != null ? $"{(dd.at - sent) * 1000f:0} ms on, by {Name(dd.by)}" : "no Cut event");
            yield return Wait(.3f);

            // The cutter taken 9 m away: no blade near the cut piece, so only the strike's time can name it.
            c.Respawn(new Vector3(-4f, top, -4f), Vector3.forward, true);
            c.EndProtection();
            yield return Wait(.5f);

            // e) far, its strike at the cut's moment: still its cutter (by time, not by distance).
            yield return Quiet(c, d, 40000);
            Frame(d, .29f, 40033, 40003);
            Frame(c, .09f, 40033, 40003);
            yield return CutOn(d, .5f);
            var e = cuts.Find(x => x.victim == d);
            Check(e.victim != null && e.by == c, "a cutter 9 m off whose strike was at the cut's moment is found",
                  e.victim != null ? $"by {Name(e.by)}" : "no Cut event");
            yield return Wait(.3f);

            // f) far, its strike 60 ms before the cut: not one blow (StrikeMatchMs 30) and no blade near - no cutter.
            yield return Quiet(c, d, 50000);
            Frame(c, .09f, 49973, 49943);   // a strike at 49963
            yield return null;
            Frame(d, .29f, 50033, 50003);   // the cut at 50023, 60 ms after it
            yield return CutOn(d, .5f);
            var f = cuts.Find(x => x.victim == d);
            Check(f.victim != null && f.by == null, "a strike 60 ms off the cut is not its cutter",
                  f.victim != null ? $"by {Name(f.by)}" : "no Cut event");
            yield return Wait(.3f);

            // g) far, no strike: no cutter (the old guess, the nearest blade within 2.5 m, finds none either).
            yield return Quiet(c, d, 60000);
            Frame(d, .29f, 60033, 60003);
            yield return CutOn(d, .5f);
            var g = cuts.Find(x => x.victim == d);
            Check(g.victim != null && g.by == null, "no strike and no blade near: no cutter",
                  g.victim != null ? $"by {Name(g.by)}" : "no Cut event");
        }

        // A made-up frame for a puppet: its host time and the host time the bodies are drawn at here.
        static void Frame(SwordFightPawn f, float flash, double frameMs, double drawnMs) =>
            f.ApplyRemote(true, false, 0f, f.WeaponOffset, f.WeaponRotation, 0f, flash, true, frameMs, drawnMs);

        // Both puppets' flashes back to nothing (so the next one is a jump), a frame, the cuts heard so far forgotten.
        IEnumerator Quiet(SwordFightPawn c, SwordFightPawn d, double atMs)
        {
            Frame(c, 0f, atMs, atMs > 0 ? atMs - 10 : 0);
            Frame(d, 0f, atMs, atMs > 0 ? atMs - 10 : 0);
            yield return null;
            cuts.Clear();
        }

        static string Name(SwordFightPawn f) => f != null && f.Pawn != null ? f.Pawn.DisplayName : "null";

        IEnumerator CutOn(SwordFightPawn victim, float seconds)
        {
            float limit = Time.unscaledTime + seconds;
            while (Time.unscaledTime < limit && !cuts.Exists(x => x.victim == victim)) yield return null;
        }

        // ---------------------------------------------------------------- helpers

        /// <summary>Everyone back where the checks want them: me facing the far side at the middle, the others well away.</summary>
        void Clear()
        {
            Place(me, new Vector3(0f, 0f, -.6f), Vector3.forward, true);
            Place(ally, new Vector3(-4f, 0f, -4f), Vector3.forward, true);
            Place(enemyA, new Vector3(-4f, 0f, 4f), Vector3.back, true);
            Place(enemyB, new Vector3(4f, 0f, 4f), Vector3.back, true);
        }

        void Place(SwordFightPawn f, Vector3 at, Vector3 facing, bool unprotected)
        {
            f.Respawn(new Vector3(at.x, top, at.z), facing, true);
            if (unprotected) f.EndProtection();
            Release(f, facing);
        }

        static void Click(SwordFightPawn f, Vector3 aim)
        {
            f.SetInput(new PawnInput { aim = aim, ability2 = true });
            f.SetInput(new PawnInput { aim = aim, ability2 = true, shove = true, shoveHeld = true });
        }

        static void Release(SwordFightPawn f, Vector3 aim) => f.SetInput(new PawnInput { aim = aim, ability2 = true });

        static bool Active(SwordFightPawn f) => ActiveField != null && (bool)ActiveField.GetValue(f);

        int Marks() => MarksField != null && MarksField.GetValue(match) is ICollection list ? list.Count : -1;

        static int Count(string name) =>
            SwordFightSkillSfx.Current != null && SwordFightSkillSfx.Current.Count.TryGetValue(name, out int n) ? n : 0;

        float Gain(string clip)
        {
            float best = -1f;
            foreach (var (name, gain) in sounds) if (name == clip) best = Mathf.Max(best, gain);
            return best;
        }

        static IEnumerator Wait(float seconds)
        {
            float until = Time.time + seconds;
            while (Time.time < until) yield return new WaitForFixedUpdate();
        }

        void MakeShooter()
        {
            shooter = new GameObject("R808 side camera").AddComponent<Camera>();
            shooter.enabled = false;
            shooter.fieldOfView = 40f;
            shooter.nearClipPlane = .05f;
            shooter.farClipPlane = 200f;
            shooter.transform.position = new Vector3(2.6f, top + 1.05f, -.05f);
            shooter.transform.LookAt(new Vector3(0f, top + .6f, -.05f));
        }

        void Shoot(string file)
        {
            if (shooter == null) return;
            var rt = RenderTexture.GetTemporary(960, 540, 24, RenderTextureFormat.ARGB32);
            var was = RenderTexture.active;
            try
            {
                shooter.targetTexture = rt;
                shooter.Render();
                RenderTexture.active = rt;
                var tex = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false);
                tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
                tex.Apply();
                File.WriteAllBytes(file, tex.EncodeToPNG());
                Destroy(tex);
            }
            catch (Exception e) { Line("(picture " + Path.GetFileName(file) + " not taken: " + e.Message + ")"); }
            finally
            {
                shooter.targetTexture = null;
                RenderTexture.active = was;
                RenderTexture.ReleaseTemporary(rt);
            }
        }

        void Check(bool ok, string what, string detail)
        {
            if (ok) passed++; else failed++;
            Line($"{(ok ? "PASS" : "FAIL")} {what} — {detail}");
        }

        void Skip(string what)
        {
            skipped++;
            Line("SKIP " + what);
        }

        void Line(string text)
        {
            report.AppendLine(text);
            Debug.Log("[SwordHitFeel] " + text);
        }

        void Finish()
        {
            Unhook();
            Line($"\nRESULT {passed} passed · {failed} failed · {skipped} skipped. Pictures: {folder}");
            try { File.WriteAllText(Path.Combine(folder, "report.txt"), report.ToString(), new UTF8Encoding(false)); }
            catch (Exception e) { Debug.LogError("[SwordHitFeel] report not written: " + e.Message); }
            Failures = failed;
            Done = true;
        }
    }
}
