# 프로젝트 개요

## 제품

- **체스파이트(ChessFight):** 체스 말 6종(킹·퀸·룩·비숍·나이트·폰)을 팀당 한 명씩 맡아 **6 대 6**으로 여러 미니게임 라운드를 겨루는 온라인 파티 게임.
- 말별 스킬은 게임 모드마다 고정된 구성을 갖는다(기획). **현재 코드에는 기물·스킬·라운드가 없다.**
- 미니게임 후보: **킹러시**(폴가이즈식 레이스, 첫 번째), 승격쟁탈전, 킹을 지켜라 등. 게임 씬은 같은 자리(Lobby 다음)에 추가한다.
- 플랫폼: Steam(PC, Windows x64). 개발 중에는 App ID 480(Spacewar)을 쓴다.

## 팀 (5명, 약 12주)

구성은 기획 1, 프로그래머 1, 서브 기획·프로그래머 2, 3D 그래픽 1이다. 개인별 직군은 기록되어 있지 않아 아래 표는 이번 주 배정만 적는다.

| 사람 | 맡은 분야 | 이번 주(2026-09-24 주차) 할 일 | 안내 |
|---|---|---|---|
| 사용자 (dua0731) | 메인 기획. AI로 네트워크·기반 개발 | 파티·매칭 마무리, 기초 개발 환경 | [HANDOFF](../../HANDOFF.md) |
| 준영 | 래그돌·캐릭터 | 래그돌 모델링과 이동·점프·잡기·밀기 프리팹. 멀티 고려 | [Player/RAGDOLL](../Player/RAGDOLL.md), 팀 가이드 5장 |
| 진호 · 지성 | 킹러시 맵·장애물 | 킹러시 맵과 장애물 세부 기획·제작, 장애물 기획서 | [KingRush](../KingRush/README.md), 팀 가이드 6장 |
| 승규 | 네트워크 방어 기획 | 멀티 구조 숙지 → 지연·호스트 이탈 등 방어 기획. 기획안 v0.1 작성함 | [Network/PLAN_V0.1_STATUS](../Network/PLAN_V0.1_STATUS.md), 팀 가이드 7장 |

팀원은 각자 다른 AI 도구(ChatGPT 아스트라 등)를 쓴다. 그래서 모든 문서는 도구에 상관없이 읽히는 마크다운으로 둔다.

## 브랜치

| 브랜치 | 내용 | 상태 |
|---|---|---|
| `Network` | 사용자 + AI 개발 브랜치. 이 문서 체계의 기준 | 최신 |
| `main` | 팀원이 받는 브랜치 | `a070367`. Network 병합 대기 |
| `JY-ragdoll` | 준영의 래그돌 랩(`Assets/ChessFight/RagdollLab/`, 로컬 2P, 네트워크 없음). `c9c1e6a`에서 분기, 최신 `2d450aa` | **`Network`에 병합됨**(2026-09-25, `fc006b8`). 이후 래그돌 작업은 `Network`/`main` 기준 |
| `킹을-지켜라` | 다른 미니게임 프로토타입(`5af7c8c`에서 분기) | **호환 안 됨**: Unity 6000.3.12f1, URP 17.3.0, uGUI 2.0.0, Input System 1.19.0, 고정 안 된 MCP 패키지, 자체 `SteamLobbyTransport`. 통합 전에 어느 쪽을 기준으로 할지 결정 필요 |
| `SteamNetworkTest` | 초기 실험 | 사용 안 함 |

## 저장소와 PC

- GitHub: `https://github.com/dua0731-cmd/ChessFight`
- 사용자가 Unity Hub로 여는 원본: `C:\Users\dua07\GitHub\ChessFighter` (Windows)
- 초기 작업 복사본(다른 AI 도구 시절): `C:\Users\dua07\Documents\Codex\2026-09-14\x20-ex-1-x20-vs-x20-2\ChessFight-Network`. GitHub Desktop에서 둘 다 `ChessFight`로 보일 수 있으니 **실제 경로를 확인하고 Pull한다.**
- 원본에는 Unity가 바꾼 `ProjectSettings/UnityConnectSettings.asset`(`m_Enabled: 0 → 1`)이 미커밋으로 남아 있을 수 있다. 네트워크와 무관하니 함부로 지우거나 커밋하지 않는다.
