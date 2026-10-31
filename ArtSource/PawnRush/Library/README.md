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

Unity가 읽는 곳(바뀌지 않음): `Assets/Resources/PawnRushLibrary/PawnRushLibraryEnvironment.fbx` · `PawnRushLibraryShuttle.fbx`
놓는 코드: `Assets/Scripts/PawnRush/Course01v4Builder.LibraryReplacement.cs`(충돌체), `LibraryShuttleDriver.cs`(셔틀 움직임)

## 1. 파일 안의 구성

| 컬렉션 | 내용 | 내보내기 |
|---|---|---|
| `PawnRushLibraryEnvironment` | 홀 · 바닥 · 계단 · 발코니 · 랜턴(빛 10) · 표시용 빈 물체(`START_spawn_zone`, `GOAL_capture_zone`, `BLOWER_*` …) 2926개 | → `PawnRushLibraryEnvironment.fbx` |
| `PawnRushLibraryShuttle` | 움직이는 컨베이어 130개. `CONVEYOR_CARRIAGE …` 빈 물체 아래가 같이 움직인다. **16초 왕복 = 1 ~ 385 프레임, 24 fps** | → `PawnRushLibraryShuttle.fbx`(애니메이션 포함) |
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
