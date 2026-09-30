using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using Steamworks;
using Unity.Netcode;
using UnityEngine;
namespace ElevatorGame
{
    // Steam authenticates the sender. Per-connection nonce binds NGO approval to that sender.
    public sealed class SteamTransport : NetworkTransport
    {
        const int Channel=70,Header=21;const uint Magic=0x454C5632;
        public override ulong ServerClientId=>0;
        public ulong RemoteHost;
        public int SessionEpoch;
        public ulong Lobby;
        bool running,server,connected;float helloAt,heartbeatAt,started;
        uint sequence;
        NetworkManager manager;
        readonly Dictionary<ulong,float> peers=new();
        readonly Dictionary<ulong,byte[]> nonces=new();
        readonly Dictionary<ulong,uint> receivedSequence=new();
        readonly Queue<(NetworkEvent kind,ulong id,byte[] data)> events=new();
        readonly IntPtr[] inbox=new IntPtr[32];
        public override void Initialize(NetworkManager networkManager=null){manager=networkManager;}
        public override bool StartServer(){if(!SteamSession.Instance||!SteamSession.Instance.Ready)return false;Reset();server=running=true;return true;}
        public override bool StartClient(){if(!SteamSession.Instance||!SteamSession.Instance.Ready||RemoteHost==0)return false;Reset();running=true;server=false;started=Time.unscaledTime;Hello();return true;}
        void Reset(){events.Clear();peers.Clear();nonces.Clear();receivedSequence.Clear();sequence=0;connected=false;helloAt=heartbeatAt=0;}
        void Hello(){helloAt=Time.unscaledTime+.6f;Packet(RemoteHost,1,Array.Empty<byte>(),true);}
        public bool ApproveIdentity(byte[] payload,out ulong identity)
        {
            identity=0;if(payload==null||payload.Length!=24)return false;
            ulong candidate=BitConverter.ToUInt64(payload,0);
            if(!SteamSession.Instance.IsMember(candidate)||!nonces.TryGetValue(candidate,out var nonce))return false;
            for(int i=0;i<16;i++)if(payload[i+8]!=nonce[i])return false;
            identity=candidate;return true;
        }
        public override void Send(ulong clientId,ArraySegment<byte> payload,NetworkDelivery delivery)
        {
            if(!running)return;ulong target=server?clientId:RemoteHost;
            var data=new byte[payload.Count];Buffer.BlockCopy(payload.Array,payload.Offset,data,0,data.Length);
            bool reliable=delivery!=NetworkDelivery.Unreliable&&delivery!=NetworkDelivery.UnreliableSequenced;
            Packet(target,reliable?(byte)3:(byte)6,data,reliable);
        }
        void Packet(ulong target,byte type,byte[] data,bool reliable)
        {
            if(target==0)return;
            var packet=new byte[Header+data.Length];Buffer.BlockCopy(BitConverter.GetBytes(Magic),0,packet,0,4);
            Buffer.BlockCopy(BitConverter.GetBytes(Lobby),0,packet,4,8);Buffer.BlockCopy(BitConverter.GetBytes(SessionEpoch),0,packet,12,4);
            packet[16]=type;Buffer.BlockCopy(BitConverter.GetBytes(++sequence),0,packet,17,4);Buffer.BlockCopy(data,0,packet,Header,data.Length);
            SendSteam(target,packet,Channel,reliable);
        }
        public static bool SendSteam(ulong target,byte[] data,int channel,bool reliable)
        {
            var identity=new SteamNetworkingIdentity();identity.SetSteamID64(target);IntPtr memory=Marshal.AllocHGlobal(data.Length);
            try{Marshal.Copy(data,0,memory,data.Length);return SteamNetworkingMessages.SendMessageToUser(ref identity,memory,(uint)data.Length,reliable?Constants.k_nSteamNetworkingSend_Reliable:Constants.k_nSteamNetworkingSend_Unreliable,channel)==EResult.k_EResultOK;}
            finally{Marshal.FreeHGlobal(memory);}
        }
        protected override void OnEarlyUpdate(){Pump();}
        void Pump()
        {
            if(!running||!SteamSession.Instance||!SteamSession.Instance.Ready)return;
            int count=SteamNetworkingMessages.ReceiveMessagesOnChannel(Channel,inbox,inbox.Length);
            for(int i=0;i<count;i++)
            {
                try
                {
                    var message=SteamNetworkingMessage_t.FromIntPtr(inbox[i]);ulong sender=message.m_identityPeer.GetSteamID64();
                    if(message.m_cbSize<Header||message.m_cbSize>600000||!SteamSession.Instance.IsMember(sender)||(!server&&sender!=RemoteHost))continue;
                    var bytes=new byte[message.m_cbSize];Marshal.Copy(message.m_pData,bytes,0,bytes.Length);
                    if(BitConverter.ToUInt32(bytes,0)!=Magic||BitConverter.ToUInt64(bytes,4)!=Lobby||BitConverter.ToInt32(bytes,12)!=SessionEpoch)continue;
                    byte type=bytes[16];uint seq=BitConverter.ToUInt32(bytes,17);
                    if(server&&type==1)
                    {
                        if(!peers.ContainsKey(sender)){peers[sender]=Time.unscaledTime;nonces[sender]=Guid.NewGuid().ToByteArray();events.Enqueue((NetworkEvent.Connect,sender,Array.Empty<byte>()));}
                        Packet(sender,2,nonces[sender],true);
                    }
                    else if(!server&&type==2&&bytes.Length==Header+16)
                    {
                        if(!connected){connected=true;manager.NetworkConfig.ConnectionData=new byte[24];Buffer.BlockCopy(BitConverter.GetBytes(SteamSession.Instance.LocalId),0,manager.NetworkConfig.ConnectionData,0,8);Buffer.BlockCopy(bytes,Header,manager.NetworkConfig.ConnectionData,8,16);events.Enqueue((NetworkEvent.Connect,0,Array.Empty<byte>()));}
                        peers[sender]=Time.unscaledTime;
                    }
                    else if(peers.ContainsKey(sender))
                    {
                        peers[sender]=Time.unscaledTime;
                        if(type==4){peers.Remove(sender);events.Enqueue((NetworkEvent.Disconnect,server?sender:0,Array.Empty<byte>()));}
                        else if(type==3||type==6)
                        {
                            if(type==6&&receivedSequence.TryGetValue(sender,out uint last)&&seq<=last)continue;
                            if(type==6)receivedSequence[sender]=seq;
                            var payload=new byte[bytes.Length-Header];Buffer.BlockCopy(bytes,Header,payload,0,payload.Length);events.Enqueue((NetworkEvent.Data,server?sender:0,payload));
                        }
                    }
                }
                finally{SteamNetworkingMessage_t.Release(inbox[i]);}
            }
            if(!server&&!connected&&Time.unscaledTime>=helloAt)Hello();
            if(Time.unscaledTime>=heartbeatAt){heartbeatAt=Time.unscaledTime+1;foreach(ulong peer in peers.Keys.ToArray())Packet(peer,5,Array.Empty<byte>(),false);}
            foreach(var pair in peers.ToArray())if(Time.unscaledTime-pair.Value>12||!SteamSession.Instance.IsMember(pair.Key)){peers.Remove(pair.Key);events.Enqueue((NetworkEvent.Disconnect,server?pair.Key:0,Array.Empty<byte>()));}
            if(!server&&!connected&&Time.unscaledTime-started>15){running=false;events.Enqueue((NetworkEvent.Disconnect,0,Array.Empty<byte>()));}
        }
        public override NetworkEvent PollEvent(out ulong clientId,out ArraySegment<byte> payload,out float receiveTime)
        {receiveTime=Time.realtimeSinceStartup;clientId=0;payload=default;if(events.Count==0)return NetworkEvent.Nothing;var e=events.Dequeue();clientId=e.id;payload=new ArraySegment<byte>(e.data);return e.kind;}
        public override void DisconnectRemoteClient(ulong clientId){Packet(clientId,4,Array.Empty<byte>(),true);peers.Remove(clientId);nonces.Remove(clientId);}
        public override void DisconnectLocalClient(){if(running&&!server)Packet(RemoteHost,4,Array.Empty<byte>(),true);}
        public override ulong GetCurrentRtt(ulong clientId)=>0;
        public override void Shutdown(){if(running)foreach(ulong peer in peers.Keys.ToArray())Packet(peer,4,Array.Empty<byte>(),true);running=false;Reset();}
    }
}
