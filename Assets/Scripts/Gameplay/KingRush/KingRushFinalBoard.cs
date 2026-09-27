using ChessFight.Network;
using UnityEngine;

namespace ChessFight.Gameplay
{
    public sealed class KingRushFinalBoard : MonoBehaviour
    {
        public Transform[] tiles;
        public Transform[] islands;
        public Transform crown;
        public GameObject approaches;
        public Bounds arena, throne, dais;
        Vector3[] origins;
        Collider[] solids;
        Renderer[] visuals;
        MaterialPropertyBlock tint;
        public int Remaining { get; private set; } = 64;
        public int Warnings { get; private set; }
        void Awake()
        {
            origins = new Vector3[64]; solids = new Collider[64]; visuals = new Renderer[64];
            tint = new MaterialPropertyBlock();
            for (int i = 0; i < 64; i++)
            { origins[i] = tiles[i].position; solids[i] = tiles[i].GetComponent<Collider>(); visuals[i] = tiles[i].GetComponent<Renderer>(); }
        }
        public void Sample(KingRushFinalRules rules, double now)
        {
            double elapsed = rules.Started ? rules.Elapsed(now) : -1;
            if (approaches != null) approaches.SetActive(elapsed < 140);
            Remaining = Warnings = 0;
            for (int i = 0; i < 64; i++)
            {
                double drop = KingRushFinalRules.TileFallsAt(i % 8, i / 8);
                bool alive = elapsed < drop, warn = alive && elapsed >= drop - 3;
                if (alive) Remaining++; if (warn) Warnings++;
                solids[i].enabled = alive;
                float fall = alive ? 0 : Mathf.Min(15, (float)((elapsed - drop) * (elapsed - drop) * 5));
                tiles[i].position = origins[i] + Vector3.down * fall + (warn ? Vector3.up * Mathf.Sin((float)elapsed * 40) * .035f : Vector3.zero);
                tint.Clear(); if (warn) tint.SetColor("_Color", Color.Lerp(Color.red, Color.yellow, .5f + .5f * Mathf.Sin((float)elapsed * 12)));
                visuals[i].SetPropertyBlock(tint);
            }
        }
        public Vector3 ReentryTarget(Vector3 from, KingRushFinalRules rules, double now)
        {
            // Look beyond the whole flight, so a tile cannot disappear just before landing.
            Vector3 best = throne.center; best.y = dais.min.y;
            float score = float.PositiveInfinity;
            for (int i = 0; i < 64; i++)
            {
                if (rules.Started && KingRushFinalRules.TileFallsAt(i % 8, i / 8) <= rules.Elapsed(now) + 6) continue;
                if (i % 8 >= 3 && i % 8 <= 4 && i / 8 >= 3 && i / 8 <= 4) continue;
                Vector3 candidate = origins[i] + Vector3.up * .3f;
                float distance = (candidate - from).sqrMagnitude;
                if (distance < score) { score = distance; best = candidate; }
            }
            return best;
        }
    }
}
