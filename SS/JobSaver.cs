using DV;
using DV.Booklets;
using DV.Logic.Job;
using DV.ThingTypes;
using DV.ThingTypes.TransitionHelpers;
using DV.Utils;
using HarmonyLib;
using UnityEngine;

namespace SelfShunt;

[HarmonyPatch]
public static class JobSaver
{
    [HarmonyPatch(typeof(JobSaveManager), nameof(JobSaveManager.LoadJobChain))]
    [HarmonyPrefix]
    private static bool LoadJobChain_Prefix(JobChainSaveData chainSaveData, List<JobBooklet> jobBooklets, ref GameObject __result)
    {
        Debug.Log("Trying to patch job "+chainSaveData.jobChainData[0].GetType().FullName);
        JobDefinitionDataBase definitionDataBase = chainSaveData.jobChainData[0];
        if (definitionDataBase is not DirectJobDefinitionData directJobDefinitionData) return true;
        
        Debug.Log("Loading Job "+chainSaveData.firstJobId);
        GameObject jobChainGO = new GameObject();
        
        StaticDirectJobDefinition staticDirectJobDefinition = jobChainGO.AddComponent<StaticDirectJobDefinition>();
        staticDirectJobDefinition.ForceJobId(chainSaveData.firstJobId);
        
        Station stationWithId = GetStationWithId(directJobDefinitionData.stationId);
        staticDirectJobDefinition.PopulateBaseJobDefinition(stationWithId, directJobDefinitionData.timeLimitForJob, directJobDefinitionData.initialWage, new StationsChainData(directJobDefinitionData.originStationId, directJobDefinitionData.destinationStationId), (DV.ThingTypes.JobLicenses) directJobDefinitionData.requiredLicenses);

        CargoType transportedCargo = directJobDefinitionData.transportedCargo;

        WarehouseMachine loadMachine = GetWarehouseMachineWithId(directJobDefinitionData.startWarehouseId);
        WarehouseMachine unloadMachine = GetWarehouseMachineWithId(directJobDefinitionData.destinationWarehouseId);

        List<Car> carsToTransport = new List<Car>();
        GetCarsFromCarGuids(directJobDefinitionData.transportCarGuids, ref  carsToTransport);

        List<float> cargoAmountPerCar = ((IEnumerable<float>) directJobDefinitionData.cargoAmountPerCar).ToList<float>();
        
        List<Car_data> displayCars = new List<Car_data>(directJobDefinitionData.displayCars.GetGenericCarDataFromTrainCarType());

        staticDirectJobDefinition.transportedCargo = transportedCargo;
        staticDirectJobDefinition.loadMachine = loadMachine;
        staticDirectJobDefinition.unloadMachine = unloadMachine;
        staticDirectJobDefinition.carsToTransport = carsToTransport;
        staticDirectJobDefinition.cargoAmountPerCar = cargoAmountPerCar;
        staticDirectJobDefinition.displayCars = displayCars;
        
        JobChainController jobChainController = new JobChainController(jobChainGO);
        jobChainController.carsForJobChain = carsToTransport;
        
        jobChainController.AddJobDefinitionToChain(staticDirectJobDefinition);
        jobChainGO.name = $"[LOADED] ChainJob[Direct Haul]: {staticDirectJobDefinition.chainData.chainOriginYardId} - {staticDirectJobDefinition.chainData.chainDestinationYardId}";
        jobChainController.FinalizeSetupAndGenerateFirstJob(true);
        if (chainSaveData.jobTaken)
        {
            SingletonBehaviour<JobsManager>.Instance.TakeJob(jobChainController.currentJobInChain, true);
            if (chainSaveData.currentJobTaskData != null)
                jobChainController.currentJobInChain.OverrideTasksStates(chainSaveData.currentJobTaskData);
            else
                Debug.LogError((object) "Job from chain was taken, but there is no task data! Task data won't be loaded!");
            InitializeCorrespondingJobBooklet(jobChainController.currentJobInChain, jobBooklets);
        }
        __result = jobChainController.jobChainGO;
        
        return false;
    }

    [HarmonyPatch(typeof(JobChainController), nameof(JobChainController.AreCarsInitialized))]
    [HarmonyPrefix]
    public static bool AreCarsInitialized_Prefix(JobChainController __instance, ref bool __result)
    {
        if (__instance.currentJobInChain?.jobType != JobType.ComplexTransport) return true;
        __result = __instance.carsForJobChain != null;
        return false;
    }
    
    private static Station GetStationWithId(string stationId)
    {
        return (bool) (UnityEngine.Object) SingletonBehaviour<LogicController>.Instance && SingletonBehaviour<LogicController>.Instance.YardIdToStationController.TryGetValue(stationId, out var stationController) && stationController.logicStation != null ? stationController.logicStation : (Station) null;
    }
    
    private static Track GetYardTrackWithId(string trackId)
    {
        Track track;
        return SingletonBehaviour<YardTracksOrganizer>.Instance.yardTrackIdToTrack.TryGetValue(trackId, out track) && track != null ? track : (Track) null;
    }
    
    private static WarehouseMachine GetWarehouseMachineWithId(string warehouseMachineId)
    {
        List<WarehouseMachineController> allControllers = WarehouseMachineController.allControllers;
        if (allControllers == null || allControllers.Count == 0)
            return (WarehouseMachine) null;
        for (int index = 0; index < allControllers.Count; ++index)
        {
            if (allControllers[index].warehouseMachine.ID == warehouseMachineId)
                return allControllers[index].warehouseMachine;
        }
        return (WarehouseMachine) null;
    }
    
    //[HarmonyPatch(typeof(JobSaveManager), "GetCarsFromCarGuids")]
    //[HarmonyPrefix]
    private static bool GetCarsFromCarGuids(string[] carGuids, ref List<Car> __result)
    {
        __result = new List<Car>();
        if (carGuids == null || carGuids.Length == 0)
        {
            return false;
        }
        List<Car> carsFromCarGuids = new List<Car>();
        for (int index = 0; index < carGuids.Length; ++index)
        {
            Car car;
            if (SingletonBehaviour<IdGenerator>.Instance.carGuidToCar.TryGetValue(carGuids[index], out car) && car != null)
            {
                carsFromCarGuids.Add(car);
            }
            else
            {
                Debug.LogError((object) $"Couldn't find corresponding Car for carGuid:{carGuids[index]}!");
                return false;
            }
        }
        __result = carsFromCarGuids;
        return false;
    }
    
    private delegate void InitializeCorrespondingJobBookletDelegate(Job job, List<JobBooklet> jobBooklets);
    private static void InitializeCorrespondingJobBooklet(Job job, List<JobBooklet> jobBooklets)
    {
        AccessTools.MethodDelegate<InitializeCorrespondingJobBookletDelegate>(AccessTools.Method(typeof(JobSaveManager), "InitializeCorrespondingJobBooklet"), JobSaveManager.Instance)(job, jobBooklets);
    }

    public static TrainCarType[] GetTrainCarTypeFromCarData(this Car_data[] carData)
    {
        TrainCarType[] trainCarTypes = new TrainCarType[carData.Length];
        for (int i = 0; i < carData.Length; i++)
        {
            trainCarTypes[i] = carData[i].type.v1;
        }
        return trainCarTypes;
    }
    
    public static Car_data[] GetGenericCarDataFromTrainCarType(this TrainCarType[] carTypes)
    {
        Car_data[] trainCarData = new Car_data[carTypes.Length];
        for (int i = 0; i < carTypes.Length; i++)
        {
            trainCarData[i] = new Car_data("?", carTypes[i].ToV2(), false, false, 0, 0, 1);
        }
        return trainCarData;
    }
}