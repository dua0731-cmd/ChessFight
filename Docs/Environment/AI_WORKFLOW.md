# AI 작업 규칙

어떤 AI 도구든 이 저장소에서 작업할 때 따르는 절차다. 목표는 **도구가 바뀌어도 하나의 도구가 계속 작업한 것처럼 이어지는 것**이다.

## 1. 시작할 때

1. `HANDOFF.md`를 끝까지 읽는다.
2. `git status`, `git log --oneline -5`, 현재 브랜치를 확인한다. **사용자의 미커밋 변경은 보존한다.**
3. 작업 분야 문서를 읽는다([HANDOFF §6](../../HANDOFF.md#6-어디를-읽을까--작업-분야별-안내)).
4. [REQUIREMENTS](../Project/REQUIREMENTS.md)에서 비슷한 과거 요청을 찾는다. 이미 해결된 문제를 다른 방식으로 다시 풀지 않는다.
5. [DECISIONS](../Project/DECISIONS.md)와 어긋나는 변경이 필요하면 **먼저 사용자에게 묻는다.**

## 2. 작업 원칙

- 브랜치: **`Network`에만 커밋·푸시**한다. main 병합과 PR은 사용자가 요청할 때만.
- 커밋 메시지는 영어. 무엇을 왜 바꿨는지 쓴다. 따옴표가 많으면 파일로 써서 `git commit -F`.
- 코드 주석은 영어, 주변 코드의 밀도와 말투를 따른다. 사용자에게 보이는 문구는 한국어.
- 새 스크립트는 해당 어셈블리 폴더에 둔다([STRUCTURE](../Architecture/STRUCTURE.md)). 새 폴더가 필요하면 새 asmdef 여부를 먼저 정한다.
- Unity 밖에서 만든 파일(스크립트, Assets 아래 문서)에는 `python3 Tools/Generators/mkmeta.py <경로>`로 `.meta`를 만든다. GUID는 경로의 md5라 다시 만들어도 같다.
- 씬·프리팹·재질을 Unity 없이 만들어야 하면 `Tools/Generators/gen_scenes.py`의 방식(결정적 GUID, 내장 메시 fileID)을 따른다. **Unity에서 이미 저장된 자산은 생성기로 덮어쓰지 않는다.**
- 네트워크 호환이 깨지는 변경: `SteamSession.Protocol` 증가, 패킷 구조 변경 시 `MotionProtocol` magic 증가, Core 테스트 추가.
- 사용자가 요청하지 않은 대형 전환(URP 도입, uGUI 도입, 다른 네트워크 프레임워크)을 끼워 넣지 않는다.

## 3. 테스트

| 환경 | 명령 | 내용 |
|---|---|---|
| Linux / 클라우드 AI | `Tools/run-tests-linux.sh` | **어셈블리 경계 검사**(Steam은 Network·Bootstrap에만, `Assets/Scripts`는 래그돌을 참조하지 않음) + Core 테스트 + 모의 Steam 세션 테스트. Mono가 없으면 apt로 설치 |
| 〃 | `Tools/run-tests-linux.sh --compile` | 위 + **Roslyn**(.NET 8 SDK, 없으면 apt로 설치)으로 Steamworks.NET 원본 소스(고정 커밋)와 Unity 참조 DLL(NuGet `UnityEngine.Modules` 2021.3)에 대해 Core·Network·Game·Gameplay·Bootstrap·RagdollLab 컴파일. Input/·에디터 코드는 제외 |
| Windows (Unity 설치) | `./Tools/Test-NetworkCore.ps1` | Core + 세션 테스트 (Unity 내장 Mono 사용) |
| 〃 | `./Tools/Test-NetworkCompile.ps1 -SteamRuntimeSources <PackageCache의 Steamworks Runtime>` | 실제 Unity DLL로 컴파일(09-26부터 랩 Steam 다리 `RagdollLabSteam`과 랩 빌더도 asmdef 참조 그대로). 이 PC: `-SteamRuntimeSources Library/PackageCache/com.rlabrecque.steamworks.net@6fb66c768572/Runtime` |
| 〃 (래그돌 물리) | 프로젝트 사본 → `Unity.exe -batchmode -nographics -projectPath <사본> -executeMethod ChessFight.RagdollLab.Editor.RagdollLabBuilder.BuildPlayerBatch -labOut <사본>\build` → `RagdollLab.exe -batchmode -nographics -ragdollAutoTest report.txt` (`-ragdollQueenHillOnly`로 퀸 오브 더 힐 점검만) | **실제 PhysX로 도는 래그돌 자동 점검.** 에디터가 같은 프로젝트를 열고 있으면 배치 모드를 못 쓰므로 사본(`Assets`·`Packages`·`ProjectSettings`·`Library`, 약 210 MB)에서 한다. 결과 기준: [RagdollLab README "자동 점검"](../RagdollLab/README.md#자동-점검). 사람의 Unity 확인을 대신하지 않는다 |
| 컴파일러 없음 | `python3 Tools/Generators/check_braces.py` | 괄호 균형만 확인 |

- 현재 기준: **Core 29개, 세션 15개**, 래그돌 자동 점검 **46 통과 / 9 실패(기존 실패, 09-26)**. 수가 줄면 뭔가 빠진 것이다.
- 테스트 파일: `Tests/Network/NetworkCoreTests.cs`, `SessionFlowTests.cs`, `FakeSteam.cs`(Steam/Unity API 모사). 새 Steam API를 쓰면 `FakeSteam.cs`에도 스텁을 추가한다.
- 테스트 통과는 **Unity 실행 검증이 아니다.** Unity·Steam에서만 확인할 수 있는 항목은 [VALIDATION](../Network/VALIDATION.md) 최상단에 '미확인'으로 추가하고 사용자에게 확인 방법을 알려준다.

## 4. 끝낼 때

[HANDOFF §8 작업 종료 체크리스트](../../HANDOFF.md#8-작업-종료-체크리스트)를 그대로 따른다. 특히:

- `Docs/Project/REQUIREMENTS.md`에 요청 한 줄
- `HANDOFF.md` §1·§2·§3 갱신
- 바꾼 분야 문서 갱신
- 커밋 → `git push -u origin <작업 브랜치>` (로비·게임모드는 `JY-lobby`, 그 밖은 `Network`. [HANDOFF §1](../../HANDOFF.md))

## 5. 사용자에게 보고할 때

- 한국어로 쓰고, 무엇을 했는지 → 확인한 것과 못 한 것 → 사용자가 Unity에서 볼 것 순으로 쓴다.
- 확인하지 못한 것을 됐다고 쓰지 않는다. "테스트 통과"와 "Unity에서 동작"을 구분한다.
