using ChessFight.Network;
using UnityEngine;

namespace ChessFight.Game
{
    // R736 (the lobby's 기물 도감 tab, mock-up A): the chosen piece big on the lobby's board, between the HUD's list on
    // the left and its panel on the right, turning slowly; a drag across the stage turns it (NetworkHudView.CodexSpin).
    // The party steps off the board while 기물 도감 or 전적 is up.
    public sealed partial class LobbyStage
    {
        // About 1.9 m tall for a pawn, a head over the lineup's own player (1.3).
        const float ShowcaseScale = 1.9f;
        // Where the piece's feet go on screen (from the bottom left): the gap between the HUD's list and panel.
        static readonly Vector2 ShowcaseViewport = new Vector2(.415f, .27f);
        // Turning by itself (radians a second), and how long a drag holds it still after.
        const float ShowcaseTurn = .35f, ShowcaseHold = 2.5f;

        LastSceneFigure showcase;
        PieceKind showcaseKind;
        int showcaseTeam = -1;
        bool partyAway;
        float showcaseYaw, showcaseHeldUntil;

        // Away: the party off the board (any tab but 플레이). Piece: what stands there instead (null: nothing).
        public void Showcase(bool away, PieceKind? piece, int team)
        {
            if (partyAway != away)
            {
                partyAway = away;
                for (int i = 0; i < Spots; i++) if (bodies[i] != null) bodies[i].SetActive(!away);
            }
            if (piece == null)
            {
                if (showcase != null) Destroy(showcase.Root);
                showcase = null;
                showcaseTeam = -1;
                return;
            }
            if (showcase != null && showcaseKind == piece.Value && showcaseTeam == team) return;
            if (showcase != null) Destroy(showcase.Root);
            showcaseKind = piece.Value;
            showcaseTeam = team;
            var at = ShowcaseSpot();
            // Facing the camera, as the lineup does.
            var toward = view != null ? transform.InverseTransformPoint(view.transform.position) : at + Vector3.forward;
            toward.y = at.y;
            showcase = MenuArt.Figure(transform, showcaseKind, team == 1 ? 1 : 0, at, toward, ShowcaseScale * stageScale);
            showcase.Root.name = "Codex " + showcaseKind;
        }

        public void SpinShowcase(float degrees)
        {
            showcaseYaw += degrees * Mathf.Deg2Rad;
            showcaseHeldUntil = Time.unscaledTime + ShowcaseHold;
        }

        // On the board where the camera sees the gap between the HUD's list and panel (kept on the board).
        Vector3 ShowcaseSpot()
        {
            var fallback = MenuArt.Web(-1f * stageScale, Top, .5f * stageScale);
            if (view == null) return fallback;
            var plane = new Plane(transform.up, transform.TransformPoint(new Vector3(0f, Top, 0f)));
            var ray = view.ViewportPointToRay(new Vector3(ShowcaseViewport.x, ShowcaseViewport.y, 0f));
            if (!plane.Raycast(ray, out float distance)) return fallback;
            var local = transform.InverseTransformPoint(ray.GetPoint(distance));
            float edge = BoardSize * stageScale * .5f - .6f * stageScale;
            local.x = Mathf.Clamp(local.x, -edge, edge);
            local.z = Mathf.Clamp(local.z, -edge, edge);
            local.y = Top;
            return local;
        }

        void TickShowcase(float t, float dt)
        {
            if (showcase == null) return;
            if (Time.unscaledTime >= showcaseHeldUntil) showcaseYaw += ShowcaseTurn * dt;
            float b = Mathf.Max(0f, Mathf.Sin(t * 2.4f));
            showcase.Pose(new FigurePose { Yaw = showcaseYaw, Jump = b * .05f, Squash = -.03f * b, Arm = .25f + .2f * Mathf.Sin(t * 2.4f) });
        }
    }
}
