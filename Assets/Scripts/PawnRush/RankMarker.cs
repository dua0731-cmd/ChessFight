using UnityEngine;

namespace ChessFight.PawnRush
{
    // The rank sign on a wall (design doc §3): a gold line along the wall and the rank
    // as a big seven-segment numeral, so the height reads as "how many ranks to go".
    // Built from gold bars rather than text: no font to lose in a prefab, and it is
    // hidden by walls like any other mesh. Decoration only, no colliders.
    public sealed class RankMarker : MonoBehaviour
    {
        [SerializeField, Range(1, 8)] int rank = 2;
        public int Rank => rank;

        //   -a-
        //  f   b
        //   -g-
        //  e   c
        //   -d-
        static readonly string[] Segments = { "abcdef", "bc", "abged", "abgcd", "fgbc", "afgcd", "afgedc", "abc", "abcdefg" };

        // Builds the sign facing local -z (the wall's face looks back down the course). `width` is
        // the gold line's length; the numeral is `height` tall, centred above the line.
        public static RankMarker Build(Transform parent, string name, Vector3 at, int rank, float width, float height, Material gold)
        {
            var root = new GameObject(name);
            root.transform.SetParent(parent, false);
            root.transform.localPosition = at;
            var marker = root.AddComponent<RankMarker>();
            marker.rank = rank;
            Bar(root.transform, new Vector3(0f, 0f, 0f), new Vector3(width, .18f, .06f), gold);
            float h = height, w = height * .55f, t = height * .12f;
            float bottom = .35f;
            foreach (char s in Segments[Mathf.Clamp(rank, 0, 8)])
            {
                Vector3 c; Vector3 size;
                switch (s)
                {
                    case 'a': c = new Vector3(0f, bottom + h, 0f); size = new Vector3(w, t, .06f); break;
                    case 'g': c = new Vector3(0f, bottom + h * .5f, 0f); size = new Vector3(w, t, .06f); break;
                    case 'd': c = new Vector3(0f, bottom, 0f); size = new Vector3(w, t, .06f); break;
                    case 'b': c = new Vector3(w * .5f, bottom + h * .75f, 0f); size = new Vector3(t, h * .5f, .06f); break;
                    case 'c': c = new Vector3(w * .5f, bottom + h * .25f, 0f); size = new Vector3(t, h * .5f, .06f); break;
                    case 'f': c = new Vector3(-w * .5f, bottom + h * .75f, 0f); size = new Vector3(t, h * .5f, .06f); break;
                    default: c = new Vector3(-w * .5f, bottom + h * .25f, 0f); size = new Vector3(t, h * .5f, .06f); break;
                }
                Bar(root.transform, c, size, gold);
            }
            return marker;
        }

        static void Bar(Transform parent, Vector3 center, Vector3 size, Material gold)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = "Gold";
            Object.DestroyImmediate(go.GetComponent<Collider>());
            go.transform.SetParent(parent, false);
            go.transform.localPosition = center;
            go.transform.localScale = size;
            if (gold != null) go.GetComponent<MeshRenderer>().sharedMaterial = gold;
        }
    }
}
