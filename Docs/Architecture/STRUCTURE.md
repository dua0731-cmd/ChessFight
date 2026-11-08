# 폴더·어셈블리 구조

2026-11-06 · R497 정리 기준. 기존 R3·R90의 **종류별 최소 폴더, 스크립트는 어셈블리별 관리** 원칙을 유지한다. 사용자가 명시한 `Prototype`은 구형 콘텐츠와 시험 결과를 구분하는 예외다.

## 1. 폴더

```text
Assets/
  Art/                  현재 모델·텍스처·생성 메시
  Materials/            공유 재질·셰이더
  Prefabs/              현재 캐릭터·장애물·PawnRushSections·PawnRushMaps(맵 시험 장면용 맵 프리팹, R507)
  Resources/            코드가 이름으로 읽는 현재 UI·글꼴·모델·VFX 세트
  Scenes/               Intro·Lobby·현재 시험 장면·결과 화면·SwordFight
  Scripts/              원래 어셈블리별 현재 코드
  Settings/             기물 튜닝·Course01Kit·PawnRushSections·URP 설정
  Shaders/              공유 URP HLSL
  StreamingAssets/      시작 영상
  VFX/                  현재 이펙트 묶음(기존 FX1 예외)
  Prototype/            구형 콘텐츠·독립 프로토타입
    PawnRush/
      Scenes/           구형 전체 맵 PawnRush_Course01
      Prefabs/OpeningCourse/  초기 구간 프리팹24개
      Resources/        구형 맵이 이름으로 읽는 보존 자산
      Scripts/          구형 코드41개, 원래 어셈블리에 대한 asmref7개
    ChessBoxing/        독립 체스복싱 프로토타입
    Previews/           기물 미리보기 장면
ArtSource/              Blender 원본·가공 스크립트·내보내기 자료
Docs/                   HANDOFF에서 분야별 문서로 이동
  PawnRush/             현재 폰 러시 문서(KingRush 폴더명 대체)
    Prototype/          초기 KingRush 설명의 역사 사본
Tests/Network/          Unity 밖 Core·모의 세션 시험
Tools/                  검사·제작·생성 도구
```

`Assets/_Recovery`와 폐기된 Queen of the Hill 전용 장면·자산·코드·제작 도구는 R497에 정리한다. 같은 기능을 새 모드로 등록하지 않는다. 폰 러시·소드 파이트가 계속 사용하는 기물 동작·물리·이펙트는 공용 코드로 남긴다.

자산을 옮길 때는 `.meta`를 함께 옮겨 GUID를 유지하고, 코드의 경로 문자열·빌드 장면 목록·생성기도 새 위치에 맞춘다. `Resources.Load`는 `Resources/` 아래 상대 이름을 계약으로 사용한다.

## 2. 어셈블리

| 현재 코드 폴더 | 어셈블리 | 역할 |
|---|---|---|
| `Scripts/Core` | `ChessFight.Network.Core` | Unity 없는 규칙·패킷·모드 목록·예약·세션 보조 |
| `Scripts/Network` | `ChessFight.Network.Steam` | Steam 세션·이동 전송, Steam 패키지 게이트 |
| `Scripts/Game` | `ChessFight.Game` | 화면·카메라·입력 추상화·씬 이름·공유 패널 |
| `Scripts/Gameplay` | `ChessFight.Gameplay` | 캐릭터 계약·코스·장애물·오프라인 플레이테스트 |
| `Scripts/Bootstrap` | `ChessFight.Game.Steam` | 세션과 씬 연결·게임 진입·온라인 컨트롤러 |
| `Scripts/Input` | `ChessFight.Game.Input` | 선택형 Input System 어댑터 |
| `Scripts/Editor` | 기본 Editor 어셈블리 | 설치·개발 빌드·씬·캡처 도구 |
| `Scripts/RagdollLab` | `ChessFight.RagdollLab` | 공용 래그돌·기물 스킬·소드 파이트·랩 |
| `Scripts/RagdollLabEditor` | `ChessFight.RagdollLab.Editor` | 기물·스킬·장면 제작 |
| `Scripts/RagdollLabSteam` | `ChessFight.RagdollLab.Net` | 래그돌·폰 러시·소드 파이트 온라인 다리 |
| `Scripts/PawnRush` | `ChessFight.PawnRush` | 현재 구간·미니게임·정원·홀·작전 회의실 |
| `Scripts/PawnRushEditor` | `ChessFight.PawnRush.Editor` | 현재 구간·모델·프리팹 제작·검사 |
| `Scripts/ImportedMapEditor` | `ChessFight.ImportedMap.Editor` | 보존한 장애물 자산 도구 |
| `Scripts/VFX`·`VFXEditor` | `ChessFight.VFX`·`ChessFight.VFX.Editor` | 공유 VFX·제작·캡처 |

프로토타입 코드의 `Core`·`Game`·`Gameplay`·`RagdollLab`·`RagdollLabEditor`·`PawnRush`·`PawnRushEditor` 폴더는 **새 어셈블리를 만들지 않는다.** 각각의 asmref가 위 원래 asmdef GUID를 가리킨다. partial 클래스·스크립트 형식·직렬화 식별자가 바뀌지 않도록 코드 파일명·namespace·메타를 유지했다.

외부 검사도 같은 논리 소속을 수집한다. `Tools/Test-NetworkCore.ps1`은 현재 Core와 프로토타입 Core를 함께 읽고, `Test-NetworkCompile.ps1`·`run-tests-linux.sh`도 각 원래 어셈블리에 프로토타입 소스를 포함한다.

## 3. 공용 코드와 이름

폐기 모드의 전용 규칙·카드·맵을 제거해도 다른 모드가 사용하는 구현은 유지한다. 현재 공용 명칭은 `Core/PioneerRules.cs`, `Gameplay/Course/PioneerCourse.cs`, `RagdollLab/PhysicsMechanicsBed.cs`, `RagdollLab/SkillKit/` 등이다. `ModeRetiredQueenHill` 같은 예약 번호는 이전 전송 값과 충돌하지 않게 남긴 호환 자리이며 모드 선택 항목이 아니다.

예전 `KingRush*` 타입은 `Prototype/PawnRush/Scripts` 안에서 보존한다. 예전 네트워크 키 `kingrush`는 통신 계약이라 폴더명과 함께 바꾸지 않는다(R508부터 그 키가 여는 장면은 `PawnRush_Round`, 씬 이름 `PawnRush_Course01`은 빌드에 남은 구형 장면).

`Prototype`은 관리용 분류다. asmref가 원래 어셈블리에 연결된 코드와 `Resources` 자산이 자동으로 플레이어 빌드에서 제외되는 것은 아니다. 특히 구형 Course01은 아직 로비의 온라인 목적지다.

## 4. 의존 방향과 게이트

- Core는 Unity·Steam을 참조하지 않는다.
- Game·Gameplay는 Steam을 참조하지 않는다. 캐릭터 구현은 `ICharacterDriver` 같은 계약으로 호출한다.
- Bootstrap과 각 Net 어셈블리가 세션·장면·캐릭터 온라인 연결을 소유한다.
- 프로토타입 폴더로 이동해도 같은 어셈블리의 참조 경계·Editor 전용 여부·플랫폼 게이트를 그대로 적용한다.
- `CHESSFIGHT_STEAM`·`CHESSFIGHT_INPUTSYSTEM`은 패키지 설치 상태, `ENABLE_INPUT_SYSTEM`은 Unity 입력 설정이다. 서로 바꾸어 쓰지 않는다.

## 5. 메뉴·씬 작업 안내

상단 메뉴는 **Pawn Rush / Sword Fight / Development / Prototype**으로 나눈다. 현재 구간 작업은 [폰 러시](../PawnRush/README.md), 장면과 빌드의 역할은 [SCENES](SCENES.md), 모드 키와 로비 흐름은 [GameModes](../GameModes/README.md)를 따른다. 파일 정리만으로 새 구간 코스의 온라인 연결이나 사용자 확인이 완료되지는 않는다.
