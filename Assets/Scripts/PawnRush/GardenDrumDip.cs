using UnityEngine;

namespace ChessFight.PawnRush
{
    // R543 (승규 11-10, "플레이어가 북 구간에 점프 했을 때 북이 내려가게 애니메이션 넣어"): a floating drum of the royal
    // water garden (Bobbing platform P*) answers the pieces on it like a drum floating on water.
    // R544 (승규 11-10, "움직임에 따라서 안기울어져 왼쪽 방향으로 가면 왼쪽으로 기울어지게"): it no longer only tips once on a
    // landing - it leans the whole time toward where the pieces stand and walk:
    //   settle  each piece on it pushes it SinkPerPiece m down, and it leans toward the piece (the piece's side goes
    //           down, TiltPerPiece degrees at the very edge) and a little more toward where it walks
    //   land    a piece coming down on it throws it further down and toward that side, as hard as it fell
    //   float   both settle on a soft, under-damped spring, so it sinks, bobs back once or twice and comes to rest
    //   pivot   it tips about a point near the pieces' feet, not its middle: the far side rises instead of the near side
    //           dropping away from under them, which (the collider staying flat) would leave them standing on air
    // A picture only: the collider and the drum's bob (ObstacleMotion) stay as they are, so nothing about the run changes.
    // Who is on it, and how fast they move, is GardenDrumDip.Online.cs (the same on the host and on every client).
    // R544: the drum's own Drum mesh is the builder's hidden stand-in (RoyalWaterGardenBuilder.Concept hides it under
    // the stone model) - only a drawn one is copied onto the pivot; R543 drew it again and it flickered through the top.
    // GardenFloatSound adds this.
    [DisallowMultipleComponent]
    public sealed partial class GardenDrumDip : MonoBehaviour
    {
        const float SinkPerPiece = .04f, MaxSink = .12f;     // m the drum settles under pieces standing on it
        const float TiltPerPiece = 4f, MaxTilt = 7f;         // degrees: one piece at the very edge, all of them
        const float Lean = 2f, LeanSpeed = 5f;               // degrees more toward where a piece walks at LeanSpeed m/s
        const float Kick = .14f;                             // m/s of sink per m/s a landing piece falls at
        const float TiltKick = 4f;                           // deg/s of tip per m/s of fall, at the very edge
        const float Spring = 70f, Damp = 7f;                 // about 0.75 s a bob, damped by half each swing

        Collider deck;
        Transform model;
        float sink, sinkSpeed;
        Vector2 tilt, tiltSpeed;                             // world x/z: toward the side that goes down, in degrees
        Vector2 pivot;                                       // world x/z from the middle, 1 = the edge: what it tips about
        const float PivotReach = .8f;                        // how far toward the pieces the pivot goes

        void Start()
        {
            var drum = transform.Find("Drum");
            deck = drum != null ? drum.GetComponent<Collider>() : null;
            if (deck == null)
            {
                enabled = false;
                return;
            }
            model = new GameObject("Drum model").transform;
            model.SetParent(transform, false);
            // Everything that only draws goes under the pivot; colliders and sounds stay on the drum.
            var draw = new System.Collections.Generic.List<Transform>();
            foreach (Transform child in transform)
                if (child != model && child != drum && child.GetComponent<Collider>() == null && child.GetComponent<AudioSource>() == null)
                    draw.Add(child);
            foreach (var child in draw) child.SetParent(model, true);
            var renderer = drum.GetComponent<MeshRenderer>();
            var filter = drum.GetComponent<MeshFilter>();
            if (renderer != null && renderer.enabled && filter != null)
            {
                var look = new GameObject("Drum look");
                look.transform.SetParent(model, false);
                look.transform.SetPositionAndRotation(drum.position, drum.rotation);
                look.transform.localScale = drum.localScale;
                look.AddComponent<MeshFilter>().sharedMesh = filter.sharedMesh;
                var copy = look.AddComponent<MeshRenderer>();
                copy.sharedMaterials = renderer.sharedMaterials;
                copy.shadowCastingMode = renderer.shadowCastingMode;
                copy.receiveShadows = renderer.receiveShadows;
                copy.renderingLayerMask = renderer.renderingLayerMask;
                renderer.enabled = false;
            }
        }

        void Update()
        {
            Gather(Time.time, Time.deltaTime);
            float sinkTarget = Mathf.Min(MaxSink, SinkPerPiece * riders.Count);
            var tiltTarget = Vector2.zero;
            foreach (var r in riders) tiltTarget += r.offset * TiltPerPiece + r.walk * Lean;
            if (tiltTarget.sqrMagnitude > MaxTilt * MaxTilt) tiltTarget = tiltTarget.normalized * MaxTilt;

            float dt = Mathf.Min(Time.deltaTime, .05f);
            // With nobody on it the pivot stays where it was, so the drum comes back level without a jump.
            if (riders.Count > 0)
            {
                var mean = Vector2.zero;
                foreach (var r in riders) mean += r.offset;
                pivot = Vector2.Lerp(pivot, mean / riders.Count * PivotReach, 1f - Mathf.Exp(-8f * dt));
            }
            int steps = Mathf.Max(1, Mathf.CeilToInt(dt * 120f));
            float h = dt / steps;
            for (int i = 0; i < steps; i++)
            {
                sinkSpeed += (Spring * (sinkTarget - sink) - Damp * sinkSpeed) * h;
                sink += sinkSpeed * h;
                tiltSpeed += (Spring * (tiltTarget - tilt) - Damp * tiltSpeed) * h;
                tilt += tiltSpeed * h;
            }
            sink = Mathf.Clamp(sink, -.06f, .35f);
            if (tilt.sqrMagnitude > 144f) tilt = tilt.normalized * 12f;

            var down = transform.InverseTransformVector(Vector3.down * sink);
            float angle = tilt.magnitude;
            if (angle < .01f)
            {
                model.localRotation = Quaternion.identity;
                model.localPosition = down;
                return;
            }
            // Turning about up x side tips the side itself down; about the pivot on the top, not the drum's middle.
            var axis = Vector3.Cross(Vector3.up, new Vector3(tilt.x, 0f, tilt.y) / angle);
            var turn = Quaternion.AngleAxis(angle, transform.InverseTransformDirection(axis));
            var box = deck.bounds;
            float radius = Mathf.Max(.1f, box.extents.x);
            var at = transform.InverseTransformPoint(new Vector3(box.center.x + pivot.x * radius, box.max.y, box.center.z + pivot.y * radius));
            model.localRotation = turn;
            model.localPosition = down + at - turn * at;
        }

        // A piece came down on it at offset (from the middle, 1 = the edge), falling at fall m/s.
        void Land(Vector2 offset, float fall)
        {
            if (tilt.sqrMagnitude < .25f) pivot = offset * PivotReach;   // a level drum tips about where it was hit
            fall = Mathf.Clamp(fall, 1.5f, 9f);
            sinkSpeed += Kick * fall;
            tiltSpeed += offset * (TiltKick * fall);
        }
    }
}
