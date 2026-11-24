using System.Collections.Generic;
using ChessFight.Game;
using ChessFight.Gameplay;
using ChessFight.Network;
using ChessFight.PawnRush;
using UnityEngine;

namespace ChessFight.RagdollLab.Net
{
    // R786 (user 11-23, "끼임 판정 + R 길게"; Docs/Architecture/UI.md §12 R786): a stuck pawn goes back to a checkpoint
    // behind it. The rule is Core StuckWatch's; this is where the match runs it.
    //
    //  * The host watches every player's pawn (its keys as the host heard them, its hips in the host's physics). This PC
    //    also watches its own pawn for the prompt: on a client the puppet the host's snapshots draw, from its own keys.
    //  * Stuck, ready (StuckWatch.Cooldown since the last time) and not waiting: "끼었나요?" and the key (GameKey.Unstuck,
    //    R) held StuckWatch.HoldSeconds asks - the host itself at once, a client with a reliable TypeUnstuck. The host
    //    takes it only if its own count is within StuckWatch.Slack of stuck, then puts the pawn back BackDelay later on
    //    the highest checkpoint it has reached that is not ahead of it (StuckWatch.Back, measured by
    //    PawnRushRound.TryProgress), else on its start spot. A fall's respawn waiting meanwhile wins.
    //  * The screen dims while it waits. No answer comes back: the client sees itself moved, or after a while says it
    //    could not (a host that judged it not stuck).
    public sealed partial class SteamPawnRushLink
    {
        // A pawn this far from where it asked has been put back.
        const float MovedBack = 2f;
        // How long a client waits for that past BackDelay before saying it could not.
        const float UnstuckAnswer = 2.5f;
        const float RefusedShown = 2.5f;

        readonly StuckWatch ownStuck = new StuckWatch();
        readonly UnstuckHold ownHold = new UnstuckHold();
        float ownUnstuckUsed = -1f, unstuckAskedAt = -1f, unstuckRefusedAt = -1f, unstuckArrivedAt = -1f;
        Vector3 unstuckFrom;
        byte unstuckSent;
        bool ownStuckNow;
        GUIStyle unstuckText;

        void StepUnstuck(PawnInput local)
        {
            float dt = Time.deltaTime;
            bool live = Started() && !ended && !Spectating;
            if (authority)
                foreach (var r in runners.Values)
                {
                    if (r.Pawn == null || r.Bot) continue;
                    bool pushing = r.Id == session.Self ? Flat(local.move) : r.Input.move.sqrMagnitude > .04f;
                    var hips = r.Pawn.Hips.position;
                    bool barred = !live || r.Pawn.BeingHeld || r.Pawn.OuterFlying || r.Pawn.CheatFlying || r.RespawnTime >= 0f
                                  || r.UnstuckTime >= 0f || !r.Pawn.IsFinite();
                    // A mini-game's room: asked only once a count has begun (TryProgress walks the sections).
                    if (!barred && r.Stuck.Pushed >= 1f && InMiniGame(hips, r.Team)) barred = true;
                    r.Stuck.Step(dt, hips.x, hips.y, hips.z, pushing, barred);
                    if (r.UnstuckTime >= 0f && Time.time >= r.UnstuckTime) PutBack(r);
                }
            OwnUnstuck(dt, local, live);
        }

        static bool Flat(Vector3 move) => move.x * move.x + move.z * move.z > .04f;

        bool InMiniGame(Vector3 at, int team)
        {
            var round = PawnRushRound.Current;
            return round != null && round.TryProgress(at, team, out _, out _, out bool mini) && mini;
        }

        // This PC's own pawn: the prompt and the held key.
        void OwnUnstuck(float dt, PawnInput local, bool live)
        {
            ownStuckNow = false;
            if (!runners.TryGetValue(session.Self, out var self) || self.Pawn == null || !live)
            {
                ownStuck.Reset();
                ownHold.Reset();
                return;
            }
            var hips = self.Pawn.Hips.position;
            if (unstuckAskedAt >= 0f)
            {
                // Waiting: put back (moved away from where it asked), or no answer in time.
                if ((hips - unstuckFrom).sqrMagnitude > MovedBack * MovedBack) { unstuckAskedAt = -1f; unstuckArrivedAt = Time.time; }
                else if (Time.time > unstuckAskedAt + StuckWatch.BackDelay + UnstuckAnswer)
                {
                    unstuckAskedAt = -1f;
                    ownUnstuckUsed = -1f;      // the host did not take it: nothing spent
                    unstuckRefusedAt = Time.time;
                }
            }
            bool stuck;
            if (authority) stuck = self.Stuck.Stuck;
            else
            {
                bool barred = self.Pawn.BeingHeld || unstuckAskedAt >= 0f;
                if (!barred && ownStuck.Pushed >= 1f && InMiniGame(hips, self.Team)) barred = true;
                ownStuck.Step(dt, hips.x, hips.y, hips.z, Flat(local.move), barred);
                stuck = ownStuck.Stuck;
            }
            ownStuckNow = stuck && unstuckAskedAt < 0f;
            bool key = !runtime.InputBlocked && Application.isFocused && !ChatBox.KeysHeld && !GameMenu.IsOpen
                       && GameSettings.Held(GameKey.Unstuck);
            bool ready = StuckWatch.Ready(Time.time, ownUnstuckUsed);
            if (ownHold.Step(dt, ownStuckNow && ready, key)) AskUnstuck(self, hips);
        }

        void AskUnstuck(Runner self, Vector3 hips)
        {
            ownHold.Reset();
            ownUnstuckUsed = Time.time;
            unstuckFrom = hips;
            unstuckRefusedAt = -1f;
            if (authority)
            {
                if (TryUnstuck(self)) unstuckAskedAt = Time.time;
                else { ownUnstuckUsed = -1f; unstuckRefusedAt = Time.time; }
                return;
            }
            unstuckAskedAt = Time.time;
            ownStuck.Reset();
            unstuckSent = (byte)(unstuckSent == 255 ? 1 : unstuckSent + 1);
            bool sent = Send(session.Host, RagdollNetProtocol.Unstuck(session.Match, unstuckSent), true);
            Debug.Log("[PawnRush] 끼임: 방장에게 되돌아가기를 요청" + (sent ? "" : " (보내지 못함)"));
        }

        void ReceiveUnstuck(Runner r, byte[] bytes)
        {
            if (!RagdollNetProtocol.ReadUnstuck(bytes, session.Match, out byte seq) || seq == r.UnstuckSeq) return;
            r.UnstuckSeq = seq;
            TryUnstuck(r);
        }

        // Host: the ask judged on the host's own count; the way back set for BackDelay from now.
        bool TryUnstuck(Runner r)
        {
            string who = session.Name(r.Id);
            if (!Started() || ended || r.Pawn == null || r.Driver == null || r.RespawnTime >= 0f || r.UnstuckTime >= 0f) return false;
            if (!StuckWatch.Ready(Time.time, r.UnstuckUsed))
            {
                Debug.Log($"[PawnRush] 끼임: {who} 님의 되돌아가기 - 아직 다시 쓸 수 없음");
                return false;
            }
            if (!r.Stuck.StuckWithin(StuckWatch.Slack))
            {
                Debug.Log($"[PawnRush] 끼임: {who} 님의 되돌아가기 - 방장 쪽에서는 끼지 않음 ({r.Stuck.Pushed:0.0}초)");
                return false;
            }
            BackSpot(r, out r.UnstuckAt, out r.UnstuckRotation, out string where);
            r.UnstuckTime = Time.time + StuckWatch.BackDelay;
            r.UnstuckUsed = Time.time;
            r.Stuck.Reset();
            Debug.Log($"[PawnRush] 끼임: {who} 님을 {StuckWatch.BackDelay:0}초 뒤 {where}(으)로 되돌림");
            return true;
        }

        void PutBack(Runner r)
        {
            r.UnstuckTime = -1f;
            // A fall's respawn waiting (or one just done) wins: that is a way back too.
            if (r.RespawnTime >= 0f || r.Pawn == null || r.Driver == null) return;
            r.Driver.Teleport(r.UnstuckAt, r.UnstuckRotation);   // marks the next snapshot as a jump cut
            var drop = r.Pawn.GetComponentInChildren<FallDistanceRespawn>();
            if (drop != null) drop.Forget();
            if (r.Id == session.Self && orbit != null) orbit.Cut();
        }

        // The highest checkpoint this seat has reached that is not ahead of the pawn, on its spot; else its start spot.
        void BackSpot(Runner r, out Vector3 at, out Quaternion rotation, out string where)
        {
            var hips = r.Pawn.Hips.position;
            var round = PawnRushRound.Current;
            var orders = new List<int>();
            var spots = new List<Vector3>();
            var turns = new List<Quaternion>();
            if (r.Checkpoint != int.MinValue)
                foreach (var checkpoint in FindObjectsByType<Checkpoint>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
                {
                    if (checkpoint == null || checkpoint.Order > r.Checkpoint || orders.Contains(checkpoint.Order)) continue;
                    var course = checkpoint.GetComponent<CourseCheckpoint>();
                    if (course != null && !course.Shared && course.Team != r.Team) continue;
                    orders.Add(checkpoint.Order);
                    spots.Add(course != null && course.SpotCount > 0 ? course.SpotWorld(r.Slot % course.SpotCount) : checkpoint.RespawnPosition);
                    turns.Add(checkpoint.transform.rotation);
                }
            int count = orders.Count;
            var sections = new int[count];
            var metres = new float[count];
            var dist = new float[count];
            for (int i = 0; i < count; i++)
            {
                dist[i] = Vector3.Distance(spots[i], hips);
                if (round == null || !round.TryProgress(spots[i], r.Team, out sections[i], out metres[i], out _)) sections[i] = -1;
            }
            int section = -1;
            float along = 0f;
            if (round == null || !round.TryProgress(hips, r.Team, out section, out along, out _)) section = -1;
            int pick = StuckWatch.Back(orders.ToArray(), sections, metres, dist, count, section, along);
            if (pick >= 0)
            {
                at = spots[pick];
                rotation = turns[pick];
                where = "체크포인트 " + orders[pick];
                return;
            }
            SpawnSpot(r.Team, r.Slot, out at, out rotation);
            where = "출발 자리";
        }

        // ------------------------------------------------------------------ the prompt

        void UnstuckGui()
        {
            if (session == null || !session.Started || session.Match == 0 || Spectating || ended || !Started()
                || LoadingScreenView.Showing || SettingsWindow.IsOpen) return;
            float unit = Screen.height / 1080f;
            float now = Time.time;
            // The screen dims while it waits, and clears again once it is back.
            float dim = 0f;
            if (unstuckAskedAt >= 0f) dim = Mathf.Clamp01((now - unstuckAskedAt) / StuckWatch.BackDelay) * .8f;
            else if (unstuckArrivedAt >= 0f && now - unstuckArrivedAt < .45f) dim = .8f * (1f - (now - unstuckArrivedAt) / .45f);
            var was = GUI.color;
            if (dim > 0f)
            {
                GUI.color = new Color(0f, 0f, 0f, dim);
                GUI.DrawTexture(new Rect(0f, 0f, Screen.width, Screen.height), Texture2D.whiteTexture);
            }
            string head = null, line = null;
            float fill = -1f;
            if (unstuckAskedAt >= 0f) head = Loc.T("되돌아가는 중…");
            else if (unstuckRefusedAt >= 0f && now - unstuckRefusedAt < RefusedShown) head = Loc.T("지금은 되돌아갈 수 없어요");
            else if (ownStuckNow)
            {
                head = Loc.T("끼었나요?");
                if (StuckWatch.Ready(now, ownUnstuckUsed))
                {
                    line = Loc.F("{0} 길게 누르면 뒤 체크포인트로 되돌아가요", GameSettings.KeyCap(GameKey.Unstuck));
                    fill = ownHold.Share;
                }
                else line = Loc.F("{0}초 뒤에 다시 되돌아갈 수 있어요", StuckWatch.SecondsLeft(now, ownUnstuckUsed));
            }
            if (head == null) { GUI.color = was; return; }
            if (unstuckText == null)
                unstuckText = new GUIStyle(GUI.skin.label) { font = RuntimePanels.KoreanFont, alignment = TextAnchor.MiddleCenter, richText = false, wordWrap = false };
            float w = 560f * unit, h = (line != null ? 86f : 50f) * unit;
            var box = new Rect(Screen.width * .5f - w * .5f, Screen.height * .64f, w, h);
            GUI.color = new Color(0f, 0f, 0f, .6f);
            GUI.DrawTexture(box, Texture2D.whiteTexture);
            unstuckText.fontSize = Mathf.Max(12, Mathf.RoundToInt(24f * unit));
            unstuckText.fontStyle = FontStyle.Bold;
            GUI.color = new Color(1f, .96f, .86f, 1f);
            GUI.Label(new Rect(box.x, box.y + 6f * unit, w, 38f * unit), head, unstuckText);
            if (line != null)
            {
                unstuckText.fontSize = Mathf.Max(10, Mathf.RoundToInt(17f * unit));
                unstuckText.fontStyle = FontStyle.Normal;
                GUI.Label(new Rect(box.x, box.y + 40f * unit, w, 28f * unit), line, unstuckText);
            }
            if (fill >= 0f)
            {
                var rail = new Rect(box.x + 40f * unit, box.yMax - 12f * unit, w - 80f * unit, 5f * unit);
                GUI.color = new Color(1f, 1f, 1f, .18f);
                GUI.DrawTexture(rail, Texture2D.whiteTexture);
                GUI.color = new Color(1f, .79f, .24f, 1f);
                GUI.DrawTexture(new Rect(rail.x, rail.y, rail.width * fill, rail.height), Texture2D.whiteTexture);
            }
            GUI.color = was;
        }
    }
}
