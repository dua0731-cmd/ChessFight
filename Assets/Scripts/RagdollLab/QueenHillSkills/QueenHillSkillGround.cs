using System.Collections.Generic;
using UnityEngine;

namespace ChessFight.RagdollLab
{
    /// <summary>
    /// The Queen of the Hill skill test bed's ground (R137; 승규 님 10-14: "실제 인 게임 에서는 체스판 바닥이니까 모든 바닥을 체스판
    /// 바닥 같이 만들어주고"). Every block of the scene — the lab's floors, walls, slopes and steps, the hill, the stone wall —
    /// wears Skill Ground: its upward faces a chessboard of 1.5 m cream and walnut squares (the hill's and the course's
    /// colours) lined up across the whole scene (the hill's edges fall on the squares' edges), its other faces its own colour
    /// and the lab's grid; the lab's floors are a thick walnut board. So the bishop's strike can break into any of it
    /// (QueenHillSkillFx.Dig.cs), and the chips it throws are what it broke (<see cref="ColorAt"/>). Only in this scene, only
    /// while it plays: the lab's own materials stay as they are.
    /// </summary>
    public static class QueenHillGround
    {
        public static readonly Color Cream = new Color32(0xED, 0xE0, 0xC2, 0xFF), Walnut = new Color32(0x8C, 0x61, 0x3D, 0xFF);
        /// <summary>The hill's sides and the floors': walnut, darker.</summary>
        public static readonly Color Side = new Color(0.43f, 0.29f, 0.18f);
        /// <summary>Inside a board block where it breaks: raw wood, lighter than its walnut sides (so a break shows on them).</summary>
        public static readonly Color RawWood = new Color(0.74f, 0.57f, 0.38f);
        /// <summary>A square of the board (m): a real chessboard square of the skills' design (A).</summary>
        public const float Square = 1.5f;

        static Shader shader;
        static readonly Dictionary<Material, Material> dressed = new Dictionary<Material, Material>();
        static readonly List<Material> owned = new List<Material>();
        static MaterialPropertyBlock block;

        /// <summary>How many blocks wear it now (the probe reports it).</summary>
        public static int Worn { get; private set; }

        public static bool Ready
        {
            get
            {
                if (shader == null) shader = Shader.Find("ChessFight/Skill Ground");
                if (shader == null) shader = Resources.Load<Shader>("QueenHillSkillFx/SkillGround");
                return shader != null && shader.isSupported;
            }
        }

        /// <summary>A ground material: <paramref name="side"/> on its faces (times <paramref name="grid"/>), a chessboard on its
        /// upward faces if <paramref name="board"/>, <paramref name="body"/> inside it.</summary>
        public static Material Make(Color side, bool board, Color body, Texture grid = null, float gloss = 0.12f)
        {
            if (!Ready) return null;
            var m = new Material(shader) { hideFlags = HideFlags.HideAndDontSave, name = board ? "Skill Ground (board)" : "Skill Ground" };
            m.SetColor("_Color", side);
            if (grid != null) m.SetTexture("_MainTex", grid);
            m.SetFloat("_Board", board ? 1f : 0f);
            m.SetColor("_BoardA", Cream);
            m.SetColor("_BoardB", Walnut);
            m.SetFloat("_Square", Square);
            var c = QueenHillLayout.HillCenter;
            m.SetVector("_Origin", new Vector4(c.x, c.z, 0f, 0f));
            m.SetColor("_Body", body);
            m.SetFloat("_Gloss", gloss);
            owned.Add(m);
            return m;
        }

        /// <summary>Put <paramref name="m"/> on <paramref name="r"/>, with its block's box: its mesh's bounds, the world into its
        /// own space and its scale (from its transform: batching would change the shader's own).</summary>
        public static void Wear(Renderer r, Material m)
        {
            if (r == null || m == null) return;
            r.sharedMaterial = m;
            var mf = r.GetComponent<MeshFilter>();
            var b = mf != null && mf.sharedMesh != null ? mf.sharedMesh.bounds : new Bounds(Vector3.zero, Vector3.one);
            if (block == null) block = new MaterialPropertyBlock();
            block.Clear();
            block.SetVector("_BoxMin", b.min);
            block.SetVector("_BoxMax", b.max);
            block.SetMatrix("_WorldToBox", r.transform.worldToLocalMatrix);
            block.SetVector("_BoxScale", r.transform.lossyScale);
            r.SetPropertyBlock(block);
            Worn++;
        }

        /// <summary>The lab's own blocks (its Lab_* materials): the floors become the walnut board, the walls, slopes and steps
        /// keep their colour with the chessboard on top, the posts, the low bar and the beam keep theirs; all of them can
        /// break. What moves (the spinning bar) is left alone. Returns how many.</summary>
        public static int DressLab()
        {
            if (!Ready) return 0;
            int n = 0;
            foreach (var r in Object.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None))
            {
                var src = r.sharedMaterial;
                if (src == null || !src.name.StartsWith("Lab_") || src.name.StartsWith("Lab_Hazard")) continue;
                if (r.GetComponentInParent<Rigidbody>() != null || Moves(r.transform)) continue;
                if (!dressed.TryGetValue(src, out var m))
                {
                    bool floor = src.name.StartsWith("Lab_Floor");
                    bool board = floor || src.name.StartsWith("Lab_Wall") || src.name.StartsWith("Lab_Slope");
                    Color own = src.HasProperty("_Color") ? src.color : Color.grey;
                    // Inside: raw wood under the floors' board, broken stone (a little of its own colour) in the rest.
                    var stone = Color.Lerp(new Color(0.72f, 0.72f, 0.74f), own, 0.3f);
                    m = floor ? Make(Side, true, RawWood, null, 0.12f) : Make(own, board, stone, src.mainTexture, board ? 0.12f : 0.05f);
                    dressed[src] = m;
                }
                Wear(r, m);
                n++;
            }
            return n;
        }

        static bool Moves(Transform t)
        {
            for (; t != null; t = t.parent)
                if (t.name == "Spinning Bar") return true;
            return false;
        }

        /// <summary>The square's colour at <paramref name="p"/> (cream or walnut).</summary>
        public static Color SquareAt(Vector3 p)
        {
            var c = QueenHillLayout.HillCenter;
            int x = Mathf.FloorToInt((p.x - c.x) / Square), z = Mathf.FloorToInt((p.z - c.z) / Square);
            return ((x + z) & 1) == 0 ? Cream : Walnut;
        }

        /// <summary>What the block at <paramref name="at"/> is made of, its face there facing <paramref name="normal"/>: the
        /// face's colour (a square's on the chessboard), the other colour that breaks with it (the next square's), its inside.</summary>
        public static void ColorAt(Vector3 at, Vector3 normal, out Color face, out Color inside, out Color other)
        {
            face = Cream;
            other = Walnut;
            inside = RawWood;
            normal = normal.sqrMagnitude > 1e-6f ? normal.normalized : Vector3.up;
            Renderer r = null;
            float best = float.MaxValue;
            foreach (var h in Physics.RaycastAll(at + normal * 0.25f, -normal, 0.6f, ~0, QueryTriggerInteraction.Ignore))
            {
                if (h.collider.isTrigger || RagdollPawn.ColliderOwner.ContainsKey(h.collider) || h.distance >= best) continue;
                var rb = h.collider.attachedRigidbody;
                if (rb != null && !rb.isKinematic) continue;
                var hr = h.collider.GetComponent<Renderer>();
                if (hr == null) continue;
                best = h.distance;
                r = hr;
            }
            var m = r != null ? r.sharedMaterial : null;
            if (m == null) return;
            if (m.shader != shader)
            {
                face = other = m.HasProperty("_Color") ? m.color : face;
                inside = face * 0.6f;
                return;
            }
            inside = m.GetColor("_Body");
            if (m.GetFloat("_Board") > 0.5f && normal.y > 0.7f)
            {
                face = SquareAt(at);
                other = face == Cream ? Walnut : Cream;
            }
            else face = other = m.GetColor("_Color");
        }

        /// <summary>Done with the scene: its materials go.</summary>
        public static void Clear()
        {
            foreach (var m in owned)
                if (m != null) Object.Destroy(m);
            owned.Clear();
            dressed.Clear();
            Worn = 0;
        }
    }
}
