using System;
using System.Collections.Generic;
using UnityEngine;

namespace ChessFight.PawnRush
{
    public enum ModuleId
    {
        M0_Start, M1_SpinBarYard, M2_PusherCorridor, M3_ThirdRank, M4_ClockTerrace, M5_DiscPit,
        M6_TeamSection1, M7_RotatingBoard, M8_PendulumHall, M9_FoldingBridges, M10_KnightPlaza,
        M11_TeamSection2, M12_ThreeBridges, M13_RookWallPlaza, M14_EighthRank,
        X1_ConveyorHill, X2_HammerHall, X3_FastBarIsland
    }

    // The order of the modules and which are on (design doc §6: Course01Layout).
    // The assembler places them one after another; turning an extension on or off
    // moves everything behind it. A module with a prefab uses it, one without is
    // built from code (Course01Modules) - the "Build Course01" menu bakes each
    // code-built module into Prefabs/Modules once and fills the prefab in here.
    [CreateAssetMenu(menuName = "ChessFight/Pawn Rush/Course Layout", fileName = "Course01Layout")]
    public sealed class Course01Layout : ScriptableObject
    {
        [Serializable]
        public sealed class Entry
        {
            public ModuleId module;
            public bool enabled = true;
            [Tooltip("Baked module prefab. Empty = built from code.")]
            public GameObject prefab;

            public Entry() { }

            public Entry(ModuleId module, bool enabled)
            {
                this.module = module;
                this.enabled = enabled;
            }
        }

        public List<Entry> entries = Default();

        // The base course with the three extensions in their places, switched off.
        public static List<Entry> Default() => new List<Entry>
        {
            new Entry(ModuleId.M0_Start, true), new Entry(ModuleId.M1_SpinBarYard, true),
            new Entry(ModuleId.M2_PusherCorridor, true), new Entry(ModuleId.M3_ThirdRank, true),
            new Entry(ModuleId.M4_ClockTerrace, true), new Entry(ModuleId.X1_ConveyorHill, false),
            new Entry(ModuleId.M5_DiscPit, true), new Entry(ModuleId.M6_TeamSection1, true),
            new Entry(ModuleId.M7_RotatingBoard, true), new Entry(ModuleId.X2_HammerHall, false),
            new Entry(ModuleId.M8_PendulumHall, true), new Entry(ModuleId.M9_FoldingBridges, true),
            new Entry(ModuleId.M10_KnightPlaza, true), new Entry(ModuleId.M11_TeamSection2, true),
            new Entry(ModuleId.M12_ThreeBridges, true), new Entry(ModuleId.X3_FastBarIsland, false),
            new Entry(ModuleId.M13_RookWallPlaza, true), new Entry(ModuleId.M14_EighthRank, true),
        };

        void Reset() => entries = Default();
    }
}
