# Claude Code 인계 — 정원·대계단 폴리곤 감축

최신 사용자 지시(2026-11-13): **"여기까지하고 나머지 작업은 클로드 코드에서 작업 할 수 있게 해"**
Codex는 추가 모델 작업·테스트를 멈추고 이 체크포인트만 남겼다.
**사용자의 "커밋 푸쉬 하지마" 지시는 계속 유효하다. 커밋·푸시하지 않는다.**

## 0. 반드시 같은 작업 폴더에서 시작

- 실제 Unity 프로젝트/작업 폴더: **C:/Work/ChessFight**
- 브랜치: main.
- 인계 직전 HEAD: ddc8e628 (다른 세션의 매칭 UI 커밋). 조사 시작 기준은 b1d19d83.
- 경량화 FBX·문서·스크립트는 아직 미커밋이다. 새 clone/worktree/다른 폴더에는 이 작업이 없다.
- 최초 채팅 cwd였던 C:/Users/kerri/OneDrive/문서/GitHub/ChessFight는 오래된 별도 seoungtest 복사본이다. 그곳에서 이 작업을 이어가지 않는다.
- HANDOFF.md 전체와 그 §6이 안내하는 관련 문서를 먼저 읽는다.
- Unity 6000.3.11f1. Blender 5.2.2 LTS 실행 파일: C:/Program Files/Blender Foundation/Blender 5.2/blender.exe.

## 1. 사용자 목표·제약

12인 코스의 정원과 대계단을 먼저 줄인다. 작전 회의실은 추후다.
기존 외형·윤곽·색·재질·꽃/나무 밀도·배치·충돌·움직임을 우선 보존한다.
감축 전/후를 같은 카메라·조명으로 촬영해 사용자에게 보고한다.
카메라에 보이지 않는다는 추측으로 면을 삭제하지 않는다. 나이트는 굴러가므로 밑면도 노출될 수 있다.
게임 C#·공유 물리·네트워크를 범위 없이 수정하지 않는다.
사용자가 Unity/Steam에서 직접 본 것만 '성공/검증됨'이라고 쓴다.
자동 검사·AI 이미지 관찰·실제 12인 FPS를 구분한다.

## 2. 1차 — 적용 및 AI 검사 완료, 사용자 실기 확인 전

보고서: Docs/PawnRush/COURSE_OPTIMIZATION_2026-11-13.md
사진: Docs/PawnRush/ConceptArt/Optimization20261113/
재현/검사: ArtSource/PawnRush/Optimization20261113/

| 구간 | 최초 | 1차 후(실제 Unity 메시 집계) |
|---|---:|---:|
| 정원 | 2,662,945 | 2,628,003 |
| 대계단 | 4,722,590 | 4,016,857 |

나이트 1개 534,136→373,894, 공중 발판 1개 23,956→17,966.
나머지 정원/홀 건축물은 0.05도 평면 정리.
실제 재질을 연결한 Unity PreviewRenderUtility로 전후 촬영했다.
Core 192·모의 세션 81 통과. 정원 245/대계단 229개 충돌체의 경로·종류·활성/트리거·월드 중심/크기 해시 전후 동일.
꽃 110,960 및 나무 399,638 삼각형은 그대로다.
나무 직접 감축과 native Mesh LOD는 세부 차이가 보여 제외했고 .fbx/.meta 모두 원복했다.
1차 목표 예산 90만/120만에는 미달한다. 숫자를 맞추려고 외형을 희생하지 않는다.
1차 캡처 과정에 MCP ExecuteCode의 ShouldRunBehaviour Assert 4건이 있었으며 보고서에 한계를 기록했다. Console 기록은 지우지 않았다.

## 3. 2차 — 현재 정확한 체크포인트

사용자 "다음 감축할 사항"에 벽/난간→성벽 탑→보트/터널→석상 받침대를 제안했고 "진행" 승인을 받았다.
0.25/0.5/1도 후보와 원본 법선을 옮겨 유지하는 후보를 시험했다.
그 뒤 사용자가 위 중단·Claude 인계를 지시했다.

### 이미 작업 폴더에 적용하고 Unity import까지 끝난 2개

| FBX | 방식 | 파일 전체 2차 전 | 후보 후 |
|---|---|---:|---:|
| Assets/Resources/PawnRushGrandStairHall/GrandStairHallV15.fbx | 원형 벽 4개+HallV15_Balustrades만 0.25도 평면 정리, 원본 corner normals 복원 | 2,233,315 | 2,160,695 |
| Assets/Resources/PawnRushGardenConcept/GardenBoats.fbx | Boat 1-1~4-2 및 Boat tunnels만 0.5도 평면 정리, 원본 corner normals 복원 | 394,652 | 388,390 |

위 수치는 FBX 전체 Blender 삼각형 수다. 활성 Unity 구간 전체 수와 섞지 않는다.
두 파일은 original→candidate 해시로 동시 변경이 없는지 확인하고 교체했다.
두 파일의 AssetDatabase.ImportAsset(ForceSynchronousImport|ForceUpdate)은 완료됐다.
기존 .meta/GUID는 바꾸지 않았다.
2차 추가 감소는 파일 기준 78,882개(72,620+6,262)다.
활성 구간에서 그대로 반영된다면 정원 2,621,741 / 대계단 3,944,237이 예상되지만 **아직 Unity 재집계하지 않은 계산값**이다. 실측값처럼 보고하지 않는다.

### 시험만 하고 적용하지 않은 것

- 성벽 탑: 12개 합계 216,000. 0.25도에서 24개만 감소. 색/금선/지붕/식생 경계 보호 시 자유롭게 줄일 정점이 매우 적었다(대표 탑 8,995정점 중 보호 제외 6개). 효과가 작아 원본 유지.
- 석상 받침대: 별도 _plinth 4개 13,184 중 64개만 감소. 얼굴·손·왕관 및 기물 본체는 미수정. 효과가 작아 원본 유지.
- 0.5/1도 벽 후보는 더 많이 줄지만 돌 표면 음영 변화가 커서 제외.
- 보트 터널 자체는 이번 정리에서 감소가 없고, 감소분은 주로 보트에서 나온다.

### 2026-11-13 Claude Code 처리 결과

1~4번 완료: Unity 실측 정원 2,621,741·대계단 3,944,227, 충돌체 해시 2차 전과 동일, 같은 구도 전후 사진 차이 미미해 두 파일 유지, Core 192·모의 81 통과. 원형 벽 자기 교차 다각형 경고 7건은 최초 원본부터 있던 것(경량화 무관). 5번(사용자 보고)만 남음. 상세: Docs/PawnRush/COURSE_OPTIMIZATION_2026-11-13.md §6.

### 2026-11-13 3차(Claude Code)

경량화 3차(Claude Code): 낙하 나이트 1개 373,894→112,168만 적용(원본 법선 옮기기). 대계단 Unity 실측 3,944,227→2,897,323(최초 대비 −38.65%), 정원 2,621,741 그대로. 충돌체 해시 동일·나이트 메시 충돌체 0, Core 192·모의 81 통과. 발판·석상·홀 벽·정원 대문/둑은 시험 후 음영 변화나 효과 부족으로 제외. 사용자 실기 확인 전, 커밋·푸시 안 함. 상세 Docs/PawnRush/COURSE_OPTIMIZATION_2026-11-13.md §7. (Temp 백업은 Unity 재시작 때 없어짐 — 아래 복원 참고)

### 2026-11-13 거리별 LOD 시험(Claude Code) — 적용 안 함

Unity 내장 Mesh LOD(generateMeshLods)를 정원 7개·대계단 9개 FBX에 켜고 재 봤으나 자동 선택이 항상 LOD0이라 그리기 시간·화면이 그대로였다(화면 차이 0.03% 이하). 벽·난간·대문이 47~172 m 한 덩어리라 화면에서 늘 크게 보이기 때문. 억지로 LOD2를 쓰면 정원 약 8~10 ms 빨라지지만 화면 7~20%가 바뀐다. .meta 16개는 git HEAD로 되돌렸다. 상세 §10.

### 2026-11-13 정원 나무·성탑 줄이기(Claude Code) — 적용, 사용자 확인 전

나무 26그루 399,638 → 279,718(줄기 그대로, 잎 70%), 성탑 13개 217,680 → 166,656(잎 반, 돌 85%), 정원 2,477,731. 도구 ArtSource/PawnRush/Optimization20261113/Pass4/decimate.py, 원본 백업 Pass4/original. 0.6 나무는 얇은 잎이 접혀 Unity에서 회색 얼룩, 한 비율 성탑은 돌 줄눈이 흐려져 제외. 상세 §12.

### 2차에서 끝나지 않은 것 — Claude가 먼저 할 일 (위 결과로 1~4 완료)

1. 변경 목록/두 적용 FBX/메타 보존을 확인하고 Unity가 같은 C:/Work/ChessFight를 열었는지 확인한다.
2. 2차 적용 후 실제 Unity 구간 메시 수와 충돌체 해시를 재집계한다. 비교 원본은 Pass2/unity_before.json이다.
3. 동일 구도·조명의 2차 후 Unity 사진을 찍고 전후 비교한다. 표면/색/아치 윤곽/난간/보트 금테·가구에 차이가 크면 2차 백업으로 해당 파일만 되돌린다.
4. 2차 자동 테스트를 실행한다. 아래 명령은 이번에 실행되지 않았다. 1차 결과를 2차 통과로 재사용하지 않는다.
5. 사용자에게 2차 전후 사진과 실제 수치·한계를 보고한 뒤 남은 감축을 이어간다. 보류 중인 나이트 추가 감축·꽃·나무·작전 회의실은 자동 착수하지 않는다.

2차 후 테스트는 시작 직전에 사용자 중단 지시가 도착했다. 자동 승인 검토가 "중단 후 후속 테스트는 최신 지시 범위 밖"이라고 차단했으므로 실행되지 않았다. Codex는 우회 실행하지 않았다.
2차 이후 사진·충돌체·FPS도 아직 미확인이다.

## 4. 재현·사진·백업

인계용 기록: ArtSource/PawnRush/Optimization20261113/Pass2/
- applied.json: 실제 적용한 2개와 적용 제외 후보를 구분.
- results.json: 시험에서 선택한 4개 후보. 이 파일에 있다고 모두 적용한 것이 아니다!
- source_validation.json: 4개 후보의 214개 메시 객체 이름·변환·재질 슬롯·UV/색 레이어 구조 검사, 오류 0.
- variants.json / variant_metrics.json: 모든 평면 각도 후보와 정면/측면 비교.
- course_mesh_pass2_normals.py: 후보 생성 스크립트. 자동 덮어쓰기/커밋 기능 없음.
- unity_before.json / unity_capture.cs.txt: 2차 전 Unity 집계·충돌체 해시와 동일 촬영 구도 코드. phase="before"를 "after"로 바꿔 별도 출력한다.
- view output path를 필요하면 변경한다. C#은 in-memory MCP code body이고 게임 런타임 코드로 설치하는 것이 아니다.

원본/후보/모든 큰 사진의 작업 폴더(같은 PC):
C:/Users/kerri/.codex/visualizations/2026/11/13/01a1d3d3-711a-7b73-a105-b8cb92b698cf/course_mesh_pass2/
- original/ : 2차 전 FBX와 meta(=1차 적용 상태).
- candidate/ : 선택 후보 파일. 실제 적용은 hall, boats 두 개만.
- normal_preserved/ : 법선 유지 후보, 각도별 파일과 정면/측면·보트 확대 사진.
- unity_*_before.png : 실제 Unity 2차 전 모형 사진. **2차 후 Unity 사진은 아직 없음.**

복원:
- 11-13 Unity를 다시 켤 때 Temp가 비워져 Temp 백업(1차 전·2차 전·3차 전)은 모두 없어졌다.
- 최초 상태(1차 전): git HEAD(ddc8e628)의 같은 경로 FBX·.meta.
- 2차 전(=1차 적용) 상태: 위 Codex 폴더 original/ (벽·보트·홀·조각상 4개).
- 3차 전 낙하 나이트(1차 적용 상태)는 남은 사본이 없다.
- 백업은 Temp가 아니라 ArtSource 아래처럼 Unity가 지우지 않는 곳에 둔다.
- 개별 파일 복원 시 원래 상대 경로의 FBX/.meta를 사용한다. 전체 git reset/checkout/discard로 사용자 변경을 지우지 않는다.
- 처음부터 같은 후처리를 반복 적용하지 않는다. 1차/2차 입력 상태를 구분한다.

## 5. 검사 명령과 작업 중 주의

Windows Core·모의 세션:
& 'C:/Work/ChessFight/Tools/Test-NetworkCore.ps1' -UnityEditor 'C:/Users/kerri/OneDrive/문서/6000.3.11f1/Editor'

원본 모델 .blend/제작기는 수정하지 않았으므로 재내보내면 후처리가 덮일 수 있다.
기존 사용자/다른 세션의 UI·소리·네트워크·Packages 변경을 stage/discard하지 않는다.
다른 세션이 HEAD에 UI 커밋을 추가했다. 이 감축 작업은 커밋하지 않았으며 해당 커밋을 되돌리지 않는다.
Unity Play를 강제로 정지하거나 사용자의 씬을 저장하지 않는다.
말은 한국어, 코드 주석은 영어. 사용자 실기 확인 전에는 '검증됨'으로 기록하지 않는다.
