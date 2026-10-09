using System.Collections.Generic;
using ChessFight.Gameplay;
using ChessFight.Network;
using UnityEngine;

namespace ChessFight.PawnRush
{
    // One team's 20 x 20 m mini-game station in a mission plaza (design doc v0.4 §5; replaces the
    // team-only slot island). Station-local frame: origin at the middle of its south edge on the
    // floor, u = local z (north, toward the team gate, 0..20), v = local x (-10..10).
    //
    // There is no wall between the two stations: the other team walks in and gets in the way, but
    // only this team's pawns move the progress (at most four at once). The game picked for the
    // round is built into GameSocket (MissionGames); its done signal opens this station's gate.
    public sealed class MissionStation : MonoBehaviour
    {
        public const float Size = 20f, Half = 10f;

        // C's rope and D's capstan worked backwards by the other team (v0.4 §5). On by default; the
        // design doc wants a switch in test builds (the playtest's F7).
        public static bool ReverseSabotage = true;

        [SerializeField, Range(0, 1)] int team = Teams.White;
        [SerializeField, Range(1, 2)] int plaza = 1;
        [Tooltip("Black's station is the mirror of white's: D's capstan turns the other way.")]
        [SerializeField] bool mirrored;
        [SerializeField] TeamGate gate;
        [SerializeField] Transform gameSocket;
        [SerializeField] Course01Kit kit;
        [SerializeField] int gameIndex = PawnRushMissions.None;

        public int Team => team;
        public int Plaza => plaza;
        public bool Mirrored => mirrored;
        public TeamGate Gate => gate;
        public Course01Kit Kit => kit;
        public int GameIndex => gameIndex;
        public MiniGameBase Game => gameSocket != null ? gameSocket.GetComponentInChildren<MiniGameBase>(true) : null;
        public Transform Socket => gameSocket;

        public void Configure(int team, int plaza, bool mirrored, TeamGate gate, Transform gameSocket, Course01Kit kit)
        {
            this.team = team;
            this.plaza = plaza;
            this.mirrored = mirrored;
            this.gate = gate;
            this.gameSocket = gameSocket;
            this.kit = kit;
        }

        // Builds the given game in place of the current one (a new round, or the playtest's keys).
        public MiniGameBase Install(int game)
        {
            for (int i = gameSocket.childCount - 1; i >= 0; i--) DestroyImmediate(gameSocket.GetChild(i).gameObject);
            gameIndex = game;
            if (gate != null && Application.isPlaying) gate.Close();
            var built = MissionGames.Build(game, this);
            if (Application.isPlaying) Hook(built);
            last.Clear();
            return built;
        }

        MiniGameBase hooked;

        // The game built in the Editor (saved with the scene) opens the gate too.
        void Start()
        {
            if (hooked == null) Hook(Game);
        }

        void Hook(MiniGameBase game)
        {
            if (hooked != null) hooked.Finished -= OpenGate;
            hooked = game;
            if (game != null) game.Finished += OpenGate;
        }

        void OpenGate()
        {
            if (gate != null) gate.Open();
        }

        public void ResetRound()
        {
            var game = Game;
            if (game != null) game.ResetGame();
            if (gate != null) gate.Close();
        }

        // ------------------------------------------------------------------ the pawns in the station

        public struct Pawn
        {
            public ICharacterDriver Driver;
            public int Team;
            public Vector3 Local;      // hips, station-local
            public Vector3 Velocity;   // station-local, m/s
            public bool Own;           // of this station's team
        }

        readonly List<Pawn> pawns = new List<Pawn>();
        readonly Dictionary<ICharacterDriver, Vector3> last = new Dictionary<ICharacterDriver, Vector3>();
        readonly Dictionary<ICharacterDriver, Vector3> next = new Dictionary<ICharacterDriver, Vector3>();
        readonly Collider[] buffer = new Collider[256];
        float gatheredAt = -1f;

        // Every character whose hips are over the station (up to 12 m up: E's towers), with its
        // velocity since the last call. Call it from FixedUpdate: once per physics step.
        public List<Pawn> Pawns()
        {
            if (gatheredAt == Time.fixedTime) return pawns;
            gatheredAt = Time.fixedTime;
            pawns.Clear();
            next.Clear();
            var center = transform.TransformPoint(new Vector3(0f, 6f, Half));
            int n = Physics.OverlapBoxNonAlloc(center, new Vector3(Half + .5f, 9f, Half + .5f), buffer, transform.rotation, ~0, QueryTriggerInteraction.Ignore);
            float dt = Mathf.Max(Time.fixedDeltaTime, 1e-4f);
            for (int i = 0; i < n; i++)
            {
                var driver = buffer[i].GetComponentInParent<ICharacterDriver>();
                if (driver == null || next.ContainsKey(driver) || driver.FollowTarget == null) continue;
                var local = transform.InverseTransformPoint(driver.FollowTarget.position);
                if (Mathf.Abs(local.x) > Half + .3f || local.z < -.3f || local.z > Size + .3f) { next[driver] = local; continue; }
                var velocity = last.TryGetValue(driver, out var before) ? (local - before) / dt : Vector3.zero;
                // A respawn or a throw across the map is not a walk.
                if (velocity.sqrMagnitude > 400f) velocity = Vector3.zero;
                int t = driver is ITeamMember m ? m.Team : Teams.None;
                pawns.Add(new Pawn { Driver = driver, Team = t, Local = local, Velocity = velocity, Own = t == team });
                next[driver] = local;
            }
            last.Clear();
            foreach (var kv in next) last[kv.Key] = kv.Value;
            return pawns;
        }

        void OnDrawGizmos()
        {
            Gizmos.color = team == Teams.White ? new Color(1f, 1f, 1f, .5f) : new Color(.1f, .1f, .12f, .6f);
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.DrawWireCube(new Vector3(0f, .05f, Half), new Vector3(Size, .1f, Size));
        }
    }
}
