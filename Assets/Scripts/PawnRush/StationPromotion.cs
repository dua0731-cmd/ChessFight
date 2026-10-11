using System;
using ChessFight.Game;
using ChessFight.Gameplay;
using ChessFight.Network;
using UnityEngine;

namespace ChessFight.PawnRush
{
    // The single-use promotion pad beside a station's mini-game (R125, in place of the four placeholder pads of
    // design doc v0.4 §5's promotion zones). Every piece starts as a pawn; the first pawn of the station's team to
    // stand on the pad for PawnRushPromotion.HoldSeconds becomes the pad's piece (and gets its Pawn Rush skill),
    // and the pad is spent for the round. Stepping off, or being pushed off, starts the count over; the other
    // team cannot use it.
    //
    // The station makes it when Play starts (MissionStation.Start), so the scene built before R125 has it too.
    // The round's piece comes from MissionPicker (both teams' pads in a plaza give the same piece). Online only
    // the host judges it; the other PCs show what the host sends (ApplyRemote), like the mini-games.
    public sealed class StationPromotion : MonoBehaviour
    {
        // Station-local: u (north) and v (across) of the pad's middle, and half its side. On the entry strip
        // (u 0..2, design doc v0.4 §5) and clear of every game: A's board starts at u 2, B's plank piles stand at
        // v ±8, u 3 (picked up within 1.9 m), E's first towers at v ±5, u 5, C's rope path is v ±1.6.
        public const float U = 1f, VOuter = 4.5f, Half = 1f, MaxHeight = 2.4f;

        // A promotion that happened (offline or on the host): the playtest's log, the online link's note.
        public static event Action<StationPromotion, ICharacterDriver, PieceKind> Promoted;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => Promoted = null;

        MissionStation station;
        PieceKind piece = PieceKind.Queen;
        bool used;
        ICharacterDriver standing;
        float standFor;

        Transform marker, fill;
        Renderer slab;
        TextMesh label;
        Material slabOn, slabOff, fillMaterial;
        PieceKind shownPiece = (PieceKind)255;
        bool shownUsed;

        public MissionStation Station => station;
        public PieceKind Piece => piece;
        public bool Used => used;
        // How far the pawn on it is (0..1), for the fill.
        public float Hold01 => used ? 1f : Mathf.Clamp01(standFor / PawnRushPromotion.HoldSeconds);
        public ICharacterDriver Standing => standing;

        // Black's station is the mirror of white's: the pad on the outer side of both.
        public static Vector3 LocalMiddle(bool mirrored) => new Vector3((mirrored ? 1f : -1f) * VOuter, 0f, U);

        public static StationPromotion Make(MissionStation station)
        {
            var go = new GameObject("Promotion pad (single use)");
            go.transform.SetParent(station.transform, false);
            go.transform.localPosition = LocalMiddle(station.Mirrored);
            var pad = go.AddComponent<StationPromotion>();
            pad.station = station;
            pad.Build();
            return pad;
        }

        public void SetPiece(PieceKind kind)
        {
            if (!PawnRushPromotion.Gives(kind)) return;
            piece = kind;
            Refresh();
        }

        // A new round (or the same one again): unused, nobody counting.
        public void ResetPad()
        {
            used = false;
            standing = null;
            standFor = 0f;
            Refresh();
        }

        // The host's word on this pad (online, not the host).
        public void ApplyRemote(PieceKind kind, bool spent)
        {
            if (PawnRushPromotion.Gives(kind)) piece = kind;
            used = spent;
            standing = null;
            standFor = 0f;
            Refresh();
        }

        void FixedUpdate()
        {
            if (station == null || used || MiniGameBase.Remote) return;
            ICharacterDriver found = null, first = null;
            var pawns = station.Pawns();
            Vector3 middle = transform.localPosition;
            for (int i = 0; i < pawns.Count; i++)
            {
                var p = pawns[i];
                if (!p.Own || !(p.Driver is IPromotable promotable) || !PawnRushPromotion.CanPromote(promotable.Piece)) continue;
                if (Mathf.Abs(p.Local.x - middle.x) > Half || Mathf.Abs(p.Local.z - middle.z) > Half || p.Local.y < -0.5f || p.Local.y > MaxHeight) continue;
                if (first == null) first = p.Driver;
                if (p.Driver == standing) found = p.Driver;
            }
            if (found == null)
            {
                // Whoever was counting stepped off (or was pushed): start over with whoever is on it now.
                standing = first;
                standFor = 0f;
                return;
            }
            standFor += Time.fixedDeltaTime;
            if (standFor < PawnRushPromotion.HoldSeconds) return;
            ((IPromotable)found).Promote(piece);
            used = true;
            standing = null;
            standFor = 0f;
            Refresh();
            Debug.Log($"[PawnRush] 승격: 광장 {station.Plaza} {Teams.Name(station.Team)}팀 발판 → {ChessPieces.Name(piece)}");
            Promoted?.Invoke(this, found, piece);
        }

        // ------------------------------------------------------------------ the look

        void Build()
        {
            var kit = station.Kit;
            var shader = Shader.Find("Standard");
            slabOn = kit != null && kit.rankGold != null ? kit.rankGold : new Material(shader) { color = new Color(.95f, .78f, .25f) };
            slabOff = kit != null && kit.trimBlack != null ? kit.trimBlack : new Material(shader) { color = new Color(.25f, .25f, .27f) };
            fillMaterial = new Material(shader) { color = new Color(1f, .97f, .85f) };

            // A low slab on the floor (no collider: the floor carries the pawns), a frame in the team's colour.
            slab = Part(PrimitiveType.Cube, transform, new Vector3(0f, .03f, 0f), new Vector3(Half * 2f, .06f, Half * 2f), slabOn).GetComponent<Renderer>();
            var frame = kit != null ? kit.Team(station.Team) : null;
            if (frame != null)
                for (int i = 0; i < 4; i++)
                {
                    bool alongX = i < 2;
                    float side = (i % 2 == 0 ? -1f : 1f) * (Half + .06f);
                    Part(PrimitiveType.Cube, transform, alongX ? new Vector3(0f, .04f, side) : new Vector3(side, .04f, 0f),
                         alongX ? new Vector3(Half * 2f + .24f, .08f, .12f) : new Vector3(.12f, .08f, Half * 2f + .24f), frame);
                }
            fill = Part(PrimitiveType.Cube, transform, new Vector3(0f, .065f, -Half + .01f), new Vector3(Half * 2f - .2f, .012f, .02f), fillMaterial).transform;

            marker = new GameObject("Piece").transform;
            marker.SetParent(transform, false);
            marker.localPosition = new Vector3(0f, 1.4f, 0f);

            var text = new GameObject("Label");
            text.transform.SetParent(transform, false);
            text.transform.localPosition = new Vector3(0f, 2.55f, 0f);
            label = text.AddComponent<TextMesh>();
            label.anchor = TextAnchor.MiddleCenter;
            label.alignment = TextAlignment.Center;
            label.characterSize = .09f;
            label.fontSize = 48;
            var font = RuntimePanels.KoreanFont;
            if (font != null)
            {
                label.font = font;
                text.GetComponent<MeshRenderer>().sharedMaterial = font.material;
            }
            Refresh();
        }

        void Refresh()
        {
            if (slab == null) return;
            slab.sharedMaterial = used ? slabOff : slabOn;
            if (piece != shownPiece)
            {
                shownPiece = piece;
                for (int i = marker.childCount - 1; i >= 0; i--) Destroy(marker.GetChild(i).gameObject);
                BuildPiece(marker, piece, slabOn);
            }
            marker.gameObject.SetActive(!used);
            shownUsed = used;
            label.text = used ? "승격 발판 · 사용함" : $"{ChessPieces.Name(piece)} 승격\n폰이 {PawnRushPromotion.HoldSeconds:0}초 서 있기";
            label.color = used ? new Color(.7f, .7f, .7f) : new Color(1f, .9f, .45f);
        }

        void LateUpdate()
        {
            if (marker == null) return;
            if (used != shownUsed) Refresh();
            marker.localRotation = Quaternion.Euler(0f, Time.time * 60f, 0f);
            marker.localPosition = new Vector3(0f, 1.4f + Mathf.Sin(Time.time * 2f) * .1f, 0f);
            float k = Hold01;
            fill.gameObject.SetActive(!used && k > 0f);
            fill.localScale = new Vector3(Half * 2f - .2f, .012f, Mathf.Max(.02f, (Half * 2f - .2f) * k));
            fill.localPosition = new Vector3(0f, .065f, -Half + .1f + fill.localScale.z * .5f);
            var cam = Camera.main;
            if (cam != null) label.transform.rotation = Quaternion.LookRotation(label.transform.position - cam.transform.position, Vector3.up);
        }

        // A plain gold piece, a little under a metre tall, from primitives (no colliders).
        static void BuildPiece(Transform at, PieceKind kind, Material gold)
        {
            Part(PrimitiveType.Cylinder, at, new Vector3(0f, -.42f, 0f), new Vector3(.5f, .06f, .5f), gold);
            switch (kind)
            {
                case PieceKind.Rook:
                    Part(PrimitiveType.Cylinder, at, new Vector3(0f, -.1f, 0f), new Vector3(.36f, .3f, .36f), gold);
                    for (int i = 0; i < 4; i++)
                    {
                        float a = i * Mathf.PI * .5f;
                        Part(PrimitiveType.Cube, at, new Vector3(Mathf.Cos(a) * .15f, .27f, Mathf.Sin(a) * .15f), Vector3.one * .12f, gold);
                    }
                    break;
                case PieceKind.Bishop:
                    Part(PrimitiveType.Capsule, at, new Vector3(0f, -.02f, 0f), new Vector3(.3f, .34f, .3f), gold);
                    Part(PrimitiveType.Sphere, at, new Vector3(0f, .42f, 0f), Vector3.one * .1f, gold);
                    break;
                case PieceKind.Knight:
                    Part(PrimitiveType.Cylinder, at, new Vector3(0f, -.2f, 0f), new Vector3(.3f, .18f, .3f), gold);
                    Part(PrimitiveType.Cube, at, new Vector3(0f, .1f, -.02f), new Vector3(.2f, .42f, .26f), gold).transform.localRotation = Quaternion.Euler(-15f, 0f, 0f);
                    Part(PrimitiveType.Cube, at, new Vector3(0f, .24f, .14f), new Vector3(.17f, .15f, .3f), gold);
                    break;
                default:   // the queen: a body and a crown of five points
                    Part(PrimitiveType.Capsule, at, new Vector3(0f, -.05f, 0f), new Vector3(.32f, .34f, .32f), gold);
                    for (int i = 0; i < 5; i++)
                    {
                        float a = i * Mathf.PI * 2f / 5f;
                        Part(PrimitiveType.Sphere, at, new Vector3(Mathf.Cos(a) * .15f, .34f, Mathf.Sin(a) * .15f), Vector3.one * .08f, gold);
                    }
                    Part(PrimitiveType.Sphere, at, new Vector3(0f, .44f, 0f), Vector3.one * .1f, gold);
                    break;
            }
        }

        static GameObject Part(PrimitiveType type, Transform parent, Vector3 at, Vector3 size, Material material)
        {
            var go = GameObject.CreatePrimitive(type);
            var collider = go.GetComponent<Collider>();
            if (collider != null) DestroyImmediate(collider);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = at;
            go.transform.localScale = size;
            if (material != null) go.GetComponent<Renderer>().sharedMaterial = material;
            return go;
        }
    }
}
