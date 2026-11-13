using System;
using System.Collections.Generic;
using UnityEngine;

namespace ChessFight.Network
{
    /// <summary>
    /// A received message whose handling threw (R297). Every Steam receive loop catches it for that one message, goes on
    /// with the next and releases each one; it is logged here, the first at once and then at most once a second per
    /// channel with how many failed in between, so a broken handler or a peer's junk cannot flood the log.
    /// </summary>
    public static class ReceiveErrors
    {
        sealed class Tally
        {
            public float LoggedAt = float.NegativeInfinity;
            public int Since;
        }

        static readonly Dictionary<int, Tally> tallies = new Dictionary<int, Tally>();

        public static void Log(int channel, Exception error)
        {
            if (!tallies.TryGetValue(channel, out var tally)) tallies[channel] = tally = new Tally();
            float now = Time.realtimeSinceStartup;
            if (now - tally.LoggedAt < 1f)
            {
                tally.Since++;
                return;
            }
            // The channel on every logged failure, so a tester finds them all in Player.log by "Steam channel".
            Debug.LogWarning(tally.Since > 0
                ? $"Steam channel {channel}: a received message failed and was dropped ({tally.Since} more since the last one logged)."
                : $"Steam channel {channel}: a received message failed and was dropped.");
            Debug.LogException(error);
            tally.LoggedAt = now;
            tally.Since = 0;
        }
    }
}
