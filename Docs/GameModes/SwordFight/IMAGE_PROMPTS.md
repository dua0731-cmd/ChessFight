# 소드 파이트 — 이미지 생성 기록

2026-09-27 · R45 · 내장 `imagegen` 사용(API/CLI 사용 안 함).

- 결과: [경기장 시안 v01](Concepts/arena-v01.png).
- 역할: 기획 검토용 콘셉트. 실제 Unity 화면이나 최종 모델이 아님.
- 참고: 사용자 영상에서 추출한 `frame_04.jpg`. 체스 말 실루엣·짧은 칼·넘어지는 상황의 참고이며 기존 화면 복제가 아님.
- 원본 영상은 사용자 로컬에 보존하며 저장소에 복사하지 않음.
- 흑백 팀은 기존 사용자 결정, 청회색·모래색 바닥·금색 테두리와 하늘 배경은 이번 미확정 아트안.
- 생성 결과의 장외·넘어진 기물·체스 모티브를 확인함. 정확한 인원·타일 치수·모든 칼 소지는 별도 설계 필요.

## 사용한 전체 프롬프트

```text
Use case: stylized-concept.
Asset type: first environment-and-action concept for ChessFight's Sword Fight mode, a cute chaotic ragdoll team ring-out brawler; this is a NEW concept image, not an actual gameplay screenshot.
Input image 1: ONLY a visual reference for the tiny chess pawn silhouette, stubby rounded body, spherical pawn head, small arms and feet, simple toy sword, and readable close-range slapstick. Do not copy its debug UI, exact checker colors, prototype graphics or camera.
Primary request: create one polished wide 3D game concept render of a compact chessboard-inspired combat arena with small ivory chess-piece people fighting small charcoal chess-piece people using short broad rounded silver swords with simple warm brass crossguards. The characters are the actual anthropomorphic chess pieces, not humans wearing chess hats. Include recognizable pawn, rook, bishop, knight, queen and king silhouettes, all cute and similarly sized. No blue/red team collars. Black and white bodies identify teams.
Arena: a single square thick raised 8-by-8 tiled board approximately 24 character-heights across, floating above a soft out-of-focus pastel sky void. Entire board visible with clear open unrailed edges for ring-outs; the top is flat and mostly open, no central pit, no narrow bridges. Alternating muted slate-blue and warm sandy tiles, rounded beveled golden-tan edging. Only four low rounded rook-shaped blocks sit well inside the corners, low enough not to hide characters. A subtle crown engraving is flush in the four central tiles, NOT a raised throne or objective. Chess carving on thick side fascia, modest toy-like materials, not an ornate giant castle or an ornate colosseum. No audience or surrounding architecture.
Action: about twelve small pieces spread in readable skirmishes, several pairs swinging swords; one charcoal piece flops sideways from an ivory piece's sword strike with loose limbs, one ivory piece tumbles beyond the near open edge with its sword still in hand, two others tangled on the floor getting up. No gore, no wounds, no grim violence. Emphasize funny off-balance body poses, clear white curved sword swishes and small impact stars. Not a solemn chess game with static pieces in formation. No big character close-up covering the arena.
Composition: elevated three-quarter 3D view, landscape 16:9, the arena occupies most of the image, near edge and dropping character clearly visible; sufficiently close to read the short arms, sword grips and funny ragdoll poses while seeing the full board. Soft shadows ground every standing character, clean silhouettes against colored tiles. Premium cohesive stylized indie-party-game art, matte painted toy ceramic, pleasantly rounded shapes, warm daylight, gentle blue ambient fill, energetic mischievous cheerful mood.
Constraints: no text, no HUD, no health bars, no logos, no watermark, no ability effects, no throne, no capture zone, no tower climbing, no blood, no black-white floor. Keep the battlefield simple and implementable in Unity. No detached or extra limbs; every sword is held by its character.
```
