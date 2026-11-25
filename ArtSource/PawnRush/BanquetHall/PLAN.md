# 대연회장 코스 그래픽: 구역 나누기와 세션 분담 계획 (R835, 2026-11-25)

사용자: "main에 대연회장 seoungtest에 옮기고 모델링 작업 환경 만들어".
이 문서는 **계획과 작업 환경만** 담는다. 아직 아무 구역도 시작하지 않았다. 방식은 왕실 천문대([그 계획](../RoyalObservatory/PLAN.md))와 같다 — 구역별 세션, 부품 하나씩 사용자 확인.

- **작업 브랜치:** seoungtest. main(R833)을 seoungtest에 합쳤다(로컬 합치기 a7935f84, 푸시 안 함). 회색 상자는 R828 그대로다.
- **Unity:** 열면 코스 프리팹 시험 장면(`Assets/Scenes/PawnRush_Courses.unity`)이 뜨고, Play하면 대연회장(`Mid_2-4_GrandBanquetHall`)이 바로 나온다 — `Assets/Scripts/PawnRushEditor/SeoungTestBanquetOnly.cs`. 끄기: ChessFight > Pawn Rush > Grand Banquet Hall Only (seoungtest). 다른 코스는 숫자 키. 천문대용 `SeoungTestObservatoryOnly.cs`는 이걸로 바꾸며 지웠다. **이 파일은 main에 옮길 때 지운다.**
- **기준은 코드:** `Assets/Scripts/PawnRush/GrandBanquetHallBuilder.cs` · `.Course.cs`(Play 때 회색 상자를 짓는다 — 프리팹은 빈 뿌리), 움직임은 `BanquetMovers.cs` · `BanquetDevices.cs`, 샹들리에 메시는 `BanquetChandelierShape.cs`. 설계 · 수치의 이유는 [설계 v0.4](../../../Docs/PawnRush/PAWN_RUSH_BANQUET_HALL_DESIGN_v0.4.md)(맨 위 R833 · R828 · R825 절이 본문보다 맞다). 문서와 다르면 코드를 따른다.
- **목표 그림:**
  - 아트 방향 — [R787 컨셉 아트](../../../Docs/PawnRush/ConceptArt/R787_BanquetHall/banquet_concept.webp): 크림색 석재 벽과 호두나무 기둥 · 들보, 아치 창, 금 테 · 금 촛대, 올리브 초록 러너와 흰 식탁보에 금 체스 문장, 흰 깃발에 금 기물, 꽃 장식, 촛불의 따뜻한 빛, 은 뚜껑.
  - 배치 — [R833 컨셉 그림 네 장](../../../Docs/PawnRush/ConceptArt/R833_BanquetHallConcept/)(R828 회색 상자를 Blender로 칠한 것: 홀 전체 · 샹들리에 · 은 뚜껑 승강기 · 초콜릿 분수와 회랑 사다리)과 [R818 샹들리에](../../../Docs/PawnRush/ConceptArt/R818_BanquetHallConcept/).
  - R833은 배치 그림이라 재질 · 장식이 얇다. 천문대 R725처럼 **이 배치에 R787의 아트 방향을 입힌 Codex 컨셉 3장**(홀 전체 · 동쪽 식탁과 은 뚜껑 · 서쪽 무대와 분수)을 B0 전에 뽑을지 사용자에게 묻는다(1-3절).

## 천문대와 같은 점 · 다른 점

| | 왕실 천문대 | 대연회장 |
|---|---|---|
| 회색 상자 | `RoyalObservatoryBuilder`가 Play 때 코드로 짓는다 | 같다 — `GrandBanquetHallBuilder.Build`(프리팹 `Mid_2-4_GrandBanquetHall.prefab`은 빈 뿌리) |
| 블록아웃 | Play 때 지어진 계층 → JSON 덤프 → `ro_blockout.py` | 같은 방식: `BanquetBlockoutDump.cs` → `bh_blockout.json` → `bh_blockout.py` → `bh_blockout.blend` (B0). 지금 있는 `banquet_blockout.py`(R815 설계 블록아웃) · `banquet_graybox_concept.py`(컨셉 그림)는 설계용이라 덮어쓰지 않는다 |
| 입히기 | 빌더 끝에서 `RoyalObservatoryDressing` | 빌더 `Build`의 `TheWay(b); Course(b);` 뒤 한 줄로 `GrandBanquetHallDressing.Dress(content)`(B0만), 구역마다 partial 파일 |
| 움직이는 것 | `Device_*` 13종 | 웨이터 넷(쟁반 · 접시 더미) · 식탁보 · 은 뚜껑 승강기 둘(접시 · 뚜껑 · 쇠줄 · 사슬) · 춤추는 열 쌍 · 무대 막(주름 10) · 초콜릿 물결 · 샹들리에 열둘(줄 · 축) · 큰 샹들리에. 모델은 그 물체의 자식으로 붙고 같이 움직인다 |
| 사람 | 없음(조각상만) | **모두 체스 기물** — 손님 · 웨이터(비숍) · 왕과 여왕 · 마술사 · 악단 · 춤추는 쌍. 다른 채팅의 기물 모델을 먼저 쓴다(1-3절) |
| 천 | 깃발(정지) | 식탁보 · 막 · 깃발 · 식탁 치마. **흔들리는 천은 모델에 넣지 않는다** — 막 주름 · 식탁보는 지금 코드가 움직이는 판에 '굳은 주름' 모델만 붙이고, 흔들림이 필요하면 Unity 코드 |
| 불 · 빛 | 등 모양만, 빛은 코드 | 같다 — 초 · 촛대 · 샹들리에 초는 **초 모양(크림 몸통 · 심지)까지만**, 불꽃 · 빛 · 빛나는 수정은 Unity 코드(B8) |

## 1. 구역

구역 경계는 빌더의 함수(`Build` 안의 순서) 그대로다.

| 구역 | 빌더 함수 | 만들 것 | 그림 |
|---|---|---|---|
| **B0 공통 기반** | 전체 | 재질 라이브러리(크림 석재 · 호두나무 · 금 · 은 · 흰 식탁보 · 올리브 러너(금 체스 문장) · 붉은 벨벳 · 대리석(단) · 체스판 바닥 두 색 · 초콜릿 · 수정 유리 · 초 크림), Codex 재질 견본 시트 → 이음매 없는 타일, `bh_kit.py`(`ro_kit.py`를 본뜸: 둥근 베벨 · 미터 UV · 재질 칸 이름 · FBX 내보내기), **공통 부품 키트**(금 테 몰딩 · 호두나무 기둥 · 난간 · 촛대 · 접시 · 잔), 블록아웃 덤프와 `bh_blockout.py`, `GrandBanquetHallDressing.cs` 기본 틀 + 빌더 한 줄, **시험 한 점**(긴 식탁 한 칸 — 식탁보 · 러너 · 접시 · 촛대)을 사용자가 Unity Play로 "이 정도" 확인 | R787, R833 |
| **B1 입구 · 배식실 · 웨이터 행렬** | `Shell`의 칸막이 · 통로, `Servery`, `WaitersParade` | 남쪽 문 · 상인방, 칸막이(호두나무 판 6 m · 금 갓), 배식 카운터와 계단 · 부엌 문 · 식료품실 문, 웨이터 넷(비숍 몸 · 쟁반 · 접시 더미 — 판정 그대로), 웨이터 길 | R787 아래쪽, R833 홀 전체 |
| **B2 긴 식탁 · 식탁보 · 은 뚜껑** | `LongTable`, `Cloches`, `DumbwaiterFrame`(`Hoist`) | 긴 식탁(12 × 38 m) · 식탁 치마 · 러너 · 접시 다섯 · 촛대 · 잔, 앉은 손님(기물) · 의자 등, 마술사(기물 · 모자 · 지팡이), 식탁보(움직이는 판에 굳은 주름), 은 뚜껑 승강기 둘(접시 · 뚜껑 · 금 고리 · 쇠줄 · 금 사슬), 배식 승강기 틀(호두나무 기둥 셋 · 가로대 · 금 도르래 여섯) | R833 은 뚜껑 승강기 |
| **B3 왕의 단 · 발코니** | `Dais`, `RoyalBalcony` | 왕의 단(대리석 앞면 · 금 테), 뒤 계단, 왕의 식탁 두 날개, 왕의 발코니(대리석 앞면 · 금 테 · 왕의 깃발 · 금 난간 · 왕좌 둘 · 차양), 출구 문 | R833 홀 전체 · 샹들리에 |
| **B4 무도장 · 왈츠** | `Waltz`, 바닥 | 체스판 무도장 바닥, 춤추는 열 쌍(여왕 + 짝, 기물 모델 — 판정인 여왕 어깨 7.4 m 그대로) | R833 홀 전체 |
| **B5 무대 · 막 · 뷔페 · 초콜릿 분수** | `StageAndCurtain`, `Buffet` | 무대(앞면 · 테), 붉은 막(주름 10 — 움직이는 판마다 굳은 주름), 막 위 가리개 · 레일, 악단 · 악보대, 악단석과 콘트라베이스 둘, 디저트 뷔페(케이크 받침 · 디저트), 초콜릿 분수(그릇 · 세 층 · 입술 · 줄기 · 방울 — 물결은 지금 코드 `ChocolateRipples`를 살려 재질만) | R833 초콜릿 분수 |
| **B6 회랑 · 샹들리에** | `Loft`, `Ladder`, `Chandeliers`, `GreatChandelier` | 천장 회랑(난간 · 바닥), 회랑의 금 사다리, 샹들리에 열둘 · 큰 샹들리에 — 지금 `BanquetChandelierShape`의 코드 메시를 정밀 모델로 바꿈(줄 끝 금 고리 · 판정 원기둥 그대로) | R818, R833 샹들리에 |
| **B7 홀 셸** | `Shell`의 벽 · 천장, 홀 둘레 | 크림 석재 벽 · 호두나무 기둥과 들보 · 아치 창 · 벽 촛대 · 흰 깃발(금 기물) · 꽃 장식 · 천장(격자 · 장미 장식), 남 · 북 벽의 문 둘레 | R787, R833 |
| **B8 조명 · 연출** | Unity 코드 · 장면 | 촛불 · 샹들리에 · 창빛, 후처리(블룸 · 색 보정), 반사 프로브, 카메라가 홀 안에 있을 때만 켜기(천문대 R7 · 서재 · 마차뜰처럼) | R787 |

- 면 수: 구역마다 지나갈 때 보이는 크기에 맞춰 정한다. 손님 · 춤추는 쌍 · 접시 · 촛대처럼 **여러 번 반복되는 것**은 한 모델을 돌려 쓰고 낮게 잡는다. 홀 전체 목표는 B0 시험 뒤 사용자와 정한다.

## 1-2. 판정은 그대로

- 걷는 면 · 콜라이더 · 움직이는 피벗(쟁반 · 접시 더미 · 식탁보 · 승강기 접시 · 뚜껑 · 여왕 · 막 주름 · 분수 층 · 샹들리에 줄 · 축)은 크기 · 위치를 바꾸지 않고 **그림만** 입힌다. 회색 상자 렌더러만 끄고 콜라이더는 남긴다.
- 모델이 판정보다 튀어나와 발을 걸면 안 된다 — 특히 식탁 위 접시 · 촛대(식탁 위가 길이다), 쟁반 테, 승강기 접시 테, 여왕 어깨, 막 주름 앞면, 분수 테(테 밑 0.45 m의 당기지 않는 띠), 샹들리에 줄 끝 고리.
- 판정을 바꿔야 하면(모양에 맞춘 콜라이더 등) 천문대처럼 빌더는 그대로 두고 그 구역의 Dressing partial이 입힐 때 바꾸고, 이 문서 3절에 적는다.
- '못 잡음 · 못 탐' 면(청회색 — 승강기 틀 기둥, 식탁 치마, 무대 앞면 등)은 모델에서도 잡을 데처럼 보이지 않게 한다.

## 1-3. 먼저 정할 것 (사용자)

1. **Codex 컨셉 3장**을 R833 배치 위에 R787 아트 방향으로 뽑고 시작할지, R787 + R833만으로 바로 B0에 들어갈지.
2. ~~에셋 시트(Codex)~~ **R836에 뽑음(사용자 "모델링 들어갈 모든 에셋 뽑아"):** 모델 시트 11장(BH_A01 ~ A11 — 홀 벽 · 천장 · 회랑 · 배식실 · 긴 식탁 · 은 뚜껑 승강기 · 단 · 발코니 · 무대 · 뷔페 · 분수 · 샹들리에 · 기물 사람들)과 재질 견본 1장(BH_T1, 12종). 목록 · 구역 · Tripo 권장 면 수 · 스크립트/Tripo 제안 · 판정과 다른 곳: [`Docs/PawnRush/ConceptArt/R836_BanquetHallAssets/README.md`](../../../Docs/PawnRush/ConceptArt/R836_BanquetHallAssets/README.md). 사람 기물(A10 · A11)과 조각 많은 소품은 Tripo(사용자), 반복 모듈 · 판정에 맞춰야 하는 장치는 Blender 스크립트가 기본안.
3. **기물(사람) — R836 뒤 사용자 결정(11-25): "블렌더에서 작업 할거고 세션 2개 켜서 아주 정밀하게 작업 할거야 최대한 컨셉 아트랑 똑같이 해".** 기물 사람들(A10 · A11)도 Tripo 없이 Blender에서 만든다. 몸(상아 · 흑단 기물)은 회전체로, 옷 · 왕관 · 모자는 시트 그림대로 따로 만들어 붙인다.
4. ~~세션 수~~ → **두 세션(11-25 사용자)**, 2절.

## 2. 세션 분담과 순서

```
1단계(혼자)   B0 공통 기반 ── 긴 식탁 한 칸을 사용자가 Unity Play에서 "이 정도" 확인
                   │
2단계(동시)   B1 입구·웨이터   B2 식탁·은 뚜껑   B3 단·발코니   B4 무도장·왈츠   B5 무대·분수   B6 회랑·샹들리에   B7 홀 셸
                   │
3단계         B8 조명·연출 (모든 구역이 들어온 뒤)
```

**R836 뒤 사용자 결정(11-25): 전부 Blender, 세션 둘, 아주 정밀하게, 컨셉 아트와 최대한 똑같이.** Tripo는 쓰지 않는다 — 모든 부품(기물 사람 포함)을 Blender 스크립트로 정밀 모델링한다. **목표 그림은 부품마다 R836 시트의 그 칸**(`Docs/PawnRush/ConceptArt/R836_BanquetHallAssets/AssetRefs/BH_A01 ~ A11`)과 R787 컨셉이다: 부품 사진마다 시트 칸을 잘라 렌더와 나란히 둔 비교 사진(`stills/b<번호>/parts/<이름>_compare.png`)을 만들어 실루엣 · 비례 · 몰딩 단수 · 금 테 위치 · 재질 색이 같아질 때까지 고친 뒤 사용자에게 보인다. 크기 · 판정은 빌더 수치를 따르고(그림이 판정과 다른 곳은 R836 README의 '볼 것'), 모양은 시트를 따른다.

| 세션 | 맡는 구역(이 순서로) | 주인인 파일 |
|---|---|---|
| **bh-path (길과 장치)** | B0 공통 기반 → B1 입구 · 배식실 · 웨이터 → B2 긴 식탁 · 은 뚜껑 승강기 → B3 왕의 단 · 발코니 → B4 무도장 · 왈츠 | `bh_kit.py` · `bh_parts.py` · 재질 라이브러리 · `BanquetBlockoutDump.cs` · `bh_blockout.py` · `GrandBanquetHallDressing.cs` 기본 · 빌더 한 줄 · `.Servery.cs` · `.Table.cs` · `.Dais.cs` · `.Waltz.cs`, 기물 사람 공통 몸(`bh_pieces.py`) |
| **bh-hall (홀과 무대)** | B7 홀 셸 → B6 회랑 · 샹들리에 → B5 무대 · 뷔페 · 초콜릿 분수 | `.Hall.cs` · `.Chandeliers.cs` · `.Stage.cs`와 그 구역 파일(`bh_b7_*` · `bh_b6_*` · `bh_b5_*`), `BanquetChandelierShape.cs` |

- bh-hall은 bh-path의 `bh_kit.py` · 재질 라이브러리가 생기기 전에는 Blender에서 모양만 만들어 사진으로 확인받는다(재질 칸 이름은 아래 표를 미리 쓴다, 내보내기는 키트가 생긴 뒤). 공통 파일은 bh-path만 고친다.
- 기물 사람(웨이터 · 손님 · 마술사 · 악사 · 왕 · 여왕 · 짝): 공통 몸은 bh-path가 `bh_pieces.py`로 만들고, B5의 악사 · 연주자는 bh-hall이 그것을 가져다 옷만 입힌다.

(아래는 R835의 처음 기본안 — 세 세션. 위 두 세션이 대신한다.)

기본안 — 세 세션(사용자가 바꿀 수 있다):

| 세션 | 맡는 구역(이 순서로) | 주인인 파일 |
|---|---|---|
| **bh-path (길)** | B0 공통 기반 → B1 입구 · 웨이터 → B2 식탁 · 은 뚜껑 | `bh_kit.py` · `bh_parts.py` · 재질 라이브러리 · `BanquetBlockoutDump.cs` · `bh_blockout.py` · `GrandBanquetHallDressing.cs` 기본 · 빌더 한 줄 · `.Servery.cs` · `.Table.cs` |
| **bh-stage (무대와 단)** | B5 무대 · 분수 → B3 단 · 발코니 → B4 무도장 · 왈츠 | `.Stage.cs` · `.Dais.cs` · `.Waltz.cs`와 그 구역 파일 |
| **bh-shell (홀)** | B7 홀 셸 → B6 회랑 · 샹들리에 | `.Hall.cs` · `.Chandeliers.cs`와 그 구역 파일 |

- **B0이 먼저인 이유:** 재질 · 금 테 · 기둥 · 촛대 · 접시를 모든 구역이 같이 쓴다. 따로 만들면 금색 · 베벨 · 이름이 제각각이 된다.
- 다른 두 세션은 B0의 `bh_kit.py`가 생기기 전에는 Blender에서 모양만 만들어 사진으로 확인받는다(내보내기는 키트가 생긴 뒤).
- **확인 순서 — 부품 하나씩, 조립은 나중에(천문대 11-19 규칙):** 구역을 부품으로 나눠 부품 하나를 따로 만들고 → Blender 사진(컨셉과 나란히)을 사용자가 "확인" → 다음 부품 → 조립 사진 → FBX → 사용자 Unity Play 확인.
- **B8은 마지막.** 조명을 먼저 잡으면 재질 색이 조명에 끌려간다. 단 B0 시험 한 점에서 촛불 색온도만은 정한다.
- 등 빛 · 촛불 · VFX 같은 연출도 Claude 세션이 한다(천문대 때 사용자 "내가 한다는 게 아니라 여기서 해").

재질 칸 이름(모든 세션 공통, `bh_kit.slot()`): `stone`(크림 석재) · `walnut` · `gold` · `silver` · `linen`(흰 식탁보) · `runner`(올리브 러너) · `velvet`(붉은 막) · `marble` · `floor_light` · `floor_dark` · `chocolate` · `crystal` · `wax`(초) · `piece_white` · `piece_black`.

## 3. 파일 충돌 막기 (세션 규칙)

모든 세션이 같은 seoungtest 작업 폴더 · 같은 Unity 프로젝트를 쓴다. 같은 폴더에서 **튜토리얼(R834) 세션도 seoungtest를 쓴다** — 그쪽 파일(`Tutorial*`, `RagdollLabBuilder.Tutorial*`, `Docs/Player/...`)은 건드리지 않는다.

| 무엇 | 주인 | 다른 세션 |
|---|---|---|
| `GrandBanquetHallBuilder.cs` · `.Course.cs` · `BanquetMovers.cs` · `BanquetDevices.cs` | **B0만**(`Build` 끝의 입히기 한 줄). 그 뒤 아무도 안 고침 — 판정 · 장치 수치는 그래픽 작업에서 바꾸지 않는다 | 이름으로 찾아 입힘. 빌더가 바뀌어야 하면 사용자에게 먼저 |
| `BanquetChandelierShape.cs` | B6 | 다른 세션은 안 고침 |
| `Mid_2-4_GrandBanquetHall.prefab` · `Assets/Settings/PawnRushSections.asset` | 아무도 안 고침 | |
| `bh_kit.py`, `bh_parts.py`(공통 부품), 재질 라이브러리(`ArtSource/PawnRush/BanquetHall/textures/`, `Assets/Resources/BanquetHall/Materials/`) | B0(bh-path) | 읽기만. 새 재질이 필요하면 자기 구역 폴더에, 공통 부품 수정은 bh-path에 요청 |
| `GrandBanquetHallDressing.cs`(기본) | B0 | 자기 partial만: `.Servery.cs`(B1) · `.Table.cs`(B2) · `.Dais.cs`(B3) · `.Waltz.cs`(B4) · `.Stage.cs`(B5) · `.Chandeliers.cs`(B6) · `.Hall.cs`(B7) · `.Lighting.cs`(B8) |
| Blender 파일 | 구역마다 하나: `bh_b<번호>_<구역>.blend` + `bh_b<번호>_<구역>_model.py`, 사진은 `stills/b<번호>/` | `bh_blockout.blend` · `banquet_*.py`(설계 · 컨셉 그림용)는 아무도 덮어쓰지 않음 |
| FBX · 텍스처 | `Assets/Resources/BanquetHall/Models/<구역>/` · `Assets/Art/BanquetHall/<구역>/` | |
| HANDOFF · REQUIREMENTS | 각자 맨 위에 자기 항목 한 줄씩(남의 항목 안 고침, 번호는 일찍 잡고 쓰기 직전 다시 확인) | |

- 투명한 것(수정 · 잔 · 유리 뚜껑)은 따라가는 카메라를 가리면 안 된다.
- Unity가 Play 중이면 Assets에 쓰지 않는다(Editor.log로 확인 — 기존 규칙).
- 커밋 · 푸시는 사용자가 말할 때만.
- 사용자가 Blender 사진 · Unity Play로 보기 전에는 "확인됨"이라 적지 않는다.
- main에 옮길 때: `SeoungTestBanquetOnly.cs`를 지우고, 대연회장 폴더 밖의 변경(공용 컴포넌트 · `Assets/Settings` · `ProjectSettings` · 도구)은 하나씩 사용자에게 묻는다.

## 4. 세션 시작 문장 (그대로 붙여 넣기)

각 세션은 `C:\Users\Makebot2\Documents\GitHub\ChessFight`(브랜치 seoungtest)에서 연다. 시작 전에 브랜치가 seoungtest인지 확인한다(GitHub Desktop으로 바뀔 수 있다).

> **B0:** `ArtSource/PawnRush/BanquetHall/PLAN.md`의 B0 공통 기반을 해 줘. 재질 라이브러리 · bh_kit.py · 공통 부품 키트 · 블록아웃 덤프와 bh_blockout.py · GrandBanquetHallDressing 기본 틀과 빌더 한 줄 · 긴 식탁 한 칸 시험(Unity 조명까지). 3절 파일 규칙 지키고, 다른 구역은 손대지 마.

> **B1 ~ B8:** `ArtSource/PawnRush/BanquetHall/PLAN.md`의 B<번호> <구역 이름>을 해 줘. B0 결과(bh_kit.py · 재질 라이브러리 · 공통 부품 키트)를 쓰고, 3절 표에서 내 구역 파일만 만들어. 부품 하나씩 Blender 사진으로 나한테 확인받아. 그림은 1절 표의 것.
