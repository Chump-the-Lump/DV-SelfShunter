using System.Numerics;
using MPAPI.Interfaces;

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
        server.OnPlayerReady += SendAllJobsToClient;
    }
    
    private static void SendAllJobsToClient(IPlayer client)
    {
        foreach (StaticDirectJobDefinition job in StaticDirectJobDefinition.jobDefinitions.Values)
        {
            DHOverviewPacket packet = JobPacketConverter.CreateDHOverviewPacket(job);
            MPAPI.MultiplayerAPI.Server.SendPacketToPlayer(packet, client);
        }
    }

    
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