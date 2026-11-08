# Pawn Rush skill sound mockup, second round (R107). 승규 님 after hearing Queen of the Hill (R104):
#   "동전 소리 안 돼, 각 기물별 소리로 서로 sfx 이어가게 해줘"
# So, unlike R101 (spec.py: five directions shared by all pieces), every piece gets its OWN five sound families:
#   - one family = one material/instrument used for every moment of that piece's skill, so the moments run on
#     into each other as one phrase (and "bed" cues - the rook's charge, the bishop's taut rope, the knight's
#     flight - are held under the gaps between the hits);
#   - A of every piece continues the direction 승규 님 picked for the same piece in Queen of the Hill (R104),
#     so a piece sounds the same in both modes;
#   - no coins: no chime, bell, ding, glockenspiel, jingle, sparkle or clinking metal in any prompt, and
#     coin.py rejects any take that still rings bright and narrow (> 1.8 kHz, >= 90 ms).
# build.py / page.py read this file when SPEC=spec_v2 (gen2/, out2/).
from spec import SKILLS as _R101

GEN, OUT = "gen2", "out2"
LETTERS = "ABCDE"

# Per piece: letter -> (name, one line). A = the Queen of the Hill pick for that piece.
FAMILIES = {
    "pawn": {
        "A": ("만화 효과음", "퀸 오브 더 힐 폰(C)과 같은 소리: 휘익 대시·고무 봉크·띠용"),
        "B": ("고무 장난감", "말랑한 고무 폰: 낮은 삑·뽁·통통 튐"),
        "C": ("잉크 도장", "화면 이펙트의 잉크 도장: 척·철퍽·꾹"),
        "D": ("나무 폰", "작은 나무 체스말: 낮은 톡·통·데구르르"),
        "E": ("작은 북", "보병의 북: 작은북 톡·탐탐 둥·북 굴림"),
    },
    "queen": {
        "A": ("만화 효과음", "퀸 오브 더 힐 퀸(C)과 같은 소리: 슬라이드 휘슬이 올라가다 만화 펑"),
        "B": ("불꽃", "화면 이펙트의 불 폭발: 화르륵 모였다 쿠웅 터지고 불이 탐"),
        "C": ("대포", "심지가 치익 타다 대포 쾅 + 연기"),
        "D": ("바람 소용돌이", "공기를 쭉 빨아들였다 한 번에 훅 밀어냄"),
        "E": ("큰 북", "팀파니가 빠르게 굴러 커지다 큰북 쿵"),
    },
    "rook": {
        "A": ("만화 효과음", "퀸 오브 더 힐 룩(C)과 같은 소리: 뽁·발 구르기 부릉·휘익 돌진·봉크"),
        "B": ("돌 성벽", "성의 돌: 돌 톡·돌덩이 쿵·드르륵 긁히며 돌진·돌벽 무너짐"),
        "C": ("번개", "화면 이펙트의 번개(R101 D 그대로): 칙·위잉·지지직 돌진·파직·번개 쾅"),
        "D": ("공성 망치", "나무 수레와 망치: 라쳇 딸깍·밧줄 끼익·바퀴 덜컹 돌진·나무 성문 쾅"),
        "E": ("타이코 북", "큰 북: 테 딱·북 둥·북 연타 돌진·쿵 쾅"),
    },
    "bishop": {
        "A": ("묵직한 타격", "퀸 오브 더 힐 비숍(D)과 같은 소리: 무거운 휙·말뚝 쿵·낮게 웅·쿵 넘어짐"),
        "B": ("마법 줄", "화면 이펙트의 보라 줄(반짝임 없이): 휘리릭·웅 퍼짐·낮은 웅웅·줄이 팽 튕김"),
        "C": ("고무줄 새총", "새총과 굵은 고무줄: 퓻·탁·끼이익 늘어남·탁! 튕김·축 늘어짐"),
        "D": ("활시위", "활과 밧줄: 쉭 날아감·퍽 박힘·끼익 당김·둥 튕기며 쿵"),
        "E": ("끈적이 거미줄", "끈적한 줄: 쭈왑 발사·철퍽·쭈우욱 늘어남·뽁 끊기며 쿵·스르르 녹음"),
    },
    "knight": {
        "A": ("8비트(낮은 음)", "퀸 오브 더 힐 나이트(A)와 같은 소리를 낮은 음으로: 8비트 점프·뿅 대신 낮은 붑·지직 착지"),
        "B": ("말발굽", "나무 말발굽(R101 A 그대로 + 날아가는 바람): 따각 박차고·공중 따각·따각 쿵 착지"),
        "C": ("바람", "화면 이펙트의 바람(R101 D 그대로 + 날아가는 바람): 휘익 솟구침·옆바람·바람 퍼지며 착지"),
        "D": ("용수철", "낮은 용수철: 보잉 튀어 오름·짧은 보잉·통 착지·찌그러지는 뿍"),
        "E": ("묵직한 말", "무거운 말: 쿵 박차고 훅·묵직한 착지·짓누르는 쿵"),
    },
}

# Same skills, moments and frames as R101, plus the held "bed" cues that join the moments.
SKILLS = {k: dict(v) for k, v in _R101.items()}
SKILLS["pawn"]["cues"] = {"step": 0.5, "push": 0.6, "knock": 1.0, "help": 1.0}
SKILLS["queen"]["cues"] = {"windup": 0.6, "blast": 2.0}
SKILLS["rook"]["cues"] = {"tick": 0.5, "lock": 0.6, "dash": 2.0, "hit": 0.6, "wall": 1.5}
SKILLS["bishop"]["cues"] = {"throw": 0.6, "land": 0.6, "arm": 0.8, "hum": 2.0, "trip": 1.2, "end": 0.8}
SKILLS["bishop"]["timeline"] = _R101["bishop"]["timeline"] + [("hum", 1.12, "start", 0, -9)]
SKILLS["bishop"]["clip"] = {"hum": [(1.12, 1.26)]}
SKILLS["knight"]["cues"] = {"leap": 0.7, "turn": 0.5, "land": 0.7, "stomp": 0.7, "daze": 1.2, "air": 2.0}
SKILLS["knight"]["timeline"] = _R101["knight"]["timeline"] + [("air", 0.05, "start", 0, -10), ("air", 2.22, "start", 2, -11)]
SKILLS["knight"]["clip"] = {"air": [(0.05, 1.38), (2.22, 0.82)]}
# Held cues: faded in, and their end faded out where the next moment lands.
BEDS = {"dash", "hum", "air"}

# Taken over from R101's gen/ as they are (same family, and coin.py found no ring in them).
REUSE = {
    ("queen", "A", "windup"): "B", ("queen", "A", "blast"): "B",
    ("queen", "B", "windup"): "D",
    **{("rook", "C", c): "D" for c in ("tick", "lock", "dash", "hit", "wall")},
    **{("knight", "B", c): "A" for c in ("leap", "turn", "land", "stomp")},
    **{("knight", "C", c): "D" for c in ("leap", "turn", "land", "stomp")},
    # Two new takes of this knock came out near-silent; R101's wooden-piece knock is the same object.
    ("pawn", "D", "push"): "A",
}

# Cut from another take of the same family: (source cue, start s, length s, semitones). Single dry drum hits
# came out near-silent again and again (pawn step/push twice, rook tick/lock/hit once, and R101's low drum five
# times), while the longer drum takes were full - so these hits are the start of those, on the same drum.
DERIVE = {
    ("pawn", "E", "step"): ("help", 0.0, 0.18, 5),
    ("pawn", "E", "push"): ("help", 0.0, 0.45, -3),
    ("rook", "E", "tick"): ("wall", 0.0, 0.10, 12),
    ("rook", "E", "lock"): ("wall", 0.0, 0.50, -2),
    ("rook", "E", "hit"): ("wall", 0.0, 0.35, 2),
    # A pebble click rang at 4.2 kHz (a coin-like tink), and a rubber slap came out near-silent twice.
    ("rook", "B", "tick"): ("lock", 0.0, 0.12, 7),
    ("bishop", "C", "land"): ("trip", 0.0, 0.30, 3),
    ("knight", "D", "land"): ("leap", 0.0, 0.40, -4),  # two landing takes near-silent
}

# Gentle low-pass (Hz) on takes whose texture is right but carry a thin bright ring coin.py flags:
# the stone scrape's squeal, the goo fizz, the magic rope's last puff, the rubber and rope creaks under the held rope.
LOWPASS = {("rook", "B", "dash"): 2500, ("bishop", "E", "end"): 3000,
           ("bishop", "C", "hum"): 1800, ("bishop", "D", "hum"): 1500, ("bishop", "B", "end"): 1600}

# One clear sound per prompt; every family repeats its material words so the moments sound like one object.
# 19 prompts here are the retakes' (the first take was near-silent or rang high): see RETAKEN.
P = {
    "pawn": {
        "A": {"step": "Cartoon quick zip dash, short whistling swoosh",
              "push": "Cartoon rubber bonk with a short low boing and a soft punch thud",
              "knock": "Cartoon punch thud followed by a quick descending low boing",
              "help": "Cartoon soft springy pop upward with a short rising low boing"},
        "B": {"step": "Small soft rubber toy squeaking once as it hops, short low squeak",
              "push": "Loud rubber toy bonk, two soft rubber toys smacking together hard, close-mic",
              "knock": "Soft rubber toy knocked over, two low rubbery bounces on a wooden floor",
              "help": "Soft rubber toy bouncing back up, low rubbery boing"},
        "C": {"step": "Rubber ink stamp pressed onto paper, quick soft thup",
              "push": "Big rubber ink stamp slammed onto a desk, firm thump with a wet ink squish",
              "knock": "Big wet ink splat slapping onto a stone tile, heavy wet splash",
              "help": "Rubber ink stamp lifted off wet paper, soft sticky peel and a puff of air"},
        "D": {"step": "Wooden chess piece tapped firmly on a wooden board, single loud wooden tok, close-mic",
              "push": "Loud hollow wooden knock, a wooden chess piece slammed into another, close-mic",
              "knock": "Wooden chess piece knocked over on a wooden board, sharp clack then a short low rolling rattle",
              "help": "Wooden chess piece set back upright on a wooden board, soft low wooden thock and a short slide"},
        "E": {"step": "Loud snare drum hit, single crisp tap, close-mic",
              "push": "Loud floor tom drum hit, single deep punchy boom, close-mic",
              "knock": "Loud tom drum fill going down, three tom hits ending on a deep bass drum boom, close-mic",
              "help": "Two quick rising tom drum taps, soft and dry"},
    },
    "queen": {
        "A": {"windup": "Fast rising cartoon slide whistle",
              "blast": "Cartoon puffy explosion ka-pow with tiny toy pieces popping out"},
        "B": {"windup": "Rising whoosh of fire gathering with crackling embers",
              "blast": "Fireball explosion whoomp, deep roaring burst of flame with a crackling fire tail"},
        "C": {"windup": "Short cannon fuse hissing and burning fast",
              "blast": "Old cannon firing, deep boom with a heavy puff of smoke"},
        "D": {"windup": "Air sucked inward fast, rising vacuum whoosh",
              "blast": "Huge air burst, deep whump shockwave pushing outward"},
        "E": {"windup": "Very fast orchestral timpani roll swelling louder",
              "blast": "Huge orchestral bass drum hit, deep boom, dry"},
    },
    "rook": {
        "A": {"tick": "Short cartoon pop, low soft pok",
              "lock": "Cartoon feet scrambling in place revving up, quick brrr",
              "dash": "Cartoon running zoom, long fast whistling swoosh",
              "hit": "Cartoon punch bonk with a short low boing",
              "wall": "Big cartoon crash into a wall, heavy bonk with falling rubble"},
        "B": {"tick": "Stone pebble dropped on a stone floor, single loud dull click, close-mic",
              "lock": "Heavy stone block dropped into place, deep thunk",
              "dash": "Heavy stone block dragged fast across rough stone, deep low rumbling scrape",
              "hit": "Heavy stone block slamming into a body, loud deep thud, close-mic",
              "wall": "Stone block crashing into a stone wall, deep crash with tumbling rocks"},
        "D": {"tick": "Single low wooden ratchet click",
              "lock": "Heavy wooden beam creaking under strain, low groan",
              "dash": "Heavy wooden cart rolling fast over cobblestones, rumbling wooden wheels",
              "hit": "Wooden battering ram thudding into a body, dull wooden boom",
              "wall": "Wooden battering ram smashing a wooden castle gate, huge wooden crash and splinters"},
        "E": {"tick": "Single taiko drum rim click, wooden, dry",
              "lock": "Single deep taiko drum hit, dry",
              "dash": "Fast rolling taiko drums, rumbling and driving",
              "hit": "Single punchy taiko drum hit, dry",
              "wall": "Huge taiko drum hit with a deep booming tail"},
    },
    "bishop": {
        "A": {"throw": "Heavy object hurled, deep powerful swoosh",
              "land": "Heavy iron stake driven into stone, deep thud",
              "arm": "Heavy mechanism locking, low clunk and a low rumble",
              "hum": "Low tense rumbling drone, steady and heavy",
              "trip": "Heavy body tripped and slammed onto a stone floor, deep thud",
              "end": "Heavy rope dropping onto a stone floor, deep low thump fading"},
        "B": {"throw": "Magic rope cast, soft airy swirling whoosh, low",
              "land": "Magic rope striking the floor, soft low whoomp",
              "arm": "Low magical energy hum swelling up",
              "hum": "Steady low magical energy drone, warm humming",
              "trip": "Magic rope snapping tight, deep elastic thrum and a body thud",
              "end": "Magic rope dissolving, soft low puff of air fading"},
        "C": {"throw": "Slingshot released, quick rubber thwip",
              "land": "Thick rubber cord slapping hard onto a wooden floor, loud thwack, close-mic",
              "arm": "Thick rubber band stretched slowly, rubbery creak",
              "hum": "Thick rubber stretched tight, slow deep rubbery groan",
              "trip": "Thick rubber band snapping back hard, loud low thwack and a body falling with a thud, close-mic",
              "end": "Slack rubber band flopping onto the floor"},
        "D": {"throw": "Arrow released from a wooden bow, fast whoosh",
              "land": "Arrow hitting a wooden target hard, loud deep thunk, close-mic",
              "arm": "Wooden bow drawn tight, bowstring and wood creaking",
              "hum": "Thick rope under heavy strain, deep slow stretching groan",
              "trip": "Low bowstring thrum as a body falls with a heavy thud",
              "end": "Rope going slack, soft swish onto the floor"},
        "E": {"throw": "Sticky goo shot out, wet splurt with a swoosh",
              "land": "Big gooey wet splat slapping onto a stone floor, loud, close-mic",
              "arm": "Sticky goo stretching, wet gooey creak",
              "hum": "Slow sticky goo strands stretching, wet squelchy creaking",
              "trip": "Sticky strand snapping with a wet pop and a body thud",
              "end": "Goo melting away, soft wet fizzle"},
    },
    "knight": {
        "A": {"leap": "Low-pitched retro 8-bit jump, short rising square-wave boing in a low register",
              "turn": "Low retro 8-bit bloop, short and deep",
              "land": "Crunchy low retro 8-bit stomp, noise burst thud",
              "stomp": "Retro 8-bit squash, crunchy low noise burst with a deep falling tone",
              "daze": "Low retro 8-bit wobble, slow warbling tone going down",
              "air": "Soft low retro 8-bit noise whoosh, steady"},
        "B": {"leap": "Wooden chess knight hopping, quick wooden hoof clops with a swoosh",
              "turn": "Single quick wooden hoof clop in the air",
              "land": "Wooden chess knight landing hard on a wooden board, two quick hoof clacks and a solid thud, close-mic",
              "stomp": "Wooden chess piece slammed down hard on top of another wooden piece, loud sharp clack, close-mic",
              "daze": "Wooden toy horse rocking slowly back and forth, low wooden creaks",
              "air": "Air rushing past during a long jump, steady soft whoosh"},
        "C": {"leap": "Strong gust of wind whooshing upward",
              "turn": "Quick sideways whoosh of wind",
              "land": "Burst of wind spreading out on landing with a soft thump",
              "stomp": "Wind slamming downward in a heavy whoosh and a thud",
              "daze": "Small whirlwind spinning softly, swirling air fading",
              "air": "Strong steady wind flowing past, long whoosh"},
        "D": {"leap": "Big low spring boing launching upward",
              "turn": "Short low spring boing",
              "land": "Big low springy bonk landing hard on a wooden floor, loud thud, close-mic",
              "stomp": "Loud squashing bonk, a heavy spring toy squashed flat, close-mic",
              "daze": "Low spring wobbling slowly and fading",
              "air": "Soft whoosh through the air with a faint low springy wobble"},
        "E": {"leap": "Heavy horse kicking off the ground, deep hoof thud and a heavy swoosh",
              "turn": "Heavy swoosh turning in the air",
              "land": "Heavy horse landing, deep ground-shaking thud",
              "stomp": "Heavy hooves crushing down, deep thud with a crunch",
              "daze": "Low dizzy rumble wobbling and fading",
              "air": "Heavy deep whoosh through the air, steady"},
    },
}

# Cues whose prompt was rewritten and generated again (first prompts: Docs/Skills/SOUND.md, R107).
RETAKEN = ['B.pawn.push', 'B.pawn.help', 'D.pawn.step', 'D.pawn.push', 'E.pawn.step', 'E.pawn.push', 'E.pawn.knock', 'B.rook.tick', 'B.rook.dash', 'B.rook.hit', 'D.rook.lock', 'C.bishop.land', 'C.bishop.trip', 'C.bishop.hum', 'D.bishop.land', 'D.bishop.hum', 'E.bishop.land', 'D.knight.land', 'D.knight.stomp']

# The reused takes keep the prompt they were made from.
from spec import P as _P101
P["rook"]["C"] = {}
for (_s, _L, _c), _src in REUSE.items():
    P[_s][_L][_c] = _P101[(_s, _c)][_src]
for (_s, _L, _c), (_src, _t0, _n, _st) in DERIVE.items():
    P[_s][_L][_c] = f"(cut: the first {_n:.2f} s of this family's {_src} take, {_st:+d} semitones)"

for _s, _d in SKILLS.items():
    assert set(FAMILIES[_s]) == set(LETTERS), _s
    for _L in LETTERS:
        assert set(P[_s][_L]) == set(_d["cues"]), (_s, _L)
import re as _re
_BANNED = _re.compile(r"\b(chime|bell|ding|glocken|jingl|sparkl|clink|coin|shimmer|twinkl|metal)", _re.I)
assert not [(s, L, c) for s in P for L in P[s] for c, t in P[s][L].items()
            if (s, L, c) not in REUSE and (s, L, c) not in DERIVE and _BANNED.search(t)], "no coin words"
