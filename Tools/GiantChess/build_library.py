"""Compile the 40 factual PGN mainlines into offline replay data; --check never writes."""
from pathlib import Path
import chess, chess.pgn
import json,sys
ROOT=Path(__file__).resolve().parents[2]
def encode(game):
    board=game.board();assert board.fen()==chess.STARTING_FEN
    rows=[]
    for move in game.mainline_moves():
        assert move in board.legal_moves
        before=''.join(board.piece_at(s).symbol() if board.piece_at(s) else '.' for s in range(64))
        san=board.san(move);rf=rt=-1
        if board.is_castling(move):
            rank=move.from_square//8*8;kingside=move.to_square>move.from_square
            rf=rank+(7 if kingside else 0);rt=rank+(5 if kingside else 3)
        capture=move.to_square if board.is_capture(move) else -1
        if board.is_en_passant(move):capture=move.to_square+(-8 if board.turn else 8)
        promotion=chess.piece_symbol(move.promotion) if move.promotion else '.'
        if board.turn:promotion=promotion.upper()
        board.push(move)
        rows.append(f'{move.from_square},{move.to_square},{rf},{rt},{int(board.attacks(move.to_square)):016X},{san},{capture},{promotion},{before}')
    assert len(rows)>20
    return rows

def outputs():
    games=[]
    with (ROOT/'Tools/GiantChess/Library/selected_games.pgn').open() as f:
        while (game:=chess.pgn.read_game(f)) is not None:
            assert not game.errors
            h=game.headers
            games.append(dict(id=f'GM{len(games)+1:03}',white=h['White'],black=h['Black'],event=h['Event'],date=h['Date'],round=h['Round'],result=h['Result'],rows=encode(game)))
    assert len(games)==40
    assert len({(g['white'],g['black'],g['date'],g['round']) for g in games})==40
    doc='# 거인 체스 무작위 기보 40경기\n\n후보자 결정전2013·2016·2018·2022에서 각10경기. 순서는 백 — 흑. 원본: [Chess-Dataset](https://github.com/mainali123/Chess-Dataset/tree/5721c8a71a0f9850d54fe6d5b2d8060abcab1357/Chess_Candidates_and_Interzonals). 원본 URL·SHA256·선택 인덱스는 Tools/GiantChess/Library/sources.json, 주석 없는 수순은 selected_games.pgn에 저장.\n\n| ID | 날짜 | 라운드 | 백 | 흑 | 결과 | 반수 |\n|---|---|---|---|---|---|---|\n'
    for g in games:doc+='| '+' | '.join(str(g[k]) for k in ('id','date','round','white','black','result'))+f" | {len(g['rows'])} |\n"
    return {'Assets/Resources/GiantChessGames.json':json.dumps({'games':games},ensure_ascii=False,indent=2)+'\n','Docs/PawnRush/GIANT_CHESS_GAME_LIST.md':doc}
if __name__=='__main__':
    for path,text in outputs().items():
        if '--check' in sys.argv:assert (ROOT/path).read_text()==text,path
        else:(ROOT/path).write_text(text)
    print('PASS: 40 standard-start legal mainlines compiled with captures, both castling sides, promotion and attack masks.')
