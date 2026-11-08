using System.Collections.Generic;
using ChessFight.ProtectKing;
using UnityEngine;

namespace ChessFight.RagdollLab
{
    // StateDrivenMover: offline clock rocker and entry flaps follow the occupied side.
    public sealed partial class ChessTimerMinigame
    {
        // Keep the entrance and clock fixed; expand only the rear into two square puzzle courts.
        readonly GameObject[,,] digits = new GameObject[2, 4, 7];
        static readonly int[] DigitMasks = { 0x3f, 0x06, 0x5b, 0x4f, 0x66, 0x6d, 0x7d, 0x07, 0x7f, 0x6f };
        const float ArenaWidth = 84f, ArenaDepth = 85f, ArenaCentreZ = 15.5f;
        const float DefuseSize = 42f, DefuseStartZ = 9f, ExitZ = 51f, TileSize = 4f;
        const float ApproachStartZ = -22f, ApproachEndZ = -.7f, ApproachHeight = 5.7f;

        const float RockerHeight = 5f, RockerThickness = 2f;
        const float RockerLocalTop = RockerThickness * .5f;

        void BuildArena()
        {
            var ivory = Mat(new Color(.90f, .85f, .72f));
            var walnut = Mat(new Color(.40f, .31f, .25f));
            var charcoal = Mat(new Color(.075f, .083f, .095f));
            var rubber = Mat(new Color(.035f, .04f, .045f));
            var cream = Mat(new Color(.98f, .96f, .88f));
            var lcd = Mat(new Color(.63f, .72f, .64f));
            teamMaterials[0] = Mat(new Color(.90f, .91f, .92f));
            teamMaterials[1] = Mat(new Color(.13f, .14f, .16f));
            foreach (var material in materials) material.SetFloat("_Glossiness", .24f);
            for (int x = 0; x < 18; x++) for (int z = 0; z < 12; z++)
                Box("Entrance chess floor", new Vector3((x - 8.5f) * 3, -.25f, -25.5f + z * 3),
                    new Vector3(3, .5f, 3), (x + z) % 2 == 0 ? ivory : walnut);
            Box("Square defuse court foundation", new Vector3(0, -.3f, DefuseStartZ + DefuseSize * .5f),
                new Vector3(ArenaWidth, .6f, DefuseSize), walnut);
            BuildExitConfluence(ivory, walnut, cream);

            // One clock and one mechanically coupled rocker; the two teams occupy its halves.
            RoundedBox("Clock rubber base", new Vector3(0, .25f, 3), new Vector3(22.4f, .5f, 10.4f), .23f, rubber, false);
            RoundedBox("Single chess clock housing", new Vector3(0, 2.6f, 3), new Vector3(22, 5.2f, 10), 1.1f, charcoal);
            var top = RoundedBox("Coupled white rocker", new Vector3(0, RockerHeight, 3), new Vector3(20.4f, RockerThickness, 8.5f), .24f, cream);
            occupancy = top.AddComponent<ChessTimerPlate>();
            rocker = top.AddComponent<Rigidbody>(); rocker.isKinematic = true; rocker.useGravity = false;
            rocker.interpolation = RigidbodyInterpolation.Interpolate;
            ImportedMovingSurface.BindDynamic(rocker, point => Vector3.Cross(rockerAngularVelocity, point - rocker.position),
                () => rockerDelta, () => isActiveAndEnabled);
            var seam = Box("Button centre seam", new Vector3(0, RockerHeight + RockerLocalTop + .005f, 3), new Vector3(.10f, .012f, 8.1f), rubber, false);
            seam.transform.SetParent(top.transform, true);
            RoundedBox("Shared LCD bezel", new Vector3(0, 2.1f, -2.06f), new Vector3(19.8f, 1.9f, .24f), .11f, rubber, false);
            for (int control = 0; control < 4; control++)
            {
                float cx = (control - 1.5f) * 1.4f;
                RoundedBox("Clock control button", new Vector3(cx, .85f, -1.9f), new Vector3(.85f, .4f, .35f), .16f, cream, false);
                var icon = Label("Clock control icon", new Vector3(cx, .85f, -2.09f), new[] { "+", "O", ">", "-" }[control], .2f, .65f, .28f);
                icon.color = Color.black;
            }
            for (int side = 0; side < 2; side++)
            {
                float x = TeamX(side); var accent = teamMaterials[side];
                var stripe = RoundedBox("Button side colour", new Vector3(x, RockerHeight + RockerLocalTop + .01f, 6.6f), new Vector3(8, .015f, .18f), .006f, accent, false);
                stripe.transform.SetParent(top.transform, true);
                RoundedBox("Team LCD window", new Vector3(x, 2.1f, -2.2f), new Vector3(8.8f, 1.5f, .1f), .045f, lcd, false);
                clocks[side] = Label("LCD countdown", new Vector3(x - 1.8f, 2.15f, -2.27f), "00:45", .62f, 4f, 1.15f);
                crowdDisplays[side] = Label("LCD occupant count", new Vector3(x + 2.4f, 2.1f, -2.27f), "00\nPLAYERS", .32f, 2.6f, 1.1f);
                clocks[side].GetComponent<Renderer>().enabled = false;
                crowdDisplays[side].color = new Color(.06f, .10f, .08f);
                BuildDigits(side, x - 1.8f, rubber);
                RoundedBox("Clock status frame", new Vector3(x, 3.5f, -2.2f), new Vector3(8.8f, .65f, .1f), .04f, lcd, false);
                clockStatus[side] = Label("Clock status", new Vector3(x, 3.5f, -2.32f), "", .25f, 8.2f, .45f);
                clockStatus[side].color = new Color(.06f, .10f, .08f);
                Label("Top team marker", new Vector3(x, 7.2f, 7), side == 0 ? "WHITE BUTTON" : "BLACK BUTTON", .35f);
                float approachX = side == 0 ? -15 : 15;
                Ramp("Walk-up approach", new Vector3(approachX, 0, ApproachStartZ), new Vector3(approachX, ApproachHeight, ApproachEndZ), 5, accent);
                Box("Walk-up landing", new Vector3(approachX, ApproachHeight - .25f, 1.15f), new Vector3(5, .5f, 3.7f), cream);
                var flap = Box("Following entry flap", Vector3.zero, Vector3.one, cream);
                var body = flap.AddComponent<Rigidbody>(); body.isKinematic = true; body.useGravity = false;
                body.interpolation = RigidbodyInterpolation.Interpolate; entryFlaps[side] = body;
                ImportedMovingSurface.BindDynamic(body, point => body.GetPointVelocity(point),
                    () => body.angularVelocity.sqrMagnitude < .000001f ? Quaternion.identity :
                        Quaternion.AngleAxis(body.angularVelocity.magnitude * Time.fixedDeltaTime * Mathf.Rad2Deg, body.angularVelocity.normalized), () => isActiveAndEnabled);
                PositionEntryFlap(side, Quaternion.identity, true);
                Label("Approach sign", new Vector3(approachX, .8f, -23), "WALK UP", .3f);

                gates[side] = Box("Team exit gate", new Vector3(BoardCentreX(side), 2, ExitZ), new Vector3(6, 4, .5f), accent);
                Label("Exit label", new Vector3(BoardCentreX(side), 4.6f, ExitZ - .4f), "NEXT STAGE", .35f);
            }
            boardMaterials = new[] { ivory, walnut, cream, charcoal };
            Box("Defuse area divider", new Vector3(0, 2.25f, 29.5f), new Vector3(.5f, 4.5f, 43.5f), walnut);
            Box("Exit wall centre", new Vector3(0, 2, ExitZ), new Vector3(36, 4, .5f), walnut);
            Box("Exit wall left", new Vector3(-33, 2, ExitZ), new Vector3(18, 4, .5f), walnut);
            Box("Exit wall right", new Vector3(33, 2, ExitZ), new Vector3(18, 4, .5f), walnut);
            for (int side = 0; side < 2; side++)
            {
                float sign = side == 0 ? -1 : 1;
                Box("Entrance side boundary", new Vector3(sign * 27, 1.5f, -9), new Vector3(.5f, 3, 36), walnut);
                Box("Court shoulder boundary", new Vector3(sign * 34.5f, 1.5f, 9), new Vector3(15, 3, .5f), walnut);
                Box("Court outer boundary", new Vector3(sign * 42, 2.5f, 33.5f), new Vector3(.5f, 5, 49), walnut);
            }
            rocker.transform.localScale = new Vector3(1f, .7f, 1f);
            for (int side = 0; side < 2; side++) PositionEntryFlap(side, Quaternion.identity, true);
            Box("Outer front boundary", new Vector3(0, 1.5f, -27), new Vector3(54, 3, .5f), walnut);
        }

        // Both team exits meet on a shared landing before the single next-stage entrance.
        void BuildExitConfluence(Material floor, Material rail, Material trim)
        {
            for (int team = 0; team < 2; team++)
            {
                float x = BoardCentreX(team);
                Box("Team exit approach", new Vector3(x, -.25f, 55), new Vector3(6, .5f, 8), floor);
                for (int edge = -1; edge <= 1; edge += 2)
                    Box("Approach guardrail", new Vector3(x + edge * 3.25f, .75f, 54.5f),
                        new Vector3(.5f, 1.5f, 7), rail);
                Box("Team route stripe", new Vector3(x, .015f, 55), new Vector3(.25f, .02f, 7), teamMaterials[team], false);
                float sign = team == 0 ? -1 : 1;
                Box("Landing front guardrail", new Vector3(sign * 9, .75f, 58), new Vector3(18, 1.5f, .5f), rail);
                Box("Landing rear guardrail", new Vector3(sign * 14.25f, .75f, 66), new Vector3(20, 1.5f, .5f), rail);
                Box("Landing end guardrail", new Vector3(sign * 24.25f, .75f, 62), new Vector3(.5f, 1.5f, 8.5f), rail);
                Box("Shared path guardrail", new Vector3(sign * 4.25f, .75f, 77), new Vector3(.5f, 1.5f, 22), rail);
                Box("Next stage arch pillar", new Vector3(sign * 4.75f, 3, 86), new Vector3(1, 6, 1), rail);
            }
            Box("Shared merge landing", new Vector3(0, -.25f, 62), new Vector3(48, .5f, 8), floor);
            Box("Shared next stage path", new Vector3(0, -.25f, 77), new Vector3(8, .5f, 22), floor);
            Box("Next stage entrance header", new Vector3(0, 6, 86), new Vector3(10.5f, 1, 1), rail);
            Label("Shared next stage sign", new Vector3(0, 6, 85.45f), "다음 스테이지", .4f, 8, .7f);
            // A short receiving platform makes the prototype entrance walkable without a scene transition.
            Box("Next stage receiving platform", new Vector3(0, -.25f, 92), new Vector3(14, .5f, 8), trim);
            Box("Prototype end boundary", new Vector3(0, .75f, 96), new Vector3(14, 1.5f, .5f), rail);
            for (int side = -1; side <= 1; side += 2)
                Box("Receiving platform guardrail", new Vector3(side * 7, .75f, 92), new Vector3(.5f, 1.5f, 8), rail);
        }

        void MoveRocker()
        {
            float tilt = Mathf.Clamp(rockerTilt, 0, 3);
            float target = rules.PressedTeam == 0 ? tilt : rules.PressedTeam == 1 ? -tilt : 0;
            float next = Mathf.MoveTowards(rockerAngle, target, Mathf.Max(.1f, rockerSpeed) * Time.fixedDeltaTime);
            rockerDelta = Quaternion.AngleAxis(next - rockerAngle, Vector3.forward);
            rockerAngularVelocity = Vector3.forward * ((next - rockerAngle) * Mathf.Deg2Rad / Time.fixedDeltaTime);
            rockerAngle = next;
            var rotation = Quaternion.AngleAxis(next, Vector3.forward);
            rocker.MoveRotation(rotation);
            for (int side = 0; side < 2; side++) PositionEntryFlap(side, rotation, false);
        }
        void PositionEntryFlap(int side, Quaternion rotation, bool immediate)
        {
            float sign = side == 0 ? -1 : 1;
            var from = new Vector3(sign * 15, ApproachHeight, 3);
            var to = rocker.position + rotation * Vector3.Scale(new Vector3(sign * 9.8f, RockerLocalTop, 0), rocker.transform.localScale);
            var facing = Quaternion.LookRotation((to - from).normalized, Vector3.up);
            var centre = (from + to) * .5f - facing * Vector3.up * .10f;
            var body = entryFlaps[side];
            body.transform.localScale = new Vector3(5, .2f, Vector3.Distance(from, to));
            if (immediate) { body.position = centre; body.rotation = facing; }
            else { body.MovePosition(centre); body.MoveRotation(facing); }
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
