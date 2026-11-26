# 왕의 서재 코스 맵 구조 (seoungtest R687, 2026-11-17)

pr/2에서 가져온 코스(왕의 서재 → 거대 체스)를 그래픽 작업용으로 정리한 문서입니다.
- 근거: `Assets/Prefabs/Obstacles/WarRoomComplete.prefab`(seoungtest에 가져온 그대로)와 `Assets/Scripts/RagdollLab/GiantChessCourse.cs`, `Assets/Scenes/GiantChessCourse.unity`(2026-11-26 정리 때 main에서 지움, Git 이력에 있음).
- 좌표는 Unity 기준(미터, +Z가 진행 방향, Y가 위)입니다.
- 게임 규칙과 장치 시간은 [WAR_ROOM_COMPLETE](WAR_ROOM_COMPLETE.md) · [GIANT_CHESS_COURSE](GIANT_CHESS_COURSE.md)(pr/2 쪽)에 있습니다.
- 컨셉아트: [R687_GiantChessLibrary](ConceptArt/R687_GiantChessLibrary) — 고른 방향은 D "거인의 책상"(`GC_D*` 체스 방, `GC_E*` 앞 구간).

> **R689(11-18): 이 코스가 폰 러쉬 판의 장애물 3(슬롯 5) 구간이 됐습니다** — 구간 프리팹 `Late_3-1_RoyalLibrary`(이 프리팹을 그대로 품음), 입구 z −151 · 출구 z 171의 문턱 바닥, 체크포인트 6, 탁자 밑 낙사, 온라인 동기화. [GIANT_CHESS_ONLINE](GIANT_CHESS_ONLINE.md). 이 장면(`GiantChessCourse.unity`)은 오프라인 시험 그대로입니다(대국이 끝나면 다음 대국).

## 1. 큰 틀

- 코스 전체가 프리팹 하나(`WarRoomComplete`, 오브젝트 2,061개)입니다. 장면에는 이 프리팹 인스턴스 하나만 있습니다.
- 모양은 모두 Unity 기본 상자 · 구 · 원기둥 · 캡슐이고, 재질은 `Resources/RoyalPalace/Materials`의 단색 13개(NetworkColor 셰이더)입니다.
- 진행은 +Z 직선입니다: 입구 z = -142 → 체스판 z = 0부터 → 출구 z = 169.
- 걷는 면은 높이 0 근처에 떠 있는 탁자들입니다(상판 윗면 y = 0, 타자기 받침만 y = -0.8). 실내 바닥은 y = -23의 장식이고, 떨어지면 체크포인트로 돌아갑니다.
- 바깥 외벽은 320 × 320 m(x ±160, z -150 ~ 170), 높이 60 m입니다. 앞 구간 천장은 y = 30입니다.

## 2. 앞 구간: 왕의 서재 (z -150 ~ -10)

서재 벽이 x = ±38에 있습니다(높이 54 m, 길이 140 m).

| z 위치 | 구간 | 프리팹 이름 | 들어 있는 것 |
|---|---|---|---|
| -137 | 차원문 입구 | `Dimensional entry vestibule` | 좁은 통로(폭 12, 높이 8, 길이 8), 룬 24개, 가림막(`Dimensional veil`, 충돌 없음), `RoyalLibraryPortal` |
| -146 ~ -129 | 출발 탁자 | `Approach floor -146` | 상판 18 × 17 m + 놋쇠 테 · 앞치마판 · 조각 다리 4개 · 다리 끝 장식 |
| -129 ~ -127 | 점프 틈 | | 2 m |
| -127 ~ -100 | 도장 탁자 | `Dispatch table after jump` | 상판 18 × 27 m. 도장 2개 `Command stamp press`(x ±3, z -122): 경고판 6 × 6, 안전 대기 줄, 도장 머리(`CommandStampPress`) |
| -100 ~ -84 | 첫째 책 | `Closing royal folio -100` + `Royal folio desk -100` | 책상 18 × 16 m. 페이지 바닥 18 × 16, 고정 표지, 책등, 움직이는 표지(가죽 · 속지 · 문장), 책갈피, 직전 안전 지점 |
| -84 ~ -76 | 인장 휴식 탁자 | `Fixed seal island` | 상판 18 × 8 m, 둥근 인장 받침 5 × 5 |
| -76 ~ -60 | 둘째 책 | `Closing royal folio -76` + `Royal folio desk -76` | 첫째 책과 같음 |
| -60 ~ -51 | 타자기 앞 탁자 | `Approach floor -60` | 상판 18 × 9 m |
| -51 ~ -24 | 타자기 | `Typewriter stairs - printed command` | 오브젝트 221개. 키 23개(줄 0 ~ 4 = 5 · 4 · 5 · 4 · 5개, 줄마다 0.32 m씩 높아짐, 키 간격 3 m · 줄 간격 2.8 m), 키 받침대, 받침판 15.4 × 14, 본체 옆판 · 앞 볼, 리본 스풀 2 · 리본, 타자봉 17개, 롤러, 종이 캐리지, 이름판, 안쪽 통로(벽 · 지붕 · 내리막, 폭 16 · 높이 6.8). 받침 가구 `Typewriter furniture base` 25 × 24 m(y = -1.4) |
| -27 ~ -8 | 타자기 출구 탁자 | `Approach floor -27` | 상판 16 × 19 m |
| 곳곳 | 장식 | | 책장 `Royal library bookcase` 7개(하나에 부품 81개, 높이 34 m, y = -23에 섬), 화분 3개, 기보 액자 4개(`Random GM replay preview`, x ±37, z -121 / -45, 64칸 미니 판), 힌트판 5개(`Memory clue`, 타자기 아래) |

체크포인트: 입구 z -142, 1 = -106, 2 = -80, 3 = -30, 접근 출구 -4.

## 3. 뒤 구간: 거대 체스 방 (총지휘부, z -10 ~ 170)

`Headquarters - enclosed command chamber`(오브젝트 238개).

- 방: 248 × 180 m(x ±124, z -10 ~ 170), 천장 y = 58.5, 들보 10개.
- 앞벽 z = -10에 근접하면 열리는 문 `Headquarters privacy doors`(폭 16, 통과 높이 6.8).
- 프리팹에 저장된 장식:
  - 거대 참나무 탁자 130 × 168 m(z -20 ~ 148), 다리 4개.
  - 지휘관 의자 2개(x ±86, z 64): 좌판 · 등받이 · 팔걸이 · 왕관.
  - 서가 6개, 큰 화초 4개(`Giant chess conservatory plant`), 창문 2개, 독서 소파 · 깔개 · 옆 탁자 · 책 더미, 뒷벽 초상화, 문 옆 깃발 2개 · 문장.
  - `Board preview` — 편집용 체스판 미리보기. Play 때 진짜 판으로 바뀝니다.
- 출구: `Final exit walkway` 12 × 24 m(z 146 ~ 170) → `Complete course exit`(z 169), 뒷벽 출구 폭 14.

### Play 때 코드가 만드는 것 (`GiantChessCourse.cs`)

프리팹에 없고, 그래픽을 입히려면 코드를 고쳐야 합니다.

| 무엇 | 만드는 곳 | 모양 |
|---|---|---|
| 8 × 8 체스판 | `Shape("Board …")` | 칸 = tileSize, 판 원점 z = 0, x 가운데. R6(11-18): 상자는 충돌체로 남고 그림은 `Models/Chess/RlChessBoard.fbx`(`rl_r6_chess_model.py`, 칸 10 m 기준 × tileSize / 10) |
| 칸마다 공격 경고판 · 점유 칸 금 테두리 | 같은 곳 | 붉은 발광 판, 금 막대 |
| 진입 · 출구 발판, 결승선, 출구 아치 | `Entry/Exit platform`, `Exit arch` | 폭 = 판 폭 |
| 거인 GM 2명 | `BuildGiant` · `GiantGrandmaster` | R6(11-18): Blender 뼈대 모델 `Models/ChessRoom/RlGiant_A/B.fbx`(`rl_giant_model.py`), 좌판 윗면 y 3, x = ±(판 반폭 + 24), z = 판 가운데. 코드가 숙이기 · 팔 IK · 쥐기, 먼 칸은 던지기. 모델이 없으면 옛 캡슐 몸통 · 늘어나는 상자 팔(x ±(반폭 + 22)) |
| 기물 | `BuildPiece` | R6(11-18): `Models/Chess/RlChess{Pawn,Knight,Bishop,Rook,Queen,King}.fbx`(D6, 받침 지름 6.4 m, 상아 `RL_Ivory` / 흑단 `RL_Ebony` + 놋쇠), 충돌체는 칸 크기 그대로. 모델이 없으면 옛 `ChessTimerPieces` |
| 책에 깔린 납작한 캐릭터 | 같은 파일 | 캐릭터 메시 복제 |

## 4. 짚어 둘 점

- **칸 크기 = 10 m로 정함(승규 11-17 "10 m (지금 장면)").** `ArtSource/PawnRush/RoyalLibraryModels/rl_tile10_fit.py`로 16 m 기준이던 장식 28개만 옮김: 참나무 탁자 82 × 112 m(z -10 ~ 102, 다리 x ±35 · z -4 / 96), 지휘관 의자 x ±62 · z 40(거인 자리), 편집용 판 미리보기 x · z 0.625배, 출구 길 z 100 ~ 170(뒷 다리 z 101.2). 벽 · 방 크기 · 문 · 서가 · 화초 · 게임 장치는 그대로(방 248 × 180 m는 판보다 크게 남음). pr/2 원본과 달라진 곳이라 pr/2를 다시 가져오면 이 스크립트를 다시 돌릴 것. 아래는 정하기 전 상황:
- **칸 크기가 서로 맞지 않았음.** 장면이 `tileSize`를 **10**으로 덮어씁니다(코드 기본 18, 프리팹 값 16). 그래서 실제 판은 80 × 80 m(z 0 ~ 80)이고 거인은 x = ±62에 섭니다. 그러나 저장된 장식은 16 m 기준(판 128 m)입니다: 참나무 탁자 130 × 168, 의자 x ±86. 코드상 출구 발판은 z 80 ~ 100에서 끝나고 출구 길은 z 146부터라 사이가 비어 보입니다(Unity에서 직접 확인 전). 그래픽 작업 전에 칸 크기를 정해야 합니다.
- **충돌체.** 걷는 면 · 벽 · 장치에만 있고 서가 · 화초 · 탁자 다리 같은 장식에는 거의 없습니다. 장식 모델을 바꿔도 걷는 판정에는 영향이 적습니다.
- **이름.** "Operations · Headquarters · Command stamp" 같은 작전실 시절 이름이 남아 있습니다. pr/2 문서에 따르면 장면 참조를 지키려고 일부러 유지했습니다.
- **컨셉과의 대응.** D 컨셉의 탁자 · 책 · 타자기 · 도장 · 의자 · 서가 · 액자는 프리팹 부품과 1 : 1로 대응해 모델을 바꿔 끼우면 됩니다. 거인 · 기물 · 체스판은 코드가 만드는 부분이라 따로 작업합니다.

## 5. 블렌더 작업 환경

`ArtSource/PawnRush/RoyalLibraryModels/` — [README](../../ArtSource/PawnRush/RoyalLibraryModels/README.md).
