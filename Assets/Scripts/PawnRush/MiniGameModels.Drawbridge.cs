using System.Collections.Generic;
using UnityEngine;

namespace ChessFight.PawnRush
{
    // C · 해자 도개교 v2 (R502, user 11-07: "사진이랑 배치랑 완전히 똑같이", "실제로 써야지", "유니티적용"): the section laid
    // out as the R311 concept with the user's own models (ArtSource/PawnRush/MiniGames/Early/c_drawbridge_v2.py,
    // c_drawbridge_v2_export.py, c_install.py): the gatehouse with both side passages cut through, the roofed towers,
    // the winches at the front, the garden stone wall along both sides, the kit's rook pylons and central parapet, all
    // standing on the stone block module dressed in the tower's stone.
    //
    // Pictures only, like every mini-game model: colliders, doors, the deck's hinge, the rope, the knots and the ratchet
    // lamps stay the game's (MgDrawbridge moved its rope lane and winch to this layout). Made here in code, not in the
    // model (user rules, 11-07): the moat water (PR_MoatWater shader), the lantern flames and lights (SOCKET_Lantern ->
    // MiniGameFixtures) and the banners, which sway (MiniGameFixtures.Pennant, MiniGameBannerCloth).
    // Without the v2 FBX the v1 model (C_Drawbridge_Env) is used as before.
    public static partial class MiniGameModels
    {
        const string EnvV2Name = "MODEL_EnvV2";
        const string WaterV2Name = "MODEL_MoatWater";
        const string BannersV2Name = "MODEL_Banners";

        static bool DrawbridgeV2(MiniGameSection owner)
        {
            var content = owner.transform.Find(MiniGameSection.ContentName);
            var env = Resources.Load<GameObject>(Folder + "Early/C/C_Drawbridge_EnvV2");
            if (content == null || env == null) return false;
            var cache = new Dictionary<string, Material>();
            if (content.Find(EnvV2Name) == null)
            {
                Hang(content, EnvV2Name, env, cache);
                MiniGameFixtures.LightSockets(content.Find(EnvV2Name));
                MoatWaterV2(content);
                BannersV2(owner, content, cache);
            }
            Hide(content.Find(EnvName));                       // a v1 model hung earlier
            WidenCentralMoat(content);
            Hide(content.Find(MiniGamePalaceArt.ShellName));
            foreach (var floor in new[] { "Entry band", "Rear corridor", "Central aisle", "Central aisle south", "Central aisle north" })
                Hide(content.Find(floor));

            var deck = Resources.Load<GameObject>(Folder + "Early/C/C_Drawbridge_DeckV2");
            var drumModel = Resources.Load<GameObject>(Folder + "Early/C/C_Drawbridge_DrumV2");
            var linkModel = Resources.Load<GameObject>(Folder + "Early/C/C_ChainLink");
            var linkMesh = linkModel != null ? linkModel.GetComponentInChildren<MeshFilter>(true)?.sharedMesh : null;
            var iron = Resources.Load<Material>(OwnMaterials + "PR_Early_DarkIron");
            var ropeModel = Resources.Load<GameObject>(Folder + "Early/C/C_RopeSegment");
            var ropeMesh = ropeModel != null ? ropeModel.GetComponentInChildren<MeshFilter>(true)?.sharedMesh : null;
            var ropeMat = Resources.Load<Material>(OwnMaterials + "PR_Early_C_Rope");
            var knotA = Resources.Load<GameObject>(Folder + "Early/C/C_KnotA");      // c_knots.py: the rope knot
            var knotC = Resources.Load<GameObject>(Folder + "Early/C/C_KnotC");      // and the leather grip
            foreach (var station in owner.Stations)
            {
                if (station == null) continue;
                foreach (var t in station.GetComponentsInChildren<Transform>(true))
                    if (t.name == "Gate passage" || t.name == "Station floor" || t.name == "Palace station ramp") Hide(t);
                var game = station.Game != null ? station.Game.transform : null;
                if (game == null) continue;
                foreach (var t in game.GetComponentsInChildren<Transform>(true))
                    switch (t.name)
                    {
                        // the model's floors, moat banks and bottom, rook pylons, winch, rope lane band and its ticks
                        case "Moat wall S look": case "Moat wall N look": case "Moat wall W": case "Moat wall E": case "Moat bottom":
                        case "Water": case "Bridge tower": case "Winch frame": case "Rope lane": case "Lane tick": case "Lane arrow":
                        case "Pawl": case "Chain": case "Rope": case "Rope to drum":   // the game's line chains, plain rope
                            Hide(t);
                            break;
                    }
                // the user's winch: its frame and gear stand in the model; its drum (c_winch_split.py) rides the game's
                // Drum, so it turns as the rope is pulled (the frame is placed so both axes meet)
                var drum = Find(game, "Drum");
                if (drum != null)
                {
                    foreach (Transform part in drum)
                        if (part.name != RideName) Hide(part);
                    if (drumModel != null && drum.Find(RideName) == null) Hang(drum, RideName, drumModel, cache);
                    // its rope coil is drawn by the one-piece rope now (MiniGameDrawbridgeRope winds it on the drum)
                    var ride = drum.Find(RideName);
                    if (ride != null)
                        foreach (var rr in ride.GetComponentsInChildren<Renderer>(true))
                            if (rr.sharedMaterial != null && rr.sharedMaterial.name.StartsWith("PR_Early_C_Rope")) rr.enabled = false;
                }
                // thick forged chains from the winch's drum over the pylons to the deck's far corners: pulling the rope
                // turns the drum and pays them out (MiniGameDrawbridgeChain)
                if (Find(game, "Drawbridge chain W") == null)
                {
                    if (!(station.Game is MgDrawbridge bridge))
                        Debug.LogWarning("[미니게임 모델] C 쇠사슬: 이 스테이션에 MgDrawbridge가 없습니다 — " + station.name);
                    else if (linkMesh == null)
                        Debug.LogWarning("[미니게임 모델] C 쇠사슬: 고리 모델이 없습니다 — Resources/" + Folder + "Early/C/C_ChainLink.fbx");
                    else
                    {
                        for (int i = 0; i < 2; i++) MiniGameDrawbridgeChain.Add(bridge, i, linkMesh, iron);
                        Debug.Log("[미니게임 모델] C 쇠사슬 2줄을 붙였습니다: " + station.name);
                    }
                }
                // the twisted rope in one piece, flowing off the drum as it is pulled (MiniGameDrawbridgeRope)
                // the team gate's own 25 / 50 / 75 % and done moments, no ground ring (user 11-07, MiniGameDrawbridgeFx)
                if (station.Game is MgDrawbridge fxGame && station.GetComponent<IGateFx>() == null)
                    station.gameObject.AddComponent<MiniGameDrawbridgeFx>().Configure(station, fxGame);
                if (station.Game is MgDrawbridge ropeGame && ropeMesh != null && Find(game, "Drawbridge rope") == null)
                {
                    MiniGameDrawbridgeRope.Add(ropeGame, 0, ropeMesh, ropeMat);     // one piece: drum, both pulleys, the knots
                }
                // the grab knots (user 11-07, "A랑 C 합쳐 번갈아 가면서 나오게"): the leather grip on knots 1 and 3, the rope
                // knot on 2 and 4; the game's gold box is hidden (its collider stays) and the model's gold parts light in
                // the team colour while gripped
                for (int k = 0; k < 4; k++)
                {
                    var knot = Find(game, "Knot " + (k + 1));
                    var model = (k & 1) == 0 ? knotC : knotA;
                    if (knot == null || model == null || knot.Find(RideName) != null) continue;
                    var holder = new GameObject(RideName).transform;
                    holder.SetParent(knot, false);
                    var s = knot.localScale;
                    holder.localScale = new Vector3(1f / Mathf.Max(1e-4f, s.x), 1f / Mathf.Max(1e-4f, s.y), 1f / Mathf.Max(1e-4f, s.z));
                    Hang(holder, "Knot model", model, cache);
                    Renderer accent = null;
                    foreach (var rr in holder.GetComponentsInChildren<Renderer>(true))
                        if (rr.name.EndsWith("_gold")) accent = rr;
                    var handle = knot.GetComponent<GrabHandle>();
                    if (handle != null) handle.Restyle(accent);
                    else Hide(knot);
                }
                var hinge = Find(game, "Drawbridge hinge");
                if (hinge != null && deck != null)
                {
                    foreach (Transform part in hinge)
                        if (part.name != RideName) Hide(part);
                    if (hinge.Find(RideName) == null) Hang(hinge, RideName, deck, cache);
                }
            }
            return true;
        }

        // The central moat is baked into the saved prefab at its old 4 m (z 16..20); until the prefabs are rebuilt
        // (MiniGameSectionBuilder now makes it 5 m), Play stretches the baked boxes to the 5 m moat of the stations.
        static void WidenCentralMoat(Transform content)
        {
            // boxes (CourseBuilder.Box) are sized by scale; triggers and collision-only blocks by their BoxCollider
            void Set(string name, Vector3 lo, Vector3 hi, bool byCollider)
            {
                var t = content.Find(name);
                if (t == null) return;
                t.localPosition = (lo + hi) * .5f;
                if (byCollider && t.TryGetComponent<BoxCollider>(out var box)) box.size = hi - lo;
                else t.localScale = hi - lo;
            }
            Set("Central aisle south", new Vector3(-2f, -1f, 4f), new Vector3(2f, 0f, 15.5f), false);
            Set("Central aisle north", new Vector3(-2f, -1f, 20.5f), new Vector3(2f, 0f, 24f), false);
            Set("Central moat water", new Vector3(-2f, -.6f, 15.5f), new Vector3(2f, -.5f, 20.5f), false);
            Set("Central moat return", new Vector3(-2f, -1.2f, 15.5f), new Vector3(2f, -.3f, 20.5f), true);
            Set("Central moat railing collision", new Vector3(-2f, 0f, 15.2f), new Vector3(2f, 1.1f, 15.5f), true);
        }

        // One sheet of the phase-1 moat water over the whole moat (x -22..22, z 16..20), at the game's water level.
        static void MoatWaterV2(Transform content)
        {
            // user 11-07 "이제 물도 넣어줘": the material was only made by the look-test menu and was missing, so the
            // water never showed. It is an asset now (PR_Early_MoatWater.mat); should it go missing it is built here.
            var water = Resources.Load<Material>(Folder + "Early/R502/PR_Early_MoatWater");
            if (water == null)
            {
                var shader = Shader.Find("ChessFight/Pawn Rush Moat Water");
                if (shader == null)
                {
                    Debug.LogWarning("[미니게임 모델] C 해자 물: 셰이더 ChessFight/Pawn Rush Moat Water가 없습니다");
                    return;
                }
                Debug.LogWarning("[미니게임 모델] C 해자 물: 재질이 없어 실행 중에 만듭니다 — Resources/" + Folder + "Early/R502/PR_Early_MoatWater.mat");
                water = new Material(shader) { name = "PR_Early_MoatWater (runtime)" };
                water.SetTexture("_WaveMap", Resources.Load<Texture2D>(Folder + "Early/R502/Textures/PR_Early_Water_Normal"));
                water.SetTexture("_CausticMap", Resources.Load<Texture2D>(Folder + "Early/R502/Textures/PR_Early_Water_Caustic"));
            }
            // the section's own glass box sits at the same height: hidden, so only this water shows (no flicker)
            var glass = content.Find("Central moat water");
            if (glass != null) Hide(glass);
            var go = GameObject.CreatePrimitive(PrimitiveType.Plane);       // 10 x 10 m
            go.name = WaterV2Name;
            Object.Destroy(go.GetComponent<Collider>());
            go.transform.SetParent(content, false);
            go.transform.localPosition = new Vector3(0f, -.5f, 18f);
            go.transform.localScale = new Vector3(4.4f, 1f, .5f);              // the 5 m moat (z 15.5..20.5)
            var r = go.GetComponent<Renderer>();
            r.sharedMaterial = water;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        // The concept's banners, swaying: each team's banner over its own passage (x +-12), cream laurel banners by the
        // central door (x +-6.3) and on the posts at the front corners (x +-20.8). Section frame, facing the entrance.
        static void BannersV2(MiniGameSection owner, Transform content, Dictionary<string, Material> cache)
        {
            var root = new GameObject(BannersV2Name).transform;
            root.SetParent(content, false);
            foreach (var station in owner.Stations)
            {
                if (station == null) continue;
                float side = Mathf.Sign(content.InverseTransformPoint(station.transform.position).x);
                MiniGameFixtures.Pennant(root, "Team banner " + (side < 0f ? "W" : "E"), new Vector3(side * 12f, 10.9f, 23.7f), 2.6f, 3.8f,
                    station.Accent(station.Team));
            }
            var cream = Resources.Load<Material>(OwnMaterials + "PR_Mid_Canvas");
            var gold = Resources.Load<Material>(OwnMaterials + "PR_Early_C_Gold");
            if (cream == null) return;
            foreach (var (x, top, z, w, h) in new[] { (6.3f, 10.2f, 23.4f, 2f, 3.4f), (20.8f, 6.6f, .95f, 1.5f, 2.8f) })
                foreach (float s in new[] { -1f, 1f })
                {
                    var at = new Vector3(s * x, top, z);
                    MiniGameBannerCloth.Hang(root, at + Vector3.down * .06f, w, h, cream);
                    var rod = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                    rod.name = "Banner rod";
                    Object.Destroy(rod.GetComponent<Collider>());
                    rod.transform.SetParent(root, false);
                    rod.transform.localPosition = at;
                    rod.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
                    rod.transform.localScale = new Vector3(.08f, (w + .4f) * .5f, .08f);
                    rod.GetComponent<Renderer>().sharedMaterial = gold;
                }
        }
    }
}
