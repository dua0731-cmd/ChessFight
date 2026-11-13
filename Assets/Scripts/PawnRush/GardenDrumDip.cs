using UnityEngine;

namespace ChessFight.PawnRush
{
    // R543 (승규 11-10, "플레이어가 북 구간에 점프 했을 때 북이 내려가게 애니메이션 넣어"): a floating drum of the royal
    // water garden (Bobbing platform P*) answers the pieces on it like a drum floating on water.
    // R544 (승규 11-10, "움직임에 따라서 안기울어져 왼쪽 방향으로 가면 왼쪽으로 기울어지게"): it no longer only tips once on a
    // landing - it leans the whole time toward where the pieces stand and walk:
    //   settle  it leans toward the pieces on it (their side goes down, TiltPerPiece degrees at the very edge) and a
    //           little more toward where they walk (R635: it no longer sinks under them)
    //   land    a piece coming down on it tips it further toward that side and the way it came, as hard as it fell
    //   float   the lean settles on a soft, under-damped spring, so it rocks back once or twice and comes to rest
    //   pivot   it tips about the pieces' feet, not its middle: the far side rises instead of the near side dropping
    //           away from under them, which (the collider staying flat) would leave them standing on air
    // A picture only: the collider and the drum's bob (ObstacleMotion) stay as they are, so nothing about the run changes.
    // Who is on it, and how fast they move, is GardenDrumDip.Online.cs (the same on the host and on every client).
    // R544: the drum's own Drum mesh is the builder's hidden stand-in (RoyalWaterGardenBuilder.Concept hides it under
    // the stone model) - only a drawn one is copied onto the pivot; R543 drew it again and it flickered through the top.
    // R635 (승규 11-13, "플레이어가 가는 방향대로 기울어지잖아 근데 기물이 떠 보여 이동하는 방향대로 바닥이랑 기물을 맞게
    // 맞춰 줘"): the pieces stand on the flat collider, so wherever the drawn top went below it they hung in the air: it
    // settled 4 cm a piece (more on a landing), it tipped about a point 20% short of their feet, and that point trailed
    // over half a metre behind a walking piece while the drum leaned the way it walked - so the floor dropped from under
    // the feet just as the user saw. Now it never sinks, and it tips about the pieces' feet themselves, the very point
    // they stand on, kept there as they walk (a piece stepping on or off eases the point across instead of jumping).
    // Several pieces spread along the lean can't all stand on a tipped top, so it leans only as far as keeps every one
    // within Gap of it. The far side rises instead; a landing kicks the tip (toward its side and the way it came).
    // GardenFloatSound adds this.
    [DisallowMultipleComponent]
    public sealed partial class GardenDrumDip : MonoBehaviour
    {
        const float TiltPerPiece = 4f, MaxTilt = 7f;         // degrees: one piece at the very edge, all of them
        const float Lean = 2f, LeanSpeed = 5f;               // degrees more toward where a piece walks at LeanSpeed m/s
        const float TiltKick = 6f;                           // deg/s of tip per m/s of fall, at the very edge
        const float Spring = 70f, Damp = 7f;                 // about 0.75 s a bob, damped by half each swing
        const float Gap = .02f;                              // m: the most any piece's feet stand off the drawn top
        const float Catch = 12f;                             // 1/s: how fast the pivot eases over when pieces change

        Collider deck;
        Transform model;
        Vector2 tilt, tiltSpeed;                             // world x/z: toward the side that goes down, in degrees
        Vector2 pivot;                                       // world x/z from the middle, 1 = the edge: what it tips about
        Vector2 shift;                                       // the pivot's way still to go to the pieces' feet
        int lastKey;                                         // who stood on it last frame (GardenDrumDip.Online riderKey)

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
            var box = deck.bounds;
            float radius = Mathf.Max(.1f, box.extents.x);
            float dt = Mathf.Min(Time.deltaTime, .05f);
            var tiltTarget = Vector2.zero;
            var mean = Vector2.zero;
            foreach (var r in riders)
            {
                tiltTarget += r.offset * TiltPerPiece + r.walk * Lean;
                mean += r.offset;
            }
            // R635: the pivot is the pieces' feet (their mean), followed every frame; when someone steps on or off, the
            // mean jumps, and the pivot eases after it rather than moving the whole drawn drum at once. With nobody on it
            // the pivot stays where it was, so the drum comes back level without a jump.
            if (riders.Count > 0)
            {
                mean /= riders.Count;
                if (riderKey != lastKey) shift = pivot - mean;
                shift *= Mathf.Exp(-Catch * dt);
                pivot = mean + shift;
            }
            lastKey = riderKey;
            tiltTarget = Vector2.ClampMagnitude(tiltTarget, Mathf.Min(MaxTilt, Allowed(tiltTarget, mean, radius)));

            int steps = Mathf.Max(1, Mathf.CeilToInt(dt * 120f));
            float h = dt / steps;
            for (int i = 0; i < steps; i++)
            {
                tiltSpeed += (Spring * (tiltTarget - tilt) - Damp * tiltSpeed) * h;
                tilt += tiltSpeed * h;
            }
            if (tilt.sqrMagnitude > 144f) tilt = tilt.normalized * 12f;
            // R635: a bounce (a landing's kick, the spring's overshoot) may not lift anyone's feet off either: brought
            // back within what the pieces' spread allows quickly, not cut (that would jump)
            float most = Allowed(tilt, mean, radius);
            if (tilt.magnitude > most)
            {
                tilt = Vector2.Lerp(tilt, Vector2.ClampMagnitude(tilt, most), 1f - Mathf.Exp(-Catch * dt));
                if (Vector2.Dot(tiltSpeed, tilt) > 0f) tiltSpeed -= tilt.normalized * Vector2.Dot(tiltSpeed, tilt.normalized);
            }

            float angle = tilt.magnitude;
            if (angle < .01f)
            {
                model.localRotation = Quaternion.identity;
                model.localPosition = Vector3.zero;
                return;
            }
            // Turning about up x side tips the side itself down; about the pivot on the top, not the drum's middle.
            var axis = Vector3.Cross(Vector3.up, new Vector3(tilt.x, 0f, tilt.y) / angle);
            var turn = Quaternion.AngleAxis(angle, transform.InverseTransformDirection(axis));
            var at = transform.InverseTransformPoint(new Vector3(box.center.x + pivot.x * radius, box.max.y, box.center.z + pivot.y * radius));
            model.localRotation = turn;
            model.localPosition = at - turn * at;
        }

        // R635: how far (degrees) it may lean toward lean with every piece on it within Gap of the drawn top - pieces
        // spread out along the lean can't all stand on a tipped top; one piece, or pieces side by side across it, can.
        float Allowed(Vector2 lean, Vector2 mean, float radius)
        {
            if (riders.Count < 2 || lean.sqrMagnitude < 1e-6f) return 90f;
            var u = lean.normalized;
            float reach = 0f;
            foreach (var r in riders) reach = Mathf.Max(reach, Mathf.Abs(Vector2.Dot(r.offset - mean, u)) * radius);
            return reach <= Gap ? 90f : Mathf.Asin(Gap / reach) * Mathf.Rad2Deg;
        }

        // A piece came down on it at offset (from the middle, 1 = the edge), walking walk (1 = LeanSpeed), falling at
        // fall m/s. R635: it no longer sinks (the drawn top would drop from under the piece): it tips about the feet,
        // toward the side it came down on and the way it was going, as hard as it fell.
        void Land(Vector2 offset, Vector2 walk, float fall)
        {
            if (tilt.sqrMagnitude < .25f) pivot = offset;                // a level drum tips about where it was hit
            fall = Mathf.Clamp(fall, 1.5f, 9f);
            tiltSpeed += Vector2.ClampMagnitude(offset + walk * .5f, 1f) * (TiltKick * fall);
        }
    }
}
