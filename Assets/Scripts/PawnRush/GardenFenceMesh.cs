using System.Collections.Generic;
using UnityEngine;

namespace ChessFight.PawnRush
{
    // Rebuild the transient mesh from serialized dimensions when reopening a baked section.
    [ExecuteAlways]
    public sealed class GardenFenceMesh : MonoBehaviour
    {
        [SerializeField] float length, height;
        Mesh mesh;

        public void Configure(float width, float tall)
        {
            length = width;
            height = tall;
            Build();
        }

        void OnEnable() => Build();

        void Build()
        {
            if (mesh != null || length <= 0f || height <= 0f) return;
            var cube = Resources.GetBuiltinResource<Mesh>("Cube.fbx");
            var bars = new List<CombineInstance>();
            void Bar(Vector3 center, Vector3 dimensions) => bars.Add(new CombineInstance
            {
                mesh = cube, transform = Matrix4x4.TRS(center, Quaternion.identity, dimensions)
            });
            int spaces = Mathf.CeilToInt(length / .50f);
            for (int i = 0; i <= spaces; i++)
            {
                float thick = i % 8 == 0 ? .20f : .075f;
                Bar(new Vector3(0, height * .5f, length * ((float)i / spaces - .5f)), new Vector3(thick, height, thick));
            }
            Bar(Vector3.up * .24f, new Vector3(.18f, .24f, length));
            Bar(Vector3.up * (height - .28f), new Vector3(.16f, .14f, length));
            mesh = new Mesh { name = name, hideFlags = HideFlags.DontSave };
            mesh.CombineMeshes(bars.ToArray());
            GetComponent<MeshFilter>().sharedMesh = mesh;
        }

        void OnDestroy()
        {
            if (mesh == null) return;
            if (Application.isPlaying) Destroy(mesh);
            else DestroyImmediate(mesh);
        }
    }
}
