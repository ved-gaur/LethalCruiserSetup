using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace CruiserSetup;

internal enum ToolLocation
{
    Ship,
    Cruiser
}

// usability f.e empty shotgun or empty weed killer is not considered usable
internal readonly struct DetectedTool(
    GrabbableObject item,
    ToolLocation location,
    bool isUsable)
{
    public GrabbableObject Item { get; } = item;
    public ToolLocation Location { get; } = location;
    public bool IsUsable { get; } = isUsable;
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
            .Select(group => group
                .OrderByDescending(tool => tool.Location == ToolLocation.Cruiser)
                .First())];
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

            tools.Add(new DetectedTool(
                item,
                location,
                ToolUsability.IsUsable(item)
            ));
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

internal static class ToolUsability
{
    public static bool IsUsable(GrabbableObject item)
    {
        if (item == null || item.itemProperties == null)
            return false;

        return item.itemProperties.itemName switch
        {
            "Shotgun" => IsUsableShotgun(item),
            "Weed killer" => IsUsableSprayPaintType(item),
            "Spray paint" => IsUsableSprayPaintType(item),
            _ => true
        };
    }

    private static bool IsUsableShotgun(GrabbableObject item)
    {
        if (item is not ShotgunItem shotgun)
            return true;

        return shotgun.shellsLoaded > 0;
    }

    // Spray paint and weed killer
    private static bool IsUsableSprayPaintType(GrabbableObject item)
    {
        if (item is not SprayPaintItem sprayPaintItem)
            return true;

        return sprayPaintItem.sprayCanTank > 0;
    }
}