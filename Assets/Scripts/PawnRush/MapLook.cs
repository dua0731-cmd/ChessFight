using System.Collections.Generic;
using ChessFight.Game;
using ChessFight.Network;
using UnityEngine;

namespace ChessFight.PawnRush
{
    // The map kit's look for things the game builds at run time (승규 10-22): round parts as true circles
    // (Unity's primitive cylinder has 20 flat sides), real water, surface textures for the mini-games' sand,
    // wood and stone, and real turned chess pieces. Everything comes from Resources
    // (Assets/Art/PawnRushMap/Resources, set up by ChessFight > Pawn Rush > Art Dress); when it is missing the
    // game keeps its plain look.
    public static class MapLook
    {
        static Mesh cylinder;
        static Material water;
        static bool loaded;
        static readonly Dictionary<string, Texture2D> textures = new Dictionary<string, Texture2D>();

        static void Load()
        {
            if (loaded) return;
            loaded = true;
            cylinder = Resources.Load<Mesh>("PR_CylinderSmooth");
            water = Resources.Load<Material>("PR_Water");
        }

        public static Material Water { get { Load(); return water; } }

        // A primitive cylinder's picture swapped for the 96-sided one (same size; the collider is untouched).
        public static void Smooth(GameObject go)
        {
            Load();
            if (cylinder == null || go == null) return;
            if (go.TryGetComponent<MeshFilter>(out var f) && f.sharedMesh != null && f.sharedMesh.name == "Cylinder")
                f.sharedMesh = cylinder;
        }

        public static Texture2D Texture(string name)
        {
            if (textures.TryGetValue(name, out var t)) return t;
            t = Resources.Load<Texture2D>(name);
            textures[name] = t;
            return t;
        }

        // A surface set (albedo + normal) on a lit material, tiled `tiling` times per face.
        public static void Surface(Material m, string set, float tiling, float bump)
        {
            var albedo = Texture(set + "_Albedo");
            var normal = Texture(set + "_Normal");
            if (m == null || albedo == null) return;
            m.mainTexture = albedo;
            m.mainTextureScale = new Vector2(tiling, tiling);
            if (normal != null && m.HasProperty("_BumpMap"))
            {
                m.SetTexture("_BumpMap", normal);
                m.SetTextureScale("_BumpMap", new Vector2(tiling, tiling));
                if (m.HasProperty("_BumpScale")) m.SetFloat("_BumpScale", bump);
                m.EnableKeyword("_NORMALMAP");
            }
        }

        // ---- real chess pieces, Staunton pattern (ArtSource/PawnRush/build_pieces.py; 승규 10-22 "실제 기물같이")
        // Height 1, base on 0, centred, cylindrical UVs (the wood grain runs up the piece). The knight's head looks
        // along -x as imported; KnightYaw turns it to +z, where the old primitive knight looked.

        public const float KnightYaw = 90f;
        static readonly Dictionary<PieceKind, Mesh> pieces = new Dictionary<PieceKind, Mesh>();
        static Mesh ring;
        static Material gold;

        public static Mesh PieceMesh(PieceKind kind)
        {
            if (pieces.TryGetValue(kind, out var mesh)) return mesh;
            mesh = FirstMesh("Pieces/PR_Piece_" + kind);
            pieces[kind] = mesh;
            return mesh;
        }

        // A torus of radius 1 (tube 0.08) lying flat: the team band round a piece's foot.
        public static Mesh RingMesh => ring != null ? ring : ring = FirstMesh("Pieces/PR_PieceRing");

        // The base's radius at height 1.
        public static float FootRadius(PieceKind kind)
        {
            switch (kind)
            {
                case PieceKind.Pawn: return .29f;
                case PieceKind.Rook: return .3f;
                case PieceKind.Bishop: return .232f;
                case PieceKind.Queen: return .23f;
                case PieceKind.King: return .223f;
                default: return .261f;
            }
        }

        // Polished gold for display pieces (the promotion pad's turning piece).
        public static Material Gold
        {
            get
            {
                if (gold != null) return gold;
                gold = LitMaterials.NewLit(new Color(.93f, .7f, .26f), .8f, 1f);
                gold.name = "M_PieceGold";
                LitMaterials.SetEmission(gold, new Color(.93f, .7f, .26f) * .12f);
                return gold;
            }
        }

        // A turned piece `height` tall under `parent` (a MeshRenderer child named `name`); null when the meshes
        // are missing, so the caller keeps its primitive piece.
        public static GameObject PieceBody(Transform parent, string name, PieceKind kind, float height, Material material)
        {
            var mesh = PieceMesh(kind);
            if (mesh == null) return null;
            var go = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
            go.transform.SetParent(parent, false);
            go.transform.localScale = Vector3.one * height;
            if (kind == PieceKind.Knight) go.transform.localRotation = Quaternion.Euler(0f, KnightYaw, 0f);
            go.GetComponent<MeshFilter>().sharedMesh = mesh;
            go.GetComponent<MeshRenderer>().sharedMaterial = material;
            return go;
        }

        static Mesh FirstMesh(string path)
        {
            var go = Resources.Load<GameObject>(path);
            var f = go != null ? go.GetComponentInChildren<MeshFilter>() : null;
            return f != null ? f.sharedMesh : null;
        }
    }
}
