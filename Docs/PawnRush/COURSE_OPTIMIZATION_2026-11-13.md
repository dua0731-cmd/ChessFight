# 정원·대계단 경량화 1차 — 전후 모형 보고

2026-11-13 · main@b1d19d83에서 시작 · 미커밋 작업
사용자: 작업 전/후 사진을 찍어 보고하고, 기존 모형의 외형을 우선해 정원·대계단만 경량화. 작전 회의실은 추후. **커밋·푸시하지 않음.**

## 1. 적용 결과

실제 Unity 6000.3.11f1에서 구간 프리팹의 Awake 빌드를 실행한 뒤 활성 MeshRenderer의 삼각형을 집계했다. 카메라 가림 처리 전의 구간 전체이며, 한 프레임의 렌더 수·FPS 측정값이 아니다.

| 구간 | 작업 전 | 작업 후 | 줄인 삼각형 | 감소율 |
|---|---:|---:|---:|---:|
| 정원 | 2,662,945 | 2,628,003 | 34,942 | 1.31% |
| 대계단 | 4,722,590 | 4,016,857 | 705,733 | 14.94% |
| 합계 | 7,385,535 | 6,644,860 | 740,675 | 10.03% |

최초 제안한 90만·120만 예산에는 도달하지 않았다. 외형 보존이 우선이라는 후속 지시에 따라 감축 폭을 제한했다. 이 결과가 12인 60fps를 보장하지 않는다.

- 낙하 나이트: 1개 534,136 → 373,894(약 30% 감소), 배치 4개 합계 2,136,544 → 1,495,576.
- 공중 발판: 1개 23,956 → 17,966(약 25% 감소), 배치 8개 합계 191,648 → 143,728.
- 정원·홀 건축물: 재질·UV·법선 경계를 구분한 0.05도 이하의 평면 분할 정리.
- 꽃 메시 110,960개와 나무 모델 399,638개는 그대로다. 수량·배치를 줄이지 않았다.
- 게임 C#·씬·구간 프리팹·충돌체·애니메이션·재질·텍스처는 이번 작업에서 바꾸지 않았다. 시각용 FBX 10개만 교체했다.

## 2. 전후 사진

**아래 사진은 Unity의 실제 구간 생성 코드가 연결한 재질을 사용한 임시 Preview Scene 렌더다. AI 생성 이미지가 아니다. 같은 카메라·조명·해상도로 촬영했고, 비교판 양쪽에 같은 영역을 잘라 배치했다.** 플레이 장면·12인 성능 측정은 아니다.

### 나이트
![나이트 전후](ConceptArt/Optimization20261113/compare_Falling_knight.png)

왕관·고삐·금색 문양 경계를 보호하며 줄였다. 촬영 구도에서 큰 외형 손실은 관찰하지 못했지만, 표면 음영의 미세한 변화는 존재한다. 사용자 직접 확인 전이다.

### 공중 발판
![발판 전후](ConceptArt/Optimization20261113/compare_Floating_pad.png)

### 정원 대문
![정원 대문 전후](ConceptArt/Optimization20261113/compare_Exit_gate.png)

### 정원 석조물
![정원 석조물 전후](ConceptArt/Optimization20261113/compare_Lock_and_dyke_stonework.png)

### 나무 — 원본 유지
![나무 근접 전후](ConceptArt/Optimization20261113/compare_Garden_tree_E1.png)

### 꽃 — 원형·밀도 유지
![꽃 전후](ConceptArt/Optimization20261113/compare_Plaza_planter_flowers.png)

### 원형 홀 벽
![원형 홀 벽 전후](ConceptArt/Optimization20261113/compare_HallV15_Round_wall_0_1.png)

원본 PNG는 같은 폴더의 unity_*_before.png / unity_*_after.png이며, 비교판보다 큰 원본 해상도로 볼 수 있다.

## 3. 시험 후 제외한 감축

- 나무를 직접 20% 감축한 Blender 사본: 잎의 빛·색 차이가 커서 적용하지 않았다.
- Unity 기본 Mesh LOD도 시험했다. LOD0 399,638 → LOD1 273,642 삼각형(나무 26개 합계), 최대 LOD1 한 단계로 제한했다.
- 근접은 거의 동일했지만, 원거리 픽셀을 확대하면 작은 꽃·잎 디테일 차이가 보여 최종 적용에서 제외했다. GardenTrees.fbx 및 .meta를 원본 상태로 유지했다(generateMeshLods=false, maximumMeshLod=-1).
- 아래 사진은 **미채택 시험본**이다. 임시 렌더러에 LOD0/1을 강제로 지정해 같은 원거리 구도로 찍었다. 실제 게임에서 해당 원거리 단계는 활성화하지 않았다.

![미채택 나무 LOD 시험](ConceptArt/Optimization20261113/compare_Tree_far.png)

원거리 시험의 외곽 마스크 겹침은 약 97.7%였다. 이 수치를 완전한 외형 보존이나 게임 실기 확인으로 해석하지 않는다.

## 4. 검사 근거와 한계

- Blender 5.2.2 LTS에서 원본·후보를 다시 가져와 변경 대상 10개 파일, 260개 메시 객체의 이름·월드 변환·재질 슬롯 순서·UV 레이어·정점 색 레이어 이름/종류가 유지되는지 비교: 오류 0개.
- Unity 구간 렌더러: 정원 155개, 대계단 757개로 전후 동일.
- 충돌체: 정원 245개, 대계단 229개. 계층 경로·종류·활성/트리거·월드 중심/크기를 정렬해 만든 SHA256이 전후 동일했다. 실제 입력으로 완주·물리 접촉을 시험한 것은 아니다.
- 나이트 정면/측면의 Blender 외곽 마스크 겹침 약 99.998%, Unity 정면 비교의 마스크 겹침 100%. 발판 Unity 비교 약 99.996%. 각 촬영 구도의 화면 지표이며 모든 각도·조명에서의 동일성을 보장하지 않는다.
- Core 192개·모의 세션 81개 통과. 코드 변경이 없어 전 어셈블리 별도 CLI 컴파일은 추가하지 않았다.
- 임시 Preview 캡처 중 MCP ExecuteCode 경로에 ShouldRunBehaviour Assert 4건이 기록됐다. Console 기록을 지우거나 게임 코드를 수정하지 않았다. 사용자 Play 확인은 별도 대기다.
- 온라인·Steam·12인 호스트 물리 비용·FPS는 측정하지 않았다. 움직이는 장애물의 실제 입력 확인도 별도다.
- 작업 도중 사용자가 켠 Play를 강제로 정지하지 않았다. 나무 LOD 생성은 Play가 끝난 뒤 수행했고, 비교 후 원래 설정으로 되돌렸다. 원래 열려 있던 Lobby 씬은 저장하지 않았다.

## 5. 파일·복원·재현

- 적용: Resources/PawnRushGrandStairHall의 FallingKnight.fbx, FloatingPad.fbx, GrandStairHallV15.fbx 및 Resources/PawnRushGardenConcept의 GardenPlaza.fbx, GardenGate.fbx, GardenLockDyke.fbx, GardenBoats.fbx, GardenWalls.fbx, GardenPlatforms.fbx, GardenCanal.fbx.
- 기존 FBX .meta와 GUID는 보존했다. 나무 .meta의 시험 설정은 백업 원본으로 복원했다.
- 원본 백업: 11-13 Unity를 다시 켤 때 Temp가 비워져 Temp 백업은 모두 없어졌다. 최초 원본은 git HEAD(ddc8e628)의 같은 경로 FBX·.meta다(파일 하나씩 `git show HEAD:<경로>`로 꺼낸다). 원래 .blend·제작 스크립트는 수정하지 않았다.
- 재현 스크립트·파일별 집계·구조 검사: ArtSource/PawnRush/Optimization20261113/.
- course_mesh_pass.py는 후보와 사진만 만든다. 기존 Assets에 자동 덮어쓰지 않는다. CHESSFIGHT_ROOT로 저장소 경로를 지정하고, Blender --background --factory-startup --python <script> -- <별도 출력 폴더>로 실행한다. MESH_PASS_ONLY는 쉼표로 대상을 고르는 선택 변수다.
- 원본 제작기로 FBX를 다시 내보내면 경량화가 덮일 수 있으므로, 재내보내기 뒤 같은 검사·전후 사진 절차를 거쳐 이 후처리를 다시 적용한다.
- 이번 사용자 지시대로 커밋·푸시는 하지 않았다. 다른 채팅의 UI·로비·문서·Packages 수정은 보존했다.
- **R639(11-13): 사용자 "최적화 등등 main에 넣고 커밋 푸쉬 해"로 main에 커밋 · upstream 푸시.** 적용된 FBX · .meta, 이 보고서 · 사진, 이 폴더의 스크립트 · json · tsv · txt · 로그만 넣었다. 이 폴더의 FBX · .meta · 시험 사진(Pass4 후보 · original, LOD before_split · split · split_test · boat_check, DrumCheck)은 크기(약 650 MB) 때문에 저장소에 넣지 않고 이 PC에만 둔다. 아래 각 절의 '되돌리기' 경로는 이 PC에서만 쓸 수 있고, 다른 PC에서는 최초 원본(git ddc8e628)으로 되돌린다.

## 6. 2차 — 원형 홀 벽·난간·보트 (Claude Code 이어받음, 2026-11-13)

Codex가 2차 후보 두 개를 적용·import한 뒤 중단했고, Claude Code가 같은 C:/Work/ChessFight Unity(인스턴스 ChessFight@c79f4227, Play 아님)에서 확인을 이어받았다.

- GrandStairHallV15.fbx: 원형 벽 4개 + HallV15_Balustrades만 0.25도 평면 정리, 원본 꼭짓점 법선 복원.
- GardenBoats.fbx: Boat 1-1~4-2 + Boat tunnels만 0.5도 평면 정리, 원본 꼭짓점 법선 복원.
- 성벽 탑·석상 받침대는 감소 효과가 거의 없어 적용하지 않았다(파일이 2차 전 백업과 바이트 단위로 같음을 확인).
- 두 FBX의 .meta는 2차 전 백업과 해시가 같다(GUID·가져오기 설정 그대로).

### 실제 Unity 집계(1차와 같은 집계 코드, phase="after")

| 구간 | 1차 후(2차 전) | 2차 후 실측 | 줄인 삼각형 | 최초 대비 합계 |
|---|---:|---:|---:|---:|
| 정원 | 2,628,003 | **2,621,741** | 6,262 | 2,662,945 → 2,621,741 (−1.55%) |
| 대계단 | 4,016,857 | **3,944,227** | 72,630 | 4,722,590 → 3,944,227 (−16.48%) |

- 대계단 실측은 인계 문서의 계산값 3,944,237보다 10개 적다. 원인: 원래 모델의 원형 벽 꼭대기에 Unity가 가져오기 때 버리는 겹친 다각형 7개가 있다(아래 한계).
- 렌더러 수 정원 155·대계단 757, 충돌체 정원 245·대계단 229로 같고, 충돌체 SHA256 해시가 2차 전과 **같다**. 실제 입력으로 완주한 것은 아니다.

### 같은 구도·조명 전후 사진

![2차 전후·차이](ConceptArt/Optimization20261113/Pass2/pass2_before_after.png)

왼쪽 전, 가운데 후, 오른쪽 차이(8배 확대). 모델 위 픽셀 기준 평균 차이(0~255): 원형 벽 0.49, 난간 0.40, 보트 0.07, 보트 터널 1.12. 24 이상 바뀐 픽셀은 모든 대상에서 0.1% 미만. 바꾸지 않은 탑·받침대·나이트 석상은 차이 0.

![원형 벽 확대 전후](ConceptArt/Optimization20261113/Pass2/zoom_HallV15_Round_wall_0_1.png)
![보트 확대 전후](ConceptArt/Optimization20261113/Pass2/zoom_Boat_1-1.png)

차이가 가장 큰 곳을 원본 해상도로 확대했다. 돌 무늬·모서리 음영·아치 윤곽, 보트 금테·좌석·등불은 AI 관찰로 구분되지 않았다. 그래서 되돌리지 않았다. 원본 크기 사진은 C:/Users/kerri/.codex/visualizations/2026/11/13/01a1d3d3-711a-7b73-a105-b8cb92b698cf/course_mesh_pass2/unity_*_before.png·_after.png(같은 PC, Git 밖).

### 검사·한계

- Core 192개·모의 세션 81개 통과(2차 후 새로 실행, Tools/Test-NetworkCore.ps1). 코드 변경 없음.
- Console에 "GrandStairHallV15.fbx의 HallV15_Round_wall_* 다각형이 자기 교차라서 버려짐" 경고 7건이 있다. Blender로 찾아 보니 같은 10개 면(약 63 cm²·34 cm²·0.001 cm², 높이 17~23 m 벽 꼭대기)이 **1차 전 최초 원본(11-11 파일)에도 똑같이 있다.** 경량화가 만든 결함이 아니며 이번에 고치지 않았다.
- 이전처럼 ShouldRunBehaviour Assert가 Console에 남아 있다(Preview 캡처 경로). 지우지 않았다.
- 화면 비교는 Preview 한 구도·한 조명이다. 실제 게임 조명·역광·근접 이동, 12인 FPS·CPU/GPU 시간은 측정하지 않았다. 사용자 Unity 실기 확인 전.
- 기록: ArtSource/PawnRush/Optimization20261113/Pass2/unity_after.json.
- 2차만 되돌리기: Temp 백업은 없어졌다. 2차 전(=1차 적용) 상태 사본은 Codex 폴더 C:/Users/kerri/.codex/visualizations/2026/11/13/01a1d3d3-711a-7b73-a105-b8cb92b698cf/course_mesh_pass2/original/Assets/... 에 남아 있다.
- 사용자 지시대로 커밋·푸시하지 않았다.

## 7. 3차 — 낙하 나이트 (Claude Code, 2026-11-13)

사용자: "겉모습을 어느 정도 유지하는 선에서 감축". 이전 방식(평면 정리)으로는 더 줄일 것이 거의 없어, 보호 규칙을 둔 점 합치기(Blender Decimate Collapse)에 **원본 법선 옮기기**(Data Transfer, 가장 가까운 면 보간)를 더했다. 재질 경계·UV 이음매·35도 넘는 모서리의 점은 움직이지 않는다. 스크립트: ArtSource/PawnRush/Optimization20261113/Pass3/pass3_decimate.py(Assets에 자동으로 쓰지 않음).

### 적용: FallingKnight.fbx — 1개 373,894 → 112,168 (0.3)

| 구간 | 3차 전 | 3차 후 실측 | 최초 대비 |
|---|---:|---:|---:|
| 정원 | 2,621,741 | 2,621,741 (변경 없음) | −1.55% |
| 대계단 | 3,944,227 | **2,897,323** | 4,722,590 → 2,897,323 (−38.65%) |

- 렌더러·충돌체 수와 충돌체 해시 모두 3차 전과 같다. 나이트 메시를 쓰는 MeshCollider는 0개라 물리 모양은 바뀌지 않는다. 메시 하위 4개·재질 4개(Cream/Dark/Gold/Ivory)·.meta GUID 그대로, import 경고 없음.
- Core 192·모의 세션 81 통과.

![나이트 Unity 전후(앞·옆·뒤·밑·머리 확대)](ConceptArt/Optimization20261113/Pass3/knight_unity_before_after.png)

Unity 실제 재질 비교: 평균 차이 1.1~2.9, 24 넘게 바뀐 픽셀 0.5~1.0%, 윤곽 겹침 99.6~99.96%. 차이는 가장자리 한 줄 수준이고, 머리 확대에서 눈·고삐·왕관·갈기·금테가 AI 관찰로 구분되지 않았다. 밑면도 확인했다.

![Blender 단계별 0.6/0.4/0.3](ConceptArt/Optimization20261113/Pass3/knight_blender_ratios.png)
![Blender 머리 확대](ConceptArt/Optimization20261113/Pass3/knight_blender_head_zoom.png)

### 시험 후 제외

| 대상 | 결과 | 제외 이유 |
|---|---|---|
| 공중 발판 0.6/0.45 | 1개 17,966 → 10,778/8,084 | 평평한 옆면에 얼룩 음영 ([사진](ConceptArt/Optimization20261113/Pass3/rejected_pad.png)) |
| 계단 석상 0.6/0.45(받침대 제외) | 332,884 → 231,800/221,232 | 평균 차이 4.8 이상, 아래 면 음영 변화 ([사진](ConceptArt/Optimization20261113/Pass3/rejected_statues.png)). 받침대까지 넣은 첫 시험은 받침대가 크게 어두워짐 |
| 홀 원형 벽·바깥벽(2개로 시험) 0.7/0.5 | 2개에서 −5.6만/−6.8만 | 윤곽 겹침 98.6%, 평균 차이 5.7 이상 ([사진](ConceptArt/Optimization20261113/Pass3/rejected_hall_walls.png)) |
| 정원 출구 대문·대문 석조 / 수문·둑 석조 | −6,584 / −2,058 | 모서리·재질 경계 보호로 거의 줄지 않음 |

### 한계·복원

- Preview 한 조명·정사영 구도 비교다. 실제 게임 조명·근접 카메라, 12인 FPS는 측정하지 않았다. 사용자 실기 확인 전.
- 3차만 되돌리기: Temp 백업(1차 적용 상태 낙하 나이트)은 Unity 재시작 때 없어졌다. 지금 되돌릴 수 있는 것은 git HEAD의 최초 원본 FallingKnight.fbx뿐이다.
- 사용자 지시대로 커밋·푸시하지 않았다.

## 8. 그리기 시간 측정 (Claude Code, 2026-11-13, 3차 후)

**측정 조건:** 이 PC(Intel Core i5-13500H, **Intel Iris Xe 내장 그래픽**), Unity 6000.3.11f1 에디터 Play, `PawnRush_MapTest` 씬에 구간 프리팹을 임시로 넣음(씬 파일 저장 안 함). 1920×1080 RenderTexture에 메인 카메라를 20번 그린 뒤 GPU 완료까지 기다린 평균 시간이다. Unity 창이 뒤에 있고 화면이 잠겨 있어 실제 FPS(Game 뷰 프레임)는 잴 수 없었다. 플레이어 빌드·12명·래그돌 물리·네트워크 비용은 포함되지 않았다. **실제 게임 FPS가 아니라 "카메라 한 번 그리는 시간"이다.**

### 대계단 (1080p, ms)

| 시점 | 기본 | 나이트 숨김 | 그림자 끔 | 점 조명 59개 끔 | 보이는 삼각형 / 렌더러 |
|---|---:|---:|---:|---:|---|
| 입구 | 40.9 | 37.4 | 34.1 | 33.2 | 287만 / 1,034 |
| 중간 | 30.7 | 28.2 | 26.2 | 27.7 | 175만 / 444 |
| 위에서 | 33.4 | 33.0 | 29.3 | 31.2 | 243만 / 990 |
| 끝에서 돌아봄 | 33.9 | 32.2 | 29.6 | 32.9 | 287만 / 979 |

입구·중간에서 추가로 나눔: 기본 28.9/28.1 → 해상도 절반 20.7/17.8 → 모델 전부 끔 11.4/9.2. 후처리를 꺼도 거의 같다.

### 정원 (1080p, ms)

| 시점 | 기본 | 나무 숨김 | 꽃 숨김 | 그림자 끔 | 모델 전부 끔 | 보이는 삼각형 / 렌더러 |
|---|---:|---:|---:|---:|---:|---|
| 입구 | 32.1 | 30.4 | 31.7 | 30.2 | 7.1 | 254만 / 156 |
| 위에서 | 38.1 | 34.8 | 36.3 | 35.5 | 8.5 | 234만 / 140 |
| 끝에서 돌아봄 | 37.5 | 36.9 | 36.6 | 31.1 | 7.1 | 238만 / 146 |
| 옆에서 | 37.1 | 34.2 | 34.8 | 30.1 | 7.7 | 232만 / 132 |

### 읽는 법

- 이 PC에서는 **GPU가 병목**이다. CPU가 그리기 명령을 내는 시간은 4~12 ms인데, GPU 완료까지는 28~41 ms 걸린다.
- **모델(삼각형)이 가장 큰 몫이다.** 모델을 모두 끄면 정원 약 30 ms → 7~8 ms, 대계단 약 29 ms → 9~11 ms로 줄어든다. 정원은 렌더러가 160개뿐인데도 대계단과 비슷하게 느리다. 그래서 그리는 횟수보다 삼각형 수가 시간을 좌우한다. 대략 100만 삼각형당 7~11 ms(이 PC·이 장면 기준 추정).
- 그림자(방향광 그림자 = 모델을 한 번 더 그림)는 3~7 ms, 대계단 점 조명 59개는 1~8 ms다. 나무 26그루는 1~3 ms, 꽃은 0.4~2.5 ms다.
- 3차 나이트 감축의 효과는 이 비율로 보면 화면에 나이트 3개가 보일 때 약 5~8 ms 절약으로 **추정**된다. 감축 전 메시로 직접 다시 재지는 않았다.
- 60fps(16.7 ms)에는 두 구간 모두 이 PC에서 아직 못 미친다. 12명 래그돌 물리·UI·네트워크가 더해지면 더 느려진다.
- 원본 숫자: ArtSource/PawnRush/Optimization20261113/Pass3/profile_hall_render.json, profile_garden_render.json.

## 9. 그림자·조명·SSAO 시험 (Claude Code, 2026-11-13) — 적용 안 함

§8과 같은 방식(Iris Xe, 1080p 카메라 렌더)으로 재되, 후보를 번갈아 5번 돌려 가장 빠른 값과 중간값을 봤다. 노트북 GPU라 같은 설정도 ±3~5 ms 흔들린다. 설정은 Play 중에만 바꾸고 바로 되돌렸다. PC_RPAsset·PC_Renderer·씬 파일 변경은 없다.

| 후보 | 대계단 줄어든 시간(중간값) | 정원 | 겉모습 | 판단 |
|---|---|---|---|---|
| 그림자 지도 4096→2048 | 0~1.8 ms | — | 그림자 가장자리가 흐려짐(픽셀 1.5~3.8% 변화) | 효과가 오차 수준 → 제외 |
| 그림자 거리 40→30 m | 0~1.7 ms | — | 먼 그림자 사라짐(2.2~2.7%) | 제외 |
| 0.6 m보다 작은 물체 189개 그림자 끔 | 0~2 ms | — | 차이 0 | 효과가 오차 수준이고 구간 코드를 고쳐야 함 → 제외 |
| 대계단 점 조명 56개 끔 | 2.2~2.6 ms | — | 조명 분위기가 크게 바뀜 | 제외(전구·등불·촛불 장식의 빛) |
| 방향광 그림자 끔(참고) | 3.7~4.6 ms | 1.2~5.1 ms | 그림자 전부 사라짐 | 제외 |
| SSAO 끔(참고) | 5.4~6.2 ms | 7.9~8.4 ms | 구석 그늘 사라짐 | 제외 |
| SSAO 절반 해상도 | 2.8~3.8 ms | 0.3~1.8 ms | 대계단에서 거의 같음(0.6%) | 정원 효과가 작음 |
| SSAO 샘플·흐림·법선 낮춤 | — | 0~1.0 ms | — | 효과 작음 |
| **SSAO "After Opaque"(깊이만 따로 그리는 단계 생략)** | 1.8~2.6 ms | **5.6~6.8 ms** | 정원은 거의 같음, 대계단은 햇빛 받은 벽 줄눈이 진해짐(4.8%) | **사용자 결정 대기** |

![SSAO After Opaque 비교](ConceptArt/Optimization20261113/Pass3/ssao_after_opaque_compare.png)

- SSAO 비용의 대부분은 효과 계산이 아니라 SSAO용으로 모든 모델을 한 번 더 그리는 깊이 단계였다. 삼각형이 많은 정원에서 특히 크다.
- After Opaque는 PC_Renderer.asset 하나를 바꾸므로 로비·소드 파이트 등 PC 품질의 모든 장면에 적용된다. 겉모습이 조금 바뀌므로 자동으로 적용하지 않았다.
- 원본 숫자: ArtSource/PawnRush/Optimization20261113/Pass3/hall_rounds.txt, garden_rounds.txt.

## 10. 거리별 LOD 시험 (Claude Code, 2026-11-13) — 적용 안 함

Unity 6 내장 Mesh LOD(FBX 가져오기 설정 generateMeshLods)를 정원 7개(GardenWalls·LockDyke·Platforms·Watergate·Gate·Canal·Boats)와 대계단 9개(GrandStairHallV15·StairStatuesV36·RoyalStair·EntranceV46·RoyalFan·FallingKnight·FloatingPad·RoyalBalconyV34·FoyerSidesV47) FBX에 켜고 §8과 같은 방식으로 쟀다. 꽃·나무가 있는 GardenPlaza·GardenTrees는 처음부터 뺐다.

| 구간 | 켜기 전(가장 빠른 값) | 자동 LOD | 강제 LOD0 | 강제 LOD2 | 자동 LOD 화면 차이 | LOD2 화면 차이 |
|---|---|---|---|---|---|---|
| 대계단 | 24.6~26.8 ms | 25.1~27.2 ms | 26.0~27.0 ms | — | 0 | — |
| 정원 | 31.1~32.4 ms | 31.4~37.6 ms | 30.9~38.2 ms | 25.2~28.5 ms | 0.01~0.03% | 6.9~19.7% |

![정원 LOD0 · 자동 · LOD2](ConceptArt/Optimization20261113/LOD/garden_lod_compare.png)

- 자동 LOD는 항상 원본(LOD0)을 골랐다. Unity Mesh LOD는 메시 하나 전체의 화면 크기로 단계를 고르는데, 이 맵은 성벽 172 m · 난간 47 m · 대문 59 m · 보트 터널 55 m처럼 큰 메시 한 덩어리라 어디서 봐도 화면을 크게 차지한다.
- LOD2를 억지로 쓰면 정원이 8~10 ms 빨라지지만 가까운 난간·바닥이 어두워지고 모양이 바뀐다(사진 셋째·다섯째). 외형 보존 원칙에 어긋나 제외.
- .meta 16개는 git HEAD로 되돌렸다(LOD 붙은 메시 0/502 확인). FBX·충돌체는 이 시험에서 바꾸지 않았다.
- 거리별 LOD가 실제로 듣게 하려면 큰 메시를 20~30 m 조각으로 나눠야 한다(FBX 다시 내보내기·충돌체 확인 필요). 이번에는 하지 않았다.
- 원본 숫자·사진: ArtSource/PawnRush/Optimization20261113/LOD/ (대계단 전후 사진은 Unity 재시작 때 Temp와 함께 없어짐, 숫자만 이 표에 남김).
- Unity를 다시 켤 때 Temp가 비워져 Temp/ModelOptimization20261113* 백업이 모두 없어졌다. 복원 경로는 §5·§6·§7에 고쳐 적었다.
- 사용자 지시대로 커밋·푸시하지 않았다.

## 11. 정원 큰 모델 나누기 + 거리별 LOD (Claude Code, 2026-11-13) — 적용, 사용자 확인 전

사용자 "정원 큰 모델 조각 내서 진행하고 기존 모델이 크게 바뀌지 않는 선에서 LOD 진행 해". §10에서 자동 LOD가 듣지 않은 까닭(큰 한 덩어리)을 풀려고 정원의 큰 장식 모델을 약 20 m 칸으로 나누고 Unity Mesh LOD를 켰다.

- 나누기: ArtSource/PawnRush/Optimization20261113/LOD/split_chunks.py (Blender 5.2, 면을 자르지 않고 면 중심이 든 칸으로 통째 나눔 → 모양·법선·UV 그대로). 가로 24 m 넘고 5,000 삼각형 이상인 정지 장식만 나눔. 코드가 이름으로 찾는 것(돌림막대·발판·지름길 기둥·수문·로봇 팔·보트·호수 보트·물·폭포·"Plaza fountain jets")은 나누지 않음. 조각 이름은 "원래 이름 #00".
- 나눈 것(18개 → 88조각): 성벽 22 · 성 바닥 19 · 출구 대문 3 · 대문 석조 6 · 둑 석조 4 · 수로 난간 3 · 수로 분수 2 · 수로 벽돌 3 · 수로 서벽 2 · 수로 꽃 2 · 광장 화단 4 · 광장 화단 꽃 4 · 광장 벽돌 6 · 광장 포장 4 · 선착장 꽃 4. 보트 터널은 조건에 걸리지 않아 그대로.
- 구조 검사: 6개 FBX의 물체 63 → 136. 원래 물체마다 조각을 합치면 위치·회전·크기·삼각형 수·재질이 나누기 전과 똑같음(차이 0건, structure_before/after.tsv). 정원 삼각형 총수 그대로(2,621,741).
- LOD 켬: GardenPlaza·Canal·LockDyke·Boats·Gate·Walls·Platforms·Watergate 8개 FBX의 generateMeshLods. 나무(GardenTrees)·물(GardenWater)은 끔 그대로.
- 충돌체: 장식 FBX는 충돌체를 만들지 않음(addColliders 0, 장식 아래 충돌체 0개). 충돌은 회색 박스 그대로.

| 구도 | 원본 LOD0 | 자동 LOD | 화면 차이 | LOD 한 단계 더(bias +1) | 그 화면 차이 |
|---|---|---|---|---|---|
| 출발 | 28.8 ms | 28.6 ms | 0.03% | 28.2 ms | 10.8% |
| 위에서 | 29.5 ms | 28.6 ms | 0.08% | 25.3 ms | 8.1% |
| 끝에서 뒤로 | 30.8 ms | 29.9 ms | 0.12% | 25.9 ms | 13.2% |
| 옆 | 29.7 ms | 30.1 ms | 0.26% | 25.5 ms | 8.6% |

![원본 · 자동 LOD · 한 단계 더](ConceptArt/Optimization20261113/LOD/garden_split_lod_compare.png)

- 자동 LOD는 겉모습이 그대로(0.3% 이하)지만 빨라진 정도도 0~1 ms로 측정 흔들림 안쪽이다.
- 한 단계 더 쓰면 4~5 ms 빨라지지만 가운데 길·바닥이 어둡고 번들거리게 바뀐다(사진 셋째) → 제외. 모델마다 따로 시험해 거의 안 바뀌는 79개만 한 단계 더 써도 화면 0.4~0.5%가 바뀌고 속도는 흔들림 안쪽 → 제외(per_renderer_bias1.tsv, selective_bias.txt).
- 결론: 나누기 + 자동 LOD만 적용. 큰 이득은 없고, 정원을 실제로 빠르게 하려면 겉모습이 바뀌는 감축이 필요하다.
- 되돌리기: ArtSource/PawnRush/Optimization20261113/LOD/before_split/ 의 6개 FBX·.meta를 Assets/Resources/PawnRushGardenConcept/에 복사, before_split_meta/의 Platforms·Watergate .meta 복사.
- 사용자 지시대로 커밋·푸시하지 않았다. 사용자 실기 확인 전.

### 11-1. 보트 되돌림 (사용자 "보트랑 물 상호작용이 약간 이상해졌어")

- 원인: 물 셰이더(PR_GardenClearWater)가 장면 깊이로 보트 둘레 물결·거품을 그리므로 보트 모양이 물 높이 근처에서 조금만 바뀌어도 보인다. 저수지 보트 8척은 2차(Codex)에서 32,812 → 약 29,440 삼각형으로 줄었고, §11에서 LOD도 켜졌다. 호수 보트 4척은 원본 모델이지만 §11 LOD로 거리에 따라 모양이 바뀌었다(화면 0.1~0.3%).
- 조치: GardenBoats.fbx의 "Boat 1-1 … 4-2"만 git HEAD 원본으로 되돌림(restore_boats.py, 나머지 부두·터널·꽃 조각은 지금 것). GardenBoats·GardenWatergate의 LOD 끔. 보트 위치·회전·크기·재질은 전과 같음, 삼각형 8척 × 32,812.
- 정원 삼각형: 2,621,741 → 2,648,675(+26,934). LOD는 이제 GardenPlaza·Canal·LockDyke·Gate·Walls·Platforms 6개만.
- 근접 사진(Play): 보트가 물에 바르게 떠 있음. 사용자 실기 확인 전.
- 되돌리기: ArtSource/PawnRush/Optimization20261113/LOD/boat_check/GardenBoats_before_boatfix.fbx(.meta).

![되돌린 보트 근접](ConceptArt/Optimization20261113/LOD/boats_restored.png)

## 12. 정원 나무·성탑 줄이기 (Claude Code, 2026-11-13) — 적용, 사용자 확인 전

사용자 "정원 쪽 삼각형을 조금 더 줄이고 싶은데 가장 비중이 큰게 뭐야" → "나무랑 성탑 해주고 기존 모양이 크게 바뀌지 않는 선에서 진행해".

- 대상: GardenTrees.fbx의 "Garden tree *" 26그루(399,638), GardenWalls.fbx의 "Castle tower *" 13개(217,680). 둘 다 조각한 듯 울퉁불퉁한 한 덩어리 메시이고, 재질에 텍스처가 없다(URP Lit, 지도 없음) → UV 이음매는 지킬 필요 없음.
- 1차(Pass3 스크립트 그대로): 각도 0.35 rad·UV 이음매·재질 경계를 다 잠그니 꼭짓점의 약 98%가 잠겨 거의 줄지 않음(성탑 −1,650, 나무 −1,274).
- 도구: ArtSource/PawnRush/Optimization20261113/Pass4/decimate.py (Blender 5.2 접기 줄이기 + 원본 법선 옮기기). 환경 변수로 각도(PASS4_ANGLE)·UV(PASS4_UVSEAM)·그대로 둘 재질(PASS4_KEEP)·재질별 단계(PASS4_STEPS)·법선 찾기(PASS4_NMAP)를 고름. 원인 조사: analyze.py(잠금 이유), matshare.py(재질별 삼각형), flipcheck.py·facecheck.py(법선·접힌 면).

### 성탑: 잎 반, 돌 85% — 217,680 → 166,656 (−51,024, −23%)

- 한 비율로 줄이면 돌 블록 줄눈이 먼저 사라짐: 0.6은 몸통 블록이 흐려지고 0.45는 크게 각짐, 0.8도 아래 몸통 줄눈이 약해짐(사진 셋째).
- 성탑 하나(18,000)의 몫: 돌 7,541 · 잎(담쟁이·덤불) 6,238 · 파란 지붕 1,904 · 금 장식 1,748 · 꽃 569.
- 적용(s0): 잎만 반으로(돌·지붕·금·꽃 잠금, 각도 잠금 없음) → 돌만 85%로(각도 1.4 rad 넘는 모서리 잠금). 지붕·금·꽃은 그대로. Unity 사진에서 앞·근접 모두 전과 거의 같음.
- 더 줄인 s1(잎 40% · 꽃 60% · 돌 80% · 지붕 70%, 성탑 −71k)은 뒤쪽 아래 몸통 줄눈이 약해져 제외.

![성탑 Unity 전후(앞·근접)](ConceptArt/Optimization20261113/Pass4/garden_tower_unity.png)

![한 비율 0.8·0.7로 줄인 성탑(제외)](ConceptArt/Optimization20261113/Pass4/garden_tower_uniform_08_07.png)

### 나무: 줄기 그대로, 잎 70% — 399,638 → 279,718 (−119,920, −30%)

- 줄기(bark·moss band)까지 줄이면 줄기가 꼬이고 각져 보임 → 줄기 재질 면은 그대로 둠(PASS4_KEEP).
- 0.6(−151k)은 Unity 근접 사진에서 잎 덩이 아래에 회색 얼룩이 늘어남. 원인: 얇은 잎이 접혀 면 방향이 원본과 반대인 면이 넓이로 4.3%(원본 대비, facecheck.py). 법선 찾기 방식을 바꾸거나 얇은 곳을 잠가도 그대로였음.
- 0.7은 접힌 면 1.4%, Unity 앞·근접 사진에서 원본과 거의 같음 → 적용. 0.8은 0.15%이지만 −80k만 줄어 제외.

![나무 Unity 원본 · 0.6 · 0.7(적용)](ConceptArt/Optimization20261113/Pass4/garden_tree_unity.png)

### 결과·한계·되돌리기

- 정원 삼각형: 2,648,675 → 2,477,731(−170,944, −6.5%). Unity 다시 읽은 뒤 집계: GardenTrees 279,718, GardenWalls 294,298(성탑 166,656). 물체 수·이름·위치는 그대로(26 · 56개).
- 물체 이름이 같고 장식은 그리기 전용(충돌체 없음)이라 코드·충돌·네트워크 영향 없음. GardenWalls의 자동 LOD는 켜진 그대로라 새 메시로 다시 만들어짐.
- 확인은 Unity 미리보기(같은 조명·같은 구도의 모형 사진)와 Blender 렌더만. 정원 안 Play 화면·속도 측정은 하지 않음(사용자 Play 중이었음). 기대: 정원 그리기 약 1 ms 안팎(삼각형 6.5%, GPU 병목 PC 기준 추정).
- 되돌리기: ArtSource/PawnRush/Optimization20261113/Pass4/original/의 GardenTrees.fbx·GardenWalls.fbx(+.meta)를 Assets/Resources/PawnRushGardenConcept/에 복사. 후보 파일: trees_F/candidates/0.7, towers_E/candidates/s0.
- 사용자 지시대로 커밋·푸시하지 않았다. 사용자 실기 확인 전.
