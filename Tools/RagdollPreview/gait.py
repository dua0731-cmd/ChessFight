import numpy as np
from render import Euler, Rx, Ry, Rz, chain, lowest_foot, render

STAND = 0.256
CURRENT = dict(moveSpeed=5.5, sprintSpeed=9.6, hopCadence=3.0, sprintCadence=3.2,
    legSwing=50, sprintLegSwing=140, armSwing=40, sprintArmSwing=76, armRestDown=20, runArmDown=55,
    chestLean=10, runLean=7, sprintLean=10, runLift=0.01, sprintLift=0.05, stepBob=0.01, runStepDip=0.03,
    sprintBob=0.06, stepRoll=1.5, sprintRoll=12, runTwist=7, boundGait=0.0, armStyle='yaw')

def lerp(a, b, t): return a + (b - a) * t

def pose(P, blend, g, speedN=1.0, clampLegs=True):
    s = np.sin(g)
    legAmp = lerp(P['legSwing'], P['sprintLegSwing'], blend) * min(1, speedN * 1.5)
    armAmp = lerp(P['armSwing'], P['sprintArmSwing'], blend) * speedN
    thL, thR = -legAmp * s, legAmp * s
    if clampLegs:
        thL, thR = np.clip(thL, -60, 60), np.clip(thR, -60, 60)
    loc = {}
    loc['ThighL'] = Euler(thL, 0, 0); loc['ThighR'] = Euler(thR, 0, 0)
    loc['FootL'] = Euler(-0.8 * thL, 0, 0); loc['FootR'] = Euler(-0.8 * thR, 0, 0)
    twist = P.get('runTwist', 0) * (1 - blend) * speedN * s
    loc['Chest'] = Euler(P['chestLean'] * speedN, -twist, 0)
    loc['Head'] = Euler(-0.5 * P['chestLean'] * speedN, 0.8 * twist, 0)
    down = lerp(P['armRestDown'], lerp(P['runArmDown'], P['armRestDown'], blend), min(1, speedN * 2))
    style = P.get('armStyle', 'yaw')
    if style == 'yaw' or blend >= 0.999:
        aL = Euler(0, -armAmp * s, down); aR = Euler(0, -armAmp * s, -down)
    else:
        # pendulum: lower first, then swing about the body's side-to-side axis
        aL = Euler(armAmp * s, 0, down); aR = Euler(-armAmp * s, 0, -down)
    loc['ArmL'] = aL; loc['ArmR'] = aR
    spread = abs(s)
    lift = lerp(P['runLift'], P['sprintLift'], blend)
    runBob = P['stepBob'] * (1 - spread) - P['runStepDip'] * spread
    if P.get('bobStyle') == 'legs':
        # ride exactly on the legs: a straight leg swung by a degrees is shorter by reach*(1-cos a)
        ang = min(abs(legAmp * s), 60.0)
        runBob = P['stepBob'] * (1 - spread) - P.get('runLegDrop', 1.0) * 0.151 * (1 - np.cos(np.radians(ang)))
    if P.get('bobStyle') == 'footfall':
        # dip as each foot lands (twice a cycle), rise through the step
        runBob = P['stepBob'] * np.sin(2 * g + P.get('bobPhase', 0.0)) * 0.5 + P['stepBob'] * 0.5 - P['runStepDip'] * spread
    bob = lerp(runBob, P['sprintBob'] * spread, blend) * speedN
    roll = lerp(P['stepRoll'], P['sprintRoll'], blend) * s * speedN
    lean = lerp(P['runLean'], P['sprintLean'], blend) * speedN
    hips_rot = Rx(lean) @ Rz(roll)
    return loc, hips_rot, STAND + lift + bob

def frames(P, blend, seconds=1.0, fps=30, view='back', clampLegs=True, dist=2.2):
    cad = lerp(P['hopCadence'], P['sprintCadence'], blend)
    speed = lerp(P['moveSpeed'], P['sprintSpeed'], blend)
    out = []; floats = []
    for k in range(int(seconds * fps)):
        t = k / fps
        g = 2 * np.pi * cad * t
        loc, hr, hy = pose(P, blend, g, clampLegs=clampLegs)
        z = speed * t
        bodies = chain((0, hy, z), hr, loc)
        lo = lowest_foot(bodies)
        if lo < 0:   # feet cannot go through the floor: the legs push the body up
            bodies = chain((0, hy - lo, z), hr, loc); lo = 0
        floats.append(lo)
        tgt = np.array([0, 0.5, z])
        eye = tgt + (np.array([0.0, 0.55, -dist]) if view == 'back' else np.array([dist, 0.45, 0.0]))
        out.append(render(bodies, eye, tgt))
    return out, floats

def sheet(imgs, cols, path, label=None):
    from PIL import Image, ImageDraw
    W, H = imgs[0].size; rows = (len(imgs) + cols - 1) // cols
    s = Image.new('RGB', (W * cols, H * rows + (24 if label else 0)), 'white')
    for i, im in enumerate(imgs): s.paste(im, ((i % cols) * W, (i // cols) * H + (24 if label else 0)))
    if label: ImageDraw.Draw(s).text((6, 5), label, fill=(0, 0, 0))
    s.save(path)
