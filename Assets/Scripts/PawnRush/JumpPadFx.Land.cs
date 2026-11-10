using System.Collections.Generic;
using ChessFight.Game;
using ChessFight.ProtectKing;
using UnityEngine;

namespace ChessFight.PawnRush
{
    // R562 (승규 11-10 "발판을 밟고 날아갈 때 C1을 넣어주고 착지 성공 할 때 B2를 넣어줘 그리고 착지할 때 착지 애니메이션을
    // 넣어줘 멋있게"): the jump pad's sounds and a landing burst.
    //   launch  HallJumpLaunch (mockup C1, a deep "부웅" rising) at the pad, with the launch burst (Burst)
    //   land    when a piece the pad threw comes down on high ground away from the pad - the far landing, not a fall
    //           back to the floor below - HallJumpLand (mockup B2, a "푸슉" air blast) where it lands and a short,
    //           punchy burst in the pad's own gold square language: a white flash, a hot inner square snapping out in
    //           0.12 s and a wide gold one in 0.3 s, sparks skidding off its four sides, a few light streaks thrown up
    //           and the square's glow left on the floor for half a second. No dust.
    // Every PC follows each piece it saw launched (no packet), as Watch does for the launch.
    public sealed partial class JumpPadFx
    {
        const string HallSfx = "PawnRushHallSfx/";
        const float LaunchVolume = .8f, LandVolume = .9f;
        const float AirMin = .3f, AirMax = 4f;            // s: no landing before / give up after
        const float HighEnough = .8f;                      // m over the deck that counts as the far side, not a fall
        const float SnapLife = .12f, WaveLife = .3f, GlowLife = .6f;

        static AudioClip launchClip, landClip;
        static bool clipsLooked;

        sealed class Flight
        {
            public ImportedObstacleActor actor;
            public float at;
            public bool airborne;
        }

        readonly List<Flight> flights = new List<Flight>();
        readonly RaycastHit[] floorHits = new RaycastHit[16];
        AudioSource launchVoice, landVoice;
        Transform landing, snap, wave, footprint;
        Renderer snapRenderer, waveRenderer, footprintRenderer;
        Light landLamp;
        float landedAt = -10f;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            launchClip = landClip = null;
            clipsLooked = false;
        }

        void StartLanding()
        {
            if (!clipsLooked)
            {
                clipsLooked = true;
                launchClip = Clip("HallJumpLaunch");
                landClip = Clip("HallJumpLand");
            }
            launchVoice = Voice(transform, "Jump pad launch sound", Vector3.up * Deck, LaunchVolume);

            // The landing's parts live on one object moved to each landing (world space, not under the pad).
            landing = new GameObject("Jump pad landing").transform;
            landing.SetParent(transform, false);
            landVoice = Voice(landing, "Jump pad land sound", Vector3.up * .3f, LandVolume);
            snap = LandingSquare("Landing snap", 2.2f);
            wave = LandingSquare("Landing wave", 3.4f);
            footprint = LandingSquare("Landing glow", 1.5f);
            snapRenderer = snap.GetComponent<Renderer>();
            waveRenderer = wave.GetComponent<Renderer>();
            footprintRenderer = footprint.GetComponent<Renderer>();
            snapRenderer.enabled = waveRenderer.enabled = footprintRenderer.enabled = false;
            landLamp = new GameObject("Landing light").AddComponent<Light>();
            landLamp.transform.SetParent(landing, false);
            landLamp.transform.localPosition = Vector3.up * .6f;
            landLamp.type = LightType.Point;
            landLamp.color = new Color(1f, .78f, .45f);
            landLamp.range = 7f;
            landLamp.shadows = LightShadows.None;
            landLamp.intensity = 0f;
            landLamp.enabled = false;
        }

        static AudioClip Clip(string name)
        {
            var clip = Resources.Load<AudioClip>(HallSfx + name);
            if (clip == null) Debug.LogWarning("[대계단 홀] 소리 파일이 없어 재생하지 않습니다: Resources/" + HallSfx + name);
            return clip;
        }

        static AudioSource Voice(Transform parent, string name, Vector3 at, float volume)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = at;
            var voice = go.AddComponent<AudioSource>();
            voice.playOnAwake = false;
            voice.spatialBlend = 1f;
            voice.rolloffMode = AudioRolloffMode.Linear;
            voice.minDistance = 3f;
            voice.maxDistance = 30f;
            voice.dopplerLevel = 0f;
            voice.volume = volume;
            RollingSpawner.Ring(go);
            SectionSoundGate.Gate(voice);   // heard only in the hall
            return voice;
        }

        Transform LandingSquare(string name, float size)
        {
            var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            Destroy(quad.GetComponent<Collider>());
            quad.name = name;
            quad.transform.SetParent(landing, false);
            quad.transform.localPosition = Vector3.up * .03f;
            quad.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            quad.transform.localScale = Vector3.one * size;
            var r = quad.GetComponent<Renderer>();
            r.sharedMaterial = LitMaterials.NewAdditiveParticles(square, new Color(2.2f, 1.6f, .8f));
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            return quad.transform;
        }

        void PlayLaunch()
        {
            if (launchClip == null || launchVoice == null) return;
            launchVoice.pitch = Random.Range(.96f, 1.04f);
            launchVoice.PlayOneShot(launchClip);
        }

        void Launched(ImportedObstacleActor actor, float t)
        {
            for (int i = 0; i < flights.Count; i++)
                if (flights[i].actor == actor)
                {
                    flights[i].at = t;
                    flights[i].airborne = false;
                    return;
                }
            flights.Add(new Flight { actor = actor, at = t });
        }

        // Each piece the pad threw: off the ground first, then down again - a landing if it is high and away.
        void Follow(float t)
        {
            for (int i = flights.Count - 1; i >= 0; i--)
            {
                var f = flights[i];
                float age = t - f.at;
                if (f.actor == null || !f.actor.isActiveAndEnabled || age > AirMax)
                {
                    flights.RemoveAt(i);
                    continue;
                }
                bool grounded = f.actor.Grounded;
                if (!f.airborne)
                {
                    if (!grounded) f.airborne = true;
                    continue;
                }
                if (age < AirMin || !grounded) continue;
                flights.RemoveAt(i);
                var at = f.actor.Position;
                var flat = at - transform.position;
                flat.y = 0f;
                float deckY = transform.TransformPoint(Vector3.up * Deck).y;
                if (at.y > deckY + HighEnough && flat.magnitude > Side * .6f) Land(t, Floor(at, f.actor));
            }
            Animate(t);
        }

        // The floor right under the piece (not the piece itself).
        Vector3 Floor(Vector3 at, ImportedObstacleActor actor)
        {
            int n = Physics.RaycastNonAlloc(at + Vector3.up * .3f, Vector3.down, floorHits, 3f, ~0, QueryTriggerInteraction.Ignore);
            float best = float.MaxValue;
            var floor = at;
            var body = actor.gameObject != null ? actor.gameObject.transform : null;
            for (int i = 0; i < n; i++)
            {
                var h = floorHits[i];
                if (body != null && h.collider.transform.IsChildOf(body)) continue;
                if (h.distance < best)
                {
                    best = h.distance;
                    floor = h.point;
                }
            }
            return floor;
        }

        void Land(float t, Vector3 floor)
        {
            landedAt = t;
            landing.position = floor;
            // Square to the pad, as its own glow.
            landing.rotation = Quaternion.Euler(0f, transform.eulerAngles.y, 0f);
            if (landClip != null && landVoice != null)
            {
                landVoice.pitch = Random.Range(.95f, 1.05f);
                landVoice.PlayOneShot(landClip);
            }
            var e = new ParticleSystem.EmitParams();
            var origin = floor + Vector3.up * .06f;
            for (int i = 0; i < 30; i++)
            {
                // Skidding off one of the four sides, low over the floor.
                int edge = Random.Range(0, 4);
                var normal = edge == 0 ? Vector3.right : edge == 1 ? Vector3.left : edge == 2 ? Vector3.forward : Vector3.back;
                var tangent = new Vector3(normal.z, 0f, -normal.x);
                var local = normal * Random.Range(.3f, .7f) + tangent * Random.Range(-.7f, .7f);
                e.position = origin + landing.TransformVector(local);
                e.velocity = landing.TransformDirection(normal) * Random.Range(3f, 7f) + Vector3.up * Random.Range(.8f, 2.6f);
                e.startLifetime = Random.Range(.3f, .6f);
                e.startSize = Random.Range(.04f, .09f);
                e.startColor = Color.white;
                sparks.Emit(e, 1);
            }
            for (int i = 0; i < 8; i++)
            {
                e.position = origin + new Vector3(Random.Range(-.4f, .4f), 0f, Random.Range(-.4f, .4f));
                e.velocity = Vector3.up * Random.Range(4f, 7f) + Random.insideUnitSphere * .6f;
                e.startLifetime = Random.Range(.15f, .26f);
                e.startSize = Random.Range(.06f, .1f);
                streaks.Emit(e, 1);
            }
        }

        void Animate(float t)
        {
            float since = t - landedAt;
            bool on = since >= 0f && since < GlowLife;
            snapRenderer.enabled = on && since < SnapLife;
            waveRenderer.enabled = on && since < WaveLife;
            footprintRenderer.enabled = on;
            landLamp.enabled = on;
            if (!on) return;
            if (snapRenderer.enabled)
            {
                // The hot one: out from a quarter at once, white.
                float u = since / SnapLife;
                snap.localScale = Vector3.one * (2.2f * Mathf.Lerp(.25f, 1f, 1f - (1f - u) * (1f - u)));
                Tint(snapRenderer, new Color(4f, 3.6f, 3f, 1f - u));
            }
            if (waveRenderer.enabled)
            {
                float u = since / WaveLife;
                wave.localScale = Vector3.one * (3.4f * Mathf.Lerp(.3f, 1f, 1f - Mathf.Pow(1f - u, 3f)));
                var c = Color.Lerp(new Color(3f, 2.6f, 1.6f), new Color(3f, 1.9f, .7f), u);
                c.a = Mathf.Clamp01(u / .1f) * (1f - u);
                Tint(waveRenderer, c);
            }
            // The glow left on the floor where it landed, fading.
            float g = 1f - since / GlowLife;
            Tint(footprintRenderer, new Color(2.4f, 1.7f, .75f, .55f * g * g));
            landLamp.intensity = 9f * Mathf.Exp(-since * 12f) + .8f * g;
        }
    }
}
