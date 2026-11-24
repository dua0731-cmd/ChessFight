# Claude Code 인계: JY 그래픽 업그레이드(JY-gfx)를 main에 합치기

JY(그래픽 담당)의 요청(2026-11-24): "승규님께서 내가 한 그래픽작업 메인에 넣을때 알아서 클로드한테 핸드오프 읽고 적용할 수 있도록"

이 문서를 읽는 Claude가 할 일:
1. `origin/JY-gfx`를 main에 합친다.
2. JY가 따로 전달한 간접광 패키지를 넣는다.
3. 사용자와 함께 확인한다.
4. 팀 규칙(CLAUDE.md, HANDOFF.md §8)대로 마무리한다.

먼저 CLAUDE.md의 지시대로 HANDOFF.md를 읽는다. 이 문서는 그 위에 더하는 것이다.

## 0. 기준

- 브랜치: `origin/JY-gfx`. 코드의 마지막 커밋은 3ce46633이고, 그 위에 이 문서 커밋이 있다.
- main에서 갈라진 곳: 8c3dc14e.
  - 작성 시점 main은 f312a0c0(R810-R823)으로 6커밋 앞서 있었다.
  - `git merge-tree --write-tree origin/main origin/JY-gfx`는 충돌 없음(2026-11-24).
- Unity 6000.3.11f1, URP 17.3.
- 간접광 데이터: `JY-gfx_baked_light.unitypackage`(185MB). git에는 없고 JY가 파일로 따로 전달한다(§4).
- 확인 상태:
  - JY는 배치 렌더 사진으로 결과를 봤다.
  - main과 합친 상태의 컴파일과 실행은 아직 아무도 확인하지 않았다.
  - 팀 규칙대로, 사용자가 Unity나 Steam에서 직접 본 것만 '확인됨'이라고 쓴다.

## 1. 순서

1. `git status`가 깨끗한지 본다. 사용자의 커밋하지 않은 작업이 있으면 먼저 어떻게 할지 묻는다.
2. main을 최신으로 맞춘다.
   ```
   git fetch origin
   git checkout main
   git pull --ff-only
   ```
3. 합친다.
   ```
   git merge --no-ff origin/JY-gfx -m "Merge JY-gfx: the graphics upgrade (sky, grade, surfaces, characters, indoor light, baked indirect light)"
   ```
   충돌이 나면 §3 표로 판단한다. JY-gfx가 팀 파일에 넣은 것은 작은 훅이라 대부분 양쪽을 다 살리면 된다.
4. Unity로 열고 Console에 컴파일 에러가 없는지 본다. 팀 테스트를 HANDOFF.md §8대로 돌린다.
5. 패키지를 넣는다. `Assets > Import Package > Custom Package...`에서 `JY-gfx_baked_light.unitypackage`를 열고, All을 고른 뒤 Import한다. `Assets/Resources/Gfx/Baked/`가 생긴다.
6. 커밋하기 전에 §4의 git 처리를 사용자에게 묻는다. 100MB가 넘는 파일이 있어서 그냥 커밋하면 푸시가 거부된다.
7. 사용자와 함께 §5대로 확인한다.
8. HANDOFF.md와 Docs/Project/REQUIREMENTS.md를 고친다(§10 문안). 그다음 커밋하고 main에 푸시한다(팀 규칙).

## 2. 업그레이드가 하는 일

전부 실행 중에만 적용된다. 팀의 프리팹, 씬, 재질 파일은 바꾸지 않는다. 빌드용 재질 변환(§6)은 메뉴를 눌러야만 실행된다.

**스위치**
- `GameGfx.Upgrade`이며 기본으로 켜져 있다.
- Play 중에 ` 키(백쿼트)로 끄고 켠다. 끄면 원래 모습 그대로이고, 화면 왼쪽 위에 "JY-gfx OFF"가 뜬다.
- 적용 범위는 `PawnRushRound`, `PawnRushCourse01`, `PawnRush_Sections` 씬뿐이다(`GameGfx.InScope`).

**Assets/Scripts/Game**

| 파일 | 하는 일 |
|---|---|
| `GameSky` | 하늘 파노라마(`Resources/Gfx/Sky/sky.exr`)와 아침 해(자기 하늘이 없는 씬만) |
| `GameGrade` | 전역 색보정(Neutral 톤매핑). GameLook의 색각 보조를 이 톤매퍼에 맞게 다시 계산한다 |
| `GameSurfaces` + `Shaders/URP/CFSurface.hlsl` | 재질 세부: 표면 세트(`Resources/Gfx/Sets`), 높이와 AO(`Resources/Gfx/Height`). Play 중에 URP Lit 재질에 Gfx Lit 쌍둥이를 입힌다 |
| `GameGrass`, `PawnRush/GfxGround` | 1코스 잔디, 이음매의 풀, 자갈 |
| `GameShafts` + `Resources/Gfx/GfxLightShafts.shader` | 빛줄기 |
| `GameProbes` | 하늘 대신 장소를 비추는 반사 프로브 |
| `GameCharacters` + `Shaders/Characters/CFCharacter.shader` | 말 셰이딩(부드러운 노멀, 둘째 조명, 전역 `_GfxCharacter`) |
| `GameCourses` (+ `ZoneLight`, `ZoneLightMixer` 수정) | 2·3코스 실내에 들어오는 하늘빛의 몫, 홀별 색보정, 빛줄기. 장면의 해는 그대로 둔다 |

**Assets/Scripts/PawnRush**

| 파일 | 하는 일 |
|---|---|
| `GfxSections` | 라운드의 섹션이 켜질 때 업그레이드를 입힌다 |
| `GfxMapLights` | 맵마다 조명 컨셉, 룩(`GameCourses.Look`), 리그 |
| `GfxMapPlans` | 맵마다 조명 설계(조명 하나하나). 생성된 파일이다(§7) |
| `GfxBakedLight` (`GfxBaked`) | 구운 간접광을 섹션에 붙이고 뗀다(§4) |

**Assets/Scripts/PawnRushEditor**

| 파일 | 하는 일 |
|---|---|
| `GfxShots` | 사진 도구. `-gfxFreeze`로 섹션을 게임에서 만든 그대로 저장한다 |
| `GfxBake` | 간접광 굽기 |
| `GfxSetup` | 업그레이드 파일들의 임포트 설정, 하늘 재질 메뉴 |
| `GfxConvert` | 빌드용 재질 변환 메뉴(§6) |

`Assets/Resources/Gfx`에는 하늘, 셰이더(GfxBeam, GfxMoon, GfxLightShafts), 표면 세트, 높이맵, 천문대 돔의 별빛 맵(`Glow/RO_DomeSky_Glow.png`)이 있다.

## 3. 팀 파일을 건드린 곳 (충돌 판단용)

| 파일 | 바꾼 것 | 충돌하면 |
|---|---|---|
| `Art/PawnRushMap/Shaders/PR_*.shader` 6개, `Materials/CoursePattern.shader` | `#define CF_SURFACE_NONE` / `CF_SURFACE_SCALE` 몇 줄 | 양쪽 다 살린다 |
| `Shaders/URP/CFLit.hlsl` (+42) | CFSurface 훅 | 양쪽 다 살린다 |
| `Shaders/Characters/CFCharacter.shader` (+77) | 둘째 조명(`_GfxCharacter`가 0이면 원래와 같다) | 양쪽 다 살린다 |
| `Scripts/Game/GameLook.cs` (+12) | `RefreshColourAid`, Neutral 톤매퍼에 맞춘 색각 보조 행렬 | 양쪽 다 살린다 |
| `Scripts/Game/ZoneLight.cs`, `ZoneLightMixer.cs` (+68) | `KeepSun`, `AmbientScale/Neutral/Tint`, 바깥에서 프로브가 바뀔 때 다시 재기(`Renotes`) | 양쪽 다 살린다 |
| `Scripts/PawnRush/YardAtmosphere.cs` (+9) | Gfx Sky 밝기 따라가기 | 양쪽 다 살린다 |
| `Settings/PC_RPAsset.asset` | MSAA 1→4, 그림자 거리 40→120, 캐스케이드 2→4, 컬러 그레이딩 HDR | JY-gfx 값을 쓴다. 성능 영향이 있다(§5) |
| `ProjectSettings/GraphicsSettings.asset` | Lightmap Stripping 0→1(Custom) | JY-gfx 값을 쓴다. 이게 없으면 빌드에서 라이트맵이 안 보인다 |

## 4. 간접광 데이터 (패키지)

**들어 있는 것.** `Assets/Resources/Gfx/Baked/`에 섹션 키마다 파일 두 종류가 있다.
- `<키>.asset`: GfxBakedLight. 부분의 경로, 두 번째 UV 메시 사본, 라이트맵 번호와 scaleOffset.
- `<키>/Lightmap-N.exr`

| 키 | .asset | 라이트맵 |
|---|---|---|
| `3-2_RoyalObservatory` | 175MB | 4장 |
| `2-1_GrandStairHall` | 229MB | 3장 |
| `MG_J_PalaceLaundry` | 62MB | 1장 |
| `MG_G_RoyalProcession` | 15MB | 3장 |

**작동.**
- 업그레이드가 켜지면 섹션 이름(키)으로 Resources에서 찾아 정적 부분에 라이트맵을 붙인다. 끄면 뗀다.
- 메시 이름이나 정점 수가 다른 부분은 건너뛴다. 팀이 프리팹을 바꾸면 그 부분만 간접광이 빠지고, 다시 구워야 한다.
- 데이터가 없는 홀은 실시간 조명만으로 돌아간다(에러 없음).

**git (반드시 사용자에게 묻는다).**
- `.asset` 두 개가 GitHub 파일 한도 100MB를 넘는다. 일반 커밋으로는 푸시가 거부된다.
- `*.exr`는 이미 `.gitattributes`에서 LFS다.
- 선택지:
  - a. LFS: `.gitattributes`에 `Assets/Resources/Gfx/Baked/*.asset lfs`를 더하고 커밋한다. LFS를 약 550MB 쓰므로 저장소의 LFS 용량을 확인한다.
  - b. 각자 로컬: `.gitignore`에 `Assets/Resources/Gfx/Baked/`를 더하고 패키지로 나눈다. 빌드를 만드는 PC에는 반드시 넣어야 한다.
- `git add -A`로 그냥 넣고 푸시하지 않는다.

**다시 굽기** (프리팹이 바뀌었을 때). 배치 모드에서 GPU 라이트매퍼로 홀 하나에 몇 분 걸린다.
1. 섹션을 게임에서 만든 그대로 저장한다. 결과는 `Assets/GfxBakeTest/Frozen/`에 생기며, 커밋하지 않는다.
   ```
   Unity.exe -batchmode -projectPath <프로젝트> -executeMethod ChessFight.PawnRush.Editor.GfxShots.Run -gfxOut <폴더> -gfxTag fz -gfxSections Late_3-2_RoyalObservatory -gfxSpots 1 -gfxFreeze -gfxTune "now:bake=0" -logFile <로그>
   ```
2. 굽는다. 결과는 `Resources/Gfx/Baked/<키>`에 쓰인다.
   ```
   Unity.exe -batchmode -projectPath <프로젝트> -executeMethod ChessFight.PawnRush.Editor.GfxBake.Bake -bakeSections 3-2_RoyalObservatory -bakeOut <폴더> -bakeTexels 10 -bakeBounces 3 -bakeSamples 512 -logFile <로그>
   ```

## 5. 확인 (사용자가 직접 본 것만 '확인됨')

Play에서 PawnRushRound(또는 PawnRush_Sections)를 열고, 각 홀에서 ` 키로 켜고 끄며 비교한다.

- **천문대(3-2, 밤)**
  - 길은 위에서 밝게 비추고, 갤러리는 어두운 밤이다.
  - 별 돔이 푸르게 빛나고, 돔 안에 달이 있다.
  - 조각상 93개 위에 따뜻한 니치 조명이 있다.
  - 원뿔 모양 빔과 뿌연 안개는 없어야 한다.
- **행렬 홀(MG_G), 세탁실(MG_J), 대계단 홀(2-1)**: 밝은 아침 실내다.
- **간접광**: 위 네 곳은 패키지를 넣은 뒤 그늘 쪽이 부드럽게 밝아진다.
- **Console**: 에러가 없어야 한다.
- **성능**: 12인 라운드에서 FPS를 본다.
  - 부담 요인: MSAA 4x, 그림자 120m에 캐스케이드 4, 천문대의 실시간 조명 110개(그림자 없음).
  - 떨어지면 JY와 상의한다. 예: 조각상 조명 수(`GfxMapPlans`의 `Max`), 품질 설정.

## 6. 빌드(Steam) 때

- **Gfx Lit.**
  - 에디터 Play에서는 URP Lit 재질이 실행 중에 Gfx Lit 쌍둥이로 바뀐다. 하지만 빌드에는 재질 파일이 쓰는 셰이더만 들어간다.
  - 빌드에서도 그 재질들의 세부를 보이려면 `ChessFight/Graphics/URP Lit materials -> Gfx Lit (for a build)` 메뉴로 재질 파일을 바꿔야 한다. 팀 재질 파일이 많이 바뀌므로 사용자, 팀과 상의한 뒤에 한다.
  - 되돌리는 메뉴는 `Gfx Lit materials -> URP Lit`이다.
  - 안 하면 그 재질들만 URP Lit 그대로 나오고, 나머지 업그레이드는 작동한다.
- **Lightmap Stripping**은 Custom(GraphicsSettings)을 유지한다.
- `Resources/Gfx`(하늘, 셰이더, 세트, 라이트맵)는 Resources라서 빌드에 자동으로 들어간다.

## 7. 조명 설계 원본 (GfxMapPlans.cs는 생성 파일)

- `GfxMapPlans.cs`와 `GfxMapLights.cs`의 일부 맵 항목은 JY의 설계 JSON에서 도구로 만든 것이다.
- 원본은 `ArtSource/Gfx/Lighting/`에 있다. 스크립트 안의 경로는 JY PC 기준(`C:/cf-gfx`)이라 참고용이다.
  - `designs/*.design.json`: 홀마다 컨셉, 룩, 리그, 조명 목록
  - `apply_designs.py`: 설계를 GfxMapLights에 반영한다
  - `gen_plans.py`: GfxMapPlans를 만든다
  - `run_halls.sh`: 반영, freeze, 굽기, 사진을 한 번에 한다
  - `lighting_method.md`: 조명 설계 방법(레이어: daylight, general, wash, uplight, decorative, accent)
- 손으로 고치면 JY가 다음에 다시 생성할 때 덮일 수 있다. 고쳤다면 JY에게 알린다.

## 8. JY가 정한 규칙 (바꾸려면 JY에게)

- 기존 게임의 그래픽 업그레이드다. 맵과 말은 그대로이고, 팀 프리팹 파일은 바꾸지 않으며(실행 중에만 적용), 업그레이드를 끄면 원래대로 돌아간다.
- 시간대는 맵 컨셉을 따른다. 지금은 천문대(3-2)만 밤이고 나머지는 전부 아침이다. 연출을 위해 시간을 바꾸지 않는다.
- 실내는 실제 낮의 궁전 방처럼 밝게 한다. 닫힌 방일수록 조명을 많이 단다. 어둡게 해서 분위기를 내지 않는다.
- 섹션 끝은 게임에서 통로로 막혀 있다. 빛이 새는지는 통로를 붙인 상태로 판단한다.
- 그늘, 안개, 색보정을 노랗게 하지 않는다. 따뜻한 것은 광원뿐이다.
- 맵마다 자기 컨셉의 조명을 쓴다. 코스 하나에 룩 하나로 통일하지 않고, 해질녘을 기본값으로 쓰지 않는다.

## 9. 아직 안 된 것 (JY가 이어서 한다)

- 새 조명 설계와 간접광이 들어간 곳은 천문대, 행렬 홀, 세탁실, 대계단 홀 네 곳이다.
- 나머지 아홉 곳은 이전 맵별 리그(9bd1bb1c) 그대로이고 간접광도 없다: 2-2 시계탑, MG_B 수리 공방, MG_E 열람실, MG_F 무도회장, 3-1 왕립 도서관, 3-3 왕실 금고, MG_H 왕의 체스 홀, MG_I 침실, 전쟁 회의실.
- 굽기 데이터 크기 줄이기: 두 번째 UV를 FBX 임포트의 Generate Lightmap UVs로 만들면 메시 사본이 필요 없다. 팀 FBX 설정을 바꾸는 일이라 결정이 필요하다.
- 3-1 왕립 도서관: 팀 코드의 주황 햇빛을 그대로 둘지는 아직 정하지 않았다.

## 10. HANDOFF.md에 넣을 문안 (마무리 때, 팀 형식에 맞게 다듬어서)

> **JY 그래픽 업그레이드 (JY-gfx, 2026-11-24 합침).**
> - 기존 맵에 실행 중에만 입히는 그래픽 업그레이드다. Play 중 ` 키로 끄고 켠다.
> - 범위: 하늘, 색보정, 재질 세부, 1코스 잔디, 빛줄기, 반사 프로브, 말 셰이딩, 2·3코스 홀별 조명(GfxMapLights/GfxMapPlans), 구운 간접광(GfxBaked).
> - 간접광 데이터(`Assets/Resources/Gfx/Baked`)는 <정한 방식: LFS 또는 로컬 패키지>.
> - 자세한 내용은 `CLAUDE_HANDOFF_JY_GFX.md`에 있다. 조명 설계 원본은 `ArtSource/Gfx/Lighting/`에 있다.
