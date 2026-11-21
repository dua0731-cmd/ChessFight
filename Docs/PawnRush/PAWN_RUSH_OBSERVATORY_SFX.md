# 폰 러쉬 왕실 천문대 — 장치 효과음 자리 (R725 ro-vfx, 2026-11-20)

사용자 "온라인에서 아까 모든 pc 테두리 섬광 등등 이런거 사운드 담당자가 편하게 할 수 있도록 적어두고". 천문대 장치 VFX(① 유성우 · ② 태양 플레어 · ③ 초점 광선 · ④ 조리개, 세션 ro-vfx)가 **언제 · 어디서 · 누구 화면에** 나는지와, 소리를 붙일 코드 자리를 적었다. R725 때는 소리가 없었다(천문대 효과음은 R725에서 하지 않기로 했었음). **R742(11-21)에 소리가 들어갔다 — 아래 '-1절'.**
⑤ 꽃잎 · ⑥ 톱니 · 궤도 · 구르는 행성 · ⑦ 홀 먼지 · 성운은 세션 ro-vfx2의 것이라 여기 없다(`RoyalObservatoryVfxPetals/Gears/Orbits/Hall*.cs`).

## -1. R742 · 지금 들어간 소리 (2026-11-21, 코드 · 자동 검사까지, 사용자 청취 · Unity 확인 전)

사용자 "천문대 사운드 필요한 곳 확인해" → "네, 만들어". 아래 2절의 자리에 ⑤ 꽃잎 · ⑥ 톱니 · 궤도 · 구르는 행성 · 대천구 고리, 홀 · 발코니 공간 소리를 더해 22종. ElevenLabs(흐름 `3SsTjKFY5rvOHg2svvFA`, 처음 붙인 이름 "R739 …")로 종류마다 2 ~ 4개를 만들고 AI가 파형 숫자로만 골랐다 — [듣기 페이지](https://claude.ai/artifact/KrnWCdo5Qi12kB7fgpKvhd), 원본 · 프롬프트 · 고르는 스크립트 `ArtSource/PawnRush/RoyalObservatory/Sound`(`prompts.md`, `bake.py`, 바꾸려면 `picks.json`).

- **코드:** `RoyalObservatorySound.cs` 하나(빌더가 Content에 붙임, 회색 상자에서도 동작). 장치는 모두 공유 시계의 함수라, 이 PC가 시계를 읽어 "그 순간을 막 지났는지"(주기 안 시각이 표시를 넘었는지, 한 걸음 0.5초 넘으면 아무것도 안 냄 = 늦게 들어온 PC는 지난 일 소리 안 남)로 모든 PC에서 같은 순간에 낸다. 보내는 것 없음. 2절 표의 VFX 코드 자리 대신 장치 값(`MeteorShower.At` · `SolarFlare.CycleTime` · `IrisLeaf.CycleAt` · `StarPetal.Now/FoldsAt/RisesAt` · `RollingPlanet.Now`)을 직접 읽는다. "맞음" 소리만 VFX에서 부른다: `RoyalObservatoryVfxBeamHit.Struck` → `BeamStruck`(모든 PC 3D + 본인 2D), `RoyalObservatoryVfxFlare.CheckCaught` → `FlareCaught`(본인 2D).
- **들리는 범위:** 모든 소리가 `SectionSoundGate`(천문대 구간 안에서만, 효과음 크기). 발코니는 구간 칸(beat) 밖이라 바람만 게이트 없이 이 코드가 직접 줄인다(발코니에 있을 때만, 효과음 크기, 로딩 화면 중 0).
- **크기:** `bake.py`가 PAWN_RUSH_AUDIO 0절 기준(보통 한 번 −21 dB · 큰 타격 −18 · 반복음 −30 RMS 등, 종류별 `HEARD`)으로 AudioSource 크기(`vol`)를 계산 → 코드의 `Volume` 표. 몇 개(광선 맞음 · 조리개 맞닿음 · 꽃잎 경고 · 펴짐 · 행성 멈춤)는 테이크가 작아 1로 막혀 목표보다 조금 작다.

| 장치 | 소리 (파일 `Resources/RoyalObservatorySfx/`) | 언제 | 3D 거리 |
|---|---|---|---|
| 유성우 2곳 | `RoMeteorWarn` · `RoMeteorFall` · `RoMeteorImpact` | 경고 시작 · 착지 `Streak` 초 전 · 착지 | 4–32 · 4–36 · 5–45 m |
| 태양 플레어 | `RoFlareCharge`(11-21 "예고음 크게" → 4 dB 크게) · `RoFlareBurst`(11-21 "살짝 크게" → 3 dB 크게), 본인 `RoFlareHitMe`(2D) | 예고 시작(1.3초) · 타오름 시작 · 그림자 밖에서 맞음 | 13–65 · 15–75 m |
| 초점 광선 2개 | `RoBeamHum`(반복, 두 빛 원 가운데) · `RoBeamStrike`, 본인 `RoBeamHitMe`(2D) | 늘 · 기물이 맞을 때 | 3–22 · 3–28 m |
| 조리개 4곳 | `RoIrisWarn` · `RoIrisScrape` · `RoIrisClash` | 경고 시작 · 닫히기 시작 · 맞닿음 (서쪽 날개 시계, 두 날개 가운데 1.2 m 위) | 3–26 · 3–28 · 4–34 m |
| 변광성 2개 | `RoPetalWarn` · `RoPetalFold`(꽃잎마다, 0.75배 · 음높이 조금씩 다르게) · `RoPetalRise` | 경고 시작 · 꽃잎이 떨어질 때마다 · 다 같이 올라올 때 | 2.5–22 · 3–24 m |
| 궤도 3 · 톱니 4 | `RoOrbitLoop` · `RoGearLoop`(반복, 음높이 조금씩 다르게 — 톱니는 11-21 "장난감 같다"로 묵직한 시계탑 톱니로 다시, 0.84 ~ 0.96배) | 늘 | 3–20 · 3–22 m |
| 구르는 행성 | `RoPlanetRoll`(반복, 속도만큼 크기 · 음높이 0.8 ~ 1.05) · `RoPlanetDock` | 구를 때 · 양쪽 정거장 도착 | 3–26 · 4–30 m |
| 대천구 고리 | `RoGlobeCreak`(반복, 작게) | 늘 | 8–32 m |
| 공간 | `RoHallAmbience`(2D, 발코니에서 30%로) · `RoBalconyWind`(2D) | 천문대 안 · 발코니 | — |

- 콘솔 한 줄 `[천문대 소리] 유성우 2 · 플레어 1 · 초점 광선 2 · 조리개 4 · 변광성 2 · 반복음 N개`.
- 아래 2 ~ 4절은 R725에 쓴 계획(파일 이름 `ro_*`)이고, 실제 파일 이름은 위 표다. 대천구 유리 · 경통 벽 충돌 소리, 돔 별자리 반짝임 소리는 넣지 않았다.

## 0. 먼저 알아 둘 것 — 온라인에서 소리가 맞는 이유

- **장치 시간은 모두 공유 장애물 시계(`ObstacleClock.Now`)의 함수다.** 유성이 언제 떨어지는지, 플레어가 언제 터지는지, 조리개가 언제 닫히는지는 모든 PC가 같은 순간에 스스로 안다. 보내는 패킷이 없다. 소리도 아래 VFX 코드 자리에서 내면 **모든 PC에서 같은 순간**에 난다.
- **맞는 것(누가 날아가는지)은 호스트만 판정한다.** 그래서 "맞았을 때" 소리는 두 가지로 나뉜다.
  - **모든 PC · 3D:** 맞은 기물에서 나는 소리(초점 광선 타격). 각 PC가 장치와 같은 규칙으로 맞는 순간을 스스로 알아내 효과를 내므로(`RoyalObservatoryVfxBeamHit`), 소리도 거기서 내면 된다. 호스트는 판정과 똑같고, 클라이언트는 빛 원 가장자리에서 아주 드물게 다를 수 있다.
  - **맞은 본인만 · 2D:** 화면 테두리 섬광과 같이 나는 소리(태양 플레어 · 초점 광선). 그 PC의 자기 기물(`MiniGameBase.LocalDriver`)일 때만 켜지므로, 소리도 거기서 `twoD: true`로 내면 맞은 사람에게만 들린다.
- **한 번만 내기:** 아래 자리 중 `LateUpdate`에 있는 것은 매 프레임 돈다. 소리는 "그 순간을 막 지났을 때" 한 번만 내야 한다 — 아래 표의 "한 번 기준"(유성 번호, 플레어 번호, 조리개 주기 번호)을 기억해 같은 번호면 다시 내지 않는다. 이미 한 번만 불리는 자리(★)는 그대로 넣으면 된다.

## 1. 소리 내는 법 (이미 있는 것)

```csharp
// 3D (3 m까지 그대로, 30 m에서 0), 또는 2D. 피치 ±5 %.
ArenaFx.Sfx("ro_meteor_impact", worldPosition, twoD: false, volume: .5f,
            fallbackPath: "RoyalObservatorySfx/ro_meteor_impact");
// 끊을 수 있는 / 반복하는 소리
var hum = ArenaFx.Hold("ro_beam_hum", worldPosition, loop: true);   // ArenaFx.Cut(hum)로 끔
```

- `ArenaFx.Sfx`는 `Resources/PawnRushMiniGameSfx/<이름>`을 찾고, 없으면 `fallbackPath`(다른 Resources 경로)를 찾는다. 천문대 파일은 새 폴더 **`Assets/Resources/RoyalObservatorySfx/`**에 두고 `fallbackPath`로 가리키기를 권한다(파일이 없으면 경고 한 번 찍고 조용히 넘어감).
- 음량 기준은 [PAWN_RUSH_AUDIO.md](PAWN_RUSH_AUDIO.md) 0절(보통 한 번 −21 dB, 위험 큰 타격 −18 dB, 반복음 RMS −30 dB).

## 2. 소리 자리 표

시간은 각 장치 한 주기 안의 초. "모든 PC"는 공유 시계라 모든 PC가 같은 순간, "본인만"은 맞은 사람 PC에서만.

| # | 순간 | 누구 · 3D/2D | 위치 | 코드 자리 | 한 번 기준 | 제안 소리 · 파일 이름 |
|---|---|---|---|---|---|---|
| ① a | 유성 경고 시작(금빛 천문 시계판이 나타남) | 모든 PC · 3D | 경고 고리 가운데 `transform.TransformPoint(at)` | `RoyalObservatoryVfxMeteor.Draw` — `age`가 0을 막 지날 때 | 유성 번호 `m.Serial` | 짧은 금속 종 · 천체 차임 `ro_meteor_warn` (−24 dB) |
| ① b | 돌이 떨어지기 시작(공기 줄무늬 · 불티) | 모든 PC · 3D | 고리 위 26 m에서 내려옴(돌 높이 `MeteorShower.StreakFrom * (1 - u)`) | 같은 곳 — `age`가 `warning - Streak`을 막 지날 때 | `m.Serial` | 내려오는 휘이익(0.3 ~ 0.4초, 피치 하강) `ro_meteor_fall` |
| ① c | 착지(금빛 충격파 · 먼지 · 불꽃) | 모든 PC · 3D | 고리 가운데 | 같은 곳 — `age`가 `warning`을 막 지날 때. 또는 장치 쪽 `MeteorShower.FixedUpdate`의 `Landed++` 줄(★ 모든 PC에서 한 번씩 불림) | `m.Serial` | 묵직한 쿵 + 돌 부서짐 + 금속 울림 `ro_meteor_impact` (−18 dB) |
| ② a | 태양 플레어 예고 시작(1.3초 동안 차오름) | 모든 PC · 3D | 태양 `transform.TransformPoint(flare.Sun)` | `RoyalObservatoryVfxFlare.LateUpdate` — `warning`이 막 참이 될 때 | 플레어 번호 `flare.Index(now)` | 1.3초 동안 커지는 웅웅 · 불꽃 타는 소리 `ro_flare_charge` |
| ② b | 움츠림(예고 마지막 15%) | 모든 PC · 3D | 태양 | 같은 곳 — `w`가 `SolarFlare.InhaleFrom`(.85)을 지날 때 | `flare.Index(now)` | 숨 들이쉬는 듯 빨려 드는 소리 `ro_flare_inhale` (선택) |
| ② c | 폭발(팝 · 충격파 고리 · 빛줄기 · 불꽃 분출 · 바닥 빛 물결) | 모든 PC · 3D(대천구 안 어디서나 크게 — 2D로 낼지 결정) | 태양 | 같은 곳 — `burning`이 막 참이 될 때(`bt`가 0 근처) | `flare.Index(now)` | 큰 폭발 · 화염 분출 `ro_flare_burst` (−18 dB) |
| ② d | **내가 그림자 밖에서 맞음(화면 테두리 섬광)** | **본인만 · 2D** | — | `RoyalObservatoryVfxFlare.CheckCaught`의 `caughtAt = Time.time;` 줄 ★ | 이미 플레어마다 한 번 | 지글 타는 · 화끈한 소리 `ro_flare_hit_me` |
| ③ a | 초점 광선 빛기둥(늘 켜져 있음) | 모든 PC · 3D 반복 | 렌즈 `transform.TransformPoint(beam.Lens)` 또는 빛 원 `TransformPoint(beam.SpotAt(now))` | `RoyalObservatoryVfxBeam.Start`에서 `ArenaFx.Hold(..., loop: true)`, `LateUpdate`에서 위치를 빛 원으로 옮김, 70 m 밖이면 `ArenaFx.Cut` | — | 낮은 윙 · 빛 웅웅 반복음 `ro_beam_hum` (RMS −30 dB) |
| ③ b | **기물이 빛에 맞아 날아감(섬광 · 빛 · 불꽃)** | **모든 PC · 3D** | 맞은 기물 몸 `body` | `RoyalObservatoryVfxBeamHit.Struck` 맨 끝 ★ | 이미 맞을 때마다 한 번 | 지지직 + 퍽 타격 `ro_beam_strike` (−21 dB) |
| ③ c | **내가 맞음(화면 테두리 섬광)** | **본인만 · 2D** | — | 같은 `Struck`의 `struckAt = Time.time;` 줄 ★ | 이미 한 번 | 화끈한 타격 `ro_beam_hit_me` |
| ④ a | 조리개 예고(날개 금이 붉게 맥박) | 모든 PC · 3D | 조리개 가운데 | 장치 쪽: `IrisLeaf.Update`에서 `warning`이 켜질 때(★ 서쪽 날개만 경고 막대를 가짐) | 이미 한 번 | 째깍 · 경고 종 `ro_iris_warn` |
| ④ b | 날개가 닫히며 갑판을 긁음(금속 불티, 0.4초) | 모든 PC · 3D | 조리개 가운데(날개 둘이 양쪽에서) | `RoyalObservatoryVfxIris.LateUpdate` — `scrapeAge`가 0을 막 지날 때 | 조리개 주기 번호(`leaf.CycleAt(now)`가 줄어드는 순간 = 새 주기) | 쇠 긁히는 소리 0.4초 `ro_iris_scrape` |
| ④ c | 두 날개가 맞닿음(이음매 섬광 · 불꽃) | 모든 PC · 3D | 조리개 가운데, 높이 1.2 m | 같은 곳 — `clashAge`가 0을 막 지날 때 | 같은 주기 번호 | 쾅 닫히는 금속 충돌 `ro_iris_clash` (−18 dB) |

- 조리개는 4곳(1.8초씩 엇갈림), 유성우는 2곳(손잡이 · 카시오페이아), 초점 광선은 2개(렌즈 하나 공유 — 빛기둥 반복음은 하나만), 플레어는 1곳.
- 대천구 유리 · 경통 벽 충돌(R725에 추가)은 소리가 필요 없다고 봤다(부딪히는 소리를 원하면 폰 쪽 충돌 소리로).

## 3. 한 번만 내는 예 (유성 착지)

```csharp
// RoyalObservatoryVfxMeteor 안: 마지막으로 소리 낸 유성 번호
long lastImpact = -1;
// Draw(...) 안, 착지 순간을 막 지났을 때
if (age >= warning && m.Serial > lastImpact)
{
    lastImpact = m.Serial;
    ArenaFx.Sfx("ro_meteor_impact", transform.TransformPoint(at), volume: .5f, fallbackPath: "RoyalObservatorySfx/ro_meteor_impact");
}
```

- `Draw`는 유성이 보이는 동안 매 프레임 불리고(60 m 밖 유성은 안 불림 — 멀면 소리도 안 나서 괜찮음), 섹션에 늦게 들어와도 이미 지난 유성은 `age`가 커서 바로 위 조건이 한 번 참이 될 수 있다 — 들어온 직후 한 번 겹쳐 나는 게 싫으면 `age < warning + .1f`도 함께 본다.
- 플레어 · 조리개도 같은 꼴: "번호가 바뀌었고 그 순간을 막 지났다"면 한 번.

## 4. ElevenLabs 프롬프트 제안 (정원 · 미니게임 소리처럼 만든다면)

| 파일 | 길이 | 프롬프트 |
|---|---|---|
| `ro_meteor_warn` | 0.6 s | soft magical brass chime with a shimmering star twinkle, celestial, short |
| `ro_meteor_fall` | 0.4 s | fast descending whoosh of a burning meteor, sparkling crackle, pitch falling |
| `ro_meteor_impact` | 1.0 s | heavy marble stone slam with golden metallic ring and dust burst, deep thud |
| `ro_flare_charge` | 1.3 s | rising solar hum with crackling fire, building tension, ends abruptly |
| `ro_flare_inhale` | 0.25 s | short reverse suck of air and fire, tension before an explosion |
| `ro_flare_burst` | 1.2 s | huge solar flare explosion, roaring fire burst with a deep boom and sizzling tail |
| `ro_flare_hit_me` | 0.6 s | close sizzling burn hit, hot searing flash, short |
| `ro_beam_hum` | loop 4 s | low warm hum of a focused light beam, soft glassy resonance, seamless loop |
| `ro_beam_strike` | 0.5 s | electric sizzle and punchy impact of a concentrated light beam hitting armor |
| `ro_beam_hit_me` | 0.5 s | close searing light hit, sharp sizzle, short |
| `ro_iris_warn` | 0.7 s | mechanical clock ticking and a small warning bell, brass machinery |
| `ro_iris_scrape` | 0.45 s | heavy brass plates sliding fast and scraping a marble floor, metal sparks |
| `ro_iris_clash` | 0.8 s | two heavy brass doors slamming shut together, metallic clang with ring-out |

## 5. 기록

- 효과 코드: `Assets/Scripts/PawnRush/RoyalObservatoryVfx*.cs`(Meteor · Flare · Beam · BeamHit · Iris), `RoyalObservatoryDressing.Vfx.cs`(붙이는 곳). 장치 코드(`MeteorShower` · `SolarFlare` · `FocusBeam` · `IrisLeaf`)에는 읽기 전용 값만 더했다.
- 확인 상태: ①·②·③·④와 초점 광선 타격감 모두 사용자 Unity Play 확인(11-20), 온라인 두 PC 확인 전 — [VALIDATION](../Network/VALIDATION.md).
