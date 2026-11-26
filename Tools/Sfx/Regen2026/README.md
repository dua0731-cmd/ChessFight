# Tools/Sfx/Regen2026 — 스킬 효과음 68개 유료 요금제로 다시 만들기 (R845, 2026-11-26)

기록: [Docs/Skills/SOUND.md 'R845'](../../../Docs/Skills/SOUND.md). 왜: ElevenLabs 도움말에 따르면 무료 요금제에서 만든 효과음은 유료로 올린 뒤에도 상업적으로 쓸 수 없다. 사용자는 2026-10-29 22:42 KST에 유료로 올렸고, 그 전에 만든(것으로 보는) 스킬 효과음 83개 중 15개는 지우고 68개는 여기서 다시 만들었다.

- 생성: ElevenLabs Sound Effects v2(`eleven_text_to_sound_v2`), **유료 요금제**(사용자가 11-26에 확인 — 커넥터의 사용량 도구는 권한이 없어 직접 못 봄), 2026-11-26 16:52 ~ 17:14 KST(생성 시각은 `creative_get_flow_run_status`의 `created_at_unix_ms`), 작업판(Flow) **"R845 skill SFX regenerated on paid plan 2026-11-26"** `ULhjnpqP98wVoPRP9vI3`. 소리마다 후보 2개(사용자 결정), 합계 약 150개 생성 · 약 465 크레딧.
- 옛 작업판의 소리는 내려받지 않았다(모두 새로 만든 것).

| 파일 | 내용 |
|---|---|
| `spec_research.json` | 옛 프롬프트 · 설정 · 후처리 조사(옛 작업판 노드 · `spec_v2.py` · 문서). 정확히 되찾은 프롬프트는 9개, 나머지 59개는 문서 · 코드 설명으로 다시 쓴 것(`prompt_source`에 표시) |
| `plan.json` | 실제로 보낸 프롬프트(사용자 "동전 소리는 나게 하지마"로 끝에 "no coins, no jingle, no bell, no chime, no ring"을 붙임) · 길이 · `prompt_influence` · 후처리 종류(`kind`). 다시 만든 소리는 `retaken`과 앞 프롬프트 |
| `nodes.json` | 작업판 노드 id |
| `sessions.json` | ElevenLabs 세션 id → `폴더/이름`(어느 생성인지 — 출처 증명용) |
| `fetch.py` | `urls*.txt`(세션 · 생성 · 서명, 2시간 뒤 만료 — 저장소에 넣지 않음)로 후보를 `ArtSource/SkillSfxRegen2026/takes/폴더/이름_생성id.mp3`에 받음. `elevenlabsusercontent.com`이 열린 클라우드 환경 "ChessFight"에서 |
| `process.py` | 후보마다 옛 소리와 같은 후처리 → 동전 울림(`coin.py`) · 띠용(`boing.py` 규칙) · 무음 검사 → 통과한 것 중 옛 소리와 길이 · 가장 큰 곳이 가까운 것을 고름 → `picks.json`. `--install`이면 `Assets/Resources/...`에 같은 이름으로 덮어씀(`.meta` · GUID 그대로) |
| `picks.json` | 고른 후보, 옛 · 새 길이와 가장 큰 곳의 시각, 떨어진 후보와 이유 |
| `record.py` | 실행한 노드 · 세션을 `sessions.json`에 적는 도우미 |

후처리(`kind`): `chain` = `Tools/Sfx/RookChain/process.py` 그대로(저역 통과 두 번 · 40 Hz 고역 통과 · 음 낮춤 · 앞 무음 자르기 · RMS · ChainRun은 이음매 없는 반복), `export_unity` = `Tools/Sfx/PawnRushSkills/export_unity.py`의 `cue()`(RookTick · KnightTurn은 첫 타격만), `rope` = 같은 사슬(옛 기록 없음 — 파일 형식이 같아 그렇게 봄), `raw` = 받은 그대로(소드 파이트 46개 — 옛것도 손대지 않았음).

떨어진 후보는 `ArtSource/SkillSfxRegen2026/takes/rejected/`에 남김(ChainPull 4 · BishopDraw 2 · CastBishop 2 · ChainRun 2 — 동전 울림).

순서: `python3 process.py` → `picks.json` 확인 → `python3 process.py --install`.

R846(2026-11-26): `SwingKnight` · `CastKing`은 이 표의 테이크가 아니라 옛 소리에 맞춰 다시 만든 새 테이크다(작업판 `n9NLRO39rItNgk7wKJkv`, 후보 `ArtSource/SkillSfxRegen2026/takes/SwordFightSkillSfx/R846/`, 사용자가 고름). `picks.json`의 두 항목은 R845 때 기록 그대로 둔다 — `process.py --install`을 다시 돌리면 R845 테이크로 되돌아가니 주의. 기록: [SOUND.md 'R846'](../../../Docs/Skills/SOUND.md).
