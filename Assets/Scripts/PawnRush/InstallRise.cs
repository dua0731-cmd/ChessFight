using System.Collections.Generic;
using UnityEngine;

namespace ChessFight.PawnRush
{
    // A newly installed game rises out of the dais (mini-games v1.0 §2.4): 0.5 m under the floor to its place in
    // 0.6 s, easing out with a little overshoot. Its bodies are kinematic while it moves and are given back after
    // (a dynamic one, like D's capstan bars, only where the characters are simulated).
    public sealed class InstallRise : MonoBehaviour
    {
        const float Depth = .5f, Seconds = .6f;

        Vector3 home;
        float age;
        readonly List<Rigidbody> freed = new List<Rigidbody>();

        public static void Play(Transform root)
        {
            if (root == null || root.GetComponent<InstallRise>() != null) return;
            root.gameObject.AddComponent<InstallRise>();
        }

        void Awake()
        {
            home = transform.localPosition;
            foreach (var body in GetComponentsInChildren<Rigidbody>(true))
                if (!body.isKinematic)
                {
                    body.isKinematic = true;
                    freed.Add(body);
                }
            transform.localPosition = home - Vector3.up * Depth;
        }

        void Update()
        {
            age += Time.deltaTime;
            float t = Mathf.Clamp01(age / Seconds);
            // ease out back
            const float c1 = 1.70158f, c3 = c1 + 1f;
            float k = 1f + c3 * Mathf.Pow(t - 1f, 3f) + c1 * Mathf.Pow(t - 1f, 2f);
            transform.localPosition = home - Vector3.up * Depth * (1f - k);
            if (t < 1f) return;
            transform.localPosition = home;
            if (!MiniGameBase.Remote)
                foreach (var body in freed)
                    if (body != null) body.isKinematic = false;
            Destroy(this);
        }
    }
}
