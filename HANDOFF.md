# ChessFight HANDOFF — 모든 작업은 여기서 시작한다

이 파일 전체를 읽고 §6에서 현재 작업에 필요한 상세 문서만 읽는다.
현재 지침과 사용자의 최신 요청이 과거 기록보다 우선한다.
작업이 끝나면 §8에 따라 코드·문서·검사 결과를 함께 마감한다.

최종 갱신: **2026-11-08 · R510 H 국왕 체스 홀 컨셉 시트 3장** (seoungtest 작업 트리, 커밋 안 함). 그 전: R502 도개교 재질 견본 4종. 그 전: R498 미니게임 깜빡임 수정, R497 main 파일·메뉴·구형 맵·폐기 모드·오래된 브랜치 정리.
다음 빈 요청 번호는 **R511**이다(R510 H 국왕 체스 홀 채팅이 사용 중 — 컨셉 시트 3장 [R510_KingsChessHall](Docs/PawnRush/ConceptArt/R510_KingsChessHall/), 사용자 확인 대기, 다음은 재질 견본 → Tripo용 에셋 시트. R509 J 왕궁 세탁장 채팅이 사용 중, R508 F 대무도회장 채팅이 사용 중, R503 B 동관·R504 G 행차 회랑·R505 D 쇠살문·R506 A 의장 광장·R507 E 왕궁 서고는 각 채팅에서 사용). R507: E 왕궁 서고 — 컨셉 시트 3장·에셋 시트 5장(GPT, [R507_RoyalLibrary](Docs/PawnRush/ConceptArt/R507_RoyalLibrary/)), 사용자 Tripo로 책장 탑(층마다 칸 모듈·호두나무 뚜껑)·손종(울리면 금빛, 램프 없음)·회전 책장 문·팀 문틀, 셸을 치우고 벽·대문·입구/출구 아치·천장으로 방 전체 재배치(`e_hall_v1.py`), B 샹들리에·벽등(불빛 코드), Blender로 직접 만든 소품 6종, 바닥 B 재질. 연결 `MiniGameModels.Library.cs`(MiniGameModels를 partial로). 사용자 Unity 확인(종 금빛·문 회전 동작은 미확인). 기록 `ArtSource/PawnRush/MiniGames/README.md` E 절. R506: A 의장 광장 컨셉 시트 3장(GPT, [R506_CeremonialCourt](Docs/PawnRush/ConceptArt/R506_CeremonialCourt/)) + 재질 견본 5종(`Early/Materials/r506_material_swatches.py`, `PR_A_*`·`PR_Early_RoofSlate`) + Tripo용 에셋 사진 3장(`R506_CeremonialCourt/AssetRefs`) + 사용자 Tripo FBX 3개 검사·손질(`Early/Source/R506_A_README.md`) → Unity 설치·배치(`MiniGameModels.ParadeCourt.cs`, Unity 확인 전). R505: D 쇠살문 컨셉 시트 3장(GPT) + 사용자 캡스턴·중앙 성문 FBX를 게임 치수로 손질하고 Unity에 연결(`MiniGameModels.Capstan`, Play 때 붙음, Unity 화면 미확인). R502: R501 모델이 원하는 스타일이 아니어서 [참고 사진 3장](Docs/PawnRush/ConceptArt/R502_DrawbridgeSheets/) 기준으로 다시 만드는 5단계 중 1단계, 재질 견본 4종(`ArtSource/PawnRush/MiniGames/Early/Materials`). R501: C 도개교 Blender 모델(`ArtSource/PawnRush/MiniGames/Early`) + Unity 연결 `MiniGameModels`. R500: 미니게임 시험 화면 안내를 단계별 세 줄(1·2·3 / 4·5·6·7 / 8·9·0)로. R499: 코스·미니게임 3단계 아트 기준 초안([PAWN_RUSH_ART_PHASES](Docs/PawnRush/PAWN_RUSH_ART_PHASES.md)).
사용자는 기존 정리 원칙을 바탕으로 흩어진 파일과 메뉴를 정리하고,
KingRush 옛 이름 변경, 이전 폰 러시 맵의 Prototype 보관, Queen of the Hill 폐기,
한 달 이상 지난 구현 브랜치 삭제를 요청했다.

R496까지의 원본 HANDOFF 전체는 [이전 인계 기록](Docs/Archive/HANDOFF_BEFORE_R497.md)에 보존했다.
당시의 기획·결정·튜닝 근거가 필요할 때만 그 기록을 읽는다.
정리 범위·경로·예외·복구 방법은 [R497 정리 안내](Docs/Architecture/PROJECT_CLEANUP_R497.md)를 따른다.

## 1. 현재 상태 스냅샷

| 항목 | 현재 상태 |
|---|---|
| 작업 위치 | **`C:\Users\dua07\GitHub\ChessFighter`의 `main`에서 직접 작업**. 사용자가 새로 요청하기 전에는 추가 워크트리를 만들거나 사용하지 않는다(T13, R496) |
| Unity | 6000.3.11f1, Windows, URP 17.3, 레거시 Input Manager(Old) |
| 개발 중인 모드 | 폰 러시·소드 파이트 두 개. Queen of the Hill은 R497에 폐기·등록/전용 맵/스킬/시험 자원 제거 |
| 폰 러시 현재 맵 | 구간 프리팹 조합: 정원·대계단 홀·미니게임10종·작전 회의실. 슬롯5 후반 장애물은 아직 없음 |
| 폰 러시 온라인 경기 | 기존 Course01을 Prototype에 보관하되 온라인 호환 씬으로 유지. 새 구간 조합 코스의 온라인 연결은 아직 없음 |
| 구형 맵 | `Assets/Prototype/PawnRush`: Course01 씬, 옛 맵 코드41개·기존 Opening 프리팹24개. GUID·기존 어셈블리 소속 유지 |
| 기타 프로토타입 | 체스복싱은 `Assets/Prototype/ChessBoxing`, 기물 미리보기는 `Assets/Prototype/Previews` |
| 공용 물리·기물 | 래그돌·Hook/Rope·승격·폰 러시/소드 스킬 유지. 옛 Queen 시험대는 PhysicsMechanicsBed, 개척 규칙은 PioneerCourse/PioneerRules로 재분류 |
| 공용 스킬 자원 | `Resources/SharedSkills/Fx` 셰이더5개·`Sfx` 음원3개는 GUID 유지. 실제 사용되는 공유 자원만 남김 |
| Unity 메뉴 | ChessFight 아래 **Pawn Rush / Sword Fight / Prototype / Development** 네 그룹. 상세 개발 도구는 Development로 |
| 문서 | `Docs/PawnRush`가 현재 자료. 옛 맵 문서9개는 `Docs/PawnRush/Prototype`, 폐기 모드 자료는 `Docs/Archive/QueenOfTheHill` |
| 브랜치 | 2026-10-06 22:09:42 KST 이전 마지막 커밋 기준 원격8개·로컬8개 삭제. 원격24→16, 로컬15→7. main·최근 브랜치 보존 |
| 자동 검사 | Core162·모의 세션55·의존성/장애물 시계 정적 검사·전체 대상 외부 컴파일 통과. Unity 저장10프리팹/시험 씬 실패0·실제 Play10종 로드/문/초기화/씨앗 검사 통과 |
| 미니게임 깜빡임 | R498: C 해자 벽·J 물 벽은 그림만 바닥 면에서 1cm(`MissionGames.LinedWall`), A 공유판 밑 금 테두리 잘라냄(`PaintBoard.ClipRims`), G 연석 1cm 밖. Play 재설치로 반영, 프리팹 재굽기 안 함. 사용자 확인 전 |
| 미니게임 모델 | R501: C 도개교를 Blender로 모델링했다(구간 FBX + 다리·드럼 FBX, 1단계 `GC_` 재질). `MiniGameModels`가 Play 때 붙이고 코드 그림은 그리기만 끈다. 크기·판정·시스템은 그대로. Unity 화면 확인 전. **R502: 사용자가 "원하는 스타일이 아니다"** → 참고 사진 3장과 같은 재질·분위기로 다시 만든다. 도개교에는 장식 금을 쓰지 않는다(게임의 금 손잡이·눈금은 그대로) |
| R502 재질 견본 | 크림 돌 벽돌·나무 판자·리벳 금속판·물을 Blender 입체 견본에서 이어 붙는 PBR 맵으로 구움(`Early/Materials/Textures`, Unity 미반영). `swatch_compare.png`로 사진 3과 비교. 이어서 사용자 GLB 2개로 **벽·바닥 = 돌 블록 모양 + 성탑 재질**: 1 m 모듈(6천 삼각형, 2048 맵 4장, `r502_tower_block.py`). 줄눈은 깊이의 40%만 메움. 사용자 확인 대기 **Unity 적용(11-07):** `MiniGameModels.Drawbridge.cs`가 Play 때 v2 모델(사용자 에셋)을 붙이고, `MgDrawbridge`는 권양기를 입구 쪽으로 옮김. Unity 화면 확인 전 |
| 미니게임 구조 | 사용자(11-07, B 동관 세션에서 전달): **입구·출구 크기와 위치만 맞으면 Unity의 건축 구조(천장 등)는 바꿔도 된다.** 목표는 참고 사진과 똑같이. 게임 장치(문·다리·밧줄·손잡이)의 판정은 별도 확인. **R502 도개교: R311 C 그림 배치와 똑같이, 사용자 에셋(성탑·돌 블록·권양기·성문)을 실제 모델로. 권양기는 그림처럼 입구 쪽으로 옮김(게임 코드 변경 허용). 금색은 사용자 에셋의 금색에 맞춘다(이전 "금 안 씀" 대체).** **불빛·흔들리는 천은 코드로**(사용자 11-07, B 동관 세션 전달: "초나 이런 건 못해 … 코드로", "천도 마찬가지로 … 코드로 해야 해"): 등불·촛불은 불 없는 몸체만 모델로, 불꽃·빛나는 유리·실제 Light는 Unity 코드로 붙임. 깃발·커튼처럼 걸려 흔들리는 천도 코드(천 메시·셰이더). Blender 미리보기의 빛나는 유리·깃발은 위치 확인용. **벽은 끊김 없이**(사용자 11-07, C 행차 회랑 세션 전달: "벽은 특히 이어지는 느낌이어야 돼"): 기존 셸 벽은 치우고 모델로 대신해도 되며, 벽 모듈은 살짝 겹쳐 전체 길이를 덮고 위에 남는 높이도 같은 재질로 채운다. **에셋 사진(GPT 시트)을 줄 때는 Tripo 권장 면 수를 함께**(사용자 11-07, C 행차 회랑 세션 전달): 사진마다 "Tripo 권장 면 수: 시트 전체 약 N" 한 줄과 근거(부품 수·게임 반복 수·손질 뒤 목표), 손질 뒤 목표의 3~5배를 받는다. **다른 채팅 에셋 재사용**(사용자 11-08, H 체스 홀 세션: "다른 채팅에서 썼던 애셋들 … 겹치면 코덱스에 신청 안하고 써도 돼"): 새 에셋 시트를 Codex에 요청하기 전에 다른 미니게임이 이미 만든 모델(`ArtSource/PawnRush/MiniGames/*/Export`, `Assets/Resources/PawnRushMiniGames`)·재질(`*/Materials/Textures`)과 겹치는 부품은 그대로 가져다 쓰고, 겹치지 않는 것만 요청한다(8개 미니게임 채팅에 전달함) |
| H 체스 홀(R510) | 1단계 컨셉 시트 3장(전경·건축 모듈·소품/재질, `codex exec`, `Docs/PawnRush/ConceptArt/R510_KingsChessHall`). 게임 숫자: 팀마다 8×8판 18 m, 기둥 c6·f3·f7(1.6×1.6×2.2 m), 0.55 m 울타리 기둥, 상대색 킹 6(파랑 팀 = 흑단, 주황 팀 = 상아), 킹 쟁반 0.7×8 m. 시트의 울타리 굵기·기둥 칸은 모델 단계에서 게임 치수로. 재질: R504 3단계 세트 7종 재사용 + 새 3종(`PR_Late_FrozenStone`·`Ebony`·`Bronze`, `Late/Materials/r510_material_swatches.py`, Unity 미반영). 다른 방 에셋 재사용 목록 `R510_KingsChessHall/REUSE.md`: 새 Codex 요청은 체스 시계·예비 기물 상자·기보 석판·홀 종 4종만 → Tripo용 시트 `R510_KingsChessHall/AssetRefs/H_assets_01_props.png`(권장 약 45,000면) 완료 → 사용자 Tripo FBX 보관·검사(`ArtSource/PawnRush/MiniGames/Late/Source/R510_H_A1_Props.fbx`, 285,180 삼각형, 판정표 `Late/Source/README.md` R510 절: 모두 줄이기·깨진 곳 없음). → Blender 손질(`Late/h_props_v1.py`·`h_board_kit_v1.py`) → 설치(`h_install_unity.py`) → **`Assets/Scripts/PawnRush/MiniGameModels.ChessHall.cs`로 배치**(G 홀 함수 재사용, 킹 `G_King` 흑단/상아 + `MiniGameKingLook` 굳은 돌, 홀 종은 체크·굳음 때 흔들림 `MiniGameHallBell`, 천장은 H만 평평한 대리석 + 사각 천창 우물 8개 `H_SkylightWell`, 팀 문 위 킹 문장 `H_TeamGateCrest`). **사용자 Unity 확인(11-08 "확인"): 방·판·킹·소품·천장.** 굳은 킹·종 울림·문장은 확인 전. 석판 기보·시계 숫자 글자판(`h_decals_v1.py`, 확인 전). H 전용 효과 `CheckmateFx`(확인 전). 남은 후보: H 효과음(체크·메이트·잡기·구출·스테일메이트 wav가 없어 지금 무음) 컴파일 통과, **Unity 확인 전**(VALIDATION 맨 위, Play 9번), 코드 변경 없음 |
| B 동관 아트(R503) | 사용자 Tripo 모델 + Blender 손질(`ArtSource/PawnRush/MiniGames/Mid`, 상세는 그 `Materials/README.md`)로 방 전체를 다시 세움: 가운데 장식 대문, 옆벽 1·2층 아치 창(유리 없이 창밖이 보임), 회랑·난간(한 줄), 처마·호두나무 들보·천장, 팀 문 아치, 입구·출구 아치, 복도 벽, 틈을 메우는 크림 돌 채움 벽, 판자 모델·틈 아래 지하·장선·비계, 바닥 재질 3종. 불빛·천은 코드(`MiniGameFixtures.cs`). `MiniGameModels.RepairWing`이 B 게임에서만 붙이고 R312 셸 그림은 전부 끔. 충돌·규칙·입구/출구 그대로. **사용자 Unity 확인됨(11-07)**, 커밋 없음 |
| F 대무도회장(R508) | 진행 중(11-08): 컨셉 시트 3장·재질 견본(B 4종 재사용 + 새 4종: 무도회장 크림 대리석·쪽마루·자두색 벨벳·회색 거울)·재사용 목록 `Docs/PawnRush/ConceptArt/R508_ClockworkBallroom/REUSE.md`(방 껍데기는 E·B, 계단·줄 기둥 G, 화분 A·D)·에셋 시트 3장. 무용수 2종·태엽 열쇠는 Blender로 직접(`Mid/f_dancers_v1.py`, 인형 느낌, 의상 재질 칸은 Unity에서 상대 팀색), Tripo 8부품 손질(`Mid/f_props_v1.py`). **거울은 Tripo로 만들지 않음**(사용자 "막혀서 거울 같은 느낌이 없잖아" → "방만 비침"): 빈 틀 + `MIRROR_Surface` 판, Unity 거울 재질·반사 프로브. Unity 설치(`Mid/f_install_unity.py`)·연결(`MiniGameModels.Ballroom.cs`: 방은 E 껍데기에서 책장만 뺀 `F_HallEnv`, 무용수 인형·쪽마루 레인·벨벳 벤치·거울 문·벽 거울·스포트라이트·소품, 반사 프로브) 완료. 거울 문은 실시간 반사 카메라 + `PR_MirrorGlass` 셰이더(사용자 "실제로 반사 되게"; 품질 설정의 실시간 프로브가 꺼져 있어 프로브만으로는 하늘이 비쳤음), 벽 거울은 시작 때 찍는 큐브맵 프로브, 잡을 수 있는 무용수 스포트 빛·간판 — 여기까지 사용자 Unity 확인(11-08). 이어서 창가 휘장(코드 천)·무대 테두리 황동(확인 전). 기록 `ArtSource/PawnRush/MiniGames/README.md` F 절. 휘장·테두리도 사용자 Unity 확인(11-08), 커밋 없음 |
| J 세탁장 아트(R509) | 1단계: 게임 좌표 표·위에서 본 배치도(`ArtSource/PawnRush/MiniGames/Mid/J/`), 컨셉 시트 3장(`Docs/PawnRush/ConceptArt/R509_PalaceLaundry`). 전경 그림은 한쪽 통 7개라 배치는 배치도가 기준. 사용자 확인됨(11-08 "확인, 다음 단계"). 2·3단계: 재질 견본 새 3종(`Mid/Materials/r509_material_swatches.py` → `PR_Mid_WhiteTile`·`OakStaves`·`DollLinen`, 나머지는 B 재사용, Unity 미반영)과 Tripo용 에셋 시트 5장(`R509_PalaceLaundry/AssetRefs`, 권장 면 수는 J README 표). 다른 채팅 모델 재사용 지시(11-08)에 따라 벽·대문·팀 문틀은 B/E 모델, 수조 벽·출발대는 Blender 상자로 하고 **Tripo는 3장만**(통·분출구·수문 문짝·바퀴를 합친 `J_tripo_01_tub_sluice.png` / 인형 6종 / 소품). 통·수문 FBX를 받아 `Mid/J/j_props_v1.py`로 손질 → `Mid/Export/J_Tub`(7천, 원점 뚜껑 윗면)·`J_Nozzle`(Tripo가 찢어져 Blender로 새로)·`J_Leaf`(3.85×4.55)·`J_Wheel`(1.2 m). `Mid/J/j_install_unity.py`로 설치하고 `MiniGameModels.Laundry.cs`(Apply에 J 한 줄)가 Play 때 통·분출구를 길 칸·점프대에, 수문 문짝을 팀 문에, 바퀴를 문 바깥 벽에 붙임(충돌·규칙 그대로) → **사용자 Unity 확인(11-08)**. 이어서 방: `Mid/J/j_hall_v1.py`가 E 방 스크립트를 불러(고치지 않음) 벽 책장을 빼고 흰 타일 허리 1.5 m + 호두나무 띠를 더한 `J_HallEnv`, 수조 흰 타일 `J_BasinEnv`(스테이션 좌표, 검은 팀은 뒤집음). Unity는 E의 조명·팀 문틀·깃발·바닥 재질 재사용, 셸 숨김. 컴파일·Core 162·모의 세션 55 통과, **방·수조 사용자 Unity 확인(11-08)**. 비눗물: 사용자 "색은 좋은데 물 처럼 안보여" → C 물 셰이더는 햇빛 의존이라 실내에서 밋밋 → J 전용 실내 물 셰이더 `PR_SoapyWater.shader`(실내 빛 코스틱·샹들리에·창 광택 반짝임·타일 일렁임) + `Mid/J/PR_Mid_J_SoapyWater.mat`(색 유지·더 맑게, 값 살짝 더) → 사용자 Unity 확인(11-08). 빨랫줄: 옆벽 창 칸마다 4.2 m 줄 + 흔들리는 흰 리넨 시트(`LaundryLines`, `MiniGameBannerCloth` 재사용, `PR_Mid_J_Linen`) → 사용자 Unity 확인(11-08). 소품 7종은 사용자 선택으로 Blender 직접(`Mid/J/j_laundry_props_v1.py` → `J_PropsEnv`, E 소품 도구 재사용, 입구 모서리·뒤 복도 끝) → 사용자 Unity 확인(11-08). 인형 6종도 사용자 선택으로 Blender 직접(`Mid/J/j_dolls_v1.py` → `J_Doll_*`, 나이트는 옆을 봄, Unity `LaundryDolls`가 기물에 걸고 상대 기물색 몸 + 상대 팀색 테두리로 칠함) → 사용자 Unity 확인(11-08). **세탁장 아트 전 항목 사용자 확인**, Tripo 대기 없음. J 재질 `.meta` 5개 바로잡음(E 설치 도구 버그, E·B `.meta`도 같은 문제 — 미수정). **인형·소품 FBX 대기**, 비눗물 셰이더·빨랫줄 시트(코드) 남음. 커밋 없음 |
| 사용자 확인 | R497 이후 Unity 화면·입력·온라인·실제 퍼즐 완주·12명 성능은 사용자 확인 전 |

브랜치 삭제 전16개 참조를 bundle·manifest·복구 안내로 보관하고 bundle 검사를 통과했다.
보관 위치: `C:\Users\dua07\Desktop\개발일지\2학기 프로젝트수업\체스파이트\Backups\R497-Branches-20261106-221133`.
약7.9 MB의 Git bundle이며 LFS 바이너리를 별도 포함하지 않는다. 기존 LFS 캐시는 보존했다.
R496의 사용자 프리팹2개 보존본·명명 stash·당시 옛 브랜치 기록도 정리 작업이 임의로 버리지 않는다.

## 2. 무엇이 되고 무엇이 안 되는가

- **현재 편집할 폰 러시**: `ChessFight → Pawn Rush → Open Section Test`.
- **미니게임만 선택**: `ChessFight → Pawn Rush → Open Mini-game Prefab Test`; 숫자1~0, F5 팀, F8 초기화.
- C/D/A는 초반, B/E/F/J는 중반, G/H/I는 후반 후보로 슬롯2/4/6에3/4/3개 등록돼 있다.
- 슬롯1 정원 → 슬롯2 초반 미니게임 → 슬롯3 홀 → 슬롯4 중반 미니게임 → 슬롯6 후반 미니게임 → 슬롯7 작전 회의실로 이어진다.
- 미니게임의 건축·장식은 저장된 프리팹이다. Play에는 Game socket의 기능 게임만 다시 설치한다.
- 자동 완료(F6/ForceComplete)는 이벤트·팀 문 연결 검사다. 실제 장치를 풀어서 달성했다는 뜻이 아니다.
- 정원·홀·작전 회의실의 일부 아트는 Play에 만들어진다. 편집 모드의 빈 루트만 보고 자산 누락으로 판단하지 않는다.
- **Prototype Course01**은 기존 온라인 경기가 호출하는 씬이다. 씬 이름 `PawnRush_Course01`, 로비 키 `kingrush`, 기존 통신 구조는 유지한다.
- `GameModes.KingRush` 이름과 `ModeRetiredQueenHill = 1` 같은 예약 통신 값은 파일 정리 때문에 바꾸지 않는다.
- Queen of the Hill을 새 모드로 다시 등록하지 않는다. Queen 기물 자체는 다른 모드가 쓰므로 유지한다.
- 새 Sections 코스를 기존 Course01 온라인 흐름에 연결했다고 말하지 않는다. 기존 Steam 흐름의 변경도 이번 범위가 아니다.
- R497 실제 Unity 저장10프리팹/시험 씬 실패0, Play10종의 기능 캐시·자기 팀 문·2번 초기화·씨앗 재생성·A 인스턴스 분리·I 외형 유지 자동 검사 통과. [VALIDATION](Docs/Network/VALIDATION.md)에 범위와 한계를 기록했다.
- AI의 컴파일·자동 검사·렌더 관찰과 사용자가 Unity/Steam에서 직접 보고 조작한 확인을 구분한다.

## 3. 진행 중인 일과 다음 할 일

1. 자동 검사와 별개로 새 메뉴·로비 두 모드·현재/Prototype 장면을 Unity GUI에서 살펴보고 [확인 목록](Docs/Network/VALIDATION.md)에 사용자 결과를 기록한다.
2. 사용자는 기존 프로젝트에서 네 메뉴 그룹, 미니게임 선택·팀 전환·초기화, Intro→Lobby→두 모드를 확인한다.
3. 폰 러시 각 퍼즐의 실제 입력·문 통과·복귀 동선과12명 성능을 확인한다.
4. 구형 맵을 고칠 때는 Prototype 문서를 먼저 읽는다. 현재 구간 코스와 구형 온라인 코스를 같은 것으로 취급하지 않는다.
5. 새 구간 코스의 온라인 연결은 별도 설계·요청으로 진행한다. 이번 정리로 프로토콜을 새로 정의하지 않았다.
6. R498 깜빡임 수정을 Play에서 1·3·7·8번으로 확인한다([VALIDATION](Docs/Network/VALIDATION.md) 맨 위). 편집 모드 프리팹까지 바꾸려면 동료 수정 여부를 확인한 뒤 Build All을 다시 굽는다.
7. **C 도개교 다시 만들기(R502, 단계마다 사용자 확인 후 다음으로):** ① 재질 견본 4종 → 성탑 재질 돌 블록 모듈로 확정 ② Unity 1단계 조명·후처리 프로필(따뜻한 햇빛·부드러운 그림자·SSAO·약한 Bloom·밝고 따뜻한 색 보정) + 물 셰이더(청록·코스틱·가장자리 거품), 견본 벽+물+폰으로 확인 → 사용자 확인됨(11-07 "열어 봤어, 괜찮아") ← **지금 ③** ③ 해자변 벽 구간(사진 2의 5·6번) 하나로 모양 확인 ④ 장치 → 건축 → 소품 ⑤ R501 FBX 자리에 교체(`MiniGameModels` 연결·게임 크기 그대로). 그 뒤 D·A로 이어간다. 미니게임 모델링은 [3단계 아트 기준](Docs/PawnRush/PAWN_RUSH_ART_PHASES.md) §5의 결정 5가지를 먼저 정하고 C·E·G부터 만든다. FBX가 나오면 미니게임 FBX 연결 코드가 필요하다. **현재(11-07):** ③~⑤를 사용자 Tripo 에셋으로 한 번에 진행해 C 도개교 v2를 Unity에 붙였다(`c_drawbridge_v2.py` → `c_drawbridge_v2_export.py` → `c_install.py`, `MiniGameModels.Drawbridge.cs`). 권양기 장치는 도르래 방식(앞쪽 권양기 x ±16, 줄이 쇠판 궤도를 따라 해자 앞 기둥의 도르래 2개를 돌아 x ±18.4로 나란히 돌아 나오고, 폰은 그 매듭을 앞쪽으로 7 m 당김, 모두 다리 밖. 쇠사슬은 망루 꼭대기 고리→상판 끝 고리, 망루 쇠판 제거). 팀 통로·뒤 복도에 바닥·벽을 깔고 뒤 지붕 탑을 y 41로 옮겨 통로를 틔움. 망루는 사용자 Unity 확인(11-07). 사용자 Unity 확인 전([VALIDATION](Docs/Network/VALIDATION.md) "C 도개교 v2 Unity 적용").
8. 다음 새 요청은 **R508**부터 기록한다. **A 의장 광장(R506)**: (11-08) 붓 칠 시안 A 채택·소리 다시 넣음(Unity 확인 전, VALIDATION 맨 위).  컨셉 시트 3장(`Docs/PawnRush/ConceptArt/R506_CeremonialCourt`)과 재질 견본 5종(`ArtSource/PawnRush/MiniGames/Early/Materials/r506_material_swatches.py` → `Textures/PR_A_Slate`·`PR_A_IvoryLimestone`·`PR_A_Basalt`·`PR_A_CourtPaving`·`PR_Early_RoofSlate`, 비교 `r506_swatch_compare.png`) 완료, 사용자 확인 전. 광장 포장·지붕 슬레이트는 D 쇠살문의 남은 바닥 포장·지붕에도 쓸 수 있다. 흰 배경 에셋 사진 3장도 완료(`Docs/PawnRush/ConceptArt/R506_CeremonialCourt/AssetRefs`: 정문 약 10만·구조 6종 약 4.5만·소품 8종 약 6만 면 권장, 깃발 천·왕관 문장·불빛·유리 없음, 팀 문짝은 게임 문짝 한 장 3.85×4.55 m에 맞춤). 사용자 Tripo FBX 3개는 `ArtSource/PawnRush/MiniGames/Early/Source/R506_A_*.fbx`에 보관·검사함(부품 15개 판정표 `Source/R506_A_README.md`, 늘릴 것 없음, 깨진 것: 탑 아래 몸통·입구 기둥 받침·버섯 모양 벤치·칠통 흰/검은 색 없음). 사용자 지시("Blender로 고쳐")로 `Early/a_props_v1.py`에서 15개를 손질·수리해 `Early/Export/A_*.fbx`로 내보냄(정문 25,000 삼각형 등, 렌더 `Early/render_a_prop_*.png`). 남은 점: 정문 오른쪽 탑 위 작은 검은 자국, 팀 아치 폭 9.25 m라 정문(±10 m)과 겹침 → 배치 때 기둥 좁히기. 사용자 지시("Unity 배치 진행")로 `Early/a_install.py`가 Assets에 설치하고 `Assets/Scripts/PawnRush/MiniGameModels.ParadeCourt.cs`가 Play 때 배치(정문 z 24·팀 아치 ±12·성벽·탑·입구 기둥·사이프러스·화분·벤치·정원 담·칠 소품, **성벽·난간(낮은 정원 담)은 틈 없이 모듈 1/5씩 겹치고 정문 옆·뒤는 돌 벽돌로 채움**(사용자 11-07 "난간이랑 벽 틈 있게 하지마"), 팀 문짝은 게임 문과 함께, 깃대는 황동 모델 + 흔들리는 천, 바닥 크림 판석), `PaintBoard` 칸은 슬레이트·상아 석회암·현무암(그림만). 컴파일·Core 162·모의 세션 55 통과, **사용자 Unity 확인됨**(11-07 "확인", VALIDATION). 이어서 깃발·문장을 코드로: `Early/a_banner_tex_v1.py` 텍스처 4장(크림 왕관 깃발 2·팀 방패 깃발 2) → 깃대·팀·뒤 통로 깃발, 정문 양옆 긴 왕관 깃발, 흉벽 위 황동 왕관(`ParadeGatehouseDress`); 사용자 "벽 때문에 지붕이 가려져" → 셸 북쪽 바깥 벽 덩어리(11 m)를 끄고 5 m 성벽 양면으로 바꿔 모서리 탑 지붕이 드러나게, 정문 탑 옆 채움 6.3 m로; "떠 보이지 않게" → 정문·깃발·왕관을 단 높이 0.4 m로 올리고 정문 아래·앞에 판석 바닥(`Gatehouse floor`); "sfx랑 vfx도 해줘" → ElevenLabs 효과음 5개(`Early/Sfx/a_sounds.py` → `Assets/Resources/PawnRushMiniGameSfx/sfx_paint_*.wav`·`sfx_rank_complete.wav`, 귀로 확인 전)와 `PaintBoard` 시각 효과(칠하는 중 반짝이·완료 빛기둥·한 줄 금 고리·팀 완성 색종이) → 사용자 "이런 vfx 말고"(원기둥 빛기둥) → A 전용 입자 효과 `PaintBoardFx.cs`로 교체(빛 알갱이·물감 자국·물감 방울·빛 고리·별 반짝이·색종이, 원기둥·네모 조각 없음); "채워지는 소리가 날카로워" → 칠하는 중 소리를 낮고 둥근 소리로 새로(`Sfx/Source/paint_charge_soft_*`, 중심 약 300 Hz) → 사용자가 고른 방향으로 다시: 채우는 소리 = 붓질 스윽(`paint_charge_brush_take3`), 완료 소리 = 도장 쾅(`paint_done_stamp_take3`). 테이크를 바꾸려면 `Early/Sfx/a_sounds.py`. 칠하는 동안 한 획 붓질 8종(`sfx_paint_stroke_1~8`)을 섞은 순서로 이어 재생(`PaintBoard.Stroke/NextStroke`, 연속 문지름 소리는 지움). 25·50·75 % 팀 문 효과는 A 전용 `PaintGateFx.cs`(물감 방울 분수·별 반짝이, 원 없음, 효과음 없음; 사용자 지시, C 세션 전달). **이후 사용자 "사운드는 다 없애고": A 소리 전부 끔·wav 삭제(공용 완료 알림음만 남음), 칠하기는 붓이 칸을 지그재그로 칠해 나가는 그림(`PaintBoard.BrushMaps/StrokeMaterial/BrushTip`), 물감 요철 노멀 맵·젖은 광택, 칸 위를 오가는 붓 모델(`Brush/PlaceBrush`); 칠은 칸별 텍스처에 붓 자국을 매 프레임 찍는 실시간 그리기(`StartPaint/PaintTo/Stamp/PathUV`).** Unity 확인 대기(미니게임 시험 3번). 다시 손질하면 `a_props_v1.py` → `a_install.py` 순서 → `MiniGameModels`로 연결. 체스판 칸 판·칠하는 판은 게임 코드(`PaintBoard`)가 그리므로 판정·크기는 그대로 두고 재질만 바꾼다. D 쇠살문(R505): 캡스턴(`D_Capstan_Static`/`Bars`)·중앙 성문(`D_CenterGate`)은 사용자 확인됨, 팀 아치·쇠살문(`D_TeamArch`/`D_Portcullis`)은 연결했고 Unity 확인 전, 사암 구조 세트는 사용자가 쓰지 않기로 함(11-07, 연결·파일 되돌림). 남은 부품(도르래 들보·벽·포장·입구 기둥·등불 틀·소품)은 GPT 시트로 Tripo용 2장(`Docs/PawnRush/ConceptArt/R505_PortcullisCourt/AssetRefs`: `D_assets_04_structure_bundle.png` = 바퀴 없는 도르래 틀·바퀴·벽·포장·입구 기둥·등불 틀, `D_assets_02_props.png` = 소품; 바퀴는 캡스턴과 함께 돌릴 예정)을 만들었고, 사용자가 준 Tripo FBX 2개는 전체 약 5천 삼각형이라 부품별 판정(바퀴·사슬 더미·투구·화분·등불은 늘리기 = Tripo 다시 내보내기, 포장은 줄이기, 도르래 틀은 고정쇠 빠짐; `ArtSource/PawnRush/MiniGames/Early/Source/README.md`). **원칙: 줄이기는 Blender, 늘리기는 Tripo 재내보내기 → Tripo는 면 수 넉넉하게.** 이번 것은 사용자 지시로 Blender에서 늘리며 맞춤(`Early/d_props_v1.py`, 부품 10개 `Early/Export/D_*.fbx`, 바닥 포장 제외) → `MiniGameModels.Court`·`CapstanChain`으로 배치·연결함(셸 옆벽 숨기고 `D_Wall`×1.3을 빈틈 없이, 도르래 바퀴·사슬은 `MiniGamePulleyChain`으로 캡스턴과 함께 돌며 사슬이 바퀴 위로 감겨 흐름). **사용자가 Play 2번에서 확인함**(11-07, [VALIDATION](Docs/Network/VALIDATION.md)). 등불 불빛(`MiniGameFlame`)·흔들리는 깃발(`MiniGameBannerCloth`)도 코드로 붙임(Unity 확인 전). 벽·아치·성문 구멍은 `ArtSource/PawnRush/MiniGames/Early/mg_holes.py`(속 채움·앞쪽 절반 뒤집기)로 막았고 사용자 확인됨. 바닥 포장은 numpy 크림 판석 텍스처(`Early/Materials/r505_paving.py`)를 `MiniGameModels.Paving`으로 입힘(사용자 확인됨). 캡스턴 몸통 판자 틈은 `d_capstan_v1.py` `core()` 속심으로 막음(사용자 확인됨), 2차로 몸통 22k 삼각형·속심 중간 밝기로(Unity 확인 전). 사이프러스 나무는 Blender로 직접 만듦(`Early/d_cypress_v1.py` → `D_Cypress`, `MiniGameModels.Cypresses`, 스테이션마다 3그루, 셸의 옛 공 모양 나무는 숨김, 사용자 확인됨). 컨셉에서 남은 것: 모서리 성탑 — GPT 시트 `Docs/PawnRush/ConceptArt/R505_PortcullisCourt/AssetRefs/D_assets_05_tower.png`(Tripo 권장 약 50,000면) → 사용자 Tripo FBX를 `Early/d_tower_v1.py`로 13.5 m·14k 삼각형으로 맞춰 `D_CornerTower`로 (±23, 0, 25)에 둠(사용자 확인됨, `Early/Source/README.md`). 이로써 컨셉의 부품은 모두 들어감. 뒤 벽 바깥쪽(|x| 9.7~22) 셸 상자는 `MiniGameModels.NorthWall`이 옛 네모 탑·기둥·판돌을 숨기고 돌 벽돌을 입히며, 벽 위에 `D_Wall` 흉벽(사용자 확인됨). 중앙 성문 문짝 아래 틈은 `d_center_gate_v1.py threshold()` 판석 문턱으로 막음, 팀 문 통로 바닥 양옆 틈은 `MiniGameModels.PassageEdges` 판석 띠로 막음(둘 다 Unity 확인 전). 소리·효과는 `Assets/Scripts/PawnRush/MiniGameCourtFx.cs`(캡스턴 돌리기·사슬 고리 소리, 한 바퀴마다 쿵·먼지, 다 열림 소리·먼지·불꽃; ElevenLabs 테이크 `Early/Sfx/Takes`, 기본 테이크 넣음; 다 열림은 D 팀 문을 4초로 늦추고 사용자가 고른 chain 3을 그동안 반복; 돌리는 소리는 나무 시안 B 4번, 쇠살문 소리는 쿵 4번·열림 2번(4초, 한 번 재생), 사슬은 드럼 B3·도르래 C3, 모두 사용자 선택, Unity 확인 전). **공용:** 25/50/75 % 진행 때 팀 문 바닥의 금빛 원과 진행 알림음(`sfx_mg_quarter`)은 사용자 지시로 모든 미니게임에서 뺐다(`ArenaDressing`, 11-07). 각 미니게임은 원 없는 자기 그림 효과만 둔다. `MiniGameModels.Apply`는 시작할 때 스테이션의 `IGateFx`를 모두 지운다(`ClearGateFx`, 11-08: 게임을 바꿔도 앞 게임 효과가 남던 문제). 각 게임은 자기 `Apply`에서 다시 붙이면 된다. 등불 불빛·깃발은 코드로. 앞의 두 모델 모델을 `Assets/Resources/PawnRushMiniGames/Early/D`에 넣고 `MiniGameModels.Capstan`으로 연결(막대는 `Capstan bars`에 붙어 함께 돎) → **사용자가 Unity Play 2번에서 확인함**(11-07, [VALIDATION](Docs/Network/VALIDATION.md) 맨 위, [Early/Source](ArtSource/PawnRush/MiniGames/Early/Source/README.md)). 다시 손질하면 Blender 스크립트 → `d_install.py` 순서.

다른 채팅의 진행 상태와 과거 제한은 원본 아카이브의 날짜·브랜치를 확인해 해석한다.
현재 사용자 작업 위치(main/기존 경로)와 최신 사용자 지시가 우선한다.

## 4. 절대 규칙 — 어기면 과거에 실제로 사고가 났던 것들

1. **Active Input Handling은 `Input Manager (Old)`(0)로 둔다. Input System 패키지는 설치하지 않는다**(`JY-lobby`, R20: 설치돼 있으면 켤 때마다 백엔드를 켜라는 창이 뜬다). uGUI가 없어서 Both/New로 바꾸면 HUD 클릭과 입력이 전부 죽는다. `InputSettingsGuard`가 자동으로 되돌린다. → [PITFALLS §1](Docs/Environment/PITFALLS.md)
2. **프로젝트는 선택 패키지가 하나도 없어도 컴파일되어야 한다.** Safe Mode에서는 설치 스크립트가 돌지 않는다. 선택 패키지 코드는 전용 asmdef + `versionDefines` + `defineConstraints`로 가둔다. `ENABLE_INPUT_SYSTEM`을 패키지 설치 여부로 쓰지 않는다.
3. **`Packages/manifest.json`·`packages-lock.json`이 바뀌면 Discard하지 말고 커밋한다.**
4. **폴더 하나 = 어셈블리 하나.** `Game`·`Gameplay`는 Steam을 참조하지 않는다. `Core`는 Unity도 참조하지 않는다. → [Architecture/STRUCTURE](Docs/Architecture/STRUCTURE.md)
5. **Steam 초기화·종료 소유자는 `NetworkRuntime` 하나다.** SteamManager를 따로 추가하지 않는다.
6. **HUD에는 기본 테마가 없다.** ScrollView·Dropdown 같은 복합 컨트롤은 보이지 않는다. 평범한 VisualElement만 쓴다.
7. **장애물 위치·회전은 `ObstacleClock` 시간의 순수 함수로만 계산한다(스스로 움직이는 kinematic 물체는 `Obstacle`을 상속, 테스트 스크립트가 검사. 플레이어 상태로 움직이는 것은 `StateDrivenMover:` 주석을 달고 오프라인 전용).** 프레임마다 누적하지 않는다. 온라인 래그돌 참가자는 방장이 스냅샷에 실은 장애물 시각을 쓴다(R51).
8. **클라이언트는 입력만 보낸다.** 좌표를 권위로 받지 않는다. 파티는 같은 팀에 통째로 예약한다.
9. **네트워크 비호환 변경을 하면 `SteamSession.Protocol`을 올린다.** 패킷 구조를 바꾸면 `MotionProtocol` magic도 올린다.
10. **검증 표기는 정직하게 한다.** 사용자가 Unity나 Steam에서 직접 본 것만 VALIDATION에 '성공'으로 적는다. 코드 작성과 테스트 통과는 검증이 아니다.
11. **`.meta`는 항상 같이 커밋한다.** Unity 밖에서 파일을 만들면 `Tools/Generators/mkmeta.py`로 만든다.
12. **같은 `.unity` 씬을 두 사람이 동시에 고치지 않는다.** 맵은 구간 프리팹으로 나눈다.
13. **네트워크·게임 코드(`Assets/Scripts`의 Core·Network·Game·Gameplay·Bootstrap·Input·Editor)는 래그돌 타입을 참조하지 않고, 래그돌(`Assets/Scripts/RagdollLab`, 10-08 폴더 정리 전 `Assets/ChessFight/RagdollLab`)은 Steam을 참조하지 않는다.** 랩을 Steam에 잇는 코드는 `Assets/Scripts/RagdollLabSteam/`(Bootstrap과 같은 다리)에만 둔다. 캐릭터는 `ICharacterDriver`로만 부른다. `Tools/run-tests-linux.sh`의 경계 검사가 확인한다.
14. **물리 콜백(`OnCollision*`·`OnTrigger*`) 안에서 순간이동하거나 관절을 즉시 지우지 않는다.** Unity가 `DestroyImmediate`를 거부해 잡기 관절이 남는다. 알리기만 하고 다음 Update에서 처리한다(`WaterZone` → 시험대·`PlaytestSpawner`). → [PITFALLS §19](Docs/Environment/PITFALLS.md)
15. **(`claude/host-migration`부터) 경기 상태를 방장 PC에만 두지 않는다.** 방장은 경기 중에 바뀔 수 있다([HOST](Docs/Network/HOST.md)). 점수·타이머·모드 상태는 스냅샷이나 로비 데이터에 실어 모두가 갖고 있게 한다. 방장만 아는 값은 이전 때 사라진다.

## 5. 문서 트리

```text
HANDOFF.md                           현재 상태·규칙·작업 경로
Docs/
├─ Project/                          REQUIREMENTS · DECISIONS · HISTORY
├─ Architecture/                     STRUCTURE · SCENES · UI · PROJECT_CLEANUP_R497
├─ Environment/                      SETUP · AI_WORKFLOW · PITFALLS
├─ Network/                          SESSION · MOTION · HOST · HOST_AUTHORITY · VALIDATION
├─ Player/                           MOVEMENT_INPUT · RAGDOLL
├─ RagdollLab/                       공용 물리 시험·어댑터 설명
├─ Skills/                           폰 러시·소드 파이트 스킬
├─ VFX/                              공용 효과·작업대
├─ PawnRush/                         현재 폰 러시 맵·구간·미니게임 자료
│  ├─ PAWN_RUSH_SECTIONS.md           구간 목록·연결·시험
│  ├─ PAWN_RUSH_MINIGAME_SECTIONS.md  실제10종 프리팹·여는 법·수정 주의
│  ├─ PAWN_RUSH_MINIGAME_MODELING.md  건축·소품·피벗·이동 공간
│  ├─ Prototype/                     Course01·이전 Opening/Castle/Final 기획
│  └─ ConceptArt/                    모델링 이미지·검사 자료
├─ GameModes/SwordFight/              소드 파이트 기획·프로토타입·시연
└─ Archive/                          이전 HANDOFF 전체·폐기 Queen 모드 문서
Assets/
├─ Scripts/                          종류별 어셈블리·현재 코드
├─ Scenes/                           현재 온라인 흐름·시험 씬
├─ Prefabs/PawnRushSections/          현재 구간·미니게임 프리팹
├─ Prototype/                        구형 맵·체스복싱·기물 미리보기
├─ Resources/SharedSkills/            현재 모드가 쓰는 공용 효과·소리
└─ Art · Materials · Settings · StreamingAssets · VFX
ArtSource/                           Blender 원본·내보내기 스크립트
Tools/                               생성기·검사·AI 연결 등 개발 도구
```

Prototype의41개 C# 파일은7개 asmref로 기존 어셈블리에 속한다.
폴더를 옮겼다는 이유로 같은 타입을 다른 어셈블리에 복제하거나 새 asmdef를 만들지 않는다.
과거 문서는 [전체 아카이브](Docs/Archive/HANDOFF_BEFORE_R497.md)에서 당시 경로와 함께 찾는다.

## 6. 어디를 읽을까 — 작업 분야별 안내

| 작업 | 먼저 읽을 것 | 그다음 코드 |
|---|---|---|
| 무엇이든 처음 | 이 파일 → [REQUIREMENTS](Docs/Project/REQUIREMENTS.md)·[DECISIONS](Docs/Project/DECISIONS.md)·[PITFALLS](Docs/Environment/PITFALLS.md) | 현재 main의 관련 파일만 |
| 파일·메뉴·Prototype·브랜치 정리 | [R497 정리 안내](Docs/Architecture/PROJECT_CLEANUP_R497.md) → [STRUCTURE](Docs/Architecture/STRUCTURE.md) | 경로 문자열·asmref·메타·생성기·검사 스크립트 |
| 현재 폰 러시 맵 | [PawnRush README](Docs/PawnRush/README.md) → [구간](Docs/PawnRush/PAWN_RUSH_SECTIONS.md) | PawnRushRound·PawnRushSection·SectionPrefabMenu·구간 프리팹 |
| 코스·미니게임 모델링·텍스처 | [3단계 아트 기준](Docs/PawnRush/PAWN_RUSH_ART_PHASES.md) → [모델링 지침](Docs/PawnRush/PAWN_RUSH_MINIGAME_MODELING.md) | MiniGamePalaceArt*·RoyalWaterGardenBuilder.Concept·ArtSource/PawnRush |
| 미니게임10종 | [실제 프리팹](Docs/PawnRush/PAWN_RUSH_MINIGAME_SECTIONS.md) → [기존 규칙](Docs/PawnRush/PAWN_RUSH_MINIGAMES_v1.md)·[모델링](Docs/PawnRush/PAWN_RUSH_MINIGAME_MODELING.md) | MiniGameSection*·MiniGamePalaceArt*·MissionStation·Mg*·PaintBoard |
| 정원·홀·작전 회의실 | [정원](Docs/PawnRush/PAWN_RUSH_GARDEN.md)·[홀 아트](Docs/PawnRush/PAWN_RUSH_GRAND_STAIR_HALL_ART.md)·[구간](Docs/PawnRush/PAWN_RUSH_SECTIONS.md) | RoyalWaterGardenBuilder*·GrandStairHall*·WarRoomSection·Course01v4Builder.* |
| 구형 맵·기존 폰 러시 온라인 | [Prototype Course01](Docs/PawnRush/Prototype/PAWN_RUSH_COURSE01.md) → [Prototype 기획](Docs/PawnRush/Prototype/DESIGN.md) | Assets/Prototype/PawnRush·SteamPawnRushLink·PawnRushNetState |
| 소드 파이트 | [PROTOTYPE](Docs/GameModes/SwordFight/PROTOTYPE.md) → [명세](Docs/GameModes/SwordFight/PROTOTYPE_SPEC_v1.0.md)·[SKILLS_ONLINE](Docs/Network/SKILLS_ONLINE.md) | SwordFightGame·SwordFightMatch*·SwordFightSkills*·LabNetLink.Sword |
| 공용 래그돌·입력·이동 | [RAGDOLL](Docs/Player/RAGDOLL.md) → [MOVEMENT_INPUT](Docs/Player/MOVEMENT_INPUT.md)·[랩](Docs/RagdollLab/README.md) | RagdollPawn*·RagdollDriver·ICharacterDriver·PhysicsMechanicsBed |
| 스킬·효과 | [Skills](Docs/Skills/README.md)·[VFX](Docs/VFX/README.md) → [SKILLS_ONLINE](Docs/Network/SKILLS_ONLINE.md) | PawnRushSkills·SwordFightSkills·SkillKit·SharedSkills·VFX |
| 매칭·파티·음성 | [SESSION](Docs/Network/SESSION.md)·[게임 모드](Docs/GameModes/README.md) | SteamSession·GameModes·TeamReservations·SteamVoice |
| 이동 동기화·방장·지연 | [MOTION](Docs/Network/MOTION.md)·[HOST](Docs/Network/HOST.md)·[HOST_AUTHORITY](Docs/Network/HOST_AUTHORITY.md) | SteamMotion·RagdollLabSteam·HostElection·스냅샷 프로토콜 |
| 로비·HUD·씬 흐름 | [UI](Docs/Architecture/UI.md)·[SCENES](Docs/Architecture/SCENES.md) | Game·Bootstrap·SceneNames·EditorBuildSettings |
| Safe Mode·컴파일·클릭 오류 | [PITFALLS](Docs/Environment/PITFALLS.md)·[SETUP](Docs/Environment/SETUP.md) | 관련 asmdef·Editor·입력 경로 |
| 검사·빌드 | [AI_WORKFLOW](Docs/Environment/AI_WORKFLOW.md)·[VALIDATION](Docs/Network/VALIDATION.md) | Tools/Test-NetworkCore.ps1·Test-NetworkCompile.ps1·MiniGamePrefabValidation |
| GPT·Claude 계정 연결 | [AI_WORKFLOW](Docs/Environment/AI_WORKFLOW.md) → [연결 사용법](Tools/AiBridge/README.md) | Tools/AiBridge |
| 폐기 모드의 과거 근거 | [아카이브](Docs/Archive/HANDOFF_BEFORE_R497.md)·[Queen 기록](Docs/Archive/QueenOfTheHill/README.md) | 현재 모드 등록이나 새 작업 지시로 재사용하지 않음 |

## 7. 사용자와 일하는 방식

- **한국어로 답한다.** 코드 주석과 커밋 메시지는 영어다.
- 사용자는 메인 기획자이며 코드를 직접 쓰지 않는다. 결과·영향·여는 순서를 쉽게 설명한다.
- 작업은 **기존 `C:\Users\dua07\GitHub\ChessFighter`의 main에서 직접** 한다. 추가 워크트리는 새 명시 요청이 있을 때만 쓴다.
- 구현·정리 요청은 실제 변경·적절한 검사·main 커밋·푸시까지 완료한다. PR은 요청할 때만 만든다.
- 새 사용자 지시가 과거 브랜치·보존 범위·번호 규칙보다 우선한다. 과거 기록의 "커밋 금지"를 현재 전체 작업 제한으로 오해하지 않는다.
- 사용자 수정본은 덮어쓰기 전에 보존한다. 파일 백업·패치·stash와 현재 main에 적용한 결과를 구분한다.
- **사용자가 Unity나 Steam에서 직접 보고 조작한 것만 '검증됨/성공'이라고 기록한다.** AI 자동 검사·렌더·코드 관찰은 그 근거와 한계를 쓴다.
- UI 자동화가 사용자 입력·Escape로 중단되면 추가 화면 조작을 멈추고 완료된 범위와 남은 범위를 구분한다.
- 공유 캐릭터 물리·카메라·네트워크는 요청 범위 없이 바꾸지 않는다. 파일 정리로 게임 규칙이나 프로토콜을 바꾸지 않는다.
- 사용자가 싫어하는 것은 클릭 불가 UI, Safe Mode, 저절로 생기는 변경점, 읽기 힘든 중첩 폴더, 근거 없는 완료 주장이다.

## 8. 작업 종료 체크리스트

1. Windows는 `Tools/Test-NetworkCore.ps1`, 필요하면 `Tools/Test-NetworkCompile.ps1 -SteamRuntimeSources <Runtime 경로>`를 실행한다. Linux는 `Tools/run-tests-linux.sh --compile`을 따른다.
2. 요청은 [REQUIREMENTS](Docs/Project/REQUIREMENTS.md)에 날짜·내용·결과·커밋·상태로 기록한다.
3. 새 결정은 [DECISIONS](Docs/Project/DECISIONS.md), 실제 겪은 함정은 [PITFALLS](Docs/Environment/PITFALLS.md)에 남긴다.
4. 변경 분야의 상세 문서와 실제 코드·파일·메뉴·리소스 경로를 맞춘다.
5. 사용자 Unity·Steam 확인 항목은 [VALIDATION](Docs/Network/VALIDATION.md) 최상단에 미확인 표로 남긴다.
6. 이 파일의 최신 날짜·번호와 §1 상태·§2 범위·§3 다음 작업·§6 경로를 갱신한다. 과거 긴 이력은 아카이브에 둔다.
7. [HISTORY](Docs/Project/HISTORY.md)에 결과를 한 줄 기록하고 **main에 커밋·푸시**한다. 관련 없는 사용자 변경을 함께 stage하지 않는다.
8. 검사를 끄거나 경고를 숨겨 통과시키지 않는다. 자동 검사·사용자 확인·플레이어 빌드·Steam·성능을 각각 구분해 보고한다.
