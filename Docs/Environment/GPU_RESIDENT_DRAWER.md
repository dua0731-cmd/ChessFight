# GPU Resident Drawer · GPU 가림 컬링 시험 (R719, 2026-11-19) — 켜지 않음

> **결론:** AI가 프로젝트 사본에서 재 보니(이 노트북 · Intel Iris Xe), GPU Resident Drawer(GRD)는 화면을 바꾸지 않았지만 **그리기 CPU를 줄이지 못했고 GPU를 11~23% 더 썼다**. GPU 가림 컬링은 **바닥 · 벽 · 천장 · 성문 같은 큰 메시를 빼먹었다**(여러 장면, 가만히 있어도). 승규 결정(11-19): **둘 다 켜지 않음**, '낮음'(Mobile) 그대로, 빌드 측정 안 함. **프로젝트 설정 파일은 바꾸지 않았다.** 다시 검토할 때는 RTX 데스크톱에서 같은 도구(`Tools/GrdProbe`)로 잰다.

모든 숫자 · 사진은 **AI 측정 · AI 확인**이다(사본의 Unity를 배치 모드로 돌림). 사용자가 Unity에서 본 것이 아니고, 실제 게임 FPS · 플레이어 빌드 · 12명 경기도 아니다.

## 1. 왜 시험했나

- 출처: [ROYAL_LIBRARY_PERF §5](../PawnRush/ROYAL_LIBRARY_PERF.md#5-결정-승규-11-18) — "GPU Resident Drawer + GPU 가림 컬링은 자이언트 체스 코스 마무리 뒤 다른 Claude 계정에서"(승규 11-18).
- GRD는 CPU가 하던 그리기 준비와 컬링 일부를 GPU(BatchRendererGroup)로 넘기는 URP 기능이다. CPU가 병목인 PC에서 프레임이 오르고, GPU가 병목이면 거의 그대로라고 알려져 있다. 화면 모습은 같아야 하고, 물리(PhysX · 래그돌)는 옮겨지지 않는다.
- 노린 것: 물리 담당 PC의 그리기 CPU가 줄면 물리에 쓸 여유가 생기고, 물리 담당 점수(`HostFitness.WithFrames`: 평균 프레임 25 ms를 넘으면 점수가 그만큼 깎임)와 "방장이 느려서 넘김"(`FrameMonitor`: 평균 40 ms가 5초 넘게)에 여유가 생긴다.
- GPU 가림 컬링은 GRD 위에서 돈다. 이 게임은 모델을 Play 때 코드로 깔아 미리 굽는 오클루전을 못 쓰므로 대안이 될 수 있는지 봤다.

## 2. 결정 (승규 11-19)

| 항목 | 결정 |
|---|---|
| GRD (PC 품질, `PC_RPAsset`) | **지금은 켜지 않음** — RTX 데스크톱에서 다시 잰 뒤 정함 |
| GPU 가림 컬링 | **켜지 않음** |
| '낮음' 프리셋(`Mobile_RPAsset` · `Mobile_Renderer`) | 그대로 |
| 빌드 전후 측정(시간 · 크기 · 셰이더 변형 수) | 이 노트북에서는 안 함(빌드한 적이 없어 셰이더를 처음부터 컴파일해야 함) |

## 3. 켜려면 바꿀 것 (지금 값 → 켤 때 값)

| 파일 · 칸 | Unity 화면 | 지금 | 켤 때 |
|---|---|---|---|
| `ProjectSettings/GraphicsSettings.asset` `m_BrgStripping` | Project Settings → Graphics → Shader Stripping → BatchRendererGroup Variants | 0 (Keep if Entities Graphics) | **2 (Keep All)** |
| `Assets/Settings/PC_RPAsset.asset` `m_GPUResidentDrawerMode` | PC_RPAsset → Rendering → GPU Resident Drawer | 0 (Disabled) | 1 (Instanced Drawing) |
| 〃 `m_GPUResidentDrawerEnableOcclusionCullingInCameras` | 〃 → GPU Occlusion Culling | 0 | 1 (가림 컬링도 켤 때만) |

- **Keep All의 저장 값은 2다**(6000.3.11f1의 `UnityEditor.Rendering.BatchRendererGroupStrippingMode`: 0 KeepIfEntitiesGraphics · 1 StripAll · 2 KeepAll). 1은 정반대(모두 뺌)다. 코드 속성 `EditorGraphicsSettings.batchRendererGroupShaderStrippingMode`는 이 버전에서 읽기 전용이다([PITFALLS 54](PITFALLS.md)).
- 두 칸은 같이 바꾼다. BRG 변형이 Keep All이 아니면 에디터에서도 GRD가 경고("BatchRendererGroup Variants setting must be Keep All")와 함께 꺼지고, 빌드에서는 DOTS 인스턴싱 셰이더 변형이 빠진다.
- 켜지는 조건(URP 17.3 소스로 확인): 렌더러 목록의 **모든** 렌더러가 Forward+ 또는 Deferred+(`PC_Renderer` 하나, Forward+ ✓), SRP Batcher(켜짐 ✓), Render Graph(호환 모드 0 ✓ — 가림 컬링에 필요). 에디터에서는 Play 중에만 켜진다. 가림 컬링은 Game · Scene 카메라에만 돈다.
- Player Settings의 Static Batching이 켜져 있어 URP 인스펙터가 "GPU 그리기에는 권장하지 않음" 안내를 띄울 수 있다. 장면 · 프리팹에 static 표시된 물체가 0개라 실제 영향은 없고, 전역 설정이라 그대로 둔다.
- **Unity가 켜진 채로 `GraphicsSettings.asset` 파일을 고쳐도 새로고침(포커스) 때 다시 읽는다**(AI 시험: 파일에 2를 쓰고 새로고침 → 0에서 2로). 그래서 켤 때는 Unity를 끄지 않고 두 파일의 그 줄만 고쳐도 되고, git diff는 그 줄만 바뀐다.

## 4. 측정 환경 · 방법

| 항목 | 값 |
|---|---|
| PC | ASUS Vivobook X1502ZA 노트북, Core i5-1240P(12코어 16스레드), RAM 16 GB, **Intel Iris Xe 내장 그래픽(외장 GPU 없음)**, 전원 연결, 1920 × 1080 |
| Unity · API | 6000.3.11f1, URP 17.3, Direct3D 11 (level 11.1) — 그래픽 API는 Auto 그대로 |
| 어디서 | 프로젝트 사본 `C:\유니티\ChessFight_TestCopy`의 Unity를 배치 모드(그래픽 켬)로. 사용자 에디터는 켜 둔 채 |
| 장면 | 코스 프리팹 시험 5번 왕의 서재 · 거인 체스(`Late_3-1_RoyalLibrary`), 구간 좌표 기준 세 구도: 체스 방 한가운데 (0, 7, 28) → (0, 3, 62) · 앞 서재 입구 (0, 5, −132) → (0, 3, −95) · 위에서 (0, 50, −8) → (0, 0, 70) |
| 잰 것 | 카메라 하나를 1920 × 1080 대상에 그리는 시간 — **CPU**: `Camera.Render`가 돌아올 때까지, **합계**: GPU가 다 그릴 때까지 기다린 시간까지, **게임 루프**: 같은 프레임의 `PlayerLoop`(스크립트 · 물리 · GRD의 프레임마다 관리) |
| 반복 | 위치 × (끔 · GRD · GRD + 가림) × 30프레임 × 3회, 순서를 번갈아 · 중앙값 |
| 도구 | `Tools/GrdProbe/Run-GrdProbe.ps1 -Mode baseline \| ab \| sweep` (사본을 맞추고 도구를 사본에만 넣어 돌림; 결과 `<사본>/Logs/GrdProbe/<mode>`) |

**한계:** 배치 모드는 렌더 스레드가 없다(그리기가 한 스레드). 그래서 Stats 창의 "CPU main · render thread"를 따로 나누지 못하고, `Gfx.WaitForPresentOnGfxThread`도 없다. Unity의 Batches · SetPass · Draw Calls · Triangles 카운터는 배치 모드에서 프로파일러를 켜도 0이라 쓰지 못했다(Game 창 Stats로만 볼 수 있음, [PITFALLS 56](PITFALLS.md)). 사용자 에디터가 같이 켜져 있어 같은 조건도 ±1 ~ 1.5 ms 흔들린다. 노트북 · 내장 그래픽 숫자이지 RTX 데스크톱 숫자가 아니다.

## 5. 결과 — 왕의 서재 (AI 측정, ms, 중앙값)

### 5-1. 켜기 전 기준선 (설정 그대로, 위치마다 90프레임)

| 위치 | CPU | 합계(+GPU) | 게임 루프 |
|---|---:|---:|---:|
| 체스 방 한가운데 | 2.56 | 18.17 | 3.16 |
| 앞 서재 입구 | 3.91 | 24.02 | 3.74 |
| 위에서 | 2.80 | 20.18 | 3.34 |

합계의 대부분이 GPU를 기다린 시간이다 → 이 노트북은 **GPU 병목**이다(원래 PC의 [경량화 조사 §8](../PawnRush/COURSE_OPTIMIZATION_2026-11-13.md)과 같은 결론).

### 5-2. 끔 · GRD · GRD + 가림 컬링 (BRG Keep All, 한 Play 안에서 번갈아, 위치 · 방식마다 90프레임)

| 위치 | 방식 | CPU | 합계(+GPU) | 게임 루프 | GRD가 그린 물체 / 명령 |
|---|---|---:|---:|---:|---|
| 체스 방 한가운데 | 끔 | 2.59 | 17.03 | 2.92 | — |
| | GRD | 2.84 | **19.66 (+15%)** | 3.80 | 134 / 109 |
| | GRD + 가림 | 2.95 | 15.66 | 3.35 | 134 / 109 |
| 앞 서재 입구 | 끔 | 3.96 | 22.69 | 3.59 | — |
| | GRD | 4.04 | **27.81 (+23%)** | 4.93 | 541 / 289 |
| | GRD + 가림 | 4.07 | 20.09 | 3.70 | 539 / 287 |
| 위에서 | 끔 | 2.76 | 18.60 | 3.23 | — |
| | GRD | 2.76 | **21.19 (+14%)** | 3.89 | 232 / 144 |
| | GRD + 가림 | 3.03 | 16.37 | 3.47 | 232 / 144 |

그림자 쪽 GRD 물체 / 명령: 81 / 60 · 95 / 79 · 31 / 28. 표는 2회차다. **3회차**(저장소의 `Run-GrdProbe.ps1` 그대로, 같은 조건): GRD 합계 16.38 → 19.52(+19%) · 22.88 → 25.39(+11%) · 19.05 → 21.94(+15%), CPU −0.16 ~ +0.18 ms, 게임 루프 +0.7 ~ 1.0 ms — 같은 경향.

읽는 법:

- **GRD는 이 장면의 그리기 CPU를 줄이지 못했다**(−0.16 ~ +0.24 ms, 2 · 3회차). 같은 메시가 여러 번 나오는 일이 적어(서재 입구: 물체 541개 → 그리기 명령 289개, 명령 하나에 2개꼴) 묶어 그릴 것이 별로 없고, SRP Batcher가 이미 싸게 그린다. GRD가 크게 이득을 보는 것은 같은 나무 · 돌을 수천 번 그리는 장면이다.
- **GPU는 11 ~ 23% 더 썼다**(합계 기준, 2 · 3회차). DOTS 인스턴싱 셰이더가 물체마다 데이터를 큰 버퍼에서 읽는 비용으로 추정한다(내장 그래픽에서 특히 클 수 있음).
- **게임 루프는 +0.7 ~ 1.4 ms**(2 · 3회차) — GRD가 움직이는 물체(거인 · 기물 · 도장 · 책 · 타자기)의 위치를 매 프레임 GPU로 올리는 관리 비용으로 추정한다. 물리 담당 PC에는 오히려 부담이다.
- GRD + 가림의 합계가 끔보다 작은 것은 큰 메시를 빼먹어서다(§6) — 이득이 아니다.
- BRG Keep All만 켠 상태(GRD 끔)는 기준선과 같은 수준이다(에디터에서는 빌드 셰이더 설정이라 영향 없음).
- 1회차는 렌더링 디버거용 GRD 통계를 켠 채 쟀더니 GRD 쪽이 3 ~ 4 ms 더 나왔다 → GRD 통계 자체도 비용이 있다. 위 표는 통계를 끈 2회차다.

## 6. 화면 확인 (AI 확인)

같은 Play 안에서 끔 · GRD · GRD + 가림으로 같은 구도를 찍어 픽셀을 비교했다(1280 × 720, 분홍 = 셰이더 오류 색 픽셀 수). 코스 6 · 미니게임 10 · 구간 시험 · 거인 체스 단독 장면 · 소드 파이트 · 아레나 · 로비 · 인트로 · 승리 · 패배, 구도 42개.

| 구도 | 끔 | GRD | GRD + 가림 컬링 |
|---|---|---|---|
| 왕의 서재 체스 방 한가운데 | ![끔](../PawnRush/Checks/R719_GpuResidentDrawer/library_chess_room_off.png) | ![GRD](../PawnRush/Checks/R719_GpuResidentDrawer/library_chess_room_grd.png) | ![GRD + 가림](../PawnRush/Checks/R719_GpuResidentDrawer/library_chess_room_grd_occlusion.png) |
| 마차뜰 입구(플레이어 시점) | ![끔](../PawnRush/Checks/R719_GpuResidentDrawer/carriage_yard_entry_off.png) | (끔과 같음, 0.9% — 움직이는 마차) | ![GRD + 가림](../PawnRush/Checks/R719_GpuResidentDrawer/carriage_yard_entry_grd_occlusion.png) |

사진은 [Checks/R719_GpuResidentDrawer](../PawnRush/Checks/R719_GpuResidentDrawer/)에 다섯 장만 남겼다. 나머지(장면마다 끔 · GRD · 가림 + 차이 지도)는 §10의 사본 폴더에 있다.

- **GRD만:** 분홍 0, 사라진 물체 없음. 끔과 다른 곳은 움직이는 것(도는 막대 · 물결 · 캐릭터 · 혼천의 · 도장)뿐이었다. 서재 문 앞에서 빠르게 360° 돌기 · 문을 60 m 달려 지나기(각 90프레임)를 손으로 그린 것과 게임처럼(Unity가 카메라를 스스로) 그린 것 모두 **0프레임 차이**(2 · 3회차). 콘솔에 GRD · BatchRendererGroup · DOTS 경고 0.
- **GRD + 가림 컬링: 큰 메시가 사라졌다.** 카메라가 순간이동한 첫 프레임만이 아니라 같은 자리에서 10번 그린 뒤에도, 게임처럼 그려도 그대로였다(돌기 · 달리기에서 83 ~ 90 / 90프레임, 최악 프레임 72 ~ 86%가 다름).

| 장면 · 구도 | 가림 컬링 때 끔과 크게(48/255 넘게) 다른 픽셀 | GRD만 |
|---|---:|---:|
| 마차뜰 입구 — 바닥 · 성문 · 탑 · 벽이 모두 없고 마차 · 말 · 크레인만 하늘에 뜸 | 96.7% | 0.9% |
| 마차뜰 위에서 | 63.5% | 0.5% |
| 대무도회장(F) 위에서 | 63.4% | 0.2% |
| 침실(I) 위에서 | 57.3% | 0% |
| 왕의 서재 위에서 — 벽 · 천장 · 출구 아치가 없음 | 51.3% | 0% |
| 거인 체스 단독 장면 위에서 | 47.9% | 0% |
| 행차 회랑(G) · 아레나 · 도개교(C) · 체스 홀(H) | 13 ~ 25% | 0 ~ 0.2% |
| 정원 · 대계단 · 작전실 · 천문대 · A · D · J · B · E 미니게임 | 0 ~ 8% | 0 ~ 4% |

원인 후보(확정 아님): 카메라를 감싸는 큰 메시(바닥 · 벽 · 천장)가 가려진 것으로 판정된다 — Unity의 가림 컬링은 물체를 **경계 구**로 근사한다([Unity 문서](https://docs.unity3d.com/Manual/urp/gpu-culling.html)). 서재의 벽처럼 Play 때 코드가 만드는 메시(`RoyalLibraryDressing.Reskin`)도 있는데, "GRD를 켜면 런타임 메시가 사라짐" 이슈([UUM-108782](https://issuetracker.unity3d.com/issues/runtime-generated-meshes-disappear-when-moving-the-camera-from-the-meshes-center-and-gpu-resident-drawer-is-enabled), 6000.0.51f1에서 고침)와 비슷하다. Intel Iris Xe · D3D11 · 배치 모드에서만 본 것이라 다른 GPU에서도 그런지는 모른다.

## 7. 켜도 GRD에서 빠지는 것 (예전 방식으로 그려짐)

- **자체 셰이더:** `.shader` 103개 중 DOTS 인스턴싱을 지원하는 것은 마차뜰 `RcyTintLit` 하나(셰이더 그래프 0개). `PR_Library*` · `PR_Palace*` · `PR_Garden*` · 물 · `CourseGlass` · `CoursePattern` · `NetworkColor` · `CFCharacter` · 스킬 · VFX 셰이더는 빠진다. 재질 에셋 658개 중 URP Lit 497 · URP Unlit 6 · `RcyTintLit` 3(약 77%)이 대상 셰이더다. 코드가 `Shader.Find("Universal Render Pipeline/Lit")`로 만드는 재질(5개 파일)은 Lit라 대상이다.
- **MaterialPropertyBlock을 쓰는 렌더러:** `Assets/Scripts`의 81개 파일(팀 색 `SkillTeamTint`, 책 리본 `RoyalLibraryDressing.Books`, `MiniGameModels*`, VFX · 스킬 효과).
- **스킨드 메시:** 래그돌 기물, 거인 2명.
- 렌더러 하나만 빼려면 `DisallowGPUDrivenRendering` 컴포넌트(SRP Core)나 `Renderer.allowGPUDrivenRendering`.

## 8. 켜는 법 · 되돌리는 법 (다시 검토할 때)

- **켜기:** Play가 아닐 때 §3의 두 줄(가림 컬링은 세 번째 줄)을 바꾼다 — Unity 화면에서 바꾸거나, 파일을 고친 뒤 Unity에 포커스. `git diff`로 그 줄만 바뀌었는지 본다. 에디터 Play 중에는 `PipelineSettings.Writable()`이 RP 에셋 사본("PC_RPAsset (play)")을 쓰므로 그때 바꾼 값은 사라진다.
- **되돌리기:** PC_RPAsset → GPU Resident Drawer = Disabled, GPU Occlusion Culling 끔. BRG 변형은 원래 값(0). GRD를 끄기만 할 거면 Keep All로 둬도 동작에는 문제없다(빌드만 조금 커짐). 커밋 전이면 `git checkout -- ProjectSettings/GraphicsSettings.asset Assets/Settings/PC_RPAsset.asset`.
- '낮음'(Mobile)에도 켜려면 `Mobile_Renderer`를 Forward → Forward+로 바꿔야 한다(조명 방식이 바뀌어 '낮음' 화면이 달라질 수 있음). 설정창에서 프리셋을 바꾸면 품질 단계와 RP 에셋이 실행 중에 바뀌므로(`GameSettings.ApplyGraphics`), 켠다면 코스 안에서 낮음 ↔ 높음을 여러 번 바꿔 본다.

## 9. 남은 것 · 후속

- **RTX 데스크톱(GPU가 빠르고 CPU가 병목인 PC)에서 다시 재기:** `Tools/GrdProbe/Run-GrdProbe.ps1 -Mode ab -Copy <그 PC의 사본 경로>`. 이 노트북에서 CPU 이득이 0이었으므로 데스크톱에서도 이득이 없을 가능성이 높다고 **추정**하지만 재 보지 않았다. 가림 컬링의 큰 메시 누락이 그 GPU에서도 나는지도 같이 본다(`-Mode sweep`).
- 가림 컬링 누락의 원인 확인(경계 구 · 런타임 메시) — Unity 새 버전에서 다시.
- 빌드 시간 · 크기 · 셰이더 변형 수: 켜기로 하면 데스크톱에서 개발 빌드 전후로.
- 그래픽 API: Auto 그대로(이 노트북은 DX11). DX12는 시험하지 않았다.
- 자체 셰이더의 DOTS 인스턴싱 지원 · property block을 재질로 바꾸기: GRD를 켜기로 하고 이득이 확인될 때만 검토.
- 작은 메시 컬링(Small-Mesh Screen-Percentage 0): 그대로.

## 10. 원본 숫자 · 사진

사본의 `C:\유니티\ChessFight_TestCopy\Logs\GrdProbe\` — `baseline`(기준선), `ab_run1_statsOnWhileTiming`(1회차, 통계 켬), `ab`(가장 최근 A/B), `sweep`(장면 전체). 각 폴더의 `report.txt` · `timings.csv` · `counts.csv` · `shots.csv` · `popin.csv` · `console.txt` · `shots/*.png`(`*_DIFF.png` = 다른 곳 빨강 · 노랑). 사본은 git 밖이라 사본을 지우면 함께 없어진다. 숫자는 이 문서의 표가 기준이다.
