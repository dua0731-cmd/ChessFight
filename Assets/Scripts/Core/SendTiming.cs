using System;

namespace ChessFight.Network
{
    /// <summary>
    /// R306 (HOST_AUTHORITY §5): a development build can put the latency changes back as they were, item by item, so the
    /// before and after are measured with one build on the same PC and line: "-latencyBefore=1235" - [1] Pawn Rush's
    /// snapshots on "an interval since the last send" (the lab's on its schedule without the early quarter), [2] a client's
    /// input on its old 60 Hz timing only, [3] the host reading its players' inputs in Update, [5] 30 snapshots a second
    /// whatever the players. "-latencyBefore" alone is all four. The measurements ([0]) stay as they are.
    /// </summary>
    public struct LatencyBefore
    {
        public const string Argument = "-latencyBefore";
        public bool Slipping, SlotsOnly, ReadInUpdate, Always30;
        public bool Any => Slipping || SlotsOnly || ReadInUpdate || Always30;

        /// <summary>The command line's ask; nothing when it is absent.</summary>
        public static LatencyBefore Parse(string[] args)
        {
            var before = new LatencyBefore();
            if (args == null) return before;
            foreach (string arg in args)
            {
                if (arg == Argument) return new LatencyBefore { Slipping = true, SlotsOnly = true, ReadInUpdate = true, Always30 = true };
                if (arg == null || !arg.StartsWith(Argument + "=", StringComparison.Ordinal)) continue;
                string items = arg.Substring(Argument.Length + 1);
                before.Slipping = items.IndexOf('1') >= 0;
                before.SlotsOnly = items.IndexOf('2') >= 0;
                before.ReadInUpdate = items.IndexOf('3') >= 0;
                before.Always30 = items.IndexOf('5') >= 0;
            }
            return before;
        }

        /// <summary>The items put back, for the metrics box ("" when none).</summary>
        public string Items => (Slipping ? "[1]" : "") + (SlotsOnly ? "[2]" : "") + (ReadInUpdate ? "[3]" : "") + (Always30 ? "[5]" : "");
    }

    /// <summary>
    /// R306 (HOST_AUTHORITY §5): a send on a fixed schedule - every interval from the last due time - rather than "an
    /// interval since the last send". The second form slips to the next frame every time: Pawn Rush's 30 Hz snapshots went
    /// out about every 50 ms at 60 fps until R306. It sends once a frame at most, so a host below the rate sends every
    /// frame. A frame late by less than an interval keeps the beat, so the next frame may take the next send; a longer
    /// hitch starts the schedule over instead of bursting to catch up. A frame up to a quarter interval early sends too:
    /// at 60 sends a second and 60 fps a frame a hair early otherwise skipped its send and the next one's.
    /// </summary>
    public struct SendSchedule
    {
        /// <summary>How early (a share of the interval) a frame may take its send.</summary>
        public const double Early = 0.25;
        double next;

        /// <summary>Whether a send is due at <paramref name="now"/> (seconds); when it is, the next one is booked.
        /// <paramref name="early"/>: how early a frame may take it (0: the schedule before R306).</summary>
        public bool Due(double now, double interval, double early = Early)
        {
            if (now < next - interval * early) return false;
            next = Book(next, now, interval, early);
            return true;
        }

        /// <summary>The send after one due at <paramref name="due"/> and taken at <paramref name="now"/>. Starting over
        /// after any late frame (the schedule before R306, early 0) halved the rate in uneven frames near it - a short
        /// frame, then a long one: the long one started over and the short one after it was too early (a loopback host
        /// at about 57 fps sent 30 snapshots a second of 60). Now only a frame late by more than an interval starts over.</summary>
        internal static double Book(double due, double now, double interval, double early)
        {
            double next = due + interval;
            return next <= now - (early > 0d ? interval : 0d) ? now + interval : next;
        }

        public void Reset() { next = 0; }
    }

    /// <summary>
    /// R306 [2]: when a client sends its input. On the 60 Hz schedule, and in the frame the input changes (a new press, a
    /// button let go, the move started, stopped or turned: InputChange) instead of waiting up to 1/60 s for the next slot.
    /// Never two within 1/120 s; a send out of turn books the next slot a whole interval on. No new packet: the host takes
    /// these like any other input.
    /// </summary>
    public struct InputSendGate
    {
        public const double Interval = 1.0 / 60, MinGap = 1.0 / 120;
        double next, last;
        bool sent;

        /// <summary>Whether to send at <paramref name="now"/> (seconds, once a frame); <paramref name="changed"/>: the input
        /// differs from the last one sent. <paramref name="early"/>: as SendSchedule's (0: the slots before R306).</summary>
        public bool Due(double now, bool changed, double early = SendSchedule.Early)
        {
            bool scheduled = now >= next - Interval * early;
            bool outOfTurn = !scheduled && changed && (!sent || now - last >= MinGap);
            if (!scheduled && !outOfTurn) return false;
            next = scheduled ? SendSchedule.Book(next, now, Interval, early) : now + Interval;
            last = now;
            sent = true;
            return true;
        }

        public void Reset()
        {
            next = last = 0;
            sent = false;
        }
    }

    /// <summary>R306 [2]: whether the move held now is a new one against the one last sent - it started or stopped, or it
    /// turned by <see cref="TurnDegrees"/> or more. A camera turning under a held key changes the move a little every frame;
    /// that waits for the schedule.</summary>
    public static class InputChange
    {
        /// <summary>A move shorter than this is standing still.</summary>
        public const float DeadZone = 0.1f;
        public const double TurnDegrees = 20;

        public static bool Moved(float sentX, float sentY, float x, float y)
        {
            bool was = sentX * sentX + sentY * sentY > DeadZone * DeadZone;
            bool now = x * x + y * y > DeadZone * DeadZone;
            if (was != now) return true;
            if (!now) return false;
            double turn = Math.Abs(Math.Atan2((double)sentX * y - (double)sentY * x, (double)sentX * x + (double)sentY * y));
            return turn * (180 / Math.PI) >= TurnDegrees;
        }
    }

    /// <summary>
    /// R306 [5] (HOST_AUTHORITY P2): how often the host sends its snapshots. 60 a second while its snapshots alone cost its
    /// upload no more than <see cref="FastBudgetBytes"/> at that rate - up to six players (5 clients x 420 B x 60 = 123
    /// KB/s, half of a full twelve's 261 KB/s at 30), or two to three players with bots filling up to twelve - else 30.
    /// A client's snapshot wait halves (17 to 8 ms) and its playback floor with it (70 to 37 ms, PlaybackDelay). Clients
    /// read the rate from the snapshots' host times: nothing on the wire changes.
    /// </summary>
    public static class SnapshotRate
    {
        public const double Fast = 1.0 / 60, Normal = 1.0 / 30;
        public const int FastBudgetBytes = 128 * 1024;

        /// <summary>The interval for snapshots of <paramref name="pawns"/> pawns (bots too) to <paramref name="receivers"/>
        /// clients.</summary>
        public static double Interval(int receivers, int pawns) =>
            (long)Math.Max(0, receivers) * SnapshotBytes(pawns) * 60 <= FastBudgetBytes ? Fast : Normal;

        /// <summary>A ragdoll snapshot of <paramref name="pawns"/> pawns with every hook put away (RagdollWire).</summary>
        public static int SnapshotBytes(int pawns) => RagdollWire.HeaderBytes + Math.Max(0, pawns) * RagdollWire.PoseBytes;
    }

    /// <summary>R306 [0]: the last few of a latency (ms) for the metrics box, and their average.</summary>
    public sealed class LatencyAverage
    {
        readonly double[] ring;
        int next, count;

        public LatencyAverage(int size) { ring = new double[Math.Max(1, size)]; }

        public int Count => count;
        /// <summary>The newest sample, -1 = none yet.</summary>
        public double Last { get; private set; } = -1;

        /// <summary>The samples kept, averaged (-1 = none yet).</summary>
        public double Average
        {
            get
            {
                int n = Math.Min(count, ring.Length);
                if (n == 0) return -1;
                double sum = 0;
                for (int i = 0; i < n; i++) sum += ring[i];
                return sum / n;
            }
        }

        public void Add(double ms)
        {
            ring[next] = ms;
            next = (next + 1) % ring.Length;
            count++;
            Last = ms;
        }

        public void Clear()
        {
            next = count = 0;
            Last = -1;
        }
    }

    /// <summary>
    /// R306 [0]: a client's press timed for the metrics box, one at a time, on the local clock (ms). Answered: the first
    /// snapshot whose echoed input time - the newest input the host has played in a physics step (RagdollWire.AckAge) - is
    /// the press's or later: the input's wait to be sent, its way there, the host's wait for its physics step and for its
    /// next snapshot, the way back. Seen: the moment this PC draws its pawn at that snapshot's host time, the playback
    /// delay on top. A press the host does not answer within <see cref="GiveUpMs"/> is dropped (counted in Lost).
    /// </summary>
    public sealed class PressTimer
    {
        public const double GiveUpMs = 1500;
        public readonly LatencyAverage Answered = new LatencyAverage(16), Seen = new LatencyAverage(16);
        public int Lost { get; private set; }
        double pressAt = -1;
        uint answerHostMs;
        bool answered;

        /// <summary>A press at <paramref name="nowMs"/>; one while another is still timed is not timed.</summary>
        public void Pressed(double nowMs)
        {
            if (pressAt >= 0) return;
            pressAt = nowMs;
            answered = false;
        }

        /// <summary>A snapshot stamped <paramref name="hostMs"/> arrived at <paramref name="nowMs"/>, echoing this PC's input sent
        /// at <paramref name="echoMs"/> (negative: no echo).</summary>
        public void Arrived(double nowMs, double echoMs, uint hostMs)
        {
            if (pressAt < 0 || answered) return;
            if (echoMs >= pressAt)
            {
                Answered.Add(nowMs - pressAt);
                answerHostMs = hostMs;
                answered = true;
            }
            else GiveUp(nowMs);
        }

        /// <summary>This frame draws this PC's pawn at host time <paramref name="drawnHostMs"/>.</summary>
        public void Drawn(double nowMs, double drawnHostMs)
        {
            if (pressAt < 0) return;
            if (answered && drawnHostMs >= answerHostMs)
            {
                Seen.Add(nowMs - pressAt);
                pressAt = -1;
                answered = false;
            }
            else GiveUp(nowMs);
        }

        void GiveUp(double nowMs)
        {
            if (nowMs - pressAt <= GiveUpMs) return;
            Lost++;
            pressAt = -1;
            answered = false;
        }

        public void Reset()
        {
            pressAt = -1;
            answered = false;
            Lost = 0;
            Answered.Clear();
            Seen.Clear();
        }
    }
}
