# R844 Grand banquet hall sounds

User 11-26: "커밋 푸쉬 하지 말고 대연회장 사운드 작업 읽고 시작해" (design: Docs/PawnRush/PAWN_RUSH_BANQUET_HALL_SFX.md, R843).

ElevenLabs flow "R844 대연회장 소리" (`qw0YQDFYqX3Q4pU1s5TP`), model `eleven_text_to_sound_v2`, 4 takes a prompt. One-shots: the default
duration, prompt_influence 0.3. Loops: asked again with `duration_seconds`, `loop: true`, prompt_influence 0.45 (the first default-length
takes came out 1 - 2 s; kept in raw/short_first_takes, not used). Every prompt ends with "dry, close, no reverb tail, no music, no voice"
(loops: "dry, close, no music, no voice"; the fanfare: no "no music"). Takes: `raw/<kind>_<n>.mp3`. `bake.py` picks by the numbers, or
what `picks.json` names, and writes `Assets/Resources/BanquetHallSfx/<File>.wav`; `page.py` + `page_template.html` -> the listening page
https://claude.ai/artifact/6k3xV1LpiLyTiEpFJrUyyu. **Not heard by the user yet; the six cue sounds are theirs to pick by ear.**

| kind | file | seconds asked | picked | game length (s) | volume in code | prompt |
|---|---|---|---|---|---|---|
| drum_roll | `BhDrumRoll` | default | 2 | 2.0 | 0.575 | Circus snare drum roll building steadily from soft to loud over two seconds, tight and bright, ends abruptly with no final hit |
| cloth_pull | `BhClothPull` | default | 3 | 0.543 | 0.977 | A magician whips a large tablecloth off a table in one fast pull: sharp fabric whoosh-snap with a bright cymbal crash on top, short |
| service_bell | `BhServiceBell` | default | 2 | 0.753 | 0.771 | Single clear ding of a small brass hotel service bell, one pure note, short ring |
| cloche_shut | `BhClocheShut` | default | 2 | 0.405 | 1.0 | A large silver dish cover set down on a silver platter: wide hollow metallic clang with a short low shimmer |
| lamp_chime | `BhLampChime` | default | 2 | 0.845 | 0.856 | Two soft rising glockenspiel notes, gentle and friendly, a short ready signal |
| curtain_cue | `BhCurtainCue` | default | 1 | 1.1 | 1.0 | A conductor taps a wooden baton three times on a music stand, crisp dry wooden taps, evenly spaced |
| hall_ambience | `BhHallAmbience` | 12 loop | 3 | 12.0 | 0.249 | Large palace banquet hall room tone: distant cutlery on porcelain, occasional crystal glasses clinking, soft airy space, no speech, seamless loop |
| kitchen_door | `BhKitchenDoor` | default | 4 | 0.422 | 0.526 | Wooden swinging kitchen half-door pushed open and flapping once, soft thump and hinge squeak, short |
| tray_clink | `BhTrayClink` | 4 loop | 3 | 4.0 | 0.935 | A tall stack of porcelain plates rattling gently on a carried silver tray, light steady clinking, seamless loop |
| cloth_thrown | `BhClothThrown` | default | 2 | 0.971 | 0.504 | Short cartoon whoosh of a small wooden toy flung through the air, rising pitch |
| cloth_lay | `BhClothLay` | default | 3 | 0.9 | 0.483 | A large linen tablecloth unfurled and settling over a long table, soft billowing fabric flaps |
| cloche_open | `BhClocheOpen` | default | 4 | 0.918 | 0.718 | A silver dish cover lifted off a platter, light bright metallic ring sliding upward, short |
| hoist_loop | `BhHoistLoop` | 3 loop | 2 | 3.0 | 1.0 | Wooden dumbwaiter hoist running: rope pulleys turning with a fine gold chain rattling, steady, seamless loop |
| gown_loop | `BhGownLoop` | 4 loop | 3 | 4.0 | 0.556 | Music-box clockwork whirring softly with silk gown fabric swishing in slow turns, seamless loop, no melody |
| curtain_draw | `BhCurtainDraw` | 2.5 loop | 3 | 2.61 | 0.558 | Heavy velvet stage curtain drawn along a brass rail: metal rings sliding and thick fabric dragging, seamless loop |
| chocolate_loop | `BhChocolateLoop` | 6 loop | 1 | 6.0 | 0.395 | Thick melted chocolate pouring down a fountain in slow heavy sheets, glossy viscous glugging, seamless loop |
| chandelier_swing | `BhChandelierSwing` | default | 4 | 0.671 | 0.61 | A swinging crystal chandelier reaching the end of its swing: scattered crystal drops tinkling with a soft brass chain creak, short |
| arrival | `BhArrival` | default | 3 | 1.6 | 0.388 | Very short royal trumpet fanfare flourish in F major, two bright trumpets, a clear ending |
