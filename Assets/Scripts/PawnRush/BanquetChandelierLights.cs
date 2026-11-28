using System.Collections.Generic;
using ChessFight.Gameplay;
using UnityEngine;

namespace ChessFight.PawnRush
{
    // The grand banquet hall's hanging chandeliers lighting up under a rider (seoungtest, the user 11-25: "대연회장 기물이
    // 샹들리에를 타면 녹색등 밑에 켜주고 위에 촛불은 실제 촛불 색으로 켜줘"). While a piece hangs on a chandelier:
    //   - the eight crystal drops hung in a circle under its main ring glow the hall's go-green (the boarding lamps'
    //     green, GrandBanquetHallDressing.LampGlobes) and light what is under the ring green (the user 11-25, of a
    //     first lamp in the middle under the bowl: "아래 가운데에서 녹색 빛이 나오는 게 아니라 원으로 둘러 싸인
    //     부분에서 나와야 해");
    //   - its eighteen candles burn: a flame on every wick in a candle flame's own colours (a dim blue foot, a
    //     yellow-white body, an orange tip), standing upright while the chandelier swings and leaning back from its
    //     speed, and one warm flickering light among them.
    // Nobody on it: the drops are the model's clear crystal and the candles are out.
    //
    // This object is the rope's "BH chandelier look" (ChandelierLook turns it with the rope: its -y runs down the rope
    // from the pivot). Who rides is looked for where the piece hangs - any character's body (ILaunchable; a network
    // puppet's too) by the last of the rope - so every PC lights it from what it sees; nothing is sent. Only looks.
    public sealed class ChandelierRideLights : MonoBehaviour
    {
        // Off: the candles burn whether or not a piece rides (the lamp still waits for a rider).
        public static bool CandlesOnlyWhenRidden = true;

        const float LookEvery = .1f, StayLit = .3f, LightUp = .25f, GoOut = .5f;
        static readonly Color CandleLight = new Color(1f, .74f, .4f), GoGreen = new Color(.49f, 1f, .6f);

        RopeLine rope;
        float length;
        GameObject[] drops = new GameObject[0];
        Light[] greens = new Light[0];
        Light warm;
        Transform[] flames = new Transform[0];
        float[] sizes = new float[0];
        float seed, nextLook, seenUntil = float.NegativeInfinity, lamp, burn;
        bool lampLit;

        static readonly Collider[] hits = new Collider[32];
        static Mesh flameMesh, dropMesh;

        public bool Ridden => Time.time < seenUntil;
        public int Flames => flames.Length;

        // R905 (LooseChandeliers, main): `Out` - this chandelier's chain ran out: its candles are blown out and its lamp
        // stays dark whoever is by it, for as long as it hangs down. `Safe` - the section's relief has made every
        // chandelier hold: lit as if ridden, so that it is seen. `Proven` - someone has ridden this one through the middle
        // of its swing this round and it held: its candles stay lit (the lamp is still only for a rider), so whoever
        // comes after sees which of a pair was crossed on.
        [System.NonSerialized] public bool Out, Safe, Proven;

        // Where the wicks of bh_b6_chandelier_model.py's hanging(L) stand, in the look's frame (Blender x, y, z is Unity
        // x, z, y here), and each candle's size: six on the upper band, four on the main band, eight on the S-arms.
        public static void Wicks(float ropeLength, List<Vector3> at, List<float> size)
        {
            float top = -(ropeLength - 2.5f), upper = top - .62f, main = top - 1.55f;
            for (int i = 0; i < 6; i++) Wick(at, size, i * 60f, .6f, upper + .155f, 1.4f);
            for (int i = 0; i < 4; i++) Wick(at, size, i * 90f, 1f, main + .185f, 1.5f);
            for (int i = 0; i < 8; i++) Wick(at, size, i * 45f + 22.5f, 1.28f, main + .64f, 1.5f);
        }

        static void Wick(List<Vector3> at, List<float> size, float degrees, float radius, float candleFoot, float candleSize)
        {
            float a = degrees * Mathf.Deg2Rad;
            // the wax ends 0.375 up a candle of size 1, its wick 0.41: the flame sits on the wick
            at.Add(new Vector3(Mathf.Cos(a) * radius, candleFoot + .39f * candleSize, Mathf.Sin(a) * radius));
            size.Add(candleSize);
        }

        // `flame`: the flames' material (null: no flames, the lights only); `dropLit`: the glowing green crystal (null:
        // the green lights only).
        public void Build(RopeLine line, float ropeLength, Material flame, Material dropLit)
        {
            rope = line;
            length = ropeLength;
            seed = transform.position.x * 1.37f + transform.position.z * .73f;

            // The green: over each of the model's eight drops under the S-arms (hanging(L): 1.12 m out, hung 0.2 m under
            // the main ring, size 1.5) a glowing drop a little bigger, shown while lit; a light under every other one.
            float ring = -(length - 2.5f) - 1.55f;
            if (dropLit != null) drops = new GameObject[8];
            greens = new Light[4];
            for (int i = 0; i < 8; i++)
            {
                float a = (i * 45f + 22.5f) * Mathf.Deg2Rad;
                Vector3 hung = new Vector3(Mathf.Cos(a) * 1.12f, ring - .2f, Mathf.Sin(a) * 1.12f);
                if (dropLit != null)
                {
                    var go = new GameObject("BH chandelier ride drop");
                    go.transform.SetParent(transform, false);
                    go.transform.localPosition = hung;
                    go.transform.localScale = Vector3.one * 1.5f;
                    go.AddComponent<MeshFilter>().sharedMesh = DropMesh();
                    var r = go.AddComponent<MeshRenderer>();
                    r.sharedMaterial = dropLit;
                    r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                    r.receiveShadows = false;
                    go.SetActive(false);
                    drops[i] = go;
                }
                if (i % 2 == 0) greens[i / 2] = NewLight("BH chandelier ride light", hung + Vector3.down * .45f, GoGreen, 5f);
            }

            var at = new List<Vector3>();
            var size = new List<float>();
            Wicks(length, at, size);
            Vector3 middle = Vector3.zero;
            foreach (var p in at) middle += p / at.Count;
            warm = NewLight("BH chandelier candle light", new Vector3(0f, middle.y + .35f, 0f), CandleLight, 8f);
            if (flame != null)
            {
                flames = new Transform[at.Count];
                sizes = size.ToArray();
                for (int i = 0; i < at.Count; i++)
                {
                    // (11-26) the hall's candle flame (BanquetShowKit.NewFlame): painted cards, a halo, an ember
                    var go = BanquetShowKit.NewFlame("BH chandelier flame", transform).gameObject;
                    go.transform.localPosition = at[i];
                    go.SetActive(false);
                    flames[i] = go.transform;
                }
            }
            Show();
        }

        Light NewLight(string name, Vector3 at, Color color, float range)
        {
            var l = new GameObject(name).AddComponent<Light>();
            l.transform.SetParent(transform, false);
            l.transform.localPosition = at;
            l.type = LightType.Point;
            l.color = color;
            l.range = range;
            l.intensity = 0f;
            l.shadows = LightShadows.None;
            l.enabled = false;
            return l;
        }

        // A piece on this chandelier: a character's body by the chandelier's part of the rope or just under its end
        // (it hangs from the hoop there, its hips 0.6 m under its hands).
        bool LookForRider()
        {
            double now = ObstacleClock.Now;
            Vector3 down = rope.Direction(now);
            Vector3 a = rope.Point(length - 2.3f, now), b = rope.Point(length, now) + down * .8f;
            int n = Physics.OverlapCapsuleNonAlloc(a, b, .45f, hits, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < n; i++)
                if (hits[i] != null && hits[i].GetComponentInParent<ILaunchable>() != null) return true;
            return false;
        }

        void FixedUpdate()
        {
            if (rope == null || Time.time < nextLook) return;
            nextLook = Time.time + LookEvery;
            if (LookForRider()) seenUntil = Time.time + StayLit;
        }

        void LateUpdate()
        {
            if (rope == null) return;
            bool ridden = !Out && (Ridden || Safe), burning = !Out && (ridden || Proven || !CandlesOnlyWhenRidden);
            float dt = Time.deltaTime;
            lamp = Mathf.MoveTowards(lamp, ridden ? 1f : 0f, dt / (ridden ? .12f : GoOut));
            burn = Mathf.MoveTowards(burn, burning ? 1f : 0f, dt / (burning ? LightUp : GoOut));
            Show();
        }

        void Show()
        {
            bool lit = lamp > .5f;
            if (lit != lampLit)
                foreach (var drop in drops) if (drop != null) drop.SetActive(lit);
            lampLit = lit;
            foreach (var green in greens)
            {
                if (green == null) continue;
                green.enabled = lamp > .01f;
                green.intensity = 1.6f * lamp;
            }
            float t = Time.time;
            if (warm != null)
            {
                warm.enabled = burn > .01f;
                float n = Mathf.PerlinNoise(t * 3.1f, seed) * .6f + Mathf.PerlinNoise(t * 9.7f, seed + 3f) * .4f;
                warm.intensity = 2.6f * burn * (.85f + .3f * n);
            }
            if (flames.Length == 0) return;
            bool show = burn > .01f;
            // Upright whatever the chandelier's angle, leaning back from where it is going.
            Vector3 speed = rope != null ? rope.PointVelocity(length - 1f, ObstacleClock.Now) : Vector3.zero;
            for (int i = 0; i < flames.Length; i++)
            {
                var f = flames[i];
                if (f == null) continue;
                if (f.gameObject.activeSelf != show) f.gameObject.SetActive(show);
                if (!show) continue;
                float a = Mathf.PerlinNoise(t * 7.3f, seed + i * 1.9f) - .5f, b = Mathf.PerlinNoise(seed + i * 2.3f, t * 5.1f) - .5f;
                Vector3 lean = Vector3.up - speed * .05f + new Vector3(a, 0f, b) * .22f;
                f.rotation = Quaternion.FromToRotation(Vector3.up, lean.normalized);
                float s = sizes[i] * burn;
                f.localScale = new Vector3(s * (1f - .12f * a), s * (1f + .3f * a + .1f * b), s * (1f - .12f * a));
            }
        }

        // A crystal drop of size 1 hung from the origin (bh_b6_chandelier_model.py's drop(): a faceted pear 0.25 m long),
        // a tenth wider so it covers the model's.
        static Mesh DropMesh()
        {
            if (dropMesh != null) return dropMesh;
            float[] radius = { 0f, .035f, .055f, .04f, 0f }, down = { .015f, .06f, .13f, .19f, .255f };
            const int sides = 8;
            var v = new List<Vector3>();
            var tris = new List<int>();
            for (int j = 0; j < radius.Length; j++)
                for (int i = 0; i < sides; i++)
                {
                    float a = 2f * Mathf.PI * i / sides;
                    v.Add(new Vector3(Mathf.Cos(a) * radius[j] * 1.1f, -down[j], Mathf.Sin(a) * radius[j] * 1.1f));
                }
            for (int j = 0; j < radius.Length - 1; j++)
                for (int i = 0; i < sides; i++)
                {
                    int p = j * sides + i, q = j * sides + (i + 1) % sides;      // (the rings go down: wound the other way)
                    tris.Add(p); tris.Add(q); tris.Add(p + sides);
                    tris.Add(q); tris.Add(q + sides); tris.Add(p + sides);
                }
            dropMesh = new Mesh { name = "BH chandelier ride drop" };
            dropMesh.SetVertices(v);
            dropMesh.SetTriangles(tris, 0);
            dropMesh.RecalculateNormals();
            dropMesh.RecalculateBounds();
            return dropMesh;
        }

        // A candle flame of a size-1 candle (0.3 m of wax): a teardrop 0.2 m high and 0.09 m across on the wick, its
        // colours in its vertices - a dim blue foot round the wick, a yellow-white body, an orange tip that thins out.
        static Mesh FlameMesh()
        {
            if (flameMesh != null) return flameMesh;
            const int rings = 12, sides = 12;
            const float height = .2f, wide = .045f;
            var foot = new Color(.3f, .45f, 1f, .35f);
            var body = new Color(1f, .93f, .66f, 1f);
            var high = new Color(1f, .74f, .3f, .95f);
            var tip = new Color(1f, .5f, .12f, 0f);
            var v = new List<Vector3>();
            var c = new List<Color>();
            var uv = new List<Vector2>();
            var tris = new List<int>();
            for (int j = 0; j <= rings; j++)
            {
                float u = (float)j / rings;
                float r = wide * Mathf.Sin(Mathf.PI * Mathf.Pow(u, .55f));     // widest a quarter of the way up
                Color col = u < .16f ? Color.Lerp(foot, body, u / .16f)
                          : u < .55f ? Color.Lerp(body, high, (u - .16f) / .39f)
                          : Color.Lerp(high, tip, (u - .55f) / .45f);
                for (int i = 0; i < sides; i++)
                {
                    float a = 2f * Mathf.PI * i / sides;
                    v.Add(new Vector3(Mathf.Cos(a) * r, u * height, Mathf.Sin(a) * r));
                    c.Add(col);
                    uv.Add(new Vector2(.5f, .5f));
                }
            }
            for (int j = 0; j < rings; j++)
                for (int i = 0; i < sides; i++)
                {
                    int p = j * sides + i, q = j * sides + (i + 1) % sides;
                    tris.Add(p); tris.Add(p + sides); tris.Add(q);
                    tris.Add(q); tris.Add(p + sides); tris.Add(q + sides);
                }
            flameMesh = new Mesh { name = "BH candle flame" };
            flameMesh.SetVertices(v);
            flameMesh.SetColors(c);
            flameMesh.SetUVs(0, uv);
            flameMesh.SetTriangles(tris, 0);
            flameMesh.RecalculateNormals();
            flameMesh.RecalculateBounds();
            return flameMesh;
        }
    }
}
