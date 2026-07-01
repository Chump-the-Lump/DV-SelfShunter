using DV.Logic.Job;
using HarmonyLib;
using UnityEngine;

namespace SelfShunt.MP;

[HarmonyPatch]
public class ExtraPatches
{
    [HarmonyPatch(typeof(JobsGenerator), nameof(JobsGenerator.CreateComplexTransportJob))]
    [HarmonyPrefix]
    public static bool CreateComplexTransportJob_Prefix(Station jobOriginStation, List<Car> cars, List<Track> destinationCheckpointTracks, Track startingTrack, float timeLimit, float initialWage, ref Job __result)
    {
        Debug.Log("Job Blocked");
        __result = null;
        return false;
    }
}