# 3단계(윗궁) 재질 견본 (R504~)

G 왕의 행차 회랑 [R504 컨셉 시트](../../../../../Docs/PawnRush/ConceptArt/R504_RoyalProcession/)의 재질 견본 줄에 맞춰, 1단계 [R502 견본](../../Early/Materials/README.md)과 같은 방법으로 만든다. 실제 입체 견본을 만들어 위에서 내려다보고 이어 붙는(타일링) PBR 텍스처로 굽는다. 3단계는 G·H·I와 작전 회의실이 이 한 세트를 같이 쓴다([3단계 아트 기준](../../../../../Docs/PawnRush/PAWN_RUSH_ART_PHASES.md) §2).

```
"C:/Program Files/Blender Foundation/Blender 5.2/blender.exe" -b --python r504_material_swatches.py -- --bake --preview
```

- `--bake`: `Textures/`에 맵을 굽는다. `--res N`은 2 m 타일 한 장의 픽셀 수(기본 2048, 1 m 타일은 그 절반). `--only 이름`은 한 재질만 굽는다.
- `--preview`: `swatch_compare.png`를 만든다. 위는 컨셉 시트의 견본 줄, 아래는 우리 견본(1줄 1 m 크기, 2줄 2×2 반복으로 이음새 확인). 16코어 CPU에서 전체 약 몇 분.
- 중간 EXR은 임시 폴더(`%TEMP%/r504_swatch_work`)에 둔다.

| 재질 | 타일 | 맵 | 입체 견본 |
|---|---|---|---|
| `PR_Late_MarbleCream` 크림 대리석 | 2 m | BaseColor · Normal · Roughness · AO · Height | 1 m 판 4장, 얇은 줄눈, 금빛 갈색 결, 반질한 면(거칠기 0.14~0.3) |
| `PR_Late_MarbleNavy` 남색 대리석 | 2 m | 〃 | 미니게임 단 `#29334A`, 흰·회청 결 |
| `PR_Late_Maple` 메이플 | 2 m | 〃 | 0.2 m 판 10줄, 꿀색 결. 길 칸의 밝은 칸 |
| `PR_Late_Walnut` 월넛 | 2 m | 〃 | 같은 판 구조, 짙은 갈색. 길 칸의 어두운 칸 |
| `PR_Late_Velvet` 보라 벨벳 | 1 m | BaseColor · Normal · Roughness · AO | 눌린 결 얼룩과 잔털. 러너·길 테두리·커튼·깃발. Unity에서는 Sheen 대신 결 얼룩이 광택 역할 |
| `PR_Late_Ermine` 담비털 | 1 m | BaseColor · Normal · Roughness · AO · Height | 흰 털 + 검은 꼬리 무늬(꼬리 + 점 3개) 4×4. **G 느린 칸에만** 쓴다 |
| `PR_Late_Brass` 무광 황동 | 1 m | BaseColor · Normal · Roughness · Metallic | 결 방향 헤어라인, 얼룩. 장식 금속 전부 |
| `PR_Late_DoorNavy` 남색 문 판자 | 2 m | BaseColor · Normal · Roughness · AO · Height | 0.22 m 판 9장, 붓자국, 닳은 모서리. 왕실 정문 `#28344D` |

**모델링 범위(사용자 지시, B 동관 세션이 전달, 11-07):** "지금 유니티에 있는 구조는 변경해도 좋고 입구랑 출구 사이즈만 맞으면 돼", "완전 레퍼런스 사진 처럼 할거라서 구조는 바꿔도 돼 천장 등등". G도 컨셉 시트에 완전히 맞추며 천장 등 건축 구조를 바꿔도 된다. 구간 입구·출구의 크기와 위치만 기존 게임에 맞춘다.

지키는 것:
- 이어지는 면(벨벳·담비털·황동·문 판자)의 노이즈는 4D 원환 노이즈(`tnoise`), 변형은 정수 주기 사인 합(`psines`)이라 타일 가장자리에서 정확히 이어진다. 블록·판 재질은 R502처럼 줄눈에서 끊기고 옆 8칸 복사로 AO가 이어진다.
- 벨벳·담비털·황동은 옆면 없는 평면 격자로 굽는다(부드러운 음영의 상자는 옆면 노멀이 섞여 판 전체가 둥근 지붕처럼 구워졌다). 벨벳·담비털·황동·문 판자는 굽은 뒤 `heal_band`로 반 타일 밀어 겹치기를 해서 가장자리 픽셀 오류와 문 판자 끝 모따기 줄을 없앤다. 이미 구운 파일은 `--heal`로 다시 굽지 않고 고칠 수 있다.
- 검사(AI): 8종 모두 타일 가장자리 차이가 안쪽 이웃 픽셀 차이 이하(비율 ≤ 1). 2048 전체 굽기는 이 PC에서 약 98분, 1 m 3종 다시 굽기 18분.
- 노멀 맵은 OpenGL 방식(G = 위). 베이스 컬러에는 빛·그림자를 넣지 않는다.
- **기능 금 `#E2B23C`**(가마 띠·룩 목띠·모퉁이 고리·차고 멈춤쇠)은 게임의 평평한 색을 그대로 쓴다. 텍스처를 만들지 않는다. 장식 금속은 전부 무광 황동이다.
- 팀색(파랑·주황)은 재질 세트에 없다. 팀 문·깃발에만 게임이 칠한다.
- 아직 Unity `Assets/`로 옮기지 않았다. 사용자가 견본을 확인한 뒤 다음 단계(Unity 3단계 조명: 천창 빛기둥·남색 그림자)에서 옮긴다.
