using DV;
using DV.Booklets;
using DV.Localization;
using DV.Logic.Job;
using DV.RenderTextureSystem.BookletRender;
using DV.ThingTypes;
using DV.ThingTypes.TransitionHelpers;
using HarmonyLib;
using UnityEngine;
using Random = System.Random;
using Task = DV.Logic.Job.Task;

namespace SelfShunt;

[HarmonyPatch]
public static class SelfShunt
{
    [HarmonyPatch(typeof(JobChainController), "OnJobGenerated")]
    [HarmonyPostfix]
    public static void OnJobGenerated_Postfix(StaticJobDefinition jobDefinition, DV.Logic.Job.Job generatedJob,
        JobChainController __instance)
    {
        if(generatedJob.jobType ==  JobType.ComplexTransport) return;
        generatedJob.ExpireJob();
        foreach (Car car in __instance.carsForJobChain)
        {
            car.UnloadCargo(car.LoadedCargoAmount, car.CurrentCargoTypeInCar);
        }

        if (jobDefinition.logicStation.availableJobs.Count < jobDefinition.logicStation.yard.GetAllYardTracks().Count()*2) CreateDirectJobChain(jobDefinition.logicStation);
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
            "Direct Haul",
            "",
            job.ID,
            new Color(1, 0.5f, 0.2f),
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
            "Direct Haul",
            "",
            job.ID,
            new Color(1, 0.5f, 0.2f),
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
        StationController startStationController = StationController.GetStationByYardID(startStation.ID);
        CargoType cargoType = PickCargoAndDestination(startStationController, out WarehouseMachine loadMachine, out StationController endStationController, out WarehouseMachine unloadMachine);
            
        if(cargoType == CargoType.None)return;
        Station endStation = endStationController.logicStation;

        float distance = JobPaymentCalculator.GetDistanceBetweenStations(startStationController, endStationController);
        float distancePriceScale = distance * 0.0001f;
        
        float timeScale = UnityEngine.Random.Range(0f, 2f);
        if (timeScale < 0.5f) timeScale = -1f;
        
        float timeLimit = (int)((600f + (distance/3f)) * timeScale);
        
        
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
        
        float price = distancePriceScale*carData.Count*((v2Cargo.fullDamagePrice + v2Cargo.environmentDamagePrice + v2Cargo.massPerUnit + v2Cargo.sensitivityPaymentModifier)/10f);
        
        StationsChainData chainData = new StationsChainData(startStation.ID,endStation.ID);

        JobLicenses licenses = 0;
        
        foreach (JobLicenseType_v2 v2 in v2Cargo.requiredJobLicenses)
        {
            licenses += (int)v2.v1;
        }
        if (carData.Count <= 2) licenses = ((int)JobLicenses.Shunting + licenses);
        else if (carData.Count > 5) licenses = ((int)JobLicenses.TrainLength2 + licenses);
        else if (carData.Count > 10) licenses = ((int)JobLicenses.TrainLength1 + licenses);
        
        GameObject jobChainGO = new GameObject($"ChainJob[Direct Haul]: {startStationController.logicStation.ID} - {endStationController.logicStation.ID}");
        StaticDirectJobDefinition jobDefinition = jobChainGO.AddComponent<StaticDirectJobDefinition>();

        jobDefinition.displayCars = carData;
        jobDefinition.cargoAmountPerCar = new List<float>();
        jobDefinition.carsToTransport = new List<Car>();
        jobDefinition.loadMachine = loadMachine;
        jobDefinition.unloadMachine = unloadMachine;
        jobDefinition.transportedCargo = cargoType;
        
        jobDefinition.PopulateBaseJobDefinition(startStation, timeLimit, price, chainData, licenses);
        
        JobChainController controller = new JobChainController(jobChainGO);
        controller.carsForJobChain = new List<Car>();
        controller.AddJobDefinitionToChain(jobDefinition);
        controller.FinalizeSetupAndGenerateFirstJob(false);

    }
    public static Job MakeDirectJob(List<Car> carsToTransport, StationsChainData chainData, WarehouseMachine unloadMachine, WarehouseMachine loadMachine, CargoType transportedCargo, float timeLimit, float initialWage, string forcedJobId, JobLicenses requiredLicenses, List<Car_data> displayCars, CargoType cargoType)
    {
        List<Task> tasks = new List<Task>();
        WarehouseTask load = new WarehouseTask(carsToTransport, WarehouseTaskType.Loading, loadMachine, transportedCargo, carsToTransport.Count);
        WarehouseTask unload = new WarehouseTask(carsToTransport, WarehouseTaskType.Unloading, unloadMachine, transportedCargo, carsToTransport.Count, (long)timeLimit, true);
        tasks.Add(load);
        tasks.Add(unload);
        
        Job newJob = new Job(tasks, JobType.ComplexTransport, timeLimit, initialWage, chainData, forcedJobId, requiredLicenses);

        StationController.GetStationByYardID(chainData.chainOriginYardId).logicStation.AddJobToStation(newJob);
        
        return newJob;
    }

    private static CargoType PickCargoAndDestination(StationController startStationController, out WarehouseMachine loadMachine, out StationController endStationController, out WarehouseMachine unloadMachine)
    {
            List<CargoGroup> cargoTypes = startStationController.proceduralJobsRuleset.outputCargoGroups;
            if (cargoTypes.Count == 0)
            {
                loadMachine = null!;
                endStationController = null!;
                unloadMachine = null!;
                return CargoType.None;
            }
            
            int cargoIndex = rand.Next(0, cargoTypes.Count);
            CargoGroup selectedCargoGroup = cargoTypes[cargoIndex];

            int stationIndex = rand.Next(0, selectedCargoGroup.stations.Count);
            endStationController = selectedCargoGroup.stations[stationIndex];


            loadMachine = startStationController.logicStation.yard.GetWarehouseMachinesThatSupportCargoTypes(selectedCargoGroup.cargoTypes)[0];
            unloadMachine = endStationController.logicStation.yard.GetWarehouseMachinesThatSupportCargoTypes(selectedCargoGroup.cargoTypes)[0];
            
            CargoType selectedCargo = selectedCargoGroup.cargoTypes[rand.Next(0, selectedCargoGroup.cargoTypes.Count)];
        
            return selectedCargo;
    }

}