# Network 구현 검증 기록

최종 갱신: 2026-09-22 (KST). 코드 기준: `c9c1e6af50548a8161d10f8754b9a9897a38b3ac` (`Network`).
대상: Unity `6000.3.11f1`, Windows x64, Steamworks.NET `2025.164.1`.

상태: **핵심 로직·모의 세션 검사 및 컴파일 통과 / 실제 Editor 1인 Steam 방 성공 / Windows 빌드 성공 / 두 계정 이동 동기화 미검증**.

## 확인 결과

| 항목 | 결과 | 근거와 한계 |
|---|---|---|
| Core C# 테스트 | 통과, 18개 | 정원·중복·입장 실패·만료, 1,000회 무작위 예약, 패킷 검증, 이동·점프 수식. 실제 물리/통신 검증 아님 |
| 세션 상태 테스트 | 통과, 7개 | 생산 SteamSession.cs를 모의 Steam 서비스와 실행. 파티 동행, 팀 입장, 취소, 늦은 콜백, 부분 실패, 호스트 이탈, 12개 동시 검색 수렴 |
| 어셈블리별 컴파일 | 통과 | 실제 Unity 라이브러리와 공식 Steamworks 소스 사용. Steamworks/Core/Steam 런타임/Editor 도구 별도 컴파일 |
| 실제 Editor import·컴파일 | 2026-09-21 성공 | 원본 ChessFighter를 Unity Hub에서 실행, 메뉴 및 Play 화면 확인 |
| UPM 의존성 설치 | 2026-09-21 성공 | 실제 manifest/lock 생성 및 패키지 컴파일. c9c1e6a에 커밋·Push |
| Editor Steam 1인 파티·경기 | 성공 | Party ready, 0 아닌 파티/경기 ID, Waiting room: 1/12 확인 |
| Editor UI·카메라·캡슐 | 표시 확인 | 체스판, BLUE 캡슐, 명단과 추적 화면 확인. 키 입력은 보냈으나 점프 궤적/조작감을 캡처만으로 입증하지 않음 |
| Windows x64 개발 빌드 | 2026-09-21 성공 | Editor.log의 Build Finished, Result: Success. 약 54초. exe, Data, Mono, steam_appid.txt, steam_api64.dll 확인 |
| 독립 실행 빌드 표시 | 2026-09-22 확인 | 체스판·UI 표시. Steam 미실행 상태에서 초기화 실패 안내 표시 |
| 독립 실행 빌드 Steam 입장 | 2026-09-22 1인 성공 | 로그인 후 빌드 재시작. Party ready, 비공개 방 Waiting room: 1/12, BLUE 캡슐·명단 확인 |
| 두 PC·두 계정 이동 동기화 | 미실행 | 모의 세션이나 동일 계정 Editor/exe를 실접속 검증으로 간주하지 않음 |
| 실제 12인 매칭·인터넷 부하 | 미실행 | 팀 실기 테스트 필요 |
| 지연·손실·강제 종료·장시간 실행 | 미실행 | 출시 준비 판정 불가 |

빌드 위치: `C:\Users\dua07\GitHub\ChessFighter\Builds\NetworkTest\ChessFight.exe`.
프로젝트 내부 Builds는 Git 제외 대상이다. 팀원에게 실행 파일을 공유할 때는 NetworkTest 폴더 전체를 전달한다.

## 관찰된 경고와 한계

- 기존 URP 템플릿 참조에 대한 `The referenced script (Unknown) on this Behaviour is missing!` 경고가 남았다. 이 브랜치에는 URP 패키지가 없으며 테스트 공간은 실행 중 Built-in 렌더링을 사용한다.
- 현재 Start 버튼은 실제 게임 규칙 시작이나 씬 전환이 아니라 경기 로비 입장 마감이다.
- UI 힌트의 `\n` 문자 표시를 관찰했다. 긴 명단, 다양한 해상도, 한글 이름 전체 지원은 별도 확인이 필요하다.
- Player.log는 현재 도구에서 파일 읽기가 거부되었다. 독립 실행 빌드 확인은 실제 화면 관찰에 근거하며, Player 로그에 오류가 전혀 없다고 주장하지 않는다.
- 원본 프로젝트의 UnityConnectSettings.asset 자동 변경은 Steam 관련 변경이 아니어서 미커밋으로 남겼다.

## 실행한 자동 검증과 해석

`Tools/Test-NetworkCore.ps1`: 18개 Core + 7개 세션 검사. Core는 Unity에 의존하지 않는다. 모의 서비스는 로비·이벤트·소유권·정원을 재현하지만 Steam 백엔드 전파와 실제 P2P를 재현하지 않는다. SteamMotion의 예측·보정 통합 검사가 아니다.

`Tools/Test-NetworkCompile.ps1`: Unity .NET Standard 2.1 및 UnityEngine/UnityEditor 어셈블리와 실제 Steamworks 소스를 사용한다. 더미 API를 사용한 컴파일을 실제 패키지 호환성 검증으로 대체하지 않는다.

## 과거 장애 이력

초기 구현은 별도 복사본에서 수행했다. 그 환경에서 headless Unity의 Licensing Client IPC 연결이 실패했고 Git HTTPS도 제한되었다. 그래서 첫 커밋에는 설치 부트스트랩만 있었다. 이후 사용자 원본 프로젝트를 Pull하고 Unity Hub의 실제 Editor로 설치·Play·빌드를 완료했다. **과거의 전체 런타임 검증 차단 기록은 현재 상태가 아니다.**

GitHub 앱 쓰기 403은 사용자 GitHub Desktop으로 Push하여 해결한 작업 도구 문제다. 초기 구현 87805f0과 실제 설치 결과 c9c1e6a가 Network에 반영되었다.

## 다음 검증 우선순위

1. 두 계정 비공개 방: 양방향 이동·점프·지연 입장·퇴장·다시 방 만들기.
2. 파티장/파티원 취소, 구성 변경, 호스트 정상 종료 및 강제 종료 후 명단/캡슐 정리.
3. 실제 6+6, 3+3+2+2+1+1, 4+4+4 거절, 부분 입장 실패.
4. 여러 PC의 동시 공개 검색, 방 분산·재시도·통합 시간 확인.
5. 입력 손실 시 점프, 지연 시 예측 보정, 호스트 프레임 저하, 12인 대역폭·CPU와 장시간 세션 측정.

설계와 재현 절차: [AI 인수인계](../AI/NETWORK_HANDOFF_KO.md), [사용자 실행 안내](README.md).
