# 폰 러쉬 맵 부품 (2026-10-17 시작)

기준 그림: 승규 님이 고른 **두 번째 시안**(파란 하늘, 각진 크림색 돌 블록 성, 광택 흑백 체스판, 빨강·흰 줄무늬 테두리, 남색+금 깃발, 금 왕관 아치, 쇠사슬, 나이트 석상).
돌은 **각진 블록**으로 간다. 로비·인트로(매끈한 성 대회장)와 맞추는 건 나중에 따로 한다.

시연 구간: 코스 01의 `S_Start` → `A_Shared`(회전 막대·3 m 벽·원판·미닫이 벽) → `Plaza1` 입구.

## 1차 부품 (Blender 스크립트, `build_parts_v1.py`)

다시 만들기:
```
"C:/Program Files/Blender Foundation/Blender 5.2/blender.exe" --background --factory-startup --python build_parts_v1.py -- <이 폴더>/v1
```

| 부품 | 크기(m) | 쓰는 곳 |
|---|---|---|
| `PR_Wall_4x3` | 4 × 1 × 3, 블록 1 × 0.5 엇갈려 쌓기 | A2 3 m 벽, 바깥 벽 |
| `PR_Battlement_4m` | 4 × 1 × 1.4 (톱니 2개) | 벽 위 |
| `PR_Floor_Checker_4p5` | 4.5 × 4.5 × 0.5, 칸 2.25(미니게임 칸과 같음) | 광장·길 바닥 |
| `PR_Trim_RedWhite_4p5` | 4.5 × 0.5 × 0.5, 0.75마다 빨강/흰 | 길 가장자리(난간 아님) |
| `PR_Pillar_1p2x3` | 1.2 × 1.2 × 3 (받침·머리 1.5) | 출발 광장, 문 양옆 |
| `PR_Tower_4x6` | 4 × 4 × 6 + 톱니(위 7.1) | 광장 모서리, E 종탑 |

기준점은 바닥 가운데, 1 단위 = 1 m. 재질은 이름만 나눠 둠(`Stone_Cream`·`Stone_CreamDark`·`Stone_Shadow`·`Tile_White`·`Tile_Black`·`Trim_Red`·`Trim_White`·`Banner_Navy`·`Gold`). Unity에서 URP 재질로 바꿔 끼운다.
`v1/PR_Kit_v1_sheet.png`는 부품을 모아 놓은 확인용 그림(EEVEE, 조명은 임시).

## 2차 부품 (Tripo로 만들고 Blender에서 다듬기)

| 부품 | 크기 목표 | Tripo 프롬프트(영어) |
|---|---|---|
| 왕관 아치 게이트 | 폭 10 × 높이 8 m | blocky low-poly castle gate arch, cream stone blocks, big golden crown on top, navy blue and gold trim, toy-like stylized game asset, clean shapes |
| 나이트 석상 | 높이 3 m | stylized chess knight statue, dark charcoal stone, chunky low-poly blocky style, on square cream stone pedestal, game asset |
| 굵은 쇠사슬 + 금 고리 | 고리 1개 0.6 m | chunky cartoon iron chain links with gold anchor bracket, low-poly stylized, game asset |
| 남색 깃발(세로) | 1.5 × 4 m | hanging navy blue banner with gold crown emblem, gold rod on top, pointed bottom, stylized game asset |
| 빨강 깃발(세로) | 1.5 × 4 m | hanging red banner with gold crown emblem, gold rod, pointed bottom, stylized game asset |
| 체크 깃발(탑 위) | 1 × 2 m | small black and white checkered flag on pole, stylized game asset |
| 바닥 화살표 | 2 × 2 m | (Blender로 직접: 남색 셰브론 판) |

다음: 1차 부품을 Unity로 가져와 코스 01 회색 상자 위에 올려 크기·룩 확인 → 2차 부품.
