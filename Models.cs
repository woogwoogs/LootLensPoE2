using System;
using System.Collections.Generic;

namespace LootLens2;

public enum AnalyzerVisibilityMode
{
    Hold,
    Toggle,
    Always
}

public enum PerfectionRangeMode
{
    CurrentTier,
    AllValidTiers,
    ItemLevelReachable
}

internal sealed class AnalyzedItem
{
    public string Name { get; set; } = "ITEM";
    public string BaseType { get; set; } = "Item";
    public string Rarity { get; set; } = "Normal";
    public int ItemLevel { get; set; }
    public int Quality { get; set; }
    public string SlotName { get; set; } = string.Empty;
    public string ItemClassName { get; set; } = string.Empty;
    public List<AnalyzedMod> Mods { get; } = [];
    public int OverallPerfection { get; set; } = -1;
    public int VariableRollCount { get; set; }
    public int VariableModCount { get; set; }
    public string UniqueDropTier { get; set; } = string.Empty;
    public WeaponDps WeaponDps { get; set; }
    public DefenceTotals DefenceTotals { get; set; }
}

internal sealed class AnalyzedMod
{
    public string Affix { get; set; } = string.Empty;
    public string Text { get; set; } = "Unknown modifier";
    public string RangeText { get; set; } = string.Empty;
    public string RangeLabel { get; set; } = "TIER RANGE";
    public int Tier { get; set; }
    public int TotalTiers { get; set; }
    public int Perfection { get; set; } = -1;
    public int VariableRollCount { get; set; }
    public bool IsUnique { get; set; }
    public bool IsFixed { get; set; }
    public bool IsImplicit { get; set; }
    public bool IsCrafted { get; set; }
    public bool IsHidden { get; set; }
}

internal readonly record struct WeaponDps(float Total, float Physical, float Elemental, float Chaos)
{
    public float Fire { get; init; }
    public float Cold { get; init; }
    public float Lightning { get; init; }
    public bool IsWeapon => Total > 0.01f;
}

internal readonly record struct DefenceTotals(int Armour, int Evasion,
    int EnergyShield, int RunicWard)
{
    public bool HasDefence => Armour > 0 || Evasion > 0 ||
                              EnergyShield > 0 || RunicWard > 0;
}

