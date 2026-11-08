using ChessFight.Network;
using UnityEngine;

namespace ChessFight.RagdollLab
{
    /// <summary>
    /// The emote wheel (skill network test scenes, R125). Holding G shows it in the middle of the screen and frees the
    /// mouse; while G is still down, hold the left button, drag onto a slot and let go there to play that emote
    /// (RagdollPawn.Emote). Letting go of the click in the middle picks nothing; letting go of G (or Esc) closes it. Slots 1..4 sit top, right, bottom, left; a
    /// locked slot (3 and 4 for now, every slot for the pawn) is grey and cannot be picked. The pick goes into P1's next
    /// input as <see cref="PawnInput.emote"/>, so online it reaches the host like the number keys.
    /// </summary>
    [DefaultExecutionOrder(230)]
    public class EmoteWheel : MonoBehaviour
    {
        public KeyCode key = KeyCode.G;

        // Design A (R125): a round wheel cut into four quarters, the picked one gold, locked ones grey.
        const float Outer = 170f, Inner = 62f, Gap = 3f;
        static readonly Color Normal = new Color(0.15f, 0.13f, 0.36f, 0.92f), NormalText = new Color(0.93f, 0.93f, 1f);
        static readonly Color Hot = new Color(0.94f, 0.62f, 0.15f, 0.97f), HotText = new Color(0.25f, 0.14f, 0.01f);
        static readonly Color Locked = new Color(0.27f, 0.27f, 0.25f, 0.85f), LockedText = new Color(0.71f, 0.70f, 0.66f);
        static readonly Color Middle = new Color(0.17f, 0.17f, 0.16f, 0.95f), DragDot = new Color(0.98f, 0.93f, 0.85f, 0.95f);
        static readonly string[] Moods = { "도발", "기쁨", "당황", "분노" };

        LabGame game;
        bool open, dragging, relock, spent;
        int hovered;
        GUIStyle title, label;
        Texture2D white, disc, discEdge;
        readonly Texture2D[] quarters = new Texture2D[PieceEmotes.Slots];

        public bool Open => open;

        void Awake()
        {
            game = GetComponent<LabGame>();
            if (game == null) game = FindFirstObjectByType<LabGame>();
        }

        void OnDisable() => Close();

        void OnDestroy()
        {
            if (white != null) Destroy(white);
            if (disc != null) Destroy(disc);
            if (discEdge != null) Destroy(discEdge);
            foreach (var q in quarters) if (q != null) Destroy(q);
        }

        void Update()
        {
            if (game == null) return;
            if (game.AutoTest || game.SuppressInput)
            {
                Close();
                return;
            }
            // Only while G is held down: let go of it and the wheel is gone. After a pick it stays shut until the next press.
            if (!Input.GetKey(key))
            {
                Close();
                spent = false;
                return;
            }
            if (!open)
            {
                if (!spent) Show();
                return;
            }
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                relock = false;   // Esc frees the mouse in the lab too
                Close();
                spent = true;
                return;
            }
            hovered = SlotAt(Input.mousePosition);
            if (Input.GetMouseButtonDown(0)) dragging = true;
            if (dragging && Input.GetMouseButtonUp(0))
            {
                dragging = false;
                if (hovered != 0 && Usable(hovered))
                {
                    game.QueueEmote(hovered);
                    Close();
                    spent = true;
                }
            }
        }

        void Show()
        {
            open = true;
            dragging = false;
            hovered = 0;
            // Unlocking a locked cursor leaves it in the middle of the screen, which is where the wheel is.
            relock = Cursor.lockState == CursorLockMode.Locked;
            game.EmoteWheelOpen = true;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        void Close()
        {
            if (!open) return;
            open = false;
            dragging = false;
            if (game != null) game.EmoteWheelOpen = false;
            if (relock)
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }
        }

        PieceKind Piece
        {
            get
            {
                var pawn = game != null ? game.LocalPawn : null;
                return pawn != null ? pawn.Piece : PieceKind.Pawn;
            }
        }

        bool Usable(int slot) => PieceEmotes.Available(Piece, slot);

        /// <summary>The slot under a screen point (Input.mousePosition, origin bottom left), 0 for the middle. The ring is cut
        /// along its diagonals, so each slot is a quarter centred on up, right, down or left.</summary>
        static int SlotAt(Vector3 mouse)
        {
            Vector2 d = new Vector2(mouse.x - Screen.width * 0.5f, mouse.y - Screen.height * 0.5f);
            if (d.magnitude < Inner) return 0;
            if (Mathf.Abs(d.y) >= Mathf.Abs(d.x)) return d.y > 0f ? 1 : 3;
            return d.x > 0f ? 2 : 4;
        }

        /// <summary>Which way a slot's quarter points, in GUI coordinates (y down).</summary>
        static Vector2 SlotDirection(int slot)
        {
            switch (slot)
            {
                case 1: return Vector2.down;
                case 2: return Vector2.right;
                case 3: return Vector2.up;
                default: return Vector2.left;
            }
        }

        void EnsureStyles()
        {
            if (label != null) return;
            var font = Font.CreateDynamicFontFromOSFont(new[] { "Malgun Gothic", "맑은 고딕", "Segoe UI", "Arial" }, 15);
            label = new GUIStyle(GUI.skin.label) { font = font, fontSize = 15, alignment = TextAnchor.MiddleCenter, richText = true, wordWrap = true };
            label.normal.textColor = Color.white;
            title = new GUIStyle(label) { fontSize = 17, fontStyle = FontStyle.Bold };
            white = new Texture2D(1, 1) { hideFlags = HideFlags.HideAndDontSave };
            white.SetPixel(0, 0, Color.white);
            white.Apply();
            for (int slot = 1; slot <= PieceEmotes.Slots; slot++) quarters[slot - 1] = Quarter(slot);
            disc = Disc(Inner - 6f);
            discEdge = Disc(Inner - 4f);
        }

        /// <summary>One quarter of the ring as a white mask (tinted when drawn): between Inner and Outer, a small gap along
        /// the diagonals, soft edges.</summary>
        static Texture2D Quarter(int slot)
        {
            int size = Mathf.CeilToInt(Outer * 2f) + 2;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { hideFlags = HideFlags.HideAndDontSave, wrapMode = TextureWrapMode.Clamp };
            var pixels = new Color32[size * size];
            float half = size * 0.5f;
            for (int py = 0; py < size; py++)
                for (int px = 0; px < size; px++)
                {
                    // Texture rows go up; the slot test is in GUI coordinates (y down).
                    float x = px + 0.5f - half, y = half - (py + 0.5f);
                    float r = Mathf.Sqrt(x * x + y * y);
                    float gap = Mathf.Abs(Mathf.Abs(x) - Mathf.Abs(y)) * 0.7071f;
                    int at = Mathf.Abs(y) >= Mathf.Abs(x) ? (y < 0f ? 1 : 3) : (x > 0f ? 2 : 4);
                    float a = at != slot ? 0f
                        : Mathf.Clamp01(Outer - r + 0.5f) * Mathf.Clamp01(r - Inner + 0.5f) * Mathf.Clamp01(gap - Gap + 0.5f);
                    pixels[py * size + px] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(a * 255f));
                }
            tex.SetPixels32(pixels);
            tex.Apply();
            return tex;
        }

        static Texture2D Disc(float radius)
        {
            int size = Mathf.CeilToInt(radius * 2f) + 2;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { hideFlags = HideFlags.HideAndDontSave, wrapMode = TextureWrapMode.Clamp };
            var pixels = new Color32[size * size];
            float half = size * 0.5f;
            for (int py = 0; py < size; py++)
                for (int px = 0; px < size; px++)
                {
                    float x = px + 0.5f - half, y = py + 0.5f - half;
                    float a = Mathf.Clamp01(radius - Mathf.Sqrt(x * x + y * y) + 0.5f);
                    pixels[py * size + px] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(a * 255f));
                }
            tex.SetPixels32(pixels);
            tex.Apply();
            return tex;
        }

        static Rect Around(Vector2 centre, Texture tex) =>
            new Rect(centre.x - tex.width * 0.5f, centre.y - tex.height * 0.5f, tex.width, tex.height);

        void OnGUI()
        {
            if (!open || game == null) return;
            EnsureStyles();
            var piece = Piece;
            var centre = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);

            for (int slot = 1; slot <= PieceEmotes.Slots; slot++)
            {
                bool usable = Usable(slot);
                bool hot = usable && hovered == slot;
                Color back = !usable ? Locked : hot ? Hot : Normal;
                Tint(Around(centre, quarters[slot - 1]), quarters[slot - 1], back);

                var at = centre + SlotDirection(slot) * ((Inner + Outer) * 0.5f);
                var r = new Rect(at.x - 60f, at.y - 26f, 120f, 52f);
                string head = $"{slot} {Moods[slot - 1]}";
                string text = usable ? $"<b>{head}</b>\n<size=13>{PieceEmotes.Name(piece, slot)}</size>" : $"{slot} 준비 중";
                var style = hot ? title : label;
                style.normal.textColor = !usable ? LockedText : hot ? HotText : NormalText;
                GUI.Label(r, text, style);
            }
            label.normal.textColor = Color.white;
            title.normal.textColor = Color.white;

            // The drag from the middle to the cursor, while the click is held.
            if (dragging)
            {
                var mouse = new Vector2(Input.mousePosition.x, Screen.height - Input.mousePosition.y);
                var line = mouse - centre;
                float length = line.magnitude;
                if (length > 1f)
                {
                    var step = line / length;
                    for (float t = Inner - 4f; t < length; t += 9f) Fill(new Rect(centre.x + step.x * t - 1.5f, centre.y + step.y * t - 1.5f, 3f, 3f), DragDot);
                    Fill(new Rect(mouse.x - 4f, mouse.y - 4f, 8f, 8f), DragDot);
                }
            }

            Tint(Around(centre, discEdge), discEdge, NormalText);
            Tint(Around(centre, disc), disc, Middle);
            string middle = piece == PieceKind.Pawn
                ? $"{ChessPieces.Name(piece)}\n<size=11>준비 중</size>"
                : $"<b>{ChessPieces.Name(piece)}</b>\n<size=11>끌어다 놓기</size>";
            GUI.Label(new Rect(centre.x - 50f, centre.y - 26f, 100f, 52f), middle, label);
        }

        static void Tint(Rect r, Texture tex, Color c)
        {
            var before = GUI.color;
            GUI.color = c;
            GUI.DrawTexture(r, tex);
            GUI.color = before;
        }

        void Fill(Rect r, Color c) => Tint(r, white, c);
    }
}
