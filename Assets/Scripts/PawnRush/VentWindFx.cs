using System.Collections.Generic;
using ChessFight.Gameplay;
using ChessFight.ProtectKing;
using ChessFight.VFX;
using UnityEngine;

namespace ChessFight.PawnRush
{
    // R351 (승규 11-04 "선풍기 vfx 적용 시켜줘 데칼 말고", the grand stair hall's royal fans): a vent's wind on the vent's own
    // clock. Every frame its layers are put in the state they have at the time since its last blow started (the same
    // time AirVent judges its wind by, from ObstacleClock), so every PC draws the same air without a packet. The layers come
    // from Resources/VfxSets/Hazards "Vent_Wind" (menu ChessFight/VFX/Hazard/Build V1), made in Play only, out of the
    // vent's VFXPoint along its front:
    //   V1 Swirl    curling arms on the fan's face, stirring through the warning, full while it blows
    //   V1 Streaks  streaks of air wound round the fan's axis, straightening as they run out across the way
    //   V1 Motes    dust riding the flow
    // in place of the kit's wind picture (its gold air-stream rods and arrow tips: renderers off, the vent still toggles
    // them). Before the menu has been run the layers are made here from their shaders with VfxHazardLooks' values.
    // R554 (승규 11-10 "지금 있는 거에서 A를 추가해", mockup A "휘어 보이는 공기"): on top of those, the blast as bent air
    // (VentWindHaze, Resources/PawnRushGrandStairHall/VentWindHaze.shader): the wall and the way behind it ripple through
    // a cone of air out of the fan, a little at its mouth through the warning, out to the way's far side as it blows.
    [DisallowMultipleComponent]
    public sealed class VentWindFx : MonoBehaviour
    {
        const string SetName = "Hazards", WindKey = "Vent_Wind";
        const string FlowShader = "ChessFight/VFX/Wind Flow", SwirlShader = "ChessFight/VFX/Wind Swirl";

        AirVent vent;
        VfxWindSwirl swirl;
        readonly List<VfxWindFlow> flows = new List<VfxWindFlow>();
        int made;

        // R554 bent air: a box round the cone along the anchor's +z, its material driven by the vent's clock.
        const string HazeShader = "PawnRushGrandStairHall/VentWindHaze";
        const float HazeLength = 5.5f, HazeNear = .9f, HazeFar = 1.5f, HazeAxisHeight = 1.3f;
        const float HazeFront = 15f;      // m/s the blast's front runs out at when it starts
        const float HazeWarnReach = .14f, HazeWarnAmount = .45f, HazeOff = .5f, HazeOn = .15f;
        static readonly int AmountId = Shader.PropertyToID("_Amount"), ReachId = Shader.PropertyToID("_Reach"),
            PhaseId = Shader.PropertyToID("_Phase"), FloorId = Shader.PropertyToID("_FloorY"),
            LengthId = Shader.PropertyToID("_Length"), RadiusId = Shader.PropertyToID("_Radius");
        Renderer haze;
        Material hazeMaterial;

        void Start()
        {
            vent = GetComponent<AirVent>();
            if (vent == null)
            {
                enabled = false;
                return;
            }
            var anchor = vent.VFXPoint != null ? vent.VFXPoint : transform;
            var set = VfxSet.Load(SetName);
            var prefab = set != null ? set.Find(WindKey) : null;
            if (prefab != null)
            {
                var fx = Instantiate(prefab, anchor, false);
                fx.name = prefab.name;
                // The vent's clock drives it, not the cue's own.
                if (fx.TryGetComponent<VfxCue>(out var cue)) cue.enabled = false;
                swirl = fx.GetComponentInChildren<VfxWindSwirl>(true);
                flows.AddRange(fx.GetComponentsInChildren<VfxWindFlow>(true));
            }
            else
            {
                swirl = Made<VfxWindSwirl>(anchor, "V1 Swirl", SwirlShader, Resources.GetBuiltinResource<Mesh>("Quad.fbx"));
                foreach (var layer in VfxHazardLooks.WindLayers)
                {
                    var flow = Made<VfxWindFlow>(anchor, layer, FlowShader, null);
                    if (flow == null) continue;
                    VfxHazardLooks.Apply(layer, flow);
                    flows.Add(flow);
                }
                if (made > 0) Debug.Log("[대계단 홀] 환풍기 바람 효과 " + made + "개를 셰이더 기본값으로 만들었습니다(프리팹은 메뉴 ChessFight/VFX/Hazard/Build)");
            }
            if (vent.WindVisual != null)
                foreach (var r in vent.WindVisual.GetComponentsInChildren<Renderer>(true)) r.enabled = false;
            Haze(anchor);
        }

        void Haze(Transform anchor)
        {
            var shader = Resources.Load<Shader>(HazeShader);
            if (shader == null)
            {
                Debug.LogWarning("[대계단 홀] 환풍기 공기 굴절 셰이더가 없습니다: Resources/" + HazeShader);
                return;
            }
            var part = new GameObject("V1 Haze");
            part.transform.SetParent(anchor, false);
            float w = HazeFar + .1f;
            var box = new Mesh { name = "Vent haze box" };
            var v = new Vector3[8];
            for (int i = 0; i < 8; i++) v[i] = new Vector3((i & 1) == 0 ? -w : w, (i & 2) == 0 ? -w : w, (i & 4) == 0 ? 0f : HazeLength);
            box.vertices = v;
            box.triangles = new[] { 0, 2, 1, 1, 2, 3, 4, 5, 6, 5, 7, 6, 0, 1, 4, 1, 5, 4, 2, 6, 3, 3, 6, 7, 0, 4, 2, 2, 4, 6, 1, 3, 5, 3, 7, 5 };
            box.RecalculateBounds();
            part.AddComponent<MeshFilter>().sharedMesh = box;
            var view = part.AddComponent<MeshRenderer>();
            hazeMaterial = new Material(shader) { name = "Vent wind haze" };
            hazeMaterial.SetFloat(LengthId, HazeLength);
            hazeMaterial.SetVector(RadiusId, new Vector4(HazeNear, HazeFar, 0f, 0f));
            view.sharedMaterial = hazeMaterial;
            view.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            view.receiveShadows = false;
            view.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
            view.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
            view.enabled = false;
            haze = view;
        }

        // tau: seconds since the last blow started (as the layers), cycle - warn .. cycle the warning before the next.
        void SeekHaze(float tau, float cycle)
        {
            if (haze == null) return;
            float warn = Mathf.Max(0f, vent.WarningDuration), blow = Mathf.Max(.1f, vent.BlowDuration);
            float amount = 0f, reach = 1f;
            if (tau < blow)
            {
                amount = Mathf.Clamp01(tau / HazeOn);
                reach = Mathf.Clamp01(HazeWarnReach + tau * HazeFront / HazeLength);
            }
            else if (tau < blow + HazeOff) amount = 1f - (tau - blow) / HazeOff;
            else if (warn > 0f && tau >= cycle - warn && tau < cycle)
            {
                amount = HazeWarnAmount * (tau - (cycle - warn)) / warn;
                reach = HazeWarnReach;
            }
            haze.enabled = amount > .001f;
            if (!haze.enabled) return;
            hazeMaterial.SetFloat(AmountId, amount);
            hazeMaterial.SetFloat(ReachId, reach);
            hazeMaterial.SetFloat(PhaseId, Time.time * 3f);
            hazeMaterial.SetFloat(FloorId, haze.transform.position.y - HazeAxisHeight + .02f);
        }

        void OnDestroy()
        {
            if (hazeMaterial != null) Destroy(hazeMaterial);
        }

        // A layer without its prefab: a mesh at the anchor with a material on its shader (a null mesh is the layer's own).
        T Made<T>(Transform anchor, string name, string shaderName, Mesh mesh) where T : Component
        {
            var shader = Shader.Find(shaderName);
            if (shader == null)
            {
                Debug.LogWarning("[대계단 홀] 환풍기 바람 셰이더가 없습니다(" + shaderName + "): 메뉴 ChessFight/VFX/Hazard/Build를 한 번 누르세요");
                return null;
            }
            var part = new GameObject(name + " (no prefab)");
            part.transform.SetParent(anchor, false);
            part.AddComponent<MeshFilter>().sharedMesh = mesh;
            var view = part.AddComponent<MeshRenderer>();
            view.sharedMaterial = new Material(shader) { name = name + " (no prefab)" };
            view.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            view.receiveShadows = false;
            view.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
            view.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
            made++;
            return part.AddComponent<T>();
        }

        void LateUpdate()
        {
            float cycle = vent.CycleDuration, lead = Mathf.Max(0f, vent.IdleDuration) + Mathf.Max(0f, vent.WarningDuration);
            // As AirVent: the time into its cycle from the shared clock, then the time since its last blow started.
            float into = ObstacleContext.PeriodicTime(ObstacleClock.Now + vent.phaseOffset, vent.StartDelay, cycle, vent.Repeat)
                       - Mathf.Max(0f, vent.StartDelay);
            float tau;
            if ((vent.context != null && !vent.context.Running) || into < 0f || (!vent.Repeat && into >= cycle)) tau = 1e4f;
            else
            {
                tau = Mathf.Repeat(into, cycle) - lead;
                if (tau < 0f) tau += cycle;
            }
            SeekHaze(tau, cycle);
            if (swirl != null)
            {
                swirl.warn = vent.WarningDuration;
                swirl.blow = vent.BlowDuration;
                swirl.cycle = cycle;
                swirl.Seek(tau);
            }
            foreach (var flow in flows)
            {
                if (flow == null) continue;
                flow.blowAt = 0f;
                flow.blow = vent.BlowDuration;
                flow.Seek(tau);
            }
        }
    }
}
