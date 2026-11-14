# 폰러시 경기 오디오

## ElevenLabs 효과음 교체

다이빙과 잡기에는 사용자가 만든 테이크를 넣었다. 유리 파손에는 프로젝트의 유리음을 짧게 편집해 넣었다. 새 소리는 아래 파일명으로 기존 WAV를 덮어쓰면 된다. 원래의 `.meta`는 유지한다. 모노 PCM WAV, 44.1 또는 48 kHz로 내보내고 첫 동작 앞의 무음을 잘라낸다.

| 재생 순간 | `Assets/Resources/` 아래 파일 | ElevenLabs에 넣을 프롬프트 |
|---|---|---|
| 폰이 실제로 몸을 던질 때 | `PawnRushSkillSfx/CourseDive.wav` | A small wooden chess piece dives headfirst, a quick dry swish of cloth and air, soft finish, one motion in 0.3 seconds. No landing, voice or music. |
| 손이 물체나 다른 폰에 붙거나 벽·밧줄에 매달릴 때 | `PawnRushSkillSfx/CourseGrab.wav` | A gloved hand catches a polished wooden chess piece. One close soft wooden tap and a tight leather grip, 0.2 seconds, dry, no voice or music. |
| 대계단 징검다리에서 약한 유리판이 깨질 때 | `PawnRushHallSfx/BridgeGlassBreak.wav` | One thick glass footbridge pane suddenly cracks underfoot. Crisp snap followed by a few light falling shards, 0.7 seconds, close and dry, no explosion, room echo, voice or music. |

킹 잡기 H의 교체 파일은 `PawnRushMiniGameSfx/`에 둔다. 이동 `sfx_king_hop`, 체크 `sfx_check`, 메이트 `sfx_mate`, 잡기 `sfx_king_capture`, 마지막 잡기 `sfx_king_sweep`, 쟁반 착지 `sfx_ep_tray`, 탈출 `sfx_stalemate`, 구출 `sfx_rescue` 순으로 각 동작에 연결된다. [킹 잡기 프롬프트와 시험 방법](PAWN_RUSH_MINIGAME_SECTIONS.md)

## 볼륨과 재생

- **대계단 장애물**은 원래 거리 감쇠에 홀 게인 0.55와 효과음 설정을 곱한다. 돌 홀 잔향은 0.4초다. 낙하 기사 착지는 A1 원음의 첫 충격 0.8초만 한 소스로 재생한다. 대리석과 카펫의 착지음, 경고의 박자와 방향은 각각의 기존 클립을 따른다.
- **다이빙·잡기**는 폰러시 이동 판정에서 재생한다. 양손이 동시에 붙어도 잡기 소리는 한 번 난다. 온라인 참가자는 방장의 동작 이벤트를 받는다. **유리**는 방장과 각 참가자가 판이 새로 깨지는 순간 재생하고, 중간 입장 때 이미 깨진 판은 조용히 상태만 맞춘다.
- **경기 BGM**은 사용자가 ElevenLabs에서 만든 150 BPM의 브라스 곡이다. `PawnRushMusic/RaceMusic.wav`의 28.4초 도입에 이어 `PawnRushMusic/RaceMusicLoop.wav`의 89.6초 구절이 반복된다. 원본과 편집 스크립트는 `ArtSource/PawnRush/Music/`에 있다. 입장 화면이 닫히면 1초간 커지고, 승리가 결정되면 1초간 작아진다. 설정의 **음악** 슬라이더로 조절한다.

Unity에서 대계단 낙하 기사와 예고음을 들은 뒤, 유리판이 깨지는 순간 및 안전한 판 위에 머무를 때를 비교한다. 두 PC에서는 다이빙과 잡기의 위치, 같은 판의 파손음이 한 번씩 들리는지 확인한다. 직접 청취 결과는 아직 기록되지 않았다.
