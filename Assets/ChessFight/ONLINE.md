# Steam 12인 + AI 교대 프로토타입

## 실행

- Scene: `Assets/Scenes/ProtectTheKing_Graybox.unity`
- Unity Play → **START AI PRACTICE**: 사용자 1명 + AI 11명. Tab으로 시험할 기물을 바꿉니다.
- Steam 클라이언트 로그인 → Play → **CREATE STEAM ROOM**: 친구 전용 12인 방을 만듭니다.
- 다른 PC/Steam 계정: 같은 빌드 실행 → 방 ID 입력 → **JOIN**, 또는 실행 중 Steam 초대를 수락합니다.
- **COPY ROOM ID**, **INVITE**, **LEAVE ROOM** 버튼을 제공합니다.
- 방장만 Enter/시작 버튼으로 경기를 시작·재시작합니다. 경기 도중에도 참가할 수 있습니다.
- 온라인에서는 Tab/슬롯 선택을 잠가 다른 사람의 기물을 조종하지 못합니다.

## 접속 및 권한

총 12개 Scene 캐릭터를 계속 재사용합니다. 빈 슬롯은 AI(소유자 0), 사람 슬롯은 Steam ID로 관리합니다.
참가 순서는 Blue King → Red King → Blue Queen → Red Queen …입니다.
한 사람이 하나의 슬롯을 소유하며 중복 참가를 중복 배정하지 않고 13번째 사람을 거절합니다.

중도 참가자는 빈 AI 슬롯의 팀, 기물, 위치, CP, 낙사 횟수를 이어받습니다.
이전 AI 입력과 왕좌 채널링은 취소합니다. 사람 이탈 시 같은 캐릭터를 AI가 이어서 조종합니다.
재접속은 그 시점의 빈 슬롯을 배정하며, 이전 자리 예약은 지원하지 않습니다.

방장이 CharacterController 이동, AI, CP/복귀, 타이머, 승리를 판정합니다.
클라이언트는 이동 입력·시선·점프·복귀·상호작용 의도만 전송합니다.
클라이언트가 보낸 위치, CP 또는 승리 선언을 수용하는 메시지는 없습니다.
Steam이 제공한 송신자 신원과 로비 멤버 여부로 입력 슬롯을 결정합니다.

입력과 전체 상태를 최대 20Hz로 전송합니다. 점프/복귀 카운터, 메시지 순서 번호,
크기/형식/유한 숫자 검사를 적용합니다. 오래된 입력은 0.5초 후 멈춥니다.
접속자 무응답 10초 후 AI로 교체하고, 클라이언트는 방장 무응답 15초 후 방을 나갑니다.
방장이 종료하면 방을 종료합니다. 자동 방장 이전은 구현하지 않았습니다.
창 전환 중에도 호스트가 계속 실행되도록 런타임에서 백그라운드 실행을 활성화합니다.

## Steam 구성

- Steamworks.NET `2025.164.1`을 UPM Git 태그로 고정했습니다.
- Steam 로비 + Steam Networking Messages를 사용합니다.
- Steam Datagram Relay를 사용할 수 있는 P2P 연결이며 게임 시뮬레이션 서버는 방장 PC입니다.
  Valve가 게임 실행용 전용 서버를 대신 운영하는 구성은 아닙니다.
- 현재 App ID는 공용 개발용 **480**입니다. 프로젝트 루트의 `steam_appid.txt`와
  Scene의 `Systems/LocalPlaySession > SteamLobbyTransport.appId`에 설정됩니다.
- 출시용 App ID를 확보하면 두 값을 바꾸고 Unity/테스트 실행 파일을 재시작합니다.
- 빌드 도구는 테스트 실행 파일 옆에 개발용 `steam_appid.txt`를 복사합니다.
  Steam 배포본에는 이 개발용 파일을 포함하지 않습니다.
- 개발용 480의 다른 게임 로비에 참가하지 않도록 게임/프로토콜 메타데이터를 검사합니다.

참고: [Steamworks.NET 설치](https://steamworks.github.io/installation/),
[Steam Networking Messages](https://partner.steamgames.com/doc/api/ISteamNetworkingMessages?l=english).

## AI 범위

`AI Navigation/Blue Waypoints`, `Red Waypoints`가 실제 Scene에 저장됩니다.
AI는 기존 이동기와 CP/낙사 규칙을 사용해 전진, 점프, 낮은 장애물 회피, 게이트 대기,
막힘 시 마지막 CP 복귀를 수행합니다. 킹은 CP5 이후 왕좌를 상호작용하고
나머지 기물은 최종 좌우 대기 위치로 이동해 진입을 확보합니다.

새 기물 능력이나 전투 기술은 추가하지 않았습니다.
협동 호위/적 킹 공격 전술, 다양한 ZONE 3 경로 선택, 고급 군중 회피는 후속 범위입니다.
AI 경로는 현재 Graybox 좌표에 맞춰 작성되었으므로 맵을 옮기면 경로도 조정해야 합니다.

## 검증 및 테스트 빌드

CHESS FIGHT > Online:
- Install into Current Map: 기존 맵에 참조와 AI 경로를 연결하고 저장합니다.
- Use Development App ID 480: 개발용 ID를 저장합니다.
- Verify Rules and Scene: 정원, 중복 접속, 슬롯 복구, 메시지 검증, Scene 참조 검사.
- Run Play Verification: AI 이동·점프·인계, 권한 차단, 두 킹의 독립 채널링, 실제 Steam 로비 생성/종료.
- Run AI Course Test: AI의 CP1~CP5 및 왕좌 완주 검사.
- Build Windows Test Player: `Artifacts/ChessFight/WindowsTest/ChessFight.exe` 개발 빌드 생성.

결과는 `Artifacts/ChessFight/*Verification.txt`에 저장됩니다.
기존 코드와 Scene의 변경 전 백업은 `Artifacts/ChessFight/BeforeOnline`에 있습니다.
Steam 패키지 설치로 Packages/manifest.json 및 packages-lock.json도 변경되었습니다.
Steamworks 패키지가 ProjectSettings/ProjectSettings.asset에 STEAMWORKS_NET 컴파일 심볼을 추가했습니다.

서로 다른 Steam 계정/PC를 사용하는 실제 2인 참가, 중도 접속·이탈, 12인 동시 플레이,
지연·패킷 손실·릴레이 경로 검증은 별도로 필요합니다.
현재 클라이언트는 서버 상태 보간을 사용하며 입력 예측/재조정은 구현하지 않았습니다.
인터넷 지연이 크면 조작 반응이 늦을 수 있습니다.
