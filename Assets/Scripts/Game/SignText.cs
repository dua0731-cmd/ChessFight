using System.Collections;
using ChessFight.Network;
using UnityEngine;

namespace ChessFight.Game
{
    // R753: the 3D words on a course (signs, gate labels, station names). Builders write them into prefabs in Korean
    // at authoring time, so they cannot go through Loc.T where they are made. Instead the round sweeps what it has
    // just built: every TextMesh whose text has Hangul keeps that Korean as its key and shows Loc.T(key). A word the
    // table does not have stays Korean, as everywhere else. When the language changes in a match the word follows,
    // unless code has written something else into it since (a counter, a label that changes with its state): that
    // text is then the code's, and the next sweep takes it as the new key.
    [DisallowMultipleComponent]
    public sealed class SignText : MonoBehaviour
    {
        TextMesh mesh;
        string key = "", shown = "";

        // Translates every TextMesh under `root`, now and once more after a frame (the Start and first Update of the
        // objects that write their words late).
        public static void TranslateUnder(Transform root)
        {
            if (root == null) return;
            Sweep(root);
            var host = root.GetComponent<SignTextLateSweep>();
            if (host == null) host = root.gameObject.AddComponent<SignTextLateSweep>();
            host.Again();
        }

        internal static int Sweep(Transform root)
        {
            int done = 0;
            if (root == null) return 0;
            foreach (var text in root.GetComponentsInChildren<TextMesh>(true))
            {
                if (text == null || !HasHangul(text.text)) continue;
                var sign = text.GetComponent<SignText>();
                if (sign == null) sign = text.gameObject.AddComponent<SignText>();
                if (sign.Take(text)) done++;
            }
            return done;
        }

        // Takes the mesh's current text as the key if it is not the one this component last put there.
        bool Take(TextMesh text)
        {
            mesh = text;
            if (mesh.text == shown && key.Length > 0) return false;
            key = mesh.text;
            Show();
            return true;
        }

        void Show()
        {
            if (mesh == null || key.Length == 0) return;
            shown = Loc.T(key);
            mesh.text = shown;
        }

        void OnEnable() => Loc.Changed += LanguageChanged;
        void OnDisable() => Loc.Changed -= LanguageChanged;

        void LanguageChanged()
        {
            if (mesh != null && mesh.text == shown) Show();
        }

        static bool HasHangul(string s)
        {
            if (string.IsNullOrEmpty(s)) return false;
            foreach (char c in s) if (c >= '가' && c <= '힣') return true;
            return false;
        }
    }

    // The second sweep a frame after the first (see SignText.TranslateUnder).
    [DisallowMultipleComponent]
    public sealed class SignTextLateSweep : MonoBehaviour
    {
        public void Again()
        {
            if (isActiveAndEnabled) StartCoroutine(Later());
        }

        IEnumerator Later()
        {
            yield return null;
            yield return null;
            SignText.Sweep(transform);
        }
    }
}
