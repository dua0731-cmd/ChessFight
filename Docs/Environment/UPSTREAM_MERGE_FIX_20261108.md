# upstream 병합 충돌 복구 — 2026-11-08

원격 main의 `b93100f`는 충돌 표시가 남은 채 커밋되어 있었다. Git의 미병합 인덱스가 없어도 Unity는 해당 C# 파일에서 `merge conflict marker encountered`를 보고한다.

- 28개 파일의 충돌 구간을 통합했다. 포크의 장애물별 클래스·하중식 다리·방향키 점프와 upstream의 바람 운반·미니게임 캐릭터 계약을 함께 보존했다.
- 잘못된 rename 병합으로 `ImportedChessFightMap.unity` 내용이 `Late_MG_H_KingsChessHall.prefab` 안에 들어갔다. 체스홀은 upstream `243b3e1` 이후 변경 없는 원본으로 복구하고, 장애물 테스트 씬은 병합 직전 `8474a74`의 원래 GUID로 별도 복구했다.
- upstream의 정원 보트/로봇을 KinematicObstacle에 연결하고 코스용 일반 이동부를 CourseObstacleMotion으로 이전했다. 추상 부모를 저장한 기존 3개 자산의 스크립트 연결도 수정했다.
- 점프 간격이 정해진 upstream 코스 빌더만 명시적으로 방향성 발사를 설정한다. 기본 JumpPad는 계속 방향키 공중 조작을 사용한다. 접이식 다리는 하중식이므로 시간 위상 목록에서 제외했다.
- upstream에서 폐기한 Queen 모드의 고립된 Level 스크립트는 텍스트 아카이브로 보존했다. 기존 래그돌 시험은 새 PhysicsMechanicsBed 이름을 사용한다.
- 이동된 자산 경로를 생성기와 검사에 반영했다. 충돌 표시, 테스트 씬/체스홀 내부 참조, 추상 스크립트의 자산 연결을 회귀 검사한다.

## 검사와 한계

- Gameplay 자동 검사 45개 통과(기존 42 + 병합 회귀 3).
- 세션 시뮬레이션 66개 통과. 실제 Steam 검사가 아니다.
- Core, Network.Steam, Game, Gameplay, VFX, Bootstrap, RagdollLab, PawnRush, RagdollLabSteam 런타임 9개를 Roslyn/Unity 2021 참조로 컴파일했다.
- Core 테스트의 기존 wire golden hash 검사가 Linux Mono에서 실패한다. 수정 전 upstream 테스트/동일 upstream Core 소스로도 동일한 해시 차이를 재현했다. 기대값을 바꾸거나 검사를 끄지 않았다.
- Editor 확장 컴파일은 설치된 구버전 참조에 없는 imageContentsHash/OpenPreviewScene/RenderPipeline 렌더 요청 API 때문에 완료하지 못했다. 런타임 컴파일 결과와 구분한다.
- Unity Editor가 없는 환경이므로 Unity 6 재임포트·플레이·물리·온라인 확인은 미완료다.

## 사용자 확인

Unity를 닫고 GitHub Desktop에서 **Fetch origin → Pull origin**으로 수정 커밋을 받은 뒤 같은 프로젝트를 다시 연다. Console을 Clear하여 이전 메시지와 새 컴파일 결과를 구분한다. GiantChessCourse/ChessTimerMinigame/ImportedChessFightMap과 체스홀 프리팹을 각각 확인한다.
