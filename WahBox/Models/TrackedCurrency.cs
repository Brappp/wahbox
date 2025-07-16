using System;
using System.Text.Json.Serialization;
using FFXIVClientStructs.FFXIV.Client.Game;
using Lumina.Excel.Sheets;
using Dalamud.Interface.Textures;
using Dalamud.Interface.Textures.TextureWraps;

namespace WahBox.Models;

public enum CurrencyType
{
    Item,
    HighQualityItem,
    Collectable,
    NonLimitedTomestone,
    LimitedTomestone,
}

public unsafe class TrackedCurrency
{
    private uint? _iconId;
    private uint? _itemId;
    private string? _name;
    private int? _maxCount;

    public required CurrencyType Type { get; init; }
    public required int Threshold { get; set; }
    
    public int MaxCount 
    { 
        get => _maxCount ?? Core.CurrencyHelper.GetCurrencyMax(ItemId);
        set => _maxCount = value;
    }
    public bool Enabled { get; set; } = true;
    public bool ChatWarning { get; set; }
    public bool ShowInOverlay { get; set; }
    public bool ShowItemName { get; set; } = true;
    public bool Invert { get; set; }
    public string WarningText { get; set; } = "Above Threshold";

    public uint ItemId
    {
        get => GetItemId();
        init => _itemId = IsSpecialCurrency() ? GetItemId() : value;
    }

    [JsonIgnore]
    public uint IconId
    {
        get => _iconId ??= Plugin.DataManager.GetExcelSheet<Item>()!.GetRow(ItemId)!.Icon;
        set => _iconId = value;
    }

    [JsonIgnore]
    public IDalamudTextureWrap? Icon => Plugin.TextureProvider.GetFromGameIcon(new GameIconLookup
    {
        HiRes = true,
        ItemHq = Type is CurrencyType.HighQualityItem,
        IconId = IconId,
    }).GetWrapOrEmpty();

    [JsonIgnore]
    public string Name => _name ??= Plugin.DataManager.GetExcelSheet<Item>()!.GetRow(ItemId)!.Name.ExtractText();

    [JsonIgnore]
    public bool CanRemove => Type is not (CurrencyType.LimitedTomestone or CurrencyType.NonLimitedTomestone);

    [JsonIgnore]
    public int CurrentCount 
    { 
        get
        {
            try
            {
                // Use CurrencyHelper for proper currency handling
                return Core.CurrencyHelper.GetCurrencyCount(ItemId);
            }
            catch
            {
                return 0;
            }
        }
    }

    [JsonIgnore]
    public bool HasWarning => Invert ? CurrentCount < Threshold : CurrentCount > Threshold;

    private uint GetItemId()
    {
        // Force regenerate itemId for special currencies
        if (IsSpecialCurrency() && _itemId is 0 or null)
        {
            _itemId = Type switch
            {
                CurrencyType.NonLimitedTomestone => GetCurrentNonLimitedTomestoneId(),
                CurrencyType.LimitedTomestone => GetCurrentLimitedTomestoneId(),
                _ => throw new Exception($"ItemId not initialized for type: {Type}"),
            };
            
            // Set max count for tomestones
            if (_itemId > 0 && (Type == CurrencyType.NonLimitedTomestone || Type == CurrencyType.LimitedTomestone))
            {
                MaxCount = 2000;
            }
        }

        return _itemId ?? 0;
    }

    private bool IsSpecialCurrency() => Type switch
    {
        CurrencyType.NonLimitedTomestone => true,
        CurrencyType.LimitedTomestone => true,
        _ => false,
    };

    private static uint GetCurrentNonLimitedTomestoneId()
    {
        foreach (var item in Plugin.DataManager.GetExcelSheet<TomestonesItem>()!)
        {
            if (item.Tomestones.RowId == 2) // Non-limited tomestone
                return item.Item.RowId;
        }
        return 0;
    }

    private static uint GetCurrentLimitedTomestoneId()
    {
        foreach (var item in Plugin.DataManager.GetExcelSheet<TomestonesItem>()!)
        {
            if (item.Tomestones.RowId == 3) // Limited tomestone
                return item.Item.RowId;
        }
        return 0;
    }
} 