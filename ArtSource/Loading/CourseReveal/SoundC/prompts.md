# R583 course reveal C sound prompts

ElevenLabs flow "R583 코스 공개 매칭 UI 소리" (`KrrwZ7t4n9Iz0QSRCQa6`), model `eleven_text_to_sound_v2`,
`prompt_influence` 0.45, 4 takes per node. Take files: `raw/<node>_<1-4>.mp3` (`sessions.json` maps them to sessions).
Picked by `process.py`: tick_2 · tick_4 · tick_1, lockB_2 (R591; lock_1 before), slam_3, open_2, title_1, dot_4, countin_3, count_1,
count_1 + countlast_1 (the "1"). Rejected: near-silent tick_3 · dot_2 · title_2, long rings slam_2 · title_4.

| node | seconds | prompt |
|---|---|---|
| tick | 0.5 | Loud crisp game UI selection tick, one sharp short plastic click with a tight low thump under it, roulette selector snapping to the next item, close-mic, dry, punchy, no ring, no chime, no voice |
| lock | 1.0 | Loud punchy game UI lock-in impact, matchmaking choice confirmed, a hard tight snap with a deep thick thump and a short whoosh tail, modern esports menu sound, dry, no chime, no bell, no ring, no voice |
| slam | 0.8 | Fast UI swoosh that ends in a hard punchy thud, a game menu panel slides in and slams into place, tight and heavy, modern game interface, dry, no voice |
| open | 1.5 | Deep punchy game UI whoosh in with a heavy low sub impact at the very start, matchmaking screen opening, dark modern esports interface sound, tight, no music, no chime, no voice |
| title | 0.6 | Quick sharp UI swipe whoosh ending on a crisp tap, game menu title sliding in, short and tight, dry, no voice |
| dot | 0.5 | Soft short muted UI tap, small digital pop for a progress square filling, low and tight, dry, no ring, no chime, no beep, no voice |
| countin | 0.7 | Short fast UI drop whoosh with a soft thud at the end, countdown panel dropping into place, game interface, dry, no voice |
| count | 1.0 | Huge punchy game countdown hit, deep tight digital impact with a crisp snap on top and a short booming tail, esports match countdown, single hit, no beep, no music, no voice |
| countlast | 2.0 | Massive game match start impact, deep booming sub hit with a sharp crack on top and a short wide boom tail, final countdown beat, esports, single hit, no music, no chime, no voice |

R591 lock-in candidates (duration 1.0, prompt_influence 0.5, 2 takes each, picked B → lockB_2):

| node | prompt |
|---|---|
| lockA | Heavy cinematic UI confirm impact, loud deep punchy boom with a hard transient slam, selection locked, short tail, dry, no chime, no music, no voice |
| lockB | Loud mechanical latch lock-in clunk, heavy steel bolt slamming shut, crisp and punchy, close-mic, short, no ring, no voice |
| lockC | Heavy wooden chess piece slammed down hard on a wooden board, loud punchy thud with a crisp knock, close-mic, dry, no voice |
| lockD | Short reverse whoosh rising quickly into a hard punchy UI impact, esports selection confirmed, tight, no chime, no music, no voice |
| lockE | Punchy digital UI confirm hit, tight electronic thump with a crunchy glitch snap, modern esports interface, short, no beep, no chime, no voice |
| lockF | Big heavy rubber stamp slammed onto a wooden desk, loud punchy thump with a sharp crack, approval stamp, close-mic, dry, no voice |

R594 countdown candidates (2 takes each; page set D picked → cdPulse_1 bed, cdHitClunk_2 for 3 and 2, cdFinal_1 for 1):

| node | seconds | prompt |
|---|---|---|
| cdHeart | 3 | Tense low heartbeat pulse, deep thumping heartbeat at a steady fast pace, dark suspense bed for a game match countdown, dry, no music, no melody, no voice |
| cdClock | 3 | Fast tense mechanical clock ticking, sharp dry ticks at a steady pace, urgent countdown timer, close-mic, no ring, no chime, no music, no voice |
| cdRiser | 3 | Dark tension riser building steadily for three seconds then cutting off, rising noise and sub swell, esports match about to start, no melody, no music, no voice |
| cdPulse | 3 | Dark pulsing low synth drone with a fast throbbing rhythm, tense esports matchmaking countdown ambience, steady pitch, no melody, no voice |
| cdHitHeavy | 1 | Huge tense countdown impact, deep sub thud with a hard snap, esports match countdown beat, single hit, dry, no beep, no chime, no voice |
| cdHitClunk | 1 | Heavy mechanical countdown clunk, big steel lever slamming into place, punchy single hit, dry, no ring, no voice |
| cdHitCrack | 1 | Tight cinematic countdown hit, sharp whip crack with a deep thump under it, single hit, dry, no beep, no voice |
| cdFinal | 2 | Massive match start slam, huge deep boom with a sharp crack and a short dark tail, esports match begins, single hit, no music, no chime, no voice |

## R600 배경음 시안 (긴장감 있는 배경음, 12초, prompt_influence 0.5, 2개씩, `bgm/raw/`)

ElevenLabs 이 커넥터는 음악 노드가 없어 효과음 모델로 만듦. 고른 판 = `bgm/picks.json`, 섞기 = `bgm/mix.py`(코스 공개 큐를 같은 숫자로 다시 계산해 그 아래 −25 dB로 깔고 1(9.8초)에서 끊음, 8초쯤 끝나는 판은 박자 주기로 이어 붙임).

| 노드 | 프롬프트 |
|---|---|
| bgmA_pulse | Tense competitive game matchmaking music bed, steady 140 BPM: fast tight ticking hi-hat sixteenths over a low pulsing synth bass, dark minor mood, constant driving tension, instrumental, no melody, no voice, no vocals, no bells, no chimes |
| bgmB_taiko | Epic arena tension music: low taiko and floor tom drums in a steady driving war rhythm, fast staccato low cello and bass string ostinato on top, dark and urgent, the moment before a big match, instrumental, no voice, no vocals, no choir, no bells, no chimes |
| bgmC_strings | Suspense orchestral underscore: relentless fast spiccato string ostinato in a dark minor key, low brass swells underneath, steady pulse, thriller chase tension, instrumental, no voice, no vocals, no choir, no bells, no chimes |
| bgmD_trailer | Dark electronic trailer bed: heavy sub bass pulses on every beat, gritty distorted synth arpeggio in a minor key, tight snare ticks, modern esports countdown tension, instrumental, no voice, no vocals, no bells, no chimes |
| bgmE_clock | Chess clock suspense music: dry wooden clock ticks on the beat, deep heartbeat kick drum, a low dark bowed bass drone and muted tense low strings, sparse and nervous, instrumental, no voice, no vocals, no breathing, no bells, no chimes |

## R601 배경음 2차 — 장면에 맞게 (긴장감 말고, 12초, prompt_influence 0.5, 2개씩)

고른 판 = `bgm/picks_scene.json`(F = bgmF_1을 한 마디 2.07초로 이음, G = bgmG_2, H = bgmH_1), 섞기 = `mix.py picks_scene.json out_scene`.

| 노드 | 프롬프트 |
|---|---|
| bgmF_royalmarch | Playful royal court march music, steady 116 BPM: crisp snare drum and soft timpani, bouncy pizzicato strings, bassoon and low French horn carrying a cheerful tune, toy soldier parade in a castle, a festive board game about to begin, instrumental, no voice, no vocals, no choir, no bells, no chimes, no glockenspiel |
| bgmG_baroquesalon | Baroque chess salon music, steady 120 BPM: brisk harpsichord ostinato with plucked cello and viola pizzicato, a light woodblock on the beat, elegant, playful and lively, a royal palace game night, instrumental, no voice, no vocals, no choir, no bells, no chimes |
| bgmH_woodengame | Cozy wooden board game music, steady 110 BPM: a bouncy plucked upright bass line, soft brushed snare, muted pizzicato strings and a warm clarinet, curious and cheerful, a game show wheel about to stop in an old library, instrumental, no voice, no vocals, no bells, no chimes, no glockenspiel |

## R602 배경음 3차 — 우리 게임 느낌 (악기 연주 말고, 12초, prompt_influence 0.5, 2개씩)

고른 판 = `bgm/picks_game.json`(I = bgmI_1 그대로, J = bgmJ_1을 2.14초로 이음, K = bgmK_2를 2.0초로 이음), 섞기 = `mix.py picks_game.json out_game`.

| 노드 | 프롬프트 |
|---|---|
| bgmI_partybeat | Cheerful party game lobby beat, steady 124 BPM electronic: punchy bouncy kick, snappy claps, rubbery wobbly synth bass, playful toy-like bleeps, colorful and goofy, chunky toy figures about to race, modern game soundtrack, no voice, no vocals, no bells, no chimes |
| bgmJ_chessgroove | Rhythmic groove made from wooden chess pieces: heavy wooden pieces knocking and sliding on a board as the drum beat, deep thumping sub bass hits on the downbeat, soft whoosh swells, steady 112 BPM, a playful hybrid game soundtrack without melody, no voice, no vocals, no bells, no chimes |
| bgmK_gameshow | Modern game show reveal bed, steady 120 BPM electronic: driving four on the floor kick, tight hi-hats, warm pulsing synth chords, a slow rising synth swell, upbeat and exciting, the big wheel spinning on a TV show, no voice, no vocals, no bells, no chimes, no glockenspiel |

## R604 배경음 4차 — K 같은 게임 쇼 (12초, prompt_influence 0.5, 2개씩)

고른 판 = `bgm/picks_show.json`(L = bgmL_1, M = bgmM_2를 2.07초로 이음, N = bgmN_1), 섞기 = `mix.py picks_show.json out_show`.

| 노드 | 프롬프트 |
|---|---|
| bgmL_gameshow_bright | Bright modern game show music bed, steady 124 BPM electronic: punchy four on the floor kick, offbeat open hi-hats, bouncy plucky synth chords, a warm sidechained pad, a big energetic spinning prize wheel, upbeat and fun, no voice, no vocals, no bells, no chimes, no glockenspiel |
| bgmM_gameshow_funky | Funky TV game show reveal groove, steady 116 BPM electronic: tight kick and snappy snare, busy shuffling hi-hats, a rubbery funky synth bass line, stabby warm synth chords, exciting countdown to a big reveal, no voice, no vocals, no bells, no chimes, no glockenspiel |
| bgmN_gameshow_build | Energetic game show build-up bed, steady 128 BPM electronic: driving kick on every beat, crisp claps, sixteenth note pulsing synth bass, bright saw synth chords and a steady rising filter sweep, contestants waiting for the wheel to stop, no voice, no vocals, no bells, no chimes, no glockenspiel |
