# 스프링 기둥 / 공기 분사구

프리팹만 생성하며 Scene에는 배치하지 않습니다. 기존 PlayerMotor, CharacterController, 네트워크/매치/체크포인트 스크립트는 수정하지 않습니다.

- `14_SpringPillar.prefab`
- `15_AirVent.prefab`

두 에셋 모두 `Assets/ChessFight/Prefabs/Obstacles`에 있습니다. Project 창에서 더블클릭하여 Prefab Mode로 확인할 수 있습니다.

## 스프링 기둥

```text
SpringPillar [SpringPillar, ObstacleContext]
├─ Wall housing [고정 BoxCollider]
├─ Pillar [Kinematic Rigidbody]
│  ├─ Solid head [BoxCollider]
│  ├─ Impact face
│  └─ Telescopic shaft / Spring collars
├─ TriggerArea [감지 영역 참조]
├─ HitArea [타격 영역 참조]
└─ WarningVisual
```

로컬 +Z(Forward)로 튀어나옵니다. 루트를 Y축으로 90/180/270° 회전하면 좌우/역방향으로 재사용할 수 있습니다. 루트는 바닥 높이, 벽의 전면 위치 기준입니다. 뒤쪽 하우징과 샤프트는 벽 속에 넣어 배치하세요. 기둥 머리는 1.5m 정육면체입니다.

| Inspector | 기본값 | 의미 |
|---|---:|---|
| TriggerArea | 3.5×2.4×4m | 플레이어 접근 감지 영역. BoxCollider Size/Center 및 자식 위치로 조절 |
| WarningDuration | 0.5초 | 돌출 전 경고 |
| ExtendDistance | 3m | 돌출 거리 |
| ExtendDuration | 0.2초 | 돌출 시간, 최소 0.1초 |
| HoldDuration | 0.6초 | 돌출 상태 유지 |
| RetractDuration | 0.8초 | 복귀 시간, 최소 0.1초 |
| Cooldown | 2초 | 복귀 완료 후 재발동 대기 |
| KnockbackForce | 12m/s | Forward 방향 넉백 속도 |
| UpwardForce | 1.5m/s | 위쪽 속도 |
| AutoTrigger | true | 접근 감지로 자동 발동. false면 코드에서 Trigger() 호출 |
| Repeat | true | false면 첫 동작 이후 추가 발동 차단 |
| RequireReentry | true | 구역을 나갔다 다시 들어오는 새 진입이 있어야 재발동 |

순서: `Ready → Warning → Extend → Hold → Retract → Cooldown → Ready`.
RequireReentry를 끄면 구역에 서 있어도 **전체 동작과 Cooldown이 끝난 후** 다시 발동합니다. 기본값은 켜짐입니다. 쿨다운 중 들어와 계속 머무르는 플레이어는 기본 설정에서 나갔다 다시 들어와야 합니다. 다른 플레이어의 새 진입도 Ready 상태에서 발동시킬 수 있습니다.

Extend 중 머리 앞면이 지나가는 범위를 검사하고, 기존 `AddObstacleImpulse`로 타격당 플레이어별 1회만 밀어냅니다. Hold/Retract/Cooldown에서는 강한 넉백을 적용하지 않습니다. Hold의 머리는 일반적인 고체 장애물로 남습니다. 직접 사망 처리는 없습니다.

## 공기 분사구

```text
AirVent [AirVent, ObstacleContext]
├─ VentModel [하우징 BoxCollider, 장식 그릴]
├─ WindTrigger [바람 영역 참조]
├─ WarningVisual
├─ VFXPoint [교체 VFX 연결 위치]
└─ WindVisual [Blowing 동안 표시되는 임시 바람 화살표]
```

| Inspector | 기본값 | 의미 |
|---|---:|---|
| IdleDuration | 3초 | 대기 |
| WarningDuration | 0.8초 | 분사 전 경고 |
| BlowDuration | 3초 | 바람 지속 시간 |
| WindStrength | 20 | WindDirection 방향의 가속도 크기 |
| WindDirection | (0,0,1) | 분사구 로컬 방향. 루트 회전을 따라감 |
| VerticalForce | 0 | 추가 월드 위쪽 가속도 |
| AirborneMultiplier | 1.3 | 공중에서 힘 배율 |
| GroundedMultiplier | 1 | 접지 상태 힘 배율 |
| MaxPushSpeed | 10m/s | 이 분사구가 누적시키는 수평 속도와 상향 발사 속도의 상한, 최대 22 |
| StartDelay | 0.5초 | 최초 추가 대기 |
| Repeat | true | false면 한 차례 분사 후 Idle 유지 |

순서: `Idle → Warning → Blowing → Idle`.
WindTrigger의 기본 크기는 3×3×6m이며 앞쪽 공간을 감쌉니다. BoxCollider의 Size/Center와 자식 위치/회전으로 범위를 조정하세요. 범위 안의 플레이어에게만 바람을 추가합니다. 범위를 벗어난 뒤 남은 관성은 기존 이동기의 감속으로 줄어듭니다.

수평 힘은 기존 `AddObstacleImpulse`에 작은 속도 변화를 프레임 시간에 비례하여 전달합니다. 기존 감속을 고려해 분사구 자신의 기여분을 추적하고 상한을 둡니다. 벽에 막혀도 바람 기여분을 무한히 쌓지 않습니다. 상향 힘도 작은 속도 증가로 처리합니다. 플레이어를 Transform으로 이동시키거나 새 Rigidbody/이동기를 추가하지 않습니다.

MaxPushSpeed는 이 바람 효과의 제한입니다. 플레이어 자신의 이동/점프, 다른 장애물 넉백, 여러 분사구가 합쳐진 최종 속도를 강제로 잘라내는 설정은 아닙니다. 기존 이동기의 합산 수평 넉백 제한 22m/s도 유지됩니다.

### 방향별 사용

- 벽형 수평 분사: 기본 방향 (0,0,1), 루트 Y 회전으로 통로 옆/뒤로 밀기.
- 바닥형 상향 분사: 루트를 X=-90°로 회전해 Forward가 위를 향하도록 배치. WindTrigger도 함께 위로 향합니다. 기본 그릴 중심의 위치를 실제 바닥에 맞추세요.
- 대각 분사: WindDirection을 (0,1,1) 등으로 설정하거나 VerticalForce를 추가. 방향만 바꾸면 WindTrigger 형태는 자동으로 회전하지 않으므로 원하는 바람 경로에 맞게 영역도 조정하세요.
- 위로 띄우려면 상향 가속도가 기존 중력(24m/s²)을 이길 만큼 충분해야 합니다. 접지/공중 배율도 함께 적용됩니다. 이번 구현은 수평 및 상향 바람용이며 하향 가속도는 적용하지 않습니다.

## 경고 / Trigger 공통

TriggerArea, HitArea, WindTrigger의 BoxCollider는 **쿼리 범위 참조용**으로 isTrigger=true, enabled=false입니다. 실제 감지는 OverlapBoxNonAlloc으로 수행하므로 비활성화 상태가 정상이며 이를 고체 Collider로 바꾸지 마세요.

WarningVisual은 경고 상태에서만 켜집니다. 원하는 Material/메시/VFX로 교체할 수 있습니다. 스프링의 OnWarning/OnExtend, 분사구의 OnWarning/OnBlowing/OnStopped UnityEvent로 AudioSource.Play 등 추가 연출을 연결하세요. 분사구 VFXPoint는 빈 연결 Transform입니다.

루트 Scale (1,1,1)을 권장합니다. 벽·천장과 기둥 사이 압착, 좁은 다리의 낙사, 여러 장애물 중첩은 배치 후 별도 검증하세요. 두 프리팹에는 바닥/낙사 판정/즉사 기능이 포함되지 않습니다.

## 온라인 범위

플레이어 힘은 방장만 적용하고 기존 위치 동기화로 결과를 전달합니다. 공기 분사구의 주기는 기존 경기 시간으로 재생합니다.

스프링은 플레이어 접근으로 발동하는 이벤트형입니다. 이번 작업에서는 **네트워크 프로토콜을 수정하지 않았으므로 발동 이벤트 자체의 전송은 추가하지 않았습니다.** 클라이언트 외형은 수신된 플레이어 위치의 접근을 감지해 재생하며, 지연이나 늦은 입장 시 방장과 시점이 어긋날 수 있습니다. 정확한 온라인 발동 시점 동기화는 후속 네트워크 작업이 필요합니다. 실제 두 PC Steam 검증은 이번 테스트에 포함되지 않습니다.

## 생성 / 검증

- `CHESS FIGHT > Obstacles > Create Spring Pillar and Air Vent (No Map Placement)`: 없는 프리팹만 생성. 기존 사용자 편집을 덮어쓰지 않음.
- `Verify Spring Pillar and Air Vent (Isolated Play)`: 기존 플레이어로 별도 임시 런타임 Scene에서 검사하고 종료. 원본 맵에 저장하지 않음.
- 결과: `Artifacts/ChessFight/SpringAndVentVerification.txt`.
- 미리보기: `Artifacts/ChessFight/ObstaclePreviews/SpringPillar_*.png`, `AirVent_*.png`.
- 직접 테스트할 때는 별도 테스트 Scene에 사용자가 배치하여 기둥 진입/재진입/쿨다운, Hold 옆면 접촉, 바람 범위 진입/이탈, 장시간 속도, 상향·대각 분사를 확인하세요.

신규 스크립트: `SpringPillar.cs`, `AirVent.cs`, `SpringAndVentPrefabBuilder.cs`, `SpringAndVentVerification.cs`. 기존 스크립트 변경 없음.
