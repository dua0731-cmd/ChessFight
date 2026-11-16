using System;
using UnityEngine;

namespace ChessFight.PawnRush
{
    // R685 (승규 11-16 "이제 vfx를 한다"): the obstacles' dust, chips, sparks and glints (YardFx), made in code at the end of
    // Awake under "Obstacle effects" beside "Built". Switching that object off stops them; nothing else changes.
    public sealed partial class RoyalCarriageYard
    {
        [Serializable]
        public sealed class FxSettings
        {
            [Tooltip("The obstacles' dust, chips, sparks and glints (R685)")]
            public bool on = true;
            [Tooltip("The most the quality settings may turn on: 0 a third of the particles, 1 two thirds, 2 all")]
            [Range(0, 2)] public int maxTier = 2;
        }

        [Header("Look: obstacle effects")]
        [SerializeField] FxSettings fxLook = new FxSettings();

        void Fx()
        {
            if (root == null) return;
            BeltSlats();
            NoGoldEdges();
            if (!fxLook.on) return;
            var go = new GameObject("Obstacle effects");
            go.transform.SetParent(transform, false);
            go.AddComponent<YardFx>().Configure(root, fxLook.maxTier);
        }

        // R685 (승규 11-17 "북쪽 구간 물레방아 구간 노란색 손잡기 제외하고 노란색 테두리 칠해져 있는 부분 지워"): the code's gold
        // edges stop drawing everywhere in the yard - the jump lines (gap and landing edges, the loft's edge and lip), the
        // machines' trims and bands (storage beams, the jibs, the hoists' arms and wells, the gantry's beam), the gate and
        // awning trims and lintels, the towers' bands. Only the lifting waterwheel's gold handle and its posts stay (what a
        // pawn holds on to). Their colliders, if any, stay; nothing else changes. The crowns, hubs and locks are not edges.
        static readonly string[] GoldEdgeWords = { "trim", "edge", "lip", "band", "lintel", "door line" };
        static readonly string[] GoldKept = { "Handle", "Handle post" };

        void NoGoldEdges()
        {
            var gold = YardKit.Gold;
            int hidden = 0;
            foreach (var r in root.GetComponentsInChildren<Renderer>(true))
            {
                if (!r.enabled || r.sharedMaterial != gold || System.Array.IndexOf(GoldKept, r.name) >= 0) continue;
                string name = r.name.ToLowerInvariant();
                foreach (var word in GoldEdgeWords)
                    if (name.Contains(word))
                    {
                        r.enabled = false;
                        hidden++;
                        break;
                    }
            }
            Debug.Log("[마차뜰] 금색 테두리 " + hidden + "개를 숨겼습니다 (물레방아 손잡이는 그대로)");
        }

        // R685: the conveyor's slats, out of RcyConveyor, run along with the loads (ConveyorSlats), in the model's clay.
        // Only where the model hangs: without it the code's own belt picture stands as before.
        void BeltSlats()
        {
            foreach (var conveyor in root.GetComponentsInChildren<CargoConveyor>(true))
            {
                var model = conveyor.transform.Find("RcyConveyor (model)");
                if (model == null) continue;
                var clay = model.GetComponentInChildren<Renderer>(true);
                var go = new GameObject("Belt slats");
                go.transform.SetParent(conveyor.transform, false);
                go.AddComponent<ConveyorSlats>().Configure(conveyor, clay != null ? clay.sharedMaterial : null);
            }
        }
    }
}
