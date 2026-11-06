using UnityEngine;

namespace ChessFight.PawnRush
{
    // R461 (승규 11-05 "표시를 해줘 데칼이라던가 이런걸로"): the north return route's speed lane painted on the floor as a
    // projected decal (PR_LibrarySpeedLane.shader, the grand stair hall's decal projection): a box over each leg of the
    // route, lying along it (the stair's tilted with the flight), paints only what faces up inside it - the landings,
    // the gallery and every tread's top - with a teal lane: edge lines, a faint fill and chevrons pointing the way, a
    // light running along them. R459's floating chevron quads are gone. Built on the first frame from the route (the
    // library pivot's space, heights the floors'); each box carries its leg's size and how far along the route it
    // starts, so the chevrons and the light run on unbroken from leg to leg.
    public sealed class LibraryStairBoostFx : MonoBehaviour
    {
        const string MaterialName = "PR_LibrarySpeedLane", ShaderName = "ChessFight/PawnRush/Speed Lane Decal";
        const float Width = 1.7f;          // the lane, m
        const float Depth = 1.2f;          // the box's height through the floor (the treads lie up to 0.2 m under the
                                           // stair's nosing line)
        static readonly int SizeId = Shader.PropertyToID("_Size"), StartId = Shader.PropertyToID("_Start");

        [SerializeField] Transform frame;
        [SerializeField] Vector3[] route;

        public void Configure(Transform pivot, Vector3[] path)
        {
            frame = pivot;
            route = path;
        }

        void Start()
        {
            if (frame == null || route == null || route.Length < 2) return;
            var material = Resources.Load<Material>(MaterialName);
            if (material == null)
            {
                var shader = Shader.Find(ShaderName);
                if (shader == null) { Debug.LogWarning("[작전 회의실] 스피드 레인 데칼 셰이더가 없습니다: " + ShaderName, this); return; }
                material = new Material(shader) { name = MaterialName };
            }
            var cube = Resources.GetBuiltinResource<Mesh>("Cube.fbx");
            var block = new MaterialPropertyBlock();
            float along = 0f;
            // R463 (승규 11-05, a shot of the lane turning off the gallery onto the old upper landing: "여기 데칼 지워주고"):
            // the last leg - from the gallery's end across the old landing to the start floor's door - is not painted;
            // the lane ends where the gallery does (its floor still speeds a pawn up).
            for (int i = 0; i + 2 < route.Length; i++)
            {
                Vector3 a = frame.TransformPoint(route[i]), b = frame.TransformPoint(route[i + 1]);
                var leg = b - a;
                float length = leg.magnitude;
                if (length < .05f) continue;
                var dir = leg / length;
                var flatRight = Vector3.Cross(Vector3.up, dir).normalized;
                var up = Vector3.Cross(dir, flatRight);              // the leg's own up (tilted with a flight)
                var box = new GameObject("Speed lane decal " + i);
                box.transform.SetParent(transform, true);
                box.transform.SetPositionAndRotation((a + b) * .5f + up * .1f, Quaternion.LookRotation(dir, up));
                box.transform.localScale = new Vector3(Width + .4f, Depth, length + .3f);
                box.AddComponent<MeshFilter>().sharedMesh = cube;
                var r = box.AddComponent<MeshRenderer>();
                r.sharedMaterial = material;
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                r.receiveShadows = false;
                block.Clear();
                block.SetVector(SizeId, new Vector4(Width + .4f, length + .3f, Width, 0f));
                block.SetFloat(StartId, along - .15f);
                r.SetPropertyBlock(block);
                along += length;
            }
        }
    }
}
