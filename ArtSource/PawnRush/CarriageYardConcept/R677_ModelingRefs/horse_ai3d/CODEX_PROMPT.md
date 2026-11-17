You are making input images for an AI image-to-3D tool (like Tripo) that will build ONE game model: the carriage horse of the "Royal Carriage Yard" course of ChessFight, a cute toy chess-kingdom party race. Use your image generation tool; do NOT write code and do not change any file except the PNGs you save here. Generate exactly FOUR images, in this order, and save each as PNG in the current working directory with these exact names:

- HORSE_01_three_quarter.png - 1024x1024, front three-quarter view (the horse's head toward the viewer's left, about 35 degrees from the side, camera at the height of the horse's back)
- HORSE_02_side.png - 1536x1024, exact left side profile (head to the left), camera at the height of the horse's back
- HORSE_03_front.png - 1024x1024, exactly from the front, camera at chest height
- HORSE_04_back.png - 1024x1024, exactly from behind, camera at the height of the hips

Make image 1 first. For images 2-4 attach/keep image 1 as the reference and say it is the SAME horse: same proportions, same colours, same mane, same tail, same pose, only the camera turns. If the tool saves somewhere else (for example ~/.codex/generated_images), copy each file here under its name. Never resize, crop or convert a result.

## Attached images

1. RCY_08 props and materials sheet - item 2 shows the carriage horses in this game's style
2. the user's own painting of the wagon yard - the horses pulling the coaches
3. the pawns as they look in the game - the overall render style of the game

Use them for style and shape only. Draw nothing else from them.

## The prompt (use for every image, with the view line of that image)

A single stylized 3D toy carriage horse for a cute chess-kingdom party game, standing alone and centred on a plain pure white background, the whole body in frame with a wide empty margin on every side.

- Shape: a chunky, sturdy draught horse like a well-made wooden-and-vinyl toy: a deep rounded barrel body, a strong arched neck, a short wide head with a straight nose and a closed mouth, small upright ears, thick straight legs with big rounded hooves, soft big bevels on every edge. Proportions of a real horse made a little chunkier and shorter in the legs; not a pony cartoon, not a chibi.
- Mane and tail: a short, neat upright hogged mane like a brush along the top of the neck; a short thick tail that hangs straight down and stands clearly away from the hind legs, with a gap between the tail and the legs.
- Eyes: small plain dark eyes on the sides of the head, no glint, no eyelashes, no brows, no blush, no smile. The horse is a plain horse: it must not look like a chess knight piece.
- Colour: bay - a warm reddish-brown coat (#7A4A2A), black mane, tail and lower legs, dark hooves. Matte, slightly satin painted finish, smooth surfaces with no hair strands and no fur texture.
- Pose: a neutral standing pose for rigging: all four legs straight and vertical, slightly apart, every leg fully visible and separated from the others, hooves flat; head up and forward in a natural carriage position; tail down.
- Light: soft even white studio light from the front and above, very light ambient occlusion, no hard cast shadow, no floor, no ground plane, no reflection.
- Bare horse only: no harness, no collar, no bridle, no reins, no saddle, no blanket, no plume, no badge, no rider, no cart or coach, no pole or traces.

AVOID: any background, scenery, sky, floor, ground shadow, vignette or gradient; flags, banners, windows, buildings, props, other animals, characters, chess pieces; any text, letters, numbers, labels, logos, watermarks, signatures, frames or borders; harness or tack of any kind; motion, rearing, galloping, a turned head; fur strands, photorealism, sketch lines, outlines, cel shading.

## CHECK (never send to the tool; call the tool at most twice per image, and if a check fails, generate once more with the failed checks named first and keep the better one)

- one bay horse, whole body, pure white background with nothing else at all;
- no harness, tack, plume, text, flag, window or ground shadow;
- four straight separated legs, the tail clear of the legs;
- images 2-4 are the same horse as image 1 seen from the stated side.

## When done

Reply with the four file paths, each file's size and resolution, and one short line describing each image. If a generation fails (for example a usage limit), say which images are missing and why.
