using System;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;

namespace WahBox.Core;

public static unsafe class CurrencyHelper
{
    /// <summary>
    /// Gets the current count for a currency item.
    /// Handles both regular items and special currencies.
    /// </summary>
    public static int GetCurrencyCount(uint itemId)
    {
        // First try regular inventory count
        var inventoryManager = InventoryManager.Instance();
        if (inventoryManager != null)
        {
            var count = inventoryManager->GetInventoryItemCount(itemId, false, false, false);
            if (count > 0) return count;
        }

        // Handle special currencies that don't show up in regular inventory
        return itemId switch
        {
            // Grand Company Seals
            20 or 21 or 22 => GetGrandCompanySeals(),
            
            // Wolf Marks
            25 => GetWolfMarks(),
            
            // Trophy Crystals  
            36656 => GetTrophyCrystals(),
            
            // Allied Seals
            27 => GetAlliedSeals(),
            
            // Centurio Seals
            10307 => GetCenturioSeals(),
            
            // Sack of Nuts
            26533 => GetSackOfNuts(),
            
            // White Scrips (both crafter and gatherer use same ID)
            25199 => GetWhiteScrips(),
            
            // Purple Scrips (both crafter and gatherer use same ID)
            25200 => GetPurpleScrips(),
            
            // Skybuilders' Scrips
            28063 => GetSkybuildersScrips(),
            
            // Bicolor Gemstones
            26807 => GetBicolorGemstones(),
            
            // Default - return inventory count
            _ => inventoryManager != null ? inventoryManager->GetInventoryItemCount(itemId, false, false, false) : 0
        };
    }

    private static int GetGrandCompanySeals()
    {
        // Get from inventory - GC seals are special inventory items
        var inventoryManager = InventoryManager.Instance();
        if (inventoryManager == null) return 0;
        
        var container = inventoryManager->GetInventoryContainer(InventoryType.Currency);
        if (container == null) return 0;

        // GC seals are in the currency container
        // Storm = 20, Serpent = 21, Flame = 22
        for (var i = 0; i < container->Size; i++)
        {
            var slot = container->GetInventorySlot(i);
            if (slot == null) continue;
            
            if (slot->ItemId == 20 || slot->ItemId == 21 || slot->ItemId == 22)
                return (int)slot->Quantity;
        }
        
        return 0;
    }

    private static int GetWolfMarks()
    {
        // Wolf marks are in currency inventory
        var inventoryManager = InventoryManager.Instance();
        return inventoryManager != null ? inventoryManager->GetInventoryItemCount(25, false, false, false) : 0;
    }

    private static int GetTrophyCrystals()
    {
        // Trophy crystals are in currency inventory  
        var inventoryManager = InventoryManager.Instance();
        return inventoryManager != null ? inventoryManager->GetInventoryItemCount(36656, false, false, false) : 0;
    }

    private static int GetAlliedSeals()
    {
        // Allied seals are in currency inventory
        var inventoryManager = InventoryManager.Instance();
        return inventoryManager != null ? inventoryManager->GetInventoryItemCount(27, false, false, false) : 0;
    }

    private static int GetCenturioSeals()
    {
        // Centurio seals are in currency inventory
        var inventoryManager = InventoryManager.Instance();
        return inventoryManager != null ? inventoryManager->GetInventoryItemCount(10307, false, false, false) : 0;
    }

    private static int GetSackOfNuts()
    {
        // Sack of Nuts currency is in currency inventory
        var inventoryManager = InventoryManager.Instance();
        return inventoryManager != null ? inventoryManager->GetInventoryItemCount(26533, false, false, false) : 0;
    }

    private static int GetWhiteScrips()
    {
        // White scrips are in currency inventory
        var inventoryManager = InventoryManager.Instance();
        return inventoryManager != null ? inventoryManager->GetInventoryItemCount(25199, false, false, false) : 0;
    }

    private static int GetPurpleScrips()
    {
        // Purple scrips are in currency inventory
        var inventoryManager = InventoryManager.Instance();
        return inventoryManager != null ? inventoryManager->GetInventoryItemCount(25200, false, false, false) : 0;
    }

    private static int GetSkybuildersScrips()
    {
        // Skybuilders' scrips are in currency inventory
        var inventoryManager = InventoryManager.Instance();
        return inventoryManager != null ? inventoryManager->GetInventoryItemCount(28063, false, false, false) : 0;
    }

    private static int GetBicolorGemstones()
    {
        // Bicolor gemstones are in currency inventory
        var inventoryManager = InventoryManager.Instance();
        return inventoryManager != null ? inventoryManager->GetInventoryItemCount(26807, false, false, false) : 0;
    }

    /// <summary>
    /// Gets the maximum cap for a currency
    /// </summary>
    public static int GetCurrencyMax(uint itemId)
    {
        return itemId switch
        {
            // Grand Company Seals
            20 or 21 or 22 => 90000,
            
            // Wolf Marks
            25 => 20000,
            
            // Trophy Crystals
            36656 => 20000,
            
            // Allied/Centurio/Sack of Nuts Seals
            27 or 10307 or 26533 => 4000,
            
            // Bicolor Gemstones
            26807 => 1000,
            
            // Tomestones
            28 => 2000,     // Poetics
            47 => 2000,     // Aesthetics
            48 => 2000,     // Heliometry
            
            // Scrips
            25199 or 25200 => 4000, // White/Purple Scrips
            28063 => 10000, // Skybuilders
            
            // Default - check if it's a tomestone
            _ => IsTomestone(itemId) ? 2000 : 0
        };
    }
    
    private static bool IsTomestone(uint itemId)
    {
        // Check if this item is a tomestone
        var item = Plugin.DataManager.GetExcelSheet<Lumina.Excel.Sheets.Item>()?.GetRow(itemId);
        if (item == null) return false;
        
        // Tomestones have specific item UI categories
        return item.Value.ItemUICategory.RowId == 100; // Currency category
    }
}
