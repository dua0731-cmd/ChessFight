# 소드파이트 — 폰 플레이 버전 (R47)

2026-09-27 · `JY-gpt_gamemode` · **구현·자동 검사 단계, 사용자 Unity/Steam 확인 대기**

## 실행

전체 Windows 빌드도 작업 폴더의 `Builds/SwordFight/ChessFight.exe`에 생성했다(Git에는 포함하지 않음). Intro→Lobby부터 시작한다. 기존 모드까지 포함한 전체 빌드 생성 통과, 실제 로비 버튼/두 PC 조작은 사용자 확인 대기다.

Unity Hub에서 **`C:/Users/trews/.codex/worktrees/jy-gpt-gamemode/ChessFight`**를 연다. 원본 `JY-ragdoll_v2` 폴더가 아니다.

1. Steam 실행 → `Assets/Scenes/Lobby.unity` → Play.
2. 모드 카드에서 **소드 파이트** 선택.
3. 혼자 시험: **비공개 방 → 12명 채우기 → 잠시 뒤 경기 시작**. 빈자리를 봇이 채운다. 공개 게임 시작은 기존대로 12명 매칭이다.
4. 친구와 시험: 같은 프로토콜 v7 빌드에서 방 번호로 입장 → 방장이 경기 시작.
5. Esc 메뉴 → 로비로 돌아가기. 파티는 유지된다.

Steam 없이도 `Assets/Scenes/SwordFight.unity`를 직접 Play하면 **나 + 아군 봇 1 + 적 봇 2**로 연습한다. 메뉴 `ChessFight → Sword Fight → Open Test Scene`도 같은 씬을 연다.

## 이번에 들어간 것

- 기존 래그돌 폰과 이동·질주·점프·카메라 사용. 공통 `RagdollPawn`·튜닝 에셋·RagdollTest 씬은 수정하지 않음.
- **이 모드에서만** 좌클릭 = 칼 휘두르기. 우클릭 = 기물 스킬 자리(폰은 없음). 우클릭 잡기/등반, E/Q/F 갈고리·종은 전달하지 않음.
- 손에 붙은 짧은 칼, 팔·상체 베기 자세, 준비 → 베기 → 회수. 검날이 지나간 공간을 검사하며 한 휘두르기당 같은 상대는 한 번만 맞음.
- 맞으면 실제 래그돌로 밀려 넘어짐. **누운 상대에게 다음 공격을 다시 맞힐 수 있음**. 기상 무적·연속타 강제 차단 없음.
- 흑팀/백팀, 장외 상대팀 +1, 부활, 점수·남은 시간·결과·로비 복귀, 간단한 추격/공격 봇.
- 로비 온라인 경기는 **방장만 물리·공격·점수·부활을 판정**. 클라이언트는 입력만 전송하고 자세·칼 상태·점수를 표시. 캡슐 경기 시뮬레이션과 중복 실행하지 않음.
- 맵 제작은 미룸. 지금은 **14m 평평한 사각 발판 하나**이며 콘셉트 경기장·장애물은 아직 없음.

## 시험용 수치 — 최종 밸런스 확정 아님

| 항목 | 현재값 |
|---|---|
| 승리 | 20점 또는 4분, 동점 무승부 |
| 장외 | hips 높이 -3m 아래, 자발적 낙사도 상대팀 +1 |
| 부활 | 3초, 팀별 지정 자리 |
| 부활 보호 | 1.25초, 본인이 공격하면 해제. **기상 보호가 아님** |
| 공격 | 준비 0.16초 + 판정 0.20초 + 회수 0.34초 |
| 칼 | 길이 0.88m, 판정 여유 반경 0.15m |
| 밀기·넘어짐 | 수평 3.4m/s + 위 1.05m/s, 착지 뒤 누움 0.65초 |
| 아군 칼 피격 | 없음. 공통 몸 충돌 설정은 그대로 |

누운 상대를 계속 맞히는 **가능성**은 남겼다. 얼마나 쉽게 이어지는지, 1대1/협공에서 재미있는지는 사람이 플레이하며 조절해야 한다. 위 수치는 밸런스 완료 선언이 아니다. 사운드·타격 이펙트·고급 봇·다른 기물·스킬·최종 맵은 후속 작업이다.

## 코드와 네트워크

- `Scripts/Core/SwordFightRules.cs`: 득점/생명당 중복 방지/부활/종료, Unity 없는 테스트.
- `RagdollLab/Scripts/SwordFightGame.cs`: 모드 실행, 팀·봇·카메라·임시 HUD.
- `RagdollLab/Scripts/SwordFightPawn.cs`: 모드 입력 차단, 검과 팔 자세, 검날 sweep, 공통 피격 API 호출.
- `RagdollLabSteam/SteamSwordFightLink.cs`: **기존 NetworkRuntime.Session 재사용**, 별도 Steam 초기화 없음.
- `Scripts/Core/SwordFightProtocol.cs`: `CFS1` 상태(최대 266바이트). 기존 `CFR4` 입력 27바이트·자세 73바이트/폰은 변경하지 않음.
- 전용 Steam 채널 **33**. 입력 60Hz(reliable ordered), 자세·상태 30Hz(unreliable 반복 전체 상태), 자세 보간 지연 약100ms. 0.35초 입력 무응답이면 정지, 호스트 자세 12초 무응답이면 경기 나감. 2PC 지연·패킷 손실 실기 확인은 남음.
- `GameSceneConfig.customMatchSimulation`: 이 씬에서만 기존 캡슐 MatchSceneView/Motion.Update 생략. Assets/Scripts에서 래그돌 타입을 참조하지 않음.

## 자동 검사

2026-09-27: **Core 37개, 모의 Steam 세션 16개, 실제 Unity DLL 전체 어셈블리 컴파일 통과.** Unity 6000.3.11f1로 만든 Windows 플레이어의 **소드파이트 14개 / 실패 0개**, 같은 빌드로 1280×720·1600×900 **두 번 통과**(입력 격리·서 있는 적/누운 적 피격·중복 방지·부활·온라인 표시 상태·HUD 크기·12폰). 카메라 렌더 이미지에서 폰/검/난전도 AI가 확인했다. **사람이 직접 조작하거나 두 Steam 계정으로 시험한 결과는 아니다.**

진단 중 발견·수정: 바닥 아래로 향하던 검끝 각도, 부활 때 비활성 검 렌더러가 복원되지 않던 문제, 클라이언트의 대기 검 방향, 부활 시 kinematic 속도 경고. 추가타 점검은 밀려난 상대를 가만히 베지 않고 실제로 추격한다. 테스트 창은 숨겨도 진행되도록 자동검사에서만 백그라운드 실행을 켠다.

`Tools/Test-NetworkCore.ps1`, `Tools/Test-NetworkCompile.ps1` 외에 실제 Unity 플레이어를 검사한다:

```powershell
# 최초 씬 생성용. 이미 있는 씬은 덮어쓰지 않는다.
Unity.exe -batchmode -nographics -projectPath <이 작업 폴더> -executeMethod ChessFight.RagdollLab.Editor.SwordFightBuilder.PrepareBatch
# 소드파이트부터 시작하는 테스트 빌드
Unity.exe -batchmode -nographics -projectPath <이 작업 폴더> -executeMethod ChessFight.RagdollLab.Editor.SwordFightBuilder.BuildBatch -swordFightTestBuild
Builds/SwordFight/ChessFight.exe -swordFightTest -screen-fullscreen 0 -screen-width 1280 -screen-height 720 -logFile <로그 경로>
```

검사 대상: 입력 격리, 한 칼 한 번 피격, 누운 상대 추격 추가타, 아군 보호, 장외 중복 방지·부활/검 복원, 공통 태클 유지, 질주, 12폰 물리 유한성. `-swordFightTestBuild` 없이 빌드하면 Intro→Lobby부터 시작하는 전체 빌드다. 빌드/검사 로그는 작업 폴더 `Logs/sword-*.log`(Git 제외)에 있다. 오래된 URP 자산의 missing-script 경고와 기존 PawnHipsHull의 convex 한도 경고는 원본에도 있는 별도 문제이며 이번에 공유 에셋을 다시 만들지 않았다.

사람이 확인할 순서: [VALIDATION R47](../../Network/VALIDATION.md). 기획 배경·미확정 방향은 [DESIGN](DESIGN.md).
