using ChessFight.Game;
using ChessFight.Gameplay;
using ChessFight.ProtectKing;
using UnityEngine;
using UnityEngine.Rendering;

namespace ChessFight.PawnRush
{
    // R354 / R374 the royal balcony's guard knights' warnings on the obstacle's own clock (the kit's flat landing disc and
    // charge strip stay off: the obstacle still toggles them).
    // R566 (승규 11-10 "지금 전투마 소리는 너무 좋은데 예고랑 이펙트가 별로야 사운드에 맞게 만들어줘 예고는 필요에 따라서
    // 예고 안넣어도 돼"): the gold ghost, embers, swirl, wind streaks and dust ring are gone. The knight's own body and
    // the floor answer the war horse sound (R564 B1, CavalryWarnFx.Sound.cs) beat by beat:
    //   warning   (silent, 1.3 s) it gathers itself on the balcony - leans back, sinks a little, trembles at the end -
    //             and grit crumbles off the balcony lip
    //   push-off  (the snort and hoof push 0.22 s into the leap) it springs nose-up, chips kicked off the balcony
    //   air       (the whoosh) its shadow on the landing floor, wide and faint, smaller and darker as it comes down
    //   landing   (four heavy hooves at 1.0 s) four strikes round the pedestal 0.12 s apart: stone chips, iron sparks,
    //             the body squashes and springs back, the view shakes nearby
    //   windup    (a hoof pawing the floor and a snort) two scrapes at its front: sparks along the floor; it rears then
    //             coils back
    //   charge    (the gallop) leaning forward, bobbing on each beat of the sound, chips and sparks off its back edge
    //   stop      (the skid) leaning back, a spray of sparks and chips off its front edge
    // No dust, no rings, no glow: chips are lit marble and dark grout, sparks are the only light. Every moment is the
    // obstacle's own sample (Evaluate on the shared clock), the same on every PC; the scatter is each PC's own.
    [DisallowMultipleComponent]
    // R564: the leap and charge sounds are in CavalryWarnFx.Sound.cs.
    public sealed partial class CavalryWarnFx : MonoBehaviour
    {
        const string ShadowShader = "ChessFight/VFX/Ground Shadow Decal";
        static readonly int RadiusId = Shader.PropertyToID("_Radius"), DarknessId = Shader.PropertyToID("_Darkness"),
            BoxId = Shader.PropertyToID("_Box");
        // Beats in the sound (s): the push-off into the leap, the hooves after the landing, the paws into the windup, the
        // gallop into the charge (HallGuardLeap / HallGuardCharge, read off their loudness).
        const float PushOff = .24f;
        static readonly float[] Hooves = { 0f, .035f, .08f, .12f };
        static readonly float[] Paws = { .01f, .07f };
        static readonly float[] Gallop = { .11f, .16f, .22f, .29f, .37f, .47f, .54f, .61f, .70f, .79f };
        const float SkidFrom = .82f;   // s into the charge when it starts to stop
        const float BeatLate = .3f;    // a beat first seen later than this is left out

        static Texture2D dot;

        KnightCavalryCharge knight;
        ParticleSystem marble, grout, sparks;
        Transform shadow;
        MeshRenderer shadowView;
        MaterialPropertyBlock block;
        Vector3 basePosition, baseScale = Vector3.one;
        Quaternion baseRotation = Quaternion.identity;
        float runAge = -1f, gritDebt, skidDebt;
        int fired;      // the beats of this run already played, one bit each
        int lastCycle = int.MinValue;

        void Start()
        {
            knight = GetComponent<KnightCavalryCharge>();
            if (knight == null || knight.LandingPoint == null || knight.ChargeEndPoint == null || knight.StartPoint == null
                || knight.Visual == null || knight.KnightRoot == null)
            {
                enabled = false;
                return;
            }
            block = new MaterialPropertyBlock();
            Hide(knight.LandingWarning);
            Hide(knight.ChargeWarning);
            basePosition = knight.Visual.localPosition;
            baseRotation = knight.Visual.localRotation;
            baseScale = knight.Visual.localScale;
            if (dot == null) dot = SoftDot(64);
            marble = Chips("Knight marble chips", new Color(.82f, .78f, .71f), .35f);
            grout = Chips("Knight grout chips", new Color(.2f, .17f, .15f), .2f);
            sparks = Sparks();
            MakeShadow();
            Shaker.Hook();
            MakeFire();   // R567 (CavalryWarnFx.Fire.cs)
        }

        void OnDisable()
        {
            if (knight != null && knight.Visual != null)
            {
                knight.Visual.localPosition = basePosition;
                knight.Visual.localRotation = baseRotation;
                knight.Visual.localScale = baseScale;
            }
        }

        void LateUpdate()
        {
            // The obstacle's own durations (as KnightCavalryCharge floors them) and its sample on the shared clock.
            float idle = Mathf.Max(0f, knight.IdleDuration), warn = Mathf.Max(.05f, knight.WarningDuration),
                  leap = Mathf.Max(.05f, knight.LeapDuration), land = Mathf.Max(.05f, knight.LandingPauseDuration),
                  windup = Mathf.Max(.05f, knight.ChargeWindupDuration), charge = Mathf.Max(.05f, knight.ChargeDuration);
            var sample = knight.Evaluate(ObstacleContext.PeriodicTime(ObstacleClock.Now + knight.phaseOffset, knight.StartDelay,
                                                                    knight.CycleDuration, knight.Repeat));
            bool running = knight.Active && (knight.context == null || knight.context.Running) && sample.cycle >= 0;
            float age = running ? sample.phase - idle : -1f;
            // R565: the wrapped clock keeps the cycle number at 0, so a new run is also seen by the age going back.
            if (sample.cycle != lastCycle || age < runAge - .5f) { lastCycle = sample.cycle; fired = 0; }
            runAge = age;
            Sound(age, sample.cycle, warn + leap, warn + leap + land);

            float leapAt = warn, landAt = warn + leap, windupAt = landAt + land, chargeAt = windupAt + windup;
            var root = knight.KnightRoot.transform;
            var landing = knight.LandingPoint.position;
            var start = knight.StartPoint.position;
            var lane = Vector3.ProjectOnPlane(knight.ChargeEndPoint.position - landing, Vector3.up);
            var dir = lane.sqrMagnitude > 1e-6f ? lane.normalized : transform.forward;
            float dt = Time.deltaTime;

            Pose(age, leapAt, landAt, windupAt, chargeAt, charge);
            Shadow(age, leapAt, landAt, windupAt, root.position, landing, start.y);
            Fire(age, leapAt, landAt, knight.CycleDuration - idle);   // R567: the leap burns, the floor after it
            if (age < 0f) return;

            // warning: grit off the balcony lip, more as the leap nears
            if (age > leapAt - .6f && age < leapAt + .3f)
                Emit(ref gritDebt, 22f * dt, () => Grit(start + dir * 1.1f, dir));
            // push-off
            if (Beat(0, age, leapAt + PushOff)) PushedOff(start, dir);
            // landing: the four hooves round the pedestal
            for (int i = 0; i < Hooves.Length; i++)
                if (Beat(1 + i, age, landAt + Hooves[i])) Hoof(root, Corner(i), dir, i == 0 ? 1f : .55f);
            // windup: the paws at its front
            for (int i = 0; i < Paws.Length; i++)
                if (Beat(5 + i, age, windupAt + Paws[i])) Paw(root, dir, i);
            // charge: a beat of the gallop, alternating back corners
            for (int i = 0; i < Gallop.Length; i++)
                if (Beat(7 + i, age, chargeAt + Gallop[i])) Stride(root, dir, i);
            // the stop: sparks and chips off the front edge as it skids
            float c = age - chargeAt;
            if (c >= SkidFrom && c < charge)
                Emit(ref skidDebt, 80f * dt, () => Skid(root, dir));
        }

        // True once, the frame a beat's time is passed (not when the run is first seen well after it).
        bool Beat(int bit, float age, float at)
        {
            int mask = 1 << bit;
            if ((fired & mask) != 0 || age < at) return false;
            fired |= mask;
            return age < at + BeatLate;
        }

        // ------------------------------------------------------------------ the knight's body

        void Pose(float a, float leapAt, float landAt, float windupAt, float chargeAt, float charge)
        {
            var visual = knight.Visual;
            if (a < 0f || !visual.gameObject.activeInHierarchy)
            {
                visual.localPosition = basePosition;
                visual.localRotation = baseRotation;
                visual.localScale = baseScale;
                return;
            }
            float pitch = 0f;     // degrees, + leans forward (nose down), - leans back (rears)
            float lift = 0f;      // m up
            float side = 0f;      // m across (a tremble)
            float squash = 0f;    // share the body is pressed down
            if (a < leapAt)
            {
                // Gathering: back and down, a tremble in the last 0.35 s.
                float k = Mathf.SmoothStep(0f, 1f, a / leapAt);
                pitch = -7f * k;
                lift = -.05f * k;
                float t = a - (leapAt - .35f);
                if (t > 0f) side = .012f * Mathf.Sin(a * Mathf.PI * 2f * 16f) * (t / .35f);
            }
            else if (a < landAt)
            {
                float p = a - (leapAt + PushOff), u = (a - leapAt) / (landAt - leapAt);
                if (p < 0f) { pitch = -7f; lift = -.05f; }
                else if (p < .1f) { pitch = Mathf.Lerp(-7f, -13f, p / .1f); lift = Mathf.Lerp(-.05f, .04f, p / .1f); }
                else pitch = Mathf.Lerp(-13f, 9f, Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(.35f, .95f, u)));
            }
            else if (a < windupAt + .01f)
            {
                // The landing: pressed flat and springing back, the nose settling.
                float d = a - landAt;
                squash = .13f * Mathf.Exp(-7f * d) * Mathf.Cos(d * 20f);
                pitch = 9f * Mathf.Exp(-12f * d);
            }
            else if (a < chargeAt)
            {
                float d = a - landAt, e = a - windupAt;
                squash = .13f * Mathf.Exp(-7f * d) * Mathf.Cos(d * 20f);
                // The paws rear it twice, then it coils back for the charge.
                float rear = Bump(e, Paws[0], .12f) + Bump(e, Paws[1], .12f) * .8f;
                float coil = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(.18f, chargeAt - windupAt, e));
                pitch = -6f * rear - 5f * coil;
                lift = -.04f * coil;
            }
            else
            {
                float c = a - chargeAt;
                float into = Mathf.Clamp01(c / .12f);
                pitch = Mathf.Lerp(-5f, 7f, into);
                lift = Mathf.Lerp(-.04f, 0f, into);
                // Bobbing on the gallop's beats.
                for (int i = 0; i < Gallop.Length; i++) lift += .06f * Bump(c, Gallop[i], .09f);
                if (c > SkidFrom)
                {
                    float s = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(SkidFrom, Mathf.Min(charge, SkidFrom + .15f), c));
                    pitch = Mathf.Lerp(pitch, -10f, s);
                    lift = Mathf.Lerp(lift, -.03f, s);
                }
            }
            // Tipped about the pedestal's edge it leans over: the back edge to rear, the front to lean forward.
            const float Edge = 1.1f;
            var pivot = new Vector3(0f, 0f, pitch < 0f ? -Edge : Edge);
            var tilt = Quaternion.AngleAxis(pitch, Vector3.right);
            visual.localRotation = baseRotation * tilt;
            visual.localPosition = basePosition + baseRotation * (pivot - tilt * pivot) + new Vector3(side, lift, 0f);
            visual.localScale = Vector3.Scale(baseScale, new Vector3(1f + squash * .4f, 1f - squash, 1f + squash * .4f));
        }

        // A quick rise and fall around at (0 .. 1 .. 0 over width seconds).
        static float Bump(float t, float at, float width)
        {
            float x = (t - at) / width;
            return x <= 0f || x >= 1f ? 0f : Mathf.Sin(x * Mathf.PI);
        }

        // ------------------------------------------------------------------ its shadow on the landing floor

        void MakeShadow()
        {
            var shader = Shader.Find(ShadowShader);
            if (shader == null) return;
            var go = new GameObject("Knight landing shadow");
            go.transform.SetParent(transform, false);
            go.AddComponent<MeshFilter>().sharedMesh = Resources.GetBuiltinResource<Mesh>("Cube.fbx");
            shadowView = go.AddComponent<MeshRenderer>();
            shadowView.sharedMaterial = new Material(shader) { name = "Knight landing shadow" };
            shadowView.shadowCastingMode = ShadowCastingMode.Off;
            shadowView.receiveShadows = false;
            shadowView.lightProbeUsage = LightProbeUsage.Off;
            shadowView.reflectionProbeUsage = ReflectionProbeUsage.Off;
            shadowView.enabled = false;
            shadow = go.transform;
        }

        // Straight under the knight on the landing floor's height while it flies: wide and faint high up, small and dark
        // as it comes down, lying under it through the landing, then gone.
        void Shadow(float a, float leapAt, float landAt, float windupAt, Vector3 at, Vector3 landing, float startY)
        {
            if (shadow == null) return;
            bool on = a >= leapAt && a < windupAt + .2f;
            shadowView.enabled = on;
            if (!on) return;
            float high = Mathf.Max(.5f, startY - landing.y + knight.LeapHeight);
            float low = 1f - Mathf.Clamp01((at.y - landing.y) / high);
            float fade = Mathf.Clamp01((a - leapAt) / .15f) * (a < windupAt ? 1f : 1f - (a - windupAt) / .2f);
            float r = knight.LandingRadius;
            const float Height = 1.6f, Sink = .7f;
            float box = 2.4f * r;
            shadow.position = new Vector3(at.x, landing.y + Height * .5f - Sink, at.z);
            shadow.rotation = Quaternion.identity;
            shadow.localScale = new Vector3(box, Height, box);
            shadowView.GetPropertyBlock(block);
            block.SetFloat(RadiusId, Mathf.Lerp(1.1f * r, .62f * r, low));
            block.SetFloat(DarknessId, Mathf.Lerp(.12f, .62f, low * low) * fade);
            block.SetFloat(BoxId, box);
            shadowView.SetPropertyBlock(block);
            block.Clear();
        }

        // ------------------------------------------------------------------ the beats

        // A corner of the pedestal on the floor (0 front right, 1 back left, 2 front left, 3 back right: the order the
        // hooves come down in).
        static Vector3 Corner(int i)
        {
            float x = i == 0 || i == 3 ? .7f : -.7f, z = i == 0 || i == 2 ? .75f : -.75f;
            return new Vector3(x, .06f, z);
        }

        void Grit(Vector3 lip, Vector3 dir)
        {
            var across = Vector3.Cross(Vector3.up, dir);
            var at = lip + across * Random.Range(-1f, 1f) + Vector3.up * .05f;
            Chip(Random.value < .6f ? grout : marble, at, dir * Random.Range(.2f, .6f) + Vector3.up * Random.Range(0f, .3f), .02f, .045f);
        }

        void PushedOff(Vector3 start, Vector3 dir)
        {
            var across = Vector3.Cross(Vector3.up, dir);
            for (int i = 0; i < 16; i++)
            {
                var at = start - dir * .6f + across * Random.Range(-.8f, .8f) + Vector3.up * .08f;
                var v = -dir * Random.Range(1.5f, 3.5f) + across * Random.Range(-1.2f, 1.2f) + Vector3.up * Random.Range(1.5f, 3.2f);
                Chip(i % 3 == 0 ? grout : marble, at, v, .03f, .07f);
            }
            for (int i = 0; i < 8; i++)
                Spark(start - dir * .7f + across * Random.Range(-.6f, .6f) + Vector3.up * .05f,
                      -dir * Random.Range(3f, 6f) + across * Random.Range(-2f, 2f) + Vector3.up * Random.Range(.5f, 2f));
        }

        void Hoof(Transform root, Vector3 corner, Vector3 dir, float weight)
        {
            var at = root.TransformPoint(corner);
            var outward = Vector3.ProjectOnPlane(at - root.position, Vector3.up).normalized;
            int chips = Mathf.RoundToInt(12 * weight), lights = Mathf.RoundToInt(10 * weight);
            for (int i = 0; i < chips; i++)
            {
                var v = outward * Random.Range(1.5f, 4.5f) + Random.insideUnitSphere * 1.2f + Vector3.up * Random.Range(2f, 4.5f);
                Chip(i % 3 == 0 ? grout : marble, at + Random.insideUnitSphere * .12f, v, .03f, .08f);
            }
            for (int i = 0; i < lights; i++)
            {
                var v = (outward + Random.insideUnitSphere * .6f).normalized * Random.Range(3f, 7f);
                v.y = Mathf.Abs(v.y) * .5f + Random.Range(.3f, 1.5f);
                Spark(at, v);
            }
            Shaker.Shake(at, weight >= 1f ? .11f : .05f, weight >= 1f ? .4f : .2f, 20f);
        }

        void Paw(Transform root, Vector3 dir, int i)
        {
            var at = root.TransformPoint(new Vector3(i == 0 ? .45f : -.45f, .05f, 1.05f));
            for (int s = 0; s < 12; s++)
                Spark(at, dir * Random.Range(2.5f, 5.5f) + Vector3.Cross(Vector3.up, dir) * Random.Range(-1.5f, 1.5f)
                          + Vector3.up * Random.Range(.2f, 1.2f));
            for (int c = 0; c < 4; c++)
                Chip(marble, at, dir * Random.Range(1f, 2.5f) + Vector3.up * Random.Range(1f, 2.2f) + Random.insideUnitSphere * .5f, .025f, .05f);
            Shaker.Shake(at, .025f, .15f, 14f);
        }

        void Stride(Transform root, Vector3 dir, int i)
        {
            var at = root.TransformPoint(new Vector3(i % 2 == 0 ? .6f : -.6f, .05f, -.9f));
            for (int c = 0; c < 4; c++)
                Chip(c % 2 == 0 ? grout : marble, at, -dir * Random.Range(1.5f, 3.5f) + Vector3.up * Random.Range(1f, 2.5f)
                                                     + Random.insideUnitSphere * .5f, .025f, .06f);
            for (int s = 0; s < 4; s++)
                Spark(at, -dir * Random.Range(2f, 5f) + Vector3.up * Random.Range(.2f, 1f) + Random.insideUnitSphere * .8f);
            Shaker.Shake(at, .03f, .12f, 16f);
        }

        void Skid(Transform root, Vector3 dir)
        {
            var at = root.TransformPoint(new Vector3(Random.Range(-.75f, .75f), .04f, 1.05f));
            if (Random.value < .75f)
                Spark(at, dir * Random.Range(4f, 8f) + Vector3.Cross(Vector3.up, dir) * Random.Range(-1.2f, 1.2f)
                          + Vector3.up * Random.Range(.1f, .9f));
            else
                Chip(marble, at, dir * Random.Range(2f, 4f) + Vector3.up * Random.Range(.6f, 1.8f), .025f, .05f);
        }

        // ------------------------------------------------------------------ chips and sparks

        static void Chip(ParticleSystem ps, Vector3 at, Vector3 velocity, float small, float big)
        {
            float s = Random.Range(small, big);
            ps.Emit(new ParticleSystem.EmitParams
            {
                position = at, velocity = velocity, startLifetime = Random.Range(1.2f, 1.9f),
                startSize3D = new Vector3(s, s * Random.Range(.4f, .9f), s * Random.Range(.6f, 1.2f)),
                rotation3D = new Vector3(Random.Range(0f, 360f), Random.Range(0f, 360f), Random.Range(0f, 360f)),
                angularVelocity3D = Random.insideUnitSphere * 720f, startColor = Color.white
            }, 1);
        }

        void Spark(Vector3 at, Vector3 velocity)
        {
            sparks.Emit(new ParticleSystem.EmitParams
            {
                position = at, velocity = velocity, startLifetime = Random.Range(.22f, .5f),
                startSize = Random.Range(.018f, .034f), startColor = Color.white
            }, 1);
        }

        static void Emit(ref float debt, float count, System.Action one)
        {
            debt += count;
            while (debt >= 1f) { one(); debt -= 1f; }
        }

        // Little lit stone pieces that fall, bounce on the floor and lie a moment.
        ParticleSystem Chips(string name, Color color, float smoothness)
        {
            var ps = System(name, 400, 2.2f);
            var main = ps.main;
            main.startSize3D = true;
            main.startRotation3D = true;
            var rotate = ps.rotationOverLifetime;
            rotate.enabled = true;
            rotate.separateAxes = true;
            var size = ps.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(.8f, 1f), new Keyframe(1f, 0f)));
            Bounce(ps, .25f, .45f);
            var r = ps.GetComponent<ParticleSystemRenderer>();
            r.renderMode = ParticleSystemRenderMode.Mesh;
            r.mesh = Resources.GetBuiltinResource<Mesh>("Cube.fbx");
            r.alignment = ParticleSystemRenderSpace.World;
            r.sharedMaterial = LitMaterials.NewLit(color, smoothness);
            r.shadowCastingMode = ShadowCastingMode.Off;
            ps.Play();
            return ps;
        }

        // Hot iron sparks off the shoes: white-gold, cooling to deep orange, streaking, bouncing once.
        ParticleSystem Sparks()
        {
            var ps = System("Knight shoe sparks", 500, 1.3f);
            var col = ps.colorOverLifetime;
            col.enabled = true;
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(new Color(1f, .93f, .75f), 0f), new GradientColorKey(new Color(1f, .55f, .18f), .5f),
                              new GradientColorKey(new Color(.8f, .25f, .05f), 1f) },
                      new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, .6f), new GradientAlphaKey(0f, 1f) });
            col.color = g;
            var size = ps.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, .3f));
            Bounce(ps, .35f, .2f);
            var r = ps.GetComponent<ParticleSystemRenderer>();
            r.renderMode = ParticleSystemRenderMode.Stretch;
            r.velocityScale = .035f;
            r.lengthScale = 1.5f;
            r.sharedMaterial = LitMaterials.NewAdditiveParticles(dot, new Color(3.2f, 1.9f, .7f));
            r.shadowCastingMode = ShadowCastingMode.Off;
            r.receiveShadows = false;
            ps.Play();
            return ps;
        }

        ParticleSystem System(string name, int max, float gravity)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.playOnAwake = false;
            main.loop = false;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startSpeed = 0f;
            main.gravityModifier = gravity;
            main.maxParticles = max;
            var emission = ps.emission;
            emission.rateOverTime = 0f;
            var shape = ps.shape;
            shape.enabled = false;
            return ps;
        }

        // They land on the hall's floor and stairs (world colliders; pieces and players do not catch them).
        static void Bounce(ParticleSystem ps, float bounce, float dampen)
        {
            var hit = ps.collision;
            hit.enabled = true;
            hit.type = ParticleSystemCollisionType.World;
            hit.mode = ParticleSystemCollisionMode.Collision3D;
            hit.quality = ParticleSystemCollisionQuality.Medium;
            hit.bounce = bounce;
            hit.dampen = dampen;
            hit.radiusScale = .5f;
            hit.enableDynamicColliders = false;
        }

        static void Hide(GameObject mark)
        {
            if (mark == null) return;
            foreach (var r in mark.GetComponentsInChildren<Renderer>(true)) r.enabled = false;
        }

        static Texture2D SoftDot(int n)
        {
            var texture = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, name = "Knight warn dot" };
            var pixels = new Color32[n * n];
            for (int j = 0; j < n; j++)
                for (int i = 0; i < n; i++)
                {
                    float x = (i + .5f) / n * 2f - 1f, y = (j + .5f) / n * 2f - 1f;
                    float v = Mathf.Pow(Mathf.Clamp01(1f - Mathf.Sqrt(x * x + y * y)), 2f);
                    byte b = (byte)(v * 255f);
                    pixels[j * n + i] = new Color32(b, b, b, b);
                }
            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            return texture;
        }

        // ------------------------------------------------------------------ the view shaking

        // The game camera shaken only while it draws (moved before, put back after), so the camera's own follow never
        // sees it. Settings → 화면 흔들림 (GameSettings.ShakeScale) scales it; it fades out with distance.
        static class Shaker
        {
            static bool hooked;
            static float amplitude, until, length;
            static Camera moved;
            static Vector3 offset;

            [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
            static void Reset()
            {
                hooked = false;
                amplitude = until = length = 0f;
                moved = null;
            }

            public static void Hook()
            {
                if (hooked) return;
                hooked = true;
                RenderPipelineManager.beginCameraRendering += Before;
                RenderPipelineManager.endCameraRendering += After;
            }

            public static void Shake(Vector3 at, float metres, float seconds, float reach)
            {
                var cam = Camera.main;
                if (cam == null) return;
                float near = 1f - Mathf.Clamp01(Vector3.Distance(cam.transform.position, at) / reach);
                metres *= near * near * GameSettings.ShakeScale;
                if (metres <= 0f) return;
                float now = Left();
                if (metres < now) return;
                amplitude = metres;
                length = seconds;
                until = Time.time + seconds;
            }

            static float Left() => length > 0f ? amplitude * Mathf.Clamp01((until - Time.time) / length) : 0f;

            static void Before(ScriptableRenderContext context, Camera cam)
            {
                if (cam != Camera.main || cam.cameraType != CameraType.Game) return;
                float a = Left();
                if (a <= 0f) return;
                float t = Time.time * 40f;
                offset = new Vector3(Mathf.PerlinNoise(t, .3f) - .5f, Mathf.PerlinNoise(.7f, t) - .5f, 0f) * (2f * a);
                offset = cam.transform.rotation * offset;
                cam.transform.position += offset;
                moved = cam;
            }

            static void After(ScriptableRenderContext context, Camera cam)
            {
                if (cam != moved) return;
                cam.transform.position -= offset;
                moved = null;
            }
        }
    }
}
