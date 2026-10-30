"""Markdown tables for ENDGAME_LD_HARD_v1.md from the layout and the checks, so no number is copied by hand.

    python eg_doc_tables.py > checks/tables_ld_v1.md
"""
import eg_estimate as EST
import eg_layout as E
import eg_timing as T


def fmt(x):
    return f"{x:g}" if isinstance(x, (int, float)) else str(x)


def beat_table(L):
    rows = ["| 비트 | z (m) | 길이 | 폭 | 높이 y | 장치와 수치 | CP |", "|---|---|---|---|---|---|---|"]
    for b in L.beats:
        fl = [f for f in L.floors if f["beat"] == b["id"]]
        devs = [d for d in L.devices if d["beat"] == b["id"]]
        width = max((f["x1"] - f["x0"] for f in fl), default=0)
        width = width or max((2 * d.get("half_x", 0) for d in devs), default=0)
        notes = []
        for d in devs:
            k, tm = d["type"], d.get("timing")
            x, y, z = d["pos"]
            if k == "board":
                notes.append(f"원형 회전판 지름 {d['diameter']:g} (z {z:g}) {tm.dps:+g}°/s")
            elif k == "spinbar":
                notes.append(f"스윕 막대 {d['length']:g} m, 높이 {d['y0']}–{d['y1']}, {tm.dps:+g}°/s")
            elif k == "ferry":
                notes.append(f"Ferry 8×8 (중심 z {z:g}) ±{tm.distance:g} m, {tm.period:g} s, 최고 {tm.distance * 6.2832 / tm.period:.2f} m/s")
            elif k == "hurdle":
                notes.append(f"고정 걸림 막대 z {z:g}, x ±{d['x1']:g}, 높이 {d['y0']}–{d['y1']}")
            elif k == "sliding":
                notes.append(f"06 미닫이 벽(6 m로 늘림) z {z:g}, 위상 {d.get('phase_s', 0):g} s")
            elif k == "clockgates":
                notes.append(f"09 시계 문 z {z:g}")
            elif k == "rookscreen":
                notes.append(f"룩 차폐판 6 m z {z:g}, ±{tm.distance:g} m, {tm.period:g} s, 위상 {tm.phase * tm.period:.2f} s")
            elif k == "pendulum":
                notes.append(f"11 진자 z {z:g}, 위상 {tm.phase:g} s")
            elif k == "lifttiles":
                notes.append(f"05 타일 (x {x:g}, z {z:g}) 위상 {d.get('phase_s', 0):g} s")
            elif k == "vent":
                notes.append(f"15 옆바람 (x {x:g}, y {y:g}, z {z:g}) 위상 {tm.phase:g} s")
            elif k == "conveyor":
                notes.append(f"08 벨트 x {x:g}, 6×{d['length']:g}, 북쪽 {d['speed']:g} m/s")
            elif k == "fallingpiece":
                notes.append(f"10 낙하 말 (x {x:g}, z {z:g}) 위상 {tm.phase:g} s")
            elif k == "crate":
                notes.append(f"사슬 상자 z {z:g} ±{tm.distance:g} m, {tm.period:g} s, 위상 {tm.phase:g}")
            elif k == "disc":
                notes.append(f"01 원판 지름 8 (z {z:g})")
            elif k == "foldingbridge":
                notes.append(f"12 접이식 다리 x {x:g}, 위상 {tm.phase:g} s")
        cp = [f"CP{i} (z {z:g})" for i, _, _, z in L.checkpoints if b["z0"] - 0.01 <= z < b["z1"] + 0.01]
        rows.append(f"| {b['id']} {b['title']} | {b['z0']:g}–{b['z1']:g} | {b['z1'] - b['z0']:.1f} | {width:g} | {b['y']:g} | "
                    + "<br>".join(notes or ["—"]) + f" | {', '.join(cp) or '—'} |")
    return "\n".join(rows)


def window_table(res):
    s2, r2, s3, r3, r4, r5, r5b, s4 = (res[k] for k in ("S2", "R2", "S3", "R3", "R4", "R5", "R5b", "S4"))
    any_r4 = [x for x in r4 if x["set"] == "any"][0]
    rows = ["| 건넘 | 주기 (s) | 경고 | 안전 창 (한 주기, s) | 참고 |", "|---|---|---|---|---|"]
    r1 = res["R1"]
    rows.append(f"| R1 스윕 막대 | 탄 사람 기준 {r1['rider_pass_every']} 마다 지나감 | 늘 보임 | 점프 여유 {r1['at_radius'][1]['jump_tolerance']}–{r1['at_radius'][3]['jump_tolerance']} | 막대 끝 속도 {r1['tip_speed_world']} m/s |")
    rows.append(f"| S2 탑승 / 하선 | {s2['period']:g} | 늘 보임 | {s2['board']['longest']} / {s2['alight']['longest']} | 틈 ≤ 1.5 m인 시간, 최소 틈 {s2['min_gap']} m |")
    if "hurdle" in s2:
        h = s2["hurdle"]
        rows.append(f"| S2 갑판 위 걸림 막대 | 반 주기마다 1번 | 늘 보임 | 점프 여유 {h['jump_tolerance_standing']} (서서) · {h['jump_tolerance_running_at_it']} (마주 달려) | 막대 높이 {h['height'][0]}–{h['height'][1]} m |")
    for w in r2["walls"]:
        rows.append(f"| R2 {w['id'][-1]} 벽 한 쌍 | 4.5 | 움직임 0.5 | {w['union_longest']} (가장 좋은 줄 {max(l['longest'] for l in w['lanes'])}) | 지그재그 |")
    if "walls_chain" in r2:
        rows.append(f"| R2 A → B 한 번에 | 4.5 | — | {r2['walls_chain']['union_longest']} | 놓치면 사이 포켓(2.9 m)에서 기다림 |")
    rows.append(f"| R2 09 시계 문 | 5 | 움직임 0.5 | {r2['gates']['union_longest']} | 문 하나 3.8 m |")
    rr = res["S3"]["rhythm_from_gate"]["routes"]
    rows.append(f"| S3 문 → 차폐판 1 → 2 | 5 | 늘 보임 | {max(r['longest'] for r in rr)} (리듬대로 반대쪽으로 바꿔 탈 때) | 각 차폐판만은 늘 반쪽이 열림(5.0) |")
    rows.append(f"| R3 진자 2개 | 4.4 | 1.0 (끝에서 바닥까지) | {r3['both_straight']['union_longest']} (질주 {r3['both_sprint']['union_longest']}) | 피하는 줄 {len(r3['lanes_never_hit'])}개 |")
    rows.append(f"| R4 타일에서 내리기 | 6 (바람 6.8) | 바람 0.8 | 한 번 {any_r4['longest_free']}, 가장 길게 기다림 {any_r4['longest_wait']} | 두 세트 중 바람 없는 쪽 |")
    best5 = max(r["longest"] for r in r5["straight"])
    sprint5 = max(r["longest"] for r in r5["straight_sprint"])
    rows.append(f"| R5 벨트 위 충격파 | 4.55 (한쪽), 이음매 2.28 | 2.05 (경고 1.4 + 낙하 0.65) | {best5} (질주 {sprint5}) | 벨트 위 4 m/s, 이음매 줄은 막힘 |")
    rows.append("| R5b 상자 뛰기 | " + f"{r5b['period']:g} | 늘 보임 | " + " · ".join(f"{h['longest']}" for h in r5b["hops"]) + " | 틈 " + f"{min(h['gap_min'] for h in r5b['hops'])}–{max(h['gap_max'] for h in r5b['hops'])} m |")
    rows.append(f"| S4 다리 하나 | 13.5 | 1.5 | {s4['entry'][0]['longest']} | 다리 판 {s4['cross_panels']} s |")
    rows.append(f"| S4 다리 + 차폐판 한 번에 | 13.5 | 1.5 | " + " · ".join(f"{c['longest']}" for c in s4["chain"]) + " | 늦으면 착지대에서 기다림 |")
    return "\n".join(rows)


def time_table(rows, total):
    out = ["| 비트 | 길이 (m) | 이상 (s) | 창 기다림 (s) | 실수 없는 평균 (s) | 첫 시도 성공 | 실패 비용 (s) | 실패 포함 평균 (s) | 실패 유형 |",
           "|---|---|---|---|---|---|---|---|---|"]
    for r in rows:
        out.append(f"| {r['beat']} | {r['metres']:g} | {r['ideal']:g} | {r['wait']:g} | {r['clean']:g} | {int(r['first_try'] * 100)}% | "
                   f"{r['penalty']:g}{' (낙사)' if r['lethal'] else ''} | {r['average']:g} | {r['failure']} |")
    out.append(f"| **합계** | {total['metres']:g} | **{total['ideal']:g}** | | **{total['clean']:g}** | | | **{total['average']:g}** | |")
    return "\n".join(out)


if __name__ == "__main__":
    L = E.ld_v1()
    res = T.run_all("ld_v1")
    rows, total = EST.beats(res, L)
    print("## 비트 표\n")
    print(beat_table(L))
    print("\n## 안전 창\n")
    print(window_table(res))
    print("\n## 시간\n")
    print(time_table(rows, total))
