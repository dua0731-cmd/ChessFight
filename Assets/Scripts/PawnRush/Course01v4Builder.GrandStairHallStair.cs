using System.Collections.Generic;
using ChessFight.Game;
using UnityEngine;

namespace ChessFight.PawnRush
{
    public static partial class Course01v4Builder
    {
        // R350 (승규 11-04, an asset preview of a cream marble grand stair with a red runner, gold balustrades and crown
        // lanterns: "계단 부분도 고퀄리티로 모델링 해줘 텍스쳐 입혀주고"): the grand stair (beat 1) in its own model
        // (royal_stair_v45.py, the hall's frame, under its pivot): marble steps with bullnose nosings and gold inlays, a
        // crimson runner in the hall's carpet texture with gold bands and rods, palace-stone side walls with pilasters,
        // gold-beaded panels and medallions, gold balustrades on stone curbs, eight newels with glowing crown lanterns, and
        // an iron drain grille in a gold frame at the foot. The hall model's flights, middle landing and stair walls are
        // switched off (the stair balustrades are cut out of its railing mesh, OpenLanding); the kit's black drain picture
        // stops drawing. Every collider and the rolling pins' lanes are the blockout's as before.
        const string StairModel = "PawnRushGrandStairHall/RoyalStair";
        static readonly string[] HallStairNodes =
        {
            "HallV15_B1_GrandStair__Lower_flight", "HallV15_B1_GrandStair__Upper_flight", "HallV15_B1_GrandStair__Middle_landing",
            "HallV15_Stair_side_",
        };
        static readonly Dictionary<string, Material> stairMaterials = new Dictionary<string, Material>();

        static void RoyalStair(Transform pivot, Transform hallModel, Transform content, Material clay)
        {
            var model = Resources.Load<GameObject>(StairModel);
            if (model == null)
            {
                Debug.LogError("[대계단 홀] 대계단 모델을 불러오지 못했습니다: Resources/" + StairModel + ".fbx");
                return;
            }
            int off = 0;
            foreach (var t in hallModel.GetComponentsInChildren<Transform>(true))
            {
                foreach (var prefix in HallStairNodes)
                {
                    if (!t.name.StartsWith(prefix)) continue;
                    t.gameObject.SetActive(false);
                    off++;
                    break;
                }
            }
            // The hall's marble floor is one mesh from the porch to the middle landing; its landing tiles (top 4.0) would
            // flicker against the stair's landing tread, so they are cut out of it (the foyer's stay).
            foreach (var f in hallModel.GetComponentsInChildren<MeshFilter>(true))
            {
                if (!f.name.StartsWith("HallV15_B1_GrandStair__Marble_floor")) continue;
                f.gameObject.AddComponent<MeshCutOut>().Configure(content, new Bounds(new Vector3(0f, 4f, 32.5f), new Vector3(16.4f, 2f, 7.2f)));
            }
            int drains = 0;
            foreach (var r in content.GetComponentsInChildren<Renderer>(true))
            {
                if (r.name != "Drain grate" && r.name != "Drain bar") continue;
                r.enabled = false;
                drains++;
            }

            var view = Object.Instantiate(model, pivot, false);
            view.name = "Royal stair v45 model";
            int lamps = 0;
            foreach (var r in view.GetComponentsInChildren<Renderer>(true))
            {
                if (r.name.StartsWith("RoyalStair_light"))
                {
                    // A marker in a lantern's globe: a warm light there, the marker itself not drawn.
                    r.enabled = false;
                    r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                    var light = new GameObject("Lantern light").AddComponent<Light>();
                    light.transform.SetParent(r.transform, false);
                    if (r.TryGetComponent<MeshFilter>(out var f) && f.sharedMesh != null) light.transform.localPosition = f.sharedMesh.bounds.center;
                    Lamp(light, 9f, 4.5f);
                    lamps++;
                    continue;
                }
                var slots = r.sharedMaterials;
                for (int i = 0; i < slots.Length; i++)
                {
                    string name = slots[i] != null ? slots[i].name : "";
                    slots[i] = StairMaterial(name) ?? PalaceMaterial(name) ?? clay;
                }
                r.sharedMaterials = slots;
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            }
            foreach (var c in view.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(c);
            Debug.Log("[대계단 홀] 대계단 새 모델(대리석 계단 · 카펫 · 금 난간 · 왕관 등불 " + lamps + "개), 홀 모델의 옛 계단 " + off +
                      "개 끔, 키트 배수구 그림 " + drains + "개 끔");
        }

        // R363 (승규 11-04, the round hall's spiral way: "B로해" - a chessboard): the hall model's pieces of the way
        // (HallV15_B3_Spiral__*: the curving stair, the dive landings, the windy way, the north landing, the last stair)
        // in PR_SpiralChecker.shader, their tops a chessboard, their sides the walls' stone. R372 ("그럼 한번 미니맵에
        // 있는 걸로 해서 해봐 칸 사이즈는 줄이고"): the chessboard is the mini-games' floor (the kit's PRC_Floor_Checker,
        // light and near-black squares on the world's axes) in 1 m squares, in place of R363 ~ R370's ivory and black
        // marble cells laid round the hub, which kept dotted rings. The pieces under the marble (the spiral's foot, onto
        // the dais) keep the floors' marble (SeamlessFloors). The floating stones, jump pad, vents and hammers keep their
        // own models.
        const string SpiralCheckerMaterial = "PR_SpiralChecker";   // Assets/Art/PawnRushMap/Resources
        const string SpiralNode = "HallV15_B3_Spiral__";

        static void SpiralPaving(Transform hallModel, Transform content)
        {
            var shared = Resources.Load<Material>(SpiralCheckerMaterial);
            if (shared == null)
            {
                Debug.LogError("[대계단 홀] 나선 체스판 재질(Resources/" + SpiralCheckerMaterial + ".mat)을 불러오지 못했습니다");
                return;
            }
            // R403: a copy that knows where the way leaves the marble foot - its first metre in the floors' marble, then a
            // gold threshold strip square across it, then the chessboard (the shared asset is left as it is).
            var paving = new Material(shared) { name = SpiralCheckerMaterial + " (foot threshold)" };
            var frame = FindUnder(content, "Spiral foot");
            frame = frame != null ? frame.parent : content;
            float phi = GrandStairHallBuilder.SpiralThreshold;
            var at = frame.TransformPoint(GrandStairHallBuilder.HallPolar(phi, GrandStairHallBuilder.SpiralRadius(phi), 8f));
            var along = frame.TransformDirection(GrandStairHallBuilder.SpiralHeading(phi)).normalized;
            // R406: anchored to the hall model - the zone is moved to its place after it is built.
            HallAnchors(hallModel).Add(paving, "_FootPoint", at, false, 7f);
            HallAnchors(hallModel).Add(paving, "_FootAlong", along, true, .12f);
            CopyMarble(HallMarble(), paving);
            int paved = 0;
            foreach (var r in hallModel.GetComponentsInChildren<Renderer>(true))
            {
                if (!r.name.StartsWith(SpiralNode) || r.name.Contains("under marble") || r.name.Contains("under_marble")) continue;
                var slots = r.sharedMaterials;
                for (int i = 0; i < slots.Length; i++) slots[i] = paving;
                r.sharedMaterials = slots;
                paved++;
            }
            Debug.Log("[대계단 홀] 나선 길 " + paved + "조각을 미니게임 바닥 체스판(" + paving.GetFloat("_Cell") + " m 칸)으로, 둥근 테는 금 · 옆은 크림 돌, " +
                      "시작 1 m는 바닥 대리석 + 금 문턱");
        }

        // The floors' marble values into the way's threshold marble (PR_VeinedMarble's names -> PR_SpiralChecker's _M*).
        static readonly string[] MarbleColours = { "_BaseColor", "_MBase", "_ShadeColor", "_MShade", "_VeinColor", "_MVein", "_GoldColor", "_MGold", "_FineColor", "_MFine" };
        static readonly string[] MarbleFloats =
        {
            "_CloudScale", "_MCloudScale", "_VeinScale", "_MVeinScale", "_FineScale", "_MFineScale", "_Warp", "_MWarp",
            "_VeinWidth", "_MVeinWidth", "_FineWidth", "_MFineWidth", "_VeinStrength", "_MVeinStrength",
            "_GoldStrength", "_MGoldStrength", "_FineStrength", "_MFineStrength", "_Glossiness", "_MGlossiness",
        };

        static void CopyMarble(Material marble, Material to)
        {
            if (marble == null || !marble.HasProperty("_FineScale")) return;   // the plain fallback: keep the shader's own
            for (int i = 0; i < MarbleColours.Length; i += 2) to.SetColor(MarbleColours[i + 1], marble.GetColor(MarbleColours[i]));
            for (int i = 0; i < MarbleFloats.Length; i += 2) to.SetFloat(MarbleFloats[i + 1], marble.GetFloat(MarbleFloats[i]));
        }

        // R403 (승규 11-04 "대리석 바닥에서 나선형 코스로 넘어 갈 때 합쳐지는 구간을 자연스럽게 처리해라"): the spiral's foot
        // now ends on the curving stair's inner corner (GrandStairHallBuilder.SpiralFootNorth), so the hall model's foot
        // box and its edge moulding - both still the old 8 m deep foot - are off. The blockout foot (its collider) is drawn
        // in the floors' marble instead, and its two open edges (west, and north up to the stair's side) get the floors'
        // moulding as the model's: a band 0.12 m under the walked edge, limestone with a gold top fillet (PR_FloorMoulding).
        static readonly Vector2[] MouldingProfile =
            { new Vector2(0f, 0f), new Vector2(.06f, .02f), new Vector2(.1f, .1f), new Vector2(.14f, .2f), new Vector2(.14f, .32f), new Vector2(0f, .36f) };

        static void SpiralFootJoin(Transform hallModel, Transform content, Material marble)
        {
            int off = 0;
            foreach (var t in hallModel.GetComponentsInChildren<Transform>(true))
            {
                string name = t.name.Replace('_', ' ');
                if (!name.Contains("Spiral foot (under marble)") && name != "HallV15 Spiral foot moulding") continue;
                t.gameObject.SetActive(false);
                off++;
            }
            var foot = FindUnder(content, "Spiral foot");
            if (foot == null || !foot.TryGetComponent<Renderer>(out var view))
            {
                Debug.LogError("[대계단 홀] 나선 발치 블록아웃(Spiral foot)을 찾지 못했습니다");
                return;
            }
            view.enabled = true;
            view.sharedMaterial = marble;
            view.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;

            var trim = Resources.Load<Material>(FloorMouldingTrim);
            if (trim == null)
            {
                Debug.LogError("[대계단 홀] 바닥 테 몰딩 재질(Resources/" + FloorMouldingTrim + ".mat)을 불러오지 못했습니다");
                return;
            }
            float top = 8f, north = GrandStairHallBuilder.SpiralFootNorth;
            // West from inside the landing (z < 66) to past the corner; north from past the corner into the stair's side.
            Moulding(foot.parent, "Spiral foot moulding W", new Vector3(4f, 0f, 65.8f), new Vector3(4f, 0f, north + .14f), Vector3.left, top, trim);
            Moulding(foot.parent, "Spiral foot moulding N", new Vector3(4f - .14f, 0f, north), new Vector3(12.6f, 0f, north), Vector3.forward, top, trim);
            Debug.Log("[대계단 홀] 나선 발치: 모델의 옛 발치 · 몰딩 " + off + "개 끔, 블록아웃 발치를 대리석으로, 새 몰딩 2줄");
        }

        // A straight run of the floors' moulding from `a` to `b` (level, in `parent`'s frame) on the side facing `outwards`,
        // its top 0.12 m under the walked edge at `top`, closed at both ends; flat-shaded, no UVs (PR_PalaceTrim needs none).
        static void Moulding(Transform parent, string name, Vector3 a, Vector3 b, Vector3 outwards, float top, Material material)
        {
            var along = (b - a).normalized;
            float baseY = top - .12f - .36f;
            Vector3 At(Vector3 end, Vector2 q) => end + outwards * q.x + Vector3.up * (baseY + q.y);
            var vertices = new List<Vector3>();
            var normals = new List<Vector3>();
            var triangles = new List<int>();
            void Quad(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, Vector3 n)
            {
                int k = vertices.Count;
                vertices.Add(p0); vertices.Add(p1); vertices.Add(p2); vertices.Add(p3);
                for (int j = 0; j < 4; j++) normals.Add(n);
                bool flip = Vector3.Dot(Vector3.Cross(p1 - p0, p2 - p0), n) < 0f;
                if (flip) triangles.AddRange(new[] { k, k + 2, k + 1, k, k + 3, k + 2 });
                else triangles.AddRange(new[] { k, k + 1, k + 2, k, k + 2, k + 3 });
            }
            var profile = MouldingProfile;
            for (int i = 0; i + 1 < profile.Length; i++)
            {
                var e = profile[i + 1] - profile[i];
                var n = (outwards * e.y - Vector3.up * e.x).normalized;   // the profile edge turned a quarter outwards
                Quad(At(a, profile[i]), At(a, profile[i + 1]), At(b, profile[i + 1]), At(b, profile[i]), n);
            }
            // The two ends: the profile (convex, closed along the wall) as a fan.
            foreach (var (end, n) in new[] { (a, -along), (b, along) })
            {
                int k = vertices.Count;
                foreach (var q in profile) { vertices.Add(At(end, q)); normals.Add(n); }
                for (int j = 1; j + 1 < profile.Length; j++)
                {
                    bool flip = Vector3.Dot(Vector3.Cross(vertices[k + j] - vertices[k], vertices[k + j + 1] - vertices[k]), n) < 0f;
                    triangles.AddRange(flip ? new[] { k, k + j + 1, k + j } : new[] { k, k + j, k + j + 1 });
                }
            }
            var mesh = new Mesh { name = name };
            mesh.SetVertices(vertices);
            mesh.SetNormals(normals);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateBounds();
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = material;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
        }

        // R375 (승규 11-04, a shot of the dais drum's grey pilasters: "원기둥 쪽에서 회색 부분 아직 텍스쳐가 안 입혀졌고 나선
        // 모양 길도 테두리 부분 텍스쳐 입혀줘"): the hall model's last clay trims in PR_PalaceTrim.shader - the dais tower's
        // sixteen pilasters, base and cornice (cream limestone, gold feet and capitals, a gold base fillet and cornice
        // corona; its marble panels are the drum's, SeamlessFloors) and the mouldings round the marble floors' edges (the
        // spiral's foot, the landings, the balcony: cream limestone with a gold top fillet). The spiral way's own border is
        // its shader's (PR_SpiralChecker: a gold rounded edge, limestone sides).
        const string DaisTowerTrim = "PR_DaisTowerTrim", FloorMouldingTrim = "PR_FloorMoulding";   // Assets/Art/PawnRushMap/Resources

        static void PalaceTrims(Transform hallModel)
        {
            var tower = Resources.Load<Material>(DaisTowerTrim);
            var moulding = Resources.Load<Material>(FloorMouldingTrim);
            if (tower == null || moulding == null)
            {
                Debug.LogError("[대계단 홀] 장식 재질(Resources/" + DaisTowerTrim + ".mat, " + FloorMouldingTrim + ".mat)을 불러오지 못했습니다");
                return;
            }
            int towers = 0, mouldings = 0;
            foreach (var r in hallModel.GetComponentsInChildren<Renderer>(true))
            {
                string name = r.name.Replace('_', ' ');
                Material m;
                if (name.StartsWith("HallV15 Royal dais tower")) { m = tower; towers++; }
                else if (name.StartsWith("HallV15 ") && name.EndsWith(" moulding")) { m = moulding; mouldings++; }
                else continue;
                var slots = r.sharedMaterials;
                for (int i = 0; i < slots.Length; i++) slots[i] = m;
                r.sharedMaterials = slots;
            }
            Debug.Log("[대계단 홀] 회색 장식 칠함: 단상 탑 " + towers + "개(크림 돌 + 금 띠), 바닥 테 몰딩 " + mouldings + "개(크림 돌 + 금 윗선)");

            // R410 (승규 11-05 "원기둥 쪽 기둥도 지워"): the dais tower's sixteen pilasters out of its mesh - each its own
            // piece (Blender z -2.8 .. 14.4 at r 8), their middles 9.8 m over the tower's foot; the base (middle 0.6 m
            // up) and the cornice (19.2 m up) are pieces round the axis outside that height, so they stay. Cut in Play
            // (MeshCutOut, as R340's balustrades), in the hall model's own frame (it only turns about y).
            foreach (var r in hallModel.GetComponentsInChildren<Renderer>(true))
            {
                if (!r.name.Replace('_', ' ').StartsWith("HallV15 Royal dais tower") || !r.TryGetComponent<MeshFilter>(out var f)) continue;
                var b = r.bounds;
                var mid = hallModel.InverseTransformPoint(new Vector3(b.center.x, b.min.y + 9.8f, b.center.z));
                if (!f.TryGetComponent<MeshCutOut>(out _))
                    f.gameObject.AddComponent<MeshCutOut>().Configure(hallModel, new Bounds(mid, new Vector3(20f, 6f, 20f)));
            }
        }

        // R353: the rolling carpet's model (RollingSpawner) and its materials: the body in the hall carpet, the spiral
        // ends in their own texture, the hubs in the stair's gold.
        internal const string RollingCarpetModel = "PawnRushGrandStairHall/RollingCarpet";
        // R354: the big roll, a faceted white marble pawn with gold rings (pawn_roll_v47.py); its marble the stair's
        // texture through its own UVs, so the veins turn with it.
        internal const string RollingStatueModel = "PawnRushGrandStairHall/RollingStatue";
        static Material rollCarpet, rollEnd, rollMarble;

        internal static Material RollMaterial(string name)
        {
            if (name.StartsWith("Roll_Marble"))
            {
                if (rollMarble == null)
                {
                    rollMarble = FanTextured("RoyalStair_Marble", .78f, 0f);
                    rollMarble.name = "Rolling statue marble";
                }
                return rollMarble;
            }
            if (name.StartsWith("Roll_Carpet"))
            {
                if (rollCarpet == null)
                    rollCarpet = new Material(PieceFigure.Lit(Color.white, .12f)) { name = "Rolling carpet", mainTexture = Resources.Load<Texture2D>(CarpetTexture) };
                return rollCarpet;
            }
            if (name.StartsWith("Roll_End"))
            {
                if (rollEnd == null)
                    rollEnd = new Material(PieceFigure.Lit(Color.white, .1f)) { name = "Rolling carpet end", mainTexture = Resources.Load<Texture2D>("PawnRushGrandStairHall/RollingCarpet_End") };
                return rollEnd;
            }
            return StairMaterial(name);
        }

        // The stair's own materials by their Blender names (the import may add "(Instance)" or a model prefix);
        // Palace_Stone is the walls' (PalaceMaterial), the gold the royal fan's textured gold.
        static Material StairMaterial(string name)
        {
            int cut = name.IndexOf(' ');
            if (cut >= 0) name = name.Substring(0, cut);
            int at = name.LastIndexOf("Stair_", System.StringComparison.Ordinal);
            if (at < 0) return null;
            string part = name.Substring(at + "Stair_".Length);
            if (stairMaterials.TryGetValue(part, out var cached) && cached != null) return cached;
            Material m;
            switch (part)
            {
                case "Marble": m = HallMarble(); break;   // R358: Calacatta Oro, as the hall's floors (was RoyalStair_Marble)
                case "Gold": return FanMaterial("RoyalFan_Gold");
                case "Carpet":
                    m = new Material(PieceFigure.Lit(Color.white, .12f)) { mainTexture = Resources.Load<Texture2D>(CarpetTexture) };
                    if (m.mainTexture == null) Debug.LogError("[대계단 홀] 계단 카펫 텍스처(Resources/" + CarpetTexture + ".png)를 불러오지 못했습니다");
                    break;   // the model's UVs are already in 2 m tiles, as the hall floor's carpet
                case "Velvet": m = new Material(PieceFigure.Lit(new Color(.42f, .03f, .06f), .32f)); break;
                case "Glass":
                    m = LitMaterials.NewLit(new Color(1f, .78f, .48f), .85f);
                    LitMaterials.SetEmission(m, CandleLight * 6f);   // HDR: over the bloom's threshold, as the hub bulbs
                    break;
                case "Iron": m = new Material(PieceFigure.Lit(new Color(.07f, .07f, .075f), .45f, .8f)); break;
                default: return null;
            }
            m.name = "Royal stair " + part;
            return stairMaterials[part] = m;
        }
    }
}
