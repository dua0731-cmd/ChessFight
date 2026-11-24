# R742 Royal Observatory sounds

User 11-21: "천문대 사운드 필요한 곳 확인해" -> "네, 만들어". The observatory had no sound at all.

ElevenLabs flow "R739 왕실 천문대 소리" (`3SsTjKFY5rvOHg2svvFA`; named R739 before the number moved to R742 - other sessions had
taken R739 - R741), model `eleven_text_to_sound_v2`, `prompt_influence` 0.45, 4 takes a prompt asked; 13 of 88 were refused by the
queue limit (not charged), so some kinds have 2 or 3. About 1,500 credits. Takes: `raw/<kind>_<n>.mp3`. `bake.py` measures them,
picks one per kind (or what `picks.json` names) and writes `Assets/Resources/RoyalObservatorySfx/<File>.wav` (played by
`Assets/Scripts/PawnRush/RoyalObservatorySound.cs`); `takes/` holds every take trimmed the same way for the listening page
(`page.py` + `page_template.html` -> https://claude.ai/artifact/KrnWCdo5Qi12kB7fgpKvhd).

The AI cannot listen: the picks are by the numbers only (one-shots: an early hit, a short tail, no stray beep; loops: even
loudness, a smooth seam). **Not heard by the user yet.** To change one: edit `picks.json` (`"iris_clash": "2"`), then run
`bake.py` again (the command is at its top; uv on Blender's Python).

| kind | file | seconds asked | picked | game length (s) | volume in code | prompt |
|---|---|---|---|---|---|---|
| meteor_warn | `RoMeteorWarn` | 0.8 | 2 | 0.732 | 0.411 | Soft magical celestial brass chime with a shimmering star twinkle, a gentle warning that something will fall from the night sky, short, clean attack, no music, no voice |
| meteor_fall | `RoMeteorFall` | 0.6 | 1 | 0.372 | 0.525 | Fast descending whoosh of a small burning meteor falling from high above, sparkling fiery crackle, pitch falling, ends right before it lands, no impact, no voice, no music |
| meteor_impact | `RoMeteorImpact` | 1.2 | 1 | 0.604 | 0.609 | Heavy stone meteor slamming onto a marble platform, a deep punchy thud with a golden metallic ring and a burst of crumbling stone and dust, close, no voice, no music |
| flare_charge | `RoFlareCharge` | 1.4 | 4 | 1.36 | 0.352 | Rising solar energy hum with crackling fire, tension building and swelling louder over one second, ends abruptly, no explosion, no voice, no music |
| flare_burst | `RoFlareBurst` | 1.8 | 1 | 1.3 | 0.29 | Huge solar flare eruption, a roaring burst of fire with a deep boom and a sizzling blazing tail, powerful, no voice, no music |
| flare_hit_me | `RoFlareHitMe` | 0.7 | 2 | 0.516 | 0.799 | Close sizzling burn hit, a hot searing flash of fire on armor, short and sharp, no voice, no music |
| beam_hum | `RoBeamHum` | 4 loop | 2 | 4.0 | 0.395 | Low warm hum of a focused beam of light pouring from a giant brass telescope lens, soft glassy resonance with a faint shimmer, steady, seamless loop, no voice, no music |
| beam_strike | `RoBeamStrike` | 0.7 | 1 | 0.309 | 1.0 | Electric sizzle and punchy impact of a concentrated beam of light hitting a small wooden chess piece and knocking it away, sharp crackle and a thump, no voice, no music |
| beam_hit_me | `RoBeamHitMe` | 0.6 | 2 | 0.309 | 1.0 | Close searing light hit, a sharp bright sizzle and a soft punch, short, no voice, no music |
| iris_warn | `RoIrisWarn` | 0.8 | 1 | 0.8 | 0.722 | Brass clockwork machinery ticking fast with a small mechanical warning bell, a metal gate about to snap shut, short, no music, no voice |
| iris_scrape | `RoIrisScrape` | 0.6 | 1 | 0.433 | 0.611 | Heavy brass plates sliding fast and scraping over a metal deck, metal grinding with sparks, short, no voice, no music |
| iris_clash | `RoIrisClash` | 1.0 | 3 | 0.691 | 1.0 | Two heavy brass iris blades slamming shut against each other, a loud metallic clang with a short ring-out, powerful, no voice, no music |
| petal_warn | `RoPetalWarn` | 0.9 | 1 | 0.826 | 1.0 | Small brass clockwork latch clicking and rattling nervously, a mechanism about to release, short, no voice, no music |
| petal_fold | `RoPetalFold` | 0.6 | 1 | 0.153 | 0.814 | A heavy brass and stone panel unlatching and dropping open on its hinge, a sharp latch clack followed by a falling metal clunk, short, no voice, no music |
| petal_rise | `RoPetalRise` | 1.0 | 2 | 0.501 | 1.0 | Brass clockwork gears whirring as heavy hinged panels swing back up together and lock into place with a firm click, mechanical, no voice, no music |
| orbit_loop | `RoOrbitLoop` | 5 loop | 2 | 5.0 | 0.314 | Slowly turning brass orrery machinery, a steady low mechanical rumble with soft gear ticks and a gentle metallic whir, seamless loop, no voice, no music |
| gear_loop | `RoGearLoop` | 5 loop | 2 | 5.0 | 0.352 (code pitch 0.84 .. 0.96) | user 11-21 "장난감 스러운 사운드야 묵직한 걸로해": takes 1-4 "Huge heavy iron and brass gears of a giant clock tower turning slowly, deep low grinding rumble, massive heavy teeth clunking into place one by one, weighty industrial machinery, low pitched, no high clicks, no toy ticking, seamless loop, no voice, no music"; takes 5-8 "Massive cathedral clockwork mechanism, enormous bronze cog wheels meshing, slow deep thunks of heavy teeth and a low rumbling groan of metal under great load, dark and powerful, low frequencies, seamless loop, no voice, no music" (prompt_influence 0.5). The first, toy-like takes are in raw/toy_gear_R742. |
| planet_roll | `RoPlanetRoll` | 4 loop | 4 | 4.0 | 0.539 | A huge heavy stone ball rolling steadily along a brass rail, a deep continuous rolling rumble with a faint metallic hum, seamless loop, no voice, no music |
| planet_dock | `RoPlanetDock` | 0.8 | 2 | 0.281 | 1.0 | A huge heavy stone ball rolling to a stop against a brass dock, a deep soft thud with a short metallic knock, no voice, no music |
| globe_creak | `RoGlobeCreak` | 6 loop | 2 | 6.0 | 0.249 | Huge brass armillary sphere rings slowly swinging, soft deep metallic creaks and a faint low resonant hum, sparse and calm, seamless loop, no voice, no music |
| hall_ambience | `RoHallAmbience` | 12 loop | 3 | 12.0 | 0.249 | Vast quiet royal observatory dome at night, a soft airy room tone with gentle night wind through an open roof slit and very faint distant clockwork ticking, calm and mysterious, seamless loop, no music, no voice |
| balcony_wind | `RoBalconyWind` | 10 loop | 4 | 10.0 | 0.314 | Calm night wind on a high stone balcony outdoors, a soft steady breeze with a gentle airy whistle, no birds, no insects, seamless loop, no music, no voice |
