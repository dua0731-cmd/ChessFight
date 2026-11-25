#!/usr/bin/env bash
# bh_lod.py over every banquet-hall model (bh-hall session, R838, 2026-11-25). Run from anywhere:
#   bash ArtSource/PawnRush/BanquetHall/lod/run_all.sh [fbx|parts|all]
# FBX are written (--write) only while Unity is not in Play (Editor.log: the last "Reloading assemblies for play
# mode" must come before the last "Temp/__Backupscenes" load, which Unity does on leaving Play).
cd "$(dirname "$0")/.." || exit 1
BLENDER="/c/Program Files/Blender Foundation/Blender 5.2/blender.exe"
LOG="$LOCALAPPDATA/Unity/Editor/Editor.log"
what=${1:-all}

in_play() {
    [ -f "$LOG" ] || return 1
    local p b
    p=$(grep -a -n "Reloading assemblies for play mode" "$LOG" | tail -1 | cut -d: -f1)
    b=$(grep -a -n "Temp/__Backupscenes" "$LOG" | tail -1 | cut -d: -f1)
    [ -n "$p" ] && { [ -z "$b" ] || [ "$p" -gt "$b" ]; }
}

if [ "$what" = fbx ] || [ "$what" = all ]; then
    for f in B1/bh_counter B1/bh_kitchen B1/bh_kitchen_leaf_E B1/bh_kitchen_leaf_W B1/bh_pantry B1/bh_screens \
             B1/bh_waiter B2/bh_cloche B2/bh_dumbwaiter B2/bh_guests B2/bh_magician B2/bh_platter B2/bh_table_body \
             B2/bh_table_chairs B2/bh_table_cloth B2/bh_table_ware B3/bh_balcony B3/bh_dais B3/bh_dais_rails \
             B3/bh_head_tables B3/bh_thrones B4/bh_couple B4/bh_dance_floor; do
        w=--write
        if in_play; then w=""; echo "LOD_RUN Unity in Play: $f judged only"; fi
        "$BLENDER" -b --factory-startup --python bh_lod.py -- --fbx "$f" $w 2>&1 | grep -E "BH_LOD|Error:|Traceback" | grep -v "Shadow buffer" | sed "s|^|$f |"
    done
fi
if [ "$what" = parts ] || [ "$what" = all ]; then
    for p in b7:window_bay b7:panel_bay b7:pilaster b7:arch_doorway b7:arch_tripo b7:sconce b7:coffer b7:rosette \
             b7:banner b7:fireplace b6l:loft_rail b6l:loft_face b6l:loft_ladder b6c:hanging b6c:great b5:stage_front \
             b5:pelmet b5:pleat b5:bass b5:buffet b5:cake b5:fountain; do
        "$BLENDER" -b --factory-startup --python bh_lod.py -- --part "$p" 2>&1 | grep -E "BH_LOD|Error:|Traceback" | grep -v "Shadow buffer" | sed "s|^|$p |"
    done
fi
echo "LOD_RUN done"
