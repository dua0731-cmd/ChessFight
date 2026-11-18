namespace ChessFight.Gameplay
{
    // Royal Library plants (ArtSource/PawnRush/RoyalLibraryModels/rl_plants_model.py, seoungtest R687 11-18): the
    // houseplants modelled in Blender (Seunggyu: no Tripo), after the plants sheet GC_assets_02_plants.png.
    //   Front gallery, in R4's pots (Gallery.cs keeps its "Ceramic planter" / "Pot rim" covers): soil and plant only.
    //     RlPlantFiddle112  fiddle-leaf fig       on "Library gallery houseplant (28, -22.5, -112)"
    //     RlPlantPalm53     kentia palm           on "Library gallery houseplant (29, -22.5, -53)"
    //     RlPlantBloom35    flowering shrub       on "Library gallery houseplant (-28, -22.5, -35)"
    //   Chess room, pot and plant together (R5 leaves these four alone):
    //     RlConservatoryMonstera5  monstera, low cream bowl     on "Giant chess conservatory plant (104, -22, 5)"
    //     RlConservatoryFiddle90   fiddle-leaf fig, fluted pot  on "Giant chess conservatory plant (107, -22, 90)"
    //     RlConservatoryPalm142    kentia palm, brass tub       on "Giant chess conservatory plant (-105, -22, 142)"
    //     RlConservatoryFern12     fern, cream footed urn       on "Giant chess conservatory plant (-104, -22, 12)"
    // The blockout plant's colliders (if any) stay; its pieces only stop drawing.
    public sealed partial class RoyalLibraryDressing
    {
        static readonly string[] PlantParts = { "Broad tropical leaf*", "Leaf", "Plant trunk", "Soil" };
        static readonly string[] PlantAndPotParts =
            { "Broad tropical leaf*", "Leaf", "Plant trunk", "Soil", "Ceramic planter", "Pot rim" };

        static Hang Plant(string model, string name, string[] covers) => new Hang
        {
            zone = "Plants", model = model, on = name, under = name, covers = covers,
        };

        static readonly Hang[] Plants =
        {
            Plant("RlPlantFiddle112", "Library gallery houseplant (28, -22.5, -112)", PlantParts),
            Plant("RlPlantPalm53", "Library gallery houseplant (29, -22.5, -53)", PlantParts),
            Plant("RlPlantBloom35", "Library gallery houseplant (-28, -22.5, -35)", PlantParts),
            Plant("RlConservatoryMonstera5", "Giant chess conservatory plant (104, -22, 5)", PlantAndPotParts),
            Plant("RlConservatoryFiddle90", "Giant chess conservatory plant (107, -22, 90)", PlantAndPotParts),
            Plant("RlConservatoryPalm142", "Giant chess conservatory plant (-105, -22, 142)", PlantAndPotParts),
            Plant("RlConservatoryFern12", "Giant chess conservatory plant (-104, -22, 12)", PlantAndPotParts),
        };
    }
}
