using Bolt;
using DV;
using DV.Booklets;
using DV.Booklets.Rendered;
using DV.CabControls.Spec;
using DV.Logic.Job;
using DV.RenderTextureSystem.BookletRender;
using DV.ServicePenalty;
using DV.ThingTypes;
using DV.ThingTypes.TransitionHelpers;
using DV.Utils;
using HarmonyLib;
using Ludiq;
using UnityEngine;
using UnityEngine.Events;
using UnityModManagerNet;
using Object = System.Object;
using Task = DV.Logic.Job.Task;

namespace SelfShunt;

[HarmonyPatch]
public class JobMechanics
{
    
    private static Dictionary<string, StationController> trackToStationController = new Dictionary<string, StationController>();
    public class JobUpdateEvent : UnityEvent<List<Car>, Job>{}
    public static JobUpdateEvent jobUpdateEvent = new JobUpdateEvent();
    
    [HarmonyPatch(typeof(WarehouseTask), nameof(WarehouseTask.UpdateTaskState))]
    [HarmonyPrefix]
    public static bool UpdateTaskState_Prefix(WarehouseTask __instance, ref TaskState __result)
    {
        
        __instance.readyForMachine = true;
        
        List<WarehouseTask>? machineTasks = AccessTools.Field(typeof(WarehouseMachine), "currentTasks").GetValue(__instance.warehouseMachine) as List<WarehouseTask>;
        
        if (machineTasks?.Contains(__instance) == true)
        {
            SetState(__instance,TaskState.InProgress);
        }
        else
        {
            SetState(__instance,TaskState.Done);
        }
        
        __result = __instance.state;
        
        return false;
    }
    
        
    [HarmonyPatch(typeof(JobDebtController), nameof(JobDebtController.RegisterGeneratedJob))]
    [HarmonyPrefix]
    public static bool RegisterGeneratedJob_Prefix(Job job, List<Car> cars)
    {
        return cars?.Count > 0;
    }
    
    [HarmonyPatch(typeof(WarehouseMachineController), "StartLoadSequence")]
    [HarmonyPrefix]
    public static void StartLoadSequence_Prefix(WarehouseMachineController __instance)
    {
        List<WarehouseMachine.WarehouseLoadUnloadDataPerJob> pendingJobsData = __instance.warehouseMachine.GetCurrentLoadUnloadData(WarehouseTaskType.Loading);
        foreach (WarehouseMachine.WarehouseLoadUnloadDataPerJob jobData in pendingJobsData)
        {
            if (jobData?.tasksAvailableToProcess == null || jobData?.tasksAvailableToProcess[0]?.cars?.Count != 0)continue;
            
            StaticDirectJobDefinition.jobDefinitions.TryGetValue(jobData.id, out StaticDirectJobDefinition sjd);
            if(sjd == null)continue;
            List<Car_data> carData = sjd.displayCars;

            if (carData.Count == 0)
            {
                Debug.LogError("Job has no cargo!");
                continue;
            }
            
            List<Car> carsOnTrack = __instance.warehouseMachine.WarehouseTrack.GetCarsFullyOnTrack();
            List<Car> validCars = new List<Car>();
                
            WarehouseTask task = jobData.tasksAvailableToProcess[0];
            
            
            foreach (Car car in carsOnTrack)
            {
                if(validCars.Count>=carData.Count)break;
                
                if(!Globals.G.Types.CargoToLoadableCarTypes[task.cargoType.ToV2()].Contains(car.carType.parentType))continue;
                if(SingletonBehaviour<JobsManager>.Instance.GetJobOfCar(car) != null) continue;
                if(car.LoadedCargoAmount!=0)continue;
                    
                validCars.Add(car);
            }
            //update cars
            if (validCars.Count != carData.Count)continue;
            AddCarsToJob(validCars, task.Job);
        }
    }

    public static void AddCarsToJob(List<Car> validCars, Job job)
    {
        if(MultiplayerShim.IsHost)jobUpdateEvent.Invoke(validCars, job);
        foreach (Task t in job.tasks)if (t is WarehouseTask warehouseTask)
        {
            warehouseTask.cars.Clear();
                
            float totalCargoSpace = 0f;
            foreach (Car c in validCars)
            {
                if(c.playerSpawnedCar) MakeCarNonPlayerSpawned(c);
                warehouseTask.cars.Add(c);
                totalCargoSpace += c.capacity;
                c.TrainCar().UpdateJobIdOnCarPlates(warehouseTask.Job.ID);
            }
            AccessTools.Field(typeof(WarehouseTask), "cargoAmount").SetValue(warehouseTask, totalCargoSpace);
        }
        (AccessTools.Field(typeof(JobsManager), "jobToJobCars").GetValue(JobsManager.Instance) as Dictionary<Job, HashSet<Car>>)[job] = new HashSet<Car>((IEnumerable<Car>)validCars);
            
        //set debt
        JobDebtController.Instance.RegisterGeneratedJob(job, validCars);
        OnJobTaken(job,false);
            
        BookletMaker.UpdateBook(job);
            
        //Prevent car softlock and ensure cars are cleaned out
        job.JobAbandoned += new Action<Job>(RemoveAllCargo);
        job.JobCompleted += new Action<Job>(RemoveAllCargo);
    }
    
    private delegate void OnJobTakenDelegate(DV.Logic.Job.Job takenJob, bool jobLoadedFromSavegame);
    private static void OnJobTaken(DV.Logic.Job.Job takenJob, bool jobLoadedFromSavegame)
    {
        AccessTools.MethodDelegate<OnJobTakenDelegate>(AccessTools.Method(typeof(JobDebtController), "OnJobTaken"), JobDebtController.Instance)(takenJob, jobLoadedFromSavegame); //how dose this know what instance to use
    }

    
    private static void SetState(WarehouseTask task, TaskState newState)
    {
        if (task.state == newState)
            return;

        float finishTime = 0.0f;
        switch (newState)
        {
            case TaskState.Done:
                finishTime = SingletonBehaviour<JobsManager>.Instance.Time;
                break;
            case TaskState.InProgress:
                finishTime = 0.0f;
                break;
            
        }
        
        AccessTools.Field(typeof(WarehouseTask), "taskFinishTime").SetValue(task, finishTime);
        task.state = newState;
    }

    private static void MakeCarNonPlayerSpawned(Car car)
    {
        AccessTools.Field(typeof(Car), "playerSpawnedCar").SetValue(car,false);
        AccessTools.Field(typeof(TrainCar), "playerSpawnedCar").SetValue(car.TrainCar(),false);
        
        car.TrainCar().GetComponent<CarDebtController>().SetDebtTracker(car.TrainCar().CarDamage,car.TrainCar().CargoDamage);
    }

    private static void RemoveAllCargo(Job job)
    {
        if(!(job.tasks[0] is WarehouseTask task))return;
        
        foreach (Car c in task.cars)
        {
            if(c.LoadedCargoAmount > 0)c.UnloadCargo(c.LoadedCargoAmount,c.CurrentCargoTypeInCar);
            c.TrainCar().UpdateJobIdOnCarPlates("");
        }
    }


    /*
    private static List<Job> deleteList = new List<Job>();
    [HarmonyPatch(typeof(Job), nameof(Job.ExpireJob))]
    [HarmonyPrefix]
    public static bool ExpireJob_Patch(Job __instance)
    {
        if (__instance.jobType != JobType.ComplexTransport) return true;
        if (deleteList.Contains(__instance))
        {
            deleteList.Remove(__instance);
            return true;
        }

        //bullshit to stop PJ from snapping my fucking jobs
        if (__instance.tasks[0] is WarehouseTask task)
        {
            if (IsTrackLoaded(task.warehouseMachine.WarehouseTrack))
            {
                if (trackToStationController.TryGetValue(task.warehouseMachine.WarehouseTrack.ID.FullID,
                        out StationController carStation))
                {
                    CheckJobExistsLater(__instance, carStation );
                    return false;
                }
            }
        }

        return false;

        static async void CheckJobExistsLater(Job job, StationController carStation)
        {
            await System.Threading.Tasks.Task.Delay(TimeSpan.FromSeconds(1));
            if(job.State!= JobState.Available)return;
            deleteList.Add(job);
            job.ExpireJob();
        }
    }*/

    [HarmonyPatch(typeof(UnusedTrainCarDeleter), "AreDeleteConditionsFulfilled")]
    [HarmonyPrefix]
    public static bool AreDeleteConditionsFulfilled_Prefix(TrainCar trainCar, ref bool __result)
    {
        __result = false;

        return false;
    }

    static void PopulateTracks()
    {
        foreach (StationController station in StationController.allStations)
        {
            foreach (RailTrack track in station.AllStationTracks)
            {
                trackToStationController.Add(track.LogicTrack().ID.FullID,station);
            }
        }
    }

    public static bool IsTrackLoaded(Track track)
    {
        if (trackToStationController.Count == 0)PopulateTracks();
        
        if(track?.ID?.FullID == null)return false;
        bool? playerInStation = false;
        if(trackToStationController.TryGetValue(track.ID.FullID, out StationController carStation)) playerInStation = AccessTools.Field(typeof(StationController), "playerEnteredJobGenerationZone").GetValue(carStation) as bool?;
        return (playerInStation == true);
    }

    private static bool DoseOverviewExist(Job job, StationController station)
    {
        List<JobOverview>? jobOverviews = AccessTools.Field(typeof(StationController), "spawnedJobOverviews").GetValue(station) as List<JobOverview>;
        if(!(jobOverviews?.Count > 0)) return false;
        foreach (JobOverview overview in jobOverviews)
        {
            if (overview.job == job)
            {
                ItemDisabler itemDisabler = overview.GetComponent<DV.CabControls.ItemBase>().itemDisabler;
                bool inTrash = (bool)AccessTools.Field(typeof(ItemDisabler), "inDumpster").GetValue(itemDisabler);
                return !inTrash;
            }
        }

        return false;
    }
}