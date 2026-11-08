# 게임 모드

2026-11-06 · R497 정리 기준. 현재 로비 모드는 **폰 러시와 소드 파이트 두 개**다. Queen of the Hill은 사용자 요청으로 폐기했고 선택 카드·전용 맵·스킬·테스트·메뉴를 정리했다.

## 1. 현재 모드

| 모드 | 통신 키 | 현재 경기 씬 | 개발 문서 |
|---|---|---|---|
| 폰 러시 | `kingrush` | `PawnRush_Round`(R508, 구간 라운드) · `Assets/Scenes/PawnRush_Round.unity` | [현재 구간 개편](../PawnRush/README.md) · [구형 Course01](../PawnRush/Prototype/PAWN_RUSH_COURSE01.md) |
| 소드 파이트 | `swordfight` | `SwordFight` | [실행·현재 구현](SwordFight/README.md) · [기획](SwordFight/DESIGN.md) |

모드 목록은 `Assets/Scripts/Core/GameModes.cs`의 `GameModes.All`이다. 기본 모드는 폰 러시다. 예전 화면 이름에서 온 `GameModes.KingRush`와 `kingrush` 키는 호환을 위해 유지한다. 사용자에게 보이는 이름과 폴더 이름은 폰 러시 기준이다.

폰 러시의 **로비 온라인 목적지는 R508부터 구간 라운드 `PawnRush_Round`**다: 매치 번호로 7구간(장애물 · 미니게임 · 작전 회의실)을 뽑아 모든 PC가 같이 짓고, 작전 회의실 골에 팀 과반이 먼저 서면 이긴다. 구형 Course01은 Prototype으로 빌드에 남아 있지만 로비가 열지 않는다. `PawnRush_Sections`와 `PawnRush_MiniGames`는 오프라인 개발 시험이다. 사용자 Unity · Steam 확인 전([VALIDATION R508](../Network/VALIDATION.md)).

Queen of the Hill의 이전 전송 번호는 다른 모드 번호로 재사용하지 않는다. 남아 있는 `ModeRetiredQueenHill` 예약 값은 호환용 빈자리이고, 현재 모드·카드·장면이 아니다. 다른 모드에서 사용하는 공용 기물 동작·물리·Pioneer/SkillKit은 보존한다.

## 2. 모드가 흐르는 길

```text
파티장이 모드를 선택
  → 파티 로비 데이터 mode
  → 같은 mode의 공개 방 검색
  → 경기 로비 데이터 mode
  → NetworkRuntime.FollowMatch
  → GameModes의 Scene 이름으로 경기 진입
  → 결과 또는 나가기 뒤 Lobby
```

| 규칙 | 적용 |
|---|---|
| 모드 선택 | 파티장이 한가한 파티에서 변경한다. 검색 중에는 선택 큐를 고정한다 |
| 공개 검색 | 같은 모드끼리만 연결한다 |
| 번호로 들어가는 비공개 방 | 그 방의 모드를 따른다 |
| 모드 키 | 예전 빌드·로비 데이터와 비교하므로 표시 이름 정리를 이유로 바꾸지 않는다 |
| 지원하지 않는 모드 | 현재 `GameModes` 목록과 세션 검증 범위를 따른다. 폐기 모드를 새 모드로 되살리지 않는다 |
| 권한 | 모드별 기존 호스트 판정·입력·상태 전송을 유지한다 |

폰 러시의 `SteamPawnRushLink`와 소드 파이트의 래그돌 경기 연결, 공통 세션·매칭·방장 이전은 [Network 문서](../Network/README.md)를 따른다. 과거 문서에 적힌 프로토콜 번호를 현재 버전으로 재사용하지 않는다.

## 3. 개발 메뉴와 장면

- **ChessFight → Pawn Rush:** 현재 구간·미니게임·스킬 시험, Build·Validate.
- **ChessFight → Sword Fight:** 경기·스킬 시험 열기.
- **ChessFight → Development:** 공용 제작·VFX·네트워크·랩·씬 도구.
- **ChessFight → Prototype:** 구형 Course01·체스복싱 등 보존 콘텐츠.

옛 KingRush 시험장의 규칙·브랜치·메뉴 기록은 [프로토타입 역사 사본](../PawnRush/Prototype/README_HISTORICAL.md)에 분리했다. 현재 작업은 [HANDOFF](../../HANDOFF.md)의 고정 경로 `main`을 따른다.

## 4. 새 모드 추가

1. `GameModes.cs`에 고유 키·표시 이름·설명을 추가한다. 키는 영문 소문자,16자 이하이고 기존 키와 겹치지 않아야 한다.
2. 준비 전 Scene은 비워 두고 로비에서 플레이 가능으로 표시하지 않는다.
3. 경기 씬과 필요한 `GameSceneConfig`·캐릭터 권한·승패 처리를 만든다.
4. `SceneNames`·`EditorBuildSettings`·개발 빌드의 `ShippedScenes`를 맞춘다. 상세는 [SCENES](../Architecture/SCENES.md)다.
5. 모드별 전송 값은 기존 번호와 충돌하지 않게 정하고 Core·세션·실제 경기 검사를 구분한다.
6. 이 문서와 [사용자 확인 목록](../Network/VALIDATION.md)을 갱신한다. 컴파일·AI 화면 관찰을 사용자 Unity·Steam 확인으로 표시하지 않는다.
