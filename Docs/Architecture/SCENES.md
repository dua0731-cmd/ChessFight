# 씬과 진입 흐름

2026-11-06 · R497 정리 기준. 현재 제작용 구간 시험과 로비가 실제로 여는 호환 경기 씬을 구분한다. 작업·Unity 경로는 기존 `C:\Users\dua07\GitHub\ChessFighter`의 `main`이다.

## 1. 현재 장면 목록

| 씬 | 위치·역할 | 여는 메뉴 |
|---|---|---|
| Intro | `Assets/Scenes/Intro.unity` · 시작 영상·타이틀·이름 설정 | Development → Scenes → Intro |
| Lobby | `Assets/Scenes/Lobby.unity` · 파티·모드·매칭 | Development → Scenes → Lobby |
| PawnRush_Sections | `Assets/Scenes/PawnRush_Sections.unity` · 개편 중인 전체 구간 연결 시험 | Pawn Rush → Open Section Test |
| PawnRush_MiniGames | `Assets/Scenes/PawnRush_MiniGames.unity` · 저장 프리팹10종을 골라 보는 오프라인 시험 | Pawn Rush → Open Mini-game Prefab Test |
| PawnRush_SkillTest | `Assets/Scenes/SkillTest/PawnRush_SkillTest/PawnRush_SkillTest.unity` · 기물 스킬 시험 | Pawn Rush → Open Skill Test |
| PawnRush_MapTest | `Assets/Scenes/PawnRush_MapTest.unity` · 맵 프리팹(`Assets/Prefabs/PawnRushMaps/`) 하나씩 오프라인 시험, 왕실 마차뜰 포함(R507에 `claude/nifty-tesla-8212va`에서 병합, 빌드 목록 밖) · 코스 01의 카메라·조명·물리·플레이테스트 폰 + `PawnRushMapTest` | Pawn Rush → Open Map Test |
| PawnRush_Course01 | `Assets/Prototype/PawnRush/Scenes/PawnRush_Course01.unity` · 구형 전체 맵, 현재 온라인 호환 씬 | Prototype → Pawn Rush → Open Course01 |
| PawnRushVictory / PawnRushLose | `Assets/Scenes/` · 폰 러시 결과·로비 복귀 | Development → Pawn Rush → Result Preview |
| SwordFight | `Assets/Scenes/SwordFight.unity` · 소드 파이트 경기 | Sword Fight → Open Test Scene |
| SwordFight 스킬·아레나 시험 | `Assets/Scenes/SkillTest/SwordFight_*` · 기존 스킬/아레나 시험 | Sword Fight → Open Skill Test |
| RagdollTest | `Assets/Scenes/RagdollTest.unity` · 공용 물리·기물 랩 | Development → Ragdoll Lab → Open Test Scene |
| SeoungTest_PawnRush / SeoungTest_SwordFight | `Assets/Scenes/SeoungTest/` · 현재 맵 제작 샌드박스 | Development → Map Authoring |
| ChessBoxingPrototype | `Assets/Prototype/ChessBoxing/Scenes/ChessBoxingPrototype.unity` · 독립 프로토타입 | Prototype → Chess Boxing |
| PiecePreview | `Assets/Prototype/Previews/PiecePreview.unity` · 기물 미리보기 | Project에서 열기 |

위 메뉴는 모두 **ChessFight** 아래에 있다. Unity의 편집 화면에서 빈 장면처럼 보여도 Play 때 프리팹·코스·캐릭터를 생성하는 시험 장면이 있다.

QueenOfTheHill 본 장면·스킬 시험·메뉴는 폐기해 제거한다. 과거 `KingRush.unity`·`KingRushOpening.unity`·`KingRushPrototype.unity`는 이미 R90에 삭제한 장면이다. 그 초기 맵 코드와 프리팹은 이번에 `Assets/Prototype/PawnRush/`로 모았다. 당시 설명은 [역사 사본](../PawnRush/Prototype/README_HISTORICAL.md)이다.

## 2. 빌드와 호환 경기

`ProjectSettings/EditorBuildSettings.asset`의 활성 장면은 런타임에서 이름으로 로드할 수 있어야 한다. Intro·Lobby·SwordFight·폰 러시 결과 장면과 **이동한 구형 PawnRush_Course01**을 유지한다. 기존 스킬/아레나 장면의 필요한 활성 설정은 별도로 관리한다. 새 구간·미니게임·랩·샌드박스를 정리 편의로 온라인 경기 목록에 추가하지 않는다.

`GameModes.KingRush.Scene`과 `SceneNames.PawnRushCourse01`의 값은 **`PawnRush_Course01`**이다. 로비의 폰 러시 온라인 진입은 여전히 프로토타입 폴더로 옮긴 이 씬을 연다. 파일 위치는 바뀌어도 씬 이름·GUID·통신 키 `kingrush`는 유지한다. `NetworkSetup.ShippedScenes`와 생성기에서도 새 실제 경로를 사용한다.

현재 `PawnRush_Sections`·`PawnRush_MiniGames`·`PawnRush_MapTest`는 오프라인 시험이다. 3광장·7슬롯 코스를 현재2광장·4스테이션 CFP4 통신에 연결하는 작업은 이 정리에 포함되지 않는다.

## 3. 온라인 진입

1. Intro 또는 Lobby에서 시작하면 `NetworkRuntime`이 세션·공유 시계·장면 컨트롤러를 만든다.
2. 파티장이 선택한 모드 키를 세션에 기록하고 같은 모드의 방을 찾는다.
3. `FollowMatch`가 경기 모드의 씬 이름을 읽고 네트워크용 플레이어 배치 상태를 설정한 뒤 씬을 연다.
4. 폰 러시는 `SteamPawnRushLink`, 소드 파이트는 기존 래그돌 경기 연결이 명단·입력·스냅샷을 다룬다.
5. 경기 종료·나가기·연결 종료는 기존 세션/결과 흐름을 따라 Lobby로 돌아간다.

Steam을 아는 컨트롤러는 런타임이 붙이며 씬 파일에 저장하지 않는 것이 기본이다. 직접 연 시험 장면과 온라인으로 진입한 경기의 플레이어 생성·권한은 같다고 취급하지 않는다.

## 4. 현재 폰 러시 구간 시험

`PawnRushRound`가 `Assets/Settings/PawnRushSections.asset`의 7슬롯을 뽑아 입구와 앞 구간 출구를 맞춰 이어 붙인다. 지금은 정원 → 초반 미니게임 → 현관홀 → 중반 미니게임 → 후반 미니게임 → 작전 회의실 순서이며 빈 슬롯5는 건너뛴다. 팀 인원·판 씨앗·공유 장애물 시각은 구간 부품에 전달한다.

`SectionPlaytest`의 F5 팀 변경·F8 새 판·F9 인원·이전/다음 구간 이동은 [구간 문서](../PawnRush/PAWN_RUSH_SECTIONS.md)를 따른다. `MiniGamePrefabTest`는 숫자1~0·F5·F8로 저장 프리팹을 골라 보고, F6은 실제 퍼즐 해결을 대신하는 완료 이벤트 시험용이다. [10종 프리팹 사용법](../PawnRush/PAWN_RUSH_MINIGAME_SECTIONS.md)을 먼저 읽는다.

## 5. 새 장면을 추가하거나 옮길 때

1. 역할을 먼저 정한다: 현재 경기·현재 개발 시험은 `Assets/Scenes`, 구형 맵·독립 프로토타입은 `Assets/Prototype`이다.
2. `.unity`와 `.meta`를 함께 옮겨 GUID를 유지한다.
3. 경로 상수·씬 복사 원본·생성기·빌드 목록을 함께 갱신한다.
4. 런타임 로드 씬은 `SceneNames`·`GameModes`·빌드 목록과 맞춘다. 표시 이름 변경과 통신 키 변경을 구분한다.
5. 장면·프리팹의 참조를 검사하고 Play 시작·나가기·다시 열기를 해당 범위에서 살펴본다.
6. 실제 사용자 Unity·Steam 확인은 [VALIDATION](../Network/VALIDATION.md)에 별도로 기록한다.

URP 설정·공통 카메라·래그돌 튜닝은 폴더 정리를 이유로 임의 변경하지 않는다.
