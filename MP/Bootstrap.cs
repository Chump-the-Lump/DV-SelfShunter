using HarmonyLib;
using UnityEngine;

namespace SelfShunt.MP;

using MPAPI;
using MPAPI.Types;


public static class Bootstrap
{
    public static void Initialize()
    {
        Debug.Log("Bootstrapping SelfShunt.MP.");
        if (!MultiplayerAPI.IsMultiplayerLoaded)
            return;

        // Set compatibility state for your mod
        MultiplayerAPI.Instance.SetModCompatibility(Main.SelfShuntModEntry.Info.Id, MultiplayerCompatibility.All);

        // Apply any extra patches required for multiplayer that can't be applied in the your core mod
        Harmony harmony = new("DoubleTrack.MP");
        harmony.PatchAll();
    }
}