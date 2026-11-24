# 폰 러시 미니게임 구간 프리팹

## R691 · 둘째 미니게임 자리에 G · H · I (2026-11-16)

사용자 "게임에 반영"(R690). 슬롯 4의 후보가 **B · E · F · J · G · H · I** 일곱이 되었다(슬롯 2는 C · D · A 그대로). 게임이 어느 자리에서 나오는지는 `MiniGamePrefabMenu.Pool`(초반 = 첫째 자리, 중반 · 후반 = 둘째 자리)이 정하고, `Zone`(초반 · 중반 · 후반)은 방의 아트 세트로만 쓴다. 광장 번호 · 승격 발판은 슬롯에서 오므로 G · H · I도 둘째 광장이다. 프로토콜 v50. Unity 사본 배치(그래픽 켬): ① `MiniGamePrefabValidation.RunBatch` — 저장된 프리팹 10개 · 슬롯 목록(3 · 7개) · 시험 장면 PASS. 판 뽑기를 씨앗 1 ~ 700으로 돌리면 넷째 슬롯에 일곱 방이 85 ~ 109번씩 고르게 나온다(G 101 · H 109 · I 104). ② `SectionCorridorValidation.RunPlayBatch`로 G · H · I가 뽑히는 판을 하나씩 지음(씨앗 23 = 왕의 행차 회랑, 2 = 국왕 체스 홀, 1 = 국왕 침실): 대계단 현관홀 → 방 → 국왕 작전실로 이어지는 복도의 이음 · 바닥 · 벽 · 뚜껑 · 지나갈 틈이 모두 PASS이고 작전실 컨베이어도 그대로다(사진 `Docs/PawnRush/Checks/R691_SecondGate/`). 폰이 걸어 지나가거나 미니게임을 실제로 깬 검사는 아니고 온라인도 아니다. 사용자 플레이 확인 전.

## R690 · 두 미니게임 자리의 컨셉 — 성문과 윗궁 문 (2026-11-16)

컨셉 정리만이고 게임은 그대로다([한 장 그림](Mockups/R690_TwoGates.svg) · [아트 기준 0절](PAWN_RUSH_ART_PHASES.md)). 슬롯 2 = **첫째 관문 · 성문**(C · D · A), 슬롯 4 = **둘째 관문 · 윗궁 문**(B · E · F · J, 그리고 G · H · I를 다시 넣기로 정함). **R691에서 G · H · I를 넷째 슬롯 목록에 넣었다**(사용자 "게임에 반영").

## R688 · 판에는 미니게임 두 번 (2026-11-16)

한 판이 6구간이 되면서([구간 문서 §3](PAWN_RUSH_SECTIONS.md)) 미니게임 슬롯은 둘이다: 슬롯 2(초반 C · D · A)와 슬롯 4(중반 B · E · F · J). **후반 3종(G · H · I)은 어느 슬롯에도 없어 판에 나오지 않는다** — 프리팹 · 모델 · 미니게임 시험 장면(8 · 9 · 0번)은 그대로다. 다시 넣으려면 `Assets/Settings/PawnRushSections.asset`의 미니게임 슬롯 목록에 프리팹을 끌어 넣으면 된다(`Build → All Mini-game Prefabs`는 초반 · 중반 후보만 등록한다). → **R691: 후반 3종을 둘째 슬롯에 다시 넣었고, 메뉴도 열 개를 다 등록한다.**

## R686 · 나이트의 길 — 갈래길과 킹 통 (2026-11-16)

J 왕궁 세탁장의 게임이 바뀌었다(방 · 수조 · 문 프리팹은 그대로, Play 때 다시 설치되는 게임만). 물 위에 나이트 이동으로 이어진 통이 **2 ~ 4갈래**(3명 2 · 4명 3 · 5명 3 · 6명 4; `b1 a3 b5 a7` / `d1 c3 d5 c7` / `f1 e3 f5 e7` / `h1 g3 h5 g7`, 작은 팀은 가운데 갈래부터), 갈래마다 통 넷, 통 2.4 ~ 2.6 m. 기물은 **팀 인원만큼**(갈래의 5번 줄 통마다 하나, 5 · 6명은 갈래 끝 통에 하나 더, 마지막은 킹). 킹은 문 앞 **킹 통(`d8`)** 위에 있고 킹 통은 물에 1 m 잠겨 있다: 금색 **누름 통 둘(`c7` · `e7`)**이 모두 눌려 있으면 1.2초에 솟고 아니면 2초에 잠긴다. 누름 통은 우리 팀 폰이 서 있는 동안과 내려온 뒤 **8초** 동안 눌린 것으로 친다(혼자 남아도 두 통을 오가면 된다). 킹은 킹 통이 90% 넘게 솟았을 때만 잡힌다. 다 잡으면 팀 문이 열리고 킹 통은 솟은 채로 남는다(문으로 가는 길). 점프대(Space, 위로 7 m/s)는 통 가운데 1.1 m 안에서 듣고, 기물은 **그 기물이 있는 통에 올라서면 잡힌다**(R687 — 통 위에 있거나 기물에서 1.2 m 안, 다이빙 조건 없음; 기물마다 머리 위에 금색 표식). 물에 빠지면 1.2초 뒤 **마지막으로 밟은 통**에서 다시(밟은 통이 없으면 입구 바닥).

코드: Core `KnightRoad`(갈래 · 누름 · 킹 통 규칙) · `MiniGameScale.KnightLanes` · `KnightPieces` · `KnightSquareSize`, `PawnRush/MgKnightPads.cs`(방장이 판정, 게임 블록 5바이트: 잡은 기물 · 점프 횟수 · 점프대 · 눌린 통 · 킹 통 높이), `MgWaterReturn.ReturnPoint`, 통 모델은 `MiniGameModels.Laundry`가 상자 이름(`Road xx`)을 보고 붙이므로 갈래가 바뀌어도 따라온다(킹 통 모델은 킹 통과 같이 움직임). 프로토콜 v48.

- 금색 누름 통에는 굵은 금색 고리와 "함께 밟기" 글씨가 있고, 눌려 있는 동안 고리가 부풀며 뛴다. 킹 통이 물 위로 솟거나 잠길 때 물보라와 물소리가 난다.
- 설명 카드(R679) 문구: "점프대에서 Space로 높이 뛰어 갈래마다 흩어진 상대 기물을 다이빙으로 잡으세요 · 금색 통 둘을 함께 밟으면 물에 잠긴 킹이 떠오릅니다".
- Unity 사본 배치(그래픽 켬)에서 미니게임 시험 장면의 **실제 폰**을 통 위로 옮겨 가며 확인 — 모두 PASS: 6 대 6은 4갈래 · 통 16 + 킹 통 · 기물 6, 3 대 3은 2갈래 · 기물 3, 5 대 5는 3갈래 · 기물 5로 지어지고 세탁장 통 모델이 모든 통(킹 통 포함)에 붙음, d5에 섰다가 물에 빠지면 2.6초 안에 d5로 돌아옴(입구 바닥 아님), c7만 밟으면 킹 통 그대로 · e7까지 밟으면(c7은 8초 유지) 킹 통이 다 솟아 그 위에 설 수 있음, 아무도 안 밟고 11.5초 뒤 다시 잠김, 클리어하면 팀 문이 열리고 킹 통이 솟은 채 남음, 실행 중 Unity 오류 0, 사진 `Docs/PawnRush/Checks/R686_KnightRoad/`.
- **R687: 올라서면 잡힘.** 사용자 "통 위에 구조물을 쓰러뜨리는 시스템 자체도 … 직관적이지도 않아" → 제안 둘 중 "올라서면 잡힘으로". 기물이 있는 통에 우리 팀 폰이 올라서면 잡히고(`KnightRoute.Takes`), 잡을 기물 위에 금색 표식이 돌며, 한 줄 안내에 잡은 수가 붙는다. Unity 사본 배치(그래픽 켬)에서 미니게임 시험 장면의 실제 폰으로 확인 — 모두 PASS: 기물이 없는 통(b1)에 서면 아무 일 없음, d5에 그냥 올라서면 그 기물이 잡힘(1/6), 이어서 b5 · f5 · h5 · g7에 서는 대로 하나씩(5/6), 킹 통이 잠겨 있을 때는 그 위 물에 있어도 킹이 안 잡힘, 금색 통 둘을 밟아 솟은 킹 통에 올라서면 킹이 잡혀 완료 · 팀 문 열림(6/6), 금색 표식은 처음 6개 → 한 개 잡은 뒤 5개 → 끝에 0개, 실행 중 Unity 오류 0(사진 `Docs/PawnRush/Checks/R686_KnightRoad/marks_view.png`).

## R679 · 미니게임 설명 카드 (2026-11-16)

사용자(6 대 6 시험 뒤) "미니게임 설명 더 확실히 (흑 백 진영 구분 및 미니게임 방법)". 지금까지 구간 라운드는 진영 방향(R653)만 알려 주고 **하는 법은 한 번도 보여 주지 않았다**(Core `PawnRushMissions.Hints` 한 줄 힌트는 코스 01의 그림 카드 전용).

- 방 상자(입구 8 m 앞 ~ 팀 문 줄)에 들어서면 화면 위쪽에 **9초 카드**: 방 이름(작게) · 게임 이름 · 하는 법 두 줄 · 팀 칩 둘. 칩은 입구에서 본 순서(왼쪽 = 백팀 청록, 오른쪽 = 흑팀 주황)대로 "입구에서 왼쪽 진영 · 청록색"처럼 적고, 내 팀 칩은 진하게 + "← 우리 팀". A(공용 판)는 "가운데 공용 판 · 밝은 칸 / 어두운 칸".
- 9초 뒤에는 "게임 이름 · 하는 법 첫 마디" 한 줄이 위쪽에 남고, 우리 팀 문이 열리면 사라진다. 20초 넘게 방을 떠났다 돌아오면 카드가 다시 뜬다(방 안 체크포인트로 되살아난 것으로는 다시 안 뜸). 메뉴 · 승격 카드가 떠 있으면 숨는다.
- 문구는 Core `PawnRushMissions.HowTo`(게임마다 " · "로 나눈 두 마디). 인원수별 규모(R543) 때문에 **개수 · 거리 숫자를 적지 않는다**(Core 시험이 본다). 규칙을 바꾸면 이 문구도 같이 고친다. 그리는 곳은 `MiniGameSection.Guidance.cs`의 `Briefing`.
- 아래쪽 진영 방향 띠(R653)는 그대로다. **사용자 확인 전**(문구 10종이 실제 규칙과 맞는지 포함).

## R653 · 미니게임 진영과 킹 잡기 (2026-11-14)

미니게임에 들어가기 8 m 전부터 자기 팀과 갈 곳을 스태미나 바 위에 표시한다. 입구에서 방을 바라볼 때 백팀은 왼쪽(청록), 흑팀은 오른쪽(주황)이다. 각 진영 간판과 출구에도 팀 이름을 쓴다. 상대편 진영으로 들어가면 자기 진영의 위치를 다시 보여준다. A 의장 광장은 두 팀이 중앙 판을 같이 쓰므로 백팀은 밝은 칸, 흑팀은 어두운 칸을 칠하라고 안내한다. 우리 팀 문이 열리면 해당 출구 방향을 표시한다. 경기에서는 이 PC의 자리, 프리팹 시험에서는 F5로 고른 팀을 따른다.

H 국왕 체스 홀에서 킹이 굳으면 머리 위 금빛 고리가 맥동하고, 자기 진영 화면에 다이빙·질주 안내가 뜬다. 포획하면 잡은 수를 표시하고 킹이 쟁반까지 팀색과 금빛 꼬리를 남긴다. 쟁반에 닿으면 짧은 빛과 착지음이 나온다. 체크음은 체크 횟수에 따라 높아지고 마지막 킹에는 별도 마무리음이 난다. 짧은 시간에 여러 킹이 함께 움직이면 체크·깡충 착지음을 각각 제한한다. 재접속 시 이미 잡힌 킹은 쟁반에서 시작한다.

### H 효과음 교체

아래 이름으로 만든 음원을 `Assets/Resources/PawnRushMiniGameSfx/`에 저장한다. WAV · 모노 · 드라이한 단발로 준비한다. 전용 파일이 들어오기 전에는 프로젝트의 체스판·매칭 효과음을 재사용한다. 방장과 참가자 모두 기존 킹 상태 변화에서 소리를 한 번 재생한다.

| 사건 | 파일 이름 | ElevenLabs 프롬프트 |
|---|---|---|
| 체크 | `sfx_check.wav` | Single crisp ivory chess clock tick with a tiny pitched metal accent, dry and close, 0.18 seconds. |
| 포획 가능 | `sfx_mate.wav` | Two short muted brass mallet notes, rising into a firm stop as a marble chess king locks in place, 0.45 seconds. |
| 킹 한 개 포획 | `sfx_king_capture.wav` | Weighty polished chess piece lifted with a wooden snap and one bright brass accent, short dry tail, 0.4 seconds. |
| 마지막 킹 포획 | `sfx_king_sweep.wav` | Three-note miniature royal cadence on warm brass and wooden mallets, decisive and playful, 0.75 seconds. |
| 상대 팀이 킹 구출 | `sfx_rescue.wav` | Small stone shell loosening in two dry clicks, descending pitch, 0.35 seconds. |
| 스테일메이트 이동 | `sfx_stalemate.wav` | Soft silk flick with a quick airy disappear-and-return gesture, 0.32 seconds. |
| 한 칸 이동 후 착지 | `sfx_king_hop.wav` | One tiny boxwood king touching a wooden chessboard, muted tap, 0.10 seconds. |
| 쟁반 착지 | `sfx_ep_tray.wav` | Single light ivory piece landing in a shallow wooden tray, short close clack, 0.16 seconds. |

`sfx_ep_tray`는 F 대무도회장 쟁반에서도 사용하는 소리다. 팀 안내는 `MiniGameSection.Guidance.cs`와 `MiniGamePalaceArt.GoalSign`, 킹 연출은 `MgCheckmate`와 `CheckmateFx`에서 조정한다. 시험 씬에서 3은 A, 9는 H, F5는 팀 전환이다. 사용자 Unity·Steam 확인 전.

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

Unity 메뉴 **ChessFight → Pawn Rush → Build → All Mini-game Prefabs**는 코드에서10개를 다시 만들어 저장하고, 구간 목록의 미니게임 슬롯 2 · 4에 3 · 7개를 등록한다(R691: 초반 3종 / 중반 4종 + 후반 3종 — `MiniGamePrefabMenu.Pool`. R688 전에는 슬롯 2 · 4 · 6에 3 · 4 · 3개). 기존 장애물 슬롯과 사용자가 추가한 다른 후보는 보존한다. 재생성은 새10개 파일의 코드 생성 영역을 덮어쓰므로 해당 영역의 변경은 빌더에 반영한다.

**ChessFight → Pawn Rush → Open Mini-game Prefab Test**로 `Assets/Scenes/PawnRush_MiniGames.unity`를 열고 Play한다. 원래 구간 시험 씬의 캐릭터·카메라·물리 설정을 복제한 별도 오프라인 장면이다. 기존 시험 씬과 Build Settings는 바꾸지 않는다.

| 키 | 기능 |
|---|---|
| 1 / 2 / 3 (1단계) | C 도개교 / D 쇠살문 / A 의장 광장 |
| 4 / 5 / 6 / 7 (2단계) | B 동관 / E 서고 / F 무도회장 / J 세탁장 |
| 8 / 9 / 0 (3단계) | G 행차 회랑 / H 체스 홀 / I 침실 |
| F5 | 백팀·흑팀 전환 후 입구에서 시작 |
| F6 | 자기 팀의 완료 이벤트·문 연결 시험. 실제 장치 수행을 대신한 개발용 지름길. (R525) 구간 시험 · 구간 라운드 경기에서도 개발 빌드면 F6 = 우리 팀의 다음 미니게임 완료(경기는 방장이 판정) |
| F8 | 현재 프리팹을 새로 불러와 초기화. (R543) 그때마다 **새 씨앗** — G 행차 회랑의 길, I 침실의 배치가 바뀐다 |
| F9 | (R543) **인원 규모** 3 → 4 → 5 → 6 대 6. 그 규모로 방을 새로 짓는다. 처음은 5 대 5. 왼쪽 위에 지금 규모 · 씨앗 표시 |
| R / Backspace | 기존 캐릭터 시험의 체크포인트 / 처음으로 |
| F1 | (R529) 캐릭터 비행 — 개발 빌드 · 에디터 치트: 보는 쪽으로 W(위를 보면 올라감) · Shift 빠르게 · 다시 F1이면 떨어짐. 자유 카메라는 **F4** |

개별 프리팹을 열어 정지 상태의 건축·장치를 볼 수 있다. 실내 천장이 전체 배치를 가리면 `ENV_Ceiling_InspectionToggle` 그룹을 편집 중 임시로 끈다.

## 2. 연결과 플레이 소유권

- **(R543) 미니게임 규모는 팀 인원(3 ~ 6)에 따라 달라진다.** 구간의 팀 인원(`PawnRushSection.TeamSize` — 판이 정하고, 온라인 경기는 시작 인원 `SteamSession.StartTeamSize`)을 스테이션이 읽어(`MissionStation.TeamSize`) 게임을 그 규모로 짓는다. 인원이 바뀌면 `MiniGameSection`이 게임을 다시 설치한다. 표와 규칙은 [미니게임 규칙 14절](PAWN_RUSH_MINIGAMES_v1.md#14-인원수별-규모-r543-2026-11-11), 숫자는 Core `MiniGameScale`. 저장된 프리팹의 Game socket 안은 예전(5 대 5) 모양이 구워져 있지만 Play 때 다시 설치되므로 다시 굽지 않아도 된다.
- **(R543) 개수가 달라지는 것에 붙는 모델은 '하나짜리'여야 한다.** E 종탑은 탑 6개를 한 덩어리로 구운 `E_StationEnv.fbx` 대신 **탑 1개** `E_Tower_9.fbx`를 게임의 탑마다 건다. D 캡스턴 막대도 4개짜리 `D_Capstan_Bars.fbx` 대신 **막대 1개** `D_Capstan_Bar.fbx`를 게임의 막대마다 건다(없으면 4개일 때만 예전 모델). 나머지 방은 이름으로 찾아 붙여서("Statue n" · "King n" · "Road xx" · "Plank (laid)" …) 개수가 달라져도 따라온다. J 통(2.2 m)은 칸 너비에 맞춰 가로로 늘거나 준다.
- 뿌리는 `PawnRushSection` + `MiniGameSection`이다. 입구(0,0,0), 출구(0,0,36), 폭44m, 양팀 스테이션 중심 x±12·z4를 유지한다. 앞 구간의 출구 위치·회전에 따라 배치된다.
- `MiniGameSectionBuilder`가 바닥·벽 충돌·두 팀 문·스테이션·체크포인트·낙하 복귀·진행 경로를 만든다. `MissionStation.Install`이 기존 `Mg*` 규칙을 설치한다. 게임 완료는 자기 문만 연다. `TeamBarrier`는 다른 팀의 출구 통과를 막는다.
- **문 열림 연출(IGateFx)은 10종 모두 방마다 따로다(R643, 11-14).** A `PaintGateFx` · B `MiniGamePlankFx` · C `MiniGameDrawbridgeFx` · D `MiniGameCourtGateFx` · E `MiniGameShelfDoorFx` · J `MiniGameLaundryFx`에 이어, 공용(`ArenaDressing`: 팀색 빛기둥 · 조각 90개 · 바닥 고리, 사용자 "파란 폭죽")을 쓰던 F · G · H · I도 `MiniGameGateMoment` 하위 연출을 쓴다 — G `MiniGameProcessionGateFx`(문 양옆 상아 · 흑단 깃발 휘장), H `MiniGameChessHallGateFx`(문 앞 4 × 4 칸이 광장 쪽부터 문 쪽으로 뒤집혀 체스판 · 황동 테두리), F · I `MiniGameDoorGateFx`(그림 없음, 방의 문이 올라가는 것만 — 승규 님 11-14 확인 뒤 "톱니 바퀴 하지 말고 그냥 문이 여는걸로", "커튼 여는 거 말고 있는 문 여는 걸로"). G 게임(`MgKingEscort`)이 썰매가 틀에 닿을 때 내던 팀색 고리 · 조각도 지웠다("8번 아직 파란색 폭죽", 착지음 `sfx_castle_land`는 남김). 상아 · 흑단 · 황동, 먼지 없음, 새 소리 없음(`sfx_gate_open`은 그대로 부름), 3.2 ~ 3.4초 뒤 사라지는 그림(충돌체 없음). 붙이는 곳 `MiniGameModels.Apply` → `GateMoment`. 위치는 팀 문 상자(스테이션 x −2 ~ 2, 높이 4.6 m, z 23) 앞 바닥(레이로 찾음) 기준. 사용자 확인 전.
- A는 공유18×18m판 하나·팀별32칸이다(5 대 5; R543: 6 × 6 ~ 10 × 8). 공유판 캐시는 같은 프리팹 인스턴스 안에서만 연결된다. C는 전체 폭 해자와 중앙 난간, 측면 권양기로4m 다리 접근을 유지한다. D는 기존8바퀴 규칙이었다(R543: 막대 = 인원수 · 3.5 ~ 7바퀴 · 회전 70%). B는 R528부터 판자를 잡기로 들어 나른다(상대 판자도, 벗어나면 원래 더미로, 팀별 나무색 · 띠, 놓거나 0.5초 들고 있으면 자동 설치 — [미니게임 규칙 B 판자](PAWN_RUSH_MINIGAMES_v1.md)).
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
| `PawnRushEditor/MiniGamePrefabValidation.cs` | 저장된 자산 참조, 실제 로드·문·초기화·대표 렌더 검사. (R543) `-miniGameTeamSize 3~6`으로 그 규모에서 검사(기본 5) |
| `Core/MiniGameScale.cs` | (R543) 인원수별 규모 표 한 곳: `MiniGameScale` · `BoardGrid`(A 판) · `BellTowers`(E 탑 자리 · 간격). 게임별 배치는 Core `PlankLayout` · `EnPassantField` · `KnightRoad` · `KingRoad` · `CheckBlockGenerator.BudgetFor` |
| `PawnRushEditor/MiniGameScaleCapture.cs` | (R543) 10방을 3 ~ 6 대 6 규모로 차례로 지어 위에서 본 그림 · 목록을 `Logs/MiniGameScale/`에 남김(메뉴 `Development → Mini-game Scale Pictures (3 to 6 a side)`, 배치 `RunBatch`) |
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

## 게임 흐름 규칙 (R703, 2026-11-18 — 1단계)

사용자 "게임 흐름이 의도되지 않아서 5개의 방법을 방장 권한으로 켜고 끌 수 있게 만들어 적용시켜보고 제일 나은 것으로 채택하려고 해"(11-17), R699 제안에 대한 답 "쉬워짐의 뜻은 장애물이 꺼진다기 보단 쉬운 길이 열리는거야. 예를들어 정원에 배를 건너는 파트에선 도개교같은 다리가 내려와 길이 생기고, 대계단의 오징어게임 다리의 경우 길이 이어지던가 모두 정답 발판이 되는 식이야. 그런데 장애물이 꺼지는 것도 괜찮은 것 같아" · "2~5번까지 추천안 그대로 진행해줘". 제안 그림: [R699_FlowRules.svg](Mockups/R699_FlowRules.svg), 결정 P57.

**① 규칙 틀.** `PawnRushRules`(Core): 설정 셋 — 낙오자 구제(끔 / 한 팀이 깨면 / 두 팀이 깨면) · 미니게임을 깬 뒤(바로 통과 / 모여서 발판 / 상대 문 20초 뒤) · 엔딩(왕관 발판 / 왕좌 차지전) — 을 숫자 하나로 묶는다. 방 생성 방의 방 데이터 `rules`에 실리고(`SteamSession.RoomRules` · `CanSetRules` · `SetRules`: 방장만, 시작 전만; 방장이 물리 담당이 아니면 `manage` 요청 `rules`), 방 화면 오른쪽 아래 **'게임 규칙' 패널**에서 방장이 누른다(`LoadingScreenView.Room.cs`의 `BuildRules` · `SyncRules`, `NetworkHud.uss` 맨 끝 R703; 방장이 아니면 보기만). 경기가 시작할 때 `SteamPawnRushLink.Attach`가 방의 값을 `PawnRushRules.Current`에 넣는다. **'모여서 발판'(3번안)과 '왕좌 차지전'(5번안)은 아직 준비 중이라 흐리게 보이고 누를 수 없다**(`PawnRushRules.Ready`). 랜덤 매칭으로 잡힌 판에는 방 화면이 없어 규칙이 0(지금까지의 흐름 그대로)이다.

**② 4번안 · 상대 문 시차 개방.** `MiniGameSection.Rules.cs`: 한 팀이 미니게임을 깨고 `PawnRushRules.GateDelay`(20초)가 지나면 판정하는 PC(오프라인 · 물리 담당)가 상대 팀 게임을 `MiniGameBase.CompleteLate()`로 끝내 문을 연다(`Late` = 스스로 깬 것이 아님 — 3단계의 시간 차감에서 세지 않을 표시). 기다리는 팀 화면에는 "상대 팀이 먼저 통과했습니다 · N초 뒤 우리 문도 열립니다".

**③ 1 · 2번안 · 낙오자 구제.** 한 팀이(또는 두 팀 다 — 늦게 열린 문도 셈) 깨면 그 미니게임 **바로 앞 장애물 구간**에 `SectionRelief.Apply`: **쉬운 길** — 정원 배 건너기에 다리가 내려오고 배는 사라진다(`ReliefBridge`, 폭 4m · 난간), 대계단 유리 발판이 모두 정답이 되고 금색으로 바뀐다(`GuessBridge.Relieve`). **꺼지는 것** — 정원 회전봉 · 간헐천 · 수문과 물살 · 로봇 팔, 대계단 굴러오는 석상 · 샹들리에 · 떨어지는 말 · 진자 · 바람 · 기사 돌격, 마차뜰 돌아다니는 마차 · 내리찍는 상자. 폰이 타거나 딛는 것(뜬 발판 · 점프대 · 승강대 · 물레방아 · 컨베이어)은 그대로. 화면에 "○○ · 쉬운 길이 열리고 장애물이 꺼졌습니다" 6초. PC마다 클리어 표시를 보고 스스로 적용하므로 더 보내는 것은 없다. **마차뜰에는 쉬운 길이 아직 없다**(승규 맵 — 어디에 낼지 사용자 답 대기), 작전 회의실은 뒤에 미니게임이 없어 구제가 없다.

| 구간 | 쉬운 길 | 꺼지는 것 | 그대로인 것 |
|---|---|---|---|
| 왕실 수로 정원 | 배 건너기에 다리(`ReliefBridge`). R729: 다리 높이는 배가 떠 있을 때의 높이(`GardenBoat.Afloat`) — 전에는 그 순간 0번 배 몸체에서 읽어, 배가 레인 끝 터널 밑으로 가라앉아 있으면(최대 9.8 m, 약 15%) 다리가 물속에 놓였다(11-20 빌드 시험 1판: 구제 뒤에도 아무도 못 건넘, [빌드 시험 §10](../Network/BUILD_TEST_2026-11-21.md)) | 회전봉 5 · 간헐천 8 · 수문 5와 물살 · 로봇 팔 2 | 까딱 발판 |
| 대계단 현관홀 | 유리 발판 14칸 모두 정답 + 금색 | 굴러오는 석상 · 샹들리에 · 떨어지는 말 · 진자 · 바람 · 기사 돌격 | 뜬 발판 · 점프대 |
| 왕실 마차뜰 | 없음(사용자 답 대기) | 돌아다니는 마차 · 내리찍는 상자 | 컨베이어 · 크레인 · 승강대 · 물레방아 · 문 |
| 국왕 작전실 | 뒤에 미니게임이 없어 구제 없음 | — | — |

코드: `Assets/Scripts/Core/PawnRushRules.cs`, `Assets/Scripts/PawnRush/MiniGameSection.Rules.cs`, `Assets/Scripts/PawnRush/SectionRelief.cs`, `GuessBridge.Relieve`, `MiniGameBase.CompleteLate`. 새 장애물을 만들면 `SectionRelief.Apply`의 목록에 넣을지 정한다(때리는 것 = 끔, 타거나 딛는 것 = 둠).

프로젝트 복사본의 Unity(배치 실행, 그래픽 켬)에서 확인: ① 라운드를 지어(왕실 수로 정원 → 정문 쇠살문 누각 → 대계단 현관홀 …) 규칙 '한 팀이 깨면' + '상대 문 20초 뒤'로 백팀의 첫 미니게임을 끝내니 정원에 구제가 들어가고(쉬운 길 1곳 · 꺼진 장애물 19개 — 다리가 서고 배 · 로봇 팔 · 간헐천 · 회전봉이 사라짐) 흑팀 문은 닫혀 있다가 20초 뒤 스스로 열렸다(`Late`). 둘째 미니게임을 끝내니 대계단에 구제(쉬운 길 1곳 · 꺼진 장애물 10개 — 유리 발판 14칸이 모두 버티고 금색). ② 왕실 마차뜰로 시작하는 라운드에서 '두 팀이 깨면'은 한 팀만 깼을 때 구제가 없고 두 팀 뒤에 들어갔다(꺼진 장애물 2개). 검사 17개 통과 · 오류 로그 없음, 그림과 결과는 `Docs/PawnRush/Checks/R703_FlowRules/`. ③ 방 화면을 방장 · 일반 참가자 눈으로 그려(`room_rules_manager.png` · `room_rules_player.png`) '게임 규칙' 패널이 관전석 줄 · 진행선과 겹치지 않고 글자가 칸 안에 드는 것을 확인(첫 그림에서 '(준비 중)' 글자가 넘치고 패널이 관전석 줄에 닿아 고침). 자동: Unity 밖 전체 컴파일 PASS, Core 201 · 모의 세션 114(각 +1: 규칙 묶기, 방장만 규칙을 정함). **사용자 Unity · Steam 확인 전**(두 PC 온라인으로는 돌려 보지 못했다).

**남은 단계:** 없음 — 2단계 '모여서 발판'은 R704, 3단계 '왕좌 차지전'은 R707에서 만듦(아래). 처음 적어 둔 3단계의 뜻 = '왕좌 차지전'(작전 회의실 문이 열리면 모두 엔딩 층으로 옮겨지고, 팀 과반이 단 위에 있는 시간을 쌓아 30초 — 스스로 깬 미니게임 하나에 5초 차감, 두 팀이 동시에 과반이면 멈춤).

## 게임 흐름 규칙 2단계 — 모여서 발판 (R704, 2026-11-18)

사용자 "2단계 진행"(R703 보고 뒤; 3번안 원문 "미니게임을 클리어해도 특정인원 이상 같은팀이 모여서 발판을 밟아야 미니게임 이후의 길이 열림", R699 제안의 추천값 그대로 = 팀 과반). 결정 P58.

**3번안 '모여서 발판'을 고를 수 있다**(방 화면 '게임 규칙' → 미니게임을 깬 뒤 → 모여서 발판; `PawnRushRules.Ready`). 이 규칙에서는 미니게임을 깨도 팀 문이 열리지 않는다. 문 앞 통로에 **금색 발판**(`GatherPad`, 폭 3.7 × 깊이 2.6 m — 문 바로 앞 통로 바닥)이 있고, **우리 팀 플레이어의 과반이 함께 올라서는 순간** 문이 열린다. 인원은 승리 조건과 같은 셈법(`PawnRushFinish.Needed`, `CrownFinish`의 지금 뛰는 플레이어 수: 6명이면 4 · 5명 3 · 4명 3 · 3명 2 · 2명 2 · 1명 1 — 나가면 줄고 돌아오면 는다), 플레이어가 한 명도 없는 팀은 깨는 즉시 열린다. 상대 팀 폰은 세지 않는다. **보이는 것**: 발판은 깨기 전에는 어둡고, 깨면 금색으로 켜져 고리가 퍼지며, 앞 가장자리에 필요한 인원만큼 칸이 있어 올라선 만큼 켜지고, 위에 '3 / 4'가 뜬다. 문이 열리면 2.5초 뒤 사라진다. 화면 아래에는 우리 팀 **모두에게**(아직 앞 구간에 있는 사람에게도) "미니게임 클리어 · 문 앞 금색 발판에 모이세요 3 / 4명" / "우리 팀이 미니게임을 깼습니다 · 문 앞 금색 발판으로 오세요 3 / 4명". **판정과 전달**: 그림과 숫자는 PC마다 스스로 세고, 문은 미니게임을 판정하는 PC(오프라인 · 물리 담당)만 연다(`MiniGameSection.Rules.cs` `Gather` → `MissionStation.OpenGateNow`). 다른 PC는 코스 상태의 스테이션 플래그 둘째 비트(`PawnRushNetState.GateOpen`, 새로 실음)로 듣는다. 문 여는 연출 · 소리(`IGateFx.Opened`)는 이제 '깬 순간'이 아니라 '문이 열린 순간'에 난다(`ArenaDressing`; 다른 규칙에서는 같은 프레임이라 전과 같다). 구제(1 · 2번안)는 그대로 '깬 순간'에 들어가므로 같이 켜면 뒤처진 사람이 쉬운 길로 와서 발판에 선다. 규칙은 판이 끝나면 지워진다(`SteamPawnRushLink.OnDestroy`, Play 시작 때도 초기화). 프로토콜 **v56**.

- **발판의 자리**: 스테이션 좌표로 문(x −2..2, z 23..23.4) 앞 통로 z 20.3..22.9, 폭 3.7 m. 문의 실제 위치에서 재므로 문을 아치 안으로 옮긴 방(A · 의장 광장)도 따라간다. 바닥 높이는 그 자리에서 아래로 쏜 선이 처음 만나는 고정 바닥. 서 있는 것으로 치는 범위는 발판에서 0.3 m 밖까지, 바닥 아래 0.6 m ~ 위 3.2 m(누워 있어도 셈).
- **코드**: `Assets/Scripts/PawnRush/GatherPad.cs`(그림 · 세기), `MiniGameSection.Rules.cs` `Gather`(판정), `MissionStation.OpenGate`(이 규칙에서는 깨도 열지 않음) · `OpenGateNow`, `ArenaDressing`(문 연출을 문이 열릴 때), `PawnRushNetState.GateOpen` · `SteamPawnRushLink`(전달).
- **다른 규칙과**: '미니게임을 깬 뒤'의 세 선택은 하나만 고른다(바로 통과 / 모여서 발판 / 상대 문 20초 뒤). 낙오자 구제와 엔딩은 따로 고른다.

프로젝트 복사본의 Unity(배치 실행, 그래픽 켬)에서 확인: 미니게임 10종 방을 하나씩 지어(여섯 대 여섯, 가짜 플레이어 12명을 승리 판정 명단에 올림) 규칙 '모여서 발판'으로 ① 문마다 발판이 하나씩 문 바로 앞 바닥에 놓이고(9개 방은 통로 바닥, 문을 아치 안으로 당긴 A · 의장 광장은 그 문 앞 단 위) ② 깨기 전에는 네 명이 올라서도 열리지 않고 ③ 백팀이 깨면 문은 닫힌 채 발판이 기다리며 ④ 백 3명 + 흑 4명이 올라서도 닫혀 있고 ⑤ 백 4명째가 올라서는 순간 열리며 ⑥ 미리 모여 있던 흑팀은 깨는 순간 열리고 ⑦ 새 판(리셋)에서 문과 발판이 처음으로 돌아가는 것을 확인. 이어서 4명 팀은 3명, 플레이어 없는 팀은 깨는 즉시, 판정하지 않는 PC는 모두 올라서도 스스로 열지 않고 물리 담당의 문 열림 표시로 열리며, 규칙을 끄면 예전처럼 깨는 순간 열리고 발판이 치워지는 것까지 — 검사 138개 통과 · 오류 로그 없음. 결과와 그림(방 4곳의 '3 / 4' 대기 모습, 방 화면)은 `Docs/PawnRush/Checks/R704_GatherPad/`. 첫 그림에서 켜진 발판이 허옇게 떠 보여 금색을 짙게 고쳤다. **화면 아래 안내 줄(OnGUI)과 문이 열릴 때의 연출 · 소리는 그림으로 확인하지 못했다.** 자동: Unity 밖 전체 컴파일 PASS, Core 202(+1: 문 열림 비트가 '깸'과 따로 오감 · 과반 셈) · 모의 세션 114. **사용자 Unity · Steam 확인 전**(두 PC 온라인으로는 돌려 보지 못했다).

## 게임 흐름 규칙 3단계 — 왕좌 차지전 (R707, 2026-11-18)

사용자 "3단계 진행"(R704 보고 뒤; 5번안 원문 "현 엔딩 구간의 마지막 문을 열면 모두가 마지막 왕좌 차지 게임으로 텔레포트되고 과반수 이상이 발판 위에 일정 시간 올라와 있어야 승리 … 미니게임 클리어 시 엔딩구간에 올라가있어야하는 시간 차감 (ex. 원래 총 30초인데 미니게임 클리어 팀은 5초씩 차감)", R699 제안의 추천값 그대로). 결정 P60.

**5번안 '왕좌 차지전'을 고를 수 있다**(방 화면 '게임 규칙' → 엔딩 → 왕좌 차지전; 이제 준비 중인 칸이 없다). 이 규칙에서는 '과반이 한 번에 올라서는 순간 승리'가 아니다. **시작**: 엔딩 바로 앞 구간이 국왕 작전실이면 그 큰 문이 다 열리는 순간(문 위 막대가 다 참) 또는 덜 열린 틈으로 누군가 엔딩 구간에 먼저 들어서는 순간, 왕의 서재면(R706부터 후반 구간 후보 — 큰 문이 없다) 누군가 서재 출구를 지나 엔딩 구간에 들어서는 순간, **플레이어와 봇 모두가 왕관 발판 옆으로 순간이동**한다 — 들어오는 방향 기준 백팀 왼쪽 · 흑팀 오른쪽, 발판 가장자리에서 2.2 m 떨어진 같은 거리에 한 줄로, 발판을 보고 선다. 그때부터 떨어지면 엔딩 구간의 체크포인트에서 살아난다(`Checkpoint.Report`). **시간**: 팀 플레이어의 과반(승리 조건과 같은 셈법)이 금색 발판 위에 있는 동안 그 팀의 시간이 쌓이고, 두 팀이 동시에 과반이면 둘 다 멈추며, 내려와도 쌓인 시간은 줄지 않는다. 먼저 자기 목표를 채운 팀이 이긴다. **목표**: 30초에서 그 팀이 **스스로 깬** 미니게임 하나에 5초씩 줄어든다(둘 다 깼으면 20초; 4번안의 시차로 열린 문은 깬 것이 아님 — `MiniGameBase.Late`). **화면**: 시작할 때 5초 동안 "왕좌 차지전!"과 두 팀의 목표 시간, 위쪽(순위 띠 아래)에 두 팀의 시간 막대("백팀 14.2 / 20초 · 발판 3/4", 백은 왼쪽에서 · 흑은 오른쪽에서 차오름)와 가운데 상태(백팀 점유 중 / 흑팀 점유 중 / 동시 점유 · 멈춤 / 발판이 비었습니다), 점유한 팀 색의 고리가 발판에서 퍼진다. **판정과 전달**: 판정하는 PC(오프라인 · 물리 담당)가 시작하고 폰을 옮기고 시간을 세며(`ThroneContest`, 셈은 Core `PawnRushThrone`), 다른 PC는 코스 상태의 구간 파트 7바이트(시작 여부와 점유 팀 · 두 팀의 시간 · 두 팀의 목표)로 듣고 같은 막대를 그린다. 승리는 `CrownFinish.Win`으로 예전 결과 흐름(배너 → 결과 장면)을 그대로 탄다. 결과판의 '발판에 선 시각'은 차지전이 시작된 뒤부터 센다. 자리는 지금의 **임시 엔딩 바닥**(12 × 20 m, 금색 원판 반지름 2.4 m) 그대로다. 프로토콜 **v59**.

- **시작 신호**: 엔딩 구간(골 `FinishZone`이 있는 구간)의 바로 앞 슬롯 구간에 있는 `PushGate`가 `IsOpen`(진행 1)이 되거나, 등록된 폰의 엉덩이가 엔딩 구간의 입구 선을 0.5 m 넘어섬. 앞 구간에 그런 문이 없으면 뒤의 것만으로 시작한다 — R706부터 후반 장애물 구간은 국왕 작전실(큰 문 있음) 또는 왕의 서재 · 거인 체스(큰 문 없음) 가운데 하나다.
- **서는 자리**: 발판 가운데에서 구간의 좌우로 (반지름 + 2.2) m, 진행 방향으로 1.2 m 간격 한 줄(여섯이면 ±3 m). 임시 바닥에서는 x ±4.6, z 10 ~ 16 — 바닥(±6, 0 ~ 20) 안이다. 그 자리 아래에 바닥이 없으면 엔딩의 체크포인트로 보낸다. **진짜 알현실을 만들면 이 자리가 맞는지 다시 볼 것.**
- **코드**: `Assets/Scripts/PawnRush/ThroneContest.cs`(시작 · 순간이동 · 판정 · 화면 · 온라인 파트), `Assets/Scripts/Core/PawnRushRules.cs`의 `PawnRushThrone`(목표 · 점유 · 한 걸음), `CrownFinish.Contest` · `Win`, `PawnRushRound.BindFinish` → `ThroneContest.Attach`, `Checkpoint.Report`, `SteamPawnRushLink.OnThroneBegan`(카메라 컷).
- **다른 규칙과**: 엔딩의 두 선택(왕관 발판 / 왕좌 차지전) 가운데 하나. 구제 · '미니게임을 깬 뒤'와 따로 고른다. '모여서 발판'에서는 깬 순간이 '깬 것'이고(문이 열렸는지와 무관), '상대 문 20초 뒤'로 열린 문은 깬 것이 아니다.

프로젝트 복사본의 Unity(배치 실행, 그래픽 켬)에서 확인(승규의 R706 — 왕의 서재가 후반 구간 후보 — 을 합친 뒤 다시): 규칙 '왕좌 차지전'으로 라운드를 지어 가짜 플레이어 12명(여섯 대 여섯)을 승리 판정 명단에 올리고, **국왕 작전실이 뽑힌 판**에서 ① 차지전이 엔딩 구간에 붙어 작전실의 큰 문(Exit gate)을 기다리고, 판의 온라인 파트가 25 / 96바이트(차지전 7 포함)이며 ② 모두가 출발점에 있는 동안에는 시작하지 않고 ③ 큰 문이 열리자 시작해 12명이 발판 가운데에서 4.6 m 좌우(백 왼쪽 · 흑 오른쪽) 바닥 위에 서고 12명 모두 엔딩 체크포인트를 받았으며 ④ 목표는 스스로 둘을 깬 백 20초 · 시차로 문만 열린 흑 30초 ⑤ 백 4명이 3초 서니 백 3.00초, 두 팀이 같이 과반인 2초 동안은 멈춤, 백이 내리고 흑이 2초 서니 흑 2.00초 · 백은 3.00초 그대로, 백 3명은 과반이 아니라 멈춤 ⑥ 백 4명이 다시 올라 17초 뒤 20초를 채워 승리(흑은 2초 그대로) ⑦ 판정하지 않는 PC는 스스로 시작하지 않다가 물리 담당의 7바이트를 읽고 같은 상태(시작 · 동시 점유 · 백 3/20 · 흑 0/30)가 됨. **왕의 서재 · 거인 체스가 뽑힌 판**에서 ⑧ 기다릴 큰 문이 없고 온라인 파트는 54 / 96바이트, 엔딩 입구 1 m 앞의 폰으로는 시작하지 않고 입구를 넘는 순간 시작(30초씩), 그 폰도 제 팀 자리로 옮겨짐. ⑨ 규칙을 끄면 차지전이 붙지 않고 백 4명이 올라서는 순간 예전처럼 승리 — 검사 27개 통과 · 오류 로그 없음. 결과와 그림(두 팀이 선 모습, 백이 점유한 모습, 방 화면)은 `Docs/PawnRush/Checks/R707_ThroneContest/`. **위쪽의 시간 막대와 시작 안내(OnGUI), 실제 래그돌 폰의 순간이동과 카메라 컷은 그림으로 확인하지 못했다.** 자동: Unity 밖 전체 컴파일 PASS, Core 204(+1: 차지전의 셈 — 목표 · 점유 · 누적 · 승리) · 모의 세션 114. **사용자 Unity · Steam 확인 전**(두 PC 온라인으로는 돌려 보지 못했다).

## 낙오자 구제의 쉬운 길 (R783, 2026-11-23)

R703의 구제는 정원의 저수지 다리와 대계단의 유리 발판만 '쉬운 길'이었고(마차뜰은 없음) 나머지는 장애물만 껐다. R783부터는 구제가 발동되면 그 구간을 **걷기만 해서** 지난다 — 구간별 기획과 좌표는 [낙오자 구제의 쉬운 길](PAWN_RUSH_RELIEF_WAYS.md). 위 R703 절의 표(쉬운 길 · 꺼지는 것)는 그 문서가 대신한다.
