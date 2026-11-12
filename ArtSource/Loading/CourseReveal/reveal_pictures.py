# R551: the course reveal's pictures. Mini-games: the R311 concept art cropped to 16:9 at twice the frame window
# (648 x 364). The UI parts and emblems drawn by art.html are copied as they are.
# R554: each sheet holds the main picture on the left and notes on the right (from `gutter`), under a title strip;
# crop the main picture alone, centred on `middle` (the sheet's y, 1024 tall).
import sys, shutil, pathlib
from PIL import Image
repo, shots, out = map(pathlib.Path, sys.argv[1:4])
art = repo / 'Docs/PawnRush/ConceptArt/R311'
games = {  # key: (sheet, gutter, middle)
    'Early_MG_C_MoatDrawbridge': ('01_early_C_moat', 1120, 470), 'Early_MG_D_PortcullisCourt': ('02_early_D_capstan', 1090, 540),
    'Early_MG_A_ParadeCourt': ('03_early_A_parade', 1123, 500), 'Mid_MG_B_RepairWing': ('04_mid_B_repair', 1141, 540),
    'Mid_MG_E_RoyalLibrary': ('05_mid_E_library', 1065, 540), 'Mid_MG_F_ClockworkBallroom': ('06_mid_F_ballroom', 1134, 540),
    'Mid_MG_J_PalaceLaundry': ('07_mid_J_laundry', 1113, 540), 'Late_MG_G_RoyalProcession': ('08_late_G_procession', 1100, 540),
    'Late_MG_H_KingsChessHall': ('09_late_H_chess', 1130, 540), 'Late_MG_I_KingsBedchamber': ('10_late_I_bedroom', 1086, 640),
}
(out / 'UI').mkdir(parents=True, exist_ok=True)
# R579: the UI parts and emblems below are design A's (R551-R564), no longer used (kept in R564_backup).
# R562: a real-map shot in MiniGames/<key>.png (made by MiniGames/clean_sky.py) replaces the concept art.
real = pathlib.Path(__file__).resolve().parent / 'MiniGames'
for key, (src, gutter, middle) in games.items():
    if (real / (key + '.png')).exists():
        # R579: design C shows them on tall 420 x 520 panels (cropped to cover), so they are kept larger.
        Image.open(real / (key + '.png')).convert('RGB').resize((1440, 810), Image.LANCZOS).save(out / (key + '.jpg'), quality=90)
        continue
    im = Image.open(art / (src + '.png')).convert('RGB')
    w, h = im.size
    x0, x1 = 8, gutter - 10
    ch = round((x1 - x0) * 9 / 16)
    top = max(64, min(h - 6 - ch, middle - ch // 2))
    im.crop((x0, top, x1, top + ch)).resize((648, 364), Image.LANCZOS).save(out / (key + '.jpg'), quality=90)
for f in shots.glob('cr_*.png'):
    name = f.stem[3:]
    if name.startswith('Emblem_'):
        key = name[7:]
        shutil.copy(f, out / 'UI' / 'Question.png' if key == 'Question' else out / (key + '.png'))
    else:
        shutil.copy(f, out / 'UI' / (name + '.png'))
print(sorted(p.name for p in out.rglob('*') if p.is_file()))
