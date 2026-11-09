using System;

namespace ChessFight.Network
{
    // Mini-game B, the plank bridge, as numbers (R528, user 11-09 list 2: "나무판자는 잡기로 잡아 옮길 수 있게, 상대방 것도 가능,
    // 일정 미니게임 지역 이탈 시 자동으로 원래 자리로 복구, 나무 판자 색 구분 가능하도록 양 팀이 다르게, 잡아서 판자 설치해야하는
    // 자리에 놓으면 자동 건설되도록. 판정은 적당히"). No Unity: the host's MgPlanks plays it with these rules and sends a
    // PlankBoard, every other PC draws one, the tests run both.
    //
    // The game's frame (floor top at 0, z away from the entry, x across, the same on every PC): a 10 m chasm across
    // the station at z 6..16, ten 2.5 x 2 m slots over it in two columns (x -2.5..0 and 0..2.5) and five rows, nearest
    // row first, and ten planks of the team's own, five on each of two piles at x -8 / +8, z 3.5. A plank is held
    // in front of the pawn that took it; it is laid in the free slot under it when it is put down there or held there
    // for HoverSeconds, it lies where it is put down on a floor, and it goes back on its own pile when it falls into
    // the chasm or leaves the station.
    public static class PlankBridge
    {
        public const int Rows = 5, Columns = 2, Slots = Rows * Columns;
        public const int Piles = 2, PerPile = 5, Planks = Piles * PerPile;
        public const float Chasm0 = 6f, Row = 2f, PlankWidth = 2.5f, Thickness = .12f;
        public const float Chasm1 = Chasm0 + Rows * Row;
        public const float PileX = 8f, PileZ = 3.5f, PileStep = .14f;
        // "판정은 적당히": a plank whose centre comes this close to a free slot's centre (across, along) is laid there.
        public const float SnapAcross = 1.5f, SnapAlong = 1.5f;
        // Held over a free slot this long, it is laid without being let go.
        public const float HoverSeconds = .5f;
        // A hand this close to a plank (or a pile) with the grab button down takes it: a plank lying on the floor is
        // under the hands' reach (the hands of a standing pawn are about 0.45 m up, the grab reaches 0.25 m).
        public const float PickReach = .5f;
        // The station (u 0..20, v -10..10) and a little more: a plank put down outside it goes back to its pile.
        public const float AreaHalf = 10.3f, AreaNear = -.3f, AreaFar = 20.3f;
        // A carried plank is held out in front, so it may be this much further out while its carrier is still in the
        // station (taken off a pile with the station's edge behind it); the carrier leaving sends it home anyway.
        public const float CarryMargin = 1.3f;

        public static int PileOf(int plank) => plank / PerPile;
        public static float PileCentreX(int pile) => pile == 0 ? -PileX : PileX;

        public static float SlotX(int slot) => (slot % Columns == 0 ? -.5f : .5f) * PlankWidth;
        public static float SlotZ(int slot) => Chasm0 + (slot / Columns + .5f) * Row;
        public static bool IsLaid(int laid, int slot) => slot >= 0 && slot < Slots && (laid >> slot & 1) != 0;

        public static int Count(int laid)
        {
            int n = 0;
            for (int s = 0; s < Slots; s++) if (IsLaid(laid, s)) n++;
            return n;
        }

        // The free slot a plank centred at (x, z) is laid in, the nearest within SnapAcross / SnapAlong
        // (scaled by `slack` for a preview), or -1.
        public static int Snap(float x, float z, int laid, float slack = 1f)
        {
            int best = -1;
            float bestD = float.MaxValue;
            for (int s = 0; s < Slots; s++)
            {
                if (IsLaid(laid, s)) continue;
                float dx = Math.Abs(x - SlotX(s)), dz = Math.Abs(z - SlotZ(s));
                if (dx > SnapAcross * slack || dz > SnapAlong * slack) continue;
                float d = dx * dx + dz * dz;
                if (d < bestD) { bestD = d; best = s; }
            }
            return best;
        }

        // Nothing under (x, z): the chasm, where no slot is laid yet.
        public static bool OverChasm(float x, float z, int laid)
        {
            if (z <= Chasm0 || z >= Chasm1) return false;
            if (Math.Abs(x) >= PlankWidth) return true;
            int row = Math.Min(Rows - 1, (int)((z - Chasm0) / Row));
            return !IsLaid(laid, row * Columns + (x < 0f ? 0 : 1));
        }

        public static bool InArea(float x, float z) => Math.Abs(x) <= AreaHalf && z >= AreaNear && z <= AreaFar;

        public static bool InCarryArea(float x, float z) =>
            Math.Abs(x) <= AreaHalf + CarryMargin && z >= AreaNear - CarryMargin && z <= AreaFar + CarryMargin;

        // Put down within a pile's footprint: it goes back on its own pile.
        public static bool OnPile(float x, float z)
        {
            for (int p = 0; p < Piles; p++)
                if (Math.Abs(x - PileCentreX(p)) <= PlankWidth * .5f && Math.Abs(z - PileZ) <= Row * .5f) return true;
            return false;
        }
    }

    public enum PlankState : byte { Pile = 0, Carried = 1, Lying = 2, Laid = 3 }

    // Where every plank of one station is: what the host's MgPlanks keeps and sends in its block (MiniGameBase.WriteNet,
    // at most PawnRushNetState.BlockBytes) and every other PC draws. The block: the laid slots (2 bytes); each plank's
    // state and whether it lies turned 90 degrees (3 bits a plank, 4 bytes); then, plank by plank, the carrier's seat
    // (carried) or where it lies (x and z, 0.1 m: x -12.8..12.7, z -2..23.5). At most 2 + 4 + 10 * 2 = 26 bytes.
    // A laid plank is spent: which slot it went to does not matter, only which slots are laid.
    public sealed class PlankBoard
    {
        public const int MaxBytes = 2 + 4 + PlankBridge.Planks * 2;
        public const byte NoSeat = 255;

        public int Laid;
        public readonly PlankState[] State = new PlankState[PlankBridge.Planks];
        public readonly bool[] Turned = new bool[PlankBridge.Planks];
        public readonly byte[] Seat = new byte[PlankBridge.Planks];
        public readonly float[] X = new float[PlankBridge.Planks], Z = new float[PlankBridge.Planks];

        public PlankBoard() => Reset();

        public int LaidCount => PlankBridge.Count(Laid);

        public void Reset()
        {
            Laid = 0;
            for (int i = 0; i < PlankBridge.Planks; i++)
            {
                State[i] = PlankState.Pile;
                Turned[i] = false;
                Seat[i] = NoSeat;
                X[i] = Z[i] = 0f;
            }
        }

        public int Stock(int pile)
        {
            int n = 0;
            for (int i = pile * PlankBridge.PerPile; i < (pile + 1) * PlankBridge.PerPile && i < PlankBridge.Planks; i++)
                if (State[i] == PlankState.Pile) n++;
            return n;
        }

        // The plank a hand takes from a pile: the highest of that pile still on it, or -1.
        public int TopOf(int pile)
        {
            for (int i = (pile + 1) * PlankBridge.PerPile - 1; i >= pile * PlankBridge.PerPile; i--)
                if (State[i] == PlankState.Pile) return i;
            return -1;
        }

        public void CopyFrom(PlankBoard o)
        {
            Laid = o.Laid;
            Array.Copy(o.State, State, State.Length);
            Array.Copy(o.Turned, Turned, Turned.Length);
            Array.Copy(o.Seat, Seat, Seat.Length);
            Array.Copy(o.X, X, X.Length);
            Array.Copy(o.Z, Z, Z.Length);
        }

        // Where a plank lies, on the 0.1 m grid the block carries (the host lays it there too, so both agree).
        public static int QuantX(float x) => Math.Max(0, Math.Min(255, (int)Math.Round((x + 12.8f) * 10f)));
        public static int QuantZ(float z) => Math.Max(0, Math.Min(255, (int)Math.Round((z + 2f) * 10f)));
        public static float GridX(float x) => QuantX(x) / 10f - 12.8f;
        public static float GridZ(float z) => QuantZ(z) / 10f - 2f;

        public void Write(ref NetBlock block)
        {
            block.U16(Laid & ((1 << PlankBridge.Slots) - 1));
            uint bits = 0;
            for (int i = 0; i < PlankBridge.Planks; i++)
                bits |= (uint)(((int)State[i] & 3) | (Turned[i] ? 4 : 0)) << (3 * i);
            block.U32(bits);
            for (int i = 0; i < PlankBridge.Planks; i++)
            {
                if (State[i] == PlankState.Carried) block.U8(Seat[i]);
                else if (State[i] == PlankState.Lying)
                {
                    block.U8(QuantX(X[i]));
                    block.U8(QuantZ(Z[i]));
                }
            }
        }

        public void Read(ref NetBlock block)
        {
            Laid = block.ReadU16() & ((1 << PlankBridge.Slots) - 1);
            uint bits = block.ReadU32();
            for (int i = 0; i < PlankBridge.Planks; i++)
            {
                uint v = bits >> (3 * i);
                State[i] = (PlankState)(v & 3);
                Turned[i] = (v & 4) != 0;
            }
            for (int i = 0; i < PlankBridge.Planks; i++)
            {
                if (State[i] == PlankState.Carried) Seat[i] = (byte)block.ReadU8();
                else Seat[i] = NoSeat;
                if (State[i] == PlankState.Lying)
                {
                    X[i] = block.ReadU8() / 10f - 12.8f;
                    Z[i] = block.ReadU8() / 10f - 2f;
                }
            }
        }
    }
}
