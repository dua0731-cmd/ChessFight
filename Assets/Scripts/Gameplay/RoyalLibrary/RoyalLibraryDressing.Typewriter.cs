namespace ChessFight.Gameplay
{
    // R3 typewriter zone (ArtSource/PawnRush/RoyalLibraryModels/PLAN.md R3, rl_r3_typewriter_model.py, 11-18): the
    // E5 typewriter, key, hint sign and course tables over blockout collection 04 Typewriter (z -60 .. -8).
    // Only renderers stop; colliders, key triggers and rigidbodies stay the prefab's. Not covered on purpose:
    //   "Ivory letter disk" - TypewriterStairs.keyVisuals, flashed red on a wrong key, and the face the letter sits on:
    //   reskinned instead (its own renderer keeps the flash; the face's rim 0.177 is under the letter 0.20); "Key letter", "Random clue glyph", "Printed order text",
    //   "Carriage branding" - the game's TextMesh labels (hiding a parent would hide them too).
    public sealed partial class RoyalLibraryDressing
    {
        const string TypewriterRoot = "Typewriter stairs - printed command";

        static readonly Hang[] Typewriter = TypewriterHangs();

        static Hang[] TypewriterHangs()
        {
            var keys = new[]
            {
                "Key 0-0", "Key 0-1", "Key 0-2", "Key 0-3", "Key 0-4", "Key 1-0", "Key 1-1", "Key 1-2", "Key 1-3",
                "Key 2-0", "Key 2-1", "Key 2-2", "Key 2-3", "Key 2-4", "Key 3-0", "Key 3-1", "Key 3-2", "Key 3-3",
                "Key 4-0", "Key 4-1", "Key 4-2", "Key 4-3", "Key 4-4",
            };
            var list = new System.Collections.Generic.List<Hang>
            {
                new Hang
                {
                    zone = "Typewriter", model = "RlTypewriter", under = TypewriterRoot,
                    covers = new[]
                    {
                        "Black enamel keyboard bed", "Curved front cheek*", "Low keyboard cheek*", "Polished side rail*",
                        "Front brass rim", "Enamel side housing*", "Inner sill", "Typewriter interior descent",
                        "Inside typewriter wall*", "Inside typewriter roof", "Type basket fascia", "Type bar*",
                        "Ink ribbon spool*", "Spool brass hub*", "Ribbon guide*", "Crimson ink ribbon",
                        "Black platen roller", "Carriage knob*", "Carriage steel rail", "Carriage nameplate",
                        "Paper pressure bar", "Paper backing", "Printed paper line*", "Keyboard instruction plate",
                        "Rubber foot*",
                    },
                },
                new Hang { zone = "Typewriter", model = "RlTwKeyStems", under = TypewriterRoot, covers = new[] { "Key stem*" } },
                // 11-18: the 23 letter disks become the concept's dished ivory faces in place (one ivory slot only:
                // the game's red flash goes on this renderer, and must not tint the brass)
                new Hang { zone = "Typewriter", model = "RlTwKeyFace", on = "Ivory letter disk", reskin = true },
                new Hang { zone = "Typewriter", model = "RlTwTableFront", covers = new[] { "Approach floor -60*" } },
                new Hang { zone = "Typewriter", model = "RlTwTableExit", covers = new[] { "Approach floor -27*" } },
                new Hang { zone = "Typewriter", model = "RlTwFurnitureBase", covers = new[] { "Typewriter furniture base*" } },
            };
            // one cap FBX (written in Key 0-0's frame) rides every key; the blockout rim and bezel stop drawing
            foreach (var key in keys)
                list.Add(new Hang { zone = "Typewriter", model = "RlTwKey", on = key, under = key,
                                    covers = new[] { "Round brass key rim*", "Black key bezel*" } });
            // the hint sign (written in Memory clue 1's frame) on all five clues
            for (int i = 1; i <= 5; i++)
            {
                string clue = "Memory clue " + i;
                list.Add(new Hang { zone = "Typewriter", model = "RlTwClueSign", on = clue, under = clue,
                                    covers = new[] { "Brass clue frame*", "Clue paper*" } });
            }
            return list.ToArray();
        }
    }
}
