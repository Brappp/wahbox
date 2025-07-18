// This is a partial class to add performance optimizations to InventoryManagementModule

using System;
using System.Numerics;
using Dalamud.Interface.Utility;
using ImGuiNET;
using WahBox.Helpers;
using WahBox.Models;

namespace WahBox.Modules.Inventory;

public partial class InventoryManagementModule
{
    private void DrawItemRow(InventoryItemInfo item, CategoryGroup category)
    {
        ImGui.TableNextRow();
        ImGui.PushID(item.GetUniqueKey());
        
        // Checkbox
        ImGui.TableNextColumn();
        var isSelected = _selectedItems.Contains(item.ItemId);
        if (ImGui.Checkbox($"##select_{item.GetUniqueKey()}", ref isSelected))
        {
            if (isSelected)
            {
                _selectedItems.Add(item.ItemId);
                item.IsSelected = true;
            }
            else
            {
                _selectedItems.Remove(item.ItemId);
                item.IsSelected = false;
            }
        }
        
        // Item name with icon
        ImGui.TableNextColumn();
        
        // Always show icons using cached textures
        if (item.IconId > 0)
        {
            var icon = _iconCache.GetIcon(item.IconId);
            if (icon != null)
            {
                ImGui.Image(icon.ImGuiHandle, new Vector2(20, 20));
                ImGui.SameLine();
            }
        }
        
        ImGui.Text(item.Name);
        if (item.IsHQ)
        {
            ImGui.SameLine();
            ImGui.TextColored(new Vector4(0.2f, 0.8f, 0.2f, 1), "[HQ]");
        }
        
        // Quantity
        ImGui.TableNextColumn();
        ImGui.Text(item.Quantity.ToString());
        
        // Location
        ImGui.TableNextColumn();
        ImGui.Text(GetLocationName(item.Container));
        
        if (Settings.ShowMarketPrices && item.CanBeTraded)  // Only show prices for tradable items
        {
            // Unit price
            ImGui.TableNextColumn();
            if (item.MarketPriceLoading)
            {
                ImGui.TextColored(new Vector4(0.5f, 0.5f, 0.5f, 1), "Loading...");
            }
            else if (item.MarketPrice.HasValue)
            {
                if (item.MarketPrice.Value == -1)
                {
                    ImGui.TextColored(new Vector4(0.5f, 0.5f, 0.5f, 1), "N/A");
                }
                else
                {
                    ImGui.Text($"{item.MarketPrice.Value:N0}g");
                }
            }
            else
            {
                ImGui.TextColored(new Vector4(0.5f, 0.5f, 0.5f, 1), "---");
                
                // Only show fetch button if not already loading
                if (!_fetchingPrices.Contains(item.ItemId))
                {
                    ImGui.SameLine();
                    ImGui.PushStyleVar(ImGuiStyleVar.FramePadding, new Vector2(2, 2));
                    if (ImGui.Button($"$##fetch_{item.GetUniqueKey()}", new Vector2(20, 20)))
                    {
                        _ = FetchMarketPrice(item);
                    }
                    ImGui.PopStyleVar();
                }
            }
            
            // Total value
            ImGui.TableNextColumn();
            if (item.MarketPrice.HasValue)
            {
                if (item.MarketPrice.Value == -1)
                {
                    ImGui.TextColored(new Vector4(0.5f, 0.5f, 0.5f, 1), "N/A");
                }
                else
                {
                    var total = item.MarketPrice.Value * item.Quantity;
                    ImGui.Text($"{total:N0}g");
                }
            }
            else
            {
                ImGui.TextColored(new Vector4(0.5f, 0.5f, 0.5f, 1), "---");
            }
        }
        else
        {
            // Status column
            ImGui.TableNextColumn();
            
            if (!item.CanBeDiscarded)
            {
                ImGui.TextColored(new Vector4(0.8f, 0.2f, 0.2f, 1), "Not Discardable");
            }
            else if (item.IsCollectable)
            {
                ImGui.TextColored(new Vector4(0.8f, 0.8f, 0.2f, 1), "Collectable");
            }
            else if (item.SpiritBond >= 100)
            {
                ImGui.TextColored(new Vector4(0.2f, 0.8f, 0.2f, 1), "Spiritbonded");
            }
            else if (Settings.BlacklistedItems.Contains(item.ItemId))
            {
                ImGui.TextColored(new Vector4(0.8f, 0.2f, 0.2f, 1), "Blacklisted");
            }
        }
        
        ImGui.PopID();
    }
}
