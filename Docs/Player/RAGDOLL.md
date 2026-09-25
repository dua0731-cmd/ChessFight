# 래그돌

담당: 준영. 사용자 요구: 래그돌 모델링과 이동·점프·잡기·밀기 프리팹, 멀티 고려(R11). **JY-ragdoll의 래그돌 랩을 현재 브랜치 기준으로 병합하고, 랩 씬 내용을 RagdollTest 씬으로 그대로 옮긴다. 네트워크·멀티에 문제가 없어야 한다**(R16).

랩 자체의 조작법, 구조, 측정 근거는 준영 님 문서 [Docs/RagdollLab/README.md](../RagdollLab/README.md)에 있다.

## 1. 현재 상태 (2026-09-25)

| 항목 | 상태 |
|---|---|
| 병합 | `JY-ragdoll`(`2d450aa`)을 `Network`에 병합함(`fc006b8`). 래그돌 파일 118개만 추가, 기존 파일 변경 없음 |
| 위치 | `Assets/ChessFight/RagdollLab/`(Art, Editor, Generated, Materials, Prefabs, Scripts, Settings). 빌더가 이 경로를 알고 있어 옮기지 않는다 |
| **RagdollTest 씬** | `Assets/Scenes/RagdollTest.unity` = **래그돌 랩 씬 그대로**. 원래의 `RagdollLab.unity`는 지웠다. RagdollTest의 GUID는 유지 |
| 어댑터 `RagdollDriver` | 구현하고 `RagdollPawn.prefab` 루트에 붙임. 빌더도 다시 만들 때 붙인다 |
| 네트워크 래그돌 | 아직 없음. 권한 결정 필요([ROADMAP](../Project/ROADMAP.md)) |
| Unity 확인 | **아직.** [VALIDATION](../Network/VALIDATION.md) 최상단 표 |

## 2. RagdollTest 씬에 있는 것 (랩 그대로)

- 30×30 바닥, **[2] 회전 봉**(북쪽, `SpinningBar` + `RagdollHazard`), **[3] 벽 2·3·4m**(동쪽), **[4] 5m 단상과 경사 15·30·45°**(서쪽), **[5] 0.6m 외줄**(남쪽, 아래는 낭떠러지), **[6] 림보 0.55m와 터널 0.65m**, 각 구역 라벨, 해, 카메라
- 실행하면 `LabGame`이 **P1(키보드+마우스), P2(패드 또는 방향키), 더미 1개**를 만든다. Tab 튜닝 패널, R 전체 리스폰, T 슬로모션, F 자유 카메라
- 자동 점검(`LabAutoTest`)은 실행 인자(`-ragdollAutoTest` 등)가 있을 때만 동작한다

좌표는 `LabLayout.cs`, 씬 구성은 빌더(`RagdollLabBuilder.BuildScene`)가 정한다. 메뉴 `ChessFight > Ragdoll Lab > Rebuild Pawn + Scene`은 이제 **`Assets/Scenes/RagdollTest.unity`에 저장**한다(씬을 손으로 고쳤다면 다시 빌드하면 덮어쓴다). `ChessFight > Scenes > Ragdoll Test (offline)`로 연다.

예전 RagdollTest(경사·턱·벽·상자·Spinner·Pendulum과 임시 캡슐 캐릭터)는 이 씬으로 대체됐다. 내용은 git 이력 `72ddf9e`에 있다.

## 3. 네트워크·멀티에 문제가 없는 이유

| 걱정 | 실제 | 보장 장치 |
|---|---|---|
| 래그돌 코드가 Steam이나 네트워크 코드에 섞임 | `ChessFight.RagdollLab` 어셈블리의 참조는 `ChessFight.Gameplay` 하나(→ Game → Core). Steam 없음 | `Tools/run-tests-linux.sh`의 경계 검사가 매번 확인 |
| 네트워크 코드가 래그돌에 의존 | `Assets/Scripts`는 래그돌을 모른다. 캐릭터는 `ICharacterDriver`로만 부른다 | 경계 검사 |
| RagdollTest를 열면 Steam이 켜짐 | `NetworkRuntime`은 첫 씬이 Intro·Lobby일 때만 만들어진다. RagdollTest는 온라인 흐름에 없다(빌드 목록 비활성, 배포 빌드 제외) | 코드(`NetworkRuntime.Boot`) |
| 전역 설정 오염 (중력, 물리 주기, 시간 배율, 커서) | `LabGame`이 씬을 떠날 때 중력·물리 주기·시간 배율을 되돌린다. **커서 잠금 해제도 추가했다** | `LabGame.OnDestroy` |
| 다른 씬에 끼어드는 코드 | `RuntimeInitializeOnLoadMethod`, `DontDestroyOnLoad`, `[InitializeOnLoad]` 없음. 빌더는 메뉴를 눌렀을 때만 동작 | 코드 확인 |
| 프로젝트 설정 변경 | 병합은 새 파일만 추가했다. ProjectSettings·Packages·레이어·태그 변경 없음. 모든 오브젝트가 Default 레이어 | 병합 diff 확인 |
| 입력 백엔드 | 랩은 레거시 `Input`과 XInput(Windows DLL, 없으면 조용히 무시)을 쓴다. Active Input Handling = Old와 맞는다 | [DECISIONS U2](../Project/DECISIONS.md) |
| 렌더링 | 랩 재질은 Built-in `Standard` 셰이더. 프로젝트의 Built-in 우회로 그대로 보인다 | `RenderPipelineOverride` |
| 폴더 메타 변경점 반복 | `Assets/ChessFight.meta`가 없으면 Unity가 PC마다 다른 GUID로 만든다. 원래 GUID로 되살려 커밋했다 | 커밋 |

**멀티를 위한 준비:** `RagdollPawn`은 입력을 직접 읽지 않고 `SetInput(PawnInput)`(월드 좌표)으로만 움직인다. `RagdollDriver`가 이것을 게임 공통 계약 `ICharacterDriver`로 연결하므로, 오프라인 플레이테스트·네트워크 호스트·봇이 같은 방식으로 래그돌을 움직일 수 있다.

**남은 멀티 과제(아직 결정·구현 안 됨):**
- 누가 시뮬레이션하나(호스트가 12명 전부 vs 각자 + 호스트 검증)
- 무엇을 동기화하나(골반 위치·회전·속도, 상태 Active/Ragdoll/GettingUp, 잡기)
- `SpinningBar`는 각도를 누적하므로 **랩 전용**이다. 네트워크 맵에서는 `Gameplay/Obstacles/Spinner`(시간의 함수)를 쓴다.

## 4. 캐릭터 계약과 어댑터

```csharp
// Assets/Scripts/Gameplay/Characters/ICharacterDriver.cs
public struct CharacterCommand { public Vector3 Move; public bool Jump, Shove, Grab; }   // Move는 월드 좌표
public interface ICharacterDriver
{
    void SetCommand(in CharacterCommand command);
    Transform FollowTarget { get; }                        // 카메라가 따라갈 것 (래그돌은 골반)
    void Teleport(Vector3 position, Quaternion rotation);  // position = 발이 닿을 바닥 지점
}
```

`Assets/ChessFight/RagdollLab/Scripts/RagdollDriver.cs`가 이것을 구현한다.
- `SetCommand` → `RagdollPawn.SetInput`. 필드는 이름만 바꿔 복사한다.
- `FollowTarget` = `Hips`.
- `Teleport(바닥, 회전)` → `RagdollPawn.Teleport(바닥 + standHeight + 0.02, 앞 방향)`. `LabGame.Respawn`과 같은 식이다.

**킹러시에서 래그돌로 달려 보려면:** `KingRush` 씬 → `Playtest` 오브젝트 → `Character Prefab`을 `Assets/ChessFight/RagdollLab/Prefabs/RagdollPawn.prefab`으로 바꾼다. KingRush에는 `PhysicsProfile`(120Hz/24회)이 이미 있다. 튜닝 에셋은 프리팹에 연결되어 있다. **Unity에서 아직 시험하지 않았다.** 시험 후 결과를 VALIDATION에 적는다.

## 5. 멀티를 위해 계속 지킬 것

1. 캐릭터는 입력을 직접 읽지 않는다. 모든 의도는 `SetInput` / `SetCommand`로만. 새 동작은 `PawnInput`·`CharacterCommand`에 필드를 늘린다.
2. 이동 방향은 월드 좌표. 카메라 기준 변환은 입력을 만드는 PC에서 끝낸다(`LabGame.ReadInput`이 그렇게 한다).
3. "누른 순간"은 다음 FixedUpdate까지 보관한다(`input.jump |= next.jump`).
4. 랜덤·시간 누적에 의존하지 않는다.
5. 상태를 작게 요약할 수 있게 둔다.
6. 물리 주기를 래그돌 코드 안에서 바꾸지 않는다. 게임 씬은 `PhysicsProfile`이, 랩은 `LabGame`이 맡는다(자동 점검 예외).
7. 장애물 판정은 물리로. 넉다운이 필요하면 장애물에 `RagdollHazard`.
8. **`Assets/Scripts`의 코드가 래그돌 타입을 직접 참조하지 않는다.** 래그돌 어셈블리는 Steam을 참조하지 않는다(경계 검사가 막는다).

## 6. 알려진 사항

- 60Hz 이하에서는 골반이 약 8cm 주저앉는다(랩 측정). 래그돌 씬에는 120Hz가 필요하다.
- 래그돌 점프 약 1.76m, 임시 캐릭터 약 1.1m.
- 모델·텍스처는 Git LFS다. LFS 없이 받으면 `Pawn.fbx`가 몇 줄짜리 텍스트로 보인다(`git lfs pull`).
- 랩 튜닝 패널 문구는 IMGUI 기본 폰트다. 한글이 네모로 보이면 알려 달라(로비 HUD와 폰트 경로가 다르다).

## 7. 준영 님 AI 시작 프롬프트

```text
ChessFight 저장소에서 래그돌 작업을 이어갑니다. 저장소 루트 HANDOFF.md를 먼저 읽고,
Docs/Player/RAGDOLL.md와 Docs/RagdollLab/README.md를 읽으세요.
래그돌 랩은 Network 브랜치에 병합되어 있고, RagdollTest 씬(Assets/Scenes/RagdollTest.unity)이 랩 씬입니다.
RagdollPawn은 SetInput(PawnInput)으로만 움직이고, RagdollDriver가 ICharacterDriver로 연결합니다. 이 구조를 유지하세요.
RAGDOLL.md 5절 규칙을 반드시 지키세요. 특히 Assets/Scripts 코드가 래그돌 타입을 참조하거나,
래그돌 어셈블리가 Steam을 참조하면 안 됩니다. 작업 후 Tools/run-tests-linux.sh(또는 Windows의 Test 스크립트)로 확인하세요.
Assets/ChessFight/RagdollLab 폴더는 옮기지 않습니다.
```
