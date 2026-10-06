using ChessFight.Gameplay;
using UnityEngine;

namespace ChessFight.PawnRush
{
    // A course checkpoint (design doc §5): its number, shared or one team's, and six
    // respawn spots 2 m apart across the course. Passing it and the respawn itself are
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

        public void Configure(int number, int team)
        {
            this.number = number;
            this.team = team;
            spots = Spots();
        }

        public void SetTeam(int team) => this.team = team;

        // Ground points of the spots: the bottom of the trigger box, x -5..5.
        static Vector3[] Spots()
        {
            var s = new Vector3[6];
            for (int i = 0; i < 6; i++) s[i] = new Vector3(-5f + 2f * i, -1.5f, 0f);
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
