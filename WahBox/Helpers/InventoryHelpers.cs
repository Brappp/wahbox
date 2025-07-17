using System;
using System.Collections.Generic;
using System.Linq;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.UI.Misc;
using Lumina.Excel.Sheets;
using WahBox.Models;

namespace WahBox.Helpers;

public unsafe class InventoryHelpers
{
    private readonly IDataManager _dataManager;
    private readonly IPluginLog _log;
    
    // Inventory types to scan
    private static readonly InventoryType[] PlayerInventories = 
    {
        InventoryType.Inventory1,
        InventoryType.Inventory2, 
        InventoryType.Inventory3,
        InventoryType.Inventory4,
        InventoryType.ArmoryMainHand,
        InventoryType.ArmoryOffHand,
        InventoryType.ArmoryHead,
        InventoryType.ArmoryBody,
        InventoryType.ArmoryHands,
        InventoryType.ArmoryLegs,
        InventoryType.ArmoryFeets,
        InventoryType.ArmoryEar,
        InventoryType.ArmoryNeck,
        InventoryType.ArmoryWrist,
        InventoryType.ArmoryRings,
        InventoryType.Crystals,
        InventoryType.Currency,
        InventoryType.SaddleBag1,
        InventoryType.SaddleBag2,
        InventoryType.PremiumSaddleBag1,
        InventoryType.PremiumSaddleBag2
    };
    
    public InventoryHelpers(IDataManager dataManager, IPluginLog log)
    {
        _dataManager = dataManager;
        _log = log;
    }
    
    public List<InventoryItemInfo> GetAllItems()
    {
        var items = new List<InventoryItemInfo>();
        var itemSheet = _dataManager.GetExcelSheet<Item>();
        
        if (itemSheet == null)
        {
            _log.Error("Failed to load Item sheet");
            return items;
        }
        
        var inventoryManager = InventoryManager.Instance();
        if (inventoryManager == null)
        {
            _log.Error("InventoryManager is null");
            return items;
        }
        
        foreach (var inventoryType in PlayerInventories)
        {
            var inventory = inventoryManager->GetInventoryContainer(inventoryType);
            if (inventory == null) continue;
            
            for (var slot = 0; slot < inventory->Size; slot++)
            {
                var item = inventory->GetInventorySlot(slot);
                if (item == null || item->ItemId == 0) continue;
                
                var itemData = itemSheet.GetRowOrDefault(item->ItemId);
                if (itemData == null) continue;
                
                var info = new InventoryItemInfo
                {
                    ItemId = item->ItemId,
                    Name = itemData.Value.Name.ExtractText(),
                    Quantity = (int)item->Quantity,
                    Container = inventoryType,
                    Slot = (short)slot,
                    IsHQ = item->Flags.HasFlag(InventoryItem.ItemFlags.HighQuality),
                    IconId = itemData.Value.Icon,
                    CanBeDiscarded = itemData.Value.IsUntradable == false, // Using IsUntradable as approximation
                    IsCollectable = item->Flags.HasFlag(InventoryItem.ItemFlags.Collectable),
                    SpiritBond = 0, // Spiritbond property not available in current ClientStructs
                    ItemUICategory = itemData.Value.ItemUICategory.RowId,
                    CategoryName = GetCategoryName(itemData.Value.ItemUICategory.RowId)
                };
                
                // Add durability for gear
                if (info.IsGear && item->Condition > 0)
                {
                    info.Durability = item->Condition;
                    info.MaxDurability = 30000; // Default max durability
                }
                
                items.Add(info);
            }
        }
        
        return items;
    }
    
    public unsafe void DiscardItem(InventoryItemInfo item)
    {
        var inventoryManager = InventoryManager.Instance();
        if (inventoryManager == null) return;
        
        var inventory = inventoryManager->GetInventoryContainer(item.Container);
        if (inventory == null) return;
        
        var slot = inventory->GetInventorySlot(item.Slot);
        if (slot == null || slot->ItemId != item.ItemId) return;
        
        // Use AgentInventoryContext to discard the item
        FFXIVClientStructs.FFXIV.Client.UI.Agent.AgentInventoryContext.Instance()->DiscardItem(slot, item.Container, item.Slot, 0);
    }
    
    private string GetCategoryName(uint categoryId)
    {
        return categoryId switch
        {
            // Weapons
            1 => "Primary Arms",
            2 => "Secondary Arms",
            3 => "One-Handed Arms",
            4 => "Two-Handed Arms",
            5 => "Main Hand",
            // Armor
            11 => "Head",
            12 => "Body", 
            13 => "Legs",
            14 => "Hands",
            15 => "Feet",
            // Accessories
            40 => "Necklaces",
            41 => "Earrings",
            42 => "Bracelets",
            43 => "Rings",
            // Consumables
            44 => "Medicine",
            46 => "Food",
            // Materials
            58 => "Materials",
            59 => "Crystals",
            60 => "Catalysts",
            // Other
            _ => "Miscellaneous"
        };
    }
    
    public static bool IsSafeToDiscard(InventoryItemInfo item, HashSet<uint> blacklist)
    {
        // Never discard blacklisted items
        if (blacklist.Contains(item.ItemId))
            return false;
            
        // Never discard equipped items
        if (item.Container >= InventoryType.ArmoryMainHand && 
            item.Container <= InventoryType.ArmoryRings)
            return false;
            
        // Never discard items that can't be discarded
        if (!item.CanBeDiscarded)
            return false;
            
        // Never discard HQ items unless explicitly selected
        if (item.IsHQ)
            return false;
            
        // Never discard collectables
        if (item.IsCollectable)
            return false;
            
        // Never discard spiritbonded items
        if (item.SpiritBond >= 100)
            return false;
            
        return true;
    }
}
