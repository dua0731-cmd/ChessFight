using System.Collections.Generic;
using ChessFight.Gameplay;
using UnityEngine;

namespace ChessFight.PawnRush
{
    // The hall's walls on the dressed hall (대연회장 연출 R845; bh-hall's B7 models, read only - this is a file of its own):
    //   - the wall sconces' candles burn: R843's flame on every wick of bh_hall_pilasters' "sconce candles (lit)", each
    //     with a soft halo behind it (no lights of their own: there are 28 sconces);
    //   - the banners stir: each banner's cloth on a few bones from its rod (still) down to its point, billowing a little
    //     out from the wall and back in a slow air that runs down it, every banner its own way.
    // The stir is a function of the shared obstacle clock (HANDOFF rule 7); the flames' waver is decoration.
    [DefaultExecutionOrder(205)]
    public sealed class BanquetHallShow : MonoBehaviour
    {
        const int Rows = 5;
        const float Billow = .14f, Sideways = .05f;

        sealed class Banner
        {
            public Transform[] rows;
            public Vector3[] rest;
            public Vector3 out_, along;
            public float seed;
        }

        readonly List<Banner> banners = new List<Banner>();
        readonly List<UnityEngine.Object> owned = new List<UnityEngine.Object>();
        BanquetShowKit.Flames flames;

        public int Candles => flames != null ? flames.Count : 0;
        public int Banners => banners.Count;
        // As last drawn: the most a banner's point stands out of its rest (m).
        public float Stirred { get; private set; }

        public void Configure(GrandBanquetHallDressing dressing)
        {
            var frame = dressing.transform;
            var model = GrandBanquetHallDressing.ModelOn(frame, "bh_hall_pilasters");
            if (model == null) return;
            var holder = new GameObject("BH hall show").transform;
            holder.SetParent(frame, false);
            var wicks = BanquetShowKit.Wicks(model, frame, n => n.Contains("sconce candles"));
            flames = new BanquetShowKit.Flames(holder, wicks, null, null, 1.5f);

            foreach (var part in BanquetShowKit.Parts(model))
            {
                if (!part.Key.StartsWith("BH hall banner")) continue;
                // the banner's extent in the frame (its finest level)
                Vector3 lo = Vector3.positiveInfinity, hi = Vector3.negativeInfinity;
                foreach (var filter in part.Value)
                {
                    var mesh = filter.sharedMesh;
                    if (mesh == null || !mesh.isReadable) continue;
                    var m = frame.worldToLocalMatrix * filter.transform.localToWorldMatrix;
                    foreach (var v in mesh.vertices)
                    {
                        var p = m.MultiplyPoint3x4(v);
                        lo = Vector3.Min(lo, p);
                        hi = Vector3.Max(hi, p);
                    }
                    break;
                }
                if (!(hi.y > lo.y + 1f)) continue;
                var middle = (lo + hi) * .5f;
                var b = new Banner { rows = new Transform[Rows], rest = new Vector3[Rows], seed = Mathf.Repeat(middle.x * .37f + middle.z * .23f, 1f) * 6.28f };
                bool eastWest = hi.x - lo.x < hi.z - lo.z;           // on the east or west wall: its cloth along z
                b.out_ = eastWest ? new Vector3(middle.x > 0f ? -1f : 1f, 0f, 0f) : new Vector3(0f, 0f, middle.z > 36f ? -1f : 1f);
                b.along = eastWest ? Vector3.forward : Vector3.right;
                float top = hi.y, foot = lo.y;
                for (int r = 0; r < Rows; r++)
                {
                    b.rows[r] = BanquetShowKit.Bone("BH banner row " + r, frame, new Vector3(middle.x, Mathf.Lerp(top, foot, (float)r / (Rows - 1)), middle.z));
                    b.rest[r] = b.rows[r].localPosition;
                }
                void Weigh(Vector3 v, out int a, out int c, out float wa)
                {
                    float q = Mathf.Clamp01((top - v.y) / Mathf.Max(.1f, top - foot)) * (Rows - 1);
                    a = Mathf.Min(Rows - 2, Mathf.FloorToInt(q));
                    c = a + 1;
                    wa = 1f - (q - a);
                }
                bool skinned = false;
                foreach (var filter in part.Value)
                    skinned |= BanquetShowKit.Skin(filter, frame, b.rows, Weigh, .3f, owned) != null;
                if (skinned) banners.Add(b);
            }
            Debug.Log("[대연회장 홀 벽 연출] 벽 촛불 " + Candles + " · 깃발 " + banners.Count);
        }

        void OnDestroy()
        {
            foreach (var o in owned)
                if (o != null) Destroy(o);
        }

        void LateUpdate()
        {
            var viewer = BanquetShowKit.Viewer();
            flames?.Update(1f, viewer != null ? viewer.transform : null);
            float t = (float)(ObstacleClock.Now % 3600.0), most = 0f;
            foreach (var b in banners)
                for (int r = 1; r < Rows; r++)
                {
                    var bone = b.rows[r];
                    if (bone == null) continue;
                    float k = (float)r / (Rows - 1), reach = Mathf.Pow(k, 1.5f);
                    // a slow air running down the cloth: mostly out from the wall and back
                    float wave = .35f + .65f * Mathf.Sin(t * .9f + b.seed - 1.3f * k) * (.8f + .2f * Mathf.Sin(t * .37f + b.seed * 2f));
                    float side = Mathf.Sin(t * .63f + b.seed * 1.7f - .9f * k);
                    var offset = b.out_ * (Billow * reach * wave) + b.along * (Sideways * reach * side);
                    bone.localPosition = b.rest[r] + offset;
                    if (r == Rows - 1) most = Mathf.Max(most, offset.magnitude);
                }
            Stirred = most;
        }
    }
}
