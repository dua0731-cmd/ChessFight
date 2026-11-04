# 폰 러쉬 도서관 컨베이어 맵 — Blender 작업 파일 (R261, 2026-10-31)

`seoungtest`의 새 폰 러쉬 맵(커밋 `4945866`, 도서관 + 왕복 컨베이어)은 Unity용 FBX 두 개로만 들어와 있었다.
이 폴더는 그 FBX를 **Blender에서 고칠 수 있는 작업 파일**로 옮겨 둔 곳이다.

> **`.blend`는 아직 저장소에 없다(R261).** 만든 세션에서 Git LFS 업로드가 막혀(`lfs.github.com … verify: Forbidden`) 스크립트와 문서만 올렸다.
> 승규 님 PC에서 아래 [4. 처음부터 다시 만들기](#4-처음부터-다시-만들기) 한 줄(약 10초)로 만들거나, 채팅으로 받은 `PawnRushLibrary.blend`를 이 폴더에 넣고 **그 PC에서 커밋 · 푸시**한다(`*.blend`는 LFS).

| 파일 | 하는 일 |
|---|---|
| `PawnRushLibrary.blend` | 작업 파일(Blender 5.2, LFS). 여기서 고친다 — **위 안내대로 만들어 넣을 것** |
| `build_library_blend.py` | Unity FBX 두 개 → `PawnRushLibrary.blend`를 처음부터 다시 만든다 |
| `export_library_fbx.py` | `PawnRushLibrary.blend` → Unity FBX 두 개로 내보낸다(검사 포함) |
| `build_graybox.py` | `.blend`에 그레이박스 컬렉션 `GRAYBOX`를 (다시) 만든다(R262, §6) |
| `air_cannons.py` | 환경 FBX의 대포 두 대에서 팬(허브 + 날개 10장)을 `BLOWER_<x>_Fan`으로 떼어 Unity가 돌릴 수 있게 한다(R291, 다시 돌려도 그대로) |
| `armillary.py` | 골의 혼천의 두 대(`Goal_armillary_±1`, 받침 · 링 · 구가 한 덩어리)를 받침만 남기고 링 3개 + 구를 짐벌로 다시 만들어 FBX를 바로 고친다: Ring1 세로축, Ring2(적도)는 Ring1 옆 핀축, Ring3는 Ring2 핀축, 구는 35° 기운 축, 축마다 `<부품>_axis` 빈 물체(R324). R327부터 골의 두 대는 없애고 하나만 `Start_armillary`로 이름을 바꿔 출발 쪽 작전 지도 판 서쪽(x −8.98, y −19.7, 바닥 z 0)에 1.6배(꼭대기 = 지도 꼭대기 3.95 m)로 세움(`-- --preview <폴더>`로 자세 렌더만, 다시 돌려도 그대로) |
| `return_stairs.py` | 바깥 돌아오는 계단(지하 문 → 출발층 문)을 디테일 모델로 다시 짓고 왼쪽에 대칭으로 하나 더 세워 FBX를 바로 고친다(R377 시안, R378 적용): 옛 상자 계단 · 기둥 · 손잡이 · 착지판을 지우고 `Return_stair_R` · `Return_stair_L`(같은 디딤판 높이 · 범위, 둥근 코 디딤판 · 아치 패널 옆벽 · 난간동자 · 공 장식 기둥 · 몰딩 착지판 · 고전 문틀), 왼쪽 벽 세 조각에 불리언으로 문 두 개, 문틀 거울 복사, 그 자리 왼쪽 책장 2개(빈 부모 + `…|Dupli|` 자식) 지움. 위층 착지판은 계단 끝(y −15.8)부터 + 문턱 띠. Unity 충돌체는 `LibraryReplacement`(양쪽, 이 README §3 · `unity_boxes()`와 같은 목록). `-- --preview <폴더>`는 렌더만, 다시 돌리면 그대로 |
| `cannon_weather.py` | 대포 몸체를 `cannon_model.py`로 다시 지어(노즐 테가 관 위에 겹치지 않게) UV를 펴고, 재질 슬롯별 부위 · 위치 · 법선 · AO를 구워 바랜 금 · 페인트 청록 · 크림 돌에 때와 먼지로 칠한 맵(`Assets/Resources/PawnRushLibrary/Cannon/LibraryCannon` + `_MS`)과 재질 `Library cannon worn`으로 FBX를 바로 고친다(R383). `-- --preview <폴더>`는 렌더만, 다시 돌리면 그대로 |
| `wall_anchors.py` | 지붕을 철골로 묶은 볼트로 읽히게, 철제 보 끝마다 바깥벽에 X자 주철 앵커(옆 기둥 윗몸 14개, 뒷벽 2개 — 세로 보를 뒷벽까지 늘임)를 달아 FBX를 바로 고친다(R381). 재질 `Library steel`. `-- --preview <폴더>`는 렌더만, 다시 돌리면 그대로 |
| `roof_vault.py` | 볼트 면 8개(`Vault web coursed stone bay 00..07`, 위만 보는 한쪽 면 판)에 아래를 보는 짝 면을 넣어 지붕이 홀 안에서도 보이게 FBX를 바로 고친다(R380). `-- --preview <폴더>`는 뒷면 컬링 켜고 아래에서 전 · 후 렌더, 다시 돌리면 그대로 |
| `compass_rose.py` | 출발 원판(`START_spawn_zone`, 반지름 1.1 m) 위에 청동 나침반(청동 띠 · 녹청 바탕 · 금 고리 · 눈금 64개 · 16방위 별, 점마다 밝은 금 / 어두운 청동 반쪽 · 북쪽 표시 +y)을 얹어 **렌더만** 한다(`-- --preview <폴더>`, 저장 · 내보내기 없음, 옛 `Upper20_Compass_star` · `Upper20_Start_compass_ring`은 렌더에서 숨김). R359, Unity 미적용 |
| `war_map.py` | 출발 작전 지도(`V21_Original_treasure_map_clay`, 한 덩어리 6.5만 삼각형 조형 · UV 없음)를 숨기고 같은 크기로 깔끔한 상자 모델(나무 기둥 · 가로대, 쇠 띠 · 집게, 금 모서리 캡 + 쇠 다이아 징, 금 볼트 · 모서리 꺾쇠)과 평평한 양피지(numpy로 그린 텍스처: 종이 결 · 찢긴 탄 가장자리 · 강 · 점선 길 · 나무 · 산 · 나침반 · 성 · 연결선)를 짓고 입체 노드(쇠 고리 + 금 돔)를 세워 **렌더만** 한다(`-- --preview <폴더>`, 텍스처 PNG와 렌더만 씀). 양면 모두 제 쪽에서 바로 읽힘. R360, Unity 미적용 |
| `cannon_model.py` | 공기 대포를 컨셉 사진대로 **직접 모델링**해 렌더만 한다(R368, v2 R370 — 돌 판재 포신 · 금 테 · 윗면 띠와 고리 · 큰 룩 청록 띠 · 리벳 박힌 청록 나팔 · 띠 두른 금 노즐 테 + 집게 · 날 20장 팬 · 겹친 트러니언 · 가운데 뚫린 A자 요크 · 계단 회전판 · 돌 블록 받침 + 룩 명판, 2.7만 면 + 팬 1,128면; `-- --preview <폴더>`): 크림 돌 받침(금 발 · 금 테 · 금 모서리 캡 + 리벳, 금 테 청록 옆판 + 금 룩), 회전판, 청록 요크 + 금 트러니언, 수평 포신(금 뒤 뚜껑 · 크림 돌 드럼 · 금 띠 · 금 룩 청록 판 · 손잡이), 청록 나팔, 금 노즐 고리 + 볼트, 어두운 강철 덕트, 새 팬(금 허브 + 소용돌이 날 14장, 원점 = 허브, 날은 y-z 평면). 축은 팬 허브 높이(z 1.501)로 수평이라 `LibraryAirCannon` 회전축과 맞음. 재질은 평면색 부위 5개(`Library gold/teal/cream/steel/dark`). 기존 조형 메시는 렌더에서 대체만, Unity 미적용 |
| `cannon_conveyor_look.py` | 대포 조형(R368 첫 시안, 이제 `cannon_model.py`로 대체)과 컨베이어에 사진 재질을 입혀 렌더만 한다: 조형 메시는 스마트 UV → Cycles로 텍셀마다 위치 · 법선을 구워 numpy가 부위를 정해 `<이름>.png` + `_MS.png`로 칠함(경계가 곧음), 벨트 조각 8종은 원본 판 공간으로 UV를 되살려 판 조형 배치(테두리 · 리벳 띠 · 옆 타일 · 2 × 7 판자 · X · 별)에 맞춘 그림, 작은 부품은 이름으로 평면색. Unity 미적용 |
| `cannon_conveyor_unity.py` | 위 두 시안을 FBX 두 개에 넣는다(R371): 환경 FBX의 대포 몸체 · 팬 메시를 `cannon_model.py` 것으로(베벨 적용 + 퇴화 면 정리), 셔틀 FBX는 `rail_shoes.py`처럼 애니메이션째 다시 써서 벨트 UV · 판 텍스처 · 섀시 UV · 맵(`Assets/Resources/PawnRushLibrary/Conveyor/`)을 넣음. Unity 재질은 `Course01v4Builder.LibraryLooks.cs`가 재질 이름(`Library …`)으로 입힘. `-- --check <폴더>`로 다시 읽어 렌더, 다시 돌리면 그대로 |
| `armillary_look.py` | 출발 혼천의(`Start_armillary`)에 컨셉 사진 재질을 입혀 **렌더만** 한다(`-- --preview <폴더>`, 별자리 PNG와 렌더만 씀): 탁자 + 금 받침 한 덩어리 메시를 탁자 윗면 높이(혼천의 공간 z 0.888)로 나눠 나무 / 금, 링 3개 금, 구는 numpy로 그린 남색 별자리 지도(등장방형 2048 × 1024, 위도 보정 별)를 구 투영(중심 + 0.5)으로, 솔기 띠(반지름 > .339)는 금. 옆 작전 지도는 `war_map.py`로 같이 그림. R361, Unity 미적용 |
| `start_props_unity.py` | 위 세 시안(`compass_rose.py` · `war_map.py` · `armillary_look.py`)을 환경 FBX에 바로 넣는다(R364): 옛 납작 별 · 고리 · 지도 조형을 지우고 `Start_compass_rose` · `War_map_frame` · `War_map_parchment`를 더하고, 혼천의 받침 · 구에 UV를 펴, 색과 금속도 · 매끈함을 Cycles로 `Assets/Resources/PawnRushLibrary/StartProps/`에 굽는다(`<이름>.png` + `<이름>_MS.png`). Unity 재질은 `Course01v4Builder.LibraryStartProps.cs`가 노드 이름으로 입힘. `-- --check <폴더>`로 구운 결과 렌더, 다시 돌리면 그대로 |
| `inner_piers_stone.py` | 안쪽 기둥 10개(`V21/V23_Inner_gallery_pier`)를 승규 님 돌기둥 에셋(`../Endgame/source/stone_column.glb`)으로 바꿔 FBX를 바로 고친다(R315, 다시 돌려도 그대로) |
| `rail_shoes.py` | 셔틀 FBX의 바퀴 · 바퀴 받침을 지우고 레일 윗날개를 감싸는 미끄럼 슈 `Carriage_rail_shoe` 4개를 단다(R288, FBX를 바로 고침, 다시 돌려도 그대로) |

Unity가 읽는 곳(바뀌지 않음): `Assets/Resources/PawnRushLibrary/PawnRushLibraryEnvironment.fbx` · `PawnRushLibraryShuttle.fbx`
놓는 코드: `Assets/Scripts/PawnRush/Course01v4Builder.LibraryReplacement.cs`(충돌체), `LibraryShuttleDriver.cs`(셔틀 움직임)

## 1. 파일 안의 구성

| 컬렉션 | 내용 | 내보내기 |
|---|---|---|
| `PawnRushLibraryEnvironment` | 홀 · 바닥 · 계단 · 발코니 · 랜턴(빛 10) · 표시용 빈 물체(`START_spawn_zone`, `GOAL_capture_zone`, `BLOWER_*` …) 2926개 | → `PawnRushLibraryEnvironment.fbx` |
| `PawnRushLibraryShuttle` | 움직이는 컨베이어 130개. `CONVEYOR_CARRIAGE …` 빈 물체 아래가 같이 움직인다. **16초 왕복 = 1 ~ 385 프레임, 24 fps** | → `PawnRushLibraryShuttle.fbx`(애니메이션 포함) |
| `GRAYBOX`(R262) | 위 둘을 상자 376개로 줄인 그레이박스. `GB_Environment` · `GB_Shuttle`(같은 16초 왕복). 처음 열면 이것만 보인다 | **안 함**(아직 Unity에 안 넣음) |
| `UNITY_Collisions_ref`(초록 와이어) | Unity 코드가 더하는 충돌 상자 56개를 같은 자리에 그린 것(바닥 · 발코니 · 골 바닥 · 아래층 · 돌아가는 계단 32칸 · 착지대 · 골 계단 16칸 · 컨베이어 덱) | **안 함**(참고용, 선택 잠금) |

- 단위는 m, Z가 위. 모델의 출발은 −Y 쪽(`START_spawn_zone`, y ≈ −17.5), 골은 +Y 쪽(골 바닥 높이 1.2 m)이다.
- 좌표 대응: Blender (x, y, z) = Unity 코드 상자 좌표 (−x, −z, y). 그래서 코드의 "Left balcony"(Unity x −11.45)는 Blender **+X** 쪽에 있다.
- 재질은 `CLAY | uniform neutral gray` 하나뿐이다(원본 그대로). Unity는 재질을 이름으로 찾으니 이름을 바꾸면 Unity 쪽 재질도 새로 생긴다.

## 2. Blender에서 고치고 Unity로 보내기

1. `PawnRushLibrary.blend`를 Blender 5.2로 연다.
2. 모델을 고친다. **바닥 · 계단 · 발코니 높이나 위치를 바꾸면 초록 상자와 어긋난다** → 아래 4번(충돌체 맞추기)도 해야 한다.
3. 내보내기 — 둘 중 하나:
   - **버튼**: 아웃라이너에서 컬렉션(`PawnRushLibraryEnvironment` 또는 `PawnRushLibraryShuttle`) 선택 → 속성 창 **Collection** 탭 → **Exporters** → **Export**. 경로와 설정이 이미 들어 있다(`//../../../Assets/Resources/PawnRushLibrary/<컬렉션 이름>.fbx`).
   - **스크립트**(검사 포함, 둘 다 한 번에): Scripting 탭 → `export_library_fbx.py` 열기 → **Run Script**. 또는 명령줄:
     ```
     "C:/Program Files/Blender Foundation/Blender 5.2/blender.exe" --background ArtSource/PawnRush/Library/PawnRushLibrary.blend --python ArtSource/PawnRush/Library/export_library_fbx.py
     ```
     스크립트는 셔틀에 `CONVEYOR_CARRIAGE`가 없거나 fps가 24가 아니면 멈춘다.
4. Unity로 돌아가면 FBX가 다시 가져와진다(`.meta`는 그대로라 GUID · 가져오기 설정 유지). `SeoungTest_PawnRush` 씬에서 **다시 짓기** → Play.
5. `.blend`와 FBX를 **같이 커밋**한다(둘 다 LFS).

### 꼭 지킬 것

| 하지 말 것 | 이유 |
|---|---|
| `CONVEYOR_CARRIAGE …` 이름 바꾸기 · 지우기 | `LibraryReplacement`가 이 이름으로 시작하는 물체에 덱 충돌체를 붙이고 `LibraryShuttleDriver`가 그 속도를 잰다 |
| 셔틀 왕복을 1 ~ 385 프레임 밖으로, fps 바꾸기 | Unity는 클립 길이(16초)를 공유 시계로 반복한다. 끝 자세 = 처음 자세여야 이음매가 안 튄다 |
| 장면 단위(Unit Scale 1.0) · 내보내기 축 바꾸기 | Unity가 같은 크기 · 방향으로 읽으려면 원본과 같은 설정(−Z 앞, Y 위, 스케일 1)이어야 한다 |
| `UNITY_Collisions_ref`를 내보내기 컬렉션에 넣기 | Unity 충돌체는 코드가 만든다. FBX에 들어가면 보이는 상자가 생긴다 |

## 3. 충돌체 맞추기(바닥 · 계단을 옮겼을 때)

> R379부터 모델 자체도 충돌한다: `Course01v4Builder.LibraryCollision.cs`가 손 닿는 높이(모델 바닥 위 3.5 m 아래에서 시작)의 모든 부품에 메시 충돌(30 cm 미만 · 두께 8 cm 미만 바닥 장식 · 도는 부품 · `_zone` 표시는 뺌, 2만 삼각형 넘는 5 m 미만 소품은 상자)을, 컨베이어는 `Fixed_*`에만. 그래서 FBX 두 개 모두 `isReadable: 1`. 아래 상자들은 걷는 바닥 그대로 남는다.

Unity 충돌체는 FBX가 아니라 `Course01v4Builder.LibraryReplacement.cs`의 `Box(...)` 줄이 만든다. 모델을 옮겼으면:

1. Blender에서 새 바닥 윗면 높이 · 가운데 · 크기를 잰다.
2. Unity 좌표로 바꾼다: Unity (x, y, z) = (−Blender x, Blender z, −Blender y), 크기 (sx, sy, sz) = (Blender 폭 X, 두께 Z, 길이 Y).
3. C# 줄과 `build_library_blend.py`의 `unity_boxes()`를 **같이** 고친다(둘이 같은 목록이다). `SeoungTestCourse.Version`을 올리면 씬이 다시 짓기를 묻는다.

## 4. 처음부터 다시 만들기

FBX가 다른 데서 바뀌었으면(예: 다른 도구로 다시 뽑음) 작업 파일을 다시 만든다:
```
"C:/Program Files/Blender Foundation/Blender 5.2/blender.exe" --background --factory-startup --python ArtSource/PawnRush/Library/build_library_blend.py
```
Blender에서 고친 내용은 덮어써지니 먼저 FBX로 내보냈는지 확인한다.

## 5. 옮기면서 확인한 것(R261, Linux, Blender 5.2.3 `bpy`)

| 검사 | 결과 |
|---|---|
| 원본 FBX 헤더 | Blender 5.2.2 FBX 내보내기 기본값(Y 위 · −Z 앞 · UnitScaleFactor 1 · 물체 스케일 100), 셔틀 테이크 1/24 ~ 385/24초 |
| 되돌려 내보낸 환경 FBX vs 원본 | 물체 2926 · 지오메트리 1602 · 꼭짓점 505,263 · 빛 10 같음, 위치 차이 최대 0.000002 |
| 되돌려 내보낸 셔틀 FBX vs 원본 | 물체 130 · 커브 1170 · 테이크 구간 같음, 8개 시점 자세 차이 최대 0.00001 |
| 버튼(Collection Exporter)으로 내보낸 것 | 스크립트로 내보낸 것과 같은 결과 |
| 초록 충돌 상자 위치 | 위에서 광선을 쏴 모델 표면과 비교: 아래층 바닥 −8.0, 돌아가는 계단 첫 칸 −7.75 · 끝 칸 0.0, 착지대 −8.0 · 0.0, 골 계단 0.15 ~ 1.2가 모델 표면과 맞음 |
| 이름 차이 | 63자를 넘던 이름 2개(`Upper20_Goal_corner_lantern.001|GAME_KIT_…`, `V23_Inner_gallery_pier_center.001|GAME_GEO_…`)는 Blender 이름 한도로 끝이 잘린다. 다시 내보내면 Unity 노드 이름도 그렇게 바뀐다(코드가 찾는 이름 아님) |

**Unity에서 확인한 것은 아직 없다.** 내보낸 FBX는 저장소에 올리지 않았다(지금 Unity FBX = 승규 님 원본 그대로).

## 6. 그레이박스(R262, 2026-11-01)

승규 님 요청 "블렌더에서 그레이박스화". `build_graybox.py`가 자세한 모델을 지우지 않고 옆에 `GRAYBOX` 컬렉션을 만든다. 자세한 두 컬렉션은 눈 아이콘으로만 숨긴다(내보내기는 전과 같이 자세한 모델).

```
"C:/Program Files/Blender Foundation/Blender 5.2/blender.exe" --background ArtSource/PawnRush/Library/PawnRushLibrary.blend --python ArtSource/PawnRush/Library/build_graybox.py
```
명령줄로 돌리면 저장까지 한다. Blender 안에서는 Scripting 탭 → Run Script → 저장. **다시 돌리면 `GRAYBOX`를 지우고 새로 만드니, 그레이박스를 손으로 고친 뒤에는 다시 돌리지 말 것.**

| 규칙 | 값 |
|---|---|
| 상자 하나 = | 물체 하나(자기 방향 그대로의 경계 상자), 키트(빈 물체 + 모델: 책장 · 책상 · 기둥 · 룩 골 말 · 송풍기 …)는 통째로 하나 |
| 뺌 | 천장(아랫면이 11 m 위), 빛, 가장 긴 변 0.6 m 미만, 가장 얇은 변 3 cm 미만, 장식 이름(트레이서리 · 몰딩 · 랜턴 · 사슬 · 리벳 · 브래킷 · 깃발 · 리브 · 보 …), 같은 색의 더 큰 상자 안에 들어가는 상자 |
| 합침 | 같은 이름으로 붙어 있는 블록(벽돌 · 바닥돌 · 체스 칸)은 합친 상자가 60% 이상 차면 하나로 |
| 컨베이어 | `GB CONVEYOR_CARRIAGE` 빈 물체가 자세한 캐리지와 같은 동작(1 · 97 · 193 · 289 · 385 프레임 위치 같음)을 재생, 벨트 64조각 → Unity 덱 충돌체 크기(3.36 × 12.6 × 0.28 m) 상자 하나, 고정 레일 · 출발 경사로는 제자리 |
| 색(뷰포트 Solid → Material) | 바닥 밝은 회색 · 벽 짙은 회색 · 출발 초록 · 골 빨강 · 움직임/위험 주황 · 소품 파랑 |
| 결과 | 물체 약 2,900개 · 면 약 76만 → 상자 376개 · 면 2,256 |

렌더 4장(전체 · 출발에서 본 모습 · 위 · 옆)은 PNG가 LFS라 저장소에 없고 채팅으로 보냈다.
