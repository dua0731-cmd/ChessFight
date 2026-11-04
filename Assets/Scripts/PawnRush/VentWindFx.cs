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
    [DisallowMultipleComponent]
    public sealed class VentWindFx : MonoBehaviour
    {
        const string SetName = "Hazards", WindKey = "Vent_Wind";
        const string FlowShader = "ChessFight/VFX/Wind Flow", SwirlShader = "ChessFight/VFX/Wind Swirl";

        AirVent vent;
        VfxWindSwirl swirl;
        readonly List<VfxWindFlow> flows = new List<VfxWindFlow>();
        int made;

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
