# VFX: 기물별 작업대와 공통 기준 (R159, 2026-10-27)

> **상태:** 코드·자산 파일 작성, Core·세션 시험과 전 어셈블리 컴파일 통과. **Unity에서 사람이 아직 보지 않았다.**
> 브랜치: `skill-network-test`(사용자 "브랜치 스킬넷에서 테스트 돌릴거고").

언리얼 Niagara 시안 4종(Fire Beam · Gothic Blood Slash · Cosmic Coil Dash · Vengeful Twilight of Terror)의 룩을
URP + VFX Graph로 다시 만드는 작업의 바탕이다. **기물 하나씩 따로** 만들고 볼 수 있게, 작업대 씬에서 기물을 고르면
그 기물 폴더의 이펙트 프리팹이 목록에 뜬다.

## 1. 프로젝트 점검 결과 (10-27)

| # | 항목 | 점검 때 | 이번에 한 것 |
|---|---|---|---|
| 1 | URP Asset HDR · Opaque Texture | `PC_RPAsset`(PC 기본 품질) 둘 다 켜짐, HDR 정밀도 32-bit. `Mobile_RPAsset`도 켜짐 | 그대로 |
| 2 | Decal Renderer Feature | `PC_Renderer`에 있음(Screen Space, 1000 m), `Mobile_Renderer`에는 없음 | 그대로. Mobile 품질에서는 룬 데칼이 안 보인다(§8) |
| 3 | Global Volume | 씬에 저장된 볼륨 없음. URP 에셋의 기본 프로필 `Settings/GameLook.asset` + Play 때 `GameLook.cs`가 만드는 볼륨 | 작업대 씬에만 시험용 볼륨(§6) |
| 4 | VFX Graph 패키지 | 없음(Shader Graph 17.3.0은 URP와 함께 있음, Timeline 없음) | `com.unity.visualeffectgraph` **17.3.0** 추가(manifest · lock) |
| 5 | 카메라 Post Processing | 씬 파일에는 `PawnRush_Course01`만 켜짐. Play 때는 `GameLook.cs`가 모든 화면 카메라에 켬 | 작업대 카메라는 씬 파일에 켜 둠(편집 모드 Game 뷰에서도 블룸) |

그 밖: Render Graph 켜짐, Linear 색 공간, SRP Lens Flare · Screen Space Lens Flare 지원 켜짐(4번 이펙트에 필요).

## 2. 폴더

`Assets/` 바로 아래에 기능 이름 폴더를 두지 않는 규칙(DECISIONS T7)의 **예외**다. 사용자가 이 구조를 지정했다(DECISIONS FX1).

```text
Assets/VFX/
├─ Scenes/VFX_Workbench.unity   작업대 씬 (빌드 목록에 없음)
├─ Prefabs/
│  ├─ Common/                   모든 기물 목록에 뜨는 공용 이펙트 (샘플 00_HDR_BloomCheck)
│  └─ King/ Queen/ Rook/ Bishop/ Knight/ Pawn/   기물별 이펙트 프리팹
├─ Graphs/                      VFX Graph (.vfx)
├─ Shaders/                     Shader Graph
├─ Materials/                   VFX_Floor(작업대 바닥), VFX_HDR_x1 · x2_5 · x6(HDR 견본)
├─ Textures/                    노이즈 · 그라디언트 · 스트릭
├─ Meshes/                      슬래시 아크 · 나선 리본 등
└─ Settings/VFX_Workbench_Volume.asset   작업대에서 시험하는 블룸
Assets/Scripts/VFX/             어셈블리 ChessFight.VFX (참조: ChessFight.Game)
├─ VfxCue.cs                    이펙트 프리팹 루트: 시작 위치 · 타이밍 · 그래프 입력
└─ VfxWorkbench.cs              작업대
```

- 빈 폴더에는 `.gitkeep`을 둔다(Git은 빈 폴더를 저장하지 않고, 폴더가 없으면 Unity가 `.meta`를 지운다).
- 여러 사람이 동시에 일할 때: **기물 폴더 하나 = 한 사람.** 작업대 씬은 고칠 필요가 없다(기물 선택은 저장되지 않음).

## 3. 작업대 쓰는 법

1. Unity에서 `Assets/VFX/Scenes/VFX_Workbench.unity`를 연다.
2. **편집 모드:** Hierarchy의 `VFX Workbench`를 고르고 Inspector의 `Piece`를 바꾸면 무대(`VFX Workbench Stage`)에 그 기물이 선다.
   앞 6 m에 과녁 폰이 있다. 무대는 **씬에 저장되지 않고** 고칠 수도 없다(기준으로만 쓴다).
3. **이펙트 만들기:** `Assets/VFX/Prefabs/<기물>/`에 프리팹을 만든다. 이름 앞 숫자로 순서를 정한다(`01_FireBeam`).
   루트에 `VfxCue`를 붙이면 시작 위치 · 타이밍을 정할 수 있다(§4). 없으면 가슴에서 3초.
4. **Play:** 왼쪽 위 패널의 키로 본다. 새 프리팹은 다음에 키를 누를 때 목록에 들어온다(따로 등록하지 않는다).

| 키 | 하는 일 |
|---|---|
| 1~6 | 기물: 킹 · 퀸 · 룩 · 비숍 · 나이트 · 폰 |
| ← / → (Q / E) | 이펙트 고르기 (기물 폴더 + `[공용]`) |
| Space | 재생 |
| L | 반복 켜기/끄기 (끝나고 0.6초 뒤 다시) |
| C | 재생 중인 것 지우기 |
| T | 속도 ×1 → ×0.5 → ×0.25 → ×0.1 |
| P | 멈춤 (정지 화면 캡처용) |
| G | 밀도 1.0 → 0.6 → 0.35 (§5) |
| B | 블룸: 게임 기준 ↔ 작업대 볼륨 (§6) |
| N | 톤매핑: 게임 ACES ↔ Neutral (R172, 톤매핑만 바꾸는 볼륨 `Bench Tonemap (Neutral)`, 우선순위 60, 시작은 꺼짐. 게임은 그대로) |
| V | 과녁 보이기/숨기기 (위치는 그대로) |
| H | 패널 숨기기 |
| 오른쪽 드래그 / 가운데 드래그 / 휠 / F | 회전 / 이동 / 거리 / 처음 시점 |

기물 모델은 로비와 같은 `Resources/MenuPieces/<기물>`(경기 모델, 바인드 포즈, 발이 y = 0, +Z를 봄)이다.

## 4. 이펙트 프리팹 규칙: `VfxCue`

| 칸 | 뜻 |
|---|---|
| `anchor` | 시작 위치: Feet(발) · Chest(가슴, 키의 55%) · Head(머리) · Target(과녁 가슴) |
| `follow` | 켜면 기물을 따라간다(대시). 끄면 시작한 자리에 남는다 |
| `length` | 이펙트 길이(초). 작업대는 이 뒤 2초에 지우고, 반복이면 0.6초 뒤 다시 튼다 |
| `steps` | 시간표. 각 줄: `time`(초) · `part`(자식) · `act` · `vfxEvent` |

`act`: **VfxEvent**(VFX Graph 이벤트 보내기, 비우면 `OnPlay`) · **TurnOn** · **TurnOff** · **PlayParticles** · **StopParticles**.

- 시간표로 켜는 그래프는 Visual Effect 컴포넌트의 **Initial Event Name을 비운다.** 그대로 두면 켜질 때 한 번, 시간표에서 또 한 번 재생된다.
- 그래프가 아래 이름의 Exposed Property를 만들어 두면 `VfxCue`가 값을 넣어 준다.

| Exposed Property | 형 | 값 |
|---|---|---|
| `TargetPosition` | Vector3 (World) | 과녁 가슴 위치 (빔 끝 · 충격 지점). 나중에 스킬에서는 맞는 기물 |
| `Density` | float | 밀도 배수 (§5) |

타이밍은 VFX Graph 이벤트 + `VfxCue` 시간표로 한다. Timeline 패키지는 넣지 않았다. 필요해지면 그때 추가한다.

## 5. 밀도: Capacity 대신 `Density`

VFX Graph의 **Capacity는 Exposed Property로 뺄 수 없다**(그래프를 컴파일할 때 정해지는 값). 그래서 이렇게 한다.

1. Capacity는 **좋은 PC 기준 최대치**로 잡는다.
2. 그래프에 float Exposed Property `Density`(기본 1)를 만들고, **Spawn 개수 · 비율에 곱한다.**
3. 약한 노트북에서는 `VfxCue.Density`를 낮춘다(작업대 G: 1.0 / 0.6 / 0.35). 나중에 설정 창에 연결하면 된다.

Capacity만큼 GPU 메모리는 잡히지만 시뮬레이션 비용은 대부분 살아 있는 입자 수를 따른다.

## 6. HDR · 블룸 공통 기준

**게임 기준(바꾸지 않음):** `Settings/GameLook.asset`

| 항목 | 값 |
|---|---|
| Bloom | Threshold **1.2**, Intensity **0.3**, Scatter 0.6, High Quality Filtering 끔, Max Iterations 6 |
| Tonemapping | ACES |
| Color Adjustments | 노출 +0.6, 대비 16, 채도 14 (Intro 밖 씬은 Play 때 대비 4 · 채도 5 · 비네트 0.08로 덮음) |
| 스킬 시험 씬 | `PawnRushSkillBloom`이 Threshold 리니어 2.2 · Intensity 0.55로 덮음(우선순위 100) |

**작업대 볼륨(B, 기본 꺼짐):** `VFX/Settings/VFX_Workbench_Volume.asset`, Threshold **0.9**, Intensity **0.5**(계획의 시작값), 우선순위 50.
값은 Play 중에 이 에셋에서 바로 고쳐 보며, 게임 전체로 옮길지는 사람이 타깃 PC에서 보고 정한다.

**왜 게임 전체를 0.8~1로 내리지 않나:** URP Threshold는 감마 값이다. 1.2는 리니어 약 1.49이고, 0.9는 약 0.79이다.
스킬 시험 바닥은 조명을 받으면 리니어 1.7까지 나왔다. Threshold 1.15에서 화면 전체가 하얗게 번진 기록도 있다
(`PawnRushSkillBloom` 주석). 그래서 이펙트가 **게임 기준 위로 충분히 밝게** 나오게 만드는 쪽을 기준으로 한다.

**HDR 밝기 사다리** (리니어 배수 기준. HDR 색 선택기의 Intensity I ≈ 2^I 배):

| 용도 | 배수 | Intensity | 게임 블룸에서 |
|---|---|---|---|
| 몸통 · 테두리 | ≤ 1.2 | ≤ 0.3 | 거의 안 번짐 |
| 은은한 빛 | 1.5 ~ 3 | 0.6 ~ 1.6 | 막 번지기 시작 |
| 뜨거운 심 | 4 ~ 8 | 2 ~ 3 | 확실히 번짐 |
| 순간 섬광 (0.1초 이하) | 10 ~ 20 | 3.3 ~ 4.3 | 화면이 번쩍 |

**색 규칙:**

| 이펙트 | 기준 색 (리니어) | 규칙 |
|---|---|---|
| Fire Beam | 주황 (1, 0.45, 0.12) | 심 ×6, 테두리 ×2, 끝 불꽃은 섬광 |
| Gothic Blood Slash | 빨강 (1, 0.02, 0.02) | **R만 높게**, G · B는 R의 5% 이하. 가운데만 R ×1.5 정도로 살짝 밝게 |
| Cosmic Coil Dash | 파랑 (0.25, 0.45, 1) | 리본 ×2~3, 코어 ×4, 별은 점 크기 섬광 |
| Vengeful Twilight | 금 (1, 0.7, 0.3) + 파랑 반점 | 빛줄기 ×1.5, 래디얼 플래시는 섬광 |

**샘플 `Prefabs/Common/00_HDR_BloomCheck`:** 같은 주황 구슬 세 개가 0 · 0.4 · 0.8초에 켜진다(×1 · ×2.5 · ×6).
게임 기준에서는 ×1이 번지지 않고 ×2.5부터 번져야 한다. B로 바꾸면 ×1도 살짝 번진다.

## 7. 다음 순서 (한 번에 하나, 확인받고 다음)

1. **공용 에셋:** Shader Graph 3종(스크롤 + 노이즈 디졸브 가산, HDR Color · 왜곡(Scene Color UV 오프셋) · 프레넬 고스트), 텍스처(노이즈 · 그라디언트 · 스트릭)
2. **Fire Beam:** 스트립 빔 + 임팩트 스파크(VFX Graph). `TargetPosition`으로 끝점
3. **Gothic Blood Slash:** 슬래시 아크 · 피 트레일 메시(스크롤 + 디졸브), 방향성 스플래터, 안개
4. **Cosmic Coil Dash:** 나선 리본 메시(UV 스크롤), 별빛, 원통 코어, 바닥 룬 데칼(`follow` 켬)
5. **Vengeful Twilight of Terror:** 룬 데칼, 원뿔 빛줄기, 에테리얼 모트, 충격파 왜곡, 팬텀 잔상(스킨드 메시 스냅샷 + 프레넬), 연기, 아나모픽 렌즈 플레어(SRP Lens Flare), 래디얼 플래시, 서브서피스 블룸, 상승 잔불

어느 기물에 넣을지는 사용자가 정한다. 공용으로 만들어 `Common/`에서 시험한 뒤 기물 폴더로 옮기면 된다.
**메시:** 블렌더가 필요한 형태는 스펙을 먼저 적어 확인받는다. 아크 · 원통 · 원뿔 · 고리 · 나선 같은 간단한 것은 스크립트로 만든다.

## 8. 주의

- `ChessFight.VFX`는 VFX Graph 패키지 없이도 컴파일된다. `VisualEffect`는 엔진 모듈(UnityEngine.VFXModule)에 있기 때문이다(절대 규칙 2). `.vfx` 그래프를 만들고 열려면 패키지가 있어야 한다.
- `Mobile_Renderer`에는 Decal 기능이 없다. 노트북을 Mobile 품질로 바꾸면 룬 데칼이 안 보인다. 필요하면 그때 추가한다.
- 작업대 무대 · 과녁은 `HideFlags.DontSave`다. Hierarchy에 보여도 저장되지 않는다.
- 작업대는 에디터 전용(폴더를 AssetDatabase로 읽음)이다. 빌드에서는 이펙트 목록이 비어 있다.

## 9. 기물별 F · E 이펙트

### 킹 (R160, 10-27)

> **R180(10-28, 사용자 "킹 E,F 만들거야"): 새 방식(§11)으로 다시 만든다.** 층 계획 F1~F5(얼굴 F3 반격 베기) · E1~E6(얼굴 E4 착지 — 지금 착지 `10_King_E_Land_New`를 그대로 둘지 답 대기) · 레퍼런스 프롬프트 · 색(앤틱 금색 `#C9952F`)은 [VFX_Progress](VFX_Progress.md) "킹" 절. 아래 표의 프리팹은 새 층이 생길 때마다 그 순간만 게임에서 빠진다(새 프리팹 `21~25` · `31~36`, 예전 파일은 지우라고 할 때까지 둠).
>
> **R183(10-28): F3 반격 베기 — 사용자 확인(캡처, 다른 점 3개는 그대로 · 게임 Play 전).** 반격(`F_Counter`)은 이제 `23_King_F_Counter`: 킹 오른 어깨를 도는 금빛 초승달 띠(바깥 1.6 m · 굵기 0.55 m, 칼이 지나는 면에 눕히고 카메라 쪽으로 60%), 칼과 같이 0.34초에 그려지고(맞는 0.085초에 70%) 시작 쪽 · 안쪽부터 타 들어가 0.75초에 사라짐. 새 부품 `VfxCrescent` · 셰이더 `VFX_CrescentAdd`(바깥 가장자리 흰 선 + 금빛 결 무늬), 빌더 `KingVfxBuilder.cs`(메뉴 `ChessFight/VFX/King/Build`), 재질 `M_King_Counter`(`#CC851F` — ACES에서 `#C9952F` 쪽으로 보이게 진하게). 아래 표의 `03_King_F_Counter`(고리)는 게임에서 빠짐. 실사 `KingSlash`와 같이 나옴(끌지 답 대기). 자세한 값 · 캡처 · 다른 점은 [VFX_Progress](VFX_Progress.md) "F3 반격 베기".
>
> **R185(10-28): F2 막음 — 사용자가 그대로 두기로 함(캡처, 다른 점 3개 안 고침 · 게임 Play 전).** 막음(`F_Block`)은 이제 `22_King_F_Parry`: 칼이 부딪힌 자리(칼날 위)에 금빛 별 섬광(가로로 3.5배 긴 판 — 따뜻한 흰 심 · 가는 빛줄기 22개 · 대각선 십자 · 가로 빛줄기, 1.2 m에서 0.6 m로 줄며 0.14초에 꺼지고 가로 줄만 0.22초까지) + 금빛 쇳조각 60개(0.18~0.45초, 공격자 쪽 위로 튀었다 떨어짐). 킹 몸에 가려도 35% 보임(두 번째 그리기 = 가려진 곳). 새 부품 `VfxStarFlash` · 셰이더 `VFX_StarFlashAdd`, 메뉴 `ChessFight/VFX/King/Build F2 (block only)`, 재질 `M_King_Star` · `M_King_Sliver`. 아래 표의 `02_King_F_Block`은 게임에서 빠짐. 실사 `SteelClash`(흰 번쩍 · 쇳불꽃)와 같이 나옴(끌지 답 대기). 자세한 값 · 캡처 · 다른 점은 [VFX_Progress](VFX_Progress.md) "F2 막음 별 섬광".
>
> **R187(10-28): F4 맞음 — 사용자가 그대로 두기로 함(캡처, 다른 점 3개 안 고침 · 게임 Play 전).** 반격에 맞은 적마다 새 키 `F_Hit`(예전 방식에는 없던 순간) = `24_King_F_Hit`: 그 기물 가슴 앞에 금빛 베인 줄 한 줄(1.8 m · 굵기 0.1 m · 42°, 킹 쪽에서 오른쪽 위 → 왼쪽 아래, 0.045초에 그려지며 번쩍 · 0.4초에 사라짐, 비숍 F7 부품 `VfxSlash`) + 금빛 조각 90개(밀리는 쪽 · 위로, 0.5초 안에 떨어짐) + 옅은 공기 줄 6개, 고리 없음, 가슴에 붙어 같이 날아감. 재질 `M_King_Cut`, 메뉴 `ChessFight/VFX/King/Build F4 (hit only)`, 게임 연결 `SwordFightSkillFx.KingHitVfx`(`KingCounterHit` 줄, 실사 킹일 때만 — 실사 `RealHit` 몸 색 나뭇조각과 같이 나옴). 자세한 값 · 캡처 · 다른 점은 [VFX_Progress](VFX_Progress.md) "F4 맞음 금빛 베인 자국".
>
> **R190(10-28): F1 자세 — 안 B 여덟 칸, 사용자가 그대로 두기로 함(캡처, 다른 점 3개 안 고침 · 게임 Play 전).** 자세(`F_Guard`)는 이제 `21_King_F_Guard`: 킹이 선 칸과 둘레 여덟 칸의 판 줄을 금빛 가는 선(0.02 m)으로, 킹 칸에서 바깥으로 0.1초에 그려지며 번쩍, 바깥 끝은 0.3칸 더 뻗어 사라짐, 칸 크기 `knightCell` 1.25 m · 킹 중심 · 킹이 보는 쪽(진짜 판 줄과 조금 어긋날 수 있음), 킹이 가운데 칸을 벗어나면 한 칸씩 따라감, 바닥 없는 칸은 안 그림 + 칼날을 손잡이 → 칼끝으로 달리는 금빛 · 칼끝 반짝 · 남는 은은한 금빛. 둘 다 자세가 끝나기 0.2초 전부터 깜빡이고 끝나면 0.06초에 꺼짐. 새 부품 `VfxSquareGrid` · `VfxBladeGlow`, 셰이더 `VFX_SquareGridAdd`, 재질 `M_King_Guard_Grid` · `M_King_Guard_Blade` · `M_King_Guard_Tip`, 메뉴 `ChessFight/VFX/King/Build F1 (guard only)`, 게임 연결 `SwordFightSkillFx.KingGuardVfx`(`RealKingCast`에서 부름 — 실사 흰 칼 반짝임 · 발밑 공기 · 금빛 조명과 같이 나옴). 아래 표의 `01_King_F_Guard`(고리 · 일렁임 벽)는 게임에서 빠짐. 자세한 값 · 캡처 · 다른 점은 [VFX_Progress](VFX_Progress.md) "F1 자세 — 안 B 여덟 칸".
>
> **R193(10-28): F5 빗나감 — 칼의 금빛이 알갱이로 흩어짐, 사용자가 그대로 두기로 함(캡처, 다른 점 3개 안 고침 · 확인은 나중에 게임에서).** 새 키 `F_Whiff` = `25_King_F_Whiff`: 자세가 헛되어 칼끝이 판 쪽으로 떨어질 때(`KingWhiff`, 예전에는 실사 킹이면 아무것도 없었음) 떨어지는 칼날 전체에서 금빛 알갱이 20개(`F5 Motes`, 0.09초까지, 천천히 가라앉음)와 칼끝 둘레 몇 개(`F5 Point`, 맴돎)가 나와 따뜻한 흰색 → 금색 → 진한 금색으로 식으며 0.4 ~ 0.85초에 사라짐. 안개 · 먼지 · 고리 없음. 빌트인 파티클 둘(재질 `M_King_Mote`, `Soft Glow Additive` 밝기 2.2), 새 부품 · 셰이더 없음, 메뉴 `ChessFight/VFX/King/Build F5 (whiff only)`, 게임 연결 `SwordFightSkillFx.KingWhiffVfx`(두 발생기를 0.45초 동안 칼의 진짜 위치에 붙임). 아래 표에는 이 순간의 예전 프리팹이 없다. 자세한 값 · 캡처 · 다른 점은 [VFX_Progress](VFX_Progress.md) "F5 빗나감".
>
> **R196(10-28): E1 칼 박기 — 별 섬광 + 깨진 벽의 금에 스며 번지는 금빛(캡처, 검토 중 · E는 비숍 채팅이 이어감).** `E_Stab`은 이제 `31_King_E_Stab`(아래 표의 `04_King_E_Stab`은 게임에서 안 씀, 파일은 남김): 칼이 판 옆면에 박히는 순간(`KingStab`) 칼이 벽면을 지나는 곳에 작은 금백색 별 섬광(F2 `VfxStarFlash` · `M_King_Star`를 다시 씀, 0.8 m · 0.1초) + 실사 깨짐(`KingBreach`, 셰이더 `ChessFight/Skill Breach`)의 금이 구덩이 테두리에서 0.55 m까지 0.3초 동안 금빛으로 스며 번지고 0.83초에 식음, 구덩이 안은 그 빛의 20%. 새 부품 `VfxCrackGlow`(깨짐이 매 프레임 읽음, 작업대 · 캡처에서는 놋쇠색 미리보기 벽에 직접 그림), 셰이더에 기본값이 예전과 같은 속성 둘(`_GlowFront` · `_GlowInside`), 재질 `M_King_StabWall` · `M_King_StabStone`, 메뉴 `ChessFight/VFX/King/Build E1 (stab only)`, 게임 연결 `SwordFightSkillFx.KingStabVfx`(`RealKingStab`의 `Land()`에서 `KingBreach` 뒤). 먼지 · 고리 없음. 자세한 값 · 캡처 · 다른 점은 [VFX_Progress](VFX_Progress.md) "E1 칼 박기". 사용자 "킹E는 다른 채팅에서 작업 할거야" → E2 ~ E6과 E1을 새 그림과 비교하는 일은 비숍 채팅(R198부터).

사용자: "킹부터 작업진행해주고 F, E스킬 적용 … 너무 화려하게 안해줬으면". 소드 파이트 킹의 **실사 연출(쇳불꽃 · 공기 · 흔들림)은 그대로 두고**
그 위에 금색(`KingGold`) 이펙트를 **얇고 짧고 낮은 밝기로** 얹었다. 모든 화면에 같이 나온다(같은 신호에서 그림).

| 순간 | 프리팹 (`Prefabs/King/`) | 더하는 것 | 세기 |
|---|---|---|---|
| F 자세 (`RealKingCast`) | `01_King_F_Guard` | 발밑 얇은 금색 고리(0.35 → 0.8 m) + 앞쪽 140° 희미한 일렁임 벽(높이 1 m), 0.9초 | 고리 ×1.6, 벽 ×0.7 |
| F 막음 (`RealKingBlock`, 비숍 손 제외) | `02_King_F_Block` | 맞닿은 곳 작은 섬광 0.09초 · 공격자 쪽을 보는 충격 고리 · 불꽃 10개 | 섬광 ×4, 고리 ×2, 불꽃 ×3 |
| F 반격 (`RealKingSpin`) | `03_King_F_Counter` | 발밑에서 1.5 m까지 퍼지는 고리 하나, 0.4초 | ×1.8 |
| E 박기 (`RealKingStab`) | `04_King_E_Stab` | 칼이 돌에 박히는 곳 작은 섬광 · 튀어나오는 불꽃 8개 | 섬광 ×3, 불꽃 ×2.5 |
| E 뛰어오름 (`RealKingVault`) | `05_King_E_Vault` | 몸 아래 공기 고리(1 m), 0.3초 | ×1.4 |
| E 뛰어오름 → 착지 지점 (`RealKingVault`, R163) | `07_King_E_Gather` | 착지할 자리 바닥에 나선 5가닥 + 바깥 초승달 4개가 돌며 0.5초 동안 중심으로 빨려 듦, 테두리 고리도 1.9 m → 0.15 m | 나선 ×1.6, 고리 ×1.4 |
| E 착지 (`RealKingLand`, R163에 다시 만듦) | `06_King_E_Land` | 둥근 섬광 + 가로로 긴 섬광(가로세로 5:1) · 바닥으로 뻗는 빛 가시 20개(0.9~2.1 m) · **반경 2 m 원판이 0.16초 빛나다 금이 가 조각 23개로 깨짐**(밀려나고 떠오르고 돌며 1.7초에 사라짐) · 충격 고리(2.3 m) · 떠오르는 금빛 알갱이 14개 | 섬광 ×5 / ×4, 가시 ×3, 원판 ×2.6 → 조각 ×1.2, 고리 ×2, 알갱이 ×2.5 |
| (작업대 전용) | `08_King_E_Return` | 위 둘을 이어서: 소용돌이 0.5초 → 착지 | — |

- 연결: `SwordFightSkillFx.PieceVfx.cs`(`KingVfx` · `RookVfx(key, 위치, 방향)`) → `Resources/VfxSets/<기물>.asset`(이름 → 프리팹) → `VfxCue.Spawn`. 끄기: `SwordFightSkillFx.KingVfxOn` · `RookVfxOn = false`.
- 부품(어셈블리 `ChessFight.VFX`): `VfxRing`(바닥 고리 · 세운 띠, 커지며 흐려지고 노이즈로 사라짐), `VfxFlash`(카메라를 보는 짧은 섬광), `VfxSparks`(코드로 만드는 파티클: 불꽃 · 불씨, `Density` 반영), `VfxSet`.
- 셰이더(`Assets/VFX/Shaders`, HLSL): `VFX_ScrollDissolveAdd`(스크롤 노이즈 + 디졸브, HDR 색, 가산), `VFX_SoftGlowAdd`(부드러운 원형 빛, 가산). 재질 `VFX_King_Ring` · `VFX_King_Glow`.
- **VFX Graph · Shader Graph가 아닌 이유:** 이 PC에서 AI가 Unity 에디터를 직접 다룰 수 없어 `.vfx` · `.shadergraph`를 만들 수 없다. 손으로 쓸 수 있는 HLSL 셰이더와 코드 파티클로 만들었고, 프리팹 · `VfxCue` 규칙은 같아서 나중에 VFX Graph로 바꿔 끼울 수 있다(DECISIONS FX5).
- 세기는 프리팹의 각 부품에서 바로 고친다(`intensity` · 반지름 · 길이 · 개수). 작업대(기물 1 = 킹)에서 하나씩 재생해 볼 수 있다.

#### 킹 착지 다시 (R163, 10-27, 사용자 동영상 "킹 착지 할 때 위의 동영상처럼 vfx 적용시켜주고 다듬어줘 색상은 킹 색으로")

시안 동영상(초록, 탑뷰 9초 반복): 원판 안 나선 가닥과 바깥 초승달이 돌다 **중심으로 빨려 들고** → 가로로 긴 **섬광** → 작은 점 → **가시처럼 터짐** → 방사형 금이 간 **빛나는 원판** → **조각으로 깨져** 흩어지고 꺼짐.
킹 금색으로 옮기면서 게임 시간에 맞춤: 뛰어오른 뒤 착지까지 0.5초(`kingVault`)가 모이는 구간, 착지 순간이 터지는 순간, 깨지는 원판은 착지 밀치기 반경 2 m(`kingLandRadius`)와 같은 크기라 맞는 범위가 보인다.

- 새 부품: `VfxSwirl`(나선 + 초승달, 돌며 빨려 듦), `VfxBurst`(바닥으로 뻗는 빛 가시, 매번 다른 모양), `VfxShatter`(원판 → 조각, 조각 가장자리가 더 밝음, 매번 다른 금), `VfxFlash.aspect`(가로로 긴 섬광).
- 착지 자리 `EdgeLand`는 온라인에서도 모든 화면에 있다(SwordFightSkills.Net). `kingVault`·`kingLandRadius`를 바꾸면 `07`의 길이(0.5초) · `06`의 원판 반지름(2 m)도 같이 고친다.

#### 킹 착지 새로 — 새 방식 (R168, 10-27, 사용자 "킹 착지 vfx가 밋밋해 더 멋있게 해주고 리얼리스틱으로 안해줘도 돼" → "전에 보냈던 사진 보면 Ground Runes Decal랑 Golden Smoke Tendrils 랑 Anamorphic Lens Flare 합쳐줘")

§11 방식으로 처음부터 다시: 실사 대신 마법 느낌. 메뉴 **ChessFight/VFX/King Land (new)/Build**(`Assets/Scripts/VFXEditor/KingLandBuilder.cs`)가
텍스처 · 메시 · 재질 · 프리팹 `Prefabs/King/10_King_E_Land_New`를 만든다(다시 눌러도 프리팹에서 조절한 값은 남음). 작업대에서 기물 1 → `10_King_E_Land_New`.
**R171부터 이것이 게임의 착지다**(사용자 "킹은 적용시켜줘" → 9번으로 교체): 빌드할 때마다 `VfxSets/King`의 `E_Land`를 이 프리팹으로 다시 연결한다. 뛰어오르는 동안의 소용돌이(`07_King_E_Gather`)는 그대로 먼저 나온다.

| 층 | 내용 | 파일 | 상태 |
|---|---|---|---|
| 1 `L1 King Seal` | 킹 문양(R169에 룬 원형에서 바꿈, 사용자 "너무 마법 부리는 거 같아"): 굵은 바깥 고리 · 얇은 고리 · 큰/작은 장식 알 24개 · 안쪽 고리 · 가운데 **왕관(받침 · 띠 · 끝에 구슬이 달린 뾰족 4개)과 가운데 십자가**, 밝은 테두리 + 흐린 채움. 중심에서 0.16초에 펼쳐지며 번쩍(×4 → ×2), 지름 3 → 3.4 m, **돌지 않음**(십자가가 킹의 앞쪽), 1.6초에 사라짐 | `VfxSigil`, `VFX_SigilAdd.shader`, `T_King_LandSeal.png` · `M_King_LandSeal.mat`(생성), `Meshes/VFX_FloorQuad.asset` | 만듦, 확인 대기 |
| 2 `L2 Smoke Tendrils` · `L2 Smoke Puffs` | **Golden Smoke Tendrils**: Shuriken. 원 둘레(반지름 1.3 m)에서 연기 줄기 10개가 나선으로 돌며(궤도 속도) 1.5 m쯤 피어오르고, 노이즈로 흔들리며 트레일이 꼬리처럼 가늘어짐 + 부드러운 금빛 연기 덩어리 14개가 돌며 퍼짐 | `ParticleSystem`(빌더가 모듈을 설정해 프리팹에 저장), `VFX_TexturedAdd.shader`, `T_King_SmokeStrand.png` · `T_King_SmokePuff.png` · `M_King_SmokeStrand.mat` · `M_King_SmokePuff.mat` | 만듦, 확인 대기 |
| 3 `L3 Lens Flare` | **Anamorphic Lens Flare**: 착지 순간 가슴 높이에서 화면을 가로지르는 가는 빛줄(가로세로 14:1), 0.4초, ×4. 색은 밝은 금백색(시안은 푸른색 — 머티리얼 색으로 바꿀 수 있음) | `VfxFlash`, `T_King_FlareStreak.png` · `M_King_LandFlare.mat` | 만듦, 확인 대기 |

- 공용 에디터 도우미 `VfxAssets`(폴더 · 마스크 PNG · 메시 · 재질 · 바닥 사각형 · 프리팹 저장)는 비숍 빌더(R167)도 쓴다. 기존 함수의 형태는 바꾸지 않는다.

#### 6번 착지(게임에서 쓰는 것) 다시 — 새 방식 (R169, 10-27, 사용자 "6번을 더 멋있게 만들어줘 그래픽을")

`06_King_E_Land`(`VfxSets/King`의 `E_Land`, 게임 착지)를 메뉴 **ChessFight/VFX/King Land (06, game)/Build**(`KingLandBuilder.Land06.cs`)로 다시 만든다.
같은 경로에 저장한다. **R171부터 작업대 전용**(게임 착지는 9번 `10_King_E_Land_New`), 이 빌드는 `E_Land`를 건드리지 않는다. **이 프리팹은 이제 빌더가 만든다**(R163의 파이썬 생성기로 다시 만들지 않는다).

| 층 | 내용 | 세기 |
|---|---|---|
| `L1 Flash` · `L1 Streak` | 둥근 섬광 0.12초 · 가로 빛줄 0.3초(가로세로 10:1) | ×5 · ×3.5 |
| `L2 Cracks` | **중심의 작은 구덩이에서 금빛 균열 9갈래가 가지를 치며 0.1초에 뻗어 나감**(생성 텍스처, 고정 씨앗), 지름 4.4 m, 1.8초에 사라짐 | ×5 → ×2 |
| `L2 Shatter` | R163의 반경 2 m 원판이 빛나다 조각으로 깨짐(그대로) | ×2.2 → ×1.1 |
| `L3 Shock` | 넓고 부드러운 충격 띠(0.3 → 2.8 m, 0.35초) | ×2 |
| `L4 Sparks` | **금빛 불꽃 40개**가 위 · 바깥으로 튀어 늘어나 보이고 **바닥에 튕김**(Shuriken 충돌) | 재질 ×3 |
| `L4 Dust Ring` | **바닥을 쓸며 퍼지는 금빛 먼지 링** 28개(속도가 빨리 줄며 커지고 흐려짐) | 재질 ×1.2 |
| `L5 Embers` | 노이즈로 흔들리며 떠오르는 불씨 20개 | 재질 ×3 |

- 예전의 납작한 빛 가시(`VfxBurst`) · 작은 알갱이는 뺐다. 착지 전 소용돌이(`07_King_E_Gather`)는 그대로.
- 새 재질 · 텍스처: `M_King_Spark` · `T_King_Spark`, `M_King_GroundCracks` · `T_King_GroundCracks`(그 밖은 9번과 공유: `M_King_SmokePuff`, `M_King_LandFlare`).

### 룩 (R161, 10-27, 사용자 "룩도 잡업해줘")

따뜻한 돌색(1, 0.86, 0.66), 킹보다 더 어둡게. 실사 연출(금 · 돌 조각 · 돌탑 · 놋쇠 휨)은 그대로.

| 순간 | 프리팹 (`Prefabs/Rook/`) | 더하는 것 | 세기 |
|---|---|---|---|
| F 내려찍기 (`RealRookSlam`) | `01_Rook_F_Slam` | 찍힌 곳 작은 섬광 · 바닥 충격 고리(1.4 m) · 위로 튀는 돌 불꽃 10개 | 섬광 ×2.5, 고리 ×1.5, 불꽃 ×2 |
| F 파동이 지나는 칸마다 (돌탑이 솟을 때) | `02_Rook_F_Stamp` | 바닥 작은 고리(0.9 m), 0.25초 | ×1.2 |
| F 맞은 상대 (`RealRookHit`) | `03_Rook_F_Hit` | 작은 섬광 · 밀리는 쪽으로 불꽃 6개 | 섬광 ×2.5, 불꽃 ×2 |
| E 내려치기 (`RealRookBreak`) | `04_Rook_E_Break` | 가장자리 섬광 · 고리 · 돌 불꽃, **끊어질 자리(안쪽 2 m)에 3 m 빛줄이 0.8초 동안 그어짐**, 0.8초에 무너지는 덩어리에서 떨어지는 부스러기 빛 14개 | 섬광 ×2.5, 빛줄 ×1.3, 부스러기 ×1.8 |

- 빛줄 위치는 기본 덩어리 크기(폭 3 m · 깊이 2 m, `rookChunkWidth` · `rookChunkDepth`)와 금 가는 시간(0.8초, `rookCrack`)에 맞춘 고정값이다. 수치를 바꾸면 프리팹의 `Crack line` · `Falling grit` 위치와 시간도 같이 고친다.
- 부품에 직선 모양(`VfxRing` Strip)과 줄로 뿜는 불꽃(`VfxSparks.emitterWidth`)을 더했다.

#### 룩 F 다시 (R165, 10-27) — 퀸 모양 · 룩 고유색, 파편 줄임

사용자: "퀸 지금 너무 괜찮다 룩은 f 스킬을 쓸 때 퀸 처럼 vfx 만들어주고 파편 조각을 줄여줘". 위 표의 F 세 줄(돌색) 대신 퀸(R164)과 같은 모양(바닥 탄 자국 줄 · 그림자 잔상)으로 바꾸고 두 개를 더했다. 이어서 "색은 룩이 지금 고유색으로 사용하고 있는 색으로 해야지" →
색은 퀸의 짙은 빨강이 아니라 **룩 고유색 베이지 `#D3B596`**(`SwordFightSkillFx.Board`의 `BOrange`, 밝은 쪽 `#ECDCC8`)이고, 탄 자국은 룩의 어두운 쪽 `#A3876B`을 거의 검게 태운 **짙은 갈색**(0.1, 0.08, 0.06)이다.
베이지는 빨강보다 밝아 세기를 낮췄다. E(성벽 붕괴 `04`)는 그대로(킹 · 룩 채팅 담당).

| 순간 | 프리팹 (`Prefabs/Rook/`) | 더하는 것 | 세기 |
|---|---|---|---|
| F 내려찍기 (`RealRookSlam`) | `01_Rook_F_Slam` | 찍힌 곳 바닥에 탄 자국 고리(1.4 m, 갈색 + 베이지 가장자리) · 밝은 베이지 섬광 · 떠오르는 베이지 불씨 6개 | 고리 ×1.4, 섬광 ×2, 불씨 ×1.8 |
| F 파동이 달리는 동안 | `05_Rook_F_Line` (새) | 줄 가운데에 **바닥 탄 자국 줄**(폭 0.6 m, 더 갈라진 모양)이 파동 앞을 따라 그어짐, 판 가장자리에서 멈춤. 파동이 끝나면 0.6초 → 1초 동안 베이지 불씨 테두리가 먼저 식고 흩어짐, 불씨 14개 | 테두리 ×1.3, 불씨 ×1.8 |
| F 돌탑이 솟는 칸마다 | `02_Rook_F_Stamp` | 그 칸 바닥에 탄 자국 고리(0.85 m) | ×1.1 |
| F 돌탑이 내려갈 때 (새) | `06_Rook_F_Phantom` | 돌탑 모양 **짙은 갈색 그림자 + 베이지 윤곽**이 그 자리에 남아 0.5초 동안 타 들어가며 사라짐(돌탑은 그 안에서 내려감) | 윤곽 ×1.6 |
| F 맞은 상대 (`RealRookHit`) | `03_Rook_F_Hit` | 밝은 베이지 섬광 · 베이지 불꽃 5개 | 섬광 ×2, 불꽃 ×2 |

**파편 줄임**(`SwordFightSkillFx.RookDebris = 0.35`, 룩 F 전부): 내려찍기 나무 조각 12 → 4, 돌탑마다 나무 조각 9 → 3, **돌탑 발밑 돌무더기는 큰 돌만 35%**(최소 2개),
판 가장자리 놋쇠 휨 조각 9 → 3, 막혔을 때 9 → 3, 맞은 상대에서 튀는 조각 10 → 4. 원래대로: `RookDebris = 1`. VFX를 끄면(`RookVfxOn = false`) 색은 R161 전 그대로지만 파편은 계속 줄어 있다.

- 연결: `SwordFightSkillFx.RookFVfx.cs`(`RookFVfx`, `RookPhantom`, `RookDebris`), `.RealRookBishop.cs`(줄 · 조각 수), `.Statues.cs`(돌무더기 · 조각 수 · 내려갈 때 그림자), `.Real.cs`(`RealHit`에 조각 수 인자), `.QueenVfx.cs`(공용 `SetVfx`).
- 재질 새로 `VFX_Rook_F_Scorch` · `ScorchRing` · `Phantom` · `Flare` · `Glow`(퀸과 같은 셰이더, 색은 룩 고유색 — 퀸과 따로 바꿀 수 있음). E가 쓰는 `VFX_Rook_Ring` · `Glow`는 그대로.
- 프리팹 01~03은 같은 경로 · 같은 GUID로 다시 씀, `Resources/VfxSets/Rook`에 `F_Line` · `F_Phantom` 더함(`E_Break` 줄은 그대로).

### 퀸 (R162 → R164, 10-27)

R162 사용자: "vfx 퀸 작업 해주면 되고 … 1번째 사진 퀸이 꼬치 베기를 했을 때 그게 잔상이였으면 좋겠어 … E F 스킬 적용시켜주고"
("1번째 사진" = VFX 채팅의 시안 1번 **Fire Beam**) → 가슴 높이 흰 노란 불빛 줄기. 이어서 R164 사용자: "흰 노란이 아니라 검은색과 빨간색(완전한 빨간색 x) 이 바닥에 그어지고 잔상도 다시 디자인해줘"
→ **흰색 · 노란색을 모두 빼고**, 줄은 **바닥에 그어지는 검은 탄 자국 + 짙은 빨강(순수한 빨강 아님) 불씨 테두리**로,
잔상은 **퀸 몸 모양의 검은 그림자 + 짙은 빨강 윤곽이 가장자리부터 타 들어가며 사라지는 것**으로 다시 만들었다.
실사 연출(칼날 흐림 · 칼끝 쇳불꽃 · 공기 · 흔들림)은 그대로. 옛 반투명 몸 복사본(10-25 승규 님)은 `QueenGhosts = true`면 같이 나온다(FX6).

색: 탄 자국 (0.02, 0.008, 0.008) 불투명도 0.85, 짙은 빨강 (0.6, 0.07, 0.06) — R만 있는 빨강이 아니라 G · B를 조금 섞은 검붉은 색.

| 순간 | 프리팹 (`Prefabs/Queen/`) | 더하는 것 | 세기 |
|---|---|---|---|
| F 돌진 (`RealQueenDash`) | `01_Queen_F_Thrust` | 떠난 자리에서 퀸 발밑까지 **바닥에 탄 자국 줄**(폭 0.28 m, 가운데 검정 · 가장자리 짙은 빨강 불씨)이 따라 그어짐. 퀸이 멈추면 0.5초 그대로 → 0.9초 동안 빨강이 먼저 식고 검정이 흩어지며 사라짐, 불씨 12개가 떠오름. 떠난 자리 바닥에 검붉은 고리(0.9 m) · 가슴 높이 짙은 빨강 섬광 | 불씨 테두리 ×2.2, 고리 ×2, 섬광 ×3 |
| F 잔상 (돌진 중 3프레임마다) | `05_Queen_Phantom` | 그 순간 퀸 몸 모양 그대로 **검은 그림자**(불투명도 0.6) + 짙은 빨강 윤곽, 0.4초 동안 뒤로 0.12 m 밀리며 노이즈 가장자리부터 빨갛게 타 들어가 사라짐 | 윤곽 ×2.5 |
| F 벰 (`RealQueenHit`, 맞은 사람마다) | `02_Queen_F_Hit` | 칼이 닿은 곳 작은 짙은 빨강 섬광 · 빨간 불꽃 10개 | 섬광 ×3, 불꽃 ×3 |
| E 일섬 (`RealQueenBlink`) | `03_Queen_E_Blink` + `05` × 5 | 출발 → 도착 **탄 자국 줄이 한 번에**(불씨 14개), 출발 자리 고리 · 섬광, 길 위에 그림자 잔상 5개(출발 쪽일수록 먼저 · 옅게). 화면을 가르는 빛과 화면 물듦도 검붉은 색으로 | 위와 같음 |
| E 베임 (`RealQueenCut`, 한 박자 뒤) | `04_Queen_E_Cut` | 베인 곳 짙은 빨강 섬광 · 베는 방향을 보는 검붉은 고리(0.6 m) · 빨간 불꽃 14개 | 섬광 ×3.5, 고리 ×2.2, 불꽃 ×3 |

- 연결: `SwordFightSkillFx.QueenVfx.cs`(`QueenVfx(key, 위치, 방향)` → 복사본, `BeamOf`, `QueenPhantom`). F는 돌진하는 동안 매 프레임 `VfxBeam.Reach(퀸 발밑 바닥)`(같은 높이 바닥일 때만 → 장 밖으로 달려 나가면 줄은 가장자리에서 멈춤), 끝나면 `Release()`. E는 `Reach` + `Release`를 한 번에. 끄기: `QueenVfxOn = false`(R162 전 그대로).
- 부품: `VfxBeam`에 바닥 모드(`onFloor`, 심 없이 한 장), 새 `VfxPhantom`(몸 모양을 구워 남기는 그림자, 작업대에서는 무대의 퀸을 +Z로 1.1 m 간격 5개). 셰이더 새로 2개(HLSL, 미리곱한 반투명이라 **검정이 실제로 어둡게** 그려짐): `VFX_ScorchPremul`(가운데 탄 자국 + 가장자리 불씨, 불씨가 먼저 식음), `VFX_PhantomPremul`(검은 몸 + 빨간 윤곽 + 3D 노이즈로 타 들어감). 섬광은 R162의 `VFX_StarburstAdd`를 짙은 빨강으로.
- 재질: `VFX_Queen_Scorch` · `ScorchRing` · `Phantom` · `Flare` · `Glow`. R162의 `VFX_Queen_BeamCore` · `BeamGlow` · `Ring`은 지움.
- 공용 부품(`VfxCue` · `VfxRing` · `VfxFlash` · `VfxSparks` · `VFX_SoftGlowAdd` · `VFX_ScrollDissolveAdd`)은 고치지 않았다.
- 고치는 곳: 탄 자국 색 · 진하기 = 재질 `VFX_Queen_Scorch`의 `_CoreColor`(알파 = 어두운 정도) · `_Color`(불씨), 굵기 · 남는 시간 = 프리팹 `Burn line`의 `glowWidth` · `afterglow` · `burnTime`, 잔상 = 재질 `VFX_Queen_Phantom`의 `_CoreColor` 알파 · 프리팹 `Phantom`의 `life` · `rimIntensity`, 잔상 간격 = `RealQueenDash`의 `3f * F`.
- 칼끝에서 튀는 흰 노란 쇳불꽃과 따뜻한 칼끝 빛은 승규 님 실사 연출(10-26)이라 그대로 두었다.

### 비숍 (R167, 10-27) — 새 방식(§11)의 첫 스킬, 레이어 하나씩

사용자: "비숍은 거의 vfx가 없으니까 만들어줘 리얼리스틱하게 안해줘도 돼". 실사가 아닌 마법 느낌으로, 색은 **비숍 고유색 은색 `#C2C9D3`**(`SwordFightSkillFx.Board`의 `BSilver`).
에디터 메뉴 **`ChessFight/VFX/Bishop/Build`**(`Assets/Scripts/VFXEditor/BishopVfxBuilder.cs`)가 메시 · 재질 · 프리팹 · `Resources/VfxSets/Bishop`을 만든다.
**10-27~28 처음부터 다시(R172 · R173 · R175):** F가 관통 핀 → X자 베기로 바뀌어, 새 층 계획(F1~F9 · E1~E3)과 레퍼런스 프롬프트는 [VFX_Progress](VFX_Progress.md)에 있다. 아래 표의 예전 1 · 2층 프리팹 · 재질 · 텍스처는 **10-28에 지웠다**(R173). **R175에 빌더를 새 층 계획으로 다시 짰다**(예전 핀 코드 지움): 지금 `Bishop/Build`는 F1(R194) · F3(R188) · F4(R191) · F5(R181) · F6 · F7(R178) · F8(R186)을 만들고, `Bishop/Build F1 (draw only)` · `Build F3 (windup only)` · `Build F4 (throw only)` · `Build F5 (flight only)` · `Build F8 (return only)`는 그 층만 만든다.
**메뉴를 한 번 누르기 전에는 아무것도 나오지 않는다.** 다시 누르면 프리팹 레이어에서 고친 값과 재질 색은 그대로 남는다.
**Play 중에는 메뉴가 꺼진다**(Play 중에 만들면 부품의 `Awake`가 돈 상태로 프리팹에 저장된다 — 킹 빌더에서 섬광 판이 켜진 채 저장된 일, R171).

| 층 | 순간 | 프리팹 (`Prefabs/Bishop/`) | 모양 | 상태 |
|---|---|---|---|---|
| **1** | F 칼이 박힐 때(사람 있든 없든) | `01_Bishop_F_Pin` | 발밑 바닥에 **은빛 X 문양**(지름 1.4 → 1.6 m): 바깥 고리 + 안 고리 + 축 위 눈금 4개, 비숍 대각선을 따른 X(가운데 · 끝에 마름모). 가운데부터 0.1초에 열리며 번쩍(×4.5) → ×1.8로 가라앉음, 1초(핀 0.8초 + 0.2초) 뒤 사라짐, 돌지 않음(두 칼의 X와 맞게) | **지움**(R173, 10-28) |
| **2** (R170) | F 박힌 동안 | `01_Bishop_F_Pin`의 `L2 Motes` | 문양 테두리(반지름 0.72 m 띠)에서 **은빛 알갱이**: 박히는 순간 8개 + 0.8초 동안 초당 32개(약 26개), 0.5~1 m/s로 오르며 천천히 문양 둘레를 돌고 살짝 흔들림, 0.6~1.1초에 작아지며 사라짐(Shuriken, 크기 3~7 cm, 재질 ×2.2) | **지움**(R173, 10-28). 만들 때 사용자: "확인은 나중에 할게 2층도 바로 만들어줘" |
| 3 | F 칼이 날아갈 때 | `02_Bishop_F_Trail` | 두 칼 뒤 은빛 꼬리(Trails) | 안 만듦 → 새 계획 F5(R181, 아래) |
| 4 | F 던질 때 | `03_Bishop_F_Cast` | 손에서 은빛 섬광 · 퍼지는 알갱이 | 안 만듦 → 새 계획(VFX_Progress) |
| 5 | E 손을 뻗을 때 | `04_Bishop_E_Reach` | 떨어지는 아군 둘레 은빛 고리 | 안 만듦 → 새 계획(VFX_Progress) |
| 6 | E 잡을 때 | `05_Bishop_E_Catch` | 섬광 + 알갱이 터짐 | 안 만듦 → 새 계획(VFX_Progress) |
| 7 | E 내려놓을 때 | `06_Bishop_E_Set` | 내려놓는 자리 바닥 은빛 원 + 피어오르는 알갱이 | 안 만듦 → 새 계획(VFX_Progress) |
| **F1** (R194, 새 계획) | F를 눌러 왼손에 두 번째 칼이 생길 때(`OffBlade`가 생기는 순간 ~ 0.6초) | `01_Bishop_F_Draw` | **은빛 조각 44개**가 주위 0.5 m에서 날아와 왼칼 칼날에 **칼날 시작 → 칼끝 순서로 붙음**(0.1초 뒤부터 0.3초), 아직 안 붙은 칼날은 **빛으로 덮여 있다가 줄어듦**(폭 0.18 m), 붙는 끝에 반짝임이 타고 올라가 마지막 조각 때 **칼끝에서 별처럼 번쩍**(0.3 m, 0.14초) → 그냥 강철. 게임에서는 그려진 왼칼을 따라감. 값은 [VFX_Progress](VFX_Progress.md) | **검토**(R194 · 10-28 만듦, 사용자 확인 전, 커밋 안 함) |
| **F3** (R188, 새 계획) | F 좌클릭으로 놓아 두 칼을 젖힐 때(`Cast` ~ 던진 뒤 0.15초) | `03_Bishop_F_Windup` | 두 칼날을 따라 **은빛이 칼끝으로 차오름**(칼날 시작 → 칼끝, 0.16초에 끝까지, 칼끝 쪽이 밝고 좁음, 폭 0.17 m, 차오르는 끝에 밝은 띠) + 빛이 닿으면 **칼끝 작은 반짝임**(0.18 m), 놓을 때 ×1.4로 번쩍했다가 날아가는 칼 위에서 0.15초에 꺼짐. 게임에서는 그려진 칼날을 따라감. 값은 [VFX_Progress](VFX_Progress.md) | **확정**(R188 · 10-28 사용자 "F3 확정", 다른 점 3개는 그대로, 커밋 안 함) |
| **F4** (R191, 새 계획) | F 두 칼이 손을 떠날 때(`BishopLeap` ~ 0.15초) | `04_Bishop_F_Throw` | 손마다 손끝(칼이 나가는 쪽 0.14 m)에 **아주 짧은 은빛 별 섬광**(0.34 → 0.17 m, 0.09초) + 손끝에서 날아가는 칼 가운데까지 **가는 빛줄**(공기가 갈라짐, 폭 0.05 m, 칼 쪽이 밝음) 0.05초 → 손끝 쪽 끝이 칼로 달려가며 0.1초에 사라짐. 게임에서는 놓는 순간 손에서 시작해 그려진 칼을 따라감. 값은 [VFX_Progress](VFX_Progress.md) | **확정**(R191 · 10-28 사용자 "F4 확정", 다른 점 3개는 그대로, 커밋 안 함) |
| **F5** (R181, 새 계획) | F 두 칼이 날아갈 때(`BishopLeap` ~ X) | `05_Bishop_F_Flight` | 칼마다 **도는 잔상 원판**(지름 0.95 m, 칼이 도는 면에 눕힘, 고리 줄무늬, 칼 바로 뒤가 밝음) + 원판 **바깥 테두리에서 휜 길을 따라 뒤로 남는 꼬리**(2.4 m, 칼 쪽이 굵고 밝은 줄무늬 띠 → 먼 끝은 가는 줄). 원판은 칼이 베기로 돌아설 때 꺼지고, 꼬리는 X가 시작되면 0.1초에 칼 쪽으로 줄어들며 사라짐. 게임에서는 그려진 칼을 따라감. 값은 [VFX_Progress](VFX_Progress.md) | **확정**(10-28 사용자 "확정", 커밋 안 함) |
| **F6** (R175, 새 계획) | F 두 칼이 엇갈릴 때(`BishopCross`) | `06_Bishop_F_Cross` | 공중에 **초승달 베기 빛 두 줄 X**(각 2.2 m · 38°, 높이 0.55 m, 높은 끝부터 0.14초에 그려짐 · 0.6초 남았다가 가늘어지며 사라짐) + 엇갈림 **별 반짝임**(0.3초). 값은 [VFX_Progress](VFX_Progress.md) | **확정**(10-28 사용자 "확정", 커밋 안 함) |
| **F7** (R178, 새 계획) | X에 맞은 적마다(`BishopHit`) | `07_Bishop_F_Hit` | 맞은 적 몸 앞에 **X 베인 자국**(가는 빛 두 줄 1.1 m, 0.045초에 그려져 번쩍 → 0.3초에 사라짐) + **X 모양 번쩍임**(0.14초) + 밀리는 쪽으로 **은빛 불꽃** 60개(원뿔 35°, R184 전 30개) + 밀리는 방향으로 **공기 줄** 12개(R184 전 7개). 고리 없음. 적 가슴에 매달려 같이 날아감. 값은 [VFX_Progress](VFX_Progress.md) | **확정**(10-28 1~3번 고친 뒤 사용자 "확정" → R184 불꽃 · 공기 줄 세게 하고 다시 확정, 커밋 안 함) |
| **F8** (R186, 새 계획) | F 두 칼이 돌아올 때(X 끝 ~ 손에 들어갈 때) | `08_Bishop_F_Return` | 칼마다 돌아오는 휜 길을 따라 **뒤에 남는 가늘고 흐린 꼬리**(2.6 m, 폭 0.12 m, 먼 끝이 0.02 m 흔들림), 원판 없음. 칼이 손에 들어가면 0.15초에 칼 쪽으로 줄어들며 사라짐. 게임에서는 그려진 칼을 따라감. 값은 [VFX_Progress](VFX_Progress.md) | **확정**(10-28 사용자 "확정", 커밋 안 함) |

- 지운 부품(R173, 10-28): 1층 텍스처 `T_Bishop_PinSigil` · 재질 `M_Bishop_PinSigil`(셰이더 `Sigil Additive`), 2층 텍스처 `T_Bishop_Mote` · 재질 `M_Bishop_Mote`(셰이더 `Textured Additive`), 프리팹 `01_Bishop_F_Pin`(`L1 Sigil` · `L2 Motes`). 공용 메시 `Meshes/VFX_FloorQuad.asset` · 재생 `VfxSigil` · 두 셰이더는 킹도 쓰므로 그대로.
- F6 부품(R175, 새 파일 — 공용 부품은 안 고침): 셰이더 `VFX_SlashAdd`(`Slash Additive`: LineRenderer 띠의 흰 심 + 은빛 번짐, `_Reveal`까지 그림 · 머리 빛 · 양 끝 흐림 · 바닥에 닿는 곳 깊이 흐림) · `VFX_GlintAdd`(`Glint Additive`: 정점 셰이더가 사각형을 카메라 쪽으로 돌려 캡처 카메라에도 맞음), 재생 `VfxSlash` · `VfxGlint`(`IVfxSeekable`), 재질 `M_Bishop_Slash` · `M_Bishop_Glint`, 메시 `SM_BillboardQuad`.
- F7 부품(R178): `VfxSlash` · `VfxGlint`를 값만 바꿔 다시 씀(새 재생 코드 없음), 새 재질 `M_Bishop_Cut`(`Slash Additive`, 몸에 닿는 곳 깊이 흐림 0.03 m) · `M_Bishop_Spark`(`Soft Glow Additive`, 불꽃 · 공기 줄), 불꽃 · 공기 줄은 Shuriken을 빌더가 프리팹에 저장.
- F5 부품(R181, 새 파일 — 공용 부품은 안 고침): 셰이더 `VFX_SpinBlurAdd`(`Spin Blur Additive`: 원판에 고리마다 밝기 · 길이가 다른 은빛 줄, 칼 바로 뒤가 밝고 돌아가며 사라짐, 정점 셰이더가 원판을 카메라 쪽으로 35% 돌려 뒤에서도 옆면으로 얇아지지 않음) · `VFX_TrailAdd`(`Trail Additive`: LineRenderer 띠에 줄마다 밝기 · 뒤로 닿는 길이가 다른 줄무늬, 칼 쪽 밝음 → 먼 끝 0), 재생 `VfxSwordFlight`(`IVfxSeekable`, 꼬리 + 자식 `Disc`, 게임에서는 `Follow`로 칼 자리를 받음, 실행 순서 120), 재질 `M_Bishop_Trail` · `M_Bishop_Spin`, 메시 `SM_BillboardQuad`(F6과 같이 씀).
- F8 부품(R186): F5의 `VfxSwordFlight`를 다시 씀 — `back`(돌아오는 길을 따라가고 미리보기도 게임의 돌아오는 곡선을 그림)과 `waver` · `waverLength`(꼬리 먼 끝이 옆으로 흔들림)를 더했고, 둘 다 기본값이 꺼져 있어 F5는 그대로. 새 재질 `M_Bishop_Return`(`Trail Additive`, 줄무늬 9줄 · 대비 0.45), 원판 없음.
- F3 부품(R188, 새 파일 — 공용 부품은 안 고침): 셰이더 `VFX_BladeChargeAdd`(`Blade Charge Additive`: 칼날을 따라 놓인 LineRenderer 띠가 `_Fill`까지 빛남, 칼끝 쪽이 밝음, 차오르는 끝 띠 `_Front`, 줄무늬, 정점 셰이더가 시선 방향으로 카메라 쪽 0.08 m 당겨 칼 몸에 가려지지 않음), 재생 `VfxBladeCharge`(`IVfxSeekable`, 자식 `Tip` 반짝임 · `Preview sword` 칼 모형 복사본, 게임에서는 `Follow`로 칼날 자리를 받고 복사본을 숨김, 실행 순서 120), 재질 `M_Bishop_Charge` · `M_Bishop_TipGlint`(`Glint Additive`) · `M_Bishop_PreviewSteel`(URP Lit).
- F4 부품(R191, 새 셰이더 없음 — 공용 부품은 안 고침): 재생 `VfxSwordRelease`(`IVfxSeekable`, LineRenderer 빛줄 + 자식 `Flash` 섬광 · `Preview sword` 칼 모형 복사본, 게임에서는 `Follow`로 놓는 순간 손 자리와 칼 자리를 받고 복사본을 숨김, 실행 순서 120), 새 재질 `M_Bishop_Split`(`Slash Additive`) · `M_Bishop_Release`(`Glint Additive`), `M_Bishop_PreviewSteel` · `SM_BillboardQuad`는 같이 씀.
- F1 부품(R194, 새 셰이더 없음 — 공용 부품은 안 고침): 재생 `VfxBladeForm`(`IVfxSeekable`, LineRenderer 덮는 빛 + 자식 `Glint` 반짝임 · `Shard 00`~`43` 조각 · `Preview sword` 칼 모형 복사본, 게임에서는 `Follow`로 칼날 자리를 받고 복사본을 숨김, 실행 순서 120), 새 재질 `M_Bishop_Form`(F3 셰이더 `Blade Charge Additive`, 다 찬 채로) · `M_Bishop_Shard`(`Glint Additive`), 칼끝 번쩍은 F4 재질 `M_Bishop_Release`를 그대로 씀, `M_Bishop_PreviewSteel` · `SM_BillboardQuad`도 같이 씀.
- 연결: `SwordFightSkillFx.BishopVfx.cs`(`BishopVfx(key, 위치, 방향)` → `PieceVfx("Bishop", …)`, R178부터 만든 복사본을 돌려줌). **F1은 이벤트가 아님**(R194): F를 누르면 조준 단계가 될 뿐 `SfFxKind`가 없으므로, `SwordFightSkillFx.Watch`(매 프레임 기물마다)에 한 줄 `BishopDrawWatch`를 더해 비숍에게 `SwordFightSkills.OffBlade`(왼손 칼)가 생기는 순간 `F_Draw`를 비숍 발밑 바닥(+Z = 조준 방향)에 한 번 내고, 왼칼 칼날(`SwordFightPawn.DrawnBlade(1)`)을 따라가게 한다(`BishopDraw`). 칼이 없어지면(베기 끝 · 취소) 다시 낼 수 있게 됨. `SwordFightSkillFx.cs`의 `SfFxKind.Cast`가 비숍일 때(실사 `RealCast` 다음) `F_Windup`을 비숍 발밑 바닥에 내고 칼마다 `SwordFightPawn.DrawnBlade`(R188에 더함, 손에 쥔 칼 → 던진 칼 복사본의 칼날 시작 · 칼끝)를 따라가게 한다(`BishopWindup`). `SfFxKind.BishopLeap`이 `F_Flight`를 비숍 발밑 바닥에 내고 칼마다 `SwordFightPawn.ThrownSwordOut`(R181에 더함, 그려진 칼 복사본의 가운데 · 칼날 방향)을 따라가게 한다(`BishopFlight`, `SwordFightSkills.BishopSwordHalf`를 public으로). 같은 `BishopLeap`이 `F_Throw`도 비숍 발밑 바닥에 내고 칼마다 `SwordFightPawn.SwordHand`(R191에 더함, 그 손의 그려진 장갑 가운데 = 놓는 자리)에서 시작해 `ThrownSwordOut`을 따라가게 한다(`BishopRelease`). `SfFxKind.BishopCross`가 `F_Cross`를 엇갈림 아래 바닥에 내고(R175), `F_Return`을 비숍 발밑 바닥에 내서 칼마다 `SwordFightPawn.ThrownSwordBack`(R186에 더함, X가 끝난 뒤 ~ 손에 들어갈 때까지의 그려진 칼)을 따라가게 한다(`BishopReturn`). `SfFxKind.BishopHit`는 `F_Hit`을 맞은 적 가슴에 내고 그 가슴에 매단다(R178 — 이벤트 위치는 비숍과 적 사이라 쓰지 않음). 예전 `DriveStakes`의 `F_Pin` 호출은 지웠다. 끄기: `BishopVfxOn = false`.

### 나이트 (R176 · R177 · R179 · R182 · R189 · R192 · R195, 10-28) — F5 포크 · E1 웅크림 · E2 뛰어나감 · E3 공중제비 · E4 차기 검토 중

사용자: "나이트 작업 시작하자". 비숍처럼 새 방식(§11)으로 한다. 소드 파이트 나이트의 **실사 연출(`SwordFightSkillFx.RealKnight.cs` · `.RealEdgeRBK.cs`)은 그대로 두고** 그 위에 층을 하나씩 얹는다(킹과 같음).
동작 · 타이밍, 층 계획(F 포크 강하 F1~F6 · E 벼랑 끝 역전 E1~E5, 얼굴 = F5 착지 · 포크와 E3 공중제비), 레퍼런스 프롬프트는 [VFX_Progress](VFX_Progress.md)의 "나이트" 절.
색(R177, 사용자 "룩 고유 색보다 진한 색으로"): **진한 황갈색 `#A8703D`**(룩 베이지 `#D3B596`을 진하게), 심 `#FFF1DD`. 적 칸은 **빨강 `#C24A41`**(R179, 사용자 "적 칸은 빨강으로" — 실사 조준 표시 `ForkFoe`도 같은 색). F 레퍼런스 4장(`F_00` 연속 그림 · `F_01` · `F_02` · `F_04`, 은색 프롬프트로 뽑은 것 → 모양만)을 `Assets/VFX/Reference/knight/`에 넣었고, 새 색 미리보기는 `VFXCaptures/knight/color_preview.png`. R179에 빌더 · F5 프리팹 · `VfxSets/Knight` · 게임 연결을, R182(사용자 "나이트 E3 공중제비를 vfx-layer 순서대로 만들어줘")에 E3를, R189(사용자 "나이트 E4 차기를 vfx-layer 순서대로 만들어줘")에 E4를, R192(사용자 "나이트 E2 뛰어나감을 vfx-layer 순서대로 만들어줘")에 E2를, R195(사용자 "나이트 E1 웅크림을 vfx-layer 순서대로 만들어줘")에 E1을 만들었다(아래).

에디터 메뉴 **`ChessFight/VFX/Knight/Build`**(`Assets/Scripts/VFXEditor/KnightVfxBuilder.cs`, Play 중에는 꺼짐)가 메시 · 재질 · 프리팹 · `Resources/VfxSets/Knight`를 만든다. **메뉴를 한 번 누르기 전에는 아무것도 나오지 않는다.** 다시 누르면 프리팹 레이어에서 고친 값과 재질 색은 그대로 남는다. **`ChessFight/VFX/Knight/Build E3 (flip only)`**(R182)는 E3만, **`Build E4 (kick only)`**(R189)는 E4만, **`Build E2 (spring only)`**(R192)는 E2만, **`Build E1 (coil only)`**(R195)는 E1만 다시 만든다(다른 프리팹은 그대로).

| 층 | 순간 | 프리팹 (`Prefabs/Knight/`) | 모양 | 상태 |
|---|---|---|---|---|
| **F5** (R179) | F 착지 · 포크(`KnightLand`) | `05_Knight_F_Fork` | 착지 칸 바닥에 **네모 판**이 번쩍(×5 → ×1.8) → 판의 네 변 가운데에서 **빛 선 네 갈래**가 두 칸 달리고(0.08초, 밝은 머리) → 끝에서 **T자**로 갈라져(0.04초) → **L자 여덟 칸**이 네모 테두리 + 옅은 속으로 번쩍(×4 → ×2) + 칸마다 **빛 기둥**(1 m)이 솟음. 적이 있는 칸은 **빨간 굵은 테두리** · 1.5배 밝기 · 기둥 1.25배. 기둥은 0.6초까지, 판 · 선 · 칸은 0.3 → 0.8초에 사라짐. 둥근 것 · 먼지 없음. 값은 [VFX_Progress](VFX_Progress.md) | **검토**(사용자 확인 대기) |
| **E1** (R195) | E 웅크림(`KnightCrouch`) | `10_Knight_E_Coil` | 웅크리는 0.35초 동안 두 발 가운데 바닥으로 **가는 황갈색 빛 실 약 13가닥**이 둘레(반지름 0.28 m 원, 바닥 위 0.32 m)에서 빨려 들고(꼬리, 점점 많이), 발밑 **바닥 빛**(황갈색 번짐 0.6 m + 따뜻한 흰 심 0.16 m)이 0.4초 동안 커지며 차오름. 뛰어나가면 남은 빛은 저절로 꺼지고, 웅크림이 끊기면 바로 지움. 값과 레퍼런스와 다른 점 3개(실이 성기고 넓음 · 바닥 빛에 닿기 전에 사라짐 · 실이 어두움)는 [VFX_Progress](VFX_Progress.md) | **검토**(사용자 확인 대기) |
| **E2** (R192) | E 뛰어나감(`KnightFlip`) | `11_Knight_E_Spring` | 발굽이 차고 나가는 놋쇠 테 위(테에서 0.6 m보다 안쪽이면 발굽이 떠나는 판 위)에 **짧은 따뜻한 흰 별 섬광**(0.55 m → 0.3 m, 0.08초, 테를 따라 긴 가로줄 + 십자, 황갈색 번짐) + 벼랑 밖 아래로 부채처럼 퍼지는 **가는 곧은 황갈색 줄 4 ~ 5가닥**(꼬리, 0.16초 안에 사라짐). 실사 놋쇠 부스러기 · 공중제비 공기 굴절은 그대로. 값과 레퍼런스와 다른 점 3개(줄 짧고 흐림 · 별 빛살 짧음 · 별과 줄이 같이 안 보이고 공기 줄 없음)는 [VFX_Progress](VFX_Progress.md), 사용자 "E2는 그대로 두고 확인은 나중에 할게"(값 그대로) | **검토**(사용자 확인 나중에) |
| **E3** (R182) | E 공중제비(`KnightFlip`) | `12_Knight_E_Flip` | 몸이 돌기 시작하면(비행의 42%) **뒷발굽이 지나간 길을 따라 긴 초승달 빛**이 그려짐: 벼랑 밖 → 적 머리 위 → 적 뒤. 발굽 쪽이 가장 밝고 넓고(18 cm, 따뜻한 흰 심 + 황갈색 번짐), 꼬리까지 굵게 남다가 0.55초 동안 좁아지며 사라져 **고리로 닫히지 않음**. 실사 공기 굴절 바람 · 잔상 · 뒷다리 흐림은 그대로. 값과 레퍼런스와 다른 점은 [VFX_Progress](VFX_Progress.md): 하얗게 바램 · 꼬리 가늚은 1차 수정(사용자 "E3 1번(색)과 2번(굵기)을 추천 값으로 고쳐줘")에서 고침, 뒤에서 세로 막대는 게임에서 보고 정함 | **검토**(사용자 확인 대기) |
| **E4** (R189) | E 뒷발차기가 맞힐 때(`KnightKickHit`) | `13_Knight_E_Kick` | 발굽이 닿는 적 몸 위에 **뜨거운 흰 별 섬광**(0.75 m → 0.4 m, 0.12초, 황갈색 번짐 · 빛살 16개, 가로줄 · 십자 없음) + 차는 쪽으로 **긴 곧은 황갈색 빛줄기 6 ~ 8개**(지나간 길만큼 자라는 꼬리, 0.3 ~ 0.8 m, 0.18초 안에 사라짐). 헛발질에는 안 나옴(사용자 "맞았을 때만"), 실사 편자 불꽃 · 색수차 · 나뭇조각은 그대로(사용자 "E4 줄기를 적게"). 값과 레퍼런스와 다른 점 3개(줄기 어둡고 짧음 · 나이트 쪽 · 반대쪽에서 줄기가 거의 안 보임 · 별이 빛 덩이)는 [VFX_Progress](VFX_Progress.md), 사용자 "E4는 그대로 두고 확인은 나중에 할게"(값 그대로) | **검토**(사용자 확인 나중에) |

- F5 부품(R179, 새 파일 — 공용 부품은 안 고침): 셰이더 `VFX_ForkGridAdd`(`Fork Grid Additive`: 판 · 선 · T · 여덟 칸을 바닥 한 장에 거리 함수로 그림, 칸마다 켜기 · 적 있음은 렌더러마다) · `VFX_ForkShaftAdd`(`Fork Shaft Additive`: 칸마다 사각형을 정점 셰이더가 세워 카메라 쪽으로 돌림, 몸에 닿는 곳 깊이 흐림), 재생 `VfxForkGrid`(`IVfxSeekable`, `Configure`로 칸 크기 · 켜기 · 적 있음), 재질 `M_Knight_ForkGrid` · `M_Knight_ForkShaft`, 메시 `SM_Knight_Fork`.
- 연결: `SwordFightSkillFx.KnightVfx.cs`(`KnightVfxFork`). `SwordFightSkillFx.cs`의 `SfFxKind.KnightLand`가 실사 착지(`RealKnightLand`) 바로 뒤에 부른다(실사가 꺼졌을 때의 예전 연출 `KnightLand`에는 안 붙음). 땅 없는 칸(`KnightSquareOn` false)은 안 그림, 적 판정은 포크(칸 반경 0.8 m) · 내리찍기(착지 칸 0.85 m)와 같음. 끄기: `KnightVfxOn = false`.
- 실사 조준 표시의 적 칸 색 `ForkFoe`를 호박색 (1, 0.62, 0.26) → 빨강 (0.761, 0.29, 0.255)으로 바꿈(`SwordFightSkillFx.RealKnight.cs`, 요약 주석도).
- E 레퍼런스 5장(`E_01_coil` · `E_02_spring` · `E_03_flip` · `E_04_kick` · `E_05_launch`, R179, 사용자 "그동안 나이트 E 키 보고 있어")을 `Assets/VFX/Reference/knight/`에 넣었다. 흰 은색으로 나와 모양만 따른다. 층마다 본 것과 만들 때 할 일(E3 호는 고리로 닫히지 않게 초승달 한 줄, E4 원뿔은 실사 편자 불꽃과 겹침)은 [VFX_Progress](VFX_Progress.md).
- E1 부품(R195, 새 파일 — 공용 부품은 안 고침): 새 재질 `M_Knight_CoilThread`(`Soft Glow Additive` `#A8703D` ×4) · `M_Knight_CoilGlow`(×2.5) · `M_Knight_CoilCore`(`#FFF1DD` ×5), 실은 E4 · E2처럼 꼬리만 그리는 Shuriken에 원 모양 나오는 곳 · 뿌리 쪽 끌어당김(`radial` −2.4)을 더한 `Threads`, 바닥 빛 두 장은 바닥에 눕힌 판(`FloorGlow`). 빌더에 메뉴 `Build E1 (coil only)`를 더함(`Build`는 F5 · E1 · E2 · E3 · E4). 연결: `SwordFightSkillFx.KnightVfx.cs`의 `KnightVfxCoil` — `SwordFightSkillFx.Edge.cs` 실사 갈래에 `KnightCrouch` 줄을 새로 더해 부르고, 두 발 가운데(판 위면 판 높이)에 냄, 단계가 Windup · Active · Recovery가 아니면(처음 0.2초는 기다림) 바로 지움. 끄기: `KnightVfxOn = false`(다른 층과 같이).
- E2 부품(R192, 새 파일 — 공용 부품은 안 고침): 킹 R185의 `VfxStarFlash` · 셰이더 `VFX_StarFlashAdd`를 기본값 그대로 다시 씀(재질 값으로 가로/세로 2.2 · 가로줄 0.8 · 십자 1.4 · 빛살 10개 · 번짐 0.5), 새 재질 `M_Knight_SpringStar` · `M_Knight_SpringStreak`(`Soft Glow Additive` ×5), 메시는 E4의 `SM_Knight_Star`를 그대로 씀(빌더 `StarMeshAsset()`: 없을 때만 만듦), 줄은 E4의 `Streaks`(꼬리만 그림)에 개수 · 속도 · 굵기 · 원뿔 26° · 아래 40°만 바꿈. 빌더에 메뉴 `Build E2 (spring only)`를 더함(`Build`는 F5 · E2 · E3 · E4). 연결: `SwordFightSkillFx.KnightVfx.cs`의 `KnightVfxSpring` — `SwordFightSkillFx.Edge.cs`의 `KnightFlip` 줄에서 실사 `RealKnightFlip` 바로 뒤 · E3 앞에 부르고, 발굽에서 가장 가까운 테(`SwordFightEdgeFloor.Nearest`)가 0.6 m(`SpringLip`) 안이면 그 테 위 · 바깥쪽으로, 아니면 발굽 자리에 40°(`SpringTip`) 들어서. 끄기: `KnightVfxOn = false`(다른 층과 같이).
- E3 부품(R182, 새 파일 — 공용 부품은 안 고침): 셰이더 `VFX_PathRibbonAdd`(`Path Ribbon Additive`: 길 위 점마다 그린 시각을 담고, 정점 셰이더가 카메라 쪽으로 띠를 펼쳐 시각이 지난 만큼 좁히고 흐리게 함, 아직 안 그린 앞쪽은 안 보임, 몸에 닿는 곳 깊이 흐림) · 재생 `VfxPathRibbon`(`IVfxSeekable`, 점 · 시각 목록 → 부드러운 곡선 메시, 게임은 `Begin` · `Trace`로 길을 깖) · 재질 `M_Knight_FlipArc`. 빌더에 메뉴 `Build E3 (flip only)`를 더함(`Build`는 F5 · E3 둘 다). 1차 수정: E3 재질은 처음 만들 때 값(심 ×1.2 · 심 폭 15% · 번짐 꺾임 0.5 · 머리 +1.0)도 넣고(`Material` 겹 함수), 띠는 폭 0.18 m · 좁아짐 0.4로 만듦(F5 줄과 셰이더 · `VfxPathRibbon` 기본값은 그대로).
- E4 부품(R189, 새 파일 — 공용 부품은 안 고침): 킹 R185의 `VfxStarFlash` · 셰이더 `VFX_StarFlashAdd`를 기본값 그대로 다시 씀(재질 값으로 정사각형 · 가로줄 0 · 십자 0 · 빛살 16개), 새 재질 `M_Knight_KickStar` · `M_Knight_KickStreak`(`Soft Glow Additive`), 메시 `SM_Knight_Star`, 빛줄기는 Shuriken 꼬리(Trails)만 그림(늘인 입자는 시작에 꼬리가 나이트 쪽으로 튀어나와서). 빌더에 메뉴 `Build E4 (kick only)`와 `Quiet` · `KeepParticles` 도우미를 더함(`Build`는 F5 · E3 · E4). 연결: `SwordFightSkillFx.KnightVfx.cs`의 `KnightVfxKick` — `SwordFightSkillFx.Edge.cs`의 `KnightKickHit` 줄에서 실사 `RealKnightKickHit` 바로 뒤에 부르고, 실사와 같이 `OnBody`로 닿는 곳을 찾음. 끄기: `KnightVfxOn = false`(다른 층과 같이).
- E3 연결: `SwordFightSkillFx.KnightVfx.cs`(`KnightVfxFlip`). `SwordFightSkillFx.Edge.cs`의 `SfFxKind.KnightFlip`(실사일 때)이 실사 공중제비(`RealKnightFlip`) 바로 뒤에 부른다. 실제 두 뒷발굽 가운데를 비행의 42%부터 착지 뒤 0.09초까지 따라 그림(`FlipPath`는 다른 PC로 안 가는 값을 써서 안 씀). 끄기: `KnightVfxOn = false`.

## 10. Unity에서 확인할 것

[VALIDATION](../Network/VALIDATION.md) 맨 위 "R159 VFX 작업대" 표.

## 11. 작업 방식 — 새 스킬부터 (R166, 10-27, DECISIONS FX7)

이미 만든 효과(킹 · 룩 · 퀸)는 **그대로 둔다.** 다음에 고치는 스킬부터 아래 방식으로 한다. 기존 효과는 사용자가 보고 고칠 게 생긴 스킬만 이 방식으로 다시 만든다.

| 항목 | 방식 |
|---|---|
| 셰이더 | HLSL `.shader` (`Assets/VFX/Shaders`) |
| 파티클 | Shuriken(Particle System)을 **C# 에디터 스크립트가 프리팹에 저장**. 편집 모드에서 미리보기 · 인스펙터 조절 |
| 메시 | 생성 스크립트가 에셋으로 저장 (`Assets/VFX/Meshes`) |
| 텍스처 | 노이즈 · 그라디언트 · 스트릭을 절차적으로 생성해 에셋으로 저장 (`Assets/VFX/Textures`) |
| 진행 | **레이어 하나씩**: 만들기 → 사용자가 메뉴 실행 · 스크린샷 → 확인 → 다음 레이어 |
| 조절 값 | 전부 머티리얼 프로퍼티 · 인스펙터 노출 값 |
| 색 · 밝기 | 기존 효과와 같은 기준: 기물 색, §6 HDR 사다리, 게임 블룸 1.2 / 0.3 |

- **에디터 스크립트:** `Assets/Scripts/VFXEditor`(어셈블리 `ChessFight.VFX.Editor`, 에디터 전용). 첫 스킬을 시작할 때 만들고 컴파일 검사 스크립트에 더한다. 메뉴는 `ChessFight/VFX/…`. AI가 이 PC의 에디터를 직접 다루지 못하므로 사용자가 메뉴를 한 번 눌러 에셋을 만든다.
- **시안:** `Assets/VFX/Reference`. 남의 영상 · 캡처가 들어가므로 **git에 올리지 않는다**(`.gitignore`, 폴더의 `.gitkeep`만 올림).
- **공용 부품 · 셰이더**(`VfxRing` · `VfxSparks` · `VfxFlash` · `VfxCue` · `VFX_ScrollDissolveAdd` · `VFX_SoftGlowAdd`): 새 스킬은 새 파일을 쓴다. 꼭 고쳐야 하면 기능만 더하고 기본값은 그대로 둬 기존 효과가 바뀌지 않게 한다.

### 11.1 레퍼런스 재현 계획을 이 방식에 합침 (R172, 10-27)

사용자가 Niagara 시안 4종(Fire Beam · Gothic Blood Slash · Cosmic Coil Dash · Vengeful Twilight)을 레이어별로 재현하는 4단계 계획(점검 → 설정 → 작업 기반 → 캡처)을 보냈고, **새로 만들지 않고 이 문서의 구조에 합치기로** 했다(사용자 "추천대로 해").

| 항목 | 정한 것 |
|---|---|
| 작업 폴더 | `skill-network-test`(`C:/Work/ChessFight-SkillNet`)만 |
| 블룸 기준 | **게임 기준 1.2 / 0.3 유지**(FX3). 계획의 0.9 / 0.5는 작업대 볼륨 B로만 |
| Tonemapping | 게임 기준 ACES 유지. Neutral 비교는 캡처 도구가 생기면 색상 차트로 |
| 코드 주석 | **영어 유지**(저장소 규칙). 문서 · 답변만 한국어 |
| 규칙 파일 | 루트 `CLAUDE.md`는 그대로, VFX 규칙은 이 문서 §11 |
| 프로젝트 설정 | 바꿀 것 없음(Linear · Force Text · HDR · Opaque · Depth 이미 켜짐) |
| 캡처 폴더 | 프로젝트 루트 `VFXCaptures/<이펙트>/<레이어>/`, `.gitignore`로 제외 |
| Unity 조작 | 이 PC에서 **Unity MCP가 SkillNet 에디터에 연결됨**(포트 6400): AI가 메뉴 실행 · 콘솔 읽기 · 캡처를 직접 한다. 위 "AI가 직접 다루지 못하므로"는 이제 맞지 않음 |
| 커밋 | 사용자가 지시할 때까지 하지 않음 |

3단계(R172): 진행 기록 [`VFX_Progress.md`](VFX_Progress.md)(비숍 7층 + 시안 4종 층 목록 · 상태 · 확정 값 · 비숍 레퍼런스 이미지 프롬프트), 명령 `/vfx-layer` · `/vfx-review` · `/vfx-done`(`.claude/skills/`, 이 폴더에서 연 세션에서만 보임, 사용자가 부를 때만 실행). 레퍼런스는 기물 스킬마다 사용자가 프롬프트로 AI 이미지를 만들어 `Assets/VFX/Reference/<대상>/`에 넣는다.
4단계(R172) **캡처 파이프라인:**

| 부분 | 내용 |
|---|---|
| 시간 맞추기 | `VfxCue.Seek(t)`: 단계(켜기 · 끄기 · 파티클 재생 · 멈춤)를 t까지 다시 적용, 모든 Shuriken을 시작 시점부터 `Simulate`(씨앗 고정), `IVfxSeekable` 부품에 각자 나이 전달, 재질에 `_VfxTime` = t. 편집 모드에서도 됨. `Play()`가 다시 흐르게 함. 런타임 재생도 같은 `VfxCue` |
| 시간 받는 부품 | `IVfxSeekable`(지금은 `VfxSigil` + 모든 Shuriken). **기존 `VfxFlash` · `VfxRing` · `VfxSparks` 등은 아직 안 받음** → 캡처에 안 나옴(캡처가 경고와 `capture.txt`에 이름을 적음). 새 부품은 처음부터 `IVfxSeekable`로, 기존 부품은 그 스킬을 다시 만들 때 기능만 더함 |
| 셰이더 시간 | 새 셰이더는 `_Time` 대신 `_VfxTime`(재질 프로퍼티, `VfxCue`가 넣음) |
| 캡처 씬 | `Scenes/VFX_Capture.unity`(어두운 단색 배경 · 어두운 unlit 바닥 · 3인칭 위치 카메라 FOV 40 · 플레이 때 GameLook이 덮는 부드러운 보정 볼륨 `Settings/VFX_Capture_Look`). **프리뷰 씬으로 열어** 열린 씬을 건드리지 않음 |
| 색 | 1280 × 720, 게임 볼륨(GameLook: ACES · 블룸 1.2 / 0.3 · 노출 0.6) + 부드러운 보정, **HDR 버퍼**(8비트 대상이면 URP가 1 넘는 값을 톤매핑 전에 잘라 블룸이 사라짐 — 처음 시험에서 확인), 마지막에 sRGB로 저장 |
| 저장 | `VFXCaptures/<대상>/<층>/` 시점별 `tX.XXX.png` + `sheet.png`(시점 라벨 붙은 모음 판) + `capture.txt`. 톤 비교는 폴더 뒤에 `_neutral` · `_none` |
| 메뉴 | `ChessFight/VFX/Capture/`: Window(프리팹 · 대상 · 층 · 톤 · 시점), Build Capture Scene, Build Color Chart, Color Chart (ACES, Neutral, None). 코드에서는 `VfxCapture.Capture(prefabPath, target, layer, tone, times, turn, eye, look)`. `VfxCapture.cs` · `VfxWorkbenchSetup.cs`는 URP가 있을 때만 컴파일(`#if CF_URP`, asmdef `versionDefines`, R174) — URP 없이 도는 컴파일 검사 스크립트용 |
| 시점 | `VfxCue.captureTimes`(기본 0.05 · 0.15 · 0.3 · 0.6 · 1.0초, 프리팹마다 바꿈) 또는 창 · 호출에서 직접 |
| 높이 | 프리팹은 바닥(원점)에 놓인다. 가슴에서 나는 효과는 `VfxCue.captureHeight`(기본 0)만큼 올려 찍는다(R178, 비숍 F7 = 0.9 m) |
| 옆에서 | `VfxCapture.Capture(…, turn)`가 효과를 세로축으로 그만큼 돌려 찍는다(기본 0 = 플레이어 시점, R178). 옆에서 본 레퍼런스와 견줄 때 다른 폴더 이름으로 찍는다(비숍 F7 = `F7_side`, 60°) |
| 카메라 | `VfxCapture.Capture(…, eye, look)`가 캡처 카메라를 `eye`로 옮겨 `look`을 보게 한다(없으면 씬의 플레이어 시점 그대로, R181). 멀리 날아가 플레이어 시점에서 화면 위쪽에 작게 보이는 효과를 위에서 전체로 볼 때 다른 폴더 이름으로 찍는다(비숍 F5 = `F5_above`, (0, 3.4, −2.8) → (0, 0.6, 2.2)). `capture.txt`에 카메라 자리를 적는다 |
| 확인 | 같은 t 두 번 캡처가 바이트까지 같음(비숍 핀 6장, 킹 착지 5장 — 연기 Shuriken 포함). 열린 씬은 더러워지지 않음. **Game 뷰와 픽셀 단위로 맞춰 보지는 않음** |

**톤매핑 비교(R172, 색상 차트 `Prefabs/Common/00_ColorChart`, 행 = 크림슨 · 코스믹 블루 · 금 · 흰색, 열 = 0 ~ +4 stop, 0.5 간격):**

| 톤매핑 | 크림슨 (1, 0.02, 0.02) | 코스믹 블루 (0.25, 0.45, 1) | 금 (1, 0.7, 0.3) |
|---|---|---|---|
| ACES(게임) | 0 빨강 → +0.5 주홍 → +1 주황 → +2 살구 → +3~4 크림. **빨강으로 남는 건 +0.5까지** | +1 연하늘, +2부터 거의 흰색 | 0 베이지, +1 크림, +2부터 흰색 |
| Neutral | +4까지 빨강 → 분홍으로 색상 유지 | +2부터 흰색 | +2부터 흰색 |
| 없음 | 잘림. 빨강 유지되나 거칢 | +0.5에 청록, +1부터 흰색 | +1부터 흰색 |

ACES에서는 심(+2~+3)이 어느 색이든 흰색 쪽으로 가고 색은 몸통(0~+1)과 블룸 번짐에서 나온다. **피 같은 빨강은 ACES에서 +0.5를 넘기면 주황이 된다**(Gothic Blood Slash 규칙 "R만 높게, 가운데 ×1.5"와 맞음). **정함(R172, 사용자 "Neutral이 괜찮아 보여. 게임 전체를 바꾸지 말고 작업대에서 Neutral로 비교할 수 있게만 해줘"):** 게임은 ACES 그대로(`GameLook.asset` 안 고침). 작업대에서 **N**으로 Neutral과 비교한다(메뉴 `ChessFight/VFX/Workbench/Add Neutral Tonemap Compare`가 작업대 씬에 볼륨 · `Settings/VFX_Workbench_Neutral.asset`을 넣음). 캡처는 `Tone.Neutral`로 같은 비교(폴더 `_neutral`). 이펙트 값은 게임 기준 ACES에서 맞춘다 — **작업 기준 ACES 확정**(사용자 10-27 "ACES로 작업해줘").
