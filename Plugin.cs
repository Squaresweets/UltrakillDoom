using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Networking;
using System.Collections;
using System.Linq;
using System.Collections.Generic;
using System;


namespace MyFirstPlugin;

[BepInPlugin(MyPluginInfo.PLUGIN_GUID, MyPluginInfo.PLUGIN_NAME, MyPluginInfo.PLUGIN_VERSION)]
[BepInProcess("ULTRAKILL.exe")]
public class Plugin : BaseUnityPlugin
{
    internal static ManualLogSource Logger;

    private void Awake()
    {
        Logger = base.Logger;
        Logger.LogInfo($"Plugin {MyPluginInfo.PLUGIN_GUID} is loaded!");

        var instance = new Harmony(MyPluginInfo.PLUGIN_GUID);
        instance.PatchAll(typeof(Patches));
    }
}
public static class Patches
{
    [HarmonyPatch(typeof(ShopZone), "Start")]
    [HarmonyPostfix]
    static void Hook(ShopZone __instance)
    {
        Doom d = __instance.gameObject.AddComponent<Doom>();

        d.tickTime = 0.028571f;
        d.wadPath = "./BepInEx/plugins/doom/DOOM.WAD";

        string pwad = "./BepInEx/plugins/doom/ultradoom.wad";
        d.pwadPath = System.IO.File.Exists(pwad) ? pwad : "";

        d.sfPath = "./BepInEx/plugins/doom/RLNDGM.SF2";

        d.SetUp();
    }
}
