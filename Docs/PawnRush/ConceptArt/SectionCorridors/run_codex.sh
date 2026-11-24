#!/usr/bin/env bash
# Generates one corridor concept image per line of corridors.txt with Codex CLI image generation.
# Usage (repo root): bash Docs/PawnRush/ConceptArt/SectionCorridors/run_codex.sh [id ...]
set -u
REPO="$(cd "$(dirname "$0")/../../../.." && pwd)"
DIR="$REPO/Docs/PawnRush/ConceptArt/SectionCorridors"
CODEX="$(ls "$LOCALAPPDATA"/OpenAI/Codex/bin/*/codex.exe | head -1)"
want=" $* "
while IFS='|' read -r id refs place; do
  [ -z "$id" ] && continue
  if [ "$#" -gt 0 ] && [[ "$want" != *" $id "* ]]; then continue; fi
  prompt="$DIR/PROMPT_$id.md"
  {
    echo "You are generating concept art for a 3D modeler. Use your image generation tool; do NOT write code. Generate exactly ONE image and copy the generated PNG into the current working directory as \`$id.png\` (never resize, crop or convert it). Call the tool at most twice: if the first result breaks the CHECK line, generate once more naming the failed checks first and keep the better one. This prompt is complete: read no other file and change no other file. After saving, reply with the file path and one short line describing it."
    echo
    echo "The attached images are references of THE PLACE described below (style, materials, colours). Do not edit or reuse a reference image; build a new camera and a new composition."
    echo
    echo "Build the image prompt as: WORLD + CORRIDOR + THE PLACE + SHOT + AVOID, each word for word."
    echo
    cat "$DIR/PROMPT_COMMON.md"
    echo
    echo "## THE PLACE (paste word for word into the image prompt)"
    echo
    echo "$place"
    echo
    echo "CHECK: corridor straight, flat, empty floor; 12 m wide (wide enough for twelve pawns abreast); three bays per wall between four piers; style clearly the place's; no text anywhere."
  } > "$prompt"
  args=()
  IFS=';' read -ra list <<< "$refs"
  for r in "${list[@]}"; do args+=(-i "$REPO/$r"); done
  ( "$CODEX" exec --skip-git-repo-check --sandbox workspace-write -C "$DIR" "${args[@]}" - < "$prompt" > "$DIR/codex_log_$id.txt" 2>&1
    echo "$id exit $? $( [ -f "$DIR/$id.png" ] && echo png-ok || echo png-missing )" ) &
done < "$DIR/corridors.txt"
wait
echo all-done
