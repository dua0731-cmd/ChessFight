using System.Collections.Generic;
using ChessFight.Gameplay;
using UnityEngine;

namespace ChessFight.PawnRush
{
    // The royal dais and the king's balcony on the dressed hall (대연회장 연출 R845):
    //   - the head tables' candelabra burn (R843's flame on every wick of bh_head_tables' "BH3 head candelabra", a soft
    //     halo behind each, a warm light over each table);
    //   - the balcony - the hall's end - celebrates whoever reaches it: gold, ivory and olive confetti showers down over
    //     it with a warm flash. Who has arrived is looked for on this PC (any character's body, ILaunchable, as the
    //     chandeliers' ride lights do, R843): a picture only, nothing is sent; each PC shows it when it sees one come.
    [DefaultExecutionOrder(205)]
    public sealed class BanquetDaisShow : MonoBehaviour
    {
        const int Confetti = 90;
        const float ShowerTime = 3.6f, LookEvery = .15f, Glow = 2f;
        static readonly Color[] Colours =
        {
            new Color(1f, .82f, .3f), new Color(.98f, .95f, .86f), new Color(.55f, .62f, .3f), new Color(1f, .7f, .2f),
        };

        readonly List<UnityEngine.Object> owned = new List<UnityEngine.Object>();
        readonly Collider[] hits = new Collider[16];
        BanquetShowKit.Flames flames;
        Light[] tableLights = new Light[0];
        Transform frame, shower;
        Transform[] pieces = new Transform[0];
        Light flash;
        float nextLook, showerAt = float.NegativeInfinity;
        bool someone;
        MaterialPropertyBlock block;

        public int Candles => flames != null ? flames.Count : 0;
        public int Pieces => pieces.Length;
        // As last drawn: confetti in the air.
        public int Falling { get; private set; }

        public void Configure(GrandBanquetHallDressing dressing)
        {
            frame = dressing.transform;
            block = new MaterialPropertyBlock();
            var holder = new GameObject("BH dais show").transform;
            holder.SetParent(frame, false);
            var tables = GrandBanquetHallDressing.ModelOn(frame, "bh_head_tables");
            var wicks = BanquetShowKit.Wicks(tables, frame, n => n.Contains("candelabra"));
            flames = new BanquetShowKit.Flames(holder, wicks, null, null, 1.5f);
            // a light over each table's candles (west and east of the balcony)
            var sides = new List<Light>();
            foreach (float s in new[] { -1f, 1f })
            {
                var mid = Vector3.zero;
                int n = 0;
                foreach (var w in wicks) if (Mathf.Sign(w.x) == s) { mid += w; n++; }
                if (n == 0) continue;
                var l = BanquetShowKit.Point("BH head table light", holder, BanquetShowKit.CandleLight, 9f, Glow);
                l.transform.localPosition = mid / n + new Vector3(0f, .5f, 0f);
                sides.Add(l);
            }
            tableLights = sides.ToArray();

            // the shower: paper pieces (a flat card each, both faces), coloured per piece
            var paper = BanquetShowKit.SoftMaterial(Color.white, owned);
            if (paper != null)
            {
                shower = new GameObject("BH balcony confetti").transform;
                shower.SetParent(holder, false);
                var card = Card();
                pieces = new Transform[Confetti];
                for (int i = 0; i < Confetti; i++)
                {
                    pieces[i] = BanquetShowKit.Drawn("BH confetti", shower, card, paper);
                    if (pieces[i].TryGetComponent<Renderer>(out var r))
                    {
                        block.Clear();
                        ChessFight.Game.LitMaterials.SetColor(block, Colours[i % Colours.Length]);
                        r.SetPropertyBlock(block);
                    }
                }
                shower.gameObject.SetActive(false);
            }
            flash = BanquetShowKit.Point("BH balcony flash", holder, new Color(1f, .85f, .55f), 14f, 0f);
            flash.transform.localPosition = new Vector3(0f, GrandBanquetHallBuilder.BalconyY + 4f, GrandBanquetHallBuilder.BalconyZ0 + 4f);
            flash.enabled = false;
            Debug.Log("[대연회장 단 · 발코니 연출] 왕의 식탁 촛불 " + Candles + " · 조명 " + tableLights.Length + " · 축하 종이 " + pieces.Length);
        }

        void OnDestroy()
        {
            foreach (var o in owned)
                if (o != null) Destroy(o);
        }

        // The shower now (as when someone arrives; the checks call it to see it).
        public void Celebrate() => showerAt = Time.time;

        void Update()
        {
            if (Time.time < nextLook) return;
            nextLook = Time.time + LookEvery;
            // the balcony's floor and a pawn's height over it, from its front to the hall's north wall
            var half = new Vector3(GrandBanquetHallBuilder.BalconyX, 1.6f, .5f * (GrandBanquetHallBuilder.HallZ1 - GrandBanquetHallBuilder.BalconyZ0 - .6f));
            var middle = frame.TransformPoint(new Vector3(0f, GrandBanquetHallBuilder.BalconyY + 1.6f, .5f * (GrandBanquetHallBuilder.HallZ1 + GrandBanquetHallBuilder.BalconyZ0 + .6f)));
            int n = Physics.OverlapBoxNonAlloc(middle, half, hits, frame.rotation, ~0, QueryTriggerInteraction.Ignore);
            bool now = false;
            for (int i = 0; i < n && !now; i++)
                if (hits[i] != null && hits[i].GetComponentInParent<ILaunchable>() != null) now = true;
            if (now && !someone) Celebrate();
            someone = now;
        }

        void LateUpdate()
        {
            var viewer = BanquetShowKit.Viewer();
            flames?.Update(1f, viewer != null ? viewer.transform : null);
            for (int i = 0; i < tableLights.Length; i++)
                tableLights[i].intensity = Glow * (.86f + .28f * Mathf.PerlinNoise(Time.time * 1.5f + i * 4f, .7f));
            Shower();
        }

        void Shower()
        {
            float age = Time.time - showerAt;
            bool on = age >= 0f && age < ShowerTime;
            if (shower != null && shower.gameObject.activeSelf != on) shower.gameObject.SetActive(on);
            if (flash != null)
            {
                flash.enabled = on && age < .9f;
                if (flash.enabled) flash.intensity = 6f * Mathf.Exp(-age / .3f);
            }
            Falling = 0;
            if (!on) return;
            float y0 = GrandBanquetHallBuilder.BalconyY, z0 = GrandBanquetHallBuilder.BalconyZ0;
            for (int i = 0; i < pieces.Length; i++)
            {
                float h1 = Hash(i * 3 + 1), h2 = Hash(i * 7 + 2), h3 = Hash(i * 11 + 5);
                // burst up and out from over the balcony's front, then flutter down
                float t = Mathf.Max(0f, age - .15f * h3);
                var start = new Vector3((h1 - .5f) * 2f * GrandBanquetHallBuilder.BalconyX, y0 + 3.2f + h2, z0 + 1.5f + 5f * h3);   // under the canopy
                var kick = new Vector3((h1 - .5f) * 6f, 2.5f + 2f * h2, (h3 - .5f) * 4f);
                float rise = 1f - Mathf.Exp(-t / .35f);
                var drift = new Vector3(.5f * Mathf.Sin(t * 3.1f + i), 0f, .4f * Mathf.Cos(t * 2.7f + i * 1.3f));
                var at = start + kick * .35f * rise + drift + Vector3.down * (1.25f * Mathf.Max(0f, t - .35f));
                if (at.y < y0 + .05f) { pieces[i].gameObject.SetActive(false); continue; }
                if (!pieces[i].gameObject.activeSelf) pieces[i].gameObject.SetActive(true);
                Falling++;
                pieces[i].localPosition = at;
                pieces[i].localRotation = Quaternion.Euler(360f * h1 + 400f * t, 270f * h2 + 230f * t, 180f * h3 + 330f * t);
                float s = .16f * Mathf.Clamp01(t / .1f + .2f);
                pieces[i].localScale = new Vector3(s, s * 1.4f, s);
            }
        }

        static float Hash(int n)
        {
            n = (n << 13) ^ n;
            return ((n * (n * n * 15731 + 789221) + 1376312589) & 0x7fffffff) / 2147483647f;
        }

        // A paper card 1 x 1, both faces, opaque.
        Mesh Card()
        {
            var m = new Mesh { name = "BH confetti" };
            m.SetVertices(new List<Vector3> { new Vector3(-.5f, -.5f, 0f), new Vector3(.5f, -.5f, 0f), new Vector3(.5f, .5f, 0f), new Vector3(-.5f, .5f, 0f) });
            m.SetColors(new List<Color> { Color.white, Color.white, Color.white, Color.white });
            m.SetTriangles(new List<int> { 0, 2, 1, 0, 3, 2, 0, 1, 2, 0, 2, 3 }, 0);
            m.RecalculateNormals();
            m.RecalculateBounds();
            owned.Add(m);
            return m;
        }
    }
}
