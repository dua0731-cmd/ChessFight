# Tools/Generators

Unity 없이 자산을 만들 때 쓴 도구다. 프로젝트 루트에서 실행한다.

| 파일 | 용도 | 주의 |
|---|---|---|
| `mkmeta.py` | 경로마다 결정적 GUID(`md5("chessfight:"+경로)`)로 `.meta` 생성. 이미 있으면 건너뜀 | 안전. Unity 밖에서 만든 스크립트·문서에 쓴다 |
| `gen_scenes.py` | Intro·KingRush 씬, 장애물·캐릭터 프리팹, 코스 재질을 Unity YAML로 생성. **RagdollTest는 2026-09-25부터 래그돌 랩 씬이라 만들지 않는다**(랩 빌더 메뉴가 만든다) | **덮어쓴다.** `--overwrite` 없이는 실행되지 않는다. Unity나 사람이 한 번이라도 저장한 뒤에는 돌리지 않는다. 현재 커밋의 파일과 바이트 단위로 같은 결과를 낸다(2026-09-24 확인) |
| `check_braces.py` | 컴파일러가 없을 때 .cs 파일의 괄호 균형 확인 | 문법 검사가 아니다 |

`gen_scenes.py`는 로비(`Lobby.unity`, 구 ChessFightLab)와 `PawnAvatar`·`Arena` 프리팹을 만들지 않는다. 그것들은 `3abe230`에서 이전 생성기로 만들었고, 그 생성기는 옛 폴더 구조(`Assets/ChessFight/…`)를 전제로 해서 보관하지 않았다.

방식 요약(새 자산을 손으로 쓸 때 따른다):
- 내장 메시는 `{fileID: 10202/10207/10208, guid: 0000000000000000e000000000000000, type: 0}`(Cube/Sphere/Capsule).
- 스크립트 참조는 해당 `.cs.meta`의 guid + `fileID: 11500000`.
- 오브젝트 fileID는 파일마다 정한 기준값에서 1씩 올린다(`Doc(base).id()`). 실행할 때마다 같은 값이 나온다.
- 조명·렌더 설정 블록은 `SampleScene.unity`에서 복사한다.
