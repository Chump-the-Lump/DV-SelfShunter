using System.Collections;
using System.Numerics;
using MPAPI.Interfaces;
using UnityEngine;

namespace SelfShunt.MP;

public static class JobPacketSetup
{
    public static void InitClient(IClient client)
    {
        if(MPAPI.MultiplayerAPI.Instance.IsHost)return;
        MPAPI.MultiplayerAPI.Client.RegisterPacket<DHOverviewPacket>(JobPacketConverter.OnDHOverviewPacket);
    }

    public static void InitServer(IServer server)
    {
        StaticDirectJobDefinition.onJobCreated.AddListener((jobDef)=>MPAPI.MultiplayerAPI.Server.SendPacketToAll(JobPacketConverter.CreateDHOverviewPacket(jobDef)));
        server.OnPlayerConnected += SendAllJobsToClient;
    }
    
    public static void StopServer()
    {
        StaticDirectJobDefinition.onJobCreated.RemoveListener((jobDef)=>MPAPI.MultiplayerAPI.Server.SendPacketToAll(JobPacketConverter.CreateDHOverviewPacket(jobDef)));
       MPAPI.MultiplayerAPI.Server.OnPlayerConnected -= SendAllJobsToClient;
    }
    
    private static void SendAllJobsToClient(IPlayer client)
    {
        Debug.Log("Sending all jobs to new client "+client.DisplayName);
        foreach (StaticDirectJobDefinition job in StaticDirectJobDefinition.jobDefinitions.Values)
        {
            DHOverviewPacket packet = JobPacketConverter.CreateDHOverviewPacket(job);
            MPAPI.MultiplayerAPI.Server.SendPacketToPlayer(packet, client);
        }
        
        return;
    }

    public class DummyComponent : MonoBehaviour {}


    public class DHOverviewPacket : MPAPI.Interfaces.Packets.IPacket
    {
        public string StartStationID {get; set;}
        public string EndStationID {get; set;}
        public int CargoCount {get; set;}
        public int CargoType {get; set;}
        public float TimeLimit {get; set;}
        public float Price {get; set;}
        public string ID {get; set;}
    }
}