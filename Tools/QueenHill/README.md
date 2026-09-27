# Tools/QueenHill — 퀸 오브 더 힐 맵 데이터와 미리보기

맵 설명: [Docs/GameModes/QueenOfTheHill/GRAYBOX.md](../../Docs/GameModes/QueenOfTheHill/GRAYBOX.md).

## 맵 데이터 만들기

```bash
python3 Tools/QueenHill/build_layout.py          # Assets/Resources/QueenHill/QueenHillLayout.json 을 다시 씀
python3 Tools/QueenHill/build_layout.py --check  # JSON이 스크립트와 같은지만 확인 (run-tests-linux.sh가 부름)
```

- 파이썬 3만 있으면 된다(추가 패키지 없음).
- **JSON은 손으로 고치지 않는다.** 스크립트를 고치고 다시 돌린다.
- 좌표는 Unity 그대로(x 동, y 위, z 북, 미터). 백팀 = 남쪽(−z), 흑팀 = 북쪽(+z). 한 팀 쪽은 `Frame`으로 만들고 반대쪽은 180° 돌려서 똑같이 만든다. 움직이는 조각의 이동 방향·축은 프레임 좌표로 적는다(180° 회전이 함께 돌려 준다).
- 조각 하나 = `{n 이름, k 종류, g 부모, p 위치, s 크기, r 회전, c 색, m 움직임, a 움직임 값, f 역할, b 역할 값, t 글자}`. 뜻은 `QueenHillLevel.cs`의 `LayoutPiece` 주석.

### 스크립트 구조 (8차, R50)

| 부분 | 무엇 |
|---|---|
| `H(rank)`, `CORE`, `PLAZA_*`, `TUBE_*`, `LIFT_*`, `LANE_*` | 높이(16 + 26 × (랭크 − 1)), 코어 크기, 팀 허브와 날개 길의 자리 |
| **모듈** `m_flight`, `m_arcade`, `m_pawn_march`, `m_checkerboard`, `m_piston_ramp`, `m_wall`, `m_statues`, `m_steps`, `m_gondolas`, `m_chain_ladder`, `m_pendulums`, `m_lift`, `m_swing`, `m_drawbridge`, `m_spiral`, `m_bishop`, `m_sliding_walls`, `m_knight`, `m_gears`, `m_clock`, `m_chess_clock`, `m_wheel`, `m_spring`, `m_islands`, `m_beams` (+ `landing`) | 장애물 한 덩이. `(Lane, u, y, 값…) → (끝 u, 끝 높이)`: 길을 따라 얼마나 가고 얼마나 오르는지 스스로 안다 |
| `Lane` | 날개의 곧은 길 하나. u = 길을 따라, v = 가로질러. `L.box`, `L.osc`(왕복 장애물), `L.ramp`, `L.sign` |
| `leg()` | 모듈을 차례로 놓고 남는 길이를 쉼터로 채움. 오르는 높이가 맞지 않거나 길보다 길면 멈춘다(assert) |
| `wing()` | 한 날개 = 북쪽 길(40 m) → 모서리 길(28 m) → 남쪽 길(38 m), 광장에서 나가 다음 랭크 광장으로 |
| `floor1()`~`floor3()`, `floor5()`, `floor6()` | 층마다 서쪽·동쪽 날개의 모듈 목록. **층을 바꾸려면 여기의 목록과 값을 고친다** |
| `floor4()`, `floor7()`, `summit()` | 공용 층(궤도 고리·룩의 탑 / 나선 계단·빛의 계단)과 정상 |
| `hub()` | 팀 광장(= 랭크 발판), 팀 종, 팀 지름길 승강기, 팀 진공관·정거장, 1랭크 다시 나오는 곳 |

## 미리보기 (Unity 없이 PNG)

```bash
cd Tools/QueenHill/preview && npm install three@0.170.0 && cd -   # 처음 한 번
python3 Tools/QueenHill/preview/render.py Tools/QueenHill/preview/out  # 시점 12개를 PNG로 (층별 시점은 그 층 높이만 잘라 보여 줌)
python3 Tools/QueenHill/preview/render.py out_dir --time 12            # 움직이는 것들의 12초 뒤 모습
```

- Chromium(또는 Chrome)이 필요하다. 못 찾으면 `CHROME=/경로/chrome`.
- 시점은 `render.py`의 `VIEWS`(이름, 눈, 보는 곳, 화각, 보여 줄 높이 범위). 브라우저에서 직접 볼 때는 `index.html#eye=x,y,z&at=x,y,z&fov=50&clip=94,124`.
- 움직임 계산은 C#(`MovingPlatform`, `OrbitPlatform`, `Oscillator`, `Pendulum`, `Spinner`, `PhaseToggle`)과 같은 식을 `index.html`에 옮겨 두었다. 한쪽을 바꾸면 다른 쪽도 바꾼다.
- `out/`, `node_modules/`, `layout.js`는 커밋하지 않는다(`.gitignore`). PNG는 Git LFS 대상이라 AI 환경에서는 커밋하지 않는다.
