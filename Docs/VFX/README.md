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

## 9. Unity에서 확인할 것

[VALIDATION](../Network/VALIDATION.md) 맨 위 "R159 VFX 작업대" 표.
