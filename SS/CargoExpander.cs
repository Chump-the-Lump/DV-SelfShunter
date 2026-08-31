using DV;
using DV.ThingTypes;
using DV.UI;
using HarmonyLib;
using UnityEngine;

namespace SelfShunt;

[HarmonyPatch(typeof(MainMenu))]
public class CargoExpander
{
    [HarmonyPatch("LoadGame")]
    [HarmonyPrefix]
    public static void PreGameLoad()
    {
        Debug.Log("[Yard Master] Loading Cargo Expander");
        TrainCarLivery carFlatShort = null;
        TrainCarLivery locoDM1U = null;
        TrainCarLivery carFlat = null;
        TrainCarLivery carFlatMilitary = null;
        
        foreach (TrainCarLivery trainCarLivery in Globals.G.Types.Liveries)
        {
            if (trainCarLivery.prefab.name == "LocoDM1U") locoDM1U = trainCarLivery;
            else if(trainCarLivery.prefab.name == "CarFlatcarShort") carFlatShort = trainCarLivery;
            else if (trainCarLivery.prefab.name == "CarFlatcar") carFlat = trainCarLivery;
            else if(trainCarLivery.prefab.name == "CarFlatcarMilitary") carFlatMilitary = trainCarLivery;
        }

        foreach (CargoType_v2 cargo in Globals.G.Types.cargos)
        {
            if (cargo.IsLoadableOnCarType(carFlat.parentType))
            {
                AddToCargoList(cargo, carFlat.parentType, carFlatMilitary);
            }
            else if (cargo.IsLoadableOnCarType(carFlatShort.parentType))
            {
                AddToCargoList(cargo, carFlatShort.parentType, carFlatMilitary);
                AddToCargoList(cargo, carFlatShort.parentType, carFlat);
            }
        }
        AccessTools.Field(typeof(DVObjectModel), "_carTypeToLoadableCargo").SetValue(Globals.G.Types, null);
    }
    public static void AddToCargoList(CargoType_v2 cargo, TrainCarType_v2 reference, TrainCarLivery car)
    {
        GameObject[] prefabs = cargo.GetCargoPrefabsForCarType(reference);

        CargoType_v2.LoadableInfo newInfo = new CargoType_v2.LoadableInfo(car.parentType, prefabs);
        CargoType_v2.LoadableInfo[] oldInfoArray = cargo.loadableCarTypes;
        CargoType_v2.LoadableInfo[] newInfoArray = oldInfoArray.Concat(new CargoType_v2.LoadableInfo[]{newInfo}).ToArray();
        cargo.loadableCarTypes = newInfoArray;
        
        Debug.Log("\n"+cargo.name);
        foreach (var loadableInfo in cargo.loadableCarTypes)
        {
            Debug.Log(loadableInfo.carType.name);
        }
        AccessTools.Field(typeof(CargoType_v2), "_trainCargoToCargoPrefabs").SetValue(cargo, null);

    }
}