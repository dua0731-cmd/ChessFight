# 소드 파이트 스킬 Mixamo 클립 (R190)

여기에 넣은 클립을 소드 파이트 기물 스킬의 몸동작으로 씁니다. 클립이 없는 스킬은 지금처럼 코드 동작(`SwordFightSkills.Motion.cs`)을 씁니다.

## 받는 법 (mixamo.com, 무료)

- **Format**: FBX for Unity · **Skin**: Without Skin · **Frames per Second**: 30 · 이동하는 동작은 **In Place** 체크
- 파일 이름: `<기물>_<F 또는 E>` 로 시작 — `King_F.fbx`, `Knight_E_backflip.fbx`
  - 기물: `King` `Queen` `Rook` `Bishop` `Knight` · `F` = F 스킬, `E` = 가장자리 스킬
  - 한 스킬에 파일이 둘이면 이름순 첫 파일만 씀

| 기물 · 스킬 | 동작 | 검색어 |
|---|---|---|
| 킹 F 왕의 반격 | 막기 → 대각선 베기 | `sword block`, `sword slash` |
| 킹 E 왕의 귀환 | 매달림 → 튕겨 오름 → 착지 베기 | `hanging`, `climb up`, `jump attack` |
| 퀸 F 꼬치 베기 | 찌르며 돌진 | `stab`, `sword dash` |
| 퀸 E 체크메이트 일섬 | 큰 대각선 베기 | `great sword slash` |
| 룩 F 열린 파일 포격 | **아래에서 위로 퍼올리기** | `upward slash`, `rising slash` |
| 룩 E 성벽 붕괴 | 위에서 내려치기 | `overhead slash` |
| 비숍 F 관통 핀 | 던지기 | `throw`, `spell cast` |
| 비숍 E 구원의 손 | 끌어올리기 | `pulling` |
| 나이트 F 포크 강하 | 도약 → 내리꽂기 | `jump attack` |
| 나이트 E 벼랑 끝 역전 | 웅크림 → 공중제비 → 착지 | `backflip` |

## Unity에서

1. 이 폴더에 넣으면 처음 가져올 때 자동으로 **Humanoid · 반복 없음**으로 들어옵니다(`SwordFightClipImporter`).
2. 메뉴 **ChessFight → Sword Fight → Skill Clips → Fill From Mixamo Folder** → `Assets/Resources/SwordFightClips.asset`이 만들어지고 선택됩니다.
3. 그 에셋의 칸마다:
   - **marks** 4개(초): 예고 = 0→1, 발동(돌진·비행·베기) = 1→2, 후딜 = 2→3. **1번을 클립에서 칼을 내리치기 시작하는 순간**에 두면 게임의 타격과 맞습니다. 처음에는 클립 길이의 40%·60%로 들어갑니다.
   - **weight**: 클립을 얼마나 쓸지(0 = 코드 동작만)
   - **chest · head · freeArm · legs · turn**: 부위별 클립 비율. 1보다 작으면 코드가 하던 조준(머리가 적을 봄, 왼손이 가장자리·아군을 잡음)이 남습니다.
4. 칼을 든 오른팔은 칼 자세 코드가 그대로 움직입니다(칼이 맞는 선이라서). 비숍처럼 칼을 던진 뒤에는 클립이 오른팔도 움직입니다.
