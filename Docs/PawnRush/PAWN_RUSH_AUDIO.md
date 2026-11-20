# 폰러시 경기 오디오

2026-11-14 · R652. 경기 음악과 폰의 움직임 소리를 다룬다. 모두 사용자가 ElevenLabs로 만든 테이크이며, 프롬프트는 같은 날 채팅에서 썼고 다이빙 · 잡기 · 점프는 사용자 화면 녹화를 참고했다. numpy 스크립트로 다듬었다. **사용자 청취 · 플레이 확인 전**이며, 확인할 항목은 [VALIDATION](../Network/VALIDATION.md) 맨 위에 있다. 로비 음악은 [UI · 로비 음악](../Architecture/UI.md)에 따로 있다. 대계단 · 유리 · 킹 잡기 소리는 R653이 넣었다(§5).

## 0. 음량 기준 (R654)

사용자 "현재 폰러시 BGM의 음량이 제일 적합한 것 같은데 이걸 기준으로 모든 음량을 적절하게 맞춰줘". 소리 파일 241개를 모두 재고(파일 크기 · 100 ms · 400 ms 크기, 타격 위치) 코드가 곱하는 값을 따라 **기본 설정(음악 0.7 · 효과음 0.8) · 가까운 거리**에서 들리는 크기를 계산해 맞췄다. 귀로 들은 것이 아니므로 듣고 고칠 때는 아래 값을 바꾼다.

| 종류 | 들리는 크기 | 해당 |
|---|---|---|
| 음악 | RMS −28 dB | 경기 음악(기준) · 로비(`LobbyMusic.Gain` .3983, 전 −26) · 코스 공개(`MusicGain` .319, 전 −28.9) · 소드 파이트 입장(`SwordMusicGain` .1621, 전 −26.8, 끝의 부풀기는 그대로) · 로고(`LogoIntro.LogoLevel` .8, −27) |
| 위험 큰 타격 | 400 ms −18 dB | 작전 회의실 포탑 착탄(무거움 .26 · 옆 .31, 전 −2 / −8 dB로 잘림) |
| 보통 한 번 | −21 dB | 포탑 발사(.39 / .55) · 충전(.41 / .46), 서고 공기 대포 충전 .45 · 발사 .41, 컨베이어 덜컹 .62, 정원 물 튐(.25 ~ .56), 회전 막대 타격(.45 ~ .75), 쇠살문 열림(.5 / .41 / .39) · 걸음(.5 / .43), 다이빙 −21 |
| 작은 소리 | −24 dB | 칠하기 붓질 .6, 대계단 유리 1.4(대계단 0.55를 지나 −24, 전 −29). 점프 · 착지 · 잡기는 R656에 1.5배로 −20 ~ −21 |
| 반복음 | RMS −30 dB | 회전 막대 축(.27 · 큰 막대 .45, 전 −21 / −24) |
| 발소리 | −26 dB (R656, 전 −30) | `PawnRushStepSfx` |

- **효과음 설정:** 구간 소리(정원 · 서고 · 미니게임 · 대계단 · 작전 회의실 · 마차뜰)는 `SectionSoundGate`가 한 번 곱한다. 전에는 대계단에서만 곱해 나머지는 효과음 막대를 무시하고 2 dB 컸다. 직접 곱하던 체크메이트 효과 · 마차 · 쾅 상자는 뺐다(두 번 곱하지 않게). 대계단의 0.55(R653)는 그대로다. 로비 화로 소리도 효과음 크기를 따른다.
- **스킬 · 움직임 소리:** 원래 기준(가장 큰 0.3초를 −21 dB, Level × 효과음)이 이미 −20 ~ −25 dB라 그대로 두었다.
- **소리가 바로 나오게:** Unity 오디오 버퍼를 1024 → 512로 낮췄다(`ProjectSettings/AudioManager.asset`, 누른 뒤 소리까지 약 85 → 43 ms). 움직임 소리와 발소리는 타격 2 ms 앞에서 시작하게 다시 잘랐다(`bake_moves.py`의 `opening`). 점프의 첫 30 ms는 몸통 크기로 올렸다.

## 1. 경기 음악

| 항목 | 내용 |
|---|---|
| 곡 | 150 BPM · F장조 · 128초. "우당탕탕 우스꽝스러운 브라스 밴드 추격" 프롬프트로 만든 테이크. 원본은 `ArtSource/PawnRush/Music/raw/PawnRushRace_2026-11-14.wav` |
| 자르기 | `ArtSource/PawnRush/Music/bake.py`가 두 파일로 자른다. `Resources/PawnRushMusic/RaceMusic.wav` = 0 ~ 28.40초(한 번), `RaceMusicLoop.wav` = 그 뒤 89.6초(56마디, 반복). 117.98초가 28.38초와 같은 음악이라 이어진다(드럼 타격 일치 0.90, 이음새 앞뒤 파형 닮음 0.88 · 0.91). 후보 여섯 개를 `--try`로 재서 가장 긴 것을 골랐다 |
| 재생 | `PawnRush/PawnRushRoundMusic.cs`, 라운드를 지을 때(`PawnRushRound.Build`) 붙는다. 입장 화면이 닫히면 오디오 시계에 처음 부분과 루프를 이어 걸고 1초 페이드 인, 승리가 정해지면 1초 페이드 아웃 후 멈춘다. 구간 시험 장면도 같은 라운드라 같은 음악이 나온다 |
| 크기 | `GameSettings.Music` × 0.3687(`RACE_GAIN`). 기본 음악 크기에서 RMS −28 dBFS. R654부터 모든 음악의 기준(§0) |
| 가져오기 | 압축해 메모리에 둠, Vorbis 0.8, 48 kHz |

## 2. 움직임 소리 (한 번씩 나는 소리)

| 소리 | 클립 (`Resources/PawnRushSkillSfx/`) | 나는 때 | 크기 (`PawnRushSkillSfx.Level`) |
|---|---|---|---|
| 다이빙 '휙' | `CourseDive` 0.15초. 원본의 오른쪽 채널만 씀(두 채널이 거의 반대라 합치면 소리가 얇아짐) | 좌클릭 다이빙이 시작될 때 | 0.62 (R656, 전 0.41) |
| 잡기 '햡' | `CourseGrab` 0.12초 | 손이 폰 · 물건 · 벽 · 밧줄을 처음 잡을 때(양손이 동시에 잡아도 한 번) | 0.48 (R656, 전 0.32) |
| 점프 '퐁' | `CourseJump` 0.17초 | 바닥에서 점프할 때. 턱을 잡고 넘을 때는 1반음 높게 0.8배 크기 | 0.62 (R656, 전 0.41) |
| 착지 '톡' | `CourseLandLight` 0.2초 | 서서 착지: 0.18초 넘게 떠 있다가 2.2 m/s 넘는 속도로 내려왔을 때 | 0.5 × 속도(2.2 → 6 m/s일 때 0.6 → 1) (R656, 전 0.33) |
| 착지 '털썩' | `CourseLandFlop` 0.41초 | 늘어진 몸(다이빙 끝 · 넘어짐 · 구르기)의 엉덩이 · 가슴 · 머리가 바닥(면이 수평에서 약 53° 안)에 0.7 m/s 넘게 닿을 때(R656: 물리 접촉으로, R652의 '엉덩이가 서 있는 높이의 3/4 아래'는 엎어진 폰이 거의 닿지 않아 소리가 안 났다. R658: 닿은 순간의 콜백에서 바로 내고, 미끄러지며 약하게 닿는 첫 접촉도 잡게 1.2 → 0.7 m/s), 또는 서서 7 m/s 넘게 착지했을 때 | 1 × 충격(0.7 → 5 m/s일 때 0.8 → 1) (R656, 전 0.53) |

- **안 나는 때:** 벽 · 턱 · 밧줄 · 갈고리 · 물에 있을 때, 계단을 내려갈 때(기준 미만), 2.5초 넘게 떨어진 뒤(부활), 같은 폰의 앞 착지에서 0.2초(털썩은 0.4초) 이내일 때.
- **재생:** `PawnRushSkillSfx`가 낸다(코스 스킬 `PawnRushCourseSkills`와 같이 붙음). 매번 음높이를 ±0.35반음 흔들고, 같은 프레임에 같은 소리가 여럿이어도 폰마다 따로 낸다. 코스 구간의 거리 규칙(`CourseSoundPlace`)을 따른다.
- **온라인:** 소리가 나는 순간은 몸을 시뮬레이션하는 PC(오프라인은 그 PC, 온라인은 방장)에서 `RagdollLab/RagdollPawn.CourseSounds.cs`가 `SkillFxKind.CourseDive · CourseGrab · CourseJump · CourseLand`로 올린다. 방장의 효과 이벤트 흐름(스킬과 같은 것)이 이를 참가자에게 보내고, 참가자는 화면의 폰과 같은 시각에 재생한다.
  - 이벤트 하나는 32바이트이고, 12인 기준 방장 업로드가 약 7% 늘 것으로 추정한다(측정 아님).
  - 모르는 효과 종류는 무시하므로 프로토콜 번호는 그대로 두었다([DECISIONS R652](../Project/DECISIONS.md)).
- **크기 기준:** 로드할 때 정규화된다(`Prepare`: 가장 큰 0.3초를 −21 dB로, 최대 8배). 그 뒤 50 ms 크기로 재면 폰 대시가 −22 dB, 스킬 타격이 −15 ~ −17 dB다. 움직임 소리는 그 아래로 맞췄다가 R656에 사용자 "1.5배는 키워도 돼"로 올렸다: 다이빙 −17.5, 잡기 −19.5, 점프 −20.5, 톡 −19.5, 털썩 −13.5 dB.

## 3. 발소리

| 항목 | 내용 |
|---|---|
| 클립 | `Resources/PawnRushStepSfx/Walk0` ~ `Walk5`(걷기 테이크에서 한 걸음씩 6개, 0.12초)와 `Run0` ~ `Run5`(달리기 테이크, 0.09초; R657에 빨라진 박자에 맞춰 짧게, 전 0.16 · 0.11초). 걸음마다 같은 크기로 맞췄다 |
| 재생 | `RagdollLab/PawnRushSkills/PawnRushStepSfx.cs`, 코스 스킬과 같이 붙는다. 모든 PC가 화면에 보이는 폰마다 걸음 박자를 센다(아래 조건). 같은 클립이 연속으로 나오지 않고, 음높이를 ±0.5반음 흔든다 |
| 크기 | 걷기 0.44 · 달리기 0.32(R656 1.5배, 전 0.29 · 0.21) × 속도(0.75 ~ 1) × 효과음 설정 × 구간 거리 규칙 × 카메라 거리(6 m부터 줄어 26 m에서 0). 대략 걷기 −26 · 달리기 −24 dB |
| 네트워크 | 보내지 않는다(폰마다 초당 3번). 각 PC가 화면의 움직임에서 만든다 |

박자를 세는 조건:
- **발로 서 있을 것.** 그 PC가 시뮬레이션하는 몸은 Grounded이고, 서 있고, 다이빙 · 벽 · 물 · 밧줄이 아니어야 한다. 참가자 화면의 폰은 엉덩이 아래 바닥이 서 있는 높이에 있고, 위아래 속도가 1 m/s 미만이어야 한다.
- **다리가 움직일 것.** 그 PC가 시뮬레이션하는 몸은 다리 위상(`RagdollPawn.GaitAngle`)이 돌 때. 참가자 화면의 폰은 바닥 기준으로 0.7 m/s 넘게 움직일 때.
- **박자 (R657).** 발이 실제로 디딜 때마다. 다리는 한 주기(걷기 1.8 · 달리기 2.6 · 전력질주 3.3회/초) 안에 두 발이 번갈아 디디므로 달리기 초당 5.2걸음, 전력질주 6.6걸음이다. 시뮬레이션하는 몸은 다리가 앞에 닿을 때마다(주기의 π/2 · 3π/2), 참가자 화면의 폰은 다리와 같은 공식으로 속도에서 (`StepsPerSecond`). R652는 주기당 한 번(2.9 · 3.5)이라 발보다 느렸다.
- **클립 고르기.** 전력질주(Shift, 참가자 화면에서는 3.05 m/s 초과)면 Run, 아니면 Walk.

## 4. 바꾸는 법

- **원본 바꾸기:** `raw/`의 파일을 바꾸고 저장소 루트에서 스크립트를 다시 돌린다. 이 PC에서는 Blender 파이썬으로 실행한다.
  - 움직임 소리: `"C:/Program Files/Blender Foundation/Blender 5.1/5.1/python/bin/python.exe" -I ArtSource/PawnRush/Sfx/bake_moves.py`
  - 미니게임 소리(R675): 같은 파이썬으로 `ArtSource/PawnRush/Sfx/bake_minigame.py`. 원본은 `raw/MiniGame/`, 자르는 곳 · 크기는 파일 맨 위 표(§8).
  - 음악: `ArtSource/PawnRush/Music/bake.py`. `--try 시작 끝`으로 이음새를 잰 뒤 `START`·`END`를 고치고, 출력된 `RACE_GAIN`을 `PawnRushRoundMusic.Gain`에 넣는다.
- **크기 조절:** `PawnRushSkillSfx.Level`의 `Course*` 값, `PawnRushStepSfx.WalkGain` · `RunGain`. 미니게임 소리는 `bake_minigame.py`의 `heard`(들리는 크기)를 고쳐 다시 굽거나, 부르는 곳(`ArenaFx.Sfx` · `Hold`)의 volume을 낮춘다.
- **벽타기 소리:** 프롬프트만 있고 파일이 없어 아직 연결하지 않았다. 넣을 때는 오르는 동안 도는 반복음을 권한다(손은 초당 최대 8번 바뀜).

## 5. 대계단 · 유리 · 킹 잡기 (R653)

R653("폰러시 플레이테스트 후속")이 넣은 소리다. 유리 파손에는 프로젝트의 유리음을 짧게 편집해 넣었다. 새 소리는 아래 파일명으로 기존 WAV를 덮어쓰면 되고, 원래의 `.meta`는 유지한다. 모노 PCM WAV, 44.1 또는 48 kHz로 내보내고 첫 동작 앞의 무음을 잘라낸다.

| 재생 순간 | `Assets/Resources/` 아래 파일 | ElevenLabs에 넣을 프롬프트 |
|---|---|---|
| 대계단 징검다리에서 약한 유리판이 깨질 때 | `PawnRushHallSfx/BridgeGlassBreak.wav` | One thick glass footbridge pane suddenly cracks underfoot. Crisp snap followed by a few light falling shards, 0.7 seconds, close and dry, no explosion, room echo, voice or music. |

킹 잡기 H의 교체 파일은 `PawnRushMiniGameSfx/`에 둔다. 이동 `sfx_king_hop`, 체크 `sfx_check`, 메이트 `sfx_mate`, 잡기 `sfx_king_capture`, 마지막 잡기 `sfx_king_sweep`, 쟁반 착지 `sfx_ep_tray`, 탈출 `sfx_stalemate`, 구출 `sfx_rescue` 순으로 각 동작에 연결된다. [킹 잡기 프롬프트와 시험 방법](PAWN_RUSH_MINIGAME_SECTIONS.md)

- **대계단 장애물**은 원래 거리 감쇠에 홀 게인 0.55와 효과음 설정을 곱한다. 돌 홀 잔향은 0.4초다. 낙하 기사 착지는 A1 원음의 첫 충격 0.8초만 한 소스로 재생한다. 대리석과 카펫의 착지음, 경고의 박자와 방향은 각각의 기존 클립을 따른다.
- **유리**는 방장과 각 참가자가 판이 새로 깨지는 순간 재생하고, 중간 입장 때 이미 깨진 판은 조용히 상태만 맞춘다.

Unity에서 대계단 낙하 기사와 예고음을 들은 뒤, 유리판이 깨지는 순간 및 안전한 판 위에 머무를 때를 비교한다. 두 PC에서는 다이빙과 잡기의 위치, 같은 판의 파손음이 한 번씩 들리는지 확인한다. 직접 청취 결과는 아직 기록되지 않았다.

## 6. 이전 기록

R646(11-14, "폰러시 테스트 후속")이 다이빙 · 잡기 소리와 코드로 만든 경기 음악을 넣었다. 하지만 커밋 전에 작업 폴더가 되돌려져서 main에 들어가지 않았고, 30e235f7 "제발 돼라" 커밋에만 남아 있다. 같은 날 밤 그 작업은 R653으로 다시 커밋됐고, 그 커밋이 같은 폴더에서 동시에 진행되던 R652의 경기 음악 · 다이빙 · 잡기 파일과 `PawnRushRoundMusic`을 먼저 담았다. 점프 · 착지 · 발소리, 움직임 소리의 크기와 음높이 흔들기, 로비 음악은 R652 커밋에 있다.

## 7. 파일이 없는 소리와 ElevenLabs 프롬프트 (R673)

사용자 "미니게임들의 효과음이 비어있는 부분이 많아 … 자리는 있는데 파일이 없어서 효과음이 없는 것들의 리스트" → "프롬프트 만들어줘". 코드가 부르는 소리 262곳을 파일과 대조한 결과(2026-11-15 main `aac81ee6`) 없는 것들이다. 아래 파일 이름 그대로 넣으면 코드 수정 없이 재생된다(`.meta`는 새로 만든다). 생성 설정: Prompt influence 0.6 ~ 0.7, 반복음(🔁)은 **Loop 켜기 · 3초**, 나머지는 적힌 길이(0.5초 미만이면 0.5초로 만들고 잘라 씀), 프롬프트마다 3 ~ 4개 생성해서 고른다.

**R675에 들어온 것(§8):** 7.1의 `sfx_mg_complete` · `sfx_mg_quarter`, 7.2 · 7.3 전부(13개). 아직 없는 것: `sfx_gate_open`, 7.4 ~ 7.7.

### 7.1 모든 미니게임 공통 — `Resources/PawnRushMiniGameSfx/`

| 파일 | 나는 때 | 프롬프트 |
|---|---|---|
| `sfx_mg_complete` | 미니게임 완료(우리 팀 크게, 상대 작게, 2D) | A short bright royal success flourish: a quick rising three-note brass and glockenspiel sting ending on a warm major chord, cheerful and playful, about 1.2 seconds, clean and close, no voice. |
| `sfx_gate_open` | 완료 뒤 팀 문이 열림 | A heavy ornate palace door unlocking and swinging open: one solid iron latch clunk, then a deep wooden door groan and a soft stone rumble, about 1.5 seconds, a little hall reverb, no voice, no music. |
| `sfx_mg_quarter` | 승격 발판(기물 고르기)이 열림(코드가 1.5배 높게 재생) | A soft magical pedestal waking up: a gentle rising shimmer with one soft chime, about 0.5 seconds, clean, no voice, no music. |

### 7.2 F 대무도회장 — 태엽 인형 무용수 석상 · 쪽마루 레인 (`MgEnPassant`)

| 파일 | 나는 때 | 프롬프트 |
|---|---|---|
| `sfx_ep_ready` | 석상이 전진하기 직전 0.5초 들썩임 | A tiny wind-up clockwork toy about to move: a quick rattling whir of a small key and spring, light and mechanical, about 0.5 seconds, close and dry, no voice, no music. |
| `sfx_ep_step` | 석상이 한 칸 내딛음(두 칸 전진이라 두 번) | One quick step of a small wooden clockwork dancer doll sliding forward on a polished parquet floor: a soft wooden clack with a short gear click, about 0.2 seconds, close and dry, no voice, no music. |
| `sfx_ep_window` | 잡을 수 있는 1.2초가 열림 | A bright short music-box cue meaning "now": one clear high bell note with a tiny sparkle, about 0.4 seconds, clean, no voice, no long tail. |
| `sfx_ep_clank` | 창이 아닐 때 부딪혀 튕겨 나옴 | A small clockwork doll bumped too early: a dull hollow metal-and-wood clank with a short rattle of loose gears, about 0.3 seconds, close and dry, no voice, no music. |
| `sfx_ep_capture` | 석상을 잡아 쓰러뜨림(I 침실에서도 씀) | A wooden chess pawn knocked over and captured: a crisp wooden topple clack and a quick tumble onto a wooden floor, satisfying and playful, about 0.5 seconds, close and dry, no voice, no music. |

### 7.3 G 왕의 행차 회랑 — 킹 썰매 호송 · 모래 칸 · 룩 탑 캐슬링 (`MgKingEscort`)

| 파일 | 나는 때 | 프롬프트 |
|---|---|---|
| `sfx_sled_drag` 🔁 | 썰매를 끄는 동안(속도에 따라 음높이 0.8 ~ 1.2) | Seamless loop of a heavy walnut sled with wooden runners dragged slowly over a polished wooden floor: a steady low scraping rumble with soft rope creaks, even and continuous, close and dry, no voice, no music. |
| `sfx_sand_crunch` 🔁 | 모래 칸 위를 끄는 동안 | Seamless loop of sled runners crunching slowly through coarse sand: a steady granular crunch and hiss, even and continuous, close and dry, no voice, no music. |
| `sfx_stone_drag` 🔁 | 룩 석상이 움직이는 동안 | Seamless loop of a large stone chess rook tower sliding slowly across a stone floor: a deep steady grinding scrape, even and continuous, close, no voice, no music. |
| `sfx_sled_blocked` | 썰매가 막혀 멈춤 | A wooden sled jolting to a stop against a block: one dull wooden thunk with a short rattle of the statue on top, about 0.35 seconds, close and dry, no voice, no music. |
| `sfx_latch` | 정지 칸에서 걸쇠가 걸림 | A brass latch snapping shut as a sled locks into place: one crisp metallic click-clack, about 0.25 seconds, close and dry, no voice, no music. |
| `sfx_castle_land` | 도착 — 룩 탑이 킹을 뛰어넘어 착지(캐슬링) | A big stone chess rook tower hopping over and landing: a heavy satisfying stone thud with a little dust crumble, about 0.8 seconds, close, no voice, no music. |

### 7.4 H 국왕 체스 홀 — 킹 잡기 (`MgCheckmate`)

지금은 다른 소리를 대신 튼다. 프롬프트는 R653이 쓴 것을 그대로 둔다([미니게임 구간](PAWN_RUSH_MINIGAME_SECTIONS.md) 맨 위 표): `sfx_check` · `sfx_mate` · `sfx_king_capture` · `sfx_king_sweep` · `sfx_rescue` · `sfx_stalemate` · `sfx_king_hop` · `sfx_ep_tray`(F 쟁반에서도 씀).

### 7.5 I 국왕 침실 — 체크 막기 (`MgCheckBlock`)

| 파일 | 나는 때 | 프롬프트 |
|---|---|---|
| `sfx_cart_roll` 🔁 | 킹 카트를 옮기는 동안 | Seamless loop of a small wooden cart with brass wheels rolling slowly across a marble floor: a soft steady wheel rumble with faint creaks, even and continuous, close, no voice, no music. |
| `sfx_beam_hum` 🔁 | 체크 빛줄기가 켜져 있는 동안(작게 재생) | Seamless loop of a soft magical energy beam humming: a warm low hum with a faint shimmering overtone, steady and continuous, never harsh, no voice, no music. |
| `sfx_shield_lock` | 방패를 끼워 공격선을 막음 | A brass shield sliding into a rail and locking: a quick metal slide followed by a firm click-clunk, about 0.5 seconds, close and dry, no voice, no music. |
| `sfx_piece_fall` | 공격하던 나이트를 쓰러뜨림 | A tall wooden chess knight toppling onto a wooden board: a short wobble and a hollow wooden clatter as it falls, about 0.6 seconds, close and dry, no voice, no music. |
| `sfx_double_check` | 이중 체크 경고 | A short urgent double warning chime: two quick sharp bell strikes, slightly dissonant, alarming but not harsh, about 0.6 seconds, clean, no voice, no music. |
| `sfx_resolve` | 체크 하나를 풂 | A relieved resolving chime: a soft rising two-note glockenspiel phrase, about 0.5 seconds, clean, no voice. |
| `sfx_ep_capture` | 그 밖의 공격 기물을 잡음 | (F와 같은 파일) |

### 7.6 J 왕궁 세탁장 — 물 위 섬 · 나이트 도약대 · 비눗물 (`MgKnightPads` · `MgWaterReturn`)

| 파일 | 나는 때 | 프롬프트 |
|---|---|---|
| `sfx_pad_launch` | 도약대가 폰을 ㄱ자로 튕겨 올림 | A springy wooden launch pad flinging a small toy figure into the air: a quick taut spring twang with a soft wooden thump and a short whoosh, about 0.4 seconds, playful, close and dry, no voice, no music. |
| `sfx_splash` | 물에 빠짐 | A small toy figure falling into a tub of soapy laundry water: a medium splash with bubbly foam fizz, about 0.8 seconds, close, no voice, no music. |

### 7.7 소드 파이트 스킬 — `Resources/SwordFightSkillSfx/`

| 파일 | 나는 때 | 프롬프트 |
|---|---|---|
| `EdgeCast` | E 스킬 시전(킹 · 퀸 · 룩) | A sword ability powering up: a quick metallic blade ring with a short rising whoosh of energy, about 0.4 seconds, crisp, no voice, no music. |
| `RookBreak` | 룩 E로 바닥이 부서짐 | A heavy stone floor cracking under a mighty blow: a deep crunching crack with small debris, about 0.7 seconds, punchy, no voice, no music. |
| `KingRepel` | 킹이 막기로 공격자를 튕겨냄 | A blocked attacker thrown back: a solid metallic deflect clang followed by a short whoosh, about 0.4 seconds, crisp, no voice, no music. |
| `CastPawn` | 폰이 스킬을 시전 | A small piece readying a quick move: a short light sword shing with a soft whoosh, about 0.3 seconds, crisp, no voice, no music. |
| `PawnStep` | 폰 스킬 스텝 | A quick light dash step: a short sharp whoosh with a soft foot scuff, about 0.25 seconds, close and dry, no voice, no music. |
| `PawnHit` | 폰이 대각선으로 들이받아 맞힘 | A small body checking another: a punchy soft thump with a cloth rustle, about 0.3 seconds, close and dry, no voice, no music. |
| `PawnBounce` | 정면으로 부딪혀 튕김 | A head-on bump that bounces back: a soft rubbery bonk with a tiny rebound, about 0.3 seconds, playful, close and dry, no voice, no music. |

빠진 게 아닌 것: `KingLandHit` · `KingDrop`은 다른 소리를 빌려 쓰고(별칭), `EdgeCancel` · `BishopDrop`은 일부러 소리를 끊는다. `BishopFire` · `BishopSlow` · `BishopPin`은 10-27부터 쓰지 않는 옛 비숍 기술이라, `sfx_mg_install`은 궁전 미니게임에서 재생되지 않는 옛 자리라 프롬프트를 쓰지 않았다.

## 8. 미니게임 소리 적용 (R675)

2026-11-15 사용자 "지금까지 뽑은 효과음들이야 전부 적용시키고 푸시해줘" — §7 프롬프트로 만든 테이크 13개. 파일 이름이 §7의 '나는 때' 문구로 왔고, F의 첫 파일은 구간 이름 "대무도회장 (태엽 인형 무용수)"으로 와서 표 첫 줄 `sfx_ep_ready`로 넣었다(0.5초 · 태엽 따르륵 6번이라 프롬프트와 맞음). 원본은 `ArtSource/PawnRush/Sfx/raw/MiniGame/<파일 이름>.wav`, `ArtSource/PawnRush/Sfx/bake_minigame.py`가 `Resources/PawnRushMiniGameSfx/`에 굽는다. 코드가 이미 이 이름들을 부르고 있어 파일만 넣으면 난다. **사용자 청취 · 플레이 확인 전.**

| 파일 | 원본에서 쓴 곳 | 길이 | 들리는 크기 400 ms / 100 ms (dB) | 다듬은 것 |
|---|---|---|---|---|
| `sfx_mg_complete` | 0.032 ~ 1.0초 | 0.97초 | −18.5 / −15.8 | 왼쪽 채널만(두 채널이 반쯤 반대라 합치면 얇아짐), 끝 0.15초 페이드 |
| `sfx_mg_quarter` | 0 ~ 0.45초 | 0.45초(코드가 1.5배 높여 0.3초) | −24.0 / −20.4 | 250 Hz 아래 웅웅거림 깎음, 오르는 반짝임은 그대로 |
| `sfx_ep_ready` | 0.09 ~ 0.45초 | 0.36초 | −26.6 / −24.7 (×0.6) | 첫 따르륵부터, 피크 8 dB 누름 |
| `sfx_ep_step` | 0.31 ~ 0.45초 | 0.14초 | −25.3 / −23.9 | 원본은 0.3초 미끄러진 뒤 '탁' → 0.15초 간격 두 번이라 미끄러짐 36 ms + '탁'만, 피크 6.5 dB |
| `sfx_ep_window` | 0 ~ 0.486초 | 0.49초 | −21.0 / −17.7 (×0.8) | 왼쪽 채널, 300 Hz 아래 깎음, 끝 0.12초 페이드(원본이 울리다 잘림) |
| `sfx_ep_clank` | 0.934 ~ 1.18초 | 0.25초 | −23.8 / −21.6 | 원본은 2초 동안 태엽이 18번 딸깍임 → 가장 큰 딸깍부터 셋, 피크 8 dB |
| `sfx_ep_capture` | 0.016 ~ 0.45초 | 0.43초 | −27.8 / −22.6 | 첫 '딱'이 몸통보다 20 dB 커서 피크 12 dB 누름. 그래도 400 ms 기준(−20)보다 작다 |
| `sfx_sled_blocked` | 0.504 ~ 0.95초 | 0.45초 | −21.0 / −17.1 | 앞 0.5초 웅웅거림 자름 |
| `sfx_latch` | 0.076 ~ 0.25초 | 0.17초 | −25.3 / −22.9 | 앞 76 ms 빈소리 자름, 피크 8 dB |
| `sfx_castle_land` | 0.028 ~ 0.8초 | 0.77초 | −23.6 / −18.9 | 첫 쾅 피크 12 dB |
| `sfx_sled_drag` 🔁 | 20.6초부터 7.37초 | 7.37초 | RMS −30 (×0.6) | 30초 테이크에서 시작과 끝이 가장 닮은 구간(0.4초 스펙트럼 차 2.9 dB · 앞뒤 3초 크기 차 1.9 dB), 끝을 처음에 0.4초 겹쳐 이음, 피크 2.5 dB |
| `sfx_sand_crunch` 🔁 | 15.4초부터 10.0초 | 10.0초 | RMS −31 (×0.6) | 같은 방법(2.7 · 1.2 dB). 썰매 소리 위에 겹쳐 1 dB 작게 |
| `sfx_stone_drag` 🔁 | 3.05초부터 7.94초 | 7.94초 | RMS −30 (×0.6) | 같은 방법(2.6 · 2.1 dB) |

- **공통:** 모노 48 kHz, 1 ms 페이드 인, 피크 −1 dBFS. 두 채널이 닮으면 합치고(L/R 닮음 0.2 이상) 아니면 큰 쪽 하나. `.meta`는 메모리에 PCM으로 풀어 두고 미리 읽는다(첫 재생 지연 없음). **가져올 때 정규화는 끔** — 이 폴더의 예전 파일들은 켜져 있지만 여기서 맞춘 크기를 지키려고.
- **크기:** §0(R654)의 기준에 부르는 곳의 volume과 기본 효과음 0.8을 넣어 맞춤(괄호의 ×값이 volume). 짧게 '딱' 하는 소리는 400 ms 대부분이 조용해서 400 ms 기준에 맞추려면 피크를 15 ~ 25 dB 눌러야 해 소리가 뭉개진다 → 피크 누름은 8 dB(쓰러뜨림 · 캐슬링 12 dB)까지만 하고 100 ms 크기를 같이 적었다(다른 효과음은 100 ms가 400 ms보다 4 ~ 6 dB 큼). 그래서 쓰러뜨림(`sfx_ep_capture`) · 걸쇠 · 딸깍이 400 ms 표보다 2 ~ 8 dB 작다. 들어 보고 작으면 그 줄의 `most`(누름 한도)를 올린다.
- **반복음이 끊김 없이 돎 (코드):** `ArenaFx.Hold`에 `loop`를 더함 — 전에는 클립이 끝나면 다음 프레임에 처음부터 다시 틀어 이음새마다 한 프레임 비었다. 행차 회랑 세 반복음과, 파일은 아직 없는 I 침실의 `sfx_cart_roll` · `sfx_beam_hum`이 `loop: true`. 반복음은 잘라야 멈추므로 두 게임이 꺼질 때(`OnDisable`) 자른다. 모래 · 룩 반복음도 썰매 · 움직이는 룩을 따라간다(전엔 처음 튼 자리에 남았다). 굽고 나서 잰 이음새의 샘플 튐은 평소 파형 변화보다 작다(썰매 0.0009 / 0.012, 모래 0.008 / 0.033, 룩 0.005 / 0.005).
- **같은 파일이 나는 다른 곳:** `sfx_ep_capture`는 I 침실(공격 기물 잡기)과 J 세탁장(나이트 도약대 발판, 진행에 따라 높아짐)에서도 난다 — 이번에 처음 소리가 난다. `sfx_mg_complete`는 승격 발판을 고를 때도 0.6으로 난다.
- **아직 없음:** `sfx_gate_open`(팀 문 열림), §7.4 ~ 7.7.

## 9. 낙하 · 복귀 소리와 승리 · 패배 음악 (R679)

사용자(6 대 6 시험 뒤) "낙하 시 유저피드백 확실히" · "승리,패배 BGM 추가". 코드 자리는 들어갔고 **파일은 아직 없다.**

| 자리 | 파일 | 지금 | 부르는 곳 |
|---|---|---|---|
| 내 폰이 떨어짐 · 물에 빠짐 | `Assets/Resources/PawnRushMiniGameSfx/sfx_fall.wav` | 없음 → `PawnRushSkillSfx/SkillFail`이 대신 | `PawnRushFallNotice.Begin`(2D, 내 폰만) |
| 체크포인트로 돌아옴 | `Assets/Resources/PawnRushMiniGameSfx/sfx_respawn.wav` | 없음 → `sfx_mg_quarter`(승격 발판 차임)가 대신 | `PawnRushFallNotice.Back`(2D, 내 폰만) |
| 이긴 팀 결과 음악 | `Assets/Resources/ResultMusic/Victory.wav` | 없음 → 무음 | `ResultMusic.Play`(폰 러쉬 승리 장면 · 소드 파이트 결과) |
| 진 팀 결과 음악 | `Assets/Resources/ResultMusic/Defeat.wav` | 없음 → 무음 | `ResultMusic.Play`(폰 러쉬 패배 장면 · 소드 파이트 결과) |

결과 음악은 굽는 단계가 필요 없다: 불러올 때 앞 30초의 RMS를 재서 다른 음악과 같은 크기(기본 음악 음량에서 −28 dBFS)로 맞추고 한 번만 재생한다(반복 없음). 폰 러쉬 결과 장면은 12초 뒤 로비로 가므로 **10 ~ 14초** 길이가 맞다. 효과음 둘은 받으면 `ArtSource/PawnRush/Sfx/bake_minigame.py` 방식으로 앞 빈소리를 자르고 크기를 맞춰 넣는다.

ElevenLabs 프롬프트:

- **sfx_fall** (Sound Effects, 0.7초): `Short cartoon falling whistle: a quick descending slide-whistle "pew-oo" ending in a soft muffled thud, light and comedic, a small wooden chess piece tumbling off a ledge, dry, close, no reverb tail, no music, no voice`
- **sfx_respawn** (Sound Effects, 0.5초): `Short bright magical pop with a tiny rising two-note chime, a wooden chess piece popping back onto a board, playful, clean attack, dry, no reverb tail, no music, no voice`
- **Victory** (Music v2.5, 12초): `Triumphant comedic brass band victory fanfare, 150 BPM, F major, the same marching band as a playful chase theme: bright trumpets and trombones, tuba, snare roll into a big held final chord with a cymbal crash, joyful and a little silly, about 12 seconds, a clear ending, instrumental only`
- **Defeat** (Music v2.5, 10초): `Gentle comedic "better luck next time" brass band sting, slow, F minor resolving to a warm major chord, a muted trumpet wah-wah falling phrase answered by tuba, soft brushed snare, light-hearted and not sad, about 10 seconds, a clear ending, instrumental only`

귀로 들은 것 없음 — 임시 대체음(스킬 실패음 · 승격 차임)이 어울리는지도 사용자 확인 전.

## 10. 마차뜰 말 마차 소리 (R686 소리 1, 2026-11-17, seoungtest)

승규 "사운드 알아서 채우지 말고 하나하나 할거야" — 마차뜰 소리는 목록에서 하나씩 골라 넣는다. 첫 번째 "1번 말 마차"에서 말발굽 · 조준 예고음 · 부딪힘을 골랐고, "너가 만들어 일레븐랩스 mcp 연결해서"라 AI가 ElevenLabs MCP(효과음 `eleven_text_to_sound_v2`, Prompt influence 0.3 기본값, 프롬프트마다 4개)로 만들었다. 바퀴 구르는 소리 · 채찍은 고르지 않아 코드로 만든 소리(`YardKit.RumbleSound` · `CrackSound`) 그대로다. **AI는 소리를 듣지 못해 파형(타격 위치 · 끝 감쇠) · 음높이(자기상관) 숫자로만 골랐다. 사용자 청취 전.**

| 파일 (`Resources/PawnRushCarriageYardSfx/`) | 원본 (`ArtSource/PawnRush/Sfx/raw/CarriageYard/`) | 나는 때 | 들리는 크기 |
|---|---|---|---|
| `HoofLight0` ~ `5` (0.14초) | `hoof_light_1` ~ `4`에서 따로 떨어진 타격 26개 중 끝이 깨끗한 6개 | 걷기 · 속보에서 발굽이 닿을 때 | 한 번 −26 dB (× 0.55 ~ 1) |
| `HoofHeavy0` ~ `5` (0.17초) | `hoof_heavy_1` · `3`의 30개 중 6개 | 반 질주부터 | 한 번 −24 dB (× 0.55 ~ 1) |
| `HorseSnort0` · `1` (1.0 · 1.2초) | `snort_1` · `snort_4`(음높이 없는 콧김) | 조준 시작(빨간 등, 돌진 0.6 ~ 1.2초 전) | 400 ms −21 dB |
| `HorseNeigh0` · `1` (1.6 · 1.4초) | `neigh_3` · `neigh_4`(울음, 기본음 500 ~ 700 Hz) | 조준 시작 | 400 ms −20 dB |
| `CarriageHit0` · `1` (0.35 · 0.5초) | `hit_3` · `hit_1` | 마차 · 말 껍질이 폰을 넉백 | 400 ms −19 dB (넘어지지 않은 밀림 × 0.6) |

프롬프트(영어 그대로, 길이는 효과음 도구가 1 ~ 3초로 정함):
- 발굽 가벼움: Single steel-shod horse hooves striking dry cobblestones one at a time, each strike clearly separated by about half a second of silence, a crisp hard clop with a little grit, close and dry, outdoors, no reverb, no voice, no music. 5 seconds.
- 발굽 무거움: Single heavy steel-shod horse hooves slamming down on cobblestones one at a time during a hard gallop, each strike separated by about half a second of silence, a deep thudding clop with scattered grit, punchy, close and dry, outdoors, no reverb, no voice, no music. 5 seconds.
- 콧김: A powerful carriage horse about to bolt: one sharp nervous snort through the nostrils and a hoof pawing the cobblestones once, about 0.7 seconds, close and dry, outdoors, no reverb, no voice, no music.
- 울음: A spirited royal carriage horse rearing to charge: a short loud high-pitched whinny ending in a snort, about 1.2 seconds, close, outdoors, no reverb, no voice, no music.
- 부딪힘: A heavy wooden royal carriage ramming a small wooden toy figure out of the way: one solid hollow wooden thud with a quick clatter, punchy and a little comical, about 0.5 seconds, close and dry, outdoors, no voice, no music.

- **굽기:** MP3를 Blender(`aud`)로 WAV로 푼 뒤 `"C:/Program Files/Blender Foundation/Blender 5.2/5.2/python/bin/python.exe" -I ArtSource/PawnRush/Sfx/bake_carriage_yard.py`. 모노(두 채널이 닮으면 합침), 1 ms 페이드 인 · 코사인 페이드 아웃, 피크 −1 dBFS(누름 8 dB까지), `.meta`는 R675와 같은 설정(PCM · 미리 읽음 · 정규화 끔). 쓸 테이크 · 크기는 파일 맨 위 표. `RCY_SFX_ROOT`를 주면 다른 폴더에 시험으로 굽는다.
- **발굽 (코드):** `YardHorse.Pose`가 다리마다 걸음 위치가 0을 지나는 순간(발굽이 땅에 닿음, 다리당 0.12초에 한 번까지) `Footfall(다리, 질주 정도, 움직임)`을 부른다 — 그림의 다리와 같은 박자라 모든 PC가 같다. `RoamingCarriage.Sound.cs`가 말 머리 쪽 소리(3 ~ 30 m)에서 한 번씩 낸다: 질주 0.5 이상이면 무거운 소리, 크기는 질주에 따라 0.55 ~ 1, 뒷발 0.85. 같은 파일이 연달아 나오지 않음. 질주하면 말 두 마리가 1초에 약 24번이라 겹쳐서 한 번보다 크게 들린다.
- **조준 예고 (코드):** 조준이 시작되는 프레임에 콧김 · 울음 4개 중 하나(조준 시작 시각으로 골라 모든 PC 같음), 조준에 0.3초 넘게 늦게 들어온 PC는 건너뜀. 소리 자리는 말 머리(5 ~ 40 m).
- **부딪힘 (코드):** 마차 · 말 껍질의 넉백. R686 소리 9부터 다른 타격과 함께 `YardSoundscape.Hits.cs`가 내고 효과 이벤트로 참가자 PC에도 간다(§18). 처음(R686 소리 1)에는 `RoamingCarriage.Sound.cs`가 방장 · 오프라인에서만 냈다.
- 모든 소리는 `SectionSoundGate`를 거친다(구간 밖 무음, 효과음 설정).
- **Unity에서 들을 것:** 마차뜰(맵 시험 또는 구간 라운드)에서 ① 걷는 · 질주하는 발굽이 다리와 맞는지, 너무 시끄럽거나 따닥거리지 않는지(줄일 때 `bake_carriage_yard.py`의 `heard`), ② 빨간 등이 켜지고 돌진하기 전 콧김 · 울음이 들리는지 · 다섯 대가 같은 소리로 느껴지지 않는지, ③ 마차에 맞을 때 '쿵'이 어울리는지(나무 '톡' 같은 소리라 가벼우면 다시 만듦), ④ 바퀴 · 채찍 코드 소리와 같이 들을 때.

## 11. 마차뜰 거대 짐 소리 (R686 소리 2, 2026-11-17, seoungtest)

승규 "6번 거대 짐"에서 "쿵 교체 + 충격파"와 "사슬 내림 · 들어 올림"을 골랐다(크레인 옮김 · 멈춤 예고음은 고르지 않음). §10과 같이 AI가 ElevenLabs MCP로 만들고 숫자로만 골랐다. **사용자 청취 전.** 한 번은 10.4초: 옮김 2.4 → 멈춤 1.0 → 사슬로 내림 1.1 → 놓여 있음 4.5 → 들어 올림 1.4.

| 파일 | 원본 | 나는 때 | 들리는 크기 |
|---|---|---|---|
| `SlamThud0` · `1` (1.3 · 1.2초) | `slam_thud_1` · `4`(무게 중심 660 ~ 730 Hz) | 궤짝이 바닥에 닿는 순간(충격파 시작) | 400 ms −18 dB(위험 큰 타격) |
| `SlamShock0` · `1` (1.6 · 1.4초) | `slam_shock_4` · `2` | 같은 순간, 쿵 밑에 겹침 | 400 ms −24 dB |
| `ChainLower0` · `1` (1.1 · 0.9초) | `chain_lower_4` · `3` | 사슬로 내리기 시작(1.1초) | 400 ms −24 dB |
| `ChainHoist0` · `1` (1.4초) | `chain_hoist_1` · `2` | 들어 올리기 시작(1.4초) | 400 ms −24 dB |

프롬프트:
- 쿵: A huge iron-bound wooden royal chest the size of a cart set down hard on cobblestones: one massive deep wooden boom with a heavy iron clank and a crunch of stone, about 1 second, close, outdoors, no reverb, no voice, no music.
- 충격파: A shockwave rolling across a stone courtyard floor after a giant heavy impact: a deep low rumbling ground tremor with rattling small stones and dust, swelling quickly then fading, about 1.5 seconds, outdoors, no voice, no music.
- 사슬 내림: A heavy iron chain paying out from a wooden crane winch, lowering a huge load: a steady fast metallic chain rattle with a creaking wooden drum and ratchet clicks, about 1.2 seconds, close, outdoors, no reverb, no voice, no music.
- 사슬 감기: A heavy iron chain being wound up onto a wooden crane winch, slowly lifting a huge load: a straining slower metallic chain clatter with a groaning wooden drum and rhythmic ratchet clicks, about 1.5 seconds, close, outdoors, no reverb, no voice, no music.

- **코드:** `SlamChest.Sound.cs`. 닿는 순간은 전과 같은 자리(`LateUpdate`, 한 번에 한 번)에서 쿵 + 울림을 내고, 파일이 없으면 예전 코드 쿵(`ThudSound`). 사슬은 내림(`CargoSlam.Lowered`일 때만) · 들어 올림이 시작될 때 한 소스에서, 0.4초 넘게 늦게 본 PC는 건너뛰고 그 안이면 늦은 만큼 앞을 건너뛰고 튼다. 두 테이크는 번 번호로 번갈아(모든 PC 같음). 소리 자리는 궤짝(쿵 6 ~ 60 m, 사슬 4 ~ 40 m), `SectionSoundGate`를 거침. 거대 짐은 둘(G1 · G2)이고 2.6초 어긋나 번갈아 쿵.
- **Unity에서 들을 것:** ① 닿는 순간과 쿵이 맞는지, 충분히 무거운지 · 너무 큰지, ② 울림이 쿵을 흐리지 않는지, ③ 사슬 소리가 내리는 · 들어 올리는 움직임과 길이가 맞는지.

## 12. 마차뜰 짐더미 줄 소리 (R686 소리 3, 2026-11-17, seoungtest)

승규 "8번 짐더미 줄"에서 "내려놓는 쿵"과 "들기 · 옮기기"를 골랐다(틈 닫힘 예고음 · 밀려남 소리는 고르지 않음). §10과 같이 AI가 ElevenLabs MCP로 만들고 숫자로만 골랐다. **사용자 청취 전.** 짐더미 줄 2개 × 양쪽 절반 = 크레인 4대, 한 번 약 2.5초: 틈 열림 1.1 ~ 1.4 → 들기 0.35 → 옮기기 0.7 → 내려놓기 0.35.

| 파일 | 원본 | 나는 때 | 들리는 크기 |
|---|---|---|---|
| `StackSet0` · `1` (0.55 · 0.6초) | `stack_set_1` · `3` | 짐더미가 새 칸에 놓일 때(다음 번 시작, 먼지 효과와 같은 순간) | 400 ms −22 dB |
| `StackMove0` · `1` (0.9 · 1.4초) | `stack_move_3` · `2` | 틈 열림이 끝나 들기 시작할 때부터 | 400 ms −27 dB |

프롬프트:
- 쿵: A stack of two heavy wooden cargo crates set down by a crane onto cobblestones: one solid dull wooden thump with a short creak and rattle of the crates settling, about 0.6 seconds, close, outdoors, no reverb, no voice, no music.
- 들기 · 옮기기: A small overhead crane trolley lifting a stack of wooden crates on hemp ropes and rolling it sideways along a wooden beam: ropes pulling taut with a creak, a rumble of iron trolley wheels rolling on the rail, the crates swaying, about 1.2 seconds, close, outdoors, no reverb, no voice, no music.

- **코드:** `StackRow.Sound.cs`(`StackRow.LateUpdate` 끝에서 `RowSounds`). 절반마다 트롤리를 따라가는 소리 자리(바닥 위 1.2 m, 옮기기 3 ~ 30 m, 쿵 4 ~ 35 m). 들기는 0.3초 넘게 늦게 본 PC는 건너뜀. 처음 본 순간에는 쿵을 내지 않음. `SectionSoundGate`를 거침.
- **Unity에서 들을 것:** ① 놓이는 순간과 쿵이 맞는지, ② 4대가 계속 움직여 시끄럽거나 지겹지 않은지(줄일 때 `bake_carriage_yard.py`의 `heard`), ③ 옮기기 소리가 트롤리 움직임과 어울리는지.

## 13. 마차뜰 컨베이어 소리 (R686 소리 4, 2026-11-17, seoungtest)

승규 "7번 컨베이어"에서 "벨트 반복음"(ElevenLabs로 새로)과 "짐 덜컥"을 골랐다(창구 들고 남 · 밀려남 소리는 고르지 않음). AI가 ElevenLabs MCP로 만들고 숫자로만 골랐다. **사용자 청취 전.**

| 파일 | 원본 | 나는 때 | 들리는 크기 |
|---|---|---|---|
| `ConveyorBelt` 🔁 (6.6초) | `belt_1` ~ `4`를 이어 붙임 | 항상, 벨트에서 듣는 사람에게 가장 가까운 점에서(4 ~ 30 m) | RMS −30 dB |
| `ConveyorRide` 🔁 (5.0초) | `ride_1` · `3` · `2`를 이어 붙임 | 짐이 벨트 위에 있는 동안 짐마다(2.5 ~ 22 m) | RMS −31 dB |

프롬프트:
- 벨트: Seamless loop of an old wooden roller conveyor running steadily outdoors: many wooden rollers turning on iron axles with a soft continuous rumble, light rhythmic clacking of belt slats and gentle creaks, even and continuous, no motor whine, close, no reverb, no voice, no music. 10 seconds.
- 짐: Seamless loop of a heavy wooden crate riding along a wooden roller conveyor: a steady bumpy rattle and knock as the crate rolls over roller after roller, fast even rhythm, close and dry, outdoors, no voice, no music. 8 seconds.

- **반복음 만들기:** MCP의 효과음 도구는 길이 · 반복 설정을 받지 않아 테이크가 1 ~ 2초다. 그래서 `bake_carriage_yard.py`의 `LOOPS`가 테이크 몇 개를 앞뒤 0.05초 다듬어 0.25초 등전력 크로스페이드로 잇고, 끝을 처음에 같은 방법으로 겹쳐 닫는다(2초마다 같은 소리가 되풀이되지 않게). 이음새 샘플 튐 0.001 · 0.007.
- **코드:** `CargoConveyor.Sound.cs`(`CargoConveyor.Build` 끝에서 `BeltSounds`, 자기 `LateUpdate`). 짐 소리는 짐 오브젝트에 붙어 같이 움직이고, 짐이 벨트 위로 그려질 때 초당 6배 속도로 커지고 바닥 밑(보이지 않는 곳)으로 가면 줄어 멈춘다. 짐마다 반복음 시작 자리를 달리해 함께 웅웅거리지 않게. 모두 `SectionSoundGate`를 거침, 그림에서 읽으므로 모든 PC 같음.
- **Unity에서 들을 것:** ① 벨트 소리가 계속 들리는 게 거슬리지 않는지(크기), ② 짐 덜컥이 짐 속도(3.6 m/s)와 어울리는지 · 짐이 여럿일 때 시끄럽지 않은지, ③ 반복 이음새가 튀지 않는지.

## 14. 마차뜰 물레방아 · 물 소리 (R686 소리 5, 2026-11-17, seoungtest)

승규 "4·5번 물레방아·물"에서 "물레방아 도는 소리", "쏟아지는 물 · 폭포", "흐르는 물"을 골랐다(바퀴살 휙 · 맞음은 고르지 않음). AI가 ElevenLabs MCP로 만들고 숫자로만 골랐다. 테이크가 1 ~ 2초라 §13처럼 이어 붙여 반복음으로 만들었다. **사용자 청취 전.**

| 파일 | 원본 | 나는 곳 | 들리는 크기 |
|---|---|---|---|
| `WheelTurn` 🔁 (5.6초) | `wheel_1` · `3` · `2` · `4` | 거대 물레방아(7 ~ 45 m) · 세로 물레방아(5 ~ 35 m)의 축 | RMS −29 dB × 회전 속도 비율 |
| `WaterPour` 🔁 (4.6초) | `pour_1` · `3` · `4` · `2` | 물이 떨어지는 곳마다(거대 물레방아 버킷, 세로 물레방아 · 절벽, 수문 둘), 그 물 그림의 밑(5 ~ 40 m) | RMS −28 dB × 0.7 = 약 −31 dB (승규 "물 소리가 30퍼 줄여") |
| `WaterFlow` 🔁 (5.6초) | `flow_1` · `2` · `4` · `3` | 운하 · 연못 수면에서 듣는 사람에게 가장 가까운 점(3 ~ 22 m) | RMS −33 dB × 0.7 = 약 −36 dB |

프롬프트:
- 물레방아: Seamless loop of a giant old wooden waterwheel turning slowly: a deep groaning creak of a heavy wooden axle in its bearings, a low wooden rumble, water dripping off the paddles, even and continuous, close, outdoors, no voice, no music. 10 seconds.
- 쏟아지는 물: Seamless loop of water pouring steadily from a wooden waterwheel and a stone sluice into a canal a few metres below: a full continuous splashing waterfall roar with bubbling, even and continuous, close, outdoors, no voice, no music. 10 seconds.
- 흐르는 물: Seamless loop of calm water in a stone canal: gentle flowing water lapping softly against stone walls, small ripples and soft gurgles, even and continuous, close, outdoors, no voice, no music. 10 seconds.

- **코드:** `RoyalCarriageYard.Sound.cs`가 Awake 끝(효과 다음)에 "Yard sounds" 오브젝트와 `YardSoundscape.cs`를 만든다(끄면 이 소리만 꺼짐). 물레방아는 transform 회전을 재서 크기 · 음높이(0.85 ~ 1)를 정해 멈추면 조용해진다. 떨어지는 물 자리는 1초마다 그 `YardWaterfall` 렌더러 범위의 밑 0.5 m. 반복음마다 시작 자리를 달리함. 모두 `SectionSoundGate`를 거치고, 그림에서 읽으므로 모든 PC 같음.
- **크기 조절 (11-17):** 승규 "물 소리가 30퍼 줄여" → 쏟아지는 물 · 흐르는 물만 `YardSoundscape.WaterVolume` 0.7(코드, 파일은 그대로). 물레방아 도는 소리 · 빠질 때 첨벙(정원 소리)은 그대로.
- **Unity에서 들을 것:** ① 물레방아 소리가 바퀴 크기 · 속도와 어울리는지, ② 쏟아지는 물이 버킷 · 폭포 자리에서 들리는지 · 너무 크지 않은지(연못 쪽은 세 곳이 겹침), ③ 흐르는 물이 운하 · 연못 가에서만 은은하게 들리는지, ④ 반복 이음새(쏟아지는 물의 이음새 샘플 튐 0.034는 다른 것보다 큼).

## 15. 마차뜰 목재 크레인 · 다락 승강대 소리 (R686 소리 6, 2026-11-17, seoungtest)

승규 "9·10번 크레인·승강대"에서 "크레인 도는 소리"와 "승강대 오르내림"을 골랐다(종 · 래칫 교체, 목재 휙은 고르지 않음 — 코드로 만든 종 3번 · 래칫 딸깍은 그대로). AI가 ElevenLabs MCP로 만들고 숫자로만 골랐다. **사용자 청취 전.**

| 파일 | 원본 | 나는 때 | 들리는 크기 |
|---|---|---|---|
| `CraneSlew` 🔁 (4.6초) | `crane_4` · `1` · `3` · `2`를 이어 붙임 | 목재 크레인 기둥(5 ~ 35 m), 지브가 도는 동안 | RMS −29 dB × 도는 속도 비율(36°/초에 1) |
| `HoistMove0` · `1` (1.5초) | `hoist_move_1` · `3` | 승강대 짝이 바뀌기 시작할 때(오르내림 1.5초), 기둥 위 팔 높이(3 ~ 28 m) | 400 ms −25 dB |
| `HoistLand0` · `1` (0.5초) | `hoist_land_1` · `3` | 오르내림이 끝날 때 각 판자 자리: 바닥에 닿는 쪽 1, 다락에 닿는 쪽 0.5(3 ~ 25 m) | 400 ms −22 dB |

프롬프트:
- 크레인: Seamless loop of a big wooden jib crane slowly swinging round on its post with a timber load hanging from ropes: a deep groaning wooden creak of the turning post, straining hemp ropes and a low grinding of the iron pivot, even and continuous, close, outdoors, no voice, no music. 10 seconds.
- 승강대 오르내림: A wooden hoist platform being hauled up on hemp ropes through wooden pulleys: ropes running fast over squeaking pulley wheels with a creaking wooden frame, about 1.5 seconds, close, outdoors, no reverb, no voice, no music.
- 승강대 닿음: A heavy wooden pallet platform coming down on ropes and landing on a stone floor: one solid hollow wooden clunk with a short rattle of slack rope, about 0.5 seconds, close, outdoors, no reverb, no voice, no music.

- **코드:** `SlewCrane.Sound.cs`(`SlewCrane.LateUpdate` 끝 `SlewSound`, 방향 변화로 도는 속도를 재고 30° 넘게 튀면 컷으로 봄), `PalletHoist.Sound.cs`(`PalletHoist.LateUpdate` 끝 `HoistSounds`, 래칫과 같은 짝 바뀜 시각, 0.3초 넘게 늦게 본 PC는 건너뜀). 테이크는 짝 바뀜마다 번갈아. 모두 `SectionSoundGate`, 공유 시계라 모든 PC 같음.
- **Unity에서 들을 것:** ① 크레인이 돌 때만 삐걱이고 쉴 때 조용한지, ② 승강대 밧줄 소리가 오르내림과 길이가 맞는지 · 닿는 쿵이 판자가 멈추는 순간인지, ③ 종 · 래칫(코드 소리)과 같이 들을 때 어색하지 않은지.

## 16. 마차뜰 출발 성문 · 최종 성문 소리 (R686 소리 7, 2026-11-17, seoungtest)

승규 "2·12번 성문"에서 "출발 성문 열림"과 "최종 성문 밀림"을 골랐다(최종 성문 다 열림 쿵은 고르지 않음). AI가 ElevenLabs MCP로 만들고 숫자로만 골랐다. **사용자 청취 전.**

| 파일 | 원본 | 나는 때 | 들리는 크기 |
|---|---|---|---|
| `GateOpen0` · `1` (2.7 · 1.7초) | `gate_open_3` · `4` | 출발 성문이 열리기 시작할 때(2.5초 동안 열림), 문 가운데(6 ~ 45 m) | 400 ms −20 dB |
| `GatePush` 🔁 (5.6초) | `gate_push_1` · `4` · `2` · `3`을 이어 붙임 | 최종 성문이 밀려 열리는 동안, 문 가운데(5 ~ 40 m) | RMS −27 dB × 열리는 속도 비율(한 사람 속도에 거의 1) |

프롬프트:
- 출발 성문: A pair of huge iron-bound wooden castle gates unlocking and swinging open by themselves: one heavy iron bolt clunk, then a long deep creak of big iron hinges as the leaves swing slowly open, ending with a soft wooden thud, about 2.5 seconds, close, outdoors, no reverb, no voice, no music.
- 최종 성문: Seamless loop of a massive wooden castle gate being slowly pushed open by hand: a steady deep groaning creak of heavy iron hinges and the gate's foot scraping across stone, even and continuous, close, outdoors, no voice, no music. 10 seconds.

- **코드:** `TimedGate.Sound.cs`(`TimedGate.Update`에서 `GateSound`, 공유 시작 시각 `openAt`에 한 번, +x로 열리는 문짝만, 0.5초 넘게 늦게 본 PC는 건너뜀 · 그 안이면 늦은 만큼 앞을 건너뜀). `PushGate.Sound.cs`(자기 `LateUpdate`, 진행 값의 초당 변화를 0.3초쯤 부드럽게 해 크기 · 음높이 0.9 ~ 1.05, 참가자 PC는 방장이 보내는 진행 값으로 같음). 모두 `SectionSoundGate`.
- **Unity에서 들을 것:** ① 출발 문이 열릴 때 빗장 · 삐걱이 문 움직임과 맞는지(2.7초 테이크는 끝에 문 닫히는 쿵), ② 최종 문을 밀 때만 삐걱 · 끌림이 나고 손을 떼면 멈추는지, 여럿이 밀 때 커지는지.

## 17. 마차뜰 타격음 (R686 소리 8, 2026-11-17, seoungtest)

승규 "13번 타격음". 마차뜰 장애물이 폰을 쳐 낼 때(모든 `YardImpact`가 `YardKit.HitGiven`을 냄) 나는 소리. AI가 ElevenLabs MCP로 만들고 숫자로만 골랐다. **사용자 청취 전.**

| 파일 | 원본 | 나는 때 | 들리는 크기 |
|---|---|---|---|
| `HitBump0` ~ `2` (0.18 ~ 0.32초) | `hit_bump_1` · `2` · `3` | 컨베이어 짐 · 짐더미 · 승강대 판자 · 목재 크레인 짐에 맞음 | 400 ms −20 dB (밀림만 × 0.6) |
| `HitPaddle0` ~ `2` (0.14 ~ 0.69초) | `hit_paddle_1` · `3` · `4` | 거대 · 세로 물레방아 날개에 맞음 | 400 ms −20 dB (밀림만 × 0.6) |

프롬프트:
- 나무 쿵: A heavy wooden crate shoving a small wooden toy figure off its feet: one punchy hollow wooden bump with a quick clatter, slightly comical, about 0.4 seconds, close and dry, outdoors, no voice, no music.
- 날개 철썩: A big wet wooden waterwheel paddle swatting a small wooden toy figure aside: one strong hollow wooden whack with a splash of water drops, punchy and a little comical, about 0.5 seconds, close, outdoors, no voice, no music.

- **코드:** `YardSoundscape.Hits.cs`(마차뜰 "Yard sounds"의 `YardSoundscape`, 소리 4개를 돌려 써 가까운 타격끼리 끊지 않음). 맞은 폰의 엉덩이 자리(3 ~ 30 m), 음높이 ±6 %. 마차 · 말(`CarriageHit`, §10)도 여기서 낸다(R686 소리 9). 거대 짐(충격파의 쿵 · 울림)은 여기서 내지 않음. 마차뜰 밖(다른 맵의 같은 부품)은 무시. `SectionSoundGate`를 거침.
- **온라인 (R686 소리 9):** 넉백은 폰을 움직이는 PC(오프라인 · 방장)에서만 계산되므로 타격음은 효과 이벤트 `SkillFxKind.CourseHit`로 참가자 PC에 보낸다(§18). 처음에는 방장 · 오프라인에서만 들렸다.
- **Unity에서 들을 것:** ① 짐 · 짐더미 · 판자 · 목재에 맞을 때 쿵이 어울리는지, ② 물레방아 날개 철썩이 어울리는지, ③ 너무 자주 · 크게 나지 않는지.

## 18. 마차뜰 소리를 온라인 참가자 PC에서도 (R686 소리 9, 2026-11-17, seoungtest)

승규 "온라인 참가자 pc 에서도 다 들리게 해 main에 합칠거니까 / 지금은 합치지는 않을거야". 마차뜰 소리마다 참가자 PC에서 무엇으로 소리 낼 때를 아는지 따졌다. **두 PC로 들어 본 것 없음, main에 합치지 않음.**

| 소리 | 참가자 PC가 아는 것 | 결과 |
|---|---|---|
| 말발굽 · 콧김 · 울음, 바퀴 · 채찍(코드) | 마차 계획(공유 시계 + 시드, 패킷 없음)과 그려진 다리 | 전부터 들림 |
| 거대 짐 쿵 · 울림 · 사슬, 쿵(코드) | 공유 시계 | 전부터 들림 |
| 짐더미 줄, 목재 크레인 · 종(코드), 승강대 · 래칫(코드) | 공유 시계 | 전부터 들림 |
| 컨베이어, 물레방아, 쏟아지는 물 · 흐르는 물 | 그려진 짐 · 바퀴 · 물(공유 시계로 움직임) | 전부터 들림 |
| 출발 성문 | 라운드가 모든 PC에 주는 시작 시각(`StartAt`) | 전부터 들림 |
| 최종 성문 밀림 | 방장이 코스 상태로 보내는 진행 값(`PushGate`, R508) | 전부터 들림(진행 값이 패킷마다 계단처럼 와서 0.3초쯤 부드럽게 함) |
| 물에 빠짐(정원 첨벙) | 각 PC에서 움직이는 폰의 콜라이더가 물 판에 들어감(`GardenSplashPool`) | 전부터 들림 |
| **타격음(마차 · 말 쿵, 짐 · 짐더미 · 판자 · 목재 쿵, 물레방아 날개 철썩)** | 전에는 아무것도 몰랐다(넉백은 방장만 계산) | **이번에 고침: 효과 이벤트로 보냄** |

- **타격음을 보내는 길:** 방장(오프라인은 그 PC)에서 `YardImpact` 넉백 → `YardKit.HitGiven` → `YardSoundscape.Hits`가 종류(0 짐 쿵 · 1 물레방아 날개 · 2 마차)를 정해 `Gameplay.CourseHitSound.Hit` → 래그돌 쪽 `CourseHitRelay`가 맞은 폰의 효과 이벤트 `SkillFxKind.CourseHit`로 올림(`count` 종류, `size` 세기 1 / 0.6, `at` 엉덩이) → 방장의 효과 이벤트 흐름(`LabNetLink`, R652 움직임 소리와 같음)이 참가자에게 보냄 → 참가자는 그 폰을 그리는 시각에 다시 올림 → `CourseHitRelay`가 `CourseHitSound.Hear` → 그 PC의 `YardSoundscape`가 소리 냄. 방장은 올리는 순간 자기 화면에서 한 번, 참가자는 받은 때 한 번이라 두 번 나지 않는다.
- **어셈블리 경계:** `Gameplay`(`CourseHitSound`)는 래그돌 타입을 모르고 숫자 · 위치만 넘긴다. `PawnRush`(마차뜰)는 래그돌을 참조하지 않고, `RagdollLab`(`CourseHitRelay`)이 `Gameplay`의 다리를 채운다(`CourseSkills` · `CourseSoundPlace`와 같은 방식). 경계 검사(`run-tests-linux.sh`의 `check_boundaries`)를 Git Bash에서 돌려 PASS.
- **프로토콜:** 효과 이벤트에 새 종류 하나(`CourseHit`, 열거형 맨 끝)만 더해 패킷 구조 · 크기는 그대로다. 모르는 종류는 받는 쪽이 건너뛰므로(R652와 같음) `SteamSession.Protocol`은 올리지 않았다. 옛 빌드 방장 + 새 빌드 참가자면 타격음만 안 들린다.
- **main과:** 로컬 `main` · `origin/main`의 `SkillFxKind`도 `CourseLand`에서 끝나 `CourseHit`이 같은 번호를 받는다(fetch는 안 함 — 합치기 전에 다시 볼 것).
- **두 PC에서 들을 것:** 참가자 PC에서 ① 마차 · 말에 맞을 때 쿵, ② 컨베이어 짐 · 짐더미 · 승강대 판자 · 목재 짐에 맞을 때 쿵, ③ 물레방아 날개 철썩이 맞는 순간에 한 번씩 나는지(방장 화면과 같은 순간인지), 방장 PC에서도 한 번씩만 나는지. 그 밖의 마차뜰 소리도 참가자 PC에서 방장 PC와 같이 들리는지.

## 12. 왕실 천문대 장치 소리 자리 (R725 ro-vfx, 11-20)

소리는 아직 없다. 유성우 · 태양 플레어 · 초점 광선 · 조리개와 화면 테두리 섬광(본인만)에 소리를 붙일 순간 · 코드 자리 · 파일 이름 · 프롬프트: [PAWN_RUSH_OBSERVATORY_SFX.md](PAWN_RUSH_OBSERVATORY_SFX.md).
