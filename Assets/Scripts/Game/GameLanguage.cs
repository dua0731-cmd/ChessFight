using System.Collections.Generic;
using ChessFight.Network;
using UnityEngine;

namespace ChessFight.Game
{
    // R747: which language this PC shows, and the string table it reads (Core Loc; Docs/Architecture/UI.md, "언어 · 번역").
    //
    // The table is Resources/Localization/Strings.txt, loaded before the first scene. The language is the player's
    // own pick (settings → 화면 표시 → 일반 → 언어, kept in PlayerPrefs) or, until there is one, Steam's language for
    // this game (UseSteam, from Bootstrap once Steam is up) or Windows' - those two only for a language the table
    // marks ready, so nobody is moved off Korean onto a half-done translation.
    public static class GameLanguage
    {
        const string SavedKey = "cf.s2.language";
        const string TablePath = "Localization/Strings";

        static string steam;

        // The player has picked a language in the settings window.
        public static bool Picked => PlayerPrefs.HasKey(SavedKey);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Boot()
        {
            Loc.Error = Debug.LogException;
            Reload();
        }

        // Reads the table again (also after editing it during Play) and picks the language.
        public static void Reload()
        {
            var asset = Resources.Load<TextAsset>(TablePath);
            if (asset == null) Debug.LogWarning("[ChessFight] 언어 표(Resources/" + TablePath + ".txt)를 찾지 못했습니다. 한국어만 보입니다.");
            var problems = new List<string>();
            var table = asset != null ? StringTable.Parse(asset.text, problems) : StringTable.Empty;
            foreach (var p in problems) Debug.LogWarning("[ChessFight] 언어 표 " + p);
            Loc.Load(table);
            Apply();
        }

        // Steam's language for this game (SteamApps.GetCurrentGameLanguage: "koreana", "english", "schinese", ...).
        public static void UseSteam(string api)
        {
            steam = api;
            Apply();
        }

        public static void Pick(string code)
        {
            PlayerPrefs.SetString(SavedKey, code);
            PlayerPrefs.Save();
            Apply();
        }

        // Back to "whatever Steam or Windows says" (the settings page's reset).
        public static void Forget()
        {
            PlayerPrefs.DeleteKey(SavedKey);
            PlayerPrefs.Save();
            Apply();
        }

        // The languages the settings list offers: those the table has a column for, Korean first.
        public static List<Language> Offered()
        {
            var list = new List<Language>();
            foreach (var l in Languages.All) if (Loc.Table.Has(l.Code)) list.Add(l);
            return list;
        }

        static void Apply()
        {
            string system = Application.systemLanguage.ToString();
            var code = Languages.Choose(PlayerPrefs.GetString(SavedKey, ""), Languages.FromSteam(steam), Languages.FromSystem(system), Loc.Table);
            if (code == Loc.Lang) return;
            Loc.Use(code);
            Debug.Log("[ChessFight] 언어: " + Loc.Lang + (Picked ? " (설정에서 고름)" : " (Steam " + (steam ?? "없음") + " · Windows " + system + ")"));
        }
    }
}
