using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using ExileCore2.PoEMemory;
using ExileCore2.PoEMemory.Elements;
using ExileCore2.PoEMemory.Elements.InventoryElements;
using ExileCore2.PoEMemory.MemoryObjects;
using ExileCore2.Shared.Enums;
using ImGuiNET;
using RectangleF = ExileCore2.Shared.RectangleF;

namespace LootLens2;

public partial class LootLens2
{
    private readonly Dictionary<long, List<int>> _inventoryTierCache = new();
    private long _nextInventoryTierRefresh;

    private void DrawInventoryTierDiamonds()
    {
        if (!Settings.ShowTierDiamonds)
        {
            _inventoryTierCache.Clear();
            _nextInventoryTierRefresh = 0;
            return;
        }

        var now = DateTime.UtcNow.Ticks;
        if (now >= _nextInventoryTierRefresh)
        {
            _inventoryTierCache.Clear();
            _nextInventoryTierRefresh = now + TimeSpan.TicksPerMillisecond * 250;
        }
        if (Settings.ShowTierDiamondsInInventory)
        {
            try
            {
                var panel = GameController.IngameState.IngameUi.InventoryPanel;
                if (panel != null && panel.IsVisible)
                    DrawSlots(panel[InventoryIndex.PlayerInventory].VisibleInventoryItems);
            }
            catch
            {
            }
        }

        if (Settings.ShowTierDiamondsInStash)
            DrawStashTierDiamonds();
        if (Settings.ShowTierDiamondsOnEquippedGear)
            DrawEquippedTierDiamonds();
        if (Settings.ShowTierDiamondsInOtherWindows)
            DrawOtherWindowTierDiamonds();
    }

    private void DrawEquippedTierDiamonds()
    {
        try
        {
            var panel = GameController.IngameState.IngameUi.InventoryPanel;
            if (panel == null || !panel.IsVisible) return;
            var names = new HashSet<string>(StringComparer.Ordinal)
            {
                "Helm", "Amulet", "Chest", "LWeapon", "RWeapon",
                "LWeaponSwap", "RWeaponSwap", "LRing", "RRing", "Ring3",
                "Gloves", "Belt", "Boots", "FlaskLife", "FlaskMana",
                "Charm1", "Charm2", "Charm3"
            };
            foreach (var index in Enum.GetValues<InventoryIndex>())
                if (names.Contains(index.ToString()))
                    DrawSlots(panel[index].VisibleInventoryItems);
        }
        catch { }
    }

    private void DrawOtherWindowTierDiamonds()
    {
        try
        {
            var ui = GameController.IngameState.IngameUi;
            if (ui.RitualWindow?.IsVisible == true)
                DrawSlots(ui.RitualWindow.Items);
            if (ui.HaggleWindow?.IsVisible == true)
                DrawSlots(ui.HaggleWindow.InventoryItems);
            DrawReflectedItemLists(ui.PurchaseWindow);
            DrawReflectedItemLists(ui.PurchaseWindowHideout);
            DrawReflectedItemLists(ui.SellWindow);
            DrawReflectedItemLists(ui.SellWindowHideout);
            DrawReflectedItemLists(ui.TradeWindow);
            DrawReflectedItemLists(ui.QuestRewardWindow);
        }
        catch { }
    }

    private void DrawReflectedItemLists(object? window)
    {
        if (window is not Element element || !element.IsVisible) return;
        var visited = new HashSet<object>(ReferenceEqualityComparer.Instance);
        Scan(window, 0);
        void Scan(object? source, int depth)
        {
            if (source == null || depth > 2 || !visited.Add(source)) return;
            foreach (var property in source.GetType().GetProperties())
            {
                if (property.GetIndexParameters().Length != 0) continue;
                object? value;
                try { value = property.GetValue(source); } catch { continue; }
                if (value is IEnumerable sequence && value is not string)
                {
                    var found = false;
                    foreach (var entry in sequence)
                        if (entry is NormalInventoryItem item)
                        {
                            DrawDiamonds(item.Item, item.GetClientRect());
                            found = true;
                        }
                    if (found) continue;
                }
                if (value is Element child) Scan(child, depth + 1);
            }
        }
    }

    private void DrawSlots(IEnumerable<NormalInventoryItem>? items)
    {
        if (items == null) return;
        foreach (var slot in items)
        {
            try { DrawDiamonds(slot.Item, slot.GetClientRect()); }
            catch { }
        }
    }

    private void DrawStashTierDiamonds()
    {
        try
        {
            var stash = GameController.IngameState.IngameUi.StashElement;
            var inventory = stash?.VisibleStash;
            if (stash == null || !stash.IsVisible || inventory == null) return;
            var rect = inventory.InventoryUIElement?.GetClientRect() ?? default;
            var columns = inventory.TotalBoxesInInventoryRow;
            var items = inventory.ServerInventory?.InventorySlotItems;
            if (rect.Width <= 0 || columns <= 0 || items == null) return;
            var cell = rect.Width / columns;
            foreach (var slot in items)
            {
                var itemRect = new RectangleF(rect.Left + slot.PosX * cell,
                    rect.Top + slot.PosY * cell, cell * Math.Max(1, slot.SizeX),
                    cell * Math.Max(1, slot.SizeY));
                DrawDiamonds(slot.Item, itemRect);
            }
        }
        catch { }
    }

    private void DrawHoveredTierDiamonds(HoverItemIcon hover)
    {
        if (!Settings.ShowTierDiamonds) return;
        try
        {
            var equipped = IsEquippedItem(hover.Item);
            if (equipped ? !Settings.ShowTierDiamondsOnEquippedGear :
                !Settings.ShowTierDiamondsInOtherWindows) return;
            var icon = hover.Item2DIcon;
            if (icon != null && icon.IsValid)
                DrawDiamonds(hover.Item, icon.GetClientRect());
        }
        catch { }
    }

    private bool IsEquippedItem(Entity? item)
    {
        if (item == null) return false;
        try
        {
            var inventories = GameController.IngameState.ServerData.PlayerInventories;
            for (var index = 1; index < inventories.Count; index++)
                if (inventories[index].Inventory.InventorySlotItems.Any(slot =>
                        slot?.Item?.Address == item.Address))
                    return true;
        }
        catch { }
        return false;
    }

    private void DrawDiamonds(Entity? item, RectangleF rect)
    {
        if (item == null || !item.IsValid || rect.Width <= 8 || rect.Height <= 8) return;
        var address = unchecked((long)item.Address);
        if (!_inventoryTierCache.TryGetValue(address, out var tiers))
            _inventoryTierCache[address] = tiers = _analyzer.GetInventoryTiers(item);
        if (tiers.Count == 0) return;
        var draw = ImGui.GetForegroundDrawList();
        var size = Math.Min(Math.Clamp((float)Settings.TierDiamondSize.Value, 4f, 18f),
            Math.Min(rect.Width, rect.Height) - 6f);
        var columns = Math.Max(1, (int)((rect.Width - 6f) / (size + 3f)));
        var rows = (tiers.Count + columns - 1) / columns;
        if (rows * (size + 3f) > rect.Height - 6f)
        {
            size = Math.Max(2f, (rect.Height - 6f) / rows - 3f);
            columns = Math.Max(1, (int)((rect.Width - 6f) / (size + 3f)));
        }
        for (var i = 0; i < tiers.Count; i++)
        {
            var center = new Vector2(rect.Left + 3f + size / 2 +
                i % columns * (size + 3f), rect.Bottom - 3f - size / 2 -
                i / columns * (size + 3f));
            var color = tiers[i] switch { 1 => Settings.Tier1DiamondColor,
                2 => Settings.Tier2DiamondColor, _ => Settings.Tier3DiamondColor };
            var half = size / 2;
            var top = center + new Vector2(0, -half);
            var right = center + new Vector2(half, 0);
            var bottom = center + new Vector2(0, half);
            var left = center + new Vector2(-half, 0);
            draw.AddQuadFilled(top, right, bottom, left, ImGui.ColorConvertFloat4ToU32(color));
            draw.AddQuad(top, right, bottom, left, 0xFF101010, 1f);
        }
    }
}
