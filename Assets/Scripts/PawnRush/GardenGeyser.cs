using System.Collections.Generic;
using ChessFight.Gameplay;
using ChessFight.Network;
using ChessFight.ProtectKing;
using UnityEngine;

namespace ChessFight.PawnRush
{
    // A water jet on the royal water garden's canal walk (v31 beat 2): its grate bubbles for 0.8 s, then a column of
    // water stands up to 5 m tall for 1 s and throws whoever is in it up and out, knocked down. The eight jets fire one
    // after another (RoyalWaterGardenRules.GeyserState, by `order`), so the wave runs at the pawns coming along the walk.
    // The state is a function of the shared obstacle clock, the same on every PC; the hit is the authority's
    // (ObstacleClock.Authority), once per jet. The water is a picture: nothing to stand on or bump into.
    public sealed class GardenGeyser : MonoBehaviour
    {
        [Tooltip("0 fires first; each next one 0.55 s later.")]
        [SerializeField] int order;
        [SerializeField] Transform column;
        [SerializeField] Transform bubbles;
        [Tooltip("The way the pawns come along: a pawn right over the grate is thrown back against it.")]
        [SerializeField] Vector3 back = Vector3.right;
        [SerializeField] float radius = .9f, height = 5f;
        [SerializeField] float lift = 7f, push = 2.5f, knockdown = 1f;

        readonly Collider[] overlaps = new Collider[48];
        readonly HashSet<ImportedObstacleActor> struck = new HashSet<ImportedObstacleActor>();
        long struckEruption = long.MinValue;
        float columnDiameter = 1.8f, bubblesDiameter = 2f;

        // For the builder: on an inactive object, before it wakes.
        public void Configure(int order, Transform column, Transform bubbles, Vector3 back)
        {
            this.order = order;
            this.column = column;
            this.bubbles = bubbles;
            this.back = back;
        }

        void Awake()
        {
            if (column != null) columnDiameter = column.localScale.x;
            if (bubbles != null) bubblesDiameter = bubbles.localScale.x;
            Draw();
        }

        void Update() => Draw();

        // The column rises in 0.2 s, stands and falls in the last 0.25 s of the jet; the bubbles pulse while it warns.
        void Draw()
        {
            double into;
            var state = RoyalWaterGardenRules.GeyserState(order, ObstacleClock.Now, out into);
            float h = 0f;
            if (state == RoyalWaterGardenRules.Jet.Jet)
            {
                float t = (float)into;
                float jet = (float)RoyalWaterGardenRules.GeyserJet;
                h = t < .2f ? height * Mathf.SmoothStep(0f, 1f, t / .2f)
                  : t > jet - .25f ? height * (1f - Mathf.SmoothStep(0f, 1f, (t - (jet - .25f)) / .25f)) : height;
            }
            if (column != null)
            {
                bool up = h > .02f;
                var r = column.GetComponent<Renderer>();
                if (r != null) r.enabled = up;
                column.localScale = new Vector3(columnDiameter, Mathf.Max(h, .01f) * .5f, columnDiameter);
                column.localPosition = new Vector3(0f, Mathf.Max(h, .01f) * .5f, 0f);
            }
            if (bubbles != null)
            {
                bool warn = state == RoyalWaterGardenRules.Jet.Warn;
                var r = bubbles.GetComponent<Renderer>();
                if (r != null) r.enabled = warn;
                float bob = warn ? .12f + .1f * Mathf.Abs(Mathf.Sin((float)into * 18f)) : .01f;
                bubbles.localScale = new Vector3(bubblesDiameter, bob * .5f, bubblesDiameter);
                bubbles.localPosition = new Vector3(0f, bob * .5f, 0f);
            }
        }

        void FixedUpdate()
        {
            if (!ObstacleClock.Authority) return;
            double now = ObstacleClock.Now, into;
            if (RoyalWaterGardenRules.GeyserState(order, now, out into) != RoyalWaterGardenRules.Jet.Jet || into > RoyalWaterGardenRules.GeyserJet - .25)
                return;
            long eruption = RoyalWaterGardenRules.GeyserCycleIndex(order, now);
            if (eruption != struckEruption) { struck.Clear(); struckEruption = eruption; }
            var foot = transform.position;
            int n = Physics.OverlapCapsuleNonAlloc(foot + Vector3.up * .2f, foot + Vector3.up * 2.2f, radius, overlaps, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < n; i++)
            {
                var actor = ImportedObstacleActor.Find(overlaps[i]);
                if (actor == null || !actor.CanReceiveObstacle || !struck.Add(actor)) continue;
                var flat = Vector3.ProjectOnPlane(actor.Position - foot, Vector3.up);
                var outward = flat.magnitude > .2f ? flat.normalized : transform.TransformDirection(back).normalized;
                actor.AddObstacleHit(outward * push + Vector3.up * lift, knockdown);
                GardenHits.Report(actor.gameObject, "물기둥");
            }
        }

        void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(.3f, .7f, 1f, .5f);
            Gizmos.DrawWireSphere(transform.position + Vector3.up * .2f, radius);
            Gizmos.DrawWireSphere(transform.position + Vector3.up * 2.2f, radius);
        }
    }
}
