# 킹을 지켜라 — Graybox 맵 / Steam + AI

## 실행
1. Assets/Scenes/ProtectTheKing_Graybox.unity를 엽니다.
2. Unity Play 버튼을 누릅니다.
3. Enter 또는 START AI PRACTICE을 누릅니다.

WASD/방향키: 이동, Space: 점프, 마우스 오른쪽 버튼+이동: 카메라 회전.
E 유지: 왕좌 상호작용, R: 개인 체크포인트 복귀, Tab 또는 HUD 버튼: 조작할 기물 전환.
경기 종료 후 Enter로 재시작합니다.

## 현재 구현 범위
- ZONE 1(0~150): BLUE/RED 분리 코스, 짧은 낙사, Pawn 슬라럼, 회전봉, 개폐 게이트.
- ZONE 2(150~250): 공용 교전장, 움직이는 Rook Wall, 회전봉, 공용 출구.
- ZONE 3(250~430): 직선 엄폐 통로, 대각선 기둥, 낮은 벽/단차, 넓은 합류 공간.
- ZONE 4(430~540): 회전 체스판, 회전봉, 이동 Blocker, 공용 출구.
- ZONE 5(540~690): 분리 코스, 교차 게이트, 이동 발판, 마지막 CP.
- ZONE 6(690~790): 최종 광장, 독립적인 좌/우 접근로, 공용 왕좌 1개.
- BLUE/RED 각각 King, Queen, Rook, Bishop, Knight, Pawn 1개씩 배치.
- CP0는 시작 위치. CP1~CP5는 개인별로 순서대로 통과해야 합니다.
- King이 모든 CP를 통과하고 왕좌 앞에서 E를 1.75초 유지하면 승리합니다.
- 입력 해제, 거리 이탈 또는 복귀로 왕좌 진행을 취소합니다.
- 시험 규칙: 본 경기 300초, 미완료 시 연장 60초, 이후에도 미완료면 무승부.

Steam 12인 슬롯과 AI 교대 기능이 추가되었습니다. 자세한 실행·설정은 [ONLINE.md](ONLINE.md)를 참고하세요.
오프라인에서는 1명 + AI 11명, Steam 방에서는 사람이 빈 AI 슬롯을 이어받습니다.
개별 기물 능력과 공격/방어 기술은 아직 구현하지 않았습니다. 실제 12대 Steam 동시 접속 검증은 별도로 필요합니다.
기물 전용 능력 없이 기본 이동/점프로 지형을 시험하는 단계입니다.
실제 6 vs 6 교전 발생률과 약 5분 플레이타임은 사람을 통한 후속 검증이 필요합니다.

## 수정 및 검증
맵은 저장된 일반 Scene GameObject이므로 Hierarchy에서 직접 배치/치수를 조정할 수 있습니다.
Assets/ChessFight/Materials와 Prefabs/Player_Graybox.prefab을 사용합니다.
기존 InputSystem_Actions.inputactions와 URP 설정을 유지했습니다.
기존 SampleScene과 NewMonoBehaviourScript는 보존했습니다.

CHESS FIGHT 메뉴:
- 01 Build or Open Graybox: 맵이 있으면 엽니다. 기존 맵을 통째로 재생성하지 않습니다.
- 02 Frame Entire Map: 전체 맵을 Scene 뷰에 표시합니다.
- 03 Validate Graybox: 구성과 참조를 검사합니다.
- 04 Run Play Verification: Play Mode에서 이동/규칙을 검사한 뒤 Play를 종료합니다.

Artifacts/ChessFight에 구조/플레이 검증 보고서와 Previews 이미지가 저장됩니다.
자동 검증은 기본 이동/점프, 벽 충돌, 낙사, CP 순서/복귀, 왕좌 자격/입력 유지/취소,
재시작, 경기 타이머/연장/종료를 확인합니다. 전체 코스 완주와 12인 온라인 테스트를 대체하지 않습니다.