"""Generate a validated replay; requires python-chess (development only)."""
from pathlib import Path
import chess
SAN = '''Nf3 Nf6 c4 g6 Nc3 Bg7 d4 O-O Bf4 d5 Qb3 dxc4 Qxc4 c6 e4 Nbd7 Rd1 Nb6 Qc5 Bg4 Bg5 Na4 Qa3 Nxc3 bxc3 Nxe4 Bxe7 Qb6 Bc4 Nxc3 Bc5 Rfe8+ Kf1 Be6 Bxb6 Bxc4+ Kg1 Ne2+ Kf1 Nxd4+ Kg1 Ne2+ Kf1 Nc3+ Kg1 axb6 Qb4 Ra4 Qxb6 Nxd1 h3 Rxa2 Kh2 Nxf2 Re1 Rxe1 Qd8+ Bf8 Nxe1 Bd5 Nf3 Ne4 Qb8 b5 h4 h5 Ne5 Kg7 Kg1 Bc5+ Kf1 Ng3+ Ke1 Bb4+ Kd1 Bb3+ Kc1 Ne2+ Kb1 Nc3+ Kc1 Rc2#'''.split()
b=chess.Board(); rows=[]
for i,s in enumerate(SAN):
    move=b.parse_san(s); assert move in b.legal_moves
    canonical=b.san(move); castle=b.is_castling(move)
    rf,rt=(63,61) if castle else (-1,-1)
    b.push(move)
    mask=int(b.attacks(move.to_square))
    assert not mask & (1 << move.to_square)
    rows.append(f'{move.from_square},{move.to_square},{rf},{rt},{mask:016X},{canonical}')
assert len(rows)==82 and b.is_checkmate() and b.turn==chess.WHITE
path=Path(__file__).resolve().parents[2]/'Assets/Resources/GiantChessScore.txt'
output='\n'.join(rows)+'\n'
import sys
if '--check' in sys.argv: assert path.read_text()==output
else: path.write_text(output)
print('PASS: 82 legal plies, castling/captures, attack masks, final Rc2# checkmate.')
