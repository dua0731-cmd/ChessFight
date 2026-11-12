using UnityEngine;
using ChessFight.Gameplay;

namespace ChessFight.PawnRush
{
    // Character controller reads both the travel and the backwards belt velocity.
    // The belt runs against the deck's forward, which is toward the goal once the pivot turns 180 degrees;
    // that reverse run is intended (승규 님, R258) - except in the round's war room, where since R518 the belt is held
    // still (LibraryShuttleDriver.BeltRuns, the user's list 13). The carriage's travel comes from LibraryShuttleDriver,
    // which moves it from the shared clock in the same physics step the riders read it (R259).
    public sealed class LibraryConveyorSurface : MonoBehaviour, IMovingSurface
    {
        // R394 (승규 11-04 "아직도 떠 보이고 밀리는 세기가 약해"): the belt pushes a rider back BeltPush times as fast as
        // its links are drawn running (0.64 m/s measured, R393), and the deck's top is set on the belt as it is drawn.
        // R425 (승규 11-05 "컨베이어 역방향 할 때 기물이랑 더 밀리게 해"): 2.2 -> 4, about 1.4 -> 2.6 m/s back along the deck
        // (a pawn runs 5 m/s, so it can still win against the belt, slower).
        const float BeltPush = 4f;

        LibraryShuttleDriver driver;
        bool fitted;

        /// <summary>The box's top has been set on the belt (the first physics step): from then on the deck is where a
        /// pawn walks on it (LibraryRookTurret lands its shots on it).</summary>
        public bool Fitted => fitted;

        void Awake() { driver = GetComponentInParent<LibraryShuttleDriver>(); }

        // R393: the belt's top run as the take moves it (LibraryShuttleDriver.BeltVelocity); the old fixed 0.644 m/s
        // back along the deck only if the links do not run.
        public Vector3 PointVelocity(Vector3 point)
        {
            var carriage = driver != null ? driver.CarriageVelocity : Vector3.zero;
            // R518: where the belt is held (the round's war room) the deck only travels with the carriage.
            if (driver != null && !driver.BeltRuns) return carriage;
            if (driver != null && driver.BeltMeasured) return carriage + (driver.BeltVelocity - carriage) * BeltPush;
            return carriage - transform.forward * (.644f * BeltPush);
        }

        public Quaternion DeltaRotation => Quaternion.identity;

        // R394: on the first physics step (the take already posed by the driver) the box's top goes where the belt's
        // top run is drawn. R395 ("아직도 기물과 컨베이어 사이에 떠보여"): drawn where a pawn walks - a link is a base
        // plate, a body whose top is the deck and two side guards 11 cm higher; R394 took the links' highest points (the
        // guards). Now each link's walking face is the highest level that faces up over a third of its footprint or
        // more (its mesh is readable), and the top run's is the level most links on top share (the drums' tilted
        // links left out).
        void FixedUpdate()
        {
            if (fitted || driver == null || !TryGetComponent<BoxCollider>(out var box)) return;
            fitted = true;
            var faces = new System.Collections.Generic.List<float>();
            foreach (var f in driver.GetComponentsInChildren<MeshFilter>(true))
            {
                if (!f.name.StartsWith("BELT_LINK_") || f.sharedMesh == null || !f.sharedMesh.isReadable) continue;
                float face = WalkingFace(f);
                if (!float.IsNaN(face)) faces.Add(face);
            }
            if (faces.Count == 0) return;
            faces.Sort();
            float belt = faces[faces.Count - 1 - faces.Count / 8];
            float top = transform.TransformPoint(box.center + Vector3.up * box.size.y * .5f).y;
            transform.position += Vector3.up * (belt - top);
            Debug.Log("[작전 회의실] 컨베이어 충돌 윗면을 벨트 판의 걷는 면에 맞춤(" + (belt - top).ToString("+0.00;-0.00") + " m, 판 " + faces.Count + "개)");
        }

        // The world height of a link's walking face: up-facing triangles (by their normals) summed by the centimetre,
        // the highest level holding a third of the link's footprint.
        static float WalkingFace(MeshFilter f)
        {
            var mesh = f.sharedMesh;
            var v = mesh.vertices;
            var n = mesh.normals;
            var tris = mesh.triangles;
            var toWorld = f.transform.localToWorldMatrix;
            var area = new System.Collections.Generic.Dictionary<int, float>();
            for (int k = 0; k < tris.Length; k += 3)
            {
                Vector3 a = toWorld.MultiplyPoint3x4(v[tris[k]]), b = toWorld.MultiplyPoint3x4(v[tris[k + 1]]), c = toWorld.MultiplyPoint3x4(v[tris[k + 2]]);
                float s = Vector3.Cross(b - a, c - a).magnitude * .5f;
                if (s < 1e-7f) continue;
                if (n.Length == v.Length)
                {
                    var up = toWorld.MultiplyVector(n[tris[k]] + n[tris[k + 1]] + n[tris[k + 2]]).normalized;
                    if (up.y < .9f) continue;
                }
                int key = Mathf.RoundToInt((a.y + b.y + c.y) / 3f * 100f);
                area[key] = (area.TryGetValue(key, out float sum) ? sum : 0f) + s;
            }
            var b0 = f.GetComponent<Renderer>().bounds;
            float footprint = b0.size.x * b0.size.z;
            int best = int.MinValue;
            foreach (var pair in area)
                if (pair.Value >= footprint / 3f && pair.Key > best) best = pair.Key;
            return best == int.MinValue ? float.NaN : best / 100f;
        }
    }
}
