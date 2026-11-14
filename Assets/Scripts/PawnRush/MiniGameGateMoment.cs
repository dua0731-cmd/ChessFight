using System.Collections.Generic;
using UnityEngine;

namespace ChessFight.PawnRush
{
    // R643 (승규 님 11-14, "미니게임 클리어 시 파란 폭죽 없애고 새로 넣기"): the four rooms that still used the shared
    // done moment (ArenaDressing: a team-colour light column, 90 bits flying, a floor ring) get one of their own, like
    // A, B, C, D, E and J already had. G and H get a short piece of the room's own furniture made of the chess set's
    // materials (ivory boxwood, ebony, brass), slammed into place in a tenth of a second and gone again after a few
    // seconds: G's banners (MiniGameProcessionGateFx), H's flipping board squares (MiniGameChessHallGateFx). F and I
    // only open their door (MiniGameDoorGateFx, Length 0: 승규 님 11-14 dropped F's gears and I's curtains).
    // No dust, no new sound: the gate keeps the shared sfx_gate_open (ArenaDressing plays sfx_mg_complete before).
    // Pictures only (no colliders), played by every PC from the progress it has, like every other gate moment.
    //
    // The frame: Root stands on the floor in front of the team gate (MiniGameSectionBuilder.Station: the door is the
    // station's x -2..2, y 0..4.6, z 23..23.4), x across the opening, y up, z out of the door toward the court.
    public abstract class MiniGameGateMoment : MonoBehaviour, IGateFx
    {
        protected const float HalfOpening = 2f, OpeningTop = 4.6f;

        protected sealed class Part
        {
            public Transform T;
            public Renderer R;
            public Vector3 Home, Size;
            public Quaternion Turn = Quaternion.identity;
        }

        readonly List<Part> parts = new List<Part>();
        protected Transform Root { get; private set; }
        protected MissionStation Station { get; private set; }
        float age = -1f;

        /// <summary>How long the moment lasts (s); its parts go after it.</summary>
        protected abstract float Length { get; }
        /// <summary>Makes the parts (Root is placed and empty).</summary>
        protected abstract void Build();
        /// <summary>Poses the parts at <paramref name="t"/> seconds into the moment.</summary>
        protected abstract void Pose(float t);

        // The shared 25 / 50 / 75 % sparkle stays as it was (no sound, no ring: ArenaDressing, 11-07).
        public void Quarter(int quarter)
        {
            var station = GetComponent<MissionStation>();
            if (station == null) return;
            var gate = station.transform.TransformPoint(new Vector3(0f, Arena.DaisHeight + .05f, 18.8f));
            ArenaFx.Burst(gate + Vector3.up * 5.6f, new[] { Arena.Gold }, 10, 2.5f, 2f, .9f, .1f);
        }

        public void Opened()
        {
            Station = GetComponent<MissionStation>();
            if (Station == null) return;
            // the gate's own sound, as before (inside MiniGameEar, which ArenaDressing set for this moment)
            ArenaFx.Sfx("sfx_gate_open", Station.transform.TransformPoint(new Vector3(0f, Arena.DaisHeight + .05f, 18.8f)));
            if (!Application.isPlaying || Length <= 0f) return;   // a door-only moment (MiniGameDoorGateFx) builds nothing
            Clear();
            if (Root == null) Root = new GameObject(GetType().Name + " moment").transform;
            Root.SetPositionAndRotation(Floor(), Station.transform.rotation * Quaternion.Euler(0f, 180f, 0f));
            Root.gameObject.SetActive(true);
            Build();
            age = 0f;
            Pose(0f);
        }

        void Update()
        {
            if (age < 0f) return;
            age += Time.deltaTime;
            if (age >= Length) { Clear(); age = -1f; return; }
            Pose(age);
        }

        void OnDestroy()
        {
            if (Root != null) Destroy(Root.gameObject);
        }

        // The floor in front of the door: the first solid surface under the doorway's court side (a piece standing
        // there is skipped), the station's own floor if there is none.
        Vector3 Floor()
        {
            var t = Station.transform;
            Vector3 from = t.TransformPoint(new Vector3(0f, 8f, 22.2f));
            float best = float.MaxValue;
            Vector3 at = t.TransformPoint(new Vector3(0f, 0f, 23f));
            foreach (var hit in Physics.RaycastAll(from, Vector3.down, 12f, ~0, QueryTriggerInteraction.Ignore))
            {
                var body = hit.collider.attachedRigidbody;
                if (body != null && !body.isKinematic) continue;
                if (hit.collider.GetComponentInParent<TeamGate>() != null) continue;
                if (hit.distance < best) { best = hit.distance; at = hit.point; }
            }
            Vector3 door = t.TransformPoint(new Vector3(0f, 0f, 23f));
            return new Vector3(door.x, at.y, door.z);
        }

        protected void Clear()
        {
            foreach (var p in parts) if (p.T != null) Destroy(p.T.gameObject);
            parts.Clear();
        }

        /// <summary>A picture box (no collider) in Root's frame: centre, size, material.</summary>
        protected Part Box(string name, Vector3 centre, Vector3 size, Material material, Transform parent = null) =>
            Shape(PrimitiveType.Cube, name, centre, size, material, parent);

        protected Part Shape(PrimitiveType type, string name, Vector3 centre, Vector3 size, Material material, Transform parent = null)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            var c = go.GetComponent<Collider>();
            if (c != null) DestroyImmediate(c);   // not in a physics callback: Opened comes from ArenaDressing.Update
            var r = go.GetComponent<Renderer>();
            r.sharedMaterial = material;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            go.transform.SetParent(parent != null ? parent : Root, false);
            go.transform.localPosition = centre;
            go.transform.localScale = size;
            var part = new Part { T = go.transform, R = r, Home = centre, Size = size };
            parts.Add(part);
            return part;
        }

        /// <summary>An empty pivot in Root's frame (or under <paramref name="parent"/>).</summary>
        protected Part Pivot(string name, Vector3 at, Transform parent = null)
        {
            var t = new GameObject(name).transform;
            t.SetParent(parent != null ? parent : Root, false);
            t.localPosition = at;
            var part = new Part { T = t, Home = at, Size = Vector3.one };
            parts.Add(part);
            return part;
        }

        // ---- timing ----

        /// <summary>The "팍!" landing: from <paramref name="from"/> times its size to 0.94 in 0.09 s, back to 1 by 0.17 s.</summary>
        protected static float Slam(float t, float from = 1.45f)
        {
            if (t <= 0f) return from;
            if (t < .09f) { float u = t / .09f; return Mathf.Lerp(from, .94f, 1f - (1f - u) * (1f - u)); }
            if (t < .17f) return Mathf.Lerp(.94f, 1f, (t - .09f) / .08f);
            return 1f;
        }

        /// <summary>0 to 1 over <paramref name="seconds"/> after <paramref name="start"/>, eased out.</summary>
        protected static float Out(float t, float start, float seconds)
        {
            float u = Mathf.Clamp01((t - start) / Mathf.Max(.0001f, seconds));
            return 1f - (1f - u) * (1f - u) * (1f - u);
        }

        /// <summary>1 until <paramref name="start"/>, then down to 0 over <paramref name="seconds"/>, eased in.</summary>
        protected static float Fade(float t, float start, float seconds)
        {
            float u = Mathf.Clamp01((t - start) / Mathf.Max(.0001f, seconds));
            return 1f - u * u;
        }

        // ---- the chess set's materials ----

        protected static Material Ivory => Arena.Lit("M_PieceWhite", new Color(.9f, .81f, .63f), 0f, .22f);
        protected static Material Ebony => Arena.Lit("M_PieceBlack", new Color(.1f, .075f, .06f), 0f, .2f);
        protected static Material Brass => Arena.TrimGold;
        // a brief brass light (a glint, a light line, a flame): the unlit glow, HDR so the bloom takes it. One
        // strength only: Arena.Glow keys its cache by the clamped colour.
        protected static Material Glint => Arena.Glow(new Color(2.2f, 1.72f, .84f, 1f));
    }
}
