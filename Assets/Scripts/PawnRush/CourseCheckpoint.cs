using ChessFight.Gameplay;
using UnityEngine;

namespace ChessFight.PawnRush
{
    // A course checkpoint: its number, shared or one team's, and six respawn spots side by
    // side across the way (2 m apart, closer on a narrow path). Passing it and the respawn itself are
    // the existing Checkpoint's (order = number; PlaytestSpawner keeps the highest
    // reached and puts the character back on it). This adds what a match will need
    // on top: whose it is and where up to six players come back side by side.
    [RequireComponent(typeof(Checkpoint))]
    public sealed class CourseCheckpoint : MonoBehaviour
    {
        [SerializeField] int number;
        [Tooltip("-1 = shared, 0 = white, 1 = black.")]
        [SerializeField, Range(-1, 1)] int team = Teams.None;
        [Tooltip("Respawn spots, local to this checkpoint, on its floor.")]
        [SerializeField] Vector3[] spots = Spots();

        public int Number => number;
        public int Team => team;
        public bool Shared => team == Teams.None;
        public int SpotCount => spots.Length;

        public void Configure(int number, int team, float spacing = 2f)
        {
            this.number = number;
            this.team = team;
            spots = Spots(spacing);
        }

        public void SetTeam(int team) => this.team = team;

        // Ground points of the spots: the bottom of the trigger box, centred across it.
        static Vector3[] Spots(float spacing = 2f)
        {
            var s = new Vector3[6];
            for (int i = 0; i < 6; i++) s[i] = new Vector3((i - 2.5f) * spacing, -1.5f, 0f);
            return s;
        }

        public Vector3 SpotWorld(int i) => transform.TransformPoint(spots[i]);

        void OnDrawGizmos()
        {
            Gizmos.color = Shared ? new Color(.45f, .9f, .55f) : team == Teams.White ? Color.white : new Color(.15f, .15f, .2f);
            for (int i = 0; i < spots.Length; i++) Gizmos.DrawWireSphere(SpotWorld(i) + Vector3.up * .5f, .4f);
        }
    }
}
