# 진자 철구 (Pendulum Ball)

완성 프리팹: `Assets/ChessFight/Prefabs/Obstacles/11_PendulumBall.prefab`

기존 맵에는 배치하지 않았습니다. Project 창에서 프리팹을 더블클릭하여 Prefab Mode에서 확인하세요.

```text
PendulumObstacle                 [ObstacleContext]
├─ Pivot                         [Kinematic Rigidbody, PendulumSwing, ObstacleImpact]
│  └─ Chain
│     ├─ Suspension cable / Chain collars
│     └─ Ball                    [MeshRenderer, SphereCollider]
│        └─ ImpactSensor         [Trigger SphereCollider]
├─ Ceiling mount
└─ OptionalWarningArea           [장식만, 비활성화 가능]
```

루트는 통로 바닥 기준입니다. Pivot 높이 7.3m, 줄 길이 6m, 철구 지름 2.2m입니다. 루트 Y 회전으로 통과 방향을 바꿉니다. 기본값은 로컬 X 방향으로 흔들려 로컬 Z 방향 통로를 가로막습니다. 회전/이동은 배치 시 루트에서 조정하고 Scale은 `(1,1,1)`을 권장합니다.

| Pivot > PendulumSwing | 기본값 | 설명 |
|---|---:|---|
| Swing Angle | 55° | 중앙에서 한쪽 끝까지의 각도. 전체 범위는 ±55°. 지원 범위 0~85°. |
| Swing Duration | 2초 | 한쪽 끝에서 반대쪽 끝까지의 이동 시간. 코사인 곡선으로 끝에서 느리고 중앙에서 빠름. |
| Start Delay | 0.5초 | 경기 시작 후 왼쪽 끝에서 기다리는 시간. |
| Pause At Ends | 0.2초 | 각 끝에서 정지하는 시간. 최초 왼쪽 끝에서도 적용. 왕복 주기는 `2 × (SwingDuration + PauseAtEnds)` = 4.4초. |
| Knockback Force | 14 | 현재 철구 이동 방향으로 적용하는 수평 넉백 속도(m/s). Rigidbody 힘(N)이 아님. |
| Upward Force | 3 | 기존 이동기에 전달하는 위쪽 속도(m/s). 0으로 설정 가능. |
| Active | true | false면 현재 각도에서 정지하고 타격을 중단. 다시 켜면 제한된 각속도로 경기 시간에 따른 위치를 따라잡음. |
| Ball | Ball Transform | 철구 중심의 순간 이동 속도와 Gizmo 반경을 계산하는 참조. |
| Context | 루트 ObstacleContext | 기존 경기 시간과 방장 권한을 재사용. 일반 배치에서는 같은 Scene의 match를 자동 연결. |

`ObstacleImpact.hitCooldown`은 0.5초입니다. 기존 장애물과 같은 플레이어별 쿨다운을 사용하여 같은 프레임의 몸체 충돌/센서 접촉이 중복 넉백을 만들지 않습니다. 철구 프리팹에서는 넉백 값을 `PendulumSwing`에서 설정하며, ObstacleImpact의 일반 `strength/lift` 값은 사용하지 않습니다.

## 생성 방법

이미 프리팹 에셋이 생성되어 있으므로 다시 만들 필요가 없습니다. 파일이 없는 프로젝트에서는 `CHESS FIGHT > Obstacles > Create Pendulum Prefab (No Map Placement)`를 실행하세요. 임시 편집 Scene에서 에셋만 저장하고 현재 맵은 변경하지 않습니다. 기존 파일이 있으면 사용자의 수정 사항을 보존하기 위해 덮어쓰지 않습니다.

## 플레이 테스트

1. 자동 검증: Play Mode를 종료하고 `CHESS FIGHT > Obstacles > Run Isolated Play Verification` 실행. 임시 런타임 Scene에서 기존 플레이어 프리팹으로 검사하며 종료 시 테스트 오브젝트가 제거됩니다. 결과는 `Artifacts/ChessFight/ObstaclePlayVerification.txt`.
2. 직접 검증: 사용자가 별도 테스트 Scene/맵 사본의 평평한 통로에 배치하고 기존 경기 시작 기능을 실행합니다. 프리팹에는 통로 바닥이 포함되지 않습니다.
3. 양방향으로 다가가 철구가 오른쪽 이동 중이면 오른쪽으로, 왼쪽 이동 중이면 왼쪽으로 밀리는지 확인합니다. BLUE/RED 모두 동일하게 적용됩니다.
4. 끝점 대기, StartDelay, Active off/on을 테스트합니다. Pivot 선택 후 Scene View의 Gizmos를 켜면 노란 이동 호와 양끝 범위, 빨간 현재 충돌 구가 표시됩니다.
5. 낙사 구간 옆에 배치할 때는 넉백 거리와 기존 개인 체크포인트 복귀를 확인하세요. 철구는 데미지·사망·직접 Respawn을 호출하지 않습니다.

## 기존 물리/온라인과의 관계

- 플레이어는 기존 CharacterController + PlayerMotor를 유지합니다. 기존 `AddObstacleImpulse`를 호출하며 플레이어에 Rigidbody를 추가하지 않습니다.
- 철구는 Pivot의 kinematic Rigidbody와 Ball의 SphereCollider를 사용합니다. 중력 Joint 시뮬레이션 대신 경기 시간에 따른 결정적 운동을 사용합니다.
- 작은 Trigger 센서가 정지해 있는 CharacterController도 감지합니다. 센서는 충돌 구보다 약 12% 넓어 물리 접촉 직전에 넉백이 시작될 수 있습니다.
- 기존 PlayerMotor의 수평 합산 속도 제한 22m/s와 감속 8m/s²를 그대로 따릅니다. 아주 큰 KnockbackForce가 비례해서 커지지는 않습니다.
- 벽과 철구 사이에 너무 좁은 틈을 만들면 CharacterController가 끼일 수 있습니다. 최소 플레이어 지름(1m)에 여유 폭을 더하고, SwingDuration을 지나치게 낮추지 마세요. CCD가 있어도 매우 빠른 운동/낮은 프레임에서는 관통 여부를 별도 확인해야 합니다.
- Pivot/Rigidbody와 Ball 사이에 다른 Rigidbody를 추가하지 마세요. 줄과 장식은 충돌하지 않습니다. 루트의 비균일 Scale은 물리 구와 회전 범위를 왜곡하므로 피하세요.
- 방장만 넉백을 적용하고 기존 플레이어 위치 동기화로 결과를 전달합니다. 경기 시간은 기존 ObstacleContext를 사용합니다. 이번 철구 추가를 위해 네트워크 프로토콜/플레이어 이동 코드는 변경하지 않았습니다.
- Inspector 값은 빌드/저장된 Scene이 양쪽에서 같아야 합니다. 실행 중 방장만 Active나 주기를 바꾸는 기능은 별도 네트워크 이벤트로 동기화되지 않습니다. 실제 두 PC Steam 지연 테스트는 별도입니다.

## 변경 파일

- 신규: `Runtime/Obstacles/PendulumSwing.cs`, `Editor/PendulumPrefabBuilder.cs`, `11_PendulumBall.prefab`, 이 문서.
- 수정: `Runtime/Obstacles/ObstacleImpact.cs`에 선택적 진자 속도/힘 참조 추가. 기존 motion만 지정한 장애물 동작은 유지.
- 수정: `Editor/ObstacleVerification.cs`에 진자 운동·양방향 충돌·쿨다운·Active·권한 검증 추가.
