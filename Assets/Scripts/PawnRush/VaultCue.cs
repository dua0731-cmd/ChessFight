using System;
using System.Collections.Generic;
using ChessFight.Gameplay;
using ChessFight.Network;
using UnityEngine;

namespace ChessFight.PawnRush
{
    // A gameplay cue of the royal vault (R725, build spec §2.1 and §2.12): a look only - it touches no collider and moves
    // nothing - driven every frame by a RoyalVaultRules query on the obstacle clock, so every PC shows the same cue at the
    // same moment as the machine it speaks for. One vocabulary for every cue (RoyalVaultRules.Cue):
    //   Off   hidden
    //   Go    green, "go now"
    //   Warn  red blinking at 4 Hz (RoyalVaultRules.Blink), "about to move"
    //   Stop  red, "moving or occupied"
    // A lamp (Configure) paints its pictures - every renderer under this object except text and another cue's - CueGreen or
    // CueRed, and hides them while Off or blinked off. A text (ConfigureText, the door's countdowns) drives the TextMesh on
    // this object: its words, and its colour - Off in ink (the digits), Go green, Warn red blinking, Stop red. Built by the
    // cue builder alone; no gameplay part refers to a cue. Lambdas handed to it must not capture a loop variable: copy the
    // index into a local inside the loop first.
    public sealed class VaultCue : MonoBehaviour
    {
        Func<double, RoyalVaultRules.Cue> state;
        Func<double, string> words;
        Renderer[] lamps;
        TextMesh text;
        Renderer textRenderer;
        bool applied, lastShown;
        RoyalVaultRules.Cue lastCue;

        // The state the cue last showed (Off until it first shows one).
        public RoyalVaultRules.Cue State => lastCue;
        // Whether it is a text cue (ConfigureText) rather than a lamp.
        public bool IsText => words != null;
        // Whether anything of it was drawn the last time it showed (an Off lamp, a Warn blinked off or an empty text was not).
        public bool Shown => lastShown;

        // A lamp: its pictures coloured by the state at each clock time.
        public void Configure(Func<double, RoyalVaultRules.Cue> state)
        {
            this.state = state;
            words = null;
            lamps = null;
            applied = false;
        }

        // A text: the TextMesh on this object, its words and its look at each clock time.
        public void ConfigureText(Func<double, string> words, Func<double, RoyalVaultRules.Cue> look)
        {
            this.words = words;
            state = look;
            applied = false;
        }

        void OnEnable() => Apply(ObstacleClock.Now);

        void Update() => Apply(ObstacleClock.Now);

        // Shows the cue as it is at a clock time (Update does it every frame; validators may ask for a time of their own).
        public void Apply(double time)
        {
            if (state == null) return;
            var cue = state(time);
            bool blink = RoyalVaultRules.Blink(time);
            if (words != null) ShowText(words(time) ?? "", cue, blink);
            else ShowLamp(cue, blink);
        }

        void ShowLamp(RoyalVaultRules.Cue cue, bool blink)
        {
            if (lamps == null) lamps = Lamps();
            bool visible = cue == RoyalVaultRules.Cue.Go || cue == RoyalVaultRules.Cue.Stop || (cue == RoyalVaultRules.Cue.Warn && blink);
            // Only when it changes: a material set every frame would be work for nothing.
            if (applied && cue == lastCue && visible == lastShown) return;
            var material = cue == RoyalVaultRules.Cue.Go ? RoyalVaultLook.CueGreen : RoyalVaultLook.CueRed;
            foreach (var lamp in lamps)
            {
                if (lamp == null) continue;
                if (visible && lamp.sharedMaterial != material) lamp.sharedMaterial = material;
                if (lamp.enabled != visible) lamp.enabled = visible;
            }
            applied = true;
            lastCue = cue;
            lastShown = visible;
        }

        // The pictures this lamp colours: every renderer under it, except text (its material is the font's) and whatever
        // belongs to another cue further down.
        Renderer[] Lamps()
        {
            var all = GetComponentsInChildren<Renderer>(true);
            var mine = new List<Renderer>(all.Length);
            foreach (var r in all)
            {
                if (r == null || r.TryGetComponent<TextMesh>(out _)) continue;
                var owner = r.transform;
                while (owner != null && !owner.TryGetComponent<VaultCue>(out _)) owner = owner.parent;
                if (owner == transform) mine.Add(r);
            }
            return mine.ToArray();
        }

        void ShowText(string now, RoyalVaultRules.Cue cue, bool blink)
        {
            if (text == null)
            {
                text = GetComponent<TextMesh>();
                if (text == null) return;
                textRenderer = text.GetComponent<Renderer>();
            }
            // Setting a TextMesh's words or colour rebuilds its mesh: only when they change.
            if (text.text != now) text.text = now;
            var colour = cue == RoyalVaultRules.Cue.Go ? RoyalVaultLook.GreenColor
                       : cue == RoyalVaultRules.Cue.Off ? RoyalVaultLook.InkColor
                       : RoyalVaultLook.RedColor;
            if (text.color != colour) text.color = colour;
            bool visible = cue != RoyalVaultRules.Cue.Warn || blink;
            if (textRenderer != null && textRenderer.enabled != visible) textRenderer.enabled = visible;
            applied = true;
            lastCue = cue;
            lastShown = visible && now.Length > 0;
        }
    }
}
