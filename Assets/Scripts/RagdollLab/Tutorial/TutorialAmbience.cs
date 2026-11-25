using ChessFight.Game;
using UnityEngine;

namespace ChessFight.RagdollLab
{
    /// <summary>
    /// The tutorial's ambience (user 11-25, picks 32 and 33; ArtSource/Tutorial/bake_ambience.py): a soft meadow breeze
    /// under everything from the first frame (Resources/TutorialSfx/WindLoop, a 12 s loop woven from the take), and
    /// now and then a burst of distant songbirds (Birds) from somewhere round the courtyard, a little off in pitch and
    /// level each time so it is not heard as one clip. Both under the effects volume. Added by TutorialCourse.Start.
    /// </summary>
    public sealed class TutorialAmbience : MonoBehaviour
    {
        const float WindVolume = .22f, BirdVolume = .35f;
        const float BirdEveryMin = 5f, BirdEveryMax = 13f;
        const float BirdRing = 22f, BirdHeight = 6f;   // m from the yard's middle, where the chirps come from

        AudioSource wind, bird;
        AudioClip birds;
        float birdAt;

        void Start()
        {
            if (Application.isBatchMode) { enabled = false; return; }
            var loop = Resources.Load<AudioClip>("TutorialSfx/WindLoop");
            birds = Resources.Load<AudioClip>("TutorialSfx/Birds");
            if (loop != null)
            {
                wind = gameObject.AddComponent<AudioSource>();
                wind.clip = loop;
                wind.loop = true;
                wind.playOnAwake = false;
                wind.spatialBlend = 0f;
                wind.time = Random.Range(0f, loop.length * .9f);
                wind.volume = WindVolume * GameSettings.Effects;
                wind.Play();
            }
            if (birds != null)
            {
                var go = new GameObject("Birds (sound)");
                go.transform.SetParent(transform, false);
                bird = go.AddComponent<AudioSource>();
                bird.playOnAwake = false;
                bird.spatialBlend = .6f;   // from somewhere, never only in one ear
                bird.rolloffMode = AudioRolloffMode.Linear;
                bird.minDistance = 15f;
                bird.maxDistance = 60f;
                bird.dopplerLevel = 0f;
            }
            birdAt = Time.time + Random.Range(2f, 5f);
        }

        void Update()
        {
            float effects = GameSettings.Effects;
            if (wind != null) wind.volume = WindVolume * effects;
            if (bird == null || Time.time < birdAt) return;
            birdAt = Time.time + Random.Range(BirdEveryMin, BirdEveryMax);
            float a = Random.Range(0f, Mathf.PI * 2f);
            bird.transform.position = new Vector3(Mathf.Cos(a) * BirdRing, BirdHeight, Mathf.Sin(a) * BirdRing);
            bird.pitch = Mathf.Pow(2f, Random.Range(-1.5f, 2f) / 12f);
            bird.PlayOneShot(birds, BirdVolume * Random.Range(.7f, 1f) * effects);
        }
    }
}
