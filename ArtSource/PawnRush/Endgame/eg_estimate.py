"""Time estimates for LD v1 (ENDGAME_LD_HARD_v1.md §3): ideal (arrive on every window), no-mistake average
(waits for windows, no falls) and average with retries. Waits come from eg_timing's windows; first-try rates and
penalties are design estimates to be replaced by playtest numbers.

    python eg_estimate.py [--json out.json]
"""
import json
import sys

import eg_layout as E
import eg_timing as T

RESPAWN = 2.0   # seconds from falling to running again (fall, fade, spawn)


def wait(period, window):
    """Expected wait for a window of `window` s every `period` s when arriving at a random moment."""
    if window >= period:
        return 0.0
    return (period - window) ** 2 / (2.0 * period)


def beats(res, L):
    s2, r5b, s4 = res["S2"], res["R5b"], res["S4"]
    r2w = res["R2"]["walls"][0]["union_longest"]
    r2g = res["R2"]["gates"]["union_longest"]
    s3 = max(r["longest"] for r in res["S3"]["rhythm_from_gate"]["routes"])
    r3 = res["R3"]["both_straight"]["union_longest"]
    r5 = max(r["longest"] for r in res["R5"]["straight"])
    hop = min(h["longest"] for h in r5b["hops"])
    s4c = min(c["longest"] for c in s4["chain"])
    run = E.RUN
    length = {b["id"]: b["z1"] - b["z0"] for b in L.beats}
    n_walls = len(res["R2"]["walls"])
    n_hops = len(r5b["hops"])
    clean_r4 = length["R4"] / run + 3.0 + 1.5
    rows = [
        # id, extra ideal seconds, expected wait, first-try rate, fail penalty (s), lethal, failure
        ("S1", 0.0, 0.0, 1.00, 0.0, False, "없음"),
        ("R1", 0.0, 0.0, 0.65, RESPAWN + 21 / run, True, "스윕 막대에 걸려 판 밖으로 쓸려 떨어짐"),
        ("S2", 6.0 - length["S2"] / run + 1.0, wait(s2["period"], s2["board"]["longest"]), 0.65,
         RESPAWN + 4 / run + s2["period"] / 2, True, "탑승 · 하선 창을 놓쳐 떨어짐, 갑판 위 걸림 막대에 밀려 떨어짐"),
        ("R2", 0.0, n_walls * wait(4.5, r2w) + wait(5.0, r2g), 0.65, 3.0 * n_walls, False,
         "책장 벽에 밀려 휘청(5 + 2), 시계 문에 막힘"),
        ("S3", 0.0, wait(5.0, s3), 0.70, 2.5, False, "차폐판에 밀림(난간 안, 떨어지지 않음)"),
        ("R3", 0.0, wait(4.4, r3), 0.55, RESPAWN + (length["S3"] + 6) / run + wait(5.0, s3), True,
         "진자(14 + 3 m/s)에 맞아 다리 밖으로 날아감"),
        ("R4", 3.0, 1.5, 0.65, 6.0, False, "옆바람에 타일에서 밀려 구덩이로 떨어짐(죽지 않음, 다시 탐)"),
        ("R5", 10.0 / (run + 1.5) - 10.0 / run, wait(E.FallingPiece().cycle(), r5), 0.60,
         RESPAWN + clean_r4 + 12 / run, True, "충격파(10 + 4)에 벨트 가장자리 밖으로 날아감, 늦게 타면 벨트가 다음 충격파로 실어 감"),
        ("R5b", 0.4 * n_hops, wait(r5b["period"], hop) * 0.5, 0.65, RESPAWN + 2.6 / run + 0.4 * n_hops + 4.0, True,
         "상자 사이로 떨어짐, 상자 위에서 밀려 떨어짐"),
        ("R6", 1.0, 0.0, 0.70, RESPAWN + length["R5b"] / run + 0.4 * n_hops + 2.0, True, "원판 바깥쪽 힘에 튕겨 떨어짐"),
        ("S4", 0.0, wait(s4["period"] / 2, s4c), 0.55, RESPAWN + 4.5 / run + s4["period"] / 4, True,
         "접히는 판에서 미끄러져 떨어짐, 착지대에서 차폐판 · 상대에게 밀려 떨어짐"),
        ("EX", 0.0, 0.0, 1.00, 0.0, False, "없음"),
    ]
    out = []
    for bid, extra, w, p, pen, lethal, fail in rows:
        metres = length[bid]
        ideal = metres / run + extra
        clean = ideal + w
        retries = (1.0 - p) / p
        avg = clean + retries * (pen + w)
        out.append(dict(beat=bid, metres=round(metres, 1), ideal=round(ideal, 1), wait=round(w, 1), clean=round(clean, 1),
                        first_try=p, penalty=round(pen, 1), lethal=lethal, average=round(avg, 1), failure=fail))
    total = {k: round(sum(r[k] for r in out), 1) for k in ("metres", "ideal", "clean", "average")}
    return out, total


if __name__ == "__main__":
    res = T.run_all("ld_v1")
    rows, total = beats(res, E.ld_v1())
    for r in rows:
        print(f"{r['beat']:4} {r['metres']:5.1f} m  ideal {r['ideal']:5.1f}  wait {r['wait']:4.1f}  clean {r['clean']:5.1f}  "
              f"first {r['first_try']:.2f}  avg {r['average']:5.1f}  {r['failure']}")
    print("total", total)
    if "--json" in sys.argv:
        with open(sys.argv[sys.argv.index("--json") + 1], "w", encoding="utf-8") as f:
            json.dump(dict(rows=rows, total=total), f, ensure_ascii=False, indent=1)
