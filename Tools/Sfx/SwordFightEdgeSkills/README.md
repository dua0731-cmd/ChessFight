# Sword Fight edge-skill SFX mockup (R115)

The pick page for the five E skills' sounds (5 skills × directions A–E, one take per sound at the skill's signature
moment): https://claude.ai/artifact/SmP9pDnP9xTaNSRvH5TJMB (private). The mp3s are on the page and in the ElevenLabs flow
"ChessFight Sword Fight edge skill SFX mockup R111" (named before the number was checked; the record is R115), not in the repo.

- `spec.json` — skills, moments, directions and the prompts finally used (Sound Effects v2, prompt_influence 0.55).
- `sessions.txt` — ElevenLabs session id → take (`_rN` = retakes).
- `fetchsfx.sh <status-file> sessions.txt <dir>` — downloads takes from a saved `creative_get_flow_run_status` result.
- `boing.py "<glob>"` — flags a sliding tone 120–1800 Hz held ≥ 120 ms (the "띠용" 승규 님 rejected in R108). Run with
  `Tools/Sfx/PawnRushSkills/coin.py` (bright coin-like rings, R104/R107) on every take.
- `edgepage.py <root>` + `edgepage.html` — builds `<root>/page/index.html` from `spec.json` and `<root>/page/sfx/*.mp3`
  (`<root>/page/img/<skill>.jpg` are frames of the R103 film).
