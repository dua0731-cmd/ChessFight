# 왕성 정문 돌파 — O04 Royal Gate Rush (R50)

2026-10-11 · `C:/Users/Admin/Documents/GitHub/ChessFight` · `JY-ragdoll_v2` 로컬 적용. 커밋·푸시 없음.

## 바로 테스트하기

`Assets/Scenes/ImportedChessFightMap.unity`를 열고 **Play → Game 화면 클릭**. 기존 RagdollTest 캐릭터가 새 구역 입구의 `(80, 0.04, 3)`에서 시작한다. WASD 이동, Shift 달리기, Space 점프, 마우스 시점, R 시작점 복귀를 그대로 사용한다.

Hierarchy: **Pawn Rush Workspace → Generated Sections → A1** 안의 `O04_RoyalGateRush` 구역이다. 원래 이식된 Map 옆, X=80에 있는 모듈 코스의 첫 구역에 배치했다. 이후 A2/B1/B2/C1/C2는 기존 조합 도구로 새 출구에 맞춰 연결했다.

프리팹: `Assets/Maps/PawnRush/Sections/Obstacles/O04_RoyalGateRush.prefab`. Inspector에서 A1 고정 슬롯을 비우면 장애물 4개 중 3개를 고르는 추첨에 참여한다. 현재는 바로 시험하도록 **A1 고정 + 저장된 미리보기** 모드다.

## 공간과 플레이 흐름

총 길이 **144m**, 주행 바닥 폭 **20m**, 입구 `(0,0,0)` / 출구 `(0,0,144)`, 진행 방향 +Z, 루트 Scale=1. 장식·복귀 공간을 포함한 Authoring Bounds는 중심 `(0,8,72)`, 크기 `(34,22,144)`다. 청색/적색 카펫은 길을 읽기 위한 장식이며 백팀/흑팀의 통과 권한을 뜻하지 않는다.

| 로컬 Z | 구간 | 구현과 의도 |
|---|---|---|
| 0–16 | 출발 광장 | 안전한 진입, 체커 바닥·왕실 깃발·폰 동상 |
| 16–48 | 이동 룩 벽 | 낮은 룩 벽 3개가 좌우 왕복. 가운데 12m 폭을 공유하며 진입 방향을 선택 |
| 48–84 | 낮은 물결 타일 | 중앙 승강 타일 3개, 양측 완만한 높이 0.8m 경사로. 중앙/측면 선택 후 합류 |
| 84–128 | 쌍해머 성문 | 좌우 두 통로에 시간차를 둔 진자 해머. 가운데 섬과 바닥 경고 표시, 양옆 낮은 복귀 발판 |
| 128–144 | 출구 광장 | 다음 미션 구역으로 가기 전 안전한 정렬 공간 |

연속 바닥과 넓은 통과 여유를 두어 필수 고난도 점프를 요구하지 않는다. 벽 앞 진입 방향, 타일/측면 선택, 해머를 기다리는 공간과 합류점에서 몸싸움이 생기도록 했다. 이 장애물 구역 자체에 미션 잠금·대기 타이머·승패 규칙은 없다.

## 프리팹 구성과 조정

```text
O04_RoyalGateRush [PawnRushSection]
├─ Entrance / Exit
├─ Geometry
│  ├─ Continuous Deck / Checkerboard Surface
│  ├─ WaveRoute
│  ├─ Center Route Island / Gate Pier
│  └─ Left Recovery Terrace / Right Recovery Terrace
├─ Obstacles
│  ├─ RookWall_01..03 [ObstacleMotion, Rigidbody]
│  ├─ WaveTile_01..03 [ObstacleMotion, Rigidbody]
│  ├─ LeftRoyalHammer [ObstacleContext]
│  │  └─ Pivot [PendulumSwing, Rigidbody]
│  │     ├─ ChainVisual
│  │     └─ HammerHead [CapsuleCollider]
│  │        └─ HitArea [Trigger, ObstacleImpact]
│  └─ RightRoyalHammer（같은 구조）
├─ Decoration
└─ Readability
```

| 대상 | 초기값 | 조절 위치 |
|---|---|---|
| 룩 벽 | 몸체 3×1.2×1.6m, X 진폭 ±1.5m, 주기 6초, 위상 0 / 1⁄3 / 2⁄3 | 각 `RookWall`의 ObstacleMotion. 한쪽 최소 통과 폭 3m 유지 |
| 승강 타일 | 6×6m, 상면 0.05–0.45m, 주기 6초, 위상 분산 | 각 `WaveTile`의 ObstacleMotion. 타일 사이 고정 바닥 2m |
| 해머 | SwingAngle 35°, SwingDuration 2.5초(편도), PauseAtEnds 0.2초 | Pivot의 PendulumSwing. 전체 주기 5.4초 |
| 해머 시간차 | StartDelay 좌 0.5초 / 우 1.85초 | PendulumSwing |
| 피격 | KnockbackForce 5.5 / UpwardForce 1 | PendulumSwing. 기존 IHitReceiver로 전달 |
| 재피격 간격 | 1.25초 | HitArea의 ObstacleImpact.hitCooldown |
| 해머 충돌체 | Z축 Capsule, 반경 1.2 / 높이 2.8 | HammerHead. Trigger는 반경 1.27 / 높이 2.94 |
| 복귀 발판 | 바닥 -1.2m, 폭 4m, 복귀 경사 길이 8m | Geometry의 양쪽 Recovery Terrace |

움직이는 벽·타일·진자 Pivot은 Kinematic Rigidbody다. 벽과 타일에는 강제 넉백 컴포넌트를 붙이지 않았다. 해머만 기존 `ObstacleImpact → ImportedObstacleActor → IHitReceiver`를 사용한다. ChainVisual과 카펫·왕관 장식에는 플레이어를 막는 Collider가 없다. 경사로는 고정 MeshCollider다.

기존 `ObstacleMotion`, `PendulumSwing`, `ObstacleClock`, `ImportedMovingSurface`를 재사용하며 플레이어 컨트롤러, 래그돌 튜닝, 네트워크, 승패 코드는 수정하지 않았다. 새 런타임 스크립트는 없다.

## 제작 도구와 안전한 편집

`Assets/Maps/ImportedChessFight/Editor/RoyalGateRushBuilder.cs`는 최초 프리팹·재질·메시 생성 및 A1 배치를 담당한다. 메뉴는 **ChessFight → Pawn Rush → 왕성 정문 돌파** 아래에 있다. 이미 존재하는 프리팹은 다시 생성하지 않아 수동 디자인을 보존한다. 생성 메뉴를 다시 실행하면 `Generated Sections`는 기존 조합 도구로 재조합되므로, 씬 인스턴스에만 남긴 디자인은 먼저 프리팹에 반영해야 한다. 메뉴를 누르기 전 씬 백업은 `Temp/RoyalGateRushBackup`에 저장된다(Temp는 영구 보관용이 아니다).

`RoyalGateRushProbe.cs`는 명시적으로 메뉴를 눌렀을 때만 실행하는 Editor 전용 검사다. Play Mode에서 물리/기본 이동/복귀 경사로 점검을 각각 선택한다. 검사 중에만 기존 LabGame 입력을 잠시 멈추고 실제 PawnInput으로 캐릭터를 움직이며, 끝나면 입력을 돌려주고 출발점으로 복귀한다. 게임 빌드에는 포함되지 않는다.

지속할 지형 수정은 Prefab Mode에서 한다. 재질과 메시 위치는 `Assets/Maps/PawnRush/RoyalGateRush/Materials`, `Meshes`다. 아트는 프리미티브 기반이며 추후 교체할 수 있다.

## 자동 검사와 남은 플레이테스트

Unity 6000.3.11f1의 실제 편집기에서 생성·저장·렌더와 Play Mode 검사를 수행했다. 상세 원문은 [ROYAL_GATE_RUSH_VALIDATION.txt](ROYAL_GATE_RUSH_VALIDATION.txt)에 보관한다. 사용자 실기 확인 및 Steam 통합 테스트를 대신하지 않는다.

- 참조·연결·카탈로그·A1 고정·스폰 구성 검사 통과. 기존 워크스페이스의 6개 구역 연결 검사도 통과.
- 벽 3개와 타일 3개의 Kinematic 이동, 타일 한 주기 탑승, 해머 실제 피격 및 중복 피격 방지 검사 통과. 타일 탑승 중 골반 높이 0.30–0.70m, 수평 표류 0.00m. 해머 시험에서는 2회 피격, 최대 수평 변위 3.83m, 실행 오류 0건.
- 기본 이동만 사용하는 안전 경로: 입구 Z=3에서 출구 전 Z≈140.44까지 **25.96초**. 점프·질주·능력·다른 플레이어 없는 측정이다. 양 끝 이동까지 합치면 약 27초 수준이며, 처음 예상한 무방해 30–45초보다 빠른 편이다. 강제 대기 장치로 늘리지 않고 우선 사용자의 난도 평가를 받는다.
- 기존 Core 34개, 모의 Steam 세션 15개 통과. 이번 변경의 별도 Windows 플레이어 빌드는 실행하지 않았다.
- 양쪽 복귀 경사로: 하단 발판에서 본 코스로 점프 없이 복귀하는 9개 지점 이동 검사 통과(합계 14.00초, 실행 오류 0건). 각 복귀 경사로만 오르는 시간과 14초를 혼동하지 않는다.
- 시작할 때 기존 `PawnHipsHull` Convex Mesh의 256 폴리곤 제한 경고가 남는다. 새 맵 생성 전에도 발생하던 래그돌 경고이며 이번 범위에서 플레이어 메시를 바꾸지 않았다.
- 자동 검사 종료 후 Play 중 Editor 스크립트를 새로 불러온 세션에서는 기존 `RagdollPawn.Drives`의 `Invalid quaternion rotation` 경고가 재현됐다. 그 상태를 끝내고 새로 시작한 최종 Play 세션에서는 해당 경고·컴파일 오류·실행 예외가 0건이었다. 플레이어 코드는 수정하지 않았다. 스크립트를 수정했다면 Play를 다시 시작하는 편이 안전하다.

12인 교전 시 목표 45–65초와 한 판 약 5분은 아직 측정하지 않았다. 세 장애물 구역과 세 미션의 실제 조합 시간으로 조정해야 한다. 이번 프리팹은 오프라인 시험 환경에 연결했으며 기존 모듈 환경의 온라인 호스트 판정/미션 상태 전송 미연결 상태는 동일하다.

사람 확인 항목: 양쪽 통로의 난도 차이, 해머 피격 후 회복 시간, 여러 명이 룩 벽/중앙 섬에 몰렸을 때 끼임, 복귀 경사로 가독성, 카메라 시야, 12인 성능과 평균 통과 시간. [사용자 확인 표](../Network/VALIDATION.md)의 R50에 결과를 남긴다.
