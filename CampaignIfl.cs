using System;
using System.Collections.Generic;
using System.Linq;

namespace LootLens2;

internal static class CampaignIfl
{
    private readonly record struct StageValues(
        int Life,
        int ElementalResistance,
        int AllElementalResistance,
        int ChaosResistance,
        int MovementSpeed,
        int Spirit,
        int AttackSpeed,
        int CastSpeed,
        int SpellDamage,
        int PhysicalDamage,
        int TotalDps,
        int CriticalChance);

    private static readonly IReadOnlyDictionary<CampaignStage,
        IReadOnlyDictionary<string, QualificationProfile>> Profiles = BuildProfiles();

    public static CampaignStage FromLevel(int level) => level switch
    {
        <= 17 => CampaignStage.Act1,
        <= 32 => CampaignStage.Act2,
        <= 44 => CampaignStage.Act3,
        <= 51 => CampaignStage.Act4,
        <= 55 => CampaignStage.Interlude1,
        <= 59 => CampaignStage.Interlude2,
        <= 64 => CampaignStage.Interlude3,
        _ => CampaignStage.EarlyMaps
    };

    public static CampaignStage FromSelection(CampaignStageSelection selection) =>
        selection switch
        {
            CampaignStageSelection.Act2 => CampaignStage.Act2,
            CampaignStageSelection.Act3 => CampaignStage.Act3,
            CampaignStageSelection.Act4 => CampaignStage.Act4,
            CampaignStageSelection.Interlude1 => CampaignStage.Interlude1,
            CampaignStageSelection.Interlude2 => CampaignStage.Interlude2,
            CampaignStageSelection.Interlude3 => CampaignStage.Interlude3,
            CampaignStageSelection.EarlyMaps => CampaignStage.EarlyMaps,
            _ => CampaignStage.Act1
        };

    public static string Label(CampaignStage stage) => stage switch
    {
        CampaignStage.Act1 => "ACT 1 HC IFL",
        CampaignStage.Act2 => "ACT 2 HC IFL",
        CampaignStage.Act3 => "ACT 3 HC IFL",
        CampaignStage.Act4 => "ACT 4 HC IFL",
        CampaignStage.Interlude1 => "INTERLUDE I HC IFL",
        CampaignStage.Interlude2 => "INTERLUDE II HC IFL",
        CampaignStage.Interlude3 => "INTERLUDE III HC IFL",
        _ => "EARLY MAPS HC IFL"
    };

    public static string SelectionLabel(CampaignStageSelection selection) =>
        selection switch
        {
            CampaignStageSelection.Automatic => "Automatic",
            CampaignStageSelection.Act1 => "Act 1",
            CampaignStageSelection.Act2 => "Act 2",
            CampaignStageSelection.Act3 => "Act 3",
            CampaignStageSelection.Act4 => "Act 4",
            CampaignStageSelection.Interlude1 => "Interlude I",
            CampaignStageSelection.Interlude2 => "Interlude II",
            CampaignStageSelection.Interlude3 => "Interlude III",
            _ => "Early Maps"
        };

    public static string LevelBand(CampaignStage stage) => stage switch
    {
        CampaignStage.Act1 => "LEVELS 1-17",
        CampaignStage.Act2 => "LEVELS 18-32",
        CampaignStage.Act3 => "LEVELS 33-44",
        CampaignStage.Act4 => "LEVELS 45-51",
        CampaignStage.Interlude1 => "LEVELS 52-55",
        CampaignStage.Interlude2 => "LEVELS 56-59",
        CampaignStage.Interlude3 => "LEVELS 60-64",
        _ => "LEVEL 65+"
    };

    public static QualificationProfile? GetProfile(CampaignStage stage,
        string slotName)
    {
        return Profiles.TryGetValue(stage, out var profiles) &&
               profiles.TryGetValue(slotName, out var profile)
            ? profile
            : null;
    }

    public static IReadOnlyList<QualificationProfile> GetProfiles(
        CampaignStage stage) => Profiles.TryGetValue(stage, out var profiles)
        ? profiles.Values.ToList()
        : [];

    public static string MandatoryLabel(MandatoryQualificationRule rule) =>
        rule switch
        {
            MandatoryQualificationRule.LifeOrDefence => "LIFE OR DEFENCE",
            MandatoryQualificationRule.MovementSpeed => "MOVE SPEED",
            MandatoryQualificationRule.TotalWeaponDps => "TOTAL DPS",
            MandatoryQualificationRule.CasterPower =>
                "SPELL DAMAGE OR SKILL LEVEL",
            _ => string.Empty
        };

    private static IReadOnlyDictionary<CampaignStage,
        IReadOnlyDictionary<string, QualificationProfile>> BuildProfiles()
    {
        var values = new Dictionary<CampaignStage, StageValues>
        {
            [CampaignStage.Act1] = new(20, 10, 5, 0, 10, 0, 5, 5, 20, 30, 25, 0),
            [CampaignStage.Act2] = new(40, 15, 5, 8, 15, 30, 7, 7, 30, 50, 55, 1),
            [CampaignStage.Act3] = new(60, 20, 7, 12, 20, 38, 8, 8, 40, 70, 95, 1),
            [CampaignStage.Act4] = new(70, 25, 8, 12, 25, 43, 10, 10, 55, 90, 145, 1),
            [CampaignStage.Interlude1] = new(85, 28, 9, 12, 25, 43, 10, 10, 60, 100, 170, 1),
            [CampaignStage.Interlude2] = new(85, 30, 10, 16, 25, 47, 11, 11, 65, 110, 205, 1),
            [CampaignStage.Interlude3] = new(100, 30, 10, 16, 25, 51, 12, 12, 75, 120, 245, 1),
            [CampaignStage.EarlyMaps] = new(100, 35, 12, 20, 25, 54, 12, 12, 80, 130, 290, 2)
        };

        return values.ToDictionary(pair => pair.Key,
            pair => (IReadOnlyDictionary<string, QualificationProfile>)
                CreateProfiles(pair.Key, pair.Value).ToDictionary(
                    profile => profile.Name, StringComparer.OrdinalIgnoreCase));
    }

    private static IEnumerable<QualificationProfile> CreateProfiles(
        CampaignStage stage, StageValues value)
    {
        var skillLevel = (int)stage >= (int)CampaignStage.Act2 ? 1 : 0;

        yield return GeneralGear("Helmet", RuleProfileKind.Armour, value);
        yield return GeneralGear("Body Armour", RuleProfileKind.Armour, value);
        yield return GeneralGear("Gloves", RuleProfileKind.Armour, value);
        yield return GeneralGear("Boots", RuleProfileKind.Boots, value,
            movementSpeed: value.MovementSpeed);
        yield return new QualificationProfile
        {
            Name = "Shield",
            Kind = RuleProfileKind.Armour,
            RequiredMatches = 2,
            MinimumFlatArmour = 1,
            MinimumArmourPercent = 1
        };
        yield return GeneralGear("Belt", RuleProfileKind.Jewellery, value);
        yield return GeneralGear("Ring", RuleProfileKind.Jewellery, value);
        yield return GeneralGear("Amulet", RuleProfileKind.Jewellery, value);
        yield return GeneralGear("Quiver", RuleProfileKind.Quiver, value);
        yield return new QualificationProfile
        {
            Name = "1H Mace",
            Kind = RuleProfileKind.MartialWeapon,
            RequiredMatches = 1,
            MinimumMeleeSkillLevels = 1
        };
        yield return Weapon("Martial Weapon", value, value.TotalDps);
        yield return Weapon("Bow", value,
            Math.Max(20, (int)Math.Round(value.TotalDps * .90)));
        yield return new QualificationProfile
        {
            Name = "Caster Weapon",
            Kind = RuleProfileKind.CasterWeapon,
            RequiredMatches = 2,
            MandatoryRule = MandatoryQualificationRule.CasterPower,
            MinimumSpirit = value.Spirit,
            MinimumCastSpeed = value.CastSpeed,
            MinimumSpellDamage = value.SpellDamage,
            MinimumSkillLevels = skillLevel,
            UseCombinedCasterPower = true
        };
    }

    private static QualificationProfile GeneralGear(string name,
        RuleProfileKind kind, StageValues value, int movementSpeed = 0) => new()
        {
            Name = name,
            Kind = kind,
            RequiredMatches = movementSpeed > 0 ? 3 : 2,
            MandatoryRule = movementSpeed > 0
                ? MandatoryQualificationRule.MovementSpeed
                : MandatoryQualificationRule.None,
            MinimumLife = value.Life,
            MinimumElementalResistancePerMod = value.ElementalResistance,
            MinimumAllElementalResistance = value.AllElementalResistance,
            MinimumChaosResistance = value.ChaosResistance,
            MinimumMovementSpeed = movementSpeed,
            MinimumArmourAppliesToElementalDamage = 1
        };

    private static QualificationProfile Weapon(string name, StageValues value,
        int totalDps) => new()
        {
            Name = name,
            Kind = RuleProfileKind.MartialWeapon,
            RequiredMatches = 2,
            MandatoryRule = MandatoryQualificationRule.TotalWeaponDps,
            MinimumAttackSpeed = value.AttackSpeed,
            MinimumPhysicalDamagePercent = value.PhysicalDamage,
            MinimumTotalDps = totalDps,
            MinimumCriticalChance = value.CriticalChance,
            MinimumAddedDamageMods = 1
        };
}
