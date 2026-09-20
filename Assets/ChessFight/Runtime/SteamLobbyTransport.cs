using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Steamworks;
using UnityEngine;

namespace ChessFight.ProtectKing
{
    // Steam lobby discovery plus authenticated Steam Networking Messages (SDR-capable P2P).
    // Gameplay authority stays with the original host; lobby-owner migration is not host migration.
    public sealed class SteamLobbyTransport : MonoBehaviour
    {
        public uint appId;
        public event Action Connected;
        public event Action<ulong,byte[]> Received;
        public event Action<ulong> PeerLeft;
        public event Action Disconnected;
        public bool Initialized { get; private set; }
        public bool InLobby => lobby.m_SteamID!=0;
        public bool IsHost => InLobby && LocalId==HostId;
        public bool Busy { get; private set; }
        public ulong LocalId { get; private set; }
        public ulong HostId { get; private set; }
        public ulong LobbyId => lobby.m_SteamID;
        public string Status { get; private set; }="Offline AI practice";
        CSteamID lobby;
        readonly HashSet<ulong> members=new HashSet<ulong>();
        readonly IntPtr[] receiveBuffer=new IntPtr[32];
        Callback<LobbyChatUpdate_t> chat;
        Callback<GameLobbyJoinRequested_t> invitation;
        Callback<SteamNetworkingMessagesSessionRequest_t> request;
        Callback<SteamNetworkingMessagesSessionFailed_t> failure;
        CallResult<LobbyCreated_t> created;
        CallResult<LobbyEnter_t> entered;
        float deadline;
        const int Channel=7;
        const string GameKey="chessfight-protect-king-v1";
        public bool IsMember(ulong id)=>InLobby&&members.Contains(id);
        public bool Initialize()
        {
            if(Initialized)return true;
            if(appId==0) { Status="Set your Steam App ID on SteamLobbyTransport first.";return false; }
            try {
                Environment.SetEnvironmentVariable("SteamAppId",appId.ToString());
                Environment.SetEnvironmentVariable("SteamGameId",appId.ToString());
                var result=SteamAPI.InitEx(out var error);
                if(result!=ESteamAPIInitResult.k_ESteamAPIInitResult_OK) { Status="Steam initialization failed: "+error;return false; }
                Initialized=true;
                if(SteamUtils.GetAppID().m_AppId!=appId) { SteamAPI.Shutdown();Initialized=false;Status="Steam App ID mismatch. Check steam_appid.txt and restart Unity.";return false; }
                LocalId=SteamUser.GetSteamID().m_SteamID;
                SteamNetworkingUtils.InitRelayNetworkAccess();
                created=CallResult<LobbyCreated_t>.Create(OnCreated);
                entered=CallResult<LobbyEnter_t>.Create(OnEntered);
                chat=Callback<LobbyChatUpdate_t>.Create(OnChat);
                invitation=Callback<GameLobbyJoinRequested_t>.Create(v=>Join(v.m_steamIDLobby.m_SteamID));
                request=Callback<SteamNetworkingMessagesSessionRequest_t>.Create(v=>{
                    ulong peer=v.m_identityRemote.GetSteamID64();
                    if(IsMember(peer)&&(IsHost||peer==HostId))SteamNetworkingMessages.AcceptSessionWithUser(ref v.m_identityRemote);
                });
                failure=Callback<SteamNetworkingMessagesSessionFailed_t>.Create(v=>{
                    ulong peer=v.m_info.m_identityRemote.GetSteamID64();
                    if(peer==HostId&&!IsHost)Leave("Connection to host failed.");
                    else PeerLeft?.Invoke(peer);
                });
                Status="Steam ready";
                return true;
            } catch(DllNotFoundException) { Status="Steam native library missing; check Steamworks package import.";return false; }
              catch(InvalidOperationException e) { Status=e.Message;return false; }
        }
        public void Host()
        {
            if(Busy||InLobby||!Initialize())return;
            Busy=true;deadline=Time.unscaledTime+20;
            Status="Creating 12-player Steam lobby...";
            created.Set(SteamMatchmaking.CreateLobby(ELobbyType.k_ELobbyTypeFriendsOnly,12));
        }
        public void Join(ulong id)
        {
            if(Busy||InLobby||id==0||!Initialize())return;
            Busy=true;deadline=Time.unscaledTime+20;Status="Joining Steam lobby...";
            entered.Set(SteamMatchmaking.JoinLobby(new CSteamID(id)));
        }
        void OnCreated(LobbyCreated_t value,bool ioFailure)
        {
            Busy=false;
            if(ioFailure||value.m_eResult!=EResult.k_EResultOK) { Status="Lobby creation failed: "+value.m_eResult;return; }
            lobby=new CSteamID(value.m_ulSteamIDLobby);HostId=LocalId;
            SteamMatchmaking.SetLobbyData(lobby,"game",GameKey);
            SteamMatchmaking.SetLobbyData(lobby,"host",HostId.ToString());
            SteamMatchmaking.SetLobbyData(lobby,"protocol",MatchProtocol.Version.ToString());
            SteamMatchmaking.SetLobbyJoinable(lobby,true);
            RefreshMembers();Status="Hosting Steam lobby "+LobbyId;Connected?.Invoke();
        }
        void OnEntered(LobbyEnter_t value,bool ioFailure)
        {
            Busy=false;
            if(ioFailure||value.m_EChatRoomEnterResponse!=(uint)EChatRoomEnterResponse.k_EChatRoomEnterResponseSuccess) { Status="Lobby join failed: "+value.m_EChatRoomEnterResponse;return; }
            lobby=new CSteamID(value.m_ulSteamIDLobby);
            if(SteamMatchmaking.GetLobbyData(lobby,"game")!=GameKey||
                SteamMatchmaking.GetLobbyData(lobby,"protocol")!=MatchProtocol.Version.ToString()||
                !ulong.TryParse(SteamMatchmaking.GetLobbyData(lobby,"host"),out var host))
                { Leave("Incompatible CHESS FIGHT lobby.");return; }
            HostId=host;RefreshMembers();
            if(!IsMember(host)||SteamMatchmaking.GetLobbyOwner(lobby).m_SteamID!=host) { Leave("Original host is no longer available.");return; }
            Status="Connected to Steam lobby "+LobbyId;Connected?.Invoke();
        }
        void RefreshMembers()
        {
            members.Clear();
            for(int i=0;i<SteamMatchmaking.GetNumLobbyMembers(lobby);i++)
                members.Add(SteamMatchmaking.GetLobbyMemberByIndex(lobby,i).m_SteamID);
        }
        void OnChat(LobbyChatUpdate_t value)
        {
            if(value.m_ulSteamIDLobby!=LobbyId)return;
            RefreshMembers();
            if(!members.Contains(HostId)) { Leave("Host left. Create or join another room.");return; }
            if(!members.Contains(value.m_ulSteamIDUserChanged)) {
                ClosePeer(value.m_ulSteamIDUserChanged);PeerLeft?.Invoke(value.m_ulSteamIDUserChanged);
            }
        }
        public bool Send(ulong peer,byte[] bytes,bool reliable)
        {
            if(!Initialized||!IsMember(peer)||bytes.Length>MatchProtocol.MaxPacket)return false;
            var identity=new SteamNetworkingIdentity();identity.SetSteamID64(peer);
            var pin=GCHandle.Alloc(bytes,GCHandleType.Pinned);
            try {
                int flags=reliable?Constants.k_nSteamNetworkingSend_Reliable:Constants.k_nSteamNetworkingSend_Unreliable;
                var result=SteamNetworkingMessages.SendMessageToUser(ref identity,pin.AddrOfPinnedObject(),(uint)bytes.Length,flags,Channel);
                return result==EResult.k_EResultOK;
            } finally { pin.Free(); }
        }
        void Update()
        {
            if(!Initialized)return;
            SteamAPI.RunCallbacks();
            if(Busy&&Time.unscaledTime>deadline) { created.Cancel();entered.Cancel();Busy=false;Status="Steam lobby request timed out."; }
            // Bound work and packet sizes before copying unmanaged memory.
            int count=SteamNetworkingMessages.ReceiveMessagesOnChannel(Channel,receiveBuffer,receiveBuffer.Length);
            for(int i=0;i<count;i++) {
                var ptr=receiveBuffer[i];
                try {
                    var message=SteamNetworkingMessage_t.FromIntPtr(ptr);
                    ulong peer=message.m_identityPeer.GetSteamID64();
                    if(message.m_cbSize<6||message.m_cbSize>MatchProtocol.MaxPacket||!IsMember(peer)||(!IsHost&&peer!=HostId))continue;
                    var bytes=new byte[message.m_cbSize];Marshal.Copy(message.m_pData,bytes,0,bytes.Length);
                    Received?.Invoke(peer,bytes);
                } finally { SteamNetworkingMessage_t.Release(ptr); }
            }
        }
        public void InviteFriends() { if(InLobby)SteamFriends.ActivateGameOverlayInviteDialog(lobby); }
        void ClosePeer(ulong peer) { var identity=new SteamNetworkingIdentity();identity.SetSteamID64(peer);SteamNetworkingMessages.CloseSessionWithUser(ref identity); }
        public void Leave(string reason="Left lobby",bool notify=true)
        {
            if(Initialized&&InLobby) {
                foreach(var peer in members)if(peer!=LocalId)ClosePeer(peer);
                SteamMatchmaking.LeaveLobby(lobby);
            }
            lobby=default;HostId=0;members.Clear();Busy=false;Status=reason;if(notify)Disconnected?.Invoke();
        }
        void OnDestroy()
        {
            if(!Initialized)return;
            Leave("Session closed",false);chat?.Dispose();invitation?.Dispose();request?.Dispose();failure?.Dispose();created?.Dispose();entered?.Dispose();
            SteamAPI.Shutdown();Initialized=false;
        }
    }
}