// Deterministic service double for SteamSession state-machine tests only.
// Outside Assets: never imported or shipped by Unity. Does NOT validate Steam networking.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web.Script.Serialization;

namespace UnityEngine
{
    public static class Time { public static float realtimeSinceStartup; }
    public static class Random { public static float Range(float a,float b) => (a+b)/2; }
    public static class JsonUtility
    {
        public static string ToJson(object o) => new JavaScriptSerializer().Serialize(o);
        public static T FromJson<T>(string text) => new JavaScriptSerializer().Deserialize<T>(text);
    }
}
namespace Steamworks
{
    public struct CSteamID { public ulong m_SteamID; public CSteamID(ulong id) {m_SteamID=id;} }
    public struct SteamAPICall_t { public int Value; }
    public enum EResult { k_EResultOK, k_EResultFail }
    public enum ELobbyType { k_ELobbyTypePublic, k_ELobbyTypePrivate }
    public enum ELobbyComparison { k_ELobbyComparisonEqual }
    public enum ELobbyDistanceFilter { k_ELobbyDistanceFilterDefault }
    public enum EChatRoomEnterResponse { k_EChatRoomEnterResponseSuccess=1, k_EChatRoomEnterResponseFull=4 }
    public enum EChatEntryType { k_EChatEntryTypeChatMsg }
    public enum EFriendFlags { k_EFriendFlagImmediate = 4 }
    public enum EPersonaState { k_EPersonaStateOffline, k_EPersonaStateOnline, k_EPersonaStateBusy, k_EPersonaStateAway }
    public struct AppId_t { public uint m_AppId; public AppId_t(uint id) {m_AppId=id;} }
    public struct CGameID { public ulong m_GameID; }
    public struct FriendGameInfo_t { public CGameID m_gameID; }
    public struct LobbyCreated_t { public ulong m_ulSteamIDLobby; public EResult m_eResult; }
    public struct LobbyEnter_t { public uint m_EChatRoomEnterResponse; }
    public struct LobbyMatchList_t { public uint m_nLobbiesMatching; }
    public struct LobbyChatMsg_t { public ulong m_ulSteamIDLobby; public uint m_iChatID; }
    public struct GameLobbyJoinRequested_t { public CSteamID m_steamIDLobby; }
    public struct GameRichPresenceJoinRequested_t { public CSteamID m_steamIDFriend; public string m_rgchConnect; }
    public sealed class Callback<T> : IDisposable
    {
        readonly Action<T> action; readonly ulong user;
        Callback(Action<T> action) {this.action=action;user=FakeSteam.User;FakeSteam.Handlers[(user,typeof(T))]=o=>action((T)o);}
        public static Callback<T> Create(Action<T> action) => new Callback<T>(action);
        public void Dispose() {FakeSteam.Handlers.Remove((user,typeof(T)));}
    }
    public sealed class CallResult<T> : IDisposable
    {
        public static CallResult<T> Create() => new CallResult<T>();
        public void Set(SteamAPICall_t call, Action<T,bool> callback)
        { var value=(T)FakeSteam.Results[call.Value];FakeSteam.Results.Remove(call.Value);FakeSteam.Enqueue(FakeSteam.User,()=>callback(value,false)); }
        public void Dispose() { }
    }
    public static class FakeSteam
    {
        public sealed class Lobby
        {
            public ulong Id,Owner; public int Limit; public bool Joinable=true; public ELobbyType Type;
            public List<ulong> Members=new List<ulong>(); public Dictionary<string,string> Data=new Dictionary<string,string>();
            public Dictionary<(ulong,string),string> MemberData=new Dictionary<(ulong,string),string>();
            public List<(ulong,byte[])> Chat=new List<(ulong,byte[])>();
        }
        public static ulong User;
        public static Dictionary<ulong,Lobby> Lobbies=new Dictionary<ulong,Lobby>();
        public static Dictionary<(ulong,Type),Action<object>> Handlers=new Dictionary<(ulong,Type),Action<object>>();
        public static Dictionary<int,object> Results=new Dictionary<int,object>();
        public static Dictionary<ulong,Queue<Action>> Pending=new Dictionary<ulong,Queue<Action>>();
        public static HashSet<ulong> Denied=new HashSet<ulong>();
        public static Dictionary<(ulong,string),string> Presence=new Dictionary<(ulong,string),string>();
        public static void Raise<T>(ulong user,T value) {if(Handlers.TryGetValue((user,typeof(T)),out var h))h(value);}
        public static Dictionary<ulong,List<ulong>> SearchResults=new Dictionary<ulong,List<ulong>>();
        public static Dictionary<string,string> Filters=new Dictionary<string,string>();
        public static int RequiredSlots;
        static ulong nextLobby=1000; static int nextCall;
        public static void Reset() {Lobbies.Clear();Presence.Clear();Handlers.Clear();Results.Clear();Pending.Clear();Denied.Clear();nextLobby=1000;nextCall=0;UnityEngine.Time.realtimeSinceStartup=0;}
        public static SteamAPICall_t Result(object value) {int id=++nextCall;Results[id]=value;return new SteamAPICall_t{Value=id};}
        public static void Enqueue(ulong user,Action action) {if(!Pending.ContainsKey(user))Pending[user]=new Queue<Action>();Pending[user].Enqueue(action);}
        public static void Dispatch() {if(!Pending.TryGetValue(User,out var q))return;int n=q.Count;for(int i=0;i<n;i++)q.Dequeue()();}
        public static Lobby Get(CSteamID id) => Lobbies.TryGetValue(id.m_SteamID,out var l)?l:null;
        public static SteamAPICall_t Create(ELobbyType type,int limit)
        {ulong id=++nextLobby;var l=new Lobby{Id=id,Owner=User,Limit=limit,Type=type};l.Members.Add(User);Lobbies[id]=l;return Result(new LobbyCreated_t{m_ulSteamIDLobby=id,m_eResult=EResult.k_EResultOK});}
    }
    public static class Packsize { public static bool Test()=>true; }
    public static class DllCheck { public static bool Test()=>true; }
    public static class SteamAPI {public static bool Init()=>true;public static void RunCallbacks()=>FakeSteam.Dispatch();public static void Shutdown(){} }
    public static class SteamUser { public static CSteamID GetSteamID()=>new CSteamID(FakeSteam.User);public static bool BLoggedOn()=>true; }
    public static class SteamNetworkingUtils { public static void InitRelayNetworkAccess(){} }
    public static class SteamUtils { public static AppId_t GetAppID()=>new AppId_t(480); }
    public static class SteamFriends
    {
        public static string GetFriendPersonaName(CSteamID id)=>"Player "+id.m_SteamID;
        public static void ActivateGameOverlayInviteDialog(CSteamID id){}
        // The session tests never exercise the friend list; these keep it compiling.
        public static int GetFriendCount(EFriendFlags flags)=>0;
        public static CSteamID GetFriendByIndex(int index,EFriendFlags flags)=>new CSteamID(0);
        public static EPersonaState GetFriendPersonaState(CSteamID id)=>EPersonaState.k_EPersonaStateOffline;
        public static bool GetFriendGamePlayed(CSteamID id,out FriendGameInfo_t info) {info=default;return false;}
        public static bool SetRichPresence(string key,string value) {if(string.IsNullOrEmpty(value))FakeSteam.Presence.Remove((FakeSteam.User,key));else FakeSteam.Presence[(FakeSteam.User,key)]=value;return true;}
        public static void ClearRichPresence() {foreach(var k in FakeSteam.Presence.Keys.Where(k=>k.Item1==FakeSteam.User).ToList())FakeSteam.Presence.Remove(k);}
    }
    public static class SteamMatchmaking
    {
        public static CSteamID GetLobbyOwner(CSteamID id)=>new CSteamID(FakeSteam.Get(id)?.Owner??0);
        public static string GetLobbyData(CSteamID id,string key) {var lobby=FakeSteam.Get(id);return lobby!=null&&lobby.Data.TryGetValue(key,out var value)?value:"";}
        public static bool SetLobbyData(CSteamID id,string key,string value) {var l=FakeSteam.Get(id);if(l==null||l.Owner!=FakeSteam.User)return false;l.Data[key]=value;return true;}
        public static int GetNumLobbyMembers(CSteamID id)=>FakeSteam.Get(id)?.Members.Count??0;
        public static CSteamID GetLobbyMemberByIndex(CSteamID id,int i)=>new CSteamID(FakeSteam.Get(id).Members[i]);
        public static string GetLobbyMemberData(CSteamID id,CSteamID user,string key) {var lobby=FakeSteam.Get(id);return lobby!=null&&lobby.MemberData.TryGetValue((user.m_SteamID,key),out var value)?value:"";}
        public static void SetLobbyMemberData(CSteamID id,string key,string value)=>FakeSteam.Get(id).MemberData[(FakeSteam.User,key)]=value;
        public static bool InviteUserToLobby(CSteamID lobby,CSteamID invitee)=>true;
        public static bool SetLobbyJoinable(CSteamID id,bool value) {var l=FakeSteam.Get(id);if(l.Owner!=FakeSteam.User)return false;l.Joinable=value;return true;}
        public static SteamAPICall_t CreateLobby(ELobbyType type,int limit)=>FakeSteam.Create(type,limit);
        public static SteamAPICall_t JoinLobby(CSteamID id)
        {
            var l=FakeSteam.Get(id);bool ok=l!=null&&l.Joinable&&l.Members.Count<l.Limit&&!FakeSteam.Denied.Contains(FakeSteam.User);
            if(ok&&!l.Members.Contains(FakeSteam.User))l.Members.Add(FakeSteam.User);
            return FakeSteam.Result(new LobbyEnter_t{m_EChatRoomEnterResponse=(uint)(ok?EChatRoomEnterResponse.k_EChatRoomEnterResponseSuccess:EChatRoomEnterResponse.k_EChatRoomEnterResponseFull)});
        }
        public static void LeaveLobby(CSteamID id) {var l=FakeSteam.Get(id);if(l==null)return;l.Members.Remove(FakeSteam.User);if(l.Owner==FakeSteam.User)l.Owner=l.Members.FirstOrDefault();if(l.Members.Count==0)FakeSteam.Lobbies.Remove(l.Id);}
        public static void AddRequestLobbyListStringFilter(string key,string value,ELobbyComparison comparison)=>FakeSteam.Filters[key]=value;
        public static void AddRequestLobbyListFilterSlotsAvailable(int n)=>FakeSteam.RequiredSlots=n;
        public static void AddRequestLobbyListDistanceFilter(ELobbyDistanceFilter f){}
        public static void AddRequestLobbyListResultCountFilter(int n){}
        public static SteamAPICall_t RequestLobbyList()
        {
            FakeSteam.SearchResults[FakeSteam.User]=FakeSteam.Lobbies.Values.Where(l=>l.Type==ELobbyType.k_ELobbyTypePublic&&l.Joinable&&l.Limit-l.Members.Count>=FakeSteam.RequiredSlots&&FakeSteam.Filters.All(f=>l.Data.TryGetValue(f.Key,out var v)&&v==f.Value)).Select(l=>l.Id).ToList();
            FakeSteam.Filters.Clear();return FakeSteam.Result(new LobbyMatchList_t{m_nLobbiesMatching=(uint)FakeSteam.SearchResults[FakeSteam.User].Count});
        }
        public static CSteamID GetLobbyByIndex(int i)=>new CSteamID(FakeSteam.SearchResults[FakeSteam.User][i]);
        public static bool SendLobbyChatMsg(CSteamID id,byte[] bytes,int length)
        {
            var l=FakeSteam.Get(id);int index=l.Chat.Count;l.Chat.Add((FakeSteam.User,bytes));
            foreach(ulong user in l.Members.ToArray()) {ulong peer=user;FakeSteam.Enqueue(peer,()=>{if(FakeSteam.Handlers.TryGetValue((peer,typeof(LobbyChatMsg_t)),out var h))h(new LobbyChatMsg_t{m_ulSteamIDLobby=id.m_SteamID,m_iChatID=(uint)index});});}return true;
        }
        public static int GetLobbyChatEntry(CSteamID id,int index,out CSteamID sender,byte[] bytes,int max,out EChatEntryType type)
        {var entry=FakeSteam.Get(id).Chat[index];sender=new CSteamID(entry.Item1);type=EChatEntryType.k_EChatEntryTypeChatMsg;int n=Math.Min(max,entry.Item2.Length);Array.Copy(entry.Item2,bytes,n);return n;}
    }
}
