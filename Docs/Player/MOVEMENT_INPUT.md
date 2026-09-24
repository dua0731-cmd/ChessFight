# 플레이어 입력과 이동

사용자 요구(R2): 캐릭터 이동은 **Unity 6 권장 Input System**으로. 다만 클릭 문제(R8) 때문에 **지금 실제로 쓰는 입력은 레거시 Input Manager**다. 구조는 Input System으로 바로 바꿀 수 있게 되어 있다.

## 1. 입력 경로

```text
키보드·마우스·패드
   │
   ▼
IMoveInputSource.Read() → MoveIntent { Move(Vector2), Jump(누른 순간), Shove(누른 순간), Grab(누르는 동안) }
   │   ├─ LegacyMoveInputSource     ← 현재 사용 (Active Input Handling = Old)
   │   └─ InputSystemMoveSource     ← 패키지 있음 + 백엔드 켜짐일 때만 (Scripts/Input, 자기 등록)
   │
   ├─ 온라인(로비·경기): NetworkRuntime → MovementGate 확인 → SteamMotion.Update(x, z, jump)
   │                     → 호스트 판정 PawnMotor (평면, Shove/Grab 무시)
   └─ 오프라인(KingRush·RagdollTest 직접 Play): PlaytestSpawner → CharacterCommand(월드 좌표)
                         → ICharacterDriver.SetCommand → PlaytestCharacter 또는 래그돌
```

| 입력 | 키 | 패드 |
|---|---|---|
| Move | WASD, 방향키(Input System) | 왼쪽 스틱 |
| Jump | Space | 남쪽 버튼 |
| Shove(밀치기) | 마우스 왼쪽 | RB |
| Grab(잡기) | 마우스 오른쪽 누르고 있기 | LB |
| 기타 | Esc(경기 나가기), R(체크포인트로), Backspace(처음부터), F8(지연 시뮬, 개발), 아무 키(인트로) | — |

기타 키는 `LegacyKeys.Down/AnyDown`으로 읽는다(레거시가 꺼져 있어도 예외를 삼킨다).

## 2. 파일

| 파일 | 역할 |
|---|---|
| `Game/MoveInputSource.cs` | `MoveIntent`, `IMoveInputSource`, `MoveInputSources`(등록·생성), `LegacyMoveInputSource`, `LegacyKeys` |
| `Input/InputSystemMoveSource.cs` | `ChessFight.Game.Input` 어셈블리(`CHESSFIGHT_INPUTSYSTEM`). `ENABLE_INPUT_SYSTEM`이 없으면 null을 돌려 레거시로 넘긴다. Shove/Grab 액션은 없어도 동작 |
| `Resources/ChessFightControls.inputactions` | `Gameplay` 맵: Move, Jump, Shove, Grab |
| `Bootstrap/NetworkRuntime.cs` | 온라인 입력 소유, `MovementGate`(로비: 번호 입력 중·창 비활성이면 정지 / 경기: 창 비활성이면 정지) |
| `Gameplay/Playtest/PlaytestSpawner.cs` | 오프라인 입력 → `CharacterCommand`. 카메라가 위아래로만 기울어 화면 위 = 월드 +Z |

## 3. Input System 백엔드로 되돌리는 방법 (지금은 하지 않는다)

1. `com.unity.ugui` 설치
2. 씬에 `EventSystem` + `InputSystemUIInputModule` 추가(또는 UI Toolkit 이벤트 경로 검증)
3. 그다음에 Active Input Handling을 Both로. `InputSettingsGuard`는 uGUI가 있으면 간섭하지 않는다
4. 클릭·번호 입력·친구 패널을 전부 다시 확인

순서를 바꾸면 HUD가 통째로 안 눌린다([PITFALLS #1](../Environment/PITFALLS.md)). 결정 [U2](../Project/DECISIONS.md).

## 4. 이동 모델 두 가지

| | 네트워크 `PawnMotor` | 오프라인 `PlaytestCharacter` |
|---|---|---|
| 어디서 | 로비, 현재 네트워크 경기 | KingRush·RagdollTest 직접 Play |
| 방식 | 수식(평면, 충돌 없음), 호스트 권위 | CharacterController(**임시품**) |
| 동작 | 이동·점프 | 이동·점프·밀치기·잡기 흉내·장애물에 밀림 |
| 수치 | 속도 6, 점프 7, 중력 22 | 같은 값 + 밀치기·잡기·넉백 값(Inspector) |
| 문서 | [Network/MOTION](../Network/MOTION.md) | 이 문서, [KingRush](../KingRush/README.md) |

최종 캐릭터는 **래그돌**이 두 곳을 모두 대체한다([RAGDOLL](RAGDOLL.md)). 네트워크 경기에서 래그돌을 쓰려면 권한 결정(호스트가 전부 시뮬레이션 vs 각자 시뮬레이션 + 호스트 검증)이 먼저다.

## 5. 표시

- 네트워크 캡슐: `PawnAvatar.prefab`. `PawnSpawner`가 팀 색 재질을 칠하고, 위치는 `1-exp(-18·dt)` Lerp, 방향은 이동 방향 Slerp. 보간 버퍼는 없다.
- 카메라: `CameraRig`. 자기 캐릭터가 생기기 전에는 위에서 내려다보고, 그 뒤 `1-exp(-6·dt)`로 따라간다.
- 체스 말 모델로 바꾸려면 `PawnAvatar.prefab`만 교체한다.
