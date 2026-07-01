using DV.Booklets;
using DV.Logic.Job;
using DV.ThingTypes;
using UnityEngine;
using UnityEngine.Events;

namespace SelfShunt;

public class StaticDirectJobDefinition : StaticJobDefinition
{
    public static Dictionary<string, StaticDirectJobDefinition> jobDefinitions = new Dictionary<string, StaticDirectJobDefinition>();
    
    public List<Car> carsToTransport;
    public WarehouseMachine loadMachine;
    public WarehouseMachine unloadMachine;
    public CargoType transportedCargo;
    public List<float> cargoAmountPerCar;
    public List<Car_data> displayCars;

    public class OnJobCreated : UnityEvent<StaticDirectJobDefinition>{}
    public static OnJobCreated onJobCreated = new OnJobCreated();

    protected override void GenerateJob(Station jobOriginStation, float timeLimit = 0, float initialWage = 0, string forcedJobId = null,
        JobLicenses requiredLicenses = JobLicenses.Basic)
    {
        job = SelfShunt.MakeDirectJob(carsToTransport, chainData, unloadMachine, loadMachine, transportedCargo, timeLimit, initialWage, forcedJobId, requiredLicenses, displayCars, transportedCargo);
        if (!jobDefinitions.TryAdd(job.ID, this))
        {
            Debug.LogWarning($"Duplicate job with ID {job.ID}");
        }
        onJobCreated.Invoke(this);
    }

    private void RemoveJobFromList(Job remJob)
    {
        jobDefinitions.Remove(remJob.ID);
    }

    public override List<TrackReservation> GetRequiredTrackReservations()
    {
        return new List<TrackReservation>();
    }

    public override JobDefinitionDataBase GetJobDefinitionSaveData()
    {
        return (JobDefinitionDataBase) new DirectJobDefinitionData(this.timeLimitForJob, this.initialWage, this.logicStation.ID, this.chainData.chainOriginYardId, this.chainData.chainDestinationYardId, (int) this.requiredLicenses, StaticJobDefinition.GetGuidsFromCars(this.carsToTransport), transportedCargo, this.cargoAmountPerCar.ToArray(), loadMachine.ID, unloadMachine.ID, displayCars.ToArray().GetTrainCarTypeFromCarData());
    }

    private void OnDestroy()
    {
        RemoveJobFromList(job);
    }
}