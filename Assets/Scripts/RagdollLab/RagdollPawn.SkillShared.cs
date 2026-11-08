using UnityEngine;

namespace ChessFight.RagdollLab
{
    // Shared skill targets and test-bot controls used by the active modes.
    public partial class RagdollPawn
    {
        public bool SkillBot { get; set; }
        public Vector3 SkillDirection => skillDir;

        public Vector3 FeetPoint
        {
            get
            {
                Vector3 h = bodies[0].position;
                return new Vector3(h.x, groundFound ? groundY : h.y - standHeight, h.z);
            }
        }
    }
}
