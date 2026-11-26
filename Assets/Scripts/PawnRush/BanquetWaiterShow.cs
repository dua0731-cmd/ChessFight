using System;
using System.Collections.Generic;
using UnityEngine;

namespace ChessFight.PawnRush
{
    // The waiters' walk on the dressed hall (대연회장 연출 R845): each waiter's figurine (bh_waiter's "BH1 waiter", one piece
    // with its LODs) waddles as he goes - a sway from foot to foot, a rise at the middle of each step, a little turn of
    // the shoulders - on the same stride as his rattling plate stack (PlateStackRattle.Stride: a clink a step). Only the
    // body: the tray and the stack (what is ridden and climbed) and their colliders are as built, and the sway is kept
    // small (the arm stays at the tray). Every pose is a function of how far he has walked - his root's z, itself the
    // obstacle clock's (HANDOFF rule 7) - so every PC sees the same and nothing is sent.
    [DefaultExecutionOrder(205)]
    public sealed class BanquetWaiterShow : MonoBehaviour
    {
        const float Sway = 1.3f, Rise = .025f, Turn = 1.6f;

        sealed class Body
        {
            public Transform root;
            public Transform[] parts;
            public Vector3[] at;
            public Quaternion[] turn;
            public Vector3 foot;
        }

        readonly List<Body> bodies = new List<Body>();

        public int Waiters => bodies.Count;
        // As last drawn: the most a body leans (degrees).
        public float Leaning { get; private set; }

        public void Configure(GrandBanquetHallDressing dressing)
        {
            foreach (var mover in dressing.GetComponentsInChildren<BanquetWaiter>(true))
            {
                var root = mover.transform;
                var model = GrandBanquetHallDressing.ModelOn(root, "bh_waiter");
                foreach (var part in BanquetShowKit.Parts(model))
                {
                    if (part.Key != "BH1 waiter") continue;
                    var b = new Body { root = root, parts = new Transform[part.Value.Count] };
                    b.at = new Vector3[b.parts.Length];
                    b.turn = new Quaternion[b.parts.Length];
                    var turnBack = Quaternion.Inverse(root.rotation);
                    var bounds = new Bounds();
                    bool any = false;
                    for (int i = 0; i < b.parts.Length; i++)
                    {
                        var t = part.Value[i].transform;
                        b.parts[i] = t;
                        b.at[i] = root.InverseTransformPoint(t.position);
                        b.turn[i] = turnBack * t.rotation;
                        if (t.TryGetComponent<Renderer>(out var r)) { if (!any) { bounds = r.bounds; any = true; } else bounds.Encapsulate(r.bounds); }
                    }
                    var middle = root.InverseTransformPoint(bounds.center);
                    b.foot = new Vector3(middle.x, root.InverseTransformPoint(bounds.min).y, middle.z);
                    bodies.Add(b);
                }
            }
            Debug.Log("[대연회장 웨이터 연출] 웨이터 몸 " + bodies.Count);
        }

        void LateUpdate()
        {
            float most = 0f;
            foreach (var b in bodies)
            {
                if (b.root == null) continue;
                float u = b.root.localPosition.z / PlateStackRattle.Stride;
                // a step every stride: the weight from one foot to the other (once every two steps), up between
                float sway = Sway * Mathf.Sin(Mathf.PI * u), rise = Rise * Mathf.Pow(Mathf.Sin(Mathf.PI * u), 2f);
                float turn = Turn * Mathf.Sin(Mathf.PI * u + .5f);
                // the walk is +z: the sway about z, the turn about y
                var lean = Quaternion.AngleAxis(turn, Vector3.up) * Quaternion.AngleAxis(sway, Vector3.forward);
                most = Mathf.Max(most, Mathf.Abs(sway));
                var foot = b.foot + new Vector3(0f, rise, 0f);
                for (int i = 0; i < b.parts.Length; i++)
                {
                    if (b.parts[i] == null) continue;
                    b.parts[i].SetPositionAndRotation(b.root.TransformPoint(foot + lean * (b.at[i] - b.foot)), b.root.rotation * (lean * b.turn[i]));
                }
            }
            Leaning = most;
        }
    }
}
