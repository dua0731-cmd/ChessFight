using System;
using System.Collections.Generic;
using UnityEngine;

namespace ChessFight.PawnRush
{
    // B1 the entry passage, the servery and the waiters' parade (seoungtest R837; ArtSource/PawnRush/BanquetHall/
    // bh_b1_servery_model.py, shapes after the R836 sheets BH_A03 and BH_A11): the screens, the counter and its ramp, the
    // kitchen's doorway and the pantry's, and each waiter (the user's figurine, bh_figures.py) with his tray and plate
    // stack riding his root. Colliders, floors and the waiters' motion stay the builder's.
    public sealed partial class GrandBanquetHallDressing
    {
        static readonly Hang[] ServeryHangs =
        {
            new Hang { zone = "B1", model = "bh_screens", covers = new[] { "BH screen S", "BH screen E", "BH screen cap S", "BH passage end" } },
            new Hang { zone = "B1", model = "bh_counter", covers = new[] { "BH counter", "BH counter ramp", "BH spare plates" } },
            new Hang { zone = "B1", model = "bh_kitchen", covers = new[] { "BH kitchen wall", "BH kitchen roof", "BH kitchen lintel" } },
            new Hang { zone = "B1", model = "bh_pantry", covers = new[] { "BH pantry post", "BH pantry lintel", "BH pantry wall W", "BH pantry roof" } },
            Waiter(1), Waiter(2), Waiter(3), Waiter(4),
        };

        static Hang Waiter(int k) => new Hang
        {
            zone = "B1", model = "bh_waiter", on = "BH waiter " + k, under = "BH waiter " + k,
            covers = new[] { "BH tray", "BH plate stack", "BH waiter body", "BH waiter head", "BH waiter arm" },
        };

        // The kitchen's half doors (user 11-25: "열렸다 닫히게"): each leaf on a pivot at its hinge (bh_b1_servery_model
        // LEAF_HINGES), swung out into the hall as each tray comes through (KitchenDoors).
        static readonly Action<GrandBanquetHallDressing> KitchenLeaves = d =>
        {
            var w = d.Leaf("W", new Vector3(26.11f, 1.6f, 3.645f));
            var e = d.Leaf("E", new Vector3(31.51f, 1.6f, 3.645f));
            if (w == null || e == null) return;
            var waiters = new List<Transform>();
            foreach (var mover in d.GetComponentsInChildren<BanquetWaiter>(true)) waiters.Add(mover.transform);
            d.gameObject.AddComponent<KitchenDoors>().Configure(w, e, waiters);
        };

        Transform Leaf(string side, Vector3 hinge)
        {
            var fbx = Resources.Load<GameObject>(ModelFolder + "B1/bh_kitchen_leaf_" + side);
            if (fbx == null) return null;
            var pivot = new GameObject("BH kitchen leaf " + side + " hinge").transform;
            pivot.SetParent(transform, false);
            pivot.localPosition = hinge;
            HangOn(pivot, fbx);
            return pivot;
        }
    }

    // The kitchen's two half doors, a picture on the waiters' clock-driven places (nothing is sent): a leaf swings out
    // 90 degrees into the hall (west about +y the one way, east the other) as a tray's front comes within 0.35 s of the
    // doorway, stays open while the tray goes through, and swings shut 0.35 s after its back has passed. Open, a leaf
    // lies along its post's doorway face, off the trays' way (x >= 26.3) and the waiter's body (x <= 30.8).
    [DefaultExecutionOrder(200)]
    public sealed class KitchenDoors : MonoBehaviour
    {
        // A waiter root's z (the tray's middle) at which the leaves start to open, are open, start to close, are shut:
        // the doorway's plane is at z 3.645 and the tray is 1.5 m round; 2.2 m/s for 0.35 s is 0.77 m.
        public const float OpenFrom = 1.38f, OpenAt = 2.15f, CloseFrom = 5.15f, ShutAt = 5.92f, Swing = 90f;

        [SerializeField] Transform west, east;
        [SerializeField] List<Transform> waiters = new List<Transform>();

        public void Configure(Transform westPivot, Transform eastPivot, List<Transform> waiterRoots)
        {
            west = westPivot;
            east = eastPivot;
            waiters = waiterRoots;
        }

        // How open the doors are (0 shut .. 1 open) for a tray's middle at z.
        public static float Openness(float z)
        {
            float a = Mathf.Clamp01((z - OpenFrom) / (OpenAt - OpenFrom)), b = Mathf.Clamp01((z - CloseFrom) / (ShutAt - CloseFrom));
            a = a * a * (3f - 2f * a);
            b = b * b * (3f - 2f * b);
            return a * (1f - b);
        }

        void LateUpdate()
        {
            float open = 0f;
            foreach (var w in waiters)
                if (w != null) open = Mathf.Max(open, Openness(w.localPosition.z));
            if (west != null) west.localRotation = Quaternion.Euler(0f, -Swing * open, 0f);
            if (east != null) east.localRotation = Quaternion.Euler(0f, Swing * open, 0f);
        }
    }
}
