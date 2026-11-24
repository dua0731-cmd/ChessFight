using UnityEngine;

namespace ChessFight.PawnRush
{
    // R769 (승규 11-22, "정원 전체 분위기 소리 넣어줘"): the royal water garden's bed of sound - a soft breeze in the trees and
    // hedges, songbirds and a far dove, a faint trickle of distant fountains (GardenAmbience.wav, one long loop). Heard
    // the same all over the garden (not from a spot: a 2D source), under every other sound. It stays inside the
    // garden as every course sound does (SectionSoundGate: the source is kept on the garden's ground, at the point
    // nearest the listener, so it always counts as the garden's; out of the garden it fades out over the gate's few
    // metres). Fades in over FadeIn s when the garden wakes. GardenPlazaSound adds this on waking.
    public sealed class GardenAmbience : MonoBehaviour
    {
        const string Clip = "PawnRushGardenSfx/GardenAmbience";
        const float Gain = .45f, FadeIn = 2f;   // the file sits at RMS -28 (its chirps kept whole)
        // The garden's ground in its own frame (RoyalWaterGardenBuilder: the start yard to the gate yard).
        const float X0 = -46f, X1 = 46f, Z0 = -60f, Z1 = 72f, Y = 4f;

        AudioSource source;
        Transform at;
        float age;
        static AudioListener listener;

        void Start()
        {
            var clip = Resources.Load<AudioClip>(Clip);
            if (clip == null)
            {
                Debug.LogWarning("[정원] 소리 파일이 없습니다: Resources/" + Clip);
                return;
            }
            at = new GameObject("Garden ambience").transform;
            at.SetParent(transform, false);
            at.localPosition = new Vector3(0f, Y, 0f);
            source = at.gameObject.AddComponent<AudioSource>();
            source.clip = clip;
            source.loop = true;
            source.playOnAwake = false;
            source.spatialBlend = 0f;
            source.dopplerLevel = 0f;
            source.volume = 0f;
            source.time = Random.value * clip.length;
            SectionSoundGate.Gate(source);
            source.Play();
        }

        void Update()
        {
            if (source == null) return;
            if (listener == null || !listener.isActiveAndEnabled) listener = FindAnyObjectByType<AudioListener>();
            if (listener != null)
            {
                var l = transform.InverseTransformPoint(listener.transform.position);
                at.localPosition = new Vector3(Mathf.Clamp(l.x, X0, X1), Y, Mathf.Clamp(l.z, Z0, Z1));
            }
            age += Time.deltaTime;
            source.volume = Gain * Mathf.Clamp01(age / FadeIn);
        }
    }
}
