# 폰러시 경기 오디오

2026-11-14 · R652. 경기 음악과 폰의 움직임 소리를 다룬다. 모두 사용자가 ElevenLabs로 만든 테이크이며, 프롬프트는 같은 날 채팅에서 썼고 다이빙 · 잡기 · 점프는 사용자 화면 녹화를 참고했다. numpy 스크립트로 다듬었다. **사용자 청취 · 플레이 확인 전**이며, 확인할 항목은 [VALIDATION](../Network/VALIDATION.md) 맨 위에 있다. 로비 음악은 [UI · 로비 음악](../Architecture/UI.md)에 따로 있다. 대계단 · 유리 · 킹 잡기 소리는 R653이 넣었다(§5).

## 1. 경기 음악

| 항목 | 내용 |
|---|---|
| 곡 | 150 BPM · F장조 · 128초. "우당탕탕 우스꽝스러운 브라스 밴드 추격" 프롬프트로 만든 테이크. 원본은 `ArtSource/PawnRush/Music/raw/PawnRushRace_2026-11-14.wav` |
| 자르기 | `ArtSource/PawnRush/Music/bake.py`가 두 파일로 자른다. `Resources/PawnRushMusic/RaceMusic.wav` = 0 ~ 28.40초(한 번), `RaceMusicLoop.wav` = 그 뒤 89.6초(56마디, 반복). 117.98초가 28.38초와 같은 음악이라 이어진다(드럼 타격 일치 0.90, 이음새 앞뒤 파형 닮음 0.88 · 0.91). 후보 여섯 개를 `--try`로 재서 가장 긴 것을 골랐다 |
| 재생 | `PawnRush/PawnRushRoundMusic.cs`, 라운드를 지을 때(`PawnRushRound.Build`) 붙는다. 입장 화면이 닫히면 오디오 시계에 처음 부분과 루프를 이어 걸고 1초 페이드 인, 승리가 정해지면 1초 페이드 아웃 후 멈춘다. 구간 시험 장면도 같은 라운드라 같은 음악이 나온다 |
| 크기 | `GameSettings.Music` × 0.3687(`RACE_GAIN`). 기본 음악 크기에서 RMS −28 dBFS로, 로비보다 2 dB 작다 |
| 가져오기 | 압축해 메모리에 둠, Vorbis 0.8, 48 kHz |

## 2. 움직임 소리 (한 번씩 나는 소리)

| 소리 | 클립 (`Resources/PawnRushSkillSfx/`) | 나는 때 | 크기 (`PawnRushSkillSfx.Level`) |
|---|---|---|---|
| 다이빙 '휙' | `CourseDive` 0.15초. 원본의 오른쪽 채널만 씀(두 채널이 거의 반대라 합치면 소리가 얇아짐) | 좌클릭 다이빙이 시작될 때 | 0.41 |
| 잡기 '햡' | `CourseGrab` 0.12초 | 손이 폰 · 물건 · 벽 · 밧줄을 처음 잡을 때(양손이 동시에 잡아도 한 번) | 0.32 |
| 점프 '퐁' | `CourseJump` 0.17초 | 바닥에서 점프할 때. 턱을 잡고 넘을 때는 1반음 높게 0.8배 크기 | 0.41 |
| 착지 '톡' | `CourseLandLight` 0.2초 | 서서 착지: 0.18초 넘게 떠 있다가 2.2 m/s 넘는 속도로 내려왔을 때 | 0.33 × 속도(2.2 → 6 m/s일 때 0.6 → 1) |
| 착지 '털썩' | `CourseLandFlop` 0.41초 | 늘어진 몸(다이빙 끝 · 넘어짐 · 구르기)이 0.12초 넘게 0.9 m/s 넘는 속도로 내려와 엉덩이가 서 있는 높이의 3/4 아래로 왔을 때, 또는 서서 7 m/s 넘게 착지했을 때 | 0.53 × 속도(1 → 7 m/s일 때 0.7 → 1) |

- **안 나는 때:** 벽 · 턱 · 밧줄 · 갈고리 · 물에 있을 때, 계단을 내려갈 때(기준 미만), 2.5초 넘게 떨어진 뒤(부활), 같은 폰의 앞 착지에서 0.2초(털썩은 0.4초) 이내일 때.
- **재생:** `PawnRushSkillSfx`가 낸다(코스 스킬 `PawnRushCourseSkills`와 같이 붙음). 매번 음높이를 ±0.35반음 흔들고, 같은 프레임에 같은 소리가 여럿이어도 폰마다 따로 낸다. 코스 구간의 거리 규칙(`CourseSoundPlace`)을 따른다.
- **온라인:** 소리가 나는 순간은 몸을 시뮬레이션하는 PC(오프라인은 그 PC, 온라인은 방장)에서 `RagdollLab/RagdollPawn.CourseSounds.cs`가 `SkillFxKind.CourseDive · CourseGrab · CourseJump · CourseLand`로 올린다. 방장의 효과 이벤트 흐름(스킬과 같은 것)이 이를 참가자에게 보내고, 참가자는 화면의 폰과 같은 시각에 재생한다.
  - 이벤트 하나는 32바이트이고, 12인 기준 방장 업로드가 약 7% 늘 것으로 추정한다(측정 아님).
  - 모르는 효과 종류는 무시하므로 프로토콜 번호는 그대로 두었다([DECISIONS R652](../Project/DECISIONS.md)).
- **크기 기준:** 로드할 때 정규화된다(`Prepare`: 가장 큰 0.3초를 −21 dB로, 최대 8배). 그 뒤 50 ms 크기로 재면 폰 대시가 −22 dB, 스킬 타격이 −15 ~ −17 dB다. 움직임 소리는 그 아래로 맞췄다: 다이빙 −21, 잡기 −23, 점프 −24, 톡 −23, 털썩 −19 dB.

## 3. 발소리

| 항목 | 내용 |
|---|---|
| 클립 | `Resources/PawnRushStepSfx/Walk0` ~ `Walk5`(걷기 테이크에서 한 걸음씩 6개, 0.16초)와 `Run0` ~ `Run5`(달리기 테이크, 0.11초). 걸음마다 같은 크기로 맞췄다 |
| 재생 | `RagdollLab/PawnRushSkills/PawnRushStepSfx.cs`, 코스 스킬과 같이 붙는다. 모든 PC가 화면에 보이는 폰마다 걸음 박자를 센다(아래 조건). 같은 클립이 연속으로 나오지 않고, 음높이를 ±0.5반음 흔든다 |
| 크기 | 걷기 0.29 · 달리기 0.21 × 속도(0.75 ~ 1) × 효과음 설정 × 구간 거리 규칙 × 카메라 거리(6 m부터 줄어 26 m에서 0). 대략 걷기 −30 · 달리기 −28 dB |
| 네트워크 | 보내지 않는다(폰마다 초당 3번). 각 PC가 화면의 움직임에서 만든다 |

박자를 세는 조건:
- **발로 서 있을 것.** 그 PC가 시뮬레이션하는 몸은 Grounded이고, 서 있고, 다이빙 · 벽 · 물 · 밧줄이 아니어야 한다. 참가자 화면의 폰은 엉덩이 아래 바닥이 서 있는 높이에 있고, 위아래 속도가 1 m/s 미만이어야 한다.
- **움직일 것.** 바닥 기준으로 0.7 m/s 넘게 움직여야 한다.
- **박자.** 속도에 따라 초당 1.6 ~ 2.9걸음(달리기 2.5 m/s) ~ 3.5걸음(전력질주 3.6 m/s).
- **클립 고르기.** 전력질주(Shift, 참가자 화면에서는 3.05 m/s 초과)면 Run, 아니면 Walk.

## 4. 바꾸는 법

- **원본 바꾸기:** `raw/`의 파일을 바꾸고 저장소 루트에서 스크립트를 다시 돌린다. 이 PC에서는 Blender 파이썬으로 실행한다.
  - 움직임 소리: `"C:/Program Files/Blender Foundation/Blender 5.1/5.1/python/bin/python.exe" -I ArtSource/PawnRush/Sfx/bake_moves.py`
  - 음악: `ArtSource/PawnRush/Music/bake.py`. `--try 시작 끝`으로 이음새를 잰 뒤 `START`·`END`를 고치고, 출력된 `RACE_GAIN`을 `PawnRushRoundMusic.Gain`에 넣는다.
- **크기 조절:** `PawnRushSkillSfx.Level`의 `Course*` 값, `PawnRushStepSfx.WalkGain` · `RunGain`.
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
