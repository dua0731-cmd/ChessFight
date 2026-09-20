using Steamworks;
using UnityEngine;

public class SteamInfo : MonoBehaviour
{
    void OnGUI()
    {
        if (!SteamManager.Initialized)
        {
            GUI.Label(new Rect(10, 200, 400, 30), "X 스팀 연결 실패");
            return;
        }

        string id = SteamUser.GetSteamID().ToString();
        GUI.Label(new Rect(10, 200, 400, 30), "이름: " + SteamFriends.GetPersonaName());
        GUI.Label(new Rect(10, 225, 400, 30), "SteamID: " + id);

        if (GUI.Button(new Rect(10, 250, 150, 30), "ID 복사"))
            GUIUtility.systemCopyBuffer = id;
    }
}