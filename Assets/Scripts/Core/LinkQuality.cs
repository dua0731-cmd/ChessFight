using System;
using System.Collections.Generic;

namespace ChessFight.Network
{
    // How long the host has been silent, as the player should feel it. The host
    // sends a snapshot every 50 ms, so half a second of nothing is already ten
    // lost snapshots in a row.
    public enum LinkHealth { Ok, Unstable, Frozen, Lost }

    public static class LinkMonitor
    {
        public const float UnstableAfter = .5f, FreezeAfter = 2f, LostAfter = 12f;
        public static LinkHealth Classify(float silence) =>
            silence >= LostAfter ? LinkHealth.Lost :
            silence >= FreezeAfter ? LinkHealth.Frozen :
            silence >= UnstableAfter ? LinkHealth.Unstable : LinkHealth.Ok;
    }

    // Input-to-confirmation time: from sending an input to the snapshot that
    // acknowledges it. This is what a player feels, so it includes the host's
    // tick and snapshot spacing on top of the wire ping.
    public sealed class ResponseTimer
    {
        struct Pending { public uint Sequence; public double At; }
        readonly Queue<Pending> pending = new Queue<Pending>();
        public double Milliseconds { get; private set; } = -1;

        public void Sent(uint sequence, double now)
        {
            pending.Enqueue(new Pending { Sequence = sequence, At = now });
            while (pending.Count > 240) pending.Dequeue();
        }

        public void Acknowledged(uint ack, double now)
        {
            double sent = -1;
            while (pending.Count > 0 && !MotionProtocol.Newer(pending.Peek().Sequence, ack))
                sent = pending.Dequeue().At;
            if (sent < 0) return;
            double sample = (now - sent) * 1000;
            // Smoothed like TCP's RTT estimate, so one late snapshot does not flicker the number.
            Milliseconds = Milliseconds < 0 ? sample : Milliseconds + (sample - Milliseconds) / 8;
        }

        public void Clear() { pending.Clear(); Milliseconds = -1; }
    }

    // Development aid: adds delay and loss to this machine's packets in both
    // directions, so one tester can play on a bad connection. Half the extra
    // round trip is added on the way out and half on the way in.
    public struct LinkProfile
    {
        public int RoundTripMs, LossPercent;
        public bool Active => RoundTripMs > 0 || LossPercent > 0;
        public override string ToString() => Active ? $"+{RoundTripMs}ms / 손실 {LossPercent}%" : "꺼짐";
        public static readonly LinkProfile[] Presets =
        {
            new LinkProfile(),
            new LinkProfile { RoundTripMs = 100 },
            new LinkProfile { RoundTripMs = 200, LossPercent = 5 },
            new LinkProfile { RoundTripMs = 300, LossPercent = 10 },
        };
    }

    public sealed class LinkSimulator<T>
    {
        struct Entry { public double Due; public T Item; }
        readonly List<Entry> queue = new List<Entry>();
        readonly Random random;
        public LinkProfile Profile;

        public LinkSimulator(int seed = 0) { random = seed == 0 ? new Random() : new Random(seed); }

        // False when the packet is dropped. Otherwise it waits in the queue.
        public bool Push(T item, double now)
        {
            if (Profile.LossPercent > 0 && random.Next(100) < Profile.LossPercent) return false;
            queue.Add(new Entry { Due = now + Profile.RoundTripMs / 2000.0, Item = item });
            return true;
        }

        // Due items in the order they were pushed; the delay is constant, so
        // the simulator never reorders.
        public void Release(double now, Action<T> deliver)
        {
            int n = 0;
            while (n < queue.Count && queue[n].Due <= now) n++;
            if (n == 0) return;
            var due = queue.GetRange(0, n);
            queue.RemoveRange(0, n);
            foreach (var entry in due) deliver(entry.Item);
        }

        public int Count => queue.Count;
        public void Clear() => queue.Clear();
    }
}
