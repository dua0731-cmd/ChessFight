# 마차뜰 블록아웃 → Blender 모델 작업 기록 (seoungtest 브랜치, R677–R680 + 진행 중인 QA)

> 이 파일만 읽으면 이어서 할 수 있게 쓴 작업 일지다. 아래 "이어서 할 일"을 먼저 보고, 그 밑의 날짜별 기록(영어, 작업 세션의 노트 그대로)을 필요할 때 본다.
> **사용자(승규)가 Unity에서 본 것은 아직 없다.** AI가 Play 화면 · 광선 검사 · 정지 화면으로만 확인했다.
> 번호 주의: main에도 R677–R682가 따로 있다(다른 작업). 이 기록의 R 번호는 seoungtest 브랜치 안에서만 쓴 번호다.

## 지금 상태 (2026-11-16)

- 소스: `ArtSource/PawnRush/CarriageYardModels/*.py` (Blender 5.2에서 exec). 공용 도구 `rcy_kit.py`, 내보내기 `export_rcy.py`.
  구역별: `canal_model.py`(2 운하 · 물레방아), `entrance_model.py`(1 마당), `movers_model.py`(마차 · 바퀴 · 문짝), `horse_model.py`(말, 리그),
  `sorting_model.py`(3 고정물), `sorting_machines_model.py`(3 기계 15개), `platform_model.py`(4 고정물), `castle_model.py`(성문 · 구간 끝, AI 키트),
  `scenery_model.py`(흉벽 톱니 · 가로등 · 까치발 띠). 작업 파일 `CarriageYard_v2.blend`(LFS, 92 MB).
- 결과물: `Assets/Resources/PawnRushCarriageYard/*.fbx`(33개, LFS). 붙이는 코드: `RoyalCarriageYard.Dressing.cs`(ZoneModel 표 · DressMovers),
  `RoyalCarriageYard.Castle.cs`(성 모델 · 상자 콜라이더 440개, `// <castle boxes>` 사이는 `castle_sync_cs`가 생성), `YardHorse.cs` · `YardHorseRig.cs`.
- 규칙(승규): 상자 콜라이더만, 폰이 닿는 면은 모델을 콜라이더에 맞춤(~10 cm 넘게 어긋나면 고치거나 보고), 게임 폭 · 틈 안 바꿈, 신호(물 · 폭포 · 거품 ·
  금 테두리 · 표지 · 등불 · 빨강/초록 표시 · 결승선 · 진행 막대)는 절대 안 가림, 성 키트 말고는 회색 클레이(텍스처 없음), 맵 밖은 모델링 안 함, 정지 화면으로 보고.
- Unity 확인은 이 폴더에 열린 에디터 `ChessFight-SeoungTest@5654cdc8`만 쓴다(main 에디터 C:/Work/ChessFight 금지). 장면 `Assets/Scenes/PawnRush_MapTest.unity`.
- **seoungtest에서 Unity를 열면 마차뜰만(11-16, 승규 "유니티에 안 보여 마차뜰만 띄우게"):** 맵 시험 장면이 기본 시험장(코드)부터 열어서 이 PC처럼 지난 맵
  기록이 없으면 마차뜰이 안 보였다 → 장면의 `PawnRushMapTest`를 `includeCalibrationYard 0` · `onlyMap RoyalCarriageYard`(새 필드, 비우면 예전처럼 전부)로,
  편집기 도구 `PawnRushEditor/SeoungTestYardOnly.cs`가 편집기를 켤 때(세션당 한 번, 저장 안 한 장면이 있으면 안 바꿈) 이 장면을 열고 어느 장면에서 Play해도
  이 장면으로 시작(`playModeStartScene`). 끄기: `ChessFight > Pawn Rush > Carriage Yard Only (seoungtest)`. 마차뜰은 Play 때 지어지므로 편집 모드에서는 빈 장면이다.
  main으로 옮길 때는 이 파일과 장면의 두 값을 빼야 한다.
- **다른 PC에서 이어 하기(11-16, HOSEO PC `C:\Users\HOSEO\Documents\GitHub\ChessFight`에서 처음 세팅):** 스크립트의 프로젝트 경로는
  `RCY_ROOT` 하나(exec 네임스페이스 또는 환경 변수, 없으면 처음 PC의 `C:/Work/ChessFight-SeoungTest`) — 예: `g = {"__name__": "rcy", "RCY_ROOT": r"C:/Users/HOSEO/Documents/GitHub/ChessFight", ...}`.
  이 PC 세팅: Blender 5.2.3 LTS(winget, 같은 5.2 줄), BlenderMCP 애드온 2.0(`addon.py`, github.com/ahujasid/blender-mcp, 켜 두면 Blender를 열 때 localhost:9876 자동 시작, 사용 데이터 전송 꺼짐),
  uv(winget) + Claude Code 로컬 MCP 서버 `blender`(`uvx --from blender-mcp==2.0.0 blender-mcp`), 스킬 94개(github.com/arjun988/blender-skills의 `.claude/skills`를
  프로젝트 `.claude/skills`에 복사, git에 안 올림 — `.git/info/exclude`). 새 세션부터 `mcp__blender__*` 도구와 스킬이 잡힌다. Blender 없이 일괄 작업은
  `blender -b --factory-startup --python <스크립트>`로 해도 된다(이번 재출력 · 측정이 그렇게 함).

## 내보내기 순서 (새 Blender 네임스페이스로)

```
g = {"__name__": "rcy", "RCY_BUILD": False}
exec(rcy_kit.py, g); exec(export_rcy.py, g)        # + 필요한 앞 파일들 (각 .py 첫 줄 주석에 적힘)
g["RCY_BUILD"] = True; exec(<구역>_model.py, g)
export("RCY Entrance", "RcyEntrance")  # 또는 platform_export() / scenery_export() / castle_export(...)
```
- sorting = rcy_kit → entrance(False) → sorting. scenery = rcy_kit → export_rcy → entrance → sorting(False) → scenery.
- 내보내기 전에 모든 컬렉션이 보이게 할 것(숨기면 빈 FBX가 나갔음 → 이제 `export()`가 오류를 냄).
- Blender (x, y, z) = Unity (x, z, y). 3–4구역은 코드가 자기 좌표(LaterShift -24)로 짓지만 모델은 맵 좌표로 낸다.

## 다음 작업 메모 (2026-11-16 HOSEO PC에서 끝냄 → 다른 PC에서 이어 가기)

**시작:** `git pull`(seoungtest, 이 메모가 든 커밋). 처음 PC(`C:/Work/ChessFight-SeoungTest`)는 스크립트의 기본 경로 그대로라 `RCY_ROOT` 없이 돈다.
QA 도구는 `ArtSource/PawnRush/CarriageYardModels/qa/`(README에 쓰는 법, 경로는 파일 위치에서 찾음). 스킬 94개 · Blender MCP는 PC마다 세팅(위 "다른 PC에서 이어 하기").
**Unity가 Play 중일 때 스크립트 · FBX를 쓰지 말 것:** Play 도중 재컴파일돼 래그돌 static 마찰 재질이 비고 넘어질 때 NullReferenceException이 났다
(11-16, 래그돌 버그 아님 · 빌드 무관 · main 코드도 같음). Play를 껐다 켜면 사라진다. 막으려면 Unity Preferences > General > Script Changes While Playing을
"Recompile After Finished Playing"으로(PC마다 설정).

1. **먼저 Unity에서 볼 것(사람 확인 전 — 11-16 커밋 전부):** `PawnRush_MapTest` Play(seoungtest는 켜면 이 장면, Play하면 마차뜰만).
   - 판석 이음 · 돌벽 이음이 전과 같은지(안 보이는 면을 지움), 둥근 기둥 · 통 테두리 · 마차 바퀴 셰이딩, 마당 양 끝(x ±19 ~ 22) 새 판석 띠.
   - 말: 질주 때 배 · 엉덩이 · 엉덩이끈이 자연스러운지, 약 14 m에서 LOD1로 바뀌는 순간이 눈에 띄지 않는지, 화면 가장자리에서 말이 깜빡이지 않는지.
   - 성: 텍스처가 BC1로 들어왔는지(Inspector 형식), 구간 끝 난간 끝(구간 모드 `Early_1-2` 프리팹에서만 지어짐 — 맵 모드에는 구간 끝 없음).
   - 콘솔 빨간 오류 0인지. 보고 나면 VALIDATION 표에 올리고 main으로 옮길지 정한다.
2. **큰 모델 나누기(Major, lod-pipeline · scene-assembly):** Entrance 18.3만 · Sorting 24.2만 · Canal 11만 삼각형이 각각 한 덩어리라 Unity가 화면 밖을
   잘라내지 못하고 LOD도 못 쓴다(정원 경량화 때 확인한 문제). 각 `build_*`의 마지막 `join`을 z 10 m 안팎 칸마다 따로 해 한 FBX 안에 여러 메쉬로 내고
   (`ZoneModel`이 FBX를 통째로 걸므로 코드 변경은 거의 없음 — 확인 필요), 칸마다 베벨을 끈 LOD1(`bevel_mod` 없이 다시 짓기)을 검토. 나눈 뒤 `render_diff.py`로 전후 비교.
3. **FBX 가져오기 설정(.meta, 33개 — Unity에서 확인하며):** `isReadable 1 → 0`(메쉬 CPU 사본 약 44 MB, 코드에서 읽는 곳 없음 — grep 확인됨),
   클레이 모델은 노멀 맵이 없으니 탄젠트 계산 끄기(성 3개는 노멀 맵이 있어 그대로). `materialImportMode`는 그대로(슬롯 이름을 `DressHitch` · `HorsePaint` ·
   나중 색 작업이 씀). `export_rcy.py`의 .meta 템플릿도 같이 바꿔야 다음 내보내기에서 안 돌아간다.
4. **세워 둔 마차 콜라이더(승규 결정):** 상자 2.3 × 2.75 × 4.2 m 안에서 모델이 옆 15 ~ 21 cm · 뒤 35 cm 떨어져 있고 바퀴 사이(높이 0.3 ~ 0.9 m)가 빔
   (블록아웃 때부터, 마차 밑에 끼지 말라고 상자를 꽉 채운 것). 그대로 / 모델에 옆 판 · 뒤 짐칸을 붙여 상자를 채우기(`movers_model.coach`, 모양만 바뀜) 중 고르기.
5. **말 남은 것:** 서 있는 발이 미끄러짐(최대 28 cm, `YardHorse.Pose`에 디딘 발 고정 — 애니메이션이 바뀜), 앞무릎 105° 접힘 때 단면 64% · 겹침 87삼각형
   (Forearm / Cannon 가중치 경사 넓히기), 재갈 고리가 주둥이에 반쯤 묻힘(`tack()` 붙임 거리), 깃털 장식 꼭대기 2.38 m가 `YardHorse.Height` 1.95보다 높음.
6. **성 남은 것:** 계단 텍스처가 12 m에 한 장이라 가로로 8:1 늘어짐(세 장으로 나누면 이음에 검은 틈 → UV만 다시 펴거나 계단만 반복 텍스처로),
   탑 텍스처는 8.5% · 벽 텍스처는 39%만 씀(UV 다시 짜고 굽기), 성 텍스처 밀도 30 ~ 100 px/m(스킬 기준 256 ~ 512의 아래 — 승규가 키트 텍스처 유지로 정함).
7. **원래 남은 보이는 것:** 깃발은 코드의 파란 천(파란색 없애는 색 작업), 최종 성벽 가운데 기둥 음영 이음, 표지 글씨가 돌 위에서 어두움,
   "1. 짐마차 마당" 표지가 닫힌 문과 겹침, 최종 문짝 뒷면의 글씨 같은 무늬.
8. **main으로 옮길 때:** `PawnRushEditor/SeoungTestYardOnly.cs`와 `PawnRush_MapTest.unity`의 `includeCalibrationYard 0` · `onlyMap`은 빼고(seoungtest 전용),
   [PITFALLS R507](../Environment/PITFALLS.md) 순서(seoungtest 계열 병합)를 따른다.

## 이어서 할 일 (진행 중이던 것)

1. **스킬 기반 QA (승규 "텍스쳐는 입히지 말고 스킬 써서 기존에 있는 거 해봐", "블렌더 94개 스킬 극한으로 사용해").** `.claude/skills`의
   qa-review · blender-modeler 정리 목록 · asset-optimization · lod-pipeline · environment-artist · set-dressing · hard-surface 등을 차례로 적용.
   - 측정 결과(현재 FBX): Entrance 218k 삼각형(넓이 0 면 396), Canal 126k(8), Sorting 278k(1,327), Platform 51k, Finish court 28k, Section floors 15k,
     Scenery 26k / own 35k, 성 OuterGate 43k · FinalGate 38k · SectionEnd 55k(AI 키트 잘린 면 비다양체 모서리 1,626). 떨어진 점 · 선 없음.
   - **이미 한 것:** `rcy_kit.join()`에 `bmesh.ops.dissolve_degenerate(dist=1e-5)` 정리 단계 추가, 8개 고정물 FBX(Entrance · Canal · Sorting · Platform · FinishCourt · SectionFloors · Scenery · SceneryOwn)를 다시 내서 넓이 0 면 0개(Sorting 277.5k → 274.2k). Unity Play에서 오류 없이 불러옴(AI 확인만). 참고:
     전체를 한 번에 다시 지으면 몇 분 걸려 MCP 응답이 끊긴 것처럼 보이지만 빌드는 끝난다.
   - **(11-16 HOSEO PC) 기계 · 마차 9개도 다시 냄:** 33개 전부를 읽기 전용으로 잰 결과 넓이 0 면이 남은 것은 Coach 91 · ConveyorCrate 32 · ConveyorStack 38 · StackRow 156 · StackCrate 54 · RowTrolley 128 · SlamTrolley 112 · SlewJib 80 · HoistFrame 56 → 소스에서 다시 지어(정리 단계 포함) 이 9개만 다시 냄. 이전 커밋 FBX와 비교: 표면 넓이 · 크기 · 점 위치 차이 0(양방향 최근접 거리 0), 재질 칸 그대로, 삼각형 2,306개 줄어듦. **지금 33개 합계 1,049,494 삼각형, 넓이 0 면 · 떨어진 점 · 선 · 3면 모서리 0.** 성 · 말 · 바퀴 · 문짝 · 고리는 원래 넓이 0 면 없음. Unity에서 다시 불러 본 것 없음(이 PC는 Unity MCP 연결 전).
   - **남은 열린 모서리:** `RcyCastleSectionEnd` 1,626개 = 14고리. 마지막 계단 디딤판 4개(높이 7.0 ~ 8.0 m, 고리마다 31 m)와 양쪽 난간 토막 10개(x ±5.94 ~ 6.50, 아랫단 z 7.0 / 8.0). 1 cm까지 붙여도 1,501개라 이음새가 아니라 잘린 아래쪽이 열린 것. 폰 눈높이 다섯 각도(계단 위 · 아래 · 낮게 뒤 · 난간 동 · 서) 마젠타 배경 · 뒷면 숨김 렌더에서 비쳐 보이는 틈 없음(`scratchpad` 렌더, 커밋 안 함). 막으려면 `castle_model.py` `part()`의 자른 면 마감(cap)을 계단 · 난간에도 — 성 내보내기는 `RoyalCarriageYard.Castle.cs`의 상자 표도 다시 쓰므로 그때 같이.
   - **(11-16 HOSEO PC) 스킬 QA 2차 — 네 갈래로 나눠 잼(읽기 전용 측정 → 고침 → 다시 냄):** 고정물 8개(asset-optimization · lod-pipeline ·
     environment-artist · set-dressing · collision-proxy · scene-assembly), 성 3개 + 텍스처(hard-surface · retopology · uv-workflow · texture-workflow ·
     materials · lookdev · collision-proxy), 움직이는 모델 22개 + FBX 가져오기 설정 33개(vehicle-artist · prop-artist · hard-surface · collision-proxy ·
     unity-export · export-pipeline), 말(creature-artist · rigging · animation · unity-export · stylized-style · cartoon-style), 공통 qa-review ·
     validation-checklist · polycount-budgets · naming-conventions, 순서는 blender-director. 측정 자료 · 렌더는 그 세션 scratchpad(커밋 안 함).
     - **고친 것(소스 → 클레이 FBX 30개 다시 냄, 성 3개는 그대로):**
       1. `rcy_kit._box_bm`: 최소 > 최대로 들어온 상자가 거울상이 돼 면이 안을 봄 → 크기 절댓값(거대 물레방아 −y 쪽 쇠판 · 테 12조각 528삼각형).
          `rcy_kit.turn_outward`: 닫힌 조각의 부피가 음수면 뒤집음(합치기 전 · 넓이 0 정리 뒤 두 번) — Entrance 1 · SceneryOwn 5 · Sorting 48조각도 바로잡음. **33개 모두 뒤집힌 조각 0.**
       2. `rcy_kit.flagstones`: 판석 밑면 · 판석끼리 맞닿는 옆면 · 그 사이 아래 모따기를 지움(`hide_sides` → `join()`의 `cull_hidden_sides`), 포장 가장자리 · 윗면 · 윗 모따기는 그대로.
          `rcy_kit.ashlar`: 같은 단 블록 사이의 평평한 이음면만 지움(모따기 · 윗면 · 아랫면까지 지웠더니 이음 모서리마다 하늘이 비쳐 되돌림).
          난수 순서는 그대로라 판석 · 블록 배치는 이전과 같다.
       3. 셰이딩: `cylinder` · `arc_ring`(→ `ring_x`) · `ellipse_ring`은 둘레를 매끈하게, 마구리 90° 모서리는 각지게(`sharp_creases`, 65°), 크레인 드럼(`lathe(sharp=True)`)의 홈도 각지게.
          육각 볼트는 각진 채(`smooth=False`). 처음에 이 규칙을 모든 모델에 걸었더니 곳간의 둥근 짐 꾸러미가 각진 판처럼 망가져 해당 도구에만 걸도록 좁힘.
       4. 짐마차 마당 양 끝(x ±19 ~ 22, z 2.0 ~ 2.6 · 47.6 ~ 50)에 판석 띠 4개(`build_entrance`, 따로 난수) — 콜라이더 위가 6 cm 낮던 곳.
       5. 말(`horse_model.skin`): 마구(Harness) · 안장(Saddle)에서 꼬리 · 목 뼈 가중치를 뺌(엉덩이끈이 꼬리 따라 20 ~ 23 cm 흔들렸음),
          모든 메쉬를 한 점에 뼈 4개 · 합 1로(`limit_weights`; 전에는 최대 7개 · 합 0.89 ~ 1.0, Unity가 4개로 자르면 배가 4.7 cm까지 어긋났음).
     - **결과:** 33개 합계 1,049,494 → **947,911 삼각형(−9.7%)**: Entrance 217,296 → 183,066, Sorting 274,160 → 242,125, Canal 126,244 → 109,490,
       Platform 50,744 → 43,339, FinishCourt 28,068 → 21,386, SectionFloors 15,312 → 10,835(나머지 수는 그대로, 셰이딩만). 넓이 0 면 · 떨어진 점 · 선 · 3면 모서리 0.
       같은 카메라 16곳(폰 눈높이 14 + 조감 2)에서 이전 커밋과 비교 렌더(회색 · 뒷면 숨김 · 마젠타 배경): 새로 비쳐 보이는 화소 0, 바뀐 화소 0 ~ 0.16%
       (차양 마룻대 · 통 테두리의 매끈해진 셰이딩, 새 판석 띠).
     - **판정표(qa-review):**

       | 묶음 | 판정 | 남은 것(심각도) |
       |---|---|---|
       | 고정물 8개 | SHIP WITH NOTES | (Major) 모델 하나가 한 덩어리(최대 18만 삼각형)라 Unity에서 화면 밖 잘라내기 · LOD가 안 됨 → 10 m 안팎 조각으로 나누기 검토. 벽 뒷면 · 맵 밖 면(카메라에 안 보임 추정 수만)은 카메라가 벽을 뚫을 때를 몰라 안 지움. (Minor) 운하 · 승강대 난간 기둥 8각 13단(약 4.5만) 줄일 여지 |
       | 성 3개 | SHIP WITH NOTES | (Major) SectionEnd 난간 3.56만(모델의 65%) — `build_castle` 난간 `part(ratio=.5)` → ~.2로 2.1만 줄일 수 있음; 계단 텍스처가 가로로 8:1 늘어짐(12 m에 한 장) → 4 m씩 3장. (Minor) 열린 고리 14개 중 동쪽 띠 난간 끝(x 6.0 ~ 6.5, 높이 7.0 ~ 8.05, z 181)만 구간 출구 쪽에서 보임 → `part()` 자른 면 마감. 텍스처 PNG 알파 → BC3(알파 끄면 메모리 절반), Towers 텍스처는 8.5%만 씀. **성은 다시 내면 `RoyalCarriageYard.Castle.cs` 상자 표 440개도 다시 써서 이번에 안 건드림** |
       | 움직이는 모델 22개 | SHIP WITH NOTES (물레방아 NO-SHIP → 고쳐서 SHIP) | 회전축 · 경첩 위치 모두 맞음(바퀴 무게중심 2 mm 안). 맵 위 합계 32만 삼각형 · 그리기 382번(재질 칸 수 × 개수; 칸을 하나로 하면 102번이지만 나중 색 작업에 칸 이름이 필요). (Major, 결정 필요) 세워 둔 마차 콜라이더 상자 안이 옆 15 ~ 21 cm · 뒤 35 cm · 바퀴 사이 비어 있음(블록아웃 때부터, 마차 밑에 끼지 말라고 상자를 꽉 채운 것) |
       | 말 | SHIP WITH NOTES | (Major) 33.2k 삼각형 × 10마리, LOD 없음 → 7 ~ 8k LOD1 제안. 다리 뼈 가중치가 배까지 번져 질주 때 배 아래가 21 cm 움직임(가중치 영역 나누기 필요, 미처리). (Minor) 서 있는 발이 미끄러짐(최대 28 cm, `YardHorse.Pose`), 앞무릎 접힘 때 단면 64%, 재갈 고리가 주둥이에 반쯤 묻힘, 피부 메쉬 경계가 자세에 따라 0.55 m 넘침(`DressHorse`의 `localBounds`) |
       | FBX 가져오기 설정 33개 | 제안만 | `isReadable 1`(메쉬 CPU 사본 약 44 MB, 코드에서 읽는 곳 없음 → 0 제안), 클레이는 노멀 맵이 없어 탄젠트(Mikk) 불필요. **Unity에서 확인할 수 없어(이 PC는 Unity MCP 없음) .meta는 안 바꿈** |
   - 다시 낸 뒤 Unity에서: 콘솔 오류 없는지, 바닥 · 짐 · 말이 전과 같아 보이는지(특히 판석 이음 · 돌벽 이음 · 둥근 부품 셰이딩 · 말 엉덩이끈).
   - **(11-16) 성 묶음:** `castle_model.part(close=True)` — 자른 뒤 남은 구멍을 모두 막음(경계 점 5e-4로 붙이고 `holes_fill`, 오목한 뚜껑은 귀 자르기로 삼각형,
     법선 다시 계산). 난간에 걸어 구간 끝 열린 모서리 1,626 → 536(남은 것: 계단 디딤판 4장의 열린 판 — 끝이 기둥 · 난간 속, 난간마다 6 cm 틈 하나).
     구간 출구 쪽에서 속이 보이던 난간 끝이 이제 막혀 보임. 54,596 → 55,674 삼각형(뚜껑). 성 텍스처 5장(바탕색)은 알파가 전부 1이라 `alphaUsage 0`
     (.meta 5개 · `_castle_png`) → Unity가 BC1로 압축하면 메모리 절반(Unity에서 형식 확인 전). **콜라이더 상자 440개 그대로**(C# 표 · json 변경 없음),
     뒤집힌 조각 0. 해 봤다가 되돌린 것: 난간 줄이기(절반 → 4분의 1, 1.8만 줄지만 폰 눈높이에서 난간 기둥이 울퉁불퉁 · 손잡이가 가늘어짐),
     계단을 4 m씩 세 장으로(텍스처 늘어짐은 풀리지만 이음마다 검은 틈) — 둘 다 렌더로 보고 되돌림.
   - **(11-16) 말 묶음:** `horse_model.mask_skin` — 뼈 열(bone heat) 가중치를 뼈마다 제 영역에만 남기고(다리 뼈: 같은 쪽 · 몸통 가운데 빼고 · 위
     다리 뼈는 어깨 / 엉덩이 관절 위 빼고, 꼬리 뼈: 꼬리뿌리 10 ~ 20 cm 안, 목 뼈: 기갑 앞 y .45 ~ .6부터), 뺀 몫은 Root(몸통)로, 경계는 15 ~ 20 cm에
     걸쳐 흐려짐. 잰 것(QA 도구 그대로, 바꾸기 전 → 뒤): 다리가 반대쪽 몸을 움직임 3.2 / 6.1 cm → 0, 앞다리가 움직이는 범위 y .01 ~ .97 → .25 ~ .94 ·
     높이 1.48 → 1.29 m, 뒷다리 y −1.0 ~ .13 → −1.0 ~ −.28, 배띠 밑 13.7 → 11.2 cm, 꼬리가 엉덩이를 움직임 15 ~ 17.6 cm → 0(꼬리 32°에서 움직인 점
     729 → 150), 목이 마구 패드 · 안장 · 등을 움직임 4 ~ 10 cm → 0, 질주 때 몸 부피 변화 −45 ~ +90 L → −38 ~ +65 L, 서 있는 모양 그대로(912.4 L).
     **LOD1**(`horse_lods` · `HORSE_LOD1`): 메쉬마다 줄인 사본 `*_LOD1`(가중치 유지 · 뼈 4개), 원본은 `*_LOD0` — 팀 말 33,236 → 8,960 삼각형.
     Unity: `DressHorse`가 이름 끝 `_LOD1`로 LOD 묶음을 직접 짬(가져오기가 만들었든 아니든: 둘이 겹쳐 그려지지 않게) — 화면 높이 12% 아래(약 14 m)부터
     LOD1, 멀어도 안 사라짐. 피부 메쉬 경계를 1.2 m 넓힘(질주 때 다리 · 꼬리가 0.55 m 넘어 화면 가장자리에서 사라질 수 있었음), Harness · Saddle은
     이름 앞부분으로 찾음. Blender 렌더(질주 옆 · 엉덩이 · 14 m LOD1)로 확인, Unity Play로는 못 봄.
     남은 것: 서 있는 발 미끄러짐(`YardHorse.Pose`, 애니메이션 변경), 앞무릎 접힘 단면(겹침 87삼각형 그대로), 재갈 고리 묻힘.
2. 남은 보이는 것: 깃발은 코드의 파란 천(파란색 없애는 색 작업은 나중), 최종 성벽 가운데 기둥 음영 이음, 표지 글씨가 돌 위에서 어두움,
   "1. 짐마차 마당" 표지가 닫힌 문과 겹침, 최종 문짝 뒷면의 글씨 같은 무늬.
3. 승규가 Unity에서 보고 확인하면 VALIDATION 표에 올리고 main으로 옮길지 정한다.

## 날짜별 기록 (작업 세션 노트 그대로)

No files in the repo touched. Plan given to 승규 in the session reply (Korean). Key facts found:

- Attach pattern to mirror: RoyalWaterGardenBuilder.Concept.cs (ConceptPiece: model / covered / hung / removed;
  pivot rotated 180 about y; FBX from ArtSource/PawnRush/GardenConcept/export_concept.py into Assets/Resources/...,
  deterministic .meta GUIDs). Grand stair hall: HideListed + RideMover (Course01v4Builder.GrandStairHallModel/Balcony).
- RoyalCarriageYard.Awake builds everything under an inactive "Built" root, then SetActive(true): the dressing pass
  belongs right before that line (same as the garden's content-built-inactive rule).
- Zones 3-4 are built in their own frame ("3-4 (own frame)", LaterShift -24) and stretched by SortGrow 17: export zone
  FBXs in MAP frame and parent them to the Built root, not to `later`.
- Moving obstacles already keep visuals and "(collider)"/"(hit)" objects separate (SlewJib, ConveyorCrate, StackCrate,
  SlamChest, HorizontalWaterwheel spokes) -> hide renderers under the moving transform, hang a per-type prop FBX there.
- Animated sub-parts the model must split to: YardHorse Body / Neck / Leg pivots, RoamingCarriage wheels
  (YardKit.Carriage returns wheel transforms), TimedGate / PushGate leaves (hinges), VerticalWaterwheel paddles + gold
  grab handles (handles must sit on the grab colliders).
- Waterwheel: axle (0, 5.27, 66), radius 9, depth 1 m, hub radius 1.1 / length 1.4 (z 65.3-66.7); walkways |x| 3.0-6.2,
  gap z 64.3-67.7. A centre post IN the wheel plane would be hit by spokes; posts outside the faces at x=0, z~64.9/67.1
  (between the walkways, no collider) fit U2.
- Timber load collider 0.13-0.55 m above floor is the obstacle; U3's high-hanging timber would change gameplay.
- Signals drawn by code (red bands, green gap floors, red sectors, lamps, progress bar) must never be covered.

Update after the folder was cut to the carriage yard only (message from the starting session):
- Unity checks go to the editor open on THIS folder: UnityMCP instance ChessFight-SeoungTest＠5654cdc8 (port 6400),
  pinned with set_active_instance before any call; never the main editor at C:/Work/ChessFight.
- Collider/pivot guide: read straight from the running Play scene via UnityMCP (execute_code over the built
  RoyalCarriageYard colliders -> JSON in the scratchpad/ArtSource), so no editor dump script is needed.
- Only scene left: Assets/Scenes/PawnRush_MapTest.unity (calibration yard off). RoyalCarriageYard_Obstacles.prefab is
  gone, so no bench check; the section prefab (Early_1-2) builds the same component, so the dressing still applies there.

승규 11-15: nothing outside the map is modelled (stage 6 dropped). The code-built walls / rook towers / palace /
hills stay as they are (their blue cone roofs and banners included unless he asks for a recolour).

Decisions asked of 승규: open wagon vs closed coach; waterwheel centre post; timber high vs low; no-blue palette;
pilot = giant waterwheel + canal.

## Pilot done (2026-11-15, uncommitted, AI-checked in Play only - 승규 has not seen it)
- Files: ArtSource/PawnRush/CarriageYardModels/{rcy_kit.py, canal_model.py, export_rcy.py, CanalWheel_v1.blend,
  guides/rcy_built_play.json, stills/}; Assets/Resources/PawnRushCarriageYard/{RcyCanal, RcyGiantWaterwheel}.fbx (+meta,
  folder meta); Assets/Scripts/PawnRush/RoyalCarriageYard.Dressing.cs (new) + RoyalCarriageYard.cs (`models` toggle,
  Dress() in Awake's try, DressMovers() after SetActive(true)).
- Play: 85 canal blockout renderers + 209 wheel renderers stop drawing; colliders untouched. Water, flume water, pour,
  spill, foam, gold gap edges and signs stay blockout.
- Centre-post decision is moot: the blockout already has two solid axle posts (x 0, z 64.8 / 67.2); the model dresses them.
- Known look issue: Built/Scenery merlons (y 5.0-5.8) sit on the canal walls and stay blockout (outside-the-map rule),
  so beige merlons on grey clay until a colour pass.
- Next zones: 1 (sheds, outer gate, carriage + horses on YardHorse pivots), 3 sorting yard, 4 pond + vertical wheel
  (handles on grab colliders), 5 gate + section end. Docs (ROYAL_CARRIAGE_YARD §20, HANDOFF, REQUIREMENTS) and tests
  still to do at commit time.

## Zone 1 done (2026-11-15, uncommitted, AI-checked in Play only - 승규 has not seen it)
승규 11-15: "닫힌 마차. 낮게. 유지 해서 다음 구역 진행 해" = closed coach, timber low (zone 3), keep no-blue (colour pass later).
- New source: entrance_model.py (RCY Entrance, map frame), movers_model.py (coach, wheel, horse body/harness/neck/fore
  leg/hind leg, hitch, gate leaves W/E, each in its pivot's frame); shared helpers moved into rcy_kit.py;
  CarriageYard_v2.blend. Exports: RcyEntrance, RcyCoach, RcyCoachWheel, RcyHorse{Body,Harness,Neck,ForeLeg,HindLeg},
  RcyHitch, RcyGateLeaf{W,E}.
- Dressing.cs: zone "1 Entrance" keeps Shed lamp + Charge line*; parked carriage dressed in Dress(); DressMovers dresses
  every RoamingCarriage (coach on each Carriage, wheels on Wheel pivots, horse parts on Body/Neck/Leg pivots, hitch on
  teams) and the start gates' TimedGates. Lamps kept and scaled to the roaming 1.8 size.
- Play: zone 1+2 Dress hid 389 renderers, DressMovers 639; 110 model pivots (6 coaches, 24 wheels, 10 horses, 5 hitches,
  2 leaves, 1 wheel, 2 zones). Wheels roll and legs stride (checked rotations over time); 24 lamps on; charge lines on.
  Model triangles drawn ~643k total (entrance 281k, canal 126k, wheel 20k, teams ~37k each).
- Look note: Built/Scenery/Banner (blue, x ±10/±17 z 2.1, yard face of the gate wall) and Scenery merlons stay
  blockout on top of the clay model.
- Stills: stills/rcy_r678_entrance_gate(_blockout), yard_sheds, parked_coach(_front), team_close, gate_leaves.
- Peer (R677 ModelingRefs session) relayed 승규's 11-15 decisions (ANALYSIS.md §4): closed royal coach with shutters
  not glass; no waterwheel centre post (axle floating); timber load low (0.13-0.55 m). Coach glass replaced by louvred
  wooden shutters (movers_model.shutter), RcyCoach re-exported (11.3k tris); still rcy_r678_parked_coach_shutters.png.
  The pilot's wheel only dresses the code's own axle posts/bearings - no centre support post was modelled.

## Horses redone (2026-11-15, uncommitted, AI-checked in Play only - 승규 has not seen it)
승규's three asks, in order: a natural running horse like his cartoon picture; white/black/brown coats "very detailed";
then a picture of two chunky toy royal horses + "이런식으로 좀 투툼하게 만들어주고 말굽, 장식 등등 표현해줘". The style is
now chunky/stylized (not realistic), in colour (his ask; the rest of the yard stays HallClay).
- Source: ArtSource/.../horse_model.py (SDF-fused chunky body, big hooves, mane/forelock/tail locks, bridle with rosettes,
  cream plume, breast collar, girth; harness = pad/terrets/side straps/breeching/crupper; saddle = red cloth, gold rim,
  seat, stirrups). Rigged (Root/Neck/Head/Tail1-3 + 4x4 leg bones), weighted. One FBX: RcyHorse.fbx (Generic rig,
  animationType 2 - with None Unity drops the skin weights), 4 skinned meshes Horse 23k / Tack 7.1k / Harness 3.1k /
  Saddle 3.4k tris. The five pinned parts (RcyHorseBody/Neck/ForeLeg/HindLeg/Harness .fbx) and their builders in
  movers_model.py are deleted.
- Code: YardHorse.cs rewritten (same API; pivots now at the model's joints, 4 joints per leg, 3 tail; trot<->gallop
  gait by stride and ground covered, rear kept). New YardHorseRig.cs (LateUpdate, order 10000) turns each bone with its
  pivot. Dressing.DressHorse hangs RcyHorse on every YardHorse Body (coat read off the blockout barrel; Harness for
  team horses, Saddle when the blockout has a "Saddle cloth"), HorsePaint colours by slot.
- Hitch: RcyHitch traces now run from the swingle-tree to the horse's own side straps (no longer through the chunkier
  body), pole straps to the breast collar; DressHitch paints its leather slot like the tack. RoamingCarriage.LateUpdate:
  turning on the spot adds the arc (team 2.2 m, loose .8 m) to the stride so the pair walk round instead of sliding.
- Play (PawnRush_MapTest): 10 team horses dressed, 21 joints bound each, no console errors; 3 saddled test horses
  spawned by code (none in this map). Stills: rcy_r678_horse_team5, _grey_gallop, _black_stand, _bay_turn,
  _saddle_three. Not checked: StableCharger in its own map, skinned-mesh culling at screen edges.
- Note: with ObstacleClock unshared the fleet starts at 3 s; once the teams stood Waiting at clock 23 s until StartAt was
  called again (not investigated; did not recur after a restart).

## AI-3D castle kit for the castle pieces (relayed 2026-11-15 by the R677 ModelingRefs session; inspected, not used yet)
승규: "마차뜰 블록아웃 채팅에서 그 구간 모델링 작업을 할 때 이거 쓰라고 하면 돼"; then "일단 에셋을 늘려서 써도 되고
이상하면 추후에 바꿈" (stretching allowed for now). For: outer gatehouse, rook towers, curtain walls, final gatehouse,
section end (z 157-181: castle_archway_stairs). Files: ArtSource/.../CarriageYardModels/source_ai3d/ (untracked, 65 MB).
Inspected in Blender (temporary scene, removed afterwards). Stills: stills/rcy_ai3d_castle_kit_overview.png, _towers.png.
- Every file: one mesh, ~1 m on its longest side (normalised), origin at the bounding-box centre (not the base), front
  -Y, up +Z, scale 1. 30k tris each (archway_stairs 50k). One material, packed 4096^2 base colour + 2048^2 normal
  (archway_stairs: base colour is a blank 1x1 white = untextured, normal 2K).
- stone_castle_gate: two round towers, a low crenellated wall with a small door and a shield (1.0 x .37 x .65).
- beige_castle_gate: a tall square arch between two towers, a crest on top (1.0 x .36 x .86).
- beige_castle_wall: a KIT of 2 pieces in one mesh - a tall square section and a long low crenellated wall.
- stone_castle_tower: a KIT of 3 rook towers in one mesh (medium, slim, wide); bases lumpy and gold-studded.
- castle_archway_stairs: open arch on two rook piers, low steps, balustrades (.86 x 1.0 x .75), white.
- All read beige/cream with gold rings - the chunky toy look of the horses, not the yard's grey clay. Open question
  for 승규 at that zone: keep their textures, or clay like the rest (the yard's rule so far).
- Before use: split the kits into pieces, put the pivot at the base, scale to the blockout, decimate (30k per copy adds
  up fast on repeated wall runs), bake/export the texture separately (2K) instead of the packed 4K.
- Collision rule 승규 approved ("전해", relayed): box colliders only, never the AI meshes; fit the model to the
  collider where pawns touch (gate passages, inner wall faces, stair treads, balustrade inner faces, walkways),
  stretching allowed; where it cannot fit, nudge/add boxes (round towers = several boxes, stair heights = the model's
  steps) but never change gameplay widths/gaps; unreachable parts may differ freely; after each zone overlay the
  colliders semi-transparently and fix every pawn-touchable face off by more than ~10 cm (invisible walls or
  clipping), and list those spots in the zone's report.

## Castle done (2026-11-15, uncommitted, AI-checked in Play only - 승규 has not seen it)
승규: "색이 없는 에셋은 에셋들과 색을 맞춘 다음 적용 시켜" then "진행" -> castle pieces keep the AI kit textures.
- Source: castle_model.py (box_face / wing_boxes / straighten_jamb / castle_sync_cs); exports RcyCastleOuterGate,
  RcyCastleFinalGate, RcyCastleSectionEnd (.fbx) + 10 compressed PNGs; guides/rcy_castle_boxes.json (148 boxes).
- Code: RoyalCarriageYard.Castle.cs (new; CastleModels, DressCastle, CastleBoxData generated between
  `// <castle boxes>` markers), Dressing.Dress() calls DressCastle(). Blockout gate walls/towers no longer collide.
- 승규 11-15 "이 작업은 다음에 하고 다음 작업 이어 가" -> DEFERRED castle fixes (do later):
  * outer tower gold studs stand 20-33 cm proud (pawns overlap them)
  * outer passage niche corners 10-15 cm off; final passage front/back corner chamfers up to ~25 cm off
  * head-height stone set back 15-30 cm (feet/body height fit)
  * blue banner at outer x ±17 hidden by the thickened pier; ±10/±12 hidden by towers
  * outer gate title text overlaps the crest shield; reads mirrored from the yard side
  * asset stairs unused; final gatehouse top squashed; section end (SectionEnd) not measured

## Zone 3 statics done (2026-11-15, uncommitted, AI-checked in Play only - 승규 has not seen it)
- Source: sorting_model.py (needs rcy_kit.py + entrance_model.py with RCY_BUILD=False first); export RcySorting.fbx
  (277k tris, map frame). Dressing.cs: ZoneModel "3-4 (own frame)/3 Loading and sorting yard" keeps water/falls/foam,
  hatch + sluice tunnels, Lamp glow, gold edges (basin, landing, loft edge/lip); new Moving(r) also leaves the
  machines' MonoBehaviour parts drawing (CargoConveyor, StackRow, SlewCrane, PalletHoist, CargoSlam, not Obstacles).
- Play: model hung, 639 blockout renderers off, machines + signals still drawn, no console errors.
- Ray check vs colliders: lamp posts, walls, wall-end piers, jib posts, timber stores within 10 cm; stall fronts get a
  1.75 m plank counter on the goods' face (x +-23.5), 4/408 rays off (2 cm slits between boards); loft/landing slits
  2 cm. Hatch posts, lamp bases, jib foot, wall-end piers pulled in from the blockout's 15-30 cm overhangs.
- Not done: zone 3 machines (conveyor + loads, storage rows + cranes, timber cranes, hoists, heavy berth gantry +
  chest, lifting waterwheel) still blockout; zone 4 statics (platform, cliff, railings, runout) still blockout.
- Stills: rcy_r678_sorting_mouth, _stalls, _stalls_counter, _loft_store, _west_side, _basin_dock.

## Zone 3 machines done (2026-11-16, uncommitted, AI-checked in Play only - 승규 has not seen it)
- Source: sorting_machines_model.py (exec rcy_kit, entrance, sorting_model with RCY_BUILD=False, then movers_model with
  RCY_BUILD_MOVERS=False first). 15 FBXs, each in its machine's own frame (all roots identity): RcyConveyor,
  RcyConveyorCrate/Stack, RcyStackRow, RcyStackCrate, RcyRowTrolley, RcySlamGantry, RcySlamChest, RcySlamTrolley,
  RcySlewCrane, RcySlewJib, RcyHoistFrame, RcyHoistPalletP/N, RcyLiftWheel (~64k tris all told).
- Dressing.cs: DressMovers() -> DressSortingMachines(): Unpaint each machine root keeping gold trims (Conveyor trim,
  Beam trim, Beam band, Jib trim, Arm trim), lamps (Trolley/Jib/Hoist lamp), stretched ropes/chains (Row rope, Hoist
  rope, Slam chain), warnings (Gap mark, Slew reach/sector, Slam circle/rim/wave/dust, Well board/edge), wheel Handle
  + Handle post. New RideUnscaled() puts the two trolleys' models under a holder undoing the box's scale.
  Pallet model chosen by the "Pallet sack" child's x sign. Conveyor slats are static pictures (not moving).
- Ray check: belt +-8 cm (rails as blockout), loads/stack crates/row stock within 10 cm, crane drums 3 cm, paddles 5 cm,
  wheel rims/spokes exact on their local faces; timber ridge beam lengthened to the collider's 8 m (blockout left
  15 cm air), flanks <= 9 cm perpendicular, 1 cm slit between the lower timbers. Remaining >10 cm hits are other
  parts in front (crown on chest, sacks on pallets, winch on gantry posts, caps on hub) as in the blockout.
- Stills: rcy_r678_machines_conveyor, _crane, _hoist, _berth, _liftwheel.
- Not done: zone 4 statics; deferred castle fixes; docs (HANDOFF, REQUIREMENTS, ROYAL_CARRIAGE_YARD.md); commit.

## Deferred castle fixes done (R679, 2026-11-16, uncommitted, AI-checked in Play only - 승규 has not seen it)
- 승규 chose "2" = "미뤄 둔 성문 문제 먼저 고치기". Source: castle_model.py (exec rcy_kit with RCY_BUILD=False, then
  castle_model with RCY_BUILD=True, then export_rcy; castle_export(models=(...), textures=False) also writes
  CastleBoxData into RoyalCarriageYard.Castle.cs, now 440 boxes).
- Towers: studs off the stone moved onto it, band boxes per tier; gate-wall piers on plinth boxes (outer 2.57,
  final 155.01); wall seams at the piers closed (close_wall_ends); final far face boxed in two parts (bends at x 6);
  final gatehouse top 25.62; banners moved between piers (outer +-13.2/+-19.5, final +-14.7/+-22).
- Labels: CastleLabels() after the map is switched on puts every castle group's TextMesh on the depth-tested
  ChessFight/KingRushCourseText material (push gate label included): no text through stone or mirrored from behind.
- Section end: the asset's opening sits .135 off its middle -> GATEWAY at x -.133, scale x 30.32 (piers' inner faces
  6.00 .. 6.14 both sides; were +6.23..6.53 / -5.96..6.30). part() got cuts=(plane, normal) bisects with welded,
  capped rims: pier courses .12 high edge to edge (were torn with sky through them), gateway pier bottoms, balustrade
  foot (.04) and far end (.68) cut straight; rail x scale kept, height 1.15. Asset step laid on the 4 code steps:
  treads within 1 cm of 7.75/7.5/7.25/7.0.
- Left (reported, not fixed): tier-top set-backs 10-15 cm, bevelled corners (pier corners up to ~30 cm at the very
  edge), unreachable faces; sign text dark on stone; "1. 짐마차 마당" sign overlaps closed doors; text-like texture
  marks on the back of the final door leaves.
- Stills (scratchpad/stills): r679_outer_court, _outer_yard, _outer_pier_fixed, _final_platform, _final_back,
  _final_wall_banners, _section_steps_fixed, _section_pier_capped, _band_rail_foot_fixed, _runout_rail.
- Not done: zone 4 statics; docs (HANDOFF, REQUIREMENTS, ROYAL_CARRIAGE_YARD.md); commit.

## R680 (2026-11-16, 승규 asleep: "구역4 고정물", "전체 모델링 디테일하게 세밀하게", "에셋 사이사이 깨진 부분 매워") - not committed, nothing user-verified

- Zone 4 statics: new ArtSource/PawnRush/CarriageYardModels/platform_model.py -> RcyPlatform (cliff face over the basin:
  rusticated base, pilasters, panels, dentil frieze, cornice, coping; platform paving, kerb, two edge balustrades),
  RcyFinishCourt (map only: paving, three walls, pedestal + velvet cushion), RcySectionFloors (section only: runout,
  terrace, band paving, side walls under the castle railings). Dressing.cs ZoneModel got mapOnly/sectionOnly; FinalKeep
  keeps Finish line / Crown / Crown point. Rays: cliff 0-5 cm, railings <=2 cm, court walls <=3.5 cm, pedestal <=7.4 cm.
- Castle kit cracks: the AI meshes are split along UV seams; decimating unwelded meshes opened hairlines. castle_model.py
  part() now remove_doubles(1e-5) first; magenta-background render of the castle models: ~3000 enclosed pixels -> 1.
  Tower fit re-measured with straight rays from outside: model within 5 cm of the boxes (the per-face sampler's 0.5 m
  flags on the octagon boxes were the sampler hitting neighbouring pieces, not the model).
- Scenery: new scenery_model.py -> RcyScenery (group "Scenery": merlons with plinth, arrow slit on the inner face and cap
  on the yard/canal walls x 22..23 top 5, z 2.5..80.5; four canal-landing lamp posts) and RcySceneryOwn (own-frame
  scenery: merlons x 26..27 top 11, map z 82.5..156.5; platform lamp posts at (+-8, 144.5) on top 8). sorting_model.py
  lamp_post got z0. Dressing.cs ZoneModel.only = SceneryCovered {Merlon, Lamp post, Lamp cap} (declared BEFORE the
  ZoneModels table: static init order - declared after, it was null and the whole group went dark).
- Fresh full chain re-export of platform_model (section floors 15312 tris now). CarriageYard_v2.blend saved (copy).
- Left: banners stay the code's blue cloth (no-blue colour pass later), the mid-pier shading seam on the final wall,
  PushGate leaves are the code's wood (look fine). Docs (HANDOFF, REQUIREMENTS, ROYAL_CARRIAGE_YARD.md) at commit time.

### R680 continued (승규 "진행")
- Gate wall pier seam (outer x +-16, final x +-18): castle_model.py mirror_part() + straighten_end_normals(); each side's
  outer segment is the wall part mirrored (wall_for(s, inner)), so a joint is one end against its mirror. Pier boxes
  re-measured in Blender: outer (14.68, 17.32), (21.70, 23.0); final (16.48, 19.52), (24.54, 26.0). Unity rays: final
  piers within 5 cm (one 4 cm sliver at the end pier's plinth edge 24.26..24.30), outer within 7.6 cm below 6 m.
- An empty RcyCastleOuterGate.fbx (4 KB) went out once because its collection was hidden in the view layer for a
  Blender look; export_rcy.export() now raises when a mesh is not selectable. Re-exported, 1.34 MB.
- scenery_model.py corbel_table(): zone 3 side walls z 82..143, 10.15..10.75, out <= .16 (gantry tops 9.9, banner
  rods 9.6); none over the platform. RcySceneryOwn 35168 tris.
- Hole render after: 0 on the piers; 73 px in the outer front view are far sky through the passage, 2 px final back.
- Stills: Temp/RcyStills/seam_final_pier_after, seam_outer_pier_after, corbels_sorting_wall.
