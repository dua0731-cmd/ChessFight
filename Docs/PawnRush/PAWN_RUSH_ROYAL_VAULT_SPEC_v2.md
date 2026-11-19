# 왕실 금고실 (Royal Vault) — build spec v2, grey-box · `Late_3-3_RoyalVault`

> **As built (R725, 2026-11-18 to 11-20).** This is the spec the vault was built from, kept as it was written except for this note and the section id. The request number became **R725** (R692–R724 went to other work first): read "R692" below as R725. The section id is **`Late_3-3_RoyalVault`** (3-1 is the Royal Library and 3-2 the Royal Observatory; renamed when this work was put on top of main on 11-20). The vault sits in the late-obstacle slot's `drafts` (R712): the course prefab test lists it as entry 7 and Validate Sections checks it, but no round draws it, so the protocol stays as main has it (v59 on 11-20). The pool step's "v51" means one step above main's protocol at that time (v60 on 11-20). The map is on `main` as a grey-box and nothing in it is verified until the user plays it. The Korean overview, the bot measurements and the tuning after them are in [PAWN_RUSH_ROYAL_VAULT.md](PAWN_RUSH_ROYAL_VAULT.md) (§5 changes from plan A, §7 fun validation, §12 what was and was not checked).
>
> **Where the build differs from this spec** (each one is commented in the code where it happens):
> 1. **Hurdles (§2.3).** The hit is judged by the bar's own box (`YardImpact.Body.Box`, a trigger box the bar's size as `solid`), not as a drum round the shell: the shell's round stands 0.3 m over the bar, so a jump that cleared the bar was still hit. A knocked-down pawn is let through the round log (`alsoSolid`): the log otherwise carried floored pawns round to the rails at the tips' 7.6 m/s. Beside the hurdles, wherever the pit lies behind the rail, the corridor rail is 2.4 m tall (a pawn hit mid-jump went over the 1.2 m rail into the pit, a fall on the safe branch).
> 2. **Hammers (§2.5).** `ghostSeconds` = 2·HammerBodyHalfAngle / HammerDegreesPerSecond + 0.1 = 2.04 s: a floored pawn slides along under the head for up to about 1.5 s, and when 1.0 s ran out inside the head it was thrown out at 17 m/s.
> 3. **Press (§2.1, §2.6).** One crush shell for the whole die (lane across the neck, `alongShare` 0, pushing out from the die's middle line toward the side the hips are on) instead of two halves: a pawn straddling z 110 was hit by both halves in one physics step. The wave skips pawns the crush shell hit on that landing and judges the band's ends once. `PressSafeToCross` models the body stepping in with its front on the band's edge and checks the die against the shell's reach (runner window [0.30, 1.88) s, sprinter [0.37, 2.65) s); `PressHeadroom` = `HammerBodyTop` + `PressCrushBelow`.
> 4. **`BeltRideSeconds` (§2.0)** stops its step loop at `length - 1e-9`: exact ties were rounded differently at clock 0 and at a Unix-scale clock (Core test 13).
> 5. **Bot harness (§5.2).** The hurdle jump is a flight check against the moving body, and watchers take the line where each bar comes at them head-on (the spec's rule looked only at the bar reaching the hips' current point and ignored the bot's own motion: 75 % of corridor crossings were knocked down, careful bots 8 of 12). Falls are counted where the hips first dropped 1.5 m below their last standing height. A bot thrown out of the corridor walks back in through the opening. Jumpers wait for a free tower top only before their run-up. Plate row switches go straight across. On the plates only height (5 m) and speed across (10 m/s) count as a fling.
> 6. **Tuning after the fun measurements (R725, 11-18 to 11-20; bot proxies, [vault doc §7](PAWN_RUSH_ROYAL_VAULT.md)).** *Hurdles (§2.3):* the bar top is 0.5 m (`HurdleBarHigh`, was 0.55) with a gold strip along it (`Cue hurdle bar k`). A knockdown pushes back over the bar (`downBack`, `downPush` 1.5 m/s). A body a hurdle knocked down is not hit by it again for half a turn plus `HurdleFreePass` 0.6 s (`HurdleHitCooldown`, 2.24 / 2.4 s), and is let through for 1.5 half turns (`HurdleLetThroughSeconds`, 2.45 / 2.7 s). A trigger box over the hurdle (`RV hurdle k zone`, in `alsoSolid`) keeps that let-through going, so the other arm passes a floored or rising pawn. A stagger waits only 1 s (`staggerCooldown`, a new opt-in YardImpact field). Blind runners now take 2 knockdowns at hurdle 1 and 1 at hurdle 2 (was 3 at hurdle 1, mostly (24 of 32) 1.5-1.9 s apart), at least 0.94 s on their feet before a repeat (round 3; 1.08 s in round 2). *Drawers (§2.6):* they knock down at 3 m/s or more for `DrawerKnockdown` 1.8 s (was a shove only, knockdownSpeed 99), with no `solid`. A face slower than that does not hit (`minSpeed` = `knockdownSpeed`). `DrawerShellBeyond` 0.3 m is the hit's reach ahead of the face: `DrawerClearFor`, `ShelfPassClear` and the fork 2 model use `DrawerReachX`. `ForkTwoDropCost` includes the knockdown. A swept run now ends slower than a clean belt run at a run (+2.15 s in round 2 and +1.18 s in round 3, few swept runs each; was -1.37 s). *Signs:* the fork 1 sign moved under the title to (0, 4.0, 6.0), and "← 허들 회랑: 길고 안전" stands at (-5, 2.8, 7.0). *Harness (§5.2):* a fling at the J2/plates edge is judged on the plates' terms; a drop counts once per crossing; careful bots wait 0.35 m back from each drawer; the hurdle lengths derive from `HurdleBarHigh`; new report lines for hurdle chains and drawer carries. *Not changed:* the door countdown stays 3, 2, 1 (design V10), and neither do the plates, the hammers or the length (no metres added).
> 7. **§6 "Round length" is out of date:** by the bots a vault round is about as long as a war-room round (leaders 64–71 s, median 82 s in round 3; people took the war room in 117–125 s and ran the carriage yard about 1.1× the bots), not about 100 s longer.

v2 is v1 with the three reviews applied (movement-fun, code-contract, concept-scope). Every disputed fact was checked in the code at R691; the citations are below. Every number marked "computed" comes from a Python port of §2.0 (scratch `rvfinal/rules_final.py`, `rvfinal/model2.py`, `rvfinal/pad.py`). None of it has been measured in Unity.

Inputs: the research files `rv_read/{sections,devices,plan-a,art,tests}.json`, the user's `PAWN_RUSH_ROYAL_VAULT_DESIGN_v0.1.md`, the plan A drawing, and the code at R691. Citations use `file:line`. The plan-a findings are tagged **G#n**: the n-th entry of `plan-a.json → contradictions_and_gaps`, numbered 1–33 in file order.

## Conventions used throughout

- **Section frame.** x across (east +), y up, +z the way on; metres. Entry (0,0,0) is the middle of the 12 m threshold, on the floor. Exit **(0, 12, 196)**, exitYaw 0.
- **Surface kinds.**
  - **F** walkable top: CourseFloor plus NoClimbSurface on the same GameObject. Section rule: *nothing in the vault is climbable*. NoClimbSurface still allows standing on a top (NoClimbSurface.cs:6-11), and step-up and ledge assist ignore it (devices.json pitfalls).
  - **N** solid with NoClimbSurface (wall, rail, block, moving part).
  - **P** picture only, no collider.
  - **T** trigger.
  - **M** moving part; the driving component is named.
- **NoClimbSurface is `[DisallowMultipleComponent]`** (NoClimbSurface.cs:16), and CourseBuilder Box/Wall/Rail/Cylinder/Slope with noClimb already add it (CourseBuilder.cs:215). The builder's `FloorNC` helper adds it only `if (!go.TryGetComponent<NoClimbSurface>(out _))`, and never on anything that already has it.
- **Names.**
  - Every vault solid and picture starts with **`RV `**. The prefix is unused elsewhere; "Vault …" is taken by Course01v4Builder.LibraryDressing.cs:88 and MiniGameModels.cs:638-646.
  - Every gameplay cue starts with **`Cue `**.
  - Signs are YardKit.Label objects named `Label <text>` (YardKit.cs:262-279).
  - Checkpoints keep CourseBuilder's `CP<n>` (CourseBuilder.cs:185).
- **Floors** are 1 m slabs whose top is the listed y (`CourseBuilder.Floor`, CourseBuilder.cs:59-64).
- **Rails** are 0.3 m thick and 1.2 m tall above their floor, and NoClimb (`CourseBuilder.Rail`, :125-127). Rails over the pit reach 1 m below their floor.
- **Pit.** `RoyalVaultRules.PitBottom = −40`. Every wall, column, cabinet, support, pier and stair-step picture that goes down into the pit starts at y −40. There is no pit floor (§1.1).
- **D2 material rule.**
  - Every model uses exactly one material: `RoyalVaultLook.Greybox = Arena.Lit("M_VaultGreybox", #BDB9B1, 0, .9)` (Arena.cs:73-85). The name is in neither KitLook nor KitSurface, so it gets no kit texture.
  - Cue objects use only three named materials:
    - `CueRed = Arena.Lit("M_VaultCueRed", #FF4A3A, 1.6, .35)`;
    - `CueGreen = Arena.Lit("M_VaultCueGreen", #59FF73, 1.2, .35)`;
    - `CueGold = Arena.Lit("M_VaultCueGold", #E2B23C, .6, .45)`.
  - Ink #292433 is used for sign text, countdown digits and the belt slats (`Ink = Arena.Lit("M_VaultInk", #292433, 0, .9)`, `InkColor`).
  - No amber, no dim red, no textures, no other colours, no decorative props.
- **Cue vocabulary** (one for every cue, `RoyalVaultRules.Cue`):
  - **Off**: hidden.
  - **Go**: green, "go now".
  - **Warn**: red blinking at 4 Hz, "about to move".
  - **Stop**: red, "moving or occupied".
  - Static gold marks an edge to step or jump from or to.

### Top view (z grows up the page)

```
z 196  exit (0,12,196) ── 4 m strip ── open arch "7" at z 192 (12 × 6 m) ── treasure room x ±6, y 12 (CP6 z 182.5)
z 178-180 door wall x ±14: octagonal doorway (inradius 3.9), disc r 4.5 rolls 9 m east into the wall; countdowns
z 168-178 terrace x ±14, y 12 (CP5)
z 156-168 stair 8→12, x ±4
z 136-156 flipping plates: W row x −4..0 | E row x 0..4, y 8, hinges on the outer edges
z 126-136 J2 x ±14, y 8 (CP4)
z 114-126 stair 4→8, x −6..2                 | shelf N x 3..7, y 8 (railed)
z 107-114 press neck x −6..2 (die at z 110)  | cabinet E top y 8 = shelf (railed)
z 103-107 landing x −6..4 (press queue)      | shelf, railed both sides from z 103.3
z  89-103 belts W x −6..−1, E x −1..4        | shelf, open W edge z 96.3-103.3 beside drawers z 98.0 / 101.8
                                             | landing zone z 91-96.3, railed both sides (pad lands z 91.9-94.7)
z  83-89  approach x ±14, y 4 (CP3 straddles z 83); launch pad x 5.2..6.8, z 83.8..85.4
z  60-83  hammer walkway x ±4, y 4, no rails; axles z 65.5 and 77.5
z  50-60  J1 x −22..8, y 4 (CP2)
z  18-50  towers Ø3.6: lanes x ±2.2, rest platform z 32.2-35.8 y 2 (CP1) | corridor x −22..−14: hurdles z 14/24.5,
                                                                       bulkheads z 31.4/35.4, stair z 38-50
z 0.6-18  forecourt x ±14, y 0 (CP0, with a reach box at the entry)
z 0       entry, 12 m
```

---

## 0. Decisions and deviations

### 0.1 The lead's decisions, as applied

**D1 – placement.**
- The vault is the second candidate for slot index 4 (slot 5, late obstacle section), beside the war room. The lead fixed these names:
  - prefab `Assets/Prefabs/PawnRushSections/Late_3-3_RoyalVault.prefab`;
  - root component `RoyalVaultSection`, whose Awake builds `Content` (pattern WarRoomSection.cs:17-33);
  - builder `RoyalVaultBuilder`.
- `PawnRushSection.Describe` is given exactly the values in the prefab YAML (PITFALLS #68; YAML in §3.3).
- Entry (0,0,0), 12 m wide. Exit (0,12,196) at the end of a level floor 12 m wide.
- The section ends with the R690 art rule (level runout, open arch, rank 7), not the mini-game boundary.
- Before the pool step, the user plays it in the Editor through the map-test twin (§4.1). The pool step is gated (§4.2).

**D2 – grey-box.**
- One material for every model.
- Landmark shapes in that material carry the concept:
  - tower cylinders;
  - rest platform;
  - dials;
  - hammer heads;
  - the screw press with its crown, screw and head beam;
  - the bottomless pit;
  - shelf and drawer cabinet;
  - the round door with its spoke;
  - three merlons on each bulkhead panel (they read as rooks, for castling);
  - the arch with "7".
- Cues are separate `Cue …` objects, all built in one partial (`RoyalVaultBuilder.Cues.cs`). No gameplay component builds or references a cue, and `VaultCue` reads only `RoyalVaultRules`.
- Signs are Korean TextMesh labels.

**D3 – plan A's structure is kept.**
- Order: forecourt → fork 1 (towers / hurdle corridor with bulkheads) → J1 → dial hammers → fork 2 (belts + press / launch pad + shelf with drawers, the shelf overlapping the belts) → J2 → flipping plates → stair → terrace → time-lock door → treasure room → exit.
- The five must-have obstacles V01, V04, V05, V09 and V10 are kept.
- Every contradiction found is fixed in §0.2.

**D4 – online.**
- Every motion is a pure function of `ObstacleClock`. The timing and every bot look-ahead query live in Core `RoyalVaultRules`, with Core tests.
- Knocks go through YardImpact: one hit, one knockdown, ChainGrace 1.5 s (YardKit.cs:477-497, YardImpact.cs:326-341). The press wave mirrors SlamChest.Shock (SlamChest.cs:278-346).
- Plate pulls, LaunchPad throws and all hits act only where `ObstacleClock.Authority` holds or on the locally simulated pawn (YardKit.cs:438-447, LaunchPad.cs:28-30).
- No ISectionNetPart. The only protocol change is v51, at the pool step.

**D5 – fun validation.**
- Built beat by beat in milestones (§5.1).
- Each milestone ends with a real-physics bot probe in `C:\유니티\ChessFight_TestCopy` (§5.2–5.3).
- The fork-2 balance is also pinned by a Core time model (Core test 10).

**D6 – concept.** The vault is the last obstacle before the audience hall (R690 "three areas, two gates"). The rules read like chess through shapes and sign words, never through colour:

| Obstacle | Chess reading | How it reads |
|---|---|---|
| Towers | pawns advance one rank; a blocked pawn waits | words |
| Bulkheads | O-O / O-O-O, castling | labels + merlons |
| Hammers | two chess clocks; your turn comes right after theirs | dial shape + words |
| Press | a checked rank | words |
| Pad | the knight's leap | words |
| Drawers | open files | words |
| Plates | change file | words |
| Door | rank 7; promotion lies beyond | sign + "7" on the arch |

The "bishop on same-colour squares" reading needs colour and waits for the art pass.

### 0.2 Deviations from plan A (each with its reason and the finding it fixes)

**1. Place and premise.**
- Plan A: slot 5 is an empty temp floor in a 7-slot round, followed by a mini-game.
- This spec: the vault is the second slot-5 candidate, and the ending follows it.
- Why: D1. Fixes G#1, G#3.

**2. End of the section.**
- Plan A: exit (0, 11, 198) after a 1 m terrace and 4 steps down.
- This spec: a level exit at (0, 12, 196). Treasure-room runout (12 m, CP6) → open arch 12 × 6 m with "7" → 4 m level strip.
- Why: an ending needs only a level 12 m floor (sections.json "Late section end"), and R690 keeps the arch and rank 7. Fixes G#3.

**3. Coin towers (V01).**
- Plan A: 7 towers 3 m wide, zigzag ±1 m, +0.55 m per tower, "gap 1.07".
- This spec:
  - two straight lanes at x ±2.2, towers **3.6 m** wide;
  - gap **0.85 m**, +0.5 m per row;
  - row 4 is one shared rest platform, 8.0 × 3.6 m at y 2, holding CP1;
  - the two flat take-off edges (forecourt z 18, rest z 35.8) are open only for **|x ∓ 2.2| ≤ 1.0**, with rail stubs elsewhere.
- Why:
  - the zigzag made 1.54/1.83 m gaps (G#11);
  - plan A's heights were roundings (G#12);
  - one file is a single-file chain for 12 players (G#28);
  - a 0.55 m lip made the R661 bots fall back (CargoConveyor.cs:8-11);
  - movement-fun found round tops widen the gap off the lane line: 1.43 m at 0.8 m off with 3.4 m towers. With 3.6 m towers it is 1.23 m. The flat edges give at most 1.15 m inside the openings, against a running reach of 1.44 m (1.35 m with the lab's 6 % shorter air time, RAGDOLL.md:426-427).

**4. V01 hard mode** (towers bobbing ±0.3 m). Not built; kept as a knob, because it creates momentary 0.85 m steps (G#28).

**5. Safe corridor.**
- Plan A: hurdles r 3.8 at z 22/30, 90°/s; bulkheads 1.8 m apart; an 8 m stair.
- This spec:
  - hurdles r 3.95 at z 14 / 24.5, +110 / −100 °/s;
  - bulkheads at z 31.4 / 35.4, two pocket panels each;
  - a 12 m stair (18.4°).
- Why:
  - the 1 s spacing rule (G#27);
  - the span check to ±0.05 m (Course01Validator.cs:28);
  - R680 rates (G#10);
  - no pinching against rails (P43);
  - plan A's stair was 26.6°, over the 19.5° rule.

**6. J1.**
- Plan A: 34 × 10 m with a purple crown circle.
- This spec: 30 × 10 m at z 50-60, no circle.
- Why: grey-box; a purple circle meant "warned spot" (G#21).

**7. Dial hammers (V04).**
- Plan A: axle 4.6 m, arm 4.2 m, head 1.4 × 2.2 × 7.6 m, 36°/s.
- This spec:
  - axle 4.4 m above the walkway;
  - head reaching radially 2.9-4.3 m, so it passes 0.1 m over the floor;
  - head 2.2 m along the way, 8.4 m across;
  - 60°/s, the pair half a turn (3 s) apart;
  - push capped at **3.8 m/s**.
- Why:
  - the plan's head sat 0.3 m in the floor (G#18);
  - a 7.6 m head fails the span check;
  - 36°/s is slower than R680 (G#10);
  - movement-fun: a 5 m/s cap slid knights (weight 0.8) back into the other hammer's zone after ChainGrace had run out, so the cap is now 3.8 (≈ 2.6 m pawn slide, ≈ 4.1 m knight, inside the 5 m rest cell).

**8. Crown press (V05).**
- Plan A: a 3 × 3 × 2 chest over an 8 m circle in a 10 m neck, 6 s cycle, a stagger ring, placed before the belts.
- This spec:
  - a full-width die 8.0 × 2.4 × 1.6 m in an 8 m neck;
  - **4.5 s** cycle (sit 0.4, rise 1.0, up 1.65, warning 1.2, fall 0.25);
  - a full-width band (die ± 1.8 m, 6 m long);
  - no stagger ring;
  - placed **after** the belts.
- Why:
  - side-dodge strips (G#14, P25/R653);
  - a radial push threw pawns forward (G#30);
  - a shelf drop after the press would skip it (R653);
  - movement-fun: 5 s was slower than the R680 sluice and robot (4.5 s, PAWN_RUSH_SECTIONS.md:16-17), and late should be harder.

**9. Jewel belts (V06).**
- Plan A: west −1.5 m/s; east ±2.5 m/s every 5 s; a 2 m strip between; 18 m long.
- This spec:
  - west **−1.0** m/s constant;
  - east alternates **stopped (0.0) / back (−1.5)** in 5 s halves with 0.5 s ramps;
  - two 5 m belts side by side, no strip;
  - 14 m long.
- Why:
  - R518: no ride-on floor pushes back faster than 1.5 m/s (G#26); the strip was a free walkway (G#15).
  - Movement-fun and concept-scope both showed that v1's forward east belt (+1.6) made the safe branch *faster* than the shelf, inverting P43.
  - With these speeds the Core time model gives the risky branch +1.9 s at 2.5 m/s and +0.6 s at 3.1 m/s (§1.18).
  - The belt choice remains: at a sprint the west belt is the faster ride for 32 % of entry times.

**10. Launch pad (V07).**
- Plan A: a pad at (9.5, 87), up 12 / forward 4.5 m/s, onto a 3 m shelf starting at z 96.
- This spec:
  - a **1.6 m** LaunchPad trigger centred (6.0, 4.3, 84.6), `sameThrow`;
  - landing offset (−1.0, 4.0, 8.7), so the landing is (5, 8, 93.3);
  - clearance **1.9**;
  - the shelf is 4 m wide (x 3..7) from z 91, with **both edges railed over the landing zone z 91-96.3**.
- Why:
  - the plan's pad could not reach the shelf (G#16), and launched pawns fly at 1 g.
  - LaunchPad throws on the first overlap of any collider and offsets each landing by the hips' ground point (LaunchPad.cs:101-108, 159-202; RagdollDriver.cs:60). The real spread is therefore the trigger plus a catch margin (0.6 m allowed), and v1's 2.8 m pad landed pawns at the open edge (movement-fun).
  - v1's pad also clipped the shelf's east rail (code-contract; the scan confirms −0.4 m).
  - With these numbers every catch up to 0.6 m outside the trigger lands at x 3.6..6.4, z 91.9..94.7. Every flight clears the lip by ≥ 0.66 m and the east rail by ≥ 0.56 m (computed).

**11. Drawers (V08).**
- Plan A: 6 drawers 2.6 m long pushing 2 m into a 3 m shelf; a 1.5 s wave; no cycle.
- This spec:
  - **2 drawers** at z 98.0 / 101.8, each 2.4 m long, sweeping the full 4 m width;
  - cycle **4.8 s** (rest 2.4, out 1.0, hold 0.4, in 1.0);
  - drawer 2 runs **1.5 s after** drawer 1 (the design's wave along the wall);
  - the shelf's west edge is open only for **z 96.3-103.3**, over the belts.
- Why:
  - side dodge (P25); a queen is swept by the face itself (G#8); the cycle was missing (G#29).
  - Movement-fun: v1's third drawer dropped pawns onto the press queue, where pawn-on-pawn landings stay raw (11.2 m/s) and floor both pawns (RagdollPawn.PawnRushPhysics.cs:154-159).
  - Concept-scope: v1's unexplained alternation (0 / 2.2 / 0) made the shelf slower. The wave is restored as a deliberate exception to P27 on this optional branch.

**12. Flipping plates (V09).**
- Plan A: 70°, 2 s up in 6 s; a checkerboard in the drawing, opposite rows in the text; hinge on the −z edge.
- This spec:
  - rows in opposite phase;
  - hinges on the outer edges, so the inner edge lifts;
  - 50°;
  - 8 s per row (flat 5.2, raise 1.0, hold 0.8, lower 1.0);
  - warning **1.6 s**.
- Why:
  - G#17;
  - the plan's timing threw riders at 14.7 m/s (devices.json V09);
  - movement-fun: a 1.0 s warning plus the 0.32 s before slipping did not cover crossing a 4 m row (1.85 s). 1.6 s gives 1.92 s.

**13. Time-lock door (V10).**
- Plan A: a 9 m disc and a 7 m opening (inconsistent); 15 s period, 6 s open.
- This spec:
  - an octagonal doorway (inradius 3.9);
  - a disc r 4.5 rolling 9 m east;
  - 15 s period with 6.0 s passable;
  - 3·2·1 countdown at the top **and two eye-level copies**;
  - the doorway-clearing shove hangs on a **separate translate-only mover**.
- Why:
  - G#22; the regroup intent is kept (G#31).
  - Concept-scope: the top countdown is off screen everywhere on the terrace at the default camera (OrbitCamera.cs:19-36, FOV 60).
  - Movement-fun: a shove shell rolling with the disc misses about 1/5 of roll angles (YardImpact.cs:368-382).
  - `Body.Ball` on that rotating box would hit pawns up to 6.65 m out (its corners), so v2 uses a translate-only shell.

**14. Checkpoints.**
- Plan A: 6, CP5 inside the room.
- This spec: 7 checkpoints: forecourt (with a reach box at the entry), tower rest, J1, the walkway's end, J2, terrace, and 2.5 m past the door.
- Why: cheap failure; war-room precedent (G#20). Code-contract: CP3 must come before the pad. The CP0 reach box covers pawns who turn to the corridor before z 8.

**15. Look.**
- Plan A: scale and chests, crown carpets, gold/teal/green.
- This spec: none of it, plus:
  - **no pit floor** (everything runs down to y −40 and out of sight);
  - a shadowless **roof**.
- Why:
  - D2, G#5.
  - Concept-scope: a sunlit single-colour pit floor reads as more floor.
  - Stage 3 is an interior; the sky belongs to stage 1 (PAWN_RUSH_ART_PHASES.md:56, 75-76).

**16. Neck width.**
- Plan A: 8 m in the text, 10 m in the drawing.
- This spec: neck 8 m (x −6..2), belt hall 10 m (x −6..4).
- Why: the shelf must overlap the east belt but never the neck.

**17. Stairs.** All three are 12 m long and 4 m high (slope rule).

**18. Kill volume.**
- Plan A: "y −4".
- This spec: a pit volume x −28..16, y −8..−4, z 0.6..178, plus a far-below volume.
- Why: G#33.

**19. Hall shell.**
- Plan A: not specified.
- This spec: walls y −40..30 around 44 × 180 m, roof at 30, room roof at 19.5.
- Why: G#33.

**20. Route length.**
- Plan A: "205 / 250 m".
- Computed here: ProgressPath 200.9 m; all-risky ≈ 206 m; all-safe ≈ 224 m.
- Why: G#13.

**21. CameraSeeThrough.** Only on moving parts. The shelf overlap is cut to 1 m (x 3..4).

**22. Sounds** (tick, bell, thud). Deferred to the art pass; grey-box cues are visual.

**23. Cues.**
- Plan A: amber, purple and gold lamps.
- This spec: one Go/Warn/Stop vocabulary in red and green only, gold for step edges, ink for digits.
- Why: D2 lists exactly warning red, go green, gold edges and the countdown (concept-scope).

Kept on purpose:
- Fork 1 stays deliberately unequal (P43 close-hard / far-easy), not "same risk" (G#24).
- The third climb has one route (G#23).
- The 4-minute-course extras stay out (G#25).
- V11–V16 are not built.

---

## 1. Layout (section frame)

### 1.1 Shell, roof, pit and kill volumes

| Name | x | y | z | Kind |
|---|---|---|---|---|
| RV wall S-W | −28..−6 | −40..30 | 0..0.6 | N |
| RV wall S-E | 6..16 | −40..30 | 0..0.6 | N |
| RV entrance lintel | −6..6 | 6.5..30 | 0..0.6 | N (closes the wall over the 12 m opening; the Mid corridor's lid is at 6.5, SectionCorridor.cs:38-41) |
| RV wall W | −28.6..−28 | −40..30 | 0..180 | N |
| RV wall E | 16..16.6 | −40..30 | 0..180 | N |
| RV wall N-W | −28..−14 | −40..30 | 178..180 | N |
| RV wall N-E | 14..16 | −40..30 | 178..180 | N |
| RV roof | −28.6..16.6 | 30..30.5 | 0..180 | P, `MeshRenderer.shadowCastingMode = Off` |
| RV room roof | −6.6..6.6 | 19.5..20 | 180..196 | P, shadows off |
| RV kill volume (pit) | −28..16 | −8..−4 | 0.6..178 | T KillVolume, respawn 1.5 s (KillVolume.cs:17-19) |
| RV kill volume (far below) | −40..30 | −42..−40 | −10..206 | T KillVolume |
| RV pier k (2 × 2 m, from −40 to the slab bottom) | under the forecourt (±7, 9), corridor (−18, 19), tower rest (0, 34), J1 (−15, 55) and (0, 55), walkway (0, 71.5), approach (±9, 86), belts (−3.5, 96) and (1.5, 96), landing (−1, 105), drawer cabinet (9.1, 99.9), shelf N (5, 120), J2 (±9, 131), terrace (±9, 173) | | | P |

There is no pit floor: depth reads from walls, columns and piers that run out of sight. The roof casts no shadow, so the scene's sun (PawnRush_Round.unity:283) still lights every model; the roof only hides the sky. The follow camera never reaches it (max 14 m, OrbitCamera.cs:21).

From the forecourt the camera sees:
- the tiers rising north;
- the hammer heads swinging at y 4-12.7;
- the 30 m door wall at z 178 as the backdrop.

### 1.2 Beat 0 – entrance and forecourt (y 0)

| Name | x | y | z | Kind |
|---|---|---|---|---|
| RV entrance sill | −6..6 | −1..0 | 0..0.6 | F |
| RV forecourt | −14..14 | −1..0 | 0.6..18 | F |
| RV rail forecourt E | 14..14.3 | −1..1.2 | 0.6..18 | N |
| RV rail forecourt N-W | −13.7..−3.2 | −1..1.2 | 17.7..18 | N |
| RV rail forecourt N-mid | −1.2..1.2 | −1..1.2 | 17.7..18 | N |
| RV rail forecourt N-E | 3.2..14.3 | −1..1.2 | 17.7..18 | N |
| Cue forecourt lip ×2 | −3.2..−1.2 and 1.2..3.2 | 0..0.02 | 17.85..18 | P CueGold (the take-off openings) |
| Start | empty GameObject at (0, 0, 3), yaw 0 | | | read only by the map test (PawnRushMapTest.cs:22, 203) |

- The forecourt's west edge (x −14) is open for z 0.6..8: the corridor entrance. For z 8..18 the corridor's east rail stands there.
- The section test drops the pawn at (0, 0, 4), forecourt floor (SectionPlaytest.cs:179-181).

### 1.3 Beat 1, risky – coin towers (y 0 → 4)

Lane centres are at x = ±2.2. Each row k = 1..7 except 4 has one tower per lane:
- named `RV tower W<k>` / `RV tower E<k>`;
- built as `CourseBuilder.Cylinder(name, (±2.2, PitBottom, TowerZ(k)), 3.6, TowerTop(k) − PitBottom, Greybox, solid: true, noClimb: true)` plus CourseFloor;
- kind F top, N side.

| Row k | Centre z (`TowerZ`) | Top y (`TowerTop`) |
|---|---|---|
| 1 | 20.65 | 0.5 |
| 2 | 25.10 | 1.0 |
| 3 | 29.55 | 1.5 |
| 4 | **RV tower rest**: slab x −4.0..4.0, y 1..2, z 32.2..35.8 (F), CP1 | 2.0 |
| 5 | 38.45 | 2.5 |
| 6 | 42.90 | 3.0 |
| 7 | 47.35 | 3.5 |

| Name | x | y | z | Kind |
|---|---|---|---|---|
| RV rail rest W / E | −4.3..−4.0 / 4.0..4.3 | 1..3.2 | 32.2..35.8 | N |
| RV rail rest N stubs | −4.0..−3.2, −1.2..1.2, 3.2..4.0 | 1..3.2 | 35.5..35.8 | N (take-off openings x −3.2..−1.2 and 1.2..3.2) |
| Cue rest lip N | the two openings | 2..2.02 | 35.65..35.8 | P CueGold |
| Cue rest lip S | −4.0..4.0 | 2..2.02 | 32.2..32.35 | P CueGold (landing edge, fully open) |
| Cue tower rim W/E k | `YardKit.Ring(axis up, radius 1.72, band .15, depth .02, 24 segments, CueGold, Solid.None)` at top + 0.005 (YardKit.cs:221-236) | | | P |
| Cue J1 lip | −4.0..−0.4 and 0.4..4.0 | 4..4.02 | 50..50.15 | P CueGold |

Gaps (computed):
- **On the lane lines** every gap is 0.85 m: forecourt edge 18 → tower 1 at 18.85, …, tower 7's north edge 49.15 → J1 at 50.
- **Lane hop**: the lanes are 0.8 m apart edge to edge at the same height. The diagonal is 2.66 m, so it is no shortcut.
- **Off the lane line, tower → tower**: 0.99 m at 0.5 m off, 1.23 m at 0.8 m off.
- **Flat take-off edge → tower**: ≤ 1.15 m inside the 2.0 m openings.
- **Reach**: a hop onto +0.5 m is airborne 0.574 s, so it carries 1.44 m at a run (1.35 m with the lab's air time), 2.07 m sprinting and 1.15 m as a king (PawnRushPhysicsParams.cs:20-22).
- **Landing edges get no stubs.** The rest's south edge and J1's south edge only receive jumps, and a stub would only block an off-line landing.

### 1.4 Beat 1, safe – gold-bar hurdle corridor (y 0 → 4)

| Name | x | y | z | Kind |
|---|---|---|---|---|
| RV corridor floor | −22.3..−14 | −1..0 | 0.6..38 | F (8 m between rail faces −22 and −14) |
| RV rail corridor W (segments) | −22.3..−22 | −1..1.2 | 0.6..30.8, 32.0..34.8, 36.0..38 | N |
| RV rail corridor E (segments) | −14..−13.7 | −1..1.2 | 8..30.8, 32.0..34.8, 36.0..38 | N |
| RV hurdle 1 / 2 | Spinner roots (−18, 0, 14.0) / (−18, 0, 24.5) | | | M (§2.3) |
| RV bulkhead pocket W1 / E1 | −26.3..−22 / −14..−9.7 | −1..3.2 | 30.8..32.0 | N |
| RV bulkhead pocket W2 / E2 | −26.3..−22 / −14..−9.7 | −1..3.2 | 34.8..36.0 | N |
| RV bulkhead panel W1, E1, W2, E2 | VaultMover roots (−24, 1.52, 31.4), (−12, 1.52, 31.4), (−24, 1.52, 35.4), (−12, 1.52, 35.4) | | | M (§2.4) |
| Cue bulkhead b W / E (floor strips) | −21.9..−18.1 / −17.9..−14.1 | 0.005..0.025 | (B−1.6)..(B−0.6), B = 31.4 / 35.4 | P cue |
| Label "O-O-O" / "O-O" | (−20, 3.4, B−0.5) / (−16, 3.4, B−0.5), size .8 | | | queenside west, kingside east |
| RV stair 1 | x −22..−14, z 38 (y 0) → 50 (y 4) | | | §1.12 |
| RV rail stair 1 W / E | RailAlongZ x −22.3..−22 / −14..−13.7 | | | N |

Spacing along the way:

| From | To | Clear |
|---|---|---|
| corridor entrance (z ≤ 8) | hurdle 1 zone (10.05..17.95) | 2.05 m |
| hurdle 1 zone | hurdle 2 zone (20.55..28.45) | 2.6 m |
| hurdle 2 zone | bulkhead 1 panels (31.0..31.8) | 2.55 m |
| bulkhead 1 | bulkhead 2 panels (35.0..35.8) | 3.2 m cell |
| bulkhead 2 | stair (38..50) | 2.2 m |

### 1.5 J1 (y 4)

| Name | x | y | z | Kind |
|---|---|---|---|---|
| RV J1 | −22..8 | 3..4 | 50..60 | F (30 × 10 m) |
| RV rail J1 S-mid | −13.7..−4.0 | 3..5.2 | 49.7..50 | N |
| RV rail J1 S-stub | −0.4..0.4 | 3..5.2 | 49.7..50 | N (tower landings x −4.0..−0.4 and 0.4..4.0 open; stair 1 arrives at x −22..−14) |
| RV rail J1 S-E | 4.0..8 | 3..5.2 | 49.7..50 | N |
| RV rail J1 W | −22.3..−22 | 3..5.2 | 50..60.3 | N |
| RV rail J1 E | 8..8.3 | 3..5.2 | 49.7..60.3 | N |
| RV rail J1 N-W / N-E | −22..−4 / 4..8 | 3..5.2 | 60..60.3 | N |

### 1.6 Beat 2 – dial-hammer walkway (y 4, no rails)

| Name | Where | Kind |
|---|---|---|
| RV hammer walkway | x −4..4, y 3..4, z 60..83 | F |
| RV hammer 1 / 2 (Spinner root) | (0, 8.4, 65.5) / (0, 8.4, 77.5) | M (§2.5) |
| RV dial 1 / 2 | picture disc, YardKit.Cylinder axis x, Ø 8.0, 1.0 thick, centre (−5.9, 8.4, 65.5 / 77.5) | P ("chess clock" landmark) |
| RV dial pedestal 1 / 2 | x −6.4..−5.4, y −40..4.4, z axle ± 0.5 | P |
| Cue hammer lamp 1 / 2 | 0.4 m box at (−4.6, 6.0, 61.6 / 73.6) | P cue |

Hammer zones (computed: head below 1.2 m over the walkway, plus 0.4 m body margin):
- hammer 1: z 62.02..68.98;
- hammer 2: z 74.02..80.98;
- rest cell: 68.98..74.02 (5.05 × 8 m);
- wait line: z ≤ 61.5;
- runout: 81..83.

### 1.7 Beat 3 – fork 2 (approach y 4; safe 4 → 8; risky at 8)

**Approach**

| Name | x | y | z | Kind |
|---|---|---|---|---|
| RV approach | −14..14 | 3..4 | 83..89 | F (28 × 6 m) |
| RV rail approach S-W / S-E | −14..−4 / 4..14 | 3..5.2 | 82.7..83 | N |
| RV rail approach W / E | −14.3..−14 / 14..14.3 | 3..5.2 | 83..89 | N |
| RV rail approach N-W / N-E | −14..−6.3 / 4.3..14 | 3..5.2 | 89..89.3 | N |
| RV pad | 5.2..6.8 | 4.0..4.04 | 83.8..85.4 | P (1.2 m clear of the walkway line x 4) |
| RV pad launch | BoxCollider trigger 1.6 × 0.6 × 1.6 centred (6.0, 4.3, 84.6) + LaunchPad | | | T (§2.8) |
| Cue pad rim | 4 strips 0.12 m wide round the pad's edge, y 4.04..4.07 | | | P CueGold |
| RV pad flight | IntendedGap at (5.5, 7.5, 89.0), yaw −6.56°, `Configure((4.4, 9, 11), "발사판 비행")` (IntendedGap.cs:15) | | | — |

**Safe route: belts → landing → press neck → stair**

| Name | x | y | z | Kind |
|---|---|---|---|---|
| RV belt W | −6..−1 | 3..4 | 89..103 | F + VaultBeltSurface(0) |
| RV belt E | −1..4 | 3..4 | 89..103 | F + VaultBeltSurface(1) |
| RV rail belt W | −6.3..−6 | 3..5.2 | 89..107 | N |
| RV rail belt E | 4..4.3 | 3..5.2 | 89..91 | N |
| RV shelf support | 4..7 | −40..7.4 | 91..107 | N (the east belt's and landing's east wall; carries the shelf) |
| RV landing | −6..4 | 3..4 | 103..107 | F (press queue) |
| RV cabinet W | −14..−6 | −40..12 | 107..114 | N |
| RV cabinet E | 2..14 | −40..8 | 107..114 | F top, N sides (its top between the shelf rails is the risky path) |
| RV neck | −6..2 | 3..4 | 107..114 | F (8 m) |
| RV press die (VaultMover) | root (−2, 8.6, 110) = the die's centre when up | | | M (§2.6) |
| RV press head | −7..3 | 15..16.4 | 109..111 | P (static beam) |
| RV press frame W / E | −6.8..−6.2 (y 12..15) / 2.0..2.6 (y 8..15) | | 109.6..110.4 | P |
| RV press wave | static GameObject at (−2, 4, 110) + VaultPressWave | | | — |
| Cue press band | −6..2 | 4.005..4.025 | 107..113 | P cue |
| RV stair 2 | x −6..2, z 114 (y 4) → 126 (y 8) | | | §1.12 |
| RV rail stair 2 W / E | RailAlongZ x −6.3..−6 / 2..2.3 | | | N |
| Cue belt W / Cue belt E | strips x −6..−1 / −1..4, y 4.005..4.025, z 89.1..89.5 | | | P cue |
| Cue belt slat W/E k (k 0..6) | 4.6 × 0.02 × 0.2 m bars at y 4.005, pitch 2 m, moved by VaultSlats | | | P Ink |

**Risky route: launch pad → shelf with drawers**

| Name | x | y | z | Kind |
|---|---|---|---|---|
| RV shelf S | 3..7 | 7.4..8.0 | 91..107 | F (landing zone z 91..96.3) |
| (cabinet E top) | 2..14 | top 8 | 107..114 | F (the shelf runs on it here) |
| RV shelf N | 3..7 | 7.4..8.0 | 114..126 | F |
| RV rail shelf W | 2.7..3.0 | 7.4..9.2 | 91..96.3 and 103.3..126 | N (open only beside the drawers) |
| RV rail shelf E | 7..7.3 | 7.4..9.2 | 91..96.3 and 103.5..126 | N |
| RV drawer cabinet | 7..11.2 | 7.4..11 | 96.3..103.5 | N |
| RV drawer 1 / 2 (VaultMover) | roots (9.0, 8.95, 98.0 / 101.8) | | | M (§2.9) |
| Cue shelf lip | 3..7 | 8.0..8.02 | 91..91.15 | P CueGold |

What this layout guarantees (computed):
- **Overlap.** The shelf overlaps the east belt only over x 3..4 (1 m).
- **Pad landings.** Every landing, for hips caught up to 0.6 m outside the trigger, is at x 3.6..6.4, z 91.9..94.7, between the rails. It is ≥ 1.8 m before drawer 1's hit range (z 96.5).
- **Drawer drops.** A pawn pushed off the open edge (z 96.3..103.3) at ~5 m/s falls 4 m in 0.71 s and lands at x ≈ −1..2:
  - on a belt, or on the landing's first 0.3 m;
  - always before the press band (z 107), so there is no bypass (R653);
  - never onto the press queue, which waits at z ≈ 105..106.7.
- **Drop landing speed.** The drop lands at 11.2 m/s. A floor landing is divided by 1.27, giving 8.85 m/s, under the 9 m/s knockdown (RagdollPawn.PawnRushPhysics.cs:154-159; RagdollTuning.asset:25). A drop from a jump, or onto another pawn, floors the pawn.

### 1.8 J2 (y 8)

| Name | x | y | z | Kind |
|---|---|---|---|---|
| RV J2 | −14..14 | 7..8 | 126..136 | F |
| RV rail J2 S-W / S-mid / S-E | −14..−6.3 / 2.3..2.7 / 7.3..14 | 7..9.2 | 125.7..126 | N |
| RV rail J2 W / E | −14.3..−14 / 14..14.3 | 7..9.2 | 126..136 | N |
| RV rail J2 N-W / N-E | −14..−4 / 4..14 | 7..9.2 | 136..136.3 | N |

### 1.9 Beat 4 – flipping plates (y 8, over the pit)

| Name | Where | Kind |
|---|---|---|
| RV plate W1..W5 (VaultPlate) | hinge-line roots at (−4.0, 8.0, 136.025 + 4(i−1)), yaw 0. Deck in local x 0..3.97 (inward) and z 0..3.95 | M; deck F + N |
| RV plate E1..E5 (VaultPlate) | roots at (4.0, 8.0, 139.975 + 4(i−1)), yaw 180 (local +x points west/inward, local +z points −z) | M; deck F + N |
| RV plate hinge W / E | picture cylinders 0.3 m Ø along z, x ∓4.05, y 7.85, z 136..156 | P |

- Seams are 0.05 m along z and 0.06 m at x 0.
- The plates of one row share one phase, so they never pinch each other.

### 1.10 Beat 5 – stair and terrace (y 8 → 12)

| Name | x | y | z | Kind |
|---|---|---|---|---|
| RV stair 3 | −4..4, z 156 (y 8) → 168 (y 12) | | | §1.12 |
| RV rail stair 3 W / E | RailAlongZ x −4.3..−4 / 4..4.3 | | | N |
| RV terrace | −14..14 | 11..12 | 168..178 | F (28 × 10 m regroup) |
| RV rail terrace S-W / S-E | −14..−4.3 / 4.3..14 | 11..13.2 | 167.7..168 | N |
| RV rail terrace W / E | −14.3..−14 / 14..14.3 | 11..13.2 | 168..178 | N |

### 1.11 Beat 6 – door, treasure room, arch, exit (y 12)

All door-wall parts span z 178..180 and are N unless noted.

| Name | x | y | z | Kind |
|---|---|---|---|---|
| RV door wall W | −14..−3.9 | −40..30 | 178..180 | N |
| RV door wall E skin S | 3.9..14 | −40..30 | 178..178.4 | N |
| RV door wall E skin N | 3.9..14 | −40..30 | 179.6..180 | N |
| RV door wall E foot / cap | 3.9..14, y −40..10.4 / 19.6..30 | | 178.4..179.6 | N (the disc's slot lies between them) |
| RV door wall top | −3.9..3.9 | 18.9..30 | 178..180 | N |
| RV doorway corner UL / UR / LL / LR | 3 × 3 × 2 m boxes turned 45° about z, centred (∓3.818, 18.818) and (∓3.818, 11.182) | | 178..180 | N: makes the octagon (inradius 3.9, centred y 15) |
| RV door sill | −3.9..3.9 | 11..12 | 178..180 | F |
| RV door disc (VaultMover) | root (0, 15, 179) = the disc's centre when shut | | | M (§2.10) |
| RV door shove (VaultMover, translate only) | root (0, 15, 179); holds only the trigger shell | | | M (§2.10) |
| Cue door countdown | TextMesh at (0, 20.2, 177.9), size 3 | | | cue (landmark) |
| Cue door countdown W / E | TextMesh at (−6.0, 14.8, 177.9) / (6.0, 14.8, 177.9), size 2.5 | | | cue (eye level from the terrace) |
| RV room floor | −6..6 | 11..12 | 180..196 | F (room 180..192 + strip 192..196) |
| RV room wall W / E | −6.6..−6 / 6..6.6 | 11..19.5 | 180..196 | N (flush with the Late corridor, |x| 6..6.6, top +7.5) |
| RV arch pier W / E | −7.2..−6 / 6..7.2 | 12..19.5 | 191.4..192.6 | N |
| RV arch lintel | −7.2..7.2 | 18..19.5 | 191.4..192.6 | N (open arch 12 m wide, 6 m tall, no door) |
| Label "7" | (0, 18.75, 191.35), size 2.5 | | | rank 7; beyond it the audience hall is rank 8, promotion |

- Doorway clear width (computed): 5.03 m at the floor, 7.03 m at 1 m up; top at y 18.9.
- When the disc rolls into the east wall it passes behind the UR/LR corner boxes' front faces, so it reads as rolling behind the frame.
- No obstacle stands in the last 12 m before the arch, and the room holds no props.

### 1.12 Stair recipe (all three stairs)

Each stair rises 4 m over 12 m along z from (z_A, y_A), 8 m wide: 16 risers of 0.25 m on 0.75 m treads, 18.43°, under the 19.5° rule (CourseBuilder.cs:74-76).

1. Copy `FlightZ`, `Walk`, `RailAlongZ`, `V` and `Picture` from GrandStairHallBuilder.cs:72-128 (they are private static) into RoyalVaultBuilder.cs, with one change: `FlightZ` takes the step pictures' bottom as a parameter (`bottom = RoyalVaultRules.PitBottom`), not the hall's `Deep = −5` (GrandStairHallBuilder.cs:38).
2. Call `FlightZ(b, name, x0, x1, zFoot: z_A + 0.375, steps: 16, depth: .75f, yFoot: y_A, bottom: PitBottom)`. This makes:
   - the step pictures (step 16's top equals the upper floor and overlaps it by 0.375 m in the same material, so no seam shows);
   - one sloped CourseFloor from (z_A, y_A) to (z_A + 12, y_A + 4), with its renderer removed (`Walk`).
3. `b.Slope(…, noClimb: true)` already adds NoClimbSurface (CourseBuilder.cs:215). **Add nothing more.**
4. Rails come from `RailAlongZ` (6 m below the walking line, 1.2 m above).

### 1.13 Checkpoints

`CourseBuilder.Checkpoint(number, ground point, yaw 0, width)` (CourseBuilder.cs:183-196) makes a trigger width × 3 × 2 m and six spots at (i − 2.5)·min(2, (w−1)/5) (CourseCheckpoint.cs:36-41). In the round they become 400–406. `RoyalVaultRules.CheckpointZ = {9, 34, 55, 83.6, 131, 172, 182.5}`.

| CP | Ground point | Width | Spots | Why |
|---|---|---|---|---|
| CP0 | (0, 0, 9) | 26 | x −5..5 | Forecourt. Plus a **reach box** on the same object, x −14..14, y 0.3..3, z 0.6..2.6 (pattern `Reach`, RoyalCarriageYard.Sections.cs:176-186). A pawn turning to the corridor before z 8 also passes CP0; the respawn stays on the first box (Checkpoint.cs:20-28). |
| CP1 | (0, 2, 34.0) | 8.0 | x ±0.7 / ±2.1 / ±3.5 | tower rest |
| CP2 | (−7, 4, 55) | 30 | x −12..−2 | J1; trigger x −22..8 |
| CP3 | (−3, 4, 83.6) | 22 | x −8..2 | Straddles the walkway's end: trigger x −14..8, z 82.6..84.6. Every pawn leaving the walkway (x −4..4 at z 83) crosses it before the pad (z ≥ 83.8). |
| CP4 | (0, 8, 131) | 28 | x −5..5 | J2 |
| CP5 | (0, 12, 172) | 28 | x −5..5 | terrace |
| CP6 | (0, 12, 182.5) | 12 | x −5..5 | 2.5 m past the door (war-room precedent) |

Where a fall goes:

| Fall from | Back to |
|---|---|
| towers 1–3 | CP0 |
| towers 5–7, rest | CP1 |
| hammer walkway | CP2 |
| fork 2 | no pit falls (landing zone and shelf N railed; the drawer edge drops onto the belts) |
| plates | CP4 |

### 1.14 ProgressPath

One path, a child of Content. Black = white: pass `route.Clone()` as black (ProgressPath.cs:23-37). The points are `RoyalVaultRules.RouteX/Y/Z`.

| Point | (x, y, z) | Leg label |
|---|---|---|
| P0 | (0, 0, 0) | 0 앞뜰 |
| P1 | (0, 0, 9) | 0 앞뜰 |
| P2 | (2.2, 0, 17.6) | 1 금화 탑 |
| P3 | (2.2, 0.5, 20.65) | 1 금화 탑 |
| P4 | (2.2, 1.0, 25.10) | 1 금화 탑 |
| P5 | (2.2, 1.5, 29.55) | 1 금화 탑 |
| P6 | (2.2, 2.0, 34.00) | 1 금화 탑 |
| P7 | (2.2, 2.5, 38.45) | 1 금화 탑 |
| P8 | (2.2, 3.0, 42.90) | 1 금화 탑 |
| P9 | (2.2, 3.5, 47.35) | 1 금화 탑 |
| P10 | (2.2, 4, 50.6) | J1 합류 |
| P11 | (0, 4, 55) | J1 합류 |
| P12 | (0, 4, 60) | 2 다이얼 망치 |
| P13 | (0, 4, 83) | 3 둘째 갈림 |
| P14 | (0, 4, 86) | 3 벨트 · 왕관 프레스 |
| P15 | (1.5, 4, 89) | 3 벨트 · 왕관 프레스 |
| P16 | (1.5, 4, 103) | 3 벨트 · 왕관 프레스 |
| P17 | (−2, 4, 105) | 3 벨트 · 왕관 프레스 |
| P18 | (−2, 4, 114) | 3 벨트 · 왕관 프레스 |
| P19 | (−2, 8, 126) | J2 합류 |
| P20 | (−2, 8, 131) | J2 합류 |
| P21 | (−2, 8, 136) | 4 뒤집히는 판 |
| P22 | (−2, 8, 156) | 5 테라스 |
| P23 | (0, 12, 168) | 5 테라스 |
| P24 | (0, 12, 172) | 6 시간 자물쇠 문 |
| P25 | (0, 12, 179) | 6 시간 자물쇠 문 |
| P26 | (0, 12, 182.5) | 6 시간 자물쇠 문 |
| P27 | (0, 12, 196) | 6 시간 자물쇠 문 |

- **Length 200.9 m** (computed), so the standings weight is sqrt(200.9/60) = 1.83 (PawnRushStandings.cs:71-77).
- The widest gap is 0.85 m (towers). Stair legs lie on their slopes.
- Edit-mode poses: plates flat (their decks exist from `VaultPlate.Configure`, §2.1), die up, door shut.
- The gap check's rays start 1 m above the path. At P25 that origin is inside the disc, and a ray starting inside a convex collider does not hit it, so the sill is found below.

### 1.15 ObstacleSpans and IntendedGap

`ObstacleSpan.Configure(across (1,0,0), min, max, Floor, probeY, …)` (ObstacleSpan.cs:33). Spans sit on the mover root when the mover's Edit-mode rest pose stands on the measured line. The validator then skips the mover's colliders (Course01Validator.cs:173-202). A static `RV span …` object is allowed only for a mover that rests off that line.

| Obstacle | Span position | Reach | probeY |
|---|---|---|---|
| Hurdle 1 / 2 | Spinner root (−18, 0, 14 / 24.5) | −3.95..3.95 | 0 |
| Bulkhead 1 / 2 | static `RV span bulkhead k` (−18, 0, 31.4 / 35.4); panels rest in their pockets | −4..4 | 0 |
| Hammer 1 / 2 | Spinner root (axle) | −4.2..4.2 | 4 |
| Press | static `RV span press` (−2, 4, 110); die rests up | −4..4 | 4 |
| Drawer 1 / 2 | static `RV span drawer k` (5, 8, z_k); drawers rest in the cabinet | −2..2 | 8 |
| Door | on the **`RV door disc` VaultMover root** (0, 15, 179). The disc rests shut across the doorway and would read as a wall to a static span (Course01Validator.cs:195). | −4.5..4.5 | 12 |

- The door span measures the sill over x −2.8..2.8, where the lower corners rise above y 12.3, and covers it.
- Towers and plates need no span: they are the floor.
- **IntendedGap:** only the pad flight. No required plain jump exceeds 1.5 m.

### 1.16 SectionBeats

`Describe(..., Beats())`. **Order matters**: the first match wins (PawnRushSection.cs:149-155).

| # | Name | x | y | z |
|---|---|---|---|---|
| 1 | 3 서랍 선반 | 2.5..11.5 | 7..12 | 89..126 |
| 2 | 1 금화 탑 | −4.5..4.5 | −9..6 | 18..50 |
| 3 | 1 허들 회랑 | −27..−13.5 | −9..10 | 0.6..50 |
| 4 | 0 앞뜰 | −15..15 | −9..9 | −0.5..18 |
| 5 | J1 합류 | −23..9 | −9..12 | 50..60 |
| 6 | 2 다이얼 망치 | −8..8 | −9..16 | 60..83 |
| 7 | 3 둘째 갈림 | −15..15 | −9..14 | 83..89 |
| 8 | 3 벨트 · 왕관 프레스 | −15..15 | −9..18 | 89..126 |
| 9 | J2 합류 | −15..15 | −9..16 | 126..136 |
| 10 | 4 뒤집히는 판 | −8..8 | −9..16 | 136..156 |
| 11 | 5 테라스 | −15..15 | −9..20 | 156..178 |
| 12 | 6 시간 자물쇠 문 | −15..15 | −9..24 | 178..196.5 |

### 1.17 Signs

All signs use `RoyalVaultLook.Sign` = `YardKit.Label(…, InkColor)`, yaw 0, so they read to players coming from −z (RoyalCarriageYard.cs:336-339). They follow the carriage yard's "N. 장소: 할 일" style and stand outside the walkable width or above head height, with no collider. Chess words never lean on a colour; cue states are named by blinking, or by green only where the sign explains a cue (as the yard does).

| Where | Text | Size |
|---|---|---|
| (0, 5.5, 6.0) | 왕실 금고실 | 2.2 |
| (0, 4.2, 17.5) | 1. 첫 갈림 — 금화 탑: 짧고 위험 · 허들 회랑: 길고 안전 | 1.0 |
| (0, 3.4, 17.5) | 탑은 폰처럼 한 칸씩 앞으로 · 앞 탑에 누가 있으면 기다리기 | 0.8 |
| (−18, 3.2, 8.5) | 금괴 허들: 막대가 오면 점프 · 격벽(캐슬링): 열린 쪽으로 | 0.9 |
| (0, 7.6, 59.6) | 2. 다이얼 망치(체스 시계): 망치가 지나간 직후가 내 차례 · 사이 칸에서 쉬기 | 1.0 |
| (0, 7.8, 88.5) | 3. 둘째 갈림 — 벨트 + 왕관 프레스(안전) · 나이트 발사판 + 서랍 선반(위험) | 1.0 |
| (0, 7.15, 88.5) | 보석 벨트: 초록 불 벨트는 멈춤 · 뒤로 가는 벨트는 템포를 잃는다 | 0.7 |
| (6.0, 6.4, 83.3) | 발사판(나이트 도약) → 서랍 선반 | 0.8 |
| (−2, 6.4, 106.6) | 왕관 프레스: 아래 줄은 체크 — 초록일 때 건너기 · 깜빡이면 줄 밖에서 기다리기 | 0.8 |
| (5, 10.8, 95.9) | 서랍이 들어간 열린 줄에서 기다리기 · 깜빡이는 서랍은 곧 나옴 | 0.7 (in front of the cabinet face at z 96.3) |
| (0, 11.6, 135.6) | 4. 뒤집히는 판: 깜빡이는 줄이 일어선다 · 누운 줄로 옮겨 타기 | 1.0 |
| (0, 14.6, 167.6) | 6. 시간 자물쇠 문(7랭크): 3 · 2 · 1 뒤 6초 동안 열림 | 1.2 (top of stair 3, as the yard's gate sign) |
| arch lintel | 7 | 2.5 |

Optional landmark (shape, no words): three Greybox merlons 0.6 × 0.5 × 0.8 m on each bulkhead panel's top edge (§2.4).

### 1.18 Lengths and times (computed, not measured)

**Lengths**
- ProgressPath (risky fork 1, safe fork 2): **200.9 m**.
- All risky: ≈ 206 m. All safe: ≈ 224 m.
- Fork 1, entry → (0, 4, 60): towers ≈ 60 m with **8 jumps** (forecourt→1, 1→2, 2→3, 3→rest, rest→5, 5→6, 6→7, 7→J1); corridor ≈ 83 m with hurdles, bulkheads and a stair. The towers save ≈ 9–12 s at a run; a tower fall costs ≈ 6–10 s.

**Fork 2 Core time model** (`ForkTwo…`, §2.0): from (0, 4, 83) to (0, 8, 129), ideal play, averaged over arrival times.

| Speed | Safe | Risky | Gain | Push-off at drawer 1, versus waiting |
|---|---|---|---|---|
| 2.5 m/s | 22.6 s | 20.6 s | +1.9 s | +1.4 s |
| 3.1 m/s | 17.8 s | 17.2 s | +0.6 s | +0.9 s |
| 3.6 m/s | 15.0 s | 15.2 s | −0.2 s | +0.7 s |

Two parallel routes over the same z cannot give a full sprinter more. The bots' Sprint and Jumper policies average about 3.1 m/s once stamina runs out (8 s bar, RagdollTuning.asset:114, 148).

**Leader** (risky both forks, sprint where it pays, no fall): ≈ 74 s.

| Beat | Seconds |
|---|---|
| Forecourt | 6 |
| Towers | 12.5 |
| J1 | 3 |
| Hammers (sprinters chain the pair, §2.5) | 8.5 |
| Fork 2 risky | 17 |
| J2 | 2.5 |
| Plates | 8 |
| Stair | 4.5 |
| Terrace and door (mean wait 2.7) | 6 |
| Room | 6 |

**Median person** ≈ 110–125 s:
- run pace ≈ 82 s;
- waits ≈ 12 s at a run (hammers ≈ 6.4, press 0.9, drawers 0.9, plates 1.5, door 2.7);
- 2–3 failures of 8–12 s.

Both targets are the design's 80 s / 120 s.

---

## 2. Obstacles

### 2.0 `RoyalVaultRules` (Core) — the single source of timing, layout numbers and look-ahead queries

File `Assets/Scripts/Core/RoyalVaultRules.cs`, directly in Core: the Linux globs are not recursive (run-tests-linux.sh:34, 37, 135). Namespace `ChessFight.Network`, `using System;` only (Core asmdef noEngineReferences).

- **mcs-safe**: no local functions and no named tuples (NetworkCoreTests.cs:12-13); `out` declarations and discards are fine, as SortingYardRules uses.
- All maths in double; `t` is ObstacleClock time at Unix scale.
- Trapezoids reuse `SortingYardRules.Profile(t, seconds, ramp)` (SortingYardRules.cs:30-40).
- The builder, components, validators and bots read these names and never retype the numbers.
- Device milestones add their own block (§5.1); the frame, route, towers and pad blocks land in M0a.

```csharp
public static class RoyalVaultRules
{
    public enum Cue { Off, Go, Warn, Stop }

    // ---------- frame and layout (section frame, metres)
    public const double ExitY = 12, ExitZ = 196, PitBottom = -40, RunSpeed = 2.5, SprintSpeed = 3.6, BodyHalf = 0.3;
    public const double ForecourtZ1 = 18, CorridorX = -18, J1Z0 = 50, J1Z1 = 60, WalkZ1 = 83, ApproachZ1 = 89,
                        BeltZ0 = 89, BeltZ1 = 103, NeckZ0 = 107, NeckZ1 = 114, StairTwoZ1 = 126, J2Z0 = 126, J2Z1 = 136,
                        PlatesZ1 = 156, TerraceZ0 = 168, DoorZ0 = 178, DoorZ1 = 180, ArchZ = 192;
    public static readonly double StairSlope = Math.Sqrt(12 * 12 + 4 * 4);                 // 12.65 m walked per stair
    public static readonly double[] CheckpointZ = { 9, 34, 55, 83.6, 131, 172, 182.5 };
    public static readonly double[] RouteX = { 0, 0, 2.2, 2.2, 2.2, 2.2, 2.2, 2.2, 2.2, 2.2, 2.2, 0, 0, 0, 0, 1.5, 1.5, -2, -2, -2, -2, -2, -2, 0, 0, 0, 0, 0 };
    public static readonly double[] RouteY = { 0, 0, 0, .5, 1, 1.5, 2, 2.5, 3, 3.5, 4, 4, 4, 4, 4, 4, 4, 4, 4, 8, 8, 8, 8, 12, 12, 12, 12, 12 };
    public static readonly double[] RouteZ = { 0, 9, 17.6, 20.65, 25.1, 29.55, 34, 38.45, 42.9, 47.35, 50.6, 55, 60, 83, 86, 89, 103, 105, 114,
                                               126, 131, 136, 156, 168, 172, 179, 182.5, 196 };
    public static double Wrap(double t, double p) { double u = t % p; return u < 0 ? u + p : u; }
    public static double Smooth(double u) { u = u < 0 ? 0 : u > 1 ? 1 : u; return u * u * (3 - 2 * u); }
    static double Hyp(double a, double b) => Math.Sqrt(a * a + b * b);
    public static bool Blink(double t) => ((long)Math.Floor(t * 8.0) & 1) == 0;            // 4 Hz, the same on every PC

    // ---------- V01 coin towers
    public const int TowerCount = 7, TowerRest = 4;
    public const double TowerDiameter = 3.6, TowerStep = 0.5, TowerLaneX = 2.2, TowerFieldZ0 = 18, TowerFieldZ1 = 50, TowerTakeoffHalf = 1.0;
    public static double TowerGap => (TowerFieldZ1 - TowerFieldZ0 - TowerCount * TowerDiameter) / (TowerCount + 1);   // 0.85
    public static double TowerZ(int k) => TowerFieldZ0 + TowerGap * k + TowerDiameter * (k - 0.5);                  // k = 1..7
    public static double TowerTop(int k) => TowerStep * k;                                                            // J1 = 8 x 0.5
    public static double TowerGapOffLine(double d) { double r = TowerDiameter / 2; return TowerDiameter + TowerGap - 2 * Math.Sqrt(Math.Max(0, r * r - d * d)); }
    public static double EdgeGapOffLine(double d) { double r = TowerDiameter / 2; return TowerGap + r - Math.Sqrt(Math.Max(0, r * r - d * d)); }
    // Pawn Rush jump (PawnRushPhysicsParams.cs:20-22): 4.9 m/s up, gravity x1.4 rising, x1.61 falling; seconds in the air onto +dh.
    public static double JumpAirSeconds(double dh)
    {   const double g = 9.81 * 1.4, gf = 9.81 * 1.4 * 1.15, v = 4.9;
        double apex = v * v / (2 * g); return v / g + Math.Sqrt(2 * Math.Max(0, apex - dh) / gf); }            // 0.574 onto +0.5

    // ---------- V02 hurdles (a full bar through the hub; Unity yaw: local x at angle a points (cos a, 0, -sin a))
    public const double HurdleHubX = -18, HurdleReach = 3.95, HurdleBarLow = 0.25, HurdleBarHigh = 0.55, HurdleBodyHalf = 0.45;
    public static readonly double[] HurdleHubZ = { 14.0, 24.5 }, HurdleDegreesPerSecond = { 110, -100 };
    public static double HurdleAngle(int h, double t) => Wrap(HurdleDegreesPerSecond[h] * t, 360);
    public static double HurdleArrivesIn(int h, double dx, double dz, double t)          // seconds until the bar passes over (dx, dz)
    {   double dps = HurdleDegreesPerSecond[h], alpha = Math.Atan2(-dz, dx) * 180 / Math.PI;
        return Wrap((alpha - HurdleAngle(h, t)) * Math.Sign(dps), 180) / Math.Abs(dps); }
    // When to press jump, hips r from the hub: the feet stay over the bar's top 0.14..0.56 s after take-off and bar + body pass in
    // +-HurdleBodyHalf / (omega r). lo >= hi: too near the hub to jump it (a slow bar there only staggers).
    public static void HurdleJumpWindow(int h, double r, out double lo, out double hi)
    {   const double g = 9.81 * 1.4, gf = 9.81 * 1.4 * 1.15, v = 4.9;
        double apex = v * v / (2 * g), up = (v - Math.Sqrt(v * v - 2 * g * HurdleBarHigh)) / g, down = v / g + Math.Sqrt(2 * (apex - HurdleBarHigh) / gf);
        double half = HurdleBodyHalf / (Math.Abs(HurdleDegreesPerSecond[h]) * Math.PI / 180 * Math.Max(r, 1e-3));
        lo = up + half; hi = down - half; }

    // ---------- V03 bulkheads (side -1 west panel, closes the west half; +1 east)
    public static readonly double[] BulkheadZ = { 31.4, 35.4 }, BulkheadPhase = { 0.0, 3.0 };
    public const double BulkheadCycle = 6.0, BulkheadLeg = 1.3, BulkheadRamp = 0.3, BulkheadTravel = 4.0, BulkheadHalfWidth = 4.0, BulkheadWarnSeconds = 1.0;
    public static double BulkheadRest => BulkheadCycle / 2 - BulkheadLeg;                  // 1.7 s in, 1.7 s out
    static double BU(int b, int side, double t) => Wrap(t + BulkheadPhase[b] + (side > 0 ? BulkheadCycle / 2 : 0), BulkheadCycle);
    public static double BulkheadOut(int b, int side, double t)                           // 0 in its pocket .. 1 closing its half
    {   double u = BU(b, side, t), r = BulkheadRest;
        if (u < r) return 0; u -= r;
        if (u < BulkheadLeg) return SortingYardRules.Profile(u, BulkheadLeg, BulkheadRamp); u -= BulkheadLeg;
        if (u < r) return 1; u -= r;
        return 1 - SortingYardRules.Profile(u, BulkheadLeg, BulkheadRamp); }
    public static bool BulkheadBlocked(int b, double x, double t)                         // x across from the corridor's middle, -4..4
    {   double west = -BulkheadHalfWidth + BulkheadTravel * BulkheadOut(b, -1, t), east = BulkheadHalfWidth - BulkheadTravel * BulkheadOut(b, 1, t);
        return x - BodyHalf < west || x + BodyHalf > east; }
    public static double BulkheadClearFor(int b, double x, double t)                      // seconds a body at x stays clear (0.02 s steps)
    {   if (BulkheadBlocked(b, x, t)) return 0;
        for (double s = 0.02; s < BulkheadCycle; s += 0.02) if (BulkheadBlocked(b, x, t + s)) return s;
        return BulkheadCycle; }
    public static Cue BulkheadCue(int b, int side, double t)                              // floor strip of one half
    {   double c = BulkheadClearFor(b, side * BulkheadHalfWidth / 2, t);
        return c >= BulkheadWarnSeconds ? Cue.Go : c > 0 ? Cue.Warn : Cue.Stop; }

    // ---------- V04 dial hammers (angle 0 = head hanging down; positive turns sweep -z at the bottom)
    public static readonly double[] HammerAxleZ = { 65.5, 77.5 };
    public const double HammerDegreesPerSecond = 60, HammerPairSeconds = 3.0, HammerAxleAbove = 4.4, HammerInner = 2.9, HammerOuter = 4.3,
                        HammerAlong = 2.2, HammerAcross = 8.4, HammerBodyTop = 1.2, HammerBodyMargin = 0.4;
    public static double HammerPhase(int i) => i * HammerPairSeconds;                     // half a turn apart
    public static double HammerAngle(int i, double t) => Wrap(HammerDegreesPerSecond * (t + HammerPhase(i)), 360);
    static double Corner => Math.Sqrt(HammerOuter * HammerOuter + HammerAlong * HammerAlong / 4);
    public static double HammerBodyHalfAngle =>
        (Math.Acos((HammerAxleAbove - HammerBodyTop) / Corner) + Math.Atan2(HammerAlong / 2, HammerOuter)) * 180 / Math.PI;   // 58.22
    public static double HammerZoneHalf => Math.Sqrt(Corner * Corner - Math.Pow(HammerAxleAbove - HammerBodyTop, 2)) + HammerBodyMargin;  // 3.48
    public static bool HammerAtBody(int i, double t) { double a = HammerAngle(i, t), b = HammerBodyHalfAngle; return a <= b || a >= 360 - b; }
    public static double HammerClearFor(int i, double t)
    {   double a = HammerAngle(i, t), b = HammerBodyHalfAngle; return a <= b || a >= 360 - b ? 0 : (360 - b - a) / HammerDegreesPerSecond; }
    public static double HammerSinceClear(int i, double t)
    {   double a = HammerAngle(i, t), b = HammerBodyHalfAngle; return a <= b || a >= 360 - b ? 0 : (a - b) / HammerDegreesPerSecond; }
    public static double HammerHeadZ(int i, double t) => HammerAxleZ[i] - (HammerInner + HammerOuter) / 2 * Math.Sin(HammerAngle(i, t) * Math.PI / 180);
    public static Cue HammerCue(int i, double t) =>
        HammerAtBody(i, t) ? Cue.Stop : HammerClearFor(i, t) < 1 ? Cue.Warn : HammerSinceClear(i, t) < 1 ? Cue.Go : Cue.Off;

    // ---------- V05 crown press (the cycle starts at a landing)
    public const double PressX = -2, PressZ = 110, PressDieAcross = 8, PressDieAlong = 2.4, PressDieTall = 1.6, PressDrop = 3.8,
                        PressPeriod = 4.5, PressSit = 0.4, PressRise = 1.0, PressWarnSeconds = 1.2, PressFall = 0.25,
                        PressBandOut = 1.8, PressWaveSeconds = 0.3, PressWavePush = 4.0, PressWaveLift = 2.0,
                        PressKnockdown = 1.0, PressCrushPush = 6.0, PressHeadroom = 1.5;
    public static double PressBandZ0 => PressZ - PressDieAlong / 2 - PressBandOut;   // 107
    public static double PressBandZ1 => PressZ + PressDieAlong / 2 + PressBandOut;   // 113
    public enum PressStage { Down, Rising, Up, Warning, Falling }
    public static double PressSinceLanding(double t) => Wrap(t, PressPeriod);
    public static double PressLandsIn(double t) => PressPeriod - PressSinceLanding(t);
    public static PressStage PressAt(double t)
    {   double s = PressSinceLanding(t);
        if (s < PressSit) return PressStage.Down;
        if (s < PressSit + PressRise) return PressStage.Rising;
        if (s < PressPeriod - PressFall - PressWarnSeconds) return PressStage.Up;     // 1.65 s up
        return s < PressPeriod - PressFall ? PressStage.Warning : PressStage.Falling; }
    public static double PressLowered(double t)                                         // 1 on the floor .. 0 up
    {   double s = PressSinceLanding(t);
        if (s < PressSit) return 1;
        if (s < PressSit + PressRise) return 1 - Smooth((s - PressSit) / PressRise);
        double f = s - (PressPeriod - PressFall); if (f < 0) return 0;
        double u = f / PressFall; return u * u; }                                       // lands at 2 x 3.8 / 0.25 = 30.4 m/s
    public static double PressDieBottom(double t) => PressDrop * (1 - PressLowered(t));
    public static bool PressSafeToCross(double t, double crossSeconds)                  // start crossing the band now
    {   double band = PressBandZ1 - PressBandZ0;
        if (PressSinceLanding(t) < PressWaveSeconds || PressLandsIn(t) <= crossSeconds + 0.1) return false;
        double enter = crossSeconds * PressBandOut / band, leave = crossSeconds * (PressBandOut + PressDieAlong) / band;
        for (double tau = enter; tau <= leave; tau += 0.05) if (PressDieBottom(t + tau) < PressHeadroom) return false;
        return true; }
    // Stop during the landing's wave; Go while a runner may start; Warn from "too late to start" through the fall.
    public static Cue PressCue(double t) => PressSinceLanding(t) < PressWaveSeconds ? Cue.Stop
        : PressSafeToCross(t, (PressBandZ1 - PressBandZ0) / RunSpeed) ? Cue.Go : Cue.Warn;

    // ---------- V06 belts (belt 0 west, 1 east; m/s along +z)
    public const double BeltWestX0 = -6, BeltSplitX = -1, BeltEastX1 = 4;
    public const double WestBeltSpeed = -1.0, EastStopSpeed = 0.0, EastBackSpeed = -1.5, EastHalf = 5.0, EastRamp = 0.5, EastWarnSeconds = 1.0;
    public static double BeltSpeed(int belt, double t)
    {   if (belt == 0) return WestBeltSpeed;
        double u = Wrap(t, 2 * EastHalf);
        if (u < EastHalf) return u < EastRamp ? EastBackSpeed + (EastStopSpeed - EastBackSpeed) * Smooth(u / EastRamp) : EastStopSpeed;
        u -= EastHalf;
        return u < EastRamp ? EastStopSpeed + (EastBackSpeed - EastStopSpeed) * Smooth(u / EastRamp) : EastBackSpeed; }
    public static double EastStoppedFor(double t) { double u = Wrap(t, 2 * EastHalf); return u >= EastRamp && u < EastHalf ? EastHalf - u : 0; }
    public static bool EastWarn(double t) { double u = Wrap(t, 2 * EastHalf); return u >= EastHalf - EastWarnSeconds && u < EastHalf; }
    public static Cue BeltCue(int belt, double t) => belt == 0 ? Cue.Stop : EastWarn(t) ? Cue.Warn : EastStoppedFor(t) > 0 ? Cue.Go : Cue.Stop;
    // Metres a point of the belt has moved along +z since clock 0 (closed form; the slats use it).
    public static double BeltTravel(int belt, double t)
    {   if (belt == 0) return WestBeltSpeed * t;
        double c = 2 * EastHalf, n = Math.Floor(t / c), u = t - n * c, a = EastBackSpeed, s = EastStopSpeed, r = EastRamp, h = EastHalf;
        double d = n * (RampTravel(a, s, r) + (h - r) * s + RampTravel(s, a, r) + (h - r) * a);
        if (u < h) return d + RampTravel(a, s, Math.Min(u, r)) + Math.Max(0, u - r) * s;
        d += RampTravel(a, s, r) + (h - r) * s; u -= h;
        return d + RampTravel(s, a, Math.Min(u, r)) + Math.Max(0, u - r) * a; }
    // Integral of lo + (hi - lo) Smooth(v / r) over v in [0, tau]: lo tau + (hi - lo) r (x^3 - x^4 / 2), x = tau / r.
    static double RampTravel(double lo, double hi, double tau)
    {   double x = tau / EastRamp; return lo * tau + (hi - lo) * EastRamp * (x * x * x - x * x * x * x / 2); }
    public static double BeltRideSeconds(int belt, double t, double v, double length)   // 0.02 s steps; bots' belt choice, fork model
    {   double z = 0, s = 0; while (z < length && s < 60) { z += (v + BeltSpeed(belt, t + s)) * 0.02; s += 0.02; } return s; }

    // ---------- V07 pad (LaunchPad.ArcVelocity under plain gravity: launched pawns skip the Pawn Rush gravity)
    public const double PadX = 6.0, PadZ = 84.6, PadSize = 1.6, PadFloor = 4.0, PadLandX = 5.0, PadLandY = 8.0, PadLandZ = 93.3,
                        PadClearance = 1.9, PadGravity = 9.81, PadCatch = 0.6;
    public static void PadThrow(out double up, out double vx, out double vz, out double seconds)
    {   double rise = PadLandY - PadFloor, apex = Math.Max(rise, 0) + PadClearance;              // 5.9
        up = Math.Sqrt(2 * PadGravity * apex);                                                   // 10.76
        seconds = Math.Sqrt(2 * apex / PadGravity) + Math.Sqrt(2 * (apex - rise) / PadGravity);  // 1.719
        vx = (PadLandX - PadX) / seconds; vz = (PadLandZ - PadZ) / seconds; }                     // -0.58, 5.06
    public static void PadFlight(double x0, double z0, double s, out double x, out double y, out double z)   // sameThrow feet
    {   PadThrow(out double up, out double vx, out double vz, out _);
        x = x0 + vx * s; y = PadFloor + up * s - PadGravity * s * s / 2; z = z0 + vz * s; }

    // ---------- V08 drawers (face at ShelfX1 when in, at ShelfX0 when out; drawer 2 runs 1.5 s after drawer 1)
    public static readonly double[] DrawerZ = { 98.0, 101.8 }, DrawerPhase = { 0.0, -1.5 };
    public const double ShelfX0 = 3, ShelfX1 = 7, ShelfZ0 = 91, ShelfOpenZ0 = 96.3, ShelfOpenZ1 = 103.3;
    public const double DrawerLong = 2.4, DrawerTravel = 4.0, DrawerRest = 2.4, DrawerOutSeconds = 1.0, DrawerHold = 0.4,
                        DrawerInSeconds = 1.0, DrawerRamp = 0.25, DrawerWarnSeconds = 1.0;
    public static double DrawerCycle => DrawerRest + DrawerOutSeconds + DrawerHold + DrawerInSeconds;   // 4.8
    static double DU(int i, double t) => Wrap(t + DrawerPhase[i], DrawerCycle);
    public static double DrawerOut(int i, double t)
    {   double u = DU(i, t); if (u < DrawerRest) return 0; u -= DrawerRest;
        if (u < DrawerOutSeconds) return SortingYardRules.Profile(u, DrawerOutSeconds, DrawerRamp); u -= DrawerOutSeconds;
        if (u < DrawerHold) return 1; u -= DrawerHold;
        return 1 - SortingYardRules.Profile(u, DrawerInSeconds, DrawerRamp); }               // face peak 4 / 0.75 = 5.33 m/s
    public static double DrawerFaceX(int i, double t) => ShelfX1 - DrawerTravel * DrawerOut(i, t);
    public static bool DrawerWarn(int i, double t) { double u = DU(i, t); return u >= DrawerRest - DrawerWarnSeconds && u < DrawerRest; }
    public static double DrawerClearFor(int i, double x, double t)                         // seconds the face stays east of a body at x
    {   if (DrawerFaceX(i, t) < x + BodyHalf) return 0;
        for (double s = 0.02; s < DrawerCycle; s += 0.02) if (DrawerFaceX(i, t + s) < x + BodyHalf) return s;
        return DrawerCycle; }
    public static Cue DrawerCue(int i, double t) => DrawerWarn(i, t) ? Cue.Warn : DrawerOut(i, t) > 0 ? Cue.Stop : Cue.Off;

    // ---------- fork 2 time model (Core test 10 and the bot report; not used in play). From (0, 4, WalkZ1) at t0 to (0, 8, 129),
    // ideal play at speed v: the faster belt, the first safe press start, the first fully clear pass at each drawer (body at x 5),
    // the pad's first contact 0.3 m outside its south-west corner, 0.4 s to stand after landing.
    public static double ForkTwoSafeSeconds(double t0, double v)
    {   double t = t0 + Hyp(1.5, BeltZ0 - WalkZ1) / v, length = BeltZ1 - BeltZ0;
        t += Math.Min(BeltRideSeconds(0, t, v, length), BeltRideSeconds(1, t, v, length));
        return SafeFromBeltEnd(t, v) - t0; }
    public static double ForkTwoRiskySeconds(double t0, double v)
    {   PadThrow(out _, out _, out _, out double flight);
        double cx = PadX - PadSize / 2 - BodyHalf, cz = PadZ - PadSize / 2, z = cz + (PadLandZ - PadZ);
        double t = ShelfWalk(t0 + Hyp(cx, cz - WalkZ1) / v + flight + 0.4, v, ref z);
        return t + (StairTwoZ1 - z) / v + Hyp(5, 3) / v - t0; }
    public static double ForkTwoDropCost(double t0, double v)                               // at the drawer-1 wait line: pushed now vs waiting
    {   double t = t0 + 0.5 + Math.Sqrt(2 * 4.0 / (9.81 * 1.4 * 1.15)), length = BeltZ1 - DrawerZ[0];
        t += Math.Min(BeltRideSeconds(0, t, v, length), BeltRideSeconds(1, t, v, length));
        double pushed = SafeFromBeltEnd(t, v), z = DrawerZ[0] - DrawerLong / 2 - BodyHalf - 0.05;
        double kept = ShelfWalk(t0, v, ref z) + (StairTwoZ1 - z) / v + Hyp(5, 3) / v;
        return pushed - kept; }
    static double SafeFromBeltEnd(double t, double v)
    {   double wait = PressBandZ0 - BodyHalf, cross = (PressBandZ1 - PressBandZ0) / v;
        t += Hyp(1.5 - PressX, wait - BeltZ1) / v;
        for (int k = 0; k < 1000 && !PressSafeToCross(t, cross); k++) t += 0.01;
        return t + (NeckZ1 - wait + StairSlope) / v + Hyp(PressX, 3) / v; }
    static double ShelfWalk(double t, double v, ref double z)
    {   for (int i = 0; i < DrawerZ.Length; i++)
        {   double back = DrawerZ[i] + DrawerLong / 2 + BodyHalf, line = DrawerZ[i] - DrawerLong / 2 - BodyHalf - 0.05;
            if (z < line) { t += (line - z) / v; z = line; }
            for (int k = 0; k < 2000 && !ShelfPassClear(i, t, z, back, v); k++) t += 0.01;
            t += (back - z) / v; z = back; }
        return t; }
    static bool ShelfPassClear(int i, double t, double z, double back, double v)
    {   double front = DrawerZ[i] - DrawerLong / 2 - BodyHalf;
        for (double s = 0; z + v * s < back; s += 0.01) if (z + v * s > front && DrawerFaceX(i, t + s) < 5 + BodyHalf) return false;
        return true; }

    // ---------- V09 plates (row 0 west, 1 east; the inner edge lifts)
    public const int PlateCount = 5;
    public const double PlateZ0 = 136, PlatePitch = 4, PlateWide = 3.97, PlateLong = 3.95, PlateCycle = 8.0, PlateRaise = 1.0,
                        PlateHold = 0.8, PlateLower = 1.0, PlateAngleMax = 50, PlateWarnSeconds = 1.6, PlateSlipAngle = 12;
    public static double PlateFlat => PlateCycle - PlateRaise - PlateHold - PlateLower;     // 5.2
    public static double PlatePhase(int row) => row * PlateCycle / 2;                      // 0, 4
    static double PU(int row, double t) => Wrap(t + PlatePhase(row), PlateCycle);
    public static double PlateAngle(int row, double t)
    {   double u = PU(row, t); if (u < PlateFlat) return 0; u -= PlateFlat;
        if (u < PlateRaise) return PlateAngleMax * Smooth(u / PlateRaise); u -= PlateRaise;
        if (u < PlateHold) return PlateAngleMax; u -= PlateHold;
        return PlateAngleMax * (1 - Smooth(u / PlateLower)); }                             // peak 75 deg/s: inner edge 5.2 m/s
    public static double PlateFlatFor(int row, double t) { double u = PU(row, t); return u < PlateFlat ? PlateFlat - u : 0; }
    public static bool PlateWarn(int row, double t) => PU(row, t) >= PlateFlat - PlateWarnSeconds;   // 1.6 s warning + the lift
    public static Cue PlateCue(int row, double t) => PlateAngle(row, t) > 0 ? Cue.Stop : PlateWarn(row, t) ? Cue.Warn : Cue.Off;

    // ---------- V10 door (the period starts as the disc starts rolling open, toward +x)
    public const double DoorPeriod = 15, DoorRoll = 2.3, DoorRollRamp = 0.5, DoorOpenHold = 3.1, DoorTravel = 9, DoorRadius = 4.5,
                        DoorThick = 1, DoorCentreAbove = 3, DoorWayInradius = 3.9, DoorPassWidth = 2.5, DoorClosingWarnSeconds = 1.5;
    public static double DoorShutHold => DoorPeriod - DoorOpenHold - 2 * DoorRoll;        // 7.3
    public static double DoorOpen(double t)
    {   double u = Wrap(t, DoorPeriod);
        if (u < DoorRoll) return SortingYardRules.Profile(u, DoorRoll, DoorRollRamp); u -= DoorRoll;   // 5 m/s cruise
        if (u < DoorOpenHold) return 1; u -= DoorOpenHold;
        if (u < DoorRoll) return 1 - SortingYardRules.Profile(u, DoorRoll, DoorRollRamp);
        return 0; }
    public static double DoorClearWidth(double t)                                          // at 1 m over the floor
    {   double y = 1 - DoorCentreAbove, tan = Math.Tan(Math.PI / 8);
        double half = Math.Abs(y) <= DoorWayInradius * tan ? DoorWayInradius : DoorWayInradius * (1 + tan) - Math.Abs(y);   // 3.515
        double chord = Math.Sqrt(DoorRadius * DoorRadius - y * y);                                                     // 4.031
        return Math.Max(0, Math.Min(half, DoorTravel * DoorOpen(t) - chord) + half); }
    public static readonly double DoorPassableStart = FindPassableStart();                // 0.853
    public static double DoorPassableEnd => 2 * DoorRoll + DoorOpenHold - DoorPassableStart;   // 6.847: 5.99 s passable per 15 s
    static double FindPassableStart()
    {   double lo = 0, hi = DoorRoll;
        for (int k = 0; k < 60; k++) { double mid = (lo + hi) / 2; if (DoorClearWidth(mid) >= DoorPassWidth) hi = mid; else lo = mid; }
        return hi; }
    public static double DoorPassableFor(double t) { double u = Wrap(t, DoorPeriod); return u >= DoorPassableStart && u < DoorPassableEnd ? DoorPassableEnd - u : 0; }
    public static double DoorOpensIn(double t)
    {   double u = Wrap(t, DoorPeriod); return u < DoorPassableStart ? DoorPassableStart - u : u < DoorPassableEnd ? 0 : DoorPeriod - u + DoorPassableStart; }
    public static int DoorCountdown(double t) { double w = DoorOpensIn(t); return w > 0 && w <= 3 ? (int)Math.Ceiling(w) : 0; }
    public static bool DoorClosingWarn(double t) { double u = Wrap(t, DoorPeriod); return u >= DoorPassableEnd - DoorClosingWarnSeconds && u < DoorPassableEnd; }
    public static string DoorSign(double t, out Cue look)                                 // digits in ink, "열림" Go, "닫힘" Warn
    {   int n = DoorCountdown(t);
        if (n > 0) { look = Cue.Off; return n.ToString(); }
        if (DoorClosingWarn(t)) { look = Cue.Warn; return "닫힘"; }
        if (DoorPassableFor(t) > 0) { look = Cue.Go; return "열림"; }
        look = Cue.Off; return ""; }
}
```

Derived numbers for the doc and the tests (computed):

| Obstacle | Number |
|---|---|
| Towers | gap 0.85; air on +0.5 is 0.574 s; reach 1.44 run / 2.07 sprint / 1.15 king |
| Hurdles | jump window at r 2 m: [0.257, 0.442) s (+110°/s) and [0.268, 0.430) (−100°/s). At r 3.8: [0.201, 0.498). Empty below r ≈ 1.2 m. Every point is swept every 1.64 / 1.80 s; tip 7.6 / 6.9 m/s |
| Bulkheads | gap always 4.0 m; peak 4.0 m/s. ClearFor at a half's middle: max 2.84 s, Go 1.87 s and Warn 0.98 s per cycle. At the corridor's middle: max 0.88 s |
| Hammers | body window 1.94 s per 6 s turn; clear 4.06 s; zone 6.95 m, crossed in 2.78 s at a run. Pair: Run waits ≈ 2.3 s at hammer 1 and ≈ 4.2 s at hammer 2; Sprint ≈ 1.5 s and ≈ 0.2 s (chains the pair 94 % of the time) |
| Press | runner window [0.30, 2.00) (38 %), mean wait 0.87 s; sprinter [0.33, 2.73), 0.49 s. Cue: Stop [0, 0.3), Go [0.3, 2.0), Warn [2.0, 4.5). A runner starting at 0.30 reaches the die when its bottom is 2.57 m up |
| Belts | 14 m ride: east 7.78 s / west 9.34 s at 2.5 m/s (east always faster); 5.91 / 6.68 at 3.1 m/s (east faster at 73 % of entry times); 4.93 / 5.40 at 3.6 m/s (68 %) |
| Pad | up 10.76, forward 5.09 m/s, 1.719 s, apex feet y 9.9. Landing 7.95 m/s total, 6.1 vertical, i.e. a floor impact of 4.8 after /1.27. Landings x 3.6..6.4, z 91.9..94.7. Clearance ≥ 0.66 over the lip and ≥ 0.56 over the shelf E rail (catch ≤ 0.6 m) |
| Drawers | face at x 5.3 blocked 1.51 s per 4.8 s; ClearFor at x 5 max 3.3 s; boxes z 96.8–99.2 and 100.6–103.0; cell 99.2–100.6 |
| Plates | both rows flat in [0, 1.2) and [4.0, 5.2); slip begins 0.32 s into the lift. Warning → slip 1.92 s ≥ 1.85 s (0.25 s reaction + 4.0 m at a run); lift 3.04 m |
| Door | mean wait 2.70 s, longest 9.0 s; disc circumradius cover 4.5 vs 4.22 |

### 2.1 Shared building blocks (new, `Assets/Scripts/PawnRush`, one class per file)

**`RoyalVaultLook`** (static class)
- Materials: `Greybox`, `CueRed`, `CueGreen`, `CueGold`, `Ink`. Colours: `InkColor` (#292433), `GreenColor` (#59FF73), `RedColor` (#FF4A3A). (§ Conventions.)
- `static SectionKit Kit`: made once with `ScriptableObject.CreateInstance<SectionKit>()` (CourseKit is abstract, SectionKit.cs:9), hideFlags DontSave. Every Material field is set to Greybox (floorChecker, wallClimbable, wallNoClimb, glass, trimWhite, trimBlack, rankGold, boardLight, boardDark, hazard, marker, respawnFloor; CourseKit.cs:13-30), so CourseBuilder output comes out neutral.
- `static TextMesh Sign(Transform, string, Vector3, float size) => YardKit.Label(…, InkColor)`.

**`VaultMover : Obstacle`** — a generic Core-driven mover; it passes check_obstacle_clock because it derives from Obstacle.

```csharp
public void Configure(Vector3 localTravel, Func<double, double> along, Vector3 localAxis = default,
                      Func<double, double> degrees = null, bool seeThroughWhileMoving = true)   // on an INACTIVE object
protected override void Awake()   { GetComponentsInChildren(true, sees /*CameraSeeThrough*/); base.Awake(); }
protected override void Evaluate(double time, out Vector3 position, out Quaternion rotation)
{   position = StartPosition + StartRotation * (travel * (float)(along != null ? along(time) : 0.0));
    rotation = degrees == null ? StartRotation : StartRotation * Quaternion.AngleAxis((float)(degrees(time) % 360.0), axis); }
protected override void FixedUpdate()   // base, then each CameraSeeThrough on while moving (> 1 cm/s or turning)
```

- `time` is `ObstacleClock.Now + phase` with phase 0 (Obstacle.cs:89), so the Core function receives the absolute clock.
- The profiles are continuous, so `JumpsBetween` stays false.
- **Lambda capture rule:** every index used in a lambda is copied into a local inside the loop body (`for (int k = 0; k < n; k++) { int d = k; … DrawerOut(d, t) … }`). A `for` variable is one shared variable, so without the copy every drawer would read index 2 and throw (code-contract). This applies to bulkheads, drawers, plates, hammers and every cue.

**`VaultPlate : Obstacle`** — the angle comes from Core; slip and pull are copied from TiltingWoodenBridge.cs:66-159.
- `Configure(int row, float wide = 3.97f, float longSide = 3.95f)` calls `Build()` **at once**, guarded by `built`. The deck, its collider and its CourseFloor must therefore exist on the inactive Content and in Edit mode. Validate Sections and RoyalVaultValidation build in Edit mode, where Awake never runs (SectionPrefabMenu.cs:159-181); without this the 20 m bridge had no floor (code-contract).
- `Awake() { Build(); base.Awake(); }`; the second `Build` is a no-op.
- Build makes:
  - `RV plate deck` = `YardKit.Box(transform, …, centre (wide/2, −.15, long/2), size (wide, .3, long), Greybox, Solid.NoClimb)`, plus CourseFloor and a disabled CameraSeeThrough;
  - the PhysicsMaterial "RV wet plate": friction .04, Minimum combine, bounciness 0.
- **No cue here**: the rim is added by the cue partial as a child of the plate root.
- `Evaluate`: `position = StartPosition; rotation = StartRotation * Quaternion.AngleAxis((float)PlateAngle(row, t), Vector3.forward)`. This lifts the local +x (inner) edge, as TiltingWoodenBridge LongSide does (:103-109).
- FixedUpdate, after base:
  - read `a = PlateAngle(row, Now)`;
  - CameraSeeThrough on while a > .5;
  - slip = a > PlateSlipAngle, and the deck's sharedMaterial is wet or null accordingly;
  - when slipping and `ObstacleClock.Authority`, pull riders with `ImportedObstacleActor.Find(c)?.Accelerate(downhill * 6f, 4f, dt)` (ImportedObstacleActor.cs:42-82). `OnCollisionStay` collects character colliders (TiltingWoodenBridge.cs:156-159).

**`VaultPressWave : MonoBehaviour`** — the shock band, mirroring `SlamChest.Shock` (SlamChest.cs:278-346) with band geometry. It runs in FixedUpdate under `ObstacleClock.Authority` only:

```csharp
s = PressSinceLanding(Now); if (s > PressWaveSeconds + .1) return;
trip = floor(Now / PressPeriod);                                    // reset `judged` per trip
reach = PressDieAlong/2 + clamp01(s / PressWaveSeconds) * PressBandOut;
foreach driver in YardKit.CharactersNear(transform.position, 7f):
    local = section.InverseTransformPoint(hips)
    skip if |local.x - PressX| > PressDieAcross/2 + .3, or |local.z - PressZ| > reach, or already judged; then mark judged
    if |local.z - PressZ| < PressDieAlong/2 + .35: continue         // the crush shells had them
    if !YardKit.Movable(...): continue
    if Lowest(owner) > floorWorldY + .4: { Spared++; continue }     // airborne spared (SlamChest's Lowest)
    push = flat(section.forward) * sign(local.z - PressZ) * PressWavePush   // along ±z only: never into the walls
    if push · forward > 0: push -= forward * (ahead * .4)           // keep 60 % forward
    push -= flat(hipsVelocity) * weight                             // replaceSpeed
    fresh = !YardKit.RecentlyDown(driver)
    hits.ApplyHit(push + up * PressWaveLift, fresh ? PressKnockdown : 0, 0, true)
    YardKit.ReportHit(driver, this, push, fresh ? PressKnockdown : 0); if fresh: YardKit.MarkDown(driver)
```

`Configure(Transform section, float floorLocalY = 4)`. A comment keeps the pointer to SlamChest.cs:278-346.

**`VaultBeltSurface : MonoBehaviour, IMovingSurface`** (on the belt box, like ConveyorBeltSurface.cs:9-20)
- `Configure(int belt)`.
- `PointVelocity(p) => transform.forward * (float)RoyalVaultRules.BeltSpeed(belt, ObstacleClock.Now)`; `DeltaRotation => identity`.
- It has no state and nothing kinematic; riders take the velocity (RagdollPawn.cs:833-839, 1222-1226).

**`VaultCue : MonoBehaviour`** (look only; touches no collider; never uses PhaseToggle)
- `Configure(Func<double, RoyalVaultRules.Cue> state)`: Off hides every child renderer; Go uses CueGreen; Warn uses CueRed shown only while `RoyalVaultRules.Blink(Now)`; Stop uses CueRed.
- `ConfigureText(Func<double, string> words, Func<double, RoyalVaultRules.Cue> look)` drives the TextMesh on the same object: Off → InkColor, Go → GreenColor, Warn → RedColor blinking, Stop → RedColor.
- Update reads `ObstacleClock.Now`.

**`VaultSlats : MonoBehaviour`** (look only)
- `Configure(int belt, Transform[] slats, float z0, float length, float pitch)`.
- Update puts slat k at `z0 + Wrap(BeltTravel(belt, Now) + k·pitch, length)`.
- The slats are children of Content, not of the scaled belt box.

**`RoyalVaultSection : MonoBehaviour`** (`[RequireComponent(typeof(PawnRushSection))]`, pattern WarRoomSection.cs:17-33)
- `const string Id = "Late_3-3_RoyalVault", DisplayName = "왕실 금고실", ContentName = "Content"`.
- `static readonly Vector3 Entry = Vector3.zero, Exit = new Vector3(0, (float)RoyalVaultRules.ExitY, (float)RoyalVaultRules.ExitZ)`.
- `[SerializeField] bool buildIfEmpty = true`; `HasContent`; Awake → `RoyalVaultBuilder.Build(this)` when empty.
- No kit field and no ISectionPart: every phase is a fixed constant on the shared clock, so the seed is unused.

### 2.2 V01 coin towers (MUST) and the rest platform

- **Component:** static `CourseBuilder.Cylinder` pieces plus CourseFloor (§1.3). The rest platform is a Floor. Nothing moves.
- **Numbers:** 7 rows (row 4 is the shared rest), 2 lanes, towers 3.6 m wide, gap 0.85 m, +0.5 m per row, J1 one more step up. Take-off openings are 2.0 m wide per lane.
- **Rules:** `TowerZ`, `TowerTop`, `TowerGap`, `TowerGapOffLine`, `EdgeGapOffLine`, `JumpAirSeconds`.
- **Cues:** gold rims, forecourt and rest-N lips (take-off), rest-S and J1 lips (landing). No warning is needed: the heights are visible.
- **Knock:** none; a fall returns to CP0 or CP1. The sides are NoClimb, so a short jump cannot turn into a climb. The 0.2 m ledge assist still works.
- **Chess reading:** pawns advance one rank at a time. A blocked pawn waits. Lane hops are allowed but are not called chess.
- **Sign:** §1.17.

### 2.3 V02 gold-bar hurdles ×2 (counter-rotating)

**Component:** `Spinner` (Spinner.cs:6-24), configured before activation: `root = b.Group("RV hurdle k")` at (−18, 0, HurdleHubZ[k]), then `AddComponent<Spinner>().Configure(Vector3.up, (float)HurdleDegreesPerSecond[k])`, phase 0. The `ObstacleSpan` goes on the root.

Children:

| Child | What it is |
|---|---|
| RV hurdle hub | cylinder 0.8 m wide, 0.9 m tall, N |
| RV hurdle bar | picture box 7.9 × 0.3 × 0.3 at y .40, plus a `CapsuleCollider` (direction x, radius .15, height 7.9, centre (0, .40, 0)) and NoClimbSurface — the garden's capsule-log pattern (RoyalWaterGardenBuilder.Beats.cs:97-112) |
| RV hurdle k (hit) | GameObject with a trigger `CapsuleCollider` (direction x, radius .45, height 8.0, centre y .40) and `YardImpact { body = Drum }`; the Spinner is its IMovingSurface (YardImpact.cs:130) |

**Knock settings** (YardImpact.cs:39-91):

| Field | Value |
|---|---|
| knockdownSpeed | 3.0 (hips beyond ≈ 1.6 m from the hub) |
| knockdownSeconds | 1.0 |
| knockbackForce | 3.0 |
| speedScale | 0.3 |
| maxPush | 5.0 |
| lift | 1.5 |
| alongShare | 0.7 |
| replaceSpeed | true |
| cooldown | 1.0 |
| course / forwardShare | section root / 0.6 |

- **Rules:** `HurdleAngle`, `HurdleArrivesIn`, `HurdleJumpWindow`.
- **Speeds:** +110 / −100 °/s (≥ the R680 garden's 105 / 95). The tip moves 7.6 m/s.
- **Near the hub** the bar is slow: it only staggers, and it cannot be jumped.
- **Rails:** the tip passes 0.05 m from the rail faces, moving parallel to them, so nobody is pinched.
- **Cue:** none; the bar is visible.
- **Chess reading:** the rook's bar — jump the line.
- **Weights:** pushes scale 1/weight (RagdollPawn.cs:3392).

### 2.4 V03 vault bulkheads ×2 (pocket panels)

**Component:** one `VaultMover` per panel.
- West panel: root (−24, 1.52, Z), `Configure(new Vector3(+4, 0, 0), t => BulkheadOut(bb, -1, t))`, where `bb` is the local copy of b.
- East panel: root (−12, 1.52, Z), travel (−4, 0, 0), side +1.

Children of each panel:
- `RV bulkhead panel`: box 4.0 × 3.0 × 0.8 m (bottom 0.02 above the floor), N, CameraSeeThrough.
- `RV bulkhead merlon` ×3: pictures 0.6 × 0.5 × 0.8 m on the top edge at local x −1.4 / 0 / 1.4, y 1.5..2.0. A rook silhouette, in Greybox.

Timing:
- 6 s cycle: in 1.7 s, moving 1.3 s (trapezoid, peak 4 m/s), out 1.7 s, moving 1.3 s;
- the two panels of a bulkhead are half a cycle apart, so a 4 m gap always exists and slides from half to half;
- bulkhead 2 runs 3 s after bulkhead 1.

**Knock:** `YardKit.Shell(panelRoot, "RV bulkhead (hit)", 0, (4.4, 3.2, 1.2), 3f)` with solid = panel box and:
- **knockdownSpeed 99** (shove only);
- alongShare 1, toward the centre, i.e. the half that is opening;
- speedScale .3, maxPush 4.5, lift .8;
- replaceSpeed, cooldown .6;
- course + forwardShare .6.

A panel closes toward the open half, so nobody is pinched against a rail (P43).

- **Rules:** `BulkheadOut`, `BulkheadBlocked`, `BulkheadClearFor`, `BulkheadCue`.
- **Cue:** `Cue bulkhead b W/E` with VaultCue(BulkheadCue). Go while the half's middle stays clear ≥ 1 s; Warn in its last second; Stop while blocked.
- **Chess reading:** castling, labels O-O-O (west) and O-O (east), with merlons for the rooks.
- **Sign:** §1.17.

### 2.5 V04 dial hammers ×2 (MUST)

**Component:** `Spinner` on root `RV hammer i` at (0, 8.4, HammerAxleZ[i]): `Configure(Vector3.right, 60f)`, then `SetPhase((float)HammerPhase(i))` (0 / 3 s) before activation. The Spinner's angle is 60·(Now + phase), identical to `HammerAngle` (Spinner.cs:20-24). The `ObstacleSpan` goes on the root.

| Child | Geometry | Kind |
|---|---|---|
| RV hammer head | box centre (0, −3.6, 0), size (8.4, 1.4, 2.2); lowest point 0.1 m over the walkway | N, CameraSeeThrough always on |
| RV hammer arm W/E | (∓4.35, −1.45, 0), size (.2, 2.9, .5) | P |
| RV hammer axle | cylinder along x, 0.6 m wide, 11 m long, centred (−0.9, 0, 0) | P |
| RV hammer i (hit) | `YardKit.Shell(root, …, (0, −3.6, 0), (8.7, 1.7, 2.5), 3f)` | YardImpact, below |

**Knock settings:**

| Field | Value |
|---|---|
| solid | head box |
| knockdownSpeed | 2.5 (the head moves ≈ 4.1 m/s at hip radius) |
| knockdownSeconds | 1.0 |
| speedScale | .5 |
| maxPush | **3.8** |
| lift | 1.5 |
| alongShare | 1 (straight back along −z at the bottom; never sideways off the unrailed walkway) |
| replaceSpeed | true |
| cooldown | 3.0 (one hit per pass; a turn is 6 s) |
| ghostSeconds | 1.0 |
| course / forwardShare | section root / .6 |

By weight (slides estimated from ROYAL_CARRIAGE_YARD.md:91 by v²):

| Piece | Ends at | Slide |
|---|---|---|
| pawn | 3.8 m/s back | ≈ 2.6 m |
| rook | 2.5 | ≈ 1.2 m |
| knight | 4.75 | ≈ 4.1 m (stays inside the 5 m rest cell, never in the other hammer's zone) |
| queen | 1.9 | ≈ 0.65 m |

- **Pair timing (computed):** half a turn apart, so the clear windows travel +z at 4 m/s. A sprinter chains both hammers 94 % of the time; a runner waits ≈ 4.2 s in the rest cell. This is kept on purpose (the design's 엇박자 and the chess-clock reading; it rewards sprint timing). The knob to set them in phase is in §6.
- **Rules:** `HammerAngle`, `HammerAtBody`, `HammerClearFor`, `HammerSinceClear`, `HammerZoneHalf`, `HammerHeadZ`, `HammerCue`.
- **Cue:** `Cue hammer lamp i` with VaultCue(HammerCue): Stop at body height, Warn in the last second before it, Go for 1 s after it leaves, Off otherwise.
- **Chess reading:** two chess clocks running in turn. Move right after the opponent's move: follow the head in. The rest cell is a safe rank.
- **Sign:** §1.17.

### 2.6 V05 crown press (MUST)

**Component:** `VaultMover` at root `RV press die` (−2, 8.6, 110): `Configure(new Vector3(0, −3.8, 0), t => RoyalVaultRules.PressLowered(t))`. The span is static (§1.15).

| Child | Geometry | Kind |
|---|---|---|
| RV press die box | 7.98 × 1.6 × 2.4 | N, CameraSeeThrough while moving |
| RV press crown | on top: band 3 × .3 × 1.2 and five 0.3 × 0.6 × 0.3 points at x −1.2..1.2 | P landmark |
| RV press screw | cylinder 0.8 m wide, local y 0.8..11.2: it stays in the head beam when lowered and sticks out above it when raised, as a screw press does | P |
| RV press crush S / N | `YardKit.Shell(die, …, (0, −.15, ∓.675), (8.3, 1.9, 1.35), 6f)`, the two halves meeting at z 0 with no overlap; each has a `lane` child yawed 180 (S) or 0 (N) | YardImpact, below |

The crush shells copy SlamChest's crush (SlamChest.cs:88-104):

| Field | Value |
|---|---|
| solid | die box |
| onFloor | false |
| judgePart, fromCentre | true |
| speedScale | 0 |
| maxPush | 6 (PressCrushPush) |
| lift | 1.5 |
| replaceSpeed | true |
| knockdownSpeed | 3 |
| knockdownSeconds | 1.0 |
| cooldown | .5 |
| ghostSeconds | 1.0 (sit 0.4 + 0.6; a floored pawn passes through the die until it lifts) |
| alongShare | 1 along the lane: pushes go ±z, never into a wall |
| course / forwardShare | section root / .6 |

The static `RV press wave` carries VaultPressWave (§2.1).

**Timing** (4.5 s): sit 0.4 → rise 1.0 (smoothstep) → up 1.65 → warning 1.2 → fall 0.25. It lands at 30.4 m/s, so anything under the die is crushed out. The band spans the full 8 m neck, z 107..113.

- **Rules:** `PressAt`, `PressLowered`, `PressDieBottom`, `PressSinceLanding`, `PressLandsIn`, `PressSafeToCross`, `PressCue`.
- **Cue:** `Cue press band` with VaultCue(PressCue): Stop during the 0.3 s wave, Go while a runner may start, Warn from "too late to start" through the fall.
- **Weights:** wave 4.0 m/s (queen 2.0); crush 6.0 (queen 3.0).
- **Chess reading:** check. The band is the checked rank, and no piece may stay on it.
- **Sign:** §1.17.

### 2.7 V06 jewel belts ×2

**Component:** the static boxes `RV belt W/E` carry CourseFloor, NoClimb and `VaultBeltSurface(0/1)`.

Speeds (m/s along +z):

| Belt | Speed | Net at a run |
|---|---|---|
| West | −1.0 always | 1.5 |
| East, stopped half (5 s) | 0.0 | 2.5 |
| East, back half (5 s) | −1.5 | 1.0 |

The east belt switches every 5 s with 0.5 s smoothstep ramps. R518 holds: no belt pushes back faster than 1.5 m/s, and none ever moves forward.

**Cues** (built in the cue partial):
- `Cue belt W`: Stop always.
- `Cue belt E`: Go while stopped with more than 1 s left, Warn in its last second, Stop while it runs back.
- `Cue belt slat W/E k`, k = 0..6: Ink bars moved by VaultSlats from `BeltTravel`, so the motion is visible. On the east belt they stand still in its stopped half.

- **Rules:** `BeltSpeed`, `EastStoppedFor`, `EastWarn`, `BeltCue`, `BeltTravel`, `BeltRideSeconds`.
- **Chess reading:** captured pieces ride to the vault. On a belt going back you lose tempo, so ride the stopped belt.
- **Sign:** §1.17.

### 2.8 V07 knight launch pad

**Component:** Gameplay `LaunchPad` (LaunchPad.cs:31-212) on `RV pad launch`, built in **M0a** (it has no motion):
- BoxCollider trigger 1.6 × 0.6 × 1.6 m, bottom at y 4.0, so Surface = (6.0, 4.0, 84.6);
- `Configure(new Vector3(-1.0f, 4.0f, 8.7f), 1.9f, 0f, 0f, sameThrow: true)` (LaunchPad.cs:88): Landing (5, 8, 93.3).

How it throws:
- It throws on the first overlap of any of a pawn's colliders. It aims from `LaunchFrom = hips − standHeight` (LaunchPad.cs:159-202, RagdollDriver.cs:60).
- With sameThrow every pawn gets `ArcVelocity` toward Landing + (its ground point − Surface): up 10.76, forward 5.06 and west 0.58 m/s; 1.719 s in the air; apex feet y 9.9.
- The arc runs under 9.81 m/s² (LaunchPad.cs:202), matching launched pawns (RagdollPawn.PawnRushPhysics.cs:132-147).
- For hips caught up to 0.6 m outside the trigger (`PadCatch`), landings fall at x 3.6..6.4, z 91.9..94.7, inside the railed landing zone. The westward drift ends at the shelf's W rail.
- It throws only pawns this PC simulates; puppets ignore it (RagdollPawn.cs:684).

- **Rules:** `PadThrow`, `PadFlight` (tests only).
- **IntendedGap:** `RV pad flight` (§1.7).
- **Cues:** `Cue pad rim`, `Cue shelf lip` (gold).
- **Knock:** none; a launch sets velocity, so weight does not matter.
- **Chess reading:** the knight's leap, the only piece that jumps over others.
- **Sign:** §1.17.

### 2.9 V08 vault drawers ×2

**Component:** one `VaultMover` per drawer: root `RV drawer k` at (9.0, 8.95, DrawerZ[k]), `Configure(new Vector3(-4, 0, 0), t => RoyalVaultRules.DrawerOut(d, t))` with `d` the local copy of k.

Child `RV drawer box`: 4.0 × 1.8 × 2.4 m (y 8.05..9.85), N, CameraSeeThrough. In, it sits inside the cabinet; out, its face is at x 3, the open edge, so it sweeps the full width.

**Timing:** 4.8 s per drawer: rest 2.4, out 1.0 (peak 5.33 m/s), hold 0.4, in 1.0. Drawer 2 runs 1.5 s after drawer 1, a wave along +z at ≈ 2.5 m/s that a runner can follow after timing drawer 1. This is the design's V08 wave and a deliberate exception to P27, on an optional branch, for the P43 balance. The cell z 99.2..100.6 is never covered.

**Knock:** `YardKit.Shell(root, "RV drawer k (hit)", (−.15, 0, 0), (4.3, 1.9, 2.5), 3f)` with:
- solid = drawer box;
- **knockdownSpeed 99** (shove only);
- speedScale .4, maxPush 5.0;
- lift .3 (low, so the 4 m drop lands at 8.85 m/s, under 9);
- alongShare 1 (west, off the edge);
- replaceSpeed, cooldown .6.

A pushed pawn falls onto the belts (§1.7) and continues on the safe route. The face also sweeps a queen physically.

- **Rules:** `DrawerOut`, `DrawerFaceX`, `DrawerWarn`, `DrawerClearFor`, `DrawerCue`, `ForkTwo…`.
- **Cue:** `Cue drawer face k` (picture child of the drawer root at local x −2.01, .02 × 1.6 × 2.2) with VaultCue(DrawerCue): Warn in the last second of rest, Stop while out or moving, Off otherwise.
- **Chess reading:** open files — wait on a file the drawers have left open.
- **Sign:** §1.17.

### 2.10 V10 time-lock door (MUST) — listed before the plates on purpose

**Component:** `VaultMover` at root `RV door disc` (0, 15, 179) with:
- travel (9, 0, 0) and `along: t => DoorOpen(t)`;
- axis Vector3.forward and `degrees: t => -DoorOpen(t) * DoorTravel / DoorRadius * 180 / Math.PI`, so it rolls without slipping (negative roll for +x travel; RoyalVaultValidation P5 checks the sign).

Its children:
- `RV door disc body`: `YardKit.Cylinder(root, …, Vector3.zero, Vector3.forward, 9.0f, 1.0f, Greybox, Solid.NoClimb)` (convex, YardKit.cs:145-162), CameraSeeThrough while moving.
- `RV door spoke`: a picture 8.6 × .6 × 1.05 across the face, so the roll is visible.
- The door's **`ObstacleSpan`** (§1.15).

**Shove:** a sibling `VaultMover` **`RV door shove`** at the same root position, `Configure(new Vector3(9, 0, 0), t => DoorOpen(t))` with no rotation. Its only child is `YardKit.Shell(root, "RV door (hit)", (0, −1.9, 0), (8.2, 2.8, 1.6), 3f)`: axis-aligned, spanning y 11.7..14.5. Its leading face runs 0.07 m ahead of the rim at 1 m height and 0.5 m ahead at hip height. Settings:
- body Box (no solid);
- knockdownSpeed 99 (shove only);
- **alongShare 0**: out of the doorway along ±z, to the side the pawn is on;
- speedScale .3, maxPush 4.5, lift .5;
- replaceSpeed, cooldown .8;
- course + forwardShare .6.

Pawns in the doorway's front and back lips beside the disc face its z faces and are not hit; only the leading face shoves.

The disc is kinematic: it passes the static sill and walls without contact, and its bottom 1.5 m below the floor is never seen.

**Timing:** 15 s period: roll open 2.3 s (5 m/s cruise), open 3.1 s, roll shut 2.3 s, shut 7.3 s. It is passable (≥ 2.5 m clear at 1 m up) for 5.99 s, from 0.853 s into the opening roll to 1.447 s into the shutting roll.

- **Rules:** `DoorOpen`, `DoorClearWidth`, `DoorPassableFor`, `DoorOpensIn`, `DoorCountdown`, `DoorClosingWarn`, `DoorSign`.
- **Cue:** `Cue door countdown`, `Cue door countdown W`, `Cue door countdown E`, each with VaultCue text from `DoorSign`:

| State | Text |
|---|---|
| last 3 s before passable | "3" / "2" / "1" in ink |
| passable | "열림" green |
| closing warning | "닫힘" red blinking |
| otherwise | empty |

- **Chess reading:** the gate of rank 7. Pawns line up on rank 7, and the next square, the audience hall, is promotion.
- **Sign:** §1.17.

### 2.11 V09 flipping plates (MUST)

**Component:** `VaultPlate`, 10 instances (§1.9): row 0 W1..W5 (yaw 0), row 1 E1..E5 (yaw 180), each `Configure(row)` on the inactive Content.

**Timing:** 8 s per row: flat 5.2, raise 1.0 to 50°, hold 0.8, lower 1.0. The east row runs +4 s, so one row is always flat, and both are flat during [0, 1.2) and [4.0, 5.2).
- The inner edge rises to 3.04 m; its peak speed is 5.2 m/s.
- The rising edge moves away from the other row, so nothing pinches.
- Riders slide outward over the hinge into the pit and come back at CP4, which is right before the bridge.

- **Rules:** `PlateAngle`, `PlateFlatFor`, `PlateWarn`, `PlateFlat`, `PlateSlipAngle`, `PlateCue`.
- **Cue:** `Cue plate rim`, a picture child of each plate root along its inner edge, at (wide − .075, .012, long/2), size (.15, .02, long − .1), with VaultCue(PlateCue): Warn during the 1.6 s warning, Stop while tilted.
- **Knock:** none; slipping and the pull are weight-independent.
- **Chess reading:** change file — the file that rises is attacked; step onto the file lying flat. Words only.
- **Sign:** §1.17.

### 2.12 Cue summary (all built in `RoyalVaultBuilder.Cues.cs`)

| Cue | Function | Go | Warn | Stop | Off |
|---|---|---|---|---|---|
| tower rims; forecourt, rest and J1 lips; pad rim; shelf lip | static | — | — | — | static CueGold |
| bulkhead floor b W/E | `BulkheadCue` | half clear ≥ 1 s | last second | blocked | — |
| hammer lamp i | `HammerCue` | 1 s after it leaves | last second before body height | at body height | rest |
| press band | `PressCue` | a runner may start | too late → fall | wave 0.3 s | — |
| belt W / E | `BeltCue` | E stopped (> 1 s left) | E last stopped second | W always; E running back | — |
| belt slats | `BeltTravel` (VaultSlats) | — | — | — | Ink, moving |
| drawer face k | `DrawerCue` | — | last second of rest | out or moving | in |
| plate rim | `PlateCue` | — | warning | tilted | flat |
| door countdown ×3 | `DoorSign` | "열림" | "닫힘" blinking | — | digits in ink, or empty |

---

## 3. Files

### 3.1 New files

Run `python3 Tools/Generators/mkmeta.py <path>` (`py -3` on Windows) from the project root for each file under Assets. Its GUID is md5("chessfight:" + path) (mkmeta.py:5-7); the values below were recomputed. Commit every .meta (HANDOFF rule 11).

| Path | Responsibility | .meta GUID |
|---|---|---|
| Assets/Scripts/Core/RoyalVaultRules.cs | §2.0 | b6bf88a2710c229b621fa0cd22e3d6b8 |
| Assets/Scripts/PawnRush/RoyalVaultSection.cs | root component | 153901132638ef3b1ca07c7d3ee48e73 |
| Assets/Scripts/PawnRush/RoyalVaultBuilder.cs | `Build`, shell, roof, helpers (FloorNC, Rail, Block, Picture, Pier, copied FlightZ/Walk/RailAlongZ/V), Start child | df3c792bf8a8676acfcc3b5999d42147 |
| …/RoyalVaultBuilder.Fork1.cs | forecourt, towers, rest, corridor, hurdles, bulkheads, J1 | f06da01687f3cda5c3a971c086ee168b |
| …/RoyalVaultBuilder.Hammers.cs | walkway, dials, hammers | c8076abcbe685bd48e285ca4de1d42a7 |
| …/RoyalVaultBuilder.Fork2.cs | approach, pad, belts, landing, cabinets, press, stair 2, shelf, drawers | 6b000cf3e7daf1ca398e451b578f2057 |
| …/RoyalVaultBuilder.Bridge.cs | J2, plates, stair 3 | 67aa136f971affcb2fb98581d113189a |
| …/RoyalVaultBuilder.Door.cs | terrace, door wall, disc, shove, room, arch, "7" | 719612157d5e883298d6e1d88c9cbb39 |
| …/RoyalVaultBuilder.Course.cs | CPs (with the CP0 reach box), kill volumes, ProgressPath, spans, IntendedGap, beats, signs | 678fd688058f9766d9f8154ef6ddc953 |
| …/RoyalVaultBuilder.Cues.cs | every `Cue …` object (§2.12) | 22939d4a88ec241ce904bf444a32a630 |
| Assets/Scripts/PawnRush/RoyalVaultLook.cs | materials, runtime kit, sign | c019d5387e3fffa731c281f6efd263bf |
| Assets/Scripts/PawnRush/VaultMover.cs | §2.1 | 2ff21dbb1be92d343a910ee8cfde5ac3 |
| Assets/Scripts/PawnRush/VaultPlate.cs | §2.1 | 0e71ba1ef13bd72465f65b034d3acc47 |
| Assets/Scripts/PawnRush/VaultPressWave.cs | §2.1 | bcbda594658f3a59584d8b4d9e6f9b86 |
| Assets/Scripts/PawnRush/VaultBeltSurface.cs | §2.1 | e07e1a3ee89c20c0d6537fbd7086aa36 |
| Assets/Scripts/PawnRush/VaultCue.cs | §2.1 | 27f9f904e23c73df469ff95513043c21 |
| Assets/Scripts/PawnRush/VaultSlats.cs | §2.1 | 179067c9710d3606bcf9407c8cd39d25 |
| Assets/Prefabs/PawnRushSections/Late_3-3_RoyalVault.prefab | root-only section prefab (§3.3) | 43b7501a58bad1451f94d5cf7bb95486 |
| Assets/Prefabs/PawnRushMaps/RoyalVault.prefab | map-test twin (§4.1) | 6e8b5fe843514e3900619ce619903d18 |
| Assets/Scripts/PawnRushEditor/RoyalVaultValidation.cs | Edit and Play checks (§5.4) | c576e3d6f5f99bb182a51204886a9208 |
| Assets/Scripts/PawnRushEditor/RoyalVaultBotTest.cs | bot harness (§5.2) | 735944b62d33d47b6487b40239b07a74 |
| Tools/Run-RoyalVault.ps1 | sync the copy, run ValidateEdit / Validate / Bots; **UTF-8 with BOM** (PITFALLS #58) | — |
| Docs/PawnRush/PAWN_RUSH_ROYAL_VAULT.md | as-built design, this spec's numbers, deviations, how to test | — |

Notes:
- One MonoBehaviour per file of its name (PITFALLS R312).
- All code stays in `Assets/Scripts/PawnRush` and `PawnRushEditor`, which Test-NetworkCompile.ps1 and the Linux compile see.
- The PawnRush assembly references only Game, Gameplay, Network.Core and VFX: use IHitReceiver, ICharacterDriver, IPromotable and ImportedObstacleActor, never RagdollLab types.

### 3.2 Changed files

**Before the pool step (M0a)**

`Assets/Scripts/PawnRush/SectionValidator.cs`, `MustBeUnclimbable` (:18-27): add

```
// The royal vault (3-2, R692): every solid is "RV …" and nothing there is climbable.
"RV ",
```

`KitOf` (:96-104) stays **unchanged** and returns null for the vault, which CheckClimbing handles (Course01Validator.cs:266-280). Returning the vault's kit would be wrong: every material is Greybox, so every NoClimb renderer would read as "climbable stone with NoClimbSurface". The no-climb rule is enforced by the "RV " prefix and RoyalVaultValidation E3.

`Assets/Scripts/PawnRushEditor/SectionPrefabMenu.cs`, `Report` (:170-176): add

```csharp
var vault = go.GetComponent<RoyalVaultSection>();
if (vault != null && !vault.HasContent) RoyalVaultBuilder.Build(vault);
```

Also update the header comment (:10-23). The line takes effect once the vault is in the set; before that, RoyalVaultValidation calls `SectionValidator.Run` itself.

`Assets/Scripts/PawnRushEditor/SectionCorridorValidation.cs` (:293-322):
- `BeltStart`: when `round.Sections` holds no `WarRoomSection`, log `"slot 5 drew <id>, no war-room conveyor to check"`, set `belt = null` and return without a failure.
- Take the conveyor picture from the section that has the `WarRoomSection`, not `Sections.LastOrDefault()`, which has been the ending since R688.

`Assets/Prefabs/PawnRushMaps/README.md`: add the row "`RoyalVault` — 왕실 금고실 회색 박스(R692), 구간 프리팹 `Late_3-3_RoyalVault`의 맵 시험 쌍둥이(우클릭 → ChessFight → Test In Pawn Rush Map Test)".

Docs, per HANDOFF §8 and :508-515:
- `Docs/PawnRush/PAWN_RUSH_SECTIONS.md`:
  - code table;
  - a vault subsection like the war room's (:194-211);
  - validators;
  - mark slot 5 "candidate, not in the pool yet";
  - fix :99 ("six corridors" → five) and :103 (the exit is the band end only before a mini-game).
- `Docs/PawnRush/PAWN_RUSH_MAP_TEST.md`.
- `Docs/PawnRush/README.md`: the 7-slot table (:18-28) is stale.
- `Docs/Project/REQUIREMENTS.md` (R692).
- `Docs/Project/DECISIONS.md`: new P56, "왕실 금고실 = 슬롯 5 둘째 후보, 회색 박스 먼저, 갈림 P43 두 번(둘째 갈림은 Core 시간 모형으로 고정), 판 밖 장치는 시계 순수 함수, 서랍 물결은 P27의 의도된 예외".
- `Docs/Network/VALIDATION.md`: a user-unverified table at the top.
- `Docs/Project/HISTORY.md`.
- `HANDOFF.md`: §1 row, §3 next steps, §6 routing row "왕실 금고실 → PAWN_RUSH_ROYAL_VAULT.md → RoyalVault*·Vault*·Core RoyalVaultRules·RoyalVaultBotTest".
- `Docs/Environment/PITFALLS.md` for new pitfalls: Edit-mode Awake, lambda capture, static spans on resting movers.

**Gated pool step** (§4.2):
- `Assets/Settings/PawnRushSections.asset`
- `Assets/Resources/Loading/PawnRushCourse.txt`
- `Assets/Scripts/Network/SteamSession.cs`
- `Tests/Network/NetworkCoreTests.cs` (test 14)
- `Assets/Scripts/PawnRush/SectionCorridor.cs` (FramesItsEnd)
- `Assets/Scripts/PawnRushEditor/SectionCorridorValidation.cs` (a picture of the vault's exit)
- docs

Not touched: `WarRoomEndingValidation` (seed 11 still draws the war room), `MiniGamePrefabValidation`, slot and Core round code.

### 3.3 Prefab YAML

Copy the layout of Final_AudienceHall_Temp.prefab:1-70 (checked). Values **must equal Describe** (PITFALLS #68).

`Late_3-3_RoyalVault.prefab`:
- GameObject &1000, `m_Name: Late_3-3_RoyalVault`;
- Transform &1001 at identity;
- MonoBehaviour &1002 with `m_Script: {fileID: 11500000, guid: 622fdb55513c2b2da37f7d58a616ba00, type: 3}` (PawnRushSection) and:

```
  id: Late_3-3_RoyalVault
  displayName: "\uC655\uC2E4 \uAE08\uACE0\uC2E4"
  kind: 0
  entry: {x: 0, y: 0, z: 0}
  exit: {x: 0, y: 12, z: 196}
  exitYaw: 0
  beats: []
  slot: 0
  seed: 1
  teamSize: 6
```

- MonoBehaviour &1003 with `m_Script: {fileID: 11500000, guid: 153901132638ef3b1ca07c7d3ee48e73, type: 3}` (RoyalVaultSection) and `buildIfEmpty: 1`.

`Assets/Prefabs/PawnRushMaps/RoyalVault.prefab` is identical except `m_Name: RoyalVault` and its own meta GUID. The map test lists it by itself (PawnRushMapTest.cs:97-112).

### 3.4 Builder entry (pattern WarRoomSection / GrandStairHallBuilder)

```csharp
public static void Build(RoyalVaultSection vault)
{   var root = vault.transform;
    var old = root.Find(RoyalVaultSection.ContentName); if (old != null) Object.DestroyImmediate(old.gameObject);
    var content = new GameObject(RoyalVaultSection.ContentName); content.SetActive(false); content.transform.SetParent(root, false);
    var b = new CourseBuilder(RoyalVaultLook.Kit, content.transform);
    Shell(b); Fork1(b, root); Hammers(b, root); Fork2(b, root); Bridge(b); Door(b, root); Course(b); Cues(b, root);   // partials
    vault.GetComponent<PawnRushSection>().Describe(RoyalVaultSection.Id, RoyalVaultSection.DisplayName,
        PawnRushSection.SlotKind.Obstacle, RoyalVaultSection.Entry, RoyalVaultSection.Exit, 0f, Beats());
    content.SetActive(true);   // Obstacles wake here with their final world pose (Obstacle.cs:45-60)
}
```

Rules for the builder:
- Every `Configure` and `SetPhase` runs while `content` is inactive.
- No Obstacle is nested in another; the door's shove mover is a sibling of the disc.
- No static floor sits under a mover; only cue pictures without colliders are children of movers.
- `Cues` runs last, so plate and drawer roots exist.

| API | Where |
|---|---|
| CourseBuilder Box / Floor / Wall / Rail / Cylinder / Trigger / Checkpoint / Slope | CourseBuilder.cs:47-196 |
| YardKit Box / Span / Cylinder / Ring / Picture / Label / Shell | YardKit.cs:103-279, 420-432 |
| YardImpact fields | YardImpact.cs:39-91 |
| Spinner | Spinner.cs:14 |
| LaunchPad | LaunchPad.cs:88 |
| ProgressPath.Configure | ProgressPath.cs:23 |
| IntendedGap.Configure | IntendedGap.cs:15 |
| ObstacleSpan.Configure | ObstacleSpan.cs:33 |
| PawnRushSection.Describe | PawnRushSection.cs:97 |
| Checkpoint reach pattern | RoyalCarriageYard.Sections.cs:176-186 |

### 3.5 Core tests

Add these to `Tests/Network/NetworkCoreTests.cs` as `Test("Royal vault …", () => …)` blocks after the sorting-yard tests (around :893). Use class-level helpers only. Sample from clock 0 **and** 1762000003.25 (the :766-767 pattern). Each test lands in the milestone that builds its device.

| # | Milestone | Test | What it pins |
|---|---|---|---|
| 1 | M0a | Royal vault towers (R692) | TowerGap 0.85 ± 1e-9 (≤ 1.5); (TowerCount+1)·TowerStep = 4. TowerZ rising, with the first and last gaps equal to TowerGap. Lanes 0.8 m apart; diagonal ≥ 2.5. JumpAirSeconds(0.5) = 0.574 ± .002; 0.94 × run reach − gap ≥ 0.3. TowerGapOffLine(0.8) ≤ 0.94 × reach − 0.1; EdgeGapOffLine(TowerTakeoffHalf) ≤ 0.94 × reach − 0.15 |
| 2 | M0a | Royal vault layout and route | Route P3..P9 equal (TowerLaneX, TowerTop(k), TowerZ(k)); length 200.9 ± 0.3; P0 = entry, P27 = exit; z non-decreasing. CheckpointZ ascending; CP3 (83.6) ≤ PadZ − PadSize/2 (83.8). Hazard intervals on each branch ≥ 2.5 m apart between different devices, cells excepted: hurdles 2.6, hurdle 2 → bulkhead 1 2.55, hammers' cell 5.05, belts' end → press band 4.0. Pad's farthest landing (PadLandZ + PadSize/2 + PadCatch = 94.7) ≤ the drawer-1 hit range (96.5) − 1.5. ShelfOpenZ1 ≤ BeltZ1 + 0.3; ShelfOpenZ0 ≥ the farthest landing + 1.5 |
| 3 | M1 | Royal vault hurdles | dps +110 / −100 (≥ R680's 105/95). Zones (hub ± 3.95) ≥ 2.5 m apart and ≥ 2 m past z 8. After waiting `HurdleArrivesIn`, the bar angle equals the point's angle mod 180 within 0.05° (2000 random points and times). HurdleJumpWindow non-empty for r ≥ 1.6 at both speeds |
| 4 | M1 | Royal vault bulkheads | Over 2 cycles at both clocks: clear width 8 − 4·(Out(W)+Out(E)) ≥ 3.99 always. Each half fully open 1.7 s per cycle; bulkhead 2 = bulkhead 1 shifted 3 s; peak panel speed ≤ 4.5 m/s. max BulkheadClearFor(±2) = 2.84 ± .05; BulkheadCue Go only while ClearFor ≥ 1 |
| 5 | M2 | Royal vault dial hammers | HammerBodyHalfAngle 58.22 ± .05; window 1.94 ± .02 s per 6 s turn; hammer 2 half a turn behind. At angle ±1° the head's z velocity is negative. Zones do not overlap and leave a cell ≥ 4.5 m. A runner starting at SinceClear 0 crosses 2·ZoneHalf before ClearFor ends. HammerCue Stop exactly at body height |
| 6 | M3a | Royal vault crown press | Stages sum to 4.5 s. PressLowered continuous (|Δ| ≤ .05 per 10 ms except the fall, which reaches 1 at the landing). For every start e in [0.30, 2.00) in 0.05 s steps a runner is never in the band at a landing and never under the die while PressDieBottom < 1.5. PressSafeToCross agrees with that simulation. Band width = PressDieAcross = neck 8. PressCue: Stop only in the wave, Go only when PressSafeToCross at a run |
| 7 | M3a | Royal vault belts | West −1.0; east 0 / −1.5 in 5 s halves; no speed ever below −1.5 or above 0 (R518). Continuous (|Δv| ≤ .06 per 10 ms). EastWarn in the last second of the stopped half. The numeric derivative of BeltTravel equals BeltSpeed within 1e-3 at both clocks. BeltRideSeconds within 0.05 of a 1 ms integration |
| 8 | M0a | Royal vault pad | PadThrow up 10.76 ± .02, forward 5.06 ± .02, 1.719 ± .005 s. For ground points on the trigger's edge expanded by 0, 0.3 and 0.6 m (13 per side), with sameThrow offsets: feet ≥ 0.5 m above the lip (z 91, top 8.0), the shelf E rail (x 7..7.3, z 91..96.3, top 9.2), the belt E rail (x 4..4.3, z 89..91, top 5.2) and the approach N-E rail (x 4.3..14, z 89..89.3, top 5.2) wherever the body (±0.3) overlaps them in flight. Landing x 3.3..6.7, z ≥ 91.5 and ≤ 94.8; landing floor impact /1.27 < 9 |
| 9 | M3b | Royal vault drawers | Cycle 4.8; drawer 2 = drawer 1 shifted 1.5 s; peak face speed ≤ 5.5. The cell (99.2..100.6) is never covered. DrawerWarn in the last second of rest. DrawerFaceX within 3..7. max DrawerClearFor(·, 5) = 3.3 ± .05 |
| 10 | M3b | Royal vault fork 2 balance (model) | Means over t0 = c + 0.37k, k < 200, at both clocks: ForkTwoSafe − ForkTwoRisky ≥ 1.5 s at 2.5 m/s (model 1.9), ≥ 0.3 at 3.1 (0.6), ≥ −0.6 at 3.6 (−0.2); ForkTwoDropCost ≥ 1.0 at 2.5 (1.4) |
| 11 | M4 | Royal vault flipping plates | At every sample one row has angle 0; both rows flat 1.2 ± .01 s twice a cycle. Peak angular speed × 3.97 m ≤ 5.5 m/s. FlatFor reaches 0 exactly when the angle leaves 0. Warn starts 1.6 s before. PlateWarnSeconds + (time to 12°) ≥ 0.25 + (PlateWide + 0.03)/RunSpeed |
| 12 | M5 | Royal vault door | The disc (r 4.5) covers the octagon (circumradius 4.22) by ≥ 0.25. Passable 6.0 ± .05 s per 15 s; mean wait 2.7 ± .1, longest 9.0 ± .1. Peak roll speed ≤ 5.1 m/s. Countdown 3 → 2 → 1 ends exactly at passable; ClosingWarn 1.5 s before the end; DoorSign texts in order |
| 13 | M1 → M5 | Royal vault clock | Every device function gives the same value at s and at s + m·P (P its own period, m·P ≈ 1.76e9) within 1e-6 (angles 1e-4°). Extended as devices land |

Core goes from 199 to 212 by M5; the session tests stay at 98. The pool step adds test 14 (§4.2).

---

## 4. Playing it before the pool, and the gated pool step

### 4.1 Least invasive way: the map-test twin (no scene, set or protocol change)

The editor map test lists **every prefab in `Assets/Prefabs/PawnRushMaps`** by itself (PawnRushMapTest.cs:97-112), so `RoyalVault.prefab` appears without touching `PawnRush_MapTest.unity`. The Section Test cannot draw the vault before the pool step, because PawnRushRound has no forced pick (PawnRushRound.cs:25-38).

User steps (they also go into PAWN_RUSH_MAP_TEST.md and VALIDATION):
1. **One click:** Project window → `Assets/Prefabs/PawnRushMaps/RoyalVault.prefab` → right-click → **ChessFight → Test In Pawn Rush Map Test**. For a prefab inside the map folder this stores its path in PlayerPrefs, opens the scene and enters Play, with **no scene write** (PawnRushMapTestMenu.cs:46-56).
   **Never right-click `Late_3-3_RoyalVault`** (outside the folder): that adds it to the scene's `maps` list and saves the shared scene (:47-55, 60-73; HANDOFF rule 12).
2. Alternative: **ChessFight → Pawn Rush → Open Map Test** → **Play** → **]** / **[** until the top-left reads **RoyalVault**. The next Play reopens it.
3. The pawn starts at the `Start` child (0, 0, 3) with Pawn Rush movement.

| Key | Does |
|---|---|
| 1~6 | pawn, knight, bishop, rook, queen, king (PawnRushMapTest.cs:274-275): try promoted pieces on hammers, press, drawers, door |
| T | put the pawn where the camera looks: jump to a beat |
| F2 | overview |
| F3 | slow motion |
| R | last checkpoint |
| Backspace | start again |
| F5 | build the vault again with the code already loaded (it re-instantiates the prefab, :271). After a code change stop Play so Unity recompiles, then Play. |

Standalone, the vault plays as slot 0 (checkpoints 0..6) with one pawn offline, no corridors and no other players.

### 4.2 Gated pool step: runs only after the user says yes

Before it:
- M6 is accepted;
- the user has played the twin;
- SectionCorridorValidation's belt check is already conditional (done in M0a).

1. `Assets/Settings/PawnRushSections.asset`: under slot index 4 ("장애물 3 (후반 · 윗궁)"), **after** `- {fileID: 1000, guid: 059960ab5c944997acc8d8e393fd203d, type: 3}` (Final_WarRoom), add:
   `    - {fileID: 1000, guid: 43b7501a58bad1451f94d5cf7bb95486, type: 3}`
   Appending keeps slots 1–4 the same for every seed, because Next(1) draws no number (SeededSchedule.cs:36). The ported draw gives the vault 354 of 700 seeds; seeds 1, 2, 11 and 23 stay on the war room, and 5, 6, 10 and 12 draw the vault. Confirm in Unity.
2. `Assets/Resources/Loading/PawnRushCourse.txt`: after `4<TAB>Final_WarRoom<TAB>국왕 작전실` add `4<TAB>Late_3-3_RoyalVault<TAB>왕실 금고실` (UTF-8 without BOM, LF in the repo). Unity's PawnRushCourseTableWriter writes the same; commit it.
3. `Assets/Scripts/Network/SteamSession.cs:158` → `"chessfight.dua0731.network.v51"`, with a comment in the :150-157 style:
   `// v51 (R692): the late obstacle section is drawn from two rooms - the war room or the royal vault (appended after it, so slots 1-4 draw as before). A v50 PC would build the war room where a v51 host builds the vault.`
4. `Assets/Scripts/PawnRush/SectionCorridor.cs:98` → `static bool FramesItsEnd(PawnRushSection section, bool exit) => exit ? section.Id == RoyalVaultSection.Id : section.Id == GrandStairHall.Id;`. The vault's "7" arch 4 m before its exit is its frame, so the corridor to the ending builds no second portal (MiniGamePalaceArt.Corridor.cs:25). The id is serialized and readable before the section wakes (PITFALLS #68). Add a picture of the vault's exit join to SectionCorridorValidation.
5. Core test 14, "Royal vault pool": `PawnRushSections.Choose` with the real slot sizes (2, 3, 1, 7, 2, 1) over seeds 1..700. Slot 5 draws each room 40–60 % of the time, and slots 1–4 equal a one-candidate slot 5 for every seed.
6. Checks:
   - Test-NetworkCore and Test-NetworkCompile;
   - in the copy: SectionCorridorValidation `-corridorSeeds 11,12` (war room, then vault), WarRoomEndingValidation `-warRoomSeed 11`, MiniGamePrefabValidation;
   - Validate Sections, which now builds the vault;
   - RoyalVaultValidation Play option B: a whole round with a vault seed such as 12, logging which room slot 5 drew.
7. Docs: PAWN_RUSH_SECTIONS §3 slot-5 row; HANDOFF §1 and §6; VALIDATION (a 6 v 6 v51 build, user-unverified); HISTORY.
8. The user plays it:
   - **Open Section Test → Play**, then **]** to slot 5;
   - offline, **F8** alternates war room and vault (lastPicks, PawnRushRound.cs:41, 138-139), or set `PawnRushRound.seed` to a vault seed;
   - then a 6 v 6 player build.

---

## 5. Build order, bot harness, validators

### 5.1 Milestones

Every milestone ends with the same checks:
- (a) `./Tools/Test-NetworkCore.ps1` and `./Tools/Test-NetworkCompile.ps1 -SteamRuntimeSources Library/PackageCache/com.rlabrecque.steamworks.net@6fb66c768572/Runtime`;
- (b) `CHESSFIGHT_TEST_OUT=<scratch> bash Tools/run-tests-linux.sh --checks-only` in Git Bash (obstacle clock, boundaries);
- (c) `./Tools/Run-RoyalVault.ps1 -What ValidateEdit`, then `-What Validate`, in the copy (from M0b on);
- (d) the beat probe;
- (e) docs, then a commit plus push to main ("R692 M<n>: …"), marked user-unverified.

The copy is at R662, and its first sync re-imports for a long time, so run it in M0b first.

**M0a — skeleton and towers (commit).**
- Rules and tests:
  - RoyalVaultRules frame/layout constants, CheckpointZ, Route, PitBottom, Cue, Blink, the V01 and V07 blocks;
  - Core tests 1, 2 and 8.
- Components and prefabs:
  - RoyalVaultSection, RoyalVaultLook, VaultMover, VaultCue;
  - both prefabs.
- Builder — every static piece of §1:
  - shell, roof, piers, floors, rails;
  - towers and rest;
  - corridor with static pockets;
  - stairs, J1, walkway, dials;
  - approach with the **static pad** (LaunchPad, IntendedGap, rim cue);
  - belts as static floors (no VaultBeltSurface);
  - landing, cabinets, neck, press frame and head (no die);
  - shelf with rails and the drawer cabinet (no drawers);
  - J2;
  - plates as static decks named `RV placeholder plate W1..E5`;
  - stair 3, terrace;
  - door wall with the octagon (open doorway), room, arch, "7";
  - CPs with the CP0 reach box, kill volumes, path, beats, static spans, signs, static gold cues, Start.
- Tool edits: the SectionValidator, SectionPrefabMenu and SectionCorridorValidation edits.
- RoyalVaultValidation: E1–E4, E9, E14.
- The user can walk the twin.

**M0b — harness (commit).**
- Build: Run-RoyalVault.ps1; the RoyalVaultBotTest lifecycle, scene setup, falls and records; `-vaultBotBeat walk`; trace.csv.
- Probe `walk`: first one bot per policy × 2 seeds, then 12 bots × 3 runs. Acceptance:
  - 0 stuck, flung or pinned;
  - **tower crossing, the riskiest number, probed first**: Jumper completes the 8 jumps without a fall in ≥ 65 % of tries;
  - leader time without hazards 60–75 s.

**M1 — fork 1 devices (V02, V03).**
- Build: hurdle and bulkhead rules, Core tests 3, 4 and 13 (started), hurdles (Spinner, YardImpact, spans), bulkheads (VaultMover, shells, merlons), their cues.
- Probe `fork1`: spawn on the forecourt, finish at J1 z ≥ 51.

**M2 — hammers (V04).**
- Build: rules, Core test 5, Spinner, head, shell, lamp.
- Probe `hammers`: spawn on J1, finish at z ≥ 84.

**M3a — safe fork 2 (V06, V05).**
- Build: belt and press rules, Core tests 6 and 7, VaultBeltSurface, slats and belt cues, press die, crush shells, VaultPressWave, band cue. The shelf stays static; the pad works, but the bots are sent on the safe branch.
- Probe `fork2safe`: spawn on the approach, finish on J2 z ≥ 128. Measure press waits, downs and pinches and belt time, and compare with ForkTwoSafeSeconds.

**M3b — risky fork 2 (V08 with V07).**
- Build: drawer rules, Core test 9 and the fork-2 model test 10, drawers and face cues.
- Probe `fork2risky`, plus the fork-balance row against M3a's measured safe times.

**M4 — plates (V09).**
- Build: plate rules, Core test 11, VaultPlate ×10 replacing the placeholders, rim cues.
- Probe `plates`: spawn on J2, finish at z ≥ 157.

**M5 — door (V10).**
- Build: door rules, Core tests 12 and 13 (complete), disc and shove movers, countdown cues (top plus two copies), door.csv and per-beat pictures.
- Probe `door`: spawn on the terrace, finish at z ≥ 195.5.

**M6 — whole vault and tuning.**
- Bots `full`: 12 bots × 4 runs × seeds 7 and 23, pawns; then `-vaultBotPieces mix` × 2 runs.
- Tune only through §6's knobs, with the Core tests first.
- E14 fails if any `RV placeholder` remains.
- Ask the user to play the twin; write the results up in PAWN_RUSH_ROYAL_VAULT.md.

**M7 — pool (gated).** §4.2, only after the user's yes.

### 5.2 Bot harness: `RoyalVaultBotTest` (lifecycle copied from CarriageYardBotTest)

**Lifecycle and arguments**
- Copy the `[InitializeOnLoad]` static constructor, `RunPlayBatch`, `BeginPlayWhenIdle`, `StopPlay` and `CompleteExit` (CarriageYardBotTest.cs:42-160, 1099-1117).
- SessionState keys `ChessFight.R692.VaultBots.Play/Exit/Code`.
- Menu `ChessFight/Pawn Rush/Validate/Royal Vault Bots (Play)`.
- Output `Logs/RoyalVaultBots`.
- Arguments:
  - `-vaultBotRuns 1..10` (4);
  - `-vaultBotSeed` (7);
  - `-vaultBotBeat walk|fork1|hammers|fork2safe|fork2risky|plates|door|full` (full);
  - `-vaultBotPieces pawn|mix` (pawn);
  - `-vaultBotNoCapture`.
- Never pass `-quit`. Force-reimport the section prefab first (CarriageYardBotTest.cs:131-134).

**Scene setup**, in this order, before anything spawns:
1. `new GameObject().AddComponent<PhysicsProfile>()`: 120 Hz, 24 iterations (PhysicsProfile.cs:17-37).
2. `CourseSkills.PawnRush = true`, and add `PawnRushCourseSkills` by type name (CarriageYardBotTest.cs:256-266).
3. Sky, sun 1.2 at (50, −30), capture camera (:267-278).
4. Write `Time.fixedDeltaTime`, `Physics.defaultSolverIterations` and the "Pawn Rush movement and skills … added" line into report.txt.
5. Option A: instantiate `Late_3-3_RoyalVault.prefab` under an **inactive** holder at the origin, `section.Configure(4, seed, 6, ObstacleClock.Now)` (PawnRushSection.cs:109), then activate.
6. **Startup reachability assertion**: each threshold below must be ≤ its query's maximum over one period, sampled at 0.02 s, or the run fails at once. Log both numbers.

All bot logic works in the section frame (`section.transform.InverseTransformPoint`) and reads every number from RoyalVaultRules.

**Bots**
- 12 `RagdollPawn.prefab` bodies; teams alternate (:297-313).
- Policies: **Run** 0–3, **Sprint** 4–7, **Jumper** 8–11. `Sprint = policy != Run` (:396), so Jumper is a careful sprinter.
- `mix` promotes after spawning with `GetComponentInParent<IPromotable>().Promote(...)` (IPromotable.cs:14):

| Bots | Piece |
|---|---|
| 2, 6, 10 | queen |
| 3, 7 | rook |
| 11 | knight |
| 1 | bishop |
| the rest | pawn |

Spawn grids, 2 rows × 6 spots, 1.5–2 m apart:

| Probe | Spawn | Seed highest CP |
|---|---|---|
| walk, fork1, full | forecourt z 4 and 8, x ±1.5 / 3 / 4.5 (PawnRushRound.StartSpots) | CP0 |
| hammers | J1 z 53 and 56, x −8..2 | CP2 |
| fork2safe, fork2risky | approach z 85 and 86.5, x −12..−2 | CP3 |
| plates | J2 z 128.5 and 131 | CP4 |
| door | terrace z 170 and 172 | CP5 |

**Branch choice** (every combination per policy per run):
- `risky1 = (index + run) % 2 == 0`;
- `risky2 = (index / 2 + run) % 2 == 0`;
- tower lane east when `(index / 2) % 2 == 0`.

The risky-1 bots split 3 east / 3 west. `fork2safe` forces safe and `fork2risky` forces risky. Report the leader per combination.

**Waypoints and decisions** (section frame; a waypoint is reached within 1.5 m flat):
- **Fork 1, risky:** (L, 0, 16.5), then jump nodes at the forecourt edge, rows 1..7 and the rest, then (L, 4, 51.5).
  - **Jump** when on feet and either the hips are within **0.2 m** of the current top's edge toward the next, or a down-ray 0.25 m ahead of the hips stops hitting the current top. Aim at the next top's centre.
  - Jumper also waits while another bot is within 1.3 m of the next centre, and switches lane on the rest if the other lane is free.
  - Log each jump's take-off and landing distance in trace.csv.
- **Fork 1, safe:** (−10, 0, 5) → (−16, 0, 5) → (−16, 0, 9) → hurdle 1 → (−16, 0, 19) → hurdle 2 → wait line (−18 ± 2, 0, 30.0) → bulkhead 1 → cell (x_half, 0, 33.4) → wait line z 34.0 → bulkhead 2 → (−18, 0, 38) → (−18, 4, 50.5) → (−4, 4, 55).
  - Hurdles (Sprint, Jumper): press jump when `HurdleArrivesIn(h, dx, dz, t)` is in `HurdleJumpWindow(h, r)`, r = the hips' distance from the hub.
  - Bulkheads: Jumper goes to the half with the larger `BulkheadClearFor(b, ∓2, t)` once it is ≥ 2.1/2.5 + 0.3; Sprint once ≥ 2.1/3.6 + 0.1. Run walks blind at the middle.
- **Hammers:** wait line (0, 4, 61.5) → (0, 4, 71.5) → wait → (0, 4, 82.5).
  - Jumper goes when `HammerClearFor ≥ 2·ZoneHalf/2.5 + 0.6` (3.38); Sprint when ≥ 2·ZoneHalf/3.6 + 0.3 (2.23). Run is blind.
- **Fork 2, risky:** (5.0, 4, 84.0), where the pad throws; steering is ignored until on feet above y 7. Then (5, 8, 95.0) → drawer wait lines z 96.45 and 100.25 → (5, 8, 104.5) → (5, 8, 125.5) → (3, 8, 129).
  - At each drawer, Jumper goes when `DrawerClearFor(i, 5, t) ≥ 3.0/2.5 + 0.3`, Sprint when ≥ 3.0/3.6 + 0.15. Run is blind.
  - **drop_to_belt** counts only when the bot lands on its feet at y 4 ± 0.6 inside x −6..4, z 89..103.5 within 2 s of being above y 7. It then switches to the safe branch from the nearest belt waypoint.
- **Fork 2, safe:** (x_b, 4, 89.5) → (x_b, 4, 102.5) → press wait line (−2, 4, 106.4) → (−2, 4, 113.8) → (−2, 8, 126.5) → (−2, 8, 129).
  - Belts: Jumper and Sprint take the belt with the smaller `BeltRideSeconds(belt, t, 3.1, 14)` at the mouth (x_b = −3.5 or 1.5). Run takes the belt on its side (index % 2).
  - Press: Jumper when `PressSafeToCross(t, 6/2.5 + 0.4)`, Sprint when `PressSafeToCross(t, 6/3.6 + 0.2)`. Run is blind.
- **Plates:** wait line (x_row, 8, 135.5), then nodes every 4 m at x_row = ∓0.8 (the inner side; a switch is 1.6 m).
  - Jumper enters a row if `PlateFlatFor(row) ≥ 2.0` and switches when its own FlatFor ≤ 1.2 and the other's ≥ 1.5. Sprint uses 1.2 / 0.8 / 1.0.
  - Run stays on the west row and steps across only once its row's angle > 0.
  - Then (0, 8, 156.5).
- **Stair, terrace, door:** (0, 12, 168.5) → door wait line (0, 12, 176.5) → when `DoorPassableFor ≥ 1.5` (Jumper) / ≥ 0.8 (Sprint) / blind (Run) → (0, 12, 181.5) → finish at local z ≥ 195.5.
- **Blind give-up:** after 2 falls or 3 knockdowns at the same beat, any policy uses the Jumper rules there (counted).
- **Unstick:** as in the yard (:798-807).

**Falls**
- On `KillVolume.Entered`, wait `RespawnDelay` (1.5 s).
- Then `EditorApplication.delayCall` teleports the bot (`ICharacterDriver.Teleport`) to spot `index % 6` of its highest reached checkpoint. That is kept idempotently from `Checkpoint.Reached` (Checkpoint.cs:31-33) and seeded per probe.
- Never teleport inside the callback (rule 14).
- Resume from that checkpoint's waypoint and count falls and time lost per beat.

**Watch**
- **Flung**: judged only while the hips are at or above the beat's lowest floor − 1 m, and not between KillVolume.Entered and the respawn. It is horizontal speed > 10 m/s, or upward speed > 8 m/s, or hips more than 3 m above the beat's **highest** floor (0 forecourt; 4 towers, corridor, J1, hammers, approach; 8 belts and press, shelf, J2, plates; 12 terrace, door). Excluded: 2.5 s after `LaunchPad.Thrown` for that bot, and rises up to 5 m on the plates. Pit falls exceed 14 m/s from 6.2 m under 1.61 g, which is why the old 14 m/s rule is gone (code-contract).
- **Pinned**:
  - as the yard (:842-850);
  - a **press pinch**: hips inside the die box while `!Physics.GetIgnoreCollision(dieBox, part)` for any part, or ≥ ghostSeconds + 0.3 = 1.3 s inside;
  - a **door pinch**: hips inside the disc ≥ 0.3 s.
- **Stuck**: < 0.4 m in 4 s and not `Waiting`; waits set Waiting.
- Hits come from `YardKit.HitGiven`. `Kind(source)` maps "RV hurdle", "RV bulkhead", "RV hammer", "RV press", "RV drawer" and "RV door".

**Records**

| File | Contents |
|---|---|
| crossings.csv | run, seed, bot, policy, piece, branch1, branch2, lane, finished, seconds, downs, staggers, falls, respawns, drop_to_belt, hammer1_wait, hammer2_wait, press_wait, belt_s, drawer_wait, plate_wait, door_wait, alone_s, max_speed, max_rise, stuck, flung, pinned, blind_gaveup |
| beats.csv | run, bot, beat (`section.BeatAt`), t_in, t_out, downs, falls, waits |
| door.csv (M5) | per opening: bots waiting, first and last arrival, first and last pass |
| hits.csv | as the yard |
| trace.csv | the first run at 0.2 s, with jump take-off and landing distances |

report.txt also prints the fork-2 model (`ForkTwoSafe/Risky/DropCost` at 2.5 / 3.1 / 3.6) next to the measured medians.

Pictures (M5 on, unless NoCapture): one fixed view per beat plus a top view. Limits: 240 s per `full` run, 60 s per beat probe, RuntimeLimit 3600 s. The exit code is 1 only for Play errors, so **read report.txt**.

### 5.3 Metrics and acceptance (bot proxies, not verification)

**Global** (`full`, pawns, 2 seeds × 4 runs)
- 0 stuck, 0 flung, 0 pinned, 0 unrecovered falls, 0 Play errors.
- ≥ 95 % of crossings finish within 240 s.
- **Leader** (fastest per run) **70–90 s**; **median of all finishers 105–140 s**.

**Per beat** (share = crossings with ≥ 1 event at that beat)

| Beat | Acceptance |
|---|---|
| 0 forecourt | 0 downs, 0 falls |
| 1 towers | falls: Jumper 10–35 %, Sprint ≤ 45 %, Run ≤ 65 %; Jumper beat time without a fall 10–16 s |
| 1 corridor | hurdle downs 15–55 %; 0 falls; bulkhead wait mean ≤ 2.5 s; median beat time 18–32 s |
| J1, J2, terrace | 0 hits, 0 falls |
| 2 hammers | downs: Jumper ≤ 20 %, Sprint ≤ 35 %, Run ≥ 30 %; falls ≤ 10 %; hammer-1 wait mean 0.5–3 s; hammer-2 wait mean ≤ 4.5 s for Run and ≤ 1.5 s for Sprint and Jumper; double hits per pass 0 |
| 3 safe | press downs: Jumper ≤ 15 %, Run ≥ 25 %; press wait mean 0.3–2.5 s; belt time mean 4–10 s; 0 press pinches |
| 3 risky | drop-to-belt: Jumper 5–30 %, Run ≥ 40 %; ≥ 98 % of launches land on the shelf; abyss falls ≤ 2 % |
| fork balance | Fork 1: towers median ≤ corridor median − 3 s for Sprint and Jumper, and the towers have more falls. Fork 2: Jumper risky median ≤ safe median; Sprint risky median ≤ safe median + 0.5 s; risky has drops; Run reported only (blind at the drawers) |
| 4 plates | falls: Jumper ≤ 25 %, Sprint ≤ 40 %, Run ≥ 40 %; median beat time 7–14 s; plate wait mean ≤ 3 s |
| 6 door | wait mean 1.5–4.5 s; mean bots per opening ≥ 2; 0 door pinches |
| `mix` run | no stuck or pinned queens or rooks; downs and falls within +10 points of the pawns'; every hammer knockdown pushes the piece ≥ 0.5 m back; no piece knocked down by both hammers in one crossing |

### 5.4 Validators

**SectionValidator / Validate Sections** (§3.2) checks span coverage, the path, checkpoint floors, the "RV " no-climb prefix, the kill volume and the entry and exit floors (SectionValidator.cs:29-93). Before the pool it runs on the vault through `RoyalVaultValidation.RunEditBatch`, which calls `SectionValidator.Run(section, notes)` and exits 0/1 (pattern `SectionPrefabMenu.CheckGardenRouteBatch`, :126-156).

**SectionCorridorValidation**: the belt check is conditional (§3.2). After the pool, `-corridorSeeds 11,12` must pass a war-room round, then a vault round:
- joins within 1 cm;
- floors 0.6 m in at x −5.5 / 0 / 5.5 within 6 cm (sill y 0, room floor y 12);
- nothing solid in the corridor box: every vault solid is at z ∈ [0, 196] or at |x| ≥ 6 on the south wall line.

**`RoyalVaultValidation`**: menus `Validate/Royal Vault (Edit)` and `(Play)`; batch `RunEditBatch` / `RunPlayBatch`; report `Logs/RoyalVaultValidation/vault.txt` plus pictures; exits 0 only when clean.

Edit checks (prefab built in a scratch scene at (0, 0, 20000) by calling the builder; Awake does not run):

| # | Check |
|---|---|
| E1 | both prefabs' YAML id, displayName, kind, entry, exit and exitYaw equal the RoyalVaultSection constants |
| E2 | every Renderer that is not a `Cue …` object and has no TextMesh uses `M_VaultGreybox` |
| E3 | every non-trigger collider under Content has NoClimbSurface **on its own GameObject** (NoClimbSurface.cs:27-30) |
| E4 | every walkable top in §1 has CourseFloor, including all 10 plate decks from M4 on (they exist because `VaultPlate.Configure` builds them) |
| E5 | Floor along every branch polyline in 0.25 m steps: towers at x ±2.2 and ±2.2 ± 0.8; corridor; shelf x 5; belts x −3.5 and 1.5; neck; plates x ∓0.8 and ∓3.2. Steps ≤ 0.31 m except tower lips (0.5) and the pad flight. Colliders with an `Obstacle` or `ObstacleSpan` in their parents and **no CourseFloor** are skipped (as Course01Validator.IsObstacle, :200-202); plate decks count as floor |
| E6 | rails ≥ 1.15 m on every regroup-platform edge over the pit except the listed openings (forecourt N and rest N take-offs; rest S; J1 S tower landings and stair 1; J1 N and approach S walkway; approach N belts; J2 S stair 2 and shelf; J2 N plates; terrace S stair 3) |
| E7 | rays down from 1 m outside every platform edge hit a kill volume |
| E8 | sphere casts (r 0.3) from (3.2, 8.5, z), z ∈ [96.3, 103.3], moving −x at 5 m/s under 15.8 m/s², land on a belt or at z ≤ 103.4 on the landing |
| E9 | CP0..CP6 present, ascending in z, CP0 with its reach box; CP3's trigger begins before z 83.8 |
| E10 | every floor sample lies in a beat box |
| E11 | 0 ISectionNetPart |
| E12 | 0 FinishZone |
| E13 | the corridor fit above |
| E14 | every renderer and every **non-trigger** collider under Content is named "RV …", "Cue …" or "Label …". Every trigger carries Checkpoint, KillVolume, LaunchPad or YardImpact. No `RV placeholder` after M6 |
| E15 | the LaunchPad's box (centre, size), landing offset, clearance and sameThrow equal RoyalVaultRules; its west face is ≥ 1.0 m east of x 4 |

Play checks (vault alone, `PlaceEntryOn` at (100, 5, 200), configured, 1 s settle):

| # | Check |
|---|---|
| P1 | At 20 clock samples every mover's pose equals RoyalVaultRules, **each against its own index**: hammer heads (`HammerHeadZ` ± 0.05 m), hurdle bar direction (± 1°), bulkhead panels (± 0.02 m), die bottom (± 0.02), drawer faces (`DrawerFaceX` ± 0.02), plate angles (± 0.5°), door centre and roll angle, shove mover = disc centre with identity rotation |
| P2 | the hammer head sweeps the walkway's full width; its velocity at angle ≈ 0 is −z |
| P3 | die down: an OverlapBox across the band at 0.5 m hits it; die up: a 1.5 m tall box under it is clear |
| P4 | at each sample one row's decks are flat; lifted decks tilt outward (`deck.up.x` points away from the centre) |
| P5 | Shut: the doorway box is blocked. At a passable time a 2.5 m box passes. The disc's lowest point moves < 0.1 m/s along x while rolling. While closing, the shove shell's leading face is never behind the disc rim at y 12.3 |
| P6 | belt `PointVelocity` = BeltSpeed along the section's +z |
| P7 | bulkhead clear width ≥ 3.9 m at every sample |
| P8 | Cue states match §2.12. From a camera 6 m behind and 15° above a pawn at (0, 12.6, 175), FOV 60°, at least one countdown copy is in the frustum |
| P9 | no Play error |
| P10 | a loose 0.6 m capsule Rigidbody placed overlapping the pad's trigger 0.3 m outside each edge midpoint and corner (9 places) is thrown and lands on the shelf top within the Core test 8 landing box |

**Commands (Windows PowerShell, repo root)**

```
./Tools/Test-NetworkCore.ps1
./Tools/Test-NetworkCompile.ps1 -SteamRuntimeSources Library/PackageCache/com.rlabrecque.steamworks.net@6fb66c768572/Runtime
# Git Bash:  CHESSFIGHT_TEST_OUT=<scratch> bash Tools/run-tests-linux.sh --checks-only
./Tools/Run-RoyalVault.ps1 -What ValidateEdit
./Tools/Run-RoyalVault.ps1 -What Validate
./Tools/Run-RoyalVault.ps1 -What Bots -Beat walk -Runs 2 -Seed 7 -NoCapture
./Tools/Run-RoyalVault.ps1 -What Bots -Beat full -Runs 4 -Seed 23
```

`Run-RoyalVault.ps1` copies `Run-CarriageYardBots.ps1:15-38`:
- robocopy /MIR into `C:\유니티\ChessFight_TestCopy`, excluding .git, Temp, Logs, obj and Library after the first time; exit code ≥ 8 throws;
- then `Start-Process "C:/Program Files/Unity/Hub/Editor/6000.3.11f1/Editor/Unity.exe" -batchmode -projectPath <copy> -executeMethod ChessFight.PawnRush.Editor.<RoyalVaultValidation.RunEditBatch|RoyalVaultValidation.RunPlayBatch|RoyalVaultBotTest.RunPlayBatch> <args> -logFile <copy>\Logs\royalvault_unity.log -Wait -PassThru`;
- print the report's tail and exit with Unity's code.

Check for a leftover Unity.exe before rerunning, and rename the results folder between experiments.

---

## 6. Risks and tuning knobs

| Risk / symptom | Constant (RoyalVaultRules unless noted) | Direction |
|---|---|---|
| Towers too hard (Jumper falls > 35 %) | `TowerDiameter` 3.6 → 3.8 (gap 0.675), or `TowerTakeoffHalf` 1.0 → 0.8 | bigger tops, narrower take-offs |
| Towers too easy (Jumper falls < 10 %) | `TowerDiameter` 3.6 → 3.4 (gap 1.025) | wider gaps |
| Hurdles knock down too often | `HurdleDegreesPerSecond` ±110/100 → ±100/95; `HurdleBarHigh` .55 → .5 | slower, lower |
| Bulkhead queues | `BulkheadCycle` 6 → 5. Shoves feel violent: `BulkheadLeg` 1.3 → 1.6 | |
| Runners wait > 4.5 s in the hammer cell | `HammerPairSeconds` 3 → 0 (in phase: runner ≈ 1.2 s, sprinter ≈ 2.4 s, nobody chains) | |
| Hammers too easy | `HammerDegreesPerSecond` 60 → 72 | |
| Hammer knockbacks reach the other zone | hammer shell maxPush 3.8 → 3.2 (builder) | |
| Press waits long | `PressPeriod` 4.5 → 5.0 with `PressSit` .5, `PressRise` 1.2 (runner window 2.2 s) | |
| Press too easy | `PressBandOut` 1.8 → 2.2 (also +0.2 s to the fork-2 gap) | |
| Risky fork 2 not faster | `WestBeltSpeed` −1.0 → −1.2; `EastStopSpeed` 0 → −0.3; `DrawerRest` 2.4 → 2.8. Re-run Core test 10 | slower safe, faster shelf |
| Risky fork 2 too strong (Jumper gain > 4 s) | `DrawerRest` 2.4 → 2.0; `DrawerHold` .4 → .8 | |
| Pad lands short or long | `PadClearance` 1.9 ± .1 (keep ≥ 1.8 for the E-rail margin); `PadLandZ` 93.3 ± .3 (keep ≤ 93.6 for the drawer-1 margin) | |
| Too many drops | `DrawerRest` 2.4 → 2.8 | |
| Drops too cheap (risky dominates) | drawer shell knockdownSpeed 99 → 3 (a knockdown before the fall) | |
| Plates fling or kill too much | `PlateAngleMax` 50 → 40; `PlateRaise` 1.0 → 1.3; `PlateCycle` 8 → 8.8 | |
| Plates too easy | `PlateCycle` 8 → 7.2 | |
| Door regroups too little | `DoorPeriod` 15 → 18 | |
| Door waits annoy | `DoorPeriod` 15 → 12 | |

**Other risks**
- **Round length.** A vault round runs about 100 s longer than a war-room round; that is the user's decision.
- **Fork-2 margins are thin for sprinters** by the model (−0.2 s at 3.6 m/s, +0.6 at 3.1). Two routes over the same z cannot give more without lengthening the belts. Lengthening them by 4 m (layout shifted north) would give about +3 s at a run; held back.
- **Shelf overhang camera.** Pawns on the east belt at x > 2.5 get the camera pulled in under the shelf. If that is bad, move `ShelfX0` 3 → 3.3; a CameraSeeThrough on the 1 m overhang strip alone is the last resort.
- **Pawn-on-pawn drops.** A drawer drop onto a belt rider lands raw at 11.2 m/s and floors both. The open edge is over the moving belts, not the press queue, and bots will show how often it happens.
- **Pad catch margin.** It assumes the hips are ≤ 0.6 m outside the trigger at first contact. A diving pawn may be caught further out; it then lands against the landing zone's W rail, not off it (P10 checks loose bodies).
- **Bots never shove, grab or read cues.** Crowding at the pad, neck, door and tower openings is unmeasured until the user's 6 v 6 test.
- **3 v 3.** The 28–30 m platforms will feel empty; there are no team-size variants in v1.
- **Untested on a route.** The plates' slip mechanics come from a test-bed device never run on a route (ROYAL_CARRIAGE_YARD.md:83). M4 is its first real test.
- **Copied logic.** VaultPressWave copies SlamChest's judgement; keep the pointer comment to SlamChest.cs:278-346.
- **Kings** (speed 0.8) are reachable only with map-test key 6, not in rounds. Their tower reach is 1.15 m against a 0.85 m gap.
- **Prefab and Describe drift** (PITFALLS #68) is caught by E1. Tunables live in code, but force-reimport in the tests anyway.
- **The copy's Library** is at R662; the first sync re-imports for a long time.
- **Mid-style corridor** into slot 5 (art.json). Left for the art pass.
