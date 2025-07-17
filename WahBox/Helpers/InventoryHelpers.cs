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
    
    // Separate inventory type groups
    private static readonly InventoryType[] MainInventories = 
    {
        InventoryType.Inventory1,
        InventoryType.Inventory2, 
        InventoryType.Inventory3,
        InventoryType.Inventory4
    };
    
    private static readonly InventoryType[] ArmoryInventories = 
    {
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
        InventoryType.ArmoryRings
    };
    
    private static readonly InventoryType[] SaddlebagInventories = 
    {
        InventoryType.SaddleBag1,
        InventoryType.SaddleBag2,
        InventoryType.PremiumSaddleBag1,
        InventoryType.PremiumSaddleBag2
    };
    
    private static readonly InventoryType[] OtherInventories = 
    {
        InventoryType.Crystals,
        InventoryType.Currency
    };
    
    public InventoryHelpers(IDataManager dataManager, IPluginLog log)
    {
        _dataManager = dataManager;
        _log = log;
    }
    
    public List<InventoryItemInfo> GetAllItems(bool includeArmory = false, bool includeSaddlebag = false)
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
        
        // Always scan main inventory
        var inventoriesToScan = new List<InventoryType>(MainInventories);
        
        // Add optional inventories
        if (includeArmory)
            inventoriesToScan.AddRange(ArmoryInventories);
        if (includeSaddlebag)
            inventoriesToScan.AddRange(SaddlebagInventories);
            
        // Note: We're intentionally excluding Crystals and Currency
        // as they're not regular items that can be discarded
        
        foreach (var inventoryType in inventoriesToScan)
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
                    CanBeTraded = itemData.Value.IsUntradable == false,
                    IsCollectable = item->Flags.HasFlag(InventoryItem.ItemFlags.Collectable),
                    SpiritBond = 0, // Spiritbond property not available in current ClientStructs
                    ItemUICategory = itemData.Value.ItemUICategory.RowId,
                    CategoryName = itemData.Value.ItemUICategory.Value.Name.ExtractText()
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
