"""Validate the real PGN, playback state and production C# draw without Unity."""
from pathlib import Path
import importlib.util
import json
import subprocess
import tempfile
import unittest
import chess
import chess.pgn

ROOT = Path(__file__).resolve().parents[2]
spec = importlib.util.spec_from_file_location('library', ROOT / 'Tools/GiantChess/build_library.py')
library = importlib.util.module_from_spec(spec)
spec.loader.exec_module(library)


class ReplayLibrary(unittest.TestCase):
    def test_all_games_replay_to_every_saved_position(self):
        games = json.loads((ROOT / 'Assets/Resources/GiantChessGames.json').read_text())['games']
        self.assertEqual(len(games), 40)
        self.assertEqual(len({g['id'] for g in games}), 40)
        for game in games:
            board = chess.Board()
            pieces = [board.piece_at(s).symbol() if board.piece_at(s) else '.' for s in range(64)]
            for row in game['rows']:
                source, dest, rook, rook_dest, attacks, san, captured, promotion, before = row.split(',')
                self.assertEqual(''.join(pieces), before, game['id'] + ' ' + san)
                source, dest, rook, rook_dest, captured = map(int, (source, dest, rook, rook_dest, captured))
                move = board.parse_san(san)
                self.assertEqual((source, dest), (move.from_square, move.to_square))
                if captured >= 0:
                    pieces[captured] = '.'
                pieces[dest], pieces[source] = pieces[source], '.'
                if rook >= 0:
                    pieces[rook_dest], pieces[rook] = pieces[rook], '.'
                if promotion != '.':
                    pieces[dest] = promotion
                board.push(move)
                self.assertEqual(int(attacks, 16), int(board.attacks(dest)))
                self.assertEqual(''.join(pieces), ''.join(board.piece_at(s).symbol() if board.piece_at(s) else '.' for s in range(64)))

    def test_production_selection_reaches_every_game_without_adjacent_repeat(self):
        source = r'''
using System;
namespace UnityEngine {
 public enum RuntimeInitializeLoadType { SubsystemRegistration }
 public class RuntimeInitializeOnLoadMethodAttribute : Attribute { public RuntimeInitializeOnLoadMethodAttribute(RuntimeInitializeLoadType x) {} }
 public static class Random { static System.Random rng = new System.Random(2026); public static int Range(int min,int max) => rng.Next(min,max); }
}
class DrawTests {
 static void Main() {
  var library = new ChessFight.RagdollLab.GiantChessGames { games = new ChessFight.RagdollLab.GiantChessGame[40] };
  for(int i=0;i<40;i++) library.games[i] = new ChessFight.RagdollLab.GiantChessGame { id=i.ToString() };
  var seen = new System.Collections.Generic.HashSet<string>(); string last=null;
  for(int i=0;i<10000;i++) { var game=library.Pick(); if(game.id==last)throw new Exception("Adjacent repeat"); seen.Add(game.id);last=game.id; }
  if(seen.Count!=40)throw new Exception("Unreachable game");
  library.games=new[]{library.games[0]}; if(library.Pick()!=library.games[0])throw new Exception("One game");
  library.games=new ChessFight.RagdollLab.GiantChessGame[0];
  try {library.Pick();throw new Exception("Empty library accepted");} catch(InvalidOperationException) {}
 }
}
'''
        with tempfile.TemporaryDirectory() as temp:
            fixture = Path(temp) / 'DrawTests.cs'
            fixture.write_text(source)
            binary = str(Path(temp) / 'DrawTests.exe')
            subprocess.run(['mcs', '-out:' + binary, str(ROOT / 'Assets/Scripts/RagdollLab/GiantChessGames.cs'), str(fixture)], check=True)
            subprocess.run(['mono', binary], check=True)

    def test_generated_files_match_selected_sources(self):
        for path, content in library.outputs().items():
            self.assertEqual((ROOT / path).read_text(), content, path)


if __name__ == '__main__':
    unittest.main()
