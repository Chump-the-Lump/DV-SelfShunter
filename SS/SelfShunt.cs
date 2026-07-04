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
    private static readonly Color DIRECT_HAUL_COLOR = new Color(1, 0.5f, 0.2f);
    private const string DIRECT_HAUL_NAME = "Direct Haul";
    
    [HarmonyPatch(typeof(JobChainController), "OnJobGenerated")]
    [HarmonyPostfix]
    public static void OnJobGenerated_Postfix(StaticJobDefinition jobDefinition, DV.Logic.Job.Job generatedJob,
        JobChainController __instance)
    {
        if (generatedJob.jobType == JobType.ComplexTransport) return;
        generatedJob.ExpireJob();
        foreach (Car car in __instance.carsForJobChain)
        {
            if(car.LoadedCargoAmount>0) car.UnloadCargo(car.LoadedCargoAmount, car.CurrentCargoTypeInCar);
        }
    }

    [HarmonyPatch(typeof(StationJobGenerationRange), nameof(StationJobGenerationRange.IsPlayerInJobGenerationZone))]
    [HarmonyPostfix]
    private static void OnStationLoad(StationJobGenerationRange __instance, ref bool __result)
    {
        if (!MultiplayerShim.IsHost) return;
        if (__result) UpdateJobSpawns(__instance.GetComponent<StationController>().logicStation);
    }

    private static void UpdateJobSpawns(Station station)
    {
        //Debug.Log("Station " + station.ID + " has " + station.availableJobs.Count + " jobs for " + station.yard.GetAllYardTracks().Count() + " tracks."+JobSpawningBusy);
        if (station.availableJobs.Count < station.yard.GetAllYardTracks().Count() * 2)
        {
            CreateDirectJobChain(station);

        }
    }
    
    [HarmonyPatch(typeof(BookletCreator_JobMissingLicense), "GetMissingLicenseTemplateData")]
    [HarmonyPrefix]
    public static bool GetJobExpiredTemplateData_Prefix(Job_data job, bool isJobLicenseMissing, ref List<TemplatePaperData> __result)
    {
        if (job.type != JobType.ComplexTransport) return true;
        
    
        string jobType = DIRECT_HAUL_NAME;
        string jobId = job.ID;
        Color jobColor = DIRECT_HAUL_COLOR;
        
        __result = !isJobLicenseMissing ? GetConcurrentJobsMissingLicenseTemplateData() : GetJobMissingLicenseTemplateData();

        return false;
        
        List<TemplatePaperData> GetJobMissingLicenseTemplateData()
        {
          List<MissingLicensesPageTemplatePaperData.LicensePrintData> licensesData = new List<MissingLicensesPageTemplatePaperData.LicensePrintData>();
          DV.ThingTypes.JobLicenses requiredLicenses = job.requiredLicenses;
          LicenseManager instance = SingletonBehaviour<LicenseManager>.Instance;
          HashSet<JobLicenseType_v2> missingLicensesForJob = instance.GetMissingLicensesForJob((IEnumerable<JobLicenseType_v2>) JobLicenseType_v2.ToV2List(requiredLicenses));
          HashSet<JobLicenseType_v2> acquiredLicensesForJob = instance.GetAcquiredLicensesForJob((IEnumerable<JobLicenseType_v2>) JobLicenseType_v2.ToV2List(requiredLicenses));
          foreach (JobLicenseType_v2 jobLicenseTypeV2 in Globals.G.Types.jobLicenses.Where<JobLicenseType_v2>((Func<JobLicenseType_v2, bool>) (l => l.v1 != 0)))
          {
            bool isAcquired = acquiredLicensesForJob.Contains(jobLicenseTypeV2);
            bool flag = missingLicensesForJob.Contains(jobLicenseTypeV2);
            if (isAcquired | flag)
              licensesData.Add(new MissingLicensesPageTemplatePaperData.LicensePrintData(LocalizationAPI.L(jobLicenseTypeV2.localizationKey), jobLicenseTypeV2.icon, isAcquired));
          }
          return new List<TemplatePaperData>()
          {
            (TemplatePaperData) new MissingLicensesPageTemplatePaperData(jobType, "", jobId, jobColor, licensesData)
          };
        }

        List<TemplatePaperData> GetConcurrentJobsMissingLicenseTemplateData()
        {
          bool isAcquired = false;
          GeneralLicenseType_v2 generalLicenseTypeV2 = SingletonBehaviour<LicenseManager>.Instance.GetMissingConcurrentJobsLicense();
          if ((UnityEngine.Object) generalLicenseTypeV2 == (UnityEngine.Object) null)
          {
            Debug.LogError((object) "Printing missing concurrent license, but license is not missing. Something is wrong");
            generalLicenseTypeV2 = GeneralLicenseType.ConcurrentJobs2.ToV2();
            isAcquired = true;
          }
          List<MissingLicensesPageTemplatePaperData.LicensePrintData> licensesData = new List<MissingLicensesPageTemplatePaperData.LicensePrintData>()
          {
            new MissingLicensesPageTemplatePaperData.LicensePrintData(LocalizationAPI.L(generalLicenseTypeV2.localizationKey), generalLicenseTypeV2.icon, isAcquired)
          };
          return new List<TemplatePaperData>()
          {
            (TemplatePaperData) new MissingLicensesPageTemplatePaperData(jobType, "", jobId, jobColor, licensesData)
          };
        }
    }
    

    [HarmonyPatch(typeof(BookletCreator_JobExpiredReport), "GetJobExpiredTemplateData")]
    [HarmonyPrefix]
    public static bool GetJobExpiredTemplateData_Prefix(Job_data job, ref List<TemplatePaperData> __result)
    {
        if (job.type != JobType.ComplexTransport) return true;
        

        __result = new List<TemplatePaperData>()
        {
            (TemplatePaperData) new JobExpiredTemplatePaperData(DIRECT_HAUL_NAME, "", job.ID, DIRECT_HAUL_COLOR)
        };
        
        return false;
    }

    [HarmonyPatch(typeof(BookletCreator_JobOverview), nameof(BookletCreator_JobOverview.GetJobOverviewTemplateData))]
    [HarmonyPrefix]
    public static bool GetJobOverviewTemplateData_Prefix(Job_data job, ref List<TemplatePaperData> __result)
    {
        if (job.type != JobType.ComplexTransport) return true;
        
        List<Car_data> allCars = StaticDirectJobDefinition.jobDefinitions[job.ID].displayCars;
        List<CargoType> cargoTypePerCar = new List<CargoType>();
        foreach (Car_data car in allCars)
        {
            cargoTypePerCar.Add(StaticDirectJobDefinition.jobDefinitions[job.ID].transportedCargo);
        }

        
        string cargoName = LocalizationAPI.L(StaticDirectJobDefinition.jobDefinitions[job.ID].transportedCargo.ToV2().localizationKeyFull);
        
        GetStats(job, allCars.Count, out string timeLimit, out string value, out string mass, out string length);

        TemplatePaperData data = new FrontPageTemplatePaperData(
            DIRECT_HAUL_NAME,
            "",
            job.ID,
            DIRECT_HAUL_COLOR,
            "Transport "+allCars.Count+" loads of " +cargoName,
            job.requiredLicenses,
            cargoTypePerCar.Distinct<CargoType>().ToList(),
            cargoTypePerCar,
            "",
            "",
            TemplatePaperData.NOT_USED_COLOR,
            LocalizationAPI.L(job.chainOriginStationInfo.LocalizationKey),
            job.chainOriginStationInfo.Type,
            job.chainOriginStationInfo.StationColor,
            LocalizationAPI.L(job.chainDestinationStationInfo.LocalizationKey),
            job.chainDestinationStationInfo.Type,
            job.chainDestinationStationInfo.StationColor,
            allCars,
            length,
            mass,
            value,
            timeLimit,
            job.basePayment.ToString("N0", (IFormatProvider) LocalizationAPI.CC),
            "",
            ""
        );

        __result = new List<TemplatePaperData>() { data };
        return false;
    }

    [HarmonyPatch(typeof(BookletCreator_Job), "GetBookletTemplateData")]
    [HarmonyPrefix]
    private static bool GetBookletTemplateData_Prefix(Job_data job, BookletCreator_Job __instance, ref List<TemplatePaperData> __result)
    {
        if (job.type != JobType.ComplexTransport) return true;
        
        CoverPageTemplatePaperData cover = new CoverPageTemplatePaperData(job.ID, "Direct Haul", "1", "5");

        List<Car_data> allCars;
        if (job.tasksData[0].cars.Count == 0) allCars = StaticDirectJobDefinition.jobDefinitions[job.ID].displayCars;
        else allCars = job.tasksData[0].cars;
        
            
        List<CargoType> cargoTypePerCar = new List<CargoType>();
        string cargoName = "";
        
        if(job.tasksData[0].cargoTypePerCar.Count == 0)
        {
            cargoTypePerCar.AddRange(allCars.Select(car => StaticDirectJobDefinition.jobDefinitions[job.ID].transportedCargo));
            cargoName = LocalizationAPI.L(StaticDirectJobDefinition.jobDefinitions[job.ID].transportedCargo.ToV2().localizationKeyFull);
        }
        else
        {
            cargoTypePerCar = job.tasksData[0].cargoTypePerCar;
            cargoName = LocalizationAPI.L(Globals.G.Types.CargoType_to_v2[job.tasksData[0].cargoTypePerCar[0]].localizationKeyFull);
        }

        GetStats(job, allCars.Count, out string timeLimit, out string value, out string mass, out string length);

        FrontPageTemplatePaperData frontPage = new FrontPageTemplatePaperData(
            DIRECT_HAUL_NAME,
            "",
            job.ID,
            DIRECT_HAUL_COLOR,
            "Transport "+allCars.Count+" loads of " +cargoName,
            job.requiredLicenses,
            cargoTypePerCar.Distinct<CargoType>().ToList(),
            cargoTypePerCar,
            "",
            "",
            TemplatePaperData.NOT_USED_COLOR,
            LocalizationAPI.L(job.chainOriginStationInfo.LocalizationKey),
            job.chainOriginStationInfo.Type,
            job.chainOriginStationInfo.StationColor,
            LocalizationAPI.L(job.chainDestinationStationInfo.LocalizationKey),
            job.chainDestinationStationInfo.Type,
            job.chainDestinationStationInfo.StationColor,
            allCars,
            length,
            mass,
            value,
            timeLimit,
            job.basePayment.ToString("N0", (IFormatProvider) LocalizationAPI.CC),
            "2",
            "5"
        );
        
        
        string loadType = LocalizationAPI.L("job/task_type_load");
        string loadDesc = LocalizationAPI.L("job/task_desc_load");
        string loadTrack = job.tasksData[0].destinationTrackID.SignIDSubYardPart + job.tasksData[0].destinationTrackID.SignIDTrackPart;
        TaskTemplatePaperData loadData = new TaskTemplatePaperData("1", loadType, loadDesc, job.chainOriginStationInfo.YardID, job.chainOriginStationInfo.StationColor, loadTrack, C.TRACK_COLOR, "", "", TemplatePaperData.NOT_USED_COLOR, allCars, (List<CargoType>) null, "3", "5");
        
        
        string unloadType = LocalizationAPI.L("job/task_type_unload");
        string unloadDesc = LocalizationAPI.L("job/task_desc_unload");
        string unloadTrack = job.tasksData[1].destinationTrackID.SignIDSubYardPart + job.tasksData[1].destinationTrackID.SignIDTrackPart;
        TaskTemplatePaperData unloadData = new TaskTemplatePaperData("2", unloadType, unloadDesc, job.chainDestinationStationInfo.YardID, job.chainDestinationStationInfo.StationColor, unloadTrack, C.TRACK_COLOR, "", "", TemplatePaperData.NOT_USED_COLOR, allCars, (List<CargoType>) null, "4", "5");
        
        
        ValidateJobTaskTemplatePaperData back = new ValidateJobTaskTemplatePaperData("3", "5", "5");
        
        
        
        List<TemplatePaperData> templatePaperData = new List<TemplatePaperData>();
        templatePaperData.Add(cover);
        templatePaperData.Add(frontPage);
        templatePaperData.Add(loadData);
        templatePaperData.Add(unloadData);
        templatePaperData.Add(back);

        
        __result = templatePaperData;
        return false;
    }

    private static void GetStats(Job_data job, int carCount, out string timeLimit, out string value, out string mass, out string length)
    {
        timeLimit = (double) job.timeLimit > 0.0 ? Mathf.FloorToInt(job.timeLimit / 60f).ToString() + " min" : C.NO_BONUS_TIME_LIMIT_STR;
        value = $"${(StaticDirectJobDefinition.jobDefinitions[job.ID].transportedCargo.ToV2().fullDamagePrice*carCount / 1000f).ToString("N2", (IFormatProvider) LocalizationAPI.CC)}K";
        mass = (StaticDirectJobDefinition.jobDefinitions[job.ID].transportedCargo.ToV2().massPerUnit*carCount * (1f / 1000f)).ToString("N2", (IFormatProvider) LocalizationAPI.CC) + " t";;
        length = carCount+" Cars";
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
        while (true)
        {
            TrainCarType_v2 shownCar = posibleCarTypes[i % posibleCarTypes.Count];
            TrainCarLivery shownLivery = shownCar.liveries[i % shownCar.liveries.Count];
            Car_data data = new Car_data("?",  shownLivery, false, false, 0f,0f, 0f);
            carData.Add(data);
            if(rand.Next(0,3) ==0 || i > 10) break;
            i++;
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
                Debug.LogError("No cargo exists at station "+startStationController.logicStation.ID+"! This should not happen and will break things!");
                loadMachine = null!;
                endStationController = null!;
                unloadMachine = null!;
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

}