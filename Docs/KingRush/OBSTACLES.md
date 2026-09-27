# 장애물

**R56 왕의 계단:** 외나무 횡풍, 긴 계단, `KingRushSkyMotion`의 복귀하는 칸/그림자 예고 손. [기획서](Obstacles/KINGS_STAIR.md). 결승은 별도 `KingRushFinalRules`의 T0에 맞춰 `KingRushFinalBoard`가 판을 붕괴시킨다. [결승 규칙](FINAL_COURSE.md). 공통 물리 튜닝은 변경하지 않았다.

**R55 성벽 코스:** `KingRushCastleMotion`의 왕복 발판/미는 벽/도개교/철퇴/성문은 모두 시간 함수다. [기획서](Obstacles/CASTLE.md). 시소 체스판은 반복 장애물이 아니라 몸 무게로 결정되는 방장 소유 미션 상태여서 `KingRushSeesawRules`/`KingRushSeesawBoard`로 분리한다. 공통 캐릭터는 기존 `IMovingSurface` 계약을 쓴다.

**R54 장난감 코스:** `KingRushToyMotion`(팽이 폰·굴러오는 머리·시계 버튼), 기존 `LaunchPad` 연못. 시간 함수·치수·물리 판정·확인 범위는 [장애물 기획서](Obstacles/TOY_BOX.md), 코스 편집은 [첫 연결 코스](OPENING_COURSE.md).

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
