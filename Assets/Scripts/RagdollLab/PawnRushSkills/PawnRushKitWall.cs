using System.Collections.Generic;
using ChessFight.Game;
using ChessFight.Gameplay;
using UnityEngine;

namespace ChessFight.RagdollLab
{
    /// <summary>
    /// A rook's wall (JY-skill-lab, 2026-11-21; the kits' second version, Docs/Skills/PAWN_RUSH_SKILL_KITS.md 6.4): a stone
    /// box standing on the floor along a line, chest high, for a while. The wall charge (R-상, RagdollPawn.KitTwo.cs)
    /// raises one behind it as it runs. On the host it is solid: a box collider the pieces run into and must climb (the
    /// rook that raised it does not touch it); every other PC is told when it stands and when it breaks
    /// (SkillFxKind.KitWall, KitWallBreak) and stands a picture of it with no collider, as the bodies it shows are the
    /// host's. It rises out of the floor, stands its time, and sinks back; an enemy rook's charge breaks it at once.
    /// Not seen in Unity by anyone yet.
    /// </summary>
    public class PawnRushKitWall : MonoBehaviour
    {
        public static readonly List<PawnRushKitWall> All = new List<PawnRushKitWall>();
        static int nextId;
        static Material stone;
        const float SinkTime = 0.3f;

        public int Id { get; private set; }
        public RagdollPawn Owner { get; private set; }
        /// <summary>A collider (the host's), or only a picture (another PC's).</summary>
        public bool Solid { get; private set; }
        public Vector3 From => from;
        public Vector3 To => to;
        public Vector3 Middle => (from + to) * 0.5f + Vector3.up * (height * 0.5f);
        public bool Standing => left > 0f;

        Vector3 from, to;
        float height, thick, left, rise, risen;
        BoxCollider box;
        Rigidbody body;
        Transform picture;

        /// <summary>The solid wall's collider (null for a picture).</summary>
        public Collider Collider => box;

        /// <summary>The wall of <paramref name="owner"/>'s that is still standing (the newest), or null.</summary>
        public static PawnRushKitWall OwnedBy(RagdollPawn owner)
        {
            for (int i = All.Count - 1; i >= 0; i--)
                if (All[i] != null && All[i].Owner == owner && All[i].Standing) return All[i];
            return null;
        }

        /// <summary>A wall that moves (the 7th rank): on the host a kinematic body, so it pushes what it meets as it goes.</summary>
        public void MakeMoving()
        {
            if (!Solid || body != null) return;
            body = gameObject.AddComponent<Rigidbody>();
            body.isKinematic = true;
            body.interpolation = RigidbodyInterpolation.None;
        }

        /// <summary>A number for a new wall (1..250, then round again).</summary>
        public static int NextId()
        {
            nextId = nextId % 250 + 1;
            return nextId;
        }

        public static PawnRushKitWall Find(int id)
        {
            foreach (var w in All)
                if (w != null && w.Id == id) return w;
            return null;
        }

        /// <summary>A wall from <paramref name="a"/> to <paramref name="b"/> (points on the floor), <paramref name="tall"/>
        /// high and <paramref name="thickness"/> thick, standing <paramref name="seconds"/> once risen over
        /// <paramref name="riseTime"/>.</summary>
        public static PawnRushKitWall Raise(int id, RagdollPawn owner, Vector3 a, Vector3 b, float tall, float thickness, float seconds, float riseTime, bool solid)
        {
            var old = Find(id);
            if (old != null) old.Break();   // (the number came round, or a moment told twice)
            var go = new GameObject("Rook wall " + id);
            var w = go.AddComponent<PawnRushKitWall>();
            w.Id = id;
            w.Owner = owner;
            w.Solid = solid;
            w.height = Mathf.Max(0.2f, tall);
            w.thick = Mathf.Max(0.1f, thickness);
            w.left = Mathf.Max(0.1f, seconds);
            w.rise = Mathf.Max(0.01f, riseTime);
            var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            var cubeCollider = cube.GetComponent<Collider>();
            if (cubeCollider != null) DestroyImmediate(cubeCollider);   // the picture only: the wall's own box is the collider
            cube.name = "Stones";
            cube.transform.SetParent(go.transform, false);
            var r = cube.GetComponent<Renderer>();
            if (r != null) r.sharedMaterial = Stone();
            w.picture = cube.transform;
            if (solid)
            {
                w.box = go.AddComponent<BoxCollider>();
                if (owner != null) owner.IgnoreBody(w.box, true);
            }
            w.SetLine(a, b);
            All.Add(w);
            return w;
        }

        static Material Stone()
        {
            if (stone != null) return stone;
            var lit = LitMaterials.Lit;
            stone = lit != null ? LitMaterials.NewLit(new Color(0.63f, 0.59f, 0.52f), 0.12f)
                                : new Material(Shader.Find("Sprites/Default")) { color = new Color(0.63f, 0.59f, 0.52f) };
            stone.name = "Rook wall stone";
            return stone;
        }

        /// <summary>Lay it along a new line (the run grows it).</summary>
        public void SetLine(Vector3 a, Vector3 b)
        {
            from = a;
            to = b;
            Fit();
        }

        /// <summary>It stands <paramref name="seconds"/> from now (the run is over).</summary>
        public void Stand(float seconds) => left = Mathf.Max(left, seconds);

        /// <summary>Taken down at once: it sinks away.</summary>
        public void Break()
        {
            left = 0f;
            if (box != null) box.enabled = false;
        }

        void Fit()
        {
            Vector3 d = to - from;
            d.y = 0f;
            float length = Mathf.Max(0.3f, d.magnitude);
            Vector3 at = (from + to) * 0.5f;
            var turn = Quaternion.LookRotation(d.sqrMagnitude > 1e-4f ? d.normalized : Vector3.forward, Vector3.up);
            if (body != null)
            {
                // (A moving wall: through its kinematic body, so the physics sees it sweep, not jump.)
                body.MovePosition(at);
                body.MoveRotation(turn);
            }
            else transform.SetPositionAndRotation(at, turn);
            // Risen this much: its top at risen * height above the floor (the rest still under it).
            float top = height * Mathf.Clamp01(risen);
            if (picture != null)
            {
                picture.localScale = new Vector3(thick, height, length);
                picture.localPosition = new Vector3(0f, top - height * 0.5f, 0f);
            }
            if (box != null)
            {
                box.size = new Vector3(thick, height, length);
                box.center = new Vector3(0f, top - height * 0.5f, 0f);
            }
        }

        void Update()
        {
            float dt = Time.deltaTime;
            if (left > 0f)
            {
                left -= dt;
                risen = Mathf.Min(1f, risen + dt / rise);
                if (left <= 0f && box != null) box.enabled = false;
            }
            else
            {
                risen -= dt / SinkTime;
                if (risen <= 0f)
                {
                    Destroy(gameObject);
                    return;
                }
            }
            Fit();
        }

        void OnDestroy() => All.Remove(this);

        /// <summary>How far a point is from the wall's line, along the floor (its ends count as the line's).</summary>
        public float FlatDistance(Vector3 p)
        {
            Vector3 a = new Vector3(from.x, 0f, from.z), b = new Vector3(to.x, 0f, to.z), q = new Vector3(p.x, 0f, p.z);
            Vector3 ab = b - a;
            float t = ab.sqrMagnitude > 1e-6f ? Mathf.Clamp01(Vector3.Dot(q - a, ab) / ab.sqrMagnitude) : 0f;
            return (q - (a + ab * t)).magnitude;
        }

        /// <summary>Host: a rook charging meets an enemy rook's standing wall ahead of its chest: the wall breaks (every PC is
        /// told). Its own team's walls it does not break.</summary>
        public static void BreakAhead(RagdollPawn rook, Vector3 chest, Vector3 dir, float reach)
        {
            if (rook == null || rook.NetworkPuppet) return;
            for (int i = All.Count - 1; i >= 0; i--)
            {
                var w = All[i];
                if (w == null || !w.Solid || !w.Standing || w.Owner == null || w.Owner == rook || !Teams.AreEnemies(w.Owner.Team, rook.Team)) continue;
                Vector3 ahead = chest + dir * reach;
                if (Mathf.Abs(ahead.y - (w.from.y + w.height * 0.5f)) > w.height + 0.5f) continue;
                if (w.FlatDistance(ahead) > w.thick * 0.5f + 0.4f && w.FlatDistance(chest) > w.thick * 0.5f + 0.4f) continue;
                w.Break();
                rook.TellWallBroken(w);
            }
        }
    }
}
