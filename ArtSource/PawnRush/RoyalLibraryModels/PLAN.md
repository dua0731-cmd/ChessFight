# 왕의 서재 코스 그래픽 — 구역 나누기 · 세션 분담 계획 (seoungtest R687, 2026-11-17)

승규: "완전 컨셉 아트 처럼 빡세게" → "구역 나눠서 어디 구현 할 지 세션에 나눠, 작업은 하지 말고".
이 문서는 **계획만** 담습니다. 아직 아무 구역도 시작하지 않았습니다.

- 목표 그림: D "거인의 책상" — 체스 방 `GC_D*`, 앞 구간 `GC_E*` ([R687_GiantChessLibrary](../../../Docs/PawnRush/ConceptArt/R687_GiantChessLibrary)).
- 맵 구조 · 좌표: [ROYAL_LIBRARY_STRUCTURE](../../../Docs/PawnRush/ROYAL_LIBRARY_STRUCTURE.md). 칸 크기 10 m 확정.
- 블록아웃: `RoyalLibrary_blockout.blend`(이 폴더, `rl_blockout.py`로 다시 만듦).
- 방식은 마차뜰과 같음: 스크립트 모델링 + 미터 단위 UV + 재질 라이브러리(Codex 텍스처 → 이음매 없는 타일) + FBX → Unity가 Play 때 입힘. 사람 · 화초처럼 유기적인 것만 Codex 시트 → Tripo(승규) → Blender 손질.

## 1. 구역

| 구역 | 범위 (블록아웃 컬렉션 · z) | 만들 것 | 컨셉 |
|---|---|---|---|
| **R0 공통 기반** | 전체 | 재질 라이브러리(호두나무 · 놋쇠 · 붉은 가죽 · 양피지 · 크림 대리석 · 흑단 · 크림/호두 체스 칸 · 검은 법랑 · 크림 천), 공통 모델링 도구 `rl_kit.py`(둥근 베벨 · 미터 UV · 이름 규칙 · FBX 내보내기), **코스 탁자 키트**(상판 · 놋쇠 테 · 앞치마판 · 돌린 다리 — 모든 구간이 씀), Unity 입히기 틀(`RoyalLibraryDressing` 기본 파일 + 프리팹에 한 번 붙이기), 시험 한 점(탁자 1개를 Unity 조명까지) | D6 재질 견본, D5 판 모서리 · 다리, E5 탁자 |
| **R1 입구 · 도장** | 01 Entrance + 02 Stamp, z -150 ~ -100 | 차원문 통로 · 아치 · 대리석 계단 · 놋쇠 공 기둥(E/D1 입구), 출발 · 도장 탁자 배치(R0 키트), 도장 장치 2개(누름팔 · 기둥 · 놋쇠 머리 · 붉은 인장 자국, 움직이는 머리는 `Stamp head` 피벗 그대로) | E2, E5, D1 |
| **R2 덮히는 책** | 03 Books, z -100 ~ -60 | 거대 책 2권(펼친 페이지 바닥 · 움직이는 가죽 표지 · 놋쇠 모서리 · 책등 경첩 — `Horizontal closing cover` 피벗 그대로), 책상 2개 · 인장 휴식 탁자 · 둥근 인장 받침 | E3, E5 |
| **R3 타자기** | 04 Typewriter, z -60 ~ -8 | 타자기 본체 · 롤러 · 종이 · 타자봉 · 리본, 둥근 키 23개(놋쇠 테 · 스프링 받침 — `Key r-c` 위치 · 크기 그대로, 글자는 게임이 씀), 안쪽 통로, 받침 가구 · 앞뒤 탁자, 힌트판 5개 | E4, E5 |
| **R4 앞 구간 서재** | 00 Shell(앞) + 05 Library decor, x ±38 · z -150 ~ -10 | 서재 벽 · 외벽 · 천장 모듈(호두나무 서가 벽 · 아치 창 · 크림 돌 벽), 책장 7개(책 채우기), 기보 액자 4개(64칸 미니 판), 화분 3개(Tripo) | D5, E1 |
| **R5 체스 방 건축** | 06 Chess room(코드 밖) + 07 Exit, z -10 ~ 170 | 방 벽 · 천장 들보 · 근접 문 · 깃발 자리, 참나무 탁자(82 × 112) · 다리, 지휘관 의자 2개(붉은 가죽), 서가 6개, 큰 창 · 독서 코너 · 초상화, 출구 길 · 출구 아치. 방이 판보다 넓게 남는 문제(248 × 180) 정리 | D1 ~ D5 |
| **R6 체스 · 거인 (코드)** | 09 Play-time chess — `GiantChessCourse.cs`가 만드는 것 | 칸 64개 재질(크림 · 호두, 베벨), 흑 · 백 기물 12종(D6, 지금 FBX 교체), 거인 GM 2명(D3 — Codex 시트 → Tripo → 손질, 팔 · 손 움직임 유지), 공격 경고 칸 · 점유 칸 표시, 진입 · 출구 발판 · 아치 | D2, D3, D6 |
| **R7 거인 책상 소품 · 연출** | 코스 바깥 원경 + Unity 장면 | 거대 잉크병 · 깃펜 · 회중시계 · 돋보기 · 스탠드 · 찻잔 · 책 더미 · 양피지 · 만년필(D6)을 코스 둘레 낮은 곳에, Unity URP 조명 · 후처리(오후 햇살 줄기, 블룸, 색 보정, 연출 카메라에만 피사계 심도), 반사 프로브 | D, D1 ~ D4, E1 |

**진행(11-18):** R0 — 재질 라이브러리 8종 · 디테일 맵 · Unity 재질 끝, `rl_kit.py` · `RoyalLibraryDressing.cs`(프리팹 루트에 붙임) 끝. 코스 탁자 키트 = E5 탁자 세션의 `build_table(W, D)`, 시험 한 점(출발 탁자, `rl_r0_test_table.py`) 승규 Unity 확인 11-18 → **R0 끝.** 탁자 1개 39,796 삼각형이라 구역 작업 때 줄일 것. 거인(R6)은 세션 "rl-giant".

**구역 세션(11-18 승규 "구역 세션 열어"):** R1 "rl-r1-entrance" · R2 "rl-r2-books" · R3 "rl-r3-typewriter" · R4 "rl-r4-gallery" · R5 "rl-r5-chessroom", 거인(R6 일부) "rl-giant", E5 소품 "rl-e5-props"(+ 하위 3). 모두 백그라운드 Blender, 같은 폴더, 3절 파일 규칙. R6 나머지(체스판 칸 · 기물)와 R7(조명 · 원경 소품)은 아직.

**R7(11-18, 세션 "rl-r7"):** 원경 소품은 바닥에서 안 보여 가죽 책 더미 받침(윗면 y -3) 위 10곳으로(`rl_r7_desk_props_model.py`, 승규 Blender · Unity 확인), 조명 · 후처리는 `RoyalLibraryLighting.cs`(장면 YAML 안 고침, 승규 Unity 확인 1차). 피사계 심도는 플레이 화면에 안 넣음.

**R6 체스(11-18, 세션 "rl-r6-chess"):** `rl_r6_chess_model.py` → `Models/Chess/` 13개(판 · 경고 · 점유 · 발판 · 결승선 · 아치 · 기물 6종), `GiantChessCourse.cs` 연결 · 컴파일 PASS. 승규 Blender 사진 · Unity Play 확인 — R6 끝.

## 2. 세션 분담과 순서

```
1단계(혼자)   R0 공통 기반 ── 시험 한 점을 승규가 Unity에서 "이 정도" 확인
                   │
2단계(동시)   R1 입구·도장   R2 책   R3 타자기   R4 앞 서재   R5 체스 방 건축
                   │                                   │
3단계(동시)   R6 체스·거인 (Tripo 대기 포함)        R7 소품·연출 (모든 구역 들어온 뒤 마무리)
```

- **R0이 먼저인 이유:** 재질 · 탁자 키트 · Unity 입히기 틀을 모든 구역이 같이 씁니다. 동시에 만들면 서로 다른 나무색 · 베벨 · 이름이 나옵니다.
- **2단계 5개는 서로 파일이 겹치지 않아** 동시에 돌려도 됩니다(아래 3절). 한 번에 다 열기보다 2 ~ 3개씩이 Unity 재임포트 · 확인 부담이 적습니다. 권장 첫 묶음: R3 타자기 + R2 책 + R5 체스 방(가장 눈에 띄는 곳).
- **R6**은 Tripo(거인 · 화초) 의뢰를 1단계 끝나자마자 미리 넣어 두면 기다림이 줄어듭니다.
- **R7 연출**은 마지막. 조명을 먼저 잡으면 재질 색이 조명에 끌려가 다시 맞춰야 합니다. 단, R0 시험 한 점에서 기본 햇살 방향 · 색온도만은 정해 둡니다.

## 3. 파일 충돌 막기 (세션 규칙)

모든 세션이 같은 seoungtest 폴더 · 같은 Unity 프로젝트를 씁니다.

| 무엇 | 주인 | 다른 세션 |
|---|---|---|
| `WarRoomComplete.prefab` | **R0만**(입히기 컴포넌트 붙이기 한 번). 이후 아무도 안 고침 | 프리팹 대신 이름으로 찾아 Play 때 입힘 |
| `rl_kit.py`, 재질 라이브러리(`textures/`, Unity `Materials`) | R0 | 읽기만. 새 재질이 필요하면 자기 구역 폴더에 |
| `RoyalLibraryDressing.cs`(기본) | R0 | 자기 partial 파일만: `RoyalLibraryDressing.Entrance.cs` · `.Books.cs` · `.Typewriter.cs` · `.Gallery.cs` · `.ChessRoom.cs` · `.Desk.cs` |
| Blender 파일 | 구역마다 하나: `rl_<구역>.blend` + `rl_<구역>_model.py` | 블록아웃 `.blend`는 아무도 덮어쓰지 않음 |
| FBX · 텍스처 내보내기 | `Resources/RoyalLibrary/Models/<구역>/` · `Art/RoyalLibrary/<구역>/` | |
| `GiantChessCourse.cs` | R6만 | |
| Unity 조명 · 후처리 · 장면 파일 | R7만 | |
| HANDOFF · REQUIREMENTS | 각자 맨 위에 자기 항목 한 줄씩 추가(남의 항목 안 고침) | |

- 장치 판정은 그대로: 걷는 면 · 콜라이더 · 움직이는 피벗(도장 머리, 책 표지, 타자기 키, 근접 문)은 크기 · 위치를 바꾸지 않고 **그림만** 입힙니다. 모델이 판정보다 크게 튀어나와 발을 걸면 안 됨.
- Unity가 Play 중이면 Assets에 쓰지 않음(Editor.log로 확인 — 기존 규칙).
- 커밋 · 푸시는 승규가 말할 때만.

## 4. 세션 시작 문장 (그대로 붙여 넣기)

각 세션은 이 폴더(`C:\Users\Makebot2\Documents\GitHub\ChessFight`, 브랜치 seoungtest)에서 엽니다.

> **R0:** `ArtSource/PawnRush/RoyalLibraryModels/PLAN.md`의 R0 공통 기반을 해 줘. 재질 라이브러리 · rl_kit.py · 코스 탁자 키트 · RoyalLibraryDressing 기본 틀 · 탁자 1개 시험(Unity 조명까지). 3절 파일 규칙 지키고, 다른 구역은 손대지 마.

> **R1 ~ R7:** `ArtSource/PawnRush/RoyalLibraryModels/PLAN.md`의 R<번호> <구역 이름>을 해 줘. R0 결과(rl_kit.py · 재질 라이브러리 · 탁자 키트)를 쓰고, 3절 표에서 내 구역 파일만 만들어. 컨셉은 1절 표의 그림.
