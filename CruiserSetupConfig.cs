using BepInEx.Configuration;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using UnityEngine;

namespace CruiserSetup;

internal sealed class CruiserSetupConfig
{
    private const string GeneralSection = "General";
    private const string DefaultPresetKey = "DefaultPreset";
    private const string PresetPrefix = "Preset.";
    private const string DefaultPresetName = "In";
    private const string DisabledValue = "disabled";

    private readonly ConfigFile _config;

    private readonly Dictionary<string, Dictionary<string, ConfigEntry<string>>> _presetToolSlots =
        new(StringComparer.OrdinalIgnoreCase);

    private ConfigEntry<string> _defaultPreset = null!;

    private static readonly Dictionary<string, string> DefaultInValues =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["Boombox"] = "-0.5,-0.20,0.40",
            ["Extension ladder"] = "0.5,-0.20,0.40",

            ["Pro-flashlight"] = "-1.00,1.15,-0.60",
            ["Kitchen knife"] = "-1.00,1.15,-1.60",
            ["Weed killer"] = "-1.00,1.15,-2.10",
            ["Shotgun"] = "-1.00,1.15,-2.55",

            ["Walkie-talkie"] = "1.00,1.15,-0.60",
            ["Flashlight"] = "disabled",
            ["Shovel"] = "1.00,1.15,-2.30",
            ["Zap gun"] = "disabled",
            ["Radar-booster"] = "disabled",

            ["Lockpicker"] = "-1.00,0.35,-1.00",
            ["Spray paint"] = "-1.00,0.35,-1.60",
            ["Stun grenade"] = "-1.00,0.35,-2.10",
            ["TZP-Inhalant"] = "-1.00,0.35,-2.40",

            ["Jetpack"] = "1.00,0.35,-1.20"
        };

    public CruiserSetupConfig(ConfigFile config)
    {
        _config = config;

        config.SaveOnConfigSet = false;

        _defaultPreset = config.Bind(
            GeneralSection,
            DefaultPresetKey,
            DefaultPresetName,
            "The preset used when /setup is run without a preset name."
        );

        EnsureDefaultPresetValue();

        RefreshPresetBindings();

        config.Save();
        config.SaveOnConfigSet = true;
    }

    public void Reload()
    {
        _config.Reload();

        EnsureDefaultPresetValue();

        RefreshPresetBindings();
    }

    public string[] GetPresetSyntax()
    {
        RefreshPresetBindings();

        List<string> syntax =
        [
            "", .. _presetToolSlots.Keys.OrderBy(name => name)
        ];

        return [.. syntax];
    }

    public string ResolvePresetOrThrow(string? requestedPreset)
    {
        RefreshPresetBindings();

        string presetName = string.IsNullOrWhiteSpace(requestedPreset)
            ? _defaultPreset.Value.Trim()
            : requestedPreset.Trim();

        if (string.IsNullOrWhiteSpace(presetName))
        {
            throw new InvalidOperationException(
                "No preset specified and General.DefaultPreset is empty."
            );
        }

        if (!_presetToolSlots.ContainsKey(presetName))
        {
            string knownPresets = string.Join(", ", _presetToolSlots.Keys.OrderBy(name => name));

            throw new InvalidOperationException(
                $"Unknown preset '{presetName}'. Known presets: {knownPresets}"
            );
        }

        return presetName;
    }

    public bool TryGetLocalPosition(string presetName, string itemName, out Vector3 localPosition)
    {
        localPosition = default;

        if (!_presetToolSlots.TryGetValue(presetName, out Dictionary<string, ConfigEntry<string>> presetEntries))
            return false;

        if (!presetEntries.TryGetValue(itemName, out ConfigEntry<string> entry))
            return false;

        string raw = entry.Value.Trim();

        if (string.IsNullOrWhiteSpace(raw))
            return false;

        if (IsDisabled(raw))
            return false;

        if (!TryParseLocalPosition(raw, out localPosition, out string parseError))
        {
            CruiserSetup.Logger.LogWarning(
                $"Invalid config value for preset '{presetName}', item '{itemName}': '{raw}'. {parseError}"
            );

            return false;
        }

        return true;
    }

    private void EnsureDefaultPresetValue()
    {
        if (string.IsNullOrWhiteSpace(_defaultPreset.Value))
        {
            _defaultPreset.Value = DefaultPresetName;
        }
    }

    private void RefreshPresetBindings()
    {
        HashSet<string> presetNames = ReadPresetNamesFromConfigFile();

        presetNames.Add(DefaultPresetName);

        string defaultPreset = _defaultPreset.Value.Trim();
        if (!string.IsNullOrWhiteSpace(defaultPreset))
        {
            presetNames.Add(defaultPreset);
        }

        foreach (string cachedPreset in _presetToolSlots.Keys.ToList())
        {
            if (!presetNames.Contains(cachedPreset))
            {
                _presetToolSlots.Remove(cachedPreset);
            }
        }

        foreach (string presetName in presetNames)
        {
            bool useInDefaults = presetName.Equals(DefaultPresetName, StringComparison.OrdinalIgnoreCase);
            BindPreset(presetName, useInDefaults);
        }
    }

    private HashSet<string> ReadPresetNamesFromConfigFile()
    {
        HashSet<string> presetNames = new(StringComparer.OrdinalIgnoreCase);

        try
        {
            if (!System.IO.File.Exists(_config.ConfigFilePath))
            {
                return presetNames;
            }

            foreach (string line in System.IO.File.ReadAllLines(_config.ConfigFilePath))
            {
                string trimmed = line.Trim();

                if (!trimmed.StartsWith("[", StringComparison.Ordinal) ||
                    !trimmed.EndsWith("]", StringComparison.Ordinal))
                {
                    continue;
                }

                string section = trimmed.Substring(1, trimmed.Length - 2).Trim();

                if (!section.StartsWith(PresetPrefix, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                string presetName = section.Substring(PresetPrefix.Length).Trim();

                if (string.IsNullOrWhiteSpace(presetName))
                {
                    continue;
                }

                presetNames.Add(presetName);
            }
        }
        catch (Exception ex)
        {
            CruiserSetup.Logger.LogWarning($"Failed to scan config presets: {ex.Message}");
        }

        return presetNames;
    }

    private void BindPreset(string presetName, bool useInDefaults)
    {
        if (_presetToolSlots.ContainsKey(presetName))
        {
            return;
        }

        Dictionary<string, ConfigEntry<string>> entries = new(StringComparer.OrdinalIgnoreCase);

        foreach (string itemName in ToolDetector.ToolNames)
        {
            string defaultValue = DisabledValue;

            if (useInDefaults &&
                DefaultInValues.TryGetValue(itemName, out string inDefaultValue))
            {
                defaultValue = inDefaultValue;
            }

            ConfigEntry<string> entry = _config.Bind(
                PresetPrefix + presetName,
                itemName,
                defaultValue,
                $"Cruiser local placement for {itemName}. Format: x,y,z. Example: 0.5,1.0,0.2. Use '{DisabledValue}' to skip this item."
            );

            entries[itemName] = entry;
        }

        _presetToolSlots[presetName] = entries;
    }

    private static bool IsDisabled(string raw)
    {
        return raw.Equals(DisabledValue, StringComparison.OrdinalIgnoreCase);
    }

    private static bool TryParseLocalPosition(string raw, out Vector3 localPosition, out string error)
    {
        localPosition = default;
        error = string.Empty;

        string[] parts = raw.Split(',');

        if (parts.Length != 3)
        {
            error = "Expected exactly 3 comma-separated values: x,y,z";
            return false;
        }

        if (!TryParseFloat(parts[0], out float x) ||
            !TryParseFloat(parts[1], out float y) ||
            !TryParseFloat(parts[2], out float z))
        {
            error = "All values must be valid numbers.";
            return false;
        }

        localPosition = new Vector3(x, y, z);
        return true;
    }

    private static bool TryParseFloat(string value, out float result)
    {
        return float.TryParse(
            value.Trim(),
            NumberStyles.Float,
            CultureInfo.InvariantCulture,
            out result
        );
    }
}