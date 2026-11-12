using UnityEngine;
using UnityEngine.Rendering;

namespace ChessFight.Game
{
    // The lobby's kingdom hall (2026-11-11, 승규's two pictures joined into one room): a long
    // cream stone hall under a glass vault. The lobby camera looks at the GRAND STAIR end
    // (curved double stairs, gallery, balcony, turning round window; R552 took the balcony's knight
    // and the banners out, R561 the crown chandelier);
    // the GREAT GATE is at the other end, behind the camera. On "게임 시작" the camera swings
    // round the stage and the party runs down the carpet into the gate (EnterGate / WalkCamera).
    //
    // Grey clay for now: made in Blender (ArtSource/Lobby/KingdomInterior: since R549 the stair end
    // and side walls come from the lobby kit, Kit/stair_end.py, and since R558 the gate end as well;
    // export_hall_kit.py joins them) and loaded from Resources/Lobby/KingdomHall. When the hall is missing the
    // lobby falls back to the courtyard castle (LobbyStage.Castle.cs).
    // Blender (x, y, z) arrives as Unity (x, z, y) once the root turns half round, as the castle's.
    public sealed partial class LobbyStage
    {
        bool hall;
        Quaternion hallLeafLeft, hallLeafRight;
        Vector3 lastParty;

        const float HallGateZ = -25f;          // face of the gate wall
        const float HallRunSpeed = 10f;        // ~27 m from the stage into the gate
        static readonly Vector3 HallGateLook = new Vector3(0f, 2.8f, HallGateZ - 1f);
        // R546 (승규: the reference's camera is lower): the hall's lobby camera sits low just
        // behind the stage and looks nearly level at the big stairs, as blockout_hall.py's Cam 1.
        const float HallFov = 36f;
        static readonly Vector3 HallLobbyAt = new Vector3(.4f, 3.2f, -12.5f), HallLobbyLook = new Vector3(.4f, 3.0f, 10f);
        static readonly float[] HallPilasterLanterns = { .27f, -16.08f };   // R556: along the hall (Blender y)
        Material lanternGlassMaterial;

        bool Hall(Transform root)
        {
            var prefab = Resources.Load<GameObject>("Lobby/KingdomHall/Hall");
            if (prefab == null) return false;
            castle = Instantiate(prefab, root);
            castle.name = "Lobby Kingdom Hall";
            castle.transform.localRotation = Quaternion.Euler(0, 180f, 0);
            hall = true;
            foreach (var r in castle.GetComponentsInChildren<Renderer>())
            {
                // R549: the glass vault's thin ribs cast no shadow (their stripes crossed the whole view)
                r.shadowCastingMode = r.name == "Hall_Vault" ? ShadowCastingMode.Off : ShadowCastingMode.On;
                r.receiveShadows = true;
            }
            HallPaint();   // R576: baked colour textures where they exist (LobbyStage.Hall.Paint.cs)
            HallWindowGlass();   // R582: the side windows' diamond panes
            HallVaultGlass();    // R588: the glass between the vault's ribs
            leafLeft = castle.transform.Find("GateLeaf_L");
            leafRight = castle.transform.Find("GateLeaf_R");
            // The leaves turn about the world's vertical through their hinges (the FBX
            // gives their nodes an axis-conversion rotation of their own).
            if (leafLeft != null) hallLeafLeft = leafLeft.rotation;
            if (leafRight != null) hallLeafRight = leafRight.rotation;
            var passageEnd = castle.transform.Find("Gate_Passage_End");
            if (passageEnd != null && passageEnd.TryGetComponent<Renderer>(out var wall))
            {
                gateLightMaterial = wall.material;   // an instance: only this wall glows
                gateLightMaterial.EnableKeyword("_EMISSION");
                gateLightMaterial.SetColor("_EmissionColor", Color.black);
            }
            // R556: the kit lanterns' glass (pilasters, niches) is its own mesh and glows warm
            var glass = castle.transform.Find("Lantern_Glass");
            if (glass != null && glass.TryGetComponent<Renderer>(out var glassRenderer))
            {
                lanternGlassMaterial = glassRenderer.material;
                lanternGlassMaterial.SetColor("_BaseColor", MenuArt.Hex(0xFFD9A0));
                lanternGlassMaterial.EnableKeyword("_EMISSION");
                lanternGlassMaterial.SetColor("_EmissionColor", MenuArt.Hex(0xFFB45E) * 1.5f);   // AI Play capture: 2.4 went white
                glassRenderer.shadowCastingMode = ShadowCastingMode.Off;
            }
            HallLight(root);
            HallReflection();   // R580: after the lights, so the probe sees the lit hall
            return true;
        }

        // R552: the hall at sunset (LobbyStage.Hall.Sunset.cs: the sun through the round window, the
        // intro's sky, the turning tracery and its light shafts) in place of R544's daylight, and warm lamps.
        void HallLight(Transform root)
        {
            view.farClipPlane = 300f;
            RenderSettings.fog = false;
            RenderSettings.reflectionIntensity = .8f;
            foreach (var light in FindObjectsByType<Light>(FindObjectsSortMode.None))
                if (light.type == LightType.Directional && light.transform.parent == null) light.enabled = false;
            HallSunset(root);
            HallWindowSun(root);   // R584: faint sunset through the +X side windows

            for (int s = -1; s <= 1; s += 2)
            {
                // R549 kit stair end: the lanterns hang in the corner blocks' front niches
                Point(root, "Niche Lantern Glow", new Vector3(s * 7.95f, 2.3f, 8.7f), 0xFFB45E, 1.0f, 3.5f);
                // R558: the gate end's kit lanterns (stair_end.py gate_end, their glass middles)
                Point(root, "Gate Lantern Glow", new Vector3(s * 8.45f, 4.93f, HallGateZ + .8f), 0xFFB45E, 1.1f, 3.5f);
                // R556: the side walls' pilaster lanterns (stair_end.py side_lanterns, their glass middles;
                // R560's deeper pilasters moved them in to 8.26)
                foreach (float z in HallPilasterLanterns)
                    Point(root, "Pilaster Lantern Glow", new Vector3(s * 8.26f, 5.57f, z), 0xFFB45E, 1.3f, 5f);
            }
            // R561 (승규 "샹들리에 지워줘"): no chandelier, so no chandelier glow
            gateGlow = Point(root, "Gate Glow", new Vector3(0, 2.8f, HallGateZ - 2.5f), 0xFFD890, 0f, 9f);
        }

        // The way each runner takes: off the front of the stage, down its two low steps
        // (blockout_hall.py Front Step), along the red carpet and through the gate into the lit passage.
        Vector3[] HallPath(float lane, Vector3 home)
        {
            float edge = StageSize * stageScale / 2f;   // the stage's front edge (-Z)
            return new[]
            {
                home,
                new Vector3(lane, Top, -edge + .3f),            // the stage's front edge
                new Vector3(lane * .6f, 0f, -edge - 1.1f),      // down the steps
                new Vector3(lane * .6f, 0f, HallGateZ + 2.6f),  // the carpet to the gate
                new Vector3(lane * .3f, .14f, HallGateZ + .6f), // onto the gate step
                new Vector3(0f, .05f, HallGateZ - 2.6f),        // through it, into the light
            };
        }

        // The leaves swing out of the hall, into the passage: the left one turns clockwise
        // seen from above, the right one the other way.
        void HallLeaves(float angle)
        {
            leafLeft.rotation = Quaternion.AngleAxis(angle, Vector3.up) * hallLeafLeft;
            leafRight.rotation = Quaternion.AngleAxis(-angle, Vector3.up) * hallLeafRight;
        }

        // The camera on "게임 시작". R599 (승규 "화면 돌 때 카메라 시점이 좀 어색해"): it used to swing round the stage's
        // middle while the party ran the other way (towards the camera, the gate is behind it), so the party left the
        // frame, the camera whipped half round in 0.2 s (about 300 degrees a second) facing an empty side wall, and at
        // the end it followed them into the gate where the leaves filled the frame. Now it turns round the party
        // itself: from the lobby view it keeps them in the middle, swings out over the right side (never through
        // them) to behind them at an even pace over most of the walk, then eases to a stop short of the gate and
        // looks in as they run into the light. blend runs 0 -> 1 over the walk (WalkCamera); a cancel runs it back.
        const float HallChaseRadius = 5.5f, HallChaseHeight = 1.9f, HallChaseArc = 1.1f;
        const float HallSwingEnd = .75f;                 // of the walk
        const float HallCameraStop = HallGateZ + 10f;    // the camera stays this side of the gate
        const float HallCameraEase = 3f;                 // metres over which it slows to that stop

        void HallCamera(ref Vector3 at, ref Vector3 look, float blend)
        {
            // where the party stands before the walk, and where it is now (heads, a metre up)
            int count = 0, homeCount = 0;
            Vector3 sum = Vector3.zero, homeSum = Vector3.zero;
            for (int i = 0; i < Spots; i++)
            {
                if (bodies[i] == null || figures[i] == null || shown[i].Invite) continue;
                homeSum += homes[i].at; homeCount++;
                if (bodies[i].activeSelf) { sum += figures[i].Position; count++; }
            }
            var home = homeCount > 0 ? homeSum / homeCount + Vector3.up : new Vector3(0f, Top + 1f, 0f);
            if (count > 0) lastParty = sum / count + Vector3.up;
            else if (lastParty == Vector3.zero) lastParty = home;
            var party = lastParty;

            // the lobby camera seen from the party's home: the turn starts from there
            var from = at - home;
            float startRadius = new Vector2(from.x, from.z).magnitude;
            float startYaw = Mathf.Atan2(from.x, -from.z);   // 0: in front of the party (towards the gate), pi: behind

            float swing = Smooth(Mathf.Clamp01(blend / HallSwingEnd));
            float close = 1f - Mathf.Pow(1f - Mathf.Clamp01(blend / HallSwingEnd), 2f);
            float yaw = Mathf.Lerp(startYaw, Mathf.PI, swing);
            float radius = Mathf.Lerp(startRadius, HallChaseRadius, close);
            float height = Mathf.Lerp(from.y, HallChaseHeight, swing) + Mathf.Sin(swing * Mathf.PI) * HallChaseArc;
            var next = party + new Vector3(Mathf.Sin(yaw) * radius, height, -Mathf.Cos(yaw) * radius);

            // it slows to a stop short of the gate instead of running into it
            float ease = HallCameraStop + HallCameraEase;
            if (next.z < ease) next.z = HallCameraStop + HallCameraEase * Mathf.Exp((next.z - ease) / HallCameraEase);

            // straight from the lobby view onto the party, and at the end into the open gate
            float onto = Smooth(Mathf.Clamp01(blend / .2f));
            float gate = Smooth(Mathf.Clamp01((blend - .55f) / .45f));
            var target = Vector3.Lerp(party, HallGateLook, .6f * gate);
            look = Vector3.Lerp(look, target, onto);
            at = next;
        }

        static float Smooth(float x) => x * x * (3f - 2f * x);
    }
}
