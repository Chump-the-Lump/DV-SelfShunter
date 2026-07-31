using DV;
using DV.Booklets;
using DV.Localization;
using DV.Logic.Job;
using DV.RenderTextureSystem.BookletRender;
using DV.ThingTypes;
using DV.ThingTypes.TransitionHelpers;
using DV.Utils;
using HarmonyLib;
using UnityEngine;
using Random = System.Random;
using Task = DV.Logic.Job.Task;

namespace SelfShunt;

[HarmonyPatch]
public static class SelfShunt
{
    private static HashSet<string> DisabledStations = new HashSet<string>();
    
    [HarmonyPatch(typeof(StationProceduralJobsController), nameof(StationProceduralJobsController.TryToGenerateJobs))]
    [HarmonyPrefix]
    public static bool TryToGenerateJobs_Prefix(StationProceduralJobsController __instance)
    {
        if(CarSpawner.Instance.AllCars.Count<SSCarSpawner.CAR_SPAWN_GOAL)SSCarSpawner.PopulateMapWithCars();
        return false;
    }

    [HarmonyPatch(typeof(StationProceduralJobsRuleset), "Awake")]
    [HarmonyPrefix]
    private static void Awake_Patch(StationProceduralJobsRuleset __instance)
    {
        __instance.jobsCapacity = 100;
    }

    [HarmonyPatch(typeof(StationJobGenerationRange), nameof(StationJobGenerationRange.IsPlayerInJobGenerationZone))]
    [HarmonyPostfix]
    private static void OnStationLoad(StationJobGenerationRange __instance, ref bool __result)
    {
        SSCarSpawner.CheckForOptimizableCars();
        if (!MultiplayerShim.IsHost) return;
        if (__result) UpdateJobSpawns(__instance.GetComponent<StationController>());
    }

    private static void UpdateJobSpawns(StationController stationController)
    {
        Station station = stationController.logicStation;
        if(DisabledStations.Contains(station.ID))return;
        int trackCount = station.yard.GetAllYardTracks().Count();
        int jobLimit = trackCount + (int)(Math.Sqrt(10 * trackCount)+1);
        if (station.availableJobs.Count < jobLimit)
        {
            CreateDirectJobChain(station);
        }
    }

    private static Random rand = new Random();
    

    public static void CreateDirectJobChain(Station startStation)
    {
        if (!MultiplayerShim.IsHost) return;
        StationController startStationController = StationController.GetStationByYardID(startStation.ID);
        CargoType cargoType = PickCargoAndDestination(startStationController, out WarehouseMachine loadMachine, out StationController endStationController, out WarehouseMachine unloadMachine);
            
        
        if(cargoType == CargoType.None)return;
        Station endStation = endStationController.logicStation;

        float distance = JobPaymentCalculator.GetDistanceBetweenStations(startStationController, endStationController);
        
        
        CargoType_v2 v2Cargo = Globals.G.Types.CargoType_to_v2[cargoType];
        
        List<TrainCarType_v2> posibleCarTypes = Globals.G.Types.CargoToLoadableCarTypes[v2Cargo];
        
        List<Car_data> carData = new List<Car_data>();

        int i = 0;
        int j = -1;
        while (true)
        {
            TrainCarType_v2 shownCar = posibleCarTypes[i % posibleCarTypes.Count];
            if (i % posibleCarTypes.Count == 0) j++;
            TrainCarLivery shownLivery = shownCar.liveries[j % shownCar.liveries.Count];
            Car_data data = new Car_data("?",  shownLivery, false, false, 0f,0f, 0f);
            carData.Add(data);
            if(rand.Next(0,3) == 0) break;
            i++;
        }
        
        //prevent jobs from being to long for there track
        while(true)
        {
            float len = GetAproxJobLength(carData)+10;//10 is just a random safety margin
            if(len<loadMachine.WarehouseTrack.length&&len<unloadMachine.WarehouseTrack.length)break;
            carData.RemoveAt(0);
        }

        float timeScale = UnityEngine.Random.Range(0f, 2f);

        float price = CalculatePayment(v2Cargo, distance, carData.Count);
        
        if (timeScale < 0.5f) timeScale = -1f;
        else price *= 1/(timeScale+0.5f) + 0.5f;
        
        float timeLimit = (int)((600f + (distance/3f)) * timeScale);
        timeLimit *= Globals.G.GameParams.JobBonusTimeLimitModifier;
        
        StationsChainData chainData = new StationsChainData(startStation.ID,endStation.ID);

        JobLicenses licenses = GetLicenses(v2Cargo, carData.Count);
        
        GameObject jobChainGO = new GameObject($"ChainJob[Direct Haul]: {startStationController.logicStation.ID} - {endStationController.logicStation.ID}");
        StaticDirectJobDefinition jobDefinition = jobChainGO.AddComponent<StaticDirectJobDefinition>();

        jobDefinition.displayCars = carData;
        jobDefinition.cargoAmountPerCar = new List<float>();
        jobDefinition.carsToTransport = new List<Car>();
        jobDefinition.loadMachine = loadMachine;
        jobDefinition.unloadMachine = unloadMachine;
        jobDefinition.transportedCargo = cargoType;
        jobDefinition.ForceJobId(JobIDMaker(chainData));
        
        jobDefinition.PopulateBaseJobDefinition(startStation, timeLimit, price, chainData, licenses);
        
        JobChainController controller = new JobChainController(jobChainGO);
        controller.carsForJobChain = new List<Car>();
        controller.AddJobDefinitionToChain(jobDefinition);
        controller.FinalizeSetupAndGenerateFirstJob(false);

    }

    private static float CalculatePayment(CargoType_v2 v2Cargo, float distance, int carCount)
    {
        float distancePriceScale = distance * 0.00005f;

        int randomAdditive = 0;
        
        for(int i = 0; i<carCount; i++)randomAdditive += rand.Next(0, 1000);
        
        float pricePerCargo = ((v2Cargo.fullDamagePrice / 5f) + (v2Cargo.environmentDamagePrice / 2f) + (v2Cargo.massPerUnit / 2f))/5 + v2Cargo.sensitivityPaymentModifier;
        float jobScale = distancePriceScale * carCount;
        
        float finalPayment = randomAdditive + (jobScale * pricePerCargo);

        return finalPayment * Globals.G.GameParams.JobPaymentModifier;

    }

    public static Job MakeDirectJob(List<Car> carsToTransport, StationsChainData chainData, WarehouseMachine unloadMachine, WarehouseMachine loadMachine, CargoType transportedCargo, float timeLimit, float initialWage, string forcedJobId, JobLicenses requiredLicenses, List<Car_data> displayCars, CargoType cargoType)
    {
        List<Task> tasks = new List<Task>();
        WarehouseTask load = new WarehouseTask(carsToTransport, WarehouseTaskType.Loading, loadMachine, transportedCargo, carsToTransport.Count);
        WarehouseTask unload = new WarehouseTask(carsToTransport, WarehouseTaskType.Unloading, unloadMachine, transportedCargo, carsToTransport.Count, (long)timeLimit, true);
        tasks.Add(load);
        tasks.Add(unload);
        
        Job newJob = new Job(tasks, JobType.ComplexTransport, timeLimit, initialWage, chainData, forcedJobId, requiredLicenses);

        Station spawnAt = StationController.GetStationByYardID(chainData.chainOriginYardId).logicStation;
        spawnAt.AddJobToStation(newJob);
        
        return newJob;
    }

    private static CargoType PickCargoAndDestination(StationController startStationController, out WarehouseMachine loadMachine, out StationController endStationController, out WarehouseMachine unloadMachine)
    {
        int i = 0;
        while (i<100)
        {

            List<CargoGroup> cargoTypes = startStationController.proceduralJobsRuleset.outputCargoGroups;

            if (cargoTypes.Count == 0)
            {
                loadMachine = null!;
                endStationController = null!;
                unloadMachine = null!;

                DisabledStations.Add(startStationController.logicStation.ID);
                return CargoType.None;
            }

            int cargoIndex = rand.Next(0, cargoTypes.Count);
            CargoGroup selectedCargoGroup = cargoTypes[cargoIndex];

            int stationIndex = rand.Next(0, selectedCargoGroup.stations.Count);
            endStationController = selectedCargoGroup.stations[stationIndex];


            loadMachine =
                startStationController.logicStation.yard.GetWarehouseMachinesThatSupportCargoTypes(selectedCargoGroup
                    .cargoTypes)[0];
            unloadMachine =
                endStationController.logicStation.yard.GetWarehouseMachinesThatSupportCargoTypes(selectedCargoGroup
                    .cargoTypes)[0];

            CargoType selectedCargo = selectedCargoGroup.cargoTypes[rand.Next(0, selectedCargoGroup.cargoTypes.Count)];

            if (selectedCargo.ToV2().loadableCarTypes.Length != 0)return selectedCargo;

            i++;
        }
        Debug.LogError("No cars exist for any cargo at station "+startStationController.logicStation.ID+"! This should not happen and will break things!");
        loadMachine = null!;
        endStationController = null!;
        unloadMachine = null!;
        return CargoType.None;
    }

    public static JobLicenses GetLicenses(CargoType_v2 v2Cargo, int carCount)
    {
        JobLicenses licenses = JobLicenses.Basic;
        foreach (JobLicenseType_v2 v2 in v2Cargo.requiredJobLicenses)
        {
            licenses += (int)v2.v1;
        }
        if (carCount <= 2) licenses = ((int)JobLicenses.Shunting + licenses);
        else if (carCount > 5) licenses = ((int)JobLicenses.TrainLength2 + licenses);
        else if (carCount > 10) licenses = ((int)JobLicenses.TrainLength1 + licenses);

        return licenses;
    }

    private static string JobIDMaker(StationsChainData data)
    {
        HashSet<string> existingIDs = AccessTools.Field(typeof(IdGenerator) ,"existingJobIds").GetValue(IdGenerator.Instance) as HashSet<string>;
        string newID = "";
        int num = 0;
        do
        {
            newID = $"{data.chainOriginYardId}-{data.chainDestinationYardId}-{num:D2}";
            num++;
        }while(existingIDs.Contains(newID)||StaticDirectJobDefinition.jobDefinitions.ContainsKey(newID));
        return newID;
    }

    private static float GetAproxJobLength(List<Car_data> data)
    {
        float total = 0;
        foreach (Car_data cd in data)
        {
            total += cd.type.prefab.GetComponent<TrainCar>().InterCouplerDistance;
        }

        return total;
    }
}