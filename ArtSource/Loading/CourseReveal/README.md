# Course reveal pictures (R551)

The course reveal on the Pawn Rush entrance screen (`Assets/Scripts/Game/LoadingScreenView.Reveal.cs`, design A
"왕실 룰렛") draws its parts from `Assets/Resources/Loading/CourseReveal`. They come from here:

- `art.html` draws every UI part (gilded frame, "?" cover, glass, glint, name plates, track, medallions, gems, flag,
  emblems, title, kicker, stamp, tint) on canvases at twice their size on the 1280 x 720 stage, and posts each as
  `cr_<Name>.png` to the little server.
- `serve.py <site folder>` serves the given folder on 127.0.0.1:8765 and saves each `POST /shot?name=X` as `X.png` in a `shots` folder beside it.
  Open `http://127.0.0.1:8765/art.html` and wait until the page says it is done.
- `reveal_pictures.py <repo> <shots> <out>` crops the mini-games' R311 concept art
  (`Docs/PawnRush/ConceptArt/R311`) to 16:9 at 648 x 364 (`<prefab key>.jpg`), and copies the parts: emblems as
  `<prefab key>.png`, everything else into `UI/`.

Run Python with Pillow (`uv run --with pillow python -I reveal_pictures.py ...`).

Real-map mini-game pictures (R562): `MiniGames/raw/<prefab key>.png` are 1920 x 1080 shots of
`Assets/Scenes/PawnRush_MiniGames.unity` from the top right corner (camera (16.6, 9.6, 2.35), Euler (20.3, -36.5, 0),
FOV 60) taken with a temporary camera while the scene played, with game signs, progress bars, text, glows, rings,
beams, particles, the playtest pawn and the mini-game pieces hidden. `MiniGames/clean_sky.py <raw> <out>` swaps the
flat test-scene sky of the outdoor games for a soft sky and crops to 16:9 into `MiniGames/<prefab key>.png`;
`reveal_pictures.py` uses such a file instead of the concept art when it exists. All ten games are real shots now;
the seven indoor ones are copied as shot (no sky to clean).
Run `clean_sky.py` with `--with pillow --with numpy --with scipy`.
