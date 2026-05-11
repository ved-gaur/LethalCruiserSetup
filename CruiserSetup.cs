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

            if (args.Length > 0)
                requestedPreset = string.Join(" ", args);

            SetupResult result = SetupManager.SetupCruiser(requestedPreset);

            ChatCommandAPI.ChatCommandAPI.Print(
                $"Cruiser setup complete: {result.PresetName}. Moved {result.MovedCount} item(s)."
            );

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

internal readonly struct SetupResult(string presetName, int movedCount)
{
    public string PresetName { get; } = presetName;
    public int MovedCount { get; } = movedCount;
}

internal static class SetupManager
{
    public static SetupResult SetupCruiser(string? requestedPreset = null)
    {
        CruiserSetup.BoundConfig.Reload();

        string presetName = CruiserSetup.BoundConfig.ResolvePresetOrThrow(requestedPreset);

        GameObject cruiser = FindCruiserOrThrow();
        EnsureCruiserIsMagnetized(cruiser);

        List<DetectedTool> tools = ToolDetector.FindTools();

        int movedCount = 0;

        if (CruiserSetup.BoundConfig.TryGetDiscardPileLocalPosition(out Vector3 discardPileLocalPosition))
        {
            foreach (DetectedTool tool in tools.Where(tool =>
                tool.Location == ToolLocation.Cruiser &&
                !tool.IsUsable))
            {
                MoveToCruiserLocalPosition(
                    tool.Item,
                    cruiser,
                    discardPileLocalPosition
                );

                movedCount++;
            }
        }

        List<DetectedTool> usableTools =
        [
            .. tools.Where(tool => tool.IsUsable)
                .OrderByDescending(tool => tool.Priority)
        ];

        Dictionary<string, CruiserToolRule> ruleCache = new(StringComparer.OrdinalIgnoreCase);

        foreach (IGrouping<string, DetectedTool> group in usableTools.GroupBy(tool => tool.RuleName))
        {
            string toolName = group.Key;

            if (!TryGetCachedToolRule(presetName, toolName, ruleCache, out CruiserToolRule rule))
                continue;

            List<DetectedTool> cruiserTools = [.. group.Where(tool => tool.Location == ToolLocation.Cruiser)];
            List<DetectedTool> shipTools = [.. group.Where(tool => tool.Location == ToolLocation.Ship)];

            int cruiserCount = cruiserTools.Count;
            int shipCount = shipTools.Count;

            foreach (DetectedTool tool in cruiserTools)
            {
                Vector3 localPosition = GetPlacementPosition(presetName, tool, rule, ruleCache);

                MoveToCruiserLocalPosition(
                    tool.Item,
                    cruiser,
                    localPosition
                );

                movedCount++;
            }

            if (cruiserCount >= rule.MaxOnCruiser)
                continue;

            int cruiserCapacity = rule.IsUnboundedMax
                ? int.MaxValue
                : rule.MaxOnCruiser - cruiserCount;

            int movableFromShip = shipCount - rule.MinOnShip;

            if (movableFromShip <= 0)
                continue;

            int shipMoveCount = rule.IsUnboundedMax
                ? movableFromShip
                : Math.Min(cruiserCapacity, movableFromShip);

            foreach (DetectedTool tool in shipTools.Take(shipMoveCount))
            {
                Vector3 localPosition = GetPlacementPosition(presetName, tool, rule, ruleCache);

                MoveToCruiserLocalPosition(
                    tool.Item,
                    cruiser,
                    localPosition
                );

                movedCount++;
            }
        }

        // Enable radar booster for light source, if requested
        if (CruiserSetup.BoundConfig._shouldEnableBooster.Value)
        {
            DetectedTool? cruiserRadar = ToolDetector.FindTools()
                .FirstOrDefault(tool =>
                    tool.Location == ToolLocation.Cruiser &&
                    tool.Item is RadarBoosterItem);

            if (cruiserRadar?.Item is RadarBoosterItem radarBoosterItem &&
                !radarBoosterItem.isBeingUsed)
            {
                radarBoosterItem.UseItemOnClient();
            }
        }

        return new SetupResult(presetName, movedCount);
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

    private static void EnsureCruiserIsMagnetized(GameObject cruiser)
    {
        if (!cruiser.TryGetComponent(out VehicleController vehicleController) ||
            !vehicleController.magnetedToShip)
        {
            throw new InvalidOperationException(
                "Cruiser is not magnetized."
            );
        }
    }
    
    private static Vector3 GetPlacementPosition(
        string presetName,
        DetectedTool tool,
        CruiserToolRule fallbackRule,
        Dictionary<string, CruiserToolRule> ruleCache)
    {
        if (!tool.PlacementRuleName.Equals(tool.RuleName, StringComparison.OrdinalIgnoreCase) &&
            TryGetCachedToolRule(presetName, tool.PlacementRuleName, ruleCache, out CruiserToolRule placementRule))
        {
            return placementRule.LocalPosition;
        }

        return fallbackRule.LocalPosition;
    }

    private static bool TryGetCachedToolRule(
        string presetName,
        string toolName,
        Dictionary<string, CruiserToolRule> ruleCache,
        out CruiserToolRule rule)
    {
        if (ruleCache.TryGetValue(toolName, out rule))
            return true;

        if (!CruiserSetup.BoundConfig.TryGetToolRule(presetName, toolName, out rule))
            return false;

        ruleCache[toolName] = rule;
        return true;
    }

    private static void MoveToCruiserLocalPosition(
        GrabbableObject item,
        GameObject cruiser,
        Vector3 localPosition)
    {
        if (item == null)
            throw new InvalidOperationException("Cannot move item because item is null.");

        if (item.NetworkObject == null)
            throw new InvalidOperationException($"Cannot move {item.name} because it has no NetworkObject.");

        PlayerControllerB player = GameNetworkManager.Instance?.localPlayerController
            ?? throw new InvalidOperationException("Could not find local player controller.");

        Vector3 adjustedLocalPosition =
            localPosition + Vector3.up * item.itemProperties.verticalOffset;

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