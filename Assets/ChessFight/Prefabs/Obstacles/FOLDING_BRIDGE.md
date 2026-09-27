# 접이식 다리 (Folding Bridge)

프리팹: `Assets/ChessFight/Prefabs/Obstacles/12_FoldingBridge.prefab`

프리팹만 생성했습니다. 게임 Scene에는 배치하지 않았으며 기존 플레이어, 맵, 네트워크, 체크포인트/복귀 코드를 수정하지 않았습니다.

## 구조와 크기

```text
FoldingBridge                         [ObstacleContext, FoldingBridge]
├─ LeftPivot                         [Kinematic Rigidbody]
│  ├─ LeftBridge                     [BoxCollider, MeshRenderer]
│  └─ Checker / Panel edge           [장식]
├─ RightPivot                        [Kinematic Rigidbody]
│  ├─ RightBridge                    [BoxCollider, MeshRenderer]
│  └─ Checker / Panel edge           [장식]
├─ LeftLanding / RightLanding        [고정 BoxCollider]
├─ LeftHinge marker / RightHinge marker
└─ WarningVisual                     [AudioSource, 기본 클립 없음]
   ├─ Warning strip 0
   └─ Warning strip 1
```

루트 위치가 통로 바닥 높이입니다. 기본 진행 방향은 로컬 X축이며, 루트를 Y축으로 90° 회전하면 Z축 통로에 맞습니다. 폭은 6m, 양쪽 힌지 사이 거리는 8m, 양끝 고정 진입판을 포함한 길이는 약 14.1m입니다. 각 패널은 길이 3.89m, 폭 6m, 두께 0.3m입니다.

두 패널은 **바깥쪽 힌지**를 중심으로 회전하여, 평상시 중앙에서 만나던 끝이 위로 들리는 `/\` 형태를 만듭니다. 접힐수록 중앙 아래가 열립니다. 패널이 서로 겹치지 않도록 중앙 약 0.12m, 힌지 약 0.1m의 틈을 두었습니다. 장식에는 Collider가 없습니다.

## Inspector

루트의 FoldingBridge 컴포넌트에서 설정합니다.

| 항목 | 기본값 | 의미 |
|---|---:|---|
| OpenDuration | 5초 | 평평하게 열려 있는 시간 |
| WarningDuration | 1.5초 | 패널이 움직이기 전 경고 시간. 0이면 생략 |
| FoldDuration | 2.5초 | 접히는 시간. 펼칠 때도 같은 시간 사용 |
| ClosedDuration | 2초 | 최대 각도로 접힌 상태 유지 |
| FoldAngle | 70° | 양쪽 패널이 위로 회전하는 각도. 최대 80° |
| StartDelay | 1초 | 경기 시작 시 한 번만 적용하는 추가 대기 |
| Repeat | true | false면 한 사이클을 끝낸 후 평평하게 열린 상태 유지 |
| Simultaneous | true | 좌우 동시 접기/펼치기 |
| RightDelay | 0.6초 | Simultaneous가 꺼졌을 때 오른쪽 패널의 지연 |
| WarningFlashRate | 4 | 초당 점멸 주기 |
| LeftPivot / RightPivot | 프리팹 내부 참조 | 두 독립 Kinematic Rigidbody |
| context | 루트 ObstacleContext | 같은 Scene의 기존 경기 시간 자동 연결 |

상태 순서는 `Open → Warning → Folding → Closed → Unfolding → Open`입니다. FoldDuration은 SmoothStep 곡선으로 가속/감속합니다. 안전을 위해 최고 각속도 60°/s를 넘지 않도록 실제 이동 시간은 `max(FoldDuration, FoldAngle × 1.5 / 60, 0.25초)`를 사용합니다. 기본 왕복 사이클은 13.5초이며 StartDelay는 최초 한 번만 더합니다. 비동시 모드는 접기와 펼치기에 RightDelay가 각각 추가됩니다.

경기가 종료되면 움직임을 멈춥니다. Ready로 복귀하면 각속도 제한 안에서 평평한 상태로 돌아옵니다. 컴포넌트를 껐다 켜거나 경기 시간이 보정돼도 순간적으로 회전하지 않고 제한된 속도로 목표 각도를 따라갑니다.

## 경고 연출 연결

- 기본으로 진입판의 주황색 Warning strip이 Warning 상태에서 깜빡입니다. `WarningVisuals` 배열에 다른 Renderer를 넣어 교체할 수 있습니다. 지정한 Renderer는 이 컴포넌트가 enabled 상태를 관리하므로, 패널 본체 Renderer를 넣지 마세요.
- 소리: 원하는 AudioClip을 `WarningClip`에 지정합니다. 프리팹에 연결된 `WarningAudio`는 3D AudioSource이며, 경고 진입 시 PlayOneShot으로 1회 재생합니다.
- 추가 연출: `OnWarning`, `OnFolding`, `OnClosed`, `OnOpened` UnityEvent에 ParticleSystem.Play, 별도 AudioSource.Play 등 프리팹 내부의 연출 메서드를 연결합니다. 이벤트는 상태 진입 시 발생하며 매 프레임 반복되지 않습니다.
- 기존 공유 Material은 바꾸지 않습니다. 다른 경고 색을 원하면 Warning strip에 원하는 Material을 지정하세요.

## 물리와 낙사 구역

- 각 Pivot의 Rigidbody는 Kinematic, Interpolate, Continuous Speculative입니다. FixedUpdate에서 MoveRotation을 호출하므로 모델과 자식 BoxCollider가 함께 움직입니다.
- 기존 PlayerMotor가 접촉한 Kinematic Rigidbody를 발판으로 추적하는 기능을 그대로 사용합니다. 플레이어를 부모로 붙이거나 Transform을 강제 이동하지 않습니다. 플레이어에 Rigidbody를 추가하지 마세요.
- Collider는 움직이는 패널 2개와 고정 진입판 2개뿐입니다. 하부 안전 바닥, 낙사 판정 또는 강제 사망 기능은 프리팹에 없습니다.
- 낙사 장애물로 쓰려면 가운데 8×6m 아래의 기존 바닥을 비우고, 양 끝 진입판을 주변 지형과 연결하세요. 아래에 연속 바닥이 남으면 플레이어가 그 위로 떨어질 뿐입니다.
- 기존 StageMap.killHeight 아래로 내려가면 기존 개인 Checkpoint/Respawn이 처리합니다. 해당 Scene에 기존 경기/복귀 구성이 있어야 합니다.
- 접히는 패널 위쪽과 옆쪽에 충분한 빈 공간을 확보하세요. 천장·벽으로 플레이어를 가두면 물리 끼임이 생길 수 있습니다. 이 프리팹은 주변 임의 지형의 압착을 자동으로 해결하지 않습니다.
- 루트 Scale은 (1,1,1)을 권장합니다. 비균일 스케일, 패널 아래 추가 Collider, 런타임 루트 이동은 따로 검증하세요.

## 테스트와 생성

1. Prefab Mode: Project 창에서 `12_FoldingBridge.prefab`을 더블클릭해 형태와 연결을 확인합니다.
2. 자동 테스트: Play Mode가 꺼진 상태에서 `CHESS FIGHT > Obstacles > Verify Folding Bridge (Isolated Play)`를 실행합니다. 별도 임시 런타임 Scene에서 기존 플레이어 프리팹으로 검사하고 자동 종료합니다. 원본 Scene은 저장하지 않습니다.
3. 결과: `Artifacts/ChessFight/FoldingBridgeVerification.txt`. 미리보기는 `Artifacts/ChessFight/ObstaclePreviews/FoldingBridge_*.png`.
4. 직접 플레이할 때는 별도 테스트 Scene 또는 맵 사본에 사용자가 배치한 뒤 기존 경기 시작 기능을 실행합니다. 세 명 이상을 양 패널에 올려 접기/펼치기, 중앙 통과, 가장자리 낙하, 힌지 통과를 확인하세요.
5. 설정 검사: Simultaneous를 꺼 지연 동작을 확인하고, Repeat를 꺼 한 번 작동 후 열린 상태로 남는지 확인합니다. 낮은 프레임과 실제 12인 환경은 별도 플레이테스트가 필요합니다.
6. 프리팹이 없을 때만 `Create Folding Bridge Prefab (No Map Placement)` 메뉴로 생성합니다. 기존 프리팹은 사용자 수정 보존을 위해 덮어쓰지 않습니다.

온라인에서는 기존 ObstacleContext의 경기 시간으로 같은 패턴을 재생합니다. 새로운 네트워크 메시지는 추가하지 않았습니다. 양쪽 빌드의 프리팹/설정이 같아야 하며 실행 중 한쪽에서만 Inspector 값을 변경하는 동작은 동기화되지 않습니다. 실제 두 PC Steam 테스트는 별도입니다.

## 이번 작업의 신규 스크립트

- `Assets/ChessFight/Runtime/Obstacles/FoldingBridge.cs`: 운동, 상태, 경고 이벤트.
- `Assets/ChessFight/Editor/FoldingBridgePrefabBuilder.cs`: 임시 편집 Scene에서 에셋 생성.
- `Assets/ChessFight/Editor/FoldingBridgeVerification.cs`: 독립 물리 검증 및 미리보기.

기존 스크립트는 수정하지 않았습니다.
