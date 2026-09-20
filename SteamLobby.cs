using Mirror;
using Steamworks;
using UnityEngine;

public class SteamLobby : MonoBehaviour
{
    const string HostKey = "HostAddress";
    NetworkManager manager;

    Callback<LobbyCreated_t> lobbyCreated;
    Callback<GameLobbyJoinRequested_t> joinRequested;
    Callback<LobbyEnter_t> lobbyEntered;

    void Start()
    {
        manager = GetComponent<NetworkManager>();
        if (!SteamManager.Initialized) return;

        lobbyCreated = Callback<LobbyCreated_t>.Create(OnLobbyCreated);
        joinRequested = Callback<GameLobbyJoinRequested_t>.Create(OnJoinRequested);
        lobbyEntered = Callback<LobbyEnter_t>.Create(OnLobbyEntered);
    }

    void OnGUI()
    {
        if (NetworkServer.active || NetworkClient.active) return;

        if (GUI.Button(new Rect(10, 300, 200, 40), "방 만들기 (Steam)"))
            SteamMatchmaking.CreateLobby(ELobbyType.k_ELobbyTypeFriendsOnly, manager.maxConnections);
    }

    void OnLobbyCreated(LobbyCreated_t cb)
    {
        if (cb.m_eResult != EResult.k_EResultOK) return;

        manager.StartHost();
        SteamMatchmaking.SetLobbyData(new CSteamID(cb.m_ulSteamIDLobby),
                                      HostKey, SteamUser.GetSteamID().ToString());
    }

    void OnJoinRequested(GameLobbyJoinRequested_t cb)
    {
        SteamMatchmaking.JoinLobby(cb.m_steamIDLobby);
    }

    void OnLobbyEntered(LobbyEnter_t cb)
    {
        if (NetworkServer.active) return;

        manager.networkAddress = SteamMatchmaking.GetLobbyData(
            new CSteamID(cb.m_ulSteamIDLobby), HostKey);
        manager.StartClient();
    }
}