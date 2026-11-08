using System.Collections.Generic;
using ChessFight.ProtectKing;
using ChessFight.VFX;
using UnityEngine;

namespace ChessFight.PawnRush
{
    // R341 (승규 11-04, "말 장애물 예고 실제 타격 vfx", the grand stair hall only): a falling piece's effects on the
    // obstacle's own clock. Every frame they are put in the state they have at the time into the piece's cycle, so every
    // PC draws the same moment without a packet, as the piece itself is a pure function of ObstacleClock. The effects come
    // from Resources/VfxSets/Hazards (menu ChessFight/VFX/Hazard/Build) and are made in Play only, so the scene keeps none.
    //   W1  FallingPiece_Warn  the floor warning, full to its rim as the piece lands, in place of the kit's flat disc
    //                          (whose renderers are turned off; the obstacle still toggles and scales it)
    //   W2  FallingPiece_Fall  the shadow straight under the piece, shrinking and darkening as it comes down, and the
    //                          speed lines above it as it falls
    //   I1  FallingPiece_Land  the flash, dust, stone chips and sparks as it lands (VfxHazardLooks without the prefab)
    //   I2  FallingPiece_Shock the shockwave at exactly the hit's reach, a band on the floor and a low wall of light, in
    //                          place of the kit's yellow ring (its renderers turned off as the disc's)
    //   I3  FallingPiece_After the cracks it leaves, hot then cooling and fading before the next warning (their pattern
    //                          picked by where the piece stands, so each piece has its own and every PC the same), the
    //                          embers out of them and a puff as the piece goes
    // Before that menu has been run (승규 11-04 "지금 유니티 화면에 안보여") each layer is made here from its shaders with
    // their own values: the same look, but nothing to tune in a prefab, and a player build keeps the shaders only once the
    // menu's materials reference them.
    [DisallowMultipleComponent]
    public sealed class FallingPieceFx : MonoBehaviour
    {
        const string SetName = "Hazards", WarnKey = "FallingPiece_Warn", FallKey = "FallingPiece_Fall", LandKey = "FallingPiece_Land",
            ShockKey = "FallingPiece_Shock", AfterKey = "FallingPiece_After";
        const string WarnShader = "ChessFight/VFX/Ground Warn Decal", ShadowShader = "ChessFight/VFX/Ground Shadow Decal",
            StreakShader = "ChessFight/VFX/Fall Streak Additive", BurstShader = "ChessFight/VFX/Burst Particles",
            ShockDecalShader = "ChessFight/VFX/Shock Decal", ShockWallShader = "ChessFight/VFX/Shock Wall Additive",
            CrackShader = "ChessFight/VFX/Crack Decal";

        FallingChessPiece piece;
        VfxGroundWarn warn;
        VfxDropShadow shadow;
        VfxFallStreaks streaks;
        VfxShockDecal shockBand;
        VfxShockWall shockWall;
        VfxCrackDecal cracks;
        readonly List<VfxParticleBurst> bursts = new List<VfxParticleBurst>();
        int made;

        void Start()
        {
            piece = GetComponent<FallingChessPiece>();
            if (piece == null)
            {
                enabled = false;
                return;
            }
            var set = VfxSet.Load(SetName);
            var w1 = Effect(set, WarnKey);
            warn = w1 != null ? w1.GetComponentInChildren<VfxGroundWarn>(true) : Made<VfxGroundWarn>("W1 Decal", WarnShader, Cube);
            var w2 = Effect(set, FallKey);
            shadow = w2 != null ? w2.GetComponentInChildren<VfxDropShadow>(true) : Made<VfxDropShadow>("W2 Shadow", ShadowShader, Cube);
            streaks = w2 != null ? w2.GetComponentInChildren<VfxFallStreaks>(true) : Made<VfxFallStreaks>("W2 Streaks", StreakShader, null);
            Bursts(Effect(set, LandKey), VfxHazardLooks.LandLayers);
            var i2 = Effect(set, ShockKey);
            shockBand = i2 != null ? i2.GetComponentInChildren<VfxShockDecal>(true) : Made<VfxShockDecal>("I2 Decal", ShockDecalShader, Cube);
            shockWall = i2 != null ? i2.GetComponentInChildren<VfxShockWall>(true) : Made<VfxShockWall>("I2 Wall", ShockWallShader, null);
            var i3 = Effect(set, AfterKey);
            cracks = i3 != null ? i3.GetComponentInChildren<VfxCrackDecal>(true) : Made<VfxCrackDecal>("I3 Cracks", CrackShader, Cube);
            // Its own pattern from where it stands, the same on every PC.
            if (cracks != null) cracks.seed = Mathf.Repeat(transform.position.x * 1.37f + transform.position.z * .71f, 97f);
            Bursts(i3, VfxHazardLooks.AfterLayers);
            if (made > 0) Debug.Log("[대계단 홀] 떨어지는 말 효과 " + made + "개를 셰이더 기본값으로 만들었습니다(프리팹은 메뉴 ChessFight/VFX/Hazard/Build)");
            if (warn != null) Hide(piece.warning);
            if (shockBand != null) Hide(piece.shockRing);
        }

        // A layer's particle bursts: its prefab's, or made here with the first values of the named layers.
        void Bursts(GameObject fx, string[] layers)
        {
            if (fx != null)
            {
                bursts.AddRange(fx.GetComponentsInChildren<VfxParticleBurst>(true));
                return;
            }
            foreach (var layer in layers)
            {
                var burst = Made<VfxParticleBurst>(layer, BurstShader, null);
                if (burst == null) continue;
                VfxHazardLooks.Apply(layer, burst);
                bursts.Add(burst);
            }
        }

        // The kit's own mark, still toggled and scaled by the obstacle, drawn no more.
        static void Hide(Transform mark)
        {
            if (mark == null) return;
            foreach (var r in mark.GetComponentsInChildren<Renderer>(true)) r.enabled = false;
        }

        static Mesh Cube => Resources.GetBuiltinResource<Mesh>("Cube.fbx");

        // The layer's prefab from the set under the piece, or null when the menu has not made it.
        GameObject Effect(VfxSet set, string key)
        {
            var prefab = set != null ? set.Find(key) : null;
            if (prefab == null) return null;
            var fx = Instantiate(prefab, transform, false);
            fx.name = prefab.name;
            // The cycle drives it, not the cue's own clock.
            if (fx.TryGetComponent<VfxCue>(out var cue)) cue.enabled = false;
            return fx;
        }

        // A part without its prefab: a mesh under the piece with a material on its shader (a null mesh is the part's own).
        T Made<T>(string name, string shaderName, Mesh mesh) where T : Component
        {
            var shader = Shader.Find(shaderName);
            if (shader == null)
            {
                Debug.LogWarning("[대계단 홀] 떨어지는 말 효과 셰이더가 없습니다(" + shaderName + "): 메뉴 ChessFight/VFX/Hazard/Build를 한 번 누르세요");
                return null;
            }
            var part = new GameObject(name + " (no prefab)");
            part.transform.SetParent(transform, false);
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
            float t = piece.CycleTime, lead = piece.warningSeconds + piece.fallSeconds;
            var drop = new VfxDrop { lead = lead, fall = piece.fallSeconds, height = piece.dropHeight, hold = piece.shockSeconds };
            if (warn != null)
            {
                warn.radius = piece.shockRadius;
                warn.lead = lead;
                warn.Seek(t);
            }
            if (shadow != null)
            {
                shadow.drop = drop;
                shadow.Seek(t);
            }
            if (streaks != null)
            {
                streaks.drop = drop;
                streaks.Seek(t);
            }
            if (shockBand != null)
            {
                shockBand.drop = drop;
                shockBand.radius = piece.shockRadius;
                shockBand.Seek(t);
            }
            if (shockWall != null)
            {
                shockWall.drop = drop;
                shockWall.radius = piece.shockRadius;
                shockWall.Seek(t);
            }
            if (cracks != null)
            {
                cracks.drop = drop;
                // Gone before the next warning, whatever the obstacle's cycle.
                cracks.life = Mathf.Min(cracks.life, piece.Period - lead - .05f);
                cracks.Seek(t);
            }
            foreach (var burst in bursts)
            {
                if (burst == null) continue;
                // The piece goes once it has lain there through the shock.
                burst.burstAt = burst.moment == VfxParticleBurst.Moment.Gone ? lead + drop.hold : lead;
                burst.Seek(t);
            }
        }
    }
}
