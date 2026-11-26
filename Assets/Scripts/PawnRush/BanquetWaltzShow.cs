using System;
using System.Collections.Generic;
using ChessFight.Gameplay;
using UnityEngine;

namespace ChessFight.PawnRush
{
    // The waltz's show on the dressed hall (대연회장 연출 R845). The ten couples keep their course, their turn and their
    // colliders (WaltzCouple; the queen's gown top at 7.4 is where a pawn stands): only pictures move, and little.
    //   - The queen does not rise nor lean (her shoulders are the floor): her gown's lower half flares out with the
    //     turn, a little more on each bar's first beat, and its hem trails the spin.
    //   - Her partner rises and falls with the waltz's three beats (up through 2 and 3, down on 1) and leans a degree in
    //     toward the couple's middle.
    // A bar is 1.6 s - three to a queen's 4.8 s stop - on the shared obstacle clock (HANDOFF rule 7): nothing is sent.
    [DefaultExecutionOrder(205)]
    public sealed class BanquetWaltzShow : MonoBehaviour
    {
        public const float Bar = 1.6f;
        const float Flare = .055f, FlareBeat = .035f, Trail = 7f, Rise = .035f, Lean = 1.2f;

        sealed class Couple
        {
            public Transform root;
            public int index;
            public Transform[] rows = Array.Empty<Transform>();      // the gown's bones, hem (0) to waist
            public float[] share = Array.Empty<float>();             // how far each row flares and trails (1 at the hem)
            public float low, waist;
            public Transform[] partner = Array.Empty<Transform>();
            public Vector3[] partnerAt = Array.Empty<Vector3>();
            public Quaternion[] partnerTurn = Array.Empty<Quaternion>();
            public Vector3 partnerFoot, queenAxis;
        }

        readonly List<Couple> couples = new List<Couple>();
        readonly List<UnityEngine.Object> owned = new List<UnityEngine.Object>();

        public int Couples => couples.Count;
        public int Gowns { get; private set; }
        public int Partners { get; private set; }
        // As last drawn: how far the hems stand out (share of their radius) and how high a partner has risen (m).
        public float Flared { get; private set; }
        public float Risen { get; private set; }

        public void Configure(GrandBanquetHallDressing dressing)
        {
            for (int i = 0; i < BanquetWaltz.Couples; i++)
                foreach (var root in dressing.FindAll("BH couple " + (i + 1)))
                {
                    var model = GrandBanquetHallDressing.ModelOn(root, "bh_couple");
                    if (model == null) continue;
                    var c = new Couple { root = root, index = i, queenAxis = new Vector3(BanquetWaltz.Pair, 0f, 0f) };
                    foreach (var part in BanquetShowKit.Parts(model))
                    {
                        string n = part.Key;
                        if (n.IndexOf("partner", StringComparison.OrdinalIgnoreCase) >= 0 && n.IndexOf("stand", StringComparison.OrdinalIgnoreCase) < 0)
                            Partner(c, part.Value, root);
                        else if (n.IndexOf("queen", StringComparison.OrdinalIgnoreCase) >= 0 && n.IndexOf("stand", StringComparison.OrdinalIgnoreCase) < 0)
                            foreach (var filter in part.Value) Skirt(c, filter);
                    }
                    couples.Add(c);
                }
            Debug.Log("[대연회장 왈츠 연출] 쌍 " + couples.Count + " · 치마 " + Gowns + " · 짝 " + Partners);
        }

        // The gown's bones: Rows of them on the queen's axis from the hem up to the waist (the lower 55 % of the way to
        // the shoulder plate at 7.4), on the couple's root; everything over the waist follows the waist's row, which
        // never moves. Every LOD of the queen and her underskirt is skinned to them.
        const int Rows = 5;

        void Skirt(Couple c, MeshFilter filter)
        {
            if (filter.sharedMesh == null || !filter.sharedMesh.isReadable) return;
            if (c.rows.Length == 0)
            {
                var toRoot = c.root.worldToLocalMatrix * filter.transform.localToWorldMatrix;
                float low = float.MaxValue;
                foreach (var v in filter.sharedMesh.vertices)
                {
                    var p = toRoot.MultiplyPoint3x4(v);
                    if (new Vector2(p.x - c.queenAxis.x, p.z).magnitude < 2.2f) low = Mathf.Min(low, p.y);
                }
                if (low == float.MaxValue) return;
                c.low = low;
                c.waist = low + .55f * (GrandBanquetHallBuilder.StageY - low);
                c.rows = new Transform[Rows];
                c.share = new float[Rows];
                for (int r = 0; r < Rows; r++)
                {
                    float k = (float)r / (Rows - 1);
                    c.rows[r] = BanquetShowKit.Bone("BH gown row " + r, c.root, new Vector3(c.queenAxis.x, Mathf.Lerp(c.low, c.waist, k), 0f));
                    c.share[r] = (1f - k) * (1f - k);
                }
            }
            void Weigh(Vector3 v, out int a, out int b, out float wa)
            {
                float q = Mathf.Clamp01((v.y - c.low) / Mathf.Max(.1f, c.waist - c.low)) * (Rows - 1);
                a = Mathf.Min(Rows - 2, Mathf.FloorToInt(q));
                b = a + 1;
                wa = 1f - (q - a);
            }
            if (BanquetShowKit.Skin(filter, c.root, c.rows, Weigh, .4f, owned) != null) Gowns++;
        }
        void Partner(Couple c, List<MeshFilter> lods, Transform root)
        {
            var turnBack = Quaternion.Inverse(root.rotation);
            c.partner = new Transform[lods.Count];
            c.partnerAt = new Vector3[lods.Count];
            c.partnerTurn = new Quaternion[lods.Count];
            var bounds = new Bounds();
            bool any = false;
            for (int i = 0; i < lods.Count; i++)
            {
                var t = lods[i].transform;
                c.partner[i] = t;
                c.partnerAt[i] = root.InverseTransformPoint(t.position);
                c.partnerTurn[i] = turnBack * t.rotation;
                if (t.TryGetComponent<Renderer>(out var r)) { if (!any) { bounds = r.bounds; any = true; } else bounds.Encapsulate(r.bounds); }
            }
            var middle = root.InverseTransformPoint(bounds.center);
            c.partnerFoot = new Vector3(-BanquetWaltz.Pair, root.InverseTransformPoint(bounds.min).y, 0f);
            if (Mathf.Abs(middle.x + BanquetWaltz.Pair) > 1.2f) c.partnerFoot.x = middle.x;
            Partners++;
        }

        void OnDestroy()
        {
            foreach (var o in owned)
                if (o != null) Destroy(o);
        }

        void LateUpdate()
        {
            double now = ObstacleClock.Now;
            float bar = (float)(now % (Bar * 1000.0)) / Bar, phase = bar - Mathf.Floor(bar);
            // up through beats 2 and 3, down on 1; the gown kicks out on 1
            float rise = Rise * Mathf.Pow(Mathf.Sin(Mathf.PI * phase), 2f);
            float kick = Mathf.Exp(-phase / .18f) + Mathf.Exp(-(phase + 1f) / .18f);
            float flare = Flare + FlareBeat * kick;
            Flared = flare;
            Risen = rise;
            var swirl = Quaternion.AngleAxis(-Trail * (.75f + .25f * kick), Vector3.up);
            var lean = Quaternion.AngleAxis(-Lean, Vector3.forward);
            foreach (var c in couples)
            {
                if (c.root == null) continue;
                for (int r = 0; r < c.rows.Length; r++)
                {
                    float s = c.share[r];
                    var row = c.rows[r];
                    row.localRotation = Quaternion.Slerp(Quaternion.identity, swirl, s);
                    row.localScale = new Vector3(1f + flare * s, 1f, 1f + flare * s);
                }
                for (int i = 0; i < c.partner.Length; i++)
                {
                    if (c.partner[i] == null) continue;
                    var foot = c.partnerFoot + new Vector3(0f, rise, 0f);
                    c.partner[i].SetPositionAndRotation(c.root.TransformPoint(foot + lean * (c.partnerAt[i] - c.partnerFoot)),
                                                        c.root.rotation * (lean * c.partnerTurn[i]));
                }
            }
        }
    }
}
