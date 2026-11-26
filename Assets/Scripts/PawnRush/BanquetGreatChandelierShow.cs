using System.Collections.Generic;
using ChessFight.Gameplay;
using UnityEngine;

namespace ChessFight.PawnRush
{
    // The great chandelier's show on the dressed hall (대연회장 연출 R845): its 24 candles burn (R843's flame, a soft halo
    // behind each, four warm lights among them) and its crystals stir in the hall's air - the 24 drops under the rim and
    // the pendant under the bowl, each on a bone of its own (bh_b6_chandelier_model.great: a drop hung at 4.5 m out,
    // 0.72 m under the floor, a pear 0.65 m long; the pendant 1.34 m under). The dish, its carpet and the column - the
    // floor the pawns rest on and its colliders - are as built and still. The stir is a function of the shared obstacle
    // clock (HANDOFF rule 7); the flames' waver is decoration (Time.time).
    [DefaultExecutionOrder(205)]
    public sealed class BanquetGreatChandelierShow : MonoBehaviour
    {
        const int Drops = 24;
        const float DropOut = 4.5f, DropEye = -.72f, Stir = 2.2f, PendantStir = 1.2f, Glow = 1.7f;

        readonly List<UnityEngine.Object> owned = new List<UnityEngine.Object>();
        Transform[] bones = new Transform[0];
        BanquetShowKit.Flames flames;
        Light[] lights = new Light[0];
        Transform frame;

        public int Candles => flames != null ? flames.Count : 0;
        public int Skinned { get; private set; }
        // As last drawn: the most a crystal is turned (degrees).
        public float Stirred { get; private set; }

        public void Configure(GrandBanquetHallDressing dressing)
        {
            frame = dressing.transform;
            var model = GrandBanquetHallDressing.ModelOn(frame, "bh_great_chandelier");
            if (model == null) return;
            var centre = new Vector3(0f, GrandBanquetHallBuilder.GreatY, GrandBanquetHallBuilder.GreatZ);
            var holder = new GameObject("BH great chandelier show").transform;
            holder.SetParent(frame, false);

            // the candles: on the wicks of the model's "candles (lit)"
            var wicks = BanquetShowKit.Wicks(model, frame, n => n.Contains("candles"));
            flames = new BanquetShowKit.Flames(holder, wicks, null, null, 1.7f);
            lights = new Light[4];
            for (int i = 0; i < lights.Length; i++)
            {
                float a = (45f + 90f * i) * Mathf.Deg2Rad;
                lights[i] = BanquetShowKit.Point("BH great chandelier light", holder, BanquetShowKit.CandleLight, 10f, Glow);
                lights[i].transform.localPosition = centre + new Vector3(Mathf.Cos(a) * 3.2f, 1.3f, Mathf.Sin(a) * 3.2f);
            }

            // the crystals' bones: a still one, one at each drop's eye, one at the pendant's top
            bones = new Transform[Drops + 2];
            bones[0] = BanquetShowKit.Bone("BH great chandelier still", frame, Vector3.zero);
            for (int i = 0; i < Drops; i++)
            {
                float a = (i * 15f + 7.5f) * Mathf.Deg2Rad;
                bones[i + 1] = BanquetShowKit.Bone("BH great chandelier drop " + i, frame, centre + new Vector3(Mathf.Cos(a) * DropOut, DropEye, Mathf.Sin(a) * DropOut));
            }
            bones[Drops + 1] = BanquetShowKit.Bone("BH great chandelier pendant", frame, centre + new Vector3(0f, -1.34f, 0f));
            void Weigh(Vector3 v, out int a, out int b, out float wa)
            {
                a = b = 0;
                wa = 1f;
                var rel = v - centre;
                float flat = new Vector2(rel.x, rel.z).magnitude;
                if (rel.y < -1.36f && flat < .45f) { a = b = Drops + 1; return; }
                if (rel.y > DropEye - .03f || rel.y < DropEye - .75f || Mathf.Abs(flat - DropOut) > .2f) return;
                float ang = Mathf.Atan2(rel.z, rel.x) * Mathf.Rad2Deg;
                int k = Mathf.RoundToInt(Mathf.Repeat(ang - 7.5f, 360f) / 15f) % Drops;
                var at = bones[k + 1].localPosition;
                if (new Vector2(v.x - at.x, v.z - at.z).magnitude > .2f) return;
                a = b = k + 1;
            }
            foreach (var filter in model.GetComponentsInChildren<MeshFilter>(true))
            {
                if (filter.name.Contains("candles")) continue;
                if (BanquetShowKit.Skin(filter, frame, bones, Weigh, .3f, owned) != null) Skinned++;
            }
            Debug.Log("[대연회장 큰 샹들리에 연출] 촛불 " + Candles + " · 수정 뼈 " + (bones.Length - 1) + " · 메시 " + Skinned);
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
            float flick = Time.time;
            for (int i = 0; i < lights.Length; i++)
                lights[i].intensity = Glow * (.86f + .28f * Mathf.PerlinNoise(flick * 1.6f + i * 3.1f, .4f));
            float t = (float)(ObstacleClock.Now % 3600.0), most = 0f;
            for (int i = 1; i < bones.Length; i++)
            {
                if (bones[i] == null) continue;
                bool pendant = i == bones.Length - 1;
                float s = i * 2.39f, amp = pendant ? PendantStir : Stir;
                // two slow airs across each other, every drop its own phase
                float x = amp * (.7f * Mathf.Sin(t * 1.9f + s) + .3f * Mathf.Sin(t * 3.3f + s * 1.7f));
                float z = amp * (.7f * Mathf.Cos(t * 1.6f + s * 1.3f) + .3f * Mathf.Sin(t * 2.7f + s * .6f));
                bones[i].localRotation = Quaternion.Euler(x, 0f, z);
                most = Mathf.Max(most, Mathf.Sqrt(x * x + z * z));
            }
            Stirred = most;
        }
    }
}
