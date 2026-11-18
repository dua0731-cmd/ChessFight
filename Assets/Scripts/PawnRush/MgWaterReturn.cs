using System.Collections.Generic;
using ChessFight.Gameplay;
using UnityEngine;

namespace ChessFight.PawnRush
{
    // J's water (mini-games v1.0 §8.2.6): a pawn that falls in floats for 1.2 s and is put back on the station's
    // south floor (u 1, v 0) - or, R686, where the game says (ReturnPoint: the last tub it stood on) - not sent to the
    // course's checkpoint. The water is a WaterZone that does not report
    // (the ragdoll floats on it; the course never hears of it), and this puts pawns back - on the host, or
    // offline. Every PC plays the splash when it sees hips go under the surface.
    public sealed class MgWaterReturn : MonoBehaviour
    {
        public const float Delay = 1.2f;

        [SerializeField] MissionStation station;
        [SerializeField] Transform game;
        [SerializeField] float surface = -.25f, u0 = 2.6f, u1 = 19.4f;

        readonly Dictionary<ICharacterDriver, float> wetSince = new Dictionary<ICharacterDriver, float>();
        readonly HashSet<ICharacterDriver> wet = new HashSet<ICharacterDriver>(), now = new HashSet<ICharacterDriver>();
        readonly List<ICharacterDriver> done = new List<ICharacterDriver>();

        /// <summary>R686: where a pawn starts again, in the game's frame (the ground point its feet go on); none: the
        /// south floor. Asked on the host (or offline) as it puts the pawn back.</summary>
        public System.Func<ICharacterDriver, Vector3> ReturnPoint;

        public static MgWaterReturn Make(Transform game, MissionStation station, float surface, float bottom, float u0, float u1)
        {
            var go = new GameObject("Water (MgWaterReturn)");
            go.transform.SetParent(game, false);
            var box = go.AddComponent<BoxCollider>();
            box.isTrigger = true;
            box.center = new Vector3(0f, (surface + bottom) * .5f, (u0 + u1) * .5f);
            box.size = new Vector3(MissionStation.Size, surface - bottom, u1 - u0);
            var zone = go.AddComponent<WaterZone>();
            zone.Report = false;
            var water = go.AddComponent<MgWaterReturn>();
            water.station = station;
            water.game = game;
            water.surface = surface;
            water.u0 = u0;
            water.u1 = u1;
            return water;
        }

        bool Under(Vector3 local) => local.y < surface + .1f && local.z > u0 && local.z < u1 && Mathf.Abs(local.x) < MissionStation.Half;

        void FixedUpdate()
        {
            if (station == null || game == null) return;
            now.Clear();
            foreach (var p in station.Pawns())
            {
                if (!Under(p.Local)) continue;
                now.Add(p.Driver);
                if (!wet.Contains(p.Driver))
                {
                    var at = game.TransformPoint(new Vector3(p.Local.x, surface, p.Local.z));
                    // J's crown, ripples and foam (MiniGameLaundryFx; R688 (seoungtest), "이런 vfx 말고": always J's own,
                    // never the shared burst and ring)
                    MiniGameLaundryFx.For(station)?.Splash(at, 1f);
                    // R688 ("물에 빠지는 소리 넣어줘"): a splash (was the missing sfx_splash, silent) and foam bubbling after it
                    MgKnightPads.SplashSound(at, 1f);
                    MgKnightPads.BubbleSound(at, .55f);
                }
            }
            wet.Clear();
            foreach (var d in now) wet.Add(d);
            if (MiniGameBase.Remote) return;
            // The host's (or offline) part: 1.2 s afloat, then back to the south floor facing the islands.
            done.Clear();
            foreach (var d in now)
                if (!wetSince.ContainsKey(d)) wetSince[d] = Time.time;
            foreach (var kv in wetSince)
            {
                if (!now.Contains(kv.Key)) { done.Add(kv.Key); continue; }
                if (Time.time - kv.Value < Delay) continue;
                kv.Key.Teleport(game.TransformPoint(ReturnPoint != null ? ReturnPoint(kv.Key) : new Vector3(0f, 0f, 1f)), game.rotation);
                done.Add(kv.Key);
            }
            foreach (var d in done) wetSince.Remove(d);
        }
    }
}
