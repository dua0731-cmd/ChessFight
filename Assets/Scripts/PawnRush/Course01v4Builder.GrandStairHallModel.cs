using System.Collections.Generic;
using ChessFight.Game;
using UnityEngine;

namespace ChessFight.PawnRush
{
    public static partial class Course01v4Builder
    {
        // R308 (승규 11-03): the Blender hall v15 (ArtSource/PawnRush/GrandStairHall, export_unity_v15.py) over the blockout
        // hall the code builds: smooth spiral bands, arches, hanging chandeliers, stone stairs, marble floors, the foyer's
        // four statues and the library walls, all untextured clay. The blockout pieces they stand in for draw nothing but
        // keep their colliders, so walking, falling and the hits are the blockout's as before.
        const string HallModel = "PawnRushGrandStairHall/GrandStairHallV15";
        const string CrystalGlass = "PR_GlassCrystal";   // Assets/Art/PawnRushMap/Resources (R332)

        static void GrandStairHallModel(CourseBuilder b, Transform hall)
        {
            var model = Resources.Load<GameObject>(HallModel);
            var hiddenText = Resources.Load<TextAsset>(HallModel + "_Hidden");
            if (model == null || hiddenText == null)
            {
                Debug.LogError("[대계단 홀] 모델을 불러오지 못했습니다: Resources/" + HallModel + ".fbx, " + HallModel + "_Hidden.txt");
                return;
            }
            // Blender names are the Unity paths under Content with '_' for ' ' and '#2', '#3' for repeated names.
            var content = hall.Find(GrandStairHall.ContentName);
            int off = HideListed(content, hiddenText);

            var pivot = new GameObject("Grand stair hall v15").transform;
            pivot.SetParent(hall, false);
            pivot.localRotation = Quaternion.Euler(0, 180, 0);
            var view = Object.Instantiate(model, pivot, false);
            view.name = "Grand stair hall v15 model";
            var clay = HallClay;
            // R358: the marble floors (and the balcony's marble) in Calacatta Oro (HallMarble, world space: no UVs).
            var marble = HallMarble();
            var under = b.kit.floorChecker;
            // R325: the round hall's oculus in the crown ring. R332: in the crystal clear glass (PR_Glass.shader: it bends,
            // frosts and mirrors what is behind it), the kit's flat glass if that material is missing.
            var glass = Resources.Load<Material>(CrystalGlass);
            if (glass == null) glass = b.kit.glass != null ? b.kit.glass : clay;
            foreach (var r in view.GetComponentsInChildren<Renderer>(true))
            {
                var slots = r.sharedMaterials;
                // R384 (승규 11-04 "원형 대형 벽은 금색 테두리 안할거야 지워"): the round hall's big wall in plain stone, its
                // window bars (the "... tracery" nodes) still gold.
                bool roundWall = r.name.StartsWith(RoundWall) && !r.name.Contains("tracery");
                // R385 (승규 11-04 "창살은 모든 대형 벽이 무광 은색으로 해"): every big wall's window bars ("<wall> tracery")
                // in matte silver instead of gold.
                bool bars = r.name.Contains(" tracery");
                for (int i = 0; i < slots.Length; i++)
                {
                    string name = slots[i] != null ? slots[i].name : "";
                    // The marble floors' joints show the lowered blockout floor under them.
                    slots[i] = ChandelierMaterial(name) ?? PalaceMaterial(name) ?? (name.StartsWith("Marble") ? marble
                             : name.StartsWith("Section_Floor_under") ? under : name.StartsWith("Glass") ? glass : clay);
                    if (roundWall && slots[i] == palaceWall) slots[i] = PlainPalaceWall();
                    else if (roundWall && slots[i] == palaceGold) slots[i] = palaceStone;
                    else if (bars && slots[i] == palaceGold) slots[i] = MatteSilver();
                }
                r.sharedMaterials = slots;
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            }
            Debug.Log("[대계단 홀] v15 모델 " + view.transform.childCount + "개 조각, 블록아웃 " + off + "개 숨김(충돌은 그대로)");
            SeamlessFloors(view.transform, marble);
            OpenLanding(view.transform, content);
            ClearLandingChandeliers(view.transform, content);
            HubChandelierLights(view.transform);
            WidenHallFloor(content);
            CarpetHallFloor(content);
            HangPads(content, clay);
            RoyalFans(content);
            WindyWay(content);
            GuardKnightWarnings(content);
            RoyalBalcony(pivot, content, marble, clay);
            StairStatues(pivot, view.transform, clay);
            LandingQueen(view.transform, content, clay);
            RoyalStair(pivot, view.transform, content, clay);
            SpiralPaving(view.transform, content);
            PalaceTrims(view.transform);
            SpiralFootJoin(view.transform, content, marble);
            DomeVault(view.transform);
            Entrance(pivot, view.transform, clay);
        }

        // R343 (승규 11-04, an asset preview of a stone fan housing with gold crowned corner caps and a gold ring round a
        // six-bladed fan: "1번째 사진 같이 모델링 해주고 회전 애니메이션 넣은 다음 택스쳐 입히고 적용해" + "블렌더에서 아주
        // 세심하게"): the windy way's two vents in the royal fan (royal_fan_v35.py: RoyalFan_Frame still, RoyalFan_Rotor
        // turned by RoyalFanSpin with the vent). It stands where the kit's VentModel was - the same 2.8 x 2.6 x 0.5 m box,
        // its front the vent's front - and the VentModel's housing, aperture and grille draw nothing but keep any collider.
        const string RoyalFanModel = "PawnRushGrandStairHall/RoyalFan";
        static readonly System.Collections.Generic.Dictionary<string, Material> fanMaterials =
            new System.Collections.Generic.Dictionary<string, Material>();

        static void RoyalFans(Transform content)
        {
            var model = Resources.Load<GameObject>(RoyalFanModel);
            if (model == null)
            {
                Debug.LogError("[대계단 홀] 왕실 환풍기 모델을 불러오지 못했습니다: Resources/" + RoyalFanModel + ".fbx");
                return;
            }
            int fans = 0;
            foreach (var t in content.GetComponentsInChildren<Transform>(true))
            {
                if (!t.name.StartsWith("15 Wind vent ")) continue;
                var old = FindUnder(t, "VentModel");
                if (old == null) continue;
                foreach (var r in old.GetComponentsInChildren<Renderer>(true)) r.enabled = false;
                var view = Object.Instantiate(model, old.parent, false);
                view.name = "Royal fan";
                view.transform.localPosition = old.localPosition;
                view.transform.localRotation = old.localRotation;
                var s = old.parent.lossyScale;
                view.transform.localScale = new Vector3(1f / s.x, 1f / s.y, 1f / s.z);   // metres, whatever the vent's scale
                foreach (var r in view.GetComponentsInChildren<Renderer>(true))
                {
                    var slots = r.sharedMaterials;
                    for (int i = 0; i < slots.Length; i++) slots[i] = FanMaterial(slots[i] != null ? slots[i].name : "");
                    r.sharedMaterials = slots;
                    r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
                }
                foreach (var c in view.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(c);
                // R403 (승규 11-04 "예고 시 나오는 환풍기 위에 막대 지워"): the kit's orange warning bar over the fan draws
                // nothing (its object stays, AirVent still switches it); the fan's speed-up and VentWindFx's swirl warn.
                var bar = FindUnder(t, "WarningVisual");
                if (bar != null)
                    foreach (var r in bar.GetComponentsInChildren<Renderer>(true)) r.enabled = false;
                var rotor = FindUnder(view.transform, "RoyalFan_Rotor");
                if (rotor != null)
                    view.AddComponent<RoyalFanSpin>().Configure(rotor, t.GetComponentInChildren<ChessFight.ProtectKing.AirVent>(true));
                else Debug.LogError("[대계단 홀] 왕실 환풍기 모델에 날개(RoyalFan_Rotor)가 없어 돌릴 수 없습니다");
                fans++;
            }
            Debug.Log("[대계단 홀] 바람 통풍구 " + fans + "개를 왕실 환풍기로");
        }

        // The fan's five materials by their Blender names: stone, gold and the blades textured (albedo + normal map, the
        // boxed UVs tile them), the steel and the dark back plain.
        static Material FanMaterial(string name)
        {
            // The import may call a material "RoyalFan_Gold (Instance)", "RoyalFan-RoyalFan_Steel" (model-material
            // naming) or after its texture: the part after "RoyalFan_" decides.
            int cut = name.IndexOf(' ');
            if (cut >= 0) name = name.Substring(0, cut);
            int at = name.LastIndexOf("RoyalFan_", System.StringComparison.Ordinal);
            string part = at >= 0 ? name.Substring(at + "RoyalFan_".Length) : name;
            if (part.EndsWith("_N")) part = part.Substring(0, part.Length - 2);
            if (fanMaterials.TryGetValue(part, out var cached) && cached != null) return cached;
            Material m;
            switch (part)
            {
                // v3 (승규 11-04 "수정해": the stone read as black slate in the hall's shade): stone, not metal.
                case "Frame": m = FanTextured("RoyalFan_Frame", .3f, 0f); break;
                case "Gold": m = FanTextured("RoyalFan_Gold", .74f, .92f); break;
                case "Blade": m = FanTextured("RoyalFan_Blade", .52f, .78f); break;
                case "Steel": m = new Material(PieceFigure.Lit(new Color(.24f, .24f, .26f), .48f, .82f)); break;
                default: m = new Material(PieceFigure.Lit(new Color(.03f, .03f, .035f), .25f, .3f)); break;   // Dark
            }
            m.name = "Royal fan " + part;
            return fanMaterials[part] = m;
        }

        static Material FanTextured(string name, float smoothness, float metallic)
        {
            var albedo = Resources.Load<Texture2D>("PawnRushGrandStairHall/" + name);
            var normal = Resources.Load<Texture2D>("PawnRushGrandStairHall/" + name + "_N");
            if (albedo == null) Debug.LogError("[대계단 홀] 왕실 환풍기 텍스처를 불러오지 못했습니다: Resources/PawnRushGrandStairHall/" + name + ".png");
            var m = new Material(PieceFigure.Lit(Color.white, smoothness, metallic)) { mainTexture = albedo };
            if (normal != null)
            {
                m.SetTexture("_BumpMap", normal);
                m.SetFloat("_BumpScale", 1f);
                m.EnableKeyword("_NORMALMAP");
            }
            return m;
        }

        // R340 (승규 11-04 "계단 뒤에 올라간 후 그 공간에 난간 지워주고"): the central landing (y 8, x -14..14, z 51..66)
        // without its balustrades. The blockout's rails are gone (GrandStairHallBuilder.CentralLanding); the model's are
        // in one mesh with every other balustrade of the hall, so MeshCutOut takes out the pieces whose middle is on the
        // landing's five rails (section frame). The upper flight's rails (x +-8.15, up to z 52) stay. R348 (승규 11-04, a
        // shot of the L-shaped railing at the landing's north-east corner: "샹들리에 쪽 아직 난간이 제거가 안 됐어"): the
        // spiral foot's two rails go too (W x 4.15, z 66 .. 71.5; N z 71.35, x 4.3 .. 10.5), the model's and the blockout's.
        static void OpenLanding(Transform view, Transform content)
        {
            Transform rails = null;
            foreach (var t in view.GetComponentsInChildren<MeshFilter>(true))
                if (t.name == "HallV15_Balustrades") { rails = t.transform; break; }
            if (rails == null)
            {
                Debug.LogError("[대계단 홀] 모델에서 난간(HallV15_Balustrades)을 찾지 못해 착지 공간 난간을 지우지 못했습니다");
                return;
            }
            static Bounds Box(float x0, float x1, float z0, float z1) =>
                new Bounds(new Vector3((x0 + x1) * .5f, 9f, (z0 + z1) * .5f), new Vector3(x1 - x0, 3f, z1 - z0));
            rails.gameObject.AddComponent<MeshCutOut>().Configure(content,
                Box(-14.6f, 4f, 65.3f, 66.05f),       // N, as far as the spiral foot's rail
                Box(-14.6f, -13.3f, 50.6f, 66.3f),    // W
                Box(13.3f, 14.6f, 50.6f, 64f),        // E, as far as the spiral foot
                Box(-14.6f, -8.36f, 50.6f, 51.7f),    // S, west of the stair
                Box(8.36f, 14.6f, 50.6f, 51.7f),      // S, east of the stair
                Box(3.7f, 4.6f, 65.6f, 72f),          // the spiral foot's W rail (R348)
                Box(3.9f, 11f, 70.7f, 72f),           // the spiral foot's N rail (R348)
                // R350: the grand stair's own balustrades (the flights' and the middle landing's, with the stone panels
                // under them) - RoyalStair draws new ones. x 7.7 .. 8.4: the foyer rails start at x 8.46.
                new Bounds(new Vector3(-8.05f, 2.25f, 32.85f), new Vector3(.7f, 16.5f, 39.1f)),
                new Bounds(new Vector3(8.05f, 2.25f, 32.85f), new Vector3(.7f, 16.5f, 39.1f)));
        }

        // R340 (승규 11-04 "장애물 샹들리에가 눈에 보이면 안돼"): the model's still chandeliers that hang over the central
        // landing (HallV15_Chandelier_N, not the hub's _6) looked like the falling ones and cast shadows like their
        // warnings; they go. Over the landing = their middle within 4 m of it (x -18..18, z 47..70, section frame).
        static void ClearLandingChandeliers(Transform view, Transform content)
        {
            var gone = new List<string>();
            foreach (var t in view.GetComponentsInChildren<Transform>(true))
            {
                if (!t.name.StartsWith("HallV15_Chandelier_") || t.name == HubChandelierNode) continue;
                // Its middle from its meshes' corners (the hall is still inactive, so no Renderer.bounds).
                bool any = false;
                var b = new Bounds();
                foreach (var f in t.GetComponentsInChildren<MeshFilter>(true))
                {
                    if (f.sharedMesh == null) continue;
                    var m = f.sharedMesh.bounds;
                    for (int k = 0; k < 8; k++)
                    {
                        var corner = m.center + Vector3.Scale(m.extents, new Vector3((k & 1) == 0 ? -1 : 1, (k & 2) == 0 ? -1 : 1, (k & 4) == 0 ? -1 : 1));
                        var p = content.InverseTransformPoint(f.transform.TransformPoint(corner));
                        if (any) b.Encapsulate(p);
                        else { b = new Bounds(p, Vector3.zero); any = true; }
                    }
                }
                if (!any) continue;
                var c = b.center;
                if (c.x < -18f || c.x > 18f || c.z < 47f || c.z > 70f) continue;
                t.gameObject.SetActive(false);
                gone.Add(t.name);
            }
            Debug.Log("[대계단 홀] 착지 공간 위 매달린 샹들리에 " + gone.Count + "개 숨김: " + string.Join(", ", gone));
        }

        // R329 (승규 11-03, a gilded palace hall for reference: "중앙 샹들리에만 … 텍스쳐 입혀 불빛 나오는 곳에는 실제로 빛이
        // 나오게"): the round hall's hub chandelier (chandelier_hub_v26.py) in gold, crystal, wax and glowing bulbs; a warm
        // point light in each of its 20 bulbs, whose markers ("HallV15_Hub_chandelier_light_NN") draw nothing, and one
        // wide light in its middle that lights the hall round it. No shadows: 21 shadowed point lights would cost too much.
        static Material chandelierGold, chandelierCrystal, chandelierWax, chandelierBulb;
        const string BulbMarker = "HallV15_Hub_chandelier_light";
        static readonly Color CandleLight = new Color(1f, .74f, .45f);

        static Material ChandelierMaterial(string name)
        {
            if (!name.StartsWith("CH_")) return null;
            if (chandelierGold == null)
            {
                chandelierGold = PieceFigure.Lit(new Color(.86f, .64f, .28f), .72f, .9f);
                // R330 ("크리스탈을 좀 더 반투명 말고 빛나고 그런 느낌으로"): opaque, mirror-smooth and lit from within, a
                // cool white over the bloom's threshold, so the drops sparkle rather than show the sky through them.
                chandelierCrystal = LitMaterials.NewLit(new Color(.92f, .96f, 1f), .97f, .35f);
                chandelierCrystal.name = "Hub chandelier crystal";
                LitMaterials.SetEmission(chandelierCrystal, new Color(.78f, .88f, 1f) * 1.6f);
                chandelierWax = PieceFigure.Lit(new Color(.96f, .93f, .86f), .3f);
                chandelierBulb = LitMaterials.NewLit(new Color(1f, .9f, .64f), .6f);
                chandelierBulb.name = "Hub chandelier bulb";
                LitMaterials.SetEmission(chandelierBulb, CandleLight * 12f);   // HDR, well over the bloom's threshold
            }
            return name.StartsWith("CH_Crystal") ? chandelierCrystal : name.StartsWith("CH_Candle") ? chandelierWax
                 : name.StartsWith("CH_Bulb") ? chandelierBulb : chandelierGold;
        }

        // R335 (승규 11-03, a reference palace wall: "이런 텍스쳐로 벽 텍스쳐 입혀"): the walls' two materials and the banners
        // (walls_v29.py). Palace_Stone: cream limestone drawn in world space (PR_PalaceStone.shader, the walls have no
        // UVs); Palace_Gold: the trim (caps, bases, level faces, the pilasters' sides, window reveals and bars) and the
        // banner rods; Palace_Banner: PalaceBanner.png, double-sided, its swallowtail cut out of the texture too.
        // R337 ("검은색이 원래 돌 텍스쳐 흰색이 골드색이야"): Palace_Wall, the architectural walls (walls_v33.py), in
        // PR_PalaceWall.shader: the same stone with the gold read per pixel from the moulding map through their UVs.
        static Material palaceStone, palaceGold, palaceBanner, palaceWall;

        static Material PalaceMaterial(string name)
        {
            if (!name.StartsWith("Palace_")) return null;
            if (palaceGold == null)
            {
                palaceStone = Resources.Load<Material>("PR_PalaceStone");
                if (palaceStone == null)
                {
                    Debug.LogError("[대계단 홀] 벽 돌 재질(Resources/PR_PalaceStone.mat)을 불러오지 못했습니다");
                    palaceStone = HallClay;
                }
                palaceGold = PieceFigure.Lit(new Color(.86f, .64f, .28f), .7f, .85f);
                var flag = Resources.Load<Texture2D>("PawnRushGrandStairHall/PalaceBanner");
                palaceBanner = new Material(PieceFigure.Lit(Color.white, .25f)) { name = "Palace banner", mainTexture = flag };
                LitMaterials.MakeCutout(palaceBanner, .5f);
                palaceBanner.SetFloat("_Cull", 0f);                  // both sides of the cloth
                palaceBanner.doubleSidedGI = true;
                if (flag == null) Debug.LogError("[대계단 홀] 깃발 텍스처(Resources/PawnRushGrandStairHall/PalaceBanner.png)를 불러오지 못했습니다");
                palaceWall = Resources.Load<Material>("PR_PalaceWall");
                if (palaceWall == null)
                {
                    Debug.LogError("[대계단 홀] 벽 재질(Resources/PR_PalaceWall.mat)을 불러오지 못했습니다");
                    palaceWall = palaceStone;
                }
            }
            return name.StartsWith("Palace_Wall") ? palaceWall : name.StartsWith("Palace_Stone") ? palaceStone
                 : name.StartsWith("Palace_Banner") ? palaceBanner : palaceGold;
        }

        // R384: the round wall's architectural wall without its gold. PR_PalaceWall gilds where the trim map passes
        // _Threshold (smoothstep over +-_Edge); a cut above the map's 0..1 leaves only the limestone.
        const string RoundWall = "HallV15_Round_wall";
        static Material plainPalaceWall;

        static Material PlainPalaceWall()
        {
            if (plainPalaceWall == null)
            {
                plainPalaceWall = new Material(palaceWall) { name = "Palace wall (no gold)" };
                plainPalaceWall.SetFloat("_Threshold", 2f);
                plainPalaceWall.SetFloat("_Edge", .01f);
            }
            return plainPalaceWall;
        }

        // R385: the window bars' matte silver - a pale grey metal, rough enough not to mirror the hall.
        static Material matteSilver;
        static Material MatteSilver()
        {
            if (matteSilver == null) matteSilver = new Material(PieceFigure.Lit(new Color(.74f, .75f, .77f), .22f, .7f)) { name = "Window bars (matte silver)" };
            return matteSilver;
        }

        // R330: the chandelier (chandelier_hub_v27.py, three times v10's size) and its bulbs' lights hang from one pivot at
        // the chain's top, which HallChandelierSway swings a couple of degrees (about 0.45 m at the bottom).
        const string HubChandelierNode = "HallV15_Chandelier_6";

        static void HubChandelierLights(Transform model)
        {
            var body = FindUnder(model, HubChandelierNode);
            Transform sway = null;
            if (body != null && body.TryGetComponent<Renderer>(out var drawn))
            {
                var b = drawn.bounds;
                sway = new GameObject("Hub chandelier sway").transform;
                sway.SetParent(model, false);
                sway.position = new Vector3(b.center.x, b.max.y, b.center.z);   // the chain's top, at the oculus' boss
                body.SetParent(sway, true);
                sway.gameObject.AddComponent<HallChandelierSway>();
            }
            else Debug.LogError("[대계단 홀] 가운데 샹들리에(" + HubChandelierNode + ")가 모델에 없어 흔들 수 없습니다");

            var bulbs = new List<Vector3>();
            foreach (var r in model.GetComponentsInChildren<Renderer>(true))
            {
                if (!r.name.StartsWith(BulbMarker)) continue;
                r.enabled = false;
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                var light = new GameObject("Bulb light").AddComponent<Light>();
                light.transform.SetParent(r.transform, false);
                light.transform.position = r.bounds.center;
                Lamp(light, 8f, 4f);
                bulbs.Add(r.bounds.center);
                if (sway != null) r.transform.SetParent(sway, true);
            }
            if (bulbs.Count == 0)
            {
                Debug.LogError("[대계단 홀] 가운데 샹들리에 전구 표시(" + BulbMarker + "_NN)가 모델에 없습니다");
                return;
            }
            var middle = Vector3.zero;
            foreach (var p in bulbs) middle += p;
            var glow = new GameObject("Hub chandelier glow").AddComponent<Light>();
            glow.transform.SetParent(sway != null ? sway : model, false);
            glow.transform.position = middle / bulbs.Count;
            Lamp(glow, 40f, 10f);
            Debug.Log("[대계단 홀] 가운데 샹들리에 전구 빛 " + bulbs.Count + "개 + 가운데 빛 1개" + (sway != null ? ", 흔들림" : ""));
        }

        static void Lamp(Light light, float range, float intensity)
        {
            light.type = LightType.Point;
            light.color = CandleLight;
            light.range = range;
            light.intensity = intensity;
            light.shadows = LightShadows.None;
            light.renderMode = LightRenderMode.ForcePixel;
        }

        // R329 ("우리 맵에 빨간색으로 된 부분 카펫 재질 텍스쳐 입히고"): the red respawn floor in a crimson carpet
        // (carpet_texture_v26.py, one tile 2 x 2 m) with gold border lines as the reference's: a double line framing
        // each side of the grand stair (between its flank wall, x 8.3, and the outer wall, x 34; the foyer's north wall,
        // z 14, and the central landing, z 50.7), and two double rings in the round hall (inside the wall, r 31, and
        // round the dais, r 8). Pictures only: the floor's collider is the same.
        const string CarpetTexture = "PawnRushGrandStairHall/HallCarpet";
        const float CarpetTile = 2f;

        static void CarpetHallFloor(Transform content)
        {
            var floor = FindUnder(content, "Hall floor (respawn)");
            var texture = Resources.Load<Texture2D>(CarpetTexture);
            if (floor == null || texture == null || !floor.TryGetComponent<Renderer>(out var drawn))
            {
                Debug.LogError("[대계단 홀] 카펫을 깔지 못했습니다: Resources/" + CarpetTexture + ".png 또는 홀 바닥");
                return;
            }
            var carpet = new Material(PieceFigure.Lit(Color.white, .12f)) { name = "Hall carpet", mainTexture = texture };
            carpet.mainTextureScale = new Vector2(floor.localScale.x / CarpetTile, floor.localScale.z / CarpetTile);
            drawn.sharedMaterial = carpet;

            var gold = PieceFigure.Lit(new Color(.86f, .66f, .3f), .65f, .8f);
            float top = content.InverseTransformPoint(floor.TransformPoint(new Vector3(0f, .5f, 0f))).y + .006f;
            var lines = new GameObject("Carpet border").transform;
            lines.SetParent(content, false);
            for (int side = -1; side <= 1; side += 2)
            {
                Frame(lines, gold, side, 9.3f, 32.9f, 15f, 49.9f, .32f, top);
                Frame(lines, gold, side, 9.85f, 32.35f, 15.55f, 49.35f, .1f, top);
            }
            var hub = GrandStairHallBuilder.Hub;
            foreach (var (r, w) in new[] { (29.55f, .32f), (29f, .1f), (9.2f, .32f), (9.75f, .1f) })
                Ring(lines, gold, new Vector3(hub.x, top, hub.y), r, w);
        }

        // A rectangle of gold line on one side of the stair (side -1 west, 1 east), x0 .. x1 out from the middle.
        static void Frame(Transform parent, Material gold, int side, float x0, float x1, float z0, float z1, float w, float y)
        {
            float xm = side * (x0 + x1) * .5f, zm = (z0 + z1) * .5f;
            Strip(parent, gold, new Vector3(xm, y, z0), x1 - x0 + w, w, 0f);
            Strip(parent, gold, new Vector3(xm, y, z1), x1 - x0 + w, w, 0f);
            Strip(parent, gold, new Vector3(side * x0, y, zm), z1 - z0 + w, w, 90f);
            Strip(parent, gold, new Vector3(side * x1, y, zm), z1 - z0 + w, w, 90f);
        }

        static void Ring(Transform parent, Material gold, Vector3 centre, float radius, float w)
        {
            const int n = 120;
            float chord = 2f * Mathf.PI * radius / n * 1.02f;
            for (int i = 0; i < n; i++)
            {
                float a = (i + .5f) * 360f / n;
                var at = centre + new Vector3(Mathf.Cos(a * Mathf.Deg2Rad), 0f, Mathf.Sin(a * Mathf.Deg2Rad)) * radius;
                Strip(parent, gold, at, chord, w, 90f - a);
            }
        }

        // A thin flat gold strip `length` along its yaw, without a collider.
        static void Strip(Transform parent, Material gold, Vector3 at, float length, float width, float yaw)
        {
            var strip = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Object.DestroyImmediate(strip.GetComponent<Collider>());
            strip.name = "Gold line";
            strip.transform.SetParent(parent, false);
            strip.transform.localPosition = at;
            strip.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
            strip.transform.localScale = new Vector3(length, .01f, width);
            var r = strip.GetComponent<Renderer>();
            r.sharedMaterial = gold;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        // R311 (승규 11-03): the red hall floor runs on under the v17 walls' recesses (x +-38.1, was +-34), so no slot shows
        // at their foot. Only the walls stand out there, so the wider collider changes nothing walked.
        const float HallFloorHalf = 38.1f;

        static void WidenHallFloor(Transform content)
        {
            var floor = FindUnder(content, "Hall floor (respawn)");
            if (floor == null) return;
            var scale = floor.localScale;
            scale.x = HallFloorHalf * 2f;
            floor.localScale = scale;
        }

        // R311: 승규's pad assets (pads_v18.py, flat tops, origin at the top centre) on the pieces that carry them. They
        // move or launch, so they hang on those pieces rather than in the hall model, and every PC builds the same.
        //   floating stones  the solar panel cube, 2.6 m square on the 2.6 m round stone, an edge along the radius; a box
        //                    collider the size of the square, so its corners hold a foot as they look
        //   02 jump pad      the glowing platform in full 3D (승규: "점프 발판은 3D로"), 3 m square on the kit pad's 3 m base,
        //                    its arrow along the launch, its deck where the kit plate's top was (0.41 m); the base's
        //                    collider raised to that plane too, so WASD on the pad meets one flat top, no edge
        const string FloatingPadModel = "PawnRushGrandStairHall/FloatingPad", JumpPadModel = "PawnRushGrandStairHall/JumpPadModel";
        const float PadDeck = .41f;   // the kit plate's top: the launch height is as before

        static void HangPads(Transform content, Material clay)
        {
            var stoneModel = Resources.Load<GameObject>(FloatingPadModel);
            var jumpModel = Resources.Load<GameObject>(JumpPadModel);
            if (stoneModel == null || jumpModel == null)
                Debug.LogError("[대계단 홀] 발판 모델을 불러오지 못했습니다: Resources/" + FloatingPadModel + ".fbx, " + JumpPadModel + ".fbx");
            int stones = 0, pads = 0;
            foreach (var t in content.GetComponentsInChildren<Transform>(true))
            {
                if (stoneModel != null && t.name.StartsWith("Floating stone ") && FloatingPad(content, t, stoneModel, clay)) stones++;
                else if (jumpModel != null && t.name == "02 Jump pad" && JumpPad(t, jumpModel, clay)) pads++;
            }
            Debug.Log("[대계단 홀] 떠 있는 발판 " + stones + "개, 점프 발판 " + pads + "개를 새 모델로");
            FallingKnights(content, clay);
            HangHammers(content, clay);
        }

        // R317 (승규 11-03): the hall's falling pieces drawn as 승규's chess knight (knight_v20.py: 1.8 m round base like
        // the kit pawn's foot, 2.4 m high, origin at the base's bottom, head to +z). It hangs on the piece's "Falling
        // pawn", which the obstacle moves; the kit pawn's foot, body and head draw nothing and keep their colliders.
        const string FallingKnightModel = "PawnRushGrandStairHall/FallingKnight";

        static void FallingKnights(Transform content, Material clay)
        {
            var model = Resources.Load<GameObject>(FallingKnightModel);
            if (model == null)
            {
                Debug.LogError("[대계단 홀] 기사 모델을 불러오지 못했습니다: Resources/" + FallingKnightModel + ".fbx");
                return;
            }
            int knights = 0;
            foreach (var t in content.GetComponentsInChildren<Transform>(true))
            {
                if (!t.name.StartsWith("10 Falling piece ")) continue;
                var pawn = FindUnder(t, "Falling pawn");
                if (pawn == null || pawn.Find("Knight model") != null) continue;
                foreach (var r in pawn.GetComponentsInChildren<Renderer>(true)) r.enabled = false;
                Hang(pawn, model, "Knight model", Vector3.zero, clay);
                // R341: its effects on its own clock (FallingPieceFx: W1 the floor warning that fills as it lands).
                var hazard = t.GetComponentInChildren<ChessFight.ProtectKing.FallingChessPiece>(true);
                if (hazard != null && !hazard.TryGetComponent<FallingPieceFx>(out _)) hazard.gameObject.AddComponent<FallingPieceFx>();
                // R341 (승규 "화살표 지워"): the kit's four push arrows, 4.5 m out, one hanging in the air off the narrow stair;
                // the shockwave (FallingPieceFx I2) shows the push now.
                foreach (var arrow in t.GetComponentsInChildren<Transform>(true))
                    if (arrow.name == "Direction arrow")
                        foreach (var r in arrow.GetComponentsInChildren<Renderer>(true)) r.enabled = false;
                knights++;
            }
            Debug.Log("[대계단 홀] 떨어지는 말 " + knights + "개를 기사 모델로");
        }

        static bool FloatingPad(Transform content, Transform stone, GameObject model, Material clay)
        {
            if (stone.Find("Pad model") != null) return false;
            var at = content.InverseTransformPoint(stone.position);
            stone.localRotation = Quaternion.Euler(0f, Mathf.Atan2(at.x - GrandStairHallBuilder.Hub.x, at.z - GrandStairHallBuilder.Hub.y) * Mathf.Rad2Deg, 0f);
            // Immediate, as CourseBuilder.Finish: the module is still inactive while it is built.
            if (stone.TryGetComponent<MeshCollider>(out var round)) Object.DestroyImmediate(round);
            if (!stone.TryGetComponent<BoxCollider>(out var box)) box = stone.gameObject.AddComponent<BoxCollider>();
            box.center = Vector3.zero;
            box.size = new Vector3(1f, 2f, 1f);          // the cylinder's own size: 2.6 x 0.5 x 2.6 under its scale
            if (stone.TryGetComponent<Renderer>(out var drawn)) drawn.enabled = false;
            Hang(stone, model, "Pad model", Vector3.up, clay);   // the cylinder's top is local y 1
            return true;
        }

        static bool JumpPad(Transform pad, GameObject model, Material clay)
        {
            if (pad.Find("Pad model") != null) return false;
            foreach (var r in pad.GetComponentsInChildren<Renderer>(true)) r.enabled = false;
            Sink(FindUnder(pad, "Spring plate"), PadDeck);
            Sink(FindUnder(pad, "Jump pad base"), PadDeck);
            Hang(pad, model, "Pad model", Vector3.up * PadDeck, clay);
            // R355: its glow, rising chevrons and motes, and the burst when a pawn lands on it (JumpPadFx).
            if (!pad.TryGetComponent<JumpPadFx>(out _)) pad.gameObject.AddComponent<JumpPadFx>();
            return true;
        }

        // A box piece moved up or down so its top is at `top` (in its parent).
        static void Sink(Transform t, float top)
        {
            if (t == null) return;
            var p = t.localPosition;
            p.y = top - t.localScale.y * .5f;
            t.localPosition = p;
        }

        static void Hang(Transform on, GameObject model, string name, Vector3 localTop, Material clay)
        {
            var view = Object.Instantiate(model, on, false);
            view.name = name;
            view.transform.localPosition = localTop;
            view.transform.localRotation = Quaternion.identity;
            var s = on.localScale;
            view.transform.localScale = new Vector3(1f / s.x, 1f / s.y, 1f / s.z);   // metres in the hall, whatever the piece's scale
            foreach (var r in view.GetComponentsInChildren<Renderer>(true))
            {
                var slots = r.sharedMaterials;
                for (int i = 0; i < slots.Length; i++)
                {
                    string slot = slots[i] != null ? slots[i].name : "";
                    slots[i] = PadMaterial(slot) ?? KnightMaterial(slot) ?? clay;
                }
                r.sharedMaterials = slots;
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            }
            foreach (var c in view.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(c);
        }

        // R338 (승규 11-03, Tripo's preview of the knight, "직접 칠해줘"): the falling knight (knight_v34.py, all 534k faces
        // kept) in four materials painted by its shape: Knight_Ivory the head, Knight_Cream the round base, Knight_Dark
        // the plinth and its box, Knight_Gold the crowns, the plinth's emblems and frames and the rims.
        static Material knightIvory, knightCream, knightDark, knightGold;

        static Material KnightMaterial(string name)
        {
            if (!name.StartsWith("Knight_")) return null;
            if (knightGold == null)
            {
                knightIvory = PieceFigure.Lit(new Color(.94f, .9f, .82f), .45f);
                knightCream = PieceFigure.Lit(new Color(.88f, .82f, .7f), .3f);
                knightDark = PieceFigure.Lit(new Color(.16f, .15f, .19f), .5f);
                knightGold = PieceFigure.Lit(new Color(.86f, .64f, .28f), .7f, .65f);
            }
            return name.StartsWith("Knight_Ivory") ? knightIvory : name.StartsWith("Knight_Cream") ? knightCream
                 : name.StartsWith("Knight_Dark") ? knightDark : knightGold;
        }

        // R331 (승규 11-03 "발판도 텍스쳐 입혀", after the assets' own previews): the pads' FBX carry UVs and a Top and a Side
        // material each (pads_v28.py); their textures (pad_textures_v28.py) sit beside them in Resources: <name>.png the
        // colour, <name>_Emit.png what glows orange (the seams, the border groove, the arrow), <name>_MS.png metallic in R
        // and smoothness in A (the gold). Built once per material name; null for any other name (the clay then).
        const string PadTextures = "PawnRushGrandStairHall/";
        static readonly Dictionary<string, Material> padMaterials = new Dictionary<string, Material>();

        static Material PadMaterial(string name)
        {
            int cut = name.IndexOf(' ');
            if (cut >= 0) name = name.Substring(0, cut);           // "FloatingPad_Top (Instance)"
            if (!name.StartsWith("FloatingPad_") && !name.StartsWith("JumpPad_")) return null;
            if (padMaterials.TryGetValue(name, out var cached) && cached != null) return cached;
            var albedo = Resources.Load<Texture2D>(PadTextures + name);
            if (albedo == null)
            {
                Debug.LogError("[대계단 홀] 발판 텍스처를 불러오지 못했습니다: Resources/" + PadTextures + name + ".png");
                return null;
            }
            var m = new Material(PieceFigure.Lit(Color.white, .5f)) { name = "Pad " + name, mainTexture = albedo };
            var ms = Resources.Load<Texture2D>(PadTextures + name + "_MS");
            if (ms != null)
            {
                m.SetTexture("_MetallicGlossMap", ms);
                m.EnableKeyword("_METALLICSPECGLOSSMAP");
                LitMaterials.SetMetallic(m, 1f);
                LitMaterials.SetSmoothness(m, 1f);                 // the map's alpha scaled by this
            }
            var glow = Resources.Load<Texture2D>(PadTextures + name + "_Emit");
            if (glow != null)
            {
                m.SetTexture("_EmissionMap", glow);
                LitMaterials.SetEmission(m, Color.white * 3f);   // HDR: the orange lines over the bloom's threshold
            }
            return padMaterials[name] = m;
        }

        static Transform FindUnder(Transform root, string name)
        {
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
                if (t.name == name) return t;
            return null;
        }

        // The hall's untextured clay (Blender's Arch_Clay), also the falling chandeliers'.
        public static Material HallClay => PieceFigure.Lit(new Color(.62f, .62f, .62f), .15f);

        static string PathUnder(Transform root, Transform t)
        {
            var path = t.name;
            for (var p = t.parent; p != null && p != root; p = p.parent) path = p.name + "/" + path;
            return path;
        }
    }
}
