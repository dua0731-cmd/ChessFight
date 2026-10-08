using UnityEngine;

namespace ChessFight.RagdollLab
{
    /// <summary>
    /// The skills' telegraphs as plain world-space lines: the rook's charge line, the queen's rings, the
    /// knight's landing circle, the bishop's X. Previz colours: cyan for the white side, orange for the black
    /// side and for pieces with no side (the lab's dummies). Test-bed art only.
    /// </summary>
    public static class SkillMarks
    {
        public static readonly Color Cyan = new Color(0.25f, 0.85f, 1f);
        public static readonly Color Orange = new Color(1f, 0.55f, 0.12f);
        public static readonly Color Gold = new Color(1f, 0.82f, 0.3f);
        /// <summary>An enemy picked out by a skill (the knight's head stomp), apart from the side colours.</summary>
        public static readonly Color Target = new Color(1f, 0.4f, 0.22f);

        /// <summary>The faint dark edge under white marks: white alone is hard to see on a light floor.</summary>
        static readonly Color Ink = new Color(0.05f, 0.08f, 0.19f);

        static Material lineMaterial;
        static Mesh discMesh;
        static Texture2D reticleOuter, reticleInner;
        static MaterialPropertyBlock discBlock;

        public static Color TeamColor(int team) => team == 0 ? Cyan : Orange;

        static Material LineMaterial()
        {
            if (lineMaterial == null)
            {
                var shader = Shader.Find("Sprites/Default");
                if (shader == null) shader = Shader.Find("Legacy Shaders/Particles/Alpha Blended");
                if (shader == null) shader = Shader.Find("Standard");
                lineMaterial = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
            }
            return lineMaterial;
        }

        public static LineRenderer Line(Transform parent, string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var lr = go.AddComponent<LineRenderer>();
            lr.sharedMaterial = LineMaterial();
            lr.useWorldSpace = true;
            lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            lr.receiveShadows = false;
            lr.numCapVertices = 2;
            lr.positionCount = 0;
            lr.enabled = false;
            return lr;
        }

        public static void Hide(LineRenderer lr)
        {
            if (lr != null) lr.enabled = false;
        }

        public static void Hide(MeshRenderer disc)
        {
            if (disc != null) disc.enabled = false;
        }

        /// <summary>A see-through picture lying flat on the floor (the knight's landing mark, R82).</summary>
        public static MeshRenderer Disc(Transform parent, string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = DiscMesh();
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = LineMaterial();
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;
            mr.enabled = false;
            return mr;
        }

        /// <summary>
        /// The knight's landing mark (R82: "원형 모형으로 회전", in place of the side's blue circles): white and
        /// see-through on the floor, an outer ring of dashes with four ticks the size of the landing shock turning
        /// slowly one way, and an inner ring of three arcs round the knight's soft shadow and a dot turning faster
        /// the other way. <paramref name="inner"/> is the inner ring's radius (the caller draws it in as the knight
        /// comes down).
        /// </summary>
        public static void Reticle(MeshRenderer outerDisc, MeshRenderer innerDisc, Vector3 center, float outer, float inner, float time)
        {
            if (reticleOuter == null) reticleOuter = ReticleTexture(true);
            if (reticleInner == null) reticleInner = ReticleTexture(false);
            // The outer ring's line is at 92 % of its picture's radius, the inner one's at 80 %.
            PlaceDisc(outerDisc, reticleOuter, center + Vector3.up * 0.03f, outer * 2f / 0.92f, time * 40f, 0.85f);
            PlaceDisc(innerDisc, reticleInner, center + Vector3.up * 0.045f, inner * 2f / 0.8f * (1f + 0.03f * Mathf.Sin(time * 7f)), -time * 110f, 0.9f);
        }

        static void PlaceDisc(MeshRenderer disc, Texture2D texture, Vector3 at, float size, float spin, float alpha)
        {
            if (disc == null) return;
            var t = disc.transform;
            t.position = at;
            t.rotation = Quaternion.Euler(0f, spin, 0f);
            Vector3 parent = t.parent != null ? t.parent.lossyScale : Vector3.one;
            t.localScale = new Vector3(size / Mathf.Max(1e-4f, parent.x), 1f, size / Mathf.Max(1e-4f, parent.z));
            if (discBlock == null) discBlock = new MaterialPropertyBlock();
            disc.GetPropertyBlock(discBlock);
            discBlock.SetTexture("_MainTex", texture);
            discBlock.SetColor("_Color", new Color(1f, 1f, 1f, alpha));
            disc.SetPropertyBlock(discBlock);
            disc.enabled = true;
        }

        /// <summary>A 1 m square lying flat, facing up, white at every corner.</summary>
        static Mesh DiscMesh()
        {
            if (discMesh != null) return discMesh;
            discMesh = new Mesh { name = "Skill mark disc", hideFlags = HideFlags.HideAndDontSave };
            discMesh.vertices = new[] { new Vector3(-0.5f, 0f, -0.5f), new Vector3(0.5f, 0f, -0.5f), new Vector3(0.5f, 0f, 0.5f), new Vector3(-0.5f, 0f, 0.5f) };
            discMesh.uv = new[] { new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(0f, 1f) };
            discMesh.colors = new[] { Color.white, Color.white, Color.white, Color.white };
            discMesh.normals = new[] { Vector3.up, Vector3.up, Vector3.up, Vector3.up };
            discMesh.triangles = new[] { 0, 2, 1, 0, 3, 2 };
            discMesh.RecalculateBounds();
            return discMesh;
        }

        /// <summary>
        /// The landing mark's two pictures, drawn once. Outer: a ring of 16 dashes over a faint full ring, four short
        /// ticks inside it (they show it turning). Inner: three arcs, a faint ring inside them, a dot in the middle
        /// and the knight's soft shadow under it all. White lines over a faint dark edge, so they read on light and
        /// dark floors alike. No star shapes (R78).
        /// </summary>
        static Texture2D ReticleTexture(bool outer)
        {
            int size = outer ? 512 : 256;
            float aa = 2f / size;   // one pixel, in units of the picture's radius
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float u = (x + 0.5f) / size * 2f - 1f, v = (y + 0.5f) / size * 2f - 1f;
                    float r = Mathf.Sqrt(u * u + v * v), angle = Mathf.Atan2(v, u);
                    float line, shade;
                    if (outer)
                    {
                        const float R = 0.92f;
                        float dash = Mathf.Repeat(angle / (Mathf.PI * 2f) * 16f, 1f);
                        float dashes = 1f - Smooth(0.31f - aa * 3f, 0.31f + aa * 3f, Mathf.Abs(dash - 0.5f));
                        float ticks = Ticks(r, angle, 0.78f, 0.88f, 0.012f, aa);
                        line = Mathf.Max(Mathf.Max(Band(r, R, 0.008f, aa) * 0.35f, Band(r, R, 0.016f, aa) * dashes), ticks);
                        shade = 0.42f * Mathf.Max(Band(r, R, 0.04f, 0.03f), Ticks(r, angle, 0.75f, 0.91f, 0.035f, 0.02f));
                    }
                    else
                    {
                        const float R = 0.8f;
                        float arc = Mathf.Repeat(angle / (Mathf.PI * 2f) * 3f, 1f);
                        float arcs = 1f - Smooth(0.333f - aa * 2f, 0.333f + aa * 2f, Mathf.Abs(arc - 0.5f));
                        float dot = 1f - Smooth(0.06f - aa, 0.06f + aa, r);
                        line = Mathf.Max(Mathf.Max(Band(r, R, 0.035f, aa) * arcs, Band(r, 0.55f, 0.012f, aa) * 0.4f), dot * 0.9f);
                        float shadow = 0.24f * (1f - Smooth(0.15f, 0.75f, r));
                        shade = Mathf.Max(shadow, 0.42f * Mathf.Max(Band(r, R, 0.07f, 0.03f) * arcs, 1f - Smooth(0.08f, 0.13f, r)));
                    }
                    // White over the dark edge.
                    float alpha = line + shade * (1f - line);
                    Color c = alpha > 1e-4f ? Color.Lerp(Ink, Color.white, line / alpha) : Color.white;
                    pixels[y * size + x] = new Color32((byte)(c.r * 255f), (byte)(c.g * 255f), (byte)(c.b * 255f), (byte)(Mathf.Clamp01(alpha) * 255f));
                }
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, true)
            {
                name = outer ? "Knight landing mark (outer)" : "Knight landing mark (inner)",
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Trilinear,
                anisoLevel = 4,
                hideFlags = HideFlags.HideAndDontSave,
            };
            tex.SetPixels32(pixels);
            tex.Apply(true, true);
            return tex;
        }

        static float Smooth(float from, float to, float x) => Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(from, to, x));

        /// <summary>1 on a ring of radius <paramref name="at"/> and half width <paramref name="half"/>, soft over
        /// <paramref name="soft"/>.</summary>
        static float Band(float r, float at, float half, float soft) => 1f - Smooth(half - soft, half + soft, Mathf.Abs(r - at));

        /// <summary>Four short bars pointing at the middle (at 0, 90, 180 and 270 degrees) from radius
        /// <paramref name="from"/> to <paramref name="to"/>, <paramref name="half"/> wide either side.</summary>
        static float Ticks(float r, float angle, float from, float to, float half, float soft)
        {
            float quarter = Mathf.PI * 0.5f;
            float off = Mathf.Abs(Mathf.Repeat(angle + quarter * 0.5f, quarter) - quarter * 0.5f) * r;
            float across = 1f - Smooth(half - soft, half + soft, off);
            float along = Smooth(from - soft, from + soft, r) * (1f - Smooth(to - soft, to + soft, r));
            return across * along;
        }

        public static void Circle(LineRenderer lr, Vector3 center, float radius, Color color, float width, int segments = 48)
        {
            Flat(lr, true);
            if (lr == null) return;
            lr.enabled = true;
            lr.loop = true;
            lr.positionCount = segments;
            for (int i = 0; i < segments; i++)
            {
                float a = i * Mathf.PI * 2f / segments;
                lr.SetPosition(i, center + new Vector3(Mathf.Cos(a) * radius, 0.04f, Mathf.Sin(a) * radius));
            }
            Paint(lr, color, width);
        }

        public static void Segment(LineRenderer lr, Vector3 a, Vector3 b, Color color, float width, bool flat = true)
        {
            if (lr == null) return;
            Flat(lr, flat);
            lr.enabled = true;
            lr.loop = false;
            lr.positionCount = 2;
            lr.SetPosition(0, a);
            lr.SetPosition(1, b);
            Paint(lr, color, width);
        }

        /// <summary>A line through several points (the bishop's wire pulled out of straight, R81).</summary>
        public static void Polyline(LineRenderer lr, Vector3[] points, Color color, float width, bool flat = false)
        {
            if (lr == null) return;
            Flat(lr, flat);
            lr.enabled = true;
            lr.loop = false;
            lr.positionCount = points.Length;
            lr.SetPositions(points);
            Paint(lr, color, width);
        }

        /// <summary>The "still aiming" look of the rook's line (the previz draws it dotted): thin and see-through.
        /// The locked line is a solid Segment.</summary>
        public static void Faint(LineRenderer lr, Vector3 a, Vector3 b, Color color, float width)
        {
            color.a *= 0.45f;
            Segment(lr, a, b, color, width * 0.5f);
        }

        /// <summary>Ground marks lie on the floor (the ribbon faces up); ropes and threads face the camera.</summary>
        static void Flat(LineRenderer lr, bool flat)
        {
            if (lr == null) return;
            lr.alignment = flat ? LineAlignment.TransformZ : LineAlignment.View;
            if (flat) lr.transform.rotation = Quaternion.LookRotation(Vector3.up, Vector3.forward);
        }

        static void Paint(LineRenderer lr, Color color, float width)
        {
            lr.startColor = lr.endColor = color;
            lr.widthMultiplier = width;
            lr.widthCurve = AnimationCurve.Constant(0f, 1f, 1f);
        }
    }
}
