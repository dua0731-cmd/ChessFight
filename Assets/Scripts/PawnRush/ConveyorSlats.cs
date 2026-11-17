using ChessFight.Gameplay;
using ChessFight.Network;
using UnityEngine;
using Solid = ChessFight.PawnRush.YardKit.Solid;

namespace ChessFight.PawnRush
{
    // R685 (승규 11-16 "컨테이너가 가만히 있는 데 박스만 움직이는 것 같아서 이상해 실제로 저 컨테이너가 움직이게 해"): the
    // conveyor's oak slats run along with its loads. They used to be part of the RcyConveyor model, so the belt stood
    // still while the loads slid over it; the model now has the bed and rails only and this draws the slats as the model
    // did (0.2 x 0.02 x 1.5 m, Pitch apart, top at the belt's top) and moves them along local +x at the belt's speed on
    // ObstacleClock, as the loads and the belt's carry (ConveyorBeltSurface) go, so every PC sees the same. A slat that
    // runs off one end comes back on at the other (the row only ever shifts by less than one pitch). Pictures only: the
    // belt's collider and its carry are CargoConveyor's.
    public sealed class ConveyorSlats : MonoBehaviour
    {
        const float Pitch = .45f, SlatLength = .2f, SlatThickness = .02f, EndMargin = .1f;

        CargoConveyor conveyor;
        Transform[] slats = new Transform[0];
        float half, first;

        public void Configure(CargoConveyor belt, Material material)
        {
            conveyor = belt;
            half = belt.Length * .5f;
            first = -half + .25f;   // the model's first slat
            int count = Mathf.FloorToInt((belt.Length - .25f) / Pitch) + 2;
            slats = new Transform[count];
            float across = belt.Width - .1f, y = belt.Height - SlatThickness * .5f;
            for (int i = 0; i < count; i++)
            {
                var slat = YardKit.Box(transform, "Belt slat", new Vector3(first + i * Pitch, y, 0f), new Vector3(SlatLength, SlatThickness, across),
                                       material != null ? material : YardKit.WoodLight, Solid.None);
                var r = slat.GetComponent<Renderer>();
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                slats[i] = slat.transform;
            }
        }

        void LateUpdate()
        {
            if (conveyor == null || slats.Length == 0) return;
            // How far the belt has run past a slat's place, 0 .. Pitch (the row repeats every pitch).
            float run = (float)((ObstacleClock.Now * conveyor.Speed) % Pitch);
            if (run < 0f) run += Pitch;
            for (int i = 0; i < slats.Length; i++)
            {
                float x = first - Pitch + run + i * Pitch;
                bool on = x - SlatLength * .5f > -half + EndMargin && x + SlatLength * .5f < half - EndMargin;
                var t = slats[i];
                if (t.gameObject.activeSelf != on) t.gameObject.SetActive(on);
                if (!on) continue;
                var p = t.localPosition;
                p.x = x;
                t.localPosition = p;
            }
        }
    }
}
