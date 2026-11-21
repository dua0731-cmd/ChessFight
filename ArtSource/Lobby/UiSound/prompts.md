# R738 lobby and settings menu sounds

User 11-21: "설정창, 플레이, 기물도감, 전적 UI 사운드도 넣어야 해". The lobby and the settings window had no sound at all.

ElevenLabs flow "R737 로비 · 설정 UI 소리" (`2Rq0SFKq7Wg1BJSBEbw9`; made under R737 before the number was moved to R738,
another session had taken R737), model `eleven_text_to_sound_v2`, `prompt_influence` 0.45, 4 takes per node, 212 credits.
Take files: `raw/<kind>_<1-4>.mp3`. `bake.py` measures them, picks one per kind (or what `picks.json` names), and writes
`Assets/Resources/UiSound/Menu<Kind>.wav` (played by `Assets/Scripts/Game/MenuSound.cs`); `takes/` holds every take trimmed
the same way for the listening page (`page.py` + `page_template.html` →
https://claude.ai/artifact/FUVfWdvZvRFxTBQktpcUNS).

The AI cannot listen: the picks are by the numbers only (no ringing tone, an early hit, a short tail, not a faint take).
**User decision 11-21: "확정" — the nine picks below stay** (heard on the listening page; not yet heard in the game). To change one: edit `picks.json` (`"click": "2"`, or another kind's take `"hover": "toggle_1"`),
then run `bake.py` again (the command is at its top; uv's own Python failed to install on this PC on 11-21, so it ran on
Blender's: `uv run --no-project --python "C:/Program Files/Blender Foundation/Blender 5.2/5.2/python/bin/python.exe" --with miniaudio --with numpy python -I bake.py <this folder> <project root>`).

| kind | seconds | prompt | in the game (11-21) |
|---|---|---|---|
| hover | 0.5 | Very soft short game menu hover tick, a tiny crisp muted tap with a faint airy flick, subtle and quiet, modern polished game UI, dry, no ring, no chime, no beep, no voice | not used: all four carry a ringing tone or are near silent → `toggle_1`, quiet (-17 dBFS) |
| click | 0.5 | Crisp punchy game menu button click, a tight satisfying tap with a small low thump under it, polished modern game UI, close-mic, dry, no ring, no chime, no beep, no voice | 4 |
| tab | 0.6 | Quick crisp game menu tab switch, a short light swipe whoosh ending on a tight snappy tap, polished modern game UI, dry, no chime, no voice | 3 |
| open | 0.8 | Smooth game menu panel opening, a soft airy swoosh in that lands on a gentle low thump, polished premium game UI, short, dry, no music, no chime, no voice | 3 |
| close | 0.6 | Game menu panel closing, a short soft reverse swoosh out ending on a muted tap, polished premium game UI, dry, no chime, no voice | 4 |
| toggle | 0.5 | Tiny crisp switch toggle click, a small precise mechanical flick, game settings option changed, very short, close-mic, dry, no ring, no beep, no voice | 3 |
| confirm | 0.7 | Satisfying game menu confirm, a punchy tight snap with a deep soft thump and a short airy tail, start button pressed, premium game UI, dry, no chime, no music, no voice | 2 |
| piece | 0.6 | A wooden chess piece set down firmly on a wooden chessboard, a clean hollow knock with a soft felt thud, close-mic, dry, satisfying, no ring, no voice | 2 |
| page | 0.5 | Quick crisp card flip, a single stiff paper card turned over fast with a light snap, close-mic, dry, no voice | 2 |

Peaks after trimming (dBFS, `bake.py` PEAK): hover -17, click -7, tab -7, open -6, close -8, toggle -9, confirm -3, piece -5, page -8.
The game plays them at the settings' 소리 → 버튼 소리 volume (default 60 %) under 전체 소리.
