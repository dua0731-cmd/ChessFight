# 개발 환경 설정

## 1. 버전과 패키지

| 항목 | 값 |
|---|---|
| Unity | **6000.3.11f1** (`ProjectSettings/ProjectVersion.txt`). 다른 버전으로 열지 않는다 |
| 플랫폼 | Windows x64 (확인된 유일한 플랫폼) |
| Steamworks.NET | 2025.164.1, git 커밋 `c21a8f0e31c56ae8707130967faf491f7dd7c0d8`에 고정 |
| Input System | **설치하지 않는다**(2026-09-25, R20 `JY-lobby`). 설치돼 있으면 Unity가 켤 때마다 "native platform backend not enabled" 창을 띄우고, 켜면 HUD 클릭이 죽는다. 입력은 Active Input Handling = Old |
| uGUI (`com.unity.ugui`) | **없다.** UI는 UI Toolkit |
| URP | 패키지 없음. 템플릿 URP 에셋만 남아 있고 `RenderPipelineOverride`가 Built-in으로 그린다 |
| Multiplayer Center | 템플릿 기본. **UGS를 쓴다는 뜻이 아니다** |
| Steam App ID | 480 (루트 `steam_appid.txt`). 출시 전에 자체 App ID로 바꾼다 |

manifest/lock은 커밋되어 있어 Pull하면 Unity가 복원한다. 복원이 실패하면 메뉴 `ChessFight > Setup > Install dependencies`로 설치하고, **바뀐 manifest/lock을 커밋한다**(Discard 금지).

## 2. 처음 열기

1. Git for Windows와 **Git LFS**를 설치한다. 명령줄을 쓰면 `git lfs install`을 한 번 실행한다(GitHub Desktop은 자동).
2. 원하는 브랜치를 Pull한다(`Network` 또는 `main`). **Unity가 여는 폴더와 Pull한 폴더가 같은지 확인한다.**
3. Unity Hub에서 6000.3.11f1로 연다. 패키지 복원과 컴파일을 기다린다.
4. Steam 클라이언트를 실행하고 로그인한다.
5. 메뉴 `ChessFight > Scenes > Intro (online flow)` 또는 `Lobby (online)`를 열고 Play한다.
   - `King Rush (offline playtest)`와 `Ragdoll Test (offline)`는 Steam 없이 바로 시험된다.

## 3. Unity 메뉴 (`Scripts/Editor/NetworkSetup.cs`)

| 메뉴 | 동작 |
|---|---|
| `ChessFight/Setup/Install dependencies` | Steamworks.NET을 UPM Client API로 설치(Input System은 설치하지 않음) |
| `ChessFight/Setup/Report input backend` | 현재 입력 설정과 백엔드를 Console에 출력 |
| `ChessFight/Scenes/…` | Intro, Lobby, King Rush, Ragdoll Test 열기 |
| `ChessFight/Network/Build Windows development test` | Intro·Lobby·KingRush로 x64 개발 빌드 → `Builds/NetworkTest/ChessFight.exe`, `steam_appid.txt` 복사 |

`InputSettingsGuard`(`[InitializeOnLoad]`)는 uGUI가 없는 동안 Active Input Handling을 Old로 되돌린다. 되돌렸다면 **Unity를 완전히 재시작**해야 적용된다.

## 4. 빌드 배포

- 팀원에게는 `Builds/NetworkTest` **폴더 전체**를 보낸다(exe만으로는 실행되지 않는다). `Builds/`는 git 대상이 아니다.
- 테스터에게 새 빌드를 줄 때마다 **Player Settings › Version을 올린다.** 로비의 `build` 값이 달라져 옛 빌드가 "게임 버전이 다릅니다" 안내를 받는다.
- 개발 빌드는 build 값에 `-dev`가 붙어 릴리스 빌드와 만나지 않는다.

## 5. Git 설정 (한 번만)

```text
git config merge.unityyamlmerge.name "Unity SmartMerge"
git config merge.unityyamlmerge.driver "'C:/Program Files/Unity/Hub/Editor/6000.3.11f1/Editor/Data/Tools/UnityYAMLMerge.exe' merge -p %O %B %A %A"
```

`.gitattributes`에 Unity YAML 병합 드라이버와 LFS(`.fbx .png .psd .wav` 등)가 지정되어 있다.

## 6. 두 PC 테스트 준비

- **서로 다른 Steam 계정**의 두 PC. 같은 계정으로 Editor와 exe를 띄운 것은 두 사용자 검증이 아니다.
- 두 PC 모두 **같은 커밋**(프로토콜 v2 이상)이어야 서로 찾는다.
- 절차는 [Network/README](../Network/README.md) '가장 빠른 2인 테스트'.
