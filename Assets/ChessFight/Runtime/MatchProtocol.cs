using System;
using System.IO;
using UnityEngine;
namespace ChessFight.ProtectKing
{
    public enum MatchMessage : byte { Hello=1, Input=2, Snapshot=3, Goodbye=4 }
    public struct PlayerCommand {
        public uint sequence, jump, respawn;
        public Vector2 move;
        public float yaw;
        public bool interact;
        public bool IsValid => Finite(move.x)&&Finite(move.y)&&Finite(yaw)&&move.sqrMagnitude<=1.02f;
        static bool Finite(float n)=>!float.IsNaN(n)&&!float.IsInfinity(n);
    }
    public sealed class MatchSnapshot {
        public uint sequence;
        public MatchPhase phase;
        public float elapsed, remaining;
        public byte result;
        public ulong[] owners=new ulong[12];
        public Vector3[] positions=new Vector3[12];
        public float[] yaw=new float[12], hold=new float[12];
        public byte[] checkpoint=new byte[12];
        public ushort[] falls=new ushort[12];
    }
    public static class MatchProtocol {
        public const int Version=1, MaxPacket=1200;
        const uint Magic=0x43464631;
        public static bool Newer(uint current,uint previous)=>(int)(current-previous)>0;
        public static byte[] Encode(MatchMessage kind, PlayerCommand command=default, MatchSnapshot snapshot=null) {
            using(var stream=new MemoryStream(600)) using(var w=new BinaryWriter(stream)) {
                w.Write(Magic); w.Write((byte)Version); w.Write((byte)kind);
                if(kind==MatchMessage.Input) {
                    w.Write(command.sequence);w.Write(command.jump);w.Write(command.respawn);
                    w.Write(command.move.x);w.Write(command.move.y);w.Write(command.yaw);w.Write(command.interact);
                } else if(kind==MatchMessage.Snapshot) {
                    w.Write(snapshot.sequence);w.Write((byte)snapshot.phase);w.Write(snapshot.elapsed);w.Write(snapshot.remaining);w.Write(snapshot.result);
                    for(int i=0;i<12;i++) {
                        w.Write(snapshot.owners[i]);w.Write(snapshot.positions[i].x);w.Write(snapshot.positions[i].y);w.Write(snapshot.positions[i].z);
                        w.Write(snapshot.yaw[i]);w.Write(snapshot.checkpoint[i]);w.Write(snapshot.falls[i]);w.Write(snapshot.hold[i]);
                    }
                }
                return stream.ToArray();
            }
        }
        public static bool Decode(byte[] data,out MatchMessage kind,out PlayerCommand command,out MatchSnapshot snapshot) {
            kind=0;command=default;snapshot=null;
            if(data==null||data.Length<6||data.Length>MaxPacket)return false;
            try {
                using(var s=new MemoryStream(data,false)) using(var r=new BinaryReader(s)) {
                    if(r.ReadUInt32()!=Magic||r.ReadByte()!=Version)return false;
                    kind=(MatchMessage)r.ReadByte();
                    if(kind==MatchMessage.Input) {
                        command.sequence=r.ReadUInt32();command.jump=r.ReadUInt32();command.respawn=r.ReadUInt32();
                        command.move=new Vector2(r.ReadSingle(),r.ReadSingle());command.yaw=r.ReadSingle();command.interact=r.ReadBoolean();
                        if(!command.IsValid)return false;
                    } else if(kind==MatchMessage.Snapshot) {
                        snapshot=new MatchSnapshot { sequence=r.ReadUInt32(),phase=(MatchPhase)r.ReadByte(),elapsed=r.ReadSingle(),remaining=r.ReadSingle(),result=r.ReadByte() };
                        if(snapshot.phase>MatchPhase.Finished||!Valid(snapshot.elapsed)||!Valid(snapshot.remaining)||snapshot.elapsed<0||snapshot.remaining<0||snapshot.result>3)return false;
                        for(int i=0;i<12;i++) {
                            snapshot.owners[i]=r.ReadUInt64();
                            snapshot.positions[i]=new Vector3(r.ReadSingle(),r.ReadSingle(),r.ReadSingle());
                            snapshot.yaw[i]=r.ReadSingle();snapshot.checkpoint[i]=r.ReadByte();snapshot.falls[i]=r.ReadUInt16();snapshot.hold[i]=r.ReadSingle();
                            var p=snapshot.positions[i];
                            if(!Valid(p.x)||!Valid(p.y)||!Valid(p.z)||!Valid(snapshot.yaw[i])||!Valid(snapshot.hold[i])||snapshot.checkpoint[i]>5)return false;
                        }
                    } else if(kind!=MatchMessage.Hello&&kind!=MatchMessage.Goodbye)return false;
                    return s.Position==s.Length;
                }
            } catch(EndOfStreamException) { return false; } catch(IOException) { return false; }
        }
        static bool Valid(float n)=>!float.IsNaN(n)&&!float.IsInfinity(n);
    }
}