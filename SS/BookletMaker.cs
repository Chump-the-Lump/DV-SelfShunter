using DV;
using DV.Booklets;
using DV.Booklets.Rendered;
using DV.Localization;
using DV.Logic.Job;
using DV.RenderTextureSystem.BookletRender;
using DV.ThingTypes;
using DV.ThingTypes.TransitionHelpers;
using DV.Utils;
using HarmonyLib;
using UnityEngine;
using UnityEngine.Events;

namespace SelfShunt;

[HarmonyPatch]
public class BookletMaker
{
    private static readonly Color DIRECT_HAUL_COLOR = new Color(1, 0.5f, 0.2f);
    private const string DIRECT_HAUL_NAME = "Direct Haul";
    public static void UpdateBook(Job jobToFix)
    {
        foreach (JobBooklet book in JobBooklet.allExistingJobBooklets.ToArray())
        {
            if (book.job != jobToFix)continue;
                
            PageBook pb = book.GetComponent<PageBook>();
            GameObject tempBook = BookletCreator_Job.Create(new Job_data(jobToFix), book.transform.position, book.transform.rotation).gameObject;
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
                    
                UnityEngine.Object.Destroy(tempBook);
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
}