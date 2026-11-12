using UnityEditor;
using UnityEngine;

namespace ChessFight.RagdollLab.Editor
{
    /// <summary>
    /// The pawn's and the pieces' materials on ChessFight/Character (JY 11-12: "우리 쉐이더 넣을려고 만들고 있었는데 그건
    /// 왜 안넣어? 팀 구분은 체스처럼 흑/백으로 하는건데"), one set per team:
    /// - white: Art/Pieces/&lt;Piece&gt;/&lt;Piece&gt;.mat and Materials/Pawn_P1 (cream body, black glossy eyes, white glints);
    /// - black: Resources/PieceModels/Black/&lt;Piece&gt;.mat, Black/Pawn.mat and Materials/Pawn_P2 (charcoal body, ivory
    ///   eyes drawn nearly flat, no glints), which RagdollPawn.PieceMaterial, SwordFightGame and LastSceneFigure wear.
    /// The pawn, rook, knight and bishop are on stripe atlases (RigWork/scripts/pieces/game_export.py); the king and
    /// queen keep their painted textures. RagdollLabBuilder makes the pawn's three through <see cref="Apply"/> too.
    /// </summary>
    public static class CharacterLook
    {
        public const string ShaderName = "ChessFight/Character";
        public const string PawnWhiteAlbedo = "Assets/Art/Pawn/PawnAlbedo.jpg";
        public const string PawnBlackAlbedo = "Assets/Art/Pawn/PawnAlbedo_Black.png";
        const string BlackDir = "Assets/Resources/PieceModels/Black";
        static readonly string[] Pieces = { "King", "Queen", "Rook", "Knight", "Bishop" };

        [MenuItem("ChessFight/Ragdoll Lab/Apply Character Look")]
        public static void ApplyAll()
        {
            foreach (string piece in Pieces)
            {
                bool stripes = piece != "King" && piece != "Queen";
                Apply(AssetDatabase.LoadAssetAtPath<Material>($"Assets/Art/Pieces/{piece}/{piece}.mat"), null, false, stripes, Color.white);
                Apply(AssetDatabase.LoadAssetAtPath<Material>($"{BlackDir}/{piece}.mat"), null, true, stripes, Color.white);
            }
            var white = AssetDatabase.LoadAssetAtPath<Texture2D>(PawnWhiteAlbedo);
            var black = AssetDatabase.LoadAssetAtPath<Texture2D>(PawnBlackAlbedo);
            Apply(MaterialAt($"{BlackDir}/Pawn.mat"), black, true, true, Color.white);
            Apply(AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Pawn_P1.mat"), white, false, true, Color.white);
            Apply(AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Pawn_P2.mat"), black, true, true, Color.white);
            Apply(AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Pawn_Dummy.mat"), white, false, true, new Color(0.6f, 0.6f, 0.62f));
            AssetDatabase.SaveAssets();
            Debug.Log("[CharacterLook] the pawn and the pieces are on " + ShaderName);
        }

        /// <summary>The material at <paramref name="path"/>, made on the character shader if there is none.</summary>
        public static Material MaterialAt(string path)
        {
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat != null) return mat;
            var shader = Shader.Find(ShaderName);
            if (shader == null) return null;
            mat = new Material(shader);
            AssetDatabase.CreateAsset(mat, path);
            return mat;
        }

        /// <summary>
        /// <paramref name="mat"/> on the character shader with one team's face settings. Its picture stays unless
        /// <paramref name="texture"/> is given (URP Lit and this shader both keep it in _BaseMap).
        /// </summary>
        public static void Apply(Material mat, Texture texture, bool blackTeam, bool stripes, Color tint)
        {
            if (mat == null) return;
            var shader = Shader.Find(ShaderName);
            if (shader == null)
            {
                Debug.LogError("[CharacterLook] " + ShaderName + " is missing (Assets/Shaders/Characters/CFCharacter.shader)");
                return;
            }
            Texture picture = texture != null ? texture : mat.HasProperty("_BaseMap") ? mat.GetTexture("_BaseMap") : mat.mainTexture;
            mat.shader = shader;
            mat.shaderKeywords = new string[0];   // URP Lit's keywords mean nothing here
            mat.SetTexture("_BaseMap", picture);
            mat.SetColor("_BaseColor", tint);
            mat.SetFloat("_Stripes", stripes ? 1f : 0f);
            // White: black eyes that shine, a glint and blush half self-lit. Black: the design's ivory eyes and their
            // hidden glints almost flat, as RigWork/README.md section 2 draws them (ivory x0.2 + emission 0.9).
            mat.SetFloat("_EyeGloss", blackTeam ? 0.15f : 0.6f);
            mat.SetFloat("_EyeEmit", blackTeam ? 0.9f : 0f);
            mat.SetFloat("_GlintEmit", blackTeam ? 0.9f : 0.5f);
            mat.SetFloat("_BlushEmit", blackTeam ? 0.3f : 0.15f);
            EditorUtility.SetDirty(mat);
        }
    }
}
