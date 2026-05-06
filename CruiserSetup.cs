using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using ChatCommandAPI;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using GameNetcodeStuff;

namespace CruiserSetup;

[BepInPlugin(MyPluginInfo.PLUGIN_GUID, MyPluginInfo.PLUGIN_NAME, MyPluginInfo.PLUGIN_VERSION)]
[BepInDependency("baer1.ChatCommandAPI", BepInDependency.DependencyFlags.HardDependency)]
public class CruiserSetup : BaseUnityPlugin
{
    public static CruiserSetup Instance { get; private set; } = null!;
    internal new static ManualLogSource Logger { get; private set; } = null!;
    internal static Harmony? Harmony { get; set; }
    internal static CruiserSetupConfig BoundConfig { get; private set; } = null!;

    private void Awake()
    {
        Logger = base.Logger;
        Instance = this;

        BoundConfig = new CruiserSetupConfig(Config);

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

    public override string[] Commands =>
    [
        "setup"
    ];

    public override string Description => "Places tools onto the Cruiser.";

    public override string[] Syntax =>
        CruiserSetup.BoundConfig == null
            ? ["[preset]"]
            : CruiserSetup.BoundConfig.GetPresetSyntax();

    public override bool Hidden => false;

    public override bool Invoke(string[] args, Dictionary<string, string> kwargs, out string? error)
    {
        error = null;

        try
        {
            string? requestedPreset = null;
            if (args.Length > 0) requestedPreset = string.Join(" ", args);
            
            string usedPreset = SetupManager.SetupCruiser(requestedPreset);

            ChatCommandAPI.ChatCommandAPI.Print($"Cruiser setup complete: {usedPreset}.");
            return true;
        }
        catch (Exception ex)
        {
            error = ex.Message;
            CruiserSetup.Logger.LogWarning($"Cruiser setup failed: {ex}");
            return false;
        }
    }
}

internal static class SetupManager
{
    public static string SetupCruiser(string? requestedPreset = null)
    {
        CruiserSetup.BoundConfig.Reload();

        string presetName = CruiserSetup.BoundConfig.ResolvePresetOrThrow(requestedPreset);

        GameObject cruiser = FindCruiserOrThrow();
        List<DetectedTool> tools = ToolDetector.FindTools();

        foreach (DetectedTool tool in tools)
        {
            string toolName = tool.Item.itemProperties.itemName;

            if (!CruiserSetup.BoundConfig.TryGetLocalPosition(presetName, toolName, out Vector3 localPosition))
            {
                continue;
            }

            CruiserItemMover.MoveToCruiserLocalPosition(
                tool.Item,
                cruiser,
                localPosition
            );
        }

        return presetName;
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
    private const string ShipPath = "Environment/HangarShip";
    private const string CruiserPath = "CompanyCruiser(Clone)";

    internal static readonly HashSet<string> ToolNames =
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
        GameObject? root = GameObject.Find(rootPath);

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

        if (!ToolNames.Contains(item.itemProperties.itemName))
            return false;

        if (item.isHeld)
            return false;

        if (item.isPocketed)
            return false;

        if (item.playerHeldBy != null)
            return false;

        return true;
    }
}

internal static class CruiserItemMover
{
    public static void MoveToCruiserLocalPosition(
        GrabbableObject item,
        GameObject cruiser,
        Vector3 localPosition)
    {
        if (item == null)
            throw new InvalidOperationException("Cannot move item because item is null.");

        if (item.NetworkObject == null)
            throw new InvalidOperationException($"Cannot move {item.name} because it has no NetworkObject.");

        PlayerControllerB? player = (GameNetworkManager.Instance?.localPlayerController) 
            ?? throw new InvalidOperationException("Could not find local player controller.");

        Vector3 adjustedLocalPosition = localPosition + Vector3.up * item.itemProperties.verticalOffset;

        player.PlaceGrabbableObject(
            cruiser.transform,
            adjustedLocalPosition,
            true,
            item
        );

        player.PlaceObjectServerRpc(
            item.NetworkObject,
            cruiser,
            adjustedLocalPosition,
            true
        );
    }
}