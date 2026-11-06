using System.Collections.Generic;
using UnityEngine;

namespace ChessFight.Gameplay
{
    public sealed class KingRushSeesawExit : MonoBehaviour
    {
        public int team;
        public Collider barrier;
        public Bounds crossing;
        public GameObject rescueBridge;
        public TextMesh label;
        readonly HashSet<Collider> ignored = new HashSet<Collider>();
        public void Apply(KingRushMatch match, bool access, bool bridge, int localTeam)
        {
            foreach (var p in match.Characters)
                foreach (var body in p.BodyColliders)
                {
                    if (body == null) continue;
                    bool pass = p.Team == team && access;
                    if (pass && ignored.Add(body)) Physics.IgnoreCollision(barrier, body, true);
                    else if (!pass && ignored.Remove(body)) Physics.IgnoreCollision(barrier, body, false);
                }
            barrier.GetComponent<Renderer>().enabled = !(access && localTeam == team);
            if (rescueBridge.activeSelf != bridge) rescueBridge.SetActive(bridge);
        }
        void OnDisable()
        {
            foreach (var body in ignored) if (body != null && barrier != null) Physics.IgnoreCollision(barrier, body, false);
            ignored.Clear();
        }
    }
}
