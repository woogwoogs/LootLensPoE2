using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text.RegularExpressions;
using ImGuiNET;

namespace LootLens2;

public partial class LootLens2
{
    private void DrawAnalyzerPanel(ExileCore2.Shared.RectangleF tooltipRect,
        AnalyzedItem analysis)
    {
        if (Settings.AnalyzerLayout == AnalyzerLayoutMode.Detailed)
        {
            DrawDetailedAnalyzerPanel(tooltipRect, analysis);
            return;
        }

        DrawCompactAnalyzerPanel(tooltipRect, analysis);
    }

    private void DrawCompactAnalyzerPanel(
        ExileCore2.Shared.RectangleF tooltipRect, AnalyzedItem analysis)
    {
        var scale = Settings.PanelScale.Value / 100f;
        var draw = ImGui.GetForegroundDrawList();
        var font = ImGui.GetFont();
        var baseFont = ImGui.GetFontSize() * OverlayTextBaseline;
        var display = ImGui.GetIO().DisplaySize;
        var displayMods = analysis.Mods
            .Select((mod, index) => (Mod: mod, Index: index))
            .OrderBy(entry => GetModifierDisplayOrder(entry.Mod))
            .ThenBy(entry => entry.Index)
            .Select(entry => entry.Mod)
            .ToList();

        var width = Math.Max(tooltipRect.Width,
            Math.Min(Settings.PanelWidth.Value * scale, display.X - 16f));
        width = Math.Clamp(width, 340f * scale,
            Math.Max(340f * scale, display.X - 16f));

        var topPadding = 3f * scale;
        var metadataHeight = 21f * scale;
        var rowHeight = 22f * scale;
        var uniqueFooter = Settings.ShowOverallPerfection.Value &&
                           analysis.OverallPerfection >= 0
            ? 51f * scale
            : 0f;

        var qualificationCount = analysis.QualificationMatches.Count;
        var qualifierColumns = Math.Min(4, Math.Max(1, qualificationCount));
        var qualifierRows = qualificationCount == 0
            ? 0
            : (int)Math.Ceiling(qualificationCount /
                                (double)qualifierColumns);
        var qualificationFooter = Settings.ShowQualificationFooter.Value &&
                                  analysis.RequiredQualificationMatches > 0
            ? (22f + qualifierRows * 18f) * scale
            : 0f;

        var height = topPadding + metadataHeight +
                     displayMods.Count * rowHeight + uniqueFooter +
                     qualificationFooter + 4f * scale;
        var gap = Settings.PanelGap.Value * scale;
        var x = Math.Clamp(tooltipRect.Left, 8f,
            Math.Max(8f, display.X - width - 8f));
        var belowY = tooltipRect.Bottom + gap;
        var y = belowY + height <= display.Y - 8f
            ? belowY
            : Math.Max(8f, tooltipRect.Top - gap - height);

        var topLeft = new Vector2(x, y);
        var bottomRight = new Vector2(x + width, y + height);
        DrawCompactBackground(draw, topLeft, bottomRight);
        draw.PushClipRect(topLeft + Vector2.One,
            bottomRight - Vector2.One, true);

        var normal = Pack(235, 236, 239, 255);
        var subdued = Pack(145, 149, 158, 255);
        var mutedLine = Pack(105, 107, 112, 125);
        var cy = y + topPadding;

        DrawCompactMetadata(draw, font, baseFont, x, cy, width, scale,
            analysis, subdued, mutedLine);
        cy += metadataHeight;

        foreach (var mod in displayMods)
        {
            DrawCompactModifierRow(draw, font, baseFont, x, cy, width,
                rowHeight, scale, mod, subdued);
            cy += rowHeight;
        }

        if (uniqueFooter > 0f)
        {
            DrawCompactPerfectionFooter(draw, font, baseFont, x, cy, width,
                scale, analysis, subdued, mutedLine);
            cy += uniqueFooter;
        }

        if (qualificationFooter > 0f)
            DrawQualificationFooter(draw, font, baseFont, x, cy, width,
                scale, analysis, normal, subdued, mutedLine);

        draw.PopClipRect();
    }

    private static void DrawCompactBackground(ImDrawListPtr draw,
        Vector2 topLeft, Vector2 bottomRight)
    {
        draw.AddRectFilled(topLeft, bottomRight, Pack(0, 0, 0, 236));
    }

    private static void DrawCompactMetadata(ImDrawListPtr draw,
        ImFontPtr font, float baseFont, float x, float y, float width,
        float scale, AnalyzedItem item, uint subdued, uint mutedLine)
    {
        var left = x + 14f * scale;
        var right = x + width - 14f * scale;
        var cursor = left;
        var textScale = .48f * scale;
        const string separator = "  |  ";
        var modCount = item.Mods.Count(mod =>
            !mod.IsImplicit && !mod.IsHidden);
        var metadata = $"iLvl {item.ItemLevel}  |  {modCount} " +
                       (modCount == 1 ? "Mod" : "Mods");

        draw.AddText(font, baseFont * textScale,
            new Vector2(cursor, y + 3f * scale), subdued, metadata);
        cursor += TextWidth(metadata, textScale);

        foreach (var metric in BuildCompactSummaryMetrics(item))
        {
            var separatorWidth = TextWidth(separator, textScale);
            var label = metric.Label + " ";
            var labelWidth = TextWidth(label, textScale);
            var valueWidth = TextWidth(metric.Value, textScale);
            if (cursor + separatorWidth + labelWidth + valueWidth > right)
                break;

            draw.AddText(font, baseFont * textScale,
                new Vector2(cursor, y + 3f * scale), subdued, separator);
            cursor += separatorWidth;
            draw.AddText(font, baseFont * textScale,
                new Vector2(cursor, y + 3f * scale), subdued, label);
            cursor += labelWidth;
            draw.AddText(font, baseFont * textScale,
                new Vector2(cursor, y + 3f * scale), metric.Color,
                metric.Value);
            cursor += valueWidth;
        }

        draw.AddLine(new Vector2(left, y + 18f * scale),
            new Vector2(right, y + 18f * scale), mutedLine,
            Math.Max(1f, scale * .65f));
    }

    private static List<(string Label, string Value, uint Color)>
        BuildCompactSummaryMetrics(AnalyzedItem item)
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
            metrics.Add(("ARMOUR", item.DefenceTotals.Armour.ToString(),
                Pack(203, 184, 151, 255)));
        if (item.DefenceTotals.Evasion > 0)
            metrics.Add(("EVASION", item.DefenceTotals.Evasion.ToString(),
                Pack(83, 215, 125, 255)));
        if (item.DefenceTotals.EnergyShield > 0)
            metrics.Add(("ES", item.DefenceTotals.EnergyShield.ToString(),
                Pack(112, 165, 235, 255)));
        if (item.DefenceTotals.RunicWard > 0)
            metrics.Add(("WARD", item.DefenceTotals.RunicWard.ToString(),
                Pack(83, 194, 208, 255)));
        return metrics;
    }

    private void DrawCompactModifierRow(ImDrawListPtr draw, ImFontPtr font,
        float baseFont, float x, float rowTop, float width, float rowHeight,
        float scale, AnalyzedMod mod, uint subdued)
    {
        var displayText = ShortenModifierText(mod.Text);
        var statColor = GetStatColor(mod.Text + " " + displayText);
        var specialColor = mod.IsHidden ? Pack(192, 128, 67, 255) :
            mod.IsUnique ? Pack(203, 111, 55, 255) :
            mod.IsImplicit ? Pack(153, 158, 168, 255) :
            mod.IsCrafted ? Pack(79, 197, 212, 255) : statColor;
        var textColor = mod.IsHidden ? Pack(205, 142, 76, 255) :
            mod.IsUnique ? Pack(213, 119, 58, 255) :
            mod.IsCrafted ? Pack(92, 207, 220, 255) :
            mod.IsImplicit ? Pack(166, 170, 179, 255) :
            Settings.FullStatColors.Value ? statColor :
            Pack(218, 220, 224, 255);
        var letterColor = mod.IsHidden || mod.IsUnique || mod.IsCrafted ||
                          mod.IsImplicit
            ? specialColor
            : Pack(151, 154, 162, 255);
        // Source styling stays on the letter/name. The accent and roll line
        // always describe the modifier's actual stat family.
        var accentColor = statColor;
        var uniqueLayout = mod.IsUnique;

        if (!uniqueLayout)
        {
            var affixIndicator = mod.IsCrafted ? "C" :
                mod.IsHidden ? "H" :
                mod.IsImplicit ? "I" :
                string.IsNullOrWhiteSpace(mod.Affix) ? "·" : mod.Affix;
            draw.AddText(font, baseFont * .52f * scale,
                new Vector2(x + 14f * scale, rowTop + 6f * scale),
                letterColor, affixIndicator);
        }

        var accentX = x + (uniqueLayout ? 16f : 29f) * scale;
        draw.AddLine(new Vector2(accentX, rowTop + 4f * scale),
            new Vector2(accentX, rowTop + rowHeight - 4f * scale),
            accentColor, Math.Max(1.25f, 1.35f * scale));

        var right = x + width - 14f * scale;
        var rightLabel = GetCompactRightLabel(mod);
        var rightLabelScale = rightLabel.Length > 5
            ? .42f * scale
            : .53f * scale;
        var rightLabelWidth = TextWidth(rightLabel, rightLabelScale);
        if (!string.IsNullOrEmpty(rightLabel))
        {
            var labelColor = mod.Perfection < 0 && mod.IsUnique
                ? subdued
                : GetBadgeColor(mod);
            draw.AddText(font, baseFont * rightLabelScale,
                new Vector2(right - rightLabelWidth,
                    rowTop + 5f * scale), labelColor, rightLabel);
        }

        var rollRight = string.IsNullOrEmpty(rightLabel)
            ? right
            : right - rightLabelWidth - 14f * scale;
        var rollLeft = x + Math.Max(width * .53f, 190f * scale);
        if (rollRight - rollLeft < 42f * scale)
            rollLeft = rollRight - 42f * scale;

        var textLeft = x + (uniqueLayout ? 27f : 40f) * scale;
        var textRight = rollLeft - 12f * scale;
        draw.AddText(font, baseFont * .66f * scale,
            new Vector2(textLeft, rowTop + 4f * scale), textColor,
            Ellipsize(displayText, Math.Max(70f, textRight - textLeft),
                .66f * scale));

        if (mod.Perfection < 0)
            return;

        var rollY = rowTop + 11f * scale;
        draw.AddLine(new Vector2(rollLeft, rollY),
            new Vector2(rollRight, rollY), Pack(130, 134, 143, 210),
            Math.Max(1.2f, 1.3f * scale));
        var markerX = rollLeft + (rollRight - rollLeft) *
            mod.Perfection / 100f;
        draw.AddLine(new Vector2(rollLeft, rollY),
            new Vector2(markerX, rollY), accentColor,
            Math.Max(1.8f, 2f * scale));
        DrawCompactDiamond(draw, new Vector2(markerX, rollY),
            4.2f * scale, accentColor);
    }

    private static void DrawCompactDiamond(ImDrawListPtr draw,
        Vector2 center, float size, uint color)
    {
        var top = new Vector2(center.X, center.Y - size);
        var right = new Vector2(center.X + size, center.Y);
        var bottom = new Vector2(center.X, center.Y + size);
        var left = new Vector2(center.X - size, center.Y);
        draw.AddTriangleFilled(top, right, bottom, color);
        draw.AddTriangleFilled(top, bottom, left, color);
        var outline = Pack(238, 239, 242, 235);
        draw.AddLine(top, right, outline, 1f);
        draw.AddLine(right, bottom, outline, 1f);
        draw.AddLine(bottom, left, outline, 1f);
        draw.AddLine(left, top, outline, 1f);
    }

    private static string GetCompactRightLabel(AnalyzedMod mod)
    {
        if (mod.IsHidden)
            return "HIDDEN";
        if (mod.IsImplicit)
            return "IMPLICIT";
        if (mod.IsCrafted)
            return string.Empty;
        if (mod.IsUnique)
            return mod.Perfection < 0 ? "FIXED" : string.Empty;
        return mod.Tier > 0
            ? mod.TotalTiers > 0
                ? $"T{mod.Tier}/{mod.TotalTiers}"
                : $"T{mod.Tier}"
            : string.Empty;
    }

    private static void DrawCompactPerfectionFooter(ImDrawListPtr draw,
        ImFontPtr font, float baseFont, float x, float y, float width,
        float scale, AnalyzedItem analysis, uint subdued, uint mutedLine)
    {
        var left = x + 14f * scale;
        var right = x + width - 14f * scale;
        draw.AddLine(new Vector2(left, y + 2f * scale),
            new Vector2(right, y + 2f * scale), mutedLine,
            Math.Max(1f, scale));
        draw.AddText(font, baseFont * .58f * scale,
            new Vector2(left, y + 6f * scale), subdued,
            "ITEM PERFECTION");

        var score = $"{analysis.OverallPerfection}%";
        var scoreColor = GetPerfectionColor(analysis.OverallPerfection);
        var scoreScale = .76f * scale;
        var scoreWidth = TextWidth(score, scoreScale);
        draw.AddText(font, baseFont * scoreScale,
            new Vector2(right - scoreWidth, y + 3f * scale),
            scoreColor, score);

        var barY = y + 26f * scale;
        draw.AddLine(new Vector2(left, barY), new Vector2(right, barY),
            Pack(112, 115, 122, 205), Math.Max(1f, scale));
        var markerX = left + (right - left) *
            analysis.OverallPerfection / 100f;
        draw.AddLine(new Vector2(left, barY), new Vector2(markerX, barY),
            scoreColor, Math.Max(1.5f, 1.7f * scale));
        DrawCompactDiamond(draw, new Vector2(markerX, barY),
            3.8f * scale, scoreColor);

        var rollText = $"{analysis.VariableRollCount} " +
                       (analysis.VariableRollCount == 1 ? "Roll" : "Rolls") +
                       "  |  " +
                       $"{analysis.VariableModCount} Variable " +
                       (analysis.VariableModCount == 1 ? "Mod" : "Mods");
        draw.AddText(font, baseFont * .48f * scale,
            new Vector2(left, y + 36f * scale), subdued, rollText);

        var uniqueTier = string.IsNullOrWhiteSpace(analysis.UniqueDropTier)
            ? "UNKNOWN UNIQUE"
            : $"{analysis.UniqueDropTier.ToUpperInvariant()} UNIQUE";
        var tierScale = .48f * scale;
        var tierWidth = TextWidth(uniqueTier, tierScale);
        draw.AddText(font, baseFont * tierScale,
            new Vector2(right - tierWidth, y + 36f * scale),
            Pack(196, 104, 46, 220), uniqueTier);
    }

    private static string ShortenModifierText(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return string.Empty;

        var clean = Regex.Replace(text.Replace('\r', ' ').Replace('\n', ' '),
            @"\s+", " ").Trim();

        void Rewrite(string pattern, string replacement)
        {
            clean = Regex.Replace(clean, pattern, replacement,
                RegexOptions.IgnoreCase);
        }

        Rewrite(@"^Adds\s+\+?(\d+(?:\.\d+)?)\s+to\s+\+?(\d+(?:\.\d+)?)\s+",
            "+$1-$2 ");
        Rewrite(@"(?<=\d)\s+to\s+(?=[+-]?\d)", "-");
        Rewrite(@"\bPhysical Damage Leeched as Life\b", "Phys Life Leech");
        Rewrite(@"\bPhysical Damage Leeched as Mana\b", "Phys Mana Leech");
        Rewrite(@"\bAll Elemental Resistances\b", "All Ele Res");
        Rewrite(@"\bFire Resistance\b", "Fire Res");
        Rewrite(@"\bCold Resistance\b", "Cold Res");
        Rewrite(@"\bLightning Resistance\b", "Lightning Res");
        Rewrite(@"\bChaos Resistance\b", "Chaos Res");
        Rewrite(@"\bMaximum Life\b", "Life");
        Rewrite(@"\bMaximum Mana\b", "Mana");
        Rewrite(@"\bMaximum Energy Shield\b", "ES");
        Rewrite(@"\bMaximum Stun Threshold\b", "Stun Threshold");
        Rewrite(@"\bEnergy Shield\b", "ES");
        Rewrite(@"\bEvasion Rating\b", "Evasion");
        Rewrite(@"\bMovement Speed\b", "Move Speed");
        Rewrite(@"\bAttack Speed\b", "Atk Speed");
        Rewrite(@"\bCast Speed\b", "Cast Speed");
        Rewrite(@"\bCritical Hit Chance\b", "Crit Chance");
        Rewrite(@"\bCritical Strike Chance\b", "Crit Chance");
        Rewrite(@"\bCritical Damage Bonus\b", "Crit Dmg");
        Rewrite(@"\bAccuracy Rating\b", "Accuracy");
        Rewrite(@"\bPhysical Thorns Damage\b", "Phys Thorns Dmg");
        Rewrite(@"\bPhysical Damage\b", "Phys Dmg");
        Rewrite(@"\bFire Damage\b", "Fire Dmg");
        Rewrite(@"\bCold Damage\b", "Cold Dmg");
        Rewrite(@"\bLightning Damage\b", "Lightning Dmg");
        Rewrite(@"\bChaos Damage\b", "Chaos Dmg");
        Rewrite(@"\bElemental Damage\b", "Ele Dmg");
        Rewrite(@"\bSpell Damage\b", "Spell Dmg");
        Rewrite(@"\bArea of Effect\b", "AoE");
        Rewrite(@"\bRarity of Items Found\b", "Item Rarity");
        Rewrite(@"\bLife Regenerated per Second\b", "Life Regen/s");
        Rewrite(@"\bRegenerated per Second\b", "Regen/s");
        Rewrite(@"\bto Level of All ([A-Za-z ]+) Skills\b",
            "All $1 Skill Lvls");
        Rewrite(@"\bto Level of All ([A-Za-z ]+) Skill Gems\b",
            "All $1 Gem Lvls");
        Rewrite(@"\bArmour also applies to Ele Dmg\b",
            "Armour Applies to Ele Dmg");
        Rewrite(@"\bStun Threshold\b", "Stun Threshold");
        Rewrite(@"\bto (Life|Mana|ES|Armour|Evasion|Accuracy|Strength|Dexterity|Intelligence|Fire Res|Cold Res|Lightning Res|Chaos Res|All Ele Res)\b",
            "$1");
        Rewrite(@"\bincreased\s+", string.Empty);
        Rewrite(@"\bAdditional\s+", string.Empty);
        Rewrite(@"\bDamage\b", "Dmg");
        Rewrite(@"\bElemental\b", "Ele");
        Rewrite(@"\bPhysical\b", "Phys");
        Rewrite(@"\bResistances?\b", "Res");
        Rewrite(@"\s+", " ");
        Rewrite(@"\+\s+", "+");

        clean = clean.Trim();
        if (Regex.IsMatch(clean, @"^\d"))
            clean = "+" + clean;
        return clean;
    }
}
