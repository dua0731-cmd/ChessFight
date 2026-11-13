using UnityEngine;

namespace ChessFight.PawnRush
{
    // R440: tells the garden water shader (PR_GardenClearWater) where the tiered falls land - each bay's landing line
    // and the height of the pool it lands in (GardenWater_Landings.json, written from water_tiers.py) - so the water
    // churns there and sends rings out at the dispersion speed (the shader's _GardenLanding arrays, which
    // GardenWaterInteraction used to fill for the old Blender garden). The lines are kept in the section's frame (this
    // transform) and turned into world space every frame, so the zone may be moved after it is built (PITFALLS 36).
    // Read only: it moves nothing.
    [ExecuteAlways]
    public sealed class GardenFallLines : MonoBehaviour
    {
        const int Max = 24;   // the shader's array size (Unity keeps a global array's first size for the session)
        static readonly int LandingId = Shader.PropertyToID("_GardenLanding");
        static readonly int LandingInfoId = Shader.PropertyToID("_GardenLandingInfo");
        static readonly int LandingCountId = Shader.PropertyToID("_GardenLandingCount");

        [SerializeField] Vector3[] from = new Vector3[0], to = new Vector3[0];
        [SerializeField] float[] strength = new float[0];
        readonly Vector4[] data = new Vector4[Max], info = new Vector4[Max];

        // For the builder: each line's ends on its pool's surface (section frame) and how hard the water churns there.
        public void Configure(Vector3[] lineFrom, Vector3[] lineTo, float[] lineStrength)
        {
            from = lineFrom;
            to = lineTo;
            strength = lineStrength;
        }

        void LateUpdate()
        {
            int n = Mathf.Min(Max, Mathf.Min(from.Length, Mathf.Min(to.Length, strength.Length)));
            for (int k = 0; k < Max; k++)
            {
                if (k >= n)
                {
                    data[k] = Vector4.zero;
                    info[k] = Vector4.zero;
                    continue;
                }
                var a = transform.TransformPoint(from[k]);
                var b = transform.TransformPoint(to[k]);
                data[k] = new Vector4(a.x, a.z, b.x, b.z);
                info[k] = new Vector4(a.y, strength[k], 0f, 0f);
            }
            Shader.SetGlobalVectorArray(LandingId, data);
            Shader.SetGlobalVectorArray(LandingInfoId, info);
            Shader.SetGlobalFloat(LandingCountId, n);
        }

        void OnDisable() => Shader.SetGlobalFloat(LandingCountId, 0f);
    }
}
