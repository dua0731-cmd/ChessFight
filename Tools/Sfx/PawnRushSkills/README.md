# Tools/Sfx/PawnRushSkills — 폰 러쉬 스킬 효과음 시안 도구 (R101)

기록: [Docs/Skills/SOUND.md](../../../Docs/Skills/SOUND.md). 시안 페이지: <https://claude.ai/artifact/HZYGZQCBe1SgxJrExMBdrC>.

ElevenLabs로 만든 순간 소리(스킬 5개 × 순간 21개 × 방향 5개 = 105개)를 실제 스킬 시간표에 맞춰 이어 붙이고, 듣고 고르는 페이지를 만든다. Unity와는 상관없다.

## 준비

- Python 3.12 이상 + `numpy`, `opencv-python-headless`, `imageio-ffmpeg`(ffmpeg가 들어 있음. 다른 ffmpeg를 쓰려면 환경 변수 `FFMPEG`).
- Windows에서 경로가 260자를 넘으면 numpy가 안 불러와진다. 짧은 경로(또는 짧은 이름의 연결 폴더)에 가상 환경을 둔다.

## 파일

| 파일 | 하는 일 |
|---|---|
| `spec.py` | **한 곳에 모은 내용**: 방향 A~E, 스킬마다 순간·생성 길이·시간표(시작/가장 큰 곳/끝 맞춤, 반음, 크기)·장면·구간, ElevenLabs에 보낸 영어 설명 105개(다시 만든 9개는 고친 설명) |
| `nodes.json` | ElevenLabs 작업판(Flow) id와 방향·순간별 노드 id |
| `sessions.json` | `방향.스킬.순간` → 지금 쓰는 소리를 만든 세션 id(`#2` 등은 예비 소리) |
| `fetch.py` | 저장된 상태 조회 결과(JSON 파일)에서 서명된 mp3 주소를 찾아 `gen/방향_스킬_순간.mp3`로 내려받음(있는 것은 건너뜀) |
| `sheet.py` | 소리 모음 판(파형 + 소리 그림, 크기 표시): `python sheet.py "gen/A_*.mp3" sheet_A.png`. 빈 소리·너무 작은 소리를 찾는 데 씀 |
| `crop_frames.py` | Unity 녹화 모음 판(`R90_frames_{pawn,queen,rook,bishop,knight}.jpg`, 4×2칸)을 잘라 `frames/`에 장면 그림을 만듦 |
| `build.py` | 다듬기(모노·38 Hz 아래 제거·빈 곳 자르기·한 번만 나야 하는 순간은 첫 소리만·크기 맞추기) → 시간표에 붙이기 → `out/audio/`(데모 25 + 순간 105, mp3), `out/meta.json`, `out/check/`(데모마다 파형 + 소리 그림 + 순간 선) |
| `page.py` + `page_template.html` | `out/page/index.html`(장면 그림 포함)과 `out/page/sfx-data.js`(소리 130개, base64) |

## 순서

1. **생성(대화의 ElevenLabs 커넥터):** 노드에 설명을 넣고(`creative_add_flow_node`, `duration_seconds`·`prompt_influence` 0.5) `creative_run_flow_nodes`로 돌린다. **한 번에 3~4개, 30초~1분 간격**(많이 보내면 429 "요청이 너무 많다"). 돌린 결과의 `session_id`를 `sessions.json`에 적는다.
2. **내려받기:** `creative_get_flow_run_status`를 세션 id 20개로 부르면 결과가 길어서 파일로 저장된다(적게 물으면 대화에 그대로 나와 주소를 옮기기 어려움 — 이미 받은 세션을 섞어 20개를 채움). 그 파일(긴 경로면 짧은 곳에 복사)을 `python fetch.py 파일...`에 준다. 주소는 2시간 뒤 만료된다.
3. **검사:** `sheet.py`로 모음 판을 보고 빈 소리·작은 소리는 설명을 고쳐(`creative_update_node`) 다시 만든다. 예전 파일은 `gen/old/`로 옮긴다.
4. `python crop_frames.py <R90 모음 판 폴더>` → `python build.py` → `out/check/*.png`로 순간 선과 소리가 맞는지 확인 → `python page.py` → `out/page/index.html`을 `sfx-data.js`와 함께 Artifact로 올린다(같은 파일로 다시 올리면 같은 주소).

`gen/`·`out/`·`frames/`는 저장소에 넣지 않는다(`.gitignore`).
