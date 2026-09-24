# 래그돌 통합

담당: 준영. 사용자 요구(R11): 래그돌 모델링 + 이동·점프·잡기·밀기 프리팹, **멀티플레이를 고려**해서 main에 올릴 수 있는 환경. 팀원용 설명은 [팀 가이드 5장](../TEAM_GUIDE_KO.md)에도 있다(같은 내용).

## 1. 현재 상태

| 항목 | 상태 |
|---|---|
| 래그돌 랩 | `JY-ragdoll` 브랜치(`2d450aa`, `c9c1e6a`에서 분기). `Assets/ChessFight/RagdollLab/`. 로컬 2P, 튜닝 패널, 자동 점검, 네트워크 없음 |
| 게임 쪽 받을 준비 | `ICharacterDriver`, `CharacterCommand`, `PlaytestSpawner`, `RagdollTest` 씬, `PhysicsProfile` — `Network` 브랜치에 있음 |
| 병합 | 아직. `main` 병합 시 충돌 없음을 확인함(래그돌 파일은 모두 새 파일). `Assets/ChessFight.meta`는 Unity가 다시 만든다 |
| 어댑터 `RagdollDriver` | 설계만(아래 코드). 병합 후 추가 |
| 네트워크 래그돌 | 미정. 권한 결정 필요 |

`RagdollPawn`은 이미 키보드를 직접 읽지 않고 `SetInput(PawnInput)`으로 **월드 좌표 명령**을 받는다. 호스트가 원격 입력을 넣을 수 있는 모양이라 그대로 살린다.

## 2. 캐릭터 계약 (`Gameplay/Characters/ICharacterDriver.cs`)

```csharp
public struct CharacterCommand { public Vector3 Move; public bool Jump, Shove, Grab; }   // Move는 월드 좌표
public interface ICharacterDriver
{
    void SetCommand(in CharacterCommand command);
    Transform FollowTarget { get; }                        // 카메라가 따라갈 것
    void Teleport(Vector3 position, Quaternion rotation);  // position = 발이 닿을 바닥 지점
}
```

`CharacterCommand`는 래그돌 랩의 `PawnInput`과 **일부러 같은 모양**이다. 어댑터는 필드 복사로 끝난다.

## 3. 어댑터 (병합 후 `Assets/ChessFight/RagdollLab/Scripts/RagdollDriver.cs`)

```csharp
using ChessFight.Gameplay;
using UnityEngine;

namespace ChessFight.RagdollLab
{
    // Lets the ragdoll be driven by anything that speaks ICharacterDriver: the
    // offline playtest now, the network host and bots later.
    [RequireComponent(typeof(RagdollPawn))]
    public sealed class RagdollDriver : MonoBehaviour, ICharacterDriver
    {
        RagdollPawn pawn;

        void Awake() => pawn = GetComponent<RagdollPawn>();

        // CharacterCommand has the same shape as PawnInput on purpose.
        public void SetCommand(in CharacterCommand command) => pawn.SetInput(new PawnInput
        {
            move = command.Move, jump = command.Jump, shove = command.Shove, grab = command.Grab
        });

        public Transform FollowTarget => pawn.Hips.transform;

        // position is the ground point; this is LabGame.Respawn's own formula.
        public void Teleport(Vector3 position, Quaternion rotation) =>
            pawn.Teleport(position + Vector3.up * (pawn.standHeight + 0.02f), rotation * Vector3.forward);
    }
}
```

붙인 뒤 `RagdollTest` 씬 → `Playtest` 오브젝트 → `Character Prefab`에 래그돌 프리팹 → Play. KingRush도 같다. 완성 프리팹은 `Assets/Prefabs/Characters/`에 둔다. 랩 폴더는 옮기지 않는다(빌더 메뉴가 경로를 안다).

## 4. 멀티를 위해 지킬 것

1. 캐릭터는 입력을 직접 읽지 않는다. 모든 의도는 `SetCommand`(`SetInput`)로만. 새 동작은 `PawnInput` 필드를 늘린다.
2. 이동 방향은 월드 좌표. 카메라 기준 변환은 입력을 만드는 PC에서 끝낸다.
3. 점프·밀치기 같은 "누른 순간"은 다음 FixedUpdate까지 보관한다(`input.jump |= next.jump`). 네트워크 입력이 늦어도 씹히지 않는다.
4. 랜덤·시간 누적에 의존하지 않는다.
5. 상태를 작게 요약할 수 있게 한다: 골반 위치·회전·속도, 상태(Active/Ragdoll/GettingUp), 잡는 중.
6. 물리 120Hz/24회는 `PhysicsProfile`이 씬 단위로 준다. 래그돌 코드에서 `Time.fixedDeltaTime`을 바꾸지 않는다(랩 자동 점검은 예외).
7. 장애물 판정은 물리로. 넉다운이 필요하면 장애물에 `RagdollHazard`. 장애물 공통 클래스는 움직임만 맡는다.

## 5. RagdollLab과 RagdollTest

| | RagdollLab (랩) | RagdollTest (게임 통합) |
|---|---|---|
| 목적 | 물리 손맛 튜닝, 2인 로컬, 자동 점검 | 게임 시스템과 통합 확인 |
| 입력 | 랩 전용 | 게임 공통 입력 → `ICharacterDriver` |
| 확인 | 강성, 기상, 밀치기 감각 | 체크포인트, 리스폰, 장애물, 카메라, 물리 주기 |

## 6. 알려진 함정

- 프로젝트 기본 물리 50Hz에서는 골반이 약 8cm 주저앉는다(랩 측정). `PhysicsProfile` 필수.
- 랩의 `SpinningBar`는 각도를 누적한다. 맵에서는 `Spinner`를 쓴다.
- 래그돌 점프 높이는 약 1.76m, 임시 캐릭터는 약 1.1m다(장애물 기획서 양식 참고).

## 7. 준영 님 AI 시작 프롬프트

```text
ChessFight 저장소의 JY-ragdoll 브랜치에서 작업합니다. main을 먼저 병합하세요.
저장소 루트 HANDOFF.md를 먼저 읽고, Docs/Player/RAGDOLL.md와 Docs/RagdollLab/README.md를 읽으세요.
RagdollPawn은 SetInput(PawnInput)으로만 움직입니다. 이 구조를 유지하세요.
목표: 래그돌 프리팹이 ICharacterDriver를 구현하게 하고(RAGDOLL.md 3절 어댑터), RagdollTest 씬의
PlaytestSpawner에 넣어 이동·점프·잡기·밀기가 동작하는 것을 확인합니다.
멀티 조건(RAGDOLL.md 4절)을 반드시 지키세요. Assets/ChessFight/RagdollLab 폴더는 옮기지 말고,
완성 프리팹만 Assets/Prefabs/Characters에 둡니다.
```
