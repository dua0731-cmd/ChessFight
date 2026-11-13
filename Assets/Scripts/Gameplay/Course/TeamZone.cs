using System;
using UnityEngine;

namespace ChessFight.Gameplay
{
    // A whole team section's volume (Pawn Rush course 01 v0.2): a character of the OTHER team
    // that gets inside - thrown there by a knockback, not walking: the team barriers stop that -
    // is reported at once, and whoever simulates it puts it back on its last checkpoint
    // (PlaytestSpawner). Its own team, and characters with no team, are left alone.
    //
    // The box is read as axis-aligned: rotate the zone's parent, not the zone.
    [RequireComponent(typeof(BoxCollider))]
    public sealed class TeamZone : MonoBehaviour
    {
        [Tooltip("0 = white, 1 = black: whose section this is.")]
        [SerializeField, Range(0, 1)] int team = Teams.White;

        public int Team => team;
        public void SetTeam(int team) => this.team = team;
        public Bounds Bounds => GetComponent<BoxCollider>().bounds;

        public static event Action<ICharacterDriver, TeamZone> Intruded;

        void Awake() => GetComponent<BoxCollider>().isTrigger = true;

        // Every limb reports; listeners ignore repeats while a respawn is pending.
        void OnTriggerEnter(Collider other)
        {
            var driver = other.GetComponentInParent<ICharacterDriver>();
            if (driver is ITeamMember member && member.Team != Teams.None && member.Team != team)
                Intruded?.Invoke(driver, this);
        }

        void OnDrawGizmos()
        {
            var box = GetComponent<BoxCollider>();
            if (box == null) return;
            Gizmos.color = team == Teams.White ? new Color(1f, 1f, 1f, .08f) : new Color(0f, 0f, 0f, .12f);
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.DrawCube(box.center, box.size);
        }
    }
}
