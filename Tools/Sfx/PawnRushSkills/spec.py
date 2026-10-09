# Pawn Rush skill sound mockup (R99 proposal): what each skill sounds like, five directions A-E.
# Every cue is generated once per direction with the ElevenLabs sound effect model, then laid on the skill's
# real timeline (Docs/Skills/DESIGN.md, PawnRushSkillParams) by build.py.
#
# timeline entries: (cue, time s, align, semitones, gain dB)
#   align "start": the cue's onset sits on the time; "peak": its loudest hit does; "end": it ends there.

DIRECTIONS = {
    "A": dict(name="나무 체스말", line="진짜 나무 체스말과 체스판: 딱·탁·또르르, 나무가 구르고 쏟아지는 소리"),
    "B": dict(name="만화 장난감", line="용수철·삑삑이·슬라이드 휘슬·뿅: 만화처럼 과장된 장난감 소리"),
    "C": dict(name="묵직한 쾅", line="짧고 묵직하게 박히는 액션 타격: 퍽·쾅·쿵, 낮은 울림, 꼬리 짧게"),
    "D": dict(name="이펙트 그대로", line="화면 이펙트와 같은 재료: 퀸 불 폭발, 룩 번개, 나이트 바람, 비숍 마법 줄, 폰 잉크 도장"),
    "E": dict(name="장난감 악기", line="기물마다 악기와 음: 폰 마림바, 퀸 글로켄슈필, 룩 팀파니, 비숍 하프, 나이트 우드블록"),
}

SKILLS = {
    "pawn": dict(
        name="폰 · 첫 두 걸음", key="F, 0.1~0.8초 안에 F 한 번 더", length=3.8,
        cues={"step": 0.5, "push": 0.6, "knock": 1.0, "help": 1.2},
        timeline=[("step", 0.00, "start", 0, -3), ("step", 0.45, "start", 3, -3), ("push", 0.58, "peak", 0, 0),
                  ("step", 1.50, "start", 0, -3), ("knock", 1.62, "peak", 0, 0),
                  ("step", 2.70, "start", 0, -4), ("help", 2.82, "start", 0, -2)],
        moments=[(0.00, "걸음 1"), (0.45, "걸음 2"), (0.58, "곧게 밀침"), (1.62, "대각선 넘어뜨림"), (2.82, "팀원 부축")],
        frames=[("step", 0.0, "걸음 · 밟은 칸"), ("push", 0.58, "밀침 · C자 고리"), ("knock", 1.62, "넘어뜨림 · 잡힌 칸"), ("help", 2.82, "부축 · 칸이 솟음")],
        parts=[("걸음 두 번 + 밀침", 0.0, 1.3), ("대각선 넘어뜨림", 1.45, 2.6), ("부축", 2.65, 3.8)],
    ),
    "queen": dict(
        name="퀸 · 팔방 밀치기", key="F (0.35초 예고 뒤 터짐)", length=2.9,
        cues={"windup": 0.6, "blast": 2.0},
        timeline=[("windup", 0.65, "end", 0, -4), ("blast", 0.65, "peak", 0, 0)],
        moments=[(0.30, "F · 모여듦"), (0.65, "터짐 · 맞음")],
        frames=[("windup", 0.30, "예고 · 금색 체스말이 모임"), ("blast", 0.65, "터짐 · 흰 공"), ("burst", 0.80, "불 덩어리·체스말이 튐"), ("smoke", 1.40, "연기가 사라짐")],
        parts=[("예고", 0.25, 0.68), ("터짐", 0.6, 2.9)],
    ),
    "rook": dict(
        name="룩 · 직선 돌파", key="F 조준 → 좌클릭", length=4.8,
        cues={"tick": 0.5, "lock": 0.5, "dash": 0.8, "hit": 0.6, "wall": 1.3},
        timeline=[("tick", 0.00, "start", 0, -10), ("tick", 0.10, "start", 2, -10), ("tick", 0.20, "start", 4, -10), ("tick", 0.30, "start", 5, -9),
                  ("lock", 0.80, "start", 0, -4), ("dash", 1.10, "start", 0, -4),
                  ("hit", 1.22, "peak", 0, -1), ("hit", 1.34, "peak", 2, -1), ("hit", 1.46, "peak", 4, 0), ("hit", 1.58, "peak", -3, 1),
                  ("lock", 2.60, "start", 0, -4), ("dash", 2.90, "start", 0, -4), ("wall", 3.20, "peak", 0, 1)],
        moments=[(0.00, "F 조준"), (0.80, "좌클릭 잠금"), (1.10, "돌진"), (1.22, "맞음 1"), (1.34, "맞음 2"), (1.46, "맞음 3"), (1.58, "4번째 멈춤"), (3.20, "벽에 쾅")],
        frames=[("aim", 0.0, "조준 · 칸 네 개"), ("dash", 1.10, "번개 돌진"), ("hit", 1.30, "맞음 · C자 고리"), ("captured", 1.60, "잡힌 칸"), ("wall", 3.20, "벽에 쾅 · 성벽 고리")],
        parts=[("조준 · 잠금", 0.0, 1.1), ("돌진 · 맞음 3번 · 멈춤", 1.05, 2.3), ("벽에 쾅", 2.55, 4.8)],
        clip={"dash": [(1.10, 0.75), (2.90, 0.30)]},
    ),
    "bishop": dict(
        name="비숍 · 교차 밧줄", key="F 조준 → 좌클릭", length=4.4,
        cues={"throw": 0.6, "land": 0.6, "arm": 0.8, "trip": 1.2, "end": 0.8},
        timeline=[("throw", 0.30, "start", 0, -5), ("land", 0.62, "peak", 0, -3), ("arm", 1.12, "start", 0, -3),
                  ("trip", 2.35, "peak", 0, 0), ("end", 3.50, "start", 0, -4)],
        moments=[(0.00, "F 조준"), (0.30, "좌클릭 던짐"), (0.62, "꽂힘"), (1.12, "무장"), (2.00, "걸려 늘어남"), (2.35, "넘어뜨림"), (3.50, "사라짐")],
        frames=[("aim", 0.0, "조준 · 작은 보라 칸"), ("throw", 0.30, "X를 던짐"), ("armed", 1.12, "무장 · 흰 띠"), ("trip", 2.35, "걸림 · 보라 고리"), ("down", 2.60, "넘어짐 · 잡힌 칸")],
        parts=[("던짐 · 꽂힘", 0.25, 1.05), ("무장", 1.05, 1.95), ("걸림", 1.9, 3.3), ("사라짐", 3.4, 4.4)],
    ),
    "knight": dict(
        name="나이트 · 꺾어 도약", key="F 도약, 공중에서 F 한 번 더", length=4.6,
        cues={"leap": 0.7, "turn": 0.5, "land": 0.6, "stomp": 0.6, "daze": 1.2},
        timeline=[("leap", 0.00, "start", 0, -3), ("turn", 0.75, "peak", 0, -3), ("land", 1.40, "peak", 0, -2),
                  ("leap", 2.20, "start", 0, -4), ("turn", 2.70, "peak", 2, -4), ("stomp", 3.00, "peak", 0, 0),
                  ("daze", 3.10, "start", 0, -8), ("land", 3.30, "peak", 2, -7)],
        moments=[(0.00, "F 도약"), (0.75, "공중 F 당"), (1.40, "착지"), (2.20, "도약"), (2.45, "적 표시"), (2.70, "F 머리로"), (3.00, "머리 밟기"), (3.10, "어지러움")],
        frames=[("leap", 0.0, "도약 · 바람 덩어리"), ("fly", 0.40, "날아감 · 가는 길 칸"), ("land", 1.40, "착지 · 말굽"), ("lock", 2.45, "적 표시 · 빨간 고리"), ("stomp", 3.00, "머리 밟기 · 납작"), ("daze", 3.40, "어지러움 · 크림 폰 3개")],
        parts=[("도약 · 당 · 착지", 0.0, 2.0), ("머리 밟기", 2.15, 4.6)],
    ),
}

# The prompts (English, as the sound model reads it best). One clear sound each, short and concrete:
# the model's own guide says several sounds in one prompt blur together.
P = {
    ('pawn', 'step'): {
        'A': 'Small wooden chess piece hopping on a wooden board, quick light wooden tok, close-mic',
        'B': 'Cartoon quick zip dash with a tiny rubber squeak',
        'C': 'Quick punchy dash whoosh with a firm low footstep thump',
        'D': 'Rubber ink stamp slapping onto paper, soft quick thup with a tiny dust puff',
        'E': 'Single short bright marimba note',
    },
    ('pawn', 'push'): {
        'A': 'Blunt hollow wooden knock, two wooden chess pieces bumping',
        'B': 'Cartoon rubber bonk with a short bouncy boing',
        'C': 'Punchy body shove impact, firm thud with a quick air crack, close-mic',
        'D': 'Soft punchy whump of air with a few liquid droplet pops, close-mic',
        'E': 'Low woodblock knock with a quick rising xylophone blip',
    },
    ('pawn', 'knock'): {
        'A': 'Sharp loud wooden clack of a chess piece capture, short rattle as it falls on the board',
        'B': 'Cartoon descending slide whistle ending in a soft toy thud',
        'C': 'Heavy punchy knockdown hit, crisp crack and deep floor thud',
        'D': 'Wet ink splat slamming onto a tile, droplets scattering',
        'E': 'Quick descending xylophone glissando ending in a low woody thunk',
    },
    ('pawn', 'help'): {
        'A': 'Warm bamboo wind chime shimmering upward after a soft wooden knock',
        'B': 'Cartoon cheerful rising bloop power-up with a sparkly chime',
        'C': 'Quick uplifting rising whoosh power-up with a soft low thump',
        'D': 'Soft magical sparkle rising with a gentle puff of air',
        'E': 'Rising four-note marimba arpeggio ending on a bright chime',
    },
    ('queen', 'windup'): {
        'A': 'Many small wooden chess pieces sliding and clattering together quickly, rising',
        'B': 'Fast rising cartoon slide whistle',
        'C': 'Short reverse whoosh swell rising fast, tense',
        'D': 'Rising whoosh of fire gathering with crackling embers',
        'E': 'Very fast rising glockenspiel arpeggio',
    },
    ('queen', 'blast'): {
        'A': 'Big hollow wooden boom of a chessboard slammed, wooden chess pieces scattering and rattling',
        'B': 'Cartoon puffy explosion ka-pow with tiny toy pieces popping out',
        'C': 'Massive punchy explosion, deep sub boom and sharp crack, debris',
        'D': 'Cartoon fire explosion whoomp with crackling flames and small metal pieces clinking',
        'E': 'Bright cymbal crash with a big glockenspiel chord and timpani hit',
    },
    ('rook', 'tick'): {
        'A': 'Crisp single wooden click, a chess piece tapped hard on a wooden board, close-mic',
        'B': 'Single tiny plastic toy tick',
        'C': 'Single deep heavy mechanical click',
        'D': 'Single tiny electric spark tick, crisp zap',
        'E': 'Single low marimba tock',
    },
    ('rook', 'lock'): {
        'A': 'Heavy wooden block thumping down onto a wooden board with a short scrape',
        'B': 'Cartoon ratchet crank then a springy latch click',
        'C': 'Heavy metal latch locking, deep ka-chunk with a low rumble',
        'D': 'Electric charge-up hum rising fast with crackling sparks',
        'E': 'Short timpani roll crescendo',
    },
    ('rook', 'dash'): {
        'A': 'Heavy wooden block sliding fast across a wooden board, rumbling grinding',
        'B': 'Cartoon zoom whoosh, rising whistle like a toy car speeding past',
        'C': 'Powerful heavy whoosh charge with a low rumbling roar',
        'D': 'Crackling electric buzz streaking forward with sizzling zaps',
        'E': 'Rapid rising tom-tom drum roll',
    },
    ('rook', 'hit'): {
        'A': 'Wooden bowling pin knocked over, sharp hollow wooden clack',
        'B': 'Cartoon bonk with a springy boing',
        'C': 'Punchy heavy body hit, deep thud with a sharp crack',
        'D': 'Electric zap impact, sharp crackling shock with a punchy thump',
        'E': 'Single timpani hit with a woodblock accent',
    },
    ('rook', 'wall'): {
        'A': 'Heavy wooden block slamming into a wooden wall, big hollow bang, small blocks clattering',
        'B': 'Cartoon crash into a wall, huge bonk with a wobbly spring boing',
        'C': 'Massive heavy slam into a stone wall, deep boom and rubble',
        'D': 'Thunderous electric crack slamming into stone, sizzling lightning',
        'E': 'Huge timpani and bass drum hit with a low gong swell',
    },
    ('bishop', 'throw'): {
        'A': 'Small wooden stick spinning through the air, soft whirring whir-whir',
        'B': 'Cartoon twirling slide whistle up and down',
        'C': 'Quick double spinning whoosh, tight and sharp',
        'D': 'Magical spinning whirl with a shimmering sparkle',
        'E': 'Quick upward harp glissando',
    },
    ('bishop', 'land'): {
        'A': 'Four small wooden pegs tapped into a board in quick succession',
        'B': 'Four quick cartoon suction cup pops',
        'C': 'Four heavy stakes thudding into the ground rapidly',
        'D': 'Four quick soft thuds with magical sparkle puffs',
        'E': 'Four quick low pizzicato string plucks, descending',
    },
    ('bishop', 'arm'): {
        'A': 'Single bright plucked string note, high clear ting with a short wooden resonance',
        'B': 'Cartoon twangy jaw harp boing',
        'C': 'Tight steel wire ting, crisp metallic shimmer',
        'D': 'Bright magic string ping with a shimmering sparkle',
        'E': 'Single high harp note pluck with a bell harmonic',
    },
    ('bishop', 'trip'): {
        'A': 'Rope creaking as it stretches, then snapping back with a low twang',
        'B': 'Cartoon rubber band stretching with a rising squeak, then a thwang release',
        'C': 'Taut cable creaking under tension, then a sharp whip snap',
        'D': 'Magic string stretching with a rising hum, then a deep twang release',
        'E': 'Low harp string bending upward in pitch, then a deep twang',
    },
    ('bishop', 'end'): {
        'A': 'String snapping with a ting, small wooden pegs sliding down',
        'B': 'Cartoon pop with a little descending bloop',
        'C': 'Taut steel wire snapping with a sharp crack, then a short low thump',
        'D': 'Magic string fading with a soft sparkle and a quiet sinking whoosh',
        'E': 'Short descending harp glissando, soft',
    },
    ('knight', 'leap'): {
        'A': 'Wooden toy horse hooves clip-clop on a wooden board, then a light whoosh up',
        'B': 'Cartoon spring jump boing with a whistle whoosh up',
        'C': 'Powerful jump, low thump push-off and strong air whoosh',
        'D': 'Gust of wind bursting upward, swirling air',
        'E': 'Two quick woodblock knocks, then a rising flute whistle',
    },
    ('knight', 'turn'): {
        'A': 'Single hollow wooden hoof clack with a short whoosh',
        'B': 'Cartoon higher-pitched boing with a quick zip',
        'C': 'Sharp punchy air crack with a fast whoosh',
        'D': 'Sharp sideways gust of wind, quick crisp fwoosh',
        'E': 'Bright woodblock hit with a short upward flute chirp',
    },
    ('knight', 'land'): {
        'A': 'Wooden chess knight landing hard on a wooden board, two quick hoof clacks and a solid thud, close-mic',
        'B': 'Cartoon landing thud with a small springy bounce',
        'C': 'Heavy landing thud, dusty impact, low rumble',
        'D': 'Puff of wind spreading across the floor with a soft thump',
        'E': 'Loud woodblock knock, toy percussion, close-mic',
    },
    ('knight', 'stomp'): {
        'A': 'Wooden chess piece slammed down hard on top of another wooden piece, loud sharp clack, close-mic',
        'B': 'Squeaky rubber toy squished hard, then a springy boing',
        'C': 'Heavy stomp impact, punchy crushing thud with a squash crunch',
        'D': 'Downward blast of wind slamming down, squashing whump',
        'E': 'Low timpani boom with a rubber squeaky toy squeak on top, loud and clear',
    },
    ('knight', 'daze'): {
        'A': 'Small wooden spinning top rattling and wobbling',
        'B': 'Cartoon dizzy wobbly spinning whistle with little tweets',
        'C': 'Low wobbly dizzy hum swirling, woozy',
        'D': 'Small swirling breeze circling with light airy whistles',
        'E': 'Tiny music box melody spinning, three notes repeating, slightly wobbly',
    },
}

assert all((s, c) in P for s, d in SKILLS.items() for c in d["cues"]), "every cue needs prompts"
assert all(set(v) == set(DIRECTIONS) for v in P.values()), "every cue needs all five directions"
