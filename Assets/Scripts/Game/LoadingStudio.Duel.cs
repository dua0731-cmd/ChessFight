using ChessFight.Network;
using UnityEngine;

namespace ChessFight.Game
{
    // R543 (11-11, 승규 님: "폰 러쉬 매칭 배경을 사진과 같이" → "실제로 저 사진과 같이 해주고 기물만 실제 인 게임에서
    // 사용 하는 폰으로"): Pawn Rush's entrance screen is the user's picture itself, a white and a black pawn facing the
    // camera across a brass strip on a polished chessboard floor in a panelled room, teal light on the left wall and red on
    // the right. The picture's two pawns are painted out (PawnRushDuel.png, made from the picture by
    // ArtSource/Loading/PawnRushDuel/clean_plate.py) and the match's own pawns stand there instead, walking in as each
    // player joins: each team in a little pyramid on its side of the strip (three in front, two behind, one at the back),
    // every pawn's head and body in sight, a shadow on the floor under it and one cast away from the lamp over the strip.
    //
    // R548 (11-11, 승규 님: "뭔가 잘 안어울린다" → "보드 게임 길로"): switched off, the course board (LoadingStudio.Board.cs)
    // took its place (Build sets duel false). Its picture moved out of Resources to ArtSource/Loading/PawnRushDuel/
    // PawnRushDuel.png so builds no longer carry it: put it back in Resources/Loading to use this again.
    //
    // The picture is drawn on a card the camera holds (Backdrop) and the camera is the picture's: the floor's vanishing
    // point (row 280 of 696) and focal length (1253 pixels, read off the squares) give its field of view and its slight
    // look down. A pawn's spot is where the ray through its foot in the picture meets the floor, so it stands on the
    // picture's floor at the right size on any screen shape.
    public sealed partial class LoadingStudio
    {
        const string DuelPicture = "Loading/PawnRushDuel";
        const float PhotoW = 1261f, PhotoH = 696f, PhotoFocal = 1253f, PhotoHorizon = 280f;
        // The picture's pawns: the middle of the foot and the top of the head, white's (black's is mirrored about the strip).
        const float PhotoFootX = 478f, PhotoFootY = 520f, PhotoTopY = 230f;
        // Where white's pawns stand in the picture (foot, in its pixels), in the order players join. 11-11, second screen
        // (승규 님: "들어가면 뒤로 줄 서는 데 그럼 어느 순간 들어온 사람은 안보여"): no longer in file behind the first, which
        // hid whoever came later, but a pyramid: three in front (the first next to the strip), two behind standing in
        // their gaps, one at the back over the middle. Drawn over the picture first (ArtSource/Loading/PawnRushDuel/
        // lineup.py): every head shows whole and 89 % or more of every body. Every pawn, arms out (.79 of its height
        // across), keeps 25 pixels or more off the strip (x 630), right of x 320 where the white team list ends on a 16:9
        // screen and above y 555 where the buttons begin (black's mirror the same).
        static readonly Vector2[] DuelFeet =
        {
            new Vector2(551f, 548f), new Vector2(462f, 548f), new Vector2(506f, 474f),
            new Vector2(373f, 548f), new Vector2(417f, 474f), new Vector2(462f, 426f)
        };
        // The match's pawn as a part of the picture's (11-11: .85 → .4 so six fit a side; the picture's pawn is 290 of its
        // 696 rows tall, the front row's 130), how far past the picture's edge it starts its walk (pixels) and how much it
        // turns from the camera towards the strip.
        const float DuelSize = .4f, DuelWalkFrom = 90f, DuelTurn = .35f;
        // The floor shadows, in the pawn's heights and at full strength (Shade): one under the feet, one cast away from
        // the lamp over the strip, as the picture's pawns cast theirs.
        const float ContactWide = .95f, ContactDeep = .7f, ContactDark = .75f;
        const float CastLong = 1.5f, CastDeep = .55f, CastOut = .55f, CastBack = .1f, CastDark = .32f;

        bool duel;
        float duelScale = 1.05f, duelPawn = 1.9f;
        readonly Vector3[] duelAt = new Vector3[PerTeam * 2];
        readonly float[] duelStart = new float[PerTeam * 2];

        Vector3 DuelSpot(int team, int spot) => duelAt[team * PerTeam + spot];
        float DuelStart(int team, int spot) => duelStart[team * PerTeam + spot];

        // Where a team's pawns stand, on average: the lights aim at the whole group, not its first pawn.
        Vector3 DuelGroup(int team)
        {
            var sum = Vector3.zero;
            for (int spot = 0; spot < PerTeam; spot++) sum += DuelSpot(team, spot);
            return sum / PerTeam;
        }

        // The two shadows under a pawn on the picture's floor, in a warm near-black like the picture's (Shade fades them
        // in and moves them with the pawn while it walks on).
        void DuelShadows(Transform t, Player p, Transform contact)
        {
            float h = duelPawn;
            contact.localScale = new Vector3(ContactWide * h / 1.3f, ContactDeep * h, 1f);
            var shade = new Color(.06f, .03f, .015f, 0f);
            p.Disc.color = shade;
            p.DiscT = contact;
            p.DiscHome = contact.localPosition;
            p.CastHome = p.At + new Vector3(-p.Side * CastOut * h, .02f, CastBack * h);
            var cast = LastSceneArt.Part(t, "Cast Shadow", LastSceneArt.Panel(1f, 1f), new Material(p.Disc) { color = shade }, p.CastHome,
                                         Quaternion.Euler(90f, 0, 0), new Vector3(CastLong * h, CastDeep * h, 1f), false);
            p.CastT = cast.transform;
            p.Cast = cast.GetComponent<Renderer>().sharedMaterial;
        }

        void Duel(Transform t, int width, int height)
        {
            float aspect = width / Mathf.Max(1f, height);
            // the picture covers the view: on a screen wider than it its top and bottom are cut, so it is taller than the view
            float tall = Mathf.Max(1f, aspect / (PhotoW / PhotoH));
            float focal = PhotoFocal / PhotoH * tall;   // in view heights
            camera.fieldOfView = 2f * Mathf.Atan(.5f / focal) * Mathf.Rad2Deg;
            camera.farClipPlane = 90f;
            camera.transform.localRotation = Quaternion.Euler(Mathf.Atan((PhotoH * .5f - PhotoHorizon) / PhotoFocal) * Mathf.Rad2Deg, 0f, 0f);
            camera.backgroundColor = MenuArt.Hex(0x1A120C);
            if (!Backdrop(camera.transform, width, height, DuelPicture))
                Debug.LogWarning("[Loading] " + DuelPicture + " is missing: the Pawn Rush entrance screen has no picture behind its pawns.");

            // How tall the match's pawn is, then the camera's height that makes it the picture's pawn's size: the
            // picture's pawn measured with the camera one unit up, everything scaling with that height.
            float pawn = PawnHeight(t) * duelScale;
            camera.transform.localPosition = new Vector3(0f, 1f, 0f);
            float perUnit = PictureHeight(PhotoFootX, PhotoFootY, PhotoTopY);
            float lift = perUnit > .01f ? pawn / (perUnit * DuelSize) : pawn;
            camera.transform.localPosition = new Vector3(0f, lift, 0f);
            duelPawn = pawn;
            for (int team = 0; team < 2; team++)
                for (int spot = 0; spot < PerTeam; spot++)
                {
                    var foot = DuelFeet[spot];
                    duelAt[team * PerTeam + spot] = OnFloor(team == 0 ? foot.x : PhotoW - foot.x, foot.y);
                    // each pawn walks in along its own row from past the picture's edge, so none pops up on the floor
                    duelStart[team * PerTeam + spot] = OnFloor(team == 0 ? -DuelWalkFrom : PhotoW + DuelWalkFrom, foot.y).x;
                }

            // The light on the pawns, as on the picture's: a warm lamp high over the strip a little in front, wide enough
            // for both groups (it lights each pawn from the strip's side, as the picture's are lit), the walls' teal and
            // red from behind each group, faint and greyed (11-11: at full colour they ringed the pawns like a colour
            // fringe), and a warm fill from the camera.
            Vector3 white = DuelGroup(0), black = DuelGroup(1), middle = (white + black) * .5f;
            float h = pawn;
            MenuArt.Spot(t, middle + new Vector3(0f, h * 4.2f, -h * 2.2f), middle + Vector3.up * h * .45f, 0xFFD6A2, 2.5f, .95f, .7f, true);
            MenuArt.Spot(t, white + new Vector3(-h * 2.4f, h * 1.8f, h * 2.2f), white + Vector3.up * h * .6f, 0x6AB4AE, 1.1f, .7f, .8f);
            MenuArt.Spot(t, black + new Vector3(h * 2.4f, h * 1.8f, h * 2.2f), black + Vector3.up * h * .6f, 0xC86A62, 1.1f, .7f, .8f);
            MenuArt.Spot(t, new Vector3(0f, lift * 1.4f, 0f), middle + Vector3.up * h * .5f, 0xC8946A, .7f, .7f, .9f);
        }

        // The picture's pixel as a point of the camera's view (the picture covering it, centred).
        Vector2 PictureView(float px, float py)
        {
            float aspect = camera.aspect, picture = PhotoW / PhotoH;
            float tall = Mathf.Max(1f, aspect / picture), wide = picture / aspect * tall;
            return new Vector2(.5f + (px / PhotoW - .5f) * wide, .5f - (py / PhotoH - .5f) * tall);
        }

        // Where the ray through the picture's pixel meets the floor (the studio's y 0), in the studio's space.
        Vector3 OnFloor(float px, float py)
        {
            var v = PictureView(px, py);
            var ray = camera.ViewportPointToRay(new Vector3(v.x, v.y, 0f));
            float along = ray.direction.y < -1e-4f ? (Origin.y - ray.origin.y) / ray.direction.y : 20f;
            return root.transform.InverseTransformPoint(ray.GetPoint(along));
        }

        // How tall something standing at the picture's foot pixel is when its top is at `topY`, for the camera as it is.
        float PictureHeight(float px, float footY, float topY)
        {
            var foot = OnFloor(px, footY);
            var v = PictureView(px, topY);
            var ray = camera.ViewportPointToRay(new Vector3(v.x, v.y, 0f));
            var from = root.transform.InverseTransformPoint(ray.origin);
            var dir = root.transform.InverseTransformDirection(ray.direction);
            // meet the upright plane through the foot that faces the camera
            var normal = new Vector3(foot.x - from.x, 0f, foot.z - from.z).normalized;
            float d = Vector3.Dot(dir, normal);
            if (Mathf.Abs(d) < 1e-4f) return 0f;
            var top = from + dir * (Vector3.Dot(foot - from, normal) / d);
            return top.y - foot.y;
        }

        // The match pawn's height at scale 1, measured on a copy built and thrown away. 11-11 (second screen, 승규 님: "지금도
        // 너무 커"): reading the height off BakeMesh still left out the model's own 1.75 (LastSceneFigure.ModelScale, on the
        // skin's parent), whichever way it was baked. The skin is now posed by hand, each vertex through its bones' own
        // matrices (bone to world times the bind pose), which no scale can slip past; the head's end bone checks it.
        float PawnHeight(Transform t)
        {
            var probe = LastSceneFigure.Build(PieceKind.Pawn, 0, t);
            probe.WearPawnRushLook();
            probe.Root.transform.localPosition = Vector3.zero;
            float skinned = 0f, bone = 0f;
            foreach (var skin in probe.Root.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                var mesh = skin.sharedMesh;
                var bones = skin.bones;
                if (mesh == null || !mesh.isReadable || bones == null || bones.Length == 0) continue;
                var bind = mesh.bindposes;
                var weights = mesh.boneWeights;
                var vertices = mesh.vertices;
                if (bind.Length != bones.Length || weights.Length != vertices.Length) continue;
                var toStudio = new Matrix4x4[bones.Length];
                for (int i = 0; i < bones.Length; i++)
                    toStudio[i] = bones[i] != null ? t.worldToLocalMatrix * bones[i].localToWorldMatrix * bind[i] : Matrix4x4.zero;
                for (int v = 0; v < vertices.Length; v++)
                {
                    var w = weights[v];
                    var p = toStudio[w.boneIndex0].MultiplyPoint3x4(vertices[v]) * w.weight0
                        + toStudio[w.boneIndex1].MultiplyPoint3x4(vertices[v]) * w.weight1
                        + toStudio[w.boneIndex2].MultiplyPoint3x4(vertices[v]) * w.weight2
                        + toStudio[w.boneIndex3].MultiplyPoint3x4(vertices[v]) * w.weight3;
                    skinned = Mathf.Max(skinned, p.y);
                }
            }
            foreach (var b in probe.Root.GetComponentsInChildren<Transform>(true))
                if (b.name == "mixamorig:HeadTop_End") bone = t.InverseTransformPoint(b.position).y;
            probe.Root.SetActive(false);
            Object.Destroy(probe.Root);
            // The model is about one unit tall in its own space, 1.75 in the world (ModelScale).
            float top = skinned > .2f ? skinned : bone > .2f ? bone : 1.75f;
            Debug.Log($"[Loading] Pawn Rush pawn {top:0.00} tall (skin {skinned:0.00}, head bone {bone:0.00})");
            return top;
        }
    }
}
