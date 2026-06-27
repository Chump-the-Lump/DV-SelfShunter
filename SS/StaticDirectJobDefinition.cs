using DV.Booklets;
using DV.Logic.Job;
using DV.ThingTypes;
using UnityEngine;

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
    
    protected override void GenerateJob(Station jobOriginStation, float timeLimit = 0, float initialWage = 0, string forcedJobId = null,
        JobLicenses requiredLicenses = JobLicenses.Basic)
    {
        job = SelfShunt.MakeDirectJob(carsToTransport, chainData, unloadMachine, loadMachine, transportedCargo, timeLimit, initialWage, forcedJobId, requiredLicenses, displayCars, transportedCargo);
        if (!jobDefinitions.TryAdd(job.ID, this))
        {
            Debug.Log($"Duplicate job with ID {job.ID}");
        }
    }

    public override List<TrackReservation> GetRequiredTrackReservations()
    {
        return new List<TrackReservation>();
    }

    public override JobDefinitionDataBase GetJobDefinitionSaveData()
    {
        Debug.Log("Saving Job "+job.ID);
        return (JobDefinitionDataBase) new DirectJobDefinitionData(this.timeLimitForJob, this.initialWage, this.logicStation.ID, this.chainData.chainOriginYardId, this.chainData.chainDestinationYardId, (int) this.requiredLicenses, StaticJobDefinition.GetGuidsFromCars(this.carsToTransport), transportedCargo, this.cargoAmountPerCar.ToArray(), loadMachine.ID, unloadMachine.ID, displayCars.ToArray().GetTrainCarTypeFromCarData());
    }

    private void OnDestroy()
    {
        jobDefinitions.Remove(job.ID);
    }
}