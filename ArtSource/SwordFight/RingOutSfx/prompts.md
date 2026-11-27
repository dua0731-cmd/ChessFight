# R869 Sword Fight 장외! stamp sound — takes

User 11-27 (after seeing the result screen): "장외!가 나올 때 bgm이 살짝 작아지는 거 좋은데 장외! 문구가 나오는 사운드가 없어 시안 줘".
The music already ducks on a point (`SwordFightMatch.Music`, `DuckPoint`); nothing sounds with the stamp itself.

ElevenLabs flow "R869 소드 파이트 장외! 도장 소리 시안" (`qtXZXcO2Bh2qC9SI3wsr`), model `eleven_text_to_sound_v2`,
`prompt_influence` 0.45, 4 takes per direction, 188 credits. Takes: `raw/<A-D>_<1-4>.mp3`. `measure.py` → `report.json`
(peak, hit, tail); `page.py` + `page_template.html` → the listening page https://claude.ai/artifact/11FUs6jPe6CDUhz6sK5ZWx
(every take played at a -3 dB peak with the stamp popping in as the game draws it). The AI cannot listen: the user picks.

| | direction | seconds | prompt |
|---|---|---|---|
| A | 나무 도장 쾅 | 1.0 | A heavy wooden stamp slammed down hard on a thick wooden table, one big punchy hollow wooden thump with a bright brass clink on top, satisfying game score impact, close-mic, short tail, no voice, no music |
| B | 북 + 심벌 스팅 | 1.2 | Punchy game score sting, one deep taiko drum hit with a short bright cymbal splash, energetic arcade party game point scored, tight and loud, short, no voice, no melody |
| C | 코믹 휙 + 쿵 | 1.2 | Cartoon knockout, a fast comic whoosh flying away followed by a big bouncy wooden bonk impact, playful party game ring out, punchy and funny, short, no voice, no music |
| D | 징 + 칼 울림 | 1.3 | A short heavy medieval brass gong hit layered with a sharp sword clang, dramatic arena point scored impact, powerful and bright, fast decay, no voice, no music |

Numbers flagged on the page: C4 hits late (0.74 s, the whoosh first); B3 and D1 were generated quiet; C3 and all of D ring
on past 0.6 s. Next, once the user picks: trim + normalise into `Assets/Resources/SwordFightSkillSfx/RingOut.wav` and play
it on every screen with the 장외! stamp (`SwordFightMatch.Hud.StepFeed`), at the effects volume, under the music's duck.

**User pick 11-27: A2** ("A2로 해서 bgm이랑 이질감 없이 적용해", R871) → `bake.py` → `Assets/Resources/SwordFightSkillSfx/RingOut.wav`.
