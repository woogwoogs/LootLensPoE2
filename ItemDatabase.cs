using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using ExileCore2.PoEMemory.Components;
using ExileCore2.PoEMemory.MemoryObjects;
using Newtonsoft.Json.Linq;

namespace LootLens2;

internal sealed class ItemDatabase
{
    private readonly Dictionary<string, HashSet<string>> _baseTags =
        new(StringComparer.Ordinal);
    private readonly Dictionary<string, List<ModFamilyData>> _familiesByKey =
        new(StringComparer.Ordinal);
    private readonly Dictionary<string, List<ModFamilyData>> _familiesByTierName =
        new(StringComparer.Ordinal);
    private readonly Dictionary<string, string> _uniqueDropTiers =
        new(StringComparer.OrdinalIgnoreCase);
    private bool _basesLoaded;
    private bool _modsLoaded;

    public static ItemDatabase Empty { get; } = new();
    public bool IsLoaded { get; private set; }
    public int BaseCount => _baseTags.Count;
    public int ModFamilyCount => _familiesByKey.Values
        .SelectMany(families => families).Distinct().Count();
    public int UniqueCount => _uniqueDropTiers.Count;

    public static ItemDatabase Load(string pluginDirectory)
    {
        var database = new ItemDatabase();
        var dataDirectory = Path.Combine(pluginDirectory, "Data");
        TryLoad(() => database.LoadBases(
            Path.Combine(dataDirectory, "item_bases.json")));
        TryLoad(() => database.LoadMods(
            Path.Combine(dataDirectory, "item_mods.json")));
        TryLoad(() => database.LoadDropTiers(
            Path.Combine(dataDirectory, "item_drop_tiers.json")));
        database.IsLoaded = database._basesLoaded && database._modsLoaded;

        return database;

        static void TryLoad(Action load)
        {
            try
            {
                load();
            }
            catch
            {
                // Each file is optional. Failed range data falls back to live
                // records without disabling an independently valid drop list.
            }
        }
    }

    public ModDatabaseMatch? Match(ItemMod itemMod, string baseType,
        int itemLevel, PerfectionRangeMode mode)
    {
        var record = itemMod.ModRecord;
        if (!IsLoaded || record?.StatRange == null || record.StatNames == null ||
            !TryGetBaseTags(baseType, out var tags))
            return null;

        var candidates = new HashSet<ModFamilyData>();
        AddCandidates(_familiesByKey, Normalize(GetMemberString(record, "Group")),
            candidates);
        AddCandidates(_familiesByKey, Normalize(GetMemberString(itemMod, "Group")),
            candidates);
        AddCandidates(_familiesByKey, Normalize(itemMod.Name), candidates);
        AddCandidates(_familiesByKey, Normalize(itemMod.RawName), candidates);
        AddCandidates(_familiesByTierName, Normalize(itemMod.Name), candidates);
        AddCandidates(_familiesByTierName, Normalize(itemMod.RawName), candidates);
        AddCandidates(_familiesByTierName,
            Normalize(GetMemberString(record, "Name", "UserFriendlyName")), candidates);

        foreach (var family in candidates)
        {
            var currentIndex = FindCurrentTier(family, itemMod, tags,
                out var scales, out var liveStatIndices, out var aggregateRange);
            if (currentIndex < 0)
                continue;

            var first = currentIndex;
            while (first > 0 && AllowsBase(family.Tiers[first - 1], tags))
                first--;
            var last = currentIndex;
            while (last + 1 < family.Tiers.Count &&
                   AllowsBase(family.Tiers[last + 1], tags))
                last++;

            var rangeLast = mode switch
            {
                PerfectionRangeMode.CurrentTier => currentIndex,
                PerfectionRangeMode.ItemLevelReachable => FindReachableTier(
                    family, first, last, currentIndex, itemLevel),
                _ => last
            };
            var movementTierBand = mode == PerfectionRangeMode.CurrentTier &&
                                   currentIndex > first &&
                                   Normalize(family.Key) == "movementvelocity";
            var rangeFirst = mode == PerfectionRangeMode.CurrentTier
                ? movementTierBand ? currentIndex - 1 : currentIndex
                : first;
            var minimumTier = family.Tiers[rangeFirst];
            var maximumTier = family.Tiers[rangeLast];
            if (minimumTier.Ranges.Count != scales.Length ||
                maximumTier.Ranges.Count != scales.Length)
                continue;

            var liveRanges = record.StatRange.ToArray();
            var minimum = liveRanges
                .Select(range => (double)Math.Min(range.Min, range.Max)).ToArray();
            var maximum = liveRanges
                .Select(range => (double)Math.Max(range.Min, range.Max)).ToArray();

            if (aggregateRange)
            {
                var start = minimumTier.Ranges[0];
                var end = maximumTier.Ranges[0];
                var aggregateMinimum = Math.Min(
                    Math.Min(start.Minimum, start.Maximum),
                    Math.Min(end.Minimum, end.Maximum)) * scales[0];
                var aggregateMaximum = Math.Max(
                    Math.Max(start.Minimum, start.Maximum),
                    Math.Max(end.Minimum, end.Maximum)) * scales[0];
                return new ModDatabaseMatch(
                    last - currentIndex + 1,
                    last - first + 1,
                    minimum,
                    maximum,
                    true,
                    aggregateMinimum,
                    aggregateMaximum);
            }

            for (var index = 0; index < scales.Length; index++)
            {
                var liveIndex = liveStatIndices[index];
                var start = minimumTier.Ranges[index];
                var end = maximumTier.Ranges[index];
                minimum[liveIndex] = Math.Min(
                    Math.Min(start.Minimum, start.Maximum),
                    Math.Min(end.Minimum, end.Maximum)) * scales[index];
                maximum[liveIndex] = Math.Max(
                    Math.Max(start.Minimum, start.Maximum),
                    Math.Max(end.Minimum, end.Maximum)) * scales[index];
            }

            return new ModDatabaseMatch(
                last - currentIndex + 1,
                last - first + 1,
                minimum,
                maximum);
        }

        return null;
    }

    private bool TryGetBaseTags(string baseType, out HashSet<string> tags)
    {
        var normalized = Normalize(baseType);
        if (_baseTags.TryGetValue(normalized, out var exactTags))
        {
            tags = exactTags;
            return true;
        }

        // ExileCore2 can include a temporary item decorator in the translated
        // base name (for example, "Runeforged Fur Plate") even though the
        // database correctly stores the underlying base as "Fur Plate".
        // Prefer the longest suffix match so similarly named bases remain
        // deterministic and short generic names cannot shadow the real base.
        var suffixMatch = _baseTags.Keys
            .Where(key => key.Length >= 4 && normalized.EndsWith(key,
                StringComparison.Ordinal))
            .OrderByDescending(key => key.Length)
            .FirstOrDefault();
        if (suffixMatch != null &&
            _baseTags.TryGetValue(suffixMatch, out var suffixTags))
        {
            tags = suffixTags;
            return true;
        }

        tags = null!;
        return false;
    }

    public string GetUniqueDropTier(string itemName)
    {
        if (string.IsNullOrWhiteSpace(itemName))
            return "UNKNOWN";

        var clean = Regex.Replace(itemName.Trim(), "^foulborn\\s+", string.Empty,
            RegexOptions.IgnoreCase);
        return _uniqueDropTiers.TryGetValue(clean, out var tier) &&
               !string.IsNullOrWhiteSpace(tier)
            ? tier.ToUpperInvariant()
            : "UNKNOWN";
    }

    private void LoadBases(string path)
    {
        var root = JObject.Parse(File.ReadAllText(path));
        foreach (var itemClass in root.Properties())
        {
            if (itemClass.Value is not JObject bases)
                continue;
            foreach (var baseProperty in bases.Properties())
            {
                if (baseProperty.Value["tags"] is not JArray tagArray)
                    continue;
                var key = Normalize(baseProperty.Name);
                if (!_baseTags.TryGetValue(key, out var tags))
                    _baseTags[key] = tags = new HashSet<string>(
                        StringComparer.OrdinalIgnoreCase);
                foreach (var token in tagArray)
                {
                    var tag = token.Value<string>();
                    if (!string.IsNullOrWhiteSpace(tag))
                        tags.Add(tag);
                }
            }
        }
        _basesLoaded = _baseTags.Count > 0;
    }

    private void LoadMods(string path)
    {
        var root = JObject.Parse(File.ReadAllText(path));
        foreach (var section in root.Properties())
        {
            if (section.Value is not JObject families)
                continue;
            foreach (var familyProperty in families.Properties())
            {
                if (familyProperty.Value is not JArray tiers)
                    continue;
                var family = new ModFamilyData(familyProperty.Name,
                    tiers.OfType<JObject>().Select(ParseTier).Where(x => x != null)
                        .Cast<ModTierData>().ToList());
                if (family.Tiers.Count == 0)
                    continue;

                AddFamily(_familiesByKey, Normalize(family.Key), family);
                foreach (var tierName in family.Tiers.Select(x => Normalize(x.Name))
                             .Where(x => x.Length > 0).Distinct())
                    AddFamily(_familiesByTierName, tierName, family);
            }
        }
        _modsLoaded = _familiesByKey.Count > 0;
    }

    private void LoadDropTiers(string path)
    {
        var root = JObject.Parse(File.ReadAllText(path));
        foreach (var property in root.Properties())
        {
            var value = property.Value.Value<string>();
            if (!string.IsNullOrWhiteSpace(value))
                _uniqueDropTiers[property.Name] = value;
        }
    }

    private static ModTierData? ParseTier(JObject source)
    {
        if (source["text"] is not JArray text)
            return null;
        var ranges = new List<DatabaseRange>();
        foreach (var entry in text.OfType<JArray>())
        {
            if (entry.Count < 2 || entry[1] is not JArray values || values.Count == 0)
                return null;
            if (!TryReadNumber(values[0], out var minimum))
                return null;
            var maximum = minimum;
            if (values.Count > 1 && !TryReadNumber(values[1], out maximum))
                return null;
            ranges.Add(new DatabaseRange(minimum, maximum));
        }

        var weights = new List<WeightRule>();
        if (source["weights"] is JArray weightArray)
        {
            foreach (var entry in weightArray.OfType<JArray>())
            {
                if (entry.Count == 0)
                    continue;
                var tag = entry[0]?.Value<string>() ?? string.Empty;
                weights.Add(new WeightRule(tag, entry.Count > 1 &&
                    TryReadNumber(entry[1], out var weight) && weight > 0));
            }
        }

        return new ModTierData(
            source["level"]?.Value<int?>() ?? 0,
            source["name"]?.Value<string>() ?? string.Empty,
            ranges,
            weights,
            source["weights"] == null);
    }

    private static bool TryReadNumber(JToken? token, out double value)
    {
        value = 0d;
        return token != null && double.TryParse(token.ToString(),
            NumberStyles.Float, CultureInfo.InvariantCulture, out value);
    }

    private static int FindCurrentTier(ModFamilyData family, ItemMod itemMod,
        HashSet<string> tags, out double[] scales, out int[] liveStatIndices,
        out bool aggregateRange)
    {
        scales = [];
        liveStatIndices = [];
        aggregateRange = false;
        var record = itemMod.ModRecord;
        var live = record?.StatRange?.ToArray();
        var statNames = record?.StatNames?.ToArray();
        var values = itemMod.Values?.ToArray();
        if (live == null || statNames == null || values == null)
            return -1;

        var liveCount = Math.Min(live.Length,
            Math.Min(statNames.Length, values.Length));
        var activeIndices = Enumerable.Range(0, liveCount)
            .Where(index => statNames[index] != null && values[index] > -1000)
            .ToArray();
        if (activeIndices.Length == 0)
            return -1;

        for (var tierIndex = 0; tierIndex < family.Tiers.Count; tierIndex++)
        {
            var tier = family.Tiers[tierIndex];
            if (!AllowsBase(tier, tags))
                continue;

            if (tier.Ranges.Count == 1 && activeIndices.Length > 1)
            {
                var liveMinimum = activeIndices.Sum(index =>
                    (double)Math.Min(live[index].Min, live[index].Max));
                var liveMaximum = activeIndices.Sum(index =>
                    (double)Math.Max(live[index].Min, live[index].Max));
                if (TryMatchScale(liveMinimum, liveMaximum, tier.Ranges[0],
                        out var aggregateScale))
                {
                    scales = [aggregateScale];
                    liveStatIndices = activeIndices;
                    aggregateRange = true;
                    return tierIndex;
                }
                continue;
            }

            if (tier.Ranges.Count != activeIndices.Length)
                continue;

            var candidateScales = new double[activeIndices.Length];
            var matched = true;
            for (var index = 0; index < activeIndices.Length; index++)
            {
                var liveIndex = activeIndices[index];
                if (!TryMatchScale(live[liveIndex].Min, live[liveIndex].Max,
                        tier.Ranges[index], out candidateScales[index]))
                {
                    matched = false;
                    break;
                }
            }

            if (!matched)
                continue;
            scales = candidateScales;
            liveStatIndices = activeIndices;
            return tierIndex;
        }

        return -1;
    }

    private static bool TryMatchScale(double liveA, double liveB,
        DatabaseRange stored, out double scale)
    {
        var liveMin = Math.Min(liveA, liveB);
        var liveMax = Math.Max(liveA, liveB);
        var storedMin = Math.Min(stored.Minimum, stored.Maximum);
        var storedMax = Math.Max(stored.Minimum, stored.Maximum);
        var bestError = double.MaxValue;
        scale = 1d;
        foreach (var candidate in new[] { 1d, 100d, .01d, 10d, 60d, 1d / 60d })
        {
            var error = Math.Abs(liveMin - storedMin * candidate) +
                        Math.Abs(liveMax - storedMax * candidate);
            if (error >= bestError)
                continue;
            bestError = error;
            scale = candidate;
        }

        var tolerance = Math.Max(.02d,
            (Math.Abs(liveMin) + Math.Abs(liveMax)) * .0005d);
        return bestError <= tolerance;
    }

    private static bool AllowsBase(ModTierData tier, HashSet<string> tags)
    {
        if (tier.Unrestricted || tier.Weights.Count == 0)
            return true;
        foreach (var rule in tier.Weights)
        {
            if (rule.Tag.Equals("default", StringComparison.OrdinalIgnoreCase) &&
                tier.Weights.Count != 1)
                continue;
            if (tags.Contains(rule.Tag))
                return rule.Allowed;
        }
        return false;
    }

    private static int FindReachableTier(ModFamilyData family, int first,
        int last, int current, int itemLevel)
    {
        var reachable = current;
        for (var index = first; index <= last; index++)
        {
            if (family.Tiers[index].Level <= itemLevel)
                reachable = Math.Max(reachable, index);
        }
        return reachable;
    }

    private static void AddCandidates(
        Dictionary<string, List<ModFamilyData>> source, string key,
        HashSet<ModFamilyData> target)
    {
        if (key.Length > 0 && source.TryGetValue(key, out var families))
            target.UnionWith(families);
    }

    private static void AddFamily(Dictionary<string, List<ModFamilyData>> target,
        string key, ModFamilyData family)
    {
        if (!target.TryGetValue(key, out var list))
            target[key] = list = [];
        list.Add(family);
    }

    private static string Normalize(string? value) => Regex.Replace(
        (value ?? string.Empty).ToLowerInvariant(), "[^a-z0-9]+", string.Empty);

    private static string GetMemberString(object? value, params string[] names)
    {
        if (value == null)
            return string.Empty;
        foreach (var name in names)
        {
            try
            {
                var type = value.GetType();
                var property = type.GetProperty(name,
                    BindingFlags.Instance | BindingFlags.Public |
                    BindingFlags.NonPublic);
                if (property?.GetValue(value) is { } propertyValue)
                    return propertyValue.ToString() ?? string.Empty;
            }
            catch
            {
            }
        }
        return string.Empty;
    }

    private sealed record ModFamilyData(string Key, List<ModTierData> Tiers);
    private sealed record ModTierData(int Level, string Name,
        List<DatabaseRange> Ranges, List<WeightRule> Weights,
        bool Unrestricted);
    private readonly record struct DatabaseRange(double Minimum, double Maximum);
    private readonly record struct WeightRule(string Tag, bool Allowed);
}

internal sealed record ModDatabaseMatch(int Tier, int TotalTiers,
    double[] Minimum, double[] Maximum, bool UsesAggregateRange = false,
    double AggregateMinimum = 0d, double AggregateMaximum = 0d);
