using System;
using System.Collections.Generic;
using System.Linq;
using Dalamud.Game.Text.SeStringHandling;
using FFXIVClientStructs.FFXIV.Client.Game;

namespace WahBox.Models;

public class InventoryItemInfo
{
    public uint ItemId { get; set; }
    public string Name { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public InventoryType Container { get; set; }
    public short Slot { get; set; }
    public bool IsHQ { get; set; }
    public uint IconId { get; set; }
    public bool CanBeDiscarded { get; set; }
    public bool CanBeTraded { get; set; }
    public bool IsCollectable { get; set; }
    public uint SpiritBond { get; set; }
    public ushort Durability { get; set; }
    public ushort MaxDurability { get; set; }
    
    // Category info
    public string CategoryName { get; set; } = "Miscellaneous";
    public uint ItemUICategory { get; set; }
    
    // Market price data
    public long? MarketPrice { get; set; }
    public bool MarketPriceLoading { get; set; }
    public DateTime? MarketPriceFetchTime { get; set; }
    
    // Selection
    public bool IsSelected { get; set; }
    
    // Computed properties
    public bool IsGear => ItemUICategory >= 1 && ItemUICategory <= 11;
    public bool IsCurrency => ItemUICategory >= 58 && ItemUICategory <= 63;
    public bool IsCrystal => ItemUICategory == 59;
    public bool IsFood => ItemUICategory == 46;
    public bool IsMedicine => ItemUICategory == 44;
    
    public string GetFormattedPrice()
    {
        if (MarketPriceLoading) return "Loading...";
        if (!MarketPrice.HasValue) return "---";
        if (MarketPrice.Value == -1) return "N/A";
        return $"{MarketPrice.Value:N0} gil";
    }
    
    public string GetUniqueKey() => $"{Container}_{Slot}";
}

public class CategoryGroup
{
    public uint CategoryId { get; set; }
    public string Name { get; set; } = string.Empty;
    public List<InventoryItemInfo> Items { get; set; } = new();
    public bool IsExpanded { get; set; } = true;
    
    public int TotalQuantity => Items.Sum(i => i.Quantity);
    public long? TotalValue => Items.All(i => i.MarketPrice.HasValue && i.MarketPrice.Value > 0) 
        ? Items.Sum(i => i.MarketPrice!.Value * i.Quantity) 
        : null;
}
