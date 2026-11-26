using System.Collections.Generic;
using ChessFight.Gameplay;
using UnityEngine;

namespace ChessFight.PawnRush
{
    // The seated guests' show on the dressed hall (대연회장 연출 R845): the nine guests (bh_guests: "BH2 guest 1" .. "9",
    // each figurine one piece with its LODs, on the chairs west of the long table, facing it) watch the magician's trick
    // (TableclothTrick's timing, as BanquetTableShow): as the drum rolls they lean in and turn toward the magician at the
    // table's south end; when the cloth is pulled they start back from the table, a little each in turn as the cloth's end
    // passes them; when it is laid again they bob as if clapping and turn back. Whole figurines turned about their seat
    // (a figurine has no neck to turn the head alone). A function of the shared obstacle clock (HANDOFF rule 7).
    [DefaultExecutionOrder(206)]
    public sealed class BanquetGuestShow : MonoBehaviour
    {
        const float Lean = 3f, Start = 10f, Look = 18f, Clap = .035f;

        sealed class Guest
        {
            public Transform[] parts;
            public Vector3[] at;
            public Quaternion[] turn;
            public Vector3 seat;
            public float toTable;               // +1: the table is +x of him
            public float toMagician;            // degrees to turn to face the magician (about y)
            public float along;                 // metres from the table's south end
        }

        readonly List<Guest> guests = new List<Guest>();
        TableclothTrick trick;
        Transform frame;
        BanquetClothRun run;

        public int Guests => guests.Count;
        // As last drawn: the most a guest is turned from his rest (degrees).
        public float Moved { get; private set; }

        public void Configure(GrandBanquetHallDressing dressing, TableclothTrick tableclothTrick)
        {
            frame = dressing.transform;
            trick = tableclothTrick;
            run = new BanquetClothRun(GrandBanquetHallBuilder.ClothZ1 - GrandBanquetHallBuilder.TableZ0, trick);
            var model = GrandBanquetHallDressing.ModelOn(frame, "bh_guests");
            var turnBack = Quaternion.Inverse(frame.rotation);
            var magician = new Vector3(GrandBanquetHallBuilder.RunnerX, 0f, 15.2f);
            float middleX = (GrandBanquetHallBuilder.TableX0 + GrandBanquetHallBuilder.TableX1) * .5f;
            foreach (var part in BanquetShowKit.Parts(model))
            {
                if (!part.Key.StartsWith("BH2 guest")) continue;
                var g = new Guest { parts = new Transform[part.Value.Count] };
                g.at = new Vector3[g.parts.Length];
                g.turn = new Quaternion[g.parts.Length];
                var bounds = new Bounds();
                bool any = false;
                for (int i = 0; i < g.parts.Length; i++)
                {
                    var t = part.Value[i].transform;
                    g.parts[i] = t;
                    g.at[i] = frame.InverseTransformPoint(t.position);
                    g.turn[i] = turnBack * t.rotation;
                    if (t.TryGetComponent<Renderer>(out var r)) { if (!any) { bounds = r.bounds; any = true; } else bounds.Encapsulate(r.bounds); }
                }
                var middle = frame.InverseTransformPoint(bounds.center);
                g.seat = new Vector3(middle.x, frame.InverseTransformPoint(bounds.min).y, middle.z);
                g.toTable = middle.x < middleX ? 1f : -1f;
                var facing = new Vector3(g.toTable, 0f, 0f);
                var to = magician - g.seat;
                to.y = 0f;
                g.toMagician = Mathf.Clamp(Vector3.SignedAngle(facing, to, Vector3.up), -Look, Look);
                g.along = g.seat.z - GrandBanquetHallBuilder.TableZ0;
                guests.Add(g);
            }
            Debug.Log("[대연회장 손님 연출] 손님 " + guests.Count);
        }

        void LateUpdate()
        {
            if (trick == null || !run.Set) return;
            float u = run.Phase(ObstacleClock.Now), most = 0f;
            foreach (var g in guests)
            {
                Pose(g, u, out float lean, out float look, out float bob);
                // lean: + toward the table (about the axis along the table, z)
                var turn = Quaternion.AngleAxis(look, Vector3.up) * Quaternion.AngleAxis(-lean * g.toTable, Vector3.forward);
                most = Mathf.Max(most, Quaternion.Angle(Quaternion.identity, turn));
                var seat = g.seat + new Vector3(0f, bob, 0f);
                for (int i = 0; i < g.parts.Length; i++)
                {
                    if (g.parts[i] == null) continue;
                    g.parts[i].SetPositionAndRotation(frame.TransformPoint(seat + turn * (g.at[i] - g.seat)), frame.rotation * (turn * g.turn[i]));
                }
            }
            Moved = most;
        }

        // Lean (degrees, + toward the table), look (degrees about y) and bob (m) at phase `u` of the trick.
        void Pose(Guest g, float u, out float lean, out float look, out float bob)
        {
            lean = look = bob = 0f;
            float roll = Mathf.Max(.01f, run.PullAt - run.RollAt);
            if (u >= run.RollAt && u < run.PullAt)
            {
                float k = BanquetShowKit.Smooth((u - run.RollAt) / roll);
                lean = Lean * k;
                look = g.toMagician * k;
                return;
            }
            if (u >= run.PullAt && u < run.LayAt)
            {
                // the start: when the cloth's end goes past him, back from the table, and settling
                float since = u - run.Leaves(g.along);
                float jolt = since < 0f ? 0f : Start * (1f - Mathf.Exp(-since / .07f)) * Mathf.Exp(-since / .9f);
                lean = Lean * Mathf.Exp(-(u - run.PullAt) / .2f) - jolt;
                look = g.toMagician * (since < 0f ? 1f : .35f + .65f * Mathf.Exp(-since / .5f));
                return;
            }
            if (u >= run.LayAt)
            {
                // the cloth back under the ware: clapping, turning back to the table
                float since = u - run.LayAt, held = .35f * g.toMagician;
                look = held * Mathf.Exp(-since / .6f);
                float clap = Mathf.Clamp01(1f - since / 1.4f);
                bob = Clap * clap * Mathf.Abs(Mathf.Sin(2f * Mathf.PI * 3.2f * since));
                lean = 1.2f * clap * Mathf.Abs(Mathf.Sin(2f * Mathf.PI * 3.2f * since));
            }
        }
    }
}
