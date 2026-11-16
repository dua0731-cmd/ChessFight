# 마차뜰 모델 QA 도구 (R680 QA, 2026-11-16)

스킬 QA 2차에서 쓴 Blender 일괄 스크립트. 모두 `blender -b --factory-startup --python <파일> -- <인자>`로 돌린다(Blender 5.2).
프로젝트 경로는 이 파일 위치에서 찾으므로 어느 PC에서든 그대로 돈다. 결과물(json · png)은 인자로 준 폴더에 쓰니 저장소 밖(임시 폴더)을 준다.

| 파일 | 하는 일 | 인자 |
|---|---|---|
| `rebuild_models.py` | 클레이 모델을 소스에서 다시 지어 FBX로 냄(성 빼고) | `-- entrance,canal,sorting,platform,scenery,movers,machines,horse` 중 고른 것(없으면 전부). 오래 걸리는 entrance · sorting은 따로 띄워 동시에 |
| `rebuild_castle.py` | 성 세 모델을 다시 지어 냄(텍스처 빼고). `RoyalCarriageYard.Castle.cs`의 상자 표도 다시 쓰므로 끝나면 `git diff`로 상자가 그대로인지 볼 것 | 없음 |
| `audit_fbx.py` | FBX마다 삼각형 · 넓이 0 면 · 열린 / 3면 모서리 · 떨어진 점 · 선 | `-- <FBX 폴더> <결과.json>` |
| `inverted_shells.py` | 닫힌 조각 중 면이 안을 보는 것(부피 음수) 개수 | `-- <FBX 폴더> [이름,이름]` |
| `compare_fbx.py` | 같은 FBX 두 벌의 삼각형 · 넓이 · 크기 · 점 거리 비교(모양이 그대로인지) | `-- <전 폴더> <후 폴더> <이름,이름>` |
| `render_diff.py` | 고정물 8개 + 성 문 둘을 같은 카메라 16곳에서 전 / 후 렌더(회색 · 뒷면 숨김 · 마젠타 하늘), 바뀐 화소 · 새로 비쳐 보이는 화소 수와 차이 그림 | `-- <전 폴더> <후 폴더> <출력 폴더>` |
| `castle_check.py` | 구간 끝 성의 열린 모서리 + 텍스처 / 구멍 렌더 전 · 후 | `-- <전 폴더> <후 폴더> <출력 폴더>` |
| `open_edges.py` | 한 FBX의 열린 모서리를 고리로 묶어 위치 · 길이 출력 | `-- <FBX 파일>` |

"전" 폴더는 커밋된 FBX를 꺼내 만든다(LFS):

```
for f in $(git ls-files 'Assets/Resources/PawnRushCarriageYard/*.fbx'); do git show "HEAD:$f" | git lfs smudge > "<전 폴더>/$(basename $f)"; done
```

말 변형 측정(질주 자세에서 어디가 얼마나 움직이는지)은 QA 세션 임시 폴더에만 있었다(메쉬 이름이 `Horse` 시절 기준, 지금은 `Horse_LOD0`).
필요하면 `horse_model.py`의 `_skin_reach` 영역을 기준으로 다시 짠다.
