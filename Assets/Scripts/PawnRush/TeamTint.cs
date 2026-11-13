using ChessFight.Gameplay;
using UnityEngine;

namespace ChessFight.PawnRush
{
    // A renderer in its team's colour (alcove arches, team markers). TeamMirror flips it
    // when it builds the black half from the white one.
    [RequireComponent(typeof(Renderer))]
    public sealed class TeamTint : MonoBehaviour
    {
        [SerializeField] Material white;
        [SerializeField] Material black;
        [SerializeField, Range(0, 1)] int team;

        public void Configure(Material white, Material black, int team)
        {
            this.white = white;
            this.black = black;
            Apply(team);
        }

        public void Apply(int team)
        {
            this.team = team;
            var m = team == Teams.Black ? black : white;
            if (m != null) GetComponent<Renderer>().sharedMaterial = m;
        }
    }
}
