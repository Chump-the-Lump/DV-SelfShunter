using DV;
using DV.Booklets;
using DV.Logic.Job;
using DV.ThingTypes;
using DV.ThingTypes.TransitionHelpers;
using HarmonyLib;
using UnityEngine;

namespace SelfShunt.MP;

public static class JobPacketConverter
{
    public static void OnDHOverviewPacket(JobPacketSetup.DHOverviewPacket packet)
    {
        foreach (Job job in AccessTools.Field(typeof(JobsManager), "allJobs").GetValue(JobsManager.Instance) as List<Job>)
        {
            if(job.ID == packet.ID) Debug.Log($"Job {job.ID} exists");
        }
        Debug.Log($"Adding {packet.ID} with {packet.CargoCount} as new job");
        
        if(StaticDirectJobDefinition.jobDefinitions.ContainsKey(packet.ID))return;
        StationController startStationController = StationController.GetStationByYardID(packet.StartStationID);
        StationController endStationController = StationController.GetStationByYardID(packet.EndStationID);
        CargoType cargoType = (CargoType)packet.CargoType;
        cargoType.ToV2();
        Station startStation = startStationController.logicStation;
        Station endStation = endStationController.logicStation;

        WarehouseMachine loadMachine = startStationController.logicStation.yard.GetWarehouseMachinesThatSupportCargoTypes(new List<CargoType>(){cargoType})[0];
        WarehouseMachine unloadMachine = endStationController.logicStation.yard.GetWarehouseMachinesThatSupportCargoTypes(new List<CargoType>(){cargoType})[0];
        
        
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

        float price = packet.Price;
        float timeLimit = packet.TimeLimit;
        
        StationsChainData chainData = new StationsChainData(startStation.ID,endStation.ID);

        JobLicenses licenses = SelfShunt.GetLicenses(v2Cargo, carData.Count);
        
        GameObject jobChainGO = new GameObject($"ChainJob[Direct Haul]: {startStation.ID} - {endStationController.logicStation.ID}");
        StaticDirectJobDefinition jobDefinition = jobChainGO.AddComponent<StaticDirectJobDefinition>();

        jobDefinition.displayCars = carData;
        jobDefinition.cargoAmountPerCar = new List<float>();
        jobDefinition.carsToTransport = new List<Car>();
        jobDefinition.loadMachine = loadMachine;
        jobDefinition.unloadMachine = unloadMachine;
        jobDefinition.transportedCargo = cargoType;
        jobDefinition.ForceJobId(packet.ID);
        
        jobDefinition.PopulateBaseJobDefinition(startStation, timeLimit, price, chainData, licenses);
        
        JobChainController controller = new JobChainController(jobChainGO);
        controller.carsForJobChain = new List<Car>();
        controller.AddJobDefinitionToChain(jobDefinition);
        controller.FinalizeSetupAndGenerateFirstJob(false);
    }

    public static JobPacketSetup.DHOverviewPacket CreateDHOverviewPacket(StaticDirectJobDefinition jobDefinition)
    {
        Debug.Log("Making Packet");
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