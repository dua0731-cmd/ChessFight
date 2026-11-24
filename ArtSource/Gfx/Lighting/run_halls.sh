#!/bin/bash
# JY-gfx 11-24: the halls' lighting designs into the game and their pictures, in one go:
#   designs (designs/<key>.design.json) -> GfxMapLights (looks, rigs, new maps) and GfxMapPlans (the plans)
#   -> the batch copy -> each hall frozen as the game builds it (without its old bake) -> each baked -> pictures.
# Usage: run_halls.sh <tag> [sections, comma-separated prefab names] [skip: designs|freeze|bake]
set -u
N=/c/cf-gfx/night
C=/c/cf-gfx/proj
U="/c/Program Files/Unity/Hub/Editor/6000.3.11f1/Editor/Unity.exe"
TAG=$1
ALL=Mid_2-1_GrandStairHall,Mid_2-2_RoyalClockTower,Mid_MG_B_RepairWing,Mid_MG_E_RoyalLibrary,Mid_MG_F_ClockworkBallroom,Mid_MG_J_PalaceLaundry,Late_3-1_RoyalLibrary,Late_3-2_RoyalObservatory,Late_3-3_RoyalVault,Late_MG_G_RoyalProcession,Late_MG_H_KingsChessHall,Late_MG_I_KingsBedchamber,Final_WarRoom
SECTIONS=${2:-$ALL}
SKIP=${3:-}
W='C:\cf-gfx'

if [ "$SKIP" != "designs" ] && [ "$SKIP" != "freeze" ] && [ "$SKIP" != "bake" ]; then
  python $N/apply_designs.py $N/designs || exit 1
  python $N/gen_plans.py $N/designs $N/src/GfxMapPlans.cs || exit 1
fi
cp $N/src/GfxMapLights.cs $N/src/GfxMapPlans.cs $N/src/GfxSections.cs $N/src/GfxBakedLight.cs $C/Assets/Scripts/PawnRush/
cp $N/src/GfxShots.cs $N/src/GfxBake.cs $C/Assets/Scripts/PawnRushEditor/

if [ "$SKIP" != "freeze" ] && [ "$SKIP" != "bake" ]; then
  echo "== freeze"
  "$U" -batchmode -projectPath "$W\\proj" -executeMethod ChessFight.PawnRush.Editor.GfxShots.Run -gfxOut "$W\\shots\\${TAG}_freeze" -gfxTag fz \
    -gfxSections "$SECTIONS" -gfxSpots 1 -gfxFreeze -gfxMsaa 4 -gfxTune 'now:bake=0' -logFile "$W\\night\\${TAG}_freeze.log"
  echo "freeze exit $?"
  grep -a -E 'frozen as|error CS|FAIL' "$N/${TAG}_freeze.log" | cut -c1-200
fi
if [ "$SKIP" != "bake" ]; then
  echo "== bake"
  KEYS=$(echo "$SECTIONS" | tr ',' '\n' | sed -E 's/^(Early|Mid|Late|Final)_//' | paste -sd, -)
  "$U" -batchmode -projectPath "$W\\proj" -executeMethod ChessFight.PawnRush.Editor.GfxBake.Bake -bakeSections "$KEYS" \
    -bakeOut "$W\\shots\\${TAG}_bake" -bakeTexels 10 -bakeBounces 3 -bakeSamples 512 -logFile "$W\\night\\${TAG}_bake.log"
  echo "bake exit $?"
  cat "/c/cf-gfx/shots/${TAG}_bake/bake_notes.txt"
fi
echo "== pictures"
"$U" -batchmode -projectPath "$W\\proj" -executeMethod ChessFight.PawnRush.Editor.GfxShots.Run -gfxOut "$W\\shots\\${TAG}" -gfxTag "$TAG" \
  -gfxSections "$SECTIONS" -gfxSpots 1,2,3,top,sky -gfxMsaa 4 -gfxTune 'now:' -logFile "$W\\night\\${TAG}.log"
echo "pictures exit $?"
grep -a -E 'baked light on|FAIL|failed [1-9]' "/c/cf-gfx/shots/${TAG}/notes_${TAG}.txt" | cut -c1-200
ls "/c/cf-gfx/shots/${TAG}" | grep -c png
