# GPT 작업 프롬프트 — F 대무도회장 / J 왕궁 세탁장 (B 동관 R503과 같은 방식)

아래 "공통 프롬프트"에 "F 전용" 또는 "J 전용" 블록을 붙여서 GPT(Codex) 채팅 하나에 하나씩 준다.

---

## 공통 프롬프트

```
너는 ChessFight(Unity 6000.3.11f1, URP) 저장소에서 폰 러시 중반 미니게임 하나의 아트를 처음부터 끝까지 다시 만든다.
B 보수 중인 동관(R503)에서 쓴 방식을 그대로 따른다.

## 먼저 읽을 것 (순서대로)
1. 저장소 루트 CLAUDE.md, HANDOFF.md 전체 (§1 표의 "미니게임 구조"·"B 동관 아트(R503)" 행이 지금 규칙이다)
2. ArtSource/PawnRush/MiniGames/Mid/Materials/README.md 전체 — B를 만든 과정·판단·실수와 고친 방법이 다 있다. 이 문서가 작업 설명서다.
3. Docs/PawnRush/PAWN_RUSH_ART_PHASES.md (2단계 아랫궁 기준), Docs/PawnRush/PAWN_RUSH_MINIGAME_MODELING.md 의 내 미니게임 절
4. 내 미니게임 게임 코드(아래 전용 블록)와 MiniGameSectionBuilder.cs, MiniGamePalaceArt.*.cs
5. B가 쓴 스크립트(복사해서 고쳐 쓴다): ArtSource/PawnRush/MiniGames/Mid/ 의
   b_center_gate_v1.py, b_wall_arch_v1.py, b_core_scene_v1.py, b_hall_v1.py, b_upper_v1.py, b_shell_v1.py,
   b_install_unity.py, b_room_preview.py, Materials/r503_material_swatches.py
   코드: Assets/Scripts/PawnRush/MiniGameModels.cs 의 RepairWing* 함수들, MiniGameFixtures.cs

## 절대 규칙
- 사용자에게는 한국어로 답한다. 코드 주석·커밋 메시지는 영어.
- git commit / push 금지(사용자가 명시적으로 시킬 때만).
- 사용자가 Unity에서 직접 본 것만 "확인됨"으로 적는다. AI 렌더·컴파일은 "AI 관찰", "Unity 미확인".
- 게임 규칙·판정·충돌·구간 입구(z 0, x −6~6)·출구(z 36, x −6~6) 크기와 위치는 바꾸지 않는다.
  그 밖의 Unity 건축 구조(R312 셸의 벽·천장·창·깃발 등)는 모두 치우고 모델로 다시 세워도 된다.
- 모델은 그림일 뿐이다. 게임 오브젝트의 그림만 끄고(Renderer.enabled = false) 충돌·SetActive·컴포넌트는 그대로 둔다.
  새 모델 연결은 MiniGameModels.Apply에 내 게임 분기를 추가하는 방식(B의 RepairWing과 같은 구조). 다른 미니게임 코드는 건드리지 않는다.
- 불빛(초·불꽃·빛나는 유리·랜턴 빛)과 흔들리는 천(커튼·깃발·먼지막이)은 Tripo/GPT 에셋으로 만들지 않는다.
  모델에는 SOCKET_Flame / SOCKET_Light / SOCKET_Lantern 빈 오브젝트만 두고, 불꽃·Light·흔들리는 천은 MiniGameFixtures(코드)로 붙인다.
  묶이거나 말려서 움직이지 않는 천 소품은 모델이어도 된다.
- 창에는 유리를 넣지 않는다(창밖이 보여야 함). 채움 벽에는 창 자리 구멍을 낸다.
- 벽은 빈틈없이 이어진다: 모듈은 간격보다 3 % 길게 겹치고 하나 건너 1 cm 뒤로(깜빡임 방지), 모듈 위 남는 높이는 같은 재질로 천장까지 채운다.
  난간도 끝 기둥끼리 겹쳐 한 줄로. 같은 평면에 두 면이 겹치지 않게(깜빡임) 1 cm 떼거나 모서리로만 만나게 한다.
- 장식 금속은 무광 황동, 기능 표시(게임이 쓰는 금)만 밝은 금. 팀색은 팀 문·팀 깃발에만.
- Windows. `python`은 동작하지 않는다 → Blender 5.2 내장 파이썬으로 실행:
  "C:/Program Files/Blender Foundation/Blender 5.2/blender.exe" -b --python <script> -- [옵션]
- 검사: Tools/Test-NetworkCore.ps1, Tools/Test-NetworkCompile.ps1 -SteamRuntimeSources Library/PackageCache/com.rlabrecque.steamworks.net@6fb66c768572/Runtime

## 작업 순서 (B와 같음). 각 단계가 끝나면 사용자에게 결과(렌더 이미지)를 보여 주고 "확인"을 받은 뒤 다음으로 간다.
1. 컨셉 시트: 이미지 생성 도구로 Docs/PawnRush/ConceptArt/<R번호>_<이름>/ 에 3장
   (01 전경 / 02 건축 모듈 / 03 소품·재질), 스타일은 Docs/PawnRush/ConceptArt/R502_DrawbridgeSheets/ 3장과
   Docs/PawnRush/ConceptArt/R503_RepairWing/ 3장을 첨부해 맞춘다. 옛 컨셉 Docs/PawnRush/ConceptArt/R311/ 의 내 그림도 첨부.
   프롬프트는 같은 폴더 PROMPT.md 로 남긴다(B의 R503_RepairWing 프롬프트 참고).
2. 재질 견본: Materials/r503_material_swatches.py 방식으로 내 방의 재질(바닥·벽·나무·금속·천 등)을 구워
   컨셉 03 시트의 재질 줄과 나란히 비교(swatch_compare.png). 4D 원환 노이즈로 타일 이음새 없게.
3. 에셋 묶음 시트: B의 Docs/PawnRush/ConceptArt/R503_RepairWing/AssetRefs/ 처럼 흰 배경·한 줄·같은 3/4 시점·
   글자/숫자/화살표/깃발/문장/캐릭터/팀색/유리/불빛/걸린 천 없음. style_reference.webp 를 첨부.
   시트마다 "Tripo 권장 면 수: 시트 전체 약 N" 한 줄과 근거(부품 수·게임 반복 수·손질 뒤 목표의 3~5배)를 사용자에게 함께 준다.
   묶음 순서는 B처럼: 핵심 장면(게임 장치) → 홀 장식(조명 몸체·문·바닥 띠) → 위층·천장·아치 → 벽.
4. 사용자가 Tripo FBX를 주면: ArtSource/PawnRush/MiniGames/Mid/Source/<R번호>_*.fbx 로 복사(Downloads 원본은 그대로),
   위에서 본 배치로 에셋을 나누고(z 위, 바닥에 흩어져 있음 — b_core_scene_v1.split 참고, x만으로 나뉘지 않으면 y로도 나눌 것),
   부품별 표(지금 삼각형 / 게임 크기·반복 수·움직임 / 목표 / 늘리기·줄이기·그대로)를 사용자에게 먼저 보고한다.
   줄이기는 Blender decimate, 늘리기는 Tripo 재내보내기 요청. 금색 판별·분류 실수로 부품이 섞이지 않았는지 렌더로 꼭 확인.
5. 손질·배치 스크립트: 게임 좌표(MiniGameSectionBuilder·내 게임 코드의 숫자) 그대로 모델을 맞추고 Export/*.fbx 로 내보낸다.
   대문(북쪽 가운데 막힌 덩어리 x −10~10, z 24~30, 닫힌 문 자리 z 23.69), 팀 문 아치(x ±12, z 23.8, 팀 문 4 × 4.6 m가 올라감),
   옆벽(x ±22.2), 입구/출구 아치(안쪽 12 m 이상), 위층·천장(13 m), 채움 벽(b_shell_v1), 바닥 재질.
6. Unity 설치·연결: b_install_unity.py 를 복사해 내 FBX·텍스처·재질을 넣고(기존 .mat 덮어쓰지 않음, 결정적 GUID .meta),
   MiniGameModels 에 내 분기를 추가한다. 셸은 Hide(shell) 하고 모델+채움 벽+바닥 재질로 대신한다.
   옛 R312 장식 중 모델이 대신하는 것(금 테두리, 정적 천 판, 옛 소품)은 이름으로 골라 그림만 끈다.
7. 방 전체 미리보기(b_room_preview.py 방식)로 빈틈·겹침·비틀림·창밖·천장(위에서도 닫힘)을 확인하고 렌더를 보여 준다.
8. 마무리: 팀 깃발(MiniGameFixtures.Pennant), 간판(B의 DressRepairWingSigns), 바닥 금 테두리 정리.
9. SFX·VFX는 사용자가 따로 지시할 때까지 하지 않는다.
10. 매 단계: 컴파일·Core 검사 → 내 README(ArtSource/PawnRush/MiniGames/Mid/<게임>/README.md 또는 Materials/README.md 의 새 절)에 기록,
    끝나면 HANDOFF §1 표에 내 행, Docs/Project/REQUIREMENTS.md 에 내 R 행, PAWN_RUSH_ART_PHASES.md "미니게임 아트 현재" 칸을 갱신.
    다른 채팅도 같은 문서를 고치므로 내 줄만 작게 추가한다. R 번호는 HANDOFF 첫머리의 "다음 빈 요청 번호"를 쓰고 그 줄을 갱신한다.

먼저 1~4번 문서를 다 읽고, 내 미니게임의 게임 좌표(판정 물체 이름·크기·위치)를 표로 정리해 보여 준 다음 1단계(컨셉 시트)로 들어가라.
```

---

## F 전용 블록

```
## 내 미니게임: F · 대무도회장 (중반, 슬롯 4, 2단계 아랫궁)
- 게임 코드: Assets/Scripts/PawnRush/MgEnPassant.cs (PawnRushMissions.EnPassant)
- 옛 컨셉: Docs/PawnRush/ConceptArt/R311/06_mid_F_ballroom.png
- 규칙 요약(PAWN_RUSH_MINIGAME_MODELING.md "F · 대무도회장"): 상대 팀색 태엽 무용수 폰 16개, 레인마다 1.1 m 칸 3개·두 칸 스텝,
  쪽마루(메이플·호두나무) 3칸 레인, 발밑 금 고리(기능 금), 위쪽 스포트라이트(빛은 코드), 잡힌 무용수가 앉는 벨벳 벤치, 거울 팀 문.
- 지킬 것: 무용수 팀색은 그 스테이션의 상대 팀색(파랑 팀 구역 표적은 주황, 주황 팀 구역은 파랑). 실시간 거울 반사·두 벌로 보이는 반사는 만들지 않는다.
  무용수의 스텝 이동·몸 회전을 장식 치마가 가리지 않게 몸체·의상·태엽을 나눈다. 모든 무용수를 동시에 밝히지 않는다.
- 2단계 공통: 크림 대리석 상벽 + 호두나무 판벽 허리, 샹들리에·벽등(불빛은 코드), 미니게임 단 짙은 청회색. B와 같은 집의 다른 방이라 B의 재질 견본·벽 모듈·채움 벽 텍스처를 재사용해도 된다.
- 무도회장 고유: 쪽마루 무대, 거울(빛 반사 없이 거울 틀만 모델·면은 은은한 회색 재질), 벨벳 벤치, 악보대, 코트 걸이, 음료 쟁반, 화분 나무.
  커튼·휘장은 걸린 천이라 코드(MiniGameFixtures의 SwayingCloth 활용).
```

## J 전용 블록

```
## 내 미니게임: J · 왕궁 세탁장 (중반, 슬롯 4, 2단계 아랫궁)
- 게임 코드: Assets/Scripts/PawnRush/MgKnightPads.cs (PawnRushMissions.KnightPads), 경로는 KnightRoute
- 옛 컨셉: Docs/PawnRush/ConceptArt/R311/07_mid_J_laundry.png
- 규칙 요약(PAWN_RUSH_MINIGAME_MODELING.md "J · 왕궁 세탁장"): 칸 2.05 m 기준 경로 d1→b2→a4→c5→e4→g5→h7→f8, 큰 나무 빨래통 8개
  (금 고리·1~8 숫자는 게임 기능 표시), b2·a4·c5·e4·g5·h7에 상대 팀색 인형 6종(폰·비숍·나이트·룩·퀸·킹), 통 가운데 방향 표시 없는 거품 분출구,
  얇은 비눗물 수면(떨어지는 곳), 수문 옆 팀 문. MiniGameSectionBuilder가 이 게임에 IntendedGap(점프 경로)을 둔다.
- 지킬 것: 인형 팀색은 그 스테이션의 상대 팀색. 통 8개의 위치·번호는 게임 좌표 그대로(그림의 번호가 아니라). 거품은 통 가장자리·수조 가장자리에만.
  같은 경로의 점프 가능성을 그림으로 바꾸지 않는다. 주방과 구별되게 구리·큰 증기보다 흰 타일·거품·흰 시트를 강조.
- 물: 실내 비눗물만(2단계 규칙). 수면은 Unity 물 셰이더/재질(B는 물이 없었음 — 1단계 C 도개교 세션의 PR_MoatWater.shader 참고, 색은 비눗물 흰빛).
  거품·물 튀김·증기는 VFX라 지금은 만들지 않는다(사용자 지시 대기).
- 2단계 공통: 크림 대리석 상벽 + 호두나무 판벽 허리, 샹들리에·벽등(불빛은 코드), 미니게임 단 짙은 청회색. B의 재질 견본·벽 모듈·채움 벽 텍스처를 재사용해도 된다.
- 세탁장 고유: 흰 타일 수조 벽, 빨래 바구니, 개어 둔 시트, 빨래판, 비누 상자. 높은 빨랫줄의 흰 시트는 걸린 천이라 코드(SwayingCloth), 점프 경로·카메라 시야를 가리지 않게.
```
