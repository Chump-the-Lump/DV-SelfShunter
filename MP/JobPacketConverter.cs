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

    public static JobPacketSetup.DHJobUpdatePacket CreateDHJobUpdatePacket(List<Car> cars, Job job)
    {
        JobPacketSetup.DHJobUpdatePacket packet = new JobPacketSetup.DHJobUpdatePacket();
        
        packet.JobID = job.ID;
        
        string[] carIDs = new string[cars.Count];
        for (int i = 0; i < carIDs.Length; i++)carIDs[i] = cars[i].ID;
        packet.CarIDs = carIDs;
        
        Debug.Log("Sending update for "+packet.JobID+" with car count of "+packet.CarIDs.Length);
        return packet;
    }
    public static void OnDHJobUpdatePacket(JobPacketSetup.DHJobUpdatePacket packet)
    {
        Debug.Log("Received update for "+packet.JobID+" with car count of "+packet.CarIDs.Length);
        
        Job job = null;
        
        foreach (Job j in JobsManager.Instance.currentJobs)
        {
            if (j.ID == packet.JobID) job = j;
        }

        if (job == null)
        {
            Debug.LogError("Job "+packet.JobID+" not found!");
            return;
        }
            
        List<Car> cars = new List<Car>();
        foreach (string carID in packet.CarIDs)
        {
            foreach (TrainCar gameCar in CarSpawner.Instance.AllCars)
            {
                if (carID == gameCar.ID)
                {
                    cars.Add(gameCar.logicCar);
                    break;
                }
            }
        }
        if (cars.Count != packet.CarIDs.Length)
        {
            Debug.LogError("Could not find all cars for job "+packet.JobID+"!");
            return;
        }
        JobMechanics.AddCarsToJob(cars, job);
    }
}