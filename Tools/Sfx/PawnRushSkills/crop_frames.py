# Cut the R90 frame sheets (4 x 2 frames of 640 x 360) into single storyboard frames, top caption bar removed.
import cv2, sys, os
S = sys.argv[1]
picks = {
    "pawn":   [(0,1,"step"), (1,1,"push"), (1,2,"knock"), (1,3,"help")],
    "queen":  [(0,0,"windup"), (0,3,"blast"), (1,0,"burst"), (1,2,"smoke")],
    "rook":   [(0,0,"aim"), (0,3,"dash"), (1,0,"hit"), (1,1,"captured"), (1,2,"wall")],
    "bishop": [(0,0,"aim"), (0,2,"throw"), (0,3,"armed"), (1,1,"trip"), (1,2,"down")],
    "knight": [(0,0,"leap"), (0,1,"fly"), (0,2,"land"), (0,3,"lock"), (1,1,"stomp"), (1,2,"daze")],
}
for piece, frames in picks.items():
    sheet = cv2.imread(os.path.join(S, f"R90_frames_{piece}.jpg"))
    h, w = sheet.shape[:2]
    fw, fh = w // 4, h // 2
    for r, c, name in frames:
        f = sheet[r*fh:(r+1)*fh, c*fw:(c+1)*fw]
        f = f[52:, :]                      # drop the caption bar
        f = cv2.resize(f, (480, int(480 * f.shape[0] / f.shape[1])), interpolation=cv2.INTER_AREA)
        cv2.imwrite(os.path.join("frames", f"{piece}_{name}.jpg"), f, [cv2.IMWRITE_JPEG_QUALITY, 82])
print(sorted(os.listdir("frames")))
