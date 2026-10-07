using System;
using ChessFight.Network;
using UnityEngine;

namespace ChessFight.Game
{
    // The name the player chose on the name screen (R84), kept on this PC. The
    // network runtime hands it to the session, which shows it and publishes it to
    // the other players (Docs/Network/SESSION.md "nick").
    public static class PlayerProfile
    {
        const string Key = "cf.name";

        public static event Action Changed;

        // The saved name, or "" before the player has chosen one (or if what is
        // saved is no longer a valid name).
        public static string Name
        {
            get
            {
                string saved = PlayerPrefs.GetString(Key, "");
                return PlayerNames.Showable(saved) ? saved : "";
            }
        }

        public static bool HasName => Name != "";

        public static void Set(string name)
        {
            string tidy = PlayerNames.Tidy(name);
            if (!PlayerNames.Check(tidy, false).Ok) return;
            PlayerPrefs.SetString(Key, tidy);
            PlayerPrefs.Save();
            Changed?.Invoke();
        }

        public static void Forget()
        {
            PlayerPrefs.DeleteKey(Key);
            PlayerPrefs.Save();
            Changed?.Invoke();
        }
    }
}
