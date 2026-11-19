using System;
using System.Collections.Generic;
using UnityEngine;

namespace ChessFight.Gameplay
{
    // R2 closing books (PLAN.md R2, 11-18; ArtSource/PawnRush/RoyalLibraryModels/rl_r2_books_model.py): the two folios
    // (one FBX written in "Closing royal folio -100"'s frame, hung on both books), their moving covers (written in
    // "Horizontal closing cover"'s frame, so they turn with ClosingBook), the two desks drawn 2.22 m lower (the book
    // grows 2 m down; the desk collider stays, buried in the book), the seal rest island and the warning bookmark.
    // Colliders, the cover pivot and the walkable page top (y 0) stay the prefab's.
    //
    // Bookmark: "Book warning bookmark" is ClosingBook.warningStrip - a property block turns it red from the warning
    // until the cover reopens (cleared = its gold). The silk ribbon with brass tips (RlBookmark) hangs on it and the strip
    // stops drawing; BookmarkRibbon copies the strip's colour each frame onto the ribbon's silk only (deep gold at rest),
    // so the signal reads as before. ClosingBook keeps writing the block on the disabled renderer.
    public sealed partial class RoyalLibraryDressing
    {
        static readonly Hang[] Books =
        {
            new Hang
            {
                zone = "Books", model = "RlFolioBook", on = "Closing royal folio -100", under = "Closing royal folio -100",
                covers = new[] { "Walkable book pages", "Fixed leather cover", "Gilded book spine", "Printed folio line*" },
            },
            new Hang
            {
                zone = "Books", model = "RlFolioBook", on = "Closing royal folio -76", under = "Closing royal folio -76",
                covers = new[] { "Walkable book pages", "Fixed leather cover", "Gilded book spine", "Printed folio line*" },
            },
            new Hang
            {
                zone = "Books", model = "RlFolioCover", on = "Horizontal closing cover", under = "Horizontal closing cover",
                covers = new[] { "Moving leather cover", "Moving ivory page", "Royal cover crest" },
            },
            new Hang { zone = "Books", model = "RlFolioDesk", on = "Royal folio desk -100", covers = new[] { "Royal folio desk -100*" } },
            new Hang { zone = "Books", model = "RlFolioDesk", on = "Royal folio desk -76", covers = new[] { "Royal folio desk -76*" } },
            new Hang { zone = "Books", model = "RlSealIsland", covers = new[] { "Fixed seal island*", "Royal seal rest island" } },
            new Hang { zone = "Books", model = "RlBookmark", on = BookmarkName, covers = new[] { BookmarkName } },
        };

        const string BookmarkName = "Book warning bookmark";

        static readonly Action<RoyalLibraryDressing> BooksAfterDress = d =>
        {
            var hosts = new List<Transform>();
            Find(d.transform, BookmarkName, hosts);
            foreach (var host in hosts)
            {
                var strip = host.GetComponent<Renderer>();
                var model = host.Find("RlBookmark (model)");
                var ribbon = model != null ? model.GetComponentInChildren<Renderer>(true) : null;
                if (strip == null || ribbon == null) continue;
                int silk = -1;
                var mats = ribbon.sharedMaterials;
                for (int i = 0; i < mats.Length && silk < 0; i++)
                    if (mats[i] != null && (mats[i].name.StartsWith("RL_CreamCloth", StringComparison.Ordinal)
                                            || mats[i].name.EndsWith("cloth", StringComparison.Ordinal)))
                        silk = i;
                if (silk < 0) continue;
                var follow = ribbon.gameObject.AddComponent<BookmarkRibbon>();
                follow.strip = strip;
                follow.silk = silk;
            }
        };

        sealed class BookmarkRibbon : MonoBehaviour
        {
            static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
            static readonly int ColorId = Shader.PropertyToID("_Color");
            static readonly Color Rest = new Color(.98f, .73f, .31f);   // deep gold over the cream silk (sRGB)

            public Renderer strip;
            public int silk;
            Renderer ribbon;
            MaterialPropertyBlock read, write;
            Color shown = new Color(-1, -1, -1, -1);

            void Awake()
            {
                ribbon = GetComponent<Renderer>();
                read = new MaterialPropertyBlock();
                write = new MaterialPropertyBlock();
            }

            void LateUpdate()
            {
                if (strip == null || ribbon == null) return;
                strip.GetPropertyBlock(read);
                var c = read.isEmpty ? Rest : read.GetColor(BaseColorId);
                if (c == shown) return;
                shown = c;
                write.Clear();
                write.SetColor(BaseColorId, c);
                write.SetColor(ColorId, c);
                ribbon.SetPropertyBlock(write, silk);
            }
        }
    }
}
