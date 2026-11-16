# 마차뜰 블록아웃 → Blender 모델 작업 기록 (seoungtest 브랜치, R677–R680 + 진행 중인 QA)

> 이 파일만 읽으면 이어서 할 수 있게 쓴 작업 일지다. 아래 "이어서 할 일"을 먼저 보고, 그 밑의 날짜별 기록(영어, 작업 세션의 노트 그대로)을 필요할 때 본다.
> **사용자(승규)가 Unity에서 본 것은 아직 없다.** AI가 Play 화면 · 광선 검사 · 정지 화면으로만 확인했다.
> 번호 주의: main에도 R677–R682가 따로 있다(다른 작업). 이 기록의 R 번호는 seoungtest 브랜치 안에서만 쓴 번호다.

## 지금 상태 (2026-11-16)

- 소스: `ArtSource/PawnRush/CarriageYardModels/*.py` (Blender 5.2에서 exec). 공용 도구 `rcy_kit.py`, 내보내기 `export_rcy.py`.
  구역별: `canal_model.py`(2 운하 · 물레방아), `entrance_model.py`(1 마당), `movers_model.py`(마차 · 바퀴 · 문짝), `horse_model.py`(말, 리그),
  `sorting_model.py`(3 고정물), `sorting_machines_model.py`(3 기계 15개), `platform_model.py`(4 고정물), `castle_model.py`(성문 · 구간 끝, AI 키트),
  `scenery_model.py`(흉벽 톱니 · 가로등 · 까치발 띠). 작업 파일 `CarriageYard_v2.blend`(LFS, 92 MB).
- 결과물: `Assets/Resources/PawnRushCarriageYard/*.fbx`(33개, LFS). 붙이는 코드: `RoyalCarriageYard.Dressing.cs`(ZoneModel 표 · DressMovers),
  `RoyalCarriageYard.Castle.cs`(성 모델 · 상자 콜라이더 440개, `// <castle boxes>` 사이는 `castle_sync_cs`가 생성), `YardHorse.cs` · `YardHorseRig.cs`.
- 규칙(승규): 상자 콜라이더만, 폰이 닿는 면은 모델을 콜라이더에 맞춤(~10 cm 넘게 어긋나면 고치거나 보고), 게임 폭 · 틈 안 바꿈, 신호(물 · 폭포 · 거품 ·
  금 테두리 · 표지 · 등불 · 빨강/초록 표시 · 결승선 · 진행 막대)는 절대 안 가림, 성 키트 말고는 회색 클레이(텍스처 없음), 맵 밖은 모델링 안 함, 정지 화면으로 보고.
- Unity 확인은 이 폴더에 열린 에디터 `ChessFight-SeoungTest@5654cdc8`만 쓴다(main 에디터 C:/Work/ChessFight 금지). 장면 `Assets/Scenes/PawnRush_MapTest.unity`.

## 내보내기 순서 (새 Blender 네임스페이스로)

```
g = {"__name__": "rcy", "RCY_BUILD": False}
exec(rcy_kit.py, g); exec(export_rcy.py, g)        # + 필요한 앞 파일들 (각 .py 첫 줄 주석에 적힘)
g["RCY_BUILD"] = True; exec(<구역>_model.py, g)
export("RCY Entrance", "RcyEntrance")  # 또는 platform_export() / scenery_export() / castle_export(...)
```
- sorting = rcy_kit → entrance(False) → sorting. scenery = rcy_kit → export_rcy → entrance → sorting(False) → scenery.
- 내보내기 전에 모든 컬렉션이 보이게 할 것(숨기면 빈 FBX가 나갔음 → 이제 `export()`가 오류를 냄).
- Blender (x, y, z) = Unity (x, z, y). 3–4구역은 코드가 자기 좌표(LaterShift -24)로 짓지만 모델은 맵 좌표로 낸다.

## 이어서 할 일 (진행 중이던 것)

1. **스킬 기반 QA (승규 "텍스쳐는 입히지 말고 스킬 써서 기존에 있는 거 해봐", "블렌더 94개 스킬 극한으로 사용해").** `.claude/skills`의
   qa-review · blender-modeler 정리 목록 · asset-optimization · lod-pipeline · environment-artist · set-dressing · hard-surface 등을 차례로 적용.
   - 측정 결과(현재 FBX): Entrance 218k 삼각형(넓이 0 면 396), Canal 126k(8), Sorting 278k(1,327), Platform 51k, Finish court 28k, Section floors 15k,
     Scenery 26k / own 35k, 성 OuterGate 43k · FinalGate 38k · SectionEnd 55k(AI 키트 잘린 면 비다양체 모서리 1,626). 떨어진 점 · 선 없음.
   - **이미 한 것:** `rcy_kit.join()`에 `bmesh.ops.dissolve_degenerate(dist=1e-5)` 정리 단계 추가, 8개 고정물 FBX(Entrance · Canal · Sorting · Platform · FinishCourt · SectionFloors · Scenery · SceneryOwn)를 다시 내서 넓이 0 면 0개(Sorting 277.5k → 274.2k). Unity Play에서 오류 없이 불러옴(AI 확인만). 참고:
     전체를 한 번에 다시 지으면 몇 분 걸려 MCP 응답이 끊긴 것처럼 보이지만 빌드는 끝난다. 성 · 기계 · 말 · 마차 FBX는 아직 다시 안 냄.
   - 남은 판정 후보: Entrance · Sorting 삼각형 예산(바닥 밑 안 보이는 판석 밑면 지우기 검토), 구역별 3/4 각도 정지 화면, qa-review 판정표(SHIP/…).
   - 다시 낸 뒤: 바닥 윗면이 콜라이더 위 ~3 cm 안인지, 구멍 렌더(마젠타 배경), Unity Play에서 콘솔 오류 없는지.
2. 남은 보이는 것: 깃발은 코드의 파란 천(파란색 없애는 색 작업은 나중), 최종 성벽 가운데 기둥 음영 이음, 표지 글씨가 돌 위에서 어두움,
   "1. 짐마차 마당" 표지가 닫힌 문과 겹침, 최종 문짝 뒷면의 글씨 같은 무늬.
3. 승규가 Unity에서 보고 확인하면 VALIDATION 표에 올리고 main으로 옮길지 정한다.

## 날짜별 기록 (작업 세션 노트 그대로)

No files in the repo touched. Plan given to 승규 in the session reply (Korean). Key facts found:

- Attach pattern to mirror: RoyalWaterGardenBuilder.Concept.cs (ConceptPiece: model / covered / hung / removed;
  pivot rotated 180 about y; FBX from ArtSource/PawnRush/GardenConcept/export_concept.py into Assets/Resources/...,
  deterministic .meta GUIDs). Grand stair hall: HideListed + RideMover (Course01v4Builder.GrandStairHallModel/Balcony).
- RoyalCarriageYard.Awake builds everything under an inactive "Built" root, then SetActive(true): the dressing pass
  belongs right before that line (same as the garden's content-built-inactive rule).
- Zones 3-4 are built in their own frame ("3-4 (own frame)", LaterShift -24) and stretched by SortGrow 17: export zone
  FBXs in MAP frame and parent them to the Built root, not to `later`.
- Moving obstacles already keep visuals and "(collider)"/"(hit)" objects separate (SlewJib, ConveyorCrate, StackCrate,
  SlamChest, HorizontalWaterwheel spokes) -> hide renderers under the moving transform, hang a per-type prop FBX there.
- Animated sub-parts the model must split to: YardHorse Body / Neck / Leg pivots, RoamingCarriage wheels
  (YardKit.Carriage returns wheel transforms), TimedGate / PushGate leaves (hinges), VerticalWaterwheel paddles + gold
  grab handles (handles must sit on the grab colliders).
- Waterwheel: axle (0, 5.27, 66), radius 9, depth 1 m, hub radius 1.1 / length 1.4 (z 65.3-66.7); walkways |x| 3.0-6.2,
  gap z 64.3-67.7. A centre post IN the wheel plane would be hit by spokes; posts outside the faces at x=0, z~64.9/67.1
  (between the walkways, no collider) fit U2.
- Timber load collider 0.13-0.55 m above floor is the obstacle; U3's high-hanging timber would change gameplay.
- Signals drawn by code (red bands, green gap floors, red sectors, lamps, progress bar) must never be covered.

Update after the folder was cut to the carriage yard only (message from the starting session):
- Unity checks go to the editor open on THIS folder: UnityMCP instance ChessFight-SeoungTest＠5654cdc8 (port 6400),
  pinned with set_active_instance before any call; never the main editor at C:/Work/ChessFight.
- Collider/pivot guide: read straight from the running Play scene via UnityMCP (execute_code over the built
  RoyalCarriageYard colliders -> JSON in the scratchpad/ArtSource), so no editor dump script is needed.
- Only scene left: Assets/Scenes/PawnRush_MapTest.unity (calibration yard off). RoyalCarriageYard_Obstacles.prefab is
  gone, so no bench check; the section prefab (Early_1-2) builds the same component, so the dressing still applies there.

승규 11-15: nothing outside the map is modelled (stage 6 dropped). The code-built walls / rook towers / palace /
hills stay as they are (their blue cone roofs and banners included unless he asks for a recolour).

Decisions asked of 승규: open wagon vs closed coach; waterwheel centre post; timber high vs low; no-blue palette;
pilot = giant waterwheel + canal.

## Pilot done (2026-11-15, uncommitted, AI-checked in Play only - 승규 has not seen it)
- Files: ArtSource/PawnRush/CarriageYardModels/{rcy_kit.py, canal_model.py, export_rcy.py, CanalWheel_v1.blend,
  guides/rcy_built_play.json, stills/}; Assets/Resources/PawnRushCarriageYard/{RcyCanal, RcyGiantWaterwheel}.fbx (+meta,
  folder meta); Assets/Scripts/PawnRush/RoyalCarriageYard.Dressing.cs (new) + RoyalCarriageYard.cs (`models` toggle,
  Dress() in Awake's try, DressMovers() after SetActive(true)).
- Play: 85 canal blockout renderers + 209 wheel renderers stop drawing; colliders untouched. Water, flume water, pour,
  spill, foam, gold gap edges and signs stay blockout.
- Centre-post decision is moot: the blockout already has two solid axle posts (x 0, z 64.8 / 67.2); the model dresses them.
- Known look issue: Built/Scenery merlons (y 5.0-5.8) sit on the canal walls and stay blockout (outside-the-map rule),
  so beige merlons on grey clay until a colour pass.
- Next zones: 1 (sheds, outer gate, carriage + horses on YardHorse pivots), 3 sorting yard, 4 pond + vertical wheel
  (handles on grab colliders), 5 gate + section end. Docs (ROYAL_CARRIAGE_YARD §20, HANDOFF, REQUIREMENTS) and tests
  still to do at commit time.

## Zone 1 done (2026-11-15, uncommitted, AI-checked in Play only - 승규 has not seen it)
승규 11-15: "닫힌 마차. 낮게. 유지 해서 다음 구역 진행 해" = closed coach, timber low (zone 3), keep no-blue (colour pass later).
- New source: entrance_model.py (RCY Entrance, map frame), movers_model.py (coach, wheel, horse body/harness/neck/fore
  leg/hind leg, hitch, gate leaves W/E, each in its pivot's frame); shared helpers moved into rcy_kit.py;
  CarriageYard_v2.blend. Exports: RcyEntrance, RcyCoach, RcyCoachWheel, RcyHorse{Body,Harness,Neck,ForeLeg,HindLeg},
  RcyHitch, RcyGateLeaf{W,E}.
- Dressing.cs: zone "1 Entrance" keeps Shed lamp + Charge line*; parked carriage dressed in Dress(); DressMovers dresses
  every RoamingCarriage (coach on each Carriage, wheels on Wheel pivots, horse parts on Body/Neck/Leg pivots, hitch on
  teams) and the start gates' TimedGates. Lamps kept and scaled to the roaming 1.8 size.
- Play: zone 1+2 Dress hid 389 renderers, DressMovers 639; 110 model pivots (6 coaches, 24 wheels, 10 horses, 5 hitches,
  2 leaves, 1 wheel, 2 zones). Wheels roll and legs stride (checked rotations over time); 24 lamps on; charge lines on.
  Model triangles drawn ~643k total (entrance 281k, canal 126k, wheel 20k, teams ~37k each).
- Look note: Built/Scenery/Banner (blue, x ±10/±17 z 2.1, yard face of the gate wall) and Scenery merlons stay
  blockout on top of the clay model.
- Stills: stills/rcy_r678_entrance_gate(_blockout), yard_sheds, parked_coach(_front), team_close, gate_leaves.
- Peer (R677 ModelingRefs session) relayed 승규's 11-15 decisions (ANALYSIS.md §4): closed royal coach with shutters
  not glass; no waterwheel centre post (axle floating); timber load low (0.13-0.55 m). Coach glass replaced by louvred
  wooden shutters (movers_model.shutter), RcyCoach re-exported (11.3k tris); still rcy_r678_parked_coach_shutters.png.
  The pilot's wheel only dresses the code's own axle posts/bearings - no centre support post was modelled.

## Horses redone (2026-11-15, uncommitted, AI-checked in Play only - 승규 has not seen it)
승규's three asks, in order: a natural running horse like his cartoon picture; white/black/brown coats "very detailed";
then a picture of two chunky toy royal horses + "이런식으로 좀 투툼하게 만들어주고 말굽, 장식 등등 표현해줘". The style is
now chunky/stylized (not realistic), in colour (his ask; the rest of the yard stays HallClay).
- Source: ArtSource/.../horse_model.py (SDF-fused chunky body, big hooves, mane/forelock/tail locks, bridle with rosettes,
  cream plume, breast collar, girth; harness = pad/terrets/side straps/breeching/crupper; saddle = red cloth, gold rim,
  seat, stirrups). Rigged (Root/Neck/Head/Tail1-3 + 4x4 leg bones), weighted. One FBX: RcyHorse.fbx (Generic rig,
  animationType 2 - with None Unity drops the skin weights), 4 skinned meshes Horse 23k / Tack 7.1k / Harness 3.1k /
  Saddle 3.4k tris. The five pinned parts (RcyHorseBody/Neck/ForeLeg/HindLeg/Harness .fbx) and their builders in
  movers_model.py are deleted.
- Code: YardHorse.cs rewritten (same API; pivots now at the model's joints, 4 joints per leg, 3 tail; trot<->gallop
  gait by stride and ground covered, rear kept). New YardHorseRig.cs (LateUpdate, order 10000) turns each bone with its
  pivot. Dressing.DressHorse hangs RcyHorse on every YardHorse Body (coat read off the blockout barrel; Harness for
  team horses, Saddle when the blockout has a "Saddle cloth"), HorsePaint colours by slot.
- Hitch: RcyHitch traces now run from the swingle-tree to the horse's own side straps (no longer through the chunkier
  body), pole straps to the breast collar; DressHitch paints its leather slot like the tack. RoamingCarriage.LateUpdate:
  turning on the spot adds the arc (team 2.2 m, loose .8 m) to the stride so the pair walk round instead of sliding.
- Play (PawnRush_MapTest): 10 team horses dressed, 21 joints bound each, no console errors; 3 saddled test horses
  spawned by code (none in this map). Stills: rcy_r678_horse_team5, _grey_gallop, _black_stand, _bay_turn,
  _saddle_three. Not checked: StableCharger in its own map, skinned-mesh culling at screen edges.
- Note: with ObstacleClock unshared the fleet starts at 3 s; once the teams stood Waiting at clock 23 s until StartAt was
  called again (not investigated; did not recur after a restart).

## AI-3D castle kit for the castle pieces (relayed 2026-11-15 by the R677 ModelingRefs session; inspected, not used yet)
승규: "마차뜰 블록아웃 채팅에서 그 구간 모델링 작업을 할 때 이거 쓰라고 하면 돼"; then "일단 에셋을 늘려서 써도 되고
이상하면 추후에 바꿈" (stretching allowed for now). For: outer gatehouse, rook towers, curtain walls, final gatehouse,
section end (z 157-181: castle_archway_stairs). Files: ArtSource/.../CarriageYardModels/source_ai3d/ (untracked, 65 MB).
Inspected in Blender (temporary scene, removed afterwards). Stills: stills/rcy_ai3d_castle_kit_overview.png, _towers.png.
- Every file: one mesh, ~1 m on its longest side (normalised), origin at the bounding-box centre (not the base), front
  -Y, up +Z, scale 1. 30k tris each (archway_stairs 50k). One material, packed 4096^2 base colour + 2048^2 normal
  (archway_stairs: base colour is a blank 1x1 white = untextured, normal 2K).
- stone_castle_gate: two round towers, a low crenellated wall with a small door and a shield (1.0 x .37 x .65).
- beige_castle_gate: a tall square arch between two towers, a crest on top (1.0 x .36 x .86).
- beige_castle_wall: a KIT of 2 pieces in one mesh - a tall square section and a long low crenellated wall.
- stone_castle_tower: a KIT of 3 rook towers in one mesh (medium, slim, wide); bases lumpy and gold-studded.
- castle_archway_stairs: open arch on two rook piers, low steps, balustrades (.86 x 1.0 x .75), white.
- All read beige/cream with gold rings - the chunky toy look of the horses, not the yard's grey clay. Open question
  for 승규 at that zone: keep their textures, or clay like the rest (the yard's rule so far).
- Before use: split the kits into pieces, put the pivot at the base, scale to the blockout, decimate (30k per copy adds
  up fast on repeated wall runs), bake/export the texture separately (2K) instead of the packed 4K.
- Collision rule 승규 approved ("전해", relayed): box colliders only, never the AI meshes; fit the model to the
  collider where pawns touch (gate passages, inner wall faces, stair treads, balustrade inner faces, walkways),
  stretching allowed; where it cannot fit, nudge/add boxes (round towers = several boxes, stair heights = the model's
  steps) but never change gameplay widths/gaps; unreachable parts may differ freely; after each zone overlay the
  colliders semi-transparently and fix every pawn-touchable face off by more than ~10 cm (invisible walls or
  clipping), and list those spots in the zone's report.

## Castle done (2026-11-15, uncommitted, AI-checked in Play only - 승규 has not seen it)
승규: "색이 없는 에셋은 에셋들과 색을 맞춘 다음 적용 시켜" then "진행" -> castle pieces keep the AI kit textures.
- Source: castle_model.py (box_face / wing_boxes / straighten_jamb / castle_sync_cs); exports RcyCastleOuterGate,
  RcyCastleFinalGate, RcyCastleSectionEnd (.fbx) + 10 compressed PNGs; guides/rcy_castle_boxes.json (148 boxes).
- Code: RoyalCarriageYard.Castle.cs (new; CastleModels, DressCastle, CastleBoxData generated between
  `// <castle boxes>` markers), Dressing.Dress() calls DressCastle(). Blockout gate walls/towers no longer collide.
- 승규 11-15 "이 작업은 다음에 하고 다음 작업 이어 가" -> DEFERRED castle fixes (do later):
  * outer tower gold studs stand 20-33 cm proud (pawns overlap them)
  * outer passage niche corners 10-15 cm off; final passage front/back corner chamfers up to ~25 cm off
  * head-height stone set back 15-30 cm (feet/body height fit)
  * blue banner at outer x ±17 hidden by the thickened pier; ±10/±12 hidden by towers
  * outer gate title text overlaps the crest shield; reads mirrored from the yard side
  * asset stairs unused; final gatehouse top squashed; section end (SectionEnd) not measured

## Zone 3 statics done (2026-11-15, uncommitted, AI-checked in Play only - 승규 has not seen it)
- Source: sorting_model.py (needs rcy_kit.py + entrance_model.py with RCY_BUILD=False first); export RcySorting.fbx
  (277k tris, map frame). Dressing.cs: ZoneModel "3-4 (own frame)/3 Loading and sorting yard" keeps water/falls/foam,
  hatch + sluice tunnels, Lamp glow, gold edges (basin, landing, loft edge/lip); new Moving(r) also leaves the
  machines' MonoBehaviour parts drawing (CargoConveyor, StackRow, SlewCrane, PalletHoist, CargoSlam, not Obstacles).
- Play: model hung, 639 blockout renderers off, machines + signals still drawn, no console errors.
- Ray check vs colliders: lamp posts, walls, wall-end piers, jib posts, timber stores within 10 cm; stall fronts get a
  1.75 m plank counter on the goods' face (x +-23.5), 4/408 rays off (2 cm slits between boards); loft/landing slits
  2 cm. Hatch posts, lamp bases, jib foot, wall-end piers pulled in from the blockout's 15-30 cm overhangs.
- Not done: zone 3 machines (conveyor + loads, storage rows + cranes, timber cranes, hoists, heavy berth gantry +
  chest, lifting waterwheel) still blockout; zone 4 statics (platform, cliff, railings, runout) still blockout.
- Stills: rcy_r678_sorting_mouth, _stalls, _stalls_counter, _loft_store, _west_side, _basin_dock.

## Zone 3 machines done (2026-11-16, uncommitted, AI-checked in Play only - 승규 has not seen it)
- Source: sorting_machines_model.py (exec rcy_kit, entrance, sorting_model with RCY_BUILD=False, then movers_model with
  RCY_BUILD_MOVERS=False first). 15 FBXs, each in its machine's own frame (all roots identity): RcyConveyor,
  RcyConveyorCrate/Stack, RcyStackRow, RcyStackCrate, RcyRowTrolley, RcySlamGantry, RcySlamChest, RcySlamTrolley,
  RcySlewCrane, RcySlewJib, RcyHoistFrame, RcyHoistPalletP/N, RcyLiftWheel (~64k tris all told).
- Dressing.cs: DressMovers() -> DressSortingMachines(): Unpaint each machine root keeping gold trims (Conveyor trim,
  Beam trim, Beam band, Jib trim, Arm trim), lamps (Trolley/Jib/Hoist lamp), stretched ropes/chains (Row rope, Hoist
  rope, Slam chain), warnings (Gap mark, Slew reach/sector, Slam circle/rim/wave/dust, Well board/edge), wheel Handle
  + Handle post. New RideUnscaled() puts the two trolleys' models under a holder undoing the box's scale.
  Pallet model chosen by the "Pallet sack" child's x sign. Conveyor slats are static pictures (not moving).
- Ray check: belt +-8 cm (rails as blockout), loads/stack crates/row stock within 10 cm, crane drums 3 cm, paddles 5 cm,
  wheel rims/spokes exact on their local faces; timber ridge beam lengthened to the collider's 8 m (blockout left
  15 cm air), flanks <= 9 cm perpendicular, 1 cm slit between the lower timbers. Remaining >10 cm hits are other
  parts in front (crown on chest, sacks on pallets, winch on gantry posts, caps on hub) as in the blockout.
- Stills: rcy_r678_machines_conveyor, _crane, _hoist, _berth, _liftwheel.
- Not done: zone 4 statics; deferred castle fixes; docs (HANDOFF, REQUIREMENTS, ROYAL_CARRIAGE_YARD.md); commit.

## Deferred castle fixes done (R679, 2026-11-16, uncommitted, AI-checked in Play only - 승규 has not seen it)
- 승규 chose "2" = "미뤄 둔 성문 문제 먼저 고치기". Source: castle_model.py (exec rcy_kit with RCY_BUILD=False, then
  castle_model with RCY_BUILD=True, then export_rcy; castle_export(models=(...), textures=False) also writes
  CastleBoxData into RoyalCarriageYard.Castle.cs, now 440 boxes).
- Towers: studs off the stone moved onto it, band boxes per tier; gate-wall piers on plinth boxes (outer 2.57,
  final 155.01); wall seams at the piers closed (close_wall_ends); final far face boxed in two parts (bends at x 6);
  final gatehouse top 25.62; banners moved between piers (outer +-13.2/+-19.5, final +-14.7/+-22).
- Labels: CastleLabels() after the map is switched on puts every castle group's TextMesh on the depth-tested
  ChessFight/KingRushCourseText material (push gate label included): no text through stone or mirrored from behind.
- Section end: the asset's opening sits .135 off its middle -> GATEWAY at x -.133, scale x 30.32 (piers' inner faces
  6.00 .. 6.14 both sides; were +6.23..6.53 / -5.96..6.30). part() got cuts=(plane, normal) bisects with welded,
  capped rims: pier courses .12 high edge to edge (were torn with sky through them), gateway pier bottoms, balustrade
  foot (.04) and far end (.68) cut straight; rail x scale kept, height 1.15. Asset step laid on the 4 code steps:
  treads within 1 cm of 7.75/7.5/7.25/7.0.
- Left (reported, not fixed): tier-top set-backs 10-15 cm, bevelled corners (pier corners up to ~30 cm at the very
  edge), unreachable faces; sign text dark on stone; "1. 짐마차 마당" sign overlaps closed doors; text-like texture
  marks on the back of the final door leaves.
- Stills (scratchpad/stills): r679_outer_court, _outer_yard, _outer_pier_fixed, _final_platform, _final_back,
  _final_wall_banners, _section_steps_fixed, _section_pier_capped, _band_rail_foot_fixed, _runout_rail.
- Not done: zone 4 statics; docs (HANDOFF, REQUIREMENTS, ROYAL_CARRIAGE_YARD.md); commit.

## R680 (2026-11-16, 승규 asleep: "구역4 고정물", "전체 모델링 디테일하게 세밀하게", "에셋 사이사이 깨진 부분 매워") - not committed, nothing user-verified

- Zone 4 statics: new ArtSource/PawnRush/CarriageYardModels/platform_model.py -> RcyPlatform (cliff face over the basin:
  rusticated base, pilasters, panels, dentil frieze, cornice, coping; platform paving, kerb, two edge balustrades),
  RcyFinishCourt (map only: paving, three walls, pedestal + velvet cushion), RcySectionFloors (section only: runout,
  terrace, band paving, side walls under the castle railings). Dressing.cs ZoneModel got mapOnly/sectionOnly; FinalKeep
  keeps Finish line / Crown / Crown point. Rays: cliff 0-5 cm, railings <=2 cm, court walls <=3.5 cm, pedestal <=7.4 cm.
- Castle kit cracks: the AI meshes are split along UV seams; decimating unwelded meshes opened hairlines. castle_model.py
  part() now remove_doubles(1e-5) first; magenta-background render of the castle models: ~3000 enclosed pixels -> 1.
  Tower fit re-measured with straight rays from outside: model within 5 cm of the boxes (the per-face sampler's 0.5 m
  flags on the octagon boxes were the sampler hitting neighbouring pieces, not the model).
- Scenery: new scenery_model.py -> RcyScenery (group "Scenery": merlons with plinth, arrow slit on the inner face and cap
  on the yard/canal walls x 22..23 top 5, z 2.5..80.5; four canal-landing lamp posts) and RcySceneryOwn (own-frame
  scenery: merlons x 26..27 top 11, map z 82.5..156.5; platform lamp posts at (+-8, 144.5) on top 8). sorting_model.py
  lamp_post got z0. Dressing.cs ZoneModel.only = SceneryCovered {Merlon, Lamp post, Lamp cap} (declared BEFORE the
  ZoneModels table: static init order - declared after, it was null and the whole group went dark).
- Fresh full chain re-export of platform_model (section floors 15312 tris now). CarriageYard_v2.blend saved (copy).
- Left: banners stay the code's blue cloth (no-blue colour pass later), the mid-pier shading seam on the final wall,
  PushGate leaves are the code's wood (look fine). Docs (HANDOFF, REQUIREMENTS, ROYAL_CARRIAGE_YARD.md) at commit time.

### R680 continued (승규 "진행")
- Gate wall pier seam (outer x +-16, final x +-18): castle_model.py mirror_part() + straighten_end_normals(); each side's
  outer segment is the wall part mirrored (wall_for(s, inner)), so a joint is one end against its mirror. Pier boxes
  re-measured in Blender: outer (14.68, 17.32), (21.70, 23.0); final (16.48, 19.52), (24.54, 26.0). Unity rays: final
  piers within 5 cm (one 4 cm sliver at the end pier's plinth edge 24.26..24.30), outer within 7.6 cm below 6 m.
- An empty RcyCastleOuterGate.fbx (4 KB) went out once because its collection was hidden in the view layer for a
  Blender look; export_rcy.export() now raises when a mesh is not selectable. Re-exported, 1.34 MB.
- scenery_model.py corbel_table(): zone 3 side walls z 82..143, 10.15..10.75, out <= .16 (gantry tops 9.9, banner
  rods 9.6); none over the platform. RcySceneryOwn 35168 tris.
- Hole render after: 0 on the piers; 73 px in the outer front view are far sky through the passage, 2 px final back.
- Stills: Temp/RcyStills/seam_final_pier_after, seam_outer_pier_after, corbels_sorting_wall.
