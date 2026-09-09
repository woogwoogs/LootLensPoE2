using System;
using System.Linq;
using System.Numerics;
using System.Runtime.InteropServices;
using ExileCore2.Shared.Nodes;
using ImGuiNET;

namespace LootLens2;

public partial class LootLens2
{
    private bool _capturingAnalyzerKey;

    [DllImport("user32.dll")]
    private static extern short GetAsyncKeyState(int virtualKey);

    public override void DrawSettings()
    {
        ImGui.TextColored(new Vector4(.86f, .66f, .22f, 1f), "LOOTLENS 2");
        ImGui.SameLine();
        ImGui.TextDisabled("Analyzer & tier diamonds");
        DrawToggle("Enabled", Settings.Enable);

        if (!ImGui.BeginTabBar("LootLens2_SettingsTabs"))
            return;

        if (ImGui.BeginTabItem("Analyzer"))
        {
            DrawGeneralSettings();
            DrawAppearanceSettings();
            ImGui.EndTabItem();
        }

        if (ImGui.BeginTabItem("Tier diamonds"))
        {
            DrawDiamondSettings();
            ImGui.EndTabItem();
        }
        ImGui.EndTabBar();
    }

    private void DrawGeneralSettings()
    {
        ImGui.Spacing();
        ImGui.SeparatorText("ANALYZER");
        DrawToggle("Show attached item analyzer", Settings.ShowAnalyzer);
        DrawAnalyzerVisibilitySettings();
        DrawToggle("Magic", Settings.AnalyzeMagicItems);
        ImGui.SameLine();
        DrawToggle("Rare", Settings.AnalyzeRareItems);
        ImGui.SameLine();
        DrawToggle("Unique", Settings.AnalyzeUniqueItems);
        DrawToggle("Include implicit modifiers", Settings.ShowImplicitModifiers);
        DrawToggle("Colour modifier text", Settings.FullStatColors);

        DrawPerfectionRangeSettings();
        if (!_database.IsLoaded) ImGui.TextWrapped("Range database unavailable; using game data fallback.");
        DrawToggle("Show overall unique-item perfection", Settings.ShowOverallPerfection);

    }

    private void DrawAnalyzerVisibilitySettings()
    {
        ImGui.Text("Analyzer visibility");
        ImGui.SameLine();
        if (ImGui.RadioButton("Hold##AnalyzerVisibility",
                Settings.AnalyzerVisibility == AnalyzerVisibilityMode.Hold))
            Settings.AnalyzerVisibility = AnalyzerVisibilityMode.Hold;
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

    private void DrawPerfectionRangeSettings()
    {
        var mode = Settings.PerfectionRange switch
        {
            PerfectionRangeMode.AllValidTiers => "All valid tiers",
            _ => "Current tier"
        };
        ImGui.SetNextItemWidth(210f);
        if (ImGui.BeginCombo("Roll range", mode))
        {
            foreach (var value in new[] { PerfectionRangeMode.CurrentTier, PerfectionRangeMode.AllValidTiers })
            {
                var label = value switch
                {
                    PerfectionRangeMode.AllValidTiers => "All valid tiers",
                            _ => "Current tier"
                };
                if (ImGui.Selectable(label, Settings.PerfectionRange == value)) Settings.PerfectionRange = value;
            }
            ImGui.EndCombo();
        }
        ImGui.TextDisabled(Settings.PerfectionRange switch
        {
            PerfectionRangeMode.AllValidTiers =>
                "Scores against the full range available to this base type.",
            _ => "Scores within the modifier's rolled tier."
        });
    }

    private void DrawAppearanceSettings()
    {
        ImGui.SeparatorText("ATTACHED PANEL");
        ImGui.TextUnformatted("Roll display");
        {
            if (ImGui.RadioButton("Range##Minimal", !Settings.MinimalSliders))
                Settings.MinimalSliders = false;
            ImGui.SameLine();
            if (ImGui.RadioButton("Sliders##Minimal", Settings.MinimalSliders))
                Settings.MinimalSliders = true;
            ImGui.TextDisabled("Hold Shift over an item to read full modifiers and ranges.");
        }
        DrawSlider("UI scale", Settings.PanelScale, "%");
        DrawSlider("Fixed panel width", Settings.PanelWidth, " px");
        DrawSlider("Gap below tooltip", Settings.PanelGap, " px");

    }

    private void DrawDiamondSettings()
    {
        ImGui.SeparatorText("INVENTORY TIER DIAMONDS");
        var diamonds = Settings.ShowTierDiamonds;
        if (ImGui.Checkbox("Show tier diamonds", ref diamonds))
            Settings.ShowTierDiamonds = diamonds;
        var bag = Settings.ShowTierDiamondsInInventory;
        var stash = Settings.ShowTierDiamondsInStash;
        var other = Settings.ShowTierDiamondsInOtherWindows;
        var equipped = Settings.ShowTierDiamondsOnEquippedGear;
        if (ImGui.Checkbox("Inventory", ref bag)) Settings.ShowTierDiamondsInInventory = bag;
        ImGui.SameLine();
        if (ImGui.Checkbox("Stash", ref stash)) Settings.ShowTierDiamondsInStash = stash;
        if (ImGui.Checkbox("Vendors, ritual and other windows", ref other))
            Settings.ShowTierDiamondsInOtherWindows = other;
        if (ImGui.Checkbox("Equipped gear", ref equipped))
            Settings.ShowTierDiamondsOnEquippedGear = equipped;
        DrawSlider("Diamond size", Settings.TierDiamondSize, " px");
        var t1 = Settings.Tier1DiamondColor;
        var t2 = Settings.Tier2DiamondColor;
        var t3 = Settings.Tier3DiamondColor;
        if (ImGui.ColorEdit4("T1 diamond", ref t1)) Settings.Tier1DiamondColor = t1;
        if (ImGui.ColorEdit4("T2 diamond", ref t2)) Settings.Tier2DiamondColor = t2;
        if (ImGui.ColorEdit4("T3 diamond", ref t3)) Settings.Tier3DiamondColor = t3;
        ImGui.TextDisabled("One diamond per T1/T2/T3 explicit affix. Hybrids count once.");
        ImGui.TextDisabled("Equipped gear is off by default; other item windows are on.");
        ImGui.Spacing();

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
        ImGui.SetNextItemWidth(210f);
        if (ImGui.SliderInt(label, ref value, node.Min, node.Max,
                "%d" + suffix))
            node.Value = value;
    }


}
