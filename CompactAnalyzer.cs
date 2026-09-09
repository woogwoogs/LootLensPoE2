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

        // LootLens keeps its own predictable width instead of inheriting
        // unusually wide native item tooltips.
        var width = Math.Clamp(Settings.PanelWidth.Value * scale,
            340f * scale,
            Math.Max(340f * scale, display.X - 16f));

        var topPadding = 4f * scale;
        if (!analysis.WeaponDps.IsWeapon && displayMods.Count > 0)
            width = Math.Min(width, Math.Max(340f * scale,
                displayMods.Max(mod => TextWidth(ShortenMinimalText(mod.Text), .70f * scale)) + 191f * scale));
        var minimal = true;
        var metadataHeight = minimal ? (analysis.WeaponDps.IsWeapon ? 42f * scale : 0f) : analysis.WeaponDps.IsWeapon
            ? 48f * scale
            : 27f * scale;
        var rowHeight = (minimal ? 25f : 30f) * scale;
        var uniqueFooter = Settings.ShowOverallPerfection.Value &&
                           (analysis.OverallPerfection >= 0 || !string.IsNullOrEmpty(analysis.UniqueDropTier))
            ? 28f * scale
            : 0f;

        var height = topPadding + metadataHeight +
                     displayMods.Count * rowHeight + uniqueFooter +
                     4f * scale;
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
        var subdued = Pack(164, 168, 177, 255);
        var mutedLine = Pack(128, 132, 141, 170);
        var cy = y + topPadding;

        if (minimal)
        {
            if (analysis.WeaponDps.IsWeapon)
                DrawMinimalWeaponStrip(draw, font, baseFont, x, cy, width, scale, analysis.WeaponDps);
        }
        else
            DrawCompactMetadata(draw, font, baseFont, x, cy, width, scale,
                analysis, subdued, mutedLine);
        cy += metadataHeight;

        foreach (var mod in displayMods)
        {
            if (minimal)
                DrawMinimalRow(draw, font, baseFont, x, cy, width, scale, mod);
            else
                DrawCompactModifierRow(draw, font, baseFont, x, cy, width,
                    rowHeight, scale, mod, subdued);
            cy += rowHeight;
        }

        if (uniqueFooter > 0f)
        {
            DrawMinimalBottomLine(draw, font, baseFont, x, cy, width, scale,
                analysis.OverallPerfection >= 0 ? $"PERFECTION {analysis.OverallPerfection}%" : "PERFECTION —",
                string.IsNullOrEmpty(analysis.UniqueDropTier) ? "" : $"{analysis.UniqueDropTier} UNIQUE", subdued);
            cy += uniqueFooter;
        }

        draw.PopClipRect();
        if (ImGui.GetIO().KeyShift)
        {
            ImGui.BeginTooltip();
            ImGui.PushTextWrapPos(ImGui.GetFontSize() * 32f);
            foreach (var mod in displayMods)
            {
                ImGui.TextUnformatted(mod.Text);
                ImGui.TextDisabled(mod.IsFixed ? "FIXED" : string.IsNullOrEmpty(mod.RangeText)
                    ? "Range unavailable" : mod.RangeText + " · " + mod.RangeLabel);
                ImGui.Spacing();
            }
            ImGui.PopTextWrapPos();
            ImGui.EndTooltip();
        }
    }

    private static void DrawCompactBackground(ImDrawListPtr draw,
        Vector2 topLeft, Vector2 bottomRight)
    {
        draw.AddRectFilled(topLeft, bottomRight, Pack(0, 0, 0, 248));
    }

    private static void DrawCompactMetadata(ImDrawListPtr draw,
        ImFontPtr font, float baseFont, float x, float y, float width,
        float scale, AnalyzedItem item, uint subdued, uint mutedLine)
    {
        var left = x + 14f * scale;
        var right = x + width - 14f * scale;
        var cursor = left;
        var metadataScale = .58f * scale;
        var valueScale = (item.WeaponDps.IsWeapon ? .73f : .66f) * scale;
        var metricLabelScale = .49f * scale;
        const string separator = "  |  ";
        var modCount = item.Mods.Count(mod =>
            !mod.IsImplicit && !mod.IsHidden);
        var metadata = $"iLvl {item.ItemLevel}  |  {modCount} " +
                       (modCount == 1 ? "Mod" : "Mods");

        draw.AddText(font, baseFont * metadataScale,
            new Vector2(cursor, y + 4f * scale), subdued, metadata);
        cursor += TextWidth(metadata, metadataScale);

        var metrics = BuildCompactSummaryMetrics(item);
        if (item.WeaponDps.IsWeapon)
        {
            var metricTop = y + 20f * scale;
            var clusterWidth = Math.Min(right - left, 430f * scale);
            var clusterLeft = left + (right - left - clusterWidth) / 2f;
            var cellWidth = clusterWidth / Math.Max(1, metrics.Count);
            for (var index = 0; index < metrics.Count; index++)
            {
                var metric = metrics[index];
                var labelScale = .56f * scale;
                var weaponValueScale = (index == metrics.Count - 1
                    ? .92f
                    : .84f) * scale;
                var label = metric.Label + " ";
                var groupWidth = TextWidth(label, labelScale) +
                                 TextWidth(metric.Value, weaponValueScale);
                var cellLeft = clusterLeft + index * cellWidth;
                var groupLeft = cellLeft + (cellWidth - groupWidth) / 2f;
                draw.AddText(font, baseFont * labelScale,
                    new Vector2(groupLeft, metricTop + 4f * scale),
                    index == metrics.Count - 1
                        ? Pack(205, 209, 218, 255)
                        : subdued, label);
                draw.AddText(font, baseFont * weaponValueScale,
                    new Vector2(groupLeft + TextWidth(label, labelScale),
                        metricTop), metric.Color, metric.Value);

                if (index > 0)
                    draw.AddLine(new Vector2(cellLeft, metricTop + 2f * scale),
                        new Vector2(cellLeft, metricTop + 17f * scale),
                        Pack(105, 109, 118, 125),
                        Math.Max(.65f, .7f * scale));
            }

            draw.AddLine(new Vector2(left, y + 45f * scale),
                new Vector2(right, y + 45f * scale), mutedLine,
                Math.Max(1f, scale * .65f));
            return;
        }

        foreach (var metric in metrics)
        {
            var separatorWidth = TextWidth(separator, metadataScale);
            var label = metric.Label + " ";
            var labelWidth = TextWidth(label, metricLabelScale);
            var valueWidth = TextWidth(metric.Value, valueScale);
            if (cursor + separatorWidth + labelWidth + valueWidth > right)
                break;

            draw.AddText(font, baseFont * metadataScale,
                new Vector2(cursor, y + 4f * scale), subdued, separator);
            cursor += separatorWidth;
            draw.AddText(font, baseFont * metricLabelScale,
                new Vector2(cursor, y + 7f * scale), subdued, label);
            cursor += labelWidth;
            draw.AddText(font, baseFont * valueScale,
                new Vector2(cursor, y + 2.5f * scale), metric.Color,
                metric.Value);
            cursor += valueWidth;
        }

        var dividerY = y + 24f * scale;
        draw.AddLine(new Vector2(left, dividerY),
            new Vector2(right, dividerY), mutedLine,
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
            metrics.Add(("TOTAL DPS", item.WeaponDps.Total.ToString("0.0"),
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
            Pack(229, 231, 235, 255);
        var letterColor = mod.IsHidden || mod.IsUnique || mod.IsCrafted ||
                          mod.IsImplicit
            ? specialColor
            : Pack(170, 174, 183, 255);
        // Source styling stays on the letter/name. The accent and underline
        // always describe the modifier's actual stat family.
        var accentColor = statColor;
        var uniqueLayout = mod.IsUnique;

        if (!uniqueLayout)
        {
            var affixIndicator = mod.IsCrafted ? "C" :
                mod.IsHidden ? "H" :
                mod.IsImplicit ? "I" :
                string.IsNullOrWhiteSpace(mod.Affix) ? "·" : mod.Affix;
            var affixScale = .64f * scale;
            draw.AddText(font, baseFont * affixScale,
                new Vector2(x + 14f * scale,
                    rowTop + 3.5f * scale),
                letterColor, affixIndicator);
        }

        var accentX = x + (uniqueLayout ? 16f : 29f) * scale;
        draw.AddLine(new Vector2(accentX, rowTop + 2f * scale),
            new Vector2(accentX, rowTop + rowHeight - 3f * scale),
            accentColor, Math.Max(1.25f, 1.35f * scale));

        var right = x + width - 14f * scale;
        var rightLabel = GetCompactRightLabel(mod);
        var rightLabelScale = rightLabel.Length > 5
            ? .54f * scale
            : .68f * scale;
        var rightLabelWidth = TextWidth(rightLabel, rightLabelScale);
        if (!string.IsNullOrEmpty(rightLabel))
        {
            var labelColor = mod.Perfection < 0 && mod.IsUnique
                ? subdued
                : GetCompactRightLabelColor(mod);
            draw.AddText(font, baseFont * rightLabelScale,
                new Vector2(right - rightLabelWidth,
                    rowTop + 4f * scale),
                labelColor, rightLabel);
        }

        // The range and tier keep stable columns. Roll quality lives directly
        // beneath the modifier text instead of in a separate slider region.
        var tierColumnWidth = 68f * scale;
        var rangeColumnWidth = Math.Clamp(width * .19f,
            82f * scale, 110f * scale);
        var tierLeft = right - tierColumnWidth;
        var textLeft = x + (uniqueLayout ? 27f : 40f) * scale;
        var underlineSpan = Math.Clamp(width * .48f,
            150f * scale, 300f * scale);
        var maximumRangeLeft = tierLeft - rangeColumnWidth;
        var preferredRangeLeft = textLeft + underlineSpan + 12f * scale;
        var rangeLeft = Math.Min(maximumRangeLeft, preferredRangeLeft);
        var rangeRight = rangeLeft + rangeColumnWidth;
        var availableTextRight = rangeLeft - 12f * scale;
        var textRight = Math.Min(availableTextRight,
            textLeft + underlineSpan);
        var modifierScale = .78f * scale;
        draw.AddText(font, baseFont * modifierScale,
            new Vector2(textLeft, rowTop + 1.5f * scale), textColor,
            Ellipsize(displayText, Math.Max(70f, textRight - textLeft),
                modifierScale));

        if (mod.Perfection < 0)
            return;

        var range = CompactRollRange(mod.RangeText);
        var rangeScale = .62f * scale;
        var compactRange = Ellipsize(range, rangeColumnWidth, rangeScale);
        var rangeWidth = TextWidth(compactRange, rangeScale);
        draw.AddText(font, baseFont * rangeScale,
            new Vector2(rangeRight - rangeWidth, rowTop + 5f * scale),
            Pack(202, 206, 215, 255), compactRange);

        var underlineY = rowTop + 24f * scale;
        var underlineLeft = textLeft;
        var underlineRight = Math.Max(underlineLeft + 36f * scale,
            textRight);
        draw.AddLine(new Vector2(underlineLeft, underlineY),
            new Vector2(underlineRight, underlineY),
            Pack(132, 137, 147, 235), Math.Max(1.35f, 1.5f * scale));
        var underlineWidth = underlineRight - underlineLeft;
        var filledWidth = Math.Min(underlineWidth,
            Math.Max(6f * scale, underlineWidth * mod.Perfection / 100f));
        var filledRight = underlineLeft + filledWidth;
        draw.AddLine(new Vector2(underlineLeft, underlineY),
            new Vector2(filledRight, underlineY), accentColor,
            Math.Max(2.4f, 2.8f * scale));
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

    private static uint GetCompactRightLabelColor(AnalyzedMod mod)
    {
        if (mod.IsHidden) return Pack(210, 145, 77, 255);
        if (mod.IsUnique) return Pack(220, 127, 68, 255);
        if (mod.IsImplicit) return Pack(181, 186, 196, 255);
        if (mod.IsCrafted) return Pack(92, 212, 225, 255);
        return mod.Tier switch
        {
            1 => Pack(244, 201, 73, 255),
            2 => Pack(99, 222, 142, 255),
            3 => Pack(75, 202, 216, 255),
            4 => Pack(161, 174, 189, 255),
            _ => Pack(153, 158, 168, 255)
        };
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
        var filledRight = left + (right - left) *
            analysis.OverallPerfection / 100f;
        draw.AddLine(new Vector2(left, barY),
            new Vector2(filledRight, barY), scoreColor,
            Math.Max(1.6f, 1.8f * scale));

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
        Rewrite(@"\bElemental Damage with Attack Skills\b", "Ele Attack Dmg");
        Rewrite(@"\bElemental Damage with Attacks\b", "Ele Attack Dmg");
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
