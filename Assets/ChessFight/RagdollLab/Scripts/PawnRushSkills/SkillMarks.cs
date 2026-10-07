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

        static Material lineMaterial;

        public static Color TeamColor(int team) => team == 0 ? Cyan : Orange;

        public static LineRenderer Line(Transform parent, string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var lr = go.AddComponent<LineRenderer>();
            if (lineMaterial == null)
            {
                var shader = Shader.Find("Sprites/Default");
                if (shader == null) shader = Shader.Find("Legacy Shaders/Particles/Alpha Blended");
                if (shader == null) shader = Shader.Find("Standard");
                lineMaterial = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
            }
            lr.sharedMaterial = lineMaterial;
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
