using System.Collections.Generic;
using System.Numerics;
using System.Windows.Forms;
using ExileCore2.Shared.Interfaces;
using ExileCore2.Shared.Nodes;

namespace LootLens2;

public sealed class LootLens2Settings : ISettings
{
    public ToggleNode Enable { get; set; } = new(true);

    public ToggleNode ShowAnalyzer { get; set; } = new(true);
    public AnalyzerVisibilityMode AnalyzerVisibility { get; set; } =
        AnalyzerVisibilityMode.Hold;
    public HotkeyNodeV2 AnalyzerKey { get; set; } =
        new HotkeyNodeV2(Keys.F8);
    public PerfectionRangeMode PerfectionRange { get; set; } =
        PerfectionRangeMode.CurrentTier;
    public ToggleNode AnalyzeMagicItems { get; set; } = new(true);
    public ToggleNode AnalyzeRareItems { get; set; } = new(true);
    public ToggleNode AnalyzeUniqueItems { get; set; } = new(true);
    public ToggleNode ShowImplicitModifiers { get; set; } = new(false);
    public ToggleNode ShowOverallPerfection { get; set; } = new(true);
    public ToggleNode FullStatColors { get; set; } = new(false);
    public ToggleNode MinimalMode { get; set; } = new(true);
    public bool MinimalSliders { get; set; } = true;

    public RangeNode<int> PanelScale { get; set; } = new(100, 70, 150);
    public RangeNode<int> PanelWidth { get; set; } = new(500, 340, 700);
    public RangeNode<int> PanelGap { get; set; } = new(4, 0, 20);
    public bool ShowTierDiamonds { get; set; } = true;
    public bool ShowTierDiamondsInInventory { get; set; } = true;
    public bool ShowTierDiamondsInStash { get; set; } = true;
    public bool ShowTierDiamondsInOtherWindows { get; set; } = true;
    public bool ShowTierDiamondsOnEquippedGear { get; set; } = false;
    public RangeNode<int> TierDiamondSize { get; set; } = new(12, 4, 18);
    public Vector4 Tier1DiamondColor { get; set; } = new(.75f, .45f, 1f, 1f);
    public Vector4 Tier2DiamondColor { get; set; } = new(.25f, .60f, 1f, 1f);
    public Vector4 Tier3DiamondColor { get; set; } = new(.30f, .85f, .45f, 1f);

    public int SettingsVersion { get; set; } = 9;
}
