using System.Collections.Generic;
using ChessFight.Network;
using UnityEngine;
using UnityEngine.Rendering;

namespace ChessFight.Game
{
    // One spot in the lobby lineup: a party member, a party bot, or an empty
    // spot offered for inviting a friend.
    public struct LineupEntry
    {
        public ulong Id;        // 0 for an invite spot
        public string Name;
        public string Tag;      // the small line under the name: "파티장 · 2/6", "대기 중"
        public bool Leader, Me, Bot, Invite;
    }

    // The lobby's 3D backdrop: the party standing on a raised chessboard in the
    // white tile plaza, an obstacle course fading into the haze behind. The
    // camera sits right of the party so the game-mode list on the right of the
    // HUD has open sky behind it.
    //
    // Display only. Nothing here moves by input or by the network; the pieces bob
    // in place. Each member gets a different chess piece (me the knight), bots
    // are grey pawns, and an empty spot is a faceless ghost on a gold pad.
    public sealed class LobbyStage : MonoBehaviour
    {
        public const int Spots = 6;
        const float Spacing = 1.35f;        // sideways between neighbouring spots
        const float StepBack = .6f;         // each pair further out also stands further back
        const float Tile = 1.4f;
        const float PlateHeight = 2.55f;    // nameplate anchor above the feet
        const float InviteHeight = 1.25f;   // the "+" button, mid-ghost
        const float GhostScale = .85f;

        static readonly Vector3 BoardCenter = new Vector3(-.7f, 0f, .8f);
        static readonly Vector3 CameraAt = new Vector3(1.95f, 3.0f, -12.5f);
        static readonly Vector3 LookAt = new Vector3(1.95f, 1.45f, 0f);
        // Who stands where, spot by spot. Spot 0 is always the local player.
        static readonly PieceKind[] Kinds =
            { PieceKind.Knight, PieceKind.Queen, PieceKind.King, PieceKind.Rook, PieceKind.Bishop, PieceKind.Pawn };

        static float top = .45f;            // the board's top face, set by Build

        Camera view;
        readonly GameObject[] bodies = new GameObject[Spots];
        readonly LineupEntry[] shown = new LineupEntry[Spots];

        public Camera View => view;

        public void Build()
        {
            view = Camera.main;
            if (view == null)
            {
                var go = new GameObject("ChessFight Camera") { tag = "MainCamera" };
                view = go.AddComponent<Camera>();
            }
            view.fieldOfView = 32f;
            view.transform.position = CameraAt;
            view.transform.LookAt(LookAt);

            StageKit.Environment(transform, view, 28f, 120f);
            StageKit.Floor(transform);
            top = StageKit.Board(transform, "Pedestal", 7, 4, Tile, BoardCenter, .4f, .8f, PieceFigure.Hex(0x767F93));
            Course();
        }

        // Far-off pieces of the obstacle course: a run, a tiled wall, floating
        // platforms and a red spinner, all soft in the haze.
        void Course()
        {
            var course = new GameObject("Course").transform;
            course.SetParent(transform, false);
            Material pale = PieceFigure.Lit(PieceFigure.Hex(0xE8EBF0), .05f), white = PieceFigure.Lit(PieceFigure.Hex(0xEEF1F5), .05f);
            StageKit.Box(course, "Run", new Vector3(16f, .7f, 52f), new Vector3(9f, 1.4f, 46f), pale).transform.localRotation = Quaternion.Euler(0, -16f, 0);
            StageKit.Box(course, "Wall", new Vector3(-21f, 2.5f, 44f), new Vector3(1f, 5f, 30f), PieceFigure.Lit(PieceFigure.Hex(0x8FA2C9), .05f));
            StageKit.Box(course, "Platform", new Vector3(-9f, 3f, 62f), new Vector3(6f, 1.2f, 6f), white);
            StageKit.Box(course, "Platform", new Vector3(8f, 4.5f, 78f), new Vector3(7f, 1.2f, 7f), white);
            StageKit.Box(course, "Platform", new Vector3(-27f, 5f, 84f), new Vector3(8f, 1.2f, 8f), white);
            StageKit.Box(course, "Spinner Post", new Vector3(17.5f, 2f, 42f), new Vector3(.5f, 1.2f, .5f), PieceFigure.Lit(PieceFigure.Hex(0x3C4250), .2f));
            var bar = StageKit.Box(course, "Spinner", new Vector3(17.5f, 2.35f, 42f), new Vector3(8f, .38f, .38f), PieceFigure.Lit(PieceFigure.Hex(0xE53935), .3f));
            bar.transform.localRotation = Quaternion.Euler(0, 40f, 0);
        }

        // Rebuilds only the spots whose occupant changed, so a party member joining
        // does not make everyone else pop.
        public void Show(IReadOnlyList<LineupEntry> lineup)
        {
            for (int i = 0; i < Spots; i++)
            {
                bool present = lineup != null && i < lineup.Count;
                var next = present ? lineup[i] : default;
                bool same = bodies[i] != null && present && shown[i].Id == next.Id && shown[i].Invite == next.Invite && shown[i].Bot == next.Bot;
                if (same || (!present && bodies[i] == null)) continue;
                if (bodies[i] != null) { Destroy(bodies[i]); bodies[i] = null; }
                shown[i] = next;
                if (present) bodies[i] = Spawn(i, next);
            }
        }

        // Where spot i stands, feet on the board. Me in front in the middle, then
        // alternating left and right, each pair a step further back so nobody
        // hides behind the one in front.
        public static Vector3 SpotPosition(int index)
        {
            int side = index == 0 ? 0 : (index % 2 == 1 ? -1 : 1) * ((index + 1) / 2);
            return new Vector3(side * Spacing, top, Mathf.Abs(side) * StepBack);
        }

        // Nameplates hang from here.
        public static Vector3 Anchor(int index) => SpotPosition(index) + Vector3.up * PlateHeight;

        // The invite "+" sits on the ghost's middle.
        public static Vector3 InviteAnchor(int index) => SpotPosition(index) + Vector3.up * InviteHeight;

        GameObject Spawn(int index, LineupEntry entry)
        {
            Vector3 at = SpotPosition(index);
            if (entry.Invite) return InviteSpot(index, at);

            var kind = entry.Bot ? PieceKind.Pawn : Kinds[index];
            var figure = PieceFigure.Build(kind, entry.Bot ? PieceSkin.Bot : PieceSkin.White, transform);
            figure.name = "Lineup " + (entry.Bot ? "Bot " : "") + entry.Id;
            figure.transform.SetPositionAndRotation(at, Quaternion.Euler(0, Facing(index, kind), 0));
            return figure;
        }

        // Everyone faces the camera (-Z), turned a little towards the middle; the
        // knight turns further so its horse profile reads.
        static float Facing(int index, PieceKind kind)
        {
            if (kind == PieceKind.Knight && index == 0) return 120f;
            float side = Mathf.Sign(SpotPosition(index).x);
            return 180f + side * 14f;
        }

        GameObject InviteSpot(int index, Vector3 at)
        {
            var spot = new GameObject("Invite Spot " + index);
            spot.transform.SetParent(transform, false);
            spot.transform.position = at;
            Pad(spot.transform, "Pad Rim", .62f, .03f, PieceFigure.Lit(PieceFigure.Hex(0xFFD23A), .4f, .1f));
            Pad(spot.transform, "Pad", .54f, .045f, PieceFigure.Lit(PieceFigure.Hex(0xF6EFE2), .1f));
            var ghost = PieceFigure.Ghost(spot.transform);
            ghost.transform.localPosition = new Vector3(0, .04f, 0);
            ghost.transform.localRotation = Quaternion.Euler(0, 180f, 0);
            ghost.transform.localScale = Vector3.one * GhostScale;
            return spot;
        }

        static void Pad(Transform parent, string name, float radius, float height, Material material)
        {
            var disc = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            disc.name = name;
            Destroy(disc.GetComponent<Collider>());
            disc.transform.SetParent(parent, false);
            disc.transform.localPosition = new Vector3(0, height / 2, 0);
            // A cylinder primitive is 2 units tall and 1 across.
            disc.transform.localScale = new Vector3(radius * 2, height / 2, radius * 2);
            var renderer = disc.GetComponent<Renderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
        }

        void Update()
        {
            // A small idle bob, out of step per spot, so the lineup looks alive.
            float t = Time.unscaledTime;
            for (int i = 0; i < Spots; i++)
            {
                if (bodies[i] == null || shown[i].Invite) continue;
                bodies[i].transform.position = SpotPosition(i) + Vector3.up * (Mathf.Abs(Mathf.Sin(t * 2.2f + i * 1.3f)) * .06f);
            }
        }
    }
}
