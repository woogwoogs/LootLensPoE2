using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Numerics;
using System.Reflection;
using ExileCore2;
using ExileCore2.PoEMemory;
using ExileCore2.PoEMemory.Components;
using ExileCore2.PoEMemory.Elements;
using ExileCore2.PoEMemory.MemoryObjects;
using ImGuiNET;

namespace LootLens2;

public partial class LootLens2 : BaseSettingsPlugin<LootLens2Settings>
{
    private const float OverlayTextBaseline = 1.625f;

    private ItemAnalyzer _analyzer = null!;
    private ItemDatabase _database = ItemDatabase.Empty;
    private int _settingsSignature;
    private long _cachedHoverAddress;
    private long _nextHoverAnalysisTicks;
    private AnalyzedItem? _cachedHoverAnalysis;

    public override bool Initialise()
    {
        // Keep old enum values readable so saved settings can migrate safely.
        if (Settings.AnalyzerVisibility != AnalyzerVisibilityMode.Always)
            Settings.AnalyzerVisibility = AnalyzerVisibilityMode.Hold;
        if (Settings.PerfectionRange == PerfectionRangeMode.ItemLevelReachable)
            Settings.PerfectionRange = PerfectionRangeMode.AllValidTiers;
        if (Settings.SettingsVersion < 9)
        {
            if (Settings.TierDiamondSize.Value == 8) Settings.TierDiamondSize.Value = 12;
            if (Settings.Tier1DiamondColor == new Vector4(1f, .78f, .22f, 1f))
                Settings.Tier1DiamondColor = new Vector4(.75f, .45f, 1f, 1f);
            if (Settings.Tier2DiamondColor == new Vector4(.25f, .85f, 1f, 1f))
                Settings.Tier2DiamondColor = new Vector4(.25f, .60f, 1f, 1f);
            if (Settings.Tier3DiamondColor == new Vector4(.75f, .45f, 1f, 1f))
                Settings.Tier3DiamondColor = new Vector4(.30f, .85f, .45f, 1f);
            Settings.SettingsVersion = 9;
        }
        _database = ItemDatabase.Load(DirectoryFullName);
        _analyzer = new ItemAnalyzer(this, _database);
        _settingsSignature = ComputeSettingsSignature();
        return true;
    }

    public override void Tick()
    {
        var signature = ComputeSettingsSignature();
        if (signature == _settingsSignature)
            return;

        _settingsSignature = signature;
        _analyzer.ClearCaches();
        _cachedHoverAddress = 0;
        _nextHoverAnalysisTicks = 0;
        _cachedHoverAnalysis = null;
    }

    public override void Render()
    {
        if (!Settings.Enable.Value)
            return;

        try
        {
            DrawInventoryTierDiamonds();

            var hover = GameController.Game.IngameState.UIHover?.AsObject<HoverItemIcon>();
            if (hover == null || !hover.IsValid)
                return;

            DrawHoveredTierDiamonds(hover);


            if (!Settings.ShowAnalyzer.Value)
                return;

            if (!ShouldShowAnalyzer())
                return;

            var item = hover.Item;
            var tooltip = hover.Tooltip;
            if (item == null || !item.IsValid || tooltip == null || !tooltip.IsVisible)
                return;

            var tooltipRect = tooltip.GetClientRect();
            if (tooltipRect.Width <= 0 || tooltipRect.Height <= 0)
                return;

            var analysis = GetHoverAnalysis(item, tooltip);
            if (analysis == null || analysis.Mods.Count == 0)
                return;

            DrawAnalyzerPanel(tooltipRect, analysis);
        }
        catch
        {
            // UI memory is volatile while panels close. A transient invalid
            // element should skip one frame, never disable the overlay.
        }
    }

    private bool ShouldShowAnalyzer() => Settings.AnalyzerVisibility switch
    {
        AnalyzerVisibilityMode.Always => true,
        _ => Settings.AnalyzerKey.IsPressed()
    };

    private AnalyzedItem? GetHoverAnalysis(Entity item, Element tooltip)
    {
        var address = unchecked((long)item.Address);
        var now = DateTime.UtcNow.Ticks;
        if (_cachedHoverAnalysis != null && _cachedHoverAddress == address &&
            now < _nextHoverAnalysisTicks)
            return _cachedHoverAnalysis;

        _cachedHoverAddress = address;
        _cachedHoverAnalysis = _analyzer.Analyze(item, tooltip);
        _nextHoverAnalysisTicks = now + TimeSpan.TicksPerMillisecond * 100;
        return _cachedHoverAnalysis;
    }

    private void DrawModifierRow(ImDrawListPtr draw, ImFontPtr font,
        float baseFont, float x, float rowTop, float width, float rowHeight,
        float scale, AnalyzedMod mod, uint subdued)
    {
        var statColor = GetStatColor(mod.Text);
        var specialColor = mod.IsHidden ? Pack(192, 128, 67, 255) :
            mod.IsUnique ? Pack(196, 104, 46, 255) :
            mod.IsImplicit ? Pack(150, 155, 164, 255) :
            mod.IsCrafted ? Pack(83, 194, 208, 255) : statColor;
        var textColor = mod.IsHidden ? Pack(205, 142, 76, 255) :
            mod.IsUnique ? Pack(213, 119, 58, 255) :
            mod.IsCrafted ? Pack(92, 207, 220, 255) :
            mod.IsImplicit ? Pack(166, 170, 179, 255) :
            Settings.FullStatColors.Value ? statColor :
            Pack(218, 220, 224, 255);
        var affixLetterColor = mod.IsHidden || mod.IsUnique || mod.IsCrafted
            ? specialColor
            : Pack(151, 154, 162, 255);
        var textY = rowTop + 5f * scale;
        var badgeY = rowTop + 2f * scale;
        var barY = rowTop + 18f * scale;

        draw.AddText(font, baseFont * .52f * scale,
            new Vector2(x + 14f * scale, rowTop + 6f * scale), affixLetterColor,
            string.IsNullOrEmpty(mod.Affix) ? "·" : mod.Affix);

        var accentX = x + 29f * scale;
        draw.AddLine(new Vector2(accentX, rowTop + 4f * scale),
            new Vector2(accentX, rowTop + rowHeight - 4f * scale),
            statColor, Math.Max(1.25f, 1.35f * scale));

        var badgeText = GetBadgeText(mod);
        var right = x + width - 14f * scale;
        var rollLeft = x + Math.Max(width * .52f, 250f * scale);
        var textLeft = x + 40f * scale;
        var textRight = rollLeft - 14f * scale;
        var text = Ellipsize(mod.Text, Math.Max(70f, textRight - textLeft),
            .66f * scale);
        draw.AddText(font, baseFont * .66f * scale,
            new Vector2(textLeft, textY), textColor, text);

        var labelLeft = right;
        if (!string.IsNullOrEmpty(badgeText))
            labelLeft = DrawModBadge(draw, font, baseFont, right,
                badgeY, scale, mod, badgeText);

        if (mod.Perfection >= 0)
        {
            var percent = $"{mod.Perfection}%";
            var percentColor = GetPerfectionColor(mod.Perfection);
            var percentWidth = TextWidth(percent, .60f * scale);
            var percentRight = labelLeft - 14f * scale;
            var percentLeft = percentRight - percentWidth;
            draw.AddText(font, baseFont * .60f * scale,
                new Vector2(percentLeft, rowTop + 5f * scale),
                percentColor, percent);

            var barLeft = rollLeft;
            var barRight = Math.Max(barLeft + 8f * scale,
                percentLeft - 12f * scale);
            var barWidth = Math.Max(1f, barRight - barLeft);
            var range = CompactRollRange(mod.RangeText);
            if (!string.IsNullOrWhiteSpace(range))
            {
                var rangeScale = .42f * scale;
                var compactRange = Ellipsize(range, barWidth, rangeScale);
                var rangeWidth = TextWidth(compactRange, rangeScale);
                draw.AddText(font, baseFont * rangeScale,
                    new Vector2(barLeft + (barWidth - rangeWidth) * .5f,
                        rowTop), Pack(166, 170, 179, 255), compactRange);
            }
            draw.AddLine(new Vector2(barLeft, barY),
                new Vector2(barRight, barY), Pack(88, 91, 97, 220),
                Math.Max(1.1f, scale));
            var markerX = barLeft + barWidth * mod.Perfection / 100f;
            draw.AddLine(new Vector2(barLeft, barY),
                new Vector2(markerX, barY), percentColor, Math.Max(1.1f, scale));
            draw.AddCircleFilled(new Vector2(markerX, barY), 2.8f * scale,
                percentColor);
        }
        else if (!mod.IsHidden)
        {
            var fixedText = "FIXED";
            var fixedScale = .48f * scale;
            var fixedWidth = TextWidth(fixedText, fixedScale);
            var fixedRight = labelLeft - 14f * scale;
            draw.AddText(font, baseFont * fixedScale,
                new Vector2(fixedRight - fixedWidth, rowTop + 6f * scale),
                subdued, fixedText);
        }
    }

    private static float DrawModBadge(ImDrawListPtr draw, ImFontPtr font,
        float baseFont, float right, float y, float scale, AnalyzedMod mod,
        string badgeText)
    {
        var color = GetBadgeColor(mod);
        if (IsNumberedTierBadge(mod))
        {
            var label = mod.TotalTiers > 0
                ? $"T{mod.Tier}/{mod.TotalTiers}"
                : $"T{mod.Tier}";
            var badgeWidth = 50f * scale;
            var badgeHeight = 20f * scale;
            var left = right - badgeWidth;
            draw.AddRect(new Vector2(left, y),
                new Vector2(right, y + badgeHeight), color, 3f * scale,
                ImDrawFlags.None, Math.Max(1f, scale));
            var tierLabelScale = .52f * scale;
            var tierLabelWidth = TextWidth(label, tierLabelScale);
            draw.AddText(font, baseFont * tierLabelScale,
                new Vector2(left + (badgeWidth - tierLabelWidth) * .5f,
                    y + 3f * scale), Pack(186, 189, 196, 255), label);
            return left;
        }

        var labelScale = .48f * scale;
        var labelWidth = TextWidth(badgeText, labelScale);
        var labelLeft = right - labelWidth;
        draw.AddText(font, baseFont * labelScale,
            new Vector2(labelLeft, y + 3f * scale), color, badgeText);
        return labelLeft;
    }

    private static void DrawPerfectionFooter(ImDrawListPtr draw, ImFontPtr font,
        float baseFont, float x, float y, float width, float scale,
        AnalyzedItem analysis, uint subdued)
    {
        draw.AddText(font, baseFont * .58f * scale,
            new Vector2(x + 14f * scale, y + 5f * scale), subdued,
            "ITEM PERFECTION");

        var score = $"{analysis.OverallPerfection}%";
        var scoreColor = GetPerfectionColor(analysis.OverallPerfection);
        var scoreWidth = TextWidth(score, .66f * scale);
        draw.AddText(font, baseFont * .66f * scale,
            new Vector2(x + width - 14f * scale - scoreWidth,
                y + 4f * scale), scoreColor, score);

        var barLeft = x + 14f * scale;
        var barRight = x + width - 14f * scale;
        var barTop = y + 24f * scale;
        draw.AddLine(new Vector2(barLeft, barTop),
            new Vector2(barRight, barTop), Pack(112, 115, 122, 205),
            Math.Max(1f, scale));
        var markerX = barLeft + (barRight - barLeft) *
            analysis.OverallPerfection / 100f;
        draw.AddLine(new Vector2(barLeft, barTop),
            new Vector2(markerX, barTop), scoreColor, Math.Max(1f, scale));
        draw.AddCircleFilled(new Vector2(markerX, barTop), 2.4f * scale,
            scoreColor);

        var detail = $"{analysis.VariableRollCount} ROLLS  ·  " +
                     $"{analysis.VariableModCount} MODS";
        draw.AddText(font, baseFont * .48f * scale,
            new Vector2(barLeft, y + 33f * scale), subdued, detail);
    }

    private static int GetModifierDisplayOrder(AnalyzedMod mod)
    {
        if (mod.IsImplicit) return 0;
        if (string.Equals(mod.Affix, "P", StringComparison.OrdinalIgnoreCase)) return 1;
        if (string.Equals(mod.Affix, "S", StringComparison.OrdinalIgnoreCase)) return 2;
        if (mod.IsHidden) return 4;
        return 3;
    }

    private static string CompactRollRange(string rangeText)
    {
        if (string.IsNullOrWhiteSpace(rangeText))
            return string.Empty;

        return rangeText.Trim()
            .Replace("POWER ", string.Empty, StringComparison.OrdinalIgnoreCase)
            .Replace("MIN ", string.Empty, StringComparison.OrdinalIgnoreCase)
            .Replace("MAX ", string.Empty, StringComparison.OrdinalIgnoreCase);
    }

    private static void DrawCardBackground(ImDrawListPtr draw, Vector2 topLeft,
        Vector2 bottomRight, float scale)
    {
        draw.AddRectFilled(topLeft, bottomRight, Pack(4, 5, 7, 248),
            1f * scale);
        draw.AddRect(topLeft, bottomRight, Pack(91, 85, 68, 155),
            1f * scale, ImDrawFlags.None, Math.Max(1f, scale));
    }

    private static void DrawItemSummaryStrip(ImDrawListPtr draw,
        ImFontPtr font, float baseFont, float x, float y, float width,
        float scale, AnalyzedItem item, uint subdued)
    {
        var metrics = BuildSummaryMetrics(item);
        var metricWidth = 56f * scale;
        var metricGap = 8f * scale;
        var metricsWidth = metrics.Count == 0
            ? 0f
            : metrics.Count * metricWidth + (metrics.Count - 1) * metricGap;
        var left = x + 14f * scale;
        var right = x + width - 14f * scale;
        var textRight = metrics.Count == 0
            ? right
            : right - metricsWidth - 18f * scale;

        var titleScale = .61f * scale;
        var subtitleScale = .46f * scale;
        var title = Ellipsize(item.Name.ToUpperInvariant(),
            Math.Max(70f * scale, textRight - left), titleScale);
        draw.AddText(font, baseFont * titleScale,
            new Vector2(left, y + 1f * scale), GetItemRarityColor(item.Rarity),
            title);

        var subtitleParts = new List<string>();
        if (!string.IsNullOrWhiteSpace(item.BaseType) &&
            !string.Equals(item.BaseType, item.Name,
                StringComparison.OrdinalIgnoreCase))
            subtitleParts.Add(item.BaseType.ToUpperInvariant());
        if (!string.IsNullOrWhiteSpace(item.ItemClassName) &&
            !subtitleParts.Contains(item.ItemClassName,
                StringComparer.OrdinalIgnoreCase))
            subtitleParts.Add(item.ItemClassName);
        if (string.Equals(item.Rarity, "Unique", StringComparison.OrdinalIgnoreCase))
            subtitleParts.Add($"{(string.IsNullOrWhiteSpace(item.UniqueDropTier) ? "UNKNOWN" : item.UniqueDropTier)} UNIQUE");

        var subtitle = Ellipsize(string.Join("  ·  ", subtitleParts),
            Math.Max(70f * scale, textRight - left), subtitleScale);
        draw.AddText(font, baseFont * subtitleScale,
            new Vector2(left, y + 18f * scale), subdued, subtitle);

        var metricX = right - metricsWidth;
        foreach (var metric in metrics)
        {
            DrawSummaryMetric(draw, font, baseFont, metricX, y, metricWidth,
                scale, metric.Label, metric.Value, metric.Color, subdued);
            metricX += metricWidth + metricGap;
        }

        draw.AddLine(new Vector2(left, y + 31f * scale),
            new Vector2(right, y + 31f * scale), Pack(85, 87, 92, 110),
            Math.Max(1f, scale * .65f));
    }

    private static List<(string Label, string Value, uint Color)>
        BuildSummaryMetrics(AnalyzedItem item)
    {
        var metrics = new List<(string Label, string Value, uint Color)>();
        if (item.WeaponDps.IsWeapon)
        {
            metrics.Add(("PDPS", item.WeaponDps.Physical.ToString("0.0"),
                Pack(215, 192, 156, 255)));
            metrics.Add(("EDPS", item.WeaponDps.Elemental.ToString("0.0"),
                Pack(112, 165, 235, 255)));
            metrics.Add(("DPS", item.WeaponDps.Total.ToString("0.0"),
                Pack(231, 233, 238, 255)));
            return metrics;
        }

        if (item.DefenceTotals.Armour > 0)
            metrics.Add(("ARM", item.DefenceTotals.Armour.ToString(),
                Pack(203, 184, 151, 255)));
        if (item.DefenceTotals.Evasion > 0)
            metrics.Add(("EVA", item.DefenceTotals.Evasion.ToString(),
                Pack(83, 215, 125, 255)));
        if (item.DefenceTotals.EnergyShield > 0)
            metrics.Add(("ES", item.DefenceTotals.EnergyShield.ToString(),
                Pack(112, 165, 235, 255)));
        if (item.DefenceTotals.RunicWard > 0)
            metrics.Add(("WARD", item.DefenceTotals.RunicWard.ToString(),
                Pack(83, 194, 208, 255)));

        if (metrics.Count == 0)
            metrics.Add(("ILVL", item.ItemLevel.ToString(),
                Pack(201, 204, 210, 255)));

        return metrics;
    }

    private static void DrawSummaryMetric(ImDrawListPtr draw, ImFontPtr font,
        float baseFont, float x, float y, float width, float scale,
        string label, string value, uint color, uint subdued)
    {
        var labelScale = .40f * scale;
        var valueScale = .62f * scale;
        var labelWidth = TextWidth(label, labelScale);
        var valueWidth = TextWidth(value, valueScale);
        draw.AddText(font, baseFont * labelScale,
            new Vector2(x + (width - labelWidth) * .5f, y + 1f * scale),
            subdued, label);
        draw.AddText(font, baseFont * valueScale,
            new Vector2(x + (width - valueWidth) * .5f, y + 15f * scale),
            color, value);
    }

    private static uint GetItemRarityColor(string rarity)
    {
        if (string.Equals(rarity, "Rare", StringComparison.OrdinalIgnoreCase))
            return Pack(255, 222, 84, 255);
        if (string.Equals(rarity, "Magic", StringComparison.OrdinalIgnoreCase))
            return Pack(136, 136, 255, 255);
        if (string.Equals(rarity, "Unique", StringComparison.OrdinalIgnoreCase))
            return Pack(196, 104, 46, 255);
        return Pack(230, 232, 236, 255);
    }

    private static string GetBadgeText(AnalyzedMod mod)
    {
        if (mod.IsHidden) return "HIDDEN";
        if (mod.IsImplicit) return "IMPLICIT";
        if (mod.IsCrafted) return "CRAFTED";
        if (mod.IsUnique) return "UNIQUE";
        return mod.Tier > 0
            ? mod.TotalTiers > 0 ? $"T{mod.Tier}/{mod.TotalTiers}" : $"T{mod.Tier}"
            : string.Empty;
    }

    private static bool IsNumberedTierBadge(AnalyzedMod mod) =>
        !mod.IsHidden && !mod.IsImplicit && !mod.IsCrafted && !mod.IsUnique &&
        mod.Tier > 0;

    private static uint GetBadgeColor(AnalyzedMod mod)
    {
        if (mod.IsHidden) return Pack(192, 128, 67, 255);
        if (mod.IsUnique) return Pack(203, 111, 55, 255);
        if (mod.IsImplicit) return Pack(153, 158, 168, 255);
        if (mod.IsCrafted) return Pack(79, 197, 212, 255);
        return mod.Tier switch
        {
            1 => Pack(227, 184, 66, 255),
            2 => Pack(76, 205, 124, 255),
            3 => Pack(52, 181, 195, 255),
            4 => Pack(118, 138, 157, 255),
            _ => Pack(112, 116, 125, 255)
        };
    }

    private static uint GetStatColor(string text)
    {
        var value = (text ?? string.Empty).ToLowerInvariant();

        // Exact mechanics and the primary stat being modified come first.
        if (value.Contains("leech"))
            return Pack(169, 58, 78, 255);
        if (value.Contains("life") &&
            (value.Contains("regen") || value.Contains("recover") ||
             value.Contains("recoup")))
            return Pack(229, 112, 125, 255);
        if (value.Contains("mana") &&
            (value.Contains("regen") || value.Contains("recover") ||
             value.Contains("recoup")))
            return Pack(123, 153, 235, 255);
        if (value.Contains("movement speed"))
            return Pack(78, 226, 112, 255);
        if (value.Contains("attack speed"))
            return Pack(228, 139, 65, 255);
        if (value.Contains("accuracy"))
            return Pack(205, 162, 108, 255);
        if (value.Contains("cast speed"))
            return Pack(124, 139, 238, 255);
        if (value.Contains("critical"))
            return Pack(207, 91, 210, 255);
        if (value.Contains("skill level") || value.Contains("gem level") ||
            value.Contains("level of all") && value.Contains("skill"))
            return Pack(69, 215, 225, 255);
        if (value.Contains("projectile"))
            return Pack(73, 169, 217, 255);
        if (value.Contains("armour") &&
            value.Contains("applies to elemental damage"))
            return Pack(183, 173, 146, 255);
        if (value.Contains("block"))
            return Pack(143, 158, 176, 255);
        if (value.Contains("stun"))
            return Pack(174, 161, 144, 255);
        if (value.Contains("rarity") || value.Contains("quantity"))
            return Pack(238, 191, 45, 255);
        if (value.Contains("flask"))
            return Pack(66, 186, 142, 255);
        if (value.Contains("reservation"))
            return Pack(70, 163, 190, 255);
        if (value.Contains("area of effect") || value.Contains(" aoe"))
            return Pack(186, 137, 218, 255);
        if (value.Contains("duration"))
            return Pack(166, 146, 207, 255);
        if (value.Contains("requirement") && value.Contains("reduced"))
            return Pack(164, 168, 178, 255);
        if (value.Contains("socket"))
            return Pack(190, 193, 200, 255);
        if (value.StartsWith("charge", StringComparison.Ordinal) ||
            value.Contains(" charge", StringComparison.Ordinal))
            return Pack(224, 202, 125, 255);

        // Ailment-specific wording inherits its damage type. Generic ailment
        // and damage-over-time mechanics keep their own related shades.
        if (value.Contains("ignite") || value.Contains("burning"))
            return Pack(255, 82, 28, 255);
        if (value.Contains("freeze") || value.Contains("chill"))
            return Pack(72, 176, 235, 255);
        if (value.Contains("shock"))
            return Pack(235, 209, 61, 255);
        if (value.Contains("bleed"))
            return Pack(187, 67, 62, 255);
        if (value.Contains("ailment"))
            return Pack(204, 100, 180, 255);
        if (value.Contains("damage over time") ||
            value.Contains("damage per second"))
            return Pack(152, 65, 105, 255);
        if (value.Contains("thorns"))
            return Pack(202, 145, 78, 255);
        if (value.Contains("minion"))
            return Pack(171, 100, 207, 255);

        // Resources, specific damage types, and defensive families.
        if (value.Contains("life"))
            return Pack(220, 83, 94, 255);
        if (value.Contains("mana"))
            return Pack(105, 136, 232, 255);
        if (value.Contains("spirit"))
            return Pack(66, 204, 190, 255);
        if (value.Contains("fire"))
            return Pack(255, 82, 28, 255);
        if (value.Contains("cold"))
            return Pack(72, 176, 235, 255);
        if (value.Contains("lightning"))
            return Pack(235, 209, 61, 255);
        if (value.Contains("chaos") || value.Contains("poison"))
            return Pack(160, 82, 200, 255);
        if (value.Contains("resistance") || value.Contains(" res"))
            return Pack(205, 185, 105, 255);
        if (value.Contains("elemental damage") ||
            value.Contains("elemental penetration") ||
            value.Contains("elemental exposure"))
            return Pack(219, 174, 74, 255);
        if (value.Contains("energy shield") || value.Contains("runic ward") ||
            value.Contains(" es "))
            return Pack(126, 211, 222, 255);
        if (value.Contains("evasion"))
            return Pack(75, 164, 99, 255);
        if (value.Contains("armour") || value.Contains("defence"))
            return Pack(183, 173, 146, 255);

        // Attribute and broad offensive families.
        if (value.Contains("strength"))
            return Pack(230, 91, 104, 255);
        if (value.Contains("dexterity"))
            return Pack(76, 185, 109, 255);
        if (value.Contains("intelligence"))
            return Pack(101, 143, 232, 255);
        if (value.Contains("attribute"))
            return Pack(201, 176, 103, 255);
        if (value.Contains("physical"))
            return Pack(205, 171, 125, 255);
        if (value.Contains("spell") || value.Contains("cast"))
            return Pack(124, 139, 238, 255);
        if (value.Contains("attack"))
            return Pack(205, 171, 125, 255);

        // Neutral utility and unfamiliar modifiers should stay understated.
        return Pack(176, 180, 188, 255);
    }

    private static uint GetPerfectionColor(int percentage)
    {
        if (percentage >= 85) return Pack(83, 225, 125, 255);
        if (percentage >= 65) return Pack(158, 214, 88, 255);
        if (percentage >= 40) return Pack(230, 184, 72, 255);
        return Pack(220, 91, 78, 255);
    }

    private static string Ellipsize(string text, float maximumWidth, float sizeScale)
    {
        text ??= string.Empty;
        if (maximumWidth <= 0 || TextWidth(text, sizeScale) <= maximumWidth)
            return text;

        const string ellipsis = "…";
        var low = 0;
        var high = text.Length;
        while (low < high)
        {
            var middle = (low + high + 1) / 2;
            if (TextWidth(text[..middle] + ellipsis, sizeScale) <= maximumWidth)
                low = middle;
            else
                high = middle - 1;
        }
        return text[..low].TrimEnd() + ellipsis;
    }

    private static float TextWidth(string text, float sizeScale) =>
        ImGui.CalcTextSize(text ?? string.Empty).X * sizeScale *
        OverlayTextBaseline;

    private static int ReadPositiveInt(object? source, params string[] names)
    {
        if (source == null)
            return 0;

        foreach (var name in names)
        {
            try
            {
                var type = source.GetType();
                var property = type.GetProperty(name,
                    BindingFlags.Instance | BindingFlags.Public |
                    BindingFlags.NonPublic);
                var value = property?.GetValue(source);
                if (value == null)
                {
                    var field = type.GetField(name,
                        BindingFlags.Instance | BindingFlags.Public |
                        BindingFlags.NonPublic);
                    value = field?.GetValue(source);
                }

                if (value is IConvertible convertible)
                {
                    var number = convertible.ToInt32(null);
                    if (number > 0)
                        return number;
                }

                var nestedValue = value?.GetType().GetProperty("Value")
                    ?.GetValue(value);
                if (nestedValue is IConvertible nestedConvertible)
                {
                    var number = nestedConvertible.ToInt32(null);
                    if (number > 0)
                        return number;
                }
            }
            catch
            {
            }
        }

        return 0;
    }

    private int ComputeSettingsSignature() => HashCode.Combine(
        Settings.PerfectionRange, Settings.AnalyzeMagicItems.Value,
        Settings.AnalyzeRareItems.Value, Settings.AnalyzeUniqueItems.Value,
        Settings.ShowImplicitModifiers.Value);

    private static uint Pack(byte red, byte green, byte blue, byte alpha) =>
        (uint)(red | green << 8 | blue << 16 | alpha << 24);
}
