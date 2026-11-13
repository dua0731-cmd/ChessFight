using ChessFight.Game;
using ChessFight.Gameplay;
using ChessFight.ProtectKing;
using UnityEngine;

namespace ChessFight.PawnRush
{
    // R354 (승규 11-04 "이 부분 예고 동작도 vfx 해줘", the royal balcony's guard knights): a knight charge's warnings on the
    // obstacle's own clock, in place of the kit's flat landing disc and charge strip (their renderers off; the obstacle
    // still toggles them). R374 (승규 11-04, a shot of the light curtains round the landing and along the lane: "이 vfx 좀
    // 마음에 안들어 다른 기술로해줘" - after the floor decal had gone the same way): no decal and no curtain, the warning
    // is the knight's own rehearsal and particles in the air:
    //   ghost    a glowing gold ghost of the knight plays its move ahead of it - leaps from the alcove onto the landing
    //            point, then dashes down the lane and fades - during the warning, so the place and the line read at once
    //   landing  embers rise off the landing circle and dust swirls in to its middle, thicker as the landing nears; when it
    //            lands, a ring of dust thrown out along the floor and sparks
    //   lane     from the landing, wind streaks shoot down the lane its width across, ever thicker until the charge; while it
    //            charges, dust kicked up behind it
    // Every frame the obstacle's own sample (Evaluate on the shared clock) gives the time since its warning started, so
    // the ghost and the moments are the same on every PC; the particles' scatter is each PC's own.
    [DisallowMultipleComponent]
    public sealed class CavalryWarnFx : MonoBehaviour
    {
        static readonly Color Gold = new Color(1f, .74f, .32f), Ember = new Color(1f, .45f, .12f), Dust = new Color(.95f, .86f, .7f);
        static Texture2D dot;

        KnightCavalryCharge knight;
        Transform ghost;
        Renderer[] ghostViews;
        MaterialPropertyBlock block;
        ParticleSystem embers, swirl, dust, sparks, streaks;
        int lastCycle = int.MinValue;
        bool landedBurst;
        float emberDebt, swirlDebt, streakDebt, dustDebt;

        void Start()
        {
            knight = GetComponent<KnightCavalryCharge>();
            if (knight == null || knight.LandingPoint == null || knight.ChargeEndPoint == null || knight.StartPoint == null)
            {
                enabled = false;
                return;
            }
            block = new MaterialPropertyBlock();
            Hide(knight.LandingWarning);
            Hide(knight.ChargeWarning);
            MakeGhost();
            if (dot == null) dot = SoftDot(64);
            embers = Particles("Knight warn embers", new Color(2.6f, 1.6f, .55f), true, -.35f);
            swirl = Particles("Knight warn swirl", new Color(1.4f, 1.2f, .9f), false, 0f);
            dust = Particles("Knight warn dust", new Color(1.1f, 1f, .85f), false, 0f);
            sparks = Particles("Knight warn sparks", new Color(3f, 1.9f, .7f), true, 1.6f);
            streaks = Particles("Knight warn streaks", new Color(3f, 1.6f, .6f), true, 0f);
            streaks.GetComponent<ParticleSystemRenderer>().velocityScale = .09f;
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
            if (sample.cycle != lastCycle) { lastCycle = sample.cycle; landedBurst = false; }

            Vector3 start = knight.StartPoint.position, landAt = knight.LandingPoint.position, end = knight.ChargeEndPoint.position;
            var down = Vector3.ProjectOnPlane(end - landAt, Vector3.up);
            var dir = down.sqrMagnitude > 1e-6f ? down.normalized : transform.forward;
            var across = Vector3.Cross(Vector3.up, dir);
            float width = knight.BodyCollider != null ? knight.BodyCollider.size.x : 1.4f;
            float dt = Time.deltaTime;

            Ghost(age, warn, start, landAt, end);

            // Landing: embers and an inward swirl from the warning to the landing, thickening; the burst as it lands.
            float landTime = warn + leap;
            if (age >= 0f && age < landTime)
            {
                float k = Mathf.Clamp01(age / landTime);
                Emit(ref emberDebt, Mathf.Lerp(12f, 70f, k * k) * dt, () => Rise(landAt));
                Emit(ref swirlDebt, Mathf.Lerp(6f, 40f, k) * dt, () => Swirl(landAt));
            }
            if (age >= landTime && !landedBurst && age < landTime + .5f)
            {
                landedBurst = true;
                Landed(landAt);
            }

            // Lane: wind streaks from the landing to the charge's end, ever thicker until it charges; dust behind it.
            float chargeAt = warn + leap + land + windup;
            if (age >= landTime && age < chargeAt + charge * .85f)
            {
                float k = Mathf.Clamp01((age - landTime) / Mathf.Max(.05f, chargeAt - landTime));
                float rate = age < chargeAt ? Mathf.Lerp(20f, 110f, k) : 60f;
                float length = down.magnitude;
                Emit(ref streakDebt, rate * dt, () => Streak(landAt, dir, across, width, length));
            }
            if (age >= chargeAt && age < chargeAt + charge)
                Emit(ref dustDebt, 45f * dt, () => KickedUp(knight.KnightRoot != null ? knight.KnightRoot.position : landAt, dir));
        }

        // ------------------------------------------------------------------ the ghost's rehearsal

        void Ghost(float age, float warn, Vector3 start, Vector3 landAt, Vector3 end)
        {
            if (ghost == null) return;
            // Inside the warning: appear at the alcove, leap (0.12 .. 0.55), dash down the lane (0.6 .. 0.9), fade.
            float u = warn > 0f ? age / warn : -1f;
            if (u < 0f || u > 1f) { Show(false, 0f); return; }
            Vector3 at;
            if (u < .12f) at = start;
            else if (u < .55f)
            {
                float t = Mathf.SmoothStep(0f, 1f, (u - .12f) / .43f);
                at = Vector3.Lerp(start, landAt, t) + Vector3.up * (4f * Mathf.Max(0f, knight.LeapHeight) * t * (1f - t));
            }
            else if (u < .6f) at = landAt;
            else at = Vector3.Lerp(landAt, end, Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((u - .6f) / .3f)));
            float alpha = Mathf.Clamp01(u / .1f) * Mathf.Clamp01((1f - u) / .12f) * .55f;
            ghost.position = at;
            Show(true, alpha);
        }

        void MakeGhost()
        {
            var source = knight.Visual != null ? knight.Visual : knight.KnightRoot != null ? knight.KnightRoot.transform : null;
            if (source == null) return;
            var model = source.Find("Knight model");
            var copy = Instantiate(model != null ? model.gameObject : source.gameObject);
            copy.name = "Knight warn ghost";
            foreach (var c in copy.GetComponentsInChildren<Collider>(true)) Destroy(c);
            foreach (var b in copy.GetComponentsInChildren<Rigidbody>(true)) Destroy(b);
            foreach (var m in copy.GetComponentsInChildren<MonoBehaviour>(true)) Destroy(m);
            ghost = new GameObject("Knight warn ghost").transform;
            ghost.SetParent(transform, false);
            ghost.rotation = (knight.KnightRoot != null ? knight.KnightRoot.transform : source).rotation;
            // The copy keeps its place under the knight's root: offset it so the ghost's origin is the root.
            var root = knight.KnightRoot != null ? knight.KnightRoot.transform : source;
            var pos = (model != null ? model : source).position - root.position;
            var rot = (model != null ? model : source).rotation;
            var scale = (model != null ? model : source).lossyScale;
            copy.transform.SetParent(ghost, false);
            copy.transform.position = ghost.position + pos;
            copy.transform.rotation = rot;
            var ls = ghost.lossyScale;
            copy.transform.localScale = new Vector3(scale.x / ls.x, scale.y / ls.y, scale.z / ls.z);
            var glow = LitMaterials.NewAdditiveParticles(Texture2D.whiteTexture, new Color(1.6f, 1.15f, .5f, 1f));
            if (glow != null) glow.name = "Knight warn ghost";
            ghostViews = copy.GetComponentsInChildren<Renderer>(true);
            foreach (var r in ghostViews)
            {
                var slots = r.sharedMaterials;
                for (int i = 0; i < slots.Length; i++) slots[i] = glow;
                r.sharedMaterials = slots;
                r.enabled = false;
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                r.receiveShadows = false;
            }
        }

        void Show(bool on, float alpha)
        {
            if (ghostViews == null) return;
            foreach (var r in ghostViews)
            {
                if (r.enabled != on) r.enabled = on;
                if (!on) continue;
                r.GetPropertyBlock(block);
                LitMaterials.SetColor(block, new Color(1.6f, 1.15f, .5f, alpha));
                r.SetPropertyBlock(block);
                block.Clear();
            }
        }

        // ------------------------------------------------------------------ particles

        void Rise(Vector3 centre)
        {
            float a = Random.value * Mathf.PI * 2f, r = knight.LandingRadius * Random.Range(.85f, 1.02f);
            var e = new ParticleSystem.EmitParams
            {
                position = centre + new Vector3(Mathf.Cos(a) * r, .05f, Mathf.Sin(a) * r),
                velocity = new Vector3(0f, Random.Range(.8f, 2.2f), 0f) + Random.insideUnitSphere * .2f,
                startLifetime = Random.Range(.6f, 1.1f), startSize = Random.Range(.05f, .1f), startColor = Color.white
            };
            embers.Emit(e, 1);
        }

        void Swirl(Vector3 centre)
        {
            float a = Random.value * Mathf.PI * 2f, r = knight.LandingRadius * Random.Range(.7f, 1f);
            var radial = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
            var tangent = Vector3.Cross(Vector3.up, radial);
            var e = new ParticleSystem.EmitParams
            {
                position = centre + radial * r + Vector3.up * Random.Range(.05f, .35f),
                velocity = tangent * Random.Range(1.5f, 2.6f) - radial * Random.Range(1.2f, 2f),
                startLifetime = Random.Range(.6f, .9f), startSize = Random.Range(.25f, .5f), startColor = new Color(1f, 1f, 1f, .45f)
            };
            swirl.Emit(e, 1);
        }

        void Landed(Vector3 centre)
        {
            for (int i = 0; i < 48; i++)
            {
                float a = i / 48f * Mathf.PI * 2f + Random.Range(-.05f, .05f);
                var radial = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                dust.Emit(new ParticleSystem.EmitParams
                {
                    position = centre + radial * .4f + Vector3.up * .15f,
                    velocity = radial * Random.Range(4f, 7f) + Vector3.up * Random.Range(.2f, .8f),
                    startLifetime = Random.Range(.5f, .9f), startSize = Random.Range(.5f, .9f), startColor = new Color(1f, 1f, 1f, .6f)
                }, 1);
            }
            for (int i = 0; i < 30; i++)
            {
                var d = Random.onUnitSphere;
                d.y = Mathf.Abs(d.y) + .4f;
                sparks.Emit(new ParticleSystem.EmitParams
                {
                    position = centre + Vector3.up * .2f, velocity = d.normalized * Random.Range(4f, 8f),
                    startLifetime = Random.Range(.4f, .8f), startSize = Random.Range(.05f, .1f), startColor = Color.white
                }, 1);
            }
        }

        void Streak(Vector3 from, Vector3 dir, Vector3 across, float width, float length)
        {
            float speed = Random.Range(12f, 18f);
            streaks.Emit(new ParticleSystem.EmitParams
            {
                position = from + across * Random.Range(-width * .5f, width * .5f) + Vector3.up * Random.Range(.08f, 1.1f) + dir * Random.Range(0f, .6f),
                velocity = dir * speed, startLifetime = Mathf.Max(.05f, length / speed), startSize = Random.Range(.05f, .09f),
                startColor = Color.white
            }, 1);
        }

        void KickedUp(Vector3 at, Vector3 dir)
        {
            dust.Emit(new ParticleSystem.EmitParams
            {
                position = new Vector3(at.x, knight.LandingPoint.position.y + .15f, at.z) + Random.insideUnitSphere * .5f,
                velocity = -dir * Random.Range(.5f, 1.5f) + Vector3.up * Random.Range(.3f, 1f) + Random.insideUnitSphere * .5f,
                startLifetime = Random.Range(.5f, .8f), startSize = Random.Range(.4f, .7f), startColor = new Color(1f, 1f, 1f, .5f)
            }, 1);
        }

        static void Emit(ref float debt, float count, System.Action one)
        {
            debt += count;
            while (debt >= 1f) { one(); debt -= 1f; }
        }

        ParticleSystem Particles(string name, Color tint, bool stretch, float gravity)
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
            main.maxParticles = 600;
            var emission = ps.emission;
            emission.rateOverTime = 0f;
            var shape = ps.shape;
            shape.enabled = false;
            var col = ps.colorOverLifetime;
            col.enabled = true;
            var g = new Gradient();
            var to = name.Contains("dust") || name.Contains("swirl") ? Dust : name.Contains("embers") ? Ember : Gold;
            g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(to, 1f) },
                      new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, .15f), new GradientAlphaKey(0f, 1f) });
            col.color = g;
            var size = ps.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, name.Contains("dust") || name.Contains("swirl")
                ? AnimationCurve.Linear(0f, .6f, 1f, 1.4f) : AnimationCurve.Linear(0f, 1f, 1f, .2f));
            var limit = ps.limitVelocityOverLifetime;
            if (name.Contains("dust"))
            {
                limit.enabled = true;
                limit.limit = 1f;
                limit.dampen = .12f;
            }
            var r = go.GetComponent<ParticleSystemRenderer>();
            r.sharedMaterial = LitMaterials.NewAdditiveParticles(dot, tint);
            r.renderMode = stretch ? ParticleSystemRenderMode.Stretch : ParticleSystemRenderMode.Billboard;
            r.velocityScale = .1f;
            r.lengthScale = 1f;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            ps.Play();
            return ps;
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
    }
}
