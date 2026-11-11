using System.Collections.Generic;
using ChessFight.Game;
using ChessFight.Gameplay;
using ChessFight.Network;
using UnityEngine;

namespace ChessFight.RagdollLab
{
    // Match HUD only. The match already owns the name, team and promotion on each PC.
    public sealed class MatchNameplate : MonoBehaviour
    {
        static readonly Color Ink = new Color32(37, 32, 29, 255);
        static readonly Color Ivory = new Color32(252, 238, 210, 255);
        static readonly RaycastHit[] SightHits = new RaycastHit[32];
        static readonly Dictionary<int, Texture2D> icons = new Dictionary<int, Texture2D>();
        static readonly GUIContent content = new GUIContent();
        static GUIStyle lettering;
        RagdollPawn pawn;
        SwordFightPawn fighter;

        public static void Attach(RagdollPawn target, SwordFightPawn sword = null)
        {
            if (!target.TryGetComponent<MatchNameplate>(out var plate))
                plate = target.gameObject.AddComponent<MatchNameplate>();
            plate.pawn = target;
            plate.fighter = sword;
        }

        void OnGUI()
        {
            if (Event.current.type != EventType.Repaint || pawn == null || (fighter != null && !fighter.Alive)) return;
            int depth = GUI.depth;
            GUI.depth = 100;
            Draw(Camera.main, pawn, pawn.Piece, false, out _);
            GUI.depth = depth;
        }

        internal static bool Draw(Camera camera, RagdollPawn target, PieceKind piece, bool skillReady, out Vector3 screen)
        {
            screen = default;
            if (Event.current.type != EventType.Repaint || !camera || string.IsNullOrEmpty(target.DisplayName)) return false;
            if (!TryProject(camera, target.bodies[(int)BodyId.Head].transform.position, out screen)) return false;

            if (lettering == null)
                lettering = new GUIStyle { font = RuntimePanels.KoreanFont, fontStyle = FontStyle.Bold,
                    alignment = TextAnchor.MiddleCenter, richText = false, wordWrap = false, clipping = TextClipping.Clip };
            float scale = Mathf.Clamp(Screen.height / 900f, .8f, 1.6f);
            lettering.fontSize = Mathf.RoundToInt(14 * scale);
            content.text = target.DisplayName;
            float textWidth = Mathf.Ceil(lettering.CalcSize(content).x);
            float height = 24 * scale, pad = 8 * scale, iconWidth = 13 * scale, gap = 4 * scale;
            float badgeWidth = skillReady ? 15 * scale + gap : 0;
            float width = pad * 2 + iconWidth + gap + textWidth + badgeWidth;
            var plate = new Rect(screen.x - width * .5f, Screen.height - screen.y - height, width, height);
            bool white = target.Team == Teams.White;
            Color background = white ? Ivory : Ink, foreground = white ? Ink : Ivory;
            var oldColor = GUI.color;
            GUI.color = Color.white;
            Pill(plate, foreground);
            var inset = new Rect(plate.x + scale, plate.y + scale, plate.width - 2 * scale, plate.height - 2 * scale);
            Pill(inset, background);
            GUI.DrawTexture(new Rect(plate.x + pad, plate.y + 3 * scale, iconWidth, height - 6 * scale), Icon(piece, white, true), ScaleMode.ScaleToFit);
            lettering.normal.textColor = foreground;
            GUI.Label(new Rect(plate.x + pad + iconWidth + gap, plate.y, textWidth, height), content, lettering);
            if (skillReady)
            {
                var badge = new Rect(plate.xMax - pad - 15 * scale, plate.y + 4 * scale, 15 * scale, height - 8 * scale);
                Pill(badge, new Color32(242, 193, 78, 255));
                lettering.normal.textColor = Ink;
                GUI.Label(badge, "E", lettering);
            }
            GUI.color = oldColor;
            return true;
        }

        static void Pill(Rect rect, Color color) =>
            GUI.DrawTexture(rect, Texture2D.whiteTexture, ScaleMode.StretchToFill, true, 0, color, 0, rect.height * .5f);

        internal static bool TryProject(Camera camera, Vector3 head, out Vector3 screen)
        {
            Vector3 anchor = head + Vector3.up * .5f;
            screen = camera.WorldToScreenPoint(anchor);
            if (screen.z <= camera.nearClipPlane || screen.z > camera.farClipPlane || !camera.pixelRect.Contains(screen)) return false;
            Vector3 headScreen = camera.WorldToScreenPoint(head);
            if (headScreen.z <= camera.nearClipPlane || !camera.pixelRect.Contains(headScreen)) return false;
            // A low wall can cover the head even when the point above it is clear.
            return !Blocked(camera, head) && !Blocked(camera, anchor);
        }

        static bool Blocked(Camera camera, Vector3 point)
        {
            Ray ray = camera.ScreenPointToRay(camera.WorldToScreenPoint(point));
            float distance = Vector3.Dot(point - ray.origin, ray.direction);
            if (distance <= 0) return true;
            int mask = camera.cullingMask;
            int count = Physics.RaycastNonAlloc(ray, SightHits, distance, mask, QueryTriggerInteraction.Ignore);
            // NonAlloc does not guarantee it keeps the nearest hits when the buffer fills.
            if (count == SightHits.Length)
            {
                var all = Physics.RaycastAll(ray, distance, mask, QueryTriggerInteraction.Ignore);
                return ContainsScenery(all, all.Length);
            }
            return ContainsScenery(SightHits, count);
        }

        static bool ContainsScenery(RaycastHit[] hits, int count)
        {
            for (int i = 0; i < count; i++)
            {
                var collider = hits[i].collider;
                // Promoted visual bodies can have additional colliders outside the ragdoll registry.
                if (!RagdollPawn.ColliderOwner.ContainsKey(collider) && !collider.GetComponentInParent<RagdollPawn>())
                    return true;
            }
            return false;
        }
        internal static Texture2D Icon(PieceKind kind, bool whiteStyle, bool teamBadge = false)
        {
            int key = (int)kind * 4 + (whiteStyle ? 1 : 0) + (teamBadge ? 2 : 0);
            if (icons.TryGetValue(key, out var have) && have != null) return have;
            Vector2[] outline = kind switch
            {
                PieceKind.King => ChessOutlines.King, PieceKind.Queen => ChessOutlines.Queen, PieceKind.Rook => ChessOutlines.Rook,
                PieceKind.Bishop => ChessOutlines.Bishop, PieceKind.Knight => ChessOutlines.Knight, _ => ChessOutlines.Pawn,
            };
            const int W = 40, H = 64;
            float s = H / 205f;
            var inside = new bool[W * H];
            for (int y = 0; y < H; y++)
                for (int x = 0; x < W; x++)
                {
                    var p = new Vector2((x + 0.5f) / s - (W / s - 100f) / 2f, (H - y - 0.5f) / s);
                    bool inn = Inside(outline, p);
                    if (kind == PieceKind.Queen) foreach (var b in ChessOutlines.QueenBalls) inn |= (p - new Vector2(b.x, b.y)).magnitude <= b.z;
                    if (kind == PieceKind.Pawn) inn |= (p - new Vector2(ChessOutlines.PawnHead.x, ChessOutlines.PawnHead.y)).magnitude <= ChessOutlines.PawnHead.z;
                    inside[y * W + x] = inn;
                }
            var t = new Texture2D(W, H, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            for (int y = 0; y < H; y++)
                for (int x = 0; x < W; x++)
                {
                    bool inn = inside[y * W + x];
                    bool edge = false;
                    if (inn)
                        for (int dy = -2; dy <= 2 && !edge; dy++)
                            for (int dx = -2; dx <= 2; dx++)
                            {
                                int nx = x + dx, ny = y + dy;
                                if (nx < 0 || ny < 0 || nx >= W || ny >= H || !inside[ny * W + nx]) { edge = true; break; }
                            }
                    Color c = whiteStyle ? (edge ? Ink : Ivory) : (teamBadge && !edge ? Ink : Ivory);
                    if (!inn) c = Color.clear;
                    t.SetPixel(x, y, c);
                }
            t.Apply();
            t.hideFlags = HideFlags.HideAndDontSave;
            icons[key] = t;
            return t;
        }

        static bool Inside(Vector2[] poly, Vector2 p)
        {
            bool c = false;
            for (int i = 0, j = poly.Length - 1; i < poly.Length; j = i++)
                if ((poly[i].y > p.y) != (poly[j].y > p.y) && p.x < (poly[j].x - poly[i].x) * (p.y - poly[i].y) / (poly[j].y - poly[i].y) + poly[i].x) c = !c;
            return c;
        }
    }
}
