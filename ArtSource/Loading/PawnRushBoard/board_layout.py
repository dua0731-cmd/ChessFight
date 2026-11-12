# The board's layout, as LoadingStudio.Board.cs builds it (kept in step by hand: change both).
# Three rows of squares joined at alternate ends (a board-game path), seven stops on it, a start pad before the first
# square and the war room's castle after the last.
import json, os
def layout(tile=1.0):
    p = json.load(open(os.environ['BOARD'])) if os.environ.get('BOARD') else {}
    t = tile
    n = p.get('n', 7); gap = p.get('gap', 3); shift = p.get('shift', -0.6)
    half = (n - 1) / 2
    xs = [i - half + shift for i in range(n)]
    rows = [-gap, 0.0, gap]
    path = []
    path += [(x, rows[0]) for x in xs]
    path += [(xs[-1], rows[0] + k) for k in range(1, int(gap))]
    path += [(x, rows[1]) for x in reversed(xs)]
    path += [(xs[0], rows[1] + k) for k in range(1, int(gap))]
    path += [(x, rows[2]) for x in xs]
    gate = p.get('gate', 1.3)
    path += [(xs[-1] + gate, rows[2])]                # the castle's gate square
    c = int(gap) - 1
    a0, b0, c0 = 0, n + c, 2 * n + 2 * c
    sa = p.get('stops', [[2, n - 1], [2, n - 1], [1, n - 2]])
    stops = {a0 + sa[0][0]: 0, a0 + sa[0][1]: 1, b0 + sa[1][0]: 2, b0 + sa[1][1]: 3, c0 + sa[2][0]: 4, c0 + sa[2][1]: 5, len(path) - 1: 6}
    zone_of = [0, 0, 1, 1, 2, 2, 3]
    squares = []; seg = 0
    for i, (x, z) in enumerate(path):
        squares.append((x * t, z * t, zone_of[seg], stops.get(i, -1)))
        if i in stops: seg = min(6, seg + 1)
    stop_at = [None] * 7
    for i, s in stops.items(): stop_at[s] = (path[i][0] * t, path[i][1] * t)
    hops = sorted(stops); h = [hops[0] + 1] + [hops[k] - hops[k - 1] for k in range(1, len(hops))]
    # Board.cs CastleAt: past the gate square by .7 (CastleGap), then half the keep's width (1.6), so the black
    # pawn on the gate square stays outside the keep
    cx = xs[-1] + gate + p.get('castle_dx', 1.5)
    cz = rows[2] + p.get('castle_dz', .2)
    return dict(tile=t, squares=squares, stops=stop_at, start=((xs[0] - 1.5) * t, rows[0] * t), hops=h,
                castle=(cx * t, cz * t), castle_size=(1.6 * t, 2.2 * t, 1.6 * t),
                tokens_demo=[(stop_at[0][0] - .36 * t, stop_at[0][1]), (stop_at[0][0] + .36 * t, stop_at[0][1])], plate_up=p.get('plate_up', 1.9) * t,
                # the war room's plate floats over the keep's crown, a little left (clear of the black team's list),
                # its line down to just over the crown (Board.cs PlateAnchor)
                final_plate=((cx - .4) * t, (.1 + 2.2 + 1.45) * t, cz * t), final_tail=(cx * t, (.1 + 2.2 + 1.05) * t, cz * t),
                longest=['왕실 수로 정원', '정문 쇠살문 누각', '대계단 현관홀', '보수 중인 동관', '후반 장애물 (임시 바닥)', '왕의 행차 회랑', '작전 회의실'])
