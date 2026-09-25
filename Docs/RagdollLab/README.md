# 킹 러쉬 래그돌 물리 테스트 리그

`kingrush-ragdoll-prototype-spec.md`(명세서)를 구현한 로컬 2인 물리 감각 테스트 리그. 네트워킹 없음, Steam 코드와 분리.

## 여는 법

1. Unity 6000.3.11f1에서 `Assets/Scenes/RagdollTest.unity`를 연다(메뉴 `ChessFight > Scenes > Ragdoll Test`). 2026-09-25 병합 때 랩 씬을 이 이름으로 옮겼다. 게임 쪽 연결과 네트워크 규칙은 [`Docs/Player/RAGDOLL.md`](../Player/RAGDOLL.md).
2. Play. 게임 화면을 클릭하면 마우스가 잠긴다(Esc로 해제).
3. Tab(또는 패드 Start)으로 튜닝 패널을 연다. 패널에서 바꾼 값은 `Settings/RagdollTuning.asset`에 그대로 남는다(에디터 기준).

폰 리그·프리팹·씬을 다시 만들려면 메뉴 `ChessFight > Ragdoll Lab > Rebuild Pawn + Scene`을 쓴다. 이미 있는 튜닝 값은 유지된다.

## 조작 (명세서의 동사 4개)

| 입력 | P1 키보드+마우스 | 게임패드 (XInput) | P2 키보드 대체 |
|---|---|---|---|
| 이동 | WASD | 왼쪽 스틱 | 방향키 |
| 점프 | Space | A | 오른쪽 Shift |
| 밀치기 | 마우스 왼쪽 | RB | 오른쪽 Ctrl |
| 잡기(홀드) | 마우스 오른쪽 | LB | Enter |

- 잡은 채 밀치기 = 던지기(밀치기 끝에 손을 놓음).
- 점프하며 잡기 → 벽 **모서리**(꼭대기 0.4 m 이내)를 잡으면, 한 번 더 점프 = 기어오르기. 땅을 밟을 때마다 한 번. 벽 옆면은 매달리기만 된다.
- R 전체 리스폰, T 슬로모션, F 자유 카메라(WASD·Q·E), 패드 Back 본인 리스폰.
- P2 입력 장치는 패널의 "플레이어 입력" 버튼으로 바꾼다. 패드가 없으면 P2는 키보드 방향키로 시작한다.

## 구조

- `RagdollPawn`: Rigidbody 11개(Hips·Chest·Head·Arm·Hand·Thigh·Foot) + ConfigurableJoint(Slerp). 절차적 퍼펫(`Puppet` 하위 Transform)이 매 FixedUpdate 목표 포즈를 만들고, 조인트 targetRotation이 이를 따른다. `LocomotionAnchor`(kinematic)가 입력대로 움직이고 Hips를 조인트로 끈다.
- 동적 강성: 접촉·잡기·경사·피격 중 **가장 낮은 배율**이 적용되고, `stiffnessLerpSpeed`로 보간된다. 넉다운 중에는 모든 스프링이 0이다.
- 시각 메시는 사용자가 준 Mixamo 리그(`Art/Pawn/Pawn.fbx`)를 그대로 쓰고, 빌더가 부위별로 가중치를 다시 계산한다(뼈 이름·구조 유지). 뼈는 LateUpdate에서 물리 몸을 따라간다.
- 파일: `Assets/ChessFight/RagdollLab/` (Scripts / Editor / Art / Generated / Materials / Prefabs / Settings). 씬은 `Assets/Scenes/RagdollTest.unity`. `RagdollDriver`가 게임 공통 캐릭터 계약(`ICharacterDriver`)을 구현한다.

## 명세서와 다른 점 (측정 근거)

| 항목 | 내용 | 이유 |
|---|---|---|
| Slerp 드라이브 ×4 | 슬라이더 값 = 실제 N·m/rad가 되도록 positionSpring·Damper에 4를 곱함 | 이 프로젝트에서 Slerp 드라이브는 설정값의 약 1/4만 작동(상체 400 → 실측 97~100 N·m/rad). 직선·XY&Z 드라이브는 1:1 |
| 물리 120 Hz, 솔버 24회 | 패널에서 50/60/90/120 Hz, 12/16/24/40회 선택 | 60 Hz·16회에서는 다리 사슬이 몸무게를 못 받아 골반이 8 cm 주저앉음. 120 Hz·16회 이상이면 기준 높이 ±3 mm |
| 골반 직립 토크 | AddTorque 대신 앵커 조인트의 각도 드라이브로 구현 | 명시적 감쇠 토크가 60 Hz에서 불안정(서 있어도 36° 흔들림) |
| 중력 1.0배 | 명세 미지정 → Unity 기본값 | 1.5배에서는 명세 시작값의 상체가 거의 중립 균형이 되어 뒤로 젖혀짐 |
| 발 마찰 | 서 있을 때 0.6, 이동·밀치기·피격 중 0.1 (Minimum 합산) | 한 마디 다리가 바닥을 긁어 골반을 17° 앞으로 넘어뜨림 |
| 몸통 마찰 0.05 | 치마·상체는 거의 마찰 없음 | 벽에 부딪힌 점프가 멈추지 않게 |
| 밀치기 | 팔 강성 ×5(명세) + 몸 기울기 22° + 0.6 m 내딛기 | 치마 지름(0.58 m)보다 팔이 짧아 팔만으로는 상대에 닿지 않음 |
| 피격 판정 | 밀치는 중인 몸에 닿으면 1초간 피격 배율(0.3) 적용 | 데미지·레이캐스트 없음, 결과는 물리 |
| 동적 배율의 하체 적용 | 하체·앵커에는 배율의 60%만 적용 | 100%면 잡은 쪽이 너무 약해져 끌기 불가 |
| 퍼펫 | Animator 대신 절차적 포즈 | 애니메이션 클립 없음. 나중에 Animator로 교체 가능한 구조 |

## 자동 점검

에디터가 열려 있으면 배치 모드를 같은 프로젝트에 쓸 수 없다. 사본 프로젝트에서 빌드한 뒤 플레이어로 돌린다.

```
RagdollLab.exe -batchmode -nographics -ragdollAutoTest report.txt
RagdollLab.exe -batchmode -ragdollShots shots
```

점검 항목: 관절 목표 방향, 서 있기, 달리기, 멈추기, 방향 전환, 점프, 넉다운→기상, 낙하, 경사 3종 구르기, 접촉 강성, 잡고 끌기, 밀치기, 회전 봉, 외줄, 벽 오르기, 밀착 안정성, NaN.

2026-09-23 명세 시작값 기준 결과: 18/18 통과. 서 있기 골반 0.254 m(기준 0.256), 달리기 4.95 m/s·골반 기울기 6.4°, 점프 1.76 m, 기상 1.22 s 시작·1.57 s 완료, 30° 경사 구르기 1.93 s vs 달리기 2.29 s, 잡고 끌기 2.94 m, 밀치기 0.44 m, 2 m 벽 혼자 오르기 성공, 3 m는 혼자 불가.

**아직 사람이 두 명이서 직접 해 본 적은 없다.** 명세서 8장의 체감 기준은 2인 플레이테스트로만 판정할 수 있다.
