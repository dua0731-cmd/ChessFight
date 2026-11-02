# 왕실 마차뜰 (Royal Carriage Yard) 맵 프리팹 (R284, 2026-11-02)

사용자가 준 지시서 「ROYAL CARRIAGE YARD – 왕실 마차뜰」 그대로 만든 **폰 러쉬 맵 프리팹**이다. 미션 · 수집 · 퍼즐 없이 장애물만 통과하는 레이스. 첨부 이미지는 **그래픽 컨셉**으로만 썼다(색 · 분위기: 크림색 돌, 파란 지붕, 금 장식, 나무 마차). 구조와 장애물은 지시서를 따랐다.

브랜치: `claude/nifty-tesla-8212va`(= `seoungtest` + R283 맵 시험 씬). **코드 · 컴파일 · 자동 검사만, Unity · 사람 확인 전.**

## 1. 여는 법

1. Unity에서 메뉴 **ChessFight → Pawn Rush Map Test → Open Scene** → Play.
2. `[` `]`로 맵 목록에서 **RoyalCarriageYard**(맵) 또는 **RoyalCarriageYard_Obstacles**(장애물 하나씩 시험장)를 고른다.
   - 또는 Project 창에서 `Assets/Prefabs/PawnRushMaps/RoyalCarriageYard.prefab` 우클릭 → **ChessFight → Test In Pawn Rush Map Test**.
3. 맵은 **Play 때 코드가 짓는다**(맵 시험 씬의 기본 시험장과 같은 방식). 그래서 Play 전 프리팹을 열면 빈 오브젝트 하나다. 숫자는 그 오브젝트의 Inspector(아래 §4)에서 바꾸고 Play 중에는 **F5**(다시 짓기)로 반영.
4. **T** = 카메라가 보는 곳으로 폰 이동(구간 바로 가기), **F3** = 느리게(타이밍 보기), **R** = 마지막 체크포인트.

## 2. 전체 배치 (위에서 본 그림, 진행은 +z = 북쪽, 단위 m)

| 구간 | z | 높이 y | 무엇 |
|---|---|---|---|
| 1 출발 · 외부 성문 | −16 ~ 2 | 0 | 출발 마당(시작점 12개: 백 6 · 흑 6), **성문은 맵이 생기고 3초 뒤 열림**(위에 3 · 2 · 1) |
| 1 좁은 통로 | 2 ~ 22 | 0 | 폭 **6 m**, 오를 수 없는 대리석 벽 |
| 1 마차뜰 | 22 ~ 52 | 0 | 폭 **44 m**, 움직이는 장애물 없음(세워 둔 마차 · 상자만) |
| 2 진입 다리 | 52 ~ 62 | 0 | 돌다리 폭 10 m, 난간. 체크포인트 1(z 54) |
| 2 물레방아 통로 | 62 ~ 76 | 0 | 폭 **3.2 m** 나무 통로, 가운데(z 69)에 **가로 회전 물레방아**, 양옆은 물(빠지면 1.5초 뒤 체크포인트) |
| 2 맞은편 다리 | 76 ~ 86 | 0 | 돌다리, 체크포인트 2(z 81.5) |
| 3 돌진 마차 구역 | 86 ~ 116 | 0 | 폭 52 m, 줄 A(z 96) 벽→가운데 2대, 줄 B(z 103) 가운데→벽 2대, 줄 C(z 110) 전체 폭 1대 |
| 3 갈림길 | 116 ~ 150 | 0 → 8 | 가운데(x −13 ~ 13) / 왼쪽 · 오른쪽 경사로(x ±18 ~ 26), 사이는 2.2 m 대리석 칸막이 |
| 3 가운데: 회전 마차 | 116 ~ 135 | 0 | 중심 (0, 125.5), 마차 4대, 반지름 7.5 m. 화단(x ±10 ~ 13)이 둘레를 막아 **마차 사이로** 지나가야 함 |
| 3 가운데: 폭포 · 세로 물레방아 | 135 ~ 150 | 0 → 8 | 착지 가장자리 z 139.3, 연못(z 139.3 ~ 150), 물레방아 축 (0, 4.6, 144) 반지름 5.2 m, 폭포 뒤 대리석 절벽(z 150, 높이 8). 체크포인트 3(z 137) |
| 3 좌우: 화물 경사로 | 118 ~ 150 | 0 → 8 | 폭 8 m, 경사 14°, 꼭대기 문루의 해치에서 화물이 떨어져 굴러 내려옴, 아래 배수구로 사라짐 |
| 4 상단 합류 플랫폼 | 150 ~ 172 | 8 | 세 길이 합류, 체크포인트 4(z 157, 폭 48) |
| 4 최종 성문 | 172 ~ 174 | 8 | 폭 8 m · 높이 7.2 m 두 짝 문, **우클릭 누른 채 밀기** |
| 4 결승 | 174 ~ 195 | 8 | 결승 구역 z 185 ~ 195(`FinishZone`, 골반 기준), 왕관 받침 |

맵 밖(벽 너머)은 탑 · 성벽 장식 · 궁전(그림만, 충돌 없음).

## 3. 장애물 7종 — 작동 방식

모두 **재사용 가능한 독립 컴포넌트**다. 빈 오브젝트에 붙이면(또는 `Assets/Prefabs/Obstacles/RoyalCarriageYard/`의 프리팹을 끌어 놓으면) Play 때 자기 모양 · 충돌을 스스로 짓는다. 같은 종류를 여러 개 두어도 각자 설정이 따로다. 편집 화면에서는 **기즈모**(주황/하늘색 선)로 위치 · 경로가 보인다.

| 장애물 | 컴포넌트 | 작동 |
|---|---|---|
| 달리는 마차 | `ChargingCarriage` | 제자리 대기 → 끝점까지 돌진(빠르게 출발, 짧게 멈춤) → 대기 → 뒤로 천천히 복귀 → 반복. 돌진 0.8초 전 등이 빨갛게. 바퀴가 이동 거리만큼 굴러감 |
| 회전하는 마차 | `RotatingCarriageGroup` | 고정된 중앙 기둥에 **한 묶음으로** 연결된 마차들이 일정 속도로 회전. 연결 들보는 머리 위(3.2 m)라 마차 사이 · 들보 아래로 지나감 |
| 가로 회전 물레방아 | `HorizontalWaterwheel` | 통로를 가로막고 서서 **진행 방향을 축으로** 회전. 날개(4개)가 통로를 쓸고 지나가며, 날개 사이 빈 칸이 통로에 올 때 통과. 맨 아래 날개 끝은 통로 위 0.35 m(점프로 넘기), 올라가는 날개 밑은 다이빙으로. 맞으면 옆으로 밀려 물에 빠질 수 있음. Play 때 콘솔에 "몇 초마다 틈이 오고 몇 초 열린다"를 찍음(기본: 3.6초마다 약 1.2초) |
| 세로 회전 물레방아 | `VerticalWaterwheel` | 폭포 앞에 서서 **가로 축으로** 회전, 앞쪽(남쪽)이 올라감. **우클릭을 누른 채** 노/금색 손잡이를 잡으면 래그돌 손이 물레방아에 붙어(기존 잡기 = 손과 물체를 잇는 관절) **실제 회전으로** 끌려 올라감. 순간이동 · 위치 고정 없음. 꼭대기를 30° 지나면 앞 4 · 위 3.5 m/s로 놓아 줘 착지대(z 148.4 ~ 150, y 8)에 내려 줌. 일찍 놓으면 연못 → 체크포인트 3. 모든 부분이 '잡기만 되는 면'이라 벽처럼 기어오르지는 않음 |
| 목재 다리 | `TiltingWoodenBridge` | 평평 → 한쪽 모서리가 들려 40°까지 → 잠시 유지 → 내려옴 → 대기 → 반복. 가팔라지면 판자가 미끄러워지고(마찰 0.04) 약한 아래쪽 당김이 더해져 **실제 기울기 · 중력 · 마찰로** 미끄러져 떨어짐. 경첩: **긴 변**(기본, 옆으로 쏟아냄) 또는 **끝**(도개교). **지시서대로 이 맵의 길에는 두지 않았고** 장애물 시험장에만 있음 |
| 굴러오는 화물 | `CargoChute` + `RollingCargo` | 경사로 한 줄(레인). 1.7초마다 문루 해치에서 통 · 짚단(지름 1.0 · 1.4 · 1.8 m)이 떨어져 5 m/s로 굴러 내려오고 배수구로 가라앉았다가 다시 위로. 매번 레인의 다른 자리로 오지만 레인 폭의 1/3보다 크지 않아 **항상 사이로 지나갈 틈**이 있음. 오른쪽 경사로는 0.85초 어긋남 |
| 최종 성문 | `PushGate` | 플랫폼 쪽에서 문짝을 **우클릭으로 잡고** 문 가까이(1.8 m 안) 있으면 열림이 진행. 한 명이면 3초, 한 명 더할 때마다 60% 빨라짐. 놓으면 그 자리에서 멈춤, 다 열리면 계속 열려 있음. 문짝이 실제로 돌고 잡은 손이 같이 끌려가 "밀며 걸어 들어가는" 모양. 문 위에 금색 진행 막대 |
| (시작 성문) | `TimedGate` | 문짝 하나. 시작 시각(오프라인: 켜진 뒤 `openDelay`초) 뒤 `openDuration`초 동안 열림, 그 뒤 계속 열림. 경기에서는 `OpenAt(공유 시각)`으로 모두 같은 때 열 수 있음 |

### 충돌 · 넉백 (`YardImpact`)

움직이는 마차 · 물레방아 날개 · 화물에는 몸보다 조금 큰 **트리거 껍질**이 있다. 캐릭터가 닿으면 **기존 `IHitReceiver.ApplyHit`**(래그돌의 피격과 같은 호출)으로 밀어낸다.

- 방향: 그 지점에서 장애물이 움직이는 방향 + 장애물 바깥쪽 조금 + 위로.
- 세기: `knockbackForce + 닿은 곳 속도 × speedScale`, **`maxPush`(13 m/s)를 넘지 않음** → 회전이 빠를수록 · 돌진 중일수록 세고, 맵 밖으로 날아가지 않음.
- 닿은 곳 속도가 `knockdownSpeed`(3 m/s) 이상이면 0.9 ~ 1초 넘어짐, 느리면 비틀거리기만. 같은 캐릭터는 0.7초에 한 번만.
- 거의 멈춘 장애물(0.4 m/s 미만)은 밀지 않음(서 있는 마차 = 벽).
- 마차 충돌체는 상자 하나(바퀴 · 차대 사이 틈 없음) → 밑에 끼이지 않음.

## 4. Inspector에서 바꾸는 값

### 맵 전체 (`RoyalCarriageYard` 프리팹의 Inspector)

| 묶음 | 값 (기본) |
|---|---|
| 1. Gates | 시작 성문 `startDelay` 3초 · `startDuration` 2.5초, 최종 성문 `finalPushSeconds` 3초 · `finalCrowdBonus` 0.6 |
| 2. Crossing | `rotationSpeed` 25°/s · `clockwise` · `bladeCount` 4 · `wheelRadius` 4.6 m · `passageWidth` 3.2 m(통로 폭) · `tipClearance` 0.35 m · `knockbackForce` 5 |
| 3. Carts | `moveSpeed` 7 m/s · `sweeperSpeed`(전체 폭 마차) 8.5 · `returnSpeed` 3 · `waitAtStart` 1.5초 · `returnDelay` 0.8초 · `knockbackForce` 7 · `knockdownSeconds` 1 |
| 3. Carousel | `rotationSpeed` 22°/s · `clockwise` · `carriageCount` 4 · `rotationRadius` 7.5 m(최대 7.5) · `knockbackForce` 6 |
| 3. Cargo | `spawnInterval` 1.7초 · `rollingSpeed` 5 m/s · `cargoSizes` 1 / 1.4 / 1.8 m · `rightRampOffset` 0.85초 · `knockbackForce` 6 |
| 3. Lift | `rotationSpeed` 20°/s · `bladeCount` 8 · `releaseAngle` 30° · `releaseForward` 4 · `releaseUp` 3.5 |
| Look | `scenery`(탑 · 성벽 · 궁전 장식), `signs`(구간 안내판) |

### 지시서 변수 → 실제 이름

| 지시서 | 컴포넌트의 값 |
|---|---|
| ChargingCarriage: MoveSpeed · StartPosition · EndPosition · ReturnDelay · KnockbackForce | `moveSpeed` · 오브젝트 위치 · `endOffset`(마차 앞(+z) 기준) · `returnDelay` · `knockbackForce` (+ `returnSpeed` · `waitAtStart` · `warning` · `knockdownSeconds`) |
| RotatingCarriageGroup: RotationSpeed · RotationDirection · RotationRadius · CarriageCount · KnockbackForce | `rotationSpeed` · `clockwise` · `rotationRadius` · `carriageCount` · `knockbackForce` (+ `armHeight`) |
| HorizontalWaterwheel: RotationSpeed · RotationDirection · WheelRadius · BladeCount · PassageWidth | `rotationSpeed` · `clockwise` · `wheelRadius` · `bladeCount` · `passageWidth` (+ `tipClearance` · `paddleLength` · `paddleThickness` · `wheelDepth`) |
| VerticalWaterwheel: RotationSpeed · RotationDirection · GrabDistance · ReleaseHeight · PlayerAttachmentOffset | `rotationSpeed` · `reverse` · `handleThickness`(잡히는 굵기) · `releaseAngle`(꼭대기 지난 각도) + `releaseForward` · `releaseUp`. **붙는 위치(Offset)는 없다**: 손이 닿은 곳에 그대로 붙는다(순간이동 없음) |
| TiltingWoodenBridge: RaiseAngle · RaiseDuration · LowerDuration · WaitingDuration · RotationSpeed | `raiseAngle` · `raiseDuration` · `lowerDuration` · `waitingDuration`, 속도 = 각도 ÷ 시간(`RotationSpeed` 읽기 전용) (+ `holdDuration` · `hinge` · `slipAngle` · `slideAssist`) |
| RollingCargo: SpawnInterval · RollingSpeed · CargoSize · SpawnPosition · RollingDirection | `CargoChute`의 `spawnInterval` · `rollingSpeed` · `cargoSizes` · 오브젝트 위치(레인 꼭대기, 경사면 위) · 오브젝트의 +z(경사 아래 방향) (+ `laneLength` · `laneWidth` · `dropHeight` · `phase` · `seed`) |

## 5. 기존 프로젝트에서 그대로 쓴 것 (새로 만들지 않음)

| 무엇 | 기존 것 |
|---|---|
| 캐릭터 · 조작 · 카메라 | 맵 시험 씬의 래그돌 폰, `PlaytestSpawner`, `OrbitCamera`, 폰 러쉬 물리 · 스킬(`CourseSkills.PawnRush`). **바꾸지 않음** |
| 장애물 시간 | `Obstacle` + `ObstacleClock`(절대 규칙 7). 경기에서는 공유 시계라 모든 PC가 같은 위치를 본다 |
| 위에 타기 | `IMovingSurface`(모든 `Obstacle`) — 마차 지붕 · 다리 위에서 같이 움직임 |
| 넉백 | `IHitReceiver.ApplyHit`, 방장(또는 오프라인)만(`ObstacleClock.Authority`), 네트워크 꼭두각시는 제외 |
| 잡기 | 래그돌 `PawnHand`의 잡기(우클릭, 손 `FixedJoint`). 물레방아 · 성문은 그 관절이 자기 몸에 붙었는지만 본다 |
| 오를 수 없는 면 / 잡기만 되는 면 | `NoClimbSurface`(대리석) / `GrabOnlySurface`(물레방아 · 성문) |
| 낙사 · 복귀 | `KillVolume`(수로 · 연못, 1.5초 뒤 마지막 체크포인트), `CourseCheckpoint` 4개, y −40 아래 자동 복귀 |
| 시작 · 결승 | `SpawnPoint` 12개, `FinishZone`(골반 기준) |
| 재질 | `Arena.Lit`(키트의 돌 · 나무 질감), `MapLook.Water`, 둥근 부분 `MapLook.Smooth` |

기존 코드 변경은 하나: `Obstacle`에 `JumpsBetween`(기본 false)을 더해, 화물이 배수구 밑에서 해치로 돌아갈 때 그 사이를 쓸고 지나가지 않고 **바로 옮겨지게** 했다. 다른 장애물은 그대로다.

## 6. 지시서에서 해석한 것 (다르면 알려 주세요)

| 지시서 | 이렇게 만듦 | 이유 |
|---|---|---|
| "가로 회전 물레방아 … 진행 방향을 가로막는 형태 … 날개 사이 구멍을 통과" | 물레방아가 통로를 가로질러 서고 **축이 진행 방향**, 날개가 통로를 옆으로 쓸고 지나감 | 날개 사이 틈이 "진행 경로에 나타났다 사라진다", 점프로 낮은 부분 넘기 · 슬라이딩으로 밑 지나기가 모두 성립 |
| "세로 회전 물레방아 … 수직 회전축" | 바퀴가 **서 있고**(축은 가로) 앞쪽이 올라감 | "붙잡고 위로 올라간다", "상단 플랫폼에 착지"가 되려면 바퀴가 서 있어야 함(축이 수직이면 회전목마라 올라가지 않음) |
| 슬라이딩 | 게임의 **다이빙(좌클릭, 배로 미끄러짐)** | 폰 러쉬에 따로 슬라이딩이 없음. 조작 시스템은 바꾸지 않음 |
| "게임 시작 시 성문이 열린다" | 맵 시험 씬에서는 **맵이 생기고 3초 뒤**(카운트다운) | 시험 씬에 경기 시작 신호가 없음. 경기용 `OpenAt(공유 시각)` 마련 |
| 목재 다리 "한쪽 끝 고정, 반대쪽이 들림" | 기본 **긴 변 경첩**(옆으로 쏟아냄), 끝 경첩(도개교)도 선택 | 끝 경첩은 올라탄 사람이 경첩 쪽으로 미끄러져 내려올 뿐 떨어지지 않음. 시험장에 둘 다 둠 |
| 성문 "밀어내는 애니메이션" | 래그돌이 문짝을 잡고 걷는 자세가 대신(전용 동작 없음) | 래그돌 동작 시스템을 새로 만들지 않음 |
| 낙하 화물 | 통 · 짚단이 **시계 함수로** 굴러감(물리 공이 아님) | 절대 규칙 7: 모든 PC가 같은 화물을 봐야 함 |
| 물에 빠짐 | 물속 `KillVolume`: 빠져서 조금 가라앉고 1.5초 뒤 체크포인트 | 기존 복귀 시스템 사용(R283 시험 씬이 처리) |

## 7. 밸런스 (계산값, 측정 아님)

| 길 | 대략 | 위험 |
|---|---|---|
| 가운데 | 회전 마차 통과 + 물레방아 기다림(노 2.25초마다) + 타고 올라가기 약 6.5초 | 마차 충돌, 물레방아 놓침 → 연못(체크포인트 3에서 다시) |
| 좌우 | 경사로 33 m 오르기 약 8 ~ 10초 + 화물 피하기 | 화물에 넘어짐, 경사로 안쪽으로 떨어짐(가운데 바닥으로, 진행 손해만) |

두 길의 실제 통과 시간은 **Unity에서 재 봐야 한다**. 맞추는 손잡이: 회전 마차 `rotationSpeed`, 물레방아 `rotationSpeed` · `bladeCount`, 화물 `spawnInterval` · `rollingSpeed` · `cargoSizes`.

## 8. 멀티플레이

- 마차 · 회전 마차 · 물레방아 2종 · 화물 · 목재 다리 · 시작 성문 모양: **공유 시계의 순수 함수**라 패킷 없이 모든 PC가 같다.
- 넉백 · 물레방아에서 놓아 주기 · 미끄럼 당김: **방장(오프라인)만** 계산.
- **아직 안 된 것:** 최종 성문 열림 정도는 상태라 방장이 보내야 함(지금은 오프라인 전용, `StateDrivenMover`). 시작 성문의 공유 시작 시각 연결 없음. 이 맵을 경기 씬/로비에 잇지 않음.

## 9. 시험장 (`RoyalCarriageYard_Obstacles`)

| z | 장애물 |
|---|---|
| 10 ~ 26 | 돌진 마차(26 m) |
| 32 ~ 56 | 회전 마차 |
| 62 ~ 80 | 가로 물레방아(물 위 통로), 체크포인트 1 |
| 86 ~ 100 | 목재 다리: 왼쪽 긴 변 경첩 · 오른쪽 끝 경첩, 구덩이, 체크포인트 2 |
| 104 ~ 142 | 화물 경사로(높이 5 m) |
| 146 ~ 176 | 세로 물레방아 → 플랫폼(y 8), 체크포인트 3 |
| 176 ~ 192 | 최종 성문, 체크포인트 4, 결승 |

## 10. 파일

| 무엇 | 경로 |
|---|---|
| 맵 프리팹 | `Assets/Prefabs/PawnRushMaps/RoyalCarriageYard.prefab`, `RoyalCarriageYard_Obstacles.prefab` |
| 장애물 프리팹 8개 | `Assets/Prefabs/Obstacles/RoyalCarriageYard/` |
| 코드 | `Assets/Scripts/PawnRush/CarriageYard/` — `RoyalCarriageYard.cs`(+ `.Sections` · `.Scenery`), `CarriageYardObstacleBench.cs`, 장애물 `ChargingCarriage` · `RotatingCarriageGroup` · `HorizontalWaterwheel` · `VerticalWaterwheel` · `CargoChute` · `RollingCargo` · `TiltingWoodenBridge` · `TimedGate` · `PushGate`, 넉백 `YardImpact`, 재질 · 도형 `YardKit`, 폭포 그림 `WaterfallFlow` |
| 기존 코드 변경 | `Assets/Scripts/Gameplay/Obstacles/Obstacle.cs`(`JumpsBetween` 추가) |

## 11. 확인한 것 / 못 한 것

- 확인: Core 133 · 세션 45 테스트 통과(Unity Mono), Core · Game · Gameplay · PawnRush(플레이어 · 에디터) · PawnRush.Editor를 **Unity 6000.3 DLL로 컴파일** 통과(경고 늘지 않음), 어셈블리 경계 검사 통과, "움직이는 장애물은 ObstacleClock" 검사 통과(최종 성문은 상태형으로 표시).
- 못 함: **Unity Play · 화면 · 실제 래그돌 동작.** 이 PC에는 프로젝트 버전(6000.3.11f1) 에디터가 설치돼 있지 않아 같은 6000.3 계열 DLL로 컴파일만 했다. 확인할 것은 [VALIDATION](../Network/VALIDATION.md) 최상단 R284 표.
- 가장 불확실한 것: **세로 물레방아에 매달려 올라가기**(래그돌이 잡은 채 들려 올라가는지, 놓아 주는 시점과 착지), **최종 성문 밀기**(잡은 채 문짝을 따라 걷는지). 안 되면 증상을 알려 주면 값(`releaseAngle` · 속도 · `interactDistance`)이나 방식을 고친다.
