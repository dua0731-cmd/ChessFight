using System.Collections.Generic;
using UnityEngine;

namespace ChessFight.PawnRush
{
    // R406 (승규 11-04, a shot of the dome with whole faces gilded on some ribs and none on others: "어디는 칠해져 있고
    // 어디는 비였어"): the hall's shaders that need a place in the world - the dome's centre (PR_PalaceTrim _DomeCentre,
    // R405) and the spiral's threshold (PR_SpiralChecker _FootPoint / _FootAlong, R403) - were given it in world space
    // while the zone was still at the origin; SeoungTestCourse then moves the zone to its place in the row, so the
    // shaders measured from a point 100 m away. This keeps each such value in the hall model's own space and writes it
    // to its material in world space whenever the model has moved (also in the Editor, so the Scene view is right).
    [ExecuteAlways]
    public sealed class HallAnchoredMaterials : MonoBehaviour
    {
        [System.Serializable]
        struct Entry
        {
            public Material material;
            public string property;
            public Vector3 local;      // a point, or a direction when `direction`, in this transform's space
            public bool direction;
            public float w;
        }

        [SerializeField] List<Entry> entries = new List<Entry>();
        Matrix4x4 written;
        bool done;

        /// <summary>Anchor `world` (a point, or a direction) to this transform as it stands now and write it.</summary>
        public void Add(Material material, string property, Vector3 world, bool direction, float w)
        {
            if (material == null) return;
            entries.Add(new Entry
            {
                material = material, property = property, direction = direction, w = w,
                local = direction ? transform.InverseTransformDirection(world) : transform.InverseTransformPoint(world),
            });
            done = false;
            Apply();
        }

        void OnEnable() { done = false; Apply(); }

        void LateUpdate()
        {
            if (!done || transform.localToWorldMatrix != written) Apply();
        }

        void Apply()
        {
            foreach (var e in entries)
            {
                if (e.material == null) continue;
                var v = e.direction ? transform.TransformDirection(e.local).normalized : transform.TransformPoint(e.local);
                e.material.SetVector(e.property, new Vector4(v.x, v.y, v.z, e.w));
            }
            written = transform.localToWorldMatrix;
            done = true;
        }
    }
}
