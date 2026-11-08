# 폰 러시

2026-11-06 · R497 정리 기준. Unity와 작업 폴더는 `C:\Users\dua07\GitHub\ChessFighter`의 `main`이다. KingRush는 예전 명칭이며 현재 문서는 `Docs/PawnRush/`에서 관리한다.

## 1. 지금 작업하는 맵

현재 개편의 기준은 **구간 프리팹을 뽑아 이어 붙이는 폰 러시**다. `PawnRush_Sections`는 전체 연결, `PawnRush_MiniGames`는 미니게임 한 종류를 골라 보는 오프라인 시험 장면이다.

| 볼 것 | Unity 메뉴 | 실제 자산 |
|---|---|---|
| 전체 구간 연결 | **ChessFight → Pawn Rush → Open Section Test** | `Assets/Scenes/PawnRush_Sections.unity` |
| 미니게임 10종 | **ChessFight → Pawn Rush → Open Mini-game Prefab Test** | `Assets/Scenes/PawnRush_MiniGames.unity` |
| 기물 스킬 | **ChessFight → Pawn Rush → Open Skill Test** | `Assets/Scenes/SkillTest/PawnRush_SkillTest/PawnRush_SkillTest.unity` |
| 구간 후보 목록 | Project에서 선택 | `Assets/Settings/PawnRushSections.asset` |
| 정원·홀·미니게임·작전 회의실 프리팹 | Project에서 선택 | `Assets/Prefabs/PawnRushSections/` |
| 맵 프리팹 하나씩(왕실 마차뜰 등, R507 병합) | **ChessFight → Pawn Rush → Open Map Test**, `[` `]`로 맵 고르기 | `Assets/Scenes/PawnRush_MapTest.unity` · `Assets/Prefabs/PawnRushMaps/` |

판은 장애물·미니게임을 번갈아 배치하는 7슬롯 구조다. 현재 후보가 있는 슬롯은 다음과 같고, 빈 슬롯5는 건너뛴다.

| 슬롯 | 현재 후보 |
|---|---|
| 1 초반 장애물 | 왕실 수로 정원 |
| 2 초반 미니게임 | C 해자 도개교 · D 쇠살문 누각 · A 의장 광장 |
| 3 중반 장애물 | 대계단 현관홀 |
| 4 중반 미니게임 | B 동관 · E 서고 · F 무도회장 · J 세탁장 |
| 5 후반 장애물 | 없음 |
| 6 후반 미니게임 | G 행차 회랑 · H 체스 홀 · I 침실 |
| 7 마지막 구간 | 작전 회의실 |

팀원 맵 **왕실 마차뜰**(`RoyalCarriageYard`, `claude/nifty-tesla-8212va`에서 R507로 병합)은 아직 슬롯 후보가 아니라 맵 시험 장면에서만 연다. 슬롯 1 "1-2 왕실 마차뜰"로 넣으려면 입구·출구와 결승 처리를 먼저 정해야 한다([ROYAL_CARRIAGE_YARD](ROYAL_CARRIAGE_YARD.md) §12 결정 1 · §13).

정원·현관홀·작전 회의실의 현재 아트와 빌더는 개편에 계속 사용한다. `Course01v4Builder`라는 클래스 이름을 가진 partial 파일도 이 맵들을 만들므로 구형 전체 코스와 함께 옮기거나 삭제하지 않는다.

## 2. 먼저 읽을 문서

| 작업 | 문서 |
|---|---|
| 전체 구간 규격·배치·후보 추가 | [PAWN_RUSH_SECTIONS](PAWN_RUSH_SECTIONS.md) |
| 미니게임10종·여는 법·저장/Play 구조 | [PAWN_RUSH_MINIGAME_SECTIONS](PAWN_RUSH_MINIGAME_SECTIONS.md) |
| 정원 규칙·동선 | [PAWN_RUSH_ROYAL_GARDEN](PAWN_RUSH_ROYAL_GARDEN.md) |
| 정원 아트·현재 제작 흐름 | [PAWN_RUSH_GARDEN](PAWN_RUSH_GARDEN.md) · [아트 원본](../../ArtSource/PawnRush/README.md) |
| 현관홀 아트 | [PAWN_RUSH_GRAND_STAIR_HALL_ART](PAWN_RUSH_GRAND_STAIR_HALL_ART.md) |
| 작전 회의실 아트·코드 | [Library 아트 원본](../../ArtSource/PawnRush/Library/README.md) |
| 미니게임 규칙 | [PAWN_RUSH_MINIGAMES_v1](PAWN_RUSH_MINIGAMES_v1.md) |
| 왕실 마차뜰(팀원 맵 프리팹)·장애물·재미 검증 | [ROYAL_CARRIAGE_YARD](ROYAL_CARRIAGE_YARD.md) |
| 맵 프리팹 하나씩 시험하는 장면·새 맵 만들기 | [PAWN_RUSH_MAP_TEST](PAWN_RUSH_MAP_TEST.md) · [맵 폴더 규칙](../../Assets/Prefabs/PawnRushMaps/README.md) |
| **코스·미니게임 3단계 아트 기준(모델링·텍스처)** | [PAWN_RUSH_ART_PHASES](PAWN_RUSH_ART_PHASES.md) |
| 공간 이미지·제작 규격 | [모델링 지침](PAWN_RUSH_MINIGAME_MODELING.md) · [이미지 갤러리](PAWN_RUSH_MINIGAME_CONCEPTS.html) |
| 사용자 Unity·Steam 확인 목록 | [VALIDATION](../Network/VALIDATION.md) |

## 3. 메뉴와 제작 도구

상단 **ChessFight**에는 `Pawn Rush`·`Sword Fight`·`Development`·`Prototype` 네 그룹만 둔다.

- **Pawn Rush:** 위의 시험 장면3종과 맵 시험(`Open Map Test`, R507), `Build` 아래 정원·홀·미니게임 재굽기, `Validate` 아래 구간·미니게임 검사.
- **Development → Pawn Rush:** 공유 아트·재질·하늘 도구, 엔드게임 모델 가져오기, 좌우 배치 도구, 결과 화면 미리보기.
- **Development → Map Authoring:** 현재 제작용 Pawn Rush·Sword Fight 샌드박스 재생성, 맵 시험의 새 맵 프리팹·고른 맵 시험·장면 재생성(R507). SeoungTest 이름의 씬과 현재 아트 소스는 유지한다.
- **Prototype → Pawn Rush:** 구형 Course01 열기·재생성·검사·전용 Art Dress. 현재 구간 코스를 고칠 때는 이 전체 코스 재생성 메뉴를 사용하지 않는다.

재굽기는 코드 생성 영역을 덮어쓴다. 수동으로 수정한 프리팹은 해당 제작 문서의 보존 범위를 먼저 읽는다.

## 4. 구형 맵과 온라인 호환

구형 전체 맵은 `Assets/Prototype/PawnRush/Scenes/PawnRush_Course01.unity`에 보관한다. 초기 OpeningCourse 프리팹24개와 구형 코드41개도 `Assets/Prototype/PawnRush/`에 모았다. 코드의 `.meta`와 기존 어셈블리 소속은 보존하며, `Scripts/`의 asmref7개가 원래 asmdef를 가리킨다.

**R508부터 로비의 폰 러시 온라인 경기는 구간 라운드 `Assets/Scenes/PawnRush_Round.unity`다**(통신 키 `kingrush` 그대로, 판은 매치 번호로 모든 PC가 같이 짓고 작전 회의실 골에 팀 과반이 서면 승리 — [구간 §3 온라인](PAWN_RUSH_SECTIONS.md)). 구형 Course01은 빌드 목록에 남아 있고 경기 링크도 그것을 그대로 돌릴 수 있지만(`MatchCourse`, 2광장·4스테이션·승격 발판), 로비는 더 이상 열지 않는다.

초기 KingRush 시험장·왕의 계단·결승의 당시 규칙과 실행 기록은 [프로토타입 역사 문서](Prototype/README_HISTORICAL.md)에 분리했다. 그 문서의 옛 씬·메뉴·브랜치 안내를 현재 작업 절차로 사용하지 않는다. 개편 전 Course01의 상세 규칙은 [PAWN_RUSH_COURSE01](Prototype/PAWN_RUSH_COURSE01.md)에 남긴다.

## 5. 확인 범위

R497은 파일·메뉴·폐기 모드 정리다. 자동 검사와 AI가 보는 Unity 화면은 사용자 직접 확인과 구분하며, 최신 결과는 [VALIDATION](../Network/VALIDATION.md)에 기록한다. 각 퍼즐의 실제 입력 완주·손맛·12명 성능·다중PC Steam은 별도 확인 항목이다.
