"""Contact sheets, GIFs and a numeric summary from the ragdoll lab's control-feel recordings.

    RagdollLab.exe -batchmode -ragdollFeel <folder>       (writes <folder>/<scenario>/f_*.png + <scenario>.csv)
    python Tools/RagdollPreview/feel_sheets.py <folder> [--every 3] [--cols 5] [--rows 4] [--gif]

For each scenario it writes <folder>/<scenario>_sheet_N.png (frames every `every`th, 30 fps source, stamped
with time, input mark, speed and state), optionally <scenario>.gif, and prints a summary per scenario:
time to 90% of top speed, stopping time, knockdowns, how long the pawn was limp, the states it went through.
Needs Pillow (numpy optional).
"""
import csv
import glob
import os
import sys

from PIL import Image, ImageDraw, ImageFont

STATES = {0: "Active", 1: "Ragdoll", 2: "GettingUp"}


def load_rows(path):
    with open(path, newline="", encoding="utf-8") as f:
        return list(csv.DictReader(f))


def font(size):
    for name in ("consola.ttf", "arial.ttf", "DejaVuSans.ttf"):
        try:
            return ImageFont.truetype(name, size)
        except OSError:
            continue
    return ImageFont.load_default()


def sheets(folder, name, rows, every, cols, per_rows, thumb_w=400, prefix="f_", tag="sheet"):
    frames = sorted(glob.glob(os.path.join(folder, name, prefix + "*.png")))
    if not frames:
        return []
    picks = list(range(0, len(frames), every))
    per_sheet = cols * per_rows
    out = []
    small = font(15)
    for s in range(0, len(picks), per_sheet):
        chunk = picks[s:s + per_sheet]
        first = Image.open(frames[chunk[0]])
        th = int(first.height * thumb_w / first.width)
        sheet = Image.new("RGB", (cols * thumb_w, ((len(chunk) + cols - 1) // cols) * th), (20, 20, 20))
        draw = ImageDraw.Draw(sheet)
        for k, i in enumerate(chunk):
            img = Image.open(frames[i]).convert("RGB").resize((thumb_w, th))
            x, y = (k % cols) * thumb_w, (k // cols) * th
            sheet.paste(img, (x, y))
            r = rows[i] if i < len(rows) else None
            label = f"#{i}"
            if r:
                label = (f"{float(r['t']):.2f}s {r['mark']}  v{float(r['speed']):.1f}"
                         f" {STATES.get(int(r['state']), r['state'])[:4]}"
                         + (" G" if r['grounded'] == '1' else "")
                         + (" CLB" if r['climbing'] == '1' else "")
                         + (" HOLD" if r['grabbing'] == '1' else "")
                         + (" HELD" if r['held'] == '1' else "")
                         + (" DIVE" if r['diving'] == '1' else ""))
            draw.rectangle([x, y, x + thumb_w, y + 20], fill=(0, 0, 0))
            draw.text((x + 4, y + 2), label, fill=(255, 255, 120), font=small)
            draw.rectangle([x, y, x + thumb_w - 1, y + th - 1], outline=(60, 60, 60))
        path = os.path.join(folder, f"{name}_{tag}_{s // per_sheet + 1}.png")
        sheet.save(path)
        out.append(path)
    return out


def gif(folder, name, every=2, width=480, prefix="f_", tag=""):
    frames = sorted(glob.glob(os.path.join(folder, name, prefix + "*.png")))[::every]
    if not frames:
        return None
    imgs = []
    for f in frames:
        img = Image.open(f).convert("RGB")
        img = img.resize((width, int(img.height * width / img.width)))
        imgs.append(img.convert("P", palette=Image.ADAPTIVE, colors=128))
    path = os.path.join(folder, f"{name}{tag}.gif")
    imgs[0].save(path, save_all=True, append_images=imgs[1:], duration=int(1000 * every / 30), loop=0)
    return path


def summary(name, rows):
    if not rows:
        return f"{name}: no rows"
    f = lambda r, k: float(r[k])
    lines = [f"== {name} ({len(rows)} frames, {f(rows[-1], 't'):.2f}s)"]
    # Segments by mark.
    seg, start = rows[0]["mark"], 0
    segs = []
    for i, r in enumerate(rows + [None]):
        if r is None or r["mark"] != seg:
            segs.append((seg, start, i - 1))
            if r is not None:
                seg, start = r["mark"], i
    for mark, a, b in segs:
        part = rows[a:b + 1]
        t0 = f(part[0], "t")
        speeds = [f(r, "speed") for r in part]
        top = max(speeds)
        reach = next((f(r, "t") - t0 for r in part if f(r, "speed") >= 0.9 * top), -1) if top > 0.5 else -1
        stop = next((f(r, "t") - t0 for r in part if f(r, "speed") < 0.3), -1)
        states = []
        for r in part:
            s = STATES.get(int(r["state"]), r["state"])
            if not states or states[-1] != s:
                states.append(s)
        tilt = max(f(r, "tilt") for r in part)
        limp = sum(1 for r in part if r["state"] == "1") / 30.0
        lead = max(f(r, "anchor_lead") for r in part)
        extra = ""
        if any(r["climbing"] == "1" for r in part):
            extra += f" climb {sum(1 for r in part if r['climbing'] == '1') / 30.0:.2f}s, hips {min(f(r, 'hy') for r in part):.2f}..{max(f(r, 'hy') for r in part):.2f}"
        if any(r["held"] == "1" for r in part):
            extra += f" held {sum(1 for r in part if r['held'] == '1') / 30.0:.2f}s"
        lines.append(f"  [{mark:>12}] {t0:5.2f}s+{f(part[-1], 't') - t0:4.2f}  top {top:4.1f} m/s"
                     f"  90%@{reach:5.2f}s  <0.3@{stop:5.2f}s  tilt≤{tilt:4.0f}°  limp {limp:4.2f}s"
                     f"  lead≤{lead:.2f}  stam {f(part[-1], 'stamina'):.2f}  {'>'.join(states)}{extra}")
    lines.append(f"  knockdowns {rows[-1]['knockdowns']}")
    return "\n".join(lines)


def main():
    args = sys.argv[1:]
    folder = args[0]
    every = int(args[args.index("--every") + 1]) if "--every" in args else 3
    cols = int(args[args.index("--cols") + 1]) if "--cols" in args else 5
    per_rows = int(args[args.index("--rows") + 1]) if "--rows" in args else 4
    only = args[args.index("--only") + 1].split(",") if "--only" in args else None
    for csv_path in sorted(glob.glob(os.path.join(folder, "*.csv"))):
        name = os.path.splitext(os.path.basename(csv_path))[0]
        if only and name not in only:
            continue
        rows = load_rows(csv_path)
        print(summary(name, rows))
        for p in sheets(folder, name, rows, every, cols, per_rows):
            print("  sheet", p)
        for p in sheets(folder, name, rows, every, cols, per_rows, prefix="s_", tag="side"):
            print("  side", p)
        if "--gif" in args:
            print("  gif", gif(folder, name))
            print("  gif", gif(folder, name, prefix="s_", tag="_side"))


if __name__ == "__main__":
    main()
