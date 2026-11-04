using ChessFight.Game;
using UnityEngine;

namespace ChessFight.PawnRush
{
    public static partial class Course01v4Builder
    {
        // R341 (승규 11-04 "해머 쪽 섬세하게 모델링 해줘"): the hall's two swinging hammers over the curving stair - kit 11
        // pendulums on black blockout posts and beams (GrandStairHallBuilder.Beats.cs Chandelier) - dressed in
        // hammer_v42.py's parts (PendulumHammer.fbx) in the palace look: cream fluted columns with gold ionic heads, dark
        // iron beams with gold fillets, an iron knee brace with gold scrolls, a gold hanger, and on the kit's Pivot a chain
        // of iron links to a dark iron ball with a gold crown, bands, medallions and finial. The parts go where the
        // blockout is (the column from the hall floor up under the beam, its head and brace along the beam), and the
        // blockout and the kit's cable, collars and ball draw nothing but keep their colliders, so the hits are as before.
        // The kit's swing marks stay.
        // R382 (승규 11-04, a shot of a steel mount with gold edges, a heavy gold chain and a polished ball: "해머 부분
        // 이렇게 해주고 해머 받치는 기둥 부분도 깔끔하게 교체 해"): the parts are hammer_v50.py's now - a broad steel
        // mount narrowing to a gold clevis, a pin and thirteen heavy gold links, a polished warm-metal ball under a small
        // gold cap; the stand a square brushed-steel post, beam and beam end with every cut corner gold, a plain foot and
        // head, and no knee brace (the beam runs on into the wall).
        const string HammerModel = "PawnRushGrandStairHall/PendulumHammer";
        const string HammerPrefix = "11 Pendulum (chandelier) ";
        const float HammerBaseTop = .48f, HammerCapitalHeight = .32f;   // hammer_v50.py BASE_TOP, CAPITAL_H
        static Material hammerSteel, hammerBall;

        static void HangHammers(Transform content, Material clay)
        {
            var model = Resources.Load<GameObject>(HammerModel);
            if (model == null)
            {
                Debug.LogError("[대계단 홀] 해머 모델을 불러오지 못했습니다: Resources/" + HammerModel + ".fbx");
                return;
            }
            var floor = FindUnder(content, "Hall floor (respawn)");
            int hammers = 0;
            foreach (var t in content.GetComponentsInChildren<Transform>(true))
            {
                if (!t.name.StartsWith(HammerPrefix) || t.name.EndsWith(" post") || t.name.EndsWith(" beam")) continue;
                var pivot = FindUnder(t, "Pivot");
                var post = FindUnder(content, t.name + " post");
                var beam = FindUnder(content, t.name + " beam");
                if (pivot == null || post == null || beam == null || floor == null || pivot.Find("Hammer swing") != null) continue;

                // The swinging part on the Pivot, the kit's cable, collars and ball under it drawing nothing.
                foreach (var r in pivot.GetComponentsInChildren<Renderer>(true)) r.enabled = false;
                HammerPart(model, "Hammer_Swing", "Hammer swing", pivot, pivot.position, pivot.rotation, Vector3.one, clay);
                // The hanger stays where the pivot rests, on the pendulum (it does not swing).
                HammerPart(model, "Hammer_Hanger", "Hammer hanger", t, pivot.position, pivot.rotation, Vector3.one, clay);

                // The beam in its own blockout's unit cube (stretched with it: its world scale is the blockout's); its end
                // over the post.
                if (beam.TryGetComponent<Renderer>(out var beamView)) beamView.enabled = false;
                HammerPart(model, "Hammer_Beam", "Hammer beam", beam, beam.position, beam.rotation, beam.lossyScale, clay);
                HammerPart(model, "Hammer_BeamEnd", "Hammer beam end", beam.parent, beam.TransformPoint(0f, 0f, -.5f), beam.rotation, Vector3.one, clay);

                // The post from the hall floor up under the beam, facing along it.
                if (post.TryGetComponent<Renderer>(out var postView)) postView.enabled = false;
                var along = Vector3.ProjectOnPlane(beam.forward, Vector3.up);
                var facing = Quaternion.LookRotation(along.sqrMagnitude > 1e-6f ? along : Vector3.forward, Vector3.up);
                float foot = floor.TransformPoint(0f, .5f, 0f).y, head = beam.TransformPoint(0f, -.5f, 0f).y;
                var axis = post.position;
                float shaft = head - HammerCapitalHeight - (foot + HammerBaseTop);
                HammerPart(model, "Hammer_ColBase", "Hammer column foot", post.parent, new Vector3(axis.x, foot, axis.z), facing, Vector3.one, clay);
                HammerPart(model, "Hammer_ColShaft", "Hammer column shaft", post.parent, new Vector3(axis.x, foot + HammerBaseTop, axis.z), facing,
                           new Vector3(1f, Mathf.Max(.01f, shaft), 1f), clay);
                HammerPart(model, "Hammer_ColCapital", "Hammer column head", post.parent, new Vector3(axis.x, head - HammerCapitalHeight, axis.z), facing, Vector3.one, clay);
                hammers++;
            }
            Debug.Log("[대계단 홀] 해머 " + hammers + "개를 새 모델로");
        }

        // A copy of one of the model's parts under `parent` at a place in the world, sized `scale` in the world along its
        // own axes, in the hall's materials and without colliders. The copy sits in a holder as it sits under the model's
        // root (the FBX import may turn its parts to Unity's axes), so the part is placed as the whole model would be.
        static void HammerPart(GameObject model, string part, string name, Transform parent, Vector3 at, Quaternion facing,
                               Vector3 scale, Material clay)
        {
            var source = model.transform.Find(part);
            if (source == null)
            {
                Debug.LogError("[대계단 홀] 해머 모델에 " + part + "가 없습니다");
                return;
            }
            var holder = new GameObject(name).transform;
            holder.SetParent(parent, false);
            holder.SetPositionAndRotation(at, facing);
            var s = parent.lossyScale;
            holder.localScale = new Vector3(scale.x / s.x, scale.y / s.y, scale.z / s.z);
            var view = Object.Instantiate(source.gameObject, holder, false);
            view.name = part;
            foreach (var r in view.GetComponentsInChildren<Renderer>(true))
            {
                var slots = r.sharedMaterials;
                for (int i = 0; i < slots.Length; i++)
                {
                    string slot = slots[i] != null ? slots[i].name : "";
                    slots[i] = PalaceMaterial(slot) ?? HammerMaterial(slot) ?? clay;
                }
                r.sharedMaterials = slots;
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            }
            foreach (var c in view.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(c);
        }

        // The hammer's brushed steel and its polished ball (its gold is the walls' Palace_Gold): the ball a warm metal, so
        // it takes the hall's candle colours as the shot's does.
        static Material HammerMaterial(string name)
        {
            if (!name.StartsWith("Hammer_")) return null;
            if (name.StartsWith("Hammer_Ball"))
            {
                if (hammerBall == null) hammerBall = PieceFigure.Lit(new Color(.66f, .55f, .47f), .86f, 1f);
                return hammerBall;
            }
            if (hammerSteel == null) hammerSteel = PieceFigure.Lit(new Color(.56f, .57f, .6f), .55f, .85f);
            return hammerSteel;
        }
    }
}
