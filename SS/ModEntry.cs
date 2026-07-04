using HarmonyLib;
using UnityEngine.SceneManagement;
using UnityModManagerNet;

namespace SelfShunt;

public class Main
{
    public static UnityModManager.ModEntry SelfShuntModEntry;
    public static bool Load(UnityModManager.ModEntry entry)
    {
        SelfShuntModEntry = entry;
        SelfShuntModEntry.OnLateUpdate += InitializeShim;
        
        var harmony = new Harmony(entry.Info.Id);
        harmony.PatchAll();
        
        return true;
    }

    public static void InitializeShim(UnityModManager.ModEntry entry, float f)
    {
        // Remove listener for LateUpdate()
        SelfShuntModEntry.OnLateUpdate -= InitializeShim;

        // Initialise shim
        MultiplayerShim.Initialize(entry);
    }
}
