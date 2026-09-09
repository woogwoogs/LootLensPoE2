using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using ExileCore2;
using ExileCore2.PoEMemory;
using ExileCore2.PoEMemory.Components;
using ExileCore2.PoEMemory.MemoryObjects;
using ExileCore2.Shared.Enums;

namespace LootLens2;

internal sealed class ItemAnalyzer(LootLens2 plugin, ItemDatabase database)
{
    private readonly Dictionary<string, (int Tier, int Total)> _tierCache = new(StringComparer.Ordinal);

    private GameController GameController => plugin.GameController;
    private LootLens2Settings Settings => plugin.Settings;

    public AnalyzedItem? Analyze(Entity? item, Element? tooltip = null)
    {
        if (item == null || !item.IsValid)
            return null;

        var mods = item.GetComponent<Mods>();
        if (mods == null)
            return null;

        var rarity = mods.ItemRarity;
        if ((rarity == ItemRarity.Magic && !Settings.AnalyzeMagicItems.Value) ||
            (rarity == ItemRarity.Rare && !Settings.AnalyzeRareItems.Value) ||
            (rarity == ItemRarity.Unique && !Settings.AnalyzeUniqueItems.Value) ||
            (rarity != ItemRarity.Magic && rarity != ItemRarity.Rare && rarity != ItemRarity.Unique))
            return null;

        var baseType = GetBaseTypeName(item);
        var slotName = GetSlotName(item.Path);
        var result = new AnalyzedItem
        {
            Name = GetItemDisplayName(item, tooltip, baseType),
            BaseType = baseType,
            Rarity = rarity.ToString(),
            ItemLevel = mods.ItemLevel,
            SlotName = slotName,
            ItemClassName = GetItemClassName(item.Path, slotName),
            Quality = item.GetComponent<Quality>()?.ItemQuality ?? 0,
            WeaponDps = CalculateWeaponDps(item, tooltip),
            DefenceTotals = CalculateDisplayedDefences(tooltip)
        };

        if (rarity == ItemRarity.Unique)
            result.UniqueDropTier = database.GetUniqueDropTier(result.Name);

        AnalyzeModifiers(item, mods, result);
        return result;
    }

    public void ClearCaches() => _tierCache.Clear();

    // Count affix records, not their rendered stat lines (hybrids count once).
    public List<int> GetInventoryTiers(Entity item)
    {
        var tiers = new List<int>();
        var mods = item.GetComponent<Mods>();
        if (mods == null || mods.ItemMods == null ||
            (mods.ItemRarity != ItemRarity.Magic && mods.ItemRarity != ItemRarity.Rare))
            return tiers;
        var baseType = GetBaseTypeName(item);
        foreach (var mod in mods.ItemMods)
        {
            var record = mod?.ModRecord;
            if (mod == null || record == null ||
                (record.AffixType != ModType.Prefix && record.AffixType != ModType.Suffix) ||
                IsHiddenInternalMod(mod) || IsCrafted(record, mod) ||
                (mods.ImplicitMods != null && mods.ImplicitMods.Any(x =>
                    x != null && string.Equals(x.RawName, mod.RawName, StringComparison.Ordinal))))
                continue;
            var match = database.Match(mod, baseType, mods.ItemLevel, PerfectionRangeMode.CurrentTier);
            var tier = match?.Tier ?? GetModTier(item, mod).Tier;
            if (tier >= 1 && tier <= 3)
                tiers.Add(tier);
        }
        tiers.Sort();
        return tiers;
    }

    private void AnalyzeModifiers(Entity item, Mods mods, AnalyzedItem result)
    {
        if (mods.ItemMods == null)
            return;

        double weightedPerfection = 0;

        foreach (var itemMod in mods.ItemMods)
        {
            if (itemMod == null)
                continue;

            var record = itemMod.ModRecord;
            if (record == null)
                continue;

            var isImplicit = mods.ImplicitMods != null && mods.ImplicitMods.Any(x =>
                x != null && string.Equals(x.RawName, itemMod.RawName, StringComparison.Ordinal));
            var isHidden = IsHiddenInternalMod(itemMod);
            if (isImplicit && !Settings.ShowImplicitModifiers.Value && !isHidden)
                continue;

            var isUnique = !isImplicit && record.AffixType == ModType.Unique &&
                           mods.ItemRarity == ItemRarity.Unique;
            var isPrefix = record.AffixType == ModType.Prefix;
            var isSuffix = record.AffixType == ModType.Suffix;
            var isCrafted = IsCrafted(record, itemMod);

            if (!isImplicit && !isUnique && !isPrefix && !isSuffix && !isCrafted && !isHidden)
                continue;

            var mod = new AnalyzedMod
            {
                Affix = isHidden ? "H" : isImplicit ? "I" : isUnique ? "U" :
                    isPrefix ? "P" : isSuffix ? "S" : "C",
                IsImplicit = isImplicit,
                IsUnique = isUnique,
                IsCrafted = isCrafted,
                IsHidden = isHidden,
                Text = BuildModText(itemMod),
            };

            ModDatabaseMatch? databaseMatch = null;
            if (!isImplicit && !isUnique && !isCrafted && !isHidden)
            {
                databaseMatch = database.Match(itemMod, result.BaseType,
                    result.ItemLevel, Settings.PerfectionRange);
                if (databaseMatch != null)
                {
                    mod.Tier = databaseMatch.Tier;
                    mod.TotalTiers = databaseMatch.TotalTiers;
                }
                else
                {
                    var tier = GetModTier(item, itemMod);
                    mod.Tier = tier.Tier;
                    mod.TotalTiers = tier.Total;
                }
            }

            (int Percentage, int VariableRolls, string RangeText, bool Fixed) perfection = isHidden
                ? (-1, 0, string.Empty, false)
                : GetRollPerfection(itemMod, databaseMatch);
            mod.Perfection = perfection.Percentage;
            mod.VariableRollCount = perfection.VariableRolls;
            mod.RangeText = perfection.RangeText;
            mod.IsFixed = perfection.Fixed;
            mod.RangeLabel = databaseMatch == null
                ? "L RANGE"
                : Settings.PerfectionRange switch
                {
                    PerfectionRangeMode.AllValidTiers => "G RANGE",
                    PerfectionRangeMode.ItemLevelReachable => "ILVL RANGE",
                    _ => "TIER RANGE"
                };
            result.Mods.Add(mod);

            if (isUnique && perfection.Percentage >= 0 && perfection.VariableRolls > 0)
            {
                weightedPerfection += perfection.Percentage * perfection.VariableRolls;
                result.VariableRollCount += perfection.VariableRolls;
                result.VariableModCount++;
            }
        }

        if (result.VariableRollCount > 0)
        {
            result.OverallPerfection = Math.Clamp((int)Math.Round(
                weightedPerfection / result.VariableRollCount,
                MidpointRounding.AwayFromZero), 0, 100);
        }
    }

    private (int Tier, int Total) GetModTier(Entity item, ItemMod itemMod)
    {
        var record = itemMod.ModRecord;
        if (record == null ||
            (record.AffixType != ModType.Prefix && record.AffixType != ModType.Suffix))
            return (0, 0);

        var cacheKey = $"{item.Path}\u001f{record.Hash32}";
        if (_tierCache.TryGetValue(cacheKey, out var cached))
            return cached;

        try
        {
            var key = Tuple.Create(record.Group, record.AffixType);
            if (!GameController.Files.Mods.recordsByTier.TryGetValue(key, out var family))
                return Cache((0, 0));

            // This mirrors the supplied PoE2 Enhanced ToolTip implementation:
            // constrain to the real mod type and collapse duplicate display rows.
            var tiers = family
                .Where(x => x != null && x.TypeName == record.TypeName)
                .GroupBy(x => x.UserFriendlyName)
                .Select(group => group.Any(x => x.Hash32 == record.Hash32)
                    ? group.First(x => x.Hash32 == record.Hash32)
                    : group.First())
                .ToList();

            var index = tiers.FindIndex(x => x.Hash32 == record.Hash32);
            // recordsByTier is ordered from the earliest/weakest roll upward,
            // while PoE displays the strongest roll as T1.
            return Cache((index >= 0 ? tiers.Count - index : 0, tiers.Count));
        }
        catch
        {
            return Cache((0, 0));
        }

        (int Tier, int Total) Cache((int Tier, int Total) value)
        {
            if (_tierCache.Count > 4096)
                _tierCache.Clear();
            _tierCache[cacheKey] = value;
            return value;
        }
    }

    private static (int Percentage, int VariableRolls, string RangeText, bool Fixed)
        GetRollPerfection(ItemMod itemMod, ModDatabaseMatch? databaseMatch = null)
    {
        var record = itemMod.ModRecord;
        if (record?.StatNames == null || record.StatRange == null || itemMod.Values == null)
            return (-1, 0, string.Empty, false);

        try
        {
            var stats = record.StatNames.ToArray();
            var ranges = record.StatRange.ToArray();
            var values = itemMod.Values.ToArray();
            var count = Math.Min(stats.Length, Math.Min(ranges.Length, values.Length));

            if (databaseMatch?.UsesAggregateRange == true)
            {
                var active = Enumerable.Range(0, count)
                    .Where(index => stats[index] != null && values[index] > -1000)
                    .ToArray();
                if (active.Length == 0)
                    return (-1, 0, string.Empty, false);

                if (databaseMatch.AggregateMinimum == databaseMatch.AggregateMaximum)
                    return (-1, 0, string.Empty, true);

                var value = active.Sum(index => (double)values[index]);
                var position = Math.Clamp(
                    (value - databaseMatch.AggregateMinimum) /
                    (databaseMatch.AggregateMaximum - databaseMatch.AggregateMinimum),
                    0d, 1d);
                var suffix = active.All(index => stats[index]!.Type == StatType.Percents)
                    ? "%"
                    : string.Empty;
                var range = $"POWER {FormatNumber(databaseMatch.AggregateMinimum)}-" +
                            $"{FormatNumber(databaseMatch.AggregateMaximum)}{suffix}";
                return (Math.Clamp((int)Math.Round(position * 100d,
                    MidpointRounding.AwayFromZero), 0, 100), 1, range, false);
            }

            var total = 0d;
            var variable = 0;
            var known = 0;
            var displayRanges = new List<string>();

            for (var i = 0; i < count; i++)
            {
                var stat = stats[i];
                var value = values[i];
                if (stat == null || value <= -1000)
                    continue;

                var minimum = databaseMatch?.Minimum.ElementAtOrDefault(i) ??
                              Math.Min(ranges[i].Min, ranges[i].Max);
                var maximum = databaseMatch?.Maximum.ElementAtOrDefault(i) ??
                              Math.Max(ranges[i].Min, ranges[i].Max);
                known++;
                if (minimum == maximum)
                    continue;

                var position = Math.Clamp(
                    (value - minimum) / (double)(maximum - minimum), 0d, 1d);
                var statKey = GetMemberString(stat, "Key", "Name");
                if (minimum >= 0 && IsLowerRollBetter(statKey, stat.ToString() ?? string.Empty))
                    position = 1d - position;

                total += position * 100d;
                variable++;

                displayRanges.Add(FormatRollRange(
                    statKey, stat.Type, minimum, maximum));
            }

            if (variable == 0)
                return (-1, 0, string.Empty, known > 0);

            return (Math.Clamp((int)Math.Round(total / variable,
                MidpointRounding.AwayFromZero), 0, 100), variable,
                string.Join(" / ", displayRanges), false);
        }
        catch
        {
            return (-1, 0, string.Empty, false);
        }
    }

    private static bool IsLowerRollBetter(string statKey, string statText)
    {
        var text = (statKey + " " + statText).ToLowerInvariant();
        return text.Contains("damage_taken_+%") ||
               text.Contains("increased damage taken") ||
               text.Contains("charges used") ||
               text.Contains("charges_used") ||
               text.Contains("souls per use") ||
               text.Contains("souls_per_use") ||
               text.Contains("requirements_+%") ||
               text.Contains("increased requirements") ||
               text.Contains("skill_cost_+%") ||
               text.Contains("mana_cost_+%") ||
               text.Contains("life_cost_+%") ||
               text.Contains("curse_effect_on_self") ||
               text.Contains("effect of curses on you");
    }

    private static string FormatRollRange(string statKey, StatType statType,
        double minimum, double maximum)
    {
        var key = (statKey ?? string.Empty).ToLowerInvariant();
        var label = key.Contains("minimum_added", StringComparison.Ordinal) ? "MIN " :
            key.Contains("maximum_added", StringComparison.Ordinal) ? "MAX " : string.Empty;

        if (key.Contains("critical_strike_chance", StringComparison.Ordinal))
        {
            return $"{label}{FormatNumber(minimum / 100d)}-" +
                   $"{FormatNumber(maximum / 100d)}%";
        }

        if (key.Contains("life_regeneration_rate_per_minute",
                StringComparison.Ordinal))
        {
            return $"{label}{FormatNumber(minimum / 60d)}-" +
                   $"{FormatNumber(maximum / 60d)}";
        }

        var suffix = statType == StatType.Percents || key.Contains('%') ? "%" : string.Empty;
        return $"{label}{FormatNumber(minimum)}-{FormatNumber(maximum)}{suffix}";
    }

    private static string BuildModText(ItemMod itemMod)
    {
        var record = itemMod.ModRecord;
        if (record?.StatNames != null && itemMod.Values != null)
        {
            try
            {
                var stats = record.StatNames.ToArray();
                var values = itemMod.Values.ToArray();
                var rows = new List<string>();
                var count = Math.Min(stats.Length, values.Length);
                for (var i = 0; i < count; i++)
                {
                    var stat = stats[i];
                    if (stat == null || values[i] <= -1000)
                        continue;

                    var valueText = stat.ValueToString(values[i])?.Trim() ?? values[i].ToString(CultureInfo.InvariantCulture);
                    var statText = stat.ToString()?.Trim() ?? string.Empty;
                    var statKey = GetMemberString(stat, "Key", "Name");
                    var line = FormatStatLine(statKey, values[i], valueText, statText);
                    line = Regex.Replace(line, @"\s+", " ").Trim();
                    if (!string.IsNullOrWhiteSpace(line) &&
                        !rows.Contains(line, StringComparer.OrdinalIgnoreCase))
                        rows.Add(line);
                }

                if (rows.Count > 0)
                    return CombineAddedDamageRows(rows);
            }
            catch
            {
            }
        }

        var translation = CleanTranslation(itemMod.Translation);
        if (!string.IsNullOrWhiteSpace(translation))
            return translation.Replace("\n", "  /  ");

        return !string.IsNullOrWhiteSpace(itemMod.Name)
            ? Humanize(itemMod.Name)
            : Humanize(itemMod.RawName);
    }

    private static string FormatStatLine(string statKey, int rawValue,
        string valueText, string statText)
    {
        statKey ??= string.Empty;
        valueText ??= string.Empty;
        statText ??= string.Empty;

        var key = statKey.ToLowerInvariant();
        var signed = valueText.StartsWith("+", StringComparison.Ordinal) ||
                     valueText.StartsWith("-", StringComparison.Ordinal)
            ? valueText
            : "+" + valueText;
        var unsigned = valueText.TrimStart('+');
        var descriptor = (key + " " + statText).ToLowerInvariant();

        if (key.Contains("weapon_implicit_hidden", StringComparison.Ordinal) &&
            key.Contains("base_damage_is_", StringComparison.Ordinal))
        {
            var damageType = key.Contains("_fire", StringComparison.Ordinal) ? "Fire" :
                key.Contains("_cold", StringComparison.Ordinal) ? "Cold" :
                key.Contains("_lightning", StringComparison.Ordinal) ? "Lightning" :
                key.Contains("_chaos", StringComparison.Ordinal) ? "Chaos" : "Elemental";
            return $"{unsigned} of Base Damage is {damageType}";
        }

        if (key.Contains("critical_strike_chance", StringComparison.Ordinal))
            return $"{FormatSignedNumber(rawValue / 100d)}% to Critical Hit Chance";

        if (key.Contains("life_regeneration_rate_per_minute",
                StringComparison.Ordinal))
            return $"{FormatNumber(Math.Round(rawValue / 60d, 1,
                MidpointRounding.AwayFromZero))} Life Regenerated per Second";

        if (ContainsAll(descriptor, "item", "found", "rarity"))
            return $"{unsigned} increased Rarity of Items Found";

        if (ContainsAll(descriptor, "evasion", "rating") &&
            valueText.Contains('%'))
            return $"{unsigned} increased Evasion Rating";

        if (ContainsAll(descriptor, "projectile", "skill", "level"))
            return $"{signed} to Level of All Projectile Skills";

        if (key.Contains("physical_damage_+%", StringComparison.Ordinal) ||
            ContainsAll(key, "physical", "damage", "increased"))
            return $"{unsigned} increased Physical Damage";

        if (ContainsAll(key, "thorns", "minimum", "physical"))
            return $"MIN_THORNS {rawValue}";
        if (ContainsAll(key, "thorns", "maximum", "physical"))
            return $"MAX_THORNS {rawValue}";

        if (key.Contains("minimum_added_physical_damage", StringComparison.Ordinal))
            return $"MIN_PHYSICAL {rawValue}";
        if (key.Contains("maximum_added_physical_damage", StringComparison.Ordinal))
            return $"MAX_PHYSICAL {rawValue}";
        if (key.Contains("minimum_added_fire_damage", StringComparison.Ordinal))
            return $"MIN_FIRE {rawValue}";
        if (key.Contains("maximum_added_fire_damage", StringComparison.Ordinal))
            return $"MAX_FIRE {rawValue}";
        if (key.Contains("minimum_added_cold_damage", StringComparison.Ordinal))
            return $"MIN_COLD {rawValue}";
        if (key.Contains("maximum_added_cold_damage", StringComparison.Ordinal))
            return $"MAX_COLD {rawValue}";
        if (key.Contains("minimum_added_lightning_damage", StringComparison.Ordinal))
            return $"MIN_LIGHTNING {rawValue}";
        if (key.Contains("maximum_added_lightning_damage", StringComparison.Ordinal))
            return $"MAX_LIGHTNING {rawValue}";
        if (key.Contains("minimum_added_chaos_damage", StringComparison.Ordinal))
            return $"MIN_CHAOS {rawValue}";
        if (key.Contains("maximum_added_chaos_damage", StringComparison.Ordinal))
            return $"MAX_CHAOS {rawValue}";
        if (key.Contains("hidden_added_minimum_lightning_damage", StringComparison.Ordinal))
            return $"MIN_LIGHTNING {rawValue}";
        if (key.Contains("hidden_added_maximum_lightning_damage", StringComparison.Ordinal))
            return $"MAX_LIGHTNING {rawValue}";
        if (key.Contains("hidden_added_minimum_chaos_damage", StringComparison.Ordinal))
            return $"MIN_CHAOS {rawValue}";
        if (key.Contains("hidden_added_maximum_chaos_damage", StringComparison.Ordinal))
            return $"MAX_CHAOS {rawValue}";

        return key switch
        {
            "base_fire_damage_resistance_%" => $"{signed} to Fire Resistance",
            "base_cold_damage_resistance_%" => $"{signed} to Cold Resistance",
            "base_lightning_damage_resistance_%" => $"{signed} to Lightning Resistance",
            "base_chaos_damage_resistance_%" => $"{signed} to Chaos Resistance",
            "base_maximum_life" => $"{signed} to Maximum Life",
            "base_maximum_mana" => $"{signed} to Maximum Mana",
            "base_movement_velocity_+%" => $"{unsigned} increased Movement Speed",
            "local_physical_damage_reduction_rating_+%" => $"{unsigned} increased Armour",
            "local_evasion_rating_+%" => $"{unsigned} increased Evasion Rating",
            "local_energy_shield_+%" => $"{unsigned} increased Energy Shield",
            "local_base_physical_damage_reduction_rating" => $"{signed} to Armour",
            "local_base_evasion_rating" => $"{signed} to Evasion Rating",
            "local_base_maximum_energy_shield" => $"{signed} to Maximum Energy Shield",
            "additional_strength" => $"{signed} to Strength",
            "additional_dexterity" => $"{signed} to Dexterity",
            "additional_intelligence" => $"{signed} to Intelligence",
            "attack_speed_+%" or "local_attack_speed_+%" => $"{unsigned} increased Attack Speed",
            "base_cast_speed_+%" or "cast_speed_+%" => $"{unsigned} increased Cast Speed",
            "spell_damage_+%" => $"{unsigned} increased Spell Damage",
            _ => FormatGenericStat(statKey, valueText, statText)
        };
    }

    private static string CombineAddedDamageRows(IReadOnlyList<string> rows)
    {
        var output = new List<string>();
        var consumed = new HashSet<int>();
        var damageTypes = new[] { "PHYSICAL", "FIRE", "COLD", "LIGHTNING", "CHAOS" };

        var minimumThornsIndex = Find("MIN_THORNS ");
        var maximumThornsIndex = Find("MAX_THORNS ");
        if (minimumThornsIndex >= 0 && maximumThornsIndex >= 0)
        {
            var minimum = rows[minimumThornsIndex]["MIN_THORNS ".Length..];
            var maximum = rows[maximumThornsIndex]["MAX_THORNS ".Length..];
            output.Add($"{minimum} to {maximum} Physical Thorns Damage");
            consumed.Add(minimumThornsIndex);
            consumed.Add(maximumThornsIndex);
        }

        foreach (var damageType in damageTypes)
        {
            var minimumIndex = Find($"MIN_{damageType} ");
            var maximumIndex = Find($"MAX_{damageType} ");
            if (minimumIndex < 0 || maximumIndex < 0)
                continue;

            var minimum = rows[minimumIndex][($"MIN_{damageType} ").Length..];
            var maximum = rows[maximumIndex][($"MAX_{damageType} ").Length..];
            output.Add($"Adds {minimum} to {maximum} {Humanize(damageType)} Damage");
            consumed.Add(minimumIndex);
            consumed.Add(maximumIndex);
        }

        for (var index = 0; index < rows.Count; index++)
        {
            if (consumed.Contains(index))
                continue;

            var row = rows[index];
            if (row.StartsWith("MIN_", StringComparison.Ordinal) ||
                row.StartsWith("MAX_", StringComparison.Ordinal))
            {
                var parts = row.Split(' ', 2);
                var descriptor = Humanize(parts[0].Replace('_', ' '));
                output.Add(parts.Length > 1 ? $"{parts[1]} {descriptor} Damage" : descriptor);
            }
            else
            {
                output.Add(row);
            }
        }

        return string.Join("  /  ", output);

        int Find(string prefix)
        {
            for (var index = 0; index < rows.Count; index++)
            {
                if (rows[index].StartsWith(prefix, StringComparison.Ordinal))
                    return index;
            }
            return -1;
        }
    }

    private static string FormatGenericStat(string statKey, string valueText, string statText)
    {
        var looksInternal = statText.Contains('_') ||
            string.Equals(statText, statKey, StringComparison.OrdinalIgnoreCase);
        if (!looksInternal && !string.IsNullOrWhiteSpace(statText))
            return (valueText + " " + statText).Trim();

        var cleanedKey = Regex.Replace(statKey ?? string.Empty,
            "^(?:local_|base_)", string.Empty, RegexOptions.IgnoreCase);
        cleanedKey = cleanedKey.Replace("_+%", "%", StringComparison.Ordinal)
            .Replace("_%", "%", StringComparison.Ordinal)
            .Replace("_+", string.Empty, StringComparison.Ordinal);
        return (valueText + " " + Humanize(cleanedKey)).Trim();
    }

    private static string FormatNumber(double value) =>
        value.ToString("0.##", CultureInfo.InvariantCulture);

    private static string FormatSignedNumber(double value) =>
        (value > 0 ? "+" : string.Empty) + FormatNumber(value);

    private static string CleanTranslation(string? input)
    {
        if (string.IsNullOrWhiteSpace(input))
            return string.Empty;

        var text = Regex.Replace(input, @"<[^>]*>", string.Empty);
        while (text.Contains('{'))
        {
            var start = text.LastIndexOf('{');
            var end = text.IndexOf('}', start);
            text = end >= 0 ? text.Remove(start, end - start + 1) : text.Remove(start, 1);
        }
        return Regex.Replace(text, @"\s+", " ").Trim();
    }

    private static bool IsCrafted(object record, ItemMod itemMod)
    {
        var text = $"{GetMemberString(record, "Domain")} {record} {itemMod.RawName} {itemMod.Group}";
        return text.Contains("craft", StringComparison.OrdinalIgnoreCase) ||
               text.Contains("bench", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsHiddenInternalMod(ItemMod itemMod)
    {
        var record = itemMod.ModRecord;
        var text = $"{itemMod.RawName} {itemMod.Name} {itemMod.Group} {record}";
        if (record?.StatNames != null)
            text += " " + string.Join(" ", record.StatNames.Select(x =>
                x == null ? string.Empty : GetMemberString(x, "Key", "Name") + " " + x));

        return text.Contains("hidden", StringComparison.OrdinalIgnoreCase);
    }

    private static WeaponDps CalculateWeaponDps(Entity item, Element? tooltip = null)
    {
        var displayed = CalculateDisplayedWeaponDps(tooltip);
        if (displayed.IsWeapon)
            return displayed;

        if (!item.TryGetComponent<Weapon>(out var weapon) ||
            !item.TryGetComponent<LocalStats>(out var localStats) ||
            weapon.AttackTime <= 0)
            return default;

        var quality = item.GetComponent<Quality>()?.ItemQuality ?? 0;
        var aps = 1000f / weapon.AttackTime;
        var physMin = (float)weapon.DamageMin;
        var physMax = (float)weapon.DamageMax;
        var physMultiplier = 1f;
        var fireMin = 0f;
        var fireMax = 0f;
        var coldMin = 0f;
        var coldMax = 0f;
        var lightningMin = 0f;
        var lightningMax = 0f;
        var chaosMin = 0f;
        var chaosMax = 0f;

        foreach (var stat in localStats.StatDictionary)
        {
            switch (stat.Key)
            {
                case GameStat.LocalPhysicalDamagePct: physMultiplier += stat.Value / 100f; break;
                case GameStat.LocalMinimumAddedPhysicalDamage: physMin += stat.Value; break;
                case GameStat.LocalMaximumAddedPhysicalDamage: physMax += stat.Value; break;
                case GameStat.LocalAttackSpeedPct: aps *= (100f + stat.Value) / 100f; break;
                case GameStat.LocalMinimumAddedFireDamage: fireMin += stat.Value; break;
                case GameStat.LocalMaximumAddedFireDamage: fireMax += stat.Value; break;
                case GameStat.LocalMinimumAddedColdDamage: coldMin += stat.Value; break;
                case GameStat.LocalMaximumAddedColdDamage: coldMax += stat.Value; break;
                case GameStat.LocalMinimumAddedLightningDamage: lightningMin += stat.Value; break;
                case GameStat.LocalMaximumAddedLightningDamage: lightningMax += stat.Value; break;
                case GameStat.LocalMinimumAddedChaosDamage: chaosMin += stat.Value; break;
                case GameStat.LocalMaximumAddedChaosDamage: chaosMax += stat.Value; break;
            }
        }

        var qualityMultiplier = 1f + quality / 100f;
        physMin *= physMultiplier * qualityMultiplier;
        physMax *= physMultiplier * qualityMultiplier;
        var physical = ((physMin + physMax) / 2f) * aps;
        var elemental = (((fireMin + fireMax) + (coldMin + coldMax) +
                           (lightningMin + lightningMax)) / 2f) * aps;
        var chaos = ((chaosMin + chaosMax) / 2f) * aps;
        return new WeaponDps(physical + elemental + chaos, physical, elemental, chaos)
        {
            Fire = (fireMin + fireMax) / 2f * aps,
            Cold = (coldMin + coldMax) / 2f * aps,
            Lightning = (lightningMin + lightningMax) / 2f * aps
        };
    }

    private static WeaponDps CalculateDisplayedWeaponDps(Element? tooltip)
    {
        if (tooltip == null)
            return default;

        var text = string.Join("\n", EnumerateElements(tooltip)
            .Select(element => GetMemberString(element, "TextNoTags", "Text")));
        var apsMatch = Regex.Match(text,
            @"Attacks\s+per\s+Second\s*:\s*(\d+(?:\.\d+)?)",
            RegexOptions.IgnoreCase);
        if (!apsMatch.Success || !float.TryParse(apsMatch.Groups[1].Value,
                NumberStyles.Float, CultureInfo.InvariantCulture, out var attacksPerSecond) ||
            attacksPerSecond <= 0f)
            return default;

        var physicalRange = ReadDamageRange("Physical");
        var fireRange = ReadDamageRange("Fire");
        var coldRange = ReadDamageRange("Cold");
        var lightningRange = ReadDamageRange("Lightning");
        var chaosRange = ReadDamageRange("Chaos");

        var physical = Average(physicalRange) * attacksPerSecond;
        var elemental = (Average(fireRange) + Average(coldRange) +
                         Average(lightningRange)) * attacksPerSecond;
        var chaos = Average(chaosRange) * attacksPerSecond;
        return new WeaponDps(physical + elemental + chaos, physical, elemental, chaos)
        {
            Fire = Average(fireRange) * attacksPerSecond,
            Cold = Average(coldRange) * attacksPerSecond,
            Lightning = Average(lightningRange) * attacksPerSecond
        };

        (float Minimum, float Maximum) ReadDamageRange(string type)
        {
            var match = Regex.Match(text,
                $@"{type}\s+Damage\s*:\s*([\d,]+)\s*[-–—]\s*([\d,]+)",
                RegexOptions.IgnoreCase);
            if (!match.Success ||
                !float.TryParse(match.Groups[1].Value.Replace(",", string.Empty),
                    NumberStyles.Float, CultureInfo.InvariantCulture, out var minimum) ||
                !float.TryParse(match.Groups[2].Value.Replace(",", string.Empty),
                    NumberStyles.Float, CultureInfo.InvariantCulture, out var maximum))
                return default;
            return (minimum, maximum);
        }

        static float Average((float Minimum, float Maximum) range) =>
            (range.Minimum + range.Maximum) / 2f;
    }

    private static DefenceTotals CalculateDisplayedDefences(Element? tooltip)
    {
        if (tooltip == null)
            return default;

        var text = string.Join("\n", EnumerateElements(tooltip)
            .Select(element => GetMemberString(element, "TextNoTags", "Text")));

        return ParseDefenceText(CleanTranslation(text));
    }

    private static DefenceTotals ParseDefenceText(string text)
    {
        return new DefenceTotals(
            ReadValue(@"\bArmour\s*:\s*([\d,]+)"),
            ReadValue(@"\bEvasion(?:\s+Rating)?\s*:\s*([\d,]+)"),
            ReadValue(@"\bEnergy\s+Shield\s*:\s*([\d,]+)"),
            ReadValue(@"\bRunic\s+Ward\s*:\s*([\d,]+)"));

        int ReadValue(string pattern)
        {
            var match = Regex.Match(text, pattern, RegexOptions.IgnoreCase);
            return match.Success && int.TryParse(
                match.Groups[1].Value.Replace(",", string.Empty),
                NumberStyles.Integer, CultureInfo.InvariantCulture,
                out var value)
                ? value
                : 0;
        }
    }

    private string GetItemDisplayName(Entity item, Element? tooltip, string baseType)
    {
        var componentName = item.GetComponent<Base>()?.Name;
        if (IsMeaningfulItemName(componentName, baseType))
            return componentName!.Trim();

        var direct = GetMemberString(item, "RenderName", "Name", "DisplayName", "Text");
        if (IsMeaningfulItemName(direct, baseType))
            return direct.Trim();

        if (tooltip != null)
        {
            foreach (var element in EnumerateElements(tooltip))
            {
                var text = GetMemberString(element, "TextNoTags", "Text");
                foreach (var line in text.Split(['\r', '\n'],
                             StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                {
                    var clean = CleanTranslation(line);
                    if (LooksLikeItemTitle(clean, baseType))
                        return clean;
                }
            }
        }

        return baseType;
    }

    private static IEnumerable<string> GetTooltipLines(Element tooltip)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var element in EnumerateElements(tooltip))
        {
            var hasChildren = false;
            try
            {
                hasChildren = element.Children.Count > 0;
            }
            catch
            {
            }

            if (hasChildren)
                continue;

            var text = GetMemberString(element, "TextNoTags", "Text");
            foreach (var line in text.Split(['\r', '\n'],
                         StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                var clean = CleanTranslation(line);
                if (!string.IsNullOrWhiteSpace(clean) && seen.Add(clean))
                    yield return clean;
            }
        }
    }

    private static IEnumerable<Element> EnumerateElements(Element root)
    {
        var pending = new Stack<Element>();
        pending.Push(root);
        while (pending.Count > 0)
        {
            var current = pending.Pop();
            yield return current;

            try
            {
                var children = current.Children;
                for (var index = children.Count - 1; index >= 0; index--)
                {
                    if (children[index] != null)
                        pending.Push(children[index]);
                }
            }
            catch
            {
            }
        }
    }

    private static bool IsMeaningfulItemName(string? value, string baseType) =>
        IsMeaningfulName(value) &&
        !string.Equals(value!.Trim(), baseType, StringComparison.OrdinalIgnoreCase);

    private static bool LooksLikeItemTitle(string value, string baseType)
    {
        if (!IsMeaningfulItemName(value, baseType) || value.Length is < 2 or > 70 ||
            !value.Any(char.IsLetter) || value.Contains(':') || value.Contains('%') ||
            value.StartsWith('+') || char.IsDigit(value[0]) ||
            value.Contains(baseType, StringComparison.OrdinalIgnoreCase))
            return false;

        var lower = value.ToLowerInvariant();
        var excluded = new[]
        {
            "requires", "physical damage", "fire damage", "cold damage",
            "lightning damage", "chaos damage", "critical hit", "attacks per second",
            "armour", "evasion", "energy shield", "one hand", "two hand", "item level",
            "quality", "prefix", "suffix", "implicit", "unique", "rare", "magic"
        };
        return !excluded.Any(lower.Contains);
    }

    private string GetBaseTypeName(Entity item)
    {
        try
        {
            var baseItem = GameController.Files.BaseItemTypes.Translate(item.Path);
            var name = GetMemberString(baseItem,
                "BaseName", "Name", "DisplayName", "ItemName", "TypeName");
            if (IsMeaningfulName(name))
                return name;
        }
        catch
        {
        }

        var leaf = (item.Path ?? string.Empty).Split('/').LastOrDefault() ?? "Item";
        return Humanize(Regex.Replace(leaf,
            "(?:Str|Dex|Int|StrDex|StrInt|DexInt)\\d+$", string.Empty,
            RegexOptions.IgnoreCase));
    }

    internal static string GetSlotName(string? path)
    {
        var value = (path ?? string.Empty).ToLowerInvariant();
        var compact = Regex.Replace(value, "[^a-z0-9]", string.Empty);
        if (value.Contains("helmet") || value.Contains("helm") || value.Contains("circlet")) return "Helmet";
        if (value.Contains("boot")) return "Boots";
        if (value.Contains("glove")) return "Gloves";
        if (value.Contains("belt")) return "Belt";
        if (value.Contains("bodyarmour") || value.Contains("body_armour") || value.Contains("chest")) return "Body Armour";
        if (value.Contains("ring")) return "Ring";
        if (value.Contains("amulet")) return "Amulet";
        if (value.Contains("shield") || value.Contains("buckler")) return "Shield";
        if (value.Contains("quiver")) return "Quiver";
        if (value.Contains("bow")) return "Bow";
        if (compact.Contains("onehand") && compact.Contains("mace")) return "1H Mace";
        if (value.Contains("wand") || value.Contains("sceptre") || value.Contains("scepter") ||
            value.Contains("focus") || value.Contains("staff") || value.Contains("staves")) return "Caster Weapon";
        if (value.Contains("weapon") || value.Contains("axe") || value.Contains("mace") ||
            value.Contains("sword") || value.Contains("spear") || value.Contains("flail") ||
            value.Contains("quarterstaff")) return "Martial Weapon";
        return string.Empty;
    }

    private static string GetItemClassName(string? path, string slotName)
    {
        var compact = Regex.Replace((path ?? string.Empty).ToLowerInvariant(),
            "[^a-z0-9]", string.Empty);

        var isTwoHanded = compact.Contains("twohand");
        var isOneHanded = compact.Contains("onehand");
        if (isTwoHanded && compact.Contains("mace")) return "2H MACE";
        if (isOneHanded && compact.Contains("mace")) return "1H MACE";
        if (isTwoHanded && compact.Contains("axe")) return "2H AXE";
        if (isOneHanded && compact.Contains("axe")) return "1H AXE";
        if (isTwoHanded && compact.Contains("sword")) return "2H SWORD";
        if (isOneHanded && compact.Contains("sword")) return "1H SWORD";
        if (compact.Contains("quarterstaff")) return "QUARTERSTAFF";
        if (compact.Contains("crossbow")) return "CROSSBOW";
        if (compact.Contains("sceptre") || compact.Contains("scepter")) return "SCEPTRE";
        if (compact.Contains("wand")) return "WAND";
        if (compact.Contains("staff") || compact.Contains("staves")) return "STAFF";
        if (compact.Contains("spear")) return "SPEAR";
        if (compact.Contains("flail")) return "FLAIL";
        if (compact.Contains("bow")) return "BOW";
        if (compact.Contains("mace")) return "MACE";
        if (compact.Contains("axe")) return "AXE";
        if (compact.Contains("sword")) return "SWORD";

        return string.IsNullOrWhiteSpace(slotName)
            ? "ITEM"
            : slotName.ToUpperInvariant();
    }

    private static bool IsMeaningfulName(string? value) =>
        !string.IsNullOrWhiteSpace(value) &&
        !string.Equals(value.Trim(), "EMPTY", StringComparison.OrdinalIgnoreCase) &&
        !value.StartsWith("Metadata/", StringComparison.OrdinalIgnoreCase);

    private static string Humanize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return "Item";

        var text = value.Replace('_', ' ');
        text = Regex.Replace(text, "([a-z])([A-Z])", "$1 $2");
        text = Regex.Replace(text, @"\s+", " ").Trim();
        return CultureInfo.InvariantCulture.TextInfo.ToTitleCase(text.ToLowerInvariant());
    }

    private static bool ContainsAll(string text, params string[] terms) =>
        terms.All(term => text.Contains(term, StringComparison.OrdinalIgnoreCase));

    private static string GetMemberString(object? obj, params string[] names)
    {
        if (obj == null)
            return string.Empty;

        foreach (var name in names)
        {
            try
            {
                var type = obj.GetType();
                var property = type.GetProperty(name,
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (property?.GetValue(obj) is { } propertyValue)
                    return propertyValue.ToString() ?? string.Empty;

                var field = type.GetField(name,
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (field?.GetValue(obj) is { } fieldValue)
                    return fieldValue.ToString() ?? string.Empty;
            }
            catch
            {
            }
        }

        return string.Empty;
    }
}
