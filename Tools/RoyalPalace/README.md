# 윗궁 코스 제작 원본

실행/편집/범위: [ROYAL_PALACE_COURSES](../../Docs/KingRush/ROYAL_PALACE_COURSES.md).

- `design.json`: 사용자가 첨부한 최신 3단계 매니페스트의 기획 필드. 이미지 생성 프롬프트/로컬 이미지 경로는 제외.
- `build_courses.py`: 5개 **코스 프리팹**의 고정 배치와 기존 클래스 설정. R117부터 시험 씬은 생성하지 않는다. 처음은 `--write`, 비교는 `--check`, 의도적 재생성만 `--overwrite`.
- `layout_report.json`: 저장 파일·루트/컴포넌트 ID·구간별 장치 목록·경로 길이. 생성기가 기록한다.
- `preview_layout.py`: 저장 프리팹의 기하를 읽어 상면 SVG로 그린다. Python matplotlib/PyYAML 필요. Unity 플레이 화면을 대신하지 않는다.

생성기 실행 위치는 프로젝트 루트. 새로운 Assets 파일/폴더의 meta는 기존 `mkmeta.py`로 만든다. 기존 씬/기존 장애물은 출력 대상이 아니다. Unity에서 수정한 코스는 생성기에 역으로 반영되지 않는다.

## R118 통합 작전실

`build_war_room.py`는 3-1 원본을 읽어 벽/천장/소품/추가 장치/총지휘부를 확장하고 `WarRoomComplete.prefab`, `GiantChessCourse.unity`, `war_room_report.json`을 출력한다. `--check`는 비교, `--overwrite`는 해당 프리팹과 씬을 덮어쓴다. 원본 5개는 수정하지 않는다. Unity 직접 편집은 생성기에 역반영되지 않는다. [실행/편집 안내](../../Docs/KingRush/WAR_ROOM_COMPLETE.md).

R119: 통합 생성기에 전술 지도 포격판·이동 폰 대열2줄·이중 승인문과 상시 우회로를 추가했다. 이동 범위의 원본 장식 일부는 통합본에서만 비활성화한다. 원본 5개 파일은 그대로다. `war_room_report.json`에 새 장치와 우회 경유점 ID를 기록한다.

R120: `square_route()`가 통합본의 4/5구간을 비활성화하고 횡단 바닥과 체크포인트2를 재배치한다. 외벽 중심선160×160m, 활성 구간1/2/3/6, 작전실 목표120~180초. 원본5개 프리팹에는 반영하지 않는다.

R121: `stable_prefab_ids.py`로 통합 프리팹의 계층/클래스 기반 ID를 유지한다. 순번 ID는 장식 삽입 시 타입이 바뀌어 Unity Prefab mismatch를 일으켰다. WarRoomComplete GUID는 이번에 한 번 이전했으며 이후 고정한다. 씬 생성은 실제 meta GUID를 읽는다.
