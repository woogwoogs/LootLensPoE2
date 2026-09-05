using System;
using System.Linq;
using System.Numerics;
using System.Runtime.InteropServices;
using ExileCore2.Shared.Nodes;
using ImGuiNET;

namespace LootLens2;

public partial class LootLens2
{
    private int _selectedProfileIndex;
    private int _selectedCampaignProfileIndex;
    private bool _capturingAnalyzerKey;

    [DllImport("user32.dll")]
    private static extern short GetAsyncKeyState(int virtualKey);

    public override void DrawSettings()
    {
        ImGui.TextColored(new Vector4(.86f, .66f, .22f, 1f), "LOOTLENS 2");
        ImGui.SameLine();
        ImGui.TextDisabled("Path of Exile 2 item analysis");
        ImGui.Spacing();

        if (!ImGui.BeginTabBar("LootLens2_SettingsTabs"))
            return;

        if (ImGui.BeginTabItem("GENERAL"))
        {
            DrawGeneralSettings();
            ImGui.EndTabItem();
        }

        if (ImGui.BeginTabItem("CHECK RULES"))
        {
            DrawCheckRuleSettings();
            ImGui.EndTabItem();
        }

        if (ImGui.BeginTabItem("APPEARANCE"))
        {
            DrawAppearanceSettings();
            ImGui.EndTabItem();
        }

        ImGui.EndTabBar();
    }

    private void DrawGeneralSettings()
    {
        ImGui.Spacing();
        DrawToggle("Enable LootLens 2", Settings.Enable);
        ImGui.SeparatorText("ANALYZER");
        DrawToggle("Show attached item analyzer", Settings.ShowAnalyzer);
        DrawAnalyzerVisibilitySettings();
        DrawAnalyzerLayoutSettings();
        DrawToggle("Analyze magic items", Settings.AnalyzeMagicItems);
        DrawToggle("Analyze rare items", Settings.AnalyzeRareItems);
        DrawToggle("Analyze unique items", Settings.AnalyzeUniqueItems);
        DrawToggle("Include ordinary implicit modifiers", Settings.ShowImplicitModifiers);
        ImGui.TextDisabled("Useful hidden weapon modifiers are shown; augments are intentionally omitted.");
        DrawPerfectionRangeSettings();
        if (_database.IsLoaded)
        {
            ImGui.TextColored(new Vector4(.32f, .86f, .48f, 1f),
                $"JSON DATABASE LOADED  ·  {_database.BaseCount} BASES  ·  " +
                $"{_database.ModFamilyCount} MOD FAMILIES  ·  " +
                $"{_database.UniqueCount} UNIQUES");
        }
        else
        {
            ImGui.TextColored(new Vector4(.90f, .34f, .30f, 1f),
                "JSON DATABASE NOT LOADED — USING LIVE FALLBACK");
        }
        DrawToggle("Show overall unique-item perfection", Settings.ShowOverallPerfection);

        ImGui.SeparatorText("GREEN CHECK");
        DrawToggle("Show qualifying check on items", Settings.ShowQualifyingCheck);
        DrawToggle("Check magic items", Settings.CheckMagicItems);
        DrawToggle("Check rare items", Settings.CheckRareItems);
        if (Settings.QualificationRules == QualificationRulesMode.Custom)
            DrawToggle("Check unique items", Settings.CheckUniqueItems);
        else
            ImGui.TextDisabled("Unique items use drop tier and perfection; campaign checks are for magic and rare gear.");
        DrawToggle("Show qualifying-stat footer", Settings.ShowQualificationFooter);

        ImGui.Spacing();
        ImGui.TextWrapped("Campaign IFL uses one acceptable-good baseline for each slot and campaign stage. Custom mode keeps the original editable threshold profiles.");
    }

    private void DrawAnalyzerVisibilitySettings()
    {
        ImGui.Text("Analyzer visibility");
        ImGui.SameLine();
        if (ImGui.RadioButton("Hold##AnalyzerVisibility",
                Settings.AnalyzerVisibility == AnalyzerVisibilityMode.Hold))
            Settings.AnalyzerVisibility = AnalyzerVisibilityMode.Hold;
        ImGui.SameLine();
        if (ImGui.RadioButton("Toggle##AnalyzerVisibility",
                Settings.AnalyzerVisibility == AnalyzerVisibilityMode.Toggle))
            Settings.AnalyzerVisibility = AnalyzerVisibilityMode.Toggle;
        ImGui.SameLine();
        if (ImGui.RadioButton("Always##AnalyzerVisibility",
                Settings.AnalyzerVisibility == AnalyzerVisibilityMode.Always))
            Settings.AnalyzerVisibility = AnalyzerVisibilityMode.Always;

        if (Settings.AnalyzerVisibility == AnalyzerVisibilityMode.Always)
            return;

        ImGui.Text(Settings.AnalyzerVisibility == AnalyzerVisibilityMode.Hold
            ? "Hold key to show analyzer"
            : "Toggle analyzer key");
        ImGui.SameLine();
        if (ImGui.Button(_capturingAnalyzerKey
                ? "PRESS A KEY..."
                : Settings.AnalyzerKey.Value.ToString(), new Vector2(180f, 0f)))
            _capturingAnalyzerKey = true;

        if (!_capturingAnalyzerKey)
            return;

        for (var key = 1; key < 256; key++)
        {
            if ((GetAsyncKeyState(key) & 0x8000) == 0 ||
                key is 1 or 2 or 4 or 5 or 6)
                continue;

            Settings.AnalyzerKey = new ExileCore2.Shared.Nodes.HotkeyNodeV2(
                (System.Windows.Forms.Keys)key);
            _capturingAnalyzerKey = false;
            break;
        }
    }

    private void DrawAnalyzerLayoutSettings()
    {
        ImGui.Text("Analyzer layout");
        ImGui.SameLine();
        if (ImGui.RadioButton("Compact##AnalyzerLayout",
                Settings.AnalyzerLayout == AnalyzerLayoutMode.Compact))
            Settings.AnalyzerLayout = AnalyzerLayoutMode.Compact;
        ImGui.SameLine();
        if (ImGui.RadioButton("Detailed##AnalyzerLayout",
                Settings.AnalyzerLayout == AnalyzerLayoutMode.Detailed))
            Settings.AnalyzerLayout = AnalyzerLayoutMode.Detailed;

        ImGui.TextDisabled(Settings.AnalyzerLayout == AnalyzerLayoutMode.Compact
            ? "Fast at-a-glance rows with shorthand names and roll diamonds."
            : "Shows exact ranges, percentages, and the expanded item summary.");
    }

    private void DrawPerfectionRangeSettings()
    {
        ImGui.Text("Perfection range");
        if (ImGui.RadioButton("Current tier##PerfectionRange",
                Settings.PerfectionRange == PerfectionRangeMode.CurrentTier))
            Settings.PerfectionRange = PerfectionRangeMode.CurrentTier;
        ImGui.SameLine();
        if (ImGui.RadioButton("All valid tiers##PerfectionRange",
                Settings.PerfectionRange == PerfectionRangeMode.AllValidTiers))
            Settings.PerfectionRange = PerfectionRangeMode.AllValidTiers;
        ImGui.SameLine();
        if (ImGui.RadioButton("Item-level reachable##PerfectionRange",
                Settings.PerfectionRange == PerfectionRangeMode.ItemLevelReachable))
            Settings.PerfectionRange = PerfectionRangeMode.ItemLevelReachable;

        ImGui.TextDisabled(Settings.PerfectionRange switch
        {
            PerfectionRangeMode.AllValidTiers =>
                "Scores against the full range available to this base type.",
            PerfectionRangeMode.ItemLevelReachable =>
                "Scores against all tiers this item's item level could roll.",
            _ => "Scores within the modifier's rolled tier."
        });
    }

    private void DrawAppearanceSettings()
    {
        ImGui.Spacing();
        ImGui.SeparatorText("ATTACHED PANEL");
        DrawSlider("UI scale", Settings.PanelScale, "%");
        DrawSlider("Panel width", Settings.PanelWidth, " px");
        DrawSlider("Gap below tooltip", Settings.PanelGap, " px");
        DrawToggle("Full stat colors", Settings.FullStatColors);
        ImGui.TextDisabled("Colors ordinary modifier names as well as their accent and roll line.");

        ImGui.SeparatorText("ITEM CHECK");
        DrawSlider("Green check size", Settings.MarkerSize, " px");
        ImGui.Spacing();
        ImGui.TextDisabled("The minimal charcoal presentation is designed to blend directly into the native item tooltip.");
    }

    private void DrawCheckRuleSettings()
    {
        ImGui.Spacing();
        ImGui.Text("Rule set");
        if (ImGui.RadioButton("Campaign IFL##QualificationRules",
                Settings.QualificationRules == QualificationRulesMode.Campaign))
            Settings.QualificationRules = QualificationRulesMode.Campaign;
        ImGui.SameLine();
        if (ImGui.RadioButton("Custom##QualificationRules",
                Settings.QualificationRules == QualificationRulesMode.Custom))
            Settings.QualificationRules = QualificationRulesMode.Custom;

        ImGui.TextDisabled(Settings.QualificationRules ==
                           QualificationRulesMode.Campaign
            ? "One slot-specific acceptable-good baseline per campaign stage."
            : "Your original editable match-count profiles.");
        ImGui.Spacing();

        if (Settings.QualificationRules == QualificationRulesMode.Campaign)
        {
            DrawCampaignRuleSettings();
            return;
        }

        if (Settings.Profiles == null || Settings.Profiles.Count == 0)
        {
            Settings.Profiles = QualificationProfile.CreateDefaults();
            _selectedProfileIndex = 0;
        }

        _selectedProfileIndex = Math.Clamp(_selectedProfileIndex, 0,
            Settings.Profiles.Count - 1);

        var available = ImGui.GetContentRegionAvail();
        var listWidth = Math.Min(155f, Math.Max(120f, available.X * .28f));
        ImGui.BeginChild("LootLens2_ProfileList", new Vector2(listWidth, 480f),
            ImGuiChildFlags.Border);
        for (var index = 0; index < Settings.Profiles.Count; index++)
        {
            var profile = Settings.Profiles[index];
            var selected = index == _selectedProfileIndex;
            if (selected)
            {
                ImGui.PushStyleColor(ImGuiCol.Button,
                    new Vector4(.39f, .28f, .09f, 1f));
                ImGui.PushStyleColor(ImGuiCol.ButtonHovered,
                    new Vector4(.50f, .36f, .12f, 1f));
            }

            if (ImGui.Button(profile.Name + "##Profile" + index,
                    new Vector2(-1f, 27f)))
                _selectedProfileIndex = index;

            if (selected)
                ImGui.PopStyleColor(2);
        }
        ImGui.EndChild();

        ImGui.SameLine();
        ImGui.BeginChild("LootLens2_ProfileEditor", new Vector2(0f, 480f),
            ImGuiChildFlags.Border);
        DrawProfileEditor(Settings.Profiles[_selectedProfileIndex]);
        ImGui.EndChild();
    }

    private void DrawProfileEditor(QualificationProfile profile)
    {
        ImGui.TextColored(new Vector4(.88f, .71f, .30f, 1f),
            profile.Name.ToUpperInvariant());
        ImGui.TextDisabled("0 disables a threshold");
        ImGui.Spacing();

        var enabled = profile.Enabled;
        if (ImGui.Checkbox("Profile enabled", ref enabled))
            profile.Enabled = enabled;

        var required = profile.RequiredMatches;
        if (ImGui.SliderInt("Required matches", ref required, 1, 6))
            profile.RequiredMatches = required;

        var mandatoryLabel = CampaignIfl.MandatoryLabel(profile.MandatoryRule);
        if (ImGui.BeginCombo("Mandatory condition",
                string.IsNullOrEmpty(mandatoryLabel) ? "None" : mandatoryLabel))
        {
            foreach (var rule in Enum.GetValues<MandatoryQualificationRule>())
            {
                var label = CampaignIfl.MandatoryLabel(rule);
                if (string.IsNullOrEmpty(label))
                    label = "None";
                if (ImGui.Selectable(label, profile.MandatoryRule == rule))
                    profile.MandatoryRule = rule;
            }
            ImGui.EndCombo();
        }

        ImGui.SeparatorText("THRESHOLDS");

        if (profile.Kind is RuleProfileKind.Armour or RuleProfileKind.Boots or
            RuleProfileKind.Jewellery or RuleProfileKind.Quiver)
        {
            profile.MinimumLife = DrawThreshold("Minimum Life", profile.MinimumLife, 0, 300);
            profile.MinimumSpirit = DrawThreshold("Minimum Spirit", profile.MinimumSpirit, 0, 100);
            profile.MinimumTotalElementalResistance = DrawThreshold("Total Elemental Resistance", profile.MinimumTotalElementalResistance, 0, 240, "%");
            profile.MinimumChaosResistance = DrawThreshold("Chaos Resistance", profile.MinimumChaosResistance, 0, 80, "%");
            profile.MinimumAttributes = DrawThreshold("Best Attribute", profile.MinimumAttributes, 0, 100);
            profile.MinimumEnergyShieldMods = DrawThreshold("Energy Shield Modifiers", profile.MinimumEnergyShieldMods, 0, 3);
        }

        if (profile.Kind is RuleProfileKind.Armour or RuleProfileKind.Boots)
        {
            profile.MinimumDefencePercent = DrawThreshold("Best Local Defence Increase", profile.MinimumDefencePercent, 0, 250, "%");
            profile.MinimumEvasionEnergyShieldMods = DrawThreshold("Evasion + ES Modifiers", profile.MinimumEvasionEnergyShieldMods, 0, 3);
        }

        if (profile.Kind == RuleProfileKind.Boots)
            profile.MinimumMovementSpeed = DrawThreshold("Movement Speed", profile.MinimumMovementSpeed, 0, 40, "%");

        if (profile.Kind is RuleProfileKind.Quiver or RuleProfileKind.MartialWeapon ||
            string.Equals(profile.Name, "Gloves", StringComparison.OrdinalIgnoreCase))
            profile.MinimumAttackSpeed = DrawThreshold("Attack Speed", profile.MinimumAttackSpeed, 0, 30, "%");

        if (profile.Kind is RuleProfileKind.Jewellery or RuleProfileKind.Quiver or
            RuleProfileKind.CasterWeapon)
            profile.MinimumSkillLevels = DrawThreshold("Skill Levels",
                profile.MinimumSkillLevels, 0, 5);

        if (profile.Kind == RuleProfileKind.MartialWeapon)
        {
            profile.MinimumPhysicalDamagePercent = DrawThreshold("Increased Physical Damage", profile.MinimumPhysicalDamagePercent, 0, 250, "%");
            profile.MinimumPhysicalDps = DrawThreshold("Physical DPS", profile.MinimumPhysicalDps, 0, 800);
            profile.MinimumTotalDps = DrawThreshold("Total DPS", profile.MinimumTotalDps, 0, 1000);
            profile.MinimumCriticalChance = DrawThreshold("Critical Chance",
                profile.MinimumCriticalChance, 0, 10, "%");
            profile.MinimumAddedDamageMods = DrawThreshold("Added Damage Mods",
                profile.MinimumAddedDamageMods, 0, 3);
        }

        if (profile.Kind == RuleProfileKind.CasterWeapon)
        {
            profile.MinimumSpirit = DrawThreshold("Minimum Spirit", profile.MinimumSpirit, 0, 100);
            profile.MinimumSpellDamage = DrawThreshold("Spell Damage", profile.MinimumSpellDamage, 0, 200, "%");
            profile.MinimumCastSpeed = DrawThreshold("Cast Speed", profile.MinimumCastSpeed, 0, 40, "%");
            profile.MinimumCriticalChance = DrawThreshold("Critical Chance",
                profile.MinimumCriticalChance, 0, 10, "%");
        }

        ImGui.Spacing();
        ImGui.Separator();
        if (ImGui.Button("Reset this profile"))
        {
            var replacement = QualificationProfile.CreateDefaults()
                .FirstOrDefault(x => string.Equals(x.Name, profile.Name,
                    StringComparison.OrdinalIgnoreCase));
            if (replacement != null)
                Settings.Profiles[_selectedProfileIndex] = replacement;
        }
        ImGui.SameLine();
        if (ImGui.Button("Reset all profiles"))
        {
            Settings.Profiles = QualificationProfile.CreateDefaults();
            _selectedProfileIndex = Math.Clamp(_selectedProfileIndex, 0,
                Settings.Profiles.Count - 1);
        }
    }

    private void DrawCampaignRuleSettings()
    {
        ImGui.Text("Campaign focus");
        ImGui.SameLine();
        if (ImGui.RadioButton("Life + Resists##CampaignFocus",
                Settings.CampaignBuildPreset == CampaignBuildPreset.LifeAndResists))
            Settings.CampaignBuildPreset = CampaignBuildPreset.LifeAndResists;
        ImGui.SameLine();
        if (ImGui.RadioButton("Evasion + ES##CampaignFocus",
                Settings.CampaignBuildPreset == CampaignBuildPreset.EvasionAndEnergyShield))
            Settings.CampaignBuildPreset = CampaignBuildPreset.EvasionAndEnergyShield;

        ImGui.TextDisabled(Settings.CampaignBuildPreset ==
                           CampaignBuildPreset.EvasionAndEnergyShield
            ? "Dexterity/Intelligence armour requires an Evasion + ES modifier; jewellery may qualify with ES or an attribute."
            : "Life and elemental-resistance baseline for general campaign gear.");
        ImGui.Spacing();
        ImGui.Text("Campaign stage");
        ImGui.SameLine();
        ImGui.SetNextItemWidth(180f);
        if (ImGui.BeginCombo("##CampaignStage",
                CampaignIfl.SelectionLabel(Settings.CampaignStage)))
        {
            foreach (var stage in Enum.GetValues<CampaignStageSelection>())
            {
                if (ImGui.Selectable(CampaignIfl.SelectionLabel(stage),
                        Settings.CampaignStage == stage))
                    Settings.CampaignStage = stage;
            }
            ImGui.EndCombo();
        }

        var activeStage = GetActiveCampaignStage();
        var playerLevel = GetPlayerLevel();
        var detected = Settings.CampaignStage == CampaignStageSelection.Automatic
            ? playerLevel > 0 ? $"  ·  PLAYER LEVEL {playerLevel}" :
                "  ·  LEVEL NOT DETECTED"
            : string.Empty;
        ImGui.TextColored(new Vector4(.32f, .86f, .48f, 1f),
            $"ACTIVE  ·  {CampaignIfl.PresetLabel(Settings.CampaignBuildPreset)}  ·  " +
            $"{CampaignIfl.Label(activeStage)}  ·  " +
            $"{CampaignIfl.LevelBand(activeStage)}{detected}");
        if (Settings.CampaignStage == CampaignStageSelection.Automatic &&
            playerLevel <= 0)
            ImGui.TextDisabled("Automatic is using Act 1 until the player level is available. Select a stage manually if needed.");

        ImGui.Spacing();
        var profiles = CampaignIfl.GetProfiles(activeStage,
            Settings.CampaignBuildPreset);
        _selectedCampaignProfileIndex = Math.Clamp(
            _selectedCampaignProfileIndex, 0,
            Math.Max(0, profiles.Count - 1));

        var available = ImGui.GetContentRegionAvail();
        var listWidth = Math.Min(155f, Math.Max(120f, available.X * .28f));
        ImGui.BeginChild("LootLens2_CampaignProfileList",
            new Vector2(listWidth, 480f), ImGuiChildFlags.Border);
        for (var index = 0; index < profiles.Count; index++)
        {
            var selected = index == _selectedCampaignProfileIndex;
            if (selected)
            {
                ImGui.PushStyleColor(ImGuiCol.Button,
                    new Vector4(.39f, .28f, .09f, 1f));
                ImGui.PushStyleColor(ImGuiCol.ButtonHovered,
                    new Vector4(.50f, .36f, .12f, 1f));
            }

            if (ImGui.Button(profiles[index].Name + "##CampaignProfile" + index,
                    new Vector2(-1f, 27f)))
                _selectedCampaignProfileIndex = index;
            if (selected)
                ImGui.PopStyleColor(2);
        }
        ImGui.EndChild();

        ImGui.SameLine();
        ImGui.BeginChild("LootLens2_CampaignProfileSummary",
            new Vector2(0f, 480f), ImGuiChildFlags.Border);
        if (profiles.Count == 0)
        {
            ImGui.TextDisabled("No campaign rules are available for this stage.");
        }
        else
        {
            var selectedProfile = profiles.ElementAtOrDefault(
                _selectedCampaignProfileIndex);
            if (selectedProfile == null)
            {
                _selectedCampaignProfileIndex = 0;
                selectedProfile = profiles[0];
            }

            try
            {
                DrawCampaignProfileSummary(selectedProfile);
            }
            catch (Exception exception)
            {
                ImGui.TextColored(new Vector4(.92f, .33f, .29f, 1f),
                    "This rule summary could not be displayed.");
                ImGui.TextWrapped(exception.Message);
            }
        }
        ImGui.EndChild();
    }

    private static void DrawCampaignProfileSummary(QualificationProfile profile)
    {
        ImGui.TextColored(new Vector4(.88f, .71f, .30f, 1f),
            profile.Name.ToUpperInvariant());
        ImGui.Text($"{profile.RequiredMatches} qualifying stats required");
        var mandatory = CampaignIfl.MandatoryLabel(profile.MandatoryRule);
        if (!string.IsNullOrEmpty(mandatory))
            ImGui.TextColored(new Vector4(.32f, .86f, .48f, 1f),
                $"REQUIRED  ·  {mandatory}");
        ImGui.Spacing();
        ImGui.SeparatorText("ACTIVE BASELINE");

        DrawCampaignThreshold("Life", profile.MinimumLife);
        DrawCampaignThreshold("Spirit", profile.MinimumSpirit);
        DrawCampaignThreshold("Total Elemental Resistance",
            profile.MinimumTotalElementalResistance, "%");
        DrawCampaignThreshold("Each Elemental Resistance",
            profile.MinimumElementalResistancePerMod, "%");
        DrawCampaignThreshold("All Elemental Resistance",
            profile.MinimumAllElementalResistance, "%");
        DrawCampaignThreshold("Chaos Resistance",
            profile.MinimumChaosResistance, "%");
        DrawCampaignThreshold("Movement Speed",
            profile.MinimumMovementSpeed, "%");
        DrawCampaignThreshold("Best Local Defence Increase",
            profile.MinimumDefencePercent, "%");
        DrawCampaignThreshold("Flat Armour modifier (any)",
            profile.MinimumFlatArmour);
        DrawCampaignThreshold("Increased Armour modifier (any)",
            profile.MinimumArmourPercent);
        DrawCampaignThreshold("Armour applies to Elemental Damage (any)",
            profile.MinimumArmourAppliesToElementalDamage);
        DrawCampaignThreshold("Energy Shield modifier (any)",
            profile.MinimumEnergyShieldMods);
        DrawCampaignThreshold("Evasion + ES modifier (any)",
            profile.MinimumEvasionEnergyShieldMods);
        DrawCampaignThreshold("Best Attribute", profile.MinimumAttributes);
        DrawCampaignThreshold("Attack Speed", profile.MinimumAttackSpeed, "%");
        DrawCampaignThreshold("Cast Speed", profile.MinimumCastSpeed, "%");
        DrawCampaignThreshold("Spell Damage", profile.MinimumSpellDamage, "%");
        DrawCampaignThreshold("Increased Physical Damage",
            profile.MinimumPhysicalDamagePercent, "%");
        DrawCampaignThreshold("Physical DPS", profile.MinimumPhysicalDps);
        DrawCampaignThreshold("Total DPS", profile.MinimumTotalDps);
        DrawCampaignThreshold("Skill Levels", profile.MinimumSkillLevels);
        DrawCampaignThreshold("Melee Skill Levels",
            profile.MinimumMeleeSkillLevels);
        DrawCampaignThreshold("Critical Chance",
            profile.MinimumCriticalChance, "%");
        DrawCampaignThreshold("Added Damage Mods",
            profile.MinimumAddedDamageMods);

        if (profile.UseCombinedCasterPower)
            ImGui.TextDisabled("Spell damage / skill level counts as one stat.");

        ImGui.Spacing();
        ImGui.TextWrapped("The green check appears only when the required condition passes and the item meets the listed match count.");
    }

    private static void DrawCampaignThreshold(string label, int value,
        string suffix = "")
    {
        if (value <= 0)
            return;

        ImGui.TextColored(new Vector4(.84f, .85f, .88f, 1f),
            $"{value}{suffix}");
        ImGui.SameLine(82f);
        ImGui.TextDisabled(label);
    }

    private static void DrawToggle(string label, ToggleNode node)
    {
        var value = node.Value;
        if (ImGui.Checkbox(label, ref value))
            node.Value = value;
    }

    private static void DrawSlider(string label, RangeNode<int> node,
        string suffix)
    {
        var value = node.Value;
        if (ImGui.SliderInt(label, ref value, node.Min, node.Max,
                "%d" + suffix))
            node.Value = value;
    }

    private static int DrawThreshold(string label, int value,
        int minimum, int maximum, string suffix = "")
    {
        ImGui.PushID(label);
        ImGui.SetNextItemWidth(Math.Max(150f,
            ImGui.GetContentRegionAvail().X * .48f));
        ImGui.SliderInt("##value", ref value, minimum, maximum,
            "%d" + suffix);
        ImGui.SameLine();
        ImGui.TextUnformatted(label);
        ImGui.PopID();
        return value;
    }
}
