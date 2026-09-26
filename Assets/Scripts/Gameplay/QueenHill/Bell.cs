using UnityEngine;

namespace ChessFight.Gameplay
{
    // A section's pioneer bell (DESIGN §3.3 B): the first team to ring it opens the
    // section's fast path (OpenPath with the same section). Rung with the interact
    // key within reach; ringing an open bell only makes it swing. It takes the
    // colour of the team that opened it (white or black; gold while unrung).
    //
    // Needs a collider the character's reach search can find (a trigger is fine).
    public sealed class Bell : MonoBehaviour, IInteractable
    {
        [SerializeField] int section = 1;
        [Tooltip("The part that swings when rung.")]
        [SerializeField] Transform swing;

        static readonly Color Unrung = new Color(0.95f, 0.75f, 0.2f);
        static readonly Color WhiteTeam = new Color(0.96f, 0.96f, 0.94f);
        static readonly Color BlackTeam = new Color(0.12f, 0.12f, 0.14f);

        float rungAt = -100f;
        int shownTeam = -2;
        Renderer[] renderers;
        MaterialPropertyBlock block;

        public int Section => section;

        public void Configure(int section, Transform swing)
        {
            this.section = section;
            this.swing = swing;
        }

        public bool Interact(ICharacterDriver who, int team)
        {
            rungAt = Time.time;
            var match = QueenHillMatch.Current;
            return match != null && match.Ring(section, who, team);
        }

        void Update()
        {
            if (swing != null)
            {
                float t = Time.time - rungAt;
                float angle = t < 4f ? 35f * Mathf.Exp(-1.6f * t) * Mathf.Sin(t * 11f) : 0f;
                swing.localRotation = Quaternion.Euler(angle, 0f, 0f);
            }
            var match = QueenHillMatch.Current;
            int team = match != null && match.IsOpen(section) ? match.Pioneer(section) : -1;
            if (team == shownTeam) return;
            shownTeam = team;
            if (renderers == null) renderers = GetComponentsInChildren<Renderer>();
            if (block == null) block = new MaterialPropertyBlock();
            block.SetColor("_Color", team == Teams.White ? WhiteTeam : team == Teams.Black ? BlackTeam : Unrung);
            foreach (var r in renderers) r.SetPropertyBlock(block);
        }
    }
}
