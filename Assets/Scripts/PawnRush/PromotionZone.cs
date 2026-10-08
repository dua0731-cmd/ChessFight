using ChessFight.Gameplay;
using UnityEngine;

namespace ChessFight.PawnRush
{
    // A promotion zone (design doc §5): four 2 x 2 m pads, queen, rook, bishop and knight.
    // Only where the pads are: the promotion rules (stand 3 s, who gets what) belong to
    // the promotion system. Which zones are on, and which one shows the queen, depends
    // on the team size:
    //
    //   team of 2  zone 2           queen in zone 2
    //   team of 3  zones 1, 2       queen in zone 2
    //   4 to 6     zones 1, 2, 3    queen in zone 3
    public sealed class PromotionZone : MonoBehaviour
    {
        public enum Pad { Queen, Rook, Bishop, Knight }

        [SerializeField, Range(1, 3)] int zone = 1;
        [SerializeField, Range(0, 1)] int team = Teams.White;
        [Tooltip("Queen, rook, bishop, knight.")]
        [SerializeField] Transform[] pads = new Transform[4];

        public int Zone => zone;
        public int Team => team;

        public void Configure(int zone, int team, Transform[] pads)
        {
            this.zone = zone;
            this.team = team;
            this.pads = pads;
        }

        public void SetTeam(int team) => this.team = team;

        public Transform PadAt(Pad pad) => pads[(int)pad];

        public static bool IsOn(int zone, int teamSize) =>
            teamSize <= 2 ? zone == 2 : teamSize == 3 ? zone <= 2 : true;

        public static int QueenZone(int teamSize) => teamSize <= 3 ? 2 : 3;

        // Hides the pads of a zone that is off, and the queen pad of every zone but the queen's.
        public void ApplyTeamSize(int teamSize)
        {
            bool on = IsOn(zone, teamSize);
            for (int i = 0; i < pads.Length; i++)
                if (pads[i] != null) pads[i].gameObject.SetActive(on && (i != (int)Pad.Queen || zone == QueenZone(teamSize)));
        }
    }
}
