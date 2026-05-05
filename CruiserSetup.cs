using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using ChatCommandAPI;
using System;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;
using UnityEngine.InputSystem.Interactions;

namespace CruiserSetup;

[BepInPlugin(MyPluginInfo.PLUGIN_GUID, MyPluginInfo.PLUGIN_NAME, MyPluginInfo.PLUGIN_VERSION)]
public class CruiserSetup : BaseUnityPlugin
{
    public static CruiserSetup Instance { get; private set; } = null!;
    internal new static ManualLogSource Logger { get; private set; } = null!;
    internal static Harmony? Harmony { get; set; }

    private void Awake()
    {
        Logger = base.Logger;
        Instance = this;

        Patch();

        // Chat Commands
        _ = new SetupCommand();

        Logger.LogInfo($"{MyPluginInfo.PLUGIN_GUID} v{MyPluginInfo.PLUGIN_VERSION} has loaded!");
    }

    internal static void Patch()
    {
        Harmony ??= new Harmony(MyPluginInfo.PLUGIN_GUID);

        Logger.LogDebug("Patching...");
        Harmony.PatchAll();
        Logger.LogDebug("Finished patching!");
    }

    internal static void Unpatch()
    {
        Logger.LogDebug("Unpatching...");
        Harmony?.UnpatchSelf();
        Logger.LogDebug("Finished unpatching!");
    }
}

public class SetupCommand : Command
{
    public override string Name => "SetupCruiser";
    public override string[] Commands => [
        "setup"
    ];
    public override string Description => "Places tools onto the Cruiser";
    public override string[] Syntax => [""];
    public override bool Hidden => false;

    public override bool Invoke(string[] args, Dictionary<string, string> kwargs, out string? error)
    {
        error = null;

        CruiserSetup.Logger.LogDebug("/setup command invoked.");

        try
        {
            SetupManager.SetupCruiser();

            ChatCommandAPI.ChatCommandAPI.Print("Cruiser setup complete.");
            return true;
        }
        catch (Exception ex)
        {
            error = ex.Message;
            // CruiserSetup.Logger.LogWarning($"Cruiser setup failed: {ex}");
            return false;
        }
    }
}

internal static class SetupManager
{
    public static void SetupCruiser()
    {
        GameObject cruiser = FindCruiserOrThrow();

        CruiserSetup.Logger.LogInfo($"Found cruiser: {cruiser.name}");

        // Detect tools.
        List<DetectedTool> tools = ToolDetector.FindTools();
    }

    private static GameObject FindCruiserOrThrow()
    {
        GameObject? cruiser = GameObject.Find("CompanyCruiser(Clone)");

        if (cruiser == null)
        {
            throw new InvalidOperationException(
                "Could not find the cruiser. Is the cruiser spawned?"
            );
        }

        return cruiser;
    }
}

internal enum ToolLocation
{
    Ship,
    Cruiser
}

internal readonly struct DetectedTool(GrabbableObject item, ToolLocation location)
{
    public GrabbableObject Item { get; } = item;
    public ToolLocation Location { get; } = location;
}

internal static class ToolDetector
{
    private const string ShipPath = "/Environment/HangarShip";
    private const string CruiserPath = "CompanyCruiser(Clone)";

    private static readonly HashSet<string> ToolNames =
    [
        "Walkie-talkie",
        "Flashlight",
        "Shovel",
        "Lockpicker",
        "Pro-flashlight",
        "Stun grenade",
        "Boombox",
        "TZP-Inhalant",
        "Zap gun",
        "Jetpack",
        "Extension ladder",
        "Radar-booster",
        "Spray paint",
        "Weed killer",
        "Shotgun",
        "Kitchen knife"
    ];

    public static List<DetectedTool> FindTools()
    {
        List<DetectedTool> tools = [];

        AddToolsFromRoot(tools, ShipPath, ToolLocation.Ship);
        AddToolsFromRoot(tools, CruiserPath, ToolLocation.Cruiser);

        return [.. tools
            .GroupBy(tool => tool.Item)
            .Select(group => group.First())];
    }

    private static void AddToolsFromRoot(
        List<DetectedTool> tools,
        string rootPath,
        ToolLocation location)
    {
        GameObject root = GameObject.Find(rootPath);

        if (root == null)
            return;

        GrabbableObject[] items =
            root.GetComponentsInChildren<GrabbableObject>(includeInactive: true);

        foreach (GrabbableObject item in items)
        {
            if (!IsToolOfInterest(item))
                continue;

            tools.Add(new DetectedTool(item, location));
        }
    }

    private static bool IsToolOfInterest(GrabbableObject item)
    {
        if (item == null || item.itemProperties == null)
            return false;

        return ToolNames.Contains(item.itemProperties.itemName);
    }
}