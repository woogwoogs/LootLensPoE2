using System;
using System.Collections.Generic;
using System.Numerics;
using System.Text.RegularExpressions;
using ImGuiNET;

namespace LootLens2;

public partial class LootLens2
{
    private void DrawMinimalRow(ImDrawListPtr draw, ImFontPtr font,
        float baseFont, float x, float y, float width, float scale, AnalyzedMod mod)
    {
        var muted = Pack(188, 192, 201, 255);
        var color = GetStatColor(mod.Text);
        var textColor = Settings.FullStatColors.Value ? color : Pack(225, 228, 234, 255);
        var text = ShortenMinimalText(mod.Text);
        var source = mod.IsCrafted ? "C" : mod.IsHidden ? "H" :
            mod.IsImplicit ? "I" : mod.IsUnique ? "" : mod.Affix;
        var size = .70f * scale;
        draw.AddText(font, baseFont * .60f * scale,
            new Vector2(x + 8f * scale, y + 2f * scale), muted, source ?? "");
        var right = x + width - 8f * scale;
        var tier = mod.IsUnique ? "" : GetCompactRightLabel(mod);
        draw.AddText(font, baseFont * .60f * scale,
            new Vector2(right - TextWidth(tier, .60f * scale), y + 2f * scale),
            GetCompactRightLabelColor(mod), tier);
        var trackRight = right - 65f * scale;
        var trackLeft = trackRight - 82f * scale;
        var textLeft = x + 26f * scale;
        draw.AddText(font, baseFont * size, new Vector2(textLeft, y), textColor,
            Ellipsize(text, Math.Max(1f, trackLeft - textLeft - 10f * scale), size));
        if (mod.Perfection < 0)
        {
            draw.AddText(font, baseFont * .56f * scale,
                new Vector2(trackLeft, y + 2f * scale), muted, mod.IsFixed ? "FIXED" : "—");
            return;
        }
        if (!Settings.MinimalSliders)
        {
            var range = CompactRollRange(mod.RangeText);
            draw.AddText(font, baseFont * .56f * scale,
                new Vector2(trackLeft, y + 2f * scale), muted,
                Ellipsize(range, trackRight - trackLeft, .56f * scale));
            return;
        }
        var center = y + 11f * scale;
        var marker = trackLeft + (trackRight - trackLeft) *
            Math.Clamp(mod.Perfection / 100f, 0f, 1f);
        draw.AddLine(new Vector2(trackLeft, center), new Vector2(trackRight, center),
            Pack(85, 88, 94, 255), Math.Max(1f, 1.5f * scale));
        if (marker > trackLeft)
            draw.AddLine(new Vector2(trackLeft, center), new Vector2(marker, center),
                color, Math.Max(1f, 2f * scale));
        draw.AddLine(new Vector2(marker, center - 4f * scale),
            new Vector2(marker, center + 4f * scale), color, Math.Max(1f, 2f * scale));
    }

    private static void DrawMinimalWeaponStrip(ImDrawListPtr draw, ImFontPtr font,
        float baseFont, float x, float y, float width, float scale, WeaponDps dps)
    {
        var metrics = new List<(string Label, float Value, uint Color)>
        {
            ("FIRE", dps.Fire, GetStatColor("Fire")),
            ("LIGHT", dps.Lightning, GetStatColor("Lightning")),
            ("COLD", dps.Cold, GetStatColor("Cold")),
            ("ELE DPS", dps.Elemental, GetStatColor("Elemental Damage")),
            ("PDPS", dps.Physical, GetStatColor("Physical"))
        };
        if (dps.Chaos > 0f)
            metrics.Add(("CHAOS", dps.Chaos, GetStatColor("Chaos")));
        metrics.RemoveAll(metric => metric.Value <= 0f);
        metrics.Add(("TOTAL", dps.Total, Pack(244, 239, 218, 255)));
        var cell = (width - 16f * scale) / metrics.Count;
        for (var i = 0; i < metrics.Count; i++)
        {
            var metric = metrics[i];
            var left = x + 8f * scale + i * cell;
            draw.AddText(font, baseFont * .44f * scale, new Vector2(left, y),
                metric.Color, metric.Label);
            var value = metric.Value.ToString("0.0");
            var valueScale = .68f * scale;
            if (TextWidth(value, valueScale) > cell - 4f * scale)
                valueScale *= (cell - 4f * scale) / TextWidth(value, valueScale);
            draw.AddText(font, baseFont * valueScale,
                new Vector2(left, y + 15f * scale), metric.Value == 0f
                    ? Pack(139, 142, 150, 255) : metric.Color, value);
        }
    }

    private static string ShortenMinimalText(string text)
    {
        var result = ShortenModifierText(text);
        var replacements = new (string Pattern, string Replacement)[]
        {
            (@"\bLife Gain Per Target\b", "Life on Hit"),
            (@"\bArmour and ES\b", "Armour + ES"),
            (@"\bArmour and Evasion\b", "Armour + Eva"),
            (@"\bEvasion and ES\b", "Eva + ES"),
            (@"\bStrength\b", "Str"), (@"\bDexterity\b", "Dex"),
            (@"\bIntelligence\b", "Int"), (@"\bAttributes\b", "Attrs"),
            (@"\bLightning\b", "Light"), (@"\bCritical\b", "Crit"),
            (@"\bStun Buildup\b", "Stun Build"),
            (@"\bStun and Ailment Threshold\b", "Stun/Ailment Threshold"),
            (@"\bSkill Lvls\b", "Lvls"), (@"\bGem Lvls\b", "Gem Lvls"),
            (@"\bDamage over Time\b", "DoT"), (@"\bDmg over Time\b", "DoT"),
            (@"\bCooldown Recovery Rate\b", "Cooldown Recovery"),
            (@"\bReduced Attribute Requirements\b", "Reduced Attr Reqs"),
            (@"\bRequirements\b", "Reqs"), (@"\bReservation Efficiency\b", "Reservation Eff"),
            (@"\bProjectile\b", "Proj"), (@"\bDuration\b", "Dur"),
            (@"\bper Second\b", "/s"), (@"\bChance to\b", "Chance:"),
            (@"\bwith Attack Skills\b", "with Attacks")
        };
        foreach (var pair in replacements)
            result = Regex.Replace(result, pair.Pattern, pair.Replacement, RegexOptions.IgnoreCase);
        return result;
    }

    private static void DrawMinimalBottomLine(ImDrawListPtr draw, ImFontPtr font,
        float baseFont, float x, float y, float width, float scale,
        string left, string right, uint color)
    {
        draw.AddLine(new Vector2(x + 8f * scale, y), new Vector2(x + width - 8f * scale, y),
            Pack(77, 80, 86, 255), 1f);
        var size = .55f * scale;
        var rightWidth = Math.Min(width * .30f, TextWidth(right, size));
        draw.AddText(font, baseFont * size, new Vector2(x + 8f * scale, y + 5f * scale), color,
            Ellipsize(left, width - rightWidth - 28f * scale, size));
        draw.AddText(font, baseFont * size,
            new Vector2(x + width - rightWidth - 8f * scale, y + 5f * scale), Pack(188, 192, 201, 255),
            Ellipsize(right, rightWidth, size));
    }
}
