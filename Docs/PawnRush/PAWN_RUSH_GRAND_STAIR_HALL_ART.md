# 대계단 현관홀(2-1) 아트 — 이어서 작업하는 사람을 위한 안내

> main R309(2026-11-05). 승규 님이 `seoungtest` 브랜치의 시험 씬(구역 H)에서 만든 대계단 현관홀 아트를 "여기서 작업은 끝났어 … 다른 사람이 이어서 작업 할 수 있게 main에다가 넣어"로 main에 옮겼다. 블록아웃(길·충돌·장애물 판정)은 [PAWN_RUSH_SECTIONS](PAWN_RUSH_SECTIONS.md)·[기획 v0.3](PAWN_RUSH_GRAND_STAIR_HALL_DESIGN_v0.3.md)이 그대로 맞고, 이 문서는 그 위에 입힌 **그림(모델 · 재질 · 효과)**만 다룬다.
>
> **번호 주의:** 코드 주석의 R308 ~ R413은 `seoungtest`의 번호다(main의 R308 · R309와 다름). 요청 하나하나의 원문 · 결과는 `seoungtest`의 `HANDOFF.md` 머리글과 `Docs/Project/REQUIREMENTS.md`에 있다.

## 1. 어떻게 도는가

| 단계 | 하는 일 | 코드 |
|---|---|---|
| 1. 블록아웃 | 프리팹 `Prefabs/PawnRushSections/Mid_2-1_GrandStairHall.prefab`의 `Content`. 메뉴 **ChessFight → Pawn Rush → Build → Mid 2-1 Grand Stair Hall**로 굽거나 Play 때 코드로 지음 | `GrandStairHallBuilder*.cs` |
| 2. 옛 블록아웃 다시 짓기 | `Content` 밑에 `Layout v2 (seoungtest R413)` 표시가 없으면(이번 전에 구운 프리팹) Play 때 새로 지음. 아트가 지금 블록아웃(나선 발치 · 환풍기 턱 · 퀸 석상 블록 등)을 전제로 하기 때문 | `GrandStairHall.Awake`, `GrandStairHallBuilder.LayoutMark` |
| 3. 아트 입히기 | **Play 때만.** 아트는 재질 · 메시를 코드로 새로 만드는데 프리팹 에셋은 그것을 담을 수 없어서 굽지 않는다. 블록아웃 조각은 그림만 끄고 충돌은 그대로 | `Course01v4Builder.DressGrandStairHall` → `GrandStairHallModel` |

끄기: 프리팹의 `GrandStairHall` → `Dress In Play` 체크 해제(블록아웃만 보임).

**Scene 창(Play 전)에는 블록아웃만 보인다.** 아트를 보려면 `PawnRush_Sections` 시험 씬에서 Play.

## 2. 무엇이 있는가(구획별)

| 구획 | 그림 | 코드(`Assets/Scripts/PawnRush/`) | 자산 |
|---|---|---|---|
| 홀 전체 | Blender 홀 v15(나선 띠 · 아치 · 원형 벽 · 돔 · 바닥) | `Course01v4Builder.GrandStairHallModel.cs`(차례와 홀 재질), `HideListed`가 블록아웃 그림 끔 | `Resources/PawnRushGrandStairHall/GrandStairHallV15.fbx` + `_Hidden.txt` |
| 벽 · 장식 | 크림 석회암 + 금 몰딩, 원형 벽은 금 없음, 창살 무광 은색 | `GrandStairHallModel.cs`(`PalaceMaterial` · `PlainPalaceWall` · `MatteSilver`) | 셰이더 `PR_PalaceStone` · `PR_PalaceWall` · `PR_PalaceTrim`, `PalaceTrim.png` |
| 바닥 · 계단 | 칼라카타 오로 대리석(월드 좌표), 이음새 없음 | `.GrandStairHallFloors.cs` · `.GrandStairHallStair.cs` | `PR_VeinedMarble` · `PR_CalacattaOro.mat` |
| 대계단(비트 1) | 대리석 계단 · 빨간 카펫 · 금 난간 · 왕관 등불 | `.GrandStairHallStair.cs`(`RoyalStair`) | `RoyalStair.fbx` · `RoyalStair_Marble*.png` · `HallCarpet.png` |
| 굴림대 | 카펫 말이(뛰어넘기) · 흰 대리석 폰(피하기), 튀고 흔들림 | `RollingSpawner.cs` | `RollingCarpet.fbx` · `RollingStatue.fbx` |
| 계단 옆 석상 4 | 흰 대리석 + 트래버틴 받침 | `.GrandStairHallStatues.cs`(`StairStatues`) | `StairStatuesV36.fbx`, `PR_StatueMarble` · `PR_Travertine` |
| 중앙 계단참 석상 | 퀸(받침 없음, 대리석 + 금 왕관 · 홀) | `.GrandStairHallStatues.cs`(`LandingQueen`), 블록 `GrandStairHallBuilder.Beats` | `QueenStatue.fbx` |
| 샹들리에 낙하 | 예고 뒤 나타나 떨어져 튐 + 바닥 예고 VFX | `ChandelierRain.cs`, `FallingPieceFx.cs` | `FallingChandelier.fbx`, `VFX/Shaders/VFX_*` |
| 나선 길 | 체스판(시작 1 m는 대리석 + 금 문턱), 금 테 | `.GrandStairHallStair.cs`(`SpiralPaving` · `SpiralFootJoin`), `HallAnchoredMaterials.cs` | `PR_SpiralChecker` |
| 해머 2 | 은색 받침 · 금 사슬 13고리(흔들리며 휨) · 공 궤적 | `.GrandStairHallHammer.cs`, `HammerChainSway.cs` | `PendulumHammer.fbx` |
| 떠 있는 발판 · 점프 발판 | 발판 모델, 점프 발판 사각 빛 VFX | `.GrandStairHallModel.cs`(`HangPads`), `JumpPadFx.cs` | `FloatingPad.fbx` · `JumpPadModel.fbx` + png |
| 바람 길 | 왕실 환풍기(회전) + 바람 VFX, 돌 턱 | `.GrandStairHallWind.cs`, `RoyalFanSpin.cs`, `VentWindFx.cs` | `RoyalFan.fbx` + png, `VFX_WindFlow` · `VFX_WindSwirl` |
| 떨어지는 기사 | 기사 모델 + 바닥 예고 데칼 | `.GrandStairHallKnights.cs`, `FallingPieceFx.cs` | `FallingKnight.fbx` |
| 왕실 발코니 | 벽감 · 시계 대문 · 근위 기사 예고(유령 리허설) | `.GrandStairHallBalcony.cs`, `CavalryWarnFx.cs` | `RoyalBalconyV34.fbx` · `RoyalBalconyMovers.fbx` |
| 입구 · 현관 옆벽 | 손으로 지은 입구(아치는 호두나무), 막힌 창 | `.GrandStairHallEntrance.cs` | `EntranceV46.fbx` · `FoyerSidesV47.fbx` + `_Lights.txt`, `PR_WorldWood` |
| 돔 | 크림 돌 뼈대 + 깎인 모서리 금선, 금 천창 | `.GrandStairHallDome.cs`, `HallAnchoredMaterials.cs` | `PR_PalaceTrim` |
| 난간 | 호두나무(월드 좌표 결) | `.GrandStairHallEntrance.cs`(`WoodenGateAndRails`) | `PR_WorldWood` |

## 3. Blender 원본

`ArtSource/PawnRush/GrandStairHall/`: 스크립트(`*.py`, 파일 머리 주석에 실행 방법), 최신 `.blend` 7개(`grand_stair_hall_v47C` = 홀 전체의 마지막 상태, 나머지는 부품), `source/`(받은 Tripo 원본 등), `textures/`, 최신 렌더 폴더. 홀 `.blend`의 옛 버전(v2 ~ v46C, 약 2 GB)과 옛 렌더는 `seoungtest` 브랜치에만 둔다.

자주 쓰는 것: 해머 `hammer_v51.py`(사슬 고리 따로), 퀸 `queen_statue_v52.py`, 환풍기 `royal_fan_v35.py`, 입구 `entrance_v46.py`, 대계단 `royal_stair_v45.py`. 내보내면 `Assets/Resources/PawnRushGrandStairHall/`의 FBX를 덮어쓴다.

## 4. 함정(겪은 것)

- **월드 좌표를 재질에 바로 넣지 않는다.** 구역 · 구간은 지은 뒤 옮겨진다. 돔 중심 · 나선 문턱처럼 위치가 필요한 셰이더 값은 `HallAnchoredMaterials`로 넣는다(옮겨지면 다시 씀).
- **모델에 UV가 없는 것이 많다**(홀 · 입구 · 난간). 그래서 대리석 · 돌 · 나무는 월드 좌표 셰이더다.
- **`MeshCutOut`은 Play 때만 자른다**(계단참 난간, 단상 탑 기둥). 플레이어 빌드에서는 FBX의 Read/Write가 켜져 있어야 한다.
- **메시를 따로 만든 `.meta`는 설정을 확인한다**(PNG가 큐브맵으로 들어온 적 있음).

## 5. 확인 상태

전부 **Unity 미확인**(코드 컴파일만). 확인 목록은 [VALIDATION](../Network/VALIDATION.md)의 R309 표.

## 6. 소리 (R531~)

**R653 · 2026-11-14.** 대계단 장애물의 최종 음량에 0.55와 설정의 효과음 음량을 곱한다. 공통 잔향은 2.2초에서 0.4초로 줄였다. 낙하 기사 착지는 A1의 앞 0.8초를 한 번 재생한다. 유리 징검다리는 약한 판이 무너질 때 그 자리에서 짧은 파열음을 낸다. 아래 기록은 각 소리의 제작 이력이다. [현재 믹스와 교체용 파일](PAWN_RUSH_AUDIO.md)

- 1번 구간 굴림(R531): `RollingSpawner.Sound.cs`. 굴림마다 반복음(석상 = 묵직한 돌, 카펫 = 부드러운 천)이 그림에 붙어 굴러 내려오고, 계단 착지마다(`Track.hits`의 위치 · 속도) 착지음이 세기에 맞춰 남. 공중에서는 반복음 1/4, 배수구 마지막 1 m에서 0. 3D 선형 감쇠(석상 4 → 42 m, 카펫 3 → 30 m) + 돌 홀 울림 2.2초, `SectionSoundGate`로 홀 안에서만. "조금만 더 작게"(11-09)로 굴림 · 착지 모두 80 %, 이어서 "여기서 20퍼 더 낮춰"로 64 %(`RollVolume` .64). 착지 바닥 구별(11-09 "카펫 쪽에서 떨어지는 거랑 대리석에서 떨어지는 사운드 구별해"): 줄 x가 빨간 카펫 반폭 4.5 m 안(가운데 두 줄 ±2 m)이면 카펫 위 착지음(`HallRoll{Statue,Carpet}StepRunner`, 고음을 깎아 둔하고 2 dB 작게), 바깥 두 줄(±6 m)은 대리석 착지음. 파일 `Resources/PawnRushHallSfx/HallRoll{Statue,Carpet}{Loop,Step,StepRunner}.wav`, 만드는 법 `ArtSource/PawnRush/GrandStairHall/hall_roll_sounds.py`.
- 굴림 구멍 예고음(R532): `RollingSpawner.Sound.cs`의 `Warn`. 구멍이 빛나는 0.8초(`warning`) 동안 그 줄 구멍 자리(줄마다 따로 둔 `Hole sound` 오브젝트, 빛 오브젝트는 꺼졌다 켜져서 쓰지 않음)에서 낮은 돌 울림이 차올라 굴림이 나올 때 가장 커짐. 석상은 음높이 0.94배, 카펫은 1.04배. 3D 선형 감쇠 4 → 36 m, 음량 0.7 × `RollVolume`, 돌 홀 울림 · 홀 안에서만. 늦게 본 PC는 지난 만큼 뒤에서 시작하고, 마지막 0.3초에 처음 본 PC는 소리를 내지 않음. 파일 `HallRollHoleWarn.wav`(땅 밑 울림 + 돌 문 긁힘 1/4).
- 샹들리에(R533): `ChandelierRain.Sound.cs`. 예고음 `HallChandelierWarn` = 시안 "A 째깍 원"(직접 합성): 바닥 원(`VfxGroundWarn`, warning 1.2 + fall 0.4 = 1.6초, 눈금 24개, 마지막 0.35초 1초에 7번 깜빡임)과 같은 시계로 0.2초마다 째깍(조금씩 높아짐), 깜빡임(1.286 · 1.429 · 1.571초)마다 523 Hz 삑. 원이 나타날 때 시작하고 늦게 본 PC는 그만큼 뒤에서(0.3초 넘으면 안 냄). 깨짐 `HallChandelierBreak` = ElevenLabs Smash 3(묵직한 쾅 + 유리, 1.35초), 떨어지는 순간(warning + fall). 둘 다 풀에서 꺼낸 샹들리에 루트(떨어지는 자리)에 붙음, 예고 4 → 32 m · 음량 0.55, 깨짐 6 → 55 m · 0.35(R534 "깨지는 소리 줄여" 0.6 → "더 줄여줘" 0.35, 처음 1.0), 돌 홀 울림, 홀 안에서만. warning · fall 값을 바꾸면 예고음 박자를 `hall_chandelier_sounds.py`에서 다시 만들어야 함.
- 구멍 예고음 시안(11-09): R532 소리가 "별로" → 1차(돌 · 톱니 · 북 등) · 2차(게임 신호: 라이저 · 카운트다운 등) 페이지 https://claude.ai/artifact/J2HFTC9XAxFrkFQaYBCKSg, 아직 안 고름(게임은 R532 그대로). 시안 원본 `sfx_takes/warn_mockup/`.
- 샹들리에 원 빨강(R534, "차징이 다 완료돼서 떨어질 때 빨간색으로 뜨게 하고 깨지는 소리 줄여"): 떨어지기 시작하는 순간(`warning` 1.2초)부터 착지까지 바닥 원이 빨갛게 바뀜. `VFX_GroundWarnDecal.shader`의 `_Strike`(0 → 1, 0.04초) · `_StrikeColor` · `_StrikeHot` · `_StrikeFlood`(원 전체에 깔리는 빨간 빛 0.45), `VfxGroundWarn.strikeAt`(초, 음수 = 안 바뀜) - 샹들리에만 `warning`으로 켬.
- 해머(R535, "아재 해머 소리"): `HammerChainSway.Sound.cs`(partial). 진자(`PendulumSwing`, 끝에서 끝 2초 · 끝에서 0.2초 멈춤)의 각도가 끝을 떠나면 `HallHammerWhoosh`(ElevenLabs Whoosh 3, 가장 큰 0.1초를 1.0초 = 공이 가장 빠른 가운데로 옮김, 2.0초) - 공에 붙어 지나가며 옮겨 들림, 5 → 40 m · 0.6. 끝에 닿으면 `HallHammerCreak`(Creak 2, 2.5 kHz 저역 통과, 0.7초) - 축에서, 4 → 30 m · 0.45. 둘 다 돌 홀 울림 · 홀 안에서만, 피치 0.96~1.03. SwingDuration을 바꾸면 `hall_hammer_sounds.py`의 SWING_MIDDLE도 바꿔야 함.
- 해머 R536("사슬 소리가 개구리 우는 소리 같고 바람 소리도 묵직하게 넣어"): 끝 소리를 삐걱 대신 둔한 사슬 쿵(Chain Clunk 1을 30 % 느리게 · 2.5 kHz 저역 통과 + 55 Hz 쿵 합성, 0.6초, 파일 이름은 `HallHammerCreak` 그대로), 바람은 Whoosh Heavy 2를 18 % 느리게 · 4 kHz 저역 통과(2.1초, 가장 큰 곳 1.0초).
- 환풍기(R537, "바람 소리 시작"): `RoyalFanSpin.Sound.cs`(partial). `HallFanHum`(Fan Hum 2, 날개 맥박 약 0.55초, 3 kHz 저역 통과, 2.75초 반복) - 늘 돌고 회전 속도(쉼 40 → 예고 480 → 불기 1150 °/s)로 피치 0.6 → 1.1 · 음량 0.08 → 0.4, 4 → 30 m. `HallFanWind`(Fan Wind 1, 2.5 kHz 저역 통과, 2.75초 반복) - 불 때만 0.55(0.15초에 켜지고 0.5초에 꺼짐), 6 → 45 m. `HallFanGust`(Fan Gust 1, 0.9초) - 불기 시작할 때 한 번 0.7. 반복 소리는 끝 0.25초를 처음에 겹쳐 이음새 없앰, 팬마다 시작 위치를 달리함. 셋 다 팬 자리 · 돌 홀 울림 · 홀 안에서만.
- 샹들리에 소리 거리(R538, "해머 장애물 코스에 도달하면 샹들리에 깨지는 소리랑 예고 소리 때문에 묵직한 바람 소리가 안 들려 - 샹들리에 쪽 소리 범위를 조절해"): 예고 3 → 14 m, 깨짐 4 → 22 m(전 4 → 32 · 6 → 55 m) - 해머 쪽에선 안 들리게.
- 해머 R539("바람 소리가 어색해 묵직하고 해머가 휘두르는 소리가 나야 돼 바람 소리가 아니라"): `HallHammerWhoosh`를 바람 대신 무거운 휘두름 '부웅' 한 번(ElevenLabs Swing B 3을 25 % 느리게 · 3 kHz 저역 통과, 가장 큰 곳 1.0초 = 공이 가운데를 지날 때, 1.6초).
- 해머 R540("해머 소리 훨씬 키워"): 휘두름 음량 1.0, 8 → 45 m, 파일 +8 dB 부드러운 리미터(약 5 dB 크게).
- 해머 R541("해머 소리가 두 번 휙휙 하는데 한번에 휙~ 소리 내게 해"): `HallHammerWhoosh` = Swing Single 2(한 번 부드럽게 부푸는 휙~), 20 % 느리게 · 3.5 kHz 저역 통과 · +4 dB 부드러운 리미터, 가장 큰 곳 1.0초, 1.7초.
- 해머 R542("스윙할 때 소리가 안 맞아 한 번 스윙하고 멈춤 지점까지 소리가 이어져야 돼"): `HallHammerWhoosh`가 스윙 2초 전체를 덮음(공 속도 sin(πt/2)^1.5 음량, 속도에 따라 500 Hz → 3.5 kHz 밝기, 2.1초). 피치 1 고정, 출발을 알아챈 시점만큼 앞에서부터 틂. `HallHammerCreak`는 쿵 10 ms 앞에서 시작(0.5초). SwingDuration을 바꾸면 `hall_hammer_sounds.py`의 SWING_SECONDS도 바꿔야 함.
- 떨어지는 기사(R543, "기사 소리 시작"): `FallingPieceFx.Sound.cs`(partial). `HallKnightWarn` = 샹들리에 '째깍 원'을 이 원의 시계(warning 1.4 + fall 0.65 = 2.05초, 마지막 0.35초 깜빡임)에 맞추고 4도 낮게(196 Hz 째깍, 392 Hz 삑), 2.05초. `HallKnightFall` = ElevenLabs Knight Fall 1을 거꾸로(착지로 커짐), 0.75초, 기사 모델에 붙음. `HallKnightLand` = Knight Land 2(DC 제거) + Land 1(-6 dB) + 45 Hz 쿵, 1.4초. 예고 · 떨어짐은 끝이 착지 순간, 착지는 시작이 착지 순간 - 늦게 본 PC는 주기 안 위치만큼 뒤에서(0.3초 넘게 지난 소리는 남은 게 0.5초 안 되면 안 냄). 예고 4 → 22 m · 0.5, 떨어짐 4 → 25 m · 0.7, 착지 6 → 40 m · 0.9, 돌 홀 울림 · 홀 안에서만. warning · fall을 바꾸면 `hall_knight_sounds.py`의 LEAD도 바꿔야 함.
- 해머 지나간 뒤 줄이기(R544, "해머 구간 통과하면 다른 소리가 묻혀 - 입장하기 전에는 유지하고 통과하면 소리를 더 줄여, 그리고 해머 소리가 마음에 안 들어"): `HammerChainSway.Sound.cs`의 Update. 공은 축의 z 둘레로 도니 축 z 방향 = 휘두르는 면의 법선. 듣는 사람이 처음 30 m(ApproachRange) 안에 들어온 쪽이 '앞', 해머에서 가로 9 m(CrossRange) 안에서 면을 건널 때만(면에서 0.6 m 넘게) 앞/뒤가 바뀜. 지나간 뒤 음량 0.25배(PassedGain, 약 -12 dB) · 끝 거리 45 → 18 m(PassedFar), 초당 3씩 바뀜. SectionSoundGate가 LateUpdate에서 곱하니 음량은 Update에서 정함. 새 휘두름 후보 A~D(ElevenLabs, `sfx_takes/hammer_mockup/` A1 · A2 · B1 · B2 · C1 · C2 · D1 · D2) 시안 https://claude.ai/artifact/CM5pTusYpQNoZrRc7Mw6ks - 고르면 R542처럼 2초 휘두르기 모양으로 다듬어 `HallHammerWhoosh.wav`에.
- 해머 소리 A(R545, 시안에서 "A"): `HallHammerWhoosh` = 후보 A2(`sfx_takes/HammerChainBall_2`)를 원래 속도로, 가장 큰 0.1초가 1.0초에 오게 + 그 큰 0.4초를 2.1초로 늘린 바탕(정지 400 Hz → 가장 빠를 때 2.5 kHz, 속도^1.5, 45 %), +3 dB 부드러운 리미터, -1 dB. 사슬 쿵은 그대로.
- 해머 소리 2차 후보(R546, "다른 사운드 시안"): E 통나무 망치(E1) · F 쇠 도끼날(F2) · G 저음 부웅(G1, 원래 평평한 저음이라 바탕만 속도에 맞춤) · H 모래주머니 밧줄(H1), `sfx_takes/hammer_mockup2/`. R545와 같은 방법(가장 큰 곳 1.0초 + 2초 바탕)으로 시안 https://claude.ai/artifact/CM5pTusYpQNoZrRc7Mw6ks 2판에.
- 해머 소리 H · 휘두르기 1.4초(R547, "H로 해주고 사운드에 맞게 해머도 속도에 맞춰"): `HallHammerWhoosh` = H1(`sfx_takes/HammerSandbag_1`)을 늘리지 않고, 가장 큰 40 ms를 0.7초(휘두름 가운데)에, +2 dB 부드러운 리미터, 1.5초. 홀 해머 SwingDuration 2 → 1.4초(빌더 `HammerSwing` · 프리팹 오버라이드), 두 번째 해머 phaseOffset 1.6 → 1.16초. 휘두르기 시간을 바꾸면 `hall_hammer_sounds.py`의 SWING_SECONDS도 같이.
- 굴림 소리 다시(R548, "사탕 소리가 나 - 카펫 구간과 대리석 바닥 떨어지는 소리 구별해 주고, 계단 > 평지 > 계단 구간이라 그 소리도 맞게"): 계단 두 곳(stairs 13.5~29.5 · 35.5~51.5)에서는 굴림 반복음 18 %(StairLoopShare) + 칸마다 착지음, 가운데 평지 · 바닥 평지에서는 반복음 전부. 착지 속도 1.6 m/s(StepMinSpeed) 아래 되튐은 소리 없음(카펫 말이의 잔 되튐이 '사탕' 소리). 카펫 길(|x| < 4.5, z ≥ 14 - 계단 · 가운데 평지 다)은 목소리에 저역 통과 900 Hz(그 밖 22 kHz), 착지음은 `...StepRunner`(450 Hz 아래, 12 ms 부드럽게 시작). 대리석 착지음은 쿵에 250~900 Hz 돌 두드림(석상 55 % · 45 ms, 카펫 말이 40 % · 30 ms). 소리 재료 `sfx_takes/roll2/`(석상 대리석 1 · 석상 카펫 1 · 카펫 대리석 2 · 카펫 카펫 2 · 평지 굴림 각 1).
- 대리석 소리 시안(R549, "대리석 사운드 느낌이 안 나"): `sfx_takes/roll3/`. 석상 A = MarbleBlock_1(0.6초), B = R548 쿵 + MarbleBlock_2 깨짐(1~4 kHz, 50 %), C = R548 쿵 + A(80 %), A~C 평지 굴림 = StoneRollMarble_1. 카펫 말이 CA = RugOnMarble_1(0.45초). 시안 https://claude.ai/artifact/5YMkQCmh9pM3f5PLgt1Stb(`rollpage/seq.py`가 굴림 경로의 착지 시각 · 평지 구간으로 11초를 만듦).
- 석상 대리석 A · 카펫 길 크게(R550, "석상 대리석은 A로 해 주고 카펫 구간에 있을 때 소리를 더 키워"): `HallRollStatueStep` = roll3/MarbleBlock_1 0.6초, `HallRollStatueLoop` = roll3/StoneRollMarble_1. 카펫 길 위 RunnerBoost 1.5(굴림음 · 착지음), `...StepRunner` 두 파일은 +5 dB tanh 뒤 -1 dB. 카펫 말이 대리석 착지는 R548 그대로.
- 왕실 팬 기계 소리 시안(R551, "확인했고 이 소리 너무 바람 소리 같아"): `sfx_takes/fan2/`. 후보마다 기계 반복음 하나가 팬 속도대로 높이 0.55 → 1.05 · 크기 0.06 → 0.9, 바람 반복음은 불 때만 20 %. 시안 https://claude.ai/artifact/WDPb4Vb92Txyxb5NKA3qMz(`fanpage/seq.py`가 AirVent 주기 · RoyalFanSpin 속도로 두 주기를 만듦).
- 왕실 팬 소리 = 시안 C 묵직한 로터(R552, "C로가자"): `HallFanHum.wav` = `FanRotor_1`(RMS -18 dB), 높이 0.55 → 1.05 · 크기 0.06 → 0.9(속도^1.3), 바람 0.11(불 때만), 돌풍 끔.
- 왕실 팬 바람 이펙트 시안(R553, "왕실 팬 vfx를 다른 걸로 바꿔줘"): 지금(R351 `VentWindFx` 하늘색 줄기 · 소용돌이 · 먼지) 대신 A 휘어 보이는 공기 · B 왕실 술 장식 리본 · C 날리는 기보 종이. 시안 https://claude.ai/artifact/4WuSz3WGrrB84d5ciKLGvG(`fan_wind_vfx_mockup_r553.html`).
- 왕실 팬 바람 = 지금 + A 휘어 보이는 공기(R554, "지금 있는 거에서 A를 추가해"): `VentWindFx`가 VFXPoint 앞 +z에 상자 하나(`V1 Haze`)를 만들고 `VentWindHaze.shader`가 원뿔(5.5 m, 반지름 0.9 → 1.5 m) 속 공기만큼 뒤 화면을 노이즈로 밀어 보여 줌. 예고: 입구 0.14까지 세기 0 → 0.45, 바람: 0.15초에 켜지고 앞머리 15 m/s, 끝나면 0.5초에 꺼짐. 굽힘 0.022(4 m 거리 기준), 흰빛 0.05, 그늘 0.03.
- 해머 휘두름 3차 후보(R555, "지금 해머 휙 소리인데 후웅~ 같은 소리로"): `sfx_takes/hammer_mockup3/`, O 후웅 섞음 · L 묵직한 쇠공 · M 낮은 울림 · N 지금 소리 낮게, 1.4초 박자 시안 https://claude.ai/artifact/CFp1b6qz97JJkJCLfVpeb7.
- 해머 휘두름 = 시안 O 후웅 섞음(R556, "O로 해주고 지금 사슬 소리는 바꾸지 마"): H를 12 % 느리게 · 1.8 kHz 저역 통과('후-') 0.7 + `HammerHoongK_1` 저음 몸통(가장 큰 0.1초 + 늘린 바탕 0.5, 200 → 900 Hz, 공 속도에 따라 35 → 100 %) 1, +2.5 dB 부드러운 리미터, -1 dB, 1.5초. 사슬 쿵 그대로.
- 떠 있는 발판 소리 시안(R557, "여기도 사운드 만들어줘"): `sfx_takes/pads/`, ① 움직이는 발판 A 부유 웅웅 · B 틀 삐걱, ② 착지 A 나무판 쿵 · B 돌판 쿵 + 출렁 · C 체스판 딱, 시안 https://claude.ai/artifact/WQtyGmDtoFdzfwGR8YaR3c. 착지 소리는 모든 PC에서 누가 발판에 내려앉았는지 알아내는 방법이 따로 필요함(지금 게임에 착지 감지 소리 없음).
- 떠 있는 발판 착지(R558, "착지는 C로 해주고 너무 탁이고 저기 구간은 기물이 뛰고 착지를 하면 살짝 내려가는 연출을 넣어줘 움직이는 발판이 내는 소리는 지금 별로야"): `FloatingPadFx.cs`(FloatingPad에서 붙임) - 발판 위 0.25 m에 기물 콜라이더가 새로 오면 모델이 8 cm 내려감(가장 낮은 곳 0.07초, 약 0.5초에 돌아옴) · 내려앉은 쪽으로 최대 2.5°, `HallPadLand.wav`(시안 C를 2.2 kHz 아래 · 8 ms 시작 · 120 Hz 몸통 0.3, 0.55초) 3 → 25 m · 0.8, 높이 0.94~1.05. 움직이는 발판 소리 없음.
- 떠 있는 발판 R558 보강: 저장된 씬은 꾸미기를 건너뛰어 `FloatingPadFx`가 안 붙었음 → `GrandStairHall.Awake`가 직접 붙임.
- 샹들리에 · 기사 예고 소리 시안(R559, "지금 기사랑 샹들리에 쪽 예고가 좀 별로야 지금 결은 비슷하게 4개 시안 줘"): 같은 박자로 A 나무 시계 째깍 · B 체스 시계 · C 낮은 북 · D 따뜻한 삑, https://claude.ai/artifact/SSMzB2Ct9vPCPjZjg9DK4N, 원본 `sfx_takes/fallwarn_mockup/`. 고르면 `hall_chandelier_sounds.py` · `hall_knight_sounds.py`의 예고 부분을 바꿈.
- 샹들리에 · 기사 예고 소리 A(R560, "샹들리에 기사 A로"): `hall_warn_wood.py` - 4 ms 나무 두드림을 1150 Hz(째깍마다 1/4음 올라감) · 420 Hz 몸통에 울리고 3.5 kHz 아래, 마지막 3번은 조금 낮게 35 ms 간격 "톡톡" + 260 Hz 둔한 울림. `hall_chandelier_sounds.py` · `hall_knight_sounds.py`가 불러 씀, 길이 · 박자 · 음량 · 거리 그대로.
- 점프 발판 소리 시안(R561, "점프 소리도 넣어줘 점프 발판 소리"): 튕겨 올리는 순간(`JumpPadFx.Burst`) 한 번 - A 나무 스프링 · B 공기 분사 · C 낮은 부웅 · D 나무 투석기 각 2개, https://claude.ai/artifact/TbQJHw7S3cCc9cqCg8D8FG, 원본 `sfx_takes/jumppad/`.
- 점프 발판 소리 · 착지(R562, "발판을 밟고 날아갈 때 C1을 넣어주고 착지 성공 할 때 B2를 넣어줘 그리고 착지할 때 착지 애니메이션을 넣어줘 멋있게"): `JumpPadFx.Land.cs` - 날아감 `HallJumpLaunch`(C1) · 착지 성공 `HallJumpLand`(B2, 발판보다 0.8 m 넘게 높고 떨어진 곳에 닿을 때만) + 번쩍 · 안쪽 사각형 0.12초 · 금빛 사각형 0.3초 · 불꽃 30 · 빛줄기 8 · 바닥 빛 0.6초. `hall_jump_sounds.py`.
- 떨어지는 기사 원 빨강(R563): 샹들리에(R534)와 같이 `warn.strikeAt = warningSeconds` - 떨어지기 시작할 때부터 착지까지 빨강.
- 근위 기사 소리 시안(R563): A 돌 석상 · B 전투마 · C 나무 체스 말 · D 바람 · 충격파, 각 2개(도약 · 착지 2.2초 + 준비 · 돌진 1.8초), https://claude.ai/artifact/4GPPKdmv42yg2M9qJtBXJD, 원본 `sfx_takes/guardknight/`.
- 근위 기사 소리 B1(R564, "B1으로 해줘"): `CavalryWarnFx.Sound.cs` - `HallGuardLeap`(전투마 콧김 · 발굽 박차기 · 바람 · 네 발굽 착지, 착지가 1.0초) 도약 시작에, `HallGuardCharge`(발굽 긁기 · 콧김 · 짧은 질주 · 미끄러져 멈춤) 준비 시작에, 기사 몸을 따라감. `hall_guard_sounds.py`.
- R565: 근위 기사 소리 · 착지 번쩍임이 첫 돌진에만 나던 것 고침(공용 시계가 한 바퀴로 감겨 cycle이 늘 0 → 나이가 되돌아가면 새 바퀴). F2 치트는 나는 동안에도 말 몸이 따라옴.
- R566 근위 기사 예고 · 이펙트 다시(소리 B1 박자): 예고(소리 없음 1.3초) 몸 웅크림 · 떨림 · 난간 부스러기 → 0.24초 박차기 조각 → 공중 착지 그림자 → 네 발굽(0 · 0.035 · 0.08 · 0.12초) 대리석 · 줄눈 조각 · 쇠 불꽃 · 몸 눌림 · 화면 흔들림 → 앞발 긁기 두 번 → 질주 박자 들썩 · 뒤 조각 → 0.82초부터 미끄러져 멈춤 불꽃. 금빛 유령 · 불씨 · 먼지 고리 · 바람 줄은 지움.
- R567 근위 기사 불(`CavalryWarnFx.Fire.cs`): 예고 끝 0.3초 작은 불 → 도약 0.2초 확 타오름(온몸 불길이 공중에 꼬리로 남음 · 불티 · 깜빡이는 불빛) → 착지에 불이 바닥으로 번지고 몸 불은 0.35초에 꺼짐 → 잔불(식어 가는 금 · 작은 불꽃 2.2초 · 불티 · 불빛 2.6초), 다음 예고 전에 사라짐. 연기 · 먼지 없음.
- R568 불길을 진짜 불처럼: 작은 불꽃 수백 개 대신 통째 불길 판(`Resources/PawnRushHallFx/HallFlameSheet.shader`, 위로 흐르는 노이즈가 혀 모양으로 갉아 먹음 · 날카로운 가장자리 · 금빛 속 → 주황 → 검붉은 끝), 몸 부품마다 1.3~2.2 m 불길 + 몸 전체 큰 판, 몸과 함께 움직이며 살짝 뒤로 흐름, 바닥 잔불도 같은 불길.
- 떨어지는 돌 기사 소리 시안(R569, "확인 떨어지는 돌석도 사운드 시안 다시 줘"): 예고 A는 그대로, 떨어짐 · 착지만 A 대리석 석상(A1 쿵 · A2 쉬이익 + 쩍) · B 돌 깨짐 · 잔해 · C 땅울림 저음 · D 거대 돌 체스 말(덜컥 흔들림), 원본 `sfx_takes/stonedrop/`(착지 시작 A1 0.035 · A2 0.70 · B1 0.88 · B2 0.97 · C1 0.04 · C2 0.57 · D1 1.22 · D2 0.02초, 자기 바람 없는 A1 · C1 · D2는 지금 `HallKnightFall`을 앞에), 시안 https://claude.ai/artifact/GuVv3mDjUv7QLHwMyye5i2. 고르면 `hall_knight_sounds.py`의 떨어짐 · 착지 부분을 바꿈.
- 떨어지는 돌 기사 소리 R570("A1로 해주고 범위는 그대로 하돼 울림을 크게 하고 바닥 떨어지는 바람 크게 해"): 착지 = A1(부딪힘 0.03초부터) + 파일에 구운 큰 돌 홀 울림(낮은 소리 3.6초 · 높은 소리 1.8초 감쇠, 이른 반사 4개, −7 dB, 2.45초) + Unity 울림 3.8초 · +6 dB(착지 소리만 따로 GameObject). 떨어지는 바람은 작은 앞부분을 끌어올려(lift 0.5, 최대 +20 dB) 떨어지는 내내 크게, 피크 −0.5 dB, 소리 크기 1.0. 들리는 거리는 R543 그대로. 원본 `hall_knight_sounds.py`. 승규 님 미확인.
- 떨어지는 돌 기사 착지 소리 R571("내려오는 기사가 바닥을 칠 때 내는 소리 훨씬 크게"): A1이 거의 200 Hz 아래 · 이미 최대치라 볼륨만으로는 안 커짐 → `punch`(tanh ×7 + 120~700 Hz 배음 ×1.1) 뒤 울림, 전체 tanh ×2.4, 피크 −0.3 dB(앞 0.5초 +5.6 dB, 120 Hz 위 +6.9 dB). `FallingPieceFx.Sound.cs`에서 같은 GameObject의 두 목소리(`landVoice` · `landDouble`)를 `PlayScheduled`로 같은 샘플에 틀어 +6 dB, 소리 크기 0.9 → 1.0. 합쳐 약 +13 dB. 승규 님 미확인.
- 다음 순서(예정): 승규 님 확인 뒤.


> 2026-11-13 정원·대계단 경량화 1차: main 기준 시각용 FBX 10개 적용. 정원 2,662,945→2,628,003(−1.31%), 대계단 4,722,590→4,016,857(−14.94%). Unity 실제 재질 전후 모형 사진, 충돌체 245/229개 기록 해시 동일, Core 192·모의 세션 81 통과. 꽃·나무 원본 유지(나무 LOD는 사진 비교 후 제외). 사용자 실기·12인 FPS 확인 전. 사용자 지시: 커밋·푸시 안 함.
[전후 사진·상세](COURSE_OPTIMIZATION_2026-11-13.md).


> 2026-11-13 사용자 중단·Claude Code 인계: 정원·대계단 2차에서 GrandStairHallV15/ GardenBoats 두 FBX만 추가 적용 및 Unity import 완료. 탑·석상 받침대는 적용 제외. 2차 후 Unity 재집계·사진·충돌체 검사·자동 테스트는 미완료이며 자동 테스트는 중단 지시에 따른 자동 승인 검토로 실행 차단됨. 경량화 변경 미커밋, 커밋·푸시 금지 유지. [Claude 인계](../../CLAUDE_HANDOFF_MESH_OPTIMIZATION.md).
