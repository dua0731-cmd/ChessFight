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
- 실행하면 `LabGame`이 **P1(키보드+마우스), P2(패드 또는 방향키), 더미 1개**를 만든다. Tab 튜닝 패널, R 전체 리스폰, T 슬로모션, F4 자유 카메라(09-26에 F에서 옮김)
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
public struct CharacterCommand
{
    public Vector3 Move;                  // 월드 좌표
    public bool Jump, Shove, Grab;        // 누른 순간, 누른 순간, 누르고 있기
    public bool Sprint;                   // 누르고 있기 (09-26 추가, 아래 §8)
    public bool Ability, Ability2;        // 누른 순간: E, Q
    public bool Interact;                 // 누르고 있기: F (앙파상 0.4초 때문)
    public Vector3 Aim;                   // 월드 단위 벡터: 카메라가 보는 방향
}
public interface ICharacterDriver
{
    void SetCommand(in CharacterCommand command);
    Transform FollowTarget { get; }                        // 카메라가 따라갈 것 (래그돌은 골반)
    void Teleport(Vector3 position, Quaternion rotation);  // position = 발이 닿을 바닥 지점. 벽·손·잡힌 상태를 모두 푼다
}

// Assets/Scripts/Gameplay/Characters/IHitReceiver.cs (09-26 추가)
public interface IHitReceiver
{
    // push = 속도 변화(m/s), knockdownSeconds 0 = 비틀·그 이상 = 그만큼 누워 있기,
    // staminaDamage = 스테미나(초 단위, 가득 = 8), dropFromWallOrRide = 벽·턱(나중에 갈고리·밧줄)을 놓고 떨어짐
    void ApplyHit(Vector3 push, float knockdownSeconds, float staminaDamage, bool dropFromWallOrRide);
}
```

`Assets/ChessFight/RagdollLab/Scripts/RagdollDriver.cs`가 이것을 구현한다.
- `SetCommand` → `RagdollPawn.SetInput`. 필드는 이름만 바꿔 복사한다.
- `ApplyHit` → `RagdollPawn.TakeHit`(09-26, §8).
- 다른 코드는 `GetComponent<IHitReceiver>()`로 찾는다. 래그돌 타입을 몰라도 된다.
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

## 8. 퀸 오브 더 힐 기능 (M1~M4)

2026-09-26, 브랜치 `JY-ragdoll_v2`, 요청 R34·R35. 무엇을 왜 만드는지와 완료 기준은 [MECHANICS_TODO](../GameModes/QueenOfTheHill/MECHANICS_TODO.md). 자동 점검(빌드한 랩 플레이어) 통과, **사용자가 Unity에서 1차 12개 확인(09-26, 모두 성공)**. 그 뒤 의견대로 고친 것(넘어진 뒤 1초 누워 있기, 물에 5초 뜨기·버둥)은 재확인 대기 → [VALIDATION](../Network/VALIDATION.md) 최상단 표.

### 8.1 새 입력 (M2)

| 입력 | P1 키보드·마우스 | 게임패드 | P2 키보드 | 종류 |
|---|---|---|---|---|
| 전력질주 `Sprint` | 왼쪽 Shift | LT·왼쪽 스틱 누르기 | `/` | 누르고 있기 (원래 있던 것을 약속에 추가) |
| 능력 `Ability` | **E** | Y | `.` | 누른 순간 |
| 능력2 `Ability2` | **Q** | RT | `,` | 누른 순간 |
| 상호작용 `Interact` | **F** | X | `'` | **누르고 있기** (앙파상은 0.4초 누르기라서. 누른 순간은 래그돌이 스스로 센다) |
| 조준 `Aim` | 카메라가 보는 방향(위아래 포함) | 〃 | 〃 | 월드 단위 벡터 |

- 경로: `CharacterCommand`(Gameplay) → `RagdollDriver.SetCommand` → `PawnInput`. 랩은 `LabGame.ReadInput`이, 킹러시 플레이테스트는 `MoveInputSource`(Game)·`PlaytestSpawner`가 채운다.
- **랩의 자유 카메라는 F → F4로 옮겼다**(F가 상호작용이 됐다).
- 래그돌은 아직 이 키로 아무것도 하지 않는다. 받은 값(누른 횟수, 누르는 중, 조준)을 `AbilityPresses`·`InteractHeld`·`Aim` 등으로 보여 줄 뿐이다. 시험대 창(화면 왼쪽 아래)에 보인다.
- 온라인 랩: 입력 패킷이 24 → **27바이트**(버튼 비트 3개 + 조준 3바이트), magic `CFR2`, **프로토콜 v4**. 상호작용은 보낼 때까지 눌림을 붙잡아 두어서 짧게 톡 눌러도 방장에게 간다.

### 8.2 탈것 (M1)

- `Gameplay/Obstacles/IMovingSurface`: `PointVelocity(점)`, `DeltaRotation`. **모든 `Obstacle`이 구현한다**(회전 봉·진자 위에 서도 실려 간다).
- `Obstacle`은 이번 물리 스텝의 움직임을 **먼저 묻는 쪽이 계산**하게 바꿨다. 래그돌이 장애물보다 먼저 도는데(실행 순서 −50), 예전처럼 장애물 자신의 FixedUpdate에서만 계산하면 래그돌은 한 스텝 늦은 값을 읽었다(3 m/s 승강기에서 2.5 cm씩). 장애물 포즈는 여전히 `ObstacleClock` 시간의 순수 함수다(규칙 7).
- 새 부품 `Gameplay/Obstacles/MovingPlatform`: 놓인 자리에서 `travel`(로컬 오프셋)까지 `speed`로 갔다가 돌아온다. `rampTime` 동안 가속·감속, 양 끝에서 `pause` 대기, `spinDegreesPerSecond`로 계속 회전(원판·궤도 고리). 박스 콜라이더가 있는 오브젝트에 붙이면 된다(Rigidbody는 자동으로 붙고 키네마틱이 된다). **`rampTime`은 `speed ÷ 6` 이상**: 올라가던 승강기가 9.81 m/s²보다 세게 서면 탄 사람이 위로 튕겨 나간다.
- 래그돌(`RagdollPawn`):
  - 발밑이 움직이면 **이동을 발판 기준으로 계산하고 발판 속도를 더한다**(수직 포함). 서 있으면 발판 위에 가만히 있고, 달리기·걸음 동작도 발판 기준 속도로 정해진다(`GroundSpeed`).
  - 원판 위에서는 몸 방향이 원판과 같이 돈다(수직축 회전만).
  - 점프는 **발판 기준으로** 뛴다(올라가는 승강기에서도 제대로 뛴다). 공중에서는 **마지막으로 밟은 것의 속도를 유지**해서 달리는 발판에서 뛰어도 다시 그 발판에 내린다.
  - **벽에 매달린 상태**에서 벽이 움직이면(`MovingPlatform`에 붙은 벽) 몸·두 손의 짚은 자리·모서리/올라서기 경로가 벽과 같이 움직인다. 놓으면 벽 속도를 가지고 떨어진다.
  - 탈것 판정은 `Riding`.
  - **알려진 동작(그대로 둠, R35):** 올라가던 승강기가 멈추는 순간 점프하면 발판 속도가 더해져 **예상보다 조금 높게** 뛴다. 나중에 활용할 수도 있어서 고치지 않는다.

### 8.3 피격 (M3)

- `Gameplay/Characters/IHitReceiver.ApplyHit(push, knockdownSeconds, staminaDamage, dropFromWallOrRide)`. `RagdollDriver`가 구현하고 `RagdollPawn.TakeHit`으로 넘긴다. 공격 코드는 `GetComponentInParent<IHitReceiver>()`로 찾으면 되고 래그돌 타입을 몰라도 된다.
- `push`: 몸 전체의 속도 변화(m/s). `knockdownSeconds` 0이면 **비틀**(피격 강성·발 미끄러짐, 이동 앵커가 같이 밀려서 끌려 돌아오지 않음), 그보다 크면 넘어져서 **바닥에 닿은 뒤부터 그 시간 동안 누워 있다가** 일어난다(날아가는 시간은 세지 않는다 — 1차에는 맞은 순간부터 세어서 6 m/s 피격이면 내리자마자 일어났다(R35). 3초 넘게 바닥에 못 닿으면 그냥 센다. 이미 누워 있으면 다시 센다). 물에 떠 있으면 넘어지지 않는다. `staminaDamage`는 스테미나 초(가득 = 8). `dropFromWallOrRide`면 벽·턱·손에 쥔 것을 놓고 떨어진다(다시 잡기 0.6초 대기). drop 없이 벽에 매달린 폰에게는 스테미나만 깎인다.
- 네트워크 인형(참가자 화면의 폰)은 피격을 무시한다. 방장만 계산한다.
- 기획 메모(R35): 비숍 돌조각은 스테미나 감소보다 **느리게 날아와 물리적으로 밀쳐내는 돌**이 될 가능성이 높다. 이 약속은 밀기만 써도 되므로 바꿀 코드는 없다.

### 8.4 물과 부활 (M4)

- `Gameplay/Course/WaterZone`: 트리거. 캐릭터의 어느 부위든 닿으면 **캐릭터 단위로 한 번** `WaterZone.Entered(driver, zone)`을 알린다(부위마다 세었다가 다 나가야 다시 알림). `RespawnDelay` 기본 **5초**(R35, 처음엔 2초). `WaterZone.SurfaceAt(점)`으로 수면 높이를 물을 수 있다(상자는 회전하지 않은 것으로 읽는다). **알리기만 한다.** 부활은 모드가 한다: `PlaytestSpawner`(킹러시 등)는 2초 뒤 마지막 체크포인트로, 랩 시험대는 시험대 체크포인트로 보낸다. 물리 콜백 안에서 바로 순간이동하지 말고 이렇게 **다음 Update로 미룬다.**
- **물에 뜨기(R35):** 골반이 수면에 닿으면 `Floating`이 된다. 벽·손에 쥔 것을 놓고, 넘어져 있었으면 일어나며, 물 위에 **둥둥** 뜬다(골반은 수면 아래 약 0.2 m, 가슴께까지 잠기고 머리·어깨·팔은 물 밖, 2.2초 주기 5 cm 출렁임). 부위마다 부력을 다르게 주면 다리가 떠서 뒤집히므로 **몸 전체에 같은 부력**(골반 깊이를 맞추는 스프링 + 물 저항)을 준다. 이동 앵커는 몸을 놓고 똑바로 서 있게만 한다. 팔을 옆으로 젓고 다리를 천천히 찬다. 걷기·점프·잡기·등반·넘어짐은 없다. **좌클릭은 버둥대기**(팔다리 파닥, 살짝 첨벙) — **아무 효과도 없고 스테미나도 안 든다.** 출렁임은 공유 시계의 순수 함수라 PC마다 같다.
- `ICharacterDriver.Teleport`(래그돌은 `RagdollPawn.Teleport`)는 이제 **모든 동작을 푼다**: 등반(운동학 자세 포함)·올라서기·모서리, 두 손, **나를 잡고 있던 상대의 손**, 버둥대기·잡힘, 턱 넘기, 던지기, 넉다운 시간, 탈것 속도. 잡기 버튼을 누른 채 부활해도 바로 무언가를 잡거나 벽에 붙지 않는다(버튼을 한 번 떼야 한다). 나중에 갈고리·밧줄도 여기서 푼다(`ClearActions`).
- **고친 기존 버그(`PawnHand`)**: 무언가를 쥔 채 충돌로 넘어지면 충돌 콜백 안에서 손을 놓는데, Unity는 물리 콜백 안의 `DestroyImmediate`를 거부한다("Destroying components immediately is not permitted…" 오류). 그래서 잡기 관절이 **아무도 모르는 채 남아** 계속 붙잡고 있었고, 일어나서 다시 잡으면 관절이 2개가 됐다. 물 점검에서 이 남은 관절이 부활 직후 폰을 물 쪽으로 초속 20 m로 끌고 갔다. 이제 손을 놓을 때 그 손의 잡기 관절을 **전부** 지우고, 물리 콜백 안이면 프레임 끝에 지운다(`PawnHand.PhysicsCallbackDepth`).

### 8.5 [7] 퀸 오브 더 힐 시험대 (RagdollTest 남동쪽)

`QueenHillTestBed`가 랩을 시작할 때 **코드로** 만든다(씬을 다시 빌드할 필요 없음, 자동 점검도 같은 것을 쓴다). 아레나 남쪽 끝(z = −15)에 붙은 바닥 x 5~35, z −15~−45.

| 자리 | 무엇 | 확인할 것 |
|---|---|---|
| [7a] 왕복 발판 | 3×3 m, 8 m를 2 m/s로 왕복(끝에서 1.5초 대기) | 올라타 10초 서 있기, 달리는 중 점프 → 다시 발판에 착지 |
| [7b] 승강기 | 3×3 m, 3 m/s로 12 m 오르내림(끝에서 2초). 옆 탑 꼭대기로 건너갈 수 있다(틈 0.3 m) | 한 바퀴 타기, 올라가는 중 점프 → 다시 착지 |
| [7c] 회전 원판 | 지름 8 m, 20°/초 | 가장자리 쪽에 10초 서 있기, 몸이 같이 돈다 |
| [7d] 움직이는 벽 | 4×5 m 벽, 8 m를 1 m/s로 옆으로 왕복 | W + 우클릭으로 매달려 가만히 있기 |
| [7e] 물 | 수영장, 부두, 부두 끝의 기둥(물 속까지 이어진 벽), 상자, 초록 체크포인트 | 기둥에 매달려 옆으로 가서 아래로 → 물 / 상자를 잡고 물에 뛰어들기 → 둥둥 뜨다가(좌클릭 버둥) 5초 뒤 체크포인트 |

키(오프라인, P1): **F9** 시험대 입구로 이동, **F5** 피격 6 m/s·1초 넘어짐, **F6** 스테미나 −2.5, **F7** 벽·탈것에서 떨어뜨리기. 화면 왼쪽 아래 창에 받은 입력, 탈것 여부, 피격, 물 부활 횟수가 보인다(Tab 패널이 열려 있으면 숨는다).

### 8.6 네트워크

- 바뀐 것은 **랩 입력 패킷**뿐이다(8.1). 스냅샷(폰당 64바이트)은 그대로다.
- 탈것·피격·물은 **방장만 계산**한다(랩 방식). 참가자 화면의 인형은 피격·물 부활을 무시하고 방장이 보낸 자세를 그린다.
- **발판은 공유 시계로 모든 PC에서 같다.** 랩 온라인 경기 동안 `SteamRagdollLink`가 장애물 시계를 Steam 서버 시계에 맞춘다. 다만 서버 시계를 그대로 쓰지 않고 **물리 스텝마다 고르게** 흐르게 하고(서버 시계는 프레임 단위로 뛰어서 승강기가 덜컥거린다) 서버 시계와의 차이만 매 프레임 조금씩 맞춘다. 참가자는 인형을 약 110 ms 늦게 그리므로 발판도 그만큼 늦게 돌려서, 승강기에 탄 폰이 승강기 위에 그려진다. 시계가 바뀌는 순간(경기 시작·끝) 장애물은 속도 없이 그 자리로 옮겨 간다(`Obstacle`). **두 PC로 확인하지 않았다.**
- 경기 씬의 래그돌 동기화와 새 상태(갈고리·기물 등)의 스냅샷은 M14에서 한다.
