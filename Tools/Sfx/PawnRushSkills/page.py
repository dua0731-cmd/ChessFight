# page.py: writes the mockup page (out/page/index.html) and its one data file (out/page/sfx-data.js: every mp3
# and storyboard frame as base64) from out/meta.json, out/audio/ and frames/.
# SPEC=spec_v2 writes the second round (R107, per-piece families) from out2/ with page_template_v2.html.
import base64, html, importlib, json, os

HERE = os.path.dirname(os.path.abspath(__file__))
SPEC = importlib.import_module(os.environ.get("SPEC", "spec"))
ROOT = os.path.join(HERE, getattr(SPEC, "OUT", "out"))
OUT = os.path.join(ROOT, "page")
os.makedirs(OUT, exist_ok=True)
meta = json.load(open(os.path.join(ROOT, "meta.json"), encoding="utf-8"))
PER_PIECE = "families" in meta

CUE_KO = {
    "pawn": {"step": "걸음", "push": "밀침", "knock": "넘어뜨림", "help": "부축"},
    "queen": {"windup": "예고", "blast": "터짐"},
    "rook": {"tick": "조준 칸", "lock": "잠금", "dash": "돌진", "hit": "맞음", "wall": "벽에 쾅"},
    "bishop": {"throw": "던짐", "land": "꽂힘", "arm": "무장", "hum": "팽팽한 줄", "trip": "걸림", "end": "사라짐"},
    "knight": {"leap": "도약", "turn": "공중 당", "land": "착지", "stomp": "머리 밟기", "daze": "어지러움", "air": "날아감"},
}
DESC = {
    "pawn": {
        "A": "나무 말이 톡 → 퉁 부딪힘 → 딱! 잡히며 데구르르 → 부축은 대나무 풍경",
        "B": "휙 미끄러지는 지잎 → 고무 봉크·보잉 → 휘슬이 쭉 내려가 퉁 → 부축은 띠로링 파워업",
        "C": "훅 대시와 발 구름 → 퍽 밀침 → 크랙 + 쿵 넘어뜨림 → 부축은 위로 솟는 훅",
        "D": "잉크 도장 척 → 공기 퍽 + 물방울 → 젖은 잉크 철퍽 → 부축은 반짝이 + 바람",
        "E": "마림바 한 음(두 번째 걸음은 높게) → 우드블록 + 실로폰 → 실로폰 쭈르륵 + 통 → 부축은 마림바 아르페지오",
    },
    "queen": {
        "A": "나무 말들이 차르륵 모여들다 → 나무판 쾅 + 체스말 와르르",
        "B": "슬라이드 휘슬이 쭈욱 올라가다 → 만화 펑 + 뿅뿅 튀어나옴",
        "C": "역재생 쉬익으로 빨려 들다 → 묵직한 폭발 쾅 + 파편",
        "D": "불이 모이는 화르륵 + 불티 → 불 폭발 쿠웅 + 금속 체스말 짤랑",
        "E": "글로켄슈필이 빠르게 올라감 → 심벌 + 글로켄 화음 + 팀파니",
    },
    "rook": {
        "A": "나무 핀 똑 ×4 → 나무 블록 쿵 → 드르륵 미끄러짐 → 볼링 핀 딱 ×3 → 나무 벽 쾅 + 블록 와르르",
        "B": "장난감 틱 ×4 → 라쳇 드르륵 찰칵 → 장난감 차 슝 → 봉크·보잉 ×3 → 벽 봉크 + 띠요옹",
        "C": "묵직한 철컥 ×4 → 걸쇠 철커덕 → 우오오 돌진 → 퍽 ×3 → 돌벽 쾅 + 돌 무너짐",
        "D": "전기 칙 ×4 → 위이잉 충전 → 지지직 돌진 → 파직 ×3 → 번개 쾅 + 지지직",
        "E": "마림바 톡 ×4 → 팀파니 롤 → 탐탐 롤 → 팀파니 둥 ×3 → 팀파니 + 큰북 + 징",
    },
    "bishop": {
        "A": "나무 막대 휘리릭 → 말뚝 톡톡 → 줄 팅 → 밧줄 끼익 뚜웅 → 줄 끊김 팅",
        "B": "휘슬 위아래 → 흡착컵 뽁뽁 → 입하프 보잉 → 고무줄 끼이잉 퉁 → 뿅",
        "C": "쌔액 두 번 → 말뚝 쿵쿵 → 강철 줄 팅 → 케이블 끼익 채찍 짝! → 줄 끊김 + 쿵",
        "D": "반짝이는 회오리 → 말뚝 퐁퐁 + 반짝 → 마법 줄 핑 → 웅~ 늘어나다 뚜웅 → 반짝 사라짐",
        "E": "하프 글리산도 위로 → 피치카토 네 번 → 하프 높은 음 → 낮은 줄 휘었다 뚜웅 → 하프 내려감",
    },
    "knight": {
        "A": "말발굽 따각 + 휙 → 공중 따각 → 따각 쿵 착지 → 딱! 밟기 → 나무 팽이 달달",
        "B": "용수철 보잉 점프 → 높은 보잉 → 통 착지 → 삑삑이 뿍 + 보잉 → 어질어질 휘파람 + 짹짹",
        "C": "쿵 박차고 훅 → 공기 팍 → 묵직한 착지 → 퍽 짓누름 → 웅웅 어지러움",
        "D": "바람 휘익 솟구침 → 옆바람 휙 → 바람 퍼지며 착지 → 내리꽂는 바람 → 작은 회오리",
        "E": "우드블록 따각 + 플루트 휘~ → 우드블록 딱 + 플루트 짹 → 우드블록 + 탐 → 삑 + 팀파니 → 오르골 빙글",
    },
}
SKILL_ORDER = ["pawn", "queen", "rook", "bishop", "knight"]
DIRS = list("ABCDE")


def fam_name(skill, d):
    return meta["families"][skill][d]["name"] if PER_PIECE else meta["directions"][d]["name"]


def fam_desc(skill, d):
    return meta["families"][skill][d]["line"] if PER_PIECE else DESC[skill][d]
e = html.escape


def b64(path):
    return base64.b64encode(open(path, "rb").read()).decode("ascii")


# ------------------------------------------------------------------ data file
sfx, frames = {}, {}
for skill in SKILL_ORDER:
    s = meta["skills"][skill]
    for d, t in s["tracks"].items():
        sfx[f"{skill}_{d}"] = b64(os.path.join(ROOT, "audio", t["file"]))
        for cue, f in t["cues"].items():
            sfx[f"cue_{skill}_{cue}_{d}"] = b64(os.path.join(ROOT, "audio", f))
    for fname, _, _ in s["frames"]:
        frames[fname[:-4]] = "data:image/jpeg;base64," + b64(os.path.join(HERE, "frames", fname))
with open(os.path.join(OUT, "sfx-data.js"), "w", encoding="ascii") as f:
    f.write("window.SFX=" + json.dumps(sfx, separators=(",", ":")) + ";\n")

# ------------------------------------------------------------------ markup
def skill_section(skill):
    s = meta["skills"][skill]
    length = max([s["length"]] + [t["length"] for t in s["tracks"].values()])
    scale = (int(length * 2) + 1) / 2.0
    pct = lambda t: f"{100 * t / scale:.2f}%"
    out = [f'<section class="skill" id="{skill}" data-skill="{skill}" data-scale="{scale}" style="--piece: var(--{skill})">']
    out.append(f'<header class="skill-head"><h2 class="banner"><span>{e(s["name"])}</span></h2>'
               f'<p class="key">{e(s["key"])}</p></header>')
    # storyboard
    out.append('<div class="board"><div class="frames" role="list">')
    for fname, t, label in s["frames"]:
        key = fname[:-4]
        out.append(f'<figure class="frame" role="listitem" data-t="{t}"><img src="{frames[key]}" alt="{e(label)}" width="480" height="230">'
                   f'<figcaption><b class="num">{t:.2f}초</b> {e(label)}</figcaption></figure>')
    out.append('</div>')
    # timeline
    out.append('<div class="timeline" aria-hidden="true"><div class="track">')
    k = 0.0
    while k <= scale + 1e-6:
        whole = abs(k - round(k)) < 1e-6
        out.append(f'<span class="tick{" whole" if whole else ""}" style="left:{pct(k)}">{f"<i>{int(round(k))}초</i>" if whole else ""}</span>')
        k += 0.5
    ends = [-1e9] * 4  # right edge of the last label in each lane, at a 1000 px wide timeline
    for t, label in s["moments"]:
        x = 1000 * t / scale - 8
        w = sum(12 if ord(ch) > 0x3000 else (4 if ch == ' ' else 7) for ch in label) + 8
        lane = next((i for i in range(4) if x > ends[i] + 6), 3)
        ends[lane] = x + w
        out.append(f'<span class="mark" data-t="{t}" style="left:{pct(t)}"><em class="lane{lane}">{e(label)}</em></span>')
    out.append('<span class="playhead"></span></div></div></div>')
    # five takes
    out.append('<ol class="takes">')
    for d in DIRS:
        if d not in s["tracks"]:
            continue
        t = s["tracks"][d]
        dn = fam_name(skill, d)
        out.append(f'<li class="take" data-dir="{d}">')
        out.append(f'<button class="play" type="button" data-play="{skill}_{d}" data-skill="{skill}" aria-label="{d} {e(dn)} 전체 듣기"><span class="ico"></span></button>')
        out.append(f'<div class="take-body"><div class="take-title"><span class="dir dir-{d}">{d}</span><strong>{e(dn)}</strong>'
                   f'<span class="len num">{t["length"]:.1f}초</span></div>')
        out.append(f'<p class="desc">{e(fam_desc(skill, d))}</p><div class="chips">')
        for cue in s["cues"]:
            out.append(f'<button class="chip" type="button" data-play="cue_{skill}_{cue}_{d}">{e(CUE_KO[skill][cue])}</button>')
        out.append('</div></div>')
        out.append(f'<label class="pick" for="pick-{skill}-{d}"><input type="radio" id="pick-{skill}-{d}" name="pick-{skill}" value="{d}"><span>이걸로</span></label>')
        out.append('</li>')
    out.append('</ol>')
    # prompts
    out.append('<details class="prompts"><summary>일레븐랩스에 보낸 설명 보기</summary><div class="ptable"><table><thead><tr><th>순간</th>')
    out.extend(f'<th>{d}</th>' for d in DIRS)
    out.append('</tr></thead><tbody>')
    for cue in s["cues"]:
        out.append(f'<tr><th>{e(CUE_KO[skill][cue])}</th>')
        for d in DIRS:
            out.append(f'<td>{e(s["tracks"][d]["prompts"][cue]) if d in s["tracks"] else ""}</td>')
        out.append('</tr>')
    out.append('</tbody></table></div></details>')
    out.append('</section>')
    return "\n".join(out)


if PER_PIECE:  # one card per piece listing its own five families
    legend = "\n".join(
        f'<li style="--piece: var(--{k})"><div><strong>{e(meta["skills"][k]["name"].split(" · ")[0])}</strong><p class="fams">'
        + " ".join(f'<span class="fam"><span class="dir dir-{d}">{d}</span>{e(fam_name(k, d))}</span>' for d in DIRS)
        + '</p></div></li>' for k in SKILL_ORDER)
else:
    legend = "\n".join(
        f'<li><span class="dir dir-{d}">{d}</span><div><strong>{e(v["name"])}</strong><p>{e(v["line"])}</p></div>'
        f'<button class="run" type="button" data-run="{d}" aria-label="{d} 방향으로 다섯 스킬 이어 듣기"><span class="ico"></span>다섯 스킬 이어 듣기</button></li>'
        for d, v in meta["directions"].items())
nav = "".join(f'<a href="#{k}" style="--piece: var(--{k})">{e(meta["skills"][k]["name"].split(" · ")[0])}</a>' for k in SKILL_ORDER)
sections = "\n".join(skill_section(k) for k in SKILL_ORDER)
picks_init = json.dumps({k: meta["skills"][k]["name"].split(" · ")[0] for k in SKILL_ORDER}, ensure_ascii=False)

fams_init = json.dumps({k: {d: fam_name(k, d) for d in DIRS} for k in SKILL_ORDER}, ensure_ascii=False)

tpl = open(os.path.join(HERE, "page_template_v2.html" if PER_PIECE else "page_template.html"), encoding="utf-8").read()
page = (tpl.replace("<!--LEGEND-->", legend).replace("<!--NAV-->", nav).replace("<!--SECTIONS-->", sections)
           .replace("/*SKILL_NAMES*/{}", picks_init).replace("/*FAMILY_NAMES*/{}", fams_init))
open(os.path.join(OUT, "index.html"), "w", encoding="utf-8").write(page)
print("page", len(page) // 1024, "KB;", "data", os.path.getsize(os.path.join(OUT, "sfx-data.js")) // 1024, "KB;",
      len(sfx), "sounds,", len(frames), "frames")
