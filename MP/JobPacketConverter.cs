using System.Collections;
using DV;
using DV.Booklets;
using DV.Logic.Job;
using DV.ThingTypes;
using DV.ThingTypes.TransitionHelpers;
using HarmonyLib;
using MPAPI.Interfaces;
using UnityEngine;

namespace SelfShunt.MP;

public static class JobPacketConverter
{
    public static void OnDHOverviewPacket(JobPacketSetup.DHOverviewPacket packet)
    {
        foreach (Job job in AccessTools.Field(typeof(JobsManager), "allJobs").GetValue(JobsManager.Instance) as List<Job>)
        {
            if(job.ID == packet.ID) Debug.Log($"Job {job.ID} exists; skipping!");
            return;
        }
        Debug.Log($"Received {packet.ID} with {packet.CargoCount} as new job");
        
        
        
        GameObject jobChainGO = new GameObject($"ChainJob[Direct Haul]: {packet.StartStationID} - {packet.EndStationID}");
        StaticDirectJobDefinition jobDefinition = jobChainGO.AddComponent<StaticDirectJobDefinition>();
        
        CargoType cargoType = (CargoType)packet.CargoType;
        CargoType_v2 v2Cargo = Globals.G.Types.CargoType_to_v2[cargoType];

        List<TrainCarType_v2> posibleCarTypes = Globals.G.Types.CargoToLoadableCarTypes[v2Cargo];

        List<Car_data> carData = new List<Car_data>();

        for(int i = 0; i < packet.CargoCount; i++)
        {
            TrainCarType_v2 shownCar = posibleCarTypes[i % posibleCarTypes.Count];
            TrainCarLivery shownLivery = shownCar.liveries[i % shownCar.liveries.Count];
            Car_data data = new Car_data("?",  shownLivery, false, false, 0f,0f, 0f);
            carData.Add(data);
        }

        jobDefinition.displayCars = carData;
        jobDefinition.transportedCargo = cargoType;
        StaticDirectJobDefinition.jobDefinitions.Add(packet.ID, jobDefinition);

        if(StationController.allStations?.Count >0)LoadMPJob.PopulateJob(packet);
        else
        {
            GameObject temp = new GameObject();
            temp.AddComponent<JobPacketSetup.DummyComponent>().StartCoroutine(PollIfReady(temp));
        }

        IEnumerator PollIfReady(GameObject GO)
        {
            yield return new WaitUntil(() => StationController.allStations?.Count > 0);

            LoadMPJob.PopulateJob(packet);
            UnityEngine.Object.Destroy(GO);
        }
    }
    
    public static JobPacketSetup.DHOverviewPacket CreateDHOverviewPacket(StaticDirectJobDefinition jobDefinition)
    {
        JobPacketSetup.DHOverviewPacket packet = new JobPacketSetup.DHOverviewPacket();
        packet.StartStationID = jobDefinition.chainData.chainOriginYardId;
        packet.EndStationID = jobDefinition.chainData.chainDestinationYardId;
        packet.CargoCount = jobDefinition.displayCars.Count;
        packet.CargoType = (int)jobDefinition.transportedCargo;
        packet.TimeLimit = jobDefinition.timeLimitForJob;
        packet.Price = jobDefinition.initialWage;
        packet.ID = AccessTools.Field(typeof(StaticDirectJobDefinition), "forcedJobId").GetValue(jobDefinition) as string;

        Debug.Log("Sending "+packet.ID+" cars "+packet.CargoCount);
        return packet;
    }
}