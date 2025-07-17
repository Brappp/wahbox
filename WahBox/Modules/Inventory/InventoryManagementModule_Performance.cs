// This is a partial class to add performance optimizations to InventoryManagementModule

using System;
using System.Numerics;
using Dalamud.Interface.Utility;
using ImGuiNET;
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
        
        // Skip icon drawing if too many items visible (performance)
        if (category.Items.Count < 50 && item.IconId > 0)
        {
            try
            {
                var icon = Plugin.TextureProvider.GetFromGameIcon(item.IconId).GetWrapOrEmpty();
                if (icon != null && icon.ImGuiHandle != IntPtr.Zero)
                {
                    ImGui.Image(icon.ImGuiHandle, new Vector2(20, 20));
                    ImGui.SameLine();
                }
            }
            catch
            {
                // Skip broken icons
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
        
        if (Settings.ShowMarketPrices)
        {
            // Unit price
            ImGui.TableNextColumn();
            if (item.MarketPriceLoading)
            {
                ImGui.TextColored(new Vector4(0.5f, 0.5f, 0.5f, 1), "Loading...");
            }
            else if (item.MarketPrice.HasValue)
            {
                ImGui.Text($"{item.MarketPrice.Value:N0}");
            }
            else
            {
                ImGui.TextColored(new Vector4(0.5f, 0.5f, 0.5f, 1), "---");
                
                // Fetch price button
                ImGui.SameLine();
                if (ImGuiHelpers.GetButtonSize("$").X < ImGui.GetContentRegionAvail().X)
                {
                    if (ImGui.SmallButton("$"))
                    {
                        _ = FetchMarketPrice(item);
                    }
                }
            }
            
            // Total value
            ImGui.TableNextColumn();
            if (item.MarketPrice.HasValue)
            {
                var total = item.MarketPrice.Value * item.Quantity;
                ImGui.Text($"{total:N0}");
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
                ImGui.TextColored(new Vector4(0.8f, 0.2f, 0.2f, 1), "Protected");
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
