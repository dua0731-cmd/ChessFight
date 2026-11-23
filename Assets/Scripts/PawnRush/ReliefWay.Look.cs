using System.Collections.Generic;
using ChessFight.Gameplay;
using UnityEngine;

namespace ChessFight.PawnRush
{
    // R784 (the user 2026-11-23, after R783's plain ivory decks with gold rails: "기능적으로는 다 됐는데, 각 맵에
    // 어울리게 길이 만들어졌으면 좋겠어"): what a relief way looks like, map by map. What is walked on and held by is
    // as R783 laid it (ReliefWay.cs: the deck's box and the two rail walls of a span, now unseen); this file dresses
    // each span in its map's own things, from the map's own materials:
    //
    //   the Royal Water Garden   a stone garden bridge - the quays' pink floor tile between cream stone curbs, a
    //                            cream balustrade with capped posts and gold finials, the royal blue runner with
    //                            gold edges that lies before the gate, stone piers under the reservoir's bridge
    //   the Royal Carriage Yard  a dockers' timber gangway - oak planks laid across two stringers, a post and rail
    //                            fence, trestles of timber legs down to the floor or the pond's bed
    //   the Grand Stair Hall     a marble gallery - the dais's veined marble with a gold edge, a crimson runner,
    //                            a slender gilt railing; nothing under it, like the floating stones it lies on
    //
    // Pictures only (no colliders): a post is never run into, the rail wall behind it is. Posts and legs stand
    // upright on a sloping span. Meshes are boxes with their UVs in metres, so a textured material tiles instead
    // of stretching along a 50 m deck.
    public static partial class ReliefWay
    {
        enum Style { Plain, Stone, Timber, Marble }

        struct Look
        {
            public Style Style;
            public Material Deck, Trim, Rail, Runner, Gold;
        }

        static Material Loaded(string path, Material fallback)
        {
            var found = Resources.Load<Material>(path);
            return found != null ? found : fallback;
        }

        static Look LookOf(string sectionId)
        {
            switch (sectionId)
            {
                case Garden:
                    return new Look
                    {
                        Style = Style.Stone,
                        Deck = Loaded("PawnRushGardenConcept/Materials/GC_floor_tile", Arena.Lit("M_ReliefTile", new Color(.93f, .75f, .68f), 0f, .8f)),
                        Trim = Loaded("PawnRushGardenConcept/Materials/GC_cream_stone", Arena.Lit("M_ReliefCream", new Color(.97f, .8f, .68f), 0f, .8f)),
                        Rail = Loaded("PawnRushGardenConcept/Materials/GC_cream_stone_pale", Arena.Lit("M_ReliefCreamPale", new Color(.98f, .88f, .78f), 0f, .8f)),
                        Runner = Loaded("PawnRushGardenConcept/Materials/GC_crown_blue", Arena.Lit("M_ReliefBlue", new Color(.3f, .43f, .8f), 0f, .85f)),
                        Gold = Loaded("PawnRushGardenConcept/Materials/GC_gold", Arena.TrimGold),
                    };
                case Yard:
                    return new Look
                    {
                        Style = Style.Timber,
                        Deck = Loaded("PawnRushCarriageYard/Materials/RCY_Oak", YardKit.WoodLight),
                        Trim = Loaded("PawnRushCarriageYard/Materials/RCY_Timber", YardKit.WoodDark),
                        Rail = Loaded("PawnRushCarriageYard/Materials/RCY_Timber", YardKit.WoodDark),
                        Runner = null,
                        Gold = Loaded("PawnRushCarriageYard/Materials/RCY_Iron", YardKit.Iron),
                    };
                case Hall:
                    return new Look
                    {
                        Style = Style.Marble,
                        Deck = Loaded("PR_CalacattaOro", Arena.Lit("M_ReliefMarble", new Color(.95f, .94f, .91f), 0f, .25f)),
                        Trim = Arena.TrimGold,
                        Rail = Arena.TrimGold,
                        Runner = Arena.Lit("M_ReliefCarpet", new Color(.56f, .1f, .16f), 0f, .92f),
                        Gold = Arena.TrimGold,
                    };
                default:
                    return new Look
                    {
                        Style = Style.Plain,
                        Deck = Arena.Lit("M_ReliefDeck", new Color(.9f, .82f, .62f), 0f, .75f),
                        Trim = Arena.TrimGold, Rail = Arena.TrimGold, Gold = Arena.TrimGold,
                    };
            }
        }

        // Dresses one span (its frame: x across, y off the deck, z along, the origin the middle of its top's line).
        // `full` is the span's length with the overlaps at its ends.
        static void Dress(Transform span, Look look, float width, float full, bool rails, bool level)
        {
            var art = new GameObject("Look").transform;
            art.SetParent(span, false);
            // Upright in the world whatever the span's slope: posts, legs, piers.
            var up = Upright(span);
            switch (look.Style)
            {
                case Style.Stone: Stone(art, look, width, full, rails, level, up); break;
                case Style.Timber: Timber(art, look, width, full, rails, up); break;
                case Style.Marble: Marble(art, look, width, full, rails, up); break;
                default:
                    Piece(art, "Deck", new Vector3(0f, -Thick * .5f, 0f), new Vector3(width, Thick, full), look.Deck, 2f);
                    if (rails)
                        for (int s = -1; s <= 1; s += 2)
                            Piece(art, "Rail", new Vector3(s * (width * .5f - RailThick * .5f), RailHigh * .5f, 0f), new Vector3(RailThick, RailHigh, full), look.Rail, 2f);
                    break;
            }
        }

        // ------------------------------------------------------------------ the garden: a stone bridge

        static void Stone(Transform art, Look look, float width, float full, bool rails, bool level, Quaternion up)
        {
            const float Slab = .36f, Curb = .3f, PostEvery = 2.4f;
            Piece(art, "Tiled deck", new Vector3(0f, -Slab * .5f, 0f), new Vector3(width, Slab, full), look.Deck, 2f);
            // The royal runner down its middle, gold along its edges.
            float runner = Mathf.Min(1.4f, width - 1.3f);
            Piece(art, "Runner", new Vector3(0f, .008f, 0f), new Vector3(runner, .016f, full), look.Runner, 2f);
            for (int s = -1; s <= 1; s += 2)
            {
                Piece(art, "Runner edge", new Vector3(s * (runner * .5f + .04f), .01f, 0f), new Vector3(.08f, .02f, full), look.Gold, 2f);
                float x = s * (width * .5f - Curb * .5f);
                Piece(art, "Curb", new Vector3(x, .07f, 0f), new Vector3(Curb, .14f, full), look.Trim, 2f);
                // Its face over the water.
                Piece(art, "Fascia", new Vector3(s * (width * .5f + .03f), -Slab * .5f, 0f), new Vector3(.08f, Slab + .1f, full), look.Trim, 2f);
                if (!rails) continue;
                Piece(art, "Top rail", new Vector3(x, 1f, 0f), new Vector3(.24f, .12f, full), look.Rail, 2f);
                Piece(art, "Low rail", new Vector3(x, .5f, 0f), new Vector3(.12f, .08f, full), look.Rail, 2f);
                int posts = Mathf.Max(2, Mathf.RoundToInt(full / PostEvery) + 1);
                for (int i = 0; i < posts; i++)
                {
                    float z = Mathf.Lerp(-full * .5f + .2f, full * .5f - .2f, posts == 1 ? .5f : (float)i / (posts - 1));
                    var post = Holder(art, "Post", new Vector3(x, 0f, z), up);
                    Piece(post, "Shaft", new Vector3(0f, .52f, 0f), new Vector3(.3f, 1.04f, .3f), look.Trim, 2f);
                    Piece(post, "Cap", new Vector3(0f, 1.08f, 0f), new Vector3(.4f, .08f, .4f), look.Rail, 2f);
                    Piece(post, "Finial", new Vector3(0f, 1.2f, 0f), new Vector3(.17f, .17f, .17f), look.Gold, 2f, Quaternion.Euler(45f, 45f, 0f));
                }
            }
            // A level bridge stands on piers (a sloping one lies on what it crosses - the drums).
            if (!level || full < 9f) return;
            foreach (float f in new[] { -1f / 6f, 1f / 6f })
            {
                var top = art.TransformPoint(new Vector3(0f, -Slab, full * f));
                float deep = Depth(top, 9f);
                if (deep < .8f) continue;
                var pier = Holder(art, "Pier", new Vector3(0f, -Slab, full * f), up);
                Piece(pier, "Shaft", new Vector3(0f, -deep * .5f, 0f), new Vector3(width - .7f, deep, 1.1f), look.Trim, 2f);
                Piece(pier, "Cap", new Vector3(0f, -.12f, 0f), new Vector3(width - .3f, .24f, 1.4f), look.Rail, 2f);
            }
        }

        // ------------------------------------------------------------------ the yard: a timber gangway

        static void Timber(Transform art, Look look, float width, float full, bool rails, Quaternion up)
        {
            const float Plank = .32f, Gap = .02f, PostEvery = 2f, LegEvery = 3.2f;
            int planks = Mathf.Max(1, Mathf.RoundToInt(full / (Plank + Gap)));
            float pitch = full / planks;
            for (int i = 0; i < planks; i++)
            {
                // Boards not quite alike: every third a little proud, the ends a little uneven.
                float z = -full * .5f + pitch * (i + .5f), proud = i % 3 == 1 ? .006f : 0f, shift = (i % 4 - 1.5f) * .012f;
                Piece(art, "Plank", new Vector3(shift, -.035f + proud, z), new Vector3(width - .04f, .07f, pitch - Gap), look.Deck, 1.4f);
            }
            for (int s = -1; s <= 1; s += 2)
            {
                Piece(art, "Stringer", new Vector3(s * (width * .5f - .32f), -.2f, 0f), new Vector3(.2f, .26f, full), look.Trim, 1.6f);
                if (!rails) continue;
                float x = s * (width * .5f - .09f);
                Piece(art, "Top rail", new Vector3(x, 1f, 0f), new Vector3(.12f, .1f, full), look.Rail, 1.6f);
                Piece(art, "Mid rail", new Vector3(x, .55f, 0f), new Vector3(.08f, .08f, full), look.Rail, 1.6f);
                int posts = Mathf.Max(2, Mathf.RoundToInt(full / PostEvery) + 1);
                for (int i = 0; i < posts; i++)
                {
                    float z = Mathf.Lerp(-full * .5f + .15f, full * .5f - .15f, (float)i / (posts - 1));
                    var post = Holder(art, "Post", new Vector3(x, -.33f, z), up);
                    Piece(post, "Shaft", new Vector3(0f, .7f, 0f), new Vector3(.14f, 1.4f, .14f), look.Rail, 1.6f);
                    Piece(post, "Band", new Vector3(0f, .1f, 0f), new Vector3(.17f, .06f, .17f), look.Gold, 1f);
                }
            }
            if (!rails) return;
            // Trestles: a pair of legs to whatever is under them, a beam across their heads, another half way down
            // a tall pair.
            int bents = Mathf.Max(2, Mathf.RoundToInt(full / LegEvery) + 1);
            for (int i = 0; i < bents; i++)
            {
                float z = Mathf.Lerp(-full * .5f + .4f, full * .5f - .4f, (float)i / (bents - 1));
                var bent = Holder(art, "Trestle", new Vector3(0f, -.33f, z), up);
                float tallest = 0f;
                for (int s = -1; s <= 1; s += 2)
                {
                    float x = s * (width * .5f - .32f);
                    float deep = Depth(bent.TransformPoint(new Vector3(x, 0f, 0f)), 14f);
                    if (deep < .35f) continue;
                    tallest = Mathf.Max(tallest, deep);
                    Piece(bent, "Leg", new Vector3(x, -deep * .5f, 0f), new Vector3(.2f, deep, .2f), look.Trim, 1.6f);
                }
                if (tallest < .35f) continue;
                Piece(bent, "Head beam", new Vector3(0f, -.09f, 0f), new Vector3(width + .1f, .18f, .2f), look.Trim, 1.6f);
                if (tallest > 2.6f) Piece(bent, "Tie beam", new Vector3(0f, -tallest * .5f, 0f), new Vector3(width - .3f, .14f, .14f), look.Trim, 1.6f);
            }
        }

        // ------------------------------------------------------------------ the hall: a marble gallery

        static void Marble(Transform art, Look look, float width, float full, bool rails, Quaternion up)
        {
            const float Slab = .22f, PostEvery = 1.3f;
            Piece(art, "Marble deck", new Vector3(0f, -Slab * .5f, 0f), new Vector3(width, Slab, full), look.Deck, 3f);
            float runner = Mathf.Min(1.2f, width - 1.1f);
            Piece(art, "Runner", new Vector3(0f, .008f, 0f), new Vector3(runner, .016f, full), look.Runner, 2f);
            for (int s = -1; s <= 1; s += 2)
            {
                Piece(art, "Runner edge", new Vector3(s * (runner * .5f + .03f), .01f, 0f), new Vector3(.06f, .02f, full), look.Gold, 2f);
                Piece(art, "Gilt edge", new Vector3(s * (width * .5f + .02f), -Slab * .5f, 0f), new Vector3(.06f, Slab + .06f, full), look.Trim, 2f);
                if (!rails) continue;
                float x = s * (width * .5f - .09f);
                Piece(art, "Hand rail", new Vector3(x, .96f, 0f), new Vector3(.1f, .07f, full), look.Rail, 2f);
                Piece(art, "Low rail", new Vector3(x, .12f, 0f), new Vector3(.05f, .05f, full), look.Rail, 2f);
                int posts = Mathf.Max(2, Mathf.RoundToInt(full / PostEvery) + 1);
                for (int i = 0; i < posts; i++)
                {
                    float z = Mathf.Lerp(-full * .5f + .1f, full * .5f - .1f, (float)i / (posts - 1));
                    var post = Holder(art, "Baluster", new Vector3(x, 0f, z), up);
                    Piece(post, "Shaft", new Vector3(0f, .48f, 0f), new Vector3(.06f, .96f, .06f), look.Rail, 2f);
                    Piece(post, "Knob", new Vector3(0f, .56f, 0f), new Vector3(.11f, .11f, .11f), look.Rail, 2f, Quaternion.Euler(0f, 45f, 0f));
                }
            }
        }

        // ------------------------------------------------------------------ pieces

        // The turn that stands a child of `span` upright in the world, facing along the span's way on the flat.
        static Quaternion Upright(Transform span)
        {
            var along = span.forward;
            along.y = 0f;
            var flat = along.sqrMagnitude > 1e-4f ? Quaternion.LookRotation(along.normalized, Vector3.up) : Quaternion.identity;
            return Quaternion.Inverse(span.rotation) * flat;
        }

        static Transform Holder(Transform parent, string name, Vector3 at, Quaternion turn)
        {
            var holder = new GameObject(name).transform;
            holder.SetParent(parent, false);
            holder.localPosition = at;
            holder.localRotation = turn;
            return holder;
        }

        // How far down from a point the first solid thing is (a pawn or a loose thing is not one), `most` for nothing.
        static float Depth(Vector3 from, float most)
        {
            float best = most;
            foreach (var hit in Physics.RaycastAll(from + Vector3.down * .05f, Vector3.down, most, ~0, QueryTriggerInteraction.Ignore))
            {
                var body = hit.collider.attachedRigidbody;
                if (body != null && !body.isKinematic) continue;
                if (hit.collider.GetComponentInParent<ICharacterDriver>() != null) continue;
                if (hit.distance + .05f < best) best = hit.distance + .05f;
            }
            return best;
        }

        // A drawn box (no collider), `size` metres, its UVs `tile` metres a repeat.
        static void Piece(Transform parent, string name, Vector3 at, Vector3 size, Material material, float tile, Quaternion? turn = null)
        {
            if (material == null) return;
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = at;
            if (turn.HasValue) go.transform.localRotation = turn.Value;
            go.AddComponent<MeshFilter>().sharedMesh = BoxMesh(size, tile);
            go.AddComponent<MeshRenderer>().sharedMaterial = material;
        }

        static readonly Dictionary<(int, int, int, int), Mesh> meshes = new Dictionary<(int, int, int, int), Mesh>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ForgetMeshes() => meshes.Clear();

        // A box of `size` about its middle, each face's UVs its own two lengths in units of `tile` metres.
        static Mesh BoxMesh(Vector3 size, float tile)
        {
            var key = (Mathf.RoundToInt(size.x * 200f), Mathf.RoundToInt(size.y * 200f), Mathf.RoundToInt(size.z * 200f), Mathf.RoundToInt(tile * 10f));
            if (meshes.TryGetValue(key, out var made) && made != null) return made;
            Vector3 h = size * .5f;
            var vertices = new List<Vector3>(24);
            var normals = new List<Vector3>(24);
            var uvs = new List<Vector2>(24);
            var triangles = new List<int>(36);
            // Each face: its normal, the two axes that span it (right-handed with the normal).
            Face(Vector3.up, Vector3.right, Vector3.forward);
            Face(Vector3.down, Vector3.forward, Vector3.right);
            Face(Vector3.right, Vector3.forward, Vector3.up);
            Face(Vector3.left, Vector3.up, Vector3.forward);
            Face(Vector3.forward, Vector3.up, Vector3.right);
            Face(Vector3.back, Vector3.right, Vector3.up);
            void Face(Vector3 n, Vector3 a, Vector3 b)
            {
                float ha = Mathf.Abs(Vector3.Dot(a, h)), hb = Mathf.Abs(Vector3.Dot(b, h));
                Vector3 middle = Vector3.Scale(n, h);
                int first = vertices.Count;
                foreach (var (sa, sb) in new[] { (-1f, -1f), (-1f, 1f), (1f, 1f), (1f, -1f) })
                {
                    vertices.Add(middle + a * (sa * ha) + b * (sb * hb));
                    normals.Add(n);
                    uvs.Add(new Vector2(sa * ha, sb * hb) / Mathf.Max(.1f, tile));
                }
                triangles.AddRange(new[] { first, first + 1, first + 2, first, first + 2, first + 3 });
            }
            made = new Mesh { name = $"Relief box {size.x:0.##} x {size.y:0.##} x {size.z:0.##}" };
            made.SetVertices(vertices);
            made.SetNormals(normals);
            made.SetUVs(0, uvs);
            made.SetTriangles(triangles, 0);
            made.RecalculateBounds();
            meshes[key] = made;
            return made;
        }
    }
}
