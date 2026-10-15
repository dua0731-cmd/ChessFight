using System.Collections.Generic;
using ChessFight.Gameplay;
using UnityEngine;

namespace ChessFight.PawnRush
{
    // J's water (mini-games v1.0 §8.2.6): a pawn that falls in floats for 1.2 s and is put back on the station's
    // south floor (u 1, v 0), not sent to the course's checkpoint. The water is a WaterZone that does not report
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
                    ArenaFx.Burst(at, new[] { Arena.Hex("#7FC8E8"), Color.white }, 24, 1.6f, 5f, .9f, .08f);
                    ArenaFx.Ring(at, 0f, 2f, .7f, Arena.Hex("#BFE6F5"));
                    ArenaFx.Sfx("sfx_splash", at);
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
                kv.Key.Teleport(game.TransformPoint(new Vector3(0f, 0f, 1f)), game.rotation);
                done.Add(kv.Key);
            }
            foreach (var d in done) wetSince.Remove(d);
        }
    }
}
