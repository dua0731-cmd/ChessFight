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
        // R111: up to this much more one-way delay, at random per packet, so packets can
        // overtake each other (the skills' moments and presses must survive that, C5).
        public int JitterMs;
        public bool Active => RoundTripMs > 0 || LossPercent > 0 || JitterMs > 0;
        public override string ToString() => Active ? $"+{RoundTripMs}ms / 손실 {LossPercent}%" + (JitterMs > 0 ? $" / 흔들림 {JitterMs}ms" : "") : "꺼짐";
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
        readonly List<Entry> due = new List<Entry>();
        readonly Random random;
        public LinkProfile Profile;

        public LinkSimulator(int seed = 0) { random = seed == 0 ? new Random() : new Random(seed); }

        // False when the packet is dropped. Otherwise it waits in the queue.
        public bool Push(T item, double now)
        {
            if (Profile.LossPercent > 0 && random.Next(100) < Profile.LossPercent) return false;
            double jitter = Profile.JitterMs > 0 ? random.NextDouble() * Profile.JitterMs / 1000.0 : 0;
            queue.Add(new Entry { Due = now + Profile.RoundTripMs / 2000.0 + jitter, Item = item });
            return true;
        }

        // Due items, earliest first. Without jitter the delay is constant and that is the
        // order they were pushed in; with jitter a later packet can come out first.
        public void Release(double now, Action<T> deliver)
        {
            due.Clear();
            for (int i = 0; i < queue.Count; i++)
                if (queue[i].Due <= now) due.Add(queue[i]);
            if (due.Count == 0) return;
            queue.RemoveAll(e => e.Due <= now);
            // Stable: equal times keep the order they were pushed in.
            var ordered = new List<Entry>(due);
            for (int i = 1; i < ordered.Count; i++)
            {
                var item = ordered[i];
                int j = i - 1;
                while (j >= 0 && ordered[j].Due > item.Due) { ordered[j + 1] = ordered[j]; j--; }
                ordered[j + 1] = item;
            }
            foreach (var entry in ordered) deliver(entry.Item);
        }

        public int Count => queue.Count;
        public void Clear() => queue.Clear();
    }
}
