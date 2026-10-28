# VFX 진행 기록 (R172, 2026-10-27~)

`/clear` 뒤에도 **이 파일만 읽으면 이어서 작업할 수 있게** 적는다. 작업 방식 · 규칙은 [README §11](README.md#11-작업-방식--새-스킬부터-r166-10-27-decisions-fx7), 밝기 · 색 기준은 [README §6](README.md#6-hdr--블룸-공통-기준).

- 작업 폴더: `C:/Work/ChessFight-SkillNet`(`skill-network-test`). Unity MCP 인스턴스 `ChessFight-SkillNet`(포트 6400).
- 명령: `/vfx-layer <대상> <층>` 만들기 → `/vfx-review <대상> <층>` 비교 · 수정안 → `/vfx-done <대상> <층>` 확정 · 커밋. 명령은 이 폴더에서 연 Claude 세션에서만 보인다(`.claude/skills/`).
- 대상 이름: 기물 스킬은 `bishop` · `knight` · `pawn` …, 시안 4종은 `fire_beam` · `blood_slash` · `cosmic_coil` · `vengeful_twilight`.
- 블룸 · 톤매핑: 게임 기준 1.2 / 0.3 · ACES 유지(FX3). 작업대 볼륨 B(0.9 / 0.5)는 비교용.
- 캡처: `VFXCaptures/<대상>/<층>/`(git 제외). 메뉴 `ChessFight/VFX/Capture/Window` 또는 MCP에서 `ChessFight.VFX.Editor.VfxCapture.Capture(prefabPath, target, layer, tone, times)`(편집 모드만). 시점별 PNG + `sheet.png` + `capture.txt`(시간을 받지 못하는 부품 이름). 같은 t는 같은 그림. 자세히는 [README §11.1](README.md).
- 톤매핑: 비교 캡처 끝(`VFXCaptures/color_chart/compare_aces_neutral_none.png`, README §11.1 표). **작업 기준 = ACES 확정**(사용자 10-27 "ACES로 작업해줘"). 캡처 · 비교 · 값 맞추기는 모두 `Tone.Game`(ACES). Neutral은 작업대 N 키 · 캡처 `Tone.Neutral`로 참고만.
- 레퍼런스: `Assets/VFX/Reference/<대상>/`(git 제외). 기물 스킬은 아래 프롬프트로 사용자가 AI 이미지를 만들어 넣는다.
- 커밋: `/vfx-done`을 부를 때만, 그 층 파일만, 푸시 안 함.

**상태:** 대기 → 진행 → 검토(사용자 확인 대기) → 완료

---

## 비숍 (소드 파이트) — 지금 작업 중

**10-28 R197에 R172 ~ R195(비숍 · 킹 · 나이트 층 모두)를 `skill-network-test`에 로컬 커밋함**(사용자 "여기까지 하고 커밋", 푸시 안 함). 아래 "커밋 안 함"은 그 전 상태다. F1 뒤 "칼이 정말 칼자루부터 생기게 게임 코드 고치기"는 사용자가 취소 — F1은 R194 그대로(칼은 통째로 그려지고, 안 생긴 칼날을 빛으로 덮음).

색: 비숍 고유색 은색 `#C2C9D3`. 실사 아님, 마법 느낌(사용자 "리얼리스틱하게 안해줘도 돼"). 먼지 퍼프 금지, 보라 · 무지개 금지.
빌더 메뉴 `ChessFight/VFX/Bishop/Build`(`Assets/Scripts/VFXEditor/BishopVfxBuilder.cs`, Play 중에는 꺼짐). 게임 연결 `SwordFightSkillFx.BishopVfx.cs`.

**사용자 10-27: "비숍 프리팹을 처음부터 다시 작업할 거야."** F 스킬이 10-27에 **관통 핀 → X자 베기**로 바뀌었으므로(`SwordFightSkills.cs` `StepBishop` · `BishopThrow` · `BishopCross`) F 층을 새 동작에 맞춰 처음부터 짠다. 예전 핀 프리팹 `01_Bishop_F_Pin`(X 문양 · 알갱이)과 그 재질 · 텍스처는 **10-28에 지움**(R173, 아래 남은 이슈).

**F X자 베기 — 동작 · 타이밍 (코드 기준, `SwordFightSkillParams`):**

| 단계 | 시간 | 일어나는 일 |
|---|---|---|
| 조준 | F 누름 ~ 좌클릭 | **왼손에 두 번째 칼**이 생김(오른손 칼과 같은 칼). 화면 가운데가 가리키는 바닥(8 m까지)을 조준, 반경 1 m 안 적이 과녁. 우클릭 취소 |
| 젖히기 | 0.2초 (`bishopThrowWindup`) | 비숍은 제자리, 두 칼을 뒤로 젖힘 |
| 날아감 | 거리 ÷ 14 m/s, 0.28~0.6초 | 두 칼이 **양옆으로 넓게 휘어** 날아감, 칼이 **납작하게 초당 6바퀴** 돎. 엇갈리는 곳 = 과녁 **0.8 m 앞**(칼은 적에 닿지 않음, 맞음은 이펙트로 보여 줌), 최소 비숍에게서 0.9 m |
| X 엇갈림 | 0.14초 (`bishopCrossTime`), 높이 0.55 m | 각 칼이 **자기 쪽 높은 곳 → 반대쪽 낮은 곳**으로 내려 베며 가운데서 엇갈림 = 공중 X. 한가운데(0.07초)에 맞음 |
| 맞음 | X 한가운데 | 엇갈림 지점 **반경 1.45 m** 적 모두 맞고 **3.2 m 밀림**(킹 방어는 칼처럼 막음) |
| 돌아옴 | 0.85초 (`bishopBackTime`) | 칼마다 **무작위 곡선**으로 휭 돌아다니며, 돌면서 **느려지고**, 끝에 손에 쥔 모양으로 돌아섬 |
| 받음 · 후딜 | 0.35초 | 두 손에 칼이 들어옴 |

예: 과녁 6.4 m → 엇갈림 5.6 m → 젖히기 0~0.2초, 날아감 0.2~0.6초, X 0.6~0.74초(맞음 0.67초), 돌아옴 0.74~1.59초, 받음 ~1.94초.
게임 연결 이벤트: `SfFxKind.Cast`(좌클릭으로 놓아 젖히기 시작) · `BishopLeap`(던짐) · `BishopCross`(X) · `BishopHit`(맞은 적마다). **F1(`F_Draw`)은 이벤트가 아님**(R194): F를 누르면 게임은 조준 단계(`SfStage.Aim`)로 갈 뿐 `SfFxKind`를 내지 않으므로, `SwordFightSkillFx.Watch`가 매 프레임 비숍의 `SwordFightSkills.OffBlade`(F부터 베기 끝까지 왼손 칼이 있음)가 생기는 순간을 보고 한 번 낸다(모든 화면이 같은 값을 봄, 뿌리 = 비숍 발밑 바닥 · +Z = 조준 방향, 부품이 `SwordFightPawn.DrawnBlade(1)` 왼칼 칼날을 따라감). **`Cast`에 F3(`F_Windup`)**(R188, 뿌리 = 비숍 발밑 바닥 · +Z = 그의 방향, 칼마다 부품이 손에 쥔 칼 → 던진 칼 복사본의 칼날을 따라감. `Cast`는 모든 기물의 F 시작에 나므로 비숍일 때만, E는 가장자리 이벤트라 여기 오지 않음), **`BishopLeap`에 F4(`F_Throw`)**(R191, 뿌리 = 비숍 발밑 바닥 · +Z = 그의 방향, 칼마다 부품이 놓는 순간의 손(`SwordFightPawn.SwordHand`)에서 시작해 던진 칼 복사본을 따라감), **`BishopCross`에 F6(`F_Cross`)**(R175, 이벤트 위치 = 엇갈림 → 뿌리는 그 아래 바닥), **`BishopHit`에 F7(`F_Hit`)**(R178, 이벤트 위치는 비숍과 적 사이라 쓰지 않고 맞은 적 가슴에 낸 뒤 그 가슴에 매달아 같이 날아감), **`BishopLeap`에 F5(`F_Flight`)**(R181, 뿌리 = 비숍 발밑 바닥 · +Z = 그의 방향, 칼마다 부품이 그 칼의 그려진 복사본을 따라감), **`BishopCross`에 F8(`F_Return`)도**(R186, 뿌리 = 비숍 발밑 바닥, 칼마다 부품이 돌아오는 칼의 그려진 복사본을 따라감)가 붙음.

**F 층 계획 (새로):**

| 층 | 순간 | 모양 (초안) | 프리팹 | 상태 | 레퍼런스 |
|---|---|---|---|---|---|
| **F1** | **F 누름** | 은빛 조각 44개가 주위에서 날아와 왼손 칼날에 칼날 시작 → 칼끝 순서로 붙음(0.1초 뒤부터 0.3초), 아직 안 붙은 칼날은 빛으로 덮여 있다가 줄어듦, 붙는 끝에 반짝임이 타고 올라가 마지막 조각 때 칼끝에서 별처럼 번쩍(0.14초) → 그냥 강철 | `01_Bishop_F_Draw` | **검토**(R194 · 10-28 만듦, 사용자 확인 전, 커밋 안 함) | `F_01_draw.png`(10.png 왼쪽), 비교용 `F_01_draw_alt.png`(10.png 오른쪽) |
| F2 | 조준 중 | 바닥에 엇갈림 지점 작은 X + 맞는 범위(1.45 m) 옅은 빛 선 | `02_Bishop_F_Aim` | 대기 | `F_01_draw.png`(10.png 두 장 모두 X · 원이 있음) |
| **F3** | **젖히기 0.2초** | 두 칼날을 따라 은빛이 칼끝으로 차오름(칼날 시작 → 칼끝, 0.16초에 끝까지, 끝으로 갈수록 밝고 좁아짐) + 빛이 닿으면 칼끝에 작은 반짝임, 놓을 때 번쩍했다가 날아가는 칼 위에서 0.15초에 꺼짐 | `03_Bishop_F_Windup` | **확정**(R188 · 10-28 사용자 "F3 확정", 다른 점 3개는 그대로, 커밋 안 함) | `F_02_throw.png`(9.png 왼쪽), 비교용 `F_02_throw_alt.png`(9.png 오른쪽) |
| **F4** | **놓는 순간** | 손끝에 아주 짧은 은빛 별 섬광(0.09초) + 손끝에서 날아가는 칼까지 이어지는 가는 빛줄(공기가 갈라짐, 칼 쪽이 밝음) 0.05초 → 칼 쪽으로 닫히며 0.1초에 사라짐 | `04_Bishop_F_Throw` | **확정**(R191 · 10-28 사용자 "F4 확정", 다른 점 3개는 그대로, 커밋 안 함) | `F_02_throw_alt.png`(칼이 손을 떠나며 손에 별 반짝임 — F4에 더 가까움), `F_02_throw.png` |
| **F5** | **날아감** | 도는 칼의 은빛 잔상 원판 + 원판 바깥 테두리에서 휜 길을 따라 뒤로 남는 줄무늬 꼬리(2.4 m, 칼 쪽이 굵고 밝음) | `05_Bishop_F_Flight` | **확정**(R181 · 10-28 사용자, 커밋 안 함) | `F_03_flight.png`(7.png 오른쪽), 비교용 `F_03_flight_close.png`(7.png 왼쪽) |
| **F6** | **X 엇갈림** | **공중에 남는 초승달 모양 베기 빛 두 줄이 X**(약 2.5 m), 엇갈림 점에 작은 별 반짝임 — 이 스킬의 얼굴 | `06_Bishop_F_Cross` | **확정**(R175 · 10-28 사용자, 커밋 안 함) | `F_04_cross.png` |
| **F7** | **맞은 적마다** | 몸에 은빛 X 베인 자국 번쩍 + 밀리는 방향으로 은빛 불꽃 · 공기 줄, **고리 없음** | `07_Bishop_F_Hit` | **확정**(R178 · 10-28 1~3번 고친 뒤 사용자, R184에 불꽃 · 공기 줄을 세게 하고 다시 확정, 커밋 안 함) | `F_05_hit.png`(받은 4장 중 3번째), 비교용 `F_05_hit_swoosh.png` |
| **F8** | **돌아옴** | 돌아오는 휜 길을 따라 칼 뒤에 남는 더 가늘고 흐린 꼬리(2.6 m), 먼 끝이 아주 조금 흔들림, 원판 없음(회전은 게임 칼이 느려짐), 손에 들어가면 칼 쪽으로 줄어듦 | `08_Bishop_F_Return` | **확정**(R186 · 10-28 사용자, 커밋 안 함) | `F_06_return.png`(8.png 오른쪽), 비교용 `F_06_return_alt.png`(8.png 왼쪽) |
| F9 | 받음 | 손에 작은 부드러운 반짝임 | `09_Bishop_F_Catch` | 대기 | `F_06_return.png` |

- 추천 순서: **F6(X) → F7(맞음) → F5(날아감) → F8 → F3 · F4 → F1 · F2 → F9**(얼굴이 되는 순간부터).
- 명령은 층 이름 그대로: `/vfx-layer bishop F6`.
- F5 · F8의 칼 자체는 게임 코드가 그림(`SwordFightPawn`의 던진 칼). VFX는 꼬리 · 잔상만. **따라가는 방식(R181에 정함):** 칼마다 재생 부품(F5는 `VfxSwordFlight`)이 게임에서는 `Follow`로 받은 함수(`SwordFightPawn.ThrownSwordOut` — 그려진 복사본의 가운데 · 칼날 방향)를 매 프레임 읽어 자리를 쌓고(실행 순서 120 = 기물을 그린 `RagdollVisualSync` 100 뒤), 따라갈 것이 없는 작업대 · 캡처에서는 게임의 길을 스스로 그린다. F8도 같은 방식(R186): 같은 부품에 `back`을 켜면 `SwordFightPawn.ThrownSwordBack`(X가 끝난 뒤 ~ 손에 들어갈 때까지)을 따라가고, 작업대 · 캡처에서는 게임의 돌아오는 길(가운데에 선 비숍의 무작위 곡선 — 게임은 비숍이 선 자리로 곡선이 바뀜)을 스스로 그린다. F3도 같은 방식(R188): 칼마다 `VfxBladeCharge`가 게임에서는 `SwordFightPawn.DrawnBlade`(오른칼 = 손에 쥔 칼, 왼칼 = 왼손 칼 복사본, 던지면 각자의 던진 복사본 — 칼날 시작 · 칼끝)를 매 프레임 읽고, 작업대 · 캡처에서는 게임의 젖히기와 던진 뒤 0.3초를 스스로 그리면서 칼 모형 복사본(`Preview sword`, 게임에서는 숨김)을 같이 그린다. F4도 같은 방식(R191): 칼마다 `VfxSwordRelease`가 게임에서는 놓는 순간 손 자리(`SwordFightPawn.SwordHand` — 오른손 `ClickGrip` · 왼손 `LeftGrip`)를 받아 두고 `SwordFightPawn.ThrownSwordOut`(던진 칼 가운데)을 매 프레임 읽으며, 작업대 · 캡처에서는 어깨 위 손에서 게임의 휜 길(F5와 같은 4.2 m · 0.3초)로 칼 모형 복사본을 스스로 던진다. F1도 같은 방식(R194): `VfxBladeForm`이 게임에서는 `SwordFightPawn.DrawnBlade(1)`(왼손 칼)을 매 프레임 읽고, 작업대 · 캡처에서는 조준 자세의 왼칼 모형 복사본을 같이 그린다.

**F6 X 엇갈림 — 만든 값 (R175, 10-28 사용자 확정 · 커밋 안 함, 확정 때 프리팹 · 재질 값 = 빌더 기본값):** 프리팹 `06_Bishop_F_Cross`(뿌리 = 엇갈림 아래 바닥, +Z = 비숍 방향, 앵커 Feet, 길이 0.8초, 부품은 모두 처음부터 켜짐), 키 `F_Cross`. 캡처 `VFXCaptures/bishop/F6/`(0 · 0.03 · 0.07 · 0.12 · 0.2 · 0.32 · 0.45초, `Tone.Game`). 큐의 0초 = 칼이 X 한가운데(맞는 순간)라 베기는 이미 절반 그려진 채 시작한다.

| 부품 | 무엇 | 값 (인스펙터, 빌드 때 그대로 남음) |
|---|---|---|
| `F6 Slash R` · `F6 Slash L` | 초승달 베기 빛: LineRenderer(View 정렬 = 늘 카메라를 봄) + 재생 `VfxSlash`, 재질 `M_Bishop_Slash`(셰이더 `Slash Additive`). R = 오른칼(오른쪽 위 → 왼쪽 아래, `leftward`), L = 반대. 높이 0.55 m(`bishopCrossHeight`) | 길이 2.2 m · 기울기 38° · 양 끝 올림 0.18 m · 양 끝 뒤로 0.08 m · 가운데 폭 0.13 m · 끝 뾰족함 1.3 · 그리기 0.14초(시작 때 0.07초 진행) · 지속 0.6초 · 마지막 60%에 사라짐 · 끝 폭 35% · 밝기 ×2.4 → ×1.5(0.15초) |
| (재질) `M_Bishop_Slash` | 처음 만들 때만 색을 넣음(그 뒤는 사용자 값) | 번짐 `#C2C9D3` · 심 흰색 ×3 · 머리 빛 +2 · 머리 길이 0.1 · 심 폭 24% · 번짐 감소 2.2 · 끝 흐림 12% · 바닥 깊이 흐림 0.1 m |
| `F6 Glint` | 엇갈림 별: 재생 `VfxGlint`, 재질 `M_Bishop_Glint`(셰이더 `Glint Additive`, 정점 셰이더가 카메라 쪽으로 돌림), 메시 `SM_BillboardQuad` | 0.3초 · 밝기 ×5(가운데 약 ×15 = §6 섬광) · 크기 0.9 → 0.45 m · 회전 0 → 25° · 긴 빛살 4 + 짧은 빛살 4(45%) · 빛살 굵기 40 |

**F7 맞음 — 만든 값 (R178, 10-28 사용자 확정 · R184 파티클 세게 · 커밋 안 함):** **R184(10-28, 사용자 "상대 hit 할 때 파티클 더 강하게 하고 확정 해줘")**: 불꽃 30 → 60개 · 3.5~8 → 5~12 m/s · 0.2~0.45 → 0.25~0.6초 · 1.2~2.6 → 1.6~3.4 cm · 늘임 0.03 → 0.035, 공기 줄 7 → 12개 · 밝기 80 → 100% · 굵기 0.8~1.4 → 1~1.8 cm · 0.12~0.22 → 0.14~0.26초 · 늘임 0.06 → 0.1, 재질 `M_Bishop_Spark` 밝기 ×4 → ×6(불꽃 · 공기 줄이 같이 씀). 베인 자국 · X 번쩍임은 그대로. 프리팹 · 재질 값을 직접 바꾸고 빌더 기본값도 같게 함. 캡처 `F7/` · `F7_side/`를 다시 찍음, 고치기 전은 `F7_r178/` · `F7_side_r178/`, 전 · 후 비교 `F7_r184_compare.png`. 프리팹 `07_Bishop_F_Hit`(뿌리 = 맞은 적 가슴, +Z = 밀리는 방향이라 비숍은 뒤쪽, 앵커 Target, 길이 0.6초, 부품은 모두 처음부터 켜짐), 키 `F_Hit`. 게임은 `BishopHit` 때 적 가슴(`bodies[Chest]`)에 내고 그 가슴에 매단다(같이 날아가고 몸이 돌면 같이 돔). 캡처 `VFXCaptures/bishop/F7/`(0 · 0.02 · 0.05 · 0.09 · 0.15 · 0.25 · 0.4초, `Tone.Game`, 바닥 위 0.9 m = `captureHeight`). 캡처 카메라는 비숍 뒤쪽 위에서 밀리는 방향을 본다(플레이어 시점) — 레퍼런스는 옆에서 본 그림이라 불꽃 방향이 다르게 보인다. 레퍼런스: 받은 4장 중 3번째(몸에 X 번쩍 · 밀리는 쪽으로만 튀는 불꽃 · 곧은 공기 줄, 고리 없음), 4번째는 바닥 고리가 있어 뺌. **10-28 1차 수정**(사용자 "레퍼런스와 다른 점 1~3번을 추천 값으로 고치고 다시 캡처해줘"): 자국 길이 0.85 → 1.1 m · 폭 0.07 → 0.055 m, 불꽃 원뿔 55 → 35°, 공기 줄 밝기 55 → 80% · 길이 배율 ×2 → ×4. 사람이 손댄 값이 없어서 프리팹 값을 직접 바꾼 뒤 다시 빌드했다(빌더 기본값도 같은 값). 옆에서 본 캡처 `VFXCaptures/bishop/F7_side/`(효과를 60° 돌림, `VfxCapture.Capture(…, turn: 60)`), 고치기 전 `F7_before/`, 전 · 후 비교 `F7_compare.png`. 같이 보낸 새 그림(날아가는 기물을 크게 감싼 은빛 곡선 줄, `F_05_hit_swoosh.png`)은 사용자 답대로 **비교에만** 쓴다 — 곡선 줄은 F7에 없고, 넣을지는 나중에 정한다(넣으면 "고리 · 호 없음"을 바꿈).

| 부품 | 무엇 | 값 (인스펙터, 빌드 때 그대로 남음) |
|---|---|---|
| `F7 Cut R` · `F7 Cut L` | 몸 앞 X 베인 자국: `VfxSlash`(F6과 같은 부품, 값만 다름), 재질 `M_Bishop_Cut`(셰이더 `Slash Additive`, 몸에 닿는 곳만 깊이 흐림 0.03 m). 가슴에서 비숍 쪽으로 0.3 m(빌더 상수 `CutOut`, 몸 밖) | 길이 1.1 m(10-28 전 0.85) · 기울기 42° · 거의 곧음(양 끝 올림 0.04 m) · 폭 0.055 m(전 0.07) · 끝 뾰족함 1.1 · 그리기 0.045초(맞는 순간부터) · 지속 0.3초 · 마지막 75%에 사라짐 · 끝 폭 30% · 밝기 ×3.5 → ×1.8(0.07초) |
| `F7 Flash` | X 번쩍임: `VfxGlint`, 재질 `M_Bishop_Glint`(F6과 같이 씀). 긴 빛살을 45° 돌려 X 모양(F6 별은 + 모양) | 0.14초 · 밝기 ×5 · 크기 0.6 → 0.3 m · 회전 45 → 60° |
| `F7 Sparks` | 은빛 불꽃: Shuriken(빌더가 프리팹에 저장), 재질 `M_Bishop_Spark`(셰이더 `Soft Glow Additive`, 은색과 흰색 반반 ×6(R184 전 ×4), 감소 2) | 60개 한 번에(최대 96, R184 전 30 · 48) · 밀리는 방향 둘레 원뿔 35°(10-28 전 55°), 위로 15° · 5~12 m/s(전 3.5~8) · 0.25~0.6초(전 0.2~0.45) · 크기 1.6~3.4 cm(전 1.2~2.6) · 중력 1.4 · 진행 방향으로 늘임 0.035(전 0.03) · 끝에 30%로 줄며 사라짐 |
| `F7 Streaks` | 공기 줄: Shuriken, 재질 `M_Bishop_Spark` | 12개(최대 24, R184 전 7 · 16) · 몸 둘레 상자(1 × 0.9 m)에서 밀리는 방향으로 곧게 · 7~11 m/s · 0.14~0.26초(전 0.12~0.22) · 굵기 1~1.8 cm(전 0.8~1.4) · 밝기 100%(R184 전 80%, 10-28 전 55%) · 늘임 0.1(전 0.06) · 길이 ×4(전 ×2 — 굵기에 곱하는 값이라 실제 길이는 거의 그대로, [PITFALLS 55](../Environment/PITFALLS.md)) · 처음 15%에 나타남 |

**F5 날아감 — 만든 값 (R181, 10-28 사용자 확정 · 커밋 안 함, 확정 때 프리팹 · 재질 값 = 빌더 기본값):** 프리팹 `05_Bishop_F_Flight`(뿌리 = 비숍 발밑 바닥, +Z = 그의 방향, 앵커 Feet, 길이 0.8초), 키 `F_Flight`. 게임은 `BishopLeap`(두 칼이 손을 떠나는 순간) 때 내고, 칼마다 `VfxSwordFlight`가 그 칼의 그려진 복사본을 X로 바뀔 때(`BishopOutTime`, 거리 ÷ 14 m/s)까지 따라간다(위 "따라가는 방식"). 작업대 · 캡처에서는 게임의 길(`BishopSwordPose`의 휜 곡선: 어깨 위 손 → 자기 쪽으로 넓게 → 엇갈림 바로 위)을 부품이 스스로 그린다: 4.2 m · 0.3초 · 초당 6바퀴. 캡처 `VFXCaptures/bishop/F5/`(플레이어 시점, 0.02 · 0.06 · 0.1 · 0.15 · 0.2 · 0.26 · 0.34초, `Tone.Game`), `F5_above/`(위에서 전체: 눈 (0, 3.4, −2.8) → (0, 0.6, 2.2), `VfxCapture.Capture`의 새 인자 `eye` · `look`), 비교 `F5_compare.png`. 레퍼런스: 사용자가 글 없이 보낸 7.png 두 장 중 오른쪽(비숍 뒤에서 본 전체 — 플레이어 시점과 같아 AI가 고름), 왼쪽 가까운 그림은 비교에만(사용자가 왼쪽을 원하면 바꿈). 첫 캡처 뒤 사용자에게 보이기 전에 고친 것: 원판이 플레이어 시점에서 옆면으로 얇게 보여 카메라 쪽으로 35% 돌려 그림 · 고리가 너무 고르게 보여 고리마다 밝기 · 길이를 다르게 · 꼬리가 원판 가운데에서 가늘게 나와 원판 바깥 테두리에서 굵게 나오게(레퍼런스처럼) · 가운데 뜨거운 점 줄임 · 원판이 밝은 반과 어두운 반으로 갈라져 보여 고르게.

| 부품 | 무엇 | 값 (인스펙터, 빌드 때 그대로 남음) |
|---|---|---|
| `F5 Sword R` · `F5 Sword L` | 칼 하나의 꼬리: LineRenderer(로컬 좌표, View 정렬) + 재생 `VfxSwordFlight`(`IVfxSeekable`), 재질 `M_Bishop_Trail`(셰이더 `Trail Additive`). R = 오른칼(`hand` 0, +X로 휨), L = 왼칼 | 길이 2.4 m · 칼 쪽 폭 0.32 m · 먼 끝 폭 15% · 원판 반지름의 66%만큼 휜 길 바깥쪽(원판 면 안, `attach`) · 밝기 ×2.2 · X로 바뀐 뒤 0.1초에 칼 쪽으로 줄어들며 사라짐 |
| 각 칼의 `Disc` | 도는 칼 잔상 원판: 메시 `SM_BillboardQuad`를 칼이 도는 면(위 + 자기 쪽 0.35로 기울인 축에 수직, 게임 회전과 같음)에 눕힘, 재질 `M_Bishop_Spin`(셰이더 `Spin Blur Additive`) | 지름 0.95 m · 밝기 ×1.8 · 0.04초에 켜짐 · 날아가는 길의 72~95%에서 꺼짐(칼이 베기로 돌아설 때) |
| (재질) `M_Bishop_Trail` | 처음 만들 때만 색을 넣음 | 번짐 `#C2C9D3` · 심 흰색 ×1 · 심 폭 18% · 번짐 감소 0.8 · 먼 끝으로 감소 1.6 · 칼 쪽 흐림 10% · 줄무늬 9줄 · 굵기 4 · 대비 0.75 · 줄마다 뒤로 닿는 길이 차이 0.55 |
| (재질) `M_Bishop_Spin` | 처음 만들 때만 색을 넣음 | 번짐 `#C2C9D3` · 가운데 흰 점 ×0.6 · 크기 10% · 칼 뒤 사라짐 2.2(반 바퀴당) · 앞쪽 흐림 0.2 · 둘레 전체 빛 12% · 고리 11개 · 굵기 4 · 대비 0.6 · 테 0.8 · 테 밝기 +0.6 · 바깥 흐림 12% · 카메라 쪽으로 35% |

**F8 돌아옴 — 만든 값 (R186, 10-28 사용자 확정 · 커밋 안 함, 확정 때 프리팹 · 재질 값 = 빌더 기본값):** 프리팹 `08_Bishop_F_Return`(뿌리 = 비숍 발밑 바닥, +Z = 그의 방향, 앵커 Feet, 길이 1.3초), 키 `F_Return`. 게임은 `BishopCross`(X 한가운데, 돌아서기 0.07초 전) 때 F6과 같이 내고, 칼마다 `VfxSwordFlight`(`back` 켬)가 그 칼의 그려진 복사본을 X가 끝난 뒤부터 손에 들어갈 때(`BishopThrowTime`)까지 따라간다. 작업대 · 캡처에서는 게임의 돌아오는 길(`BishopSwordPose`의 돌아오는 곡선: X 반대쪽 아래 → 넓게 돌아 비숍 뒤 → 앞으로 내민 손, 흔들림 0.18 m, 처음 천천히 · 끝 천천히)을 스스로 그린다: 4.2 m 앞 X에서 · 0.85초 · 초당 4.8바퀴에서 느려짐. 캡처 `VFXCaptures/bishop/F8/`(플레이어 시점, 0.1 · 0.25 · 0.4 · 0.55 · 0.7 · 0.8 · 0.92초, `Tone.Game`), `F8_above/`(위에서 전체, F5와 같은 눈), `F8_front/`(레퍼런스처럼 비숍 앞 위: 눈 (1.1, 2.7, 2.9) → (0, 0.9, 0.4)), 비교 `F8_compare.png`. 레퍼런스: 사용자가 글 없이 보낸 8.png 두 장 중 오른쪽(머리가 비숍 모자이고 꼬리가 칼 뒤에 남음 — AI가 고름), 왼쪽(머리가 왕관 모양)은 비교에만. 첫 캡처 뒤 사용자에게 보이기 전에 고친 것: 줄무늬 5줄이 굵은 띠로 보여 9줄 · 대비 0.6 → 0.45 · 흔들림 0.06 m(0.8 m마다)가 꿈틀거려 0.02 m(1.2 m마다) · 레퍼런스보다 짧아 1.8 → 2.6 m · 폭 0.14 → 0.12 m.

| 부품 | 무엇 | 값 (인스펙터, 빌드 때 그대로 남음) |
|---|---|---|
| `F8 Sword R` · `F8 Sword L` | 칼 하나의 돌아오는 꼬리: LineRenderer(로컬 좌표, View 정렬) + 재생 `VfxSwordFlight`(`back` 켬, 원판 없음), 재질 `M_Bishop_Return`(셰이더 `Trail Additive`, F5와 같은 셰이더 · 다른 재질) | 길이 2.6 m · 칼 쪽 폭 0.12 m · 먼 끝 폭 10% · 칼 가운데에서 나옴(`attach` 0) · 밝기 ×1.4 · 먼 끝 흔들림 0.02 m(1.2 m마다, 뒤로 흘러감) · 손에 들어간 뒤 0.15초에 칼 쪽으로 줄어들며 사라짐 |
| (재질) `M_Bishop_Return` | 처음 만들 때만 색을 넣음 | 번짐 `#C2C9D3` · 심 흰색 ×0.8 · 번짐 감소 1.2 · 줄무늬 9줄 · 대비 0.45 · 나머지는 셰이더 기본값(먼 끝으로 감소 1.6 · 칼 쪽 흐림 10% · 심 폭 18% · 굵기 4 · 줄마다 길이 차이 0.55) |

**F3 젖히기 — 만든 값 (R188, 10-28 사용자 "F3 확정" · 다른 점 3개는 고치지 않음 · 커밋 안 함, 확정 때 프리팹 · 재질 값 = 빌더 기본값):** 사용자가 F3 · F4 프롬프트로 뽑은 9.png 두 장을 글 없이 보냄 → 층 하나씩이라 F3부터, 사용자 "비숍 F3(R188) 이어서 해줘. Unity에 넣고 빌드·캡처·비교까지 하고 멈춰". 프리팹 `03_Bishop_F_Windup`(뿌리 = 비숍 발밑 바닥, +Z = 그의 방향, 앵커 Feet, 길이 0.5초), 키 `F_Windup`. 게임은 `Cast`(좌클릭으로 놓아 젖히기가 시작될 때) 때 내고, 칼마다 `VfxBladeCharge`가 그 칼날을 따라간다(위 "따라가는 방식"). 젖히기 시간은 게임 값(`bishopThrowWindup` 0.2초)을 받는다. 작업대 · 캡처에서는 게임의 젖히기(`BishopKey` · `BishopBlade`: 낮게 앞으로 든 손 → 어깨 위 뒤로 0.12 m 솟으며, 칼날이 앞 아래 → 뒤 위로 돌아감)와 던진 뒤 0.3초(`BishopSwordPose`의 휜 길, 초당 6바퀴)를 스스로 그리고, 비숍 칼 모형(`Resources/SwordFightSwords/Bishop.fbx`, 게임처럼 1.2배)의 복사본을 밋밋한 강철(`M_Bishop_PreviewSteel`)로 같이 그려 빛이 칼날 위에 보이게 한다. **칼날 시작 = 칼 모형 높이 0.36**(Unity에서 메시 폭을 높이마다 재서 정함: 코등이 0.06~0.13, 그 위 투각 장식 0.13~0.35, 칼날 0.36~0.88 — 처음 값 0.11로는 장식부터 빛남), `VfxBladeCharge.BladeStart` · `SwordFightPawn.BladeStart` 두 곳. 캡처 `VFXCaptures/bishop/F3/`(플레이어 시점 = 킹 F3와 같은 눈 (0, 2.68, −5.53) → (0, 1.3, 0)), `F3_front/`(레퍼런스처럼 앞 왼쪽: (−1.9, 1.2, 2.9) → (0, 1.35, 0)), 둘 다 0.04 · 0.08 · 0.12 · 0.16 · 0.2 · 0.25 · 0.32초, `Tone.Game`, 비교 `F3_compare.png`(왼쪽 레퍼런스, 위 앞에서 0.04~0.2초, 아래 플레이어 시점 0.08~0.32초). 시간 못 받는 부품 없음. 레퍼런스: 9.png 두 장 중 왼쪽(두 손에 칼을 쥔 채 칼날이 빛남 — F3에 가까워 AI가 고름), 오른쪽(칼이 손을 떠나며 손에 별 반짝임)은 F4 · 비교용. 첫 캡처 뒤 사용자에게 보이기 전에 고친 것: 빛이 코등이 위 장식부터 시작 → 칼날 시작 0.11 → 0.36 · 가는 줄로만 보임 → 폭 0.09 → 0.17 m · 밝기 ×1.4 → ×2 · 칼끝 별이 너무 큼 → 0.3 → 0.18 m · 빛살 0.35 → 0.2 · 짧은 빛살 0.35 → 0.25 · 칼을 날 쪽에서 보면 칼 몸이 빛 가운데를 가려 검은 줄 → 카메라 쪽으로 0.04 → 0.08 m(시선 방향으로 당겨 화면 자리는 그대로) · 흰 막대처럼 보임 → 심 폭 16 → 22%(중간에 35%도 해 봄) · 심 ×2.5 → ×1.8 · 번짐 감소 1.8 → 1.2 · 줄무늬 7 → 14줄 · 굵기 5 → 9 · 대비 0.35 → 0.55 · 칼끝 쪽으로 폭이 좁아지게(끝 30%). 사람이 손댄 값이 없어 프리팹 · 재질 값을 직접 바꾸고 빌더 · 셰이더 · 부품 기본값도 같게 함.

| 부품 | 무엇 | 값 (인스펙터, 빌드 때 그대로 남음) |
|---|---|---|
| `F3 Blade R` · `F3 Blade L` | 칼날 빛: LineRenderer(로컬 좌표, View 정렬, 칼날 시작 → 칼끝 두 점, 폭 곡선 1 → 0.85(70%) → 0.3(끝) — 빌드할 때마다 빌더 값) + 재생 `VfxBladeCharge`(`IVfxSeekable`, 실행 순서 120), 재질 `M_Bishop_Charge`(새 셰이더 `Blade Charge Additive`). R = 오른칼(`hand` 0), L = 왼칼 | 폭 0.17 m · 밝기 ×2 · 젖히기의 80%(0.16초)에 칼끝까지 차오름 · 차오르는 끝에 밝은 띠(빛이 80%를 넘으면 사라짐) · 놓을 때 ×1.4로 번쩍했다가 0.15초에 꺼짐 · 미리보기 칼 회전 초당 6바퀴 |
| 각 칼의 `Tip` | 칼끝 반짝임: 메시 `SM_BillboardQuad`, 재질 `M_Bishop_TipGlint`(셰이더 `Glint Additive`) | 빛이 75%를 넘으면 켜짐 · 크기 0.09 → 0.18 m · 밝기 ×4(놓을 때 ×5.6) · 12° 돌림(왼칼은 반대) |
| 각 칼의 `Preview sword` | 작업대 · 캡처용 칼 모형 복사본(게임에서는 `Follow` 때 숨김), 충돌체 · 애니메이터 지움 | 그림자 없음, 재질 `M_Bishop_PreviewSteel`(URP Lit, 회색 0.8 · 매끈함 0.45, 처음 만들 때만) |
| (재질) `M_Bishop_Charge` | 처음 만들 때만 색을 넣음 | 번짐 `#C2C9D3` · 심 흰색 ×1.8 · 심 폭 22% · 번짐 감소 1.2 · 칼날 시작 쪽 밝기 35%(칼끝으로 1.6제곱으로 밝아짐) · 빛 끝 흐림 4% · 띠 폭 14% · 띠 밝기 +1.2 · 줄무늬 14줄 · 굵기 9 · 대비 0.55 · 카메라 쪽으로 0.08 m |
| (재질) `M_Bishop_TipGlint` | 처음 만들 때만 색을 넣음 | 은색과 흰색 60%(`#E7EAEE`) · 빛살 0.2 · 짧은 빛살 0.25 · 빛살 굵기 60 · 나머지는 셰이더 기본값 |

**F4 던짐 — 만든 값 (R191, 10-28 사용자 "F4 확정" · 다른 점 3개는 고치지 않음 · 커밋 안 함, 확정 때 프리팹 · 재질 값 = 빌더 기본값):** 사용자 "F3 확정. F4 던짐으로 가줘" → 9.png 오른쪽 `F_02_throw_alt.png`(칼이 손을 떠나며 손에 별 반짝임 · 손에서 칼까지 가는 빛줄)를 레퍼런스로. 프리팹 `04_Bishop_F_Throw`(뿌리 = 비숍 발밑 바닥, +Z = 그의 방향, 앵커 Feet, 길이 0.4초), 키 `F_Throw`. 게임은 `BishopLeap`(두 칼이 손을 떠나는 순간, F5와 같이) 때 내고, 칼마다 `VfxSwordRelease`가 그 순간의 손 자리에서 시작해 던진 칼을 따라간다(위 "따라가는 방식"). 새 셰이더 없음: 섬광은 `Glint Additive`, 빛줄은 `Slash Additive`(F6 셰이더)를 새 재질로 씀. 캡처 `VFXCaptures/bishop/F4/`(플레이어 시점 = F3와 같은 눈 (0, 2.68, −5.53) → (0, 1.3, 0)), `F4_front/`(레퍼런스처럼 앞에서: (−1, 1.5, 5.8) → (0, 1.2, 0.8)), 둘 다 0 · 0.02 · 0.04 · 0.06 · 0.09 · 0.12 · 0.16초, `Tone.Game`, 비교 `F4_compare.png`(왼쪽 레퍼런스, 위 앞에서 · 아래 플레이어 시점, 0~0.09초). 시간 못 받는 부품 없음. 첫 캡처 뒤 사용자에게 보이기 전에 고친 것: 섬광을 손 자리에 두었더니 뒤(플레이어 시점)에서 칼 손잡이 끝이 섬광 가운데를 가려 **검은 구멍이 있는 고리**처럼 보임 → 섬광과 빛줄 시작을 칼이 나가는 쪽(옆 · 앞 · 조금 위)으로 0.14 m 내밀어 손끝에 둠(`flashAhead`), 앞에서 본 캡처의 칼이 0.12초부터 카메라 바로 옆을 지나 눈을 4.5 → 5.8 m로 물림.

| 부품 | 무엇 | 값 (인스펙터, 빌드 때 그대로 남음) |
|---|---|---|
| `F4 Hand R` · `F4 Hand L` | 공기가 갈라지는 빛줄: LineRenderer(로컬 좌표, View 정렬, 손끝 → 칼 가운데 두 점, 폭 곡선 0.35(손끝) → 1(칼) — 빌드할 때마다 빌더 값) + 재생 `VfxSwordRelease`(`IVfxSeekable`, 실행 순서 120), 재질 `M_Bishop_Split`(`Slash Additive`, `_Reveal` 1 = 칼 쪽 끝이 머리라 가장 밝음). R = 오른손(`hand` 0), L = 왼손 | 폭 0.05 m · 밝기 ×2.5 · 놓은 뒤 0.05초 손끝 → 칼 그대로 · 그 뒤 0.1초에 손끝 쪽 끝이 칼로 달려가며 사라짐 · 미리보기 칼 회전 초당 6바퀴 |
| 각 손의 `Flash` | 손끝 섬광: 메시 `SM_BillboardQuad`, 재질 `M_Bishop_Release`(`Glint Additive`) | 0.09초 · 크기 0.34 → 0.17 m · 밝기 ×5(사라짐 (1 − t)²) · 빛살 20° 돌림(왼손은 반대) · 손에서 칼이 나가는 쪽 (0.7, 0.2, 0.68)으로 0.14 m(`flashAhead`) |
| 각 손의 `Preview sword` | 작업대 · 캡처용 칼 모형 복사본(F3와 같은 `PreviewSword`, 게임에서는 `Follow` 때 숨김) | 재질 `M_Bishop_PreviewSteel`(F3와 같이 씀) |
| (재질) `M_Bishop_Split` | 처음 만들 때만 색을 넣음 | 번짐 `#C2C9D3` · 심 흰색 ×2 · 심 폭 30% · 머리 빛 +1.5 · 머리 길이 35% · 양 끝 흐림 15% · 바닥 · 몸 깊이 흐림 0.03 m · 나머지는 셰이더 기본값(번짐 감소 2.2 · 머리 끝 흐림 0.03) |
| (재질) `M_Bishop_Release` | 처음 만들 때만 색을 넣음 | 은색과 흰색 60%(`#E7EAEE`) · 빛살 굵기 60 · 빛살 세기 1.2 · 짧은 빛살 0.5 · 심 0.1 · 번짐 감소 6(기본) |

**F1 두 번째 칼 — 만든 값 (R194, 10-28 만듦 · 사용자 확인 전 · 커밋 안 함):** 사용자 "F4 확정" → F1 · F2 프롬프트를 드림 → 사용자가 뽑은 10.png 두 장을 글 없이 보냄 → 층 하나씩이라 F1부터. 레퍼런스: 왼쪽(비숍 뒤 어깨 너머, 칼이 칼자루부터 조각으로 생김 — 플레이어 시점에 가까워 AI가 고름) `F_01_draw.png`, 오른쪽(앞 왼쪽에서 본 그림) `F_01_draw_alt.png`. 둘 다 바닥 X · 원이 있어 F2 때도 씀. 프리팹 `01_Bishop_F_Draw`(뿌리 = 비숍 발밑 바닥, +Z = 그의 방향, 앵커 Feet, 길이 0.6초), 키 `F_Draw`. 게임은 비숍에게 왼손 칼(`OffBlade`)이 생기는 순간 내고(위 "게임 연결 이벤트"), 부품 `VfxBladeForm`이 그 칼날(`DrawnBlade(1)`, 칼날 시작 = 모형 높이 0.36)을 매 프레임 따라간다. 작업대 · 캡처에서는 조준 자세의 왼칼(손 (−0.3, 0.9, 0.3), 칼날이 앞 · 아래 · 바깥으로 — 게임 `BishopKey` · `BishopBlade`의 낮게 앞으로 든 자세를 거울로)을 칼 모형 복사본으로 같이 그린다. **새 셰이더 없음**: 덮는 빛은 F3의 `Blade Charge Additive`(다 찬 채로 · 차오르는 띠 없이), 조각 · 반짝임은 `Glint Additive`. 캡처 `VFXCaptures/bishop/F1/`(플레이어 시점 = F3와 같은 눈 (0, 2.68, −5.53) → (0, 1.3, 0), 0.02 · 0.08 · 0.14 · 0.22 · 0.32 · 0.4 · 0.5초), `F1_close/`(왼쪽 뒤 가까이: (−1.9, 1.6, −0.9) → (−0.45, 0.68, 0.75), 0.02 · 0.1 · 0.18 · 0.26 · 0.34 · 0.41 · 0.48초), 둘 다 `Tone.Game`, 비교 `F1_compare.png`(왼쪽 레퍼런스, 위 가까이 0.02~0.41초, 아래 플레이어 시점 0.02~0.32초). 시간 못 받는 부품 없음. 첫 캡처 뒤 사용자에게 보이기 전에 고친 것: 조각이 작고 적어 안 보임 → 24 → 44개 · 크기 0.055 → 0.07 m · 밝기 ×3 → ×4 · 날아오는 거리 0.45 → 0.5 m · 0.14 → 0.16초, 덮는 빛 폭 0.15 → 0.18 m · 밝기 ×2.2 → ×2.6, 칼끝 번쩍이 작은 점으로만 보임(F3 칼끝 재질은 빛살이 약함) → F4 손끝 섬광 재질 `M_Bishop_Release`를 그대로 씀 · 크기 0.24 → 0.3 m, 붙는 끝 반짝임 0.12 → 0.16 m. 사람이 손댄 값이 없어 부품을 지우고 다시 빌드함(빌더 `Keep`이 예전 값을 옮기지 않게).

| 부품 | 무엇 | 값 (인스펙터, 빌드 때 그대로 남음) |
|---|---|---|
| `F1 Blade L` | 아직 안 생긴 칼날을 덮는 빛: LineRenderer(로컬 좌표, View 정렬, 붙는 끝 → 칼끝 두 점, 폭 곡선 1 → 0.85(70%) → 0.3(끝) — 빌드할 때마다 빌더 값) + 재생 `VfxBladeForm`(`IVfxSeekable`, 실행 순서 120, `hand` 1 = 왼칼), 재질 `M_Bishop_Form`(F3 셰이더 `Blade Charge Additive`, `_Fill` 1 · `_Front` 0) | 첫 조각까지 0.1초(`lead`) · 칼날 시작 → 칼끝 0.3초(`form`) · 폭 0.18 m · 밝기 ×2.6 · 0.05초에 켜짐 · 붙는 끝이 85%를 넘으면 사라짐 |
| `Shard 00` ~ `Shard 43` | 날아오는 조각 44개: 메시 `SM_BillboardQuad`, 재질 `M_Bishop_Shard`(`Glint Additive`) | 크기 0.07 m(조각마다 60~100%, 붙을 때 1.6배로 커지며 사라짐) · 밝기 ×4 · 칼날 둘레 0.5 m(조각마다 55~100%)에서 날아옴 · 0.16초(조각마다 75~125%, 끝에 빨라짐) · 칼날 위 자기 자리에 닿는 시각 = 0.1초 + 0.3초 × 자리 · 빛살은 조각마다 다른 각도 |
| `Glint` | 붙는 끝을 타고 오르는 반짝임 → 칼끝 번쩍: 메시 `SM_BillboardQuad`, 재질 `M_Bishop_Release`(F4 손끝 섬광 재질, 바꾸지 않음) | 오르는 동안 0.16 m · 밝기 ×3 · 마지막 조각 때 칼끝에서 0.3 m → 0.18 m · 밝기 ×5 · 0.14초에 (1 − t)²로 사라짐 · 빛살 12° 돌림(왼칼이라 반대) |
| `Preview sword` | 작업대 · 캡처용 칼 모형 복사본(F3 · F4와 같은 `PreviewSword`, 게임에서는 `Follow` 때 숨김) | 재질 `M_Bishop_PreviewSteel`(같이 씀) |
| (재질) `M_Bishop_Form` | 처음 만들 때만 색을 넣음 | 번짐 `#C2C9D3` · 심 흰색 ×1.4 · 심 폭 20% · 번짐 감소 1.4 · 칼날 시작 쪽 밝기 75%(칼끝으로 1제곱) · 줄무늬 18줄(촘촘한 조각처럼) · 굵기 10 · 대비 0.75 |
| (재질) `M_Bishop_Shard` | 처음 만들 때만 색을 넣음 | 은색과 흰색 반반 · 심 0.18 · 번짐 감소 5 · 빛살 굵기 30 · 짧은 빛살 0.3 · 빛살 세기 0.5 |

**E 구원의 손 (나중):** 8 m 안 아군이 가장자리 밖으로 떨어지는 중이면(−3 m 전까지) → 진짜 세 가닥 마 밧줄을 던져 아군 허리를 감음 → 당겨 팽팽해지며 끌어올림 → 가장자리 1.2 m 안쪽에 내려놓으면 밧줄이 느슨해짐.

| 층 | 순간 | 프리팹 | 상태 | 레퍼런스 |
|---|---|---|---|---|
| E1 | 밧줄을 던질 때 | `10_Bishop_E_Reach` | 대기 | `E_reach.png` |
| E2 | 밧줄이 팽팽해질 때 | `11_Bishop_E_Catch` | 대기 | `E_catch.png` |
| E3 | 내려놓을 때 | `12_Bishop_E_Set` | 대기 | `E_set.png` |

남은 이슈:
- 예전 핀 층은 **10-28에 지움**(R173, 사용자 요청): 프리팹 `01_Bishop_F_Pin`, 재질 `M_Bishop_PinSigil` · `M_Bishop_Mote`, 텍스처 `T_Bishop_PinSigil` · `T_Bishop_Mote`(Unity 휴지통 처리, Mote 둘은 git에 없던 파일이라 Windows 휴지통에서만 되살림), `Resources/VfxSets/Bishop`의 `F_Pin` 항목(지금 빈 목록). 공용 메시 `VFX_FloorQuad` · 부품 `VfxSigil`은 킹이 써서 남김.
- **빌더를 R175에 새 계획으로 다시 짬**: 예전 핀 코드를 지웠고, 지금 `Bishop/Build`는 F1(R194) · F3(R188) · F4(R191) · F5(R181) · F6 · F7(R178) · F8(R186)을 만들고, `Bishop/Build F1 (draw only)` · `Build F3 (windup only)` · `Build F4 (throw only)` · `Build F5 (flight only)` · `Build F8 (return only)`는 그 층만 만든다(다른 층 프리팹은 그대로). 다음 층은 같은 빌더에 `Build<층>()`으로 더한다. `DriveStakes`의 죽은 `BishopVfx("F_Pin", …)` 호출도 지움.
- **F5 꼬리 방향**: 레퍼런스의 휜 줄은 칼에서 **앞(과녁) 쪽**으로 뻗어 있다. 꼬리는 칼이 지나온 뒤에만 남으므로 F5는 **뒤(비숍) 쪽**으로 뻗는다 → 0.02초엔 꼬리가 거의 없고, 0.2~0.26초(X 직전)에 레퍼런스와 같은 두 초승달이 칼이 X 쪽 끝에 있는 채로 생긴다. 앞쪽 길을 미리 보여 주는 줄은 넣지 않았다(원하면 따로 정함).
- F5는 플레이어 시점(비숍 뒤 3.6 m)에서 칼이 4 m 앞으로 날아가 화면 위쪽에 작게 보인다 → 모양은 `F5_above`(위에서 전체)로 보고 비교한다.
- F5 캡처에는 칼이 없다(게임이 그림). 게임에서는 원판 가운데에 진짜 칼이 돈다. **스킬 시험 씬(Play)에서는 아직 안 봄**: 원판 면은 게임의 회전축(위 + 자기 쪽 0.35)으로 정했으니, 진짜 칼과 어긋나 보이면 축을 칼의 자세에서 읽게 바꾼다. 가까이 던지면(엇갈림은 최소 0.9 m) 날아가는 길이 짧아 꼬리도 그만큼만 생긴다.
- F5 원판 고리가 레퍼런스의 흐릿한 소용돌이보다 아직 고르다(레코드판 느낌, 10-28 사용자는 이대로 확정) → 더 흐리게는 `M_Bishop_Spin`의 고리 11 → 8 · 대비 0.6 → 0.45.
- F5는 작업대(4 비숍)에서 발밑(앵커 Feet)에서 스스로 4.2 m 날아간다. 게임의 거리(과녁 − 0.8 m) · 시간(거리 ÷ 14 m/s)은 게임에서만 쓴다.
- **F8 처음 0.4초**: 돌아오는 칼이 처음에 천천히 움직여서(게임 곡선이 천천히 시작) 꼬리가 짧고 X 근처에 작게만 보인다. 그동안은 F6 X 빛이 떠 있다. 레퍼런스처럼 긴 곡선은 0.55초부터(비숍 둘레로 돌 때) 보인다.
- **F8 레퍼런스와 다른 점**: (1) 레퍼런스 꼬리는 비숍 둘레를 크게 감는 S자로 3 m쯤 길다 → 지금 2.6 m, 더 길게는 3.2 m(꼬리 길이는 칼이 지나온 만큼까지만 생김). (2) 레퍼런스는 칼이 칼끝부터 손에 떨어진다 → 게임은 손잡이부터 잡히는 게 맞고(F 프롬프트대로) VFX와 관계없음. (3) 레퍼런스의 손이 몸에서 떨어져 떠 있음 → 그림 AI의 실수, 무시.
- F8은 작업대(4 비숍)에서 4.2 m 앞 X에서 발밑 손으로 스스로 돌아온다(가운데에 선 비숍의 곡선). 게임은 비숍이 선 자리마다 곡선이 달라 캡처와 다르게 휠 수 있다. **스킬 시험 씬(Play)에서는 아직 안 봄**: 꼬리가 진짜 칼 가운데에서 나오는지, 손에 들어갈 때 칼 쪽으로 줄어드는지 볼 것.
- **F3 레퍼런스와 다른 점(R188, 10-28 사용자가 고치지 않고 "F3 확정")**: (1) 레퍼런스는 칼날 둘레로 푸른빛 도는 번짐이 칼 폭만큼 넓게 퍼지는데, F3는 비숍 은색(`#C2C9D3`)으로 칼날에 붙은 빛이다 → 더 넓게는 폭 0.17 → 0.22 m · 번짐 감소 1.2 → 0.9(푸른빛은 비숍 색이 아니라 넣지 않음). (2) 레퍼런스의 손 쪽 별 반짝임 · 손에서 뻗는 가는 빛줄은 F4(놓는 순간 손 섬광)라 F3에 넣지 않았다. (3) 레퍼런스는 칼을 앞 · 옆으로 내민 자세인데 게임 젖히기는 어깨 위 뒤로 든다 → 빛은 게임 칼을 따라가고, 칼끝이 레퍼런스보다 위쪽에서 빛난다.
- **F3와 실사 `RealCast`가 겹침**: 게임에서 비숍 F를 놓으면 지금 실사 연출(`SwordFightSkillFx.Real.cs`의 `RealCast`)이 0.05초 뒤 오른칼에 반짝임(`BladeGlint`, 0.24초)을 내서 F3 빛과 같이 나온다 → 비숍일 때 끌지 물었으나 확정 때 답이 없어 둘 다 둠(게임 Play로 보고 정함). 킹 F3의 `KingSlash` · F2의 `SteelClash`와 같은 질문.
- **F4 레퍼런스와 다른 점(R191, 10-28 사용자가 고치지 않고 "F4 확정")**: (1) 레퍼런스의 손 섬광은 장갑보다 크게 번지고 가는 빛살이 부채처럼 많다 → 지금 0.34 m · 빛살 8개(긴 4 · 짧은 4), 더 크게는 `flashSize` 0.34 → 0.5 · `flashTime` 0.09 → 0.12. (2) 레퍼런스는 한쪽에 가는 빛줄이 2~3줄 겹쳐 있다 → 지금 손마다 한 줄, 더하려면 부품에 줄 수를 더함(옆으로 몇 cm씩 벌린 흐린 줄). (3) 레퍼런스는 두 팔을 옆으로 뻗은 채 놓고 칼이 하얗게 빛난다 → 게임은 어깨 위로 젖힌 손에서 놓고(그 뒤 팔을 앞으로 휘두름), 칼 빛은 F3가 놓을 때 번쩍했다가 0.15초에 꺼지는 것이라 F4 캡처(F4만 찍음)에는 없다 — 게임에서는 F3 · F4 · F5가 같이 나온다.
- F4는 **스킬 시험 씬(Play)에서는 아직 안 봄**: 놓는 순간 손 자리(`ClickGrip` · `LeftGrip`)가 진짜 장갑에 맞는지, 손끝 섬광이 장갑 · 몸에 가려지는지, 빛줄이 진짜 칼 가운데에 붙는지(이벤트 프레임에 칼 복사본이 아직 안 그려져 있으면 다음 프레임부터 빛줄이 나옴) 볼 것. 실사 `RealCast` 오른칼 반짝임(`BladeGlint`, 젖히기 시작 뒤 0.05~0.29초 — 0.2초에 던지므로 F4와 같은 때)과도 겹친다.
- **F1 레퍼런스와 다른 점(R194, 사용자 답 대기)**: (1) 레퍼런스는 칼을 몸 옆에 세워 들고 있는데 게임 조준 자세는 칼을 낮게 앞으로 든다 → 플레이어 시점(비숍 뒤)에서는 칼이 앞으로 누워 짧게 보여, 칼날보다 조각 무리가 주로 보인다(가까이 캡처에서는 칼날 · 조각 · 칼끝 번쩍이 다 보임). (2) 레퍼런스는 칼 윗부분이 **진짜로 아직 없고** 조각으로만 되어 있다 → 게임은 F를 누르는 순간 왼칼을 통째로 그리므로(`SwordFightPawn.OffHand`) 아직 안 생긴 부분을 빛으로 덮었다. 정말 칼자루부터 생기게 하려면 게임 칼 그리기 코드를 고쳐야 한다(사용자에게 물음). (3) 레퍼런스 조각은 칼날을 따라 흐르는 아주 작은 점들이 훨씬 촘촘하다 → 지금은 별 모양 조각 44개가 0.5 m 밖에서 날아온다. 더 촘촘하게는 조각 수 44 → 70 · 크기 0.07 → 0.05 · 날아오는 거리 0.5 → 0.35.
- F1은 **스킬 시험 씬(Play)에서는 아직 안 봄**: 빛 · 조각이 진짜 왼칼에 붙는지, 조준 자세가 0.12초에 섞여 들어오는 동안 칼이 움직여도 따라가는지, F를 눌렀다가 우클릭으로 취소한 뒤 다시 누르면 다시 나오는지(`OffBlade`가 없어지면 다시 낼 수 있게 함) 볼 것. 조준 단계는 다른 PC에도 보내지므로(`SwordFightSkills.Net`) F1은 모든 화면에 나온다. 조준 중 실사 표시(`RealBishopMarks`, 조준하는 사람 화면만)는 바닥 표시라 칼 위의 F1과 자리가 다르다.
- F1 캡처의 칼은 F3처럼 캡처 장면 조명 때문에 어둡게 보인다(게임 칼은 경기장 빛을 받음).
- F3 캡처의 칼은 캡처 장면 조명 때문에 거의 검게 보인다(게임 칼은 경기장 빛을 받음). 앞에서 본 0.25초 그림은 던진 칼이 카메라 바로 옆을 지나 크게 보인다(캡처 자리 탓, 게임과 관계없음).
- F3는 **스킬 시험 씬(Play)에서는 아직 안 봄**: 빛이 진짜 칼날 위에 붙는지(특히 왼칼 복사본이 놓는 순간 이미 그려져 있는지 — 없으면 그 칼은 그려질 때부터 빛남), 몸 · 손에 가려지는지, 0.08 m 당김이 몸 앞으로 비쳐 보이지 않는지 볼 것.
- F7은 작업대(4 비숍)에서 **과녁 더미 가슴**(앵커 Target)에 나온다. 게임처럼 몸에 매달려 날아가지는 않는다(더미는 밀리지 않음).
- F7 베인 자국은 가슴 중심에서 비숍 쪽 0.3 m에 그린다. 그보다 굵은 기물(킹 · 퀸)이면 자국이 몸 안에 묻힐 수 있다 → 스킬 시험 씬에서 보고 `CutOut`을 0.4로.
- F7 공기 줄은 밀리는 방향으로 곧게 가서 플레이어 시점(비숍 뒤)에서는 X 아래 짧은 세로 줄로만 보인다(옆에서 보면 길게 보임). 10-28 수정 뒤에도 같고, 진해져서 조금 더 보인다. 길이는 늘임(`velocityScale`)이 정한다: R184에 0.06 → 0.1(7~11 m/s × 0.1 ≈ 0.7~1.1 m), 12개 · 밝기 100%로 세게 함 → 더 길게는 0.1 → 0.14, 뒤에서 X 아래로 처져 보이는 게 거슬리면 생기는 상자 높이 0.9 → 0.5.
- F7 불꽃은 R184에 60개 · 5~12 m/s · 0.6초까지로 세게 해서 플레이어 시점에서도 X 위로 넓게 퍼져 보인다(캡처). 실제 경기(맞은 기물 몸 · 화면 크기)에서 너무 세면 개수 60 → 45 · 재질 밝기 ×6 → ×5부터 줄인다.
- F7 불꽃을 35°로 좁혀서 옆에서는 레퍼런스처럼 밀리는 쪽으로만 튀지만, 플레이어 시점에서는 X 가운데로 모이고 게임에서는 맞은 기물 몸 뒤로 더 숨을 수 있다(캡처에는 몸이 없음) → 작업대 · 스킬 시험 씬에서 볼 것.
- `BishopHit`에는 화면 멈춤 · 흔들림이 없다(다른 기물의 맞음은 `RealHit` · `Hit`로 있음). VFX 층 밖이라 넣지 않음.
- 작업대(4 비숍)에서 F6은 **비숍 몸 자리**(발 + 0.55 m)에 나온다: 앵커가 Feet이고 작업대에는 "과녁 앞" 앵커가 없다. 실제 자리(과녁 0.8 m 앞)는 스킬 시험 씬에서 본다.
- 레퍼런스의 "흐트러진 공기 물결"(X 둘레 동심 줄)은 고리 금지라 넣지 않음.
- E 층의 "고리"는 [자연스러운 이펙트](../Skills/EFFECTS.md) 방향(고리 · 덧그린 선 줄이기)과 부딪힐 수 있음 → 레퍼런스를 보고 정함.

### 비숍 레퍼런스 이미지 프롬프트

이미지 AI에 그대로 붙여 넣는다. **공통 머리말 + 순간별 문장**을 이어 붙인다. 만든 이미지는 표의 파일 이름으로 `Assets/VFX/Reference/bishop/`에 넣는다. 한 순간에 2~3장 뽑아 제일 가까운 것을 고른다. **F_00 연속 그림**을 먼저 뽑으면 전체 흐름 · 색이 하나로 맞춰져서, 그 그림을 이미지 AI에 참고 이미지로 넣고 나머지를 뽑으면 장면끼리 어긋나지 않는다.

**공통 머리말 (F · E 모두, R178에 고침):** 확정된 레퍼런스 `F_04_cross.png`를 이미지 AI에 참고 이미지로 같이 넣는다(둘째 문장이 그 그림에 맞추라고 함 — 참고 이미지 없이 뽑을 때는 둘째 문장을 지움). `F_04_cross.png`에 X 둘레 물결이 그려져 와서 금지 목록에 물결 · 호를 더했다.
```
Stylized game VFX concept frame, Unreal Engine Niagara quality. Match the board, lighting, chess pieces and silver slash light of the attached reference image. Setting: a giant polished wooden chessboard arena (walnut and maple squares, thin brass trim along the board edge), dim warm evening light so the effect reads clearly. Characters are toy-like wooden chess pieces with simple rounded arms and white gloved hands. The bishop is ivory-white wood with a tall mitre head; in this skill he holds two identical slim steel longswords with engraved blades, one in each hand. The opponent is a dark ebony wooden chess piece. Effect color: cool silver-white (#C2C9D3) with a hot near-white core and bright steel highlights; no purple, no blue-violet, no rainbow colors, no fire, no lightning, no smoke or dust clouds, no rings, ripples, arcs or circular shockwaves, no blood, no cartoon outlines, no text, no UI. 16:9, one single frame, sharp focus, the effect is the subject.
```

**F_00 · 전체 연속 그림 (`F_00_storyboard.png`)** — 공통 머리말의 "one single frame"을 빼고 붙인다:
```
Show the whole skill as a 6-panel storyboard in two rows of three, the same camera in every panel (behind the bishop, 3 m back and slightly above, looking toward the opponent 6 m ahead). Panel 1: a second sword forming out of silver light in his empty left hand, faint silver aim marks on the board ahead. Panel 2: both swords drawn back over his shoulders, light running up the blades to the tips. Panel 3: the two swords spinning flat out on two wide mirrored curves, one to each side, with silver trails. Panel 4: the two swords crossing in mid-air at waist height just in front of the opponent, leaving a sharp X of silver slash light. Panel 5: the opponent knocked backward off his feet with a brief silver X slash mark and sparks flying the way he is pushed. Panel 6: the swords wandering back on loose curves with faint trails and dropping into the bishop's hands.
```

**F1 · F2 · 두 번째 칼 · 조준 (`F_01_draw.png`)** — 10-28(F4 확정 뒤)에 고쳐서 드린 판. 머리말을 두 군데 바꿔 붙인다: `he holds two identical slim steel longswords with engraved blades, one in each hand`를 `he holds a slim steel longsword with an engraved blade in his right hand`로(두 번째 칼은 아직 생기는 중이라), `no rings, ripples, arcs or circular shockwaves, `를 `no ripples or shockwaves, `로(조준 범위 원 하나는 들어가야 함):
```
Camera: behind the bishop, 3 m back and slightly above, looking past his right shoulder toward the ebony opponent 6 m ahead. He has just called the skill and stands planted, one foot forward, his right sword held low and forward. In his open left glove a second sword identical to the right one is forming from the hilt up: the hilt and lower blade are already solid steel, while the upper blade is still thin silver light shards flying in from the air around his hand and snapping into place, with one crisp glint at the point where the new edge is completing. On the board just in front of the opponent, lying flat on the polished wood: a small sharp X of thin silver light where his swords will cross, and one soft, very faint thin circle of silver light about 3 m across centred on the X. Nothing else is drawn on the board. His head is a tall bishop's mitre like the chess bishop piece, not a crown.
```
처음 판에서 고친 점: ① 카메라를 플레이어 시점(F_00 · F5와 같은 비숍 뒤 3 m)으로, ② 게임은 F를 누르면 조준이 시작되고 그동안 왼손에 거울로 비춘 두 번째 칼을 든다(`SwordFightSkills.OffBlade`) → 칼이 칼자루부터 칼끝으로 생기게(F3 빛이 칼자루 → 칼끝으로 차오르는 것과 같은 방향), ③ 원 = X가 맞히는 반경 `bishopCrossRadius` 1.45 m(지름 약 3 m), 겹쳐 그려지지 않게 "원 하나 · 그 밖엔 아무것도", ④ R186처럼 머리가 왕관으로 그려지지 않게.
- **F1 · F2를 만들 때 볼 것**: 게임은 F를 누르는 순간 왼손 칼을 바로 그린다(`SwordFightPawn.OffHand`) — 그림처럼 칼이 칼자루부터 생기게 하려면 VFX만으로는 안 되고 칼 그리기 코드도 조금 고쳐야 한다(R194에서는 아직 안 생긴 칼날을 빛으로 덮었고, 코드를 고칠지 사용자에게 물음). 지금 실사 조준 표시(`RealBishopMarks`)의 원은 반지름 `bishopRadius` 1 m(예전 관통 핀 값)인데 실제 X가 맞히는 범위는 1.45 m다 → F2는 1.45 m로 그리고 실사 원과 겹치지 않게 정한다.

**F3 · F4 · 젖히기 · 던짐 (`F_02_throw.png`)** — 10-28(F8 확정 뒤)에 고쳐서 드린 판. 머리말의 `he holds two identical slim steel longswords with engraved blades, one in each hand`를 `he throws two identical slim steel longswords with engraved blades, one from each hand`로 바꿔 붙인다(놓는 순간이라):
```
Camera: low three-quarter view from the bishop's front-left. He drew both swords up and back over his shoulders, then whipped both arms forward and out and let go: the two swords have just left his open gloved hands and are only a forearm's length away, one heading forward and out to his left, one forward and out to his right, each just beginning to spin flat. During the draw-back silver light ran up each blade from hilt to tip, so the tips are the brightest points. At each hand a crisp, very short silver flash with a few thin straight light streaks. The air is split by two thin bright straight lines, one from each hand to its sword, angled outward to his left and to his right; each line lies only between his hand and its sword, never ahead of the sword. His head is a tall bishop's mitre like the chess bishop piece, not a crown. He stays planted where he stands, one foot forward.
```
처음 판(`He has drawn both swords back over his shoulders and has just let go. … the air just in front of him is split by two thin bright lines where the swords left …`)에서 고친 점: ① 게임 자세(`SwordFightSkills.Motion.cs`의 `BishopKey` — 0.2초 동안 어깨 위로 젖혔다가 두 팔을 앞 · 바깥으로 휘두르며 놓음, 칼은 손에서 바로 앞 · 바깥으로 돌며 나감)에 맞춰 "칼이 막 손을 떠나 팔뚝 길이만큼" 떨어진 순간으로 못 박음(들고 있는지 놓았는지 섞이지 않게), ② R181 · R186처럼 줄이 칼 **앞**으로 그려지지 않게 "손과 칼 사이에만", ③ R186 왼쪽 그림처럼 머리가 왕관으로 그려지지 않게 "비숍 모자, 왕관 아님".

**F5 · 날아감 (`F_03_flight.png`):**
```
Camera: 3 m behind and above the bishop, looking down the line of the throw. Both swords are mid-flight, spinning flat and very fast, flying out wide in two mirrored curves, one to each side, toward a point 5 m ahead. Each spinning sword is wrapped in a thin silver motion-blur disc and leaves a curved ribbon trail along its path, bright right behind the sword and fading to nothing over about 1.5 m. The bishop stands still with empty hands.
```
R181에 쓴 판(받은 그림 7.png): 머리말의 금지 목록에서 `arcs, `를 빼고(휜 꼬리가 막히지 않게), `he holds two identical slim steel longswords with engraved blades, one in each hand`를 `he has just thrown his two identical slim steel longswords with engraved blades`로 바꿨다(이 순간 비숍은 빈손). 그래도 그림의 휜 줄은 칼 **앞**쪽으로 그려져 왔다(위 남은 이슈). F8도 칼이 날아오는 동안 빈손이고 휜 꼬리가 있으니 같은 두 군데를 바꾼다. R186에 쓴 판(받은 그림 8.png): 머리말의 `arcs, `를 빼고 들고 있는 문장을 `he has thrown his two identical slim steel longswords with engraved blades and they are now flying back to him`으로, 아래 F8 문장 끝에 `Each trail lies only along the path its sword has already flown, behind the sword, never ahead of it.`와 `his left hand is still empty, open and waiting`을 더함 → 꼬리가 칼 **뒤**에 남는 그림이 왔다(오른쪽). 왼쪽 그림은 비숍 머리가 왕관처럼 그려져 왔다.

**F6 · X 엇갈림 — 핵심 (`F_04_cross.png`):**
```
Camera: side three-quarter view at waist height, 4 m from where the swords meet. The two swords meet in mid-air at waist height just in front of the ebony opponent and cut across each other in a sharp X: each has swept diagonally down from high on its own side to low on the other. Two crescent-shaped slash streaks of silver light hang in the air along those two diagonals, crisp and thin at their edges with a hot white core, forming a clean X about 2.5 m across. Exactly where the blades cross, a tiny intense white star glint with short thin rays. A faint ripple of disturbed air spreads from the X toward the opponent.
```
같은 순간 플레이어 시점(`F_04_cross_player.png`): 첫 문장을 `Camera: behind the bishop, 3 m back and slightly above, looking at the X 5 m ahead.`로 바꾼다.

**F7 · 맞음 (`F_05_hit.png` — R178에 받은 4장 중 3번째를 씀):**
```
Camera: side view of the opponent. The ebony opponent has just been struck by the X slash and is thrown backward off his feet. Across his body a bright silver X-shaped slash mark glows for an instant on the surface of the wood, with a burst of tiny silver sparks and fine steel-bright flecks flying in the direction of the push. A few thin straight streaks of displaced air trail behind him, showing which way he is pushed.
```

**F8 · F9 · 돌아옴 · 받음 (`F_06_return.png`):**
```
Camera: wide side shot showing the bishop and the space in front of him. The two swords are flying back to him on loose, wandering, different curved paths, still spinning but slowing down, each leaving a thinner, fainter silver trail that wavers. One sword is just dropping into his open right hand, hilt first: a small soft silver glint where the hilt meets the glove.
```

**E1 · 밧줄을 던질 때 (`E_reach.png`):**
```
At the edge of the chessboard an ivory ally piece is falling off into the dark below. The bishop, standing at the brass edge, has thrown a real three-strand hemp rope that loops around the falling ally's waist. A soft silver glow outlines the rope's loop and a faint silver halo surrounds the falling ally, marking them as saved.
```

**E2 · 밧줄이 팽팽해질 때 (`E_catch.png`):**
```
The moment the hemp rope snaps taut around the falling ally's waist: a short bright silver flash at the knot, a burst of small silver motes spraying outward from it, and a silver shimmer running along the taut rope back to the bishop's hands.
```

**E3 · 내려놓을 때 (`E_set.png`):**
```
The rescued ivory ally is set down safely on the board just inside the brass edge, the rope going slack. On the board under the ally a soft silver circle of light spreads outward and fades, and gentle silver motes rise around the ally's feet.
```

**(선택) 4칸 연속 그림:** 순간별 문장 뒤에 덧붙인다.
```
Show it as a 4-panel horizontal storyboard of the same shot at 0.05 s, 0.15 s, 0.3 s and 0.6 s after the moment, same camera in every panel.
```

---

## 나이트 (소드 파이트) — F5 포크 · E1 웅크림 · E2 뛰어나감 · E3 공중제비 · E4 차기 검토 중 (R179 · R182 · R189 · R192 · R195)

**사용자 10-28: "나이트 작업 시작하자"**(URP VFX 채팅이 비숍 F6을 만드는 동안 다른 채팅에서). 비숍처럼 **동작을 코드에서 읽어 층 계획 · 레퍼런스 프롬프트부터** 썼다(R176). 그 뒤 **"룩 고유 색보다 진한 색으로" · "F 스킬 나왔어"**(R177): 색을 정하고 F 그림 4장을 받았다. 그 뒤 **"적 칸은 빨강으로 하고 F5 만들어"**(R179): 적 칸을 빨강으로 정하고 F5 포크를 만들었다(아래 "F5 포크 — 만든 값"). 그 뒤 **"나이트 E3 공중제비를 vfx-layer 순서대로 만들어줘"**(R182): E3 공중제비를 만들었다(아래 "E3 공중제비 — 만든 값"). 그 뒤 **"나이트 E4 차기를 vfx-layer 순서대로 만들어줘"**(R189): 물음에 "E4 줄기를 적게" · "맞았을 때만"으로 답해 E4 차기를 만들었다(아래 "E4 차기 — 만든 값"). 그 뒤 **"나이트 E2 뛰어나감을 vfx-layer 순서대로 만들어줘"**(R192)로 E2 뛰어나감을 만들었다(아래 "E2 뛰어나감 — 만든 값"). 그 뒤 **"나이트 E1 웅크림을 vfx-layer 순서대로 만들어줘"**(R195)로 E1 웅크림을 만들었다(아래 "E1 웅크림 — 만든 값"). 다음 층은 같은 빌더에 `Build<층>()`으로 더한다.

- **색 (R177):** 룩 고유색 베이지 `#D3B596`(룩 F VFX 색)과 같은 계열(색상 약 30°)을 진하게 한 **진한 황갈색 `#A8703D`**(몸통 · 번짐), 밝은 쪽 `#E2B47E`, 어두운 쪽 `#5E3B1C`, 심 = 따뜻한 흰색 `#FFF1DD`. 킹 · 퀸 금색(약 40°)보다 붉고 비숍 은색과는 확실히 다르다. 받은 은색 그림을 이 색으로 칠해 본 미리보기 = `VFXCaptures/knight/color_preview.png`(git 제외). 게임 쪽 `kit.knight`(지금 `BSilver`: 실사 조준 칸 · 잔상 등)는 **아직 안 바꿈**(R179에 물음, 답 대기).
- **적이 있는 칸:** 지금 실사 표시 호박색 `ForkFoe` (1, 0.62, 0.26) = `#FF9E42`는 새 나이트 색과 색상이 같아(약 29°) 구별이 안 된다 → **빨강 `#C24A41`**(퀸 오브 더 힐 `BEnemy`와 같은 적 색)으로 정함(사용자 10-28 "적 칸은 빨강으로", R179). F5와 실사 조준 표시(`RealKnightMarks`의 `ForkFoe`, 지금 (0.761, 0.29, 0.255))를 같은 색으로 바꿨다. 먼지 퍼프 · 맞을 때 고리 금지. 잔상(`RealGhost`)은 지금 코드 그대로(VFX 층에 안 넣음).
- **레퍼런스 (R177):** `F_00_storyboard.png`(6칸) · `F_01_leap.png` · `F_02_apex.png` · `F_04_fork.png`. 사용자가 채팅에 붙인 그림의 화면 캡처라 작다(약 270×320). 모두 **은색 프롬프트로 뽑은 것**이라 모양 · 타이밍만 따르고 색은 위 새 색. 아직 없음: `F_03_dive` · `F_05_hit`(연속 그림 4 · 6칸으로 대신), `F_04_fork_player`, `E_00_storyboard`. E 그림 5장은 R179에 받음(아래 "E — 받은 그림에서 본 것").
- 지금 실사 연출(`SwordFightSkillFx.RealKnight.cs` · `.RealEdgeRBK.cs`)은 **그대로 두고 그 위에 얹는다**(킹과 같은 방식). 겹쳐서 지저분하면 그 층을 확인할 때 실사 쪽을 줄일지 묻는다.
- R179에 만듦: 빌더 `Assets/Scripts/VFXEditor/KnightVfxBuilder.cs`(메뉴 `ChessFight/VFX/Knight/Build`, Play 중에는 꺼짐, 지금 F5 · E1 · E2 · E3 · E4 — 그 층만 다시 만드는 메뉴 `Build E1 (coil only)`는 R195, `Build E2 (spring only)`는 R192, `Build E3 (flip only)`는 R182, `Build E4 (kick only)`는 R189), 프리팹 `Prefabs/Knight/05_Knight_F_Fork` · `10_Knight_E_Coil`(R195) · `11_Knight_E_Spring`(R192) · `12_Knight_E_Flip`(R182) · `13_Knight_E_Kick`(R189), `Resources/VfxSets/Knight`(`F_Fork` · `E_Coil` · `E_Spring` · `E_Flip` · `E_Kick`), 게임 연결 `SwordFightSkillFx.KnightVfx.cs`(`KnightVfxFork` · `KnightVfxCoil` · `KnightVfxSpring` · `KnightVfxFlip` · `KnightVfxKick`, 끄기 `KnightVfxOn = false`).

**F 포크 강하 — 동작 · 타이밍 (코드 기준, `SwordFightSkillParams`):**

| 단계 | 시간 | 일어나는 일 |
|---|---|---|
| 조준 | F 누름 ~ 좌클릭 | 화면 가운데가 가리키는 바닥(1.5~6 m)이 착지 칸. 착지 칸 + **L자 여덟 칸**(한 칸 `knightCell` 1.25 m, 두 칸 가고 한 칸 꺾은 자리 — 경기장 바닥 칸 2 m와 크기가 다름)이 빛 테두리로 보이고, 적이 있는 칸은 빨간 굵은 테두리 + 바닥 빛(`RealKnightMarks`, R179 전엔 호박색), 비행 길은 옅은 선. 우클릭 · F 취소 |
| 웅크림 | 0.07초 (`knightGather`) | 살짝 내려앉아 앞으로 숙임 |
| 날아오름 | 0.3초 (`knightRise`) | 떠나는 순간 신호. 길의 72% 지점 위 **3.2 m** 꼭대기로, 몸을 뒤로 젖힘(−18°), 칼은 위로 |
| 꼭대기 | 0.14초 (`knightHover`) | 거의 멈춰 앞으로 고꾸라짐(−18° → 38°), 왼손이 착지 칸을 가리킴. L자 빛 선이 칸마다 그어짐(지금 코드) |
| 내리꽂기 | 0.16초 (`knightDive`) | 착지 칸으로 거의 수직, 점점 빨라짐(k²). 칼을 앞 아래로 내리찍음. 지금 코드: 반투명 잔상 · 칼 흐림 |
| 착지 · 포크 | 그 순간 | 착지 칸 반경 0.85 m 적 = **내리찍기**(1.6 m 밀림), L자 칸 반경 0.8 m 적 = **포크**(2.4 m 밀림). 지금 코드: 나뭇조각 · 멈춤 6프레임 · 큰 흔들림 |
| 후딜 | 0.35초 (`knightRecovery`) | 칸 위에 숙인 채 버팀 → 낮은 자세 → 풂 |

예: 4 m 앞 칸 → 웅크림 0~0.07초, 날아오름 0.07~0.37, 꼭대기 0.37~0.51, 내리꽂기 0.51~0.67(착지 · 포크 0.67초), 후딜 ~1.02초.
게임 연결 이벤트: `SfFxKind.KnightLeap`(뜀: 출발 바닥 · 방향 · 거리) · `KnightLand`(착지 칸 · 방향 · 칸 크기) · `KnightHit`(맞은 적마다). **`KnightLand`에 F5(`F_Fork`)**(R179, 실사 착지 `RealKnightLand` 바로 뒤, 뿌리 = 착지 칸 아래 바닥)가 붙음. **꼭대기 · 내리꽂기 시작에는 신호가 없다** → `RealKnightLeap`이 이미 재는 그 순간(`top` · `dive`)에서 부른다.

**F 층 계획 (초안):**

| 층 | 순간 | 모양 (초안) | 프리팹 | 상태 | 레퍼런스 |
|---|---|---|---|---|---|
| F1 | 뜀 (`KnightLeap`) | 출발 자리에 **편자 모양 빛 자국**(약 0.5 m, 트인 쪽이 가는 방향)이 번쩍 찍혔다 0.5초에 식음 + 발밑에서 아래 · 뒤로 눌리는 짧은 공기 줄 | `01_Knight_F_Leap` | 대기 | `F_01_leap.png` (받음) |
| F2 | 날아오름 0.3초 | 몸을 따라 휘어 오르는 **가는 빛 꼬리**(심 흰색, 가장자리 날카로움, 1 m쯤에서 사라짐) | `02_Knight_F_Rise` | 대기 | `F_01_leap.png` (받음) |
| F3 | 꼭대기 0.14초 | 몸에 작은 반짝임 + 꼭대기에서 착지 칸까지 **가는 조준 빛줄기**가 내리그어짐, L자 칸들이 살짝 밝아짐 | `03_Knight_F_Apex` | 대기 | `F_02_apex.png` (받음) |
| F4 | 내리꽂기 0.16초 | 꼭대기 → 칸으로 **곧은 빛 창 자국**(몸 뒤 굵고 짧은 줄 + 가는 속도선 몇 줄), 착지 뒤 0.15초 남았다 사라짐 | `04_Knight_F_Dive` | 대기 | `F_03_dive.png` (아직 — 연속 그림 4칸) |
| **F5** | **착지 · 포크** | **착지 칸이 네모 판으로 번쩍(둥근 고리 아님) → 빛이 바닥을 따라 앞 · 뒤 · 왼 · 오른 네 갈래로 두 칸 달린 뒤 T자로 갈라져 L자 여덟 칸으로(약 0.12초) → 칸마다 네모로 번쩍 + 반투명 빛 기둥(약 1 m)**, 적이 있는 칸이 가장 밝게(빨간 굵은 테두리), 0.6초에 걸쳐 식음 — 이 스킬의 얼굴 | `05_Knight_F_Fork` | **검토**(R179) | `F_04_fork.png` (받음) |
| F6 | 맞은 적마다 (`KnightHit`) | 몸에 **강철빛 번쩍 + 비스듬히 그어진 빛 줄** + 밀리는 쪽으로 날카로운 빛 조각, **고리 없음**(지금 코드의 베인 자국 `CutMark` · 나뭇조각은 그대로) | `06_Knight_F_Hit` | 대기 | `F_05_hit.png` (아직 — 연속 그림 6칸) |

- 추천 순서: **F5(포크) → F6(맞음) → F4(내리꽂기) → F1 → F2 · F3**(얼굴이 되는 순간부터).
- 조준 칸 · 적 칸 표시는 지금 실사 코드 그대로 둔다(사용자 10-26 "더 명확하게"로 고친 것, 색만 빨강으로 바꿈 — R179). VFX로 바꾸고 싶으면 나중에 F0으로.
- F5: 땅이 없는 칸(`KnightSquareOn` false)은 안 그리고, 칸 크기는 신호의 칸 크기(1.25 m)를 따른다 → 새 재생 부품이 칸마다 켜기 · 적 있음을 받는다(R179: `VfxForkGrid.Configure(칸 크기, 켜기[8], 적[8], 착지 칸에 적)`, 아래).
- F2 · F4 꼬리: `SwordFightSkills.KnightFlight`(모든 PC에서 같은 길)로 미리 계산한 길을 따라 그리면 캡처에도 나온다(F2 때 정함).

**F5 — 받은 그림에서 본 것 (R177, 만들 때 기준):**
- 판: 착지 칸 = 칸 크기(1.25 m) 네모 판, 밝은 테두리 + 옅게 채운 속. 둥근 것 없음.
- 길: 판의 네 변 가운데에서 **앞 · 뒤 · 왼 · 오른 네 갈래**가 두 칸(2.5 m) 곧게 뻗고, 끝에서 **T자로 갈라져** 양옆 한 칸 → 여덟 칸(L자 여덟 길이 처음 두 칸을 둘씩 같이 씀). 선은 가늘고 날카롭게(약 4~6 cm), 바닥에 붙어 있음.
- 칸: 끝 칸마다 네모 테두리 + 옅게 채운 속 + **바닥에서 솟는 반투명 네모 빛 기둥**(약 1 m, 아래가 가장 밝고 위로 사라짐).
- 적 칸: 가장 밝고 테두리만 다른 색(그림 = 호박색 → 새 색과 겹쳐 빨강 추천).
- 시간(안): 0초 판 번쩍(§6 섬광) → 0~0.08초 네 갈래가 두 칸 달림 → 0.08~0.12초 T자로 갈라짐 → 칸마다 번쩍 + 기둥이 솟음(~0.2초) → 0.6초에 걸쳐 식음(기둥 먼저, 선 · 판 나중).
- 만들 것(안): 판 · 선 · 칸 · 기둥을 그리는 새 재생 부품 하나(`IVfxSeekable`, 칸마다 켜기 · 적 있음을 받음) + 새 가산 셰이더 하나, 빌더 `KnightVfxBuilder.cs`(메뉴 `ChessFight/VFX/Knight/Build`), `Resources/VfxSets/Knight`, 게임 연결 `SwordFightSkillFx.KnightVfx.cs`(`KnightLand` → 키 `F_Fork`). 공용 부품 기본값은 안 고침.

**F5 포크 — 만든 값 (R179, 검토 중):** 프리팹 `05_Knight_F_Fork`(뿌리 = 착지 칸 가운데 바닥, +Z = 나이트가 가는 방향, 앵커 Feet, 길이 0.9초), 키 `F_Fork`. 게임은 `KnightLand` 때 실사 착지(`RealKnightLand`) 바로 뒤에 착지 칸 아래 바닥(`FloorUnder`)에 내고, `VfxForkGrid.Configure`로 칸 크기(신호의 1.25 m) · 땅 있는 칸(`KnightSquareOn`) · 적 있는 칸(칸 반경 0.8 m 안 적 = 포크 판정과 같음) · 착지 칸에 적(반경 0.85 m = 내리찍기)을 넘긴다. 프리팹에는 앞 오른쪽 칸(`KnightMoves` 0번)에 적이 있는 것으로 저장했다(작업대 · 캡처에 빨간 칸이 보이게, 게임은 자기 값으로 덮음). 캡처 `VFXCaptures/knight/F5/`(0 · 0.04 · 0.1 · 0.16 · 0.3 · 0.55초, `Tone.Game`, 표준 캡처 카메라) + 위에서 넓게 본 `VFXCaptures/knight/F5_overview/`(같은 시간, 여덟 칸이 다 보이게, 캡처 도구는 안 고치고 MCP 코드로 찍음).

| 부품 | 무엇 | 값 (인스펙터, 빌드 때 그대로 남음) |
|---|---|---|
| `F5 Fork` | 판 · 선 · T · 여덟 칸 · 빛 기둥을 메시 하나로: 재생 `VfxForkGrid`(`IVfxSeekable`), 메시 `SM_Knight_Fork`(바닥 판 한 장 + 칸마다 기둥 사각형), 재질 두 개(바닥 `M_Knight_ForkGrid` · 기둥 `M_Knight_ForkShaft`). 물체 크기 = 칸 크기(한 칸 = 1 단위) | 네 갈래 0.08초 · T 0.04초(→ 0.12초에 칸이 켜짐) · 번쩍이 가라앉음 0.1초 · 기둥 솟음 0.1초 · 기둥은 0.6초까지 사라짐 · 판 · 선 · 칸은 0.3 → 0.8초에 사라짐 · 판 ×5 → ×1.8 · 선 ×2 · 달리는 머리 ×3 · 칸 ×4 → ×2 · 적 칸 ×1.5 · 기둥 ×1.5 |
| (재질) `M_Knight_ForkGrid` | 셰이더 `Fork Grid Additive`(새, 바닥). 처음 만들 때만 색을 넣음(그 뒤는 사용자 값) | 번짐 `#A8703D` · 심 `#FFF1DD` ×3 · 적 칸 번짐 `#C24A41` · 심 `#FFD9CF` · 선 폭 5 cm · 칸 테두리 5 cm(적 칸 ×1.8 = 9 cm) · 번짐 거리 5 cm · 속 채움 12%(테두리 쪽 20 cm가 더 밝음) · 판 = 칸의 90%(1.125 m) · 끝 칸 = 칸의 62%(0.775 m) · 달리는 머리 빛 12 cm |
| (재질) `M_Knight_ForkShaft` | 셰이더 `Fork Shaft Additive`(새, 기둥: 정점 셰이더가 칸 가운데에 세워 카메라 쪽으로 돌림 → 캡처 카메라에도 맞음) | 번짐 `#A8703D` · 심 `#FFF1DD` ×1 · 폭 0.6 m · 높이 1 m(적 칸 ×1.25) · 위로 사라짐 1.8 · 옆 흐림 70% · 심 폭 35% · 바닥 밝은 띠 6 cm ×1.5 · 몸에 닿는 곳 깊이 흐림 0.05 m |

**레퍼런스(`F_04_fork.png`)와 다른 점 (R179 캡처 — 사용자 10-28 "F5는 일단 그대로 두고 확인은 나중에 할게": 값은 그대로 두고, 아래 고칠 값은 사용자가 확인할 때 같이 본다):** 모양(네모 판 · 네 갈래 · T · L자 여덟 칸 · 기둥)과 순서는 맞다.
1. **색이 하얗게 바램**: 적 칸 테두리의 가장 밝은 곳 `#FEF7F3`(거의 흰색, 빨강은 흐린 번짐에만), 선 `#FEFDFB`. 심이 ×3 · ×2 · ×1.5로 겹쳐 6~9배가 되어 ACES가 흰색으로 만든다. → 바닥 재질 `_CoreBoost` 3 → 1.2, 적 칸 심 `_FoeCoreColor` (1, 0.851, 0.812) → (1, 0.25, 0.2), 적 칸 테두리 `_FoeEdge` 1.8 → 2.4, 재생 `foeBoost` 1.5 → 1(밝기 대신 굵기 · 색으로 구별).
2. **빛 기둥이 가는 불꽃처럼 보임**(가운데 심이 좁고 위로 빨리 사라짐 — 불 금지와 부딪힘), 위에서 보면 거의 안 보임. 레퍼런스는 넓고 부드러운 네모 빛기둥. → 기둥 재질 `_Width` 0.6 → 0.75 m, `_Soft` 0.7 → 0.35, `_CoreWidth` 0.35 → 0.8, `_Height` 1 → 1.3 m, `_Falloff` 1.8 → 1.1, 재생 `shaftLight` 1.5 → 2.
3. **판 · 칸 속이 어둡고 대각선 주름**(네모 거리 함수로 "가장자리 쪽 밝아짐"을 만들어 피라미드처럼 보임). 레퍼런스는 속까지 밝은 빛 판. → 셰이더의 가장자리 쪽 밝아짐을 빼서 속을 평평하게, `_Fill` 0.12 → 0.3.

F5 남은 이슈:
- 바닥 판은 착지 칸 높이의 한 장이다. 높이가 다른 칸(단 · 계단)에서는 칸이 뜨거나 묻힐 수 있다 → 스킬 시험 씬에서 보고, 필요하면 칸마다 높이를 받게.
- 실사 착지 연출(나뭇조각 · 멈춤 · 흔들림)과 실사 조준 칸(`RealKnightMarks`, 착지하면 꺼짐)은 그대로다. 겹쳐서 지저분하면 실사 쪽을 줄일지 묻는다.
- 캡처 무대 바닥은 어두운 회색이라 게임의 나무 체스판보다 빛이 진하게 보인다 → 체스판에서 너무 밝으면 `squareLight` · `lineLight` · 재질 `_CoreBoost`부터 낮춘다.

**E 벼랑 끝 역전 — 동작 · 타이밍 (코드 기준, `SwordFightEdgeParams`):**

| 단계 | 시간 | 일어나는 일 |
|---|---|---|
| 조건 | E 누름 | 가장자리를 등지고(1.5 m 안) 서 있고 앞 3 m 안에 적. 떨어지기 시작한 첫 0.6초 안에도 됨(그 자리 공중에 멈춤). 지금 코드: 착지할 자리(적 뒤 0.9 m)에 옅은 빛 원 0.45 m |
| 웅크림 | 0.35초 (`knightCrouch`) | 신호 `KnightCrouch`(착지 자리). 다리에 몸을 싣고 앞으로 숙여 적을 노려봄, 칼은 어깨 뒤 위로 |
| 뛰어나감 | 비행의 첫 30% (0.24초) | 신호 `KnightFlip`(출발 바닥 · 방향). 벼랑 밖 0.7 m · 위 0.95 m로 뒤로 젖히며 튀어 나감(−24°). 지금 코드: 놋쇠 부스러기 · 화면 당김 |
| 멈칫 | 0.24초 지점 | 벼랑 밖 공중에서 한 박자 + 화면이 나이트를 돌아봄(38°) |
| 공중제비 | 0.24~0.8초 (`knightFlip`) | 적 머리 위(발판 위 1.45 m)를 앞으로 한 바퀴(360° + 40°) 돌며 넘어 적 뒤에 내려옴. 지금 코드: 머리 · 뒷발이 번갈아 남기는 공기 굴절 바람, 반투명 잔상, 0.64초부터 뒷발 흐림 |
| 뒷발차기 | 0.8초 (착지 순간) | 신호 `KnightKick`(착지 자리, 차는 방향) + 적이 1.9 m 안이면 `KnightKickHit`: 적이 **벼랑 쪽으로 3.5 m** 날아감. 지금 코드: 편자 불꽃(원뿔 30 + 12개) · 뜨거운 빛 · 색수차 화면 충격 · 나뭇조각 |
| 후딜 | 0.35초 | 다 뻗은 차기 → 뒷다리로 무게가 내려앉음 |

예: 웅크림 0~0.35초, 뛰어나감 0.35~0.59, 멈칫 0.59, 공중제비 0.59~1.15, 뒷발차기 1.15초, 후딜 ~1.5초.

**E 층 계획 (초안):**

| 층 | 순간 | 모양 (초안) | 프리팹 | 상태 | 레퍼런스 |
|---|---|---|---|---|---|
| E1 | 웅크림 0.35초 (`KnightCrouch`) | 두 발 가운데 바닥으로 **가는 황갈색 빛 실 약 13가닥**이 둘레(반지름 0.28 m 원, 바닥 위 0.32 m)에서 빨려 들고(0.3초 동안 점점 많이), 발밑 **바닥 빛**(황갈색 번짐 0.6 m + 따뜻한 흰 심 0.16 m)이 0.4초 동안 커지며 차오름, 고리 없음. 웅크림이 끊기면 바로 지움 | `10_Knight_E_Coil` | **검토**(R195) | `E_01_coil.png` (받음) |
| E2 | 뛰어나감 (`KnightFlip`) | 발굽이 차고 나가는 놋쇠 테(테에서 0.6 m보다 안쪽이면 발굽이 떠나는 판 위)에 **짧은 따뜻한 흰 별 섬광**(테를 따라 긴 가로줄 + 십자, 0.55 m → 0.3 m, 0.08초) + 벼랑 밖 아래로 부채처럼 퍼지는 **가는 곧은 황갈색 줄 4~5가닥**, 고리 없음. 실사 놋쇠 부스러기 · 공중제비 공기 굴절은 그대로, 그 위에 | `11_Knight_E_Spring` | **검토**(R192, 값 그대로 · 확인은 나중에) | `E_02_spring.png` (받음) |
| **E3** | **공중제비** | **뒷발굽이 그리는 긴 초승달 빛 궤적**(벼랑 밖 → 적 머리 위 → 적 뒤, 심 흰색 · 가장자리 날카로움, 발굽 쪽이 가장 밝고 꼬리부터 사라짐) — 이 스킬의 얼굴. 지금 코드의 공기 굴절 바람 · 잔상은 그대로 | `12_Knight_E_Flip` | **검토**(R182, 1·2번 고침) | `E_03_flip.png` (받음) |
| E4 | 뒷발차기가 맞힐 때 (`KnightKickHit`만) | 발굽이 닿는 몸 위에 **뜨거운 흰 별빛 섬광**(0.75 m, 0.12초) + 차는 쪽으로 **긴 곧은 황갈색 빛줄기 6~8개**, 고리 없음. 지금 편자 불꽃 · 색수차(사용자 10-26에 고름)는 그대로, 그 위에. 헛발질(`KnightKick`)에는 안 나옴(사용자 10-28 "E4 줄기를 적게" · "맞았을 때만") | `13_Knight_E_Kick` | **검토**(R189, 값 그대로 · 확인은 나중에) | `E_04_kick.png` (받음) |
| E5 | 날아가는 적 | 벼랑 쪽으로 날아가는 적 몸 뒤 짧은 빛 꼬리(0.4초) | `14_Knight_E_Launch` | 대기 | `E_05_launch.png` (받음) |

- 추천 순서: **E3(공중제비) → E4(차기) → E2 → E1 → E5**.
- E3 궤적(R182에 정함): 처음 생각한 `FlipPath`로 미리 만든 휜 리본은 쓰지 않았다. 그 길의 출발점과 넘는 적 자리(`flipFrom` · `flipOver`)가 다른 PC로 보내지지 않아 화면마다 길이 달라지기 때문이다. 대신 **실제 두 뒷발굽의 가운데를 프레임마다 따라 그린다**(모든 화면이 같은 발굽을 움직인다). 작업대 · 캡처용으로는 빌더가 보통 길을 프리팹에 저장한다.
- 착지 자리 표시(옅은 원)는 지금 코드 그대로. 예전에 편자 → 원으로 바뀐 적이 있어, 바꿀지는 레퍼런스를 보고 정함(받은 E 그림에는 착지 표시가 없음 → 지금 그대로).

**E — 받은 그림에서 본 것 (R179, 사용자 "그동안 나이트 E 키 보고 있어", 만들 때 기준):** 다섯 장 모두 흰색 · 푸른 은색으로 나와(정한 진한 황갈색이 아님) F처럼 **모양 · 타이밍만** 따르고 색은 황갈색 `#A8703D` + 따뜻한 흰 심 `#FFF1DD`. 채팅에 붙인 그림이라 작다(약 270×330).
- E1 (`E_01_coil.png`, 받은 순서 2번째): 두 발굽 따로가 아니라 **다리 사이 바닥으로 모이는 한 덩이 빛** — 아래로 좁아지는 가는 빛 실 다발 + 발밑 바닥 빛. → R195: 한 덩이 바닥 빛 + 모여드는 실 약 13가닥으로 만듦(아래 "E1 웅크림 — 만든 값", 다발이 성긴 것은 다른 점 1번).
- E2 (`E_02_spring.png`, 3번째): 놋쇠 테 위 **빛살 4개 별 섬광**(가로 빛살이 김) + 벼랑 밖 아래로 부채처럼 퍼지는 **가는 곧은 줄 5~6가닥**(계획의 두세 가닥보다 많음 → 4~5가닥으로). → R192: 줄 4~5가닥으로 만듦, 휜 흐린 공기 줄은 따로 안 만듦(아래 "E2 뛰어나감 — 만든 값"의 다른 점 3번).
- E3 (`E_03_flip.png`, 4번째): 거꾸로 선 나이트 둘레 **큰 빛 호**(심 흰색 · 넓은 번짐). 그림은 한 바퀴 반을 돌아 **고리처럼 닫히고 두 겹** → 고리 금지라 발굽이 실제로 지나간 길을 따라 **초승달 한 줄**, 꼬리부터 사라져 닫히지 않게.
- E4 (`E_04_kick.png`, 5번째): 맞는 곳에 **긴 빛살 별 섬광**(그림은 몸보다 큼 → 0.6~0.8 m로) + 차는 쪽으로 **원뿔 모양 가는 빛줄기 15~20개** + 작은 불꽃. 지금 실사 편자 불꽃(원뿔 30 + 12개)과 모양이 거의 같아 겹치면 너무 많다 → E4 때 빛줄기 수를 줄이거나 실사 불꽃을 줄일지 묻는다. → R189에 물음: 사용자 "E4 줄기를 적게"(실사 불꽃은 그대로, 줄기는 6~8개만) · "맞았을 때만"(헛발질은 지금 그대로).
- E5 (`E_05_launch.png`, 1번째): 날아가는 적을 꿰는 **길고 곧은 가는 빛 꼬리**(그림은 2~3 m) + 맞은 곳 섬광 · 불꽃. 계획은 짧은 꼬리 0.4초 → 1.5 m 안팎으로 시작.
- 지금 실사 코드에 이미 있는 것(겹침 주의): 공중제비 — 공기 굴절 바람 · 반투명 잔상 · 뒷다리 흐림(`HoofTrails`), 차기 — 편자 불꽃 · 색수차 · 나뭇조각 · 작은 섬광(`ClashFlash`).

**E3 공중제비 — 만든 값 (R182, 1·2번 고침, 검토 중):** 프리팹 `12_Knight_E_Flip`(뿌리 = 뛰어나가는 자리의 바닥 = `KnightFlip` 신호 자리, +Z = 나이트가 가는 방향, 앵커 Feet, 길이 1.4초), 키 `E_Flip`. 게임은 `KnightFlip` 때 실사 공중제비(`RealKnightFlip`) 바로 뒤에 낸다. 비행의 42%(`KnightTuckAt`, 0.336초 — 몸이 돌기 시작할 때)부터 착지 뒤 0.09초(뒷발차기로 뒷다리가 들릴 때)까지 **두 뒷발굽(`FootL` · `FootR`)의 가운데를 프레임마다 궤적에 더한다**(앞 점과 1.5 cm보다 가까우면 건너뜀). 공중에서 맞으면 거기서 멈추고, 그린 데까지만 사라진다. 다른 PC는 신호가 단계보다 먼저 올 수 있어서, 웅크림 단계면 0.25초까지 기다린다. 프리팹에는 작업대 · 캡처용 **보통 길**을 빌드할 때마다 새로 저장한다: 벼랑에서 0.8 m 안에 선 나이트가 1.5 m 앞 적을 넘어 적 뒤 0.9 m에 착지, 발굽은 엉덩이 아래 0.2 m, `FlipPath` · `KnightFlipTurn`을 따라 점 37개(0.336 → 0.8초), 가장 높은 곳 1.46 m, 앞뒤로 −1.17 → 2.27 m. 캡처 `VFXCaptures/knight/E3/`(표준 카메라 = 뒤에서) · `E3_side/`(`turn` 90 = 옆에서, +Z가 오른쪽), 0.42 · 0.5 · 0.58 · 0.66 · 0.8 · 0.95 · 1.1초, `Tone.Game`. 처음 값의 캡처는 `E3_before/` · `E3_side_before/`, 전 · 후 비교 `E3_compare.png`(아래 1차 수정).

| 부품 | 무엇 | 값 (인스펙터, 빌드 때 그대로 남음) |
|---|---|---|
| `E3 Arc` | 빛 띠 하나. 재생 `VfxPathRibbon`(새, `IVfxSeekable`): 점 · 시각 목록을 부드러운 곡선(Catmull-Rom)으로 이어 메시를 만든다. 게임은 `Begin` · `Trace`로 길을 깐다. 재질 `M_Knight_FlipArc` | 점 사이 곡선 4점 · 점 최소 간격 1.5 cm · 폭 **0.18 m**(발굽 쪽, 처음 0.14) · 꼬리로 좁아짐 **0.4**(처음 0.8) · 한 자리가 남는 시간 0.55초 · 밝기 ×1.5 (폭 · 좁아짐은 빌더도 이 값으로 만듦. 길 자체는 빌드가 정함 — 다시 빌드하면 보통 길로 돌아감) |
| (재질) `M_Knight_FlipArc` | 셰이더 `Path Ribbon Additive`(새). 정점 셰이더가 길 양옆으로 카메라를 향해 펼침 → 어느 카메라에서도 띠로 보임. 아직 안 그린 앞쪽은 안 보임. 처음 만들 때만 색과 굵게 적은 값을 넣음(그 뒤는 사용자 값. 셰이더 기본값은 처음 값 그대로) | 번짐 `#A8703D` · 심 `#FFF1DD` ×**1.2**(처음 2.5) · 심 폭 **15%**(처음 30%) · 번짐 꺾임 **0.5**(처음 0.8) · 가장자리 흐림 30% · 머리 더 밝게 **+1.0**(처음 +1.5, 0.06초) · 머리 둥글게 0.025초 · 머리 경계 0.01초 · 꼬리 쪽 어두워짐 1.5 · 몸에 닿는 곳 흐림 0.05 m |

**레퍼런스(`E_03_flip.png`)와 다른 점 (R182 캡처):** 고리로 닫히지 않는 초승달 한 줄, 발굽 쪽이 가장 밝고 꼬리부터 사라지는 것은 맞다(옆에서 0.66 → 0.8 → 1.1초). 레퍼런스의 둥근 고리보다 길고 납작한 호인 것은 계획대로다(벼랑 밖 → 적 머리 위 → 적 뒤).
1. **색이 하얗게 바램**: 띠 가운데가 꼬리까지 거의 흰색(`#EEEBE7` ~ `#FEFEFE`, 채도 0.1 아래)이고 황갈색은 옅은 번짐(`#877052`)에만 있다. 심 ×2.5 · 밝기 ×1.5 · 머리 ×2.5가 겹쳐 ACES가 흰색으로 만든다(F5 1번과 같은 까닭). → 재질 `_CoreBoost` 2.5 → 1.2, `_CoreWidth` 0.3 → 0.15, `_GlowFalloff` 0.8 → 0.5(황갈색 몸이 가장자리까지 참), `_HeadGlow` 1.5 → 1.0. **→ 1차 수정에 넣음.**
2. **꼬리 쪽이 너무 빨리 가늘어짐**: 착지 때(0.8초) 발굽 쪽은 약 12 cm인데 꼭대기는 4 ~ 5 cm, 꼬리는 2.4 cm다. 레퍼런스는 호 대부분이 굵은 띠다. → `taper` 0.8 → 0.4, `width` 0.14 → 0.18 m. **→ 1차 수정에 넣음.**
3. **뒤에서 보면 세로 빛기둥**: 호가 카메라와 같은 면에 있어 표준 캡처(뒤에서 = 플레이어 시점)에서는 초승달이 아니라 위아래 막대로 보인다(F5 빛 기둥과 헷갈림). 게임에서는 꼭대기에서 실사 `Orbit`이 화면을 38° 돌려 조금은 휘어 보일 것이다. → 새 값 `sideBow`(지금 없음) 0.25 m: 그린 길을 나이트 오른쪽으로 활처럼 휘게(가운데에서 가장 크게). 대신 빛이 발굽에서 최대 25 cm 떨어진다. 싫으면 그대로. **→ 사용자: 게임에서 본 뒤 정함(1차 수정에서 안 고침).**

**E3 1차 수정 (10-28, 사용자 "E3 1번(색)과 2번(굵기)을 추천 값으로 고쳐줘. 3번은 게임에서 본 뒤 정할게"):** 재질 `M_Knight_FlipArc`의 `_CoreBoost` 1.2 · `_CoreWidth` 0.15 · `_GlowFalloff` 0.5 · `_HeadGlow` 1.0, 프리팹 `E3 Arc`의 `width` 0.18 m · `taper` 0.4를 에셋에 바로 넣고(`Keep`이 프리팹 값을 남기므로) E3만 다시 빌드했다. 빌더도 같은 값으로 만든다: 처음 만드는 재질에 값도 넣는 `Material` 겹 함수를 더했고, 띠 값은 `Keep` 앞에 정한다(F5 줄 · 공용 부품 `VfxPathRibbon` · 셰이더 기본값은 그대로). 잰 값(옆 0.58 ~ 0.8초 · 뒤 0.58 · 0.8초): 번짐 색 `#877052` 근처(채도 0.28 ~ 0.39) → `#8E744F` ~ `#947955`(채도 0.43 ~ 0.48), 가장 밝은 쪽(최대의 85%가 넘는 곳) 채도 0.01 ~ 0.09 → 0.09 ~ 0.13, 착지 때(0.8초) 옆에서 본 띠 굵기 꼬리 → 발굽 2.4 ~ 18 cm → 7.3 ~ 23 cm(거의 가로로 지나는 꼭대기 5.3 ~ 7.3 → 11.7 ~ 14.2 cm). 띠 몸이 황갈색으로 보이고 꼬리까지 굵게 남는다. 가운데 흰 심 선과 발굽 쪽 머리는 아직 크림 · 흰색이다(설계한 따뜻한 흰 심) — 몸을 더 진한 황갈색으로 하려면 다음에 `intensity` 1.5 → 1.1. 뒤에서는 여전히 세로 막대(3번, 안 고침). 캡처 `E3/` · `E3_side/`(새 값), `E3_before/` · `E3_side_before/`(처음 값), 전 · 후 비교 `VFXCaptures/knight/E3_compare.png`. Unity 컴파일 · `Test-NetworkCompile.ps1` 통과.

E3 남은 이슈:
- 보통 길에는 시작(0.42 ~ 0.5초)과 착지 앞(0.7초 근처)에 작은 S자 굽이가 있다(엉덩이 길 + 회전). 게임은 실제 발굽을 따라가니 모양이 조금 다르다 → 스킬 시험 씬에서 보고, 꿈틀거리면 따라 그리기 시작을 비행의 42% → 50%로 늦추거나(`KnightTuckAt`은 동작 값이라 그대로 두고 따로) 점을 더 부드럽게.
- 게임 Play(스킬 시험 씬)와 두 PC에서는 아직 안 봤다. 실사 공기 굴절 바람 · 잔상 · 뒷다리 흐림과 겹쳐 지저분하면 그때 묻는다.
- 캡처 무대 바닥은 어두운 회색이라 나무 체스판보다 빛이 진하게 보인다.

**E4 차기 — 만든 값 (R189, 검토 중):** 프리팹 `13_Knight_E_Kick`(뿌리 = 발굽이 닿는 적 몸 위 나이트 쪽 5 cm, +Z = 적이 차이는 방향(수평), 앵커 Chest, 길이 0.5초), 키 `E_Kick`. 게임은 `KnightKickHit`(뒷발차기가 적을 맞힐 때만) 때 실사 맞음(`RealKnightKickHit`) 바로 뒤에 낸다. 닿는 곳은 실사와 같은 방법으로 찾는다(두 뒷발굽 가운데에서 적 몸으로 가슴 높이 광선, `OnBody`). 헛발질(`KnightKick`)에는 안 나온다(지금 작은 섬광 · 나뭇조각 그대로, 사용자 "맞았을 때만"). 실사 편자 불꽃(원뿔 30 + 12개) · 불빛 · 색수차 · 나뭇조각 · 작은 섬광은 그대로 두고 그 위에 얹었다(사용자 "E4 줄기를 적게"). 킹 R185의 별 섬광 부품 `VfxStarFlash` · 셰이더 `VFX_StarFlashAdd`를 기본값 그대로 다시 쓰고(킹 채팅에 알림), 재질 · 메시만 새로 만들었다. 캡처 `VFXCaptures/knight/E4/`(0° = 나이트 쪽에서 차는 방향을 봄) · `E4_side/`(90° = 옆, 차는 쪽이 오른쪽 — 레퍼런스와 같은 방향) · `E4_front/`(180° = 반대쪽, 줄기가 카메라 쪽으로), 0 · 0.02 · 0.04 · 0.07 · 0.1 · 0.14 · 0.18초, `Tone.Game`, 바닥 위 0.9 m, 몸 없이 효과만. 비교 `VFXCaptures/knight/E4_compare.png`. 첫 캡처 뒤 보여 드리기 전에 고친 것: 줄기를 늘인 입자(`Stretch`)로 그렸더니 시작(0.02 ~ 0.04초)에 꼬리가 맞은 자리보다 뒤(나이트 쪽)로 0.3 ~ 0.45 m 튀어나와 양쪽으로 쏘는 것처럼 보였다(늘인 입자는 제 자리에서 길이만큼 뒤로 그려짐) → 입자는 안 그리고 **지나간 길만큼만 자라는 꼬리(Trails)**로 그림. 고치기 전 캡처는 `E4_stretch/` · `E4_side_stretch/` · `E4_front_stretch/`.

| 부품 | 무엇 | 값 (인스펙터, 빌드 때 그대로 남음) |
|---|---|---|
| `E4 Star` | 별 섬광. 재생 `VfxStarFlash`(킹 F2와 같은 부품), 메시 `SM_Knight_Star`(1 m 사각형, 셰이더가 카메라 쪽으로 돌림), 재질 `M_Knight_KickStar` | 처음 크기 **0.75 m** → 0.4 m · **0.12초**(가로줄 시간도 0.12초 — 가로줄은 재질에서 꺼서 안 보임) · 밝기 ×2.5 · 십자 돌림 ±12° · 사는 동안 8° 돎(기본값, 십자를 꺼서 안 보임) |
| (재질) `M_Knight_KickStar` | 셰이더 `Star Flash Additive`(킹 R185, 안 고침). 처음 만들 때만 색과 굵게 적은 값을 넣음(그 뒤는 사용자 값) | 번짐 · 빛살 `#A8703D` · 심 `#FFF1DD` ×4 · 가로/세로 **1**(정사각형, 킹 3.5) · 가로줄 **0**(킹 1) · 십자 **0**(킹 1.2) · 빛살 **16개**(킹 22) · 빛살 세기 **1.5**(킹 1.8) · 번짐 0.8 · 번짐 꺾임 3 · 가장 짧은 빛살 30% · 빛살 가늘기 30 · 카메라 쪽 0.15 m · 앞에 가려도 35% 보임 |
| `E4 Streaks` | 빛줄기. Shuriken을 빌더가 프리팹에 저장. 입자는 안 그리고(`None`) 꼬리만 그림, 재질 `M_Knight_KickStreak` | 한 번에 **6 ~ 8개** · 6 ~ 9 m/s · 0.1 ~ 0.18초 · 굵기 2 ~ 3.5 cm(끝에 40%) · 중력 0 · 차는 쪽 원뿔 **24°** · 위로 12° 기울임(실사 편자 불꽃과 같은 쪽) · 반지름 3 cm. 꼬리: 입자 수명의 **50%**(길이 0.3 ~ 0.8 m) · 꼬리 끝 굵기 20% · 꼬리 끝으로 흐려짐 · 2 cm마다 점 · 입자와 같이 사라짐 |
| (재질) `M_Knight_KickStreak` | 셰이더 `Soft Glow Additive`(공용, 안 고침) | `#A8703D` × **3** · 꺾임 2 |

**레퍼런스(`E_04_kick.png`)와 다른 점 (R189 캡처 — 사용자 10-28 "E4는 그대로 두고 확인은 나중에 할게": 값은 그대로 두고, 아래 고칠 값은 사용자가 확인할 때 같이 본다):** 맞는 곳의 뜨거운 흰 별 섬광, 차는 쪽으로만 나가는 곧은 줄기, 고리 없음은 맞다. 줄기 수가 적은 것은 사용자가 고른 대로다(작은 불꽃은 실사 편자 불꽃이 맡음). 잰 값: 0초 별 가운데 `#FEFEFE`, 둘레 번짐 `#C9B291`(채도 0.29), 빛이 닿는 폭 약 0.6 m → 0.04초 0.33 m → 0.07초에 거의 꺼짐. 줄기(옆, 0.07 ~ 0.1초): 4줄이 보이고 별 밖으로 0.16 ~ 0.34 m(별 안까지 치면 약 0.4 ~ 0.6 m), 가장 밝은 곳 `#B89C72` ~ `#C5AA81`, 평균 `#614B31` ~ `#6F583C`(채도 0.46 ~ 0.51).
1. **줄기가 어둡고 짧아 보임**: 레퍼런스 줄기는 별만큼 밝고 몸 길이만큼 뻗는데, 여기 줄기는 가장 밝은 곳도 `#C5AA81`이고 별 밖으로 0.34 m까지다. → 재질 `M_Knight_KickStreak` `_Intensity` 3 → 6, 꼬리 `lifetime` 0.5 → 0.7(길이 0.4 ~ 1.1 m).
2. **나이트 쪽 · 반대쪽에서는 줄기가 거의 안 보임**: 줄기가 시선과 거의 같은 방향으로 나가서 0°에서는 짧은 줄 2개, 180°에서는 0 ~ 1개만 보인다(옆 90°에서는 4개). 게임 카메라가 어느 쪽에 있든 짧게 보일 수 있다. → 원뿔 24° → 40°(화면에서 부채처럼 퍼짐, 대신 차는 방향이 덜 또렷함).
3. **별이 부드러운 빛 덩이에 가까움**: 레퍼런스 별은 가는 빛살이 번짐보다 훨씬 멀리 뻗는다. 여기는 번짐(0.6 m)이 크고 빛살이 짧으며 0.07초에 사라진다. → 재질 `_Glow` 0.8 → 0.5, `_RayThin` 30 → 50, `_RayMin` 0.3 → 0.5, `duration` 0.12 → 0.16초.

E4 남은 이슈:
- 캡처에는 몸이 없다. 게임에서는 별이 적 몸 위(나이트 쪽 5 cm, 카메라 쪽으로 15 cm 더 당김)에 나오고, 줄기는 차는 쪽으로 나가므로 처음 0.3 m쯤은 적 몸 속을 지나 가려진다 → 게임에서 보고, 다 가려지면 원뿔을 넓히거나(2번) 줄기가 나가는 자리를 몸 옆으로.
- 실사 편자 불꽃 · 작은 섬광(`ClashFlash`)과 겹친 모습, 맞음 멈춤(`HitStop`) 동안 별이 어떻게 보이는지는 게임 Play(스킬 시험 씬)와 두 PC에서 아직 안 봤다.
- 편집 모드 캡처는 한 번에 아주 작은 메시 하나를 편집기에 남긴다(편집기를 끄면 없어짐). 게임 · 작업대에서는 효과가 없어질 때 같이 지운다.

**E2 뛰어나감 — 만든 값 (R192, 검토 중):** 프리팹 `11_Knight_E_Spring`(뿌리 = 발굽이 차고 나가는 놋쇠 테 위(판 높이), +Z = 벼랑 밖(수평), 앵커 Feet, 길이 0.4초), 키 `E_Spring`. 게임은 `KnightFlip`(웅크림 0.35초 뒤 뛰어나가는 순간) 때 실사 `RealKnightFlip` 바로 뒤, E3 `KnightVfxFlip` 앞에 낸다(`KnightVfxSpring`). 자리: 그 순간 두 뒷발굽 가운데에서 가장 가까운 벼랑 테(`SwordFightEdgeFloor.Nearest`)가 **0.6 m 안**이면 그 테 위, +Z = 테 바깥쪽(레퍼런스처럼 줄이 벼랑 밖 아래로). 더 안쪽이면(E는 가장자리 1.5 m 안에서 쓸 수 있음) 발굽이 떠나는 판 위, +Z = 나이트 뒤쪽, 효과 전체를 40° 위로 들어 줄이 판에 묻히지 않고 판 위를 스치게 한다(프리팹은 줄을 40° 아래로 기울임 — 빌더 `SpringTip`과 게임 코드 `SpringTip`이 같은 값이어야 함). 킹 R185의 `VfxStarFlash` · `VFX_StarFlashAdd`를 기본값 그대로 다시 쓰고(두 파일은 안 고침), 별 메시는 E4가 만든 `SM_Knight_Star`를 그대로 쓴다(E2 빌드는 그 메시가 없을 때만 만듦), 재질 두 개만 새로 만들었다. 실사 놋쇠 부스러기 · 몸 흔들림 · 공중제비 공기 굴절은 그대로 두고 그 위에 얹었다. 캡처 `VFXCaptures/knight/E2_side/`(90° 옆 — 레퍼런스와 같은 쪽, 벼랑 밖이 오른쪽) · `E2/`(0° 나이트 쪽에서 벼랑 밖을 봄) · `E2_front/`(180° 벼랑 밖에서 나이트 쪽을 봄), 0 · 0.015 · 0.03 · 0.05 · 0.08 · 0.12 · 0.18초, `Tone.Game`, 바닥 위 0.6 m(벼랑 밖 아래로 가는 줄이 무대 바닥에 잘리지 않게), 몸 · 체스판 없이 효과만. 비교 `VFXCaptures/knight/E2_compare.png`.

| 부품 | 무엇 | 값 (인스펙터, 빌드 때 그대로 남음) |
|---|---|---|
| `E2 Star` | 별 섬광. 재생 `VfxStarFlash`(킹 F2 · 나이트 E4와 같은 부품), 메시 `SM_Knight_Star`(E4와 같이 씀), 재질 `M_Knight_SpringStar`, 뿌리 위 4 cm | 처음 크기 **0.55 m** → 0.3 m · **0.08초** · 가로줄 **0.1초** · 밝기 ×2.5 · 십자 돌림 ±12°(Play마다 새로) · 사는 동안 8° 돎(기본값) |
| (재질) `M_Knight_SpringStar` | 셰이더 `Star Flash Additive`(킹 R185, 안 고침). 처음 만들 때만 아래 값을 넣음(그 뒤는 사용자 값) | 번짐 · 빛살 `#A8703D` · 심 `#FFF1DD` ×4 · 가로/세로 **2.2**(킹 3.5, E4 1) · 가로줄 **0.8**(놋쇠 테를 따라) · 십자 **1.4**(레퍼런스의 긴 빛살 네 개) · 빛살 **10개** · 빛살 세기 **0.8** · 번짐 **0.5**(기본 0.8, 또렷하게) · 빛살 가늘기 **45** · 십자 가늘기 **45** · 나머지 기본값 |
| `E2 Streaks` | 가는 곧은 줄. E4와 같은 Shuriken 꼬리(Trails)만 그리는 방식(`Streaks`), 재질 `M_Knight_SpringStreak` | 한 번에 **4 ~ 5개** · 5 ~ 7.5 m/s · 0.1 ~ 0.16초 · 굵기 1.2 ~ 2.2 cm(끝에 40%) · 중력 0 · 원뿔 **26°**(부채) · **40° 아래로** 기울임 · 반지름 3 cm. 꼬리: 입자 수명의 50% · 끝 굵기 20% · 끝으로 흐려짐 |
| (재질) `M_Knight_SpringStreak` | 셰이더 `Soft Glow Additive`(공용, 안 고침) | `#A8703D` × **5**(E4는 3) · 꺾임 2 |

**레퍼런스(`E_02_spring.png`)와 다른 점 (R192 캡처 — 사용자 10-28 "E2는 그대로 두고 확인은 나중에 할게": 값은 그대로 두고, 아래 고칠 값은 사용자가 확인할 때 같이 본다):** 발굽 자리의 짧은 따뜻한 흰 별 섬광, 벼랑 밖 아래로 부채처럼 퍼지는 곧은 줄, 고리 없음은 맞다. 잰 값: 0초 별 가운데 `#FEFEFE`, 둘레 번짐 `#816A4D`(채도 0.44), 빛이 닿는 폭 약 0.64 m(대부분 가로줄) → 0.03초 0.58 m → 0.05초에 거의 꺼짐. 줄(옆 · 반대쪽, 0.05 ~ 0.08초): 3 ~ 4줄, 보이는 길이 0.2 ~ 0.33 m, 가장 밝은 곳 `#C5AA81` ~ `#DDC8A5`, 평균 `#6F583C` ~ `#957D5D`(채도 0.38 ~ 0.46), 0.12초에 다 사라짐. 나이트 쪽(0°)에서는 줄이 시선 방향이라 0.1 m 안팎으로 짧다.
1. **줄이 짧고 흐림**: 레퍼런스 줄은 별보다 몇 배 길게(나이트 키의 절반쯤) 벼랑 아래로 뻗는데, 여기는 0.2 ~ 0.33 m에 끝으로 갈수록 흐려 짧아 보인다. → 꼬리 `lifetime` 0.5 → 1.0(지나간 길 전부), 속도 5 ~ 7.5 → 7 ~ 10 m/s(길이 0.7 ~ 1.6 m), 재질 `_Intensity` 5 → 8.
2. **별 빛살이 짧음**: 레퍼런스는 가는 빛살 여러 개가 사방으로 길게(가장 긴 것은 0.5 m 넘게) 뻗는 반짝임인데, 여기는 가로줄만 길고 세로 · 대각선 빛살은 짧고 흐리다. → `startSize` 0.55 → 0.8, 재질 `_RayStrength` 0.8 → 1.6 · `_RayMin` 0.3 → 0.5.
3. **별과 줄이 한 장에 같이 안 보임 · 공기 줄 없음**: 별은 0.05초에 거의 꺼지고 줄은 그 무렵에야 별 밖으로 나와, 레퍼런스처럼 섬광과 줄이 같이 보이는 순간이 짧다. 레퍼런스 오른쪽의 휜 흐린 공기 줄 두세 가닥은 만들지 않았다(실사 공중제비 공기 굴절이 비행 12%부터 나오고, 그어 놓은 공기 줄은 만화 속도선처럼 보일 수 있어서 — 킹 F4 때 그랬음). → `duration` 0.08 → 0.12초, 줄 수명 0.1 ~ 0.16 → 0.14 ~ 0.22초. 공기 줄이 꼭 있어야 하면 다음에 따로 물음.

E2 남은 이슈:
- 캡처에는 몸 · 체스판 · 벼랑이 없다. 게임에서 놋쇠 테 위 별 아래쪽 절반은 판에 가려지고(별은 카메라 쪽으로 15 cm 당겨 그리고, 가려진 곳도 35%는 보임), 줄은 벼랑 밖 아래로 나간다 → 게임에서 보고 정함.
- 테에서 0.6 m보다 안쪽에서 뛸 때(줄을 판 위로 스치게 듦)는 캡처로 안 봤다. 줄이 판에 닿아 반쯤 묻힐 수 있다.
- 다른 PC에서는 신호가 조금 늦게 오면 발굽이 이미 떠 있을 수 있다(자리는 그 화면의 발굽 · 테로 찾음, 판 높이는 신호의 `e.at` 높이). 두 PC로 아직 안 봤다.

**E1 웅크림 — 만든 값 (R195, 검토 중):** 프리팹 `10_Knight_E_Coil`(뿌리 = 두 발 가운데 바닥(판 위면 판 높이), +Z = 뛰어나갈 쪽(수평), 앵커 Feet, 길이 0.45초), 키 `E_Coil`. 게임은 `KnightCrouch`(웅크림이 시작될 때, 0.35초) 때 낸다(`KnightVfxCoil`, `SwordFightSkillFx.Edge.cs` 실사 갈래 `KnightCrouch` 줄 — 실사 웅크림 연출은 원래 없음). 자리는 그 순간 두 발 가운데(`Hooves`), 판 위면 판 높이로. 웅크림이 끝나 뛰어나가면(`SfEdge.Active`) 남은 빛은 그대로 꺼지게 두고(바닥 빛 0.4초), 웅크림이 끊기면(맞거나 취소 — 단계가 Windup도 Active도 아님) 바로 지운다. 다른 PC는 신호가 단계보다 먼저 올 수 있어 처음 0.2초는 기다린다. 공용 셰이더 `Soft Glow Additive`만 쓰고 재질 세 개만 새로 만들었다(다른 프리팹 · 메시는 안 고침). 캡처 `VFXCaptures/knight/E1_side/`(90° 옆 — 레퍼런스와 같은 쪽) · `E1/`(0° 나이트 뒤) · `E1_front/`(180° 나이트 앞), 0 · 0.08 · 0.16 · 0.24 · 0.3 · 0.34 · 0.4초, `Tone.Game`, 바닥 위 0.6 m, 몸 · 체스판 없이 효과만. 비교 `VFXCaptures/knight/E1_compare.png`, 가운데 2배 확대 `E1_zoom.png`.

| 부품 | 무엇 | 값 (인스펙터, 빌드 때 그대로 남음) |
|---|---|---|
| `E1 Threads` | 빨려 드는 가는 빛 실. E4 · E2와 같은 Shuriken 꼬리(Trails)만 그리는 방식, 재질 `M_Knight_CoilThread` | 0.3초 동안 나오는 수 **15 → 70/초**(모두 약 13개) · 나오는 곳 **반지름 0.28 m 원**(두께 35% → 0.18 ~ 0.28 m) · 뿌리 위 **0.32 m** · 흩어짐 6 cm · 뿌리 쪽으로 **2.4 m/s** 끌어당김(`radial` −2.4) · 수명 **0.15 ~ 0.18초** · 굵기 **0.8 ~ 1.4 cm**(60% → 100%) · 투명도 0 → 1(25%) → 1(80%) → 0 · Local · 최대 40개. 꼬리: 입자 수명의 60% · 끝 굵기 20% · 끝으로 흐려짐 |
| (재질) `M_Knight_CoilThread` | 셰이더 `Soft Glow Additive`(공용, 안 고침) | `#A8703D` × **4** · 꺾임 2 |
| `E1 Glow` | 발밑 바닥 빛(바닥에 눕힌 판 `HorizontalBillboard`), 재질 `M_Knight_CoilGlow`, 바닥 위 5 mm | 크기 **0.6 m**(50% → 100%, 85%에서 다 큼) · 0.4초 · 투명도 0 → 0.25(35%) → 1(85%) → 0 |
| (재질) `M_Knight_CoilGlow` | 셰이더 `Soft Glow Additive` | `#A8703D` × **2.5** · 꺾임 2.5 |
| `E1 Core` | 바닥 빛 가운데 따뜻한 흰 심(같은 방식), 재질 `M_Knight_CoilCore` | 크기 **0.16 m** · 나머지는 `E1 Glow`와 같음 |
| (재질) `M_Knight_CoilCore` | 셰이더 `Soft Glow Additive` | `#FFF1DD` × **5** · 꺾임 3 |

**레퍼런스(`E_01_coil.png`)와 다른 점 (R195 캡처):** 다리 사이 바닥 한 덩이 빛, 그쪽으로 아래로 모이는 가는 빛 실, 고리 없음은 맞다. 색은 레퍼런스의 은색이 아니라 정한 황갈색(모양만 따름). 잰 값(옆 90°): 0.16초 바닥 빛 가운데 `#F0ECE7`, 빛이 닿는 폭 0.21 m → 0.3초 가운데 `#FEFEFD` · 심 `#F1E9DD` · 둘레 번짐 `#5B472F`(채도 0.51), 폭 0.32 m(앞 180°에서는 0.54 m) → 0.34초 0.34 m로 가장 밝음 → 0.4초에 꺼짐(실 몇 가닥만 남음). 실: 확대 그림에서 한 번에 4 ~ 8가닥, 보이는 길이 0.1 ~ 0.4 m, 가장 밝은 곳 `#EBE1D3` ~ `#EDE3D3`, 평균 `#7A6448` ~ `#856F52`(채도 0.38 ~ 0.44).
1. **실이 성기고 넓게 퍼짐**: 레퍼런스는 가는 빛줄기 수십 가닥이 발굽 하나로 빽빽이 모이는 다발(아래로 좁아지는 원뿔)인데, 여기는 한 번에 4 ~ 8가닥이 지름 약 0.56 m 원에서 드문드문 들어온다. → 나오는 수 15 → 70/초를 **40 → 160/초**(모두 약 30개), 원 반지름 0.28 → **0.2 m**, 최대 40 → **60개**.
2. **실이 바닥 빛에 닿기 전에 사라짐**: 확대 그림에서 실 끝이 바닥 빛 위 5 ~ 15 cm 공중에서 끊긴다(마지막 20%에서 흐려지고, 0.15 ~ 0.18초 × 2.4 m/s = 0.36 ~ 0.43 m라 시작 거리 0.37 ~ 0.43 m를 겨우 채움). 레퍼런스는 다발 끝이 발굽에서 가장 밝다. → 투명도 곡선 1(80%) → **1(95%)**, 끌어당김 2.4 → **3 m/s**.
3. **실이 바닥 빛보다 훨씬 어두움**: 바닥 빛 가운데는 `#FEFEFD`인데 실은 평균 `#7A6448` ~ `#856F52`이고 굵기 0.8 ~ 1.4 cm라 다발로 안 읽힌다. → 재질 `_Intensity` 4 → **7**, 굵기 0.008 ~ 0.014 → **0.012 ~ 0.02 m**.

E1 남은 이슈:
- 캡처에는 몸 · 체스판이 없다. 게임에서는 바닥 위 0.32 m 원에서 들어오는 실이 나이트 다리 · 몸에 가려질 수 있다 → 게임에서 보고 정함.
- 웅크림이 0.35초라 뛰어나갈 때 바닥 빛(0.4초)이 0.05초쯤 E2 별 섬광과 겹친다. 겹쳐 지저분하면 바닥 빛 수명을 0.35초로.
- 효과는 웅크림을 시작한 자리에 고정된다(나이트가 웅크리며 조금 움직이면 발밑에서 어긋날 수 있음). 두 PC로 아직 안 봤다.

### 나이트 레퍼런스 이미지 프롬프트

이미지 AI에 그대로 붙여 넣는다. **공통 머리말 + 순간별 문장**을 이어 붙인다. 만든 이미지는 표의 파일 이름으로 `Assets/VFX/Reference/knight/`에 넣는다(채팅에 붙이면 AI가 찾아 넣음 — R177에서는 세션 임시 폴더에 없어 같은 시각의 `OneDrive/사진/Screenshots` 화면 캡처에서 가져옴. 원본 파일을 직접 넣어 주면 더 크고 선명). 한 순간에 2~3장 뽑아 제일 가까운 것을 고른다. **F_00 · E_00 연속 그림**을 먼저 뽑아 참고 이미지로 넣고 나머지를 뽑으면 장면끼리 어긋나지 않는다. 4칸 연속 그림이 필요하면 비숍 절의 "(선택) 4칸 연속 그림" 문장을 그대로 덧붙인다.

**공통 머리말 (F · E 모두):**
```
Stylized game VFX concept frame, Unreal Engine Niagara quality. Setting: a giant polished wooden chessboard arena (walnut and maple squares, thin brass trim along the board edge, a dark drop beyond the edge), dim warm evening light so the effect reads clearly. Characters are toy-like wooden chess pieces with simple rounded arms, white gloved hands and short sturdy legs. The knight is an ivory-white wooden chess knight with a carved horse head; he holds one short curved steel blade (kukri-like) in his right hand. The opponent is a dark ebony wooden chess piece. Effect color: deep warm bronze light (#A8703D) with a hot warm-white core (#FFF1DD) and bright gold-bronze highlights; no purple, no blue-violet, no rainbow colors, no fire or flames, no lightning, no smoke or dust clouds, no rings or circular shockwaves, no blood, no cartoon outlines, no text, no UI. 16:9, one single frame, sharp focus, the effect is the subject.
```
**R177(10-28)에 색을 정해 머리말과 순간별 문장을 새 색으로 고쳤다**: 빛 = 진한 황갈색 `#A8703D`(영어로 "deep warm bronze"), 적 칸 = 빨강 `#C24A41`(답 대기, 주황으로 두면 `red (#C24A41)`을 `warm amber`로). 이미 받은 F 그림 4장은 그 전 은색 프롬프트로 뽑은 것이라 모양만 따른다. 앞으로 뽑을 그림(`F_03` · `F_05` · `F_04_fork_player` · E 전부)은 이 프롬프트 그대로.

**F_00 · F 전체 연속 그림 (`F_00_storyboard.png`)** — 공통 머리말의 "one single frame"을 빼고 붙인다:
```
Show the whole skill as a 6-panel storyboard in two rows of three, the same camera in every panel (behind and to the right of the knight, 4 m back and 2 m up, looking toward a spot on the board 4 m ahead). Panel 1: thin square outlines of bronze light lie on the board ahead, one where he will land and eight smaller ones around it at knight's-move positions (two squares out, one across); the one with the ebony opponent in it is outlined in red (#C24A41). Panel 2: he springs up off the board; a horseshoe-shaped imprint of bronze light flares on the spot he left and a thin bright trail curves up behind him. Panel 3: at the top of the leap, 3 m up, he tips over nose-down; a thin straight beam of bronze light drops from him to the landing square. Panel 4: he dives almost straight down like a spear, a short thick streak of light and a few speed lines behind him. Panel 5: the instant he lands, the landing square flashes as a flat square plate of light, and lines of light race across the board along eight L-shaped paths to the eight knight's-move squares, each flaring with a short upright shaft of light; the red-edged square with the opponent is the brightest. Panel 6: the opponent on that square is thrown back with a brief white-hot flash on his body while the light on the board cools and fades.
```

**F1 · F2 · 뜀 · 날아오름 (`F_01_leap.png`):**
```
Camera: low side view at board level, 3 m from the knight. He has just sprung up off the board, body rearing back, legs driving down, sword raised. On the spot he pushed off from, a crisp horseshoe-shaped imprint of bronze light about half a metre across flares on the wood, its open end facing the way he is going; a few short thin streaks of displaced air are pressed down and back from under him. A thin bright trail of light, hot white in its core and crisp at its edges, curves up from the imprint to his body and fades to nothing about a metre behind him.
```

**F3 · 꼭대기 (`F_02_apex.png`):**
```
Camera: low on the board, 4 m to the side, looking up at the knight at the top of his leap, 3 m above the board. He hangs for an instant, tipping forward nose-down, his free hand pointing at a square on the board below and ahead. A small brief glint of white light flashes on him. A single thin straight beam of bronze light drops from him to that square; around it on the board, eight small square outlines of faint light at knight's-move positions (two out, one across) brighten slightly.
```

**F4 · 내리꽂기 (`F_03_dive.png`):**
```
Camera: side view, 5 m away, the whole drop in frame. The knight dives almost straight down from 3 m up onto a square on the board, sword driven down in front of him, moving very fast. Behind him a short, thick, spear-like streak of bronze light with a hot white core and crisp straight edges, and a few thin parallel speed lines, mark the path he has just fallen; the streak is brightest right behind him and fades upward over about 1.5 m.
```

**F5 · 착지 · 포크 — 핵심 (`F_04_fork.png`):**
```
Camera: high three-quarter view, 6 m up and 6 m back, the knight in the middle of the frame. The instant he lands, bowed over his blow, the square under him (about 1.2 m wide) flashes as a flat square plate of bronze light with a hot white centre: square, not round. From it eight lines of light race outward across the wood along eight L-shaped paths, two squares straight out and then one square across, like a chess diagram of a knight's moves drawn in light, and at the end of each path a small square of light flares with a short upright shaft of light about 1 m tall rising from it. One of those squares holds the ebony opponent: its square and shaft are the brightest, with a red (#C24A41) edge. The light lies flat on the board in crisp lines, no circles.
```
같은 순간 플레이어 시점(`F_04_fork_player.png`): 첫 문장을 `Camera: behind the knight, 3 m back and 1.5 m up, looking over him at the board.`로 바꾼다.

**F6 · 맞음 (`F_05_hit.png`):**
```
Camera: side view of the opponent, who stands on a square of light 3 m from the knight. The ebony opponent has just been struck by the fork and is thrown backward off his feet, away from the knight. A bright white-hot flash bursts on his body where he was struck, a crisp thin streak of light slants down across him, and a spray of small sharp slivers of bronze light flies the way he is pushed. A few thin straight streaks of displaced air trail behind him.
```

**E_00 · E 전체 연속 그림 (`E_00_storyboard.png`)** — 공통 머리말의 "one single frame"을 빼고 붙인다:
```
Show the whole skill as a 5-panel horizontal storyboard, the same side camera in every panel (6 m away; the board's brass-trimmed edge and the dark drop beyond it on the left; the ebony opponent 2 m in from the edge, facing the knight). Panel 1: the knight stands with his back to the edge, crouched low and coiled, staring at the opponent; thin threads of bronze light gather round his hooves. Panel 2: he springs backward off the brass edge, out over the drop, with a short bright flash where his hooves left the brass. Panel 3: he somersaults forward high over the opponent's head; his hind hooves draw a long bright crescent arc of bronze light along the flip. Panel 4: he lands behind the opponent already kicking, both hind hooves slamming into the opponent's back: a hot white star flash at the hooves and a cone of bright white-hot sparks and bronze light streaks thrown toward the edge. Panel 5: the opponent flies off over the edge into the dark with a short trail of light behind him, the crescent fading in the air.
```

**E1 · 웅크림 (`E_01_coil.png`):**
```
Camera: low side view at the board's brass-trimmed edge, the dark drop behind the knight. The knight stands with his back to the edge, crouched low over his legs, coiled to spring, staring at the ebony opponent 2 m in front of him, his sword cocked high behind his shoulder. Thin threads of bronze light are drawn in toward his hooves and gather there as a growing glow, as if all his strength were packing into his hind legs.
```

**E2 · 뛰어나감 (`E_02_spring.png`):**
```
Camera: side view along the edge. The knight has just kicked off the brass trim backward, out over the dark drop, his body thrown back and rising. Where his hooves left the brass, a crisp, very short warm-white flash with a few thin straight light streaks; two or three thin lines of displaced air spread outward over the drop.
```

**E3 · 공중제비 — 핵심 (`E_03_flip.png`):**
```
Camera: side view, 6 m away; the board's brass edge and the dark drop on the left, the ebony opponent standing 2 m in from the edge. The knight is in the middle of a forward somersault high above the opponent's head, upside down at the top of the turn. His hind hooves have drawn a long crescent arc of bronze light through the air along the somersault, starting out over the drop and curving up over the opponent, thin and crisp at its edges with a hot white core, brightest right at the hooves and fading toward its tail. Faint swirling streaks of bent air curl round the turn.
```

**E4 · E5 · 뒷발차기 · 날아가는 적 (`E_04_kick.png`):**
```
Camera: low three-quarter view from behind the knight, with the opponent, the board's edge and the dark drop beyond him in frame. The knight has just landed behind the ebony opponent and bucked: both hind hooves slam into the opponent's back. At the point of impact, a hot white star flash with short sharp rays, and a cone of bright white-hot sparks and straight bronze light streaks thrown forward the way he is kicked, toward the edge. The opponent is launched off his feet toward the drop, a short bright trail of bronze light behind him.
```

---

## 킹 (소드 파이트) — F3 사용자 확인 · F2 · F4 · F1 · F5 그대로 두기로 함 (R187 · R190 · R193) · E1 ~ E6 만듦 · 게임 Play로 AI 시험 (R196 · R198 ~ R203, 사용자 확인 전)

**사용자 10-28: "현재 진행 되는 URP VFX 채팅내역 확인 후 킹 E,F 만들거야"**(비숍 채팅 · 나이트 채팅이 각자 F5를 Unity로 만드는 동안 다른 채팅에서). 킹은 R160 · R163 · R168 · R169에 이전 방식으로 만들어 두었다(README §9, 착지만 새 방식). 비숍 · 나이트처럼 **동작을 코드에서 읽어 층 계획 · 레퍼런스 프롬프트부터** 썼다. 만들기는 그림을 받은 뒤 `/vfx-layer king <층>`으로, 다른 채팅이 Unity를 쓰지 않을 때.

- **색:** 킹 고유색 **앤틱 금색 `#C9952F`**(몸통 · 번짐), 밝은 쪽 `#F1D48A`, 어두운 쪽 `#7C5517`, 심 = 따뜻한 흰색 `#FFF4D6`(프롬프트 영어 "deep antique gold"). 세 모드 판 색 `BKing`과 같은 값이다 — R129 사용자 "킹이랑 퀸 색을 구별 해줘 퀸이 더 빛이 강하게"로 킹은 깊고 차분하게, 퀸은 밝게 빛나게(`BQueen` `#F2B73F`) 정한 것을 따른다. 이전 방식 · 실사의 `KingGold` (1, 0.76, 0.38)은 더 밝은 노란 금색이라 새 층부터 `#C9952F`로 맞춘다(실사 쪽 빛 · `KingSlash` 색은 안 바꿈 — 겹칠 때 물음). 나이트 F5(R179)에서 본 대로 ACES에서는 심 배수가 겹치면 색이 흰색으로 바래므로, 킹 층은 심 배수를 낮게 · HDR 색을 진하게 시작해 앤틱 금색이 노랗거나 하얗게 보이지 않게 한다.
- **지금 게임에 나오는 것(그대로 둠):** 실사 연출 `SwordFightSkillFx.Real.cs`(F: 칼 반짝임 · 발밑 공기 굴절 · 금빛 조명 · 막을 때 `SteelClash` 흰 섬광 + 렌즈 빛줄 · 색수차 `LensShock` · 반격 `KingSlash` 금색 초승달 · `BladeBlur`) · `.RealEdge.cs`(E: 벽 깨짐 `KingBreach` · 부스러기 · 잔상 `RealGhost` · 착지 나뭇조각)와 이전 방식 프리팹 `Prefabs/King/01~08` · `10`(README §9 표). 새 층을 만들면 **그 순간의 게임 키(`VfxSets/King`)만 새 프리팹으로 바꾸고**, 예전 프리팹 파일은 사용자가 지우라고 할 때까지 둔다(비숍 R173처럼). 겹쳐서 지저분하면 그 층을 확인할 때 실사 쪽을 줄일지 묻는다.
- **새 프리팹 번호:** 예전 01~10과 겹치지 않게 F는 `21~25`, E는 `31~36`.
- **빌더(R183):** `Assets/Scripts/VFXEditor/KingVfxBuilder.cs`, 메뉴 `ChessFight/VFX/King/Build`(이미 있는 착지 빌더 `KingLandBuilder.cs`와 따로). 지금은 F1 ~ F5와 E1을 만든다(R185 메뉴 `Build F2 (block only)` · R187 `Build F4 (hit only)` · R190 `Build F1 (guard only)` · R193 `Build F5 (whiff only)` · R196 `Build E1 (stab only)`는 그 층만 — 다른 층 프리팹은 그대로). 게임 연결은 예전 키가 있으면 코드를 바꾸지 않고 `VfxSets/King`의 키만 새 프리팹으로 바꾼다(`SwordFightSkillFx.PieceVfx.cs`의 `KingVfx(key, …)`를 그대로 씀). 예전에 없던 순간(F4 `F_Hit`)은 새 키와 부르는 줄을 더한다. 자세 내내 킹을 따라가야 하는 F1은 키는 그대로(`F_Guard`) 두고 부르는 줄을 `KingGuardVfx`로 바꿈(R190). 떨어지는 칼을 따라가야 하는 F5는 새 키 `F_Whiff`와 `KingWhiffVfx`(R193). 벽 깨짐에 맞춰야 하는 E1은 키는 그대로(`E_Stab`) 두고 부르는 줄을 `KingStabVfx`로 바꿈(R196).
- **레퍼런스(10-28, 사용자 "E,F 레퍼런스 사진이랑 세부적인 E 레퍼런스 사진. F 레퍼런스 사진 작업 완료 되면 E 스킬 VFX 진행"):** 채팅에 붙인 9장을 `Assets/VFX/Reference/king/`에 넣음(git 밖). 한 장에 두 안이 나란히 있다. 세부 E 그림은 아직 없다(E는 연속 그림만).
  - `F_00_storyboard` — F 연속 그림 6칸 × 2: 칼에 금빛 → 막을 때 금백색 별 섬광 · 불꽃 → 칼을 젖힘 → 큰 금빛 초승달 베기 → 맞은 적이 날아감.
  - `E_00_storyboard` — E 연속 그림 6칸 × 2: 떨어지며 칼을 판 옆면에 박음(섬광) → 매달림 → 금빛 호를 그리며 뛰어오름 → 착지 바닥에 금빛 킹 문양 · 금 · 가로 빛줄 → 맞은 적.
  - `F_01_guard_a` — F1 안 A: 킹을 둘러싼 **세로 금빛 실 가닥 원통**(위 열림) + 칼날에 금빛.
  - `F_01_guard_b` — F1 안 B: 킹 칸 둘레 **판 줄을 따라 금빛 네모 테두리**(여덟 칸) + 칼날에 금빛.
  - `F_02_parry` — F2: 칼이 맞닿은 곳에 **금백색 별 섬광**(빛살) + 가로 빛줄 + 금빛 불꽃이 튐.
  - `F_03_counter` — F3: 오른쪽 위에서 앞을 지나 왼쪽 아래로 가는 **굵은 금빛 초승달**, 바깥 가장자리가 흰빛, 결 무늬.
  - `F_03_counter_player` — F3 플레이어 시점(뒤에서): 초승달이 킹 **오른쪽에 `)` 모양**으로 크게, 거의 정면으로 보임.
  - `F_04_hit` — F4: 적 몸을 비스듬히 가르는 **금빛 베인 줄** + 밀리는 쪽으로 금빛 불꽃 조각.
  - `F_05_whiff` — F5: 칼끝이 바닥에 떨어지고 칼날 둘레에서 **금빛 알갱이가 흩어짐**, 몸 둘레 옅은 금빛 안개.

**F 왕의 반격 — 동작 · 타이밍 (코드 기준, `SwordFightSkillParams`):**

| 단계 | 시간 | 일어나는 일 |
|---|---|---|
| 자세 | F 누름 | 신호 `Cast`(발밑). 칼을 앞에 가로로 듦(0.07초, 칼끝은 왼쪽 · 조금 앞 · 위), 빈손으로 칼을 받침. 자세 중에도 걸음(빠르기 0.7배, `kingGuardMove`), 자세는 걷는 쪽을 봄 |
| 받아내기 | 0.9초 (`kingGuard`) | 이 안에 들어온 칼을 막음(경기 프로토타입은 칼만, 스킬 시험 씬은 퀸 찌르기 · 나이트 강하 · 차기도). 아무것도 안 오면 → 빈틈 |
| 막음 | 막는 순간 | 신호 `KingParry`(맞닿은 곳 · 공격자 쪽), 공격자 쪽으로 돎. 퀸 · 나이트는 그 자리에서 튕겨 나감(신호 `KingRepel`). 실사: 멈춤 · 짧은 슬로 · 흔들림 |
| 멈칫 | 0.16초 (`kingCounterBeat`) | 칼을 오른 어깨 위로 높이 젖힘 |
| 반격 베기 | 0.34초 (`kingCounterTime`) | 신호 `KingCounterSpin`(발밑 · 방향 · 반경 2.5 m). **오른쪽 위 → 앞을 지나 → 왼쪽 아래로 큰 대각선 한 번**. 베기의 25%(0.085초)에서 맞음 |
| 맞음 | 베기 0.085초 | 반경 2.5 m · 막은 상대 쪽 앞 150° 안 적 모두 **2.8 m 밀림**(신호 `KingCounterHit`, 맞은 적마다) |
| 후딜 | 0.4초 (`kingRecovery`) | 칼을 왼쪽 아래에 0.12초 두었다가 거둠 |
| 빈틈 | 0.6초 (`kingWhiff`) | 막을 게 없었으면 신호 `KingWhiff`: 칼끝이 바닥으로 떨어져 0.33초 열린 채 있다가 다시 듦 |

예: 0.4초에 막음 → 멈칫 0.4~0.56초, 베기 0.56~0.9초(맞음 0.645초), 후딜 0.94~1.34초. 안 막으면 빈틈 0.9~1.5초. 쿨 8초.

**F 층 계획 (초안):**

| 층 | 순간 | 모양 (초안) | 프리팹 · 키 | 상태 | 레퍼런스 |
|---|---|---|---|---|---|
| F1 | 자세 · 받아내기 0.9초 (`Cast`) | 칼을 드는 순간 금빛이 손잡이 → 칼끝으로 달려 칼끝 반짝 → 칼날에 은은한 금빛이 머묾 + 둘레 표시(안 A: 몸 앞 · 옆 · 뒤를 감싼 아주 옅은 빛 방패막, 위는 열림 — R132 사용자 요청 모양 / 안 B: 발밑에 **킹이 갈 수 있는 여덟 칸** 금빛 네모 테두리, 킹을 따라 움직임), 끝나기 0.2초 전부터 깜빡이며 약해짐(언제 끝나는지 보임). 막거나 빈틈이 되면 바로 꺼짐 | `21_King_F_Guard` · `F_Guard` | **안 B로 만듦(R190, 캡처) — 사용자가 그대로 두기로 함** | `F_01_guard_a.png` / `F_01_guard_b.png` |
| F2 | 막음 (`KingParry`) | 칼과 칼이 맞닿은 곳에 **뜨거운 금백색 별 섬광**(짧은 빛살) + 맞닿은 곳을 가로지르는 납작한 빛줄 + 공격자 쪽으로 튀는 날카로운 금빛 조각, 고리 없음(안 A면 방패막이 맞은 자리만 한 번 번쩍) | `22_King_F_Parry` · `F_Block` | **그대로 두기로 함**(R185, 10-28 사용자 "F2는 그대로 두고", 다른 점 3개 안 고침, 게임 Play 전) | `F_02_parry.png` |
| **F3** | **반격 베기** (`KingCounterSpin`) | **오른쪽 위 → 왼쪽 아래로 앞을 크게 가르는 금빛 초승달 베기 한 줄**(맞는 범위 2.5 m · 앞 150°에 맞춘 크기, 가운데 굵고 밝음 · 양 끝 가늘고 날카로움 · 심 흰색), 0.34초에 그려지고 잠깐 남았다 사라짐 — 이 스킬의 얼굴 | `23_King_F_Counter` · `F_Counter` | **사용자 확인**(R183, 10-28 캡처 보고 "확인 했고 다음 작업 진행", 다른 점 3개는 그대로, 게임 Play 전) | `F_03_counter.png` · `F_03_counter_player.png` |
| F4 | 맞은 적마다 (`KingCounterHit`) | 몸에 **금빛 대각선 베인 자국 번쩍**(베기와 같은 방향) + 밀리는 쪽으로 날카로운 금빛 조각 · 곧은 공기 줄, 고리 없음 | `24_King_F_Hit` · `F_Hit`(새 키) | **그대로 두기로 함**(R187, 아래 · 10-28 사용자 "F4는 그대로 두고", 다른 점 3개 안 고침, 게임 Play 전) | `F_04_hit.png` |
| F5 | 빈틈 (`KingWhiff`) | 칼끝이 떨어질 때 칼날의 금빛이 몇 알갱이로 흩어져 떨어지며 꺼짐(자세가 헛됨) | `25_King_F_Whiff` · `F_Whiff`(새 키) | **만듦(R193, 캡처) — 사용자가 그대로 두기로 함(확인은 나중에 게임에서)** | `F_05_whiff.png` |

- 추천 순서: **F3(반격) → F2(막음) → F4(맞음) → F1(자세) → F5(빈틈)**(얼굴이 되는 순간부터).
- F3를 만들면 지금 실사 `KingSlash`(10-26, 사용자 "킹 칼 휘두르는 vfx 멋있게" → "금색 재질이 더 좋아"로 만든 금색 초승달)와 겹친다 → F3 확인 때 `KingSlash`를 끌지 묻는다.
- F1은 길이가 정해져 있지 않다(막는 순간 또는 0.9초까지). 막음 · 빈틈 때 게임 쪽에서 꺼 줘야 한다(연결 방식은 F1 때 정함). 안 A · B는 그림을 보고 고른다.
- 막은 상대가 비숍일 때(스킬 시험 씬)는 지금 실사 연출 그대로.

**F3 반격 베기 (R183, 10-28) — 사용자 확인** (사용자 "F 레퍼런스 사진 작업 완료 되면 E 스킬 VFX 진행" → 추천 순서대로 F3부터. 캡처를 보고 "확인 했고 다음 작업 진행" → 아래 값 그대로, 다른 점 1~3은 고치지 않음, 커밋 아님)

- **순간:** `SfFxKind.KingCounterSpin`(멈칫 0.16초 뒤 베기가 시작할 때). 뿌리 = 킹 엉덩이 아래 바닥, +Z = 베는 쪽(게임 연결 `RealKingSpin`의 `KingVfx("F_Counter", …)` 그대로, 코드 안 바꿈).
- **모양:** 킹 오른 어깨(뿌리에서 오른쪽 0.2 · 위 1 · 앞 0.05 m)를 도는 **금빛 초승달 띠**. 칼이 지나는 면(코드의 칼 방향 `KingC` → `KingX` → `KingL` = 오른쪽 위 3° → 앞 102° → 왼쪽 아래 138°)에 눕힘. 바깥 가장자리(칼끝 길) 1.6 m, 가장 굵은 곳 0.55 m(길이의 55% 자리), 양 끝 뾰족, −6°~146°.
- **빛:** 바깥 가장자리에 **가는 흰 선**(1.2 cm) + 안쪽으로 갈수록 옅어지는 금빛 **결 무늬**(26줄, 줄마다 밝기 · 길이가 다름) + 가장자리 밖 옅은 번짐 8 cm. 머리(지금 칼이 있는 곳)가 가장 밝음.
- **시간:** 칼과 같이 그려짐 — 빨라지며 0.085초(맞는 순간)에 70%, 느려지며 0.34초에 끝까지(칼 동작과 같은 In2 → Out2). 맞는 순간 밝기 1.8 → 1.0으로 가라앉음(0.12초). 0.375~0.75초에 시작 쪽 · 안쪽부터 타 들어가 끝 쪽 가장자리가 마지막에 사라짐.
- **카메라 쪽으로 60% 돌림**(셰이더 `_Face`): 칼이 지나는 면은 플레이어(뒤)에서 보면 거의 옆면이라, 그대로 두면 가는 줄로만 보인다.
- **색:** 재질 `_Color` = `#CC851F`(킹 `#C9952F`보다 진하고 주황 쪽). 첫 캡처(`F3_full`)에 `#C9952F` · 밝기 1.4로 넣었더니 ACES에서 크림색으로 바램 → 진하게 · 밝기 1.0으로 낮춤(나이트 R179와 같은 이유). 흰 선 = `#FFF4D6` × 2.
- **새 파일:** 셰이더 `VFX_CrescentAdd`(`ChessFight/VFX/Crescent Additive`), 재생 `VfxCrescent`(`IVfxSeekable`. Play에서는 값으로 메시를 스스로 만들고, 캡처는 빌더가 저장한 메시 `SM_King_Counter`를 씀 → 모양 값을 바꾸면 다시 빌드), 빌더 `KingVfxBuilder.cs`, 재질 `M_King_Counter`, 프리팹 `23_King_F_Counter`(층 `F3 Crescent`, 앵커 Feet, 길이 0.9초). `VfxSets/King`의 `F_Counter` → 새 프리팹(예전 `03_King_F_Counter` 고리 파일은 그대로 둠).
- **캡처**(게임 카메라 `LabCamera` = 거리 3.6 m · 아래로 14° · 시야 60°를 캡처 시야 40°에 맞춰 뒤로 물림): `VFXCaptures/king/F3/`(플레이어 시점, (0, 2.68, −5.53) → (0, 1.3, 0)), `F3_side/`(130° 돌려 앞 왼쪽에서 — 레퍼런스 쪽), `F3_full/`(첫 값, 비교용), 비교 `F3_compare.png`(왼쪽 레퍼런스 · 오른쪽 캡처). 시간 못 받는 부품 없음.

| 조절 값 | 지금 | 권장 범위 | 효과 |
|---|---|---|---|
| `radius` | 1.6 m | 1.3~2.2 | 초승달 크기(바깥 가장자리) |
| `width` | 0.55 m | 0.4~0.9 | 가장 굵은 곳 |
| `from` · `arc` | −6° · 152° | — | 시작 · 길이(칼 길은 3°~138°) |
| `intensity` · `flashIntensity` | 1.0 · 1.8 | 0.8~1.5 · 1.5~3 | 남은 밝기 · 맞는 순간 밝기 |
| `_Face`(재질) | 0.6 | 0.3~0.9 | 카메라 쪽으로 돌리는 몫 |
| `_Inner`(재질) | 1.6 | 0.8~2.5 | 안쪽이 옅어지는 정도(작을수록 꽉 참) |
| `_EdgeWidth` · `_EdgeBoost`(재질) | 0.012 m · 2 | 0.008~0.03 · 1.5~3 | 흰 선 굵기 · 밝기 |
| `halo` · `_Halo` | 0.08 m · 0.05 m | 0.05~0.25 | 가장자리 밖 번짐 |
| `duration` · `fadeOut` | 0.75초 · 0.5 | 0.5~1 · 0.3~0.7 | 남는 시간 · 타 들어가는 몫 |

레퍼런스와 다른 점(수정안):

1. **굵기 · 꽉 참:** 레퍼런스 초승달은 띠 전체가 금빛으로 차 있고 굵다(반지름의 40~50%). 지금은 안쪽이 빨리 옅어져 가늘어 보임 → `width` 0.55 → 0.75, `_Inner` 1.6 → 0.9.
2. **밝기 · 번짐:** 레퍼런스는 둘레로 크게 번지는 금빛이 있다. 지금은 ACES 바램을 피하려고 밝기를 낮춰 번짐이 거의 없음 → `halo` 0.08 → 0.2 m · `_Halo` 0.05 → 0.12, `intensity` 1.0 → 1.2(색 `#CC851F`는 그대로).
3. **플레이어 시점 모양:** 레퍼런스(뒤에서)는 킹 오른쪽에 `)` 모양으로 정면에 가깝게 보이는데, 지금은 앞 위로 걸친 납작한 `⌒` → `_Face` 0.6 → 0.85(뒤에서 `)`에 가까워지고 옆에서도 넓게 보임. 대신 진짜 칼과 빛의 자리가 조금 더 어긋남).

- 게임에서는 지금 실사 `KingSlash`(진짜 칼 길을 따라 그리는 금색 초승달)와 **같이 나온다** → 둘 다 둘지, `KingSlash`를 끌지 사용자 답 대기(F3 확인 때 답이 없어 둘 다 둠).
- Unity 컴파일 · `Tools/Test-NetworkCompile.ps1` 통과. 게임 Play(진짜 칼과 맞는지) 전, 커밋 안 함.

**F2 막음 별 섬광 (R185, 10-28) — 사용자가 그대로 두기로 함** (사용자 "확인 했고 다음 작업 진행" → 추천 순서대로 F3 다음 F2. 캡처를 보고 "F2는 그대로 두고 다음 층 F4 맞음 진행해줘" → 아래 값 그대로, 다른 점 1~3은 고치지 않음, 커밋 아님)

- **순간:** `SfFxKind.KingParry`(킹 자세에 공격이 막힌 순간). 게임 연결은 `RealKingBlock`의 `KingVfx("F_Block", at, toward)` 그대로(코드 안 바꿈): 뿌리 = 킹 칼 위 맞닿은 곳(`OnBlade`), +Z = 공격자 쪽(바닥과 나란히). 비숍 손을 막을 때는 나오지 않음(예전처럼 실사만). 실사 `RealKingBlock`이 켜져 있을 때만 나옴.
- **모양:** 맞닿은 곳에 **금백색 별 섬광** — 흰 심 + 금빛 번짐 + 길이가 제각각인 가는 빛살 22개(자리 · 길이는 Play마다 바뀜) + 위아래 · 대각선의 긴 빛살(십자, Play마다 ±12° 돌아감) + **화면에 가로로 긴 빛줄기**(별 높이의 3.5배 너비, 가운데 흰 실). 금빛 조각 60개가 공격자 쪽 위로(위로 35° · 둘레 50°) 늘어진 줄로 튀어 떨어지며, 옅은 금색에서 진한 금색으로 식어 사라짐. 고리 없음.
- **시간:** 별 0.14초(크기 1.2 → 0.6 m, 빨리 꺼짐), 가로 빛줄기 0.22초, 조각 0.18~0.45초(0.03초에 14개 더). 길이 0.6초.
- **가려질 때:** 셰이더가 별을 눈 쪽으로 0.15 m 당겨 그리고(맞닿은 칼에 안 잘리게), 앞을 무엇이 가리면 35%만 비쳐 보이게 한 번 더 그림(두 번째 패스 `SRPDefaultUnlit` · `ZTest Greater`) — 플레이어 카메라가 킹 뒤라 맞닿은 곳이 킹 몸에 가려질 수 있어서. 캡처에는 몸이 없어 Play로 볼 것.
- **색:** 별 · 빛살 · 번짐 · 빛줄기 `#CC851F`(F3와 같은 진한 금색), 심 · 빛줄기 가운데 실 `#FFF4D6`. 조각 재질은 둘의 가운데 옅은 금색 × 3.
- **새 파일:** 셰이더 `VFX_StarFlashAdd`(`ChessFight/VFX/Star Flash Additive`), 재생 `VfxStarFlash`(`IVfxSeekable`, E1 칼 박기 섬광에도 쓸 수 있음), 재질 `M_King_Star` · `M_King_Sliver`(Soft Glow Additive), 메시 `SM_King_Star`(경계 8 m — 빛줄기가 화면 밖에서 잘리지 않게), 프리팹 `22_King_F_Parry`(층 `F2 Star` · `F2 Slivers`, 앵커 Chest, 캡처 높이 1 m, 길이 0.6초), 메뉴 `ChessFight/VFX/King/Build F2 (block only)`. `VfxSets/King`의 `F_Block` → 새 프리팹(예전 `02_King_F_Block`은 파일만 남음).
- **처음 넣은 이름의 실수:** 셰이더를 처음에 `VFX_StarburstAdd.shader`로 넣어 퀸 R162의 같은 이름 셰이더(Fire Beam 섬광)를 덮어씀 → Unity가 읽기 전에 git에서 되살림(3282바이트 그대로, 다른 채팅은 읽기만 했음) → 새 이름으로(PITFALLS 59).
- **캡처:** `VFXCaptures/king/F2/`(플레이어 시점 = F3 카메라를 0.5 m 뒤로: (0, 2.68, −6.03) → (0, 1.3, −0.5)), `F2_side/`(레퍼런스처럼 옆에서 — 공격자 왼쪽 · 킹 오른쪽, 허리 높이 3.2 m), 비교 `F2_compare.png`(왼쪽 위 레퍼런스, 오른쪽 위 옆 0초, 왼쪽 아래 옆 0.07초, 오른쪽 아래 플레이어 시점 0초). 시간 못 받는 부품 없음.
- **한 번 고침(첫 캡처):** 가는 빛살이 거의 안 보이고(십자만) 번짐이 작음 → 빛살 20 → 22개 · 길이 0.25~0.8 → 0.3~1 · 세기 1 → 1.8 · 굵게(얇기 45 → 30), 번짐 0.5 → 0.8 · 퍼짐 4 → 3, 별 1 → 1.2 m, 조각 44 → 60개 · 굵기 1~2.2 → 1.4~3 cm. 첫 값 캡처는 덮어써서 남지 않음.

| 조절 값 | 지금 | 권장 범위 | 효과 |
|---|---|---|---|
| `startSize` · `endSize` | 1.2 · 0.6 m | 0.8~1.6 | 별 크기(빛살 끝에서 끝) |
| `duration` · `streakTime` | 0.14 · 0.22초 | 0.1~0.25 · 0.15~0.35 | 별 · 가로 빛줄기가 남는 시간 |
| `intensity` | 2.5 | 1.5~4 | 밝기(심은 재질 `_CoreBoost` 4배) |
| `_Rays` · `_RayStrength` · `_RayThin`(재질) | 22 · 1.8 · 30 | 12~32 · 1~3 · 20~60 | 가는 빛살 수 · 세기 · 가늘기 |
| `_Glow` · `_Falloff`(재질) | 0.8 · 3 | 0.4~1.2 · 2~5 | 둘레 번짐 |
| `_Aspect` · `_Streak`(재질) | 3.5 · 1 | 2~6 · 0.5~2 | 가로 빛줄기 길이 · 세기 |
| `_Glare`(재질) | 0.35 | 0~0.6 | 가려졌을 때 비쳐 보이는 몫 |
| 조각 개수 · 굵기 · 둘레 | 46 + 14개 · 1.4~3 cm · 50° | 40~100 · 0.8~3 · 40~70° | 금빛 조각 |

레퍼런스와 다른 점(수정안):

1. **조각:** 레퍼런스 불꽃은 더 가늘고 많고, 맞닿은 곳 위쪽으로 넓게 흩어진다. 지금은 굵은 줄이 좁은 부채꼴로 모임 → 굵기 1.4~3 → 0.8~1.8 cm, 60 → 90개, 둘레 50° → 65°.
2. **별과 조각이 함께:** 레퍼런스는 별이 아직 환할 때 조각이 퍼져 있다. 지금은 별이 빨리 작아져, 조각이 퍼지는 0.07초에 이미 작음 → `duration` 0.14 → 0.2초(그래도 짧게 "팍").
3. **플레이어 시점:** 킹 뒤에서는 조각이 멀어지는 쪽(공격자 쪽)으로 날아 작게 보이고, 맞닿은 곳이 킹 몸에 가려지면 별이 35%만 비침 → Play에서 보고 정함(별 1.2 → 1.4 m, 또는 조각 일부를 옆으로도 튀게).

- 게임에서는 실사 `SteelClash`(흰 섬광 · 화면 가로 흰 렌즈 빛줄 2.8 m · 차가운 흰 조명)와 금빛 조명 · 공기 굴절 · `LensShock`가 **같이 나온다** → 흰 빛줄과 금빛 빛줄기가 겹침. 실사 흰 섬광 · 빛줄을 끌지 사용자 답 대기(F3의 `KingSlash`와 같은 질문).
- 10-25 실사 작업 때 사용자 "불꽃 말고 다른 걸로"(실사 막기는 불꽃 없음)가 있었지만, 이번 레퍼런스 `F_02_parry`와 R180 계획에 금빛 조각이 있어 넣었다 → 싫으면 `F2 Slivers`만 끔.
- Unity 컴파일(오류 없음) · `Tools/Test-NetworkCompile.ps1` 통과. 게임 Play · 사람 확인 전, 커밋 안 함.

**F4 맞음 금빛 베인 자국 (R187, 10-28) — 사용자가 그대로 두기로 함** (사용자 "F2는 그대로 두고 다음 층 F4 맞음 진행해줘" → 캡처를 보고 "F4는 그대로 두고 R187 문서 쓴 다음 F1 자세로 가줘" → 아래 값 그대로, 다른 점 1~3은 고치지 않음, 커밋 아님)

- **순간:** `SfFxKind.KingCounterHit`(반격 베기 0.085초, 맞은 적마다). 신호 자리(`e.at`)는 킹과 적 사이라, 새 함수 `SwordFightSkillFx.KingHitVfx`(`.PieceVfx.cs`)가 **맞은 기물의 가슴**에 내고(+Z = 밀리는 쪽 `e.dir`, 킹은 뒤쪽) 가슴에 붙여 같이 날아가게 한다(비숍 F7과 같은 방식). `SwordFightSkillFx.cs`의 `KingCounterHit` 줄에서 실사 `RealHit`(몸 색 나뭇조각 10개 · 멈춤 · 흔들림) 다음에 부름 — 실사 킹(`Real(PieceKind.King)`)일 때만. 새 키 `F_Hit`(예전 방식에는 없던 순간).
- **모양:** 적 가슴 앞(킹 쪽으로 0.3 m)에 **금빛 베인 줄 한 줄** — 길이 1.8 m · 굵기 0.1 m, 42° 기울어 킹 쪽에서 볼 때 오른쪽 위 → 왼쪽 아래(반격 칼의 면 `KingC` → `KingL`), 가운데가 조금 아래로 휨(0.07 m). 비숍 F7의 부품 `VfxSlash` · 셰이더 `Slash Additive`를 그대로 씀(코드 안 바꿈). + **금빛 조각 90개**(70개 + 0.03초 뒤 20개): 베인 줄을 따라 고루 나와 밀리는 쪽 · 위로 20° · 퍼짐 0.7, 3.5~9 m/s · 0.2~0.5초 · 1~2.2 cm · 중력 1.3, 조금만 늘어진 점, 흰빛에서 진한 금으로 식으며 사라짐(F2 조각과 같은 재질 `M_King_Sliver` · 식는 색). + **공기 줄 6개**: 몸 둘레(1 × 0.9 m)에서 밀리는 쪽으로 7~11 m/s, 아주 옅게(30%), 0.14~0.26초. 고리 없음.
- **시간:** 베인 줄은 높은 쪽 끝부터 0.045초에 그려지며 번쩍(밝기 3.5 → 1.6, 0.08초), 0.4초에 사라짐(끝 70%에 걸쳐 흐려지며 끝 쪽 굵기 30%까지 가늘어짐). 조각은 0.5초 안에 다 떨어짐. 길이 0.6초.
- **색:** 베인 줄 `#CC851F`(F3 · F2와 같은 진한 금색), 심 `#FFF4D6` × 2(Slash 셰이더 기본 3보다 낮춰 ACES에서 하얗게 바래지 않게), 심 너비 0.15 · 번짐 퍼짐 1.8(가는 심 + 넓고 부드러운 번짐 = 레퍼런스의 빛줄), 몸 쪽 흐림 0.03 m(비숍 F7과 같음).
- **새 파일:** 재질 `M_King_Cut`(Slash Additive), 프리팹 `24_King_F_Hit`(층 `F4 Cut` · `F4 Slivers` · `F4 Air`, 앵커 Target, 캡처 높이 0.9 m, 길이 0.6초), 메뉴 `ChessFight/VFX/King/Build F4 (hit only)`(`Build`는 F3 · F2 · F4 모두). `VfxSets/King`에 `F_Hit` 키를 더함. 빌더에서 F2 조각 재질 · 식는 색을 함께 쓰는 함수로 뺌(`SliverMat` · `Cooling`, F2 값은 그대로).
- **캡처:** `VFXCaptures/king/F4/`(플레이어 시점 = F3 카메라, 맞은 적이 킹 1.6 m 앞이라고 보고 (0, 2.68, −7.13) → (0, 1.3, −1.6)), `F4_side/`(50° 돌려 비스듬히 앞에서 — 레퍼런스 쪽, (0, 1.1, −3) → (−0.1, 0.9, 0)), 첫 값 `F4_first/` · `F4_side_first/`(비교용), 비교 `F4_compare.png`(왼쪽 위 레퍼런스 `F_04_hit`, 오른쪽 위 옆 0.05초, 왼쪽 아래 옆 0.15초, 오른쪽 아래 플레이어 시점 0.05초). 0초 그림은 비어 있음(줄이 아직 안 그려지고 조각이 아직 안 나옴). 시간 못 받는 부품 없음.
- **한 번 고침(첫 캡처):** 공기 줄이 만화 속도선처럼, 조각이 베인 줄을 따라 늘어선 머리카락처럼 보임 → 베인 줄 1.5 → 1.8 m · 굵기 0.07 → 0.1 · 심 너비 0.24 → 0.15 · 번짐 퍼짐 2.2 → 1.8, 조각 54 + 16 → 70 + 20개 · 크기 1.2~2.6 → 1~2.2 cm · 퍼짐 0.5 → 0.7 · 늘임 0.03 → 0.015, 공기 줄 10 → 6개 · 진하기 65 → 30% · 늘임 0.1 → 0.06. 프리팹 · 재질을 직접 고치고 빌더 기본값도 같게 함.

| 조절 값 | 지금 | 권장 범위 | 효과 |
|---|---|---|---|
| `length` · `width`(`F4 Cut`) | 1.8 · 0.1 m | 1.4~2.4 · 0.06~0.16 | 베인 줄 길이 · 굵기 |
| `lean` · `bend` | 42° · 0.07 m | 30~50 · 0~0.15 | 기울기 · 가운데 휨 |
| `sweep` · `duration` · `fadeOut` | 0.045 · 0.4초 · 0.7 | 0.03~0.08 · 0.3~0.6 · 0.5~0.8 | 그려지는 시간 · 남는 시간 · 흐려지는 몫 |
| `flashIntensity` · `intensity` | 3.5 · 1.6 | 2.5~5 · 1.2~2.2 | 번쩍 · 남은 밝기 |
| `_CoreWidth` · `_GlowFalloff` · `_CoreBoost`(재질) | 0.15 · 1.8 · 2 | 0.1~0.24 · 1.5~2.5 · 1.5~3 | 심 너비 · 번짐 퍼짐 · 심 밝기 |
| 조각 개수 · 크기 · 퍼짐 | 70 + 20개 · 1~2.2 cm · 0.7 | 60~140 · 0.8~2.6 · 0.5~1 | 금빛 조각 |
| 조각 빠르기 · 중력 · 늘임 | 3.5~9 m/s · 1.3 · 0.015 | 3~12 · 0.8~2 · 0~0.03 | 튀는 거리 · 떨어짐 · 길이 |
| 공기 줄 개수 · 진하기 · 늘임 | 6개 · 30% · 0.06 | 0~12 · 20~60% · 0.04~0.1 | 밀리는 쪽 공기 |

레퍼런스와 다른 점(수정안 — 사용자가 고치지 않기로 함):

1. **기울기:** 옆에서 보면 레퍼런스보다 가파르다(플레이어 시점에서는 약 36°로 보임) → 고친다면 `lean` 42 → 35°.
2. **조각 퍼짐:** 레퍼런스는 금빛 점이 베인 줄 둘레에 촘촘히 퍼져 있다. 지금은 위쪽 · 밀리는 쪽으로만 튐 → 퍼짐 0.7 → 0.9, 90 → 120개.
3. **플레이어 시점:** 맞은 적이 카메라에서 약 7 m라 작게 보이고, 조각이 카메라 반대쪽으로 날아 적 몸에 일부 가려질 수 있음 → Play에서 보고 정함(줄 1.8 → 2.2 m · 밝기 1.6 → 2).

- 게임에서는 실사 `RealHit`(몸 색 나뭇조각 · 멈춤 · 흔들림)와 실사 `KingSlash`(금색 초승달)가 같이 나온다. `KingSlash` · `SteelClash`를 끌지는 여전히 답이 없어 둘 다 켜 둠.
- 미러 컴파일(Core · Game · VFX · VFX 빌더 · Gameplay · RagdollLab) · Unity 컴파일(오류 없음) · `Tools/Test-NetworkCompile.ps1` 통과. 게임 Play · 사람 확인 전, 커밋 안 함.

**F1 자세 — 안 B 여덟 칸 (R190, 10-28) — 사용자가 그대로 두기로 함** (사용자 "F4는 그대로 두고 R187 문서 쓴 다음 F1 자세로 가줘" → 안 A · B 질문에 "안 B 여덟 칸 (추천)" → 캡처를 보고 "F1은 그대로 두고 다음 층 F5 빗나감으로 가줘")

- **순간:** `SfFxKind.Cast`(킹이 F를 눌러 자세를 잡는 순간)부터 자세(`SfStage.Windup`, `kingGuard` 0.9초)가 끝날 때까지. 새 함수 `SwordFightSkillFx.KingGuardVfx`(`.PieceVfx.cs`)가 엉덩이 아래 바닥에 내고(+Z = 킹이 보는 쪽) 매 프레임 킹을 따라가게 한다. `.Real.cs`의 `RealKingCast`에서 예전 `KingVfx("F_Guard", …)` 한 줄을 이 함수로 바꿈(실사 칼 반짝임 · 발밑 공기 · 금빛 조명은 그대로). 다른 화면은 신호가 단계보다 조금 먼저 올 수 있어 0.25초까지 기다린다(나이트 E3와 같은 방식). 자세가 끝나면(막음 → `Active`, 빈틈 → `Recovery`, 킹이 없어짐) 0.06초에 꺼짐. 게임이 0.3초 넘게 손을 놓으면 저절로 꺼짐.
- **칸(`F1 Squares`, 새 부품 `VfxSquareGrid` · 새 셰이더 `Square Grid Additive`):** 킹이 선 칸과 둘레 여덟 칸(킹이 갈 수 있는 칸)의 판 줄을 금빛 가는 선으로. 칸 크기는 나이트 포크와 같은 `knightCell` 1.25 m, **킹 중심 · 킹이 보는 쪽에 맞춤**(진짜 판 줄과 조금 어긋날 수 있음 — 안 B에서 말씀드린 대로). 킹 칸에서 바깥으로 0.1초에 그려지며(앞머리가 밝음) 번쩍(2.5 → 1, 0.15초), 바깥 끝은 0.3칸 더 뻗으며 사라짐, 여덟 칸 안은 아주 옅게(2%) 칸 가장자리 쪽만, 부드러운 밝은 띠가 천천히 돎(초당 2.5 rad). 방향은 시작할 때 킹이 본 쪽으로 고정, 킹이 가운데 칸을 벗어나면 그쪽으로 한 칸씩 0.08초에 미끄러져 따라감. 바닥이 없는 칸(낭떠러지 · 0.6 m 넘게 낮은 곳)은 안 그림.
- **칼(`F1 Blade`, 새 부품 `VfxBladeGlow`):** 0.05초 뒤 금빛이 손잡이 → 칼끝으로 0.1초에 달림(밝기 2.5 → 1.1), 칼끝에 도착하면 반짝(별 빛 0.4 m · 밝기 4) 뒤 작은 반짝(0.16 m · 1.4)이 계속 돎, 칼날에는 은은한 금빛이 남는데 칼끝 쪽이 더 밝음. 칼의 진짜 위치(`SwordFightPawn.BladeRoot` → `BladeTip`)를 매 프레임 따라감, 카메라 쪽으로 3 cm 띄움. 비숍 F7 · 킹 F4의 셰이더 `Slash Additive`(재질 `M_King_Guard_Blade`, 심 2 · 심 너비 0.2 · 앞머리 0.25 · 끝 흐림 0.05 · 몸 쪽 흐림 없음)와 비숍의 `Glint Additive`(재질 `M_King_Guard_Tip`)를 씀.
- **끝 알림:** 칸 · 칼 모두 자세가 끝나기 0.2초 전부터 초당 15번 깜빡이며 55%까지 어두워짐(언제 끝나는지 보임).
- **색:** 칸 번짐 `#CC851F` · 심 `#FFF4D6` × 1.5(F2 ~ F4와 같은 진한 금색, ACES에서 `#C9952F`로 보임), 칼끝 반짝은 금색과 흰색 60% 섞음.
- **새 파일:** 셰이더 `VFX_SquareGridAdd.shader`, 부품 `VfxSquareGrid.cs` · `VfxBladeGlow.cs`, 재질 `M_King_Guard_Grid` · `M_King_Guard_Blade` · `M_King_Guard_Tip`, 프리팹 `21_King_F_Guard`(층 `F1 Squares` · `F1 Blade`(밑에 `Tip`), 앵커 Feet, 길이 1.2초), 메뉴 `ChessFight/VFX/King/Build F1 (guard only)`(`Build`는 F1 · F3 · F2 · F4 모두). `VfxSets/King`의 `F_Guard` 키를 새 프리팹으로 바꿈(예전 `01_King_F_Guard` 파일은 그대로). 새 이름은 만들기 전에 비어 있는지 확인함(PITFALLS 59).
- **캡처:** `VFXCaptures/king/F1/`(플레이어 시점 = F3 카메라 (0, 2.68, −5.53) → (0, 1.3, 0)), `F1_front/`(150° 돌려 앞쪽 비스듬히 — 레퍼런스 B 왼쪽 쪽), 비교 `F1_compare.png`(왼쪽 위 레퍼런스 `F_01_guard_b`, 오른쪽 위 플레이어 시점 0.45초, 왼쪽 아래 앞쪽 0.06초(그려지며 번쩍), 오른쪽 아래 앞쪽 0.45초). 캡처 장면에는 킹 · 칼 · 체스판이 없어서 칼 빛은 칼 없이 허공에 떠 있음(미리 보기 자리 `previewGuard` (0.18, 0.95, 0.32) → `previewTip` (−0.55, 1.06, 0.4)) — 칼에 붙은 모습은 Play에서만 보임. 시간 못 받는 부품 없음.
- **한 번 고침(첫 캡처):** 선이 레퍼런스보다 훨씬 굵고 번지며 칸 안 채움이 볼록한 베개처럼 보임 → 선 굵기 0.035 → 0.02 m · 번짐 0.07 → 0.025 m · 채움 5 → 2% · 채움 가장자리 0.25 → 0.12 m, 밝기 1.4 → 1 · 번쩍 3 → 2.5. 재질 · 프리팹을 직접 고치고 셰이더 · 부품 기본값도 같게 함.

| 조절 값 | 지금 | 권장 범위 | 효과 |
|---|---|---|---|
| `_LineWidth` · `_GlowWidth`(재질) | 0.02 · 0.025 m | 0.012~0.04 · 0.01~0.06 | 선 굵기 · 번짐 |
| `_Fill` · `_FillEdge`(재질) | 0.02 · 0.12 m | 0~0.06 · 0.05~0.3 | 여덟 칸 안 채움 |
| `_CoreBoost` · `_Shimmer`(재질) | 1.5 · 0.35 | 1~2.5 · 0~0.8 | 심 밝기 · 도는 띠 |
| `overrun`(`F1 Squares`) | 0.3칸 | 0~0.6 | 바깥으로 더 뻗는 길이 |
| `flash` · `intensity` | 2.5 · 1 | 1.5~4 · 0.6~1.6 | 번쩍 · 남은 밝기 |
| `build` · `flashTime` · `stepTime` | 0.1 · 0.15 · 0.08초 | 0.05~0.2 · 0.1~0.3 · 0.05~0.15 | 그려지는 시간 · 번쩍 · 따라가기 |
| `blinkTime` · `blinkRate` · `blinkDim` | 0.2초 · 15 · 0.55 | 0.15~0.3 · 8~20 · 0.3~0.8 | 끝 알림 깜빡임 |
| `runIntensity` · `intensity`(`F1 Blade`) | 2.5 · 1.1 | 1.5~4 · 0.6~1.6 | 칼 빛 달림 · 남은 빛 |
| `width` · `hold` | 0.05 m · 1.1 | 0.03~0.08 · 1.05~1.4 | 칼 빛 굵기 · 칼끝 쪽 밝기 |
| `glint` · `sparkle` · `glintSize` · `sparkleSize` | 4 · 1.4 · 0.4 · 0.16 m | 2~6 · 0.8~2 · 0.25~0.6 · 0.1~0.25 | 칼끝 반짝 |

레퍼런스와 다른 점(사용자가 그대로 두기로 함 — 1 ~ 3 안 고침, 게임 Play에서 보고 다시 정할 수 있음):

1. **밝기:** 캡처 장면이 검은 바탕이라 레퍼런스(판에 박힌 금 상감 같은 가는 선)보다 선이 더 빛나 보인다. 나무판 위에서는 더 밝아 보일 수 있음 → Play에서 보고 정함(밝기 1 → 0.7 · 번짐 0.025 → 0.015 m).
2. **판 줄과 어긋남:** 레퍼런스는 진짜 판 줄을 따라 빛나지만 지금은 킹 중심 · 킹이 보는 쪽에 맞춘 칸이라 판 줄과 조금 어긋날 수 있다(안 B에서 말씀드린 대로) → 판 줄에 맞추려면 경기장 판의 칸 크기 · 원점을 읽어 붙여야 함(다음 단계).
3. **칼 빛:** 캡처에는 칼 모델이 없어 레퍼런스처럼 칼날 홈을 따라 빛나는지 볼 수 없다 → Play에서 보고 굵기 0.05 m · 카메라 쪽 3 cm를 정함.

- 게임에서는 실사 `RealKingCast`의 흰 칼 반짝임(`BladeGlint`, 손잡이 → 칼끝 0.24초) · 발밑 공기 굴절 4방향 · 금빛 조명이 같이 나온다(흰 빛과 금빛이 칼날 위에서 겹침).
- 콘솔의 `VfxRing.cs:101` ArgumentNullException(dest) 9개는 다시 불러올 때 예전 `VfxRing`이 `OnEnable`에서 내는 것으로 F1과 무관(고치지 않음).
- 미러 컴파일(Core · Game · VFX · VFX 빌더 · Gameplay · RagdollLab) · Unity 컴파일(오류 없음, 셰이더 오류 없음) · `Tools/Test-NetworkCompile.ps1` 통과. 시작할 때 VFX 작업대가 Play 중이어서 사용자에게 묻고 멈춤. 게임 Play · 사람 확인 전, 커밋 안 함.

**F5 빗나감 — 칼의 금빛이 알갱이로 흩어짐 (R193, 10-28) — 사용자가 그대로 두기로 함** (사용자 "F1은 그대로 두고 다음 층 F5 빗나감으로 가줘" → 캡처를 보고 "F5는 그대로 두고, 확인은 나중에 게임에서 할게. 다음은 E로 가줘")

- **순간:** `SfFxKind.KingWhiff` — 자세(0.9초) 동안 막을 공격이 안 오면 `Recovery`로 넘어가며 나오는 신호(자리 = 엉덩이). 그때 칼은 0.12초에 자세(`KingG`)에서 칼끝을 판 쪽으로 떨어뜨리고(`KingD`: 오른쪽 조금 · 아래 · 앞) 0.45초까지 그대로, 0.6초에 다시 듦. 어깨가 처지고 고개가 떨어짐. 예전에는 실사 킹이면 아무것도 안 그렸고(실사가 아닐 때 회색 퍼프는 그대로), 이제 실사 킹이면 새 함수 `SwordFightSkillFx.KingWhiffVfx`(`.PieceVfx.cs`)가 엉덩이 아래 바닥에 새 키 `F_Whiff`를 낸다(+Z = 킹이 보는 쪽). F1의 칸 · 칼 빛은 자세가 끝나며 0.06초에 꺼지므로, 킹을 붙잡던 빛이 알갱이로 흩어지는 것처럼 보인다.
- **칼 알갱이(`F5 Motes`):** 칼날 전체(0.75 m, `BladeRoot` → `BladeTip`)에서 칼이 떨어지는 동안 0 · 0.03 · 0.06 · 0.09초에 5개씩(20개). 거의 안 움직이고(0.05 ~ 0.25 m/s, 모든 방향) 공기에 느려지며(끌림 1.5) 조금 흔들리고(노이즈 0.12) 천천히 가라앉음(중력 0.08). 크기 0.019 ~ 0.04 m, 0.5 ~ 0.85초, 끝으로 갈수록 40%까지 작아짐.
- **칼끝 알갱이(`F5 Point`):** 칼끝이 내려온 0.1초에 7개 + 0.4초까지 초당 10개(약 4개), 칼끝 둘레 4 cm에서 나와 떨어지지 않고 맴돎(중력 0). 크기 0.022 ~ 0.043 m, 0.3 ~ 0.6초. F1 칼 빛이 칼끝 쪽이 밝았던 것처럼 칼끝에 더 모임.
- **칼 따라가기:** 게임에서는 두 발생기를 0.45초 동안 매 프레임 칼의 진짜 위치에 붙인다(`F5 Motes` = 칼 가운데, +X가 칼끝 쪽 / `F5 Point` = 칼끝) → 알갱이가 떨어지는 칼을 따라 흘려진다. 첫 프레임 전에도 한 번 붙여서 처음 알갱이가 작업대 자리에서 나오지 않음. 작업대 · 캡처에서는 떨어진 칼 자리(손잡이 쪽 (0.14, 0.72, 0.42)에서 `KingD` 방향으로 0.75 m, 칼끝 (0.29, 0.15, 0.88))에 그대로.
- **색:** 칼을 떠날 때 따뜻한 흰색 → 0.4에 금색 → 끝에 진한 금색, 처음 6%에 켜지고 45%까지 그대로였다가 사라짐. 재질 `M_King_Mote`(`Soft Glow Additive`, 금색과 흰색을 반씩 섞은 옅은 금색 (0.9, 0.74, 0.48) · 밝기 2.2 · 퍼짐 2.5 — F2 · F4 조각 `M_King_Sliver`(밝기 3)보다 어둡고 부드럽게). 안개 · 먼지 · 고리 없음.
- **새 에셋:** 재질 `M_King_Mote`, 프리팹 `25_King_F_Whiff`(층 `F5 Motes` · `F5 Point`, 앵커 Feet, 길이 1초), 메뉴 `ChessFight/VFX/King/Build F5 (whiff only)`(`Build`는 F1 ~ F5 모두), `VfxSets/King`에 새 키 `F_Whiff`. 새 부품 · 셰이더 없음(빌트인 파티클 둘). 이름은 만들기 전에 비어 있는지 확인함(PITFALLS 59). 공용 `SwordFightSkillFx.cs`는 `KingWhiff` case(실사 킹이면 `KingWhiffVfx(e)`)와 그 위 주석 한 줄만 바꿈.
- **캡처:** `VFXCaptures/king/F5/`(플레이어 시점 = F3 카메라 (0, 2.68, −5.53) → (0, 1.3, 0)), `F5_front/`(150° 돌려 앞쪽, 카메라 (0, 1.2, −2.4) → (0, 0.45, 0)), 비교 `F5_compare.png`(왼쪽 위 레퍼런스 `F_05_whiff`, 오른쪽 위 플레이어 시점 0.16초, 왼쪽 아래 앞쪽 0.1초, 오른쪽 아래 앞쪽 0.25초). 캡처 장면에는 킹 · 칼 · 판이 없음.
- **한 번 고침(첫 캡처):** 플레이어 시점에서 알갱이가 1 ~ 2 픽셀로 거의 안 보이고, 식으며 빨간 갈색이 됨 → 크기 0.014 ~ 0.03 → 0.019 ~ 0.04 m(칼끝 0.016 ~ 0.032 → 0.022 ~ 0.043), 칼 알갱이 4 → 5개씩 · 0.4 ~ 0.75 → 0.5 ~ 0.85초, 칼끝 6 → 7개, 색 가운데 (1, 0.72, 0.3) → (1, 0.8, 0.45) · 끝 (0.85, 0.5, 0.18) → (1, 0.66, 0.3). 프리팹을 직접 고치고 빌더 기본값도 같게 함.

| 조절 값 | 지금 | 권장 범위 | 효과 |
|---|---|---|---|
| 칼 알갱이 수(`F5 Motes`, 4번) | 5개씩 | 3 ~ 8 | 흩어지는 양 |
| 칼끝 알갱이(`F5 Point`) | 7개 + 초당 10 | 4 ~ 12 · 0 ~ 20 | 칼끝에 모이는 양 |
| 크기(`startSize`) | 0.019 ~ 0.04 · 0.022 ~ 0.043 m | 0.012 ~ 0.06 | 멀리서 보이는 정도 |
| 삶(`startLifetime`) | 0.5 ~ 0.85 · 0.3 ~ 0.6초 | 0.3 ~ 1.2 | 남는 시간 |
| 중력(`gravityModifier`) | 0.08 · 0 | −0.1 ~ 0.3 | 가라앉음(음수면 떠오름) |
| 노이즈 · 끌림 | 0.12 · 1.5 | 0 ~ 0.3 · 0.5 ~ 3 | 흔들림 · 멈춤 |
| `_Intensity` · `_Falloff`(`M_King_Mote`) | 2.2 · 2.5 | 1.2 ~ 3.5 · 1.5 ~ 4 | 밝기 · 부드러움 |

레퍼런스와 다른 점(사용자가 그대로 두기로 함 — 1 ~ 3 안 고침, 확인은 나중에 게임에서):

1. **떠오름:** 레퍼런스의 알갱이는 불씨처럼 칼 위로 떠올라 킹 옆까지 퍼져 있는데, 지금은 프롬프트("drift down off it")대로 천천히 가라앉는다 → 떠오르게 하려면 칼 알갱이 중력 0.08 → −0.05.
2. **몸 둘레 빛:** 레퍼런스 왼쪽은 킹 몸 둘레에 옅은 금빛 안개가 남아 있는데 지금은 없다(안개 · 먼지 금지, F1 칸 · 칼 빛이 꺼지는 것으로 대신) → 넣는다면 안개 대신 F1 칼 빛이 0.06초가 아니라 0.3초에 사라지게.
3. **작게 보임:** 플레이어 시점(5.5 m 뒤)에서는 알갱이가 몇 픽셀이고, 칼이 킹 몸 앞쪽이라 일부 가려질 수 있다. 캡처에는 칼 모델이 없어 칼에 붙은 모습은 Play에서만 보인다 → Play에서 보고 크기 · 수를 정함.

- 미러 컴파일 · Unity 컴파일(오류 없음) · `Tools/Test-NetworkCompile.ps1` 통과. 게임 Play · 사람 확인 전, 커밋 안 함.

**E 왕의 귀환 — 동작 · 타이밍 (코드 기준, `SwordFightEdgeParams`):**

| 단계 | 시간 | 일어나는 일 |
|---|---|---|
| 조건 | E 누름 | **밀려 떨어지는 중에만**: 떨어지기 시작한 첫 0.6초 안(`fallWindow`), 발판 윗면에서 2 m 아래까지(`fallDepth`), 가장 가까운 가장자리가 3 m 안(`kingReach`). 준비되면 그 가장자리에 옅은 빛 한 줄(실사 `RealGlowEdge`) |
| 칼 박기 | 0.05초 | 신호 `EdgeCast` · `KingStab`(손잡이 자리 · 바깥 방향). 칼이 **가장자리 놋쇠 테 바로 아래 판 옆면**에 마지막 0.3 m를 0.05초에 꽂힘 → 손잡이가 부르르 떨다 멈춤. 실사: 섬광 · 벽이 깨짐(`KingBreach`: 구덩이 · 놋쇠와 나뭇조각 · 금) · 멈춤 · 흔들림 |
| 끌려감 | 0.12초 (`kingCatch`) | 몸이 매달릴 자리(가장자리에서 0.22 m 밖 · 0.42 m 아래)로 끌려감, 빈손은 가장자리를 잡음 |
| 매달림 | 0.4초 (`kingHang`) | 손잡이에 매달려 살짝 흔들림, 마지막 0.12초에 뒤로 젖혀 발바닥을 판 옆면에 댐. **착지할 자리**(가장자리에서 1.5 m 안쪽, `kingLandIn`)는 E를 누를 때 정해지고 모든 화면에 있다. 이때 맞거나 박힌 손잡이를 베이면 → 실패 |
| 뛰어오름 | 0.5초 (`kingVault`) | 신호 `KingVault`(엉덩이 · 안쪽 방향). 칼을 뽑아 튕겨 오름: 길의 42%에서 꼭대기(서 있을 때 엉덩이 높이보다 **1.25 m** 위), 빨리 올라 느려지고 천천히 내려오다 빨라짐, 몸이 −10° → +20° 앞으로 기욺. 내려올 때 칼을 들어 내려찍기 준비 |
| 착지 | 그 순간 | 신호 `KingLand`(착지 자리 · 방향 · 반경 2 m). **내려찍기가 발과 같이 닿음**, 반경 2 m 적 **2 m 밀림**(신호 `KingLandHit`, 맞은 적마다) |
| 후딜 | 0.35초 | 다리로 받아 낮게 → 일어섬 |
| 실패 | 매달림 중 | 맞거나 박힌 손잡이를 베이면 신호 `KingDrop`(손잡이 자리): 칼이 빠지며 떨어짐, 쿨 1초 |

예: E 0초 → 칼 박힘 약 0.05초 → 매달림 0.12~0.52초 → 뛰어오름 0.52초(꼭대기 약 0.73초) → 착지 1.02초 → 후딜 ~1.37초. 쿨 20초.

**E 층 계획 (초안):**

| 층 | 순간 | 모양 (초안) | 프리팹 · 키 | 상태 | 레퍼런스 |
|---|---|---|---|---|---|
| E1 | 칼 박기 (`KingStab`) | 칼이 판 옆면에 박히는 곳에 **금백색 별 섬광**(0.1초) + 깨진 구덩이 둘레의 금(실사 `KingBreach`)을 따라 **금빛이 테두리에서 0.55 m까지 스며 번짐**(0.3초 번지고 0.08초 그대로, 0.45초에 식음), 구덩이 안은 그 빛의 6%만 + **R198: 별 둘레로 튀는 금빛 불똥 40개 · 떨어지는 불씨 7개** | `31_King_E_Stab` · `E_Stab` | **검토**(R196 만듦 → R198 새 그림과 비교해 고침, 아래 "E 새 그림으로 만든 값", 사용자 확인 전) | `E_01_stab.png` |
| E2 | 매달림 → 착지 (`KingStab` ~ `KingLand`, 약 1초) | **착지할 자리에 금빛 선 왕관이 그려짐**: 바닥 고리(0.08 ~ 0.3초) → 세 뿔(0.27 ~ 0.5초) → 가운데 십자가(0.5 ~ 0.62초), 숨쉬듯 밝아졌다 어두워짐, 왕관은 카메라 쪽으로 돎. 박힌 칼 손잡이에 부드러운 금빛. 착지 순간 번쩍하고 0.1초에 사라짐(E4가 이어받음), 떨어지면 0.25초에 꺼짐 | `32_King_E_Mark` · `E_Gather` | **검토**(R199, 사용자 확인 전) | `E_02_mark.png` |
| E3 | 뛰어오름 (`KingVault`) | 칼을 뽑는 순간 구덩이에서 금빛 별 섬광 + 바깥 아래로 튀는 불똥 24개 + 아래로 눌리는 공기 줄 5개 + **엉덩이를 따라 휘어 오르는 금빛 꼬리**(구덩이에서 시작, 0.3초 남음) | `33_King_E_Vault` · `E_Vault` | **검토**(R200, 사용자 확인 전) | `E_03_vault.png` |
| **E4** | **착지** (`KingLand`) | **새 그림으로 다시 만듦**: 바닥에 지름 약 4 m 금빛 킹 문양(예전 `T_King_LandSeal` 그림을 다시 씀)이 가운데부터 0.1초에 펼쳐지며 번쩍 + 발밑 별 섬광 + 허리 높이 가로 렌즈 빛줄 + **휘감아 오르는 금빛 띠 4줄**(반경 1.75 → 1.1 m, 높이 1.7 m까지) | `34_King_E_Land` · `E_Land` (예전 `10_King_E_Land_New` 파일은 남김) | **검토**(R201, 사용자 확인 전) | `E_04_land.png` |
| E5 | 착지에 맞은 적마다 (`KingLandHit`) | 가슴에 금백색 별 번쩍 + 비스듬히 내리그은 짧은 금빛 줄(카메라 쪽으로 돎) + 밀리는 쪽 날카로운 금빛 조각 45개, 고리 없음 | `35_King_E_Hit` · `E_Hit`(새 키) | **검토**(R202, 사용자 확인 전) | `E_05_hit.png` |
| E6 | 실패 (`KingDrop`) | 칼이 빠질 때 빛나던 금이 0.12초에 꺼지고, 구덩이 · 손잡이 자리에서 금빛 알갱이가 떨어짐(8 + 4 + 3개, 손잡이 6개). 왕관 표시도 0.25초에 꺼짐 | `36_King_E_Drop` · `E_Drop`(새 키) | **검토**(R203, 사용자 확인 전) | `E_06_drop.png` |

- (예전 계획) 추천 순서는 E1 → E3 → E2 → E5 → E6, E4는 답 대기였다. **10-28 사용자가 E 그림 6장을 주며 "처음부터 끝까지 너가 테스트 하고 작업 해"** → 비숍 채팅(URP VFX)이 E1 고침과 E2 ~ E6을 층마다 멈추지 않고 한 번에 만듦(R198 ~ R203, 아래 "E 새 그림으로 만든 값"). E4는 그림대로 새로 만듦.
- 실사 벽 깨짐 · 부스러기 · 잔상 · 착지 나뭇조각은 그대로. 먼지 퍼프 금지(10-25).
- **R196 뒤 넘김:** 사용자 10-28 "킹E는 다른 채팅에서 작업 할거야" → "너가 킹 E 스킬 한거까지 한고 다른 채팅에 레퍼런스 사진 올려 놨는데 거기서 작업 이어갈 수 있게 해". E1까지 이 채팅(URP VFX3)이 만들고 커밋했고, **E 나머지(E2 ~ E6)와 E1을 새 그림 `E_01_stab` ~ `E_06`과 비교하는 일은 비숍 채팅(URP VFX)이 맡는다**(레퍼런스는 그 채팅이 `Assets/VFX/Reference/king/`에 넣음, R198부터). 킹 F 층은 그대로 이 채팅 몫.

**E1 칼 박기 — 만든 값 (R196, 10-28, 검토 중 · 새 그림과 비교 전):** 사용자 "F5는 그대로 두고, 확인은 나중에 게임에서 할게. 다음은 E로 가줘" → E4(지금 착지를 그대로 둘지) 답이 없어도 되는 E1부터. 세부 E 그림이 없어 연속 그림 `E_00_storyboard` 2칸(칼 박기 · 매달림)과 위 계획 표만 보고 만들었다.

- **순간:** `SfFxKind.KingStab` — 칼이 판 옆면에 들어가는 순간(실사 `RealKingStab`의 `Land()`, 마지막 0.3 m를 세 프레임에 박은 뒤). 자리 = 손잡이(`SwordFightSkills.Hilt` = 테 + 바깥 0.18 m + 아래 0.12 m), +Z = 판 바깥. `Land()`는 이제 `KingBreach(s)`(실사 벽 깨짐)를 먼저 하고 새 `KingStabVfx(s, at, dir)`(`.PieceVfx.cs`)를 부른다. 이 함수는 `E_Stab`을 손잡이에 낸 뒤 방금 생긴 깨짐(`breaches`에서 그 킹의 아직 박혀 있는 것)을 찾아 별을 칼이 벽면을 지나는 곳(`g.at`) 바깥 6 cm로, 금 빛 부품을 `g.at`(+Z = 벽 바깥)으로 옮기고 그 부품을 깨짐에 건다(`Breach.gild`). 깨짐이 없으면(바닥을 못 찾음) 별만 손잡이 둘레에 나오고 빛나는 금은 없다. `KingVfxOn = false`면 예전처럼 아무것도 안 나옴.
- **별(`E1 Star`):** F2의 `VfxStarFlash` · 재질 `M_King_Star` · 메시 `SM_King_Star`를 다시 씀(코드 · 재질 안 바꿈, 프리팹 값만): 0.1초(`duration`) · 가로 빛줄 0.14초(`streakTime`) · 밝기 3 · 0.8 → 0.35 m · 무작위 돌림 ±20°. F2(칼과 칼, 1.2 m · 0.14초)보다 작고 짧게 — 쇠가 돌에 박히는 한 번의 번쩍. 실사 흰 섬광(`ClashFlash`) · 불빛(`SparkLight`)은 그대로 같이 나옴.
- **금 빛(`E1 Cracks`, 새 부품 `VfxCrackGlow`):** 실사 깨짐 셰이더 `ChessFight/Skill Breach`가 이미 그리는 금(11가닥 · 0.55 m)과 구덩이를 그대로 쓰고, 그 셰이더의 빛(`_Glow`)을 이 부품이 정한다. 깨짐은 매 프레임 `gild.Glow(나이, out 세기, out 앞머리)`를 읽어 `_Glow`(색 = 부품의 `colour`) · 새 `_GlowFront` · 새 `_GlowInside`에 넣는다. 세기: 0.03초에 차오름 → 0.38초까지 그대로 → 0.45초 동안 식음(제곱으로, 0.83초에 0). 앞머리: 구덩이 테두리 안쪽(반지름의 90%)에서 0.3초 동안 0.55 m까지 바깥으로 달림(처음 빠르게) — 그 바깥 금은 아직 어둠. 색 HDR (2.4, 1.56, 0.36). 구덩이 안 깨진 돌은 그 빛의 20%(`inside`). 예전 실사 킹은 빛 0(금이 처음부터 어두움)이었고, 실사가 아닐 때(잉크)의 0.22초 금빛은 그대로.
- **셰이더 `SkillBreach.shader`:** 속성 둘만 더함 — `_GlowFront`(기본 100 = 끝까지, 예전과 같음): 깨지지 않은 돌의 금 빛에 `saturate((_GlowFront − (r − R0 × 0.9)) / 0.06)`을 곱함(r = 벽면에서 구덩이 가운데까지 거리, R0 = 구덩이 테두리 반지름) / `_GlowInside`(기본 1, 예전과 같음): 구덩이 안 빛(`deep²`)에 곱함. 기본값이라 퀸 오브 더 힐 · 실사 아닐 때 · 다른 데서 쓰는 깨짐은 그대로.
- **작업대 · 캡처용 미리보기:** 게임은 진짜 벽을 쓰므로 부품이 `Live()`를 받으면 숨기는 자식 `Preview wall`: 놋쇠색 벽면 · 윗면(`Stone face` · `Stone top`, 새 재질 `M_King_StabStone` = URP Unlit (0.55, 0.42, 0.24) · 양면) 위에 깨짐 셰이더의 벽면 · 윗면 쿼드(`Face` · `Top`, 새 재질 `M_King_StabWall`)를 게임과 같은 크기 · 자리로. 구덩이 0.3 m · 깊이 0.2 m · 금 0.55 m · 위로 0.17 m · 아래로 0.6 m · 속 나무색 (0.45, 0.3, 0.18) 6 cm(놋쇠 테 두께), 게임처럼 3 · 5프레임에 자람.
- **새 에셋:** 부품 `Assets/Scripts/VFX/VfxCrackGlow.cs`(`IVfxSeekable`, 실행 순서 120), 재질 `M_King_StabWall` · `M_King_StabStone`, 프리팹 `31_King_E_Stab`(앵커 Chest, 길이 1.2초, 캡처 높이 0.7 m — 미리보기 벽이 캡처 바닥 위에 오게), `VfxSets/King`의 `E_Stab` 키를 예전 `04_King_E_Stab`에서 새 프리팹으로(예전 파일은 남김), 메뉴 `ChessFight/VFX/King/Build E1 (stab only)`(`Build`는 F1 ~ F5 + E1). 이름은 만들기 전에 비어 있는지 확인함(PITFALLS 59). 게임 코드: `.Breach.cs`(`Breach.gild` · `StepBreach`의 빛 세 줄), `.RealEdge.cs`(`Land()` 두 줄 순서), `.PieceVfx.cs`(`KingStabVfx`).
- **캡처:** `VFXCaptures/king/E1/`(판 바깥에서 벽면 정면, 카메라 (0, 1.25, 2.3) → (0, 0.66, −0.15)), `E1_side/`(옆, (2.1, 1.0, 1.1) → 같은 곳), `E1_top/`(판 위 플레이어 쪽, (0.4, 2.3, −2.2) → (0, 0.8, 0)), 0 · 0.02 · 0.05 · 0.1 · 0.2 · 0.3 · 0.45 · 0.65 · 0.9초. 비교 `E1_compare.png`(왼쪽부터 연속 그림의 칼 박기 · 매달림 칸, 정면 0.02 · 0.2초, 플레이어 쪽 0.2초, 정면 0.65초 식는 중). 캡처 장면에는 킹 · 칼 · 진짜 판이 없고 미리보기 벽만 있음.
- **한 번 고침(첫 캡처):** 정면에서 구덩이 안이 하얗게 타는 용암처럼 보여 너무 화려함 → 셰이더에 `_GlowInside`를 더하고 부품 `inside` 1 → 0.2(호박색으로 달아올랐다가 0.9초에 어두운 나무색).

| 조절 값 | 지금 | 권장 범위 | 효과 |
|---|---|---|---|
| `colour` (`E1 Cracks`) | (2.4, 1.56, 0.36) | 밝기 ×0.6 ~ ×1.5 | 금의 색 · 밝기 |
| `seep` · `reach` | 0.3초 · 0.55 m | 0.1 ~ 0.6 · 0.2 ~ 0.55 | 번지는 빠르기 · 거리 |
| `hold` · `cool` | 0.08 · 0.45초 | 0 ~ 0.3 · 0.2 ~ 1 | 남는 시간 |
| `inside` | 0.2 | 0 ~ 0.5 | 구덩이 안이 달아오르는 정도 |
| 별 `duration` · `startSize` · `intensity` (`E1 Star`) | 0.1초 · 0.8 m · 3 | 0.06 ~ 0.2 · 0.4 ~ 1.2 · 1.5 ~ 4 | 번쩍임 |

레퍼런스(연속 그림)와 다른 점 — 새 그림 `E_01_stab`과의 비교 · 고치기는 비숍 채팅이 함:

1. **불똥:** 연속 그림 칼 박기 칸은 별 둘레로 짧은 금빛 불똥이 튀는데, 지금은 별과 실사 벽 조각뿐이다(F2 `F2 Slivers` 같은 조각을 더할 수 있음).
2. **빛나는 금:** 연속 그림 매달림 칸에는 벽에 빛나는 금이 거의 안 보인다. 지금 금 빛은 계획 표("금빛이 스며 번짐")를 따른 것 → 너무 세면 `colour`를 낮추거나 `reach`를 줄임.
3. **구덩이 안:** 0.5초쯤 호박색으로 달아올라 있다(용암처럼 보일 수 있음) → `inside` 0.2 → 0 ~ 0.1.

- 미러 컴파일 · Unity 컴파일(오류 없음, 셰이더 경고 없음) · `Tools/Test-NetworkCompile.ps1` 통과. 게임 Play · 사람 확인 전. 캡처는 미리보기 벽이라 진짜 놋쇠 테 · 나무 칸 · 칼 · 킹과 겹친 모습은 Play에서만 보인다.
- (위 다른 점 1 · 3은 R198에서 고침 — 아래.)

**E 새 그림으로 만든 값 (R198 ~ R203, 10-28, 비숍 채팅 · 사용자 확인 전 · 커밋 안 함):** 사용자가 붙인 6장(11 ~ 16.png)을 `Assets/VFX/Reference/king/E_01_stab` ~ `E_06_drop`(+ 각 `_alt`)으로 잘라 넣음(git에 안 올라감). E2 ~ E6은 새 빌더 파일 `Assets/Scripts/VFXEditor/KingVfxBuilder.Edge.cs`(`KingVfxBuilder`가 `partial`이 됨), 메뉴 `ChessFight/VFX/King/Build E2 (mark only)` ~ `Build E6 (drop only)`, `Build`는 이제 F1 ~ F5 + E1 ~ E6. 이름은 만들기 전에 비어 있는지 확인함(PITFALLS 59). 비교 판 `VFXCaptures/king/E_compare.png`(레퍼런스 · 작업대 캡처 · 게임 Play 정지 화면).

- **R198 E1 고침:** 그림처럼 별 둘레로 튀는 **금빛 불똥 `E1 Sparks`**(늘어난 알갱이 40개, 원뿔 62°, 2.5 ~ 6.5 m/s, 0.12 ~ 0.35초, 중력 1.2, 재질 `M_King_Sliver`) + **떨어지는 불씨 `E1 Embers`**(7개, 0.5 ~ 0.9초, 중력 0.9) — 둘 다 구덩이 바로 바깥. 구덩이 안 달아오름 `inside` 0.2 → 0.06, 별 `startSize` 0.8 → 1.0 m. (프리팹 `31_King_E_Stab`을 빌더 `StabSparks`로 고침.)
- **R199 E2 착지 예고(`32_King_E_Mark`, 새 부품 `Assets/Scripts/VFX/VfxCrownMark.cs`):** 선은 모두 `VfxPathRibbon`(재질 `M_King_Mark`, 너비 0.04 ~ 0.045 m). 바닥 고리 = 반원 둘(반경 0.62 m, 0.035 m 띄움, 0.08 → 0.3초) · 자식 `Crown` 아래 왕관 반쪽 둘(0.27 → 0.5초) · 십자가 세로(0.5 → 0.58초) · 가로(0.56 → 0.62초). `VfxCrownMark`가 매 프레임 선 밝기를 정함: 0.5초에 걸쳐 밝아짐(`brighten`), 숨쉬기 ±20% · 초당 9 라디안, 착지(`Land()`)면 2.2배 번쩍하고 0.1초에 사라짐, 떨어지면(`Stop()`) 0.25초에 꺼짐, 둘 다 안 오면 1.6초(`liveLimit`)에 스스로 꺼짐. `Crown`은 카메라 쪽으로 위아래 축만 돎(어느 쪽에서 봐도 왕관). 자식 `Hilt glow`(재질 `M_King_HiltGlow`, Soft Glow) = 박힌 손잡이의 부드러운 금빛(0.45 m, 0.12초에 켜짐). 게임: `RealKingStab`의 `Land()`에서 `KingMarkVfx(s)`가 착지할 자리(`EdgeLand` 아래 바닥)에 내고 `Live()` · `Hilt(손잡이)`, `RealKingLand`에서 `KingMarkEnd(s, true)`, `KingDrop`에서 `false`. 예전 `07_King_E_Gather`(소용돌이, 뛰어오를 때)는 키에서 빠짐(파일은 남김).
- **R200 E3 뛰어오름(`33_King_E_Vault`):** 구덩이(칼 뽑는 곳)에 `E3 Burst` 별 섬광(F2 `VfxStarFlash` 다시 씀, 0.12초 · ×2.4 · 0.7 → 0.3 m) + `E3 Spit` 불똥 24개(바깥 아래로, 원뿔 50°) + `E3 Air` 공기 줄 5개(아래로 5 ~ 8 m/s) + `E3 Trail` 꼬리(`VfxPathRibbon`, 재질 `M_King_Trail`, 너비 0.2 m · 끝 가늘게 0.9 · 0.3초 남음 · 밝기 1.8). 게임: `RealKingVault`가 `KingVaultVfx(s)`를 부름 — 그 킹의 깨짐에서 구덩이 자리를 찾아 섬광 · 불똥을 옮기고, 꼬리를 구덩이에서 시작해 0.55초 동안 엉덩이보다 0.2 m 아래를 따라 그림(`Begin` · `Trace`). 작업대 미리보기는 게임과 같은 포물선(`Spring`).
- **R201 E4 착지(`34_King_E_Land`):** `E4 Seal` = `VfxSigil` 바닥 문양(예전 그림 `T_King_LandSeal.png`를 다시 씀, 새 재질 `M_King_Seal`), 0.1초에 펼쳐짐 · 번쩍 4배 0.22초 · 지름 3.7 → 4 m · 1.4초 남고 마지막 55%에 흐려짐 · 돌지 않음. `E4 Flash` 발밑 별(0.13초, 가로 빛줄 0.03초만 — 빛줄이 둘로 겹쳐 보여서) + `E4 Streak` 허리 높이(1.05 m) 가로 렌즈 빛줄(재질 `M_King_LandStreak`, `_Aspect` 8, 0.26초) + `E4 Ribbon 1` ~ `4` 휘감아 오르는 띠(`VfxPathRibbon`, 재질 `M_King_Ribbon`, 반경 1.75 → 1.1 m · 높이 0.05 → 1.7 m · 200° 돎 · 너비 0.06 m · 0.32초 남음). 게임 키 `E_Land`가 예전 `10_King_E_Land_New`에서 새 프리팹으로(예전 파일은 남김).
- **R202 E5 맞은 적마다(`35_King_E_Hit`, 새 키 `E_Hit`):** `E5 Star` 별 번쩍(1.0 → 0.45 m, ×3) + `E5 Streak` 비스듬한 짧은 금빛 줄(`VfxSlash`, 재질 `M_King_Cut`, 0.85 m · 58° · 0.32초) + `E5 Slivers` 조각 45개(원뿔 45°). 게임: `SfFxKind.KingLandHit`에 `KingLandHitVfx(e)` — 맞은 적 가슴에 내고 줄을 카메라 쪽으로 돌린 뒤 가슴에 매달아 같이 밀려감.
- **R203 E6 실패(`36_King_E_Drop`, 새 키 `E_Drop`):** `E6 Motes` 구덩이에서 떨어지는 금빛 알갱이(8 + 4 + 3개, 중력 0.55) + `E6 Hilt` 손잡이 자리 6개(중력 0.7). 게임: `KingDrop`에서 `KingDropVfx(s, e)` — 왕관 표시를 끄고(`KingMarkEnd(false)`), 아직 박혀 있던 깨짐의 금 빛을 꺼뜨림(새 `VfxCrackGlow.Quench()`, 0.12초 · `quench`), 그다음 `E_Drop`.
- **캡처:** `VFXCaptures/king/E2/` ~ `E6/`(E2 (0.9, 1.7, −3.8) → (0, 0.4, 0) · E3 (5, 1.4, 0.8) → (0, 1.2, 0.8) · E4 (2.5, 4.5, −4.5) → (0, 0.6, 0) · E5 (−0.9, 1.2, −2.6) → (0, 0.9, 0) · E6 (2.1, 1.3, 1.1) → (0, 1, −0.15)).
- **캡처를 보고 고침:** 선이 금색이 아니라 크림색으로 하얗게 타 보임 → `M_King_Mark` 등 선 재질의 `_CoreBoost` · `_CoreWidth`를 낮추고 `_GlowFalloff`를 올림 / E4 가로 빛줄이 둘로 보임 → `E4 Flash`의 빛줄 0.03초로 / E5 줄이 옆에서 보면 선 하나로 납작함 → 게임에서 카메라 쪽으로 돌림.
- **게임 Play 시험(AI, 10-28):** SkillNet 편집기 `SwordFight_SkillTest`에서 시험 `SwordFightSkillProbe.Run("edge-king")` · `"edge-king-cut"`, 게임 카메라와 옆 카메라로 정지 화면만(동영상 안 찍음). 결과: 칼 박기 → 매달림 0.4초 → 뛰어오름 → 착지에 적 2명 2 m 밀림 / 베이면 0.39초에 "실패 — 맞아서 떨어짐". **오류 없음**(콘솔에는 예전부터 있던 `PawnHipsHull` 경고뿐). 게임 화면에서 왕관이 왕관으로 읽히고, 꼬리 · 문양 · 띠 · 불똥이 순서대로 나옴. 첫 실행에서만 뛰어오르는 킹의 잔상이 하늘색 얼룩으로 찍혔는데, 다시 두 번 돌렸을 땐 안 나옴 — 편집기가 잔상 셰이더를 처음 준비하는 동안 칠하는 임시 하늘색으로 봄(빌드에서는 없음).

| 조절 값 | 지금 | 권장 범위 | 효과 |
|---|---|---|---|
| E1 불똥 개수 · 빠르기 (`E1 Sparks`) | 40개 · 2.5 ~ 6.5 m/s | 20 ~ 70 · 2 ~ 9 | 튀는 양 · 거리 |
| E2 선 너비 · 밝기 (`E2 …` 선들) | 0.04 ~ 0.045 m · 1.3 | 0.03 ~ 0.07 · 0.8 ~ 2 | 왕관 굵기 · 밝기 |
| E2 `breathe` · `breatheRate` · `landFlare` | 0.2 · 9 · 2.2 | 0 ~ 0.4 · 5 ~ 14 · 1.5 ~ 3 | 숨쉬기 · 착지 번쩍 |
| E2 `glowIntensity` · `glowSize` (`Hilt glow`) | 1.4 · 0.45 m | 0.8 ~ 2.5 · 0.3 ~ 0.7 | 손잡이 금빛 |
| E3 꼬리 `width` · `life` · `intensity` (`E3 Trail`) | 0.2 m · 0.3초 · 1.8 | 0.1 ~ 0.3 · 0.2 ~ 0.5 · 1 ~ 2.5 | 꼬리 굵기 · 길이 · 밝기 |
| E4 문양 `scale` · `flash` · `duration` (`E4 Seal`) | 3.7 → 4 m · 4 · 1.4초 | 3 ~ 4.5 · 2.5 ~ 5 · 1 ~ 2 | 문양 크기 · 번쩍 · 남는 시간 |
| E4 띠 너비 · 남는 시간 (`E4 Ribbon 1` ~ `4`) | 0.06 m · 0.32초 | 0.04 ~ 0.1 · 0.2 ~ 0.5 | 띠 굵기 · 꼬리 길이 |
| E5 별 크기 · 줄 길이 | 1.0 → 0.45 m · 0.85 m | 0.6 ~ 1.4 · 0.6 ~ 1.2 | 맞음 번쩍 |
| E6 알갱이 수 · 중력 | 15 + 6개 · 0.55 · 0.7 | 8 ~ 30 · 0.3 ~ 1 | 떨어지는 양 · 빠르기 |
| E6 `quench` (`VfxCrackGlow`) | 0.12초 | 0.05 ~ 0.3 | 금 빛이 꺼지는 빠르기 |

**R206 E 소리를 VFX에 맞춤 (10-28, 사용자 "sfx도 vfx랑 잘 어울려지게 넣어줘", 커밋 안 함):** 예전에는 박기(`KingStab`) · 뛰어오름(`KingVault`) · 착지(`KingLand`) 세 소리뿐이라 매달림 0.1 ~ 0.5초(왕관이 그려지는 동안)와 E5 · E6이 조용했다. `SwordFightSkillSfx.cs`:
- **E2 왕관 `KingMark`(새 소리, ElevenLabs Sound Effects v2, flow `DyaQN8O8lr1lQQmgKDmb`, 두 후보 중 A):** 낮고 묵직한 에너지가 차오르는 소리(무게 중심 약 60 Hz, 0.4 ~ 0.5초에 가장 크고 0.7초에 사라짐 — 칼 박기 뒤 뛰어오르기 직전과 맞음, 밝은 울림 없음). `KingStab`의 겹 소리(`Layers`)로 같이 시작, 가장자리 소리(`Edge`)라 칼이 베이면 끊김. 크기 0.55. B안은 1.6 kHz 높은 음이 계속 울려 뺌.
- **E5 `KingLandHit` → 킹 반격 맞음 `KingCounterHit`을 다시 씀**(새 `Alias`, 크기 0.6 — `KingLand` 밑에 깔림). **E6 `KingDrop` → 킹 빗나감 `KingWhiff`(칼날 금빛이 떨어짐)을 다시 씀**(크기 0.6), 그 전에 `CutEdge`로 왕관 소리를 끊음. 같은 기물은 같은 소리 계열(메모 "효과음은 기물별로").
- Unity 컴파일 · `Test-NetworkCompile.ps1` 통과. **AI는 소리를 들어 보지 못함**(파형 · 주파수만 봄), 게임 Play 시험은 사용자가 경기장 씬 Play 중이라 못 함. 저음이라 노트북 스피커에서는 작게 들릴 수 있음 → 그러면 크기 0.55 → 0.8.

**R207 다른 기물 새 VFX에도 소리 · 비숍 F 소리 새로 (10-28, 사용자 "다른 기물들도 다 sfx 소리 넣어줘", "비숍 f 기존에 있던 사운드는 지우고 아예 새롭게 해줘", 커밋 안 함):** 새 ElevenLabs 소리 8개(Sound Effects v2, flow `DyaQN8O8lr1lQQmgKDmb`, 쇳소리 · 울림 · 종소리 빼 달라고 씀), `Resources/SwordFightSkillSfx`:

- **비숍 F(X자 베기):** 예전 `BishopFire` · `BishopPin` 파일을 지우고(예전 핀 F의 소리), 크기 표에서도 뺌. `CastBishop`은 내용만 바꿈(GUID 그대로) = 두 칼을 뒤로 젖힐 때 공기가 빨려 드는 바람 소리. F1 두 번째 칼이 생길 때 `BishopDraw`(모이는 바람 — 이 순간은 `SfFxKind`가 없어 `SwordFightSkillFx.BishopVfx.cs` `BishopDraw`에서 새 `SwordFightSkillSfx.Moment`로 부름), 던짐 `BishopLeap`(두 칼이 날아가는 겹 휙), X 엇갈림 `BishopCross`(엇갈리는 휙 + 낮은 공기 쿵), 맞음 `BishopHit`(베는 휙 + 나무 몸 쿵).
- **킹:** F3 반격 베기 `KingCounterSpin`(큰 칼 한 번 묵직한 휙). 막기 밀어냄(`KingRepel`)은 막음 소리가 이미 있어 넣지 않음.
- **나이트:** E1 웅크림 `KnightCrouch`(조여드는 가죽 · 몸 소리, 가장자리 소리라 칼이 베이면 끊김), E4 차기 `KnightKick`(짧은 휙, 맞으면 예전 `KnightKickHit`이 이어짐).
- **퀸:** F 빗나감 `QueenMiss` → 퀸 멈춤 소리 `QueenStop`을 다시 씀(`Alias`).
- 검사(파이썬 · 파형): 1.8 kHz 위에 오래 남는 울림 없음, 무음 컷 없음. `CastBishop` · `KnightCrouch`는 바람 · 숨 같은 소리라 원래 작게 나와 −21 dB로 맞춰 올라감. `Test-NetworkCompile.ps1` 통과. Unity가 Play 중이라 Unity 안 컴파일 · 게임 시험은 못 함, **AI는 소리를 들어 보지 못함**.

**R208 나이트 E 처음 소리 바꿈 (10-28, 사용자 "나이트 E 처음에 소리 바꿔줘", 커밋 안 함):** R207 `KnightCrouch`는 0.6초 내내 같은 크기의 쉿 소리라 0.35초 웅크림과 맞지 않았다. 새 소리(같은 flow, 노드 `rADzBWdAU7zbkrMdzxIH`, 0.5초): 공기를 짧게 빨아들이다 낮고 단단한 쿵으로 조여 듦 — 조용히 시작해 0.2 ~ 0.3초에 가장 크고 0.35초 뒤 빨리 줄어듦(뛰어나감 소리로 이어짐). 끝에 3 kHz 쯤 아주 작은 울림이 잠깐(가장 큰 곳보다 약 −34 dB). 파일만 바꿈(코드 · 크기 0.6 그대로). AI는 듣지 못함.

**R209 나이트 E 시작 소리를 더 멋있게 (10-28, 사용자 "나이트 e 시작 소리 멋있는 걸로 바꿔줘", 커밋 안 함):** R208 소리는 조용하고 얇았다. 새 `KnightCrouch`(같은 flow, 노드 `rkL63Cq18H96EFcA53KE`, 0.6초): 낮게 몰아치는 공기 압력 + 묵직한 쿵 — 시작부터 크고(300 ~ 600 Hz 저음이 몸통), 0.3초 뒤 빠르게 줄어듦. 쇳소리 · 밝은 울림 없음(3 kHz 쯤 작은 성분만). 후보 A(`O35rCyAun7rke0YNM7I8`, 차오르는 저음 → 쿵)는 요청 제한(429)으로 못 만들었고 노드만 남아 있음. 파일만 바꿈. AI는 듣지 못함.

**R210 나이트 E 시작 소리에서 목소리 뺌 (10-28, 사용자 "저건 음성 사운드잖아 sfx소리로 바꿔줘", 커밋 안 함):** R209 소리("전사가 힘을 모으는")는 사람 목소리처럼 들렸다 — 파형에서도 337 Hz에 아주 뚜렷하고 일정한 음높이(주기성 0.80)가 있었다. 새 `KnightCrouch`(같은 flow, 노드 `sIBqLcBmTJTmf2UaypYi`, 0.6초): "목소리 · 숨소리 · 기합 없음, 공기 압력이 차오르다 깊은 저음 쿵, 합성음 + 바람"으로 다시 씀. 주기성 0.51(음높이 401 Hz, 찾는 범위 끝) = 목소리보다 바람 · 충격 쪽. 앞으로 소리 프롬프트에 "no voice, no vocals, no breathing, no grunt"를 넣고, 사람 · 전사 · 몸 같은 말을 빼는 것이 안전하다. 파일만 바꿈. AI는 듣지 못함(음높이만 잼).

레퍼런스와 다른 점(남김, 사용자가 게임에서 보고 정함):

1. **E1 구덩이 안**이 아직 살짝 호박색이다(`inside` 0.06 → 0이면 없어짐).
2. **E2 왕관**이 시험 씬의 밝은 회색 판 위에서는 크림색 · 가늘게 보인다(더하기 빛이라 밝은 바닥에서 하얘짐). 그림은 어두운 나무판 위 진한 금색. 진짜 체스판 경기장에서 다시 봐야 함 → 너무 약하면 선 너비 0.06 m.
3. **E3 꼬리**는 그림처럼 크게 휘는 호가 아니라 몸이 실제로 오르는 길을 따라가서, 게임 카메라(킹 뒤)에서는 거의 곧은 빛기둥으로 보인다. 옆에서는 휜다. 머리 쪽이 처음에 굵고 하얗다.
4. **E4 문양**은 그림의 왕관 문양과 같지만 그림보다 조금 덜 진하다(밝은 판 위).
5. **E6**은 알갱이와 금 빛이 꺼지는 것뿐이라 수수하다(그림의 칼날 금빛은 칼 모델이라 손대지 않음).
6. 예전 프리팹 `04_King_E_Stab` · `05` · `06` · `07_King_E_Gather` · `10_King_E_Land_New`는 지우지 않고 남김. 귀환 키 `E_Return`(08)은 그대로.

### 킹 레퍼런스 이미지 프롬프트

이미지 AI에 그대로 붙여 넣는다. **공통 머리말 + 순간별 문장**을 이어 붙인다. 만든 이미지는 표의 파일 이름으로 `Assets/VFX/Reference/king/`에 넣는다(채팅에 붙이면 AI가 찾아 넣음). 한 순간에 2~3장 뽑아 제일 가까운 것을 고른다. **F_00 · E_00 연속 그림을 먼저 뽑고**, 그 그림을 참고 이미지로 넣어 나머지를 뽑으면 장면끼리 어긋나지 않는다 — 그때는 머리말 둘째 문장으로 `Match the board, lighting, chess pieces and gold light of the attached reference image.`를 넣는다. 4칸 연속 그림이 필요하면 비숍 절의 "(선택) 4칸 연속 그림" 문장을 덧붙인다.

**공통 머리말 (F · E 모두):** 비숍 R178에서 배운 대로 물결(ripples)은 처음부터 금지 목록에 넣었다. 호(arcs)는 넣지 않았다 — 반격 초승달 · 뛰어오름 꼬리가 휜 선이라서.
```
Stylized game VFX concept frame, Unreal Engine Niagara quality. Setting: a giant polished wooden chessboard arena (walnut and maple squares, thin brass trim along the board edge, a dark drop beyond the edge), dim warm evening light so the effect reads clearly. Characters are toy-like wooden chess pieces with simple rounded arms, white gloved hands and short sturdy legs. The king is an ivory-white wooden chess king, the tallest piece, with a carved crown topped by a small cross; he wields one heavy royal greatsword of plain polished steel with a long straight double-edged blade and an ornate crossguard. The opponent is a dark ebony wooden chess piece. Effect color: deep antique gold light (#C9952F) with a hot warm-white core (#FFF4D6) and pale gold highlights (#F1D48A), calmer and deeper than bright yellow gold; no purple, no blue, no rainbow colors, no fire or flames, no lightning, no smoke or dust clouds, no rings, ripples or circular shockwaves, no blood, no cartoon outlines, no text, no UI. 16:9, one single frame, sharp focus, the effect is the subject.
```

**F_00 · F 전체 연속 그림 (`F_00_storyboard.png`)** — 공통 머리말의 "one single frame"을 빼고 붙인다:
```
Show the whole skill as a 6-panel storyboard in two rows of three, the same camera in every panel (behind and to the right of the king, 3 m back and slightly above, looking past him at the ebony opponent 2 m in front of him). Panel 1: the king snaps into his guard, the greatsword held level across his front; a line of gold light runs along the blade from the hilt to the tip. Panel 2: he holds the guard as he steps forward, the blade glowing softly, a faint gold light close around him. Panel 3: the opponent's sword strikes the king's blade and is stopped dead: a hot white-gold star flash where the blades meet, sharp gold slivers thrown back at the attacker. Panel 4: a split-second beat: the king has drawn the greatsword up high over his right shoulder, light gathering along its edge. Panel 5: he cuts once, a huge diagonal stroke from high on his right to low on his left across his whole front, leaving a wide crescent of gold slash light in the air. Panel 6: the opponent is thrown back with a brief gold diagonal slash mark across his body and gold slivers flying the way he is pushed, while the crescent fades.
```

**F1 · 자세 · 받아내기 — 안 A 빛 방패막 (`F_01_guard_a.png`):**
```
Camera: front three-quarter view at chest height, 3 m from the king. He has just snapped into his guard: the greatsword held level across his front at chest height, its tip pointing to his left, his free gloved hand bracing the flat of the blade. A line of gold light has just run down the blade from the hilt to the tip, and the tip glints warm-white. Close around him, a faint translucent shell of gold light made of fine upright threads of light wraps his front, sides and back like a ward, open above his head; it is barely visible except where its edges catch the light.
```

**F1 · 안 B 킹의 여덟 칸 (`F_01_guard_b.png`)** — 안 A의 마지막 문장(`Close around him, …`)을 이것으로 바꾼다:
```
On the board around his feet, thin crisp lines of gold light outline the square he stands on and the eight squares around it, the squares a king may move to: a flat 3 by 3 grid about 3.6 m across centred on him, lying on the wood.
```

**F2 · 막음 (`F_02_parry.png`):**
```
Camera: side view at waist height, 3 m away, the king on the right and the ebony opponent on the left. The opponent's sword has just struck the king's guard and is stopped dead: exactly where the two blades meet, a hot white-gold star flash with short sharp rays, a thin flat streak of light across the point of contact, and a spray of small sharp gold slivers thrown back toward the attacker. The king stands firm and unmoved; the opponent's blade and arm are knocked back.
```

**F3 · 반격 베기 — 핵심 (`F_03_counter.png`):**
```
Camera: low three-quarter view from the front-left of the king, 4 m away, the ebony opponent between us and him. A split second after his block, the king has whipped the greatsword up over his right shoulder and cut once: a single huge diagonal stroke from high on his right down to low on his left, across his whole front. The stroke leaves a wide crescent of gold slash light hanging in the air, reaching about 2.5 m out in front of him, thick and bright in its middle, crisp and thin at both ends, with a hot warm-white core; the blade is at the low end of the crescent, still moving.
```
같은 순간 플레이어 시점(`F_03_counter_player.png`): 첫 문장을 `Camera: behind the king, 3 m back and slightly above, looking past him at the opponent 2 m in front of him.`로 바꾼다.

**F4 · 맞음 (`F_04_hit.png`):**
```
Camera: side view of the opponent. The ebony opponent has just been struck by the king's diagonal cut and is thrown backward off his feet, away from the king. Across his body a bright gold diagonal slash mark, from high on one side to low on the other, glows for an instant on the surface of the wood, with a spray of small sharp gold slivers flying the way he is pushed and a few thin straight streaks of displaced air behind him.
```

**F5 · 빈틈 (`F_05_whiff.png`):**
```
Camera: front three-quarter view, 3 m from the king. His guard has come to nothing: no blow came, and the tip of his greatsword has dropped to the board, leaving him open for a moment. The gold light on the blade breaks up into a few fading gold motes that drift down off it, and the faint light around him goes out.
```

**E_00 · E 전체 연속 그림 (`E_00_storyboard.png`)** — 공통 머리말의 "one single frame"을 빼고 붙인다:
```
Show the whole skill as a 6-panel storyboard in two rows of three, the same side camera in every panel (6 m away, level with the board's edge; the board's brass-trimmed edge and the dark drop beyond it on the left, the board stretching to the right, an ebony opponent standing on it 1.5 m in from the edge). Panel 1: the king, knocked off the board, falls past the edge, reaching out with his greatsword. Panel 2: he drives the sword into the thick side of the board just under the brass trim: a hot white-gold star flash where the steel goes in, gold light seeping into the cracks round the blade. Panel 3: he hangs from the buried sword's hilt below the edge, feet against the side of the board; up on the board, 1.5 m in from the edge, a mark of gold light shaped like the king's crown draws itself in where he will land. Panel 4: he tears the sword out and vaults high over the edge, a bright gold trail curving up behind him from the broken spot. Panel 5: he comes down on the marked spot in a heavy downward chop: the mark bursts into a flat seal of gold light on the board with a long thin horizontal streak of light across it. Panel 6: the opponent beside him is thrown back with a gold flash on his body while the light on the board fades.
```

**E1 · 칼 박기 (`E_01_stab.png`):**
```
Camera: low side view at the board's edge, 3 m away, looking along the thick side of the board below the brass trim, the dark drop below. The falling king has just driven his greatsword deep into the side of the board just under the brass trim: where the blade goes in, a hot white-gold star flash with short sharp rays; round the blade a small crater of torn brass and split wood, its cracks glowing with gold light that seeps outward along them; a few chips falling away. The hilt sticks out of the side, his gloved hand gripping it.
```

**E2 · 매달림 · 착지 예고 (`E_02_mark.png`):**
```
Camera: from out over the drop, 3 m from the edge, looking back at it and slightly down onto the board. The king hangs below the edge from the hilt of his greatsword buried in the side of the board, his free hand on the brass trim, feet against the side, about to spring; the buried blade glows softly gold. Up on the board, 1.5 m in from the edge, a mark of thin gold light is drawing itself in on the wood where he will land: the king's crown with a small cross, about 1.5 m wide, brightening as it forms.
```

**E3 · 뛰어오름 (`E_03_vault.png`):**
```
Camera: side view, 5 m away, the board's edge and the dark drop on the left. The king has just torn his greatsword out of the side of the board and vaulted up over the edge, body rising high, sword raised for a downward chop. From the broken spot below him a short burst of gold light and a few chips are spat out, a few thin streaks of displaced air are pressed down beneath him, and a bright gold trail curves up from the broken spot along his path, hot white in its core and crisp at its edges, fading to nothing about a metre behind him.
```

**E4 · 착지 — 새로 만들 때만 (`E_04_land.png`):**
```
Camera: high three-quarter view, 5 m up and 5 m back. The instant the king lands on the board 1.5 m in from the edge in a heavy downward chop, his blade striking the wood as his feet land: under him a flat seal of gold light bursts open on the board, about 4 m wide, the king's crown with a small cross at its centre, a hot white flash at his feet, a long thin horizontal streak of light across the frame at chest height, and thin spiralling ribbons of gold light rising from the rim of the seal. The ebony opponent standing 1.5 m from him is thrown back.
```

**E5 · 착지에 맞음 (`E_05_hit.png`):**
```
Camera: side view of the opponent, who stood 1.5 m from where the king landed. The ebony opponent has just been hit by the king's landing chop and is thrown backward off his feet, away from the king. A bright white-gold flash bursts on his body where he was struck, a crisp short streak of gold light slants down across him, and a spray of small sharp gold slivers flies the way he is pushed.
```

**E6 · 실패 (`E_06_drop.png`):**
```
Camera: side view at the board's edge. An enemy's sword has struck the hilt of the king's greatsword buried in the side of the board below the brass trim: the sword is knocked loose and the king drops away into the dark below. The gold light on the blade and in the cracks goes out, breaking into a few fading gold motes that fall with him.
```

---

## 기존 기물 (이전 방식, 그대로 둠 — FX7)

| 기물 | 상태 | 비고 |
|---|---|---|
| 킹 | 새 방식으로 다시(R180) · F3 반격 베기 사용자 확인(R183, 캡처) · F2 막음 별 섬광 · F4 맞음 베인 자국 그대로 두기로 함(R185 · R187, 캡처) · F1 자세 안 B 여덟 칸 그대로 두기로 함(R190, 캡처) · F5 빗나감 금빛 알갱이 그대로 두기로 함(R193, 캡처) · E1 칼 박기 별 섬광 + 금 빛 만듦(R196, 캡처, 검토) → E는 비숍 채팅이 이어감 | 위 "킹" 절. 게임 자세(`F_Guard`)는 새 `21_King_F_Guard`, 반격(`F_Counter`)은 새 `23_King_F_Counter`, 막음(`F_Block`)은 새 `22_King_F_Parry`, 맞음(`F_Hit`, 새 키)은 `24_King_F_Hit`, 빈틈(`F_Whiff`, 새 키)은 `25_King_F_Whiff`, 칼 박기(`E_Stab`)는 새 `31_King_E_Stab`, 나머지(이전 방식 `01 · 05~08`, 착지 `10_King_E_Land_New`)는 새 층이 생길 때까지 그대로. README §9 |
| 룩 | 완료(이전 방식) | R161 · R165 |
| 퀸 | 완료(이전 방식) | R162 Fire Beam 시안 → R164 검정 · 짙은 빨강 |
| 나이트 | F5 포크 검토(R179) · E3 공중제비 검토(R182, 1·2번 고침), 나머지 대기 | 위 "나이트" 절. 실사 연출(이전 방식) 위에 새 방식 층을 얹음 |
| 폰 | 대기 | 사용자가 고르면 비숍처럼 층 계획부터 |

## 시안 4종 (Niagara 콘셉트) — 공용으로 만들 때

어느 기물에 넣을지는 사용자가 정한다. 공용으로 만들면 `Prefabs/Common/`에서 시험한다. 이미 일부 층을 기물에 참고한 것은 비고에 적었다(그 층의 완료가 아님).

| 시안 | 층 | 상태 | 비고 |
|---|---|---|---|
| fire_beam | 1 빔 코어 · 2 빔 외곽 불꽃 흐름 · 3 시작점 스타버스트 플래시 · 4 스파크 파티클 · 5 끝점 글로우 | 대기 | R162 퀸에 빔 · 스타버스트를 썼다가 R164에 바꿈 |
| blood_slash | 1 Crimson Slash Arc · 2 Viscous Blood Trail · 3 Directional Splatters · 4 Atmospheric Blood Mist | 대기 | |
| cosmic_coil | 1 Spiral Ribbon Mesh · 2 Starlight Particles · 3 Cylindrical Energy Core · 4 Ground Runes | 대기 | |
| vengeful_twilight | 1 Ground Runes · 2 Conical Light Shafts · 3 Ethereal Motes · 4 Shockwave Distortion · 5 Phantom Remnants · 6 Golden Smoke Tendrils · 7 Anamorphic Lens Flare · 8 Radial Flash · 9 Sub-surface Bloom · 10 Rising Embers | 대기 | R168 킹 착지에 1 · 6 · 7을 합쳐 씀. **5 Phantom Remnants는 캐릭터 모델이 필요 → 착수 전에 사용자 확인** |

시안 이미지 4장(`fire_beam` 등)은 아직 `Assets/VFX/Reference/`에 없다.
