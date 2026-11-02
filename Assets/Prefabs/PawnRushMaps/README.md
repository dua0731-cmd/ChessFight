# 폰 러쉬 맵 프리팹 폴더

이 폴더에 둔 프리팹(하위 폴더 포함)은 **폰 러쉬 맵 시험 씬**(`Assets/Scenes/PawnRush_MapTest.unity`)의 맵 목록에 저절로 들어간다. 맵 하나 = 프리팹 하나.

## 맵 프리팹 규칙

| 항목 | 규칙 |
|---|---|
| 방향 | 진행 방향은 **+z(북쪽)**, 위는 +y, 단위는 m. 폰 키는 약 1 m |
| 위치 | 프리팹 원점 그대로 월드 원점에 놓인다. 바닥은 y 0 근처, y −30 아래로 떨어지면 시작점으로 돌아간다 |
| 시작점 | `SpawnPoint`(index가 가장 작은 것) → 없으면 이름이 `Start`인 자식 → 없으면 남쪽 가장자리 가운데 바닥 |
| 낙사 | 물이나 구덩이 아래에 `KillVolume`(트리거 상자). 들어가면 1.5초 뒤 마지막 체크포인트로 |
| 체크포인트 | `Checkpoint`(번호 순서). 맵 시험 씬의 R이 마지막 체크포인트로 보낸다 |
| 움직이는 장애물 | `Obstacle`을 상속(`Spinner` · `Oscillator` · `Pendulum` 등). 위치는 `ObstacleClock` 시간의 순수 함수(HANDOFF 절대 규칙 7) |
| 오를 수 없는 면 | `NoClimbSurface`, 잡기만 되는 면은 `GrabOnlySurface` |

## 만드는 법

1. Unity 메뉴 **ChessFight → Pawn Rush Map Test → New Map Prefab**: 이 폴더에 시작점 · 바닥 · 낙사 상자가 든 `NewMap.prefab`이 생긴다.
2. 프리팹을 열어 맵을 꾸민다(이름은 바꿔도 된다).
3. Project 창에서 프리팹을 우클릭하고 **ChessFight → Test In Pawn Rush Map Test**를 누르면 시험 씬이 열리고 그 맵으로 Play가 시작된다.

자세한 안내는 [Docs/KingRush/PAWN_RUSH_MAP_TEST.md](../../../Docs/KingRush/PAWN_RUSH_MAP_TEST.md).
