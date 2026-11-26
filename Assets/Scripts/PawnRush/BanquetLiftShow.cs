using System;
using System.Collections.Generic;
using ChessFight.Gameplay;
using UnityEngine;

namespace ChessFight.PawnRush
{
    // The cloche lifts' show on the dressed hall (대연회장 연출 R845). The lifts (ClocheLift: the platter a floor, the
    // cloche a dome over it) keep their course and colliders; what is drawn adds:
    //   - the dumbwaiter's six pulleys turn as their cable or chain runs over them (bh_b2_cloche_model.pulley: a sheave
    //     0.15 m in radius with eight spokes; its vertices are turned about the axle, as far as the line has run);
    //   - the platter and its cloche jolt as they arrive, top and bottom (a dip of a few centimetres that dies out), in
    //     the pictures only;
    //   - as a cloche lifts off, steam pours out from under it and rises off the platter, and the silver dome glints.
    // All of it from the shared obstacle clock (HANDOFF rule 7): nothing is sent, nothing adds up. The steam fades where
    // it comes near the camera, so it never hides the pawn being followed.
    [DefaultExecutionOrder(205)]
    public sealed class BanquetLiftShow : MonoBehaviour
    {
        const float Sheave = .15f, Jolt = .035f, JoltTilt = .9f, JoltHertz = 3.4f, JoltFade = .22f;
        const int Puffs = 22, Glints = 3;
        const float SteamLife = 2.4f, SteamEvery = .055f, GlintLife = .55f;

        sealed class Lift
        {
            public ClocheLift platter, cloche;
            public Transform platterModel, clocheModel;
            public Vector3 platterRest, clocheRest;
            public Transform[] puffs, glints;
            public float seed;
        }

        readonly List<Lift> lifts = new List<Lift>();
        Transform[] sheaves = Array.Empty<Transform>();
        readonly List<UnityEngine.Object> owned = new List<UnityEngine.Object>();
        readonly float[] turned = new float[6];
        Transform frame;
        MaterialPropertyBlock block;

        public int Lifts => lifts.Count;
        public int PulleyMeshes { get; private set; }
        // As last drawn: puffs of steam shown, the most a pulley has turned (degrees), the deepest jolt (m).
        public int Steam { get; private set; }
        public float Turned { get; private set; }
        public float Jolted { get; private set; }

        public void Configure(GrandBanquetHallDressing dressing)
        {
            frame = dressing.transform;
            block = new MaterialPropertyBlock();
            var steam = BanquetShowKit.SoftMaterial(new Color(1f, 1f, 1f, 1f), owned);
            var glint = BanquetShowKit.GlowMaterial(new Color(1f, .97f, .9f, 1f), owned);
            foreach (string side in new[] { "W", "E" })
            {
                ClocheLift platter = null, cloche = null;
                foreach (var t in dressing.FindAll("BH cloche platter " + side)) if (t.TryGetComponent(out platter)) break;
                foreach (var t in dressing.FindAll("BH cloche " + side)) if (t.TryGetComponent(out cloche)) break;
                if (platter == null || cloche == null) continue;
                var l = new Lift { platter = platter, cloche = cloche, seed = side == "W" ? .3f : .7f };
                l.platterModel = GrandBanquetHallDressing.ModelOn(platter.transform, "bh_platter");
                l.clocheModel = GrandBanquetHallDressing.ModelOn(cloche.transform, "bh_cloche");
                if (l.platterModel != null)
                {
                    l.platterRest = l.platterModel.localPosition;
                    Brushed(l.platterModel);
                }
                if (l.clocheModel != null) l.clocheRest = l.clocheModel.localPosition;
                var holder = new GameObject("BH cloche steam " + side).transform;
                holder.SetParent(frame, false);
                l.puffs = new Transform[steam != null ? Puffs : 0];
                for (int i = 0; i < l.puffs.Length; i++)
                {
                    l.puffs[i] = BanquetShowKit.Drawn("BH steam puff", holder, BanquetShowKit.SoftDiscMesh(), steam);
                    l.puffs[i].gameObject.SetActive(false);
                }
                l.glints = new Transform[glint != null ? Glints : 0];
                for (int i = 0; i < l.glints.Length; i++)
                {
                    var g = new GameObject("BH cloche glint").transform;
                    g.SetParent(holder, false);
                    // a four-pointed star: two blades of light across each other
                    var a = BanquetShowKit.Drawn("ray", g, BanquetShowKit.BladeMesh(), glint);
                    a.localPosition = new Vector3(0f, -.5f, 0f);
                    a.localScale = new Vector3(.09f, 1f, 1f);
                    var b = BanquetShowKit.Drawn("ray", g, BanquetShowKit.BladeMesh(), glint);
                    b.localRotation = Quaternion.Euler(0f, 0f, 90f);
                    b.localPosition = new Vector3(.5f, 0f, 0f);
                    b.localScale = new Vector3(.09f, 1f, 1f);
                    g.gameObject.SetActive(false);
                    l.glints[i] = g;
                }
                lifts.Add(l);
            }
            SetUpPulleys();
            Debug.Log("[대연회장 승강기 연출] 승강기 " + lifts.Count + " · 도르래 메시 " + PulleyMeshes);
        }

        // The six sheaves of the dumbwaiter's frame (bh_dumbwaiter): a bone on each axle; every vertex within the
        // sheave's radius of an axle and within its thickness of the frame's middle line follows it (the yoke's legs,
        // 0.088 out, and the rest of the frame stay with a still bone).
        void SetUpPulleys()
        {
            var model = GrandBanquetHallDressing.ModelOn(frame, "bh_dumbwaiter");
            if (model == null) return;
            sheaves = new Transform[7];
            sheaves[0] = BanquetShowKit.Bone("BH dumbwaiter still", frame, Vector3.zero);
            int n = 1;
            foreach (float cx in GrandBanquetHallBuilder.ClocheX)
                for (int s = -1; s <= 1; s++, n++)
                    sheaves[n] = BanquetShowKit.Bone("BH dumbwaiter sheave " + n, frame,
                        new Vector3(cx + s * GrandBanquetHallBuilder.CableX, GrandBanquetHallBuilder.GantryY - .25f, GrandBanquetHallBuilder.ClocheZ));
            void Weigh(Vector3 v, out int a, out int b, out float wa)
            {
                a = b = 0;
                wa = 1f;
                if (Mathf.Abs(v.z - GrandBanquetHallBuilder.ClocheZ) > .063f) return;
                for (int k = 1; k < sheaves.Length; k++)
                {
                    var at = sheaves[k].localPosition;
                    if (new Vector2(v.x - at.x, v.y - at.y).magnitude > Sheave + .003f) continue;
                    a = b = k;
                    return;
                }
            }
            foreach (var filter in model.GetComponentsInChildren<MeshFilter>(true))
                if (BanquetShowKit.Skin(filter, frame, sheaves, Weigh, .05f, owned) != null) PulleyMeshes++;
        }

        // (11-26, the user: "바닥에 통과 되어 보이잖아") The platter's walked top - its silver disc and its gold rim - is
        // wound the wrong way round as exported (bh_platter): seen from above it was not drawn, and the table under it
        // showed through. The platter's silver and gold are drawn with both faces (copies for the platters only; the
        // cloches and every other silver and gold keep theirs). Its silver is also brushed (bh_b2_cloche_model.platter:
        // "brushed silver"), not the library's perfect mirror.
        readonly Dictionary<Material, Material> twoSided = new Dictionary<Material, Material>();

        void Brushed(Transform model)
        {
            foreach (var r in model.GetComponentsInChildren<Renderer>(true))
            {
                var mats = r.sharedMaterials;
                bool changed = false;
                for (int i = 0; i < mats.Length; i++)
                {
                    var m = mats[i];
                    if (m == null) continue;
                    bool silver = m.name.StartsWith("BH_Silver", StringComparison.Ordinal), gold = m.name.StartsWith("BH_Gold", StringComparison.Ordinal);
                    if (!silver && !gold) continue;
                    if (!twoSided.TryGetValue(m, out var copy))
                    {
                        copy = new Material(m) { name = m.name + " (platter, both faces)" };
                        if (copy.HasProperty("_Cull")) copy.SetFloat("_Cull", 0f);
                        copy.doubleSidedGI = true;
                        if (silver)
                        {
                            if (copy.HasProperty("_Smoothness")) copy.SetFloat("_Smoothness", .42f);
                            if (copy.HasProperty("_Metallic")) copy.SetFloat("_Metallic", .85f);
                            copy.color = new Color(.9f, .9f, .92f, 1f);
                        }
                        owned.Add(copy);
                        twoSided[m] = copy;
                    }
                    mats[i] = copy;
                    changed = true;
                }
                if (changed) r.sharedMaterials = mats;
            }
        }

        void OnDestroy()
        {
            foreach (var o in owned)
                if (o != null) Destroy(o);
        }

        void LateUpdate()
        {
            if (frame == null) return;
            double now = ObstacleClock.Now;
            var viewer = BanquetShowKit.Viewer();
            var eye = viewer != null ? viewer.transform : null;
            int steam = 0;
            float jolted = 0f;
            for (int i = 0; i < lifts.Count; i++)
            {
                var l = lifts[i];
                if (l.platter == null || l.cloche == null) continue;
                l.platter.Heights(now, out float platter, out float cloche);
                // the lines over this lift's pulleys: the cables pay out as the platter rises, the chain as the cloche does
                turned[i * 3 + 0] = turned[i * 3 + 2] = platter / Sheave;
                turned[i * 3 + 1] = (platter + cloche - l.cloche.Open) / Sheave;
                jolted = Mathf.Max(jolted, JoltLift(l, now));
                steam += Steaming(l, now, platter, cloche, eye);
            }
            Steam = steam;
            Jolted = jolted;
            TurnPulleys();
        }

        // Seconds since the platter last came to rest after moving (top or bottom), or -1 if it moves now or rests longer.
        static float Arrived(ClocheLift lift, double now)
        {
            if (Moving(lift, now)) return -1f;
            for (float s = .02f; s < 1.6f; s += .02f)
                if (Moving(lift, now - s)) return s;
            return -1f;
        }

        static bool Moving(ClocheLift lift, double t)
        {
            lift.Heights(t, out float a, out _);
            lift.Heights(t - .02, out float b, out _);
            return Mathf.Abs(a - b) > 2e-4f;
        }

        float JoltLift(Lift l, double now)
        {
            float s = Arrived(l.platter, now);
            float dip = 0f, tilt = 0f;
            if (s > 0f)
            {
                float e = Mathf.Exp(-s / JoltFade);
                // (11-26, the user: through the table) it hops up off where it lands, never down into it, and tipped it
                // stands on its low rim: lifted by what would otherwise sink that rim (the platter 2.2 m out)
                dip = Jolt * e * Mathf.Abs(Mathf.Sin(2f * Mathf.PI * JoltHertz * s));
                tilt = JoltTilt * e * Mathf.Sin(2f * Mathf.PI * JoltHertz * 1.3f * s + l.seed * 4f);
            }
            var lean = Quaternion.Euler(tilt, 0f, tilt * .6f);
            float rim = (GrandBanquetHallBuilder.ClocheR + .1f) * Mathf.Sin(1.17f * Mathf.Abs(tilt) * Mathf.Deg2Rad);
            if (l.platterModel != null)
            {
                l.platterModel.localPosition = l.platterRest + new Vector3(0f, dip + rim, 0f);
                l.platterModel.localRotation = lean * Quaternion.Euler(0f, 180f, 0f);
            }
            if (l.clocheModel != null)
            {
                // the cloche rides on the platter while shut: it jolts with it, tipped as much, its band's low side on it
                l.clocheModel.localPosition = l.clocheRest + new Vector3(0f, dip + 2f * rim, 0f);
                l.clocheModel.localRotation = lean * Quaternion.Euler(0f, 180f, 0f);
            }
            return Mathf.Abs(dip);
        }

        // Seconds since the cloche began to lift off the platter (within the last few seconds), or -1.
        static float LiftedOff(ClocheLift platter, double now, float cloche)
        {
            if (cloche <= .001f) return -1f;
            for (float s = .02f; s < SteamLife + Puffs * SteamEvery; s += .02f)
            {
                platter.Heights(now - s, out _, out float c);
                if (c <= .001f) return s;
            }
            return -1f;
        }

        int Steaming(Lift l, double now, float platter, float cloche, Transform eye)
        {
            float since = LiftedOff(l.platter, now, cloche);
            // at the dais the dish has come up hot: more steam than at the table
            bool top = platter > l.platter.Rise - .01f;
            float strength = top ? 1f : .55f;
            var middle = l.platter.transform.position;
            var root = frame.InverseTransformPoint(middle);
            int shown = 0;
            for (int i = 0; i < l.puffs.Length; i++)
            {
                var puff = l.puffs[i];
                float age = since - i * SteamEvery;
                bool on = since >= 0f && age >= 0f && age < SteamLife;
                if (puff.gameObject.activeSelf != on) puff.gameObject.SetActive(on);
                if (!on) continue;
                shown++;
                float k = age / SteamLife, a = 2f * Mathf.PI * (l.seed + i * .382f);
                var outward = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                // out from under the cloche's rim, then up and spreading, slowing as it cools
                float rim = GrandBanquetHallBuilder.ClocheR * (.75f + .3f * Mathf.Sqrt(k));
                float up = .15f + 2.6f * (1f - Mathf.Exp(-age / .9f)) + .25f * Mathf.Sin(age * 2.1f + i);
                var at = root + outward * rim + new Vector3(.25f * Mathf.Sin(age * 1.7f + i * 2f), up, .25f * Mathf.Cos(age * 1.3f + i));
                puff.localPosition = at;
                float size = (.5f + 1.7f * Mathf.Sqrt(k)) * strength;
                puff.localScale = new Vector3(size, size, size);
                if (eye != null) puff.rotation = Quaternion.LookRotation(puff.position - eye.position) * Quaternion.Euler(0f, 0f, 47f * i + 20f * age);
                float alpha = .3f * strength * Mathf.Sin(Mathf.PI * Mathf.Pow(k, .6f));
                if (eye != null)
                {
                    float d = Vector3.Distance(eye.position, frame.TransformPoint(at));
                    alpha *= Mathf.Clamp01((d - size - .5f) / 2f);
                }
                Tint(puff, new Color(.97f, .96f, .94f, alpha));
            }
            // the dome glints as it comes up into the light
            for (int i = 0; i < l.glints.Length; i++)
            {
                var g = l.glints[i];
                float age = since - .08f - i * .14f;
                bool on = since >= 0f && age >= 0f && age < GlintLife;
                if (g.gameObject.activeSelf != on) g.gameObject.SetActive(on);
                if (!on) continue;
                float a = (35f + 55f * i) * Mathf.Deg2Rad;
                var dome = l.cloche.transform.position + Vector3.up * (GrandBanquetHallBuilder.ClocheBand + .25f + .45f * i);
                float r = GrandBanquetHallBuilder.ClocheR * (1f - .25f * i);
                g.position = dome + frame.TransformDirection(new Vector3(Mathf.Cos(a) * r, 0f, -Mathf.Sin(a) * r));
                if (eye != null) g.rotation = Quaternion.LookRotation(g.position - eye.position) * Quaternion.Euler(0f, 0f, 30f * age);
                float s = .9f * Mathf.Sin(Mathf.PI * age / GlintLife);
                g.localScale = new Vector3(s, s, s);
                foreach (Transform ray in g) Tint(ray, new Color(1f, .97f, .9f, .9f));
            }
            return shown;
        }

        void Tint(Transform t, Color c)
        {
            if (!t.TryGetComponent<Renderer>(out var r)) return;
            block.Clear();
            ChessFight.Game.LitMaterials.SetColor(block, c);
            r.SetPropertyBlock(block);
        }

        void TurnPulleys()
        {
            float most = 0f;
            foreach (float a in turned) most = Mathf.Max(most, Mathf.Abs(a));
            Turned = most * Mathf.Rad2Deg;
            for (int k = 1; k < sheaves.Length; k++)
                if (sheaves[k] != null) sheaves[k].localRotation = Quaternion.AngleAxis(turned[k - 1] * Mathf.Rad2Deg, Vector3.forward);
        }
    }
}
