# 폰 러시 코스 01 「여덟 번째 랭크」 — 구현 (R68)

2026-10-06. 기획: [폰 러시 코스 01 레벨 디자인 v0.1](PAWN_RUSH_COURSE01_DESIGN_v0.1.md)(성한 님, 사용자 첨부 원문 그대로). 재료: [PAWN_RUSH_MAP_KIT](PAWN_RUSH_MAP_KIT.md).
**상태: Linux 컴파일·자동 검사만 통과. Unity에서 사람이 본 적 없음.** 확인 목록은 [VALIDATION](../Network/VALIDATION.md) 최상단.

## 1. 여는 법

1. Unity 메뉴 **ChessFight → Pawn Rush → Open Course01** (또는 `Assets/Scenes/PawnRush/PawnRush_Course01.unity`).
2. 그대로 **Play** → 씬에 모듈이 하나도 없으면 코드가 기획서 표대로 코스 전체를 만들고, 래그돌 폰(백팀)이 출발 광장에 선다. 3초 카운트다운 뒤 출발 막대가 내려간다.
3. 모듈을 씬에 실제 오브젝트로 두려면 Play를 멈추고 **ChessFight → Pawn Rush → Build Course01**. 모듈 18개를 `Prefabs/Modules/PR01_*.prefab`로 한 번 굽고(이미 있으면 그대로 씀) 레이아웃 순서로 씬에 놓은 뒤 씬을 저장하고 검증기를 돌린다. 이후 Play는 씬의 프리팹을 그대로 쓴다.

| 키 | 동작 |
|---|---|
| WASD·Space·Shift·좌클릭·우클릭·마우스 | 래그돌 기본 조작(퀸 오브 더 힐 씬과 같은 오비트 카메라) |
| **F5** | 백팀 ↔ 흑팀 바꾸기(그 팀 출발 자리에서 다시 시작) |
| R | 마지막 체크포인트로 · Backspace 처음부터(카운트다운 다시) |
| V | 자유 카메라 |

오른쪽 위에 현재 모듈·랭크·진행 m, 섬에 들어가면 그림 카드 3초와 버튼 진척 막대가 뜬다. 모듈 입장·퇴장 시각은 `Logs/PawnRush/course01_날짜.csv`(프로젝트 폴더, git 제외)에 남는다(열: 모듈, enter/exit, 초, 팀).

## 2. 메뉴 (ChessFight → Pawn Rush)

| 메뉴 | 하는 일 |
|---|---|
| Open Course01 | 코스 씬을 열고 코스 루트 선택 |
| Build Course01 | 빠진 모듈 프리팹만 코드로 굽고 → 씬의 모듈을 지우고 레이아웃 순서로 다시 배치 → 씬 저장 → 검증. 두 번 실행해도 같다 |
| Validate Course01 | 검증기만 돌리고 결과 창 |
| Rebuild Selected Module Prefab | 선택한 모듈 하나만 코드로 다시 굽는다(확인 창, 손으로 고친 내용 사라짐) |
| Mirror Team_White → Team_Black | 모듈 프리팹을 연 상태에서 백팀 절반으로 흑팀 절반을 다시 만든다 |
| Mirror Selected (Left-Right Pair) | 공용 모듈의 좌우 쌍: 선택한 오브젝트를 모듈 가운데 기준으로 거울 복제, 왕복·흔들림 장애물은 위상 + 주기/2 |

확장 모듈 X1~X3은 `Data/Course01Layout.asset`에서 enabled를 켜고 **Build Course01**을 다시 누르면 뒤 모듈이 밀린다.

## 3. 파일

| 경로 | 내용 |
|---|---|
| `Assets/Scenes/PawnRush/PawnRush_Course01.unity` | 카메라·조명·물리 120 Hz·코스 루트(`PawnRushCourse`)·플레이테스트(`PlaytestSpawner` 백팀 + `Course01Playtest`). 생성기 `Tools/Generators/gen_pawnrush_course01.py` |
| `Assets/Maps/PawnRush/Course01/Data/` | `Course01Layout.asset`(모듈 순서·켜기·구운 프리팹), `Course01Kit.asset`(재질 8개 + 가져온 장애물 프리팹 13종) |
| `.../Materials/` | `Floor_Checker`, `Wall_Climbable`(돌 블록 줄눈), `Wall_NoClimb`(매끈한 대리석), `Glass_TeamDivider`, `Trim_White`, `Trim_Black`, `Rank_Gold`, `Hazard_Course`. 셰이더 `CoursePattern`(월드 좌표 무늬: 윗면 2 m 체스판, 옆면 민무늬/블록/대리석), `CourseGlass` |
| `.../Scripts/` (asmdef `ChessFight.PawnRush`) | 아래 4절 |
| `.../Editor/` (asmdef `ChessFight.PawnRush.Editor`) | `Course01Assembler`(Open·Build·Validate 메뉴), `Course01ModuleBuilder`(프리팹 굽기), `TeamMirrorMenu` |
| `.../Prefabs/Modules/` | Build Course01이 처음 실행될 때 Unity가 만든다(지금은 없음) |

## 4. 구현과 기획서 대응

모든 모듈은 `Course01Modules*.cs`에 **기획서 4절 표의 로컬 좌표 그대로** 적혀 있다(주석에 표의 줄). 형식은 "x0~x1, z0~z1, 윗면 y" 박스를 그대로 부르는 `CourseBuilder`(바닥 두께 1 m, 경사로는 회전한 박스).

| 기획서 컴포넌트 | 구현 |
|---|---|
| `CourseModule` | 모듈 루트. `length`, `deltaY`, `isTeamModule`, `pathPoints`(팀 모듈은 백팀 레인 x −6.5) |
| 조립기 | `PawnRushCourse.Assemble`: M0 원점 (0,0,−14), 다음 = 앞 + (0, deltaY, length). 결승선 전역 z 501, 높이 24 m, 길이 합 523 m(표의 길이와 같음) |
| `NoClimbSurface` | **`Assets/Scripts/Gameplay/Course/`에 새로 만듦**(래그돌이 참조해야 해서 공용 Gameplay). 등반 코드 변경 범위: `RagdollPawn.ClimbableHit`(표시된 면은 등반 불가), `RagdollPawn.WallRay`(등반 불가 면 뒤의 등반 가능 면은 가려짐), `PawnHand.Tick`(등반 불가 환경 물체는 잡기·턱 잡기 불가). 그 밖의 래그돌 코드·튜닝은 그대로 |
| `TeamBarrier` | 퀸 오브 더 힐 `OpenPath`와 같은 방식(같은 팀 몸의 충돌을 `Physics.IgnoreCollision`으로 끔, `TeamBodies` 사용). 상대 팀은 막힘, 등반 불가 |
| `Checkpoint` | **기존 `Gameplay/Course/Checkpoint` 그대로 사용**(번호 = order, 통과 시 개인 기록 = `PlaytestSpawner`). 새 `CourseCheckpoint`가 옆에 붙어 공용/팀과 부활 자리 6곳(x −5~5, 2 m 간격)을 갖는다. 기존 `Checkpoint`에 코드용 `Configure(order)`만 추가 |
| `KillVolume` | **`Gameplay/Course/`에 새로 만듦**(`WaterZone`은 몸이 뜨므로 따로). 모듈 입구 바닥 y −8, 폭 ±60 m. `PlaytestSpawner`가 1.5초 뒤 마지막 체크포인트로 부활시킨다(기존 R 키도 체크포인트로) |
| `MiniGameSlot` | 슬롯 번호, 팀, 출구 높이 3/6, 출구 모드(경사로/높은 단, 인스펙터에서 바꾸면 경사로 ↔ 앞면 등반 불가 블록), 문(`SlotDoor`, 출구 높이, 완료 신호 한 번으로 열리고 안 닫힘), `Game socket` |
| `MG_Placeholder` | `MiniGamePlaceholder`: 지름 3 m 버튼, 위의 폰 수 × 0.1/초, 1이면 완료. 기반 `MiniGameBase`(진척 0~1, 완료 신호 하나)는 **저장소에 없어서 새로 만듦**(미니게임 기획서의 이름) |
| `PromotionZone` | 존 번호 ①②③, 발판 네 자리(퀸·룩·비숍·나이트, x −5/−3/3/5). 팀 인원(2~6, `Course01Playtest.teamSize`)에 따라 켜지는 존과 퀸 발판. 규칙 없음 |
| `FinishLine` | **기존 `Gameplay/Course/FinishZone` 사용**(통과하면 플레이테스트에 "골인!"과 기록). M14 z 26 |
| `NoGrabZone` | 위치와 트리거만(M14 z 18~26) |
| `RankMarker` | 금색 가로줄 + 7분할 숫자(글꼴 없이 막대로). 2(출발 뒤 벽), 3(M3), 4(슬롯 ① 문 벽), 5(M8), 6(M10), 7(슬롯 ② 문 벽), 8(M14) |
| 출발 막대 | `StartBar`: 카운트다운 0에 1.4 m 내려감. 오프라인은 씬 시작 3초, 경기용은 `Release()` |
| §6 장치 | 코드로 만든다(`Course01Modules.cs`): `Device_SpinBar`(막대 길이·°/s), `Device_RookWall`(±m·주기·위상), `Device_RotatingBoardRound`(지름 23, 두께 0.5, 18°/s, 회전 체스판과 같은 `ObstacleMotion`+탑승 처리), `Device_PawnStatue`. 원본 씬 오브젝트를 복사한 것이 아니라 같은 컴포넌트·값으로 다시 만든 것 |
| 위상 | 모두 **초**로 지정(`ObstaclePhase.Shift`). `ObstacleMotion`은 자체 위상(주기의 비율)으로 바꿔 넣고, 위상 필드가 없던 `PendulumSwing`·`AirVent`·`FoldingBridge`·`KnightCavalryCharge`에 `phaseOffset`(초, 기본 0)을 추가. `FallingChessPiece`는 원래 있던 `phaseOffset` 사용 |
| 01 회전 방향 | `ObstacleSurface.counterClockwise` 추가(기본 끔 = 원래대로 시계 방향) |
| 02 변형 | 변형 프리팹 파일 대신 **놓인 인스턴스의 값**(위 9, 앞 4.5 m/s)으로 바꿈. 구운 모듈 프리팹 안에서는 원본 02 프리팹의 오버라이드로 남는다 |
| 거울 | `TeamMirror`: 모든 하위 Transform에 x 반사(위치 x → −x, 회전 (x,y,z,w) → (x,−y,−z,w)), 가져온 장애물 프리팹은 루트에서 멈춤. 미끄럼 축 x 반전, 회전 축 (x,−y,−z), 바람 방향 x 반전, 원판 회전 방향 반전, 위상 그대로, 팀 재질·장벽·체크포인트·슬롯·존·출발 자리 소속 교체 |
| 진행도 경로 | 모듈 `pathPoints`를 이은 경로(`PawnRushCourse.Path(team)`), 화면의 "진행 m". M9는 다리 사이가 낭떠러지라 왼쪽 다리(x −4)로 지난다 |
| 검증기 | `Course01Validator`: 출구 높이 = 다음 입구, 필수 경로 틈 ≤ 2.5 m(아래 4 m 안에 바닥이 없는 구간), 부활 자리 아래 바닥, 낭떠러지 아래 낙사 영역, 두 팀 경로 길이 같음, 슬롯 문 높이 = 출구 높이 |
| CSV | `Course01Playtest`: 모듈, enter/exit/restart, 초, 팀 |

### 기획서 수치에서 손댄 곳

| 곳 | 기획서 | 구현 | 이유 |
|---|---|---|---|
| 팀 레인 바닥 안쪽 끝 | x −0.5 | x −0.25(투명 벽에 붙임) | 0.25 m 틈에 발이 걸리지 않게 |
| 섬 폭 | x −12.5~−0.5 | 바깥 −12.5, 안쪽은 투명 벽(−0.25)까지 | 같은 이유 |
| M7 다리 | z 6~9, 32~35 | z 6~8.9, 32.1~35 | "원판과 0.1 m 띄움"을 원판 지름 23 그대로 두고 맞춤 |
| M8 진자 기둥 | x ±6 | x ±7(±6.6~7.4) | 철구(지름 2.2)가 축에서 6.0 m까지 와서 ±6이면 기둥을 친다 |
| 진자 받침(M8, M12) | 없음(프리팹은 천장 걸이만) | 등반 불가 기둥 2개 + 들보 | 걸이가 공중에 떠 있지 않게 |
| 바람 분사구 받침 | 없음 | 등반 불가 기둥 | 분사구가 낭떠러지 위에 떠 있지 않게 |
| 섬 문 | 출구 높이, 폭 3 | 폭 3 문 + 양옆 문 벽(높이 4, 등반 불가) | 6 m 경사로의 나머지 3 m로 문을 돌아가지 못하게 |
| 기병 출발대 | "받침을 벽(불가)으로" | 3 m 받침 2개(K1 (−7, 3), K2 (7, 18)) | 출발점이 광장 위라서 |
| M14 | 관전 단 끝 z 34 | 뒤 난간 + 왕관 받침(z 34.5~38) | 뒤로 떨어지지 않게, 왕관이 떠 있지 않게 |
| 갈고리 | — | 이 씬에서는 폰의 E 갈고리를 못 던짐 | 갈고리는 어느 벽에나 박혀 등반 불가 벽을 넘는다(`HookStartZone`을 코스 밖 아주 아래 하나만 둠) |

## 5. 하지 않은 것 (기획서 범위 밖 또는 다음 작업)

- 미니게임 A~E 구현, 프로모션·결승·과반 판정 규칙, 부활 직후 2초 면역, 온라인 동기화, 래그돌 수치.
- 장애물 힘의 방장 판정 연결(P0). 온라인에서 공유 시계를 켜면 02·03·10·11·15·17의 힘이 꺼지는 것은 그대로다.
- 모듈 프리팹·Kit 프리팹(`Device_*`, `MiniGameSlot_1/2`, `MG_Placeholder`)과 `PR_02_JumpPad_Rank` 변형 파일은 Unity 없이 만들 수 없어 **코드로 만든다**. 모듈 프리팹은 Build Course01이 Unity에서 만든다. 장치·슬롯은 모듈 프리팹 안의 오브젝트로 들어간다.
- `ImportedChessFightMap.unity`와 원본 장애물 프리팹은 건드리지 않았다(스크립트에 기본값 0/끔 필드만 추가).
- 흑팀 절반의 장애물은 거울 복제라서 원본 장애물 프리팹과의 연결(프리팹 링크)이 없다. 원본 프리팹을 고친 뒤에는 모듈 프리팹을 열어 Mirror Team_White → Team_Black을 다시 누른다.

## 6. 자동 검사로 확인한 것과 못 한 것

- `Tools/run-tests-linux.sh --compile`: 런타임(`ChessFight.PawnRush`)과 에디터 코드(`ChessFight.PawnRush.Editor`, NuGet `Unity3D.SDK` 2021.1의 `UnityEditor.dll`)까지 Roslyn 컴파일, Core 83·세션 39, 경계·장애물 시계 검사 통과.
- **못 한 것: Unity 실행.** 셰이더 컴파일, 검증기 결과, 래그돌로 걸어서 완주, 각 갈림길, 장애물 위치·위상, 등반 불가 벽, 팀 장벽은 전부 사람이 Unity에서 봐야 한다(VALIDATION 표).
