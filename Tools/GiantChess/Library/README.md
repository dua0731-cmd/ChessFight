# GM replay source data

40 standard-start games: ten each from the 2013, 2016, 2018 and 2022 Candidates tournaments.
Source: https://github.com/mainali123/Chess-Dataset at commit `5721c8a71a0f9850d54fe6d5b2d8060abcab1357`.
`sources.json` records the four exact URLs, SHA256 hashes and selected zero-based game indices.
Selection used `random.Random(year).sample(range(game_count), 10)` and sorted the selected indices.
`selected_games.pgn` retains factual headers and mainlines only, without commentary or variations.
All entrants in these four tournaments were grandmasters; 2022 source headers omit title tags.

Generate with `python Tools/GiantChess/build_library.py`; add `--check` to compare without writing.
Install the generation dependency from `Tools/GiantChess/requirements.txt` in a virtual environment.
Run `python -m unittest discover -s Tests/GiantChess -v` with Python dependencies and Mono available.
Unity loads the committed Resources JSON offline; Python and a network connection are not needed to play.

Rows encode source, destination, castling rook source/destination (-1 when absent), post-move attack mask,
SAN, captured square (-1 when absent), promotion symbol (case encodes color), and the pre-move 64-square board.
Square indices are `rank * 8 + file`, a1 = 0. The runtime rotates this board into the course's world frame.
