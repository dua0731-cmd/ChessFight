using System.Globalization;
using ChessFight.Game;
using UnityEngine;

namespace ChessFight.PawnRush
{
    public static partial class Course01v4Builder
    {
        // R361 (승규 11-04, an asset preview of cream stone walls and a bridge arch with gold bands, crown plaques,
        // candelabra, crown banners and a lantern-posted balustrade: "입구쪽 내가 준 에셋 지워주고 모양은 인 게임에 있는
        // 모양으로 해주고 1번 째 사진처럼 모델링 해줘" + "깃발은 빨간색으로"): the entrance built by hand on the castle
        // archway's footprint (entrance_v46.py): the arch, the passage walls along the porch, the wings with their three
        // windows, the masonry under them. The hall model's archway and bases are switched off; the new model stands in
        // the hall's frame under its pivot; a warm light at each candelabrum and lantern (EntranceV46_Lights.txt, written
        // by the script in Blender's frame: the hall's x, z, y). Pictures only - the blockout keeps its colliders.
        // R362 ("옆쪽도 교체해줘 창문 막혀 있는 걸로"): the foyer's side walls the same way (foyer_sides_v47.py), their
        // windows blind recesses with candelabra; the hall model's Foyer_side_E/W and their tracery switched off.
        const string EntranceModel = "PawnRushGrandStairHall/EntranceV46", FoyerSidesModel = "PawnRushGrandStairHall/FoyerSidesV47";
        static readonly string[] OldEntrance = { "HallV15_Entrance_archway", "HallV15_Entrance_base_E", "HallV15_Entrance_base_W" };
        static readonly string[] OldFoyerSides = { "HallV15_Foyer_side_E", "HallV15_Foyer_side_W", "HallV15_Foyer_side_E tracery", "HallV15_Foyer_side_W tracery" };
        static Material entranceRed, entranceBronze;

        static void Entrance(Transform pivot, Transform hallModel, Material clay)
        {
            HandBuilt(pivot, hallModel, clay, EntranceModel, OldEntrance, "입구");
            HandBuilt(pivot, hallModel, clay, FoyerSidesModel, OldFoyerSides, "현관 옆벽");
        }

        // One hand-built piece: the hall model's nodes it replaces off, its model under the hall's pivot in the hall's
        // materials, a warm light at each place its _Lights.txt names (Blender's frame: the hall's x, z, y).
        static void HandBuilt(Transform pivot, Transform hallModel, Material clay, string path, string[] replaces, string label)
        {
            var model = Resources.Load<GameObject>(path);
            var lightsText = Resources.Load<TextAsset>(path + "_Lights");
            if (model == null)
            {
                Debug.LogError("[대계단 홀] " + label + " 모델을 불러오지 못했습니다: Resources/" + path + ".fbx");
                return;
            }
            int off = 0;
            foreach (var t in hallModel.GetComponentsInChildren<Transform>(true))
                if (System.Array.IndexOf(replaces, t.name) >= 0) { t.gameObject.SetActive(false); off++; }
            var view = Object.Instantiate(model, pivot, false);
            view.name = model.name + " model";
            foreach (var r in view.GetComponentsInChildren<Renderer>(true))
            {
                var slots = r.sharedMaterials;
                for (int i = 0; i < slots.Length; i++)
                {
                    string name = slots[i] != null ? slots[i].name : "";
                    // R365: the masonry's top where it shows between the porch floor and the walls is floor (Marble_*).
                    slots[i] = EntranceMaterial(name) ?? BalconyMaterial(name) ?? PalaceMaterial(name)
                             ?? (name.StartsWith("Marble") ? HallMarble() : clay);
                }
                r.sharedMaterials = slots;
                // The glowing glass and flames cast no shadow over their own lights.
                r.shadowCastingMode = r.name.Contains("flames") ? UnityEngine.Rendering.ShadowCastingMode.Off : UnityEngine.Rendering.ShadowCastingMode.On;
            }
            int lit = 0;
            if (lightsText != null)
            {
                foreach (var line in lightsText.text.Split('\n'))
                {
                    var f = line.Trim().Split(' ');
                    if (f.Length < 4 || f[0].StartsWith("#")) continue;
                    float x = float.Parse(f[1], CultureInfo.InvariantCulture), y = float.Parse(f[2], CultureInfo.InvariantCulture),
                          z = float.Parse(f[3], CultureInfo.InvariantCulture);
                    bool lantern = f[0] == "lantern";
                    var light = new GameObject(label + (lantern ? " lantern light" : " candle light")).AddComponent<Light>();
                    light.transform.SetParent(pivot.parent, false);
                    light.transform.localPosition = new Vector3(x, z, y);
                    Lamp(light, lantern ? 9f : 6f, lantern ? 4f : 2.5f);
                    lit++;
                }
            }
            else Debug.LogError("[대계단 홀] " + label + " 빛 자리(Resources/" + path + "_Lights.txt)를 불러오지 못했습니다");
            Debug.Log("[대계단 홀] 새 " + label + " 모델 " + view.transform.childCount + "개 조각, 옛 조각 " + off + "개 끔, 빛 " + lit + "개");
        }

        // The entrance's own materials: the banners' red cloth, the plaques' bronze. Null for any other name.
        static Material EntranceMaterial(string name)
        {
            if (name.StartsWith("Entrance_Red"))
            {
                if (entranceRed == null) entranceRed = PieceFigure.Lit(new Color(.62f, .03f, .04f), .25f);
                return entranceRed;
            }
            if (name.StartsWith("Entrance_Bronze"))
            {
                if (entranceBronze == null) entranceBronze = PieceFigure.Lit(new Color(.32f, .18f, .09f), .6f, .6f);
                return entranceBronze;
            }
            return null;
        }
    }
}
