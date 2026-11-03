# 왕실 마차뜰 (Royal Carriage Yard) 맵 프리팹 (R284 → R286, 2026-11-03)

**폰 러쉬 맵 프리팹**이다. 처음에는 사용자 지시서 「ROYAL CARRIAGE YARD – 왕실 마차뜰」대로 만들었고(R284), 11-03 사용자가 맵 방향을 다시 정했다(R286). 6 대 6 팀이 몸싸움을 하며 장애물을 통과해 결승까지 가는 레이스이고, 미션 · 수집 · 퍼즐은 없다.

첨부 이미지(왕궁 마차뜰 그림 · 나무 물레방아 사진)는 **그래픽 · 형태 참고**로 썼다. 구조와 장애물은 사용자 지시를 따랐다.

| 날짜 | 사용자 지시 | 반영 |
|---|---|---|
| 11-02 (R284) | 지시서 4구간 · 장애물 5종 + 화물 · 성문 | 처음 판 |
| 11-03 (R286) | "성문을 들어가서 바로 있는 넓은 공간에서만 랜덤하게 돌진하며 돌아다니는 마차 4개", "물레방아 길은 길을 2개로, 그 길 2개가 닿을 정도로 거대한 물레방아(사진의 원형 틀과 6개의 작대기 사이 구멍), 구멍 사이로 점프+슬라이딩하여 타이밍 맞게 통과" | 좁은 통로 삭제 → 성문 바로 뒤 넓은 마당 + **돌아다니는 마차 4대**. 물레방아 구간을 **길 2개 + 거대 물레방아 하나**로. 경기장의 돌진 마차는 빼고 3 · 4구간을 24 m 당김 |

브랜치: `claude/nifty-tesla-8212va`(= `seoungtest` + R283 맵 시험 씬 + `main` 병합 R285). **코드 · 컴파일 · 자동 검사만 했고, Unity와 사람 확인은 아직이다.**

## 1. 여는 법

1. Unity에서 메뉴 **ChessFight → Pawn Rush Map Test → Open Scene** → Play.
2. `[` `]`로 맵 목록에서 고른다.
   - **RoyalCarriageYard**: 이 맵.
   - **RoyalCarriageYard_Obstacles**: 장애물을 하나씩 시험하는 곳.
   - 목록 맨 앞의 "기본 시험장"은 R283의 측정용이다.
3. 맵은 **Play 때 코드가 짓는다**(기본 시험장과 같은 방식). 그래서 Play 전에 프리팹을 열면 빈 오브젝트 하나만 보인다. 값은 그 오브젝트의 Inspector(§4)에서 바꾸고, Play 중에는 **F5**(다시 짓기)로 반영한다.
4. 맵 시험 씬의 키:
   - **T**: 카메라가 보는 곳으로 폰 이동(구간 바로 가기)
   - **F3**: 느리게(타이밍 보기)
   - **R**: 마지막 체크포인트로
   - 콘솔에 거대 물레방아의 구멍 주기가 찍힌다(`[RoyalCarriageYard] 가로 물레방아 …`).

## 2. 전체 배치 (위에서 본 그림, 진행은 +z = 북쪽, 단위 m)

| 구간 | z | 높이 y | 무엇 |
|---|---|---|---|
| 1 출발 · 외부 성문 | −16 ~ 2 | 0 | 출발 마당(시작점 12개: 백 6 · 흑 6). **성문은 맵이 생기고 3초 뒤 열림**(위에 3 · 2 · 1). 성문 폭 6 m |
| 1 마차뜰 | 2 ~ 50 | 0 | 폭 44 m · 깊이 48 m. **돌아다니는 마차 4대**가 마당 전체를 같이 쓰며 아무 데로나 돌진한다(서로 닿지 않음). 성문이 열릴 때 움직이기 시작. 마당 끝 양옆은 수로 쪽 난간 |
| 2 들어가는 돌 바닥 | 50 ~ 58 | 0 | 폭 20 m. 난간이 있고, 두 길이 시작하는 곳만 트여 있다. 체크포인트 1(z 53, 바닥 폭 전체) |
| 2 거대 물레방아 길 | 58 ~ 74 | 0 | 나무 길 2개(x −6.2 ~ −3.0, 3.0 ~ 6.2, 폭 3.2 m, 난간 없음)가 **물레방아(축 (0, 5.27, 66), 반지름 9 m) 면을 통과**한다. 물레방아 자리(z 64.7 ~ 67.3)는 길이 **2.6 m 끊겨** 있어 구멍으로 뛰어 건넌다(금색 줄 = 뛰는 곳). 아래는 물(빠지면 1.5초 뒤 체크포인트) |
| 2 나가는 돌 바닥 | 74 ~ 82 | 0 | 체크포인트 2(z 78.5, 바닥 폭 전체) |
| 3 경기장 입구 | 82 ~ 92 | 0 | 폭 52 m, 빈 길 10 m |
| 3 갈림길 | 92 ~ 126 | 0 → 8 | 가운데(x −13 ~ 13) / 왼쪽 · 오른쪽 경사로(x ±18 ~ 26), 사이는 2.2 m 대리석 칸막이 |
| 3 가운데: 회전 마차 | 92 ~ 111 | 0 | 중심 (0, 101.5), 마차 4대, 반지름 7.5 m. 화단이 둘레를 막아 **마차 사이로** 지나가야 함 |
| 3 가운데: 폭포 · 세로 물레방아 | 111 ~ 126 | 0 → 8 | 착지 가장자리 z 115.3, 연못, 물레방아 축 (0, 4.6, 120) 반지름 5.2 m, 대리석 절벽(z 126, 높이 8). 체크포인트 3(z 113, 가운데 길 폭 전체) |
| 3 좌우: 화물 경사로 | 94 ~ 126 | 0 → 8 | 폭 8 m, 경사 14°, 꼭대기 문루 해치에서 화물이 떨어져 굴러 내려옴 |
| 4 상단 합류 플랫폼 | 126 ~ 148 | 8 | 세 길이 합류. 체크포인트 4(z 133, 폭 48) |
| 4 최종 성문 | 148 ~ 150 | 8 | 폭 8 m · 높이 7.2 m 두 짝 문, **우클릭 누른 채 밀기** |
| 4 결승 | 150 ~ 171 | 8 | 결승 구역 z 161 ~ 171(`FinishZone`, 골반 기준), 왕관 받침 |

- 3 · 4구간은 코드에서 11-02 때 숫자(경기장 시작 z 106)를 그대로 쓰고, 묶음 전체를 24 m 남쪽으로 옮겨 짓는다(`LaterShift`). 이 문서의 숫자는 옮긴 뒤 실제 위치다.
- 맵 밖(벽 너머)의 탑 · 성벽 장식 · 궁전은 그림뿐이라 충돌이 없다. 마당 안에는 장식을 두지 않았다(마차가 지나갈 자리라서). 세워 둔 마차 · 상자는 출발 마당 구석에 있다.

## 3. 장애물 — 작동 방식

모두 **재사용 가능한 독립 컴포넌트**다.

- 빈 오브젝트에 붙이거나, `Assets/Prefabs/Obstacles/RoyalCarriageYard/`의 프리팹 9개를 끌어 놓으면 Play 때 모양과 충돌을 스스로 짓는다.
- 같은 종류를 여러 개 두어도 설정이 따로다.
- 편집 화면에서는 **기즈모**(주황 · 하늘색 선)로 위치와 범위가 보인다.

| 장애물 | 컴포넌트 | 맵 | 작동 |
|---|---|---|---|
| **돌아다니는 마차** (R286) | `CarriageFleet` + `RoamingCarriage` (+ Core `RoamingFleet`) | 1구간 4대 | 네 대가 **마당 전체를 같이 쓴다**. 한 대씩 계속 반복한다: ① 다음 지점(마당 아무 데나, 10 ~ 32 m 떨어진 무작위 지점)으로 **제자리에서 몸을 돌림**(1.2초) ② **등이 빨개진 채 조준**(0.6 ~ 1.2초) ③ **돌진**(최고 9 ~ 9.9 m/s, 대마다 다름) ④ 쉼(0.4 ~ 1.4초). 다음 지점을 고를 때 **자기 몸이 휩쓸 길(돌진선 + 몸 반경 2.4 m)이 다른 마차들의 지금 돌진선 · 서 있는 곳과 0.6 m 이상 떨어지게** 고른다 → 서로 절대 닿지 않는다. 고를 곳이 없으면 0.5초 기다렸다 다시 고른다(전체의 약 4%). 마당 어디로든 와서 **차선 · 경계선 같은 숨을 곳이 없다**. 다만 마차 중심이 벽에서 2.6 m 떨어져 다니므로 **벽에 바짝 붙은 길은 덜 맞는다**(검토 시뮬레이션: 가운데 직진은 맞을 수 있는 시간 약 41%, 벽 따라가기 0.3 ~ 0.5%). 대신 그 길은 약 33 m 더 멀어 달리기로 6.5초 늦다 → "빠르지만 위험한 가운데 / 느리지만 안전한 벽" 선택 |
| 회전하는 마차 | `RotatingCarriageGroup` | 3구간 | 고정된 중앙 기둥에 **한 묶음으로** 연결된 마차들이 일정 속도로 돈다. 들보는 머리 위(3.2 m)라 마차 사이 · 들보 아래로 지나간다 |
| **거대 물레방아** (R286, 가로 회전) | `HorizontalWaterwheel` | 2구간 | 사진처럼 생겼다: **둥근 테 2겹 + 테 사이 통 판자 28장 + 앞뒤 면 바퀴살 6쌍**, 쇠 덮개를 씌운 굴대 통. 길을 가로질러 서서 **진행 방향을 축으로** 시계 방향(다가가며 볼 때)으로 15°/초 돈다. 왼쪽 길에서는 바퀴살이 올라가고 오른쪽 길에서는 내려온다. 길 하나에서 **4초마다 구멍이 오고 약 1.5초 열린다**. 바퀴살에 닿으면 옆으로 밀려(4 + 속도 m/s, 넘어지지는 않음) 물에 빠질 수 있다. 위의 나무 물길에서 물이 쏟아지고, 내려가는 쪽으로 물이 흘러내린다(그림) |
| 세로 회전 물레방아 | `VerticalWaterwheel` | 3구간 | **우클릭을 누른 채** 노 · 금색 손잡이를 잡으면 래그돌 손이 물레방아에 붙어 실제 회전으로 끌려 올라간다. 꼭대기를 30° 지나면 착지대로 놓아 준다 |
| 굴러오는 화물 | `CargoChute` + `RollingCargo` | 3구간 좌우 | 1.7초마다 문루 해치에서 통 · 짚단이 떨어져 5 m/s로 굴러 내려오고, 배수구로 사라졌다가 다시 위로 온다 |
| 최종 성문 | `PushGate` | 4구간 | 플랫폼 쪽에서 문짝을 **우클릭으로 잡고** 가까이 있으면 열림이 진행된다. 한 명이면 3초, 한 명 더할 때마다 60% 빨라진다 |
| 시작 성문 | `TimedGate` | 1구간 | 시작 시각(오프라인: 켜진 뒤 3초) 뒤 2.5초 동안 열린다 |
| 달리는 마차 | `ChargingCarriage` | 시험장만 | 정해진 직선을 돌진 · 복귀한다(R284). 맵에서는 돌아다니는 마차로 바뀌었다 |
| 목재 다리 | `TiltingWoodenBridge` | 시험장만 | 한쪽이 들려 위의 폰을 떨어뜨린다. 지시서대로 맵의 길에는 두지 않았다 |

### 충돌 · 넉백 (`YardImpact`)

움직이는 마차 · 물레방아 바퀴살 · 화물에는 몸보다 조금 큰 **트리거 껍질**이 있다. 캐릭터가 닿으면 **기존 `IHitReceiver.ApplyHit`**(래그돌 피격과 같은 호출)으로 밀어낸다.

- 방향: 그 지점에서 장애물이 움직이는 방향, 장애물 바깥쪽 조금, 위로.
- 세기: `knockbackForce + 닿은 곳 속도 × 0.6`. **최대 13 m/s**를 넘지 않는다.
- 3 m/s 이상으로 움직이는 데 닿으면 넘어진다. 바퀴살은 넘어뜨리지 않고 밀기만 한다.
- 거의 멈춘 장애물(0.4 m/s 미만)은 밀지 않는다(조준 중인 마차 = 벽).
- 돌아다니는 마차의 넉백은 **돌진 중에만** 켜진다. 제자리에서 도는 동안은 몸통(충돌 상자)으로 미는 물리 충돌만 있다. 늘 거의 반대로 돌아서 앞뒤 끝이 빨리 휘둘리는데, 방금 서 있던 마차가 사람을 날려 버리지 않게 하려는 것이다.

## 4. Inspector에서 바꾸는 값

### 맵 전체 (`RoyalCarriageYard` 프리팹의 Inspector)

| 묶음 | 값 (기본) |
|---|---|
| 1. Gates | `startDelay` 3초 · `startDuration` 2.5초 · `finalPushSeconds` 3초 · `finalCrowdBonus` 0.6 |
| 1. Yard (R286) | `moveSpeed` 9 m/s(두 번째 마차부터 0.3씩 빠름) · `turnSeconds` 1.2 · `seed` 101(바꾸면 다른 춤) · `minAim`/`maxAim` 0.6/1.2초 · `knockbackForce` 7 · `knockdownSeconds` 1. 쉼 · 돌진 거리 등 나머지는 Play 중 `Roaming carriages`(`CarriageFleet`) Inspector |
| 2. Canal (R286) | `rotationSpeed` 15°/s · `clockwise` · `spokeCount` 6 · `wheelRadius` 9 m(8 ~ 10, 축 높이 · 수로 깊이가 따라감) · `passageWidth` 3.2 m(길 폭, 2.4 ~ 3.6) · `knockbackForce` 4 |
| 3. Carousel | `rotationSpeed` 22°/s · `clockwise` · `carriageCount` 4 · `rotationRadius` 7.5 m · `knockbackForce` 6 |
| 3. Cargo | `spawnInterval` 1.7초 · `rollingSpeed` 5 m/s · `cargoSizes` 1/1.4/1.8 m · `rightRampOffset` 0.85초 · `knockbackForce` 6 |
| 3. Lift | `rotationSpeed` 20°/s · `bladeCount` 8 · `releaseAngle` 30° · `releaseForward` 4 · `releaseUp` 3.5 |
| Look | `scenery`, `signs` |

### 컴포넌트별 (Play 중 각 오브젝트 Inspector)

| 컴포넌트 | 값 |
|---|---|
| `CarriageFleet` (마당의 무리) | 영역 `minX`/`maxX`/`minZ`/`maxZ`(마차 **중심**이 다니는 곳, 벽에서 `BodyRadius` 약 2.4 m + 여유) · `count` · `moveSpeed` · `speedStep` · `turnSeconds` · `minAim`/`maxAim` · `minRest`/`maxRest` · `minCharge`/`maxCharge` · `startDelay` · `seed` · `knockbackForce` · `knockdownSeconds`. 경기용 `StartAt(공유 시각)` |
| `RoamingCarriage` (한 대) | 무리 안에서는 무리를 따른다. **혼자 놓으면** 한 대짜리 무리가 된다: `halfWidth` · `halfDepth`(자기 위치 기준 영역) · `moveSpeed` · `turnSeconds` · `minAim`/`maxAim` · `seed` · `knockbackForce` · `knockdownSeconds` · `paint` · `phase`(같은 시드끼리 박자 어긋나게). 켜진 때부터 움직이므로 온라인에서는 `StartAt(공유 시각)`을 받아야 PC마다 같다(아직 부르는 곳 없음 → 지금은 오프라인 전용) |
| `HorizontalWaterwheel` | `rotationSpeed` · `clockwise` · `wheelRadius` · `rimWidth` · `spokeCount` · `spokeWidth` · `wheelDepth` · `hubRadius` · `bucketCount` · `knockbackForce` · 길 정보 `passageOffset` · `passageWidth` · `deckBelowAxle`(콘솔에 찍는 열림 시간 계산용) |
| 나머지 | [R284 표](#지시서-변수--실제-이름) 그대로 |

### 지시서 변수 → 실제 이름

| 지시서 | 컴포넌트의 값 |
|---|---|
| ChargingCarriage(돌진 마차): MoveSpeed · StartPosition · EndPosition · ReturnDelay · KnockbackForce | 맵에서는 `CarriageFleet`: `moveSpeed` · 영역(시작 · 끝점은 무작위) · `minRest`/`maxRest` · `knockbackForce`. 직선 왕복판은 `ChargingCarriage`(`moveSpeed` · 위치 · `endOffset` · `returnDelay` · `knockbackForce`) |
| RotatingCarriageGroup | `rotationSpeed` · `clockwise` · `rotationRadius` · `carriageCount` · `knockbackForce` |
| HorizontalWaterwheel: RotationSpeed · RotationDirection · WheelRadius · BladeCount · PassageWidth | `rotationSpeed` · `clockwise` · `wheelRadius` · `spokeCount` · `passageWidth` |
| VerticalWaterwheel | `rotationSpeed` · `reverse` · `handleThickness` · `releaseAngle` · `releaseForward` · `releaseUp` (붙는 위치 오프셋은 없음: 손이 닿은 곳에 붙는다) |
| TiltingWoodenBridge | `raiseAngle` · `raiseDuration` · `lowerDuration` · `waitingDuration` · `RotationSpeed`(읽기 전용) · `hinge` |
| RollingCargo | `CargoChute`의 `spawnInterval` · `rollingSpeed` · `cargoSizes` · 위치 · +z 방향 |

## 5. 기존 프로젝트에서 그대로 쓴 것

| 무엇 | 기존 것 |
|---|---|
| 캐릭터 · 조작 · 카메라 | 맵 시험 씬의 래그돌 폰, 폰 러쉬 물리 · 스킬. **바꾸지 않음** |
| 장애물 시간 | `Obstacle` + `ObstacleClock`(절대 규칙 7). 돌아다니는 마차는 대계단 현관홀의 무작위 일정과 같은 방식: 시작 시각 + 시드로 만든 계획에서 시계로 위치를 읽는다 |
| 무작위 | Core `SeededRandom`(mulberry32, 대계단 현관홀과 같은 것) |
| 넉백 · 잡기 · 낙사 · 시작 · 결승 | `IHitReceiver`, 래그돌 잡기 관절, `KillVolume`, `CourseCheckpoint`, `SpawnPoint`, `FinishZone` |
| 오를 수 없는 면 / 잡기만 되는 면 | `NoClimbSurface` / `GrabOnlySurface` |
| 재질 | `Arena.Lit`, `MapLook.Water`, `MapLook.Smooth` |

기존 코드 변경: `Obstacle.JumpsBetween`(R284, 기본 false) 하나뿐이다. 나머지는 새 파일이다.

## 6. 지시를 해석한 부분 (다르면 알려 주세요)

| 지시 | 이렇게 만듦 | 이유 |
|---|---|---|
| "넓은 공간에서 랜덤하게 돌진하며 돌아다니는 마차 4개" | 네 대가 **마당 전체를 같이 쓰고**, 다음 지점을 고를 때 다른 마차들을 피해서 고름 | 처음엔 띠 4개로 나눴는데(검토에서 "차선 4개처럼 보이고 띠 사이 · 벽 쪽이 안전 지대"로 지적), 진짜로 돌아다니며 안전 지대가 없게 바꿈 |
| "랜덤" | 모든 PC가 같은 시작 시각 · 시드로 같은 "무작위" 계획을 만든다 | 온라인에서 마차가 사람마다 다른 곳에 보이면 안 됨(절대 규칙 7). 그래서 패킷 없이 같다(경기에 연결할 때 공유 시작 시각만 넘기면 됨) |
| "길 2개가 닿을 정도로 거대한 물레방아" | 반지름 9 m 물레방아 하나, 축을 사이에 두고 길 2개가 **물레방아 면을 뚫고** 지나감 | 사진의 "원형 틀 안 6개 작대기 사이 구멍"으로 통과하려면 길이 바퀴 면을 지나가야 함. 축 받침 기둥은 두 길 사이 물속에 있음(사진처럼) |
| "점프 + 슬라이딩으로 구멍 통과" | 물레방아 자리에서 길이 **2.6 m 끊겨** 있음. 그 위를 바퀴살이 지나감 | 걸어서도, 전력질주로 발을 디뎌서도 못 건넌다(질주로 떨어지면 약 0.6 m 내려가 넘어감 도움 범위를 넘음). **달려서 점프**(약 3.5 m)하거나 **점프 + 다이빙**(+3 m/s)이어야 건넌다. 금색 줄(기다리는 곳)은 바퀴살에서 0.8 m 떨어져 있어 기다리다 맞지는 않는다 |
| 3 · 4구간 | 그대로 두되 24 m 당김 | 지시하지 않은 구간은 바꾸지 않음. 경기장 앞 빈 30 m는 10 m만 남김 |

R284의 해석(세로 물레방아는 서 있는 바퀴, 슬라이딩 = 다이빙, 성문 밀기 = 잡기 + 걷기, 목재 다리 긴 변 경첩)은 그대로다.

## 7. 밸런스 (계산값, 측정 아님)

| 구간 | 대략 |
|---|---|
| 1 마차뜰 | 48 m. 1시간 시뮬레이션(시드 3개): 마차 한 대가 **분당 약 11번**(5초에 한 번) 돌진, 평균 돌진 거리 **17.5 m**. **시간의 약 90%**는 적어도 한 대가 돌진 중(2대 이상 약 55%). 마차끼리 가장 가까이 붙은 것도 중심 간 5.39 m(설계 한계, 닿지 않음) |
| 2 물레방아 | 길마다 구멍 4초마다 1.5초. 12명이 길 2개로 나뉘면 한 길에 약 6명이 몰려 밀치기 · 떨어뜨리기가 생김 |
| 3 | 가운데: 회전 마차 + 세로 물레방아 타기 약 6.5초. 좌우: 경사로 33 m + 화물 |

실제 통과 시간과 재미는 **Unity에서 재 봐야 한다**. 맞추는 손잡이: 마차 `moveSpeed` · `turnSeconds` · `minAim`/`maxAim` · `minRest`/`maxRest` · `minCharge`/`maxCharge`, 물레방아 `rotationSpeed` · `spokeCount` · `passageWidth`, 회전 마차 · 화물 값.

## 8. 멀티플레이

- 거대 물레방아 · 회전 마차 · 세로 물레방아 · 화물 · 목재 다리 · 시작 성문 모양은 **공유 시계의 순수 함수**다. 그래서 패킷 없이 모든 PC가 같은 모습을 본다.
- 돌아다니는 마차는 **시작 시각 + 시드**로 만든 계획이다. 시작 시각이 같으면 패킷 없이 모든 PC가 같은 마차를 본다(`CarriageFleet.StartAt(공유 시각)`; 바뀐 계획으로는 순간 이동 컷, 마당을 가로질러 쓸지 않음).
- 시계가 1시간 넘게 뛰면(매치 시계로 바뀌거나 돌아올 때) 마차 계획은 그 시각에서 새로 시작한다. 그 사이 구간을 다 계산하느라 멈추거나, 돌아간 시계에서 마차가 서 버리지 않는다. 주의:
  - 새로 시작하는 시각은 PC마다 자기 시계가 뛴 순간이다. 경기는 시계를 바꾼 **뒤에** `StartAt`을 불러야 PC끼리 같아진다.
  - 1시간보다 먼 미래의 시작, 1시간 넘게 지난 시작(1시간 넘게 진행된 경기에 늦게 들어온 사람)도 지금 시각에서 새로 시작한다. 한 판이 몇 분이라 상관없다.
  - 지금 장면 중에는 시계가 바뀌는 곳에 이 맵이 없다(맵 시험 씬은 오프라인, 폰 러쉬 경기는 씬을 열기 전에 Steam 시계로 바꿈). 안전장치로 둔 것이다.
- 넉백 · 물레방아에서 놓아 주기 · 미끄럼 당김은 **방장(오프라인)만** 계산한다.
- **아직 안 된 것:**
  - 최종 성문 열림 상태 전달(오프라인 전용, `StateDrivenMover`)
  - 시작 성문 · 마차 무리의 공유 시작 시각 연결(`TimedGate.OpenAt` · `CarriageFleet.StartAt`; 지금은 PC마다 켜진 시각 기준이라 온라인에서는 마차가 PC마다 다르게 보임)
  - 이 맵을 경기 씬 · 로비에 잇는 것

## 9. 시험장 (`RoyalCarriageYard_Obstacles`)

| z | 장애물 |
|---|---|
| 10 ~ 26 | 달리는 마차(직선 26 m) |
| 30 ~ 42 | 돌아다니는 마차 한 대(30 × 6 m 영역, 한 대짜리 무리) |
| 46 ~ 70 | 회전 마차 |
| 76 ~ 96 | 거대 물레방아 + 길 2개(물 위), 체크포인트 1 |
| 106 ~ 116 | 목재 다리: 왼쪽 긴 변 경첩 · 오른쪽 끝 경첩, 체크포인트 2 |
| 122 ~ 160 | 화물 경사로(높이 5 m) |
| 168 ~ 179 | 세로 물레방아 → 플랫폼(y 8), 체크포인트 3 |
| 179 ~ 210 | 최종 성문, 체크포인트 4, 결승 |

## 10. 파일

| 무엇 | 경로 |
|---|---|
| 맵 프리팹 | `Assets/Prefabs/PawnRushMaps/RoyalCarriageYard.prefab`, `RoyalCarriageYard_Obstacles.prefab` |
| 장애물 프리팹 9개 | `Assets/Prefabs/Obstacles/RoyalCarriageYard/` (R286에 `RoamingCarriage` 추가, 혼자 놓으면 한 대짜리 무리) |
| 맵 코드 | `Assets/Scripts/PawnRush/CarriageYard/`:<br>• 맵: `RoyalCarriageYard.cs`(+ `.Sections` · `.Scenery`)<br>• 시험장: `CarriageYardObstacleBench.cs`<br>• 장애물: `CarriageFleet` · `RoamingCarriage` · `HorizontalWaterwheel` · `RotatingCarriageGroup` · `VerticalWaterwheel` · `CargoChute` · `RollingCargo` · `ChargingCarriage` · `TiltingWoodenBridge` · `TimedGate` · `PushGate`<br>• 넉백 `YardImpact`, 도구 `YardKit`, 폭포 그림 `WaterfallFlow` |
| 마차 무리 계획 (R286) | `Assets/Scripts/Core/RoamingFleet.cs`, 시험은 `Tests/Network/NetworkCoreTests.cs`의 "Royal Carriage Yard roaming fleet" |
| 기존 코드 변경 | `Assets/Scripts/Gameplay/Obstacles/Obstacle.cs`(`JumpsBetween`, R284; R286부터 첫 물리 프레임도 Awake 때 자세에서 `JumpsBetween`을 물음 — 그 사이 받은 새 계획 · 다시 시작한 화물은 쓸지 않고 바로 옮김) |

## 11. 확인한 것 / 못 한 것

- **확인 (R286):**
  - Core 155 · 세션 55 테스트 통과. 새 시험이 실제 마당 설정(마차 4대, 장면 시간 · 유닉스 시간)으로 10분 동안 1/60초마다 확인함:
    - 마차끼리 중심 간 5.39 m 안으로 들어오지 않는지(몸통 2개 + 0.6 m)
    - 마당 안에만 있는지, 순간이동 없는지
    - 돌 때만 방향이 바뀌고 돌진할 때만 움직이는지
    - 영역 네 가장자리 3 m 안까지 다 가는지(마당 전체를 돌아다님)
    - 계획이 시간을 한 번에 넘기든 잘게 넘기든 같은지
    - 시계가 매치 시계로 뛰거나 돌아오면 그 시각에 새로 만든 무리와 똑같이 움직이는지(50분 틈은 그대로 이어 계산)
  - Core ~ RagdollLab ~ PawnRush(에디터 포함)를 **Unity 6000.3 DLL로 컴파일** 통과(경고 늘지 않음)
  - 장애물 시계 검사 통과
  - 배치 수치 점검 통과:
    - 물레방아 테 안쪽이 길 바깥 모서리 밑 0.4 m를 지남
    - 굴대 통과 4.5 m, 수로 바닥과 0.77 m 떨어짐
    - 받침 기둥 · 물길과 겹치지 않음
  - 네 관점(요구사항 · Unity 실행 · 온라인 동기화 · 배치) 코드 검토 + 반박 검증을 거쳐 고친 것:
    - 성문 밑 바닥 구멍(R284부터)
    - 체크포인트 1 · 2 · 3 폭
    - 마차가 "차선 4개"처럼 움직이던 것
    - 세로 물레방아 · 성문이 사람을 못 찾을 수 있던 검색 칸 부족(R284부터)
    - 기다리는 자리까지 닿던 바퀴살 넉백
    - 질주로 그냥 건너지던 틈
    - 성벽에 가려진 1구간 안내판
  - 마차 무리 코드만 따로 두 관점(계획 · Unity 연결)으로 검토 + 반박 검증. 실제 결함으로 확인된 것은 혼자 놓은 마차에 `phase`가 안 먹던 것 하나이고, 나머지는 지금 장면에서는 생기지 않는 잠재 문제라 보강으로 고침:
    - 시계 출처가 바뀔 때 계획이 그 사이 구간을 전부 계산하다 멈추던 것, 돌아간 시계에서 마차가 서 버리던 것
    - 켜지기 전에 부른 `StartAt`이 무시되던 것, 계획이 바뀔 때(켜진 직후 첫 물리 프레임 전 포함) 마차가 마당을 가로질러 쓸고 가던 것
    - 혼자 놓은 마차에 공유 시작 시각을 줄 방법이 없던 것(`StartAt` 추가; 부르는 곳은 아직 없음)
    - "벽 쪽이 거의 안전하다"는 지적은 반박 검증에서 설계(벽 따라가기는 덜 맞지만 약 33 m 멂)로 판정되어 그대로 둠. 플레이해 보고 너무 안전하면 돌진 끝점 일부를 벽 쪽으로 몰 수 있음(그때는 벽에 끼이는지 따로 확인 필요)
- **못 함:** Unity Play · 화면 · 실제 래그돌 동작. Unity에서 확인할 것은 [VALIDATION](../Network/VALIDATION.md) 맨 위 R286 표에 있다.
- **불확실한 것:**
  - 거대 물레방아 틈을 실제 폰이 점프 · 다이빙으로 넘는 느낌
  - 바퀴살에 밀려 떨어지는 빈도
  - 마차의 돌진 빈도 · 속도 체감
  - 세로 물레방아 매달리기와 최종 성문 밀기(R284부터)
