# Builds the eight ready-to-paste image prompts of the Royal Carriage Yard concept art (R676) from PROMPT.md:
# REFS (scenes or sheets) + STYLE + CUES + the image's entry without its CHECK line + AVOID, word for word, and the
# short version for tools without attachments. Standard library only.
# Run: python rcy_assemble_prompts.py [<folder with PROMPT.md>]
import os
import re
import sys

here = os.path.dirname(os.path.abspath(__file__))
folder = sys.argv[1] if len(sys.argv) > 1 else os.path.join(here, '..', '..', '..', 'Docs', 'PawnRush', 'ConceptArt', 'R676_RoyalCarriageYard')
folder = os.path.abspath(folder)
lines = open(os.path.join(folder, 'PROMPT.md'), encoding='utf-8').read().replace('\r\n', '\n').split('\n')


def block(label):
    head = '**' + label + '**'
    found = [l[len(head):].strip() for l in lines if l.startswith(head)]
    if len(found) != 1:
        raise SystemExit('expected one %s block, found %d' % (label, len(found)))
    return found[0]


refs_scene = block('REFS (images 1-6):')
refs_sheet = block('REFS (images 7-8):')
style = block('STYLE:')
cues = block('CUES:')
avoid = block('AVOID:')

entries = []
i = 0
while i < len(lines):
    m = re.match(r'^### (\d)\. (RCY_\d\d_[a-z_]+)\.png\s*$', lines[i])
    if not m:
        i += 1
        continue
    body = []
    i += 1
    while i < len(lines) and not lines[i].startswith('### ') and not lines[i].startswith('## '):
        if not lines[i].startswith('- CHECK:'):
            body.append(lines[i])
        i += 1
    while body and not body[0].strip():
        body.pop(0)
    while body and not body[-1].strip():
        body.pop()
    entries.append((int(m.group(1)), m.group(2), '\n'.join(body)))

if [n for n, _, _ in entries] != list(range(1, 9)):
    raise SystemExit('expected entries 1-8, found %s' % [n for n, _, _ in entries])


def write(name, text):
    with open(os.path.join(folder, name), 'w', encoding='ascii', newline='\r\n') as f:
        f.write(text)
    print('%s  %d chars' % (name, len(text)))


for n, name, body in entries:
    refs = refs_scene if n <= 6 else refs_sheet
    write(name + '_prompt.txt', '%s\n\nSTYLE: %s\n\nCUES: %s\n\n%s\n\nAVOID: %s\n' % (refs, style, cues, body, avoid))

start = next(k for k, l in enumerate(lines) if l.startswith('## Short version'))
short = '\n'.join(lines[start + 1:]).strip()
write('RCY_short_prompt.txt', short + '\n')
