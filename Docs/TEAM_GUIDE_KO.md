# ChessFight 팀 작업 가이드

작성: 2026-09-24 · 기준 Unity `6000.3.11f1` · 기준 브랜치 `main`

이번 주차 역할 분담에 맞춰 **각자 main에 바로 작업을 올릴 수 있도록** 만든 개발 환경의 설명서다. 처음 합류하는 사람과, 각자 쓰는 AI 모두 이 문서부터 읽는다.

| 담당 | 이번 주 할 일 | 이 문서에서 볼 곳 |
|---|---|---|
| 메인 기획 | 파티·매칭 마무리, 기초 개발 환경 | 1~4장, 9장 |
| 준영 | 래그돌 모델링 + 이동·점프·잡기·밀기 프리팹, 멀티 고려 | 1~4장, **5장** |
| 진호 · 지성 | 킹러시 맵 + 장애물 세부 기획·제작 | 1~4장, **6장** |
| 승규 | 멀티 구조 숙지 → 지연·호스트 이탈 등 방어 기획 | 1장, **7장** |

---

## 1. 씬 구성과 흐름

```text
 Intro ──아무 키──▶ Lobby ──경기 시작──▶ KingRush ──경기 종료/Esc──▶ Lobby
 (타이틀)          (파티·매칭)          (게임 씬)
                                        승격쟁탈전 등 다른 모드도 같은 자리에 추가

 RagdollTest  ← 개발 전용. 게임 흐름에 포함되지 않는다.
```

| 씬 | 파일 | 무엇 | Play를 누르면 |
|---|---|---|---|
| Intro | `Assets/Scenes/Intro.unity` | 타이틀. Steam 초기화 | **온라인** 흐름 시작 |
| Lobby | `Assets/Scenes/Lobby.unity` | 파티·매칭·친구 초대·봇 | **온라인** |
| KingRush | `Assets/Scenes/KingRush.unity` | 킹러시 코스 | **오프라인 플레이테스트** |
| RagdollTest | `Assets/Scenes/RagdollTest.unity` | 래그돌 통합 시험장 | **오프라인 플레이테스트** |

메뉴 `ChessFight > Scenes`에서 바로 연다.

**핵심 한 가지:** Steam 세션은 이제 씬에 속하지 않는다. `NetworkRuntime`이 한 번 만들어져 씬이 바뀌어도 살아남는다. 그래서 로비에서 게임 씬으로 넘어가도 파티와 경기가 끊기지 않는다. 경기가 시작되면(모든 클라이언트가 `phase=playing`을 봄) 전원이 KingRush로, 경기가 끝나면 로비로 자동 이동한다.

**KingRush를 직접 열고 Play하면 Steam이 켜지지 않는다.** 맵·장애물 작업은 매칭 없이 혼자 바로 돌려볼 수 있다.

---

## 2. 폴더 규칙

```text
Assets/
  Art/                원본 모델·텍스처·애니메이션         준영 (+ 그래픽)
  Materials/
  Prefabs/
    Characters/       플레이어 캐릭터 프리팹              준영
    Obstacles/        장애물 프리팹                       진호 · 지성
  Resources/          코드가 이름으로 읽는 것만 (UI, 입력 액션)
  Scenes/
  Scripts/            폴더 하나 = 어셈블리 하나
    Core/             네트워크 규칙·봇 (순수 C#)            메인
    Network/          Steam 로비·파티·이동 전송             메인 · 승규
    Game/             HUD·카메라·입력                       메인
    Gameplay/         ▼ 모두가 쓰는 게임 로직 (Steam 모름)
      Characters/     캐릭터 계약 ICharacterDriver         준영
      Obstacles/      장애물 기반 클래스와 예시             진호 · 지성
      Course/         스폰·체크포인트·골인
      Playtest/       오프라인 플레이테스트
    Bootstrap/        씬 흐름, 경기 화면                     메인
    Input/ Editor/
```

- **`Gameplay`에서 Steam 코드를 참조하지 않는다.** 캐릭터·장애물은 네트워크를 몰라야 오프라인에서도, 호스트에서도, 봇으로도 돌아간다.
- 새 스크립트는 담당 폴더에 만든다. asmdef가 없는 폴더(예: 래그돌 랩)에 두면 `Gameplay`를 가져다 쓸 수는 있지만, 반대로 `Gameplay`가 그 코드를 참조할 수는 없다. 그래서 캐릭터를 부르는 쪽은 항상 인터페이스(`ICharacterDriver`)를 쓴다.
- **`.meta` 파일은 반드시 같이 커밋한다.** 빠지면 다른 사람 PC에서 참조가 전부 끊긴다.

---

## 3. 브랜치·병합 규칙

1. `main`에서 각자 브랜치를 딴다. 예: `feature/ragdoll`, `feature/kingrush-map`, `feature/obstacles`.
2. 끝나면 `main`으로 Pull Request. 올리기 전에 `main`을 자기 브랜치에 먼저 병합해서 충돌을 자기 쪽에서 해결한다.
3. **같은 `.unity` 씬을 두 사람이 동시에 고치지 않는다.** Unity 씬은 텍스트지만 사람이 병합하기 거의 불가능하다.
   - `KingRush.unity`는 한 번에 한 명만 편집한다 (진호·지성이 교대로, 누가 잡고 있는지 공유).
   - 맵은 **구간별 프리팹**(`Prefabs/Obstacles/`나 `Prefabs/KingRush/Section_01.prefab`)으로 만들고, 씬에는 배치만 한다. 그러면 둘이 동시에 작업해도 충돌이 안 난다.
4. 병합 도구: `.gitattributes`에 Unity 전용 병합(`unityyamlmerge`)이 지정돼 있다. 각자 PC에서 한 번 설정해야 동작한다.
   ```text
   git config merge.unityyamlmerge.name "Unity SmartMerge"
   git config merge.unityyamlmerge.driver "'C:/Program Files/Unity/Hub/Editor/6000.3.11f1/Editor/Data/Tools/UnityYAMLMerge.exe' merge -p %O %B %A %A"
   ```
5. **모델·텍스처(`.fbx` `.png` `.psd` `.wav` 등)는 Git LFS로 올라간다.** GitHub Desktop은 LFS를 자동 처리한다. 명령줄을 쓰면 `git lfs install`을 한 번 해둔다. LFS 없이 받으면 모델이 몇 줄짜리 텍스트로 보인다.
6. **건드리면 안 되는 것**
   - `Packages/manifest.json`, `packages-lock.json`이 자동으로 바뀌면 **Discard하지 말고 커밋**한다. Discard하면 다음 실행 때 다시 설치되며 끝없이 반복된다.
   - `Project Settings > Player > Active Input Handling`을 바꾸지 않는다. 이 프로젝트에는 uGUI가 없어서 이 값을 Both/New로 바꾸면 **HUD 클릭과 입력칸이 전부 죽는다.** (에디터 스크립트가 자동으로 되돌린다.)
   - 물리 주기(`Time > Fixed Timestep`)를 프로젝트 전체로 바꾸지 않는다. 래그돌 씬은 4장의 `PhysicsProfile`이 알아서 올린다.

---

## 4. 오프라인 플레이테스트 (모두 공통)

`KingRush` 또는 `RagdollTest`를 열고 Play.

| 입력 | 동작 |
|---|---|
| WASD | 이동 |
| Space | 점프 |
| 마우스 왼쪽 | 밀치기 |
| 마우스 오른쪽 (누르고 있기) | 잡기 |
| R | 마지막 체크포인트로 |
| Backspace | 처음부터 다시 (기록 초기화) |

- 씬의 `Playtest` 오브젝트(`PlaytestSpawner`)가 캐릭터 하나를 첫 스폰 지점에 만든다. 떨어지면(`fallLimit` 아래) 자동 리스폰.
- **지금 나오는 캡슐은 임시 캐릭터**(`Prefabs/Characters/PlaytestCharacter`)다. CharacterController라 코스 통과는 정확하지만 맞기·밀기·잡기는 흉내만 낸다. 준영님 래그돌이 들어오면 `PlaytestSpawner`의 `Character Prefab` 칸만 바꾸면 된다(5장).
- 체크포인트를 지나면 거기서 리스폰, 골인 지점에 닿으면 기록이 뜬다.
- 씬에 있는 `PhysicsProfile`이 이 씬 동안만 물리를 **120Hz / 솔버 24회**로 올린다. 래그돌 랩이 측정한 최소 조건이다(아래 5장). 로비로 나가면 원래 값(50Hz / 6회)으로 돌아간다.

---

## 5. 준영 — 래그돌을 main에 올리기

`JY-ragdoll` 브랜치의 래그돌 리그를 읽어봤다. **이미 멀티에 필요한 구조를 갖추고 있다.** `RagdollPawn`이 키보드를 직접 읽지 않고 `SetInput(PawnInput)`으로 **월드 좌표 명령**을 받는다. 호스트가 원격 입력을 그대로 넣을 수 있는 형태다. 그대로 살리면 된다.

### 5.1 병합

`JY-ragdoll`은 폴더 재구성 **이전**(`c9c1e6a`)에서 갈라졌다. 그래도 `main`을 병합하면 충돌 없이 합쳐진다. 래그돌 파일은 전부 새 파일(`Assets/ChessFight/RagdollLab/`)이고 `main`이 바꾼 파일과 겹치지 않는다. `Assets/ChessFight.meta`는 `main`에서 지워졌는데, Unity가 폴더를 보고 새로 만든다. 무해하다.

```text
git checkout JY-ragdoll
git merge origin/main
```

`Assets/ChessFight/RagdollLab/`은 **당분간 그 자리에 둔다.** 빌더 메뉴(`Rebuild Pawn + Scene`)가 경로를 알고 있어서 옮기면 깨질 수 있다. 완성된 캐릭터 프리팹만 `Assets/Prefabs/Characters/`에 두면 된다.

### 5.2 어댑터 하나 추가 (필수)

래그돌과 게임을 잇는 연결은 이것 하나다. 병합 후 `Assets/ChessFight/RagdollLab/Scripts/RagdollDriver.cs`로 추가하고 래그돌 프리팹 루트에 붙인다.

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

그다음 `RagdollTest` 씬 → `Playtest` 오브젝트 → `Character Prefab`에 래그돌 프리팹을 넣고 Play. KingRush도 같은 방법이다.

### 5.3 RagdollTest와 RagdollLab은 다르다

| | RagdollLab (준영님 것) | RagdollTest (이번에 만든 것) |
|---|---|---|
| 목적 | 물리 **손맛** 튜닝, 2인 로컬, 자동 점검 | 게임 시스템과의 **통합** 확인 |
| 입력 | 랩 전용 (패드·P2 키보드·튜닝 패널) | 게임 공통 입력 → `ICharacterDriver` |
| 확인할 것 | 강성·기상·밀치기 감각 | 체크포인트·리스폰·장애물 공통 클래스·카메라·물리 주기 |
| 들어있는 것 | 랩 전용 맵 | 경사 15°, 1m 턱, 2m 벽(혼자 오르기), 3m 벽(둘이), 밀 수 있는 상자, 회전봉, 진자 |

랩은 계속 튜닝용으로 쓰고, "게임에 들어가서도 똑같이 움직이는가"는 RagdollTest에서 본다.

### 5.4 멀티를 위해 지켜야 할 것 — AI에게 반드시 전달

1. **캐릭터는 입력을 직접 읽지 않는다.** 모든 의도는 `SetCommand`(=`SetInput`)로만 받는다. 지금 구조가 이미 그렇다. 새 동작(다이브, 감정표현 등)을 추가할 때도 `PawnInput`에 필드를 늘리는 방식으로 한다.
2. **이동 방향은 월드 좌표.** 카메라 기준 변환은 입력을 만드는 쪽(자기 PC)에서 끝낸다. 호스트는 남의 카메라를 모른다.
3. **점프·밀치기는 "눌린 순간"을 다음 FixedUpdate까지 보관**한다(`input.jump |= next.jump`). 지금 코드가 이미 그렇다. 네트워크로 입력이 늦게 와도 버튼이 씹히지 않는다.
4. **랜덤·시간 누적에 의존하지 않는다.** 같은 입력이면 같은 결과가 나와야 호스트 판정과 화면이 맞는다.
5. **상태를 작게 요약할 수 있게 둔다.** 나중에 호스트가 매 틱 보낼 것은 골반 위치·회전·속도와 상태(Active/Ragdoll/GettingUp, 잡는 중) 정도다. 이 값들을 한곳에서 꺼내고 넣을 수 있게 유지한다.
6. **물리 120Hz / 24회는 `PhysicsProfile`이 씬 단위로 준다.** 래그돌 코드 안에서 `Time.fixedDeltaTime`을 바꾸지 않는다 (랩 자동 점검 코드는 예외).
7. **장애물 판정은 지금처럼 물리로.** 장애물에 `RagdollHazard`를 붙이면 된다. 6장의 장애물 공통 클래스는 **움직임만** 맡고 맞는 판정에 끼어들지 않는다.

### 5.5 시작 프롬프트 (AI에게)

```text
ChessFight 저장소의 JY-ragdoll 브랜치에서 작업합니다. main을 먼저 병합하세요.
Docs/TEAM_GUIDE_KO.md 5장과 Docs/RagdollLab/README.md를 읽고 시작하세요.
RagdollPawn은 SetInput(PawnInput)으로만 움직입니다. 이 구조를 유지하세요.
목표: 래그돌 프리팹이 ICharacterDriver를 구현하게 하고(5.2 어댑터), RagdollTest 씬의
PlaytestSpawner에 넣어 이동·점프·잡기·밀기가 동작하는 것을 확인합니다.
멀티플레이 조건(5.4)을 반드시 지키세요: 입력 직접 읽기 금지, 월드 좌표 명령,
눌림 신호는 FixedUpdate까지 보관, 시간 누적·랜덤 의존 금지, 물리 주기는 PhysicsProfile에 맡김.
Assets/ChessFight/RagdollLab 폴더는 옮기지 말고, 완성 프리팹만 Assets/Prefabs/Characters에 둡니다.
```

---

## 6. 진호 · 지성 — 킹러시 맵과 장애물

### 6.1 지금 KingRush 씬에 있는 것

```text
z = -20 ~ 20   Start Platform   40 × 40   스폰 12곳 (청 6 / 주황 6)
z =  20 ~ 200  Runway           폭 16
                 z=50   Spinner A      회전봉 (점프로 넘기)
                 z=80   Checkpoint 1
                 z=100  Sliding Wall   좌우로 미끄러지는 벽 (피하기)
                 z=130  Pendulum       좌우로 흔들리는 진자
                 z=150  Checkpoint 2
                 z=180  Spinner B      반대로 도는 회전봉
z = 200 ~ 230  Finish Platform  Finish Zone
```

**이건 전부 자리를 잡아둔 뼈대다.** 크기·배치·장애물은 기획대로 갈아엎으면 된다. 스폰 지점·체크포인트·골인 지점 컴포넌트와 `Playtest`, `ChessFight Game Root`(네트워크 경기 화면용 설정)만 남겨둔다.

### 6.2 장애물 규칙 — 멀티 때문에 딱 하나

> **장애물의 위치·회전은 "시간의 함수"로만 계산한다. 매 프레임 누적하지 않는다.**

```csharp
// ❌ 이렇게 하면 PC마다 막대 위치가 달라진다
angle += speed * Time.deltaTime;

// ✅ 공유 시계에서 바로 계산 — 모든 PC가 같은 위치를 본다
angle = speed * ObstacleClock.Now;
```

누적 방식은 PC마다 시작 시점과 프레임이 달라서 조금씩 어긋나고, 한 화면에서는 맞았는데 다른 화면에서는 빗나가는 일이 생긴다. 시간의 함수로 계산하면 **장애물 정보를 네트워크로 한 번도 보내지 않아도** 모든 PC가 같은 장면을 본다. 경기 중에는 `ObstacleClock`이 모든 클라이언트가 공유하는 Steam 서버 시계를 쓴다.

참고: 래그돌 랩의 `SpinningBar`는 누적 방식이다. 맵에서는 아래 `Spinner`를 쓴다.

### 6.3 이미 있는 장애물 (`Scripts/Gameplay/Obstacles/`, `Prefabs/Obstacles/`)

| 컴포넌트 | 프리팹 | 움직임 | 주요 값 |
|---|---|---|---|
| `Spinner` | `Spinner.prefab` | 한 축으로 계속 회전 | 축, 초당 각도(음수=반대) |
| `Oscillator` | `SlidingWall.prefab` | 왕복 이동 | 이동량, 주기 |
| `Pendulum` | `Pendulum.prefab` | 진자 흔들림 | 축, 진폭, 주기 |

모두 `phase`(초)가 있어서 같은 장애물 여러 개를 엇박자로 놓을 수 있다. Rigidbody는 자동으로 kinematic이 된다.

### 6.4 새 장애물 만들기

`Obstacle`을 상속하고 `Evaluate` 하나만 쓴다.

```csharp
using UnityEngine;

namespace ChessFight.Gameplay
{
    // 예: 위아래로 튀어나왔다 들어가는 기둥
    public sealed class PopUpPillar : Obstacle
    {
        [SerializeField] float height = 2f;
        [SerializeField] float period = 4f;

        protected override void Evaluate(double time, out Vector3 position, out Quaternion rotation)
        {
            rotation = StartRotation;
            // Wave: -1..1, 시간의 함수. 0..1로 바꿔서 위로만 솟게.
            float up = (Wave(time, period) + 1f) * .5f;
            position = StartPosition + Vector3.up * (up * height);
        }
    }
}
```

- `Evaluate`는 **같은 시간이면 항상 같은 값**을 돌려줘야 한다. 멤버 변수를 누적하지 않는다.
- 회전 각도는 `WrapDegrees(초당각도 * time)`로 감싼다. 시계 값이 커서(유닉스 시간) float로 바로 곱하면 정밀도가 무너진다.
- 부딪혔을 때 결과(넘어짐 등)는 캐릭터가 물리로 처리한다. 래그돌 넉다운이 필요하면 장애물에 `RagdollHazard`를 같이 붙인다.
- 순수 정적 장애물(벽, 경사)은 스크립트 없이 콜라이더만 있으면 된다.

### 6.5 장애물 기획서

**장애물마다 기획서를 같이 만든다.** 양식은 `Docs/KingRush/OBSTACLE_TEMPLATE.md`. 완성본은 `Docs/KingRush/Obstacles/이름.md`로 둔다. 종류는 많을수록 좋다.

### 6.6 네트워크 경기에서의 현재 한계

경기로 KingRush에 들어가면 **지금은 코스를 달릴 수 없다.** 네트워크 캐릭터가 아직 로비용 평면 이동(가로세로 38m, 충돌 없음)을 쓰기 때문이다. 래그돌이 호스트에서 시뮬레이션되기 전까지 코스 검증은 **오프라인 플레이테스트**로 한다. 장애물 움직임은 이미 경기 중에도 모든 PC에서 같게 보인다.

### 6.7 시작 프롬프트 (AI에게)

```text
ChessFight 저장소 main에서 feature 브랜치를 만들어 킹러시 맵과 장애물을 작업합니다.
Docs/TEAM_GUIDE_KO.md 2·3·4·6장을 먼저 읽으세요.
Assets/Scenes/KingRush.unity가 작업 씬입니다. 직접 Play하면 오프라인 플레이테스트가 됩니다.
장애물은 Assets/Scripts/Gameplay/Obstacles/Obstacle을 상속하고 Evaluate만 구현합니다.
반드시 지킬 것: 위치·회전은 ObstacleClock 시간의 순수 함수로만 계산하고
프레임마다 누적하지 않습니다(멀티에서 모든 PC가 같은 장면을 보기 위함).
각 장애물은 Docs/KingRush/OBSTACLE_TEMPLATE.md 양식으로 기획서를 함께 만듭니다.
맵 구간은 프리팹으로 나눠 만들어 두 사람이 같은 씬 파일을 동시에 고치지 않게 합니다.
```

---

## 7. 승규 — 네트워크 방어 기획

먼저 읽을 것: `Docs/AI/NETWORK_HANDOFF_KO.md` (전체 구조), 그중 6~12장(파티·매칭·예약·이동 동기화)과 17장(알려진 한계). 그다음 `Docs/Network/VALIDATION.md`에서 실제로 확인된 것과 아닌 것을 구분한다.

지금 구조를 한 줄로: **서버 없음. 경기 방을 만든 플레이어 PC가 호스트로서 모든 이동을 판정하고, 나머지는 입력만 보낸다.**

기획해줬으면 하는 변수와, 지금 코드가 어떻게 되어 있는지:

| 변수 | 지금 동작 | 관련 코드 |
|---|---|---|
| 호스트 이탈 | 경기 즉시 종료, 전원 로비로. 호스트 이전 없음 | `SteamSession.PollMatch` |
| 호스트 강제 종료·네트워크 단절 | 12초간 호스트 신호 없으면 이탈 처리 | `SteamMotion.Update` |
| 입력 지연 | 클라이언트 예측 후 호스트 값으로 보정 | `SteamMotion` |
| 패킷 손실 | 입력·스냅샷 모두 비신뢰 전송, 최신 값만 사용. **점프 입력이 사라질 수 있음** | `MotionProtocol` |
| 장애물 동기화 | Steam 서버 시계(초 단위) + 로컬 시계로 소수점. **PC 간 실제 오차 미측정** | `NetworkRuntime.SharedClock` |
| 래그돌 물리 권한 | **미정.** 호스트가 12명 래그돌을 다 돌릴지, 각자 돌리고 호스트가 검증할지 | 준영님 작업과 맞물림 |
| 봇 | 호스트가 전부 계산. 호스트 부하 미측정 | `BotBrain`, `SteamMotion` |
| 부정행위 | 좌표를 받지 않고 입력만 받음. 악성 호스트 방어 없음 | — |

특히 **"래그돌을 누가 시뮬레이션하느냐"**가 킹러시 전체 구조를 정한다. 준영님 5.4의 조건들이 그 결정을 쉽게 하려는 것이니 같이 보면 좋다.

### 7.1 시작 프롬프트 (AI에게)

```text
ChessFight 저장소 main 브랜치의 멀티플레이 구조를 파악하고 방어 기획을 세웁니다.
Docs/AI/NETWORK_HANDOFF_KO.md와 Docs/Network/VALIDATION.md, Docs/TEAM_GUIDE_KO.md 7장을 읽으세요.
전용 서버 없이 Steam 로비 + 호스트 PC 판정 구조입니다.
검토할 것: 지연 감소, 호스트 이탈·강제 종료 대응(호스트 이전 가능성 포함), 패킷 손실 시 점프 누락,
장애물 공유 시계 오차, 래그돌 물리를 누가 시뮬레이션할지, 호스트 부하(12인 + 봇).
실제 검증 결과와 코드 추정을 구분하고, 결론은 Docs/Network/ 아래 문서로 남기세요.
```

---

## 8. 자주 막히는 것

| 증상 | 원인 | 해결 |
|---|---|---|
| Safe Mode로 열림 | 컴파일 오류. 대개 설치 안 된 패키지를 참조한 코드 | Console 첫 오류를 본다. 선택 패키지를 쓰는 코드는 asmdef로 막아야 한다 |
| HUD가 안 눌림 | Active Input Handling이 Both/New | Old로 두고 Unity 재시작 |
| 모델이 텍스트 파일로 보임 | Git LFS 없이 받음 | `git lfs install` 후 `git lfs pull` |
| manifest가 계속 바뀜 | 패키지 설치 결과 | 커밋한다. Discard 금지 |
| 래그돌이 주저앉음 | 물리 50Hz | 씬에 `PhysicsProfile` 있는지 확인 |
| 장애물이 PC마다 다른 위치 | 시간 누적 방식 | `Evaluate`를 시간의 순수 함수로 |
| KingRush에서 Steam이 안 켜짐 | 정상. 직접 열면 오프라인 | 온라인은 Intro나 Lobby에서 Play |

---

## 9. 메인 기획 — 남은 것

- **이 환경은 아직 Unity에서 열어보지 않았다.** 씬·프리팹·재질은 텍스트로 작성했다. 팀에 배포하기 전에 한 번 열어서 확인한다(`Docs/Network/VALIDATION.md` 최상단 체크리스트).
- `Network` 브랜치의 이 작업을 `main`으로 병합해야 팀원이 받는다.
- 파티·매칭 남은 일: 두 계정 양방향 이동 확인, 12인 매칭, 경기 종료 규칙.
- 네트워크 경기에서 코스를 달리게 하려면 래그돌 호스트 시뮬레이션(준영·승규 결론)이 선행되어야 한다.
