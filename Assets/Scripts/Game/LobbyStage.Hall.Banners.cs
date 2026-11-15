using UnityEngine;

namespace ChessFight.Game
{
    // R663 (승규 "메인 로비에 허전해서 깃발을 두려고 하는데" → the recommended colours): two long banners
    // on the stair-end wall either side of the rose window, hung from a brass rod just under the
    // cornice (y 10.25 - 10.5, 0.28 m proud), above the galleries. Deep wine cloth with a gold-leaf
    // trim and an ivory king, so they sit in the hall's warm sunset instead of standing out of it.
    // The cloth is the title screen's pennant (IntroStage.Pennant), swaying gently; TickPennants runs it.
    public sealed partial class LobbyStage
    {
        const float HallEndWallZ = 15.48f;          // the stair-end wall's face (Unity z), measured in Play
        const int HallBannerCloth = 0x5A1A22, HallBannerTrim = 0xC9A24A, HallBannerGlyph = 0xEDE3CF;
        static readonly Vector2 HallBannerSize = new Vector2(1.5f, 3.0f);
        const float HallBannerTop = 10.05f, HallBannerX = 5.7f;

        void HallBanners(Transform root)
        {
            var brass = MenuArt.BrassMetal();
            for (int s = -1; s <= 1; s += 2)
            {
                // the cloth hangs WallGap (0.26 m) behind its top edge at most, so the rod stands that far out
                var top = new Vector3(s * HallBannerX, HallBannerTop, HallEndWallZ - .34f);
                var pennant = new IntroStage.Pennant(root, top,
                    IntroStage.PennantTexture("♔", HallBannerCloth, HallBannerTrim, HallBannerGlyph), pennants.Count);
                pennant.Shape.localRotation = Quaternion.Euler(0, 180f, 0);   // facing the lobby camera (+Z view)
                pennant.Shape.localScale = new Vector3(HallBannerSize.x / 1.25f, HallBannerSize.y / 2.4f, 1f);
                pennant.Shape.name = "Hall Banner";
                // the rod with a ball at each end, and two brackets back to the wall
                float half = HallBannerSize.x / 2f + .18f;
                var rodAt = top + new Vector3(0, .04f, -.03f);
                LastSceneArt.Part(root, "Hall Banner Rod", LastSceneArt.Cylinder(.03f, .03f, half * 2f, 12), brass,
                                  rodAt, Quaternion.Euler(0, 0, 90f), null, true);
                for (int e = -1; e <= 1; e += 2)
                {
                    var end = rodAt + new Vector3(e * half, 0, 0);
                    LastSceneArt.Part(root, "Hall Banner Finial", LastSceneArt.Sphere(.06f, 12, 8), brass, end, null, null, true);
                    float depth = HallEndWallZ - end.z;
                    LastSceneArt.Part(root, "Hall Banner Bracket", LastSceneArt.Cylinder(.018f, .018f, depth, 8), brass,
                                      new Vector3(end.x - e * .1f, end.y, end.z + depth / 2f), Quaternion.Euler(90f, 0, 0), null, true);
                }
                pennants.Add(pennant);
            }
        }
    }
}
