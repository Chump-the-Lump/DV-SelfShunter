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
using UnityModManagerNet;
using Object = System.Object;
using Task = DV.Logic.Job.Task;

namespace SelfShunt;

[HarmonyPatch]
public class JobMechanics
{
    
    private static Dictionary<string, StationController> trackToStationController = new Dictionary<string, StationController>();
    
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
            
            foreach (Task t in task.Job.tasks)if (t is WarehouseTask warehouseTask)
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
            (AccessTools.Field(typeof(JobsManager), "jobToJobCars").GetValue(JobsManager.Instance) as Dictionary<Job, HashSet<Car>>)[task.Job] = new HashSet<Car>((IEnumerable<Car>)validCars);
            
            //set debt
            JobDebtController.Instance.RegisterGeneratedJob(task.Job, validCars);
            OnJobTaken(task.Job,false);
            
            //fix book
            foreach (JobBooklet book in JobBooklet.allExistingJobBooklets.ToArray())
            {
                if (book.job != task.Job)continue;
                
                PageBook pb = book.GetComponent<PageBook>();
                GameObject tempBook = BookletCreator_Job.Create(new Job_data(task.Job), book.transform.position, book.transform.rotation).gameObject;
                PageBook tempPb = tempBook.GetComponent<PageBook>();
                
                tempPb.PageBookGenerated += () =>
                {
                    pb.pageTextures = tempPb.pageTextures;
                    
                    for (int i = 0; i < tempPb.pages.Count; i++)
                    {
                        Transform newPageTransform = tempPb.pages[i].transform.Find("Paper");
                        
                        UnityEngine.Object.Destroy(pb.pages[i].renderer.material);
                        UnityEngine.Object.Destroy(pb.pages[i].pageMaterial);
                        
                        pb.pages[i].renderer.material = tempPb.pages[i].renderer.material;
                        pb.pages[i].pageMaterial = tempPb.pages[i].pageMaterial;

                        tempPb.pages[i].renderer.material = null;
                        tempPb.pages[i].pageMaterial = null;
                        
                        UnityEngine.Object.Destroy(newPageTransform.gameObject);
                    }

                    RenderedTexturesBooklet tempRendTextures = tempBook.GetComponent<RenderedTexturesBooklet>();
                    RenderedTexturesBooklet newRendTextures = book.GetComponent<RenderedTexturesBooklet>();
                    
                    object newTextures = AccessTools.Field(typeof(RenderedTexturesBooklet), "textures").GetValue(tempRendTextures);
                    object oldTextures = AccessTools.Field(typeof(RenderedTexturesBooklet), "textures").GetValue(newRendTextures);
                    AccessTools.Field(typeof(RenderedTexturesBooklet), "textures").SetValue(newRendTextures, newTextures);
                    AccessTools.Field(typeof(RenderedTexturesBooklet), "textures").SetValue(tempRendTextures, oldTextures);
                    
                    UnityEngine.Object.Destroy(pb.coverMaterial);
                    pb.coverMaterial = tempPb.coverMaterial;
                    tempPb.coverMaterial = null;
                    
                    
                    //tempBook.transform.SetParent(book.transform);
                    //tempBook.transform.position = new Vector3(tempBook.transform.position.x,tempBook.transform.position.y-2,tempBook.transform.position.z);
                    UnityEngine.Object.Destroy(tempBook);
                };
                

            }
            
            //Prevent car softlock and ensure cars are cleaned out
            task.Job.JobAbandoned += new Action<Job>(RemoveAllCargo);
            task.Job.JobCompleted += new Action<Job>(RemoveAllCargo);

        }
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
    }

    [HarmonyPatch(typeof(UnusedTrainCarDeleter), "AreDeleteConditionsFulfilled")]
    [HarmonyPrefix]
    public static bool AreDeleteConditionsFulfilled_Prefix(TrainCar trainCar, ref bool __result)
    {
        __result = false;

        return !IsTrackLoaded(trainCar.logicCar.CurrentTrack);
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

    private static bool IsTrackLoaded(Track track)
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