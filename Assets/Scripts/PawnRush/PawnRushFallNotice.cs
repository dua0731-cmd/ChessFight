using System.Collections.Generic;
using ChessFight.Game;
using ChessFight.Gameplay;
using UnityEngine;

namespace ChessFight.PawnRush
{
    // R679 (the user after the 6 vs 6 test, 11-16: "낙하 시 유저피드백 확실히"): what a player is shown when its own
    // pawn falls off the course, into the water or into the other team's zone. Until now a match showed nothing at
    // all - the pawn floated or fell for a second and a half and was somewhere else (only the offline playtest's
    // help box said "추락!").
    //
    //   the fall     the screen's edges darken, "떨어졌습니다!" (or the water's / the zone's line) stands over the
    //                middle with a brass line that runs out as the rule's delay does, and a sound
    //   the return   a short bright flash, "체크포인트에서 다시 출발!", a sound, and a brass ring on the floor where
    //                the pawn stands again - the ring for every pawn, so the others see who came back where
    //
    // Nothing is sent. Every PC hears the same course rules as the host (KillVolume, WaterZone, FallDistanceRespawn,
    // TeamZone: a client's puppets pass through the same triggers, R502) and sees the return as the pawn's jump - a
    // step of more than JumpMetres in one frame, which only a teleport makes. A return that is never seen (a
    // checkpoint next to the fall) ends the notice a moment after the rule's delay.
    //
    // Sounds: `sfx_fall` and `sfx_respawn` in Resources/PawnRushMiniGameSfx, and until those files exist the skill's
    // fail sound and the promotion pad's chime in their place (PAWN_RUSH_AUDIO.md has the prompts).
    public sealed class PawnRushFallNotice : MonoBehaviour
    {
        enum Cause { Fall, Water, Zone }

        sealed class Drop
        {
            public Cause Cause;
            public float At, Delay;
            public Vector3 Last;
        }

        const float JumpMetres = 4f;        // a step this long in one frame is the respawn's teleport, not a fall
        const float GiveUp = 1.2f;          // past the rule's delay: the notice ends without a seen return
        const float BackSeconds = 1.3f;     // how long the return's line stays
        static readonly Color Ivory = new Color(.96f, .92f, .82f), Brass = new Color(.85f, .7f, .35f), Ebony = new Color(.05f, .04f, .06f);

        readonly Dictionary<ICharacterDriver, Drop> drops = new Dictionary<ICharacterDriver, Drop>();
        readonly List<ICharacterDriver> done = new List<ICharacterDriver>();
        float backAt = -10f;
        Cause backCause;
        Texture2D vignette;
        GUIStyle title, line;
        int styledFor;

        void OnEnable()
        {
            KillVolume.Entered += OnKill;
            WaterZone.Entered += OnWater;
            FallDistanceRespawn.Fell += OnFell;
            TeamZone.Intruded += OnIntruded;
        }

        void OnDisable()
        {
            KillVolume.Entered -= OnKill;
            WaterZone.Entered -= OnWater;
            FallDistanceRespawn.Fell -= OnFell;
            TeamZone.Intruded -= OnIntruded;
            drops.Clear();
        }

        void OnDestroy()
        {
            if (vignette != null) Destroy(vignette);
        }

        void OnKill(ICharacterDriver who, KillVolume volume) => Begin(who, volume.RespawnDelay, Cause.Fall);
        void OnWater(ICharacterDriver who, WaterZone water) => Begin(who, water.RespawnDelay, Cause.Water);
        void OnFell(ICharacterDriver who, FallDistanceRespawn rule) => Begin(who, rule.RespawnDelay, Cause.Fall);
        void OnIntruded(ICharacterDriver who, TeamZone zone) => Begin(who, 0f, Cause.Zone);

        // Called from physics callbacks: only noted here, nothing is moved (HANDOFF rule 14).
        void Begin(ICharacterDriver who, float delay, Cause cause)
        {
            if (who == null || drops.ContainsKey(who)) return;
            var target = who.FollowTarget;
            if (target == null) return;
            Vector3 at = target.position;
            // the garden's water puts pawns back through a kill volume just under its surface
            if (cause == Cause.Fall && WaterZone.SurfaceAt(at, out _, 1.5f)) cause = Cause.Water;
            drops[who] = new Drop { Cause = cause, At = Time.time, Delay = Mathf.Max(0f, delay), Last = at };
            if (who == MiniGameBase.LocalDriver && cause != Cause.Zone)
                ArenaFx.Sfx("sfx_fall", at, twoD: true, volume: .8f, fallbackPath: "PawnRushSkillSfx/SkillFail");
        }

        void Update()
        {
            if (drops.Count == 0) return;
            done.Clear();
            foreach (var pair in drops)
            {
                var who = pair.Key;
                var drop = pair.Value;
                var target = who is Object o && o == null ? null : who.FollowTarget;
                if (target == null) { done.Add(who); continue; }
                Vector3 at = target.position;
                bool jumped = (at - drop.Last).sqrMagnitude > JumpMetres * JumpMetres;
                drop.Last = at;
                if (jumped) Back(who, drop, at);
                if (jumped || Time.time - drop.At > drop.Delay + GiveUp) done.Add(who);
            }
            foreach (var who in done) drops.Remove(who);
        }

        void Back(ICharacterDriver who, Drop drop, Vector3 at)
        {
            // the follow target is the hips: the ring goes on the floor under them
            Vector3 floor = at + Vector3.down * .9f;
            if (Physics.Raycast(at, Vector3.down, out var hit, 3f, ~0, QueryTriggerInteraction.Ignore)) floor = hit.point;
            ArenaFx.Ring(floor, .3f, 1.9f, .6f, Brass);
            if (who != MiniGameBase.LocalDriver) return;
            backAt = Time.time;
            backCause = drop.Cause;
            ArenaFx.Sfx("sfx_respawn", at, twoD: true, volume: .7f, fallbackPath: "PawnRushMiniGameSfx/sfx_mg_quarter");
        }

        void OnGUI()
        {
            if (Event.current.type != EventType.Repaint || LoadingScreenView.Showing) return;
            var me = MiniGameBase.LocalDriver;
            Drop mine = null;
            if (me != null) drops.TryGetValue(me, out mine);
            float back = Time.time - backAt;
            if (mine == null && back > BackSeconds) return;
            Styles();
            float unit = Mathf.Max(.75f, Screen.height / 1080f);
            var full = new Rect(0, 0, Screen.width, Screen.height);
            var old = GUI.color;
            if (mine != null)
            {
                float since = Time.time - mine.At, k = Mathf.Clamp01(since / .2f);
                GUI.color = new Color(Ebony.r, Ebony.g, Ebony.b, .92f * k);
                GUI.DrawTexture(full, Vignette());
                GUI.color = new Color(0f, 0f, 0f, .22f * k);
                GUI.DrawTexture(full, Texture2D.whiteTexture);
                float y = Screen.height * .3f;
                Shadowed(new Rect(0, y, Screen.width, 60 * unit), FallLine(mine.Cause), title, Ivory, k);
                Shadowed(new Rect(0, y + 58 * unit, Screen.width, 30 * unit), "체크포인트로 돌아갑니다", line, Brass, k);
                if (mine.Delay > .3f)
                {
                    float left = Mathf.Clamp01(1f - since / mine.Delay), w = 280f * unit, h = 4f * unit;
                    var bar = new Rect((Screen.width - w) * .5f, y + 98 * unit, w, h);
                    GUI.color = new Color(0f, 0f, 0f, .55f * k);
                    GUI.DrawTexture(bar, Texture2D.whiteTexture);
                    GUI.color = new Color(Brass.r, Brass.g, Brass.b, k);
                    GUI.DrawTexture(new Rect(bar.x, bar.y, w * left, h), Texture2D.whiteTexture);
                }
            }
            else
            {
                float flash = Mathf.Clamp01(1f - back / .35f);
                if (flash > 0f)
                {
                    GUI.color = new Color(1f, .97f, .88f, .38f * flash);
                    GUI.DrawTexture(full, Texture2D.whiteTexture);
                }
                float fade = Mathf.Clamp01((BackSeconds - back) / .4f);
                Shadowed(new Rect(0, Screen.height * .3f + 14 * unit, Screen.width, 40 * unit),
                    backCause == Cause.Zone ? "상대 진영입니다 · 체크포인트에서 다시 출발!" : "체크포인트에서 다시 출발!", line, Ivory, fade);
            }
            GUI.color = old;
        }

        static string FallLine(Cause cause)
        {
            switch (cause)
            {
                case Cause.Water: return "<b>물에 빠졌습니다!</b>";
                case Cause.Zone: return "<b>상대 진영입니다!</b>";
                default: return "<b>떨어졌습니다!</b>";
            }
        }

        void Shadowed(Rect r, string text, GUIStyle style, Color color, float alpha)
        {
            GUI.color = new Color(0f, 0f, 0f, .7f * alpha);
            GUI.Label(new Rect(r.x + 2, r.y + 2, r.width, r.height), text, style);
            GUI.color = new Color(color.r, color.g, color.b, alpha);
            GUI.Label(r, text, style);
        }

        void Styles()
        {
            if (title != null && styledFor == Screen.height) return;
            styledFor = Screen.height;
            float unit = Mathf.Max(.75f, Screen.height / 1080f);
            var font = RuntimePanels.KoreanFont;
            title = new GUIStyle(GUI.skin.label) { font = font, fontSize = Mathf.RoundToInt(46 * unit), alignment = TextAnchor.MiddleCenter, richText = true };
            line = new GUIStyle(title) { fontSize = Mathf.RoundToInt(22 * unit) };
            title.normal.textColor = line.normal.textColor = Color.white;
        }

        // Clear in the middle, full at the corners: stretched over the screen and tinted.
        Texture2D Vignette()
        {
            if (vignette != null) return vignette;
            const int size = 96;
            vignette = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.HideAndDontSave };
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float dx = (x + .5f) / size * 2f - 1f, dy = (y + .5f) / size * 2f - 1f;
                    float d = Mathf.Sqrt(dx * dx + dy * dy) / 1.4142f;
                    float a = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(.32f, 1f, d));
                    pixels[y * size + x] = new Color32(255, 255, 255, (byte)(a * 255f));
                }
            vignette.SetPixels32(pixels);
            vignette.Apply(false, true);
            return vignette;
        }
    }
}
