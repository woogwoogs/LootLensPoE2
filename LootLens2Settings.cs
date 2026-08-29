using System.Collections.Generic;
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
    public AnalyzerLayoutMode AnalyzerLayout { get; set; } =
        AnalyzerLayoutMode.Compact;
    public HotkeyNodeV2 AnalyzerKey { get; set; } =
        new HotkeyNodeV2(Keys.F8);
    public PerfectionRangeMode PerfectionRange { get; set; } =
        PerfectionRangeMode.CurrentTier;
    public ToggleNode AnalyzeMagicItems { get; set; } = new(true);
    public ToggleNode AnalyzeRareItems { get; set; } = new(true);
    public ToggleNode AnalyzeUniqueItems { get; set; } = new(true);
    public ToggleNode ShowImplicitModifiers { get; set; } = new(false);
    public ToggleNode ShowOverallPerfection { get; set; } = new(true);
    public ToggleNode ShowTierRanges { get; set; } = new(true);
    public ToggleNode FullStatColors { get; set; } = new(false);

    public ToggleNode ShowQualifyingCheck { get; set; } = new(true);
    public ToggleNode CheckMagicItems { get; set; } = new(false);
    public ToggleNode CheckRareItems { get; set; } = new(true);
    public ToggleNode CheckUniqueItems { get; set; } = new(false);
    public ToggleNode ShowQualificationFooter { get; set; } = new(true);
    public QualificationRulesMode QualificationRules { get; set; } =
        QualificationRulesMode.HcCampaign;
    public CampaignStageSelection CampaignStage { get; set; } =
        CampaignStageSelection.Automatic;

    public RangeNode<int> PanelScale { get; set; } = new(100, 70, 150);
    public RangeNode<int> PanelWidth { get; set; } = new(480, 340, 700);
    public RangeNode<int> PanelGap { get; set; } = new(4, 0, 20);
    public RangeNode<int> MarkerSize { get; set; } = new(15, 10, 26);

    public List<QualificationProfile> Profiles { get; set; } = QualificationProfile.CreateDefaults();
    public int SettingsVersion { get; set; } = 4;
}
