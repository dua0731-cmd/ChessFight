using System.Collections.Generic;
using ChessFight.Network;
using UnityEngine;

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

    // The lobby's 3D backdrop (design C revised, 2026-10-03): the party on a big
    // maple and walnut chessboard in a mahogany frame with brass trim, raised on a
    // dark wooden stage whose top edge glows; lamps on two pillars and warm beams.
    // (The sign wall behind and the scrolling sign on the stage's face were taken
    // out on 2026-10-03: the HUD covered them.) Numbers are the design sample's
    // (MenuArt.Web: +Z towards the camera there).
    //
    // Display only. Nothing here moves by input or by the network; the pieces bob
    // in place. Each member gets a different chess piece (me the knight, bigger,
    // in front), bots are charcoal pawns, and an empty spot is a turning brass
    // ring for the HUD's "+ 친구 초대".
    public sealed class LobbyStage : MonoBehaviour
    {
        public const int Spots = 6;
        const float StageSize = 10.8f, BaseHeight = .9f, FrameHeight = .34f, BoardSize = 9.6f;
        const float Top = BaseHeight + FrameHeight + .005f;
        const float MeScale = 1.3f, MemberScale = 1.15f;
        const float InviteHeight = 1.15f;   // the "+" ring, above the brass ring

        // Spot 0 is always the local player, front and centre; then the front row
        // either side, then the back row in the gaps. Sample coordinates (x, z).
        static readonly Vector2[] Places =
            { new Vector2(0, .5f), new Vector2(-2.4f, .5f), new Vector2(2.4f, .5f), new Vector2(-1.2f, -1.5f), new Vector2(1.2f, -1.5f), new Vector2(-3.6f, -1.5f) };
        // Who stands where, spot by spot.
        static readonly PieceKind[] Kinds =
            { PieceKind.Knight, PieceKind.Queen, PieceKind.King, PieceKind.Rook, PieceKind.Bishop, PieceKind.Pawn };

        Camera view;
        readonly GameObject[] bodies = new GameObject[Spots];
        readonly LastSceneFigure[] figures = new LastSceneFigure[Spots];
        readonly LineupEntry[] shown = new LineupEntry[Spots];
        readonly List<Transform> beams = new List<Transform>();
        readonly List<Material> beamMaterials = new List<Material>();

        public Camera View => view;

        public void Build()
        {
            view = Camera.main;
            if (view == null)
            {
                var go = new GameObject("ChessFight Camera") { tag = "MainCamera" };
                view = go.AddComponent<Camera>();
            }
            view.fieldOfView = 30f;
            var root = transform;
            MenuArt.Hall(root, view, 22f, 72f);
            Aim(0f);
            Stage(root);
            Lamps(root);
        }

        void Stage(Transform root)
        {
            MenuArt.Slab(root, "Stage", new Vector3(0, BaseHeight / 2, 0), new Vector3(StageSize, BaseHeight, StageSize),
                         MenuArt.Wood(MenuArt.DarkWood, .55f, new Vector2(3f, 1f)));
            var led = MenuArt.Lamp(MenuArt.Led);
            for (int s = -1; s <= 1; s += 2)
            {
                MenuArt.Slab(root, "Stage Light", new Vector3(0, BaseHeight - .02f, s * StageSize / 2), new Vector3(StageSize + .06f, .05f, .05f), led);
                MenuArt.Slab(root, "Stage Light", new Vector3(s * StageSize / 2, BaseHeight - .02f, 0), new Vector3(.05f, .05f, StageSize + .06f), led);
            }
            for (int i = -4; i <= 4; i++)
                MenuArt.Halo(root, MenuArt.Web(i * 1.3f, BaseHeight, StageSize / 2 + .02f), MenuArt.Warm, .6f, .28f, view);

            MenuArt.Slab(root, "Frame", new Vector3(0, BaseHeight + FrameHeight / 2, 0), new Vector3(10.4f, FrameHeight, 10.4f),
                         MenuArt.Wood(MenuArt.Mahogany, .68f, new Vector2(2f, 1f)));
            MenuArt.Slab(root, "Board", new Vector3(0, Top - .01f, 0), new Vector3(BoardSize, .02f, BoardSize), MenuArt.Board(8));
            var brass = MenuArt.BrassMetal();
            for (int s = -1; s <= 1; s += 2)
            {
                MenuArt.Slab(root, "Inlay", new Vector3(0, Top + .006f, s * (BoardSize / 2 + .06f)), new Vector3(BoardSize + .18f, .025f, .06f), brass);
                MenuArt.Slab(root, "Inlay", new Vector3(s * (BoardSize / 2 + .06f), Top + .006f, 0), new Vector3(.06f, .025f, BoardSize + .18f), brass);
            }
            foreach (var (a, b) in new[] { (-1, -1), (1, -1), (-1, 1), (1, 1) })
                MenuArt.Slab(root, "Corner", new Vector3(a * 5f, Top + .02f, b * 5f), new Vector3(.42f, .07f, .42f), brass);
        }

        // Pillars with three lamps each, beams onto the stage, the key light.
        void Lamps(Transform root)
        {
            for (int s = -1; s <= 1; s += 2)
            {
                float x = s * 12.5f;
                MenuArt.Slab(root, "Pillar", MenuArt.Web(x, 6.5f, -6f), new Vector3(.5f, 13f, .5f), PieceFigure.Lit(MenuArt.Hex(0x1A120B), .6f, .6f));
                for (int k = 0; k < 3; k++)
                {
                    float y = 12.6f - k * 1.1f;
                    MenuArt.Slab(root, "Lamp", MenuArt.Web(x - s * .5f, y, -5.6f), new Vector3(1.2f, .6f, .7f), PieceFigure.Lit(MenuArt.Hex(0x2A1C12), .65f, .6f));
                    MenuArt.Halo(root, MenuArt.Web(x - s * .9f, y, -5.2f), 0xFFC27A, 2.2f, .9f, view);
                    var beam = MenuArt.Beam(root, MenuArt.Web(x - s * .9f, y, -5.2f), MenuArt.Web(s * (1.2f + k * .8f), Top, k * .6f), MenuArt.Warm, 1.6f, .05f);
                    beams.Add(beam);
                    beamMaterials.Add(beam.GetComponent<Renderer>().sharedMaterial);
                }
            }
            MenuArt.Spot(root, MenuArt.Web(0, 15, 5), MenuArt.Web(0, Top, .5f), 0xFFE6C2, 1.8f, .3f, .55f, true);
            MenuArt.Beam(root, MenuArt.Web(0, 15, 5), MenuArt.Web(0, Top, .5f), 0xFFE8C8, 2.2f, .07f);
            MenuArt.Halo(root, MenuArt.Web(0, 15, 5), 0xFFF0D8, 3f, .9f, view);
            MenuArt.Spot(root, MenuArt.Web(-12.5f, 12.6f, -5.6f), MenuArt.Web(-1, Top, 0), MenuArt.Warm, 1.3f, .42f, .6f);
            MenuArt.Spot(root, MenuArt.Web(12.5f, 12.6f, -5.6f), MenuArt.Web(1, Top, 0), MenuArt.Warm, 1.3f, .42f, .6f);
            MenuArt.Spot(root, MenuArt.Web(0, 8, -10), MenuArt.Web(0, 2.2f, 0), 0xFFE2B8, 1.5f, .4f, .6f);
        }

        void Aim(float t)
        {
            if (view == null) return;
            view.transform.position = MenuArt.Web(.6f + Mathf.Sin(t * .1f) * .3f, 7f, 18.6f);
            view.transform.LookAt(MenuArt.Web(.6f, 1.75f, 0));
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
                if (bodies[i] != null) { Destroy(bodies[i]); bodies[i] = null; figures[i] = null; }
                shown[i] = next;
                if (present) Spawn(i, next);
            }
        }

        // Where spot i stands, feet on the board.
        public static Vector3 SpotPosition(int index)
        {
            var p = Places[Mathf.Clamp(index, 0, Spots - 1)];
            return MenuArt.Web(p.x, Top, p.y);
        }

        // Nameplates hang from here, just over the head.
        public static Vector3 Anchor(int index) => SpotPosition(index) + Vector3.up * (index == 0 ? 3.05f : 2.7f);

        // The invite "+" floats over the brass ring.
        public static Vector3 InviteAnchor(int index) => SpotPosition(index) + Vector3.up * InviteHeight;

        void Spawn(int index, LineupEntry entry)
        {
            Vector3 at = SpotPosition(index);
            if (entry.Invite) { bodies[index] = InviteSpot(index, at); return; }

            var kind = entry.Bot ? PieceKind.Pawn : Kinds[index];
            // Me turned out to the right so the knight's head reads; the others face
            // the camera, a little towards the middle.
            var lookAt = index == 0 ? MenuArt.Web(7.5f, Top, 8.5f) : MenuArt.Web(Places[index].x * .5f, Top, 9f);
            var figure = MenuArt.Figure(transform, kind, entry.Bot ? 1 : 0, at, lookAt, index == 0 ? MeScale : MemberScale);
            figure.Root.name = "Lineup " + (entry.Bot ? "Bot " : "") + entry.Id;
            figures[index] = figure;
            bodies[index] = figure.Root;
        }

        GameObject InviteSpot(int index, Vector3 at)
        {
            var spot = new GameObject("Invite Spot " + index);
            spot.transform.SetParent(transform, false);
            spot.transform.localPosition = at + Vector3.up * .01f;
            LastSceneArt.Part(spot.transform, "Ring", MenuArt.Torus(.72f, .045f, 64), MenuArt.Lamp(0xFFD08A), new Vector3(0, .04f, 0), null, null, false);
            MenuArt.FloorGlow(spot.transform, new Vector3(0, .03f, 0), MenuArt.Warm, .7f, .42f);
            return spot;
        }

        void Update()
        {
            float t = Time.unscaledTime;
            Aim(t);
            for (int i = 0; i < Spots; i++)
            {
                if (bodies[i] == null) continue;
                if (shown[i].Invite)
                {
                    bodies[i].transform.localRotation = Quaternion.Euler(0, t * .8f * Mathf.Rad2Deg, 0);
                    bodies[i].transform.localScale = Vector3.one * (1 + .05f * Mathf.Sin(t * 3 + i));
                }
                else if (figures[i] != null) MenuArt.Idle(figures[i], t, i, .6f);
            }
            for (int i = 0; i < beamMaterials.Count; i++)
            {
                var c = beamMaterials[i].color;
                c.a = .045f + .02f * Mathf.Sin(t * 1.7f + i);
                beamMaterials[i].color = c;
            }
        }
    }
}
