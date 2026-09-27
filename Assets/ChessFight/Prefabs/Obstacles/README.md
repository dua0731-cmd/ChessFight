# 체스파이트 장애물 프리팹

Unity Primitive 기반의 기능형 Graybox 프리팹 10종. 기존 맵에는 배치하지 않았습니다.
Project 창의 `Assets/ChessFight/Prefabs/Obstacles`에서 프리팹을 더블클릭하면 형태와 컴포넌트를 확인할 수 있습니다.

| 파일 | 형태와 기능 | 주요 조절값 |
|---|---|---|
| 01_SpinningDisc | 3중 회전 원판, 중심부에서 빠르고 가장자리에서 느린 접선 이동 + 바깥 밀림 | ObstacleSurface: centerSpeed, edgeSpeed, outwardSpeed, radius |
| 02_JumpPad | 스프링과 방향 화살표가 있는 발판, 밟으면 위/앞으로 발사 | ObstacleSurface: launchVelocity, launchCooldown |
| 03_ReciprocatingPusher | 레일 위 긴 막대가 좌우 왕복하며 충돌 플레이어를 밀어냄 | ObstacleMotion: distance, period; ObstacleImpact: strength, lift |
| 04_RotatingHammer | 기둥에 달린 큰 해머가 수직 원형 궤도로 회전 | ObstacleMotion: axis, degreesPerSecond; ObstacleImpact: strength |
| 05_RisingFallingTiles | 2×2 체스 타일이 번갈아 ±3m 상승/하강, 탑승 플레이어 운반 | 각 Lift tile: distance, period, phase |
| 06_SlidingWalls | 엇갈린 위치의 룩 모양 벽이 좌우에서 번갈아 돌출 | 각 wall: distance, period, phase, strength |
| 07_WeightedBridge | 체스판 다리가 좌우 탑승 인원/위치에 따라 기울고 빈 상태에서 수평 복귀 | WeightedBridge: maximumTilt, degreesPerPlayer, tiltSpeed, slideSpeed |
| 08_ConveyorChessboard | 움직이는 방향 표식이 있는 체스판, 플레이어를 옆/뒤로 운반 | ObstacleSurface: direction, conveyorSpeed |
| 09_ChessClockGates | 두 시계와 2개의 승강 게이트가 반대 위상으로 개폐 | 각 gate: distance, period, phase |
| 10_FallingChessPiece | 착지 경고 → 폰 낙하 → 바깥으로 확장하는 충격파 → 대기 반복 | FallingChessPiece: warningSeconds, fallSeconds, shockRadius, knockback, restSeconds |

## 배치할 때

- 루트는 바닥 기준 중심입니다. 기본 Scale `(1,1,1)`을 권장합니다. 높이·폭은 하위 메시/콜라이더와 해당 동작 컴포넌트 값을 함께 조정하세요.
- 루트 Y 회전으로 방향을 바꿀 수 있습니다. 점핑 발판의 발사 방향과 컨베이어 방향은 루트 로컬 좌표입니다. 왕복 막대와 벽도 로컬 축을 따릅니다.
- 원판 지름 8m, 점프 발판 3m, 컨베이어 6×8m, 다리 데크 6×12m입니다. 발판형 프리팹 표면은 루트보다 약 0.4m 높습니다.
- 상승/하강 타일의 루트 Y가 주변 바닥면입니다. 낙하 위험을 만들려면 **타일 아래의 기존 바닥을 비우고** 낙사/복귀 높이와 연결하세요. 겹친 바닥 위에 놓으면 아래 바닥이 추락을 막습니다.
- 밀대·해머·슬라이딩 벽·시계 게이트·낙하 기물은 별도 통로 바닥 위에 배치하는 형태입니다. 낙하 기물의 루트를 실제 착지면에 맞추세요.
- 흔들다리는 중앙 받침과 진입 경사로를 포함합니다. 하중 감지는 플레이어의 접지 상태와 발 위치를 사용하며 좌우 거리와 인원에 비례합니다. 공중 플레이어는 하중에서 제외됩니다.
- 낙하 기물은 시각적 조형물이고, 실제 판정은 충격파가 담당합니다. 1회 낙하마다 플레이어당 1회 적용됩니다. `coverBlocksShock`가 켜지면 기본 지형 레이어의 엄폐물이 충격파를 막습니다.
- 방향 화살표와 체크 무늬는 장식입니다. 충돌은 단순 콜라이더로 처리합니다.

## 기존 게임과 연결

`ObstacleContext`는 같은 Scene의 `ProtectTheKingMatchController`를 자동 연결합니다. 인스펙터에서 직접 지정할 수도 있습니다. 씬이 분리된 additive 구성에서는 match를 명시적으로 지정하세요.
플레이어 효과는 기존 `PlayerMotor`와 `CharacterController`를 통해 적용됩니다. 기존 체크포인트/복귀 판정을 우회하지 않습니다. 경기 대기/종료 상태에서는 힘을 적용하지 않습니다.

온라인에서는 방장만 플레이어 충돌 효과와 다리 하중을 계산합니다. 주기형 장애물은 기존 경기 시간으로 재생되고, 다리는 스냅샷으로 기울기를 전달합니다. 저장된 Scene의 계층 이름과 순서가 호스트/클라이언트에서 같아야 합니다. 현재 지원 범위는 **사전 배치한 다리 최대 64개**이며, 런타임 생성/삭제 장애물의 네트워크 스폰은 이 작업 범위에 포함하지 않습니다.

다리 상태 추가로 `MatchProtocol.Version`은 2입니다. **온라인 테스트 시 두 PC 모두 같은 최신 빌드**를 사용해야 합니다. 기존 WindowsTest 실행 파일은 자동으로 갱신되지 않습니다. 실제 두 PC Steam 접속/지연 환경의 검증은 별도로 필요합니다.

## 검증/제작 메뉴

- `CHESS FIGHT > Obstacles > Create Missing Prefabs (No Map Placement)`: 없는 프리팹만 생성. 이미 편집한 프리팹을 덮어쓰지 않음.
- `Validate Prefab Assets`: 10종 에셋·스크립트·머티리얼 참조 검사.
- `Run Isolated Play Verification` (`Ctrl+Shift+O`): 임시 런타임 Scene에서 동작 확인 후 Play Mode 종료. 맵에 테스트 오브젝트를 저장하지 않음.
- 검증 결과: `Artifacts/ChessFight/ObstaclePrefabValidation.txt`, `ObstaclePlayVerification.txt`.
- 형태 미리보기: `Artifacts/ChessFight/ObstaclePreviews`.

수치들은 블록아웃용 시작값입니다. 레벨에 배치한 뒤 통로 폭, 밀림 거리, 연속 피격, 12인 동시 탑승과 낙사 복귀 시간을 조정해야 합니다.
