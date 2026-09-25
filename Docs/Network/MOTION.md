# 이동 동기화·패킷·연결 품질 (`SteamMotion`, `MotionProtocol`, `LinkQuality`)

코드: `Scripts/Network/SteamMotion.cs`, `Scripts/Core/MotionProtocol.cs`(패킷, `PawnMotor`), `Scripts/Core/LinkQuality.cs`.

## 1. 권한과 흐름

```text
클라이언트: 입력 수집 → 로컬 예측 → Input(sequence, X, Z, Jumps) 전송 ─┐
호스트:     발신자·세션·범위 확인 → 30Hz로 모든 캐릭터 판정 ←───────────┘
호스트:     Snapshot(tick, 각 PawnState + Ack) 최대 20Hz로 전원에게 전송
클라이언트: 자기 캐릭터는 호스트 값에서 미확인 입력을 다시 적용, 남은 캐릭터는 호스트 값 그대로
```

- 전송: `SteamNetworkingMessages`, **채널 31**, `Unreliable | NoNagle`. 직접 연결·릴레이 선택은 Steam이 한다.
- 호스트와 클라이언트 모두 같은 `PawnMotor.Advance`를 쓴다. 클라이언트는 좌표를 보내지 않는다. 입력의 주인은 Steam이 알려준 피어 ID다.
- 봇은 호스트가 `BotDirector`로 움직이고 절대 보내거나 받지 않는다([BOTS](BOTS.md)).

**호스트**
- roster에 있는 사람(과 봇)만 상태를 만든다.
- 누적 시간을 1/30초 단위로 소비, 한 프레임 최대 4 step.
- 상대별 **가장 최근의 유효 입력 하나**를 저장해 매 step 적용. 0.25초 넘게 입력이 없으면 X/Z = 0, 점프 없음.
- **점프:** 입력의 누른 횟수 `Jumps`가 상대별 소비 값과 다르면 한 번 뛰고 소비 값을 갱신한다(`MotionProtocol.TakeJump`). 패킷 하나가 사라져도 다음 패킷이 같은 횟수를 가져오므로 **한 번만 늦게** 뛴다.
- 최소 0.05초 간격으로 전체 스냅샷 전송.

**클라이언트**
- roster에 자기 ID가 있으면 30Hz로 입력을 만들어 보낸다. 점프를 누른 tick에 `Jumps++`.
- 자기 캐릭터는 같은 모터로 즉시 예측. 미확인 입력 최대 120개.
- 더 새 스냅샷이 오면 자기 Ack 이하 입력을 지우고 남은 입력을 다시 적용한다.

한계: 호스트는 입력마다 tick을 재현하지 않고 최신 입력을 유지한다. 클라이언트 재실행과 방식이 달라 지연 조건에서 보정 오차가 생길 수 있다(측정 필요).

## 2. 모터 수치 (로비·현재 경기 공용, 네트워크 시험값)

| 항목 | 값 |
|---|---|
| step | 1/30초 |
| 속도 | 6 unit/s, 즉시 반응, 대각선 정규화 |
| 점프 초속 / 중력 | 7 / 22 |
| 지면 높이 / 점프 가능 | Y=1 / Y ≤ 1.001 |
| 경계 | X, Z ∈ [-19, 19] |
| 스폰 | X: 청 -8 / 주황 8, Z: (slot - 2.5) × 2 |

충돌·경사·움직이는 발판이 없다. **킹러시 코스를 달리려면 다른 이동 모델(래그돌)이 필요하다.**

## 3. 패킷 형식 (little-endian, BinaryWriter)

**입력: 정확히 26바이트**

| 순서 | 형식 | 필드 |
|---|---|---|
| 1 | uint32 | magic `0x43464632` (`CFF2`. v1은 `CFF1`) |
| 2 | byte | type = 1 |
| 3 | uint64 | session (= 경기 로비 ID) |
| 4 | uint32 | sequence |
| 5–6 | float32 × 2 | X, Z |
| 7 | byte | Jumps (누른 횟수, 255 다음 0) |

**스냅샷: 18 + 30 × 인원 바이트** (12명 378바이트)

| 구간 | 구성 |
|---|---|
| 헤더 18 | magic, type=2, session, tick(uint32), count(byte) |
| 1인당 30 | Id(uint64), Ack(uint32), X, Y, Z, Vertical(float32 × 4), Team(byte), Slot(byte) |

검증: 입력은 길이·magic·type·session·유한 수·|X|,|Z| ≤ 1.01. 스냅샷은 최대 1024바이트, 정확한 길이, 최대 12명, ID 0·중복 금지, Team/Slot 범위, 유한 수, |X|,|Z| ≤ 20, Y 0~10. `Newer(a, b)`는 wraparound를 고려한 순서 비교. 한 프레임 최대 32개 수신, native 메시지는 `finally`에서 Release.

대역폭(페이로드만, 이론값): 12인 호스트 송신 378 × 20 × 11 ≈ 83 KB/s, 수신 26 × 30 × 11 ≈ 8.6 KB/s.

**패킷을 바꾸면:** magic을 올리고 `SteamSession.Protocol`도 올린다. Core 테스트에 옛 패킷 거절 테스트를 둔다.

## 4. 연결 품질 (2026-09-24, 기획안 v0.1 반영)

| 기능 | 동작 | 코드 |
|---|---|---|
| **방장 끊김 단계** | 클라이언트가 roster에 들어간 순간부터 스냅샷 공백을 잰다. **0.5초** "방장 연결이 불안정합니다" → **2초** "방장 응답 없음 - 멈춤 (n초 뒤 파티로 복귀)" + 이동 입력 0·점프 무시 → **12초** `Cancel()` 후 파티로 | `LinkMonitor.Classify`, `SteamMotion.UpdateHealth` |
| 표시 | 경기 씬: 상단 배너(노랑/빨강). 로비: 연결 정보 카드 문구 | `MatchSceneView.ShowWarning` |
| **핑** | Steam 전송 핑. 클라이언트는 방장까지, 방장은 가장 나쁜 참가자. 1초마다 `GetSessionConnectionInfo` | `SteamMotion.PollPing` |
| **응답** | 입력을 보낸 시각 → 그 입력을 Ack한 스냅샷 도착까지. 1/8 지수 평활. 호스트 tick과 스냅샷 간격이 포함된 체감값 | `ResponseTimer` |
| **F8 지연 시뮬레이터** | 개발 빌드만. 꺼짐 → +100ms → +200ms·손실 5% → +300ms·손실 10%. 이 PC의 송신과 수신에 각각 절반 지연, 손실은 방향마다. 순서는 바꾸지 않는다 | `LinkSimulator<T>`, `LinkProfile.Presets`, `NetworkRuntime.Update` |
| 한 줄 표시 | `SteamMotion.QualityLine`: "핑 42ms · 응답 95ms · 지연 시뮬 +200ms / 손실 5%" | |

씬 로딩 중 멈춤(수 초)이 있으면 반대편에 잠깐 "불안정"이 뜰 수 있다. 그 순간 실제로 신호가 없었던 것이므로 정상이다.

## 5. 아직 없는 것

신뢰 전송 이벤트 채널, 보간 버퍼(지금은 `1-exp(-18·dt)` Lerp 표시), 입력 이력 재현, 되감기 판정, 호스트 이전. [PLAN_V0.1_STATUS](PLAN_V0.1_STATUS.md), [ROADMAP](../Project/ROADMAP.md).
