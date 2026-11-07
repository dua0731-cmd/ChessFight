# H 국왕 체스 홀 — 다른 방 에셋 재사용 목록 (R510)

사용자 지시(11-08): "다른 채팅에서 썼던 애셋들 있을 거야 그런것도 겹치면 코덱스에 신청 안하고 써도 돼".
H는 G 행차 회랑과 같은 3단계(윗궁)라 대부분을 G에서 가져온다. G 세션이 알려 준 위치와 주의점을 함께 적는다.

## 가져다 쓰는 것 (Codex·Tripo 요청 안 함)

| H 부품 (컨셉 시트 번호) | 가져올 에셋 | 메모 |
|---|---|---|
| 옆벽·기둥·높은 창 (H_02 #7) | `G_WallBayWindow`·`G_WallBayPlain`·`G_WallColumn` (12 m) | 칸은 기둥 간격에 맞춰 가로로 늘려 이어 붙임(G 방식). 커튼은 코드 천 |
| 천창 (H_02 #8) | `G_SkylightVault` | 반쪽 원통, 한쪽을 x로 뒤집어 맞댐 |
| 입구 계단 (H_02 #9) | `G_StairsCenter`·`G_StairsRamp` | 맨 윗단 0.4 m = `Arena.DaisHeight` |
| 뒤 벽 왕실 문 (H_02 #4) | `G_CenterGate` | 남색 문·왕관. 정문 벽 뒷선은 팀 판 끝(z 24) 뒤로(G 세션 주의) |
| 팀 문 닫힘/열림 (H_02 #5·6) | `G_TeamGateFrame`·`G_TeamGateDoor`·`G_TeamGateCrest` | 문짝 슬롯 `PR_Late_G_GatePaint`를 팀색으로 칠함 |
| 위층 난간 | `G_Balustrade` | |
| 벨벳 줄 기둥 (H_03 #10) | `G_StanchionPair` | |
| 킹 조각상 (H_03 #1) | `G_King` (1.7 m 상아 대리석) | 0.9 m로 줄여 씀. 흑단 킹은 `PR_Late_Ebony`로 바꿔 입힘, 굳은 킹은 `PR_Late_FrozenStone`(코드가 재질 교체) |
| 왕실 깃발 (H_03 #8) | G 코드 천 깃발 방식 + `PR_Late_G_RoyalBanner` 계열 텍스처 | H 문장(킹)은 코드 텍스처로. 천 봉에는 벽 받침대(G 세션 주의: 떠 보임) |
| 팀 문 깃발 (H_03 #9) | 코드 천(11-07 규칙) | |
| 화분 (H_03 #11) | `A_Planter`(1단계) 또는 G 소품 | 배치 때 크기·재질 확인 |
| 재질 7종 | `PR_Late_MarbleCream`·`MarbleNavy`·`Maple`·`Walnut`·`Velvet`·`Brass` | 상아 기물도 `PR_Late_MarbleCream`. 새 재질 3종은 `ArtSource/PawnRush/MiniGames/Late/Materials/r510_material_swatches.py` |

배치 코드 참고: `Assets/Scripts/PawnRush/MiniGameModels.cs`의 `RoyalProcession*`(`ProcessionWalls`·`RoyalProcessionDais`·`RoyalProcessionLight`·`RoyalProcessionCloth`·`TeamGateDoor`·`TileOver`/`MarbleBlock`).

## Blender로 직접 만드는 것 (모양이 단순함)

- 판 기둥 c6·f3·f7 (H_02 #1): 1.6×1.6×2.2 m 크림 대리석 + 황동 띠·계단 받침.
- 울타리 기둥 (H_02 #3): 0.15 m 굵기·0.55 m 월넛 + 황동 머리(시트의 0.55 m 굵기는 게임과 다름).
- 킹 쟁반 (H_03 #3): 0.7×8×0.3 m 월넛 틀 + 보라 벨벳 바닥.
- 체스판 칸·공격 칸 판은 게임 코드(`MgCheckmate`)가 그리므로 재질만 바꿈.

## 새로 요청할 것 (Codex 에셋 시트 → 사용자 Tripo)

다른 방에 없는 소품 4종만 한 장으로: 받침대 위 큰 체스 시계(H_03 #4), 예비 기물 상자(#5), 기보 석판(#6), 받침쇠에 걸린 청동 홀 종(#7, E 서고 `E_Bell`은 손잡이 달린 손종이라 모양이 다름).

**완료(11-08):** [AssetRefs/H_assets_01_props.png](AssetRefs/H_assets_01_props.png) — 위 4종, 흰 배경 한 줄(`AssetRefs/PROMPT.md`). **Tripo 권장 면 수: 시트 전체 약 45,000**(손질 뒤 목표 시계 3,000·상자 3,000·석판 1,000·종+받침쇠 4,000). 시계 숫자·석판 기보는 글자 금지 규칙으로 뺐다(필요하면 코드 텍스처로). AI 관찰: 시계 받침대가 요청(시계의 2배)보다 낮음 → 손질 때 높임.
