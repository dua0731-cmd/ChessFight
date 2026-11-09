# 폰 러시 미니게임 구간 프리팹

2026-11-06 · R312 · **프로젝트 구현 작업 / 사용자 Unity·Steam 확인 전**

사용자 요청: R311의 공간 이미지·3D 설계를 바탕으로 장애물 구간 프리팹처럼 실제 프로젝트의 미니게임 프리팹들을 제작한다. 초반3종·중반4종·후반3종의 공간과 기존10종 플레이 규칙을 연결한다. 근거는 [모델링 지침](PAWN_RUSH_MINIGAME_MODELING.md), [기획 원문](PAWN_RUSH_MINIGAME_ZONES_v0.1.md), [기존 미니게임 규칙](PAWN_RUSH_MINIGAMES_v1.md), [구간 연결](PAWN_RUSH_SECTIONS.md)이다.

## 1. 파일과 여는 법

### 1.1 현재 작업·Unity 프로젝트 위치(R496)

**현재 프로젝트 루트는 `C:\Users\dua07\GitHub\ChessFighter`, 브랜치는 `main`이다.** 사용자 요청으로 이 기존 폴더를 최신 main `dff53cdc`로 전환했다. 앞으로 작업·Pull·Unity 확인을 같은 경로에서 직접 하며 새 워크트리를 만들지 않는다. 아래 R313의 `.worktrees/api-bridge-main` 열기 절차는 당시 대응 이력이며 현재 열 위치가 아니다.

1. GitHub Desktop에서 기존 **ChessFighter** 폴더를 선택하고 **Repository → Show in Explorer**가 `C:\Users\dua07\GitHub\ChessFighter`를 여는지 확인한다.
2. Unity Hub의 기존 **ChessFighter** 항목에서 같은 폴더를 Unity **6000.3.11f1**로 연다. 목록에 없다면 **Add → Add project from disk**에 이 폴더 자체를 지정한다.
3. 가져오기·컴파일 뒤 `Assets/Prefabs/PawnRushSections`의 `*_MG_*`10종을 보고 **ChessFight → Pawn Rush → Open Mini-game Prefab Test**로 시험 씬을 연다. 저장된 프리팹은 다시 굽지 않아도 된다.

원래 수정 프리팹2개와 각 `.meta`(4파일)는 SHA256 일치 사본·manifest·binary patch를 `C:\Users\dua07\Desktop\개발일지\2학기 프로젝트수업\체스파이트\Backups\R496-20261106-214131`에 보관했다. `R496 preserve original project prefab edits before main migration` stash와 옛 `claude/bold-johnson-8ez95n` 브랜치의 R153~R155 3커밋도 유지한다. 옛 C는 최신 main에 없는 `EarlyPlaza`를 참조하므로 보관본을 main 자산에 자동 적용하지 않는다. 백업은 보존본이고 현재 main 자산으로 복원한 결과가 아니다. 이전 main 작업 폴더는 내용 삭제 없이 detached HEAD로 남겼으며 현재 작업 대상이 아니다. 게임 코드·새 프리팹 재생성 없이 작업 경로를 통일했고, 최신 R492~R495 팀 변경을 유지한다.

Core164·모의세션55는 기존 폴더에서 재통과했다(`Logs/R496-CoreTests.log`). 기존 `C:\Users\dua07\GitHub\ChessFighter`의 `ChessFighter / PawnRush_MiniGames` 창에서 Project의 MG 프리팹 목록과 **Build All Mini-game Prefabs / Open Mini-game Prefab Test / Validate Mini Game Prefabs** 메뉴를 실제로 펼쳐 AI가 관찰했다. Open Test를 GUI로 선택하고 Play하여 C 도개교 궁전·`RagdollPawn`·HUD가 표시되는 Start를 관찰했으며, Game View에 초점을 두고 **숫자5 입력으로 E 왕궁 서고·책장 공간으로 전환**되는 것도 관찰했다. GitHub Desktop의 기존 폴더 선택은 확인 전이고, 전체 숫자1~0·F5·F8·각 퍼즐 실제 완주·사용자 직접 성공·Steam은 미확인이다. 이번 편집기 로그에서 C# 컴파일 오류·Exception·Missing Script 일치0이었고 기존 `PawnHipsHull` ConvexMesh partial hull 경고1건을 관찰했다. 관련 기물 물리 코드는 변경하지 않았다.

### 1.2 R313의 경로 불일치와 당시 대응 이력

2026-11-06 사용자 제보: "전부 풀 받았는데 프리팹도 안보이고 기능도 안보여". GitHub Desktop은 최신 `main`을 받았지만 Unity가 다른 작업 폴더를 열고 있었다. AI가 두 폴더의 Git 상태·실제 파일과 실행 중 Unity의 `Editor.log` 프로젝트 경로를 대조했다.

| 구분 | 프로젝트 루트 | 브랜치·당시 커밋 | 실제 파일 |
|---|---|---|---|
| GitHub Desktop에서 Pull한 폴더 | `C:\Users\dua07\GitHub\ChessFighter\.worktrees\api-bridge-main` | `main` · `5ab4ecc8`(R312) | `*_MG_*.prefab` 10개·`MiniGamePrefabMenu.cs`·시험 씬 존재 |
| 제보 당시 Unity가 연 폴더 | `C:\Users\dua07\GitHub\ChessFighter` | `claude/bold-johnson-8ez95n` · `1ebc9cb0`(R155) | 기존 `Early_C_MoatDrawbridge`·`Mid_2-1_GrandStairHall` 2개, 새 메뉴 코드 없음 |

**GitHub Desktop의 Repository → Show in Explorer가 여는 폴더와 Unity가 연 프로젝트 루트가 같아야 한다.** 같은 프로젝트 이름의 다른 복사본이므로 Unity 창 제목만으로 구분하지 않는다. Pull은 GitHub Desktop에서 선택한 작업 폴더에만 반영된다.

**아래는 R313 당시의 절차다. R496부터는 위의 기존 경로를 사용한다.**

1. GitHub Desktop에서 `main`의 **api-bridge-main**을 선택하고 **Repository → Show in Explorer**로 위치를 확인한다.
2. Unity Hub의 **Projects → Add → Add project from disk**(버전에 따라 Add/디스크에서 프로젝트 추가)로 위의 `C:\Users\dua07\GitHub\ChessFighter\.worktrees\api-bridge-main` 폴더 자체를 등록하고 Unity **6000.3.11f1**로 연다.
3. 가져오기·컴파일을 기다린 뒤 Project의 `Assets/Prefabs/PawnRushSections`에서 `*_MG_*`10개를 보고, **ChessFight → Pawn Rush → Open Mini-game Prefab Test**로 시험 씬을 연다. 새 파일은 이미 저장돼 있으므로 메뉴에서 다시 굽지 않아도 된다.

원래 폴더의 수정 중인 프리팹2개는 그대로 보존한다. 이번 조치는 올바른 프로젝트 열기이며 게임 코드 재생성·기존 브랜치 강제 변경·원본 자산 덮어쓰기를 하지 않는다. **경로 원인은 AI가 확인했고, 아래와 같이 올바른 프로젝트의 실제 Start 표시도 AI가 관찰했다. 사용자 직접 성공 확인·퍼즐 완주·Steam은 미확인이다.**

R313에서 `-projectPath`를 위 main 폴더로 지정하고 `ChessFight.PawnRush.Editor.MiniGamePrefabMenu.OpenTest`로 별도 Unity를 열었다. 기존 `ChessFighter / PawnRush_Sections` 창과 새 `api-bridge-main / PawnRush_MiniGames` 창이 함께 존재하는 것을 확인했다. 새 창의 Play를 눌러 실제 저장 시험 씬에서 `Mini-game being tested`·C 도개교 궁전·`RagdollPawn (Playtest)` 캐릭터·HUD가 나타나는 **Start 흐름을 AI가 관찰했다**. **숫자1~0·F5 조작은 시도 전 사용자 마우스 입력을 감지하여 중지했고 미확인**이다.

이번 `R313-CoreTests.log`는 Core164·모의세션55 통과다. `R313-Open-Main-Editor.log`에서 C# 컴파일 오류·Exception·누락 스크립트 일치0이었다. 공유래그돌 `PawnHipsHull`의 ConvexMesh partial hull 경고1건을 관찰했으며 이번 작업에서 기물 물리 코드를 변경하지 않았다. 이 기록은 AI의 화면·로그 관찰이며 사용자 확인으로 바꾸지 않는다.

### 1.3 프리팹과 시험 장면

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

Unity 메뉴 **ChessFight → Pawn Rush → Build → All Mini-game Prefabs**는 코드에서10개를 다시 만들어 저장하고, 구간 목록의 미니게임 슬롯2·4·6에3·4·3개를 등록한다. 기존 장애물 슬롯과 사용자가 추가한 다른 후보는 보존한다. 재생성은 새10개 파일의 코드 생성 영역을 덮어쓰므로 해당 영역의 변경은 빌더에 반영한다.

**ChessFight → Pawn Rush → Open Mini-game Prefab Test**로 `Assets/Scenes/PawnRush_MiniGames.unity`를 열고 Play한다. 원래 구간 시험 씬의 캐릭터·카메라·물리 설정을 복제한 별도 오프라인 장면이다. 기존 시험 씬과 Build Settings는 바꾸지 않는다.

| 키 | 기능 |
|---|---|
| 1 / 2 / 3 (1단계) | C 도개교 / D 쇠살문 / A 의장 광장 |
| 4 / 5 / 6 / 7 (2단계) | B 동관 / E 서고 / F 무도회장 / J 세탁장 |
| 8 / 9 / 0 (3단계) | G 행차 회랑 / H 체스 홀 / I 침실 |
| F5 | 백팀·흑팀 전환 후 입구에서 시작 |
| F6 | 자기 팀의 완료 이벤트·문 연결 시험. 실제 장치 수행을 대신한 개발용 지름길. (R525) 구간 시험 · 구간 라운드 경기에서도 개발 빌드면 F6 = 우리 팀의 다음 미니게임 완료(경기는 방장이 판정) |
| F8 | 현재 프리팹을 새로 불러와 초기화 |
| R / Backspace | 기존 캐릭터 시험의 체크포인트 / 처음으로 |
| F1 | 자유 카메라 |

개별 프리팹을 열어 정지 상태의 건축·장치를 볼 수 있다. 실내 천장이 전체 배치를 가리면 `ENV_Ceiling_InspectionToggle` 그룹을 편집 중 임시로 끈다.

## 2. 연결과 플레이 소유권

- 뿌리는 `PawnRushSection` + `MiniGameSection`이다. 입구(0,0,0), 출구(0,0,36), 폭44m, 양팀 스테이션 중심 x±12·z4를 유지한다. 앞 구간의 출구 위치·회전에 따라 배치된다.
- `MiniGameSectionBuilder`가 바닥·벽 충돌·두 팀 문·스테이션·체크포인트·낙하 복귀·진행 경로를 만든다. `MissionStation.Install`이 기존 `Mg*` 규칙을 설치한다. 게임 완료는 자기 문만 연다. `TeamBarrier`는 다른 팀의 출구 통과를 막는다.
- A는 공유18×18m판 하나·팀별32칸이다. 공유판 캐시는 같은 프리팹 인스턴스 안에서만 연결된다. C는 전체 폭 해자와 중앙 난간, 측면 권양기로4m 다리 접근을 유지한다. D는 기존8바퀴 규칙이다.
- 기존 Course01의 팔레트와 흐름은 그대로이고 새 `PalaceStyle` 스테이션만 파랑·주황 표시와 짙은 아레나 단을 쓴다. 빨강은 체크·위험 표시다. 기존 아레나 장식·솟아오름을 새 공간에 중복 생성하지 않는다. **승격 발판은 R516부터 다시 만든다**(사용자 11-09 "승격발판 다시 넣어야 함"): 코스 01과 같은 스테이션 자리(입구 쪽 바깥, 스테이션 기준 v ±4.5 · u 1 → 구간 기준 x ±16.5 · z 5, 무대 위 0.4 m)와 같은 규칙(자기 팀 폰이 2초 서 있으면 카드 4장에서 기물 선택, 판마다 한 번), 테두리만 방의 팀색(파랑 · 주황). Play 때 만들어져 프리팹에는 없다. 온라인 구간 라운드는 발판마다 "사용함"과 고르는 사람을 코스 상태로 보낸다(`MatchCourse` RoundMatch, 프로토콜 v29).
- **Blender 모델(R501~):** C 도개교는 `Resources/PawnRushMiniGames/Early/C_Drawbridge_*.fbx`가 Play 때 구간과 다리 힌지·드럼에 붙고, 대신하는 코드 그림은 그리기만 꺼진다(`MiniGameModels`, [원본](../../ArtSource/PawnRush/MiniGames/README.md)). 판정·크기는 그대로다.
- **겹친 면 금지(R498):** 바닥 판 가장자리에 묻는 벽은 `MissionGames.LinedWall`(충돌체 그대로, 그림만 `Proud`=1cm 떼기)로 만든다. A 공유판은 각 스테이션의 금 테두리 중 판 밑 부분을 `PaintBoard.ClipRims`로 잘라 낸다. 새 장식도 다른 면과 같은 평면에 두지 않는다([PITFALLS §39](../Environment/PITFALLS.md)).
- 공통 공간은 초반 석회암 성문·정원, 중반 판벽·큰 창·발코니, 후반 높은 대리석·천창·보라 벨벳이다. 게임별 소품은 기능 장치의 자식으로 붙여 기존 위치·회전·활성 상태를 따른다. 새 장식은 충돌체나 네트워크 상태를 추가하지 않는다.

**온라인 범위:** 현재 구간 조합 코스는 기존부터 온라인 연결 전 상태다. 이번 작업은 프리팹·구간 목록·오프라인 시험 연결이며, 기존 Course01의2광장·4스테이션 Steam 패킷 형식은 바꾸지 않는다. 이10종이3광장 온라인 경기에 자동 연결됐다고 취급하지 않는다.

## 3. 프리팹 저장과 런타임 재연결

단순히 실행 중 오브젝트를 프리팹으로 저장하면 일부 기존 게임의 비직렬화 배열·리짓바디 참조가 복구되지 않는다. 그래서 **건축·장식은 저장한 채 유지하고, Play 시작에 Game socket의 기능 게임만 기존 빌더로 다시 설치**한다. 새 게임에 외형과 텍스트를 다시 연결하며, I는 씨앗 변경·초기화로 Position을 다시 만들 때도 외형을 재연결한다.

- 프리팹에는 편집용 전체 계층과 메시·재질 참조를 저장한다. 빌더가 만든 임시 Mesh·Material·Texture는 원본 캐시를 자산으로 바꾸지 않고 복제해 `Assets/Art/PawnRushMiniGames/`·`Assets/Materials/PawnRushMiniGames/`에 저장한다.
- 기존 FBX·프로젝트 재질처럼 이미 영속 자산인 참조는 그대로 사용한다. 새 Material은 기능성 금·장식 황동·팀색·경고색을 구분한다.
- OS 동적 글꼴과 atlas는 프리팹에 직접 저장하지 않는다. 저장용 기본 폰트를 두고 `PalaceTextBinding`이 프리팹 편집·Play 때 한국어 폰트를 다시 연결한다. 게임 재생성 뒤 텍스트 참조도 갱신한다.
- `MiniGamePalaceArt`의 공통 건축·고유 소품을 고치고 다시 굽는 것이 재현 가능한 수정 방법이다. Game socket 안을 손으로 고친 변경은 Play의 재설치에서 유지되지 않는다. 정적 건축의 편집은 Play가 보존하지만 **Build → All Mini-game Prefabs** 재실행은 코드에서 다시 만든다.

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
| R516 `RunPlayBatch` 추가 | 방마다 **승격 발판 점검**(두 팀 발판이 있고, 위에서 쏜 광선이 발판 높이의 바닥에 닿고, 발판 위 1.8 m 안에 단단한 충돌체가 없음)과 **발판 캡처**(`<게임>_pad_white/black.png`). 실행 옵션: `-miniGameOnly G,H`(그 방만), `-miniGameNoCapture`(그림 없이). 렌더 파이프라인이 Play 중 찍는 `.urtshader` 편집기 오류는 실패로 안 셈. 그 오류가 콘솔의 "Error Pause"로 Play를 멈추게 해서 점검이 게임 시간을 기다리다 시간 초과되던 것(11-09, C 방 캡처 뒤 · F 방 시작 때)을 매 프레임 일시정지를 풀어 막음. 전체 제한 180 → 1200초(승규 님 아트의 첫 렌더 셰이더 컴파일) |
| R520 `RunPlayBatch` 추가 | **F 대무도회장 석상 점검**: 양 팀 32개 석상마다 인형(MODEL_Ride)과 그 열쇠, 팀 띠 고리, 잡기 고리 말고는 켜진 그림이 없고(게임의 폰 몸통 · R312 코드 옷이 꺼짐) `MiniGameDancerLook`가 붙어 있는지. Play에서 한 번 설치될 때 옛 모양이 남던 것(R520)을 다시 잡음 |
| 대표 렌더 | C·E·G 각각 입구/전체 1536×1024 PNG, 총 6장 생성. 대표 각도만의 AI 관찰 자료이며 전체 카메라·충돌·플레이성 검사를 대신하지 않음 |
| 최종 오류 기록 | 최종 Native 로그에 C# 컴파일 오류·예외·누락 스크립트·검사 실패 없음. Play 중 수집된 Error/Assert/Exception 0. 기존 필드·구식 API 컴파일 경고는 남음 |

지속 보관본은 [저장 참조 보고서](ConceptArt/R312/SAVED_PREFABS.txt) · [Play 검사 보고서](ConceptArt/R312/RUNTIME.txt), [C 입구](ConceptArt/R312/C_Drawbridge_entry.png) · [E 입구](ConceptArt/R312/E_Bells_entry.png) · [G 입구](ConceptArt/R312/G_KingEscort_entry.png)다. 원래 로컬 근거는 `Temp-Compile-R312-final.log`, `Temp-R312-native-build-final.log`·`Temp-R312-native-saved-final.log`·`Temp-R312-native-play-final.log`와 `Logs/MiniGameValidation/`의 `saved-prefabs.txt`·`runtime.txt`, 원본 PNG 6장이다. **원본 로그·그림은 Git 무시 대상이며 선택한 사본만 문서에 보관한다.** Unity 정상 종료가 지우는 `Temp/` 대신 `Logs/`에 상세 증거를 남긴다.

초기 저장 검사에서는 `ArenaDressing`의 파일명·MonoBehaviour 직렬화 문제와 편집 미리보기에서 바뀐 OS 글꼴 atlas의 임시 재질을 발견했다. 클래스를 별도 파일로 옮겼고, 저장 자산의 영구 참조를 먼저 엄격히 검사한 뒤 미리보기의 살아 있는 글꼴 참조를 따로 검사한다. 원본 YAML의 글꼴 102개는 모두 내장 글꼴, null 재질 참조는 0개였다. 저장 참조 누락을 허용하도록 검사를 완화하지 않았다.

Play 첫 두 번은 `UnityEditor.Search.SearchDatabase`의 시작 초기화 예외로 실패했다. 실패 기록(`Logs/MiniGameValidation/runtime-search-startup-failure*.txt`, `Temp-R312-native-play-search-startup-failure*.log`)을 보존하고, 편집기 가져오기·컴파일이 끝난 뒤 2초 기다려 Play로 진입하도록 검사 도구를 고쳤다. 최종 실행은 해당 예외 없이 통과했고 오류 필터·검색 캐시는 변경하지 않았다. 제한된 실행 환경의 Unity 자체 통신·라이선스 시작 실패 로그도 `Temp-R312-native-restricted-startup-failure.log`에 남겼다.

**완료 검사는 `ForceComplete`로 이벤트·팀 문 연결을 시험한 것이다. 실제 입력으로 장치를 풀어서 달성한 결과가 아니다.** 사용자 확인 항목은 10종 외형·카메라·동선, 시험 씬의 실제 Start·1~0/F5/F8 키, A 양팀 칠하기, C 실제 매듭 당기기와 다리 통과, D 밀기와 쇠살문, B 운반·설치, E 등반·등 점등, F 타이밍, J 점프/다이빙, G 물체 자체 밀기, H 킹 몰이, I 방패·침대 이동이다. 실제 해결 가능성·손맛·플레이어 빌드·12명 성능·Steam은 미확인이다. 구간 조합 코스의 온라인 연결은 이번 구현 범위에 포함되지 않는다. 항목별 수동 확인 표는 [VALIDATION R312](../Network/VALIDATION.md)에 있다.

## 6. 가운데 문 — 없애기로 함, 아트 담당이 정리 (R515, 2026-11-09)

사용자(11-09 할 일 목록 7번): **"각 미니게임 구간에 중앙문 제거(의미도 없는데 있으니까 햇갈림)"**. 가운데 통로(x ±2, z 4~24)가 북쪽 가운데 블록(x −10~10, z 24~30, 충돌 상자 · `NoClimbSurface`)의 닫힌 문으로 끝나서, 출구처럼 보이지만 열리지 않는다. 팀은 자기 팀 문(x ±12, z 23)으로만 나간다. 문은 **그림뿐**이라 지워도 판정 · 충돌 · 네트워크는 그대로다.

조사해 보니 10개 방의 가운데 문이 세 가지 형태라, 사용자가 **"승규 님께 넘기기"**로 정했다(R515: 코드 · 에셋은 안 바꾸고 이 목록만 남김). 경로는 11-09 main 기준.

| 방 | 지금 가운데 문 | 어디서 | 지울 때 |
|---|---|---|---|
| 공통(코드 셸) | `DEC_ClosedCeremonialDoor`(6.7 × 7.6 m 나무 문 · 쇠띠 · 리벳 · 고리) + `Crown arch`(폭 7.5 · 높이 9.4 m) + 그 위 `Crown` | `MiniGamePalaceArt.Shell.cs` `Facade()`. 10개 `*_MG_*` 프리팹에 **구워져 있음** | 코드에서 빼고 **Build → All Mini-game Prefabs**로 다시 굽거나(굽기는 프리팹 안 손 편집을 덮어씀, §3), `MiniGameModels.Apply`에서 숨김. 뒤에 `North wall mass` · `Ashlar face`가 있어 구멍은 안 생김 |
| A 의장 광장 | `A_Facade.fbx` — Tripo 통짜 메시 하나(성문 정면 · 탑 둘 · 가운데 나무 이중문) | `Assets/Resources/PawnRushMiniGames/Early/A/A_Facade.fbx`, 원본 `ArtSource/PawnRush/MiniGames/Early/a_props_v1.py`(`FACADE` = R506_A_Facade_StoneCastleGate) | 메시에서 문 면을 지우고 벽으로 메워 다시 내보내기(문만 따로 숨길 수 없음) |
| B 보수 중인 동관 | `B_CenterGate`(아치 대문, 16 × 9.9 m) | `MiniGameModels.cs` `RepairWing()`(배치) · `HideCentreDoor(content, 10f)` | 배치를 빼면 셸의 가운데 벽 · 석재면이 보임. 셸 문 그림(위 공통)은 숨겨야 함 |
| C 해자 도개교 | `C_Drawbridge_EnvV2.fbx` 안의 `PR_Early_RoyalGate`(50만 폴리곤 성문: 왕관 달린 가운데 문 + 양옆 아치 통로, 통로는 R502에 뚫음) | 원본 `ArtSource/PawnRush/MiniGames/Early/c_gate_open.py` · `c_drawbridge_v2.py` · `c_drawbridge_v2_export.py`. 셸은 통째로 숨김 | 모델에서 가운데 문을 벽으로(양옆 통로는 팀 길이라 그대로) |
| D 정문 쇠살문 누각 | `D_CenterGate` | `MiniGameModels.cs` `Capstan()`(배치) · `HideCentreFacade(content)` — **가운데 벽까지** 숨김 | 배치를 빼면 `HideCentreFacade` 대신 문 그림만 숨겨야 벽이 남음 |
| E 왕궁 서고 | `E_HallEnv.fbx`(통짜 메시) 안의 R507_ModularFantasyKit "가운데 정면 — 닫힌 이중문"(폭 16 m, 앞면 z 23.69) | `ArtSource/PawnRush/MiniGames/Mid/e_hall_v1.py`(센터 정면 배치 부분, `GATE_W` · `GATE_FRONT`). 셸은 통째로 숨김 | 스크립트에서 문 정면 대신 벽 조각으로 바꿔 다시 내보내기 |
| F 대무도회장 | `F_HallEnv.fbx`(통짜 메시, E 홀 계열 — 가운데 문 있음으로 보임, 렌더로 확인 필요) | `ArtSource/PawnRush/MiniGames/Mid/f_install_unity.py` | E와 같은 방법 |
| G 왕의 행차 회랑 | `G_CenterGate`("Royal Stone Gateway", 16 × 10.4 m, z 26) | `MiniGameModels.cs` `RoyalProcession()`(배치 · 셸 문 그림 숨김, 셸 가운데 벽은 10.4 m부터 남김) | 배치를 빼면 셸 가운데 벽 아래쪽이 보이는지 확인 |
| H 국왕 체스 홀 | `G_CenterGate` + 그 위 대리석 `Gate head wall`(8.4 m ~ 천장) · 양옆 기둥(x ±8.535) | `MiniGameModels.ChessHall.cs` `ChessHallGate()` · `ChessHallWalls` 쪽 "Over the centre gate" | 대문을 빼면 0 ~ 8.4 m가 빔 → 대리석 벽을 바닥까지 |
| I 국왕 침실 | `ChessHallGate`(G 대문 재사용) | `MiniGameModels.Bedchamber.cs` `KingsBedchamber()` · `BedchamberHall()`(셸 북쪽 정면을 포털 통로만 남기고 숨김) | 대문을 빼면 가운데가 뚫림 → 셸 가운데 벽을 살리거나 벽을 채워야 |
| J 왕궁 세탁장 | `J_HallEnv.fbx`(통짜 메시, E 홀 기반) 안의 가운데 문 + `centre door backing`(Tripo 문짝이 찢어진 곳을 가린 호두나무 판) · 문 밑 문턱 | `ArtSource/PawnRush/MiniGames/Mid/J/j_hall_v1.py`("출구 쪽 문 빈틈 없이 채워" 부분) | E와 같은 방법, 문 뒤 판 · 문턱도 같이 |

- 3단계(G · H · I)의 가운데 보라 카펫(`Central velvet runner`, 셸 바닥)도 이 문으로 이어진다. 문을 지울 때 같이 볼 것.
- 문을 지운 자리는 벽만 남아도 되고(뒤가 막힌 블록), 기획상 장식이 필요하면 문이 아니라고 읽히는 것(문장 · 깃발 · 조각 등)으로.
