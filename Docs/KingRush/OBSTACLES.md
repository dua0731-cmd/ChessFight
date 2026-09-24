# 장애물

코드: `Scripts/Gameplay/Obstacles/`. 프리팹: `Prefabs/Obstacles/`. 기획서 양식: [OBSTACLE_TEMPLATE.md](OBSTACLE_TEMPLATE.md).

## 1. 단 하나의 규칙

> **장애물의 위치·회전은 `ObstacleClock` 시간의 순수 함수로만 계산한다. 매 프레임 누적하지 않는다.**

```csharp
angle += speed * Time.deltaTime;      // ❌ PC마다 시작 시점과 프레임이 달라 어긋난다
angle = speed * ObstacleClock.Now;    // ✅ 모든 PC가 같은 장면을 본다
```

- 오프라인: `ObstacleClock` = 씬 시간.
- 경기 중: `NetworkRuntime`이 공유 시계(Steam 서버 시간 + 로컬 소수부)로 바꾼다. **장애물 패킷을 한 번도 보내지 않고** 모든 PC가 같은 위치를 본다.
- 래그돌 랩의 `SpinningBar`는 누적 방식이라 쓰지 않는다.

## 2. 기반 클래스 `Obstacle`

- `protected abstract void Evaluate(double time, out Vector3 position, out Quaternion rotation)` 하나만 구현한다.
- `StartPosition`, `StartRotation`: 배치된 원래 자세.
- `Wave(time, period)`: -1 ~ 1 사인파. `WrapDegrees(degrees)`: double에서 360으로 감싼 뒤 float로(유닉스 시간 × 속도의 정밀도 손실 방지).
- `phase`(초): 같은 장애물을 엇박자로 둔다.
- Rigidbody는 자동으로 kinematic, `MovePosition/MoveRotation`으로 움직인다. `VelocityAt(point)`로 접점 속도를 준다(밀려나는 계산용).

## 3. 기존 장애물

| 컴포넌트 | 프리팹 | 움직임 | 주요 값 |
|---|---|---|---|
| `Spinner` | `Spinner.prefab` | 한 축으로 계속 회전 | 축, 초당 각도(음수 = 반대) |
| `Oscillator` | `SlidingWall.prefab` | 왕복 이동 | 이동량, 주기 |
| `Pendulum` | `Pendulum.prefab` | 진자 (피벗에 붙이고 팔·머리를 자식으로) | 축, 진폭, 주기 |

## 4. 새 장애물 예

```csharp
using UnityEngine;

namespace ChessFight.Gameplay
{
    // 위아래로 튀어나왔다 들어가는 기둥
    public sealed class PopUpPillar : Obstacle
    {
        [SerializeField] float height = 2f;
        [SerializeField] float period = 4f;

        protected override void Evaluate(double time, out Vector3 position, out Quaternion rotation)
        {
            rotation = StartRotation;
            float up = (Wave(time, period) + 1f) * .5f;   // 0..1
            position = StartPosition + Vector3.up * (up * height);
        }
    }
}
```

- 같은 시간이면 항상 같은 값. 멤버 변수를 누적하지 않는다.
- 부딪힌 결과(넘어짐 등)는 캐릭터가 물리로 처리한다. 래그돌 넉다운이 필요하면 `RagdollHazard`를 함께 붙인다.
- 정적 장애물(벽, 경사)은 스크립트 없이 콜라이더만.
- 새 스크립트는 `Scripts/Gameplay/Obstacles/`에(`ChessFight.Gameplay` 어셈블리). Steam 코드를 참조하지 않는다.

## 5. 제작 체크

- [ ] `Obstacle` 상속, `Evaluate`가 시간의 순수 함수
- [ ] 프리팹은 `Assets/Prefabs/Obstacles/`
- [ ] 오프라인 플레이테스트로 통과 가능
- [ ] 기획서 `Docs/KingRush/Obstacles/이름.md`
- [ ] 래그돌로 확인(래그돌 병합 후)
