using System.Collections.Generic;
using ChessFight.Game;
using UnityEngine;

namespace ChessFight.PawnRush
{
    public static partial class Course01v4Builder
    {
        // R342 (승규 11-04, beat 6 still blockout: blue tiled side walls, grey alcoves, the kit clock gate): the royal
        // balcony modelled in the hall's language (ArtSource/PawnRush/GrandStairHall/royal_balcony_v34.py). The static
        // FBX (side walls with arched niches, the guard knights' plinths, the clock gate's facade, clocks, transom
        // grilles, pediment with the crown, the balcony's face to the hall) stands in the hall's frame under its pivot;
        // the blockout pieces it replaces draw nothing and keep their colliders, as the hall's do. What moves rides its
        // obstacle: a walnut door on each kit gate, a steel hand on each clock hand, 승규's knight on each guard knight.
        const string BalconyModel = "PawnRushGrandStairHall/RoyalBalconyV34", BalconyMovers = "PawnRushGrandStairHall/RoyalBalconyMovers";
        const string ClockGate = "09 Clock gate (the palace door)";
        const float GuardKnightSize = 1.3f;   // 2.34 m across, 3.1 m high: clear of the gate's pilasters (r 1.17)

        static void RoyalBalcony(Transform pivot, Transform content, Material marble, Material clay)
        {
            var model = Resources.Load<GameObject>(BalconyModel);
            var hiddenText = Resources.Load<TextAsset>(BalconyModel + "_Hidden");
            var movers = Resources.Load<GameObject>(BalconyMovers);
            if (model == null || hiddenText == null || movers == null)
            {
                Debug.LogError("[대계단 홀] 발코니 모델을 불러오지 못했습니다: Resources/" + BalconyModel + ".fbx, _Hidden.txt, " + BalconyMovers + ".fbx");
                return;
            }
            int off = HideListed(content, hiddenText);
            var view = Object.Instantiate(model, pivot, false);
            view.name = "Royal balcony v34 model";
            foreach (var r in view.GetComponentsInChildren<Renderer>(true))
            {
                var slots = r.sharedMaterials;
                for (int i = 0; i < slots.Length; i++)
                {
                    string name = slots[i] != null ? slots[i].name : "";
                    slots[i] = BalconyMaterial(name) ?? PalaceMaterial(name) ?? (name.StartsWith("Marble") ? marble : clay);
                }
                r.sharedMaterials = slots;
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            }
            BalconyLanterns(view.transform);

            int doors = 0, hands = 0, knights = 0;
            var knight = Resources.Load<GameObject>(FallingKnightModel);
            foreach (var t in content.GetComponentsInChildren<Transform>(true))
            {
                bool onGate = t.parent != null && t.parent.name == ClockGate;
                if (onGate && (t.name == "Left gate" || t.name == "Right gate") && t.Find("Door model") == null)
                {
                    foreach (var r in t.GetComponentsInChildren<Renderer>(true)) r.enabled = false;
                    RideMover(t, movers, "Door", clay);
                    doors++;
                }
                else if (onGate && t.name == "Clock hand" && t.Find("Hand model") == null)
                {
                    foreach (var r in t.GetComponentsInChildren<Renderer>(true)) r.enabled = false;
                    RideMover(t, movers, "Hand", clay);
                    hands++;
                }
                else if (knight != null && t.name == "Visual" && t.parent != null && t.parent.name == "KnightRoot"
                         && t.parent.parent != null && t.parent.parent.name.StartsWith("17 Guard knight") && t.Find("Knight model") == null)
                {
                    foreach (var r in t.GetComponentsInChildren<Renderer>(true)) r.enabled = false;
                    Hang(t, knight, "Knight model", Vector3.zero, clay);
                    t.Find("Knight model").localScale *= GuardKnightSize;
                    knights++;
                }
            }
            Debug.Log("[대계단 홀] 왕실 발코니 모델 " + view.transform.childCount + "개 조각, 블록아웃 " + off + "개 숨김, 문 " + doors
                      + "개 · 시계 바늘 " + hands + "개 · 근위 기사 " + knights + "개를 새 모델로");
        }

        // Renderers whose path under `content` (spaces as '_', '#n' repeats dropped) is listed: they stop drawing, their
        // colliders stay. Returns how many.
        static int HideListed(Transform content, TextAsset list)
        {
            var hidden = new HashSet<string>();
            foreach (var line in list.text.Split('\n'))
            {
                var path = line.Trim();
                int mark = path.IndexOf('#');
                if (mark >= 0) path = path.Substring(0, mark);
                if (path.Length > 0) hidden.Add(path);
            }
            int off = 0;
            foreach (var r in content.GetComponentsInChildren<Renderer>(true))
            {
                if (!hidden.Contains(PathUnder(content, r.transform).Replace(' ', '_'))) continue;
                r.enabled = false;
                off++;
            }
            return off;
        }

        // One node of the movers' FBX riding `on` (built in its obstacle's frame: no offset, no turn). The whole model
        // is copied so the node keeps whatever the import put above it; the other nodes go.
        static void RideMover(Transform on, GameObject movers, string node, Material clay)
        {
            var view = Object.Instantiate(movers, on, false);
            view.name = node + " model";
            view.transform.localPosition = Vector3.zero;
            view.transform.localRotation = Quaternion.identity;
            var s = on.lossyScale;
            view.transform.localScale = new Vector3(1f / s.x, 1f / s.y, 1f / s.z);
            for (int i = view.transform.childCount - 1; i >= 0; i--)
            {
                var child = view.transform.GetChild(i);
                if (child.name != node) Object.DestroyImmediate(child.gameObject);
            }
            foreach (var r in view.GetComponentsInChildren<Renderer>(true))
            {
                var slots = r.sharedMaterials;
                for (int i = 0; i < slots.Length; i++)
                {
                    string name = slots[i] != null ? slots[i].name : "";
                    slots[i] = BalconyMaterial(name) ?? PalaceMaterial(name) ?? clay;
                }
                r.sharedMaterials = slots;
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            }
            foreach (var c in view.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(c);
        }

        // The balcony's own materials (royal_balcony_v34.py): crimson velvet (the niches, panels, cartouche, crowns'
        // caps), walnut (the doors, PR_Wood under the box UVs the doors carry), ivory enamel (the clock dials), ebony
        // (the chessboard frieze's dark squares, chapter rings), blued steel (the hands), ruby (the clocks' flags, the
        // crowns' stones) and the lanterns' glowing flames. Built once each; null for any other name.
        static readonly Dictionary<string, Material> balconyMaterials = new Dictionary<string, Material>();

        static Material BalconyMaterial(string name)
        {
            int cut = name.IndexOf(' ');
            if (cut >= 0) name = name.Substring(0, cut);           // "Balcony_Velvet (Instance)"
            if (!name.StartsWith("Balcony_")) return null;
            if (balconyMaterials.TryGetValue(name, out var cached) && cached != null) return cached;
            Material m;
            switch (name)
            {
                case "Balcony_Velvet": m = PieceFigure.Lit(new Color(.42f, .03f, .06f), .3f); break;
                case "Balcony_Ivory": m = PieceFigure.Lit(new Color(.94f, .9f, .81f), .75f); break;
                case "Balcony_Ebony": m = PieceFigure.Lit(new Color(.07f, .06f, .07f), .8f); break;
                case "Balcony_Steel": m = PieceFigure.Lit(new Color(.07f, .09f, .16f), .8f, .9f); break;
                case "Balcony_Ruby": m = PieceFigure.Lit(new Color(.7f, .04f, .06f), .75f); break;
                case "Balcony_Bulb":
                    m = LitMaterials.NewLit(new Color(1f, .9f, .64f), .6f);
                    m.name = "Balcony lantern flame";
                    LitMaterials.SetEmission(m, CandleLight * 10f);   // HDR, over the bloom's threshold
                    break;
                case "Balcony_Walnut":
                    var wood = Resources.Load<Texture2D>("PR_Wood_Albedo");
                    if (wood == null)
                    {
                        Debug.LogError("[대계단 홀] 문 나뭇결(Resources/PR_Wood_Albedo.png)을 불러오지 못했습니다");
                        m = PieceFigure.Lit(new Color(.3f, .17f, .1f), .45f);
                        break;
                    }
                    m = new Material(PieceFigure.Lit(new Color(.5f, .33f, .22f), .45f)) { name = "Balcony walnut", mainTexture = wood };
                    var grain = Resources.Load<Texture2D>("PR_Wood_Normal");
                    if (grain != null)
                    {
                        m.SetTexture("_BumpMap", grain);
                        m.EnableKeyword("_NORMALMAP");
                    }
                    break;
                default: return null;
            }
            return balconyMaterials[name] = m;
        }

        // A warm light in each urn lantern's flame on the gate's pylons (no shadows, as the hub chandelier's bulbs).
        static void BalconyLanterns(Transform model)
        {
            int lit = 0;
            foreach (var f in model.GetComponentsInChildren<MeshFilter>(true))
            {
                if (!f.name.StartsWith("Balcony_lantern_flame") || f.sharedMesh == null) continue;
                var light = new GameObject("Lantern light").AddComponent<Light>();
                light.transform.SetParent(f.transform, false);
                light.transform.localPosition = f.sharedMesh.bounds.center;
                Lamp(light, 12f, 5f);
                lit++;
            }
            if (lit == 0) Debug.LogError("[대계단 홀] 발코니 등불(Balcony_lantern_flame_*)이 모델에 없습니다");
        }
    }
}
