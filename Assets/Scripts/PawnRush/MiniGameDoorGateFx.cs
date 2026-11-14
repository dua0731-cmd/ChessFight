namespace ChessFight.PawnRush
{
    // R643 (revised after 승규 님's 11-14 check), F · 대무도회장 (EnPassant) and I · 국왕 침실 (CheckBlock): the team
    // gate just opens, the gears and the curtains went (승규 님 11-14: "톱니 바퀴
    // 하지 말고 그냥 문이 여는걸로", "커튼 여는 거 말고 있는 문 여는 걸로"). The room's own door rises with the gate box as
    // in every room; this moment only keeps the shared blue column and burst away (ArenaDressing plays them when a
    // station has no IGateFx), with the gold 25 / 50 / 75 % sparkle and sfx_gate_open as before. See MiniGameGateMoment.
    public sealed class MiniGameDoorGateFx : MiniGameGateMoment
    {
        protected override float Length => 0f;
        protected override void Build() { }
        protected override void Pose(float t) { }
    }
}
