using System.Collections.Generic;
using ChessFight.ProtectKing;
using UnityEngine;

namespace ChessFight.RagdollLab
{
    public sealed partial class ChessTimerMinigame
    {
        // 54 x 54 = 2916 m², versus the original 24 x 24 = 576 m² (5.0625 times).
        readonly GameObject[,,] digits = new GameObject[2, 4, 7];
        static readonly int[] DigitMasks = { 0x3f, 0x06, 0x5b, 0x4f, 0x66, 0x6d, 0x7d, 0x07, 0x7f, 0x6f };
        const float ArenaWidth = 54f, ArenaDepth = 54f;
        const float ApproachStartZ = -18f, ApproachEndZ = -.7f, ApproachHeight = 4.3f;

        void BuildArena()
        {
            var ivory = Mat(new Color(.90f, .85f, .72f));
            var walnut = Mat(new Color(.40f, .31f, .25f));
            var charcoal = Mat(new Color(.075f, .083f, .095f));
            var rubber = Mat(new Color(.035f, .04f, .045f));
            var cream = Mat(new Color(.98f, .96f, .88f));
            var lcd = Mat(new Color(.63f, .72f, .64f));
            var gold = Mat(new Color(.94f, .69f, .24f));
            teamMaterials[0] = Mat(new Color(.30f, .64f, .92f));
            teamMaterials[1] = Mat(new Color(.96f, .47f, .39f));
            foreach (var material in materials) material.SetFloat("_Glossiness", .24f);
            for (int x = 0; x < 18; x++) for (int z = 0; z < 18; z++)
                Box("Chess floor", new Vector3((x - 8.5f) * 3, -.25f, (z - 8.5f) * 3),
                    new Vector3(ArenaWidth / 18, .5f, ArenaDepth / 18), (x + z) % 2 == 0 ? ivory : walnut);

            for (int side = 0; side < 2; side++)
            {
                float x = TeamX(side); var accent = teamMaterials[side];
                // Photo-inspired silhouette: soft black housing, broad white top rocker, twin LCD windows.
                RoundedBox("Clock rubber base", new Vector3(x, .25f, 3), new Vector3(11.4f, .5f, 10.4f), .23f, rubber, false);
                RoundedBox("Clock rounded housing", new Vector3(x, 1.9f, 3), new Vector3(11, 3.8f, 10), .9f, charcoal);
                RoundedBox("Clock shoulder trim", new Vector3(x, 3.55f, 3), new Vector3(10.6f, .35f, 9.4f), .17f, accent, false);
                var pad = RoundedBox("Clock white battle button", new Vector3(x, 4, 3), new Vector3(9.8f, .5f, 8.5f), .24f, cream);
                pads[side] = pad.GetComponent<Collider>();
                occupancy[side] = pad.AddComponent<ChessTimerPlate>();
                var body = pad.AddComponent<Rigidbody>(); body.isKinematic = true; body.useGravity = false;
                body.interpolation = RigidbodyInterpolation.Interpolate; plateBodies[side] = body;
                ImportedMovingSurface.BindDynamic(body, point => body.GetPointVelocity(point), () => Quaternion.identity, () => started);
                // Decorative colour stripe is attached to the moving button and never blocks feet.
                var stripe = RoundedBox("Team button stripe", new Vector3(x, 4.255f, 6.6f), new Vector3(8, .015f, .18f), .006f, accent, false);
                stripe.transform.SetParent(pad.transform, true);

                RoundedBox("LCD bezel", new Vector3(x, 2.1f, -2.06f), new Vector3(9.7f, 1.8f, .24f), .11f, rubber, false);
                for (int window = 0; window < 2; window++)
                    RoundedBox("LCD window", new Vector3(x + (window == 0 ? -2.35f : 2.35f), 2.1f, -2.2f),
                        new Vector3(4.4f, 1.5f, .1f), .045f, lcd, false);
                clocks[side] = Label("LCD countdown", new Vector3(x - 2.35f, 2.15f, -2.27f), "00:45", .62f);
                crowdDisplays[side] = Label("LCD occupant count", new Vector3(x + 2.35f, 2.1f, -2.27f), "00\nPLAYERS", .32f);
                clocks[side].color = crowdDisplays[side].color = new Color(.06f, .10f, .08f);
                clocks[side].GetComponent<Renderer>().enabled = false;
                BuildDigits(side, x - 2.35f, rubber);
                clockStatus[side] = Label("Clock status", new Vector3(x, 3.25f, -2.32f), "", .25f);
                for (int control = 0; control < 4; control++)
                {
                    float cx = x + (control - 1.5f) * 1.25f;
                    RoundedBox("Clock control button", new Vector3(cx, .85f, -1.9f), new Vector3(.85f, .4f, .35f), .16f, cream, false);
                    var icon = Label("Clock control icon", new Vector3(cx, .85f, -2.09f), new[] { "+", "O", ">", "-" }[control], .2f);
                    icon.color = Color.black;
                }
                Label("Top team marker", new Vector3(x, 5.8f, 7), side == 0 ? "BLUE CLOCK" : "RED CLOCK", .45f);

                // Continuous surfaces: no risers, vertical step lips or jump-only final landing.
                float outside = side == 0 ? -1 : 1;
                float approachX = x + outside * 7.5f;
                // Approach outside the housing so neither LCD is hidden behind a ramp.
                Ramp("Walk-up approach", new Vector3(approachX, 0, ApproachStartZ), new Vector3(approachX, ApproachHeight, ApproachEndZ), 5, accent);
                Box("Walk-up landing", new Vector3(approachX, ApproachHeight - .25f, 1.15f), new Vector3(5, .5f, 3.7f), cream);
                Ramp("Walk-up button blend", new Vector3(approachX, ApproachHeight, 3), new Vector3(x + outside * 3, 3.95f, 3), 5, cream);
                Label("Approach sign", new Vector3(approachX, .8f, -19), "WALK UP", .3f);

                for (int stage = 0; stage < 3; stage++)
                {
                    RoundedBox("Defuse console " + stage, Panel(side, stage), new Vector3(1.4f, 1.4f, .9f), .25f, accent);
                    panelLabels[side, stage] = Label("Console instruction", Panel(side, stage) + new Vector3(0, 1.3f, -.5f), "", .19f);
                }
                gates[side] = Box("Team exit gate", new Vector3(x, 2, 23), new Vector3(5, 4, .5f), accent);
                Label("Exit label", new Vector3(x, 4.6f, 22.6f), "NEXT STAGE", .35f);
            }
            // One shared rooftop arena. Both exposed sides permit knock-offs to the lower floor.
            Box("Open duel bridge", new Vector3(0, 4.2f, 3), new Vector3(10.4f, .5f, 6), ivory);
            Ramp("Blue bridge blend", new Vector3(-7.7f, 3.95f, 3), new Vector3(-5.2f, 4.45f, 3), 6, ivory);
            Ramp("Red bridge blend", new Vector3(7.7f, 3.95f, 3), new Vector3(5.2f, 4.45f, 3), 6, ivory);
            // Perimeter only; there is no wall between teams.
            Box("Exit wall centre", new Vector3(0, 2, 23), new Vector3(17, 4, .5f), walnut);
            Box("Exit wall left", new Vector3(-20.25f, 2, 23), new Vector3(13.5f, 4, .5f), walnut);
            Box("Exit wall right", new Vector3(20.25f, 2, 23), new Vector3(13.5f, 4, .5f), walnut);
            Box("Outer left boundary", new Vector3(-27, 1.5f, 0), new Vector3(.5f, 3, 54), walnut);
            Box("Outer right boundary", new Vector3(27, 1.5f, 0), new Vector3(.5f, 3, 54), walnut);
            Box("Outer front boundary", new Vector3(0, 1.5f, -27), new Vector3(54, 3, .5f), walnut);
        }

        void BuildDigits(int side, float centre, Material ink)
        {
            var positions = new[] { new Vector2(0, .5f), new Vector2(.29f, .25f), new Vector2(.29f, -.25f),
                new Vector2(0, -.5f), new Vector2(-.29f, -.25f), new Vector2(-.29f, .25f), Vector2.zero };
            var offsets = new[] { -1.5f, -.6f, .6f, 1.5f };
            for (int digit = 0; digit < 4; digit++) for (int segment = 0; segment < 7; segment++)
            {
                bool horizontal = segment == 0 || segment == 3 || segment == 6;
                digits[side, digit, segment] = Box("LCD digit segment",
                    new Vector3(centre + offsets[digit] + positions[segment].x, 2.1f + positions[segment].y, -2.285f),
                    horizontal ? new Vector3(.49f, .075f, .015f) : new Vector3(.075f, .4f, .015f), ink, false);
            }
            foreach (float y in new[] { -.17f, .17f })
                Box("LCD colon", new Vector3(centre, 2.1f + y, -2.285f), new Vector3(.08f, .08f, .015f), ink, false);
        }
        void UpdateDigits(int side, int seconds)
        {
            int value = Mathf.Clamp(seconds / 60, 0, 99) * 100 + seconds % 60;
            for (int digit = 3; digit >= 0; digit--)
            {
                int mask = DigitMasks[value % 10]; value /= 10;
                for (int segment = 0; segment < 7; segment++) digits[side, digit, segment].SetActive((mask & (1 << segment)) != 0);
            }
        }

        GameObject RoundedBox(string name, Vector3 pos, Vector3 size, float radius, Material material, bool solid = true)
        {
            var go = new GameObject(name); go.transform.SetParent(transform, false); go.transform.localPosition = pos;
            var mesh = RoundedMesh(name, size, radius, 12);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterial = material;
            if (solid)
            {
                // 56 unique hull vertices at three subdivisions stay below PhysX's convex polygon limit.
                var collider = go.AddComponent<MeshCollider>(); collider.sharedMesh = RoundedMesh(name + " collision", size, radius, 3);
                collider.convex = true;
            }
            return go;
        }

        Mesh RoundedMesh(string name, Vector3 size, float radius, int segments)
        {
            var mesh = new Mesh { name = name + " mesh" }; meshes.Add(mesh);
            var vertices = new List<Vector3>(); var triangles = new List<int>(); var normals = new List<Vector3>();
            Vector3 half = size * .5f;
            float r = Mathf.Min(radius, Mathf.Min(half.x, Mathf.Min(half.y, half.z)) * .99f);
            Vector3 inner = half - Vector3.one * r;
            foreach (var normal in new[] { Vector3.right, Vector3.left, Vector3.up, Vector3.down, Vector3.forward, Vector3.back })
            {
                Vector3 u = Mathf.Abs(normal.y) > .5f ? Vector3.right : Vector3.up;
                Vector3 v = Vector3.Cross(normal, u);
                int offset = vertices.Count;
                for (int y = 0; y <= segments; y++) for (int x = 0; x <= segments; x++)
                {
                    var cube = Vector3.Scale(normal + u * (2f * x / segments - 1) + v * (2f * y / segments - 1), half);
                    var core = new Vector3(Mathf.Clamp(cube.x, -inner.x, inner.x), Mathf.Clamp(cube.y, -inner.y, inner.y), Mathf.Clamp(cube.z, -inner.z, inner.z));
                    var n = (cube - core).normalized;
                    vertices.Add(core + n * r); normals.Add(n);
                }
                for (int y = 0; y < segments; y++) for (int x = 0; x < segments; x++)
                {
                    int a = offset + y * (segments + 1) + x, b = a + 1, c = a + segments + 1, d = c + 1;
                    triangles.Add(a); triangles.Add(b); triangles.Add(c);
                    triangles.Add(b); triangles.Add(d); triangles.Add(c);
                }
            }
            mesh.SetVertices(vertices); mesh.SetTriangles(triangles, 0); mesh.SetNormals(normals); mesh.RecalculateBounds();
            return mesh;
        }

        void Ramp(string name, Vector3 from, Vector3 to, float width, Material material)
        {
            Vector3 along = to - from; along.y = 0;
            Vector3 across = Vector3.Cross(Vector3.up, along.normalized) * (width * .5f);
            float bottom = Mathf.Min(from.y, to.y) - .25f;
            var vertices = new[] { from - across, from + across, to + across, to - across,
                new Vector3(from.x - across.x, bottom, from.z - across.z),
                new Vector3(from.x + across.x, bottom, from.z + across.z),
                new Vector3(to.x + across.x, bottom, to.z + across.z),
                new Vector3(to.x - across.x, bottom, to.z - across.z) };
            var mesh = new Mesh { name = name + " mesh" }; meshes.Add(mesh);
            mesh.vertices = vertices;
            mesh.triangles = new[] { 0,3,2, 0,2,1, 4,5,6, 4,6,7, 0,1,5, 0,5,4, 1,2,6, 1,6,5, 2,3,7, 2,7,6, 3,0,4, 3,4,7 };
            mesh.RecalculateNormals(); mesh.RecalculateBounds();
            var go = new GameObject(name); go.transform.SetParent(transform, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterial = material;
            var collider = go.AddComponent<MeshCollider>(); collider.sharedMesh = mesh; collider.convex = true;
        }
    }
}
