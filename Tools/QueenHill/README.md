# Tools/QueenHill — 퀸 오브 더 힐 맵 데이터와 미리보기

맵 설명: [Docs/GameModes/QueenOfTheHill/GRAYBOX.md](../../Docs/GameModes/QueenOfTheHill/GRAYBOX.md).

## 맵 데이터 만들기

```bash
python3 Tools/QueenHill/build_layout.py          # Assets/Resources/QueenHill/QueenHillLayout.json 을 다시 씀
python3 Tools/QueenHill/build_layout.py --check  # JSON이 스크립트와 같은지만 확인 (run-tests-linux.sh가 부름)
```

- 파이썬 3만 있으면 된다(추가 패키지 없음).
- **JSON은 손으로 고치지 않는다.** 층을 바꾸려면 `build_layout.py`의 해당 함수(`floor1()` … `floor7()`, `summit()`)를 고친다.
- 좌표는 Unity 그대로(x 동, y 위, z 북, 미터). 백팀 = 남쪽(−z), 흑팀 = 북쪽(+z). 한 팀 쪽은 `Frame`으로 만들고 반대쪽은 180° 돌려서 똑같이 만든다.
- 조각 하나 = `{n 이름, k 종류, g 부모, p 위치, s 크기, r 회전, c 색, m 움직임, a 움직임 값, f 역할, b 역할 값, t 글자}`. 뜻은 `QueenHillLevel.cs`의 `LayoutPiece` 주석.

## 미리보기 (Unity 없이 PNG)

```bash
cd Tools/QueenHill/preview && npm install three@0.170.0 && cd -   # 처음 한 번
python3 Tools/QueenHill/preview/render.py Tools/QueenHill/preview/out  # 시점 10개를 PNG로
python3 Tools/QueenHill/preview/render.py out_dir --time 12            # 움직이는 것들의 12초 뒤 모습
```

- Chromium(또는 Chrome)이 필요하다. 못 찾으면 `CHROME=/경로/chrome`.
- 움직임 계산은 C#(`MovingPlatform`, `OrbitPlatform`, `Oscillator`, `Pendulum`, `Spinner`, `PhaseToggle`)과 같은 식을 `index.html`에 옮겨 두었다. 한쪽을 바꾸면 다른 쪽도 바꾼다.
- `out/`, `node_modules/`, `layout.js`는 커밋하지 않는다(`.gitignore`). PNG는 Git LFS 대상이라 AI 환경에서는 커밋하지 않는다.
