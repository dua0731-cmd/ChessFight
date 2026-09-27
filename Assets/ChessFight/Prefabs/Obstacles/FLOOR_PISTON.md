# 바닥 피스톤 (Floor Piston)

완성 프리팹: `Assets/ChessFight/Prefabs/Obstacles/13_FloorPiston.prefab`

프리팹만 생성했습니다. 기존 Scene에 배치하지 않았으며 플레이어 이동, 네트워크, 넉백, 체크포인트/복귀 등 기존 게임 스크립트를 수정하지 않았습니다.

```text
FloorPiston                   [ObstacleContext, FloorPiston]
├─ Base                       [고정 프레임 BoxCollider 4개]
│  └─ Socket                  [장식]
├─ Piston                     [Kinematic Rigidbody]
│  ├─ Solid cap               [BoxCollider]
│  ├─ Shaft                   [장식]
│  └─ Checker                 [장식]
├─ WarningArea                [교체 가능한 경고 GameObject]
└─ HitArea                    [판정 범위용 BoxCollider, 비활성화]
```

루트 Y가 바닥 높이이며, 대기 상태에서 피스톤의 충돌면이 바닥과 같은 높이에 있습니다. 프레임 외곽 4.6×4.6m, 피스톤 상판 3.9×3.9m, 발사 판정 중심 영역 3.8×3.8m입니다. 지하 장식은 약 3m 깊이까지 내려갑니다.

## Inspector

| 항목 | 기본값 | 의미 |
|---|---:|---|
| IdleDuration | 3초 | 바닥 높이에서 대기 |
| WarningDuration | 0.8초 | 상승 전 경고. 0이면 생략 |
| RiseDuration | 0.25초 | 빠르게 상승하는 시간. 최소 0.1초 |
| HoldDuration | 0.6초 | 상승 높이에서 유지 |
| FallDuration | 1초 | 다시 내려오는 시간. 최소 0.1초 |
| RiseHeight | 2.5m | 기본 바닥 높이에서 상승 거리 |
| LaunchForce | 18m/s | 기존 이동기에 전달하는 위쪽 발사 속도 |
| HorizontalForce | 4m/s | 가장자리에서 더하는 최대 수평 발사 속도 |
| StartDelay | 0.5초 | 첫 사이클 이전 추가 대기 |
| Repeat | true | false면 한 사이클 후 Idle로 복귀하여 유지 |

상태: `Idle → Warning → Rising → Hold → Falling → Idle`.
주기는 Idle+Warning+Rise+Hold+Fall이며 기본 5.65초입니다. StartDelay는 최초 한 번만 더합니다. Rise/Fall은 SmoothStep으로 움직입니다.

## 플레이어 발사

- 기존 플레이어는 CharacterController + PlayerMotor입니다. 새 Rigidbody나 별도 이동기를 붙이지 않습니다.
- 기존 `PlayerMotor.LaunchFromObstacle`를 호출합니다. Force라는 이름이지만 단위는 Rigidbody의 힘(N)이 아닌 기존 이동기에 전달하는 속도(m/s)입니다.
- **Rising 중 윗면이 상승하는 물리 프레임에서만** 발사합니다. Hold/Falling/Idle/Warning 중에는 발사하지 않습니다.
- 이전 윗면부터 다음 윗면까지 쓸고 지나간 범위를 OverlapBoxNonAlloc으로 검사합니다. 빠르게 올라오는 Trigger에만 의존하지 않습니다.
- 플레이어 발 위치가 상판 위 판정 영역 안에 있을 때만 적용합니다. 옆면에 몸통만 닿는 것은 제외합니다.
- 중심에서는 위쪽 발사가 주가 되며, 중심에서 가장자리로 갈수록 바깥 방향 수평 속도가 커집니다. BLUE/RED 모두 같은 방식입니다.
- 플레이어별 발사 기록을 사이클마다 저장합니다. 동일 Rising에 재진입해도 한 번만 발사하며, 다음 사이클에는 다시 발사할 수 있습니다.
- 실제 사망/데미지/강제 Respawn은 호출하지 않습니다. 이후 낙사는 기존 StageMap과 개인 체크포인트 복귀가 처리합니다.
- 온라인에서는 방장만 발사를 적용하고 기존 플레이어 위치 동기화를 사용합니다. 주기 운동은 기존 경기 시간으로 재생합니다. 실행 중 한쪽에서만 설정을 바꾸는 것은 동기화되지 않습니다.

## Warning과 판정 교체

- `WarningArea`의 주황색 테두리는 Warning 상태에서 활성화되고 다른 상태에서 꺼집니다. 하위 모델/Material/VFX를 교체하거나 다른 GameObject를 참조할 수 있습니다. 이 오브젝트의 활성 상태는 FloorPiston이 관리합니다.
- `OnWarning`, `OnRise` UnityEvent에 AudioSource.Play나 ParticleSystem.Play를 연결할 수 있습니다. 기본 음원은 포함하지 않습니다.
- `HitArea`의 BoxCollider는 **판정 모양을 읽기 위한 참조**여서 enabled가 꺼져 있는 것이 정상입니다. 실제 판정은 코드의 swept overlap query입니다. 루트를 선택하면 빨간 Gizmo로 현재 범위를 확인할 수 있습니다.
- HitArea는 루트의 직접 자식, 로컬 회전 0, Scale 1로 유지하세요. 크기와 중심은 실행 전에 설정하며, 런타임 캐시를 사용하므로 플레이 중 바꿔도 판정에 즉시 반영되지는 않습니다.

## 배치와 물리 주의점

- 루트 Scale (1,1,1), 수평 바닥을 기준으로 설계했습니다. Y축 회전은 가능하지만 경사진 배치나 비균일 스케일은 별도 검증하세요.
- 상판 아래의 바닥을 비워 지하 샤프트 공간을 확보하고, 외곽 프레임을 주변 바닥과 연결하세요. 프리팹은 지형에 자동으로 구멍을 내지 않습니다.
- 상판은 Kinematic Rigidbody와 BoxCollider를 사용합니다. Hold 상태에서는 기존 이동 플랫폼처럼 서 있을 수 있습니다. 발사 효과가 없어도 움직이는 상판 자체의 물리 접촉은 유지됩니다.
- 위쪽 천장이나 옆 벽과의 끼임을 피할 공간을 확보하세요. RiseHeight를 크게 하거나 RiseDuration을 지나치게 줄이면 피스톤이 발사된 플레이어를 다시 따라잡을 수 있으므로 수치를 함께 조정하세요.
- 기본 LaunchForce 18은 기존 중력 -24에서 대략 6.75m의 수직 상승을 만드는 시작값입니다. 충돌, 피스톤 높이, 움직임에 따라 실제 경로는 달라집니다.

## 테스트

1. Project 창에서 `13_FloorPiston.prefab`을 더블클릭하여 형태와 연결을 확인합니다.
2. Play Mode를 종료한 뒤 `CHESS FIGHT > Obstacles > Verify Floor Piston (Isolated Play)`를 실행합니다. 별도 임시 런타임 Scene에서 기존 플레이어 3명으로 검사한 뒤 자동 종료합니다. 맵에는 테스트 오브젝트를 저장하지 않습니다.
3. 결과: `Artifacts/ChessFight/FloorPistonVerification.txt`.
4. 미리보기: `Artifacts/ChessFight/ObstaclePreviews/FloorPiston_Idle.png`, `FloorPiston_Warning.png`, `FloorPiston_Hold.png`.
5. 직접 테스트할 때는 별도 테스트 Scene/맵 사본에 사용자가 배치하고 기존 경기 시작 기능을 실행하세요. 중앙/가장자리 발사, Hold 옆면 접촉, 상승 중 재진입, 다음 사이클 재발사, 낙사 복귀를 확인하세요.
6. 에셋이 없는 경우 `Create Floor Piston Prefab (No Map Placement)` 메뉴로 생성할 수 있습니다. 기존 프리팹은 덮어쓰지 않습니다.

자동 검증은 실제 두 PC Steam 지연 테스트나 12인 밸런스 테스트를 대신하지 않습니다.

신규 스크립트: `Runtime/Obstacles/FloorPiston.cs`, `Editor/FloorPistonPrefabBuilder.cs`, `Editor/FloorPistonVerification.cs`. 기존 스크립트 변경 없음.
