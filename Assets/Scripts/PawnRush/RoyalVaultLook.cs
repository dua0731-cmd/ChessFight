using UnityEngine;

namespace ChessFight.PawnRush
{
    // The royal vault's grey-box look (R725; build spec §0.1 D2 and § Conventions): every model in the vault wears the one
    // neutral Greybox material, so its shapes alone carry the concept until the art pass. The only other colours are the
    // gameplay cues, separate "Cue ..." objects in CueRed / CueGreen / CueGold, and Ink for sign text, the door's countdown
    // digits and the belt slats. No textures: none of these names is in Arena's KitLook or KitSurface tables, so Arena.Lit
    // gives each the plain colour it is asked for (and caches it, so every call returns the same material).
    public static class RoyalVaultLook
    {
        // Sign text, countdown digits and slats; go-now green; stop and warning red (the cue vocabulary, RoyalVaultRules.Cue).
        public static readonly Color InkColor = Arena.Hex("#292433"), GreenColor = Arena.Hex("#59FF73"), RedColor = Arena.Hex("#FF4A3A");
        static readonly Color GreyColor = Arena.Hex("#BDB9B1"), GoldColor = Arena.Hex("#E2B23C");

        // Every model: floors, walls, rails, movers, landmarks.
        public static Material Greybox => Arena.Lit("M_VaultGreybox", GreyColor, 0f, .9f);
        // Cue objects only. Red: Stop, and Warn while it blinks; green: Go; gold: a static edge to step or jump from or to.
        public static Material CueRed => Arena.Lit("M_VaultCueRed", RedColor, 1.6f, .35f);
        public static Material CueGreen => Arena.Lit("M_VaultCueGreen", GreenColor, 1.2f, .35f);
        public static Material CueGold => Arena.Lit("M_VaultCueGold", GoldColor, .6f, .45f);
        // The belt slats (the signs and digits are TextMeshes coloured InkColor).
        public static Material Ink => Arena.Lit("M_VaultInk", InkColor, 0f, .9f);

        static SectionKit kit;

        // A section kit made at run time (and in the Editor, where the validators build the vault) whose every material is
        // Greybox, so whatever CourseBuilder makes from it - floors, walls, rails, slopes - comes out in the vault's one
        // neutral material. CourseKit is abstract (SectionKit.cs), so it is a SectionKit; its obstacle prefabs stay empty
        // (the vault places none). Never saved: it lives only as long as the session that made it.
        public static SectionKit Kit
        {
            get
            {
                if (kit == null)
                {
                    kit = ScriptableObject.CreateInstance<SectionKit>();
                    kit.name = "RoyalVaultKit";
                    kit.hideFlags = HideFlags.DontSave;
                }
                // Painted again if the cached Greybox was ever replaced (a destroyed material made anew by Arena.Lit).
                var grey = Greybox;
                if (kit.floorChecker != grey)
                {
                    kit.floorChecker = grey;
                    kit.wallClimbable = grey;
                    kit.wallNoClimb = grey;
                    kit.glass = grey;
                    kit.trimWhite = grey;
                    kit.trimBlack = grey;
                    kit.rankGold = grey;
                    kit.boardLight = grey;
                    kit.boardDark = grey;
                    kit.hazard = grey;
                    kit.marker = grey;
                    kit.respawnFloor = grey;
                }
                return kit;
            }
        }

        // A sign in ink (named "Label <text>" by YardKit.Label). The vault's signs stand at yaw 0, so they read to players
        // coming from -z.
        public static TextMesh Sign(Transform parent, string text, Vector3 at, float size) =>
            YardKit.Label(parent, text, at, size, InkColor);
    }
}
