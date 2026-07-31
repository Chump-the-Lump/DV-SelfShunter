using System.Collections.Generic;
using System.Linq;
using DV;
using DV.ThingTypes;
using DV.ThingTypes.TransitionHelpers;
using HarmonyLib;
using Rewired;
using UnityEngine;
using Random = System.Random;

namespace SelfShunt;

public class SSCarSpawner
{
    public const int CAR_SPAWN_GOAL = 600;
    private static readonly Random _random = new Random();
    public static bool Spawing;
    public static void PopulateMapWithCars()
    {
        Dictionary<RailTrack, List<TrainCarType_v2>> spawnOnRailTracks = new Dictionary<RailTrack, List<TrainCarType_v2>>();
        foreach (StationController station in StationController.allStations)
        {
            List<TrainCarType_v2> carTypes = GetCarTypesForStation(station);
            List<RailTrack> loadTracks = new List<RailTrack>();
            foreach (WarehouseMachineController warehouseMachineController in station.warehouseMachineControllers)
            {
                loadTracks.Add(warehouseMachineController.warehouseTrack);
            }
            
            foreach (RailTrack track in station.AllStationTracks)
            {
                if(!loadTracks.Contains(track)) spawnOnRailTracks.Add(track, carTypes);
            }
        }
        SpawnCarsOnTrack(spawnOnRailTracks, CAR_SPAWN_GOAL-CarSpawner.Instance.AllCars.Count);
        CheckForOptimizableCars();
    }

    private static async void SpawnCarsOnTrack(Dictionary<RailTrack, List<TrainCarType_v2>> railTracks, int remainingGoal)
    {
        Spawing = true;
        
        List<RailTrack> shuffledTracks = railTracks.Keys.OrderBy(_ => _random.Next()).ToList();
        
        foreach (RailTrack track in shuffledTracks)
        {
            await System.Threading.Tasks.Task.Delay(TimeSpan.FromMilliseconds(100));
            List<TrainCarType_v2> carTypes = railTracks[track];
            if (carTypes.Count == 0) return;
            
            if (!track.LogicTrack().IsFree()) continue;

            List<TrainCarLivery> liveriesToSpawn = new List<TrainCarLivery>();

            // Build a short consist/rake of cars for this track
            
            TrainCarType_v2 randomCarType = carTypes[_random.Next(0, carTypes.Count)];
            if (randomCarType.liveries.Count == 0) continue;
            while (liveriesToSpawn.Count < remainingGoal)
            {
                TrainCarLivery randomLivery = randomCarType.liveries[_random.Next(0, randomCarType.liveries.Count)];
                
                // Track capacity check
                float currentLength = CarSpawner.Instance.GetTotalCarLiveriesLength(liveriesToSpawn);
                float nextCarLength = randomLivery.prefab.GetComponent<TrainCar>().InterCouplerDistance;

                if (currentLength + nextCarLength > track.LogicTrack().length - 10)
                {
                    break; // Track is full
                }

                liveriesToSpawn.Add(randomLivery);

                // Chance to end this specific consist early (creates natural train cuts)
                if (_random.Next(0, 20-liveriesToSpawn.Count) == 0) break;
            }

            if (liveriesToSpawn.Count > 0)
            {
                CarSpawner.Instance.SpawnCarTypesOnTrackRandomOrientation(liveriesToSpawn, track, false, true);
                Debug.Log("[Yard Master] Updated car spawns by adding "+liveriesToSpawn.Count+" cars for a total of "+CarSpawner.Instance.AllCars.Count+"/"+CAR_SPAWN_GOAL+" cars in the map");
                remainingGoal -= liveriesToSpawn.Count;
            }
            
            if(CarSpawner.Instance.AllCars.Count >= CAR_SPAWN_GOAL)break;
        }
        Spawing = false;
    }

    private static List<TrainCarType_v2> GetCarTypesForStation(StationController stationController)
    {
        List<TrainCarType_v2> carTypes = new List<TrainCarType_v2>();
        
        if (stationController.warehouseMachineControllers == null) return carTypes;

        foreach (WarehouseMachineController warehouse in stationController.warehouseMachineControllers)
        {
            if (warehouse.supportedCargoTypes == null) continue;

            foreach (CargoType cargo in warehouse.supportedCargoTypes)
            {
                CargoType_v2 cargoV2 = cargo.ToV2();
                if (Globals.G.Types.CargoToLoadableCarTypes.TryGetValue(cargoV2, out List<TrainCarType_v2> loadableCars))
                {
                    carTypes.AddRange(loadableCars);
                }
            }
        }
        return carTypes.Distinct().ToList();
    }

    private static Timer optimizeTimer;

    public static void StartOptimizeTimer()
    {
        optimizedCars.Clear();
        optimizeTimer = new Timer(OnTimerTick, null, 0, 10000);
    }

    private static void OnTimerTick(object state)
    {
        CheckForOptimizableCars();
    }
    
    public static void CheckForOptimizableCars()
    {
        return;
        foreach (Trainset train in Trainset.allSets)
        {
            if(OptimizeConditionsMet(train))OptimizeCars(train);
            else ActivateCars(train);
        }
    }

    private static bool OptimizeConditionsMet(Trainset train)
    {
        if (Spawing) return false;
        if (train.locoIndices.Count != 0) return false;
        if (!train.firstCar.isStationary)return false;
        if (!train.lastCar.isStationary)return false;
        if (JobMechanics.IsTrackLoaded(train.firstCar.logicCar.CurrentTrack)) return false;
        if (JobMechanics.IsTrackLoaded(train.lastCar.logicCar.CurrentTrack)) return false;
        if (Vector3.Distance(PlayerManager.PlayerTransform.position, train.firstCar.transform.position) < 1000) return false;
        if (Vector3.Distance(PlayerManager.PlayerTransform.position, train.lastCar.transform.position) < 1000) return false;
        return true;
    }

    private static HashSet<Trainset> optimizedCars = new HashSet<Trainset>();
    private static void OptimizeCars(Trainset train)
    {   
        if(optimizedCars.Contains(train))return;
        optimizedCars.Add(train);
        foreach (TrainCar car in train.cars)
        {
            car.gameObject.GetComponent<Rigidbody>().isKinematic = true;
            car.TrainCarCollisions.enabled = false;
            if(car.SimController!=null)car.SimController.enabled = false;
            if (car != train.firstCar && car != train.lastCar)
            {
                Transform collision = (Transform)AccessTools.Field(typeof(TrainCarColliders), "collisionRoot").GetValue(car.carColliders);
                collision.gameObject.SetActive(false);
                
                Transform collider = (Transform)AccessTools.Field(typeof(TrainCarColliders), "carColliderRoot").GetValue(car.carColliders);
                collider.gameObject.SetActive(false);
            }
        }
        train.firstCar.TrainCarCollisions.CarDamaged += (float health, Vector3 direction)=>ActivateOnHealthChange(train);
        train.lastCar.TrainCarCollisions.CarDamaged += (float health, Vector3 direction)=>ActivateOnHealthChange(train);
    }

    private static void ActivateOnHealthChange(Trainset train)
    {
        ActivateCars(train);
    }
    
    private static void ActivateCars(Trainset train)
    {
        if(!optimizedCars.Contains(train))return;
        optimizedCars.Remove(train);
        foreach (TrainCar car in train.cars)
        {
            car.gameObject.GetComponent<Rigidbody>().isKinematic = false;
            car.TrainCarCollisions.enabled = true;
            if(car.SimController!=null)car.SimController.enabled = true;
            
            Transform collision = (Transform)AccessTools.Field(typeof(TrainCarColliders), "collisionRoot").GetValue(car.carColliders);
            collision.gameObject.SetActive(true);

            Transform collider = (Transform)AccessTools.Field(typeof(TrainCarColliders), "carColliderRoot").GetValue(car.carColliders);
            collider.gameObject.SetActive(true);
        }
        train.firstCar.TrainCarCollisions.CarDamaged -= (float health, Vector3 direction)=>ActivateOnHealthChange(train);
        train.lastCar.TrainCarCollisions.CarDamaged -= (float health, Vector3 direction)=>ActivateOnHealthChange(train);
    }
}