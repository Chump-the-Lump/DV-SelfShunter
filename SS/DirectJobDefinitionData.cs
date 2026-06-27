using DV.Booklets;
using DV.ThingTypes;

namespace SelfShunt;


public class DirectJobDefinitionData : JobDefinitionDataBase
{
    public string[] transportCarGuids;
    public string startWarehouseId;
    public string destinationWarehouseId;
    public CargoType transportedCargo;
    public float[] cargoAmountPerCar;
    public TrainCarType[] displayCars;

    public DirectJobDefinitionData(
        float timeLimitForJob,
        float initialWage,
        string stationId,
        string originStationId,
        string destinationStationId,
        int requiredLicenses,
        string[] transportCarGuids,
        CargoType transportedCargo,
        float[] cargoAmountPerCar,
        string startWarehouseId,
        string destinationWarehouseId,
        TrainCarType[] displayCars)
        : base(timeLimitForJob, initialWage, stationId, originStationId, destinationStationId, requiredLicenses)
    {
        this.transportCarGuids = transportCarGuids;
        this.transportedCargo = transportedCargo;
        this.cargoAmountPerCar = cargoAmountPerCar;
        this.startWarehouseId = startWarehouseId;
        this.destinationWarehouseId = destinationWarehouseId;
        this.displayCars = displayCars;
    }
}