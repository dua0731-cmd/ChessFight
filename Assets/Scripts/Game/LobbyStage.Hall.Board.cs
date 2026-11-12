using UnityEngine;

namespace ChessFight.Game
{
    // R589 (승규 "체스 판을 레퍼런스 사진처럼", reference.webp / reference_stairs.webp / reference_side_windows.webp): the
    // kingdom hall's stage as in the pictures. Honey maple and warm red-brown walnut squares (the old board's walnut was
    // nearly black), a polished red mahogany frame with a thin gold line round the squares and along its top edges, the
    // stage under it in a deeper mahogany with gold trim top and bottom and gold-framed panels on its faces (on the
    // front either side of the carpet), and a gold crown on the front of the frame over the carpet (StageCrown.fbx,
    // ArtSource/Lobby/KingdomInterior/stage_crown.py). The courtyard castle and the old hall keep Stage's own board.
    public sealed partial class LobbyStage
    {
        static readonly int[] HallMaple = { 0xEDBE7E, 0xC48A4C, 0xF9DCA6 };
        static readonly int[] HallWalnut = { 0xA35A30, 0x76391B, 0xBE6E3E };
        static readonly int[] HallMahogany = { 0x8A3420, 0x5C1F12, 0xA4442A };
        static readonly int[] HallStageWood = { 0x6E2616, 0x481408, 0x86331E };
        const int HallGold = 0xE3B04B;
        const float HallBoardGloss = .62f, HallFrameGloss = .8f, HallStageGloss = .7f;
        const float HallCarpetHalf = 1.5f;      // the front steps' carpet (blockout Front Steps), the front panels clear it
        const float HallCrownWidth = .55f;      // the crown's width on the frame, metres

        void HallStage(Transform root)
        {
            float k = stageScale, size = StageSize * k, board = BoardSize * k, frame = 10.4f * k;
            var gold = PieceFigure.Lit(MenuArt.Hex(HallGold), .78f, .6f);
            MenuArt.Slab(root, "Stage", new Vector3(0, stageBase / 2, 0), new Vector3(size, stageBase, size),
                         MenuArt.Wood(HallStageWood, HallStageGloss, new Vector2(3f, 1f)));
            MenuArt.Slab(root, "Frame", new Vector3(0, stageBase + stageFrame / 2, 0), new Vector3(frame, stageFrame, frame),
                         MenuArt.Wood(HallMahogany, HallFrameGloss, new Vector2(2f, 1f)));
            MenuArt.Slab(root, "Board", new Vector3(0, Top - .01f, 0), new Vector3(board, .02f, board),
                         MenuArt.Board(8, HallMaple, HallWalnut, HallBoardGloss));

            float frameTop = stageBase + stageFrame;
            for (int s = -1; s <= 1; s += 2)
            {
                // the gold line round the squares
                MenuArt.Slab(root, "Inlay", new Vector3(0, Top + .004f, s * (board / 2 + .05f)), new Vector3(board + .14f, .02f, .04f), gold);
                MenuArt.Slab(root, "Inlay", new Vector3(s * (board / 2 + .05f), Top + .004f, 0), new Vector3(.04f, .02f, board + .14f), gold);
                // the frame's top edges, and the stage's top and bottom edges
                foreach (var (y, half, h) in new[] { (frameTop - .012f, frame / 2f, .03f), (stageBase - .02f, size / 2f, .04f), (.025f, size / 2f, .03f) })
                {
                    float w = 2f * half + .035f;
                    MenuArt.Slab(root, "Trim", new Vector3(0, y, s * half), new Vector3(w, h, .035f), gold);
                    MenuArt.Slab(root, "Trim", new Vector3(s * half, y, 0), new Vector3(.035f, h, w), gold);
                }
            }

            // gold-framed panels on the stage's faces
            float y0 = .075f, y1 = stageBase - .075f, edge = size / 2f - .3f;
            var front = new[] { (-edge, -HallCarpetHalf - .25f), (HallCarpetHalf + .25f, edge) };
            var other = new[] { (-edge, -1.35f), (-1.15f, 1.15f), (1.35f, edge) };
            foreach (var (normal, along) in new[] { (Vector3.back, Vector3.right), (Vector3.forward, Vector3.right), (Vector3.left, Vector3.forward), (Vector3.right, Vector3.forward) })
                foreach (var (u0, u1) in normal == Vector3.back ? front : other)
                    Panel(root, gold, normal * (size / 2f + .012f), along, u0, u1, y0, y1);

            HallCrown(root, frame);
        }

        // A rectangle of thin gold bars standing on a face of the stage: u along the face, y up.
        static void Panel(Transform root, Material gold, Vector3 face, Vector3 along, float u0, float u1, float y0, float y1)
        {
            const float bar = .022f, depth = .02f;
            Vector3 Size(float lu, float ly) => along.x != 0 ? new Vector3(lu, ly, depth) : new Vector3(depth, ly, lu);
            float um = (u0 + u1) / 2f, ym = (y0 + y1) / 2f;
            MenuArt.Slab(root, "Panel", face + along * um + Vector3.up * y0, Size(u1 - u0, bar), gold);
            MenuArt.Slab(root, "Panel", face + along * um + Vector3.up * y1, Size(u1 - u0, bar), gold);
            MenuArt.Slab(root, "Panel", face + along * u0 + Vector3.up * ym, Size(bar, y1 - y0), gold);
            MenuArt.Slab(root, "Panel", face + along * u1 + Vector3.up * ym, Size(bar, y1 - y0), gold);
        }

        // The crown on the front of the frame, over the carpet, facing the hall's gate end (-Z).
        void HallCrown(Transform root, float frame)
        {
            var prefab = Resources.Load<GameObject>("Lobby/KingdomHall/StageCrown");
            if (prefab == null) return;
            var crown = Instantiate(prefab, root);
            crown.name = "Stage Crown";
            crown.transform.localRotation = Quaternion.Euler(0, 180f, 0);
            var bounds = new Bounds(crown.transform.position, Vector3.zero);
            var renderers = crown.GetComponentsInChildren<Renderer>();
            foreach (var r in renderers) bounds.Encapsulate(r.bounds);
            float scale = bounds.size.x > 0 ? HallCrownWidth / bounds.size.x : 1f;
            crown.transform.localScale = Vector3.one * scale;
            // its foot on the stage's top ledge, its back against the frame's front face
            crown.transform.localPosition = new Vector3(0, stageBase, -frame / 2f - .08f);
            var gold = PieceFigure.Lit(MenuArt.Hex(HallGold), .8f, .7f);
            var ruby = PieceFigure.Lit(MenuArt.Hex(0xB0182A), .9f, 0f);
            foreach (var r in renderers)
            {
                var mats = r.sharedMaterials;
                for (int i = 0; i < mats.Length; i++)
                    mats[i] = mats[i] != null && mats[i].name.StartsWith("Ruby") ? ruby : gold;
                r.sharedMaterials = mats;
            }
        }
    }
}
