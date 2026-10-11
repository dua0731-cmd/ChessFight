# 장애물 클래스와 Inspector — R70 (2026-10-10)

테스트 씬의 15종은 각각 전용 클래스를 갖고, 모두 `ChessFight.Gameplay.Obstacle`을 공통 조상으로 상속한다. 부모에 모든 물리 수치를 모으지 않는다. 새 클래스의 조절값은 private `[SerializeField]`이며, 이미 전용 클래스였던 7종은 기존 Inspector 필드 이름과 값을 유지한다.

## 이전 구조와 바뀐 역할

이전 `ObstacleSurface`는 `kind`로 원판·점프 패드·컨베이어를 고르는 하나의 클래스였다. 세 종류의 설정과 배열, 원판 운반, 발사 판정, 컨베이어 표시가 섞여 있어 원판에서도 점프 높이가 보였다. `ObstacleMotion`도 이동·회전·문 개폐를 `kind`로 구분했다.

지금은 두 클래스가 **직접 붙일 수 없는 추상 부모**다. `kind`는 없고 실행할 동작은 자식 클래스가 정한다.

```mermaid
classDiagram
    Obstacle <|-- ObstacleSurface
    Obstacle <|-- ObstacleMotion
    Obstacle <|-- KinematicObstacle
    ObstacleSurface <|-- SpinningDisc
    ObstacleSurface <|-- JumpPad
    ObstacleSurface <|-- ConveyorChessboard
    ObstacleMotion <|-- ReciprocatingPusher
    ObstacleMotion <|-- RotatingHammer
    ObstacleMotion <|-- RisingFallingTiles
    ObstacleMotion <|-- SlidingWall
    ObstacleMotion <|-- ChessClockGate
    ObstacleMotion <|-- ChessClockHand
    Obstacle <|-- WeightedBridge
    Obstacle <|-- FallingChessPiece
    Obstacle <|-- PendulumSwing
    Obstacle <|-- FoldingBridge
    Obstacle <|-- FloorPiston
    Obstacle <|-- SpringPillar
    Obstacle <|-- AirVent
```

- `Obstacle`: 장애물 공통 타입과 공유 시계 접근. Rigidbody나 각 장애물의 수치를 강제하지 않는다.
- `ObstacleSurface`: context 활성 상태와 `IMovingSurface` 계약만 공통 처리. 원판의 `ISurfaceTransport`는 `SpinningDisc`에만 있다.
- `ObstacleMotion`: Rigidbody 설정·시간 함수 실행·탑승 속도 전달을 재사용한다. 축·거리·속도·주기·타격 수치는 자식이 갖는다.
- `KinematicObstacle`: 기존 `Obstacle`의 kinematic 이동 구현을 그대로 옮긴 부모. 기존 Spinner·Oscillator·Pendulum·MovingPlatform·OrbitPlatform·킹러시 이동 장치가 이를 상속한다. 기존 `phase` 데이터와 RequireComponent는 여기서 유지한다.
- `ObstacleImpact`: 충돌을 전달하고 캐릭터별 중복 피격을 막는 보조 컴포넌트. 힘·상승력·재피격 간격은 연결된 장애물에서 읽는다.
- `ChessClockHand`: 09번 체스 시계의 회전 바늘용 보조 자식 클래스. 문은 왕복 개폐, 바늘은 회전이므로 동작을 분리했다.

## 어디에서 조절하나

`ObstacleRagdollTest`를 열고 Hierarchy에서 해당 프리팹을 펼친다. 아래 오브젝트를 선택하면 Inspector에 전용 컴포넌트가 나온다. 여러 판/벽/문은 각각의 값을 편집한다. 영구 저장은 Play를 끈 상태에서 하고 씬을 저장한다.

| # | 장애물 / 클래스 | 선택할 오브젝트 | 주요 조절값 |
|---|---|---|---|
| 01 | 원판 / `SpinningDisc` | `01_SpinningDisc` | Diameter(m), Rotation Direction, Rotation Speed(°/초) |
| 02 | 점프 패드 / `JumpPad` | `02_JumpPad` | Jump Height(m), Forward Speed(m/s), Launch Cooldown |
| 03 | 왕복 밀대 / `ReciprocatingPusher` | `Moving pusher` | Axis, Distance, Period, Phase, Strength, Lift, Hit Cooldown |
| 04 | 회전 해머 / `RotatingHammer` | `Hammer pivot` | Axis, Degrees Per Second, Phase, Strength, Lift, Hit Cooldown |
| 05 | 승강 타일 / `RisingFallingTiles` | `Lift tile …` 각각 | Axis, Distance, Period, Phase |
| 06 | 슬라이딩 벽 / `SlidingWall` | `Left/Right sliding wall` | Axis, Distance, Period, Phase, Strength, Lift, Hit Cooldown |
| 07 | 하중 다리 / `WeightedBridge` | `Tilting deck` | Deck Size, Degrees Per Player, Maximum Tilt 등 |
| 08 | 컨베이어 / `ConveyorChessboard` | `08_ConveyorChessboard` | Direction, Conveyor Speed, Belt Length, Belt Markers |
| 09 | 체스 시계 문 / `ChessClockGate` | `Left/Right gate` | Axis, Distance, Period, Phase. `Clock hand`는 별도 회전 속도 |
| 10 | 낙하 기물 / `FallingChessPiece` | `10_FallingChessPiece` | Drop Height, Warning/Fall/Shock/Rest Seconds, Shock Radius, Knockback, Lift |
| 11 | 진자 공 / `PendulumSwing` | `Pivot` | Swing Angle/Duration, Pause At Ends, Knockback/Upward Force, Hit Cooldown |
| 12 | 접이식 다리 / `FoldingBridge` | `12_FoldingBridge` | Open/Warning/Fold/Closed Duration, Fold Angle, Right Delay |
| 13 | 바닥 피스톤 / `FloorPiston` | `13_FloorPiston` | Rise Height/Duration, Launch/Horizontal Force, 주기 |
| 14 | 스프링 기둥 / `SpringPillar` | `14_SpringPillar` | Extend Distance/Duration, Cooldown, Knockback/Upward Force |
| 15 | 바람 / `AirVent` | `15_AirVent` | Wind Strength/Direction, Max Push Speed, 지상·공중 배율, 주기 |

Phase는 이식 장치에서 **주기의 비율**이고, 기존 `KinematicObstacle`의 Phase는 **초**다. 서로 다른 원래 동작을 보존했으므로 혼동하지 않는다. 원판 45°/초·점프 높이 10m·컨베이어 3m/s 등 기존 프리팹 수치는 유지한다. 슬라이딩 벽과 시계 문은 기존 SmoothStep 개폐, 밀대와 승강 타일은 기존 사인 왕복이다.

## 연결과 검사

- 프리팹 9개와 기존 `ImportedChessFightMap`의 직접 부착 장치 29개를 새 스크립트 GUID로 교체했다. 15종 시험 씬은 같은 프리팹을 사용하므로 별도 재생성하지 않는다.
- 기존 오브젝트/컴포넌트 fileID, 배치, 콜라이더, 재질과 유지되는 수치는 보존했다. 피격 힘과 재피격 간격은 같은 fileID의 소유 장애물로 옮겼다. 기록된 이식 검사: 10개 에셋·50개 변경 컴포넌트, 이동 모드 41개 보존.
- 구조/직렬화 회귀 검사 **7개**: `PYTHONDONTWRITEBYTECODE=1 python3 -m unittest discover -s Tests/Gameplay -p 'test_*.py' -v`.
- 기존 Linux Core **83**, 모의 Steam 세션 **39**, 경계·시계·맵 JSON 검사 및 **7개 런타임 어셈블리 컴파일** 통과. Unity Editor 코드와 실제 PhysX 실행은 이 검사에 포함되지 않는다.
- **Unity import/Play·사용자 재확인 대기**: missing script, Inspector 항목, 15종 동작, 09번 문과 바늘, 기존 이식 맵, 기존 Spinner·이동 발판을 확인한다. 온라인 권한·상태형 장애물의 기존 제한은 동일하다.

코드: `Assets/Scripts/Gameplay/ImportedObstacles/`, 공통 부모와 기존 kinematic 계열: `Assets/Scripts/Gameplay/Obstacles/`.

## R71 — 기존 장애물 전체로 확장 (2026-10-10)

R70의 15종뿐 아니라 이식 장애물 18종 전체, 기존 일반 발판/밧줄/점멸 장치, 킹러시 장난감·성벽·하늘 코스 및 상태형 시소/결승판을 공통 `Obstacle` 체계로 정리했다. 이미 독립된 단일 기능 클래스는 기존 설정/API를 유지한다. 캐릭터·체크포인트·게임 규칙·충돌 전달·context 같은 보조 컴포넌트는 장애물 클래스로 바꾸지 않는다.

| 기존 묶음 | 현재 전용 클래스 | 자식에서 조절하는 값 |
|---|---|---|
| 장난감 Orbit | `KingRushOrbitPawn` | Radius |
| 장난감 RollingHead | `KingRushRollingHead` | Travel, Return Depth, Roll Degrees Per Cycle |
| 장난감 ClockButton | `KingRushClockButton` | Amplitude, Vertical Offset |
| 성벽 Ferry | `KingRushFerry` | Distance |
| 성벽 SlidingWall | `KingRushCastleWall` | Distance |
| 성벽 Drawbridge | `KingRushDrawbridge` | Angle |
| 성벽 Portcullis | `KingRushPortcullis` | Height |
| 성벽 Mace | `KingRushMace` | Angle |
| 하늘 코스 낙하 칸 | `KingRushFallingStep` | Fall Distance, Fall Acceleration, Warning Amplitude |
| 하늘 코스 미는 손 | `KingRushSweepingHand` | Half Travel, Withdrawn Height, Shadow |
| 접촉 발사대 | `ContactLaunchPad` | Landing, Clearance, Same Throw(공통 발사 부모의 필드) |
| 주기 발사 스프링 | `SpringLaunchPad` | 발사 설정 + Period, Phase, Wind Key, Wind Turns |

장난감은 `KingRushToyMotion`에서 Period/Offset, 성벽은 `KingRushCastleMotion`에서 Period, 하늘 코스는 `KingRushSkyMotion`에서 Offset과 복귀 충돌 차단·경고 표시를 재사용한다. 세 부모는 이제 추상 클래스이며 종류 enum/bool로 다른 장애물을 고르지 않는다. 모두 `KinematicObstacle → Obstacle`을 상속한다.

`LaunchPad`도 추상 발사 공통 부모다. 궤적 계산·탑승자 감지·중복 발사 방지를 한 번만 구현하고 자식이 접촉/주기 발사를 결정한다. 기존 `ArcVelocity`, `ArcSeconds`, `Configure`, 발사 횟수·이벤트 등 호출 계약은 유지한다. 생성기는 period > 0이면 스프링, 아니면 접촉 패드를 생성한다. 일반 패드 Inspector에는 스프링용 주기·태엽 설정이 없다.

`CrumblingRoyalFloor`, `KnightCavalryCharge`, `CastlingCrossingBridges`, `RopeLine`, `PhaseToggle`, `KingRushSeesawBoard`, `KingRushFinalBoard`도 공통 `Obstacle`을 상속한다. 각각의 기존 상태 전이·권한·주기·규칙 연계는 보존한다. 여기서 상태 enum은 한 장애물의 준비/경고/작동 단계를 나타내므로 종류 분기와 다르며 유지한다. 기존 `MovingPlatform`의 이동+회전은 하나의 탑승 발판 동작이고 설정도 그 발판에만 속하므로 전용 클래스를 유지한다.

### 이전과 확인

- 기존 킹러시 프리팹 **9개·48개 컴포넌트**의 스크립트 연결을 이전했다. fileID·오브젝트·Transform·콜라이더와 유지되는 설정값을 이전 커밋과 비교해 보존 확인. 기존 씬은 재생성하지 않았다.
- 이전 상수였던 이동 거리/각도 등을 자식 필드로 옮기면서 같은 초기값을 직렬화했다. 기존 상태형 코드의 동작은 바꾸지 않았다.
- Unity Editor 생성기 3개, 퀸오브더힐 런타임 생성기/시험대, 기존 성벽 테스트의 발판 타입 조회도 새 클래스를 사용한다.
- 구조/직렬화 회귀 검사 **10개**, Core **83개**, 모의 세션 **39개**, **7개 런타임 어셈블리** 컴파일 통과. 추상 클래스가 에셋에 직접 붙어 있거나 생성기에서 직접 AddComponent되는지 검사한다.
- **Unity import·PhysX·Editor 생성기 실행은 미확인**. 기존 킹러시/퀸오브더힐/래그돌 시험장에서 이동·경고·복귀·일반 발사·스프링 발사를 재확인한다. 기존 래그돌 물리 자동 점검은 이번 클라우드에서 실행하지 않았다.

## R72 — 원판 크기·방향 설정 (2026-10-11)

`SpinningDisc`의 원판 배열을 제거하고 단일 회전 축 `Rotating Surface`에 기존 세 고리를 연결했다. `Disc Geometry` 아래에 바닥·회전부를 함께 두어 Diameter가 메시와 콜라이더를 같은 비율로 조절한다. 두 Transform 참조는 숨겨진 직렬화 필드이며 Inspector에는 개수 조절이 없다. 기존 고리·콜라이더 ID와 배치는 유지한다. 접지 원형 운반·점프 접선 관성은 그대로이며 방향 선택이 표시 회전과 운반에 함께 적용된다.
