# 폰 러시 미니게임 구간 프리팹

2026-11-06 · R312 · **프로젝트 구현 작업 / 사용자 Unity·Steam 확인 전**

사용자 요청: R311의 공간 이미지·3D 설계를 바탕으로 장애물 구간 프리팹처럼 실제 프로젝트의 미니게임 프리팹들을 제작한다. 초반3종·중반4종·후반3종의 공간과 기존10종 플레이 규칙을 연결한다. 근거는 [모델링 지침](PAWN_RUSH_MINIGAME_MODELING.md), [기획 원문](PAWN_RUSH_MINIGAME_ZONES_v0.1.md), [기존 미니게임 규칙](PAWN_RUSH_MINIGAMES_v1.md), [구간 연결](PAWN_RUSH_SECTIONS.md)이다.

## 1. 파일과 여는 법

### 1.1 프리팹·메뉴가 안 보일 때(R313)

2026-11-06 사용자 제보: "전부 풀 받았는데 프리팹도 안보이고 기능도 안보여". GitHub Desktop은 최신 `main`을 받았지만 Unity가 다른 작업 폴더를 열고 있었다. AI가 두 폴더의 Git 상태·실제 파일과 실행 중 Unity의 `Editor.log` 프로젝트 경로를 대조했다.

| 구분 | 프로젝트 루트 | 브랜치·당시 커밋 | 실제 파일 |
|---|---|---|---|
| GitHub Desktop에서 Pull한 폴더 | `C:\Users\dua07\GitHub\ChessFighter\.worktrees\api-bridge-main` | `main` · `5ab4ecc8`(R312) | `*_MG_*.prefab` 10개·`MiniGamePrefabMenu.cs`·시험 씬 존재 |
| 제보 당시 Unity가 연 폴더 | `C:\Users\dua07\GitHub\ChessFighter` | `claude/bold-johnson-8ez95n` · `1ebc9cb0`(R155) | 기존 `Early_C_MoatDrawbridge`·`Mid_2-1_GrandStairHall` 2개, 새 메뉴 코드 없음 |

**GitHub Desktop의 Repository → Show in Explorer가 여는 폴더와 Unity가 연 프로젝트 루트가 같아야 한다.** 같은 프로젝트 이름의 다른 복사본이므로 Unity 창 제목만으로 구분하지 않는다. Pull은 GitHub Desktop에서 선택한 작업 폴더에만 반영된다.

1. GitHub Desktop에서 `main`의 **api-bridge-main**을 선택하고 **Repository → Show in Explorer**로 위치를 확인한다.
2. Unity Hub의 **Projects → Add → Add project from disk**(버전에 따라 Add/디스크에서 프로젝트 추가)로 위의 `C:\Users\dua07\GitHub\ChessFighter\.worktrees\api-bridge-main` 폴더 자체를 등록하고 Unity **6000.3.11f1**로 연다.
3. 가져오기·컴파일을 기다린 뒤 Project의 `Assets/Prefabs/PawnRushSections`에서 `*_MG_*`10개를 보고, **ChessFight → Pawn Rush → Open Mini-game Prefab Test**로 시험 씬을 연다. 새 파일은 이미 저장돼 있으므로 메뉴에서 다시 굽지 않아도 된다.

원래 폴더의 수정 중인 프리팹2개는 그대로 보존한다. 이번 조치는 올바른 프로젝트 열기이며 게임 코드 재생성·기존 브랜치 강제 변경·원본 자산 덮어쓰기를 하지 않는다. **경로 원인은 AI가 확인했고, 아래와 같이 올바른 프로젝트의 실제 Start 표시도 AI가 관찰했다. 사용자 직접 성공 확인·퍼즐 완주·Steam은 미확인이다.**

R313에서 `-projectPath`를 위 main 폴더로 지정하고 `ChessFight.PawnRush.Editor.MiniGamePrefabMenu.OpenTest`로 별도 Unity를 열었다. 기존 `ChessFighter / PawnRush_Sections` 창과 새 `api-bridge-main / PawnRush_MiniGames` 창이 함께 존재하는 것을 확인했다. 새 창의 Play를 눌러 실제 저장 시험 씬에서 `Mini-game being tested`·C 도개교 궁전·`RagdollPawn (Playtest)` 캐릭터·HUD가 나타나는 **Start 흐름을 AI가 관찰했다**. **숫자1~0·F5 조작은 시도 전 사용자 마우스 입력을 감지하여 중지했고 미확인**이다.

이번 `R313-CoreTests.log`는 Core164·모의세션55 통과다. `R313-Open-Main-Editor.log`에서 C# 컴파일 오류·Exception·누락 스크립트 일치0이었다. 공유래그돌 `PawnHipsHull`의 ConvexMesh partial hull 경고1건을 관찰했으며 이번 작업에서 기물 물리 코드를 변경하지 않았다. 이 기록은 AI의 화면·로그 관찰이며 사용자 확인으로 바꾸지 않는다.

### 1.2 프리팹과 시험 장면

모든 프리팹은 `Assets/Prefabs/PawnRushSections/`에 둔다. 원래 작업 폴더에서 사용자가 수정한 `Early_C_MoatDrawbridge`와 구분하기 위해 새 파일에는 `MG`를 넣었다.

| 구역 | 게임 | 프리팹 파일 |
|---|---|---|
| 초반 | C 해자 도개교 | `Early_MG_C_MoatDrawbridge.prefab` |
| 초반 | D 정문 쇠살문 누각 | `Early_MG_D_PortcullisCourt.prefab` |
| 초반 | A 정문 의장 광장 | `Early_MG_A_ParadeCourt.prefab` |
| 중반 | B 보수 중인 동관 | `Mid_MG_B_RepairWing.prefab` |
| 중반 | E 왕궁 서고 | `Mid_MG_E_RoyalLibrary.prefab` |
| 중반 | F 대무도회장 | `Mid_MG_F_ClockworkBallroom.prefab` |
| 중반 | J 왕궁 세탁장 | `Mid_MG_J_PalaceLaundry.prefab` |
| 후반 | G 왕의 행차 회랑 | `Late_MG_G_RoyalProcession.prefab` |
| 후반 | H 국왕 체스 홀 | `Late_MG_H_KingsChessHall.prefab` |
| 후반 | I 국왕 침실 | `Late_MG_I_KingsBedchamber.prefab` |

Unity 메뉴 **ChessFight → Pawn Rush → Build All Mini-game Prefabs**는 코드에서10개를 다시 만들어 저장하고, 구간 목록의 미니게임 슬롯2·4·6에3·4·3개를 등록한다. 기존 장애물 슬롯과 사용자가 추가한 다른 후보는 보존한다. 재생성은 새10개 파일의 코드 생성 영역을 덮어쓰므로 해당 영역의 변경은 빌더에 반영한다.

**ChessFight → Pawn Rush → Open Mini-game Prefab Test**로 `Assets/Scenes/PawnRush_MiniGames.unity`를 열고 Play한다. 원래 구간 시험 씬의 캐릭터·카메라·물리 설정을 복제한 별도 오프라인 장면이다. 기존 시험 씬과 Build Settings는 바꾸지 않는다.

| 키 | 기능 |
|---|---|
| 1 / 2 / 3 | C 도개교 / D 쇠살문 / A 의장 광장 |
| 4 / 5 / 6 / 7 | B 동관 / E 서고 / F 무도회장 / J 세탁장 |
| 8 / 9 / 0 | G 행차 회랑 / H 체스 홀 / I 침실 |
| F5 | 백팀·흑팀 전환 후 입구에서 시작 |
| F6 | 자기 팀의 완료 이벤트·문 연결 시험. 실제 장치 수행을 대신한 개발용 지름길 |
| F8 | 현재 프리팹을 새로 불러와 초기화 |
| R / Backspace | 기존 캐릭터 시험의 체크포인트 / 처음으로 |
| F1 | 자유 카메라 |

개별 프리팹을 열어 정지 상태의 건축·장치를 볼 수 있다. 실내 천장이 전체 배치를 가리면 `ENV_Ceiling_InspectionToggle` 그룹을 편집 중 임시로 끈다.

## 2. 연결과 플레이 소유권

- 뿌리는 `PawnRushSection` + `MiniGameSection`이다. 입구(0,0,0), 출구(0,0,36), 폭44m, 양팀 스테이션 중심 x±12·z4를 유지한다. 앞 구간의 출구 위치·회전에 따라 배치된다.
- `MiniGameSectionBuilder`가 바닥·벽 충돌·두 팀 문·스테이션·체크포인트·낙하 복귀·진행 경로를 만든다. `MissionStation.Install`이 기존 `Mg*` 규칙을 설치한다. 게임 완료는 자기 문만 연다. `TeamBarrier`는 다른 팀의 출구 통과를 막는다.
- A는 공유18×18m판 하나·팀별32칸이다. 공유판 캐시는 같은 프리팹 인스턴스 안에서만 연결된다. C는 전체 폭 해자와 중앙 난간, 측면 권양기로4m 다리 접근을 유지한다. D는 기존8바퀴 규칙이다.
- 기존 Course01의 팔레트와 흐름은 그대로이고 새 `PalaceStyle` 스테이션만 파랑·주황 표시와 짙은 아레나 단을 쓴다. 빨강은 체크·위험 표시다. 기존 아레나 장식·솟아오름·승격 발판을 새 공간에 중복 생성하지 않는다.
- 공통 공간은 초반 석회암 성문·정원, 중반 판벽·큰 창·발코니, 후반 높은 대리석·천창·보라 벨벳이다. 게임별 소품은 기능 장치의 자식으로 붙여 기존 위치·회전·활성 상태를 따른다. 새 장식은 충돌체나 네트워크 상태를 추가하지 않는다.

**온라인 범위:** 현재 구간 조합 코스는 기존부터 온라인 연결 전 상태다. 이번 작업은 프리팹·구간 목록·오프라인 시험 연결이며, 기존 Course01의2광장·4스테이션 Steam 패킷 형식은 바꾸지 않는다. 이10종이3광장 온라인 경기에 자동 연결됐다고 취급하지 않는다.

## 3. 프리팹 저장과 런타임 재연결

단순히 실행 중 오브젝트를 프리팹으로 저장하면 일부 기존 게임의 비직렬화 배열·리짓바디 참조가 복구되지 않는다. 그래서 **건축·장식은 저장한 채 유지하고, Play 시작에 Game socket의 기능 게임만 기존 빌더로 다시 설치**한다. 새 게임에 외형과 텍스트를 다시 연결하며, I는 씨앗 변경·초기화로 Position을 다시 만들 때도 외형을 재연결한다.

- 프리팹에는 편집용 전체 계층과 메시·재질 참조를 저장한다. 빌더가 만든 임시 Mesh·Material·Texture는 원본 캐시를 자산으로 바꾸지 않고 복제해 `Assets/Art/PawnRushMiniGames/`·`Assets/Materials/PawnRushMiniGames/`에 저장한다.
- 기존 FBX·프로젝트 재질처럼 이미 영속 자산인 참조는 그대로 사용한다. 새 Material은 기능성 금·장식 황동·팀색·경고색을 구분한다.
- OS 동적 글꼴과 atlas는 프리팹에 직접 저장하지 않는다. 저장용 기본 폰트를 두고 `PalaceTextBinding`이 프리팹 편집·Play 때 한국어 폰트를 다시 연결한다. 게임 재생성 뒤 텍스트 참조도 갱신한다.
- `MiniGamePalaceArt`의 공통 건축·고유 소품을 고치고 다시 굽는 것이 재현 가능한 수정 방법이다. Game socket 안을 손으로 고친 변경은 Play의 재설치에서 유지되지 않는다. 정적 건축의 편집은 Play가 보존하지만 **Build All** 재실행은 코드에서 다시 만든다.

## 4. 주요 코드

| 파일 | 역할 |
|---|---|
| `MiniGameSection.cs`·`MiniGameSectionBuilder.cs` | 고정 게임 구간, 연결·설치·초기화·기능 충돌 |
| `MiniGamePalaceArt.cs`·`.Shell.cs`·`.Games.cs`·`.Props.cs` | 공유 메시·팔레트,3개 건축,10개 게임 외형·주변 소품 |
| `PalaceGateLeaf.cs` | E의 회전 책장 시각. 물리 문 상태는 기존 TeamGate 소유 |
| `PalaceTextBinding.cs` | 저장할 수 없는 OS 글꼴·atlas 재연결 |
| `MiniGamePrefabTest.cs` | 오프라인10종 선택·팀 전환·완료 연결 시험 |
| `PawnRushEditor/MiniGamePrefabMenu.cs` | 영속 자산 굽기·10프리팹·목록 등록·시험 씬 |
| `PawnRushEditor/MiniGamePrefabValidation.cs` | 저장된 자산 참조, 실제 로드·문·초기화·대표 렌더 검사 |
| `Tools/Test-NetworkCompile.ps1` | Unity 번들 Roslyn 사용, 실제 asmdef에 맞는 VFX·PawnRush.Editor 참조 검사 |

## 5. 확인 범위

다음 표는 R312에서 AI가 별도 작업 폴더의 **Unity 6000.3.11f1**로 실행한 결과다. 이후 R313에는 위 §1.1처럼 실제 저장 시험 씬의 C Start·캐릭터·HUD까지 AI가 관찰했으며, 1~0/F5 입력은 여전히 미확인이다. **사용자 Unity·Steam 확인 전**이며, 자동 검사 통과를 실제 퍼즐 해결이나 사용자 확인으로 취급하지 않는다.

| 검사 | 결과와 한계 |
|---|---|
| Core·모의 세션 | `Tools/Test-NetworkCore.ps1`: Core 164개·모의 세션 55개 통과. Steam 실접속 검사가 아님 |
| 실제 Unity DLL·번들 Roslyn | 전체 대상 어셈블리 컴파일 통과. PawnRush·PawnRush.Editor 포함. 외부 검사에서 제외하는 Input은 실제 Unity 가져오기·Play 실행과 구분 |
| Native `BuildBatch` | 종료 코드 0. 10개 프리팹·구간 목록·별도 시험 씬 재생성·저장 |
| 저장 자산 `RunBatch` | 종료 코드 0, 프리팹 10개 모두 PASS·실패 0. 영구 메시·재질·글꼴, 누락 스크립트, 두 팀 연결, 입출구·바닥·통과 공간, A 공유판 1개·64칸, 후보 3·4·3개 검사 |
| 저장 시험 씬 | Preview Scene에서 선택기의 프리팹 10개 순서·스포너·캐릭터 프리팹/드라이버·OrbitCamera/Camera·활성 상태·누락 스크립트 검사 통과. 원래 열린 씬을 저장·수정하지 않으며 Start·키 입력은 실행하지 않음 |
| 실제 Play Mode `RunPlayBatch` | 종료 코드 0, 프리팹 10개 모두 PASS. 빈 씬에서 저장 프리팹의 런타임 캐시·게임 재설치, 두 팀 독립 완료·문, 두 번 초기화, 씨앗 변경 재생성 검사. A 두 인스턴스가 공유판을 섞지 않음, I 초기화 후 킹 외형 유지도 통과 |
| 대표 렌더 | C·E·G 각각 입구/전체 1536×1024 PNG, 총 6장 생성. 대표 각도만의 AI 관찰 자료이며 전체 카메라·충돌·플레이성 검사를 대신하지 않음 |
| 최종 오류 기록 | 최종 Native 로그에 C# 컴파일 오류·예외·누락 스크립트·검사 실패 없음. Play 중 수집된 Error/Assert/Exception 0. 기존 필드·구식 API 컴파일 경고는 남음 |

지속 보관본은 [저장 참조 보고서](ConceptArt/R312/SAVED_PREFABS.txt) · [Play 검사 보고서](ConceptArt/R312/RUNTIME.txt), [C 입구](ConceptArt/R312/C_Drawbridge_entry.png) · [E 입구](ConceptArt/R312/E_Bells_entry.png) · [G 입구](ConceptArt/R312/G_KingEscort_entry.png)다. 원래 로컬 근거는 `Temp-Compile-R312-final.log`, `Temp-R312-native-build-final.log`·`Temp-R312-native-saved-final.log`·`Temp-R312-native-play-final.log`와 `Logs/MiniGameValidation/`의 `saved-prefabs.txt`·`runtime.txt`, 원본 PNG 6장이다. **원본 로그·그림은 Git 무시 대상이며 선택한 사본만 문서에 보관한다.** Unity 정상 종료가 지우는 `Temp/` 대신 `Logs/`에 상세 증거를 남긴다.

초기 저장 검사에서는 `ArenaDressing`의 파일명·MonoBehaviour 직렬화 문제와 편집 미리보기에서 바뀐 OS 글꼴 atlas의 임시 재질을 발견했다. 클래스를 별도 파일로 옮겼고, 저장 자산의 영구 참조를 먼저 엄격히 검사한 뒤 미리보기의 살아 있는 글꼴 참조를 따로 검사한다. 원본 YAML의 글꼴 102개는 모두 내장 글꼴, null 재질 참조는 0개였다. 저장 참조 누락을 허용하도록 검사를 완화하지 않았다.

Play 첫 두 번은 `UnityEditor.Search.SearchDatabase`의 시작 초기화 예외로 실패했다. 실패 기록(`Logs/MiniGameValidation/runtime-search-startup-failure*.txt`, `Temp-R312-native-play-search-startup-failure*.log`)을 보존하고, 편집기 가져오기·컴파일이 끝난 뒤 2초 기다려 Play로 진입하도록 검사 도구를 고쳤다. 최종 실행은 해당 예외 없이 통과했고 오류 필터·검색 캐시는 변경하지 않았다. 제한된 실행 환경의 Unity 자체 통신·라이선스 시작 실패 로그도 `Temp-R312-native-restricted-startup-failure.log`에 남겼다.

**완료 검사는 `ForceComplete`로 이벤트·팀 문 연결을 시험한 것이다. 실제 입력으로 장치를 풀어서 달성한 결과가 아니다.** 사용자 확인 항목은 10종 외형·카메라·동선, 시험 씬의 실제 Start·1~0/F5/F8 키, A 양팀 칠하기, C 실제 매듭 당기기와 다리 통과, D 밀기와 쇠살문, B 운반·설치, E 등반·등 점등, F 타이밍, J 점프/다이빙, G 물체 자체 밀기, H 킹 몰이, I 방패·침대 이동이다. 실제 해결 가능성·손맛·플레이어 빌드·12명 성능·Steam은 미확인이다. 구간 조합 코스의 온라인 연결은 이번 구현 범위에 포함되지 않는다. 항목별 수동 확인 표는 [VALIDATION R312](../Network/VALIDATION.md)에 있다.
