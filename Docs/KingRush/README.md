# 킹러시

첫 미니게임. 폴가이즈식 장애물 레이스. 담당: 진호·지성(맵·장애물). 사용자 요구(R11): 킹러시 맵을 구현할 씬. 팀원용 설명은 [팀 가이드 6장](../TEAM_GUIDE_KO.md)(같은 내용).

| 문서 | 내용 |
|---|---|
| 이 문서 | 씬 구성, 코스 컴포넌트, 오프라인 플레이테스트, 네트워크 한계 |
| [OBSTACLES.md](OBSTACLES.md) | 장애물 규칙, 기존 장애물, 새 장애물 만드는 법 |
| [OBSTACLE_TEMPLATE.md](OBSTACLE_TEMPLATE.md) | 장애물 기획서 양식. 완성본은 `Docs/KingRush/Obstacles/이름.md` |

## 1. `Assets/Scenes/KingRush.unity` 현재 배치 (자리만 잡은 뼈대)

```text
z = -20 ~ 20   Start Platform   40 × 40   스폰 12곳 (청 6 / 주황 6, y = 0)
z =  20 ~ 200  Runway           폭 16
                 z=50   Spinner A      회전봉 (점프로 넘기)
                 z=80   Checkpoint 1
                 z=100  Sliding Wall   좌우로 미끄러지는 벽
                 z=130  Pendulum       진자
                 z=150  Checkpoint 2
                 z=180  Spinner B      반대로 도는 회전봉
z = 200 ~ 230  Finish Platform  Finish Zone
```

크기·배치·장애물은 기획대로 바꿔도 된다. **남겨 둘 것:** 스폰 지점, 체크포인트, 골인 컴포넌트, `Playtest`, `Physics`(`PhysicsProfile` 120Hz), `ChessFight Game Root`(네트워크 경기 화면용).

씬 파일은 `Tools/Generators/gen_scenes.py`로 처음 만들었다. **Unity에서 저장한 뒤에는 생성기를 다시 돌리지 않는다.**

## 2. 코스 컴포넌트 (`Scripts/Gameplay/Course/`)

| 컴포넌트 | 역할 |
|---|---|
| `SpawnPoint` | 입장 위치. **바닥(발 닿는 곳)**에 둔다. index 0은 오프라인 스폰, 나머지는 경기 때 roster slot으로 배정할 예정 |
| `Checkpoint` | 트리거. 지나가면 리스폰 지점이 된다. 더 높은 `Order`만 갱신. 정적 이벤트 `Checkpoint.Reached` |
| `FinishZone` | 골인. 도착만 알린다(`FinishZone.Reached`). 점수·라운드 종료는 모드 규칙 몫 |
| `PhysicsProfile` | 씬 동안만 물리 120Hz / 솔버 24회, 나갈 때 복구 |

## 3. 오프라인 플레이테스트

KingRush를 직접 열고 Play → Steam 없이 캐릭터 하나. WASD, Space, 왼쪽 클릭(밀치기), 오른쪽 누르기(잡기), R(체크포인트로), Backspace(처음부터). 떨어지면(`fallLimit`) 자동 리스폰, 골인하면 기록 표시.

지금 캐릭터는 **임시 CharacterController 캡슐**(`Prefabs/Characters/PlaytestCharacter`)이다. 래그돌이 병합되었으므로(2026-09-25) `Playtest`의 `Character Prefab`을 `Assets/ChessFight/RagdollLab/Prefabs/RagdollPawn.prefab`으로 바꾸면 래그돌로 달릴 수 있다(**Unity 미확인**, [Player/RAGDOLL §4](../Player/RAGDOLL.md)).

## 4. 네트워크 경기에서의 한계

- 경기가 시작되면 모두 KingRush로 넘어가고 장애물은 **모든 PC에서 같은 위치**로 움직인다(공유 시계).
- 하지만 캐릭터는 아직 로비용 평면 모터(38×38, 충돌 없음)라 **스타트 플랫폼 위에만 서 있고 코스를 달릴 수 없다.**
- 체크포인트·골인은 로컬 이벤트다. 호스트 판정·순서 검증은 없다.
- 해결 순서: ~~래그돌 병합~~(09-25 완료) → 래그돌 권한 결정 → 호스트 시뮬레이션 → 체크포인트·골인 호스트 판정 → 라운드 규칙.

## 5. 협업 규칙

- `KingRush.unity`는 **한 번에 한 명만** 편집한다(누가 잡고 있는지 공유).
- 맵은 **구간 프리팹**(`Prefabs/KingRush/Section_01.prefab` 등)으로 만들고 씬에는 배치만 한다.
- 장애물마다 기획서를 쓴다([양식](OBSTACLE_TEMPLATE.md)).

## 6. 진호·지성 님 AI 시작 프롬프트

```text
ChessFight 저장소 main에서 feature 브랜치를 만들어 킹러시 맵과 장애물을 작업합니다.
저장소 루트 HANDOFF.md를 먼저 읽고, Docs/KingRush/README.md와 OBSTACLES.md를 읽으세요.
Assets/Scenes/KingRush.unity가 작업 씬입니다. 직접 Play하면 오프라인 플레이테스트가 됩니다.
장애물은 Assets/Scripts/Gameplay/Obstacles/Obstacle을 상속하고 Evaluate만 구현합니다.
반드시 지킬 것: 위치·회전은 ObstacleClock 시간의 순수 함수로만 계산하고 프레임마다 누적하지 않습니다.
각 장애물은 Docs/KingRush/OBSTACLE_TEMPLATE.md 양식으로 기획서를 함께 만듭니다.
맵 구간은 프리팹으로 나눠 두 사람이 같은 씬 파일을 동시에 고치지 않게 합니다.
```
