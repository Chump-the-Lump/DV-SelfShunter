using DV;
using DV.Booklets;
using DV.Logic.Job;
using DV.ThingTypes;
using DV.ThingTypes.TransitionHelpers;
using UnityEngine;

namespace SelfShunt.MP;

public class LoadMPJob
{
    public static void PopulateJob(JobPacketSetup.DHOverviewPacket packet)
    {
        Debug.Log("LoadMPJob");
        if(!StaticDirectJobDefinition.jobDefinitions.ContainsKey(packet.ID))
        {
            Debug.LogError("Failed to populate job "+packet.ID+" as it dose not exist!");
            return;
        }
        StaticDirectJobDefinition jobDefinition = StaticDirectJobDefinition.jobDefinitions[packet.ID];
        
        StationController startStationController = StationController.GetStationByYardID(packet.StartStationID);
        StationController endStationController = StationController.GetStationByYardID(packet.EndStationID);
        CargoType cargoType = jobDefinition.transportedCargo;
        
        Station startStation = startStationController.logicStation;
        Station endStation = endStationController.logicStation;

        WarehouseMachine loadMachine = startStationController.logicStation.yard.GetWarehouseMachinesThatSupportCargoTypes(new List<CargoType>(){cargoType})[0];
        WarehouseMachine unloadMachine = endStationController.logicStation.yard.GetWarehouseMachinesThatSupportCargoTypes(new List<CargoType>(){cargoType})[0];


        CargoType_v2 v2Cargo = Globals.G.Types.CargoType_to_v2[cargoType];

        List<Car_data> carData = jobDefinition.displayCars;

        float price = packet.Price;
        float timeLimit = packet.TimeLimit;

        StationsChainData chainData = new StationsChainData(startStation.ID,endStation.ID);
        JobLicenses licenses = SelfShunt.GetLicenses(v2Cargo, carData.Count);
        
        jobDefinition.displayCars = carData;
        jobDefinition.cargoAmountPerCar = new List<float>();
        jobDefinition.carsToTransport = new List<Car>();
        jobDefinition.loadMachine = loadMachine;
        jobDefinition.unloadMachine = unloadMachine;
        jobDefinition.ForceJobId(packet.ID);

        jobDefinition.PopulateBaseJobDefinition(startStation, timeLimit, price, chainData, licenses);
    }
}