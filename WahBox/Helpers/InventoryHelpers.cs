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
    
    // ARDiscard-level safety lists
    public static readonly HashSet<uint> HardcodedBlacklist = new()
    {
        // Preorder earrings
        16039, // Ala Mhigan earrings
        24589, // Aetheryte earrings
        33648, // Menphina's earrings
        41081, // Azeyma's earrings
        
        // Ultimate tokens
        21197, // UCOB token
        23175, // UWU token
        28633, // TEA token
        36810, // DSR token
        38951, // TOP token
        
        // Special items
        10155, // Ceruleum Tank
        10373, // Magitek Repair Materials
    };
    
    // Add currency range (1-99) to blacklist
    public static readonly HashSet<uint> CurrencyRange = 
        Enumerable.Range(1, 99).Select(x => (uint)x).ToHashSet();
    
    // Items that are safe to discard despite being unique/untradeable
    public static readonly HashSet<uint> SafeUniqueItems = new()
    {
        2962, // Onion Doublet
        3279, // Onion Gaskins
        3743, // Onion Patterns
        
        9387, // Antique Helm
        9388, // Antique Mail
        9389, // Antique Gauntlets
        9390, // Antique Breeches
        9391, // Antique Sollerets
        
        6223, // Mended Imperial Pot Helm
        6224, // Mended Imperial Short Robe
        
        7060, // Durability Draught
        14945, // Squadron Enlistment Manual
        15772, // Contemporary Warfare: Defense
        15773, // Contemporary Warfare: Offense
        15774, // Contemporary Warfare: Magicks
        4572, // Company-issue Tonic
        20790, // High Grade Company-issue Tonic
    };
    
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
                    CategoryName = itemData.Value.ItemUICategory.Value.Name.ExtractText(),
                    
                    // Add safety metadata
                    ItemLevel = itemData.Value.LevelItem.RowId,
                    EquipLevel = itemData.Value.LevelEquip,
                    Rarity = itemData.Value.Rarity,
                    IsUnique = itemData.Value.IsUnique,
                    IsUntradable = itemData.Value.IsUntradable,
                    IsIndisposable = itemData.Value.IsIndisposable,
                    EquipSlotCategory = itemData.Value.EquipSlotCategory.RowId
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
        _log.Information($"DiscardItem called for {item.Name} in {item.Container} slot {item.Slot}");
        
        var inventoryManager = InventoryManager.Instance();
        if (inventoryManager == null) 
        {
            _log.Error("InventoryManager is null");
            return;
        }
        
        var inventory = inventoryManager->GetInventoryContainer(item.Container);
        if (inventory == null) 
        {
            _log.Error($"Could not get inventory container {item.Container}");
            return;
        }
        
        var inventoryItem = inventory->GetInventorySlot(item.Slot);
        if (inventoryItem == null) 
        {
            _log.Error($"Could not get inventory item at slot {item.Slot}");
            return;
        }
        
        if (inventoryItem->ItemId != item.ItemId) 
        {
            _log.Error($"Item mismatch: expected {item.ItemId}, found {inventoryItem->ItemId}");
            return;
        }
        
        _log.Information($"Calling AgentInventoryContext.DiscardItem for {item.Name}");
        
        // Use AgentInventoryContext to discard the item
        FFXIVClientStructs.FFXIV.Client.UI.Agent.AgentInventoryContext.Instance()->DiscardItem(inventoryItem, item.Container, item.Slot, 0);
        
        _log.Information($"DiscardItem call completed for {item.Name}");
    }
    
    /// <summary>
    /// Advanced safety check based on ARDiscard logic
    /// </summary>
    public static SafetyAssessment AssessItemSafety(InventoryItemInfo item, InventorySettings settings)
    {
        var assessment = new SafetyAssessment 
        { 
            ItemId = item.ItemId,
            IsSafeToDiscard = true,
            SafetyFlags = new List<string>()
        };
        
        var filters = settings.SafetyFilters;
        
        // Check user blacklist first
        if (settings.BlacklistedItems.Contains(item.ItemId))
        {
            assessment.IsSafeToDiscard = false;
            assessment.SafetyFlags.Add("User Blacklisted");
            assessment.FlagColor = SafetyFlagColor.Critical;
            return assessment;
        }
        
        // Hard-coded blacklist (ultimate tokens, preorder items)
        if (filters.FilterUltimateTokens && HardcodedBlacklist.Contains(item.ItemId))
        {
            assessment.IsSafeToDiscard = false;
            assessment.SafetyFlags.Add("Ultimate/Special Item");
            assessment.FlagColor = SafetyFlagColor.Critical;
            return assessment;
        }
        
        // Currency items
        if (filters.FilterCurrencyItems && CurrencyRange.Contains(item.ItemId))
        {
            assessment.IsSafeToDiscard = false;
            assessment.SafetyFlags.Add("Currency");
            assessment.FlagColor = SafetyFlagColor.Critical;
            return assessment;
        }
        
        // Category-based filtering
        if (filters.FilterCrystalsAndShards && (item.ItemUICategory == 63 || item.ItemUICategory == 64)) // Crystals/Shards
        {
            assessment.IsSafeToDiscard = false;
            assessment.SafetyFlags.Add("Crystal/Shard");
            assessment.FlagColor = SafetyFlagColor.Critical;
            return assessment;
        }
        
        // Indisposable items
        if (filters.FilterIndisposableItems && item.IsIndisposable)
        {
            assessment.IsSafeToDiscard = false;
            assessment.SafetyFlags.Add("Indisposable");
            assessment.FlagColor = SafetyFlagColor.Critical;
            return assessment;
        }
        
        // Gearset protection
        if (filters.FilterGearsetItems && IsInGearset(item.ItemId))
        {
            assessment.IsSafeToDiscard = false;
            assessment.SafetyFlags.Add("In Gearset");
            assessment.FlagColor = SafetyFlagColor.Critical;
            return assessment;
        }
        
        // High-level gear protection
        if (filters.FilterHighLevelGear && item.EquipSlotCategory > 0 && 
            item.ItemLevel >= filters.MaxGearItemLevel)
        {
            assessment.IsSafeToDiscard = false;
            assessment.SafetyFlags.Add($"High Level (i{item.ItemLevel})");
            assessment.FlagColor = SafetyFlagColor.Warning;
            return assessment;
        }
        
        // Unique/Untradeable check (with whitelist exceptions)
        if (filters.FilterUniqueUntradeable && item.IsUnique && item.IsUntradable && 
            !SafeUniqueItems.Contains(item.ItemId))
        {
            assessment.IsSafeToDiscard = false;
            assessment.SafetyFlags.Add("Unique & Untradeable");
            assessment.FlagColor = SafetyFlagColor.Warning;
            return assessment;
        }
        
        // HQ items
        if (filters.FilterHQItems && item.IsHQ)
        {
            assessment.IsSafeToDiscard = false;
            assessment.SafetyFlags.Add("High Quality");
            assessment.FlagColor = SafetyFlagColor.Caution;
            return assessment;
        }
        
        // Collectables
        if (filters.FilterCollectables && item.IsCollectable)
        {
            assessment.IsSafeToDiscard = false;
            assessment.SafetyFlags.Add("Collectable");
            assessment.FlagColor = SafetyFlagColor.Warning;
            return assessment;
        }
        
        // Spiritbond check
        if (filters.FilterSpiritbondedItems && item.SpiritBond >= filters.MinSpiritbondToFilter)
        {
            assessment.IsSafeToDiscard = false;
            assessment.SafetyFlags.Add($"Spiritbond {item.SpiritBond}%");
            assessment.FlagColor = SafetyFlagColor.Caution;
            return assessment;
        }
        
        // If we made it here, item appears safe
        // But add informational flags for awareness
        if (item.IsHQ && !filters.FilterHQItems)
            assessment.SafetyFlags.Add("HQ");
        if (item.Rarity >= 3)
            assessment.SafetyFlags.Add($"Rare ({item.Rarity}⭐)");
        if (item.EquipSlotCategory > 0 && item.ItemLevel > 0)
            assessment.SafetyFlags.Add($"i{item.ItemLevel}");
        
        if (assessment.SafetyFlags.Any())
            assessment.FlagColor = SafetyFlagColor.Info;
        
        return assessment;
    }
    
    /// <summary>
    /// Legacy method for backwards compatibility
    /// </summary>
    public static bool IsSafeToDiscard(InventoryItemInfo item, HashSet<uint> blacklist)
    {
        // Create minimal settings for legacy check
        var settings = new InventorySettings 
        { 
            BlacklistedItems = blacklist,
            SafetyFilters = new SafetyFilters() // All filters enabled by default
        };
        
        return AssessItemSafety(item, settings).IsSafeToDiscard;
    }
    
    public static unsafe bool IsInGearset(uint itemId)
    {
        var gearsetModule = RaptureGearsetModule.Instance();
        if (gearsetModule == null) return false;
        
        for (int i = 0; i < 100; i++)
        {
            var gearset = gearsetModule->GetGearset(i);
            if (gearset == null || !gearset->Flags.HasFlag(RaptureGearsetModule.GearsetFlag.Exists))
                continue;
                
            var gearsetItems = new[]
            {
                gearset->GetItem(RaptureGearsetModule.GearsetItemIndex.MainHand),
                gearset->GetItem(RaptureGearsetModule.GearsetItemIndex.OffHand),
                gearset->GetItem(RaptureGearsetModule.GearsetItemIndex.Head),
                gearset->GetItem(RaptureGearsetModule.GearsetItemIndex.Body),
                gearset->GetItem(RaptureGearsetModule.GearsetItemIndex.Hands),
                gearset->GetItem(RaptureGearsetModule.GearsetItemIndex.Legs),
                gearset->GetItem(RaptureGearsetModule.GearsetItemIndex.Feet),
                gearset->GetItem(RaptureGearsetModule.GearsetItemIndex.Ears),
                gearset->GetItem(RaptureGearsetModule.GearsetItemIndex.Neck),
                gearset->GetItem(RaptureGearsetModule.GearsetItemIndex.Wrists),
                gearset->GetItem(RaptureGearsetModule.GearsetItemIndex.RingLeft),
                gearset->GetItem(RaptureGearsetModule.GearsetItemIndex.RingRight),
            };
            
            foreach (var gearsetItem in gearsetItems)
            {
                if (gearsetItem.ItemId == itemId)
                    return true;
            }
        }
        
        return false;
    }
}
