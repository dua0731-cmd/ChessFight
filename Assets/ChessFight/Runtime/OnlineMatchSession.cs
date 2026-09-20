using UnityEngine;
namespace ChessFight.ProtectKing
{
    [DefaultExecutionOrder(-100)]
    public sealed class OnlineMatchSession : MonoBehaviour
    {
        public StageMap map;
        public LocalPlaySession local;
        public SteamLobbyTransport transport;
        public bool enableBots=true;
        public PlayerRoster Roster { get; }=new PlayerRoster();
        public bool Online=>transport!=null&&transport.InLobby;
        public bool Authority=>!Online||transport.IsHost;
        public int LocalSlot { get; private set; }=0;
        public bool HasLocalSlot=>!Online||LocalSlot>=0;
        public string Status=>transport.Status;
        PlayerMotor[] motors;
        BotDriver[] bots;
        readonly PlayerCommand[] commands=new PlayerCommand[12];
        readonly float[] lastInput=new float[12],lastPacket=new float[12];
        readonly uint[] jumps=new uint[12],returns=new uint[12];
        PlayerCommand localCommand;
        MatchSnapshot target;
        uint snapshotSequence,lastSnapshot;
        float nextSend,nextHello,lastHostPacket;
        bool initialized;
        bool previousBackground;
        public bool IsBot(int slot)=>enableBots&&(!Online?slot!=local.selectedPlayer:Roster.Owner(slot)==0);
        void Awake()
        {
            previousBackground=Application.runInBackground;Application.runInBackground=true;
            motors=new PlayerMotor[12];bots=new BotDriver[12];
            for(int i=0;i<12;i++) { motors[i]=map.players[i].GetComponent<PlayerMotor>();bots[i]=map.players[i].GetComponent<BotDriver>(); }
            transport.Connected+=OnConnected;transport.Received+=Receive;transport.PeerLeft+=Release;transport.Disconnected+=OnDisconnected;
            local.online=this;initialized=true;
        }
        void OnConnected()
        {
            Roster.Clear();ClearCommands();target=null;lastSnapshot=0;localCommand=default;nextSend=0;nextHello=0;lastHostPacket=Time.unscaledTime;
            if(transport.IsHost) { LocalSlot=Roster.Assign(transport.LocalId);local.SelectPlayer(LocalSlot); }
            else { LocalSlot=-1;map.match.HasAuthority=false; }
            SetAuthority();
        }
        void OnDisconnected()
        {
            if(!initialized)return;
            Roster.Clear();ClearCommands();target=null;LocalSlot=0;map.match.HasAuthority=true;
            map.match.ReturnToReady();SetAuthority();local.SelectPlayer(0);
        }
        void SetAuthority()
        {
            map.match.HasAuthority=Authority;
            for(int i=0;i<12;i++) { motors[i].Simulate=Authority;motors[i].ExternalControl=Authority&&enableBots; }
        }
        void ClearCommands()
        {
            System.Array.Clear(commands,0,12);System.Array.Clear(jumps,0,12);System.Array.Clear(returns,0,12);
            for(int i=0;i<12;i++) { motors[i].ClearInput();bots[i]?.ResetRoute(); }
        }
        public void Submit(Vector2 move,float yaw,bool jump,bool interact,bool respawn)
        {
            if(!HasLocalSlot)return;
            localCommand.move=Vector2.ClampMagnitude(move,1);localCommand.yaw=yaw;localCommand.interact=interact;
            if(jump)localCommand.jump++;if(respawn)localCommand.respawn++;
            int slot=Online?LocalSlot:local.selectedPlayer;
            if(Authority) { localCommand.sequence++;Accept(slot,localCommand); }
        }
        public void StartMatch()
        {
            if(!Authority)return;
            ClearCommands();localCommand=default;map.match.StartMatch();
        }
        void Accept(int slot,PlayerCommand input)
        {
            if(slot<0||slot>=12||!input.IsValid||!MatchProtocol.Newer(input.sequence,commands[slot].sequence))return;
            // Establish counter baselines after join/restart; old presses must not replay.
            if(commands[slot].sequence==0) { jumps[slot]=input.jump;returns[slot]=input.respawn; }
            commands[slot]=input;lastInput[slot]=Time.unscaledTime;lastPacket[slot]=Time.unscaledTime;
        }
        public void Release(ulong peer)
        {
            int slot=Roster.Release(peer);if(slot<0)return;
            commands[slot]=default;jumps[slot]=0;returns[slot]=0;motors[slot].ClearInput();bots[slot]?.ResetRoute();
            map.match.throne.ResetPlayer(map.players[slot]);
        }
        void Receive(ulong peer,byte[] bytes)
        {
            if(!MatchProtocol.Decode(bytes,out var kind,out var input,out var snapshot))return;
            if(Authority) {
                if(!transport.IsMember(peer))return;
                if(kind==MatchMessage.Hello) {
                    bool newPeer=Roster.Find(peer)<0;
                    int slot=Roster.Assign(peer);
                    if(slot>=0) {
                        if(newPeer) { motors[slot].ClearInput();commands[slot]=default;jumps[slot]=0;returns[slot]=0;map.match.throne.ResetPlayer(map.players[slot]); }
                        lastPacket[slot]=Time.unscaledTime;SendSnapshot(peer,true);
                    }
                } else if(kind==MatchMessage.Input) {
                    int slot=Roster.Find(peer);if(slot>=0)Accept(slot,input);
                } else if(kind==MatchMessage.Goodbye)Release(peer);
            } else if(peer==transport.HostId&&kind==MatchMessage.Snapshot&&MatchProtocol.Newer(snapshot.sequence,lastSnapshot)) {
                lastHostPacket=Time.unscaledTime;lastSnapshot=snapshot.sequence;target=snapshot;Roster.Apply(snapshot.owners);
                int assigned=Roster.Find(transport.LocalId);
                if(assigned!=LocalSlot) { LocalSlot=assigned;if(assigned>=0)local.SelectPlayer(assigned); }
                map.match.ApplyRemote(snapshot.phase,snapshot.elapsed,snapshot.remaining,snapshot.result);
                for(int i=0;i<12;i++) {
                    var p=map.players[i];bool snap=p.falls!=snapshot.falls[i]||Vector3.Distance(p.transform.position,snapshot.positions[i])>4;
                    p.checkpoint=snapshot.checkpoint[i];p.falls=snapshot.falls[i];
                    map.match.throne.ApplyRemote(i,snapshot.hold[i]);
                    if(snap)motors[i].Teleport(snapshot.positions[i]);
                }
            }
        }
        void Update()
        {
            SetAuthority();
            if(!Authority) {
                if(Time.unscaledTime-lastHostPacket>15) { transport.Leave("Host stopped responding.");return; }
                if(target!=null)for(int i=0;i<12;i++) {
                    motors[i].Teleport(Vector3.Lerp(motors[i].transform.position,target.positions[i],1-Mathf.Exp(-20*Time.unscaledDeltaTime)));
                    motors[i].ViewYaw=target.yaw[i];
                }
                if(LocalSlot<0&&Time.unscaledTime>=nextHello) { nextHello=Time.unscaledTime+1;transport.Send(transport.HostId,MatchProtocol.Encode(MatchMessage.Hello),true); }
                if(LocalSlot>=0&&Time.unscaledTime>=nextSend) {
                    nextSend=Time.unscaledTime+.05f;localCommand.sequence++;
                    transport.Send(transport.HostId,MatchProtocol.Encode(MatchMessage.Input,localCommand),false);
                }
                return;
            }
            for(int i=0;i<12;i++) {
                if(Online&&Roster.Owner(i)!=0&&i!=LocalSlot&&Time.unscaledTime-lastPacket[i]>10)Release(Roster.Owner(i));
                var motor=motors[i];
                motor.ExternalControl=Online||IsBot(i);
                if(IsBot(i)) { if(map.match.IsRunning)bots[i]?.Drive(Time.deltaTime); }
                else if(Online||i==local.selectedPlayer) {
                    var command=commands[i];bool fresh=Time.unscaledTime-lastInput[i]<.5f;
                    motor.ViewYaw=command.yaw;motor.SetInput(fresh?command.move:Vector2.zero,fresh&&command.jump!=jumps[i]);
                    jumps[i]=command.jump;
                    if(fresh&&command.respawn!=returns[i]&&map.match.IsRunning)map.Respawn(motor);
                    returns[i]=command.respawn;
                }
            }
        }
        void LateUpdate()
        {
            if(!Authority)return;
            for(int i=0;i<12;i++) {
                bool held=IsBot(i)?bots[i]!=null&&bots[i].Interact:
                    Time.unscaledTime-lastInput[i]<.5f&&commands[i].interact;
                map.match.throne.TickInteraction(map.players[i],held,Time.deltaTime);
            }
            if(Online&&Time.unscaledTime>=nextSend) {
                nextSend=Time.unscaledTime+.05f;
                var bytes=MatchProtocol.Encode(MatchMessage.Snapshot,snapshot:Capture());
                for(int i=0;i<12;i++) { var peer=Roster.Owner(i);if(peer!=0&&peer!=transport.LocalId)transport.Send(peer,bytes,false); }
            }
        }
        MatchSnapshot Capture()
        {
            var s=new MatchSnapshot {sequence=++snapshotSequence,phase=map.match.Phase,elapsed=map.match.Elapsed,remaining=map.match.Remaining,
                result=(byte)(map.match.Result.StartsWith("BLUE")?1:map.match.Result.StartsWith("RED")?2:map.match.Phase==MatchPhase.Finished?3:0)};
            for(int i=0;i<12;i++) {
                s.owners[i]=Roster.Owner(i);s.positions[i]=map.players[i].transform.position;s.yaw[i]=motors[i].ViewYaw;
                s.checkpoint[i]=(byte)map.players[i].checkpoint;s.falls[i]=(ushort)Mathf.Clamp(map.players[i].falls,0,65535);
                s.hold[i]=map.match.throne.GetFraction(i);
            }
            return s;
        }
        void SendSnapshot(ulong peer,bool reliable)=>transport.Send(peer,MatchProtocol.Encode(MatchMessage.Snapshot,snapshot:Capture()),reliable);
        void OnDestroy()
        {
            Application.runInBackground=previousBackground;
            if(transport==null)return;
            transport.Connected-=OnConnected;transport.Received-=Receive;transport.PeerLeft-=Release;transport.Disconnected-=OnDisconnected;
        }
    }
}