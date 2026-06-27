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
        var harmony = new Harmony(entry.Info.Id);
        harmony.PatchAll();
        
        MultiplayerShim.Initialize(entry);
        
        return true;
    }
}
