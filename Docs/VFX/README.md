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

## 9. 기물별 F · E 이펙트

### 킹 (R160, 10-27)

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
에디터 메뉴 **`ChessFight/VFX/Bishop/Build`**(`Assets/Scripts/VFXEditor/BishopVfxBuilder.cs`)가 텍스처 · 재질 · 프리팹 · `Resources/VfxSets/Bishop`을 만든다.
**메뉴를 한 번 누르기 전에는 아무것도 나오지 않는다.** 다시 누르면 프리팹 레이어에서 고친 값과 재질 색은 그대로 남는다.
**Play 중에는 메뉴가 꺼진다**(Play 중에 만들면 부품의 `Awake`가 돈 상태로 프리팹에 저장된다 — 킹 빌더에서 섬광 판이 켜진 채 저장된 일, R171).

| 층 | 순간 | 프리팹 (`Prefabs/Bishop/`) | 모양 | 상태 |
|---|---|---|---|---|
| **1** | F 칼이 박힐 때(사람 있든 없든) | `01_Bishop_F_Pin` | 발밑 바닥에 **은빛 X 문양**(지름 1.4 → 1.6 m): 바깥 고리 + 안 고리 + 축 위 눈금 4개, 비숍 대각선을 따른 X(가운데 · 끝에 마름모). 가운데부터 0.1초에 열리며 번쩍(×4.5) → ×1.8로 가라앉음, 1초(핀 0.8초 + 0.2초) 뒤 사라짐, 돌지 않음(두 칼의 X와 맞게) | **만듦, 확인 대기** |
| **2** (R170) | F 박힌 동안 | `01_Bishop_F_Pin`의 `L2 Motes` | 문양 테두리(반지름 0.72 m 띠)에서 **은빛 알갱이**: 박히는 순간 8개 + 0.8초 동안 초당 32개(약 26개), 0.5~1 m/s로 오르며 천천히 문양 둘레를 돌고 살짝 흔들림, 0.6~1.1초에 작아지며 사라짐(Shuriken, 크기 3~7 cm, 재질 ×2.2) | **만듦, 확인 대기** (사용자: "확인은 나중에 할게 2층도 바로 만들어줘") |
| 3 | F 칼이 날아갈 때 | `02_Bishop_F_Trail` | 두 칼 뒤 은빛 꼬리(Trails) | 다음 |
| 4 | F 던질 때 | `03_Bishop_F_Cast` | 손에서 은빛 섬광 · 퍼지는 알갱이 | 다음 |
| 5 | E 손을 뻗을 때 | `04_Bishop_E_Reach` | 떨어지는 아군 둘레 은빛 고리 | 다음 |
| 6 | E 잡을 때 | `05_Bishop_E_Catch` | 섬광 + 알갱이 터짐 | 다음 |
| 7 | E 내려놓을 때 | `06_Bishop_E_Set` | 내려놓는 자리 바닥 은빛 원 + 피어오르는 알갱이 | 다음 |

- 2층 부품: 텍스처 `Textures/T_Bishop_Mote.png`(64, 뜨거운 점 + 부드러운 빛), 재질 `Materials/M_Bishop_Mote.mat`(셰이더 `Textured Additive`, R168 VFX1), Shuriken 값은 프리팹 `L2 Motes`의 Particle System에서 바로 고침(개수 = Emission, 오르는 속도 = Velocity over Lifetime Y, 도는 속도 = Orbital Y, 흔들림 = Noise).
- 1층 부품: 텍스처 `Textures/T_Bishop_PinSigil.png`(512, 생성), 재질 `Materials/M_Bishop_PinSigil.mat`(셰이더 `Sigil Additive`), 메시 공용 `Meshes/VFX_FloorQuad.asset`, 재생 `VfxSigil`(킹 착지 새 버전 R168과 같은 부품, 기본값 안 바꿈).
- 연결: `SwordFightSkillFx.BishopVfx.cs`(`BishopVfx(key, 위치, 방향)` → `PieceVfx("Bishop", …)`), `.RealRookBishop.cs`의 `DriveStakes`가 칼이 박히는 순간 `F_Pin`. 끄기: `BishopVfxOn = false`.
- 고치는 곳: 프리팹 `01_Bishop_F_Pin` → `L1 Sigil`의 `VfxSigil`(길이 · 열리는 시간 · 번쩍 세기 · 세기 · 크기 · 회전 · 사라지는 비율), 재질 `M_Bishop_PinSigil`(`_Color` · `_RevealSoft` · `_EdgeGlow`).

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
