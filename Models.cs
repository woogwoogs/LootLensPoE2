using System;
using System.Collections.Generic;

namespace LootLens2;

public enum AnalyzerVisibilityMode
{
    Hold,
    Toggle,
    Always
}

public enum AnalyzerLayoutMode
{
    Compact,
    Detailed
}

public enum PerfectionRangeMode
{
    CurrentTier,
    AllValidTiers,
    ItemLevelReachable
}

public enum RuleProfileKind
{
    Armour,
    Boots,
    Jewellery,
    Quiver,
    MartialWeapon,
    CasterWeapon
}

public enum QualificationRulesMode
{
    Campaign,
    Custom
}

public enum CampaignBuildPreset
{
    LifeAndResists,
    EvasionAndEnergyShield
}

public enum CampaignStageSelection
{
    Automatic,
    Act1,
    Act2,
    Act3,
    Act4,
    Interlude1,
    Interlude2,
    Interlude3,
    EarlyMaps
}

public enum CampaignStage
{
    Act1,
    Act2,
    Act3,
    Act4,
    Interlude1,
    Interlude2,
    Interlude3,
    EarlyMaps
}

public enum MandatoryQualificationRule
{
    None,
    LifeOrDefence,
    MovementSpeed,
    TotalWeaponDps,
    CasterPower,
    EvasionAndEnergyShield
}

public sealed class QualificationProfile
{
    public string Name { get; set; } = "Gear";
    public RuleProfileKind Kind { get; set; } = RuleProfileKind.Armour;
    public bool Enabled { get; set; } = true;
    public int RequiredMatches { get; set; } = 2;
    public MandatoryQualificationRule MandatoryRule { get; set; }

    // A value of zero disables that criterion for this profile.
    public int MinimumLife { get; set; }
    public int MinimumSpirit { get; set; }
    public int MinimumTotalElementalResistance { get; set; }
    public int MinimumElementalResistancePerMod { get; set; }
    public int MinimumAllElementalResistance { get; set; }
    public int MinimumChaosResistance { get; set; }
    public int MinimumMovementSpeed { get; set; }
    public int MinimumDefencePercent { get; set; }
    public int MinimumFlatArmour { get; set; }
    public int MinimumArmourPercent { get; set; }
    public int MinimumArmourAppliesToElementalDamage { get; set; }
    public int MinimumEnergyShieldMods { get; set; }
    public int MinimumEvasionEnergyShieldMods { get; set; }
    public int MinimumAttributes { get; set; }
    public int MinimumAttackSpeed { get; set; }
    public int MinimumCastSpeed { get; set; }
    public int MinimumSpellDamage { get; set; }
    public int MinimumPhysicalDamagePercent { get; set; }
    public int MinimumPhysicalDps { get; set; }
    public int MinimumTotalDps { get; set; }
    public int MinimumSkillLevels { get; set; }
    public int MinimumMeleeSkillLevels { get; set; }
    public int MinimumCriticalChance { get; set; }
    public int MinimumAddedDamageMods { get; set; }
    public bool UseCombinedCasterPower { get; set; }

    public static List<QualificationProfile> CreateDefaults()
    {
        return
        [
            new() { Name = "Helmet", Kind = RuleProfileKind.Armour, RequiredMatches = 2, MinimumLife = 50, MinimumTotalElementalResistance = 50, MinimumDefencePercent = 60 },
            new() { Name = "Body Armour", Kind = RuleProfileKind.Armour, RequiredMatches = 2, MinimumLife = 60, MinimumTotalElementalResistance = 50, MinimumDefencePercent = 80 },
            new() { Name = "Gloves", Kind = RuleProfileKind.Armour, RequiredMatches = 2, MinimumLife = 50, MinimumTotalElementalResistance = 50, MinimumDefencePercent = 60, MinimumAttackSpeed = 10 },
            new() { Name = "Boots", Kind = RuleProfileKind.Boots, RequiredMatches = 3, MinimumLife = 50, MinimumTotalElementalResistance = 40, MinimumMovementSpeed = 20 },
            new() { Name = "Shield", Kind = RuleProfileKind.Armour, RequiredMatches = 2, MinimumLife = 50, MinimumTotalElementalResistance = 50, MinimumDefencePercent = 60 },
            new() { Name = "Belt", Kind = RuleProfileKind.Jewellery, RequiredMatches = 2, MinimumLife = 50, MinimumTotalElementalResistance = 50, MinimumAttributes = 25 },
            new() { Name = "Ring", Kind = RuleProfileKind.Jewellery, RequiredMatches = 2, MinimumLife = 40, MinimumTotalElementalResistance = 50, MinimumAttributes = 25 },
            new() { Name = "Amulet", Kind = RuleProfileKind.Jewellery, RequiredMatches = 2, MinimumLife = 40, MinimumSpirit = 20, MinimumTotalElementalResistance = 40, MinimumAttributes = 25 },
            new() { Name = "Quiver", Kind = RuleProfileKind.Quiver, RequiredMatches = 2, MinimumLife = 40, MinimumTotalElementalResistance = 40, MinimumAttackSpeed = 8 },
            new() { Name = "Martial Weapon", Kind = RuleProfileKind.MartialWeapon, RequiredMatches = 2, MinimumAttackSpeed = 10, MinimumPhysicalDamagePercent = 80, MinimumPhysicalDps = 120, MinimumTotalDps = 150 },
            new() { Name = "Bow", Kind = RuleProfileKind.MartialWeapon, RequiredMatches = 2, MinimumAttackSpeed = 10, MinimumPhysicalDamagePercent = 70, MinimumPhysicalDps = 100, MinimumTotalDps = 130 },
            new() { Name = "Caster Weapon", Kind = RuleProfileKind.CasterWeapon, RequiredMatches = 2, MinimumSpirit = 20, MinimumCastSpeed = 10, MinimumSpellDamage = 40 }
        ];
    }
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
    public List<QualificationMatch> QualificationMatches { get; } = [];
    public List<QualificationMatch> QualificationFailures { get; } = [];
    public int RequiredQualificationMatches { get; set; }
    public string QualificationRuleSet { get; set; } = string.Empty;
    public string MandatoryQualificationLabel { get; set; } = string.Empty;
    public bool MandatoryQualificationPassed { get; set; } = true;
    public bool Qualifies { get; set; }
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
    public bool IsImplicit { get; set; }
    public bool IsCrafted { get; set; }
    public bool IsHidden { get; set; }
}

internal readonly record struct QualificationMatch(string Label, int Actual, int Required, string Suffix);

internal struct StatSnapshot
{
    public int Life;
    public int Spirit;
    public int FireResistance;
    public int ColdResistance;
    public int LightningResistance;
    public int AllElementalResistance;
    public int ChaosResistance;
    public int MovementSpeed;
    public int FlatArmour;
    public int ArmourPercent;
    public int EvasionPercent;
    public int EnergyShieldPercent;
    public int EnergyShieldMods;
    public int EvasionEnergyShieldMods;
    public int Strength;
    public int Dexterity;
    public int Intelligence;
    public int AttackSpeed;
    public int CastSpeed;
    public int SpellDamage;
    public int PhysicalDamagePercent;
    public int SkillLevels;
    public int MeleeSkillLevels;
    public int CriticalChance;
    public int AddedDamageMods;
    public int RunicWardPercent;
    public int ArmourAppliesToElementalDamage;
    public WeaponDps WeaponDps;

    public int TotalElementalResistance => FireResistance + ColdResistance +
                                             LightningResistance +
                                             AllElementalResistance * 3;
    public int BestDefencePercent => Math.Max(RunicWardPercent,
        Math.Max(ArmourPercent, Math.Max(EvasionPercent, EnergyShieldPercent)));
    public int BestAttribute => Math.Max(Strength, Math.Max(Dexterity, Intelligence));
}

internal readonly record struct WeaponDps(float Total, float Physical, float Elemental, float Chaos)
{
    public bool IsWeapon => Total > 0.01f;
}

internal readonly record struct DefenceTotals(int Armour, int Evasion,
    int EnergyShield, int RunicWard)
{
    public bool HasDefence => Armour > 0 || Evasion > 0 ||
                              EnergyShield > 0 || RunicWard > 0;
}

internal readonly record struct CheckMarker(System.Numerics.Vector2 TopLeft, float Size);
