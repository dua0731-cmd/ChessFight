# I 국왕 침실 — 다른 방 에셋 재사용 목록 (R511)

사용자 지시(11-08): "다른 채팅에서 썼던 애셋들 있을 거야 그런것도 겹치면 코덱스에 신청 안하고 써도 돼".
I는 G 행차 회랑·H 체스 홀과 같은 3단계(윗궁)라 방 껍데기는 거의 전부 G·H에서 가져온다. H의 [REUSE.md](../R510_KingsChessHall/REUSE.md)와 같은 표 형식.
판 위치(팀마다 8×8, 2.25 m, 파랑 x −21~−3·z 6~24)도 H와 같아서, H의 방 배치 코드(`MiniGameModels.ChessHall.cs`, G 홀 함수 재사용)를 거의 그대로 쓸 수 있다.

## 가져다 쓰는 것 (Codex·Tripo 요청 안 함)

| I 부품 (컨셉 시트 번호) | 가져올 에셋 | 메모 |
|---|---|---|
| ~~옆벽·기둥·높은 창 (I_02 #7)~~ | ~~G 벽 모듈~~ → **I 전용** `Late/I/i_hall_v1.py` `I_HallEnv` | 사용자 11-08: 건축 구조물은 방마다 컨셉대로 (세 방 벽이 같던 문제) |
| ~~천창 (I_02 #8)~~ | ~~G/H 천장~~ → **I 전용** 아치형 천창 랜턴(`I_HallEnv`) | 같은 이유 |
| 입구 계단·0.4 m 단 (I_02 #9) | `G_StairsCenter`·`G_StairsRamp`·`G_DaisEdge` | |
| 뒤 벽 왕실 문 (I_02 #4) | `G_CenterGate` | 남색 문·왕관, 열리지 않음 |
| 팀 문 닫힘/열림 (I_02 #5·6) | `G_TeamGateFrame`·`G_TeamGateDoor`·`H_TeamGateCrest`(킹 문장) | 문짝 팀색 칠은 G 방식 |
| 통로·러너 (I_02 #10) | G/H 통로 코드(남색 대리석 타일 + 좁은 보라 러너) | |
| 판 테두리 황동 띠 (I_02 #1) | H `h_board_kit_v1.py` 방식 | |
| 위층 난간 | `G_Balustrade` | |
| 공격 기물 5종 (I_03 #4) | **`Assets/Art/PawnRushMap/Resources/Pieces/PR_Piece_*`** 6종(룩·비숍·퀸·나이트·폰·킹) + `PR_PieceRing` | 이미 `MapLook`이 쓰는 기물 모델. 흑단 `PR_Late_Ebony`·상아 `PR_Late_MarbleCream`로 입힘, 고리는 상대 팀색 |
| 수레 위 우리 기물 (I_03 #3) | 같은 `PR_Piece_*` | |
| 잠든 킹 (I_03 #1) | `PR_Piece_King`(또는 `G_King`)을 눕혀 씀 + Blender 나이트캡 | 나이트캡은 원뿔+방울, 팀색 재질 |
| 벤치 (I_03 #7) | `F_Bench`(벨벳 벤치) | 개어 둔 가운을 위에 올림 |
| 화분 | `F_Topiary` | |
| 왕실 깃발 (I_03 #11) | G 코드 천 깃발 + 문장 텍스처(킹 + 초승달은 코드 텍스처로) | |
| 팀 문 깃발 (I_03 #12) | 코드 천 | |
| 재질 9종 | `PR_Late_MarbleCream`·`MarbleNavy`·`Velvet`·`Ermine`·`Brass`·`Walnut`(레일)·`Ebony`(R510)·`PR_Mid_DollLinen`(R509 J, 침대 리넨) | 새 재질 2종(카펫)은 `ArtSource/PawnRush/MiniGames/Late/Materials/r511_material_swatches.py` |

## Blender로 직접 만드는 것 (모양이 단순함)

- 레일 (I_02 #2): 어두운 월넛 칸 3개 + 가운데 홈 + 금 마름모. 게임이 레일 칸·홈·마름모를 그리므로 그림만 바꿈(판정 그대로).
- 이중 체크 바닥 뚜껑 (I_02 #3): 둥근 뚜껑 + 황동 테 + 경첩, 닫힘/열림. 기물이 솟을 때 열림(코드).
- 나이트캡: 위 표.
- 카펫 판 칸 (I_02 #1): 게임 코드(`MgCheckBlock`)가 그리므로 새 카펫 재질만 입힘.

## 새로 요청할 것 (Codex 에셋 시트 → 사용자 Tripo)

다른 방에 없는 것만 2장(`AssetRefs/PROMPT.md`, 맨 위에 권장 면 수):

| 시트 | 내용 | Tripo 권장 면 수 |
|---|---|---|
| ~~`AssetRefs/I_assets_01_bed_cart.png`~~ | 캐노피 침대(빈 침대, 둥근 받침 + 금 띠, 커튼은 기둥 위에 묶음) · 방패 수레 + 3폭 병풍(앞 갑판은 기물 자리로 비움, 앞면 금 줄) | 시트 전체 약 55,000 (침대 8,000 + 수레 5,000 = 손질 뒤 13,000의 약 4배) | → **Tripo 안 함**: 사용자 11-08 "그것도 블렌더로 디테일 하게 만들어", `ArtSource/PawnRush/MiniGames/Late/I/i_devices_v1.py`로 Blender 직접 |
| ~~`AssetRefs/I_assets_02_props.png`~~ | 협탁(불 없는 촛대·책) · 옷장 · 세면대(대야·물병) · 실내화 · 개어 둔 담비털 가운 · 병풍 | 시트 전체 약 40,000 (손질 뒤 약 10,300의 약 4배) | → **Tripo 안 함**: 사용자 11-08 "사진 소모품들은 너가 진짜 디테일하게 작업해", `ArtSource/PawnRush/MiniGames/Late/I/i_props_v1.py`로 Blender 직접 |
