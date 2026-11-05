using UnityEngine;

namespace ChessFight.PawnRush
{
    public static partial class Course01v4Builder
    {
        // R405 (승규 11-04, a shot of the round hall's grey star vault: "원형 벽 위에 쪽 모델링 텍스쳐 입히려는 데 무슨 컬러가
        // 좋을까" -> option A "크림 돌 + 금 테" -> "A 진행해"): the dome over the round wall (round_hall_v22.py Dome_1, in the
        // hall model since R325) dressed like the walls it rises from.
        //   ribs     HallV15_Lamella_NN_L/R (56) and their corbels HallV15_Rib_corbel_NN: the walls' cream limestone
        //            (PR_PalaceTrim, as the floors' mouldings) with gold on each rib's four cut corners - the faces that
        //            slant off the dome's radius, measured from the dome's centre (Blender (0, HY, ZS 25.4), found here
        //            from the crown ring: 19 m under its foot, on its axis)
        //   crown    HallV15_Crown_ring: the same limestone with its bulging belt (1.2 m, 0.6 m over its foot) in gold
        //   oculus   the frame, boss and star in the walls' gold, the leading over the glass in the window bars' matte
        //            silver (R385); the glass stays the crystal glass
        const string DomeRib = "HallV15_Lamella_", DomeCorbel = "HallV15_Rib_corbel_", DomeCrown = "HallV15_Crown_ring";
        const float CrownOverDomeCentre = 44.4f - 25.4f;           // round_hall_v22.py: the crown ring's foot, ZS
        const float CrownBeltFrom = .6f, CrownBeltTo = 1.8f;       // its belt (z 45.0 .. 46.2) over its foot (44.4)

        static void DomeVault(Transform hallModel)
        {
            var mould = Resources.Load<Material>(FloorMouldingTrim);
            if (mould == null)
            {
                Debug.LogError("[대계단 홀] 돔 재질의 바탕(Resources/" + FloorMouldingTrim + ".mat)을 불러오지 못했습니다");
                return;
            }
            Renderer crown = null;
            foreach (var r in hallModel.GetComponentsInChildren<Renderer>(true))
                if (r.name.StartsWith(DomeCrown)) { crown = r; break; }
            if (crown == null)
            {
                Debug.LogError("[대계단 홀] 돔 꼭대기 고리(" + DomeCrown + ")를 찾지 못했습니다");
                return;
            }
            var ring = crown.bounds;
            var centre = new Vector3(ring.center.x, ring.min.y - CrownOverDomeCentre, ring.center.z);

            // The ribs: stone, gold only on the cut corners (no gold on faces looking up, no height bands).
            var rib = new Material(mould) { name = "Dome rib (stone, gold corners)" };
            rib.SetFloat("_GoldUp", 2f);
            rib.SetVector("_GoldBandsA", new Vector4(-1000f, -1000f, -1000f, -1000f));
            rib.SetVector("_GoldBandsB", new Vector4(-1000f, -1000f, -1000f, -1000f));
            // R406: the centre anchored to the hall model (the zone is moved after it is built), and the cut corners'
            // band measured on the FBX (Blender, every Lamella face): cut corners .646 .. .716, the broad faces .927 and
            // over or about 0 - so .55 .. .8 holds the corners alone with room on both sides.
            HallAnchors(hallModel).Add(rib, "_DomeCentre", centre, false, 1f);
            rib.SetVector("_DomeSlant", new Vector4(.55f, .8f, 0f, 0f));

            // The crown ring: stone with its belt in gold (heights over the ring's own origin, as the shader reads them).
            var belt = new Material(mould) { name = "Dome crown ring (stone, gold belt)" };
            float foot = ring.min.y - crown.transform.position.y;
            belt.SetFloat("_GoldUp", 2f);
            belt.SetVector("_GoldBandsA", new Vector4(foot + CrownBeltFrom, foot + CrownBeltTo, -1000f, -1000f));
            belt.SetVector("_GoldBandsB", new Vector4(-1000f, -1000f, -1000f, -1000f));

            var gold = PalaceMaterial("Palace_Gold");
            var silver = MatteSilver();
            int ribs = 0, corbels = 0, oculus = 0;
            foreach (var r in hallModel.GetComponentsInChildren<Renderer>(true))
            {
                string name = r.name;
                Material m;
                if (name.StartsWith(DomeRib)) { m = rib; ribs++; }
                else if (name.StartsWith(DomeCorbel)) { m = rib; corbels++; }
                else if (name.StartsWith(DomeCrown)) m = belt;
                else if (name.StartsWith("HallV15_Oculus_") && !name.StartsWith("HallV15_Oculus_glass"))
                {
                    m = name.StartsWith("HallV15_Oculus_lead") ? silver : gold;
                    oculus++;
                }
                else continue;
                var slots = r.sharedMaterials;
                for (int i = 0; i < slots.Length; i++) slots[i] = m;
                r.sharedMaterials = slots;
            }
            Debug.Log("[대계단 홀] 돔: 뼈대 " + ribs + "개 · 받침 " + corbels + "개 = 크림 돌 + 금 모서리, 꼭대기 고리 = 돌 + 금 띠, 천창 금속 " +
                      oculus + "개(살은 은색), 돔 중심 " + centre);
        }

        // The hall model's keeper of world-space shader values (HallAnchoredMaterials), added once.
        static HallAnchoredMaterials HallAnchors(Transform hallModel) =>
            hallModel.TryGetComponent<HallAnchoredMaterials>(out var a) ? a : hallModel.gameObject.AddComponent<HallAnchoredMaterials>();
    }
}
