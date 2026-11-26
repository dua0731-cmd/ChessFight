# 폰 러쉬 대연회장 — 소리 설계 (R843, 2026-11-26)

사용자 "대연회장 사운드 필요한 부분 설계 해". **설계만이다 — 소리 파일도 코드도 아직 없다.** 대연회장에는 지금 소리가 하나뿐이다(seoungtest의 벽난로 타는 소리, 아래 1절).

- 읽은 것: [설계 v0.4](PAWN_RUSH_BANQUET_HALL_DESIGN_v0.4.md)(R828 배치), main의 장치 코드(`GrandBanquetHallBuilder` · `BanquetMovers` · `BanquetDevices`), seoungtest의 모델 입히기 코드(`GrandBanquetHallDressing.*`, R837 · R838). 시간은 전부 코드의 수치다.
- 방식은 [천문대 소리(R742)](PAWN_RUSH_OBSERVATORY_SFX.md)와 같다: 코드 한 파일, 공유 시계를 읽어 모든 PC가 같은 순간에 낸다, 보내는 것 없음.
- 크기 기준은 [경기 오디오 0절](PAWN_RUSH_AUDIO.md)(보통 한 번 −21 dB · 큰 타격 −18 · 작은 소리 −24 · 반복음 RMS −30).

## 한눈에

- **새 소리 23종**: 꼭 필요한 **신호음 6** · 장치가 살아 보이게 하는 소리 12 · 다듬기 5.
- 이 홀은 "언제 움직이나"를 지금 **눈으로만** 알린다(빨개지는 식탁보, 초록 등, 닫힌 막). 길이 홀을 세 번 지나며 여러 장치가 한꺼번에 보이므로, 소리 신호가 있으면 화면 밖 장치의 박자도 읽힌다. 그래서 신호음 여섯이 먼저다.
- 신호음 넷은 **음색이 서로 다르게** 잡았다 — 북(식탁보) · 호출 종(은 뚜껑) · 글로켄슈필 두 음(왈츠 등) · 지휘봉(막). 소리만 듣고도 어느 장치인지 안다.
- 낙하 · 복귀, 점프 · 잡기 · 착지 · 발소리, 밧줄 잡기는 **이미 있는 공용 소리**가 그대로 난다(새로 만들지 않는다).
- 악단의 음악은 넣을지부터 정해야 한다(6절 — 권하는 답: 1차에는 넣지 않는다).

## 1. 지금 있는 소리

| 무엇 | 어디서 | 비고 |
|---|---|---|
| 벽난로 셋의 타는 소리 | seoungtest `GrandBanquetHallDressing.Hall.cs`의 `BanquetHearthFires`(코드로 만든 소리, 3D 4 ~ 32 m) | main에는 없다(회색 상자). `SectionSoundGate`를 거치는지는 구현 때 확인 — 안 거치면 홀 밖에서도 들리고 효과음 설정을 따르지 않는다 |
| 낙하 '휘익' · 복귀 차임 | 공용 `sfx_fall` · `sfx_respawn`(내 폰만, 2D) | 무도장 · 악단석 · 식료품실 바닥(`KillVolume`)과 8 m 낙하에 그대로 난다 |
| 점프 · 잡기 · 모서리 잡기 · 착지 · 잡힌 폰 · 발소리 | 공용 `PawnRushSkillSfx` · `PawnRushStepSfx` | 샹들리에 줄 끝 고리 · 여왕의 드레스 · 막을 잡을 때 잡기 '햡'이 난다. 벽 · 줄에 매달려 움직이는 동안은 원래 소리가 없다 |
| 경기 음악 | `PawnRushRoundMusic`(150 BPM · F장조) | 홀 안에서도 계속 나온다 |

## 2. 소리 목록

시간은 장치마다 **한 주기 안의 초**(공유 시계 기준). "모든 PC"는 시계에서 나오므로 모든 PC가 같은 순간에 듣는다. 거리는 3D 소리가 그대로 들리는 거리 ~ 0이 되는 거리. 파일은 새 폴더 `Assets/Resources/BanquetHallSfx/`.

### 2-1. 꼭 필요한 신호음 (1순위, 6종)

| # | 파일 | 장치 · 주기 | 언제 | 어디 · 거리 | 크기 | 왜 |
|---|---|---|---|---|---|---|
| 1 | `BhDrumRoll` 2.0초 | ② 식탁보 · 10초 | 3.5초(식탁보가 빨개질 때), 점점 세게 | 마술사(식탁 남쪽 끝) · 10 ~ 60 m | −20 | 식탁 32 m 어디서든 "접시로"를 알아야 한다. 설계부터 '북이 울리면'인데 북소리가 없다 |
| 2 | `BhClothPull` 0.6초 | ② 식탁보 | 5.5초(빼는 순간) — 심벌 + 천 '촤악' | 식탁 가운데 · 8 ~ 50 m | −18 | 북의 끝. 당한 순간이 귀로 맺힌다 |
| 3 | `BhServiceBell` 0.5초 | ③ 은 뚜껑 승강기 둘 · 12.4초(서로 6.2초 어긋남) | 2.4초(식탁에서 닫히기 0.6초 전) · 8.4초(단에서 닫히기 0.6초 전, 조금 낮은 음) | 그 승강기 · 3 ~ 24 m | −21 | "곧 닫힌다 — 지금 타라 / 내려라". 지금은 뚜껑이 내려오기 시작해야 안다 |
| 4 | `BhClocheShut` 0.5초 | ③ | 3.6초 · 9.6초(뚜껑이 접시에 닿음) — 은 '챙' | 그 승강기 · 3 ~ 26 m | −21 | 탔는지 놓쳤는지의 확인 |
| 5 | `BhLampChime` 0.5초 | ④ 왈츠 · 4.775초 | 초록 등이 켜질 때(여왕이 멈추기 1.2초 전). 단 앞 등은 0초 기준, 무대 앞 등은 8.36초 기준(음을 높여 구별) | 그 등 · 3 ~ 22 m | −22 | 뛰어 붙는 박자. 여왕 어깨 위에서는 등이 뒤에 있어 안 보일 때가 많다 |
| 6 | `BhCurtainCue` 0.9초 | ⑤ 무대 막 · 15초 | 5.0초(열리기 1초 전) — 지휘봉 '딱 딱 딱' | 막이 만나는 곳 · 5 ~ 40 m | −21 | 막이 열릴 때 베이스보다 높이 매달려 있어야 한다. 닫힌 6초가 언제 끝나는지 지금은 알 수 없다 |

### 2-2. 장치가 살아 보이게 (2순위, 12종)

| # | 파일 | 장치 | 언제 | 어디 · 거리 | 크기 |
|---|---|---|---|---|---|
| 7 | `BhHallAmbience` 반복 | 홀 전체 | 홀 안에 있는 동안 — 식기 · 잔 부딪는 소리, 넓은 방의 울림(목소리 없음) | 2D | RMS −34 |
| 8 | `BhKitchenDoor` 0.4초 | ① 웨이터 | 쟁반이 부엌 문을 지날 때(3.3초마다) — 반문 '펄럭' | 부엌 문 · 3 ~ 22 m | −24 |
| 9 | `BhTrayClink` 반복 | ① 웨이터 넷 | 늘 — 쟁반 위 접시 탑이 달그락(웨이터마다 음높이 조금 다르게) | 쟁반 · 2 ~ 14 m | RMS −30 |
| 10 | `BhClothThrown` 0.4초 | ② 식탁보 | 천 위에 있던 폰이 날아갈 때 — 짧은 '슈웅' | 그 폰 · 3 ~ 28 m | −21 |
| 11 | `BhClothLay` 1.8초 | ② 식탁보 | 8.0초(다시 까는 2초) — 천 '펄럭' | 식탁 남쪽에서 북쪽으로 · 5 ~ 36 m | −24 |
| 12 | `BhClocheOpen` 0.4초 | ③ 은 뚜껑 | 5.8초(단에서 열림) · 11.8초(식탁에서 열림) — 가벼운 은 울림 | 그 승강기 · 3 ~ 22 m | −24 |
| 13 | `BhHoistLoop` 반복 | ③ 승강기 | 3.6 ~ 5.8초(오름, 음높이 1.1) · 9.6 ~ 11.8초(내림, 0.9) — 도르래 · 사슬 | 접시 · 3 ~ 24 m | RMS −27 |
| 14 | `BhGownLoop` 반복 | ④ 춤추는 열 쌍 | 늘 — 오르골 받침의 태엽 소리 + 드레스 스침 | 그 쌍 · 2 ~ 12 m | RMS −32 |
| 15 | `BhCurtainDraw` 반복 | ⑤ 막 | 6 ~ 11초(열림) · 13 ~ 15초(닫힘, 음높이 1.25) — 레일 고리 + 무거운 벨벳 | 남 · 북쪽 맨 앞 자락을 따라감 · 4 ~ 30 m | RMS −27 |
| 16 | `BhChocolateLoop` 반복 | ⑥ 초콜릿 분수 | 늘 — 걸쭉하게 흘러내리는 소리 | 분수 가운데 · 4 ~ 30 m | RMS −30 |
| 17 | `BhChandelierSwing` 0.7초 | ⑦ 샹들리에 열 줄 · 5초 | 줄마다 흔들림의 양 끝(2.5초마다) — 수정 '짤랑' + 사슬 삐걱. 긴 줄(8 m)은 낮은 음 | 줄 끝 · 3 ~ 16 m | −26 |
| 18 | `BhArrival` 1.2초 | ⑧ 왕의 발코니 | 내 폰이 발코니에 처음 설 때 — 짧은 왕실 나팔 | 2D · 내 PC만 | −21 |

- 17은 신호 구실도 한다: 이웃 줄은 반 박자 어긋나 **흔들림의 끝에서 만나므로**, '짤랑'이 곧 옮겨 잡을 때다.
- 9 · 14는 올라탄 사람에게 주로 들린다. 올라탄 쟁반 · 여왕이 소리 없이 미끄러지면 장난감 같지 않고 허전하다.

### 2-3. 다듬기 (3순위, 5종 — 나중에 해도 된다)

| # | 파일 | 무엇 | 메모 |
|---|---|---|---|
| 19 | `BhCurtainMeet` 0.4초 | 막의 두 자락이 다시 만날 때(15초) 묵직한 '툭' | 3D 4 ~ 24 m, −24 |
| 20 | `BhGreatChandelier` 반복 | 큰 샹들리에(쉼터)의 잔잔한 수정 떨림 | 3D 3 ~ 18 m, RMS −33. 쉬는 곳임을 귀로도 |
| 21 | `BhGuestsToast` 1.0초 | 식탁보를 뺀 직후(6.0초) 손님들이 잔을 두드림 | 3D 식탁 서쪽 손님 줄, −26. 목소리 없음 |
| 22 | `BhBassBump` 0.6초 | 막에 낮게 매달린 폰이 콘트라베이스에 걸릴 때 낮은 현 '붕' | 충돌을 봐야 해서 코드가 더 든다(각 PC가 자기 화면에서) |
| 23 | `BhChocoStep0 ~ 2` | 분수의 그릇 · 층 위(고리)를 걸을 때 끈적한 발소리 | 외나무다리(`WoodFloor`)처럼 바닥 표시 `ChocolateFloor`를 더해 `PawnRushStepSfx`가 냄 |

파일이 필요 없는 선택 하나: **뚜껑 안에서 먹먹하게** — 내 폰이 닫힌 뚜껑 안에 있는 동안(3.6 ~ 5.8초) 이 PC의 구간 소리를 낮은 소리만 남기고 줄인다. 재미있지만 없어도 된다.

### 2-4. 넣지 않는 것

- 웨이터 · 춤추는 쌍의 발소리(넷 + 스물이 늘 걷는다 — 시끄럽기만 하다).
- 초콜릿을 거슬러 오르는 손 소리, 사다리 · 막을 오르는 소리(게임 전체에 벽타기 소리가 없다 — 넣으려면 공용 폰 소리 작업).
- 초록 등이 꺼지는 소리, 식탁보가 놓여 있는 동안의 소리, 은 뚜껑이 열려 있는 동안의 소리.

## 3. 장치별 시간표

| 장치 | 주기 | 순서 (초) — **굵은 것**이 소리 |
|---|---|---|
| 웨이터 넷 | 3.3초마다 한 명 | **부엌 문(8)** → 카운터 옆 → 식탁 옆 8 m → 식료품실 문으로 사라짐. **접시 달그락(9)**은 늘 |
| 식탁보 | 10 | 0 놓여 있음 → 3.5 **북(1)** → 5.5 **빼기(2)** · **날아감(10)** → 6.0 **손님(21)** → 8.0 **다시 깔기(11)** → 10 |
| 은 뚜껑 승강기(서 0초 · 동 6.2초 늦게) | 12.4 | 0 열림(탑승) → 2.4 **종(3)** → 3.0 닫히기 시작 → 3.6 **닫힘(4)** · **오름(13)** → 5.8 **열림(12)** → 8.4 **종(3)** → 9.6 **닫힘(4)** · **내림(13)** → 11.8 **열림(12)** |
| 왈츠 | 4.775 | 여왕이 멈추기 1.2초 전 **등 차임(5)** → 멈춤 → 0.4초 뒤 등 꺼짐. 단 앞과 무대 앞은 따로(무대 앞은 3.585초 늦다) |
| 무대 막 | 15 | 0 닫힘 → 5.0 **지휘봉(6)** → 6.0 **열림(15)** → 11 열려 있음 → 13 **닫힘(15)** → 15 **만남(19)** |
| 샹들리에 줄 | 5 | 2.5초마다 양 끝에서 **짤랑(17)**. 이웃 줄과 번갈아 |

여섯 주기가 서로 달라(10 · 12.4 · 15 · 4.775 · 3.3 · 2.5초) 신호가 겹칠 때가 있다. 겹쳐도 되도록 한 것: 신호마다 음색이 다르고, 거리(위 표)로 자기 구간의 것만 크게 들린다. 한 자리에서 둘이 같이 크게 들리는 곳은 **식탁 북쪽 끝**뿐이다 — 북(1)과 호출 종(3).

## 4. 음 · 음색

- 경기 음악이 F장조라, 음높이가 있는 소리는 F장조 음으로 맞춘다: 호출 종 C6(단에서는 A5), 등 차임 F5 → A5(무대 앞은 A5 → C6), 도착 나팔 F장조. ElevenLabs가 음을 정확히 내지 않으면 굽는 스크립트에서 맞춘다(재생 속도).
- 은 뚜껑(챙) · 호출 종(띵) · 수정(짤랑)은 다 금속성 높은 소리라 헷갈릴 수 있다 → 뚜껑은 **넓고 낮게 울리는 은그릇**, 종은 **또렷한 한 음**, 수정은 **여러 알이 흩어지는** 소리로 프롬프트를 가른다.
- 사람 목소리는 쓰지 않는다(손님 · 웨이터 · 악단이 모두 체스 기물이고, 폰 반응 소리도 목소리 없이 정했다 — R841).

## 5. 만드는 법 (구현할 때)

- **코드 한 파일** `Assets/Scripts/PawnRush/GrandBanquetHallSound.cs`(천문대의 `RoyalObservatorySound`를 본뜸). 빌더 `Build` 끝에서 Content에 붙인다 — 회색 상자(main)에서도, 모델을 입힌 홀(seoungtest)에서도 같다.
- **시계만 읽는다.** 이 PC가 공유 시계로 "그 순간을 막 지났는지"를 보고 낸다. 한 걸음이 0.5초보다 길면(첫 프레임 · 끊김 · 늦게 들어옴) 아무것도 내지 않는다. 보내는 것 없음, 프로토콜 그대로.
- 읽을 값(이미 있다): `TableclothTrick.Phase`, `ClocheLift.Heights`, `ClockLamp.Lit`, `CurtainPleat.Open`, `BanquetWaiter.Along`, `RopeLine.Angle`.
- 장치 코드에 더할 것은 하나: 식탁보가 누구를 어디서 던졌는지(`TableclothTrick.Pull` 안에서 자리만 알림) — 10번 소리용. 던지는 판정은 PC마다 자기 화면으로 하므로 드물게 PC끼리 다를 수 있다(천문대의 광선 맞음과 같다).
- 18(도착)만 내 PC의 폰을 본다. 22 · 23은 3순위.
- 모든 소리는 `SectionSoundGate`를 거친다(홀 안에서만, 효과음 설정을 따름). 벽난로 소리(1절)도 같이 넣는다.
- 반복음은 14개다(웨이터 4 · 춤추는 쌍은 **가까운 셋만** · 승강기 2 · 막 2 · 분수 1 · 공간 1 · 큰 샹들리에 1). 한 번 나는 소리는 목소리 14개를 돌려 쓴다(천문대와 같은 수).
- 원본 · 프롬프트 · 굽는 스크립트는 `ArtSource/PawnRush/BanquetHall/Sound/`(`prompts.md` · `bake.py` · `picks.json`), 크기는 `bake.py`가 0절 기준으로 계산해 코드의 표에 넣는다.

## 6. 먼저 정할 것

| # | 정할 것 | 권하는 답 | 이유 |
|---|---|---|---|
| 1 | **악단의 음악**을 넣을까 | 1차에는 넣지 않는다 | 경기 음악(150 BPM · F장조)이 홀 안에서도 계속 나와, 무대에서 왈츠가 흐르면 두 음악이 부딪힌다. 넣는다면: 3박자 · 약 188 BPM · 47.75초 반복(여왕이 한 번 도는 4.775초 = 다섯 마디, 춤 한 바퀴 = 50마디)으로 만들어 시계에 맞춰 무대에서 3D로 내고, 홀 안에서는 경기 음악을 줄인다. 음악은 이 채팅의 ElevenLabs 연결로는 못 만들어 사용자가 직접 만들어야 한다 |
| 2 | 손님들 소리에 **목소리** | 없음(식기 · 잔 소리만) | 4절 |
| 3 | 발코니 **도착 나팔**을 누가 듣나 | 본인만 | 열두 명이 차례로 도착하면 나팔이 열두 번 울린다 |
| 4 | **어느 브랜치**에서 만드나 | seoungtest | 모델을 입힌 홀 · 부엌 문 · 벽난로가 거기 있다. 소리 코드는 회색 상자에서도 돌아 main에 먼저 넣을 수도 있다 |
| 5 | **테이크 고르기** | 신호음 여섯(1 ~ 6)은 사용자가 번호로 듣고, 나머지는 AI가 파형 숫자로 | 신호음은 음색 구별이 목적이라 귀로 골라야 한다(튜토리얼 R841 방식). 나머지는 천문대 R742 방식 |

## 7. ElevenLabs 프롬프트 초안

모두 끝에 `dry, close, no reverb tail, no music, no voice`를 붙인다. 반복음은 `seamless loop`.

| 파일 | 길이 | 프롬프트 |
|---|---|---|
| `BhDrumRoll` | 2.0 s | circus snare drum roll building steadily from soft to loud, tight and bright, ends abruptly with no final hit |
| `BhClothPull` | 0.6 s | a magician whips a large tablecloth off a table in one fast pull: sharp fabric whoosh-snap with a bright cymbal crash on top |
| `BhServiceBell` | 0.5 s | single clear ding of a small brass hotel service bell, one pure note, short ring |
| `BhClocheShut` | 0.5 s | a large silver dish cover set down on a silver platter: wide hollow metallic clang with a short low shimmer |
| `BhLampChime` | 0.5 s | two soft rising glockenspiel notes, gentle and friendly, a "ready" signal |
| `BhCurtainCue` | 0.9 s | a conductor taps a wooden baton three times on a music stand, crisp dry wooden taps, evenly spaced |
| `BhHallAmbience` | loop 12 s | large palace banquet hall room tone: distant cutlery on porcelain, occasional crystal glasses clinking, soft airy space, no speech |
| `BhKitchenDoor` | 0.4 s | wooden swinging kitchen half-door pushed open and flapping once, soft thump and hinge squeak |
| `BhTrayClink` | loop 4 s | a tall stack of porcelain plates rattling gently on a carried silver tray, light steady clinking |
| `BhClothThrown` | 0.4 s | short cartoon whoosh of a small wooden toy flung through the air, rising pitch |
| `BhClothLay` | 1.8 s | a large linen tablecloth unfurled and settling over a long table, soft billowing fabric flaps |
| `BhClocheOpen` | 0.4 s | a silver dish cover lifted off a platter, light bright metallic ring sliding upward |
| `BhHoistLoop` | loop 2 s | wooden dumbwaiter hoist running: rope pulleys turning with a fine gold chain rattling, steady |
| `BhGownLoop` | loop 4 s | music-box clockwork whirring softly with silk gown fabric swishing in slow turns |
| `BhCurtainDraw` | loop 2.5 s | heavy velvet stage curtain drawn along a brass rail: metal rings sliding and thick fabric dragging |
| `BhChocolateLoop` | loop 6 s | thick melted chocolate pouring down a fountain in slow heavy sheets, glossy viscous glugging |
| `BhChandelierSwing` | 0.7 s | a swinging crystal chandelier reaching the end of its swing: scattered crystal drops tinkling with a soft brass chain creak |
| `BhArrival` | 1.2 s | very short royal trumpet fanfare flourish in F major, two bright trumpets, a clear ending |
| `BhCurtainMeet` | 0.4 s | two heavy velvet curtains swinging closed and meeting, soft deep fabric thud |
| `BhGreatChandelier` | loop 6 s | very quiet shimmer of many crystal drops trembling on a huge chandelier, delicate and sparse |
| `BhGuestsToast` | 1.0 s | a dozen diners tapping spoons on crystal glasses in approval, bright scattered ringing |
| `BhBassBump` | 0.6 s | a double bass bumped by accident: low open strings booming with a hollow wooden knock |
| `BhChocoStep` | 0.15 s ×3 | single sticky footstep in thick melted chocolate, soft wet squelch |

## 8. 확인 상태

설계 문서뿐이다. 소리를 만들지도, 코드를 쓰지도, 누가 듣지도 않았다. 시간 · 자리는 main의 코드에서 읽었고, seoungtest의 모델 입히기(부엌 문 · 벽난로 · 오르골 받침)는 코드로만 봤다(Unity에서 본 것 아님).
