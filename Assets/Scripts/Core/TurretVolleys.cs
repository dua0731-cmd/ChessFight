using System;

namespace ChessFight.Network
{
    // R616 (the user, 11-12, the round's last section: "4개의 포탑은 3초마다 한 번씩 랜덤한 포탑 하나가 공격하게 해줘. 직전에
    // 공격한 포탑은 연속으로 공격하지 않게 해줘"): which of the war room's turrets fires each volley.
    //
    // A volley every Period seconds of the shared obstacle clock; volley n runs from n * Period. One turret fires it,
    // drawn from the round's seed and the volley's number, never the one that fired the volley before. Nothing is sent:
    // every PC works the same turret out of the same seed and clock (HANDOFF rule 7), at any time on the clock, however
    // long the match has run and on a PC that has only just come (back) in - the answer for a volley never depends on
    // more than the fifteen volleys before it.
    //
    // How: the volleys go in blocks of Block. A block's first turret is drawn on its own; each one after it is one of the
    // others, drawn; the block's last is one of those that is neither the one before it nor the next block's first. So
    // no turret fires twice running, inside a block or across two, and every turret comes up as often as the others.
    // (Two turrets can only take turns, one turret always fires.)
    public static class TurretVolleys
    {
        public const double Period = 3.0;
        const int Block = 16;

        // The volley a time on the shared clock is in, and when a volley begins.
        public static long Volley(double clock) => (long)Math.Floor(clock / Period);
        public static double Start(long volley) => volley * Period;

        // The turret (0 .. turrets - 1) that fires a volley.
        public static int Turret(int seed, long volley, int turrets)
        {
            if (turrets <= 1) return 0;
            if (turrets == 2) return (int)((((volley % 2) + 2) % 2 + First(seed, 0, 2)) % 2);
            long block = FloorDiv(volley, Block);
            int at = (int)(volley - block * Block);
            int turret = First(seed, block, turrets);
            for (int i = 1; i <= at; i++)
            {
                uint roll = Mix(seed, block * Block + i, 0x51u);
                if (i < Block - 1)
                {
                    turret = (turret + 1 + (int)(roll % (uint)(turrets - 1))) % turrets;
                    continue;
                }
                // The block's last: not the one before it, not the one that opens the next block.
                int before = turret, next = First(seed, block + 1, turrets);
                int pick = (int)(roll % (uint)(before == next ? turrets - 1 : turrets - 2));
                for (int t = 0; t < turrets; t++)
                {
                    if (t == before || t == next) continue;
                    if (pick-- == 0) { turret = t; break; }
                }
            }
            return turret;
        }

        static int First(int seed, long block, int turrets) => (int)(Mix(seed, block, 0xA7u) % (uint)turrets);

        static uint Mix(int seed, long number, uint salt)
        {
            unchecked
            {
                uint a = SeededSchedule.Hash((uint)seed, salt);
                return SeededSchedule.Hash(SeededSchedule.Hash(a, (uint)(number >> 32)), (uint)number);
            }
        }

        static long FloorDiv(long a, long b)
        {
            long q = a / b;
            return a % b != 0 && (a < 0) != (b < 0) ? q - 1 : q;
        }
    }
}
