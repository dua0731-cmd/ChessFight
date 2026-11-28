namespace ChessFight.PawnRush
{
    // B3 the royal dais and the king's balcony (seoungtest R837; ArtSource/PawnRush/BanquetHall/bh_b3_dais_model.py, shapes
    // after the R836 sheet BH_A06): the dais's marble front and top, its balustrades and the gallery's end walls (the
    // boarding lamps' globes stay the builder's - they light - on the models' gold cups), the head tables under draped
    // cloths with their candelabra, the balcony with its canopy, the two thrones. Colliders and floors stay the builder's.
    public sealed partial class GrandBanquetHallDressing
    {
        static readonly Hang[] DaisHangs =
        {
            new Hang { zone = "B3", model = "bh_dais", covers = new[] { "BH royal dais", "BH dais edge" } },
            new Hang { zone = "B3", model = "bh_dais_rails",
                       covers = new[] { "BH balustrade", "BH gallery end W", "BH gallery end E", "BH gallery end NE", "BH boarding post" } },
            new Hang { zone = "B3", model = "bh_head_tables", covers = new[] { "BH head table W", "BH head table E", "BH head table candle", "BH head table flame" } },
            new Hang { zone = "B3", model = "bh_balcony",
                       covers = new[] { "BH balcony", "BH balcony edge", "BH balcony rail W", "BH balcony rail E", "BH canopy" } },
            new Hang { zone = "B3", model = "bh_thrones", covers = new[] { "BH throne" } },
        };

        // R904 (user 11-28: "대연회장 깜빡거리는 부분 ... 고쳐주고"): the dais's marble lies on the hall's west, east and north
        // walls at once (and its panels flicker with theirs), which no single push clears - so every level of it is drawn
        // DaisInset narrower on each side, about its own middle, across the floor (never up). CoplanarScan keeps the dais
        // where this leaves it (its Target.fixedModels) and moves what else meets it.
        const float DaisInset = .006f;

        static readonly System.Action<GrandBanquetHallDressing> DaisOffWalls = d =>
        {
            foreach (var model in d.FindAll("bh_dais (model)"))
                foreach (var mf in model.GetComponentsInChildren<UnityEngine.MeshFilter>(true))
                {
                    var t = mf.transform;
                    var mesh = mf.sharedMesh;
                    if (mesh == null) continue;
                    var b = mesh.bounds;
                    var scale = UnityEngine.Vector3.one;
                    var shift = UnityEngine.Vector3.zero;
                    for (int axis = 0; axis < 3; axis++)
                    {
                        var dir = UnityEngine.Vector3.zero;
                        dir[axis] = 1;
                        var world = t.TransformVector(dir);   // one local unit, in metres and direction
                        if (UnityEngine.Mathf.Abs(world.normalized.y) > .7f || b.size[axis] < 1e-4f) continue;
                        float inset = DaisInset / world.magnitude;   // local units
                        float f = UnityEngine.Mathf.Max(.5f, (b.size[axis] - 2 * inset) / b.size[axis]);
                        scale[axis] = f;
                        shift[axis] = b.center[axis] * (1 - f);   // the middle stays put
                    }
                    var ls = t.localScale;
                    t.localPosition += t.localRotation * UnityEngine.Vector3.Scale(ls, shift);
                    t.localScale = UnityEngine.Vector3.Scale(ls, scale);
                }
        };

        // R845: the dais's show (BanquetDaisShow.cs): the head tables' candelabra burn, the balcony showers confetti on
        // whoever reaches it (seen on each PC, nothing sent). Pictures only.
        static readonly System.Action<GrandBanquetHallDressing> DaisShow = d =>
            d.gameObject.AddComponent<BanquetDaisShow>().Configure(d);
    }
}
