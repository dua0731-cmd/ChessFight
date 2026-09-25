using System.Collections.Generic;
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

    // The lobby's 3D backdrop: the party standing on a gold ring under a bright
    // sky, the local player in the middle, like a party game's main menu.
    //
    // Display only. Nothing here moves by input or by the network; the pawns bob
    // in place. Built from primitives and tinted copies of the team material so
    // the lobby needs no new scene or prefab assets. An artist replaces the pawn
    // body through the PawnAvatar prefab.
    public sealed class LobbyStage : MonoBehaviour
    {
        public const int Spots = 6;
        const float Spacing = 1.55f;
        const float StandHeight = 1f;       // PawnAvatar's pivot is its middle; ground + 1
        const float HeadClearance = 1.45f;  // where a nameplate sits above the pivot
        const float GhostScale = .8f;

        static readonly Color Sky = new Color(.60f, .79f, .95f);
        static readonly Color Floor = new Color(.93f, .94f, .96f);
        static readonly Color Gold = new Color(.95f, .76f, .20f);
        static readonly Color Cream = new Color(.97f, .92f, .80f);
        static readonly Color Piece = new Color(.96f, .86f, .66f);
        static readonly Color BotPiece = new Color(.90f, .92f, .96f);
        static readonly Color Ghost = new Color(.70f, .72f, .76f);
        static readonly Color Prop = new Color(.84f, .88f, .94f);

        PawnAvatar prefab;
        Material template, accent;
        Camera view;
        readonly List<Material> owned = new List<Material>();
        readonly List<GameObject> scenery = new List<GameObject>();
        readonly GameObject[] bodies = new GameObject[Spots];
        readonly LineupEntry[] shown = new LineupEntry[Spots];
        Material pieceMaterial, botMaterial, ghostMaterial;

        public Camera View => view;

        // `template` is any material on the ChessFight/NetworkColor shader; the
        // stage copies it for its own colours. `accentMaterial` tints the collar.
        public void Build(PawnAvatar pawnPrefab, Material templateMaterial, Material accentMaterial)
        {
            prefab = pawnPrefab;
            template = templateMaterial;
            accent = accentMaterial;
            pieceMaterial = Tint(Piece);
            botMaterial = Tint(BotPiece);
            ghostMaterial = Tint(Ghost);

            view = Camera.main;
            if (view == null)
            {
                var go = new GameObject("ChessFight Camera") { tag = "MainCamera" };
                view = go.AddComponent<Camera>();
            }
            view.clearFlags = CameraClearFlags.SolidColor;
            view.backgroundColor = Sky;
            view.fieldOfView = 38f;
            view.transform.position = new Vector3(0f, 2.2f, -8.2f);
            view.transform.LookAt(new Vector3(0f, .95f, 0f));

            Block("Lobby Floor", new Vector3(0, -.1f, 10), new Vector3(90, .2f, 70), Floor, false);
            Block("Lobby Ring", new Vector3(0, .005f, 0), new Vector3(6.2f, .01f, 6.2f), Gold, true);
            Block("Lobby Ring Inner", new Vector3(0, .012f, 0), new Vector3(5.7f, .01f, 5.7f), Cream, true);
            // Far-off blocks give the sky a horizon, as in the reference video.
            Block("Lobby Prop A", new Vector3(-16, 1.5f, 26), new Vector3(10, 3, 1), Prop, false);
            Block("Lobby Prop B", new Vector3(13, .9f, 30), new Vector3(8, 1.8f, 1), Prop, false);
            Block("Lobby Prop C", new Vector3(-4, 3.2f, 34), new Vector3(4, .6f, 1), Prop, false);
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

        // Where spot i stands. Me in the middle, then alternating left and right,
        // bending gently back so the outer spots do not hide behind the middle.
        public static Vector3 SpotPosition(int index)
        {
            int side = index == 0 ? 0 : (index % 2 == 1 ? -1 : 1) * ((index + 1) / 2);
            return new Vector3(side * Spacing, StandHeight, Mathf.Abs(side) * .45f);
        }

        // Nameplates and the invite "+" hang from here.
        public static Vector3 Anchor(int index) => SpotPosition(index) + Vector3.up * HeadClearance;

        GameObject Spawn(int index, LineupEntry entry)
        {
            Vector3 at = SpotPosition(index);
            // The invite silhouette is scaled down about its middle; keep it on the floor.
            if (entry.Invite) at.y = StandHeight * GhostScale;
            // PawnAvatar's accent faces +Z; the camera looks along +Z, so turn round.
            var facing = Quaternion.Euler(0f, 180f, 0f);
            if (prefab == null)
            {
                var fallback = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                StripCollider(fallback);
                fallback.transform.SetPositionAndRotation(at, facing);
                fallback.transform.SetParent(transform, true);
                fallback.GetComponent<Renderer>().sharedMaterial = entry.Invite ? ghostMaterial : pieceMaterial;
                return fallback;
            }
            var avatar = Instantiate(prefab, at, facing, transform);
            avatar.name = entry.Invite ? "Invite Spot " + index : "Lineup " + (entry.Bot ? "Bot " : "") + entry.Id;
            avatar.Bind(entry.Id, 0, entry.Invite ? ghostMaterial : entry.Bot ? botMaterial : pieceMaterial,
                        entry.Invite ? ghostMaterial : accent);
            // An empty spot is a smaller, flatter silhouette: clearly nobody yet.
            if (entry.Invite) avatar.transform.localScale = Vector3.one * GhostScale;
            foreach (var collider in avatar.GetComponentsInChildren<Collider>()) Destroy(collider);
            return avatar.gameObject;
        }

        void Update()
        {
            // A small idle bob, out of step per spot, so the lineup looks alive.
            float t = Time.unscaledTime;
            for (int i = 0; i < Spots; i++)
            {
                if (bodies[i] == null || shown[i].Invite) continue;
                Vector3 at = SpotPosition(i);
                bodies[i].transform.position = at + Vector3.up * (Mathf.Abs(Mathf.Sin(t * 2.2f + i * 1.3f)) * .06f);
            }
        }

        void Block(string name, Vector3 position, Vector3 scale, Color color, bool round)
        {
            var block = GameObject.CreatePrimitive(round ? PrimitiveType.Cylinder : PrimitiveType.Cube);
            block.name = name;
            StripCollider(block);
            block.transform.SetParent(transform, false);
            block.transform.localPosition = position;
            // A cylinder primitive is 2 units tall; halve Y so scale means size.
            block.transform.localScale = round ? new Vector3(scale.x, scale.y * .5f, scale.z) : scale;
            block.GetComponent<Renderer>().sharedMaterial = Tint(color);
            scenery.Add(block);
        }

        static void StripCollider(GameObject go)
        {
            var collider = go.GetComponent<Collider>();
            if (collider != null) Destroy(collider);
        }

        Material Tint(Color color)
        {
            Material material;
            if (template != null) material = new Material(template);
            else
            {
                var shader = Shader.Find("ChessFight/NetworkColor");
                material = new Material(shader != null ? shader : Shader.Find("Unlit/Color"));
            }
            material.color = color;
            owned.Add(material);
            return material;
        }

        void OnDestroy()
        {
            foreach (var material in owned) if (material != null) Destroy(material);
            owned.Clear();
        }
    }
}
