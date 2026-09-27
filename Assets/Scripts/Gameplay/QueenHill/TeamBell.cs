using UnityEngine;

namespace ChessFight.Gameplay
{
    // A team's pioneer bell at the top of a floor (8th revision, R50): the first
    // player OF THIS TEAM to ring it opens the team's shortcut up that floor
    // (TeamPath with the same floor and team). The other team's players can make it
    // swing but open nothing. Gold while unrung, the team's colour once open.
    //
    // Needs a collider the character's reach search can find (a trigger is fine).
    public sealed class TeamBell : MonoBehaviour, IInteractable
    {
        [SerializeField] int floor = 1;
        [SerializeField] int team;
        [Tooltip("The part that swings when rung.")]
        [SerializeField] Transform swing;

        static readonly Color Unrung = new Color(0.95f, 0.75f, 0.2f);
        static readonly Color WhiteTeam = new Color(0.96f, 0.96f, 0.94f);
        static readonly Color BlackTeam = new Color(0.12f, 0.12f, 0.14f);

        float rungAt = -100f;
        int shown = -1;
        Renderer[] renderers;
        MaterialPropertyBlock block;

        public int Floor => floor;
        public int Team => team;

        public void Configure(int floor, int team, Transform swing)
        {
            this.floor = floor;
            this.team = team;
            this.swing = swing;
        }

        public bool Interact(ICharacterDriver who, int whoTeam)
        {
            rungAt = Time.time;
            var match = QueenHillRaceMatch.Current;
            return match != null && whoTeam == team && match.Ring(floor, team, who);
        }

        void Update()
        {
            if (swing != null)
            {
                float t = Time.time - rungAt;
                float angle = t < 4f ? 35f * Mathf.Exp(-1.6f * t) * Mathf.Sin(t * 11f) : 0f;
                swing.localRotation = Quaternion.Euler(angle, 0f, 0f);
            }
            var match = QueenHillRaceMatch.Current;
            int state = match != null && match.IsOpen(floor, team) ? 1 : 0;
            if (state == shown) return;
            shown = state;
            if (renderers == null) renderers = GetComponentsInChildren<Renderer>();
            if (block == null) block = new MaterialPropertyBlock();
            block.SetColor("_Color", state == 0 ? Unrung : team == Teams.Black ? BlackTeam : WhiteTeam);
            foreach (var r in renderers) r.SetPropertyBlock(block);
        }
    }
}
