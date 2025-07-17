using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Interface;
using Dalamud.Interface.Utility;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Component.GUI;
using ImGuiNET;
using WahBox.Core;
using WahBox.Core.Interfaces;
using WahBox.External;
using WahBox.Helpers;
using WahBox.Models;
using ECommons.Automation.NeoTaskManager;

namespace WahBox.Modules.Inventory;

public partial class InventoryManagementModule : BaseModule, IDrawable
{
    public override string Name => "Inventory Manager";
    public override ModuleType Type => ModuleType.Special;
    public override ModuleCategory Category => ModuleCategory.Tools;
    public override bool HasWindow => false; // We'll draw directly in the main window
    
    private readonly InventoryHelpers _inventoryHelpers;
    private UniversalisClient _universalisClient;
    private readonly TaskManager _taskManager;
    private readonly IconCache _iconCache;
    private bool _initialized = false;
    
    // Performance optimization
    private DateTime _lastRefresh = DateTime.MinValue;
    private DateTime _lastCategoryUpdate = DateTime.MinValue;
    private readonly TimeSpan _refreshInterval = TimeSpan.FromSeconds(1); // Reduced from 2 to 1 for faster updates
    private readonly TimeSpan _categoryUpdateInterval = TimeSpan.FromMilliseconds(500);
    private bool _expandedCategoriesChanged = false;
    private DateTime _lastConfigSave = DateTime.MinValue;
    private readonly TimeSpan _configSaveInterval = TimeSpan.FromSeconds(2);
    
    // UI State
    private List<CategoryGroup> _categories = new();
    private List<InventoryItemInfo> _allItems = new();
    private string _searchFilter = string.Empty;
    private Dictionary<uint, bool> ExpandedCategories => Settings.ExpandedCategories;
    private readonly HashSet<uint> _selectedItems = new();
    private readonly Dictionary<uint, (long price, DateTime fetchTime)> _priceCache = new();
    private readonly HashSet<uint> _fetchingPrices = new();
    
    // Filter options
    private bool _showInventory = true;
    private bool _showArmory = false;
    private bool _showOnlyHQ = false;
    private bool _showOnlyDiscardable = false;
    private string _selectedWorld = "";
    private List<string> _availableWorlds = new();
    private bool _filtersExpanded = true; // Show filters by default
    
    // Settings references
    private InventorySettings Settings => Plugin.Configuration.InventorySettings;
    
    // Discard state
    private bool _isDiscarding = false;
    private List<InventoryItemInfo> _itemsToDiscard = new();
    private int _discardProgress = 0;
    private string? _discardError = null;
    
    public InventoryManagementModule(Plugin plugin) : base(plugin)
    {
        _inventoryHelpers = new InventoryHelpers(Plugin.DataManager, Plugin.Log);
        _iconCache = new IconCache(Plugin.TextureProvider);
        
        // Initialize with default world name, will be recreated in Initialize()
        _universalisClient = new UniversalisClient(Plugin.Log, "Aether");
        _taskManager = new TaskManager();
    }
    
    public override void Initialize()
    {
        base.Initialize();
        // Don't access ClientState here - defer to first use
    }
    
    public override void Update()
    {
        // Lazy initialization when we're sure to be on main thread
        if (!_initialized)
        {
            InitializeOnMainThread();
        }
        
        // Only update prices every few seconds to reduce CPU load
        if (Settings.AutoRefreshPrices && !_isDiscarding && DateTime.Now - _lastRefresh > _refreshInterval)
        {
            _lastRefresh = DateTime.Now;
            
            var stalePrices = _allItems.Where(item => 
                item.CanBeTraded && // Only check prices for tradable items
                !_fetchingPrices.Contains(item.ItemId) &&
                (!_priceCache.TryGetValue(item.ItemId, out var cached) || 
                 DateTime.Now - cached.fetchTime > TimeSpan.FromMinutes(Settings.PriceCacheDurationMinutes)))
                .Take(10) // Increased from 5 to 10 for faster price fetching
                .ToList();
                
            foreach (var item in stalePrices)
            {
                _ = FetchMarketPrice(item);
            }
        }
        
        // Save config periodically if needed
        if (_expandedCategoriesChanged && DateTime.Now - _lastConfigSave > _configSaveInterval)
        {
            Plugin.Configuration.Save();
            _expandedCategoriesChanged = false;
            _lastConfigSave = DateTime.Now;
        }
    }
    
    public void Draw()
    {
        // Ensure initialization before drawing
        if (!_initialized)
        {
            InitializeOnMainThread();
        }
        if (_isDiscarding)
        {
            DrawDiscardConfirmation();
            return;
        }
        
        // Search bar
        ImGui.SetNextItemWidth(300);
        if (ImGui.InputTextWithHint("##Search", "Search items...", ref _searchFilter, 100))
        {
            // Debounce category updates
            _lastCategoryUpdate = DateTime.Now;
        }
        
        // Only update categories after a delay to avoid updating on every keystroke
        if (_lastCategoryUpdate != DateTime.MinValue && DateTime.Now - _lastCategoryUpdate > _categoryUpdateInterval)
        {
            UpdateCategories();
            _lastCategoryUpdate = DateTime.MinValue;
        }
        
        ImGui.SameLine();
        if (ImGui.Button("Refresh"))
        {
            RefreshInventory();
        }
        
        ImGui.SameLine();
        ImGui.Text($"Total Items: {_allItems.Count} | Selected: {_selectedItems.Count}");
        
        // Compact filter section
        ImGui.Separator();
        
        // First row: Location filters
        ImGui.AlignTextToFramePadding();
        ImGui.Text("Show:");
        ImGui.SameLine();
        ImGui.TextColored(new Vector4(0.7f, 0.7f, 0.7f, 1), "[Inventory]");
        ImGui.SameLine();
        if (ImGui.Checkbox("Armory", ref _showArmory)) 
        {
            RefreshInventory();
        }
        
        // Separator
        ImGui.SameLine();
        ImGui.TextColored(new Vector4(0.5f, 0.5f, 0.5f, 1), "|");
        
        // Item filters on same row
        ImGui.SameLine();
        if (ImGui.Checkbox("HQ Only", ref _showOnlyHQ)) UpdateCategories();
        ImGui.SameLine();
        if (ImGui.Checkbox("Discardable Only", ref _showOnlyDiscardable)) UpdateCategories();
        
        // Settings bar
        ImGui.Separator();
        var showPrices = Settings.ShowMarketPrices;
        if (ImGui.Checkbox("Show Market Prices", ref showPrices))
        {
            Settings.ShowMarketPrices = showPrices;
            Plugin.Configuration.Save();
        }
        
        if (Settings.ShowMarketPrices)
        {
            ImGui.SameLine();
            var autoRefresh = Settings.AutoRefreshPrices;
            if (ImGui.Checkbox("Auto-refresh Prices", ref autoRefresh))
            {
                Settings.AutoRefreshPrices = autoRefresh;
                Plugin.Configuration.Save();
            }
            
            ImGui.SameLine();
            ImGui.SetNextItemWidth(100);
            var cacheMinutes = Settings.PriceCacheDurationMinutes;
            if (ImGui.InputInt("Cache (min)", ref cacheMinutes))
            {
                Settings.PriceCacheDurationMinutes = Math.Max(1, cacheMinutes);
                Plugin.Configuration.Save();
            }
            
            // World selection for market prices
            ImGui.SameLine();
            ImGui.SetNextItemWidth(150);
            if (ImGui.BeginCombo("World", _selectedWorld))
            {
                foreach (var world in _availableWorlds)
                {
                    bool isSelected = world == _selectedWorld;
                    if (ImGui.Selectable(world, isSelected))
                    {
                        _selectedWorld = world;
                        // Recreate client with new world
                        _universalisClient.Dispose();
                        _universalisClient = new UniversalisClient(Plugin.Log, _selectedWorld);
                        // Clear price cache to fetch new prices
                        _priceCache.Clear();
                        foreach (var item in _allItems)
                        {
                            item.MarketPrice = null;
                            item.MarketPriceFetchTime = null;
                        }
                    }
                    if (isSelected)
                        ImGui.SetItemDefaultFocus();
                }
                ImGui.EndCombo();
            }
        }
        
        // Action buttons
        ImGui.SameLine(ImGui.GetContentRegionMax().X - 250);
        if (ImGui.Button("Select All"))
        {
            foreach (var item in _allItems.Where(i => InventoryHelpers.IsSafeToDiscard(i, Settings.BlacklistedItems)))
            {
                _selectedItems.Add(item.ItemId);
                item.IsSelected = true;
            }
        }
        
        ImGui.SameLine();
        if (ImGui.Button("Clear Selection"))
        {
            _selectedItems.Clear();
            foreach (var item in _allItems)
            {
                item.IsSelected = false;
            }
        }
        
        ImGui.SameLine();
        if (_selectedItems.Count > 0)
        {
            ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(0.8f, 0.2f, 0.2f, 1));
            if (ImGui.Button($"Discard Selected ({_selectedItems.Count})"))
            {
                PrepareDiscard();
            }
            ImGui.PopStyleColor();
        }
        else
        {
            ImGui.BeginDisabled();
            ImGui.Button("Discard Selected (0)");
            ImGui.EndDisabled();
        }
        
        ImGui.Separator();
        
        // Main content area
        ImGui.BeginChild("InventoryContent", new Vector2(0, 0), false);
        
        foreach (var category in _categories)
        {
            if (category.Items.Count == 0) continue;
            
            var isExpanded = ExpandedCategories.GetValueOrDefault(category.CategoryId, true);
            
            ImGui.PushID(category.Name);
            
            // Category header
            var headerText = $"{category.Name} ({category.Items.Count} items, {category.TotalQuantity} total)";
            if (Settings.ShowMarketPrices && category.TotalValue.HasValue)
            {
                headerText += $" - {category.TotalValue.Value:N0} gil";
            }
            
            if (ImGui.CollapsingHeader(headerText, isExpanded ? ImGuiTreeNodeFlags.DefaultOpen : ImGuiTreeNodeFlags.None))
            {
                ExpandedCategories[category.CategoryId] = true;
                _expandedCategoriesChanged = true;
                DrawCategoryItems(category);
                
                // Immediately fetch prices for visible tradable items in this category
                if (Settings.ShowMarketPrices)
                {
                    var tradableItems = category.Items.Where(i => i.CanBeTraded && !i.MarketPrice.HasValue && !_fetchingPrices.Contains(i.ItemId)).Take(5);
                    foreach (var item in tradableItems)
                    {
                        _ = FetchMarketPrice(item);
                    }
                }
            }
            else
            {
                ExpandedCategories[category.CategoryId] = false;
                _expandedCategoriesChanged = true;
            }
            
            ImGui.PopID();
        }
        
        ImGui.EndChild();
    }
    
    private void DrawCategoryItems(CategoryGroup category)
    {
        ImGui.Indent();
        
        if (ImGui.BeginTable($"ItemTable_{category.Name}", Settings.ShowMarketPrices ? 6 : 5, 
            ImGuiTableFlags.Borders | ImGuiTableFlags.RowBg | ImGuiTableFlags.SizingStretchProp))
        {
            ImGui.TableSetupColumn("", ImGuiTableColumnFlags.WidthFixed, 25); // Checkbox
            ImGui.TableSetupColumn("Item", ImGuiTableColumnFlags.WidthStretch);
            ImGui.TableSetupColumn("Qty", ImGuiTableColumnFlags.WidthFixed, 50);
            ImGui.TableSetupColumn("Location", ImGuiTableColumnFlags.WidthFixed, 120);
            if (Settings.ShowMarketPrices)
            {
                ImGui.TableSetupColumn("Price", ImGuiTableColumnFlags.WidthFixed, 100);
                ImGui.TableSetupColumn("Total", ImGuiTableColumnFlags.WidthFixed, 100);
            }
            else
            {
                ImGui.TableSetupColumn("Status", ImGuiTableColumnFlags.WidthFixed, 100);
            }
            
            ImGui.TableHeadersRow();
            
            foreach (var item in category.Items)
            {
                DrawItemRow(item, category);
            }
            
            ImGui.EndTable();
        }
        
        ImGui.Unindent();
    }
    
    private void DrawDiscardConfirmation()
    {
        var windowSize = new Vector2(500, 400);
        ImGui.SetNextWindowSize(windowSize, ImGuiCond.Always);
        ImGui.SetNextWindowPos(ImGui.GetMainViewport().GetCenter(), ImGuiCond.Always, new Vector2(0.5f, 0.5f));
        
        ImGui.Begin("Confirm Discard", ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoMove);
        
        ImGui.TextColored(new Vector4(0.8f, 0.2f, 0.2f, 1), "WARNING: This will permanently delete the following items!");
        ImGui.Separator();
        
        ImGui.BeginChild("ItemList", new Vector2(0, 280), true);
        
        var totalValue = 0L;
        foreach (var item in _itemsToDiscard)
        {
            ImGui.Text($"• {item.Name} x{item.Quantity}");
            if (item.MarketPrice.HasValue)
            {
                ImGui.SameLine();
                ImGui.TextColored(new Vector4(0.5f, 0.5f, 0.5f, 1), 
                    $"({item.MarketPrice.Value * item.Quantity:N0} gil)");
                totalValue += item.MarketPrice.Value * item.Quantity;
            }
        }
        
        ImGui.EndChild();
        
        ImGui.Text($"Total items: {_itemsToDiscard.Count}");
        if (totalValue > 0)
        {
            ImGui.Text($"Total value: {totalValue:N0} gil");
        }
        
        if (!string.IsNullOrEmpty(_discardError))
        {
            ImGui.TextColored(new Vector4(0.8f, 0.2f, 0.2f, 1), _discardError);
        }
        
        ImGui.Separator();
        
        if (_discardProgress > 0)
        {
            var progress = (float)_discardProgress / _itemsToDiscard.Count;
            ImGui.ProgressBar(progress, new Vector2(-1, 0), $"{_discardProgress}/{_itemsToDiscard.Count}");
        }
        
        if (_discardProgress == 0)
        {
            if (ImGui.Button("Start Discarding", new Vector2(120, 30)))
            {
                StartDiscarding();
            }
            
            ImGui.SameLine();
        }
        
        if (ImGui.Button("Cancel", new Vector2(120, 30)))
        {
            CancelDiscard();
        }
        
        ImGui.End();
    }
    
    private void InitializeOnMainThread()
    {
        if (_initialized) return;
        
        try
        {
            // Now we can safely access ClientState
            var currentWorld = Plugin.ClientState.LocalPlayer?.CurrentWorld.Value;
            var worldName = currentWorld?.Name.ExtractText() ?? "Aether";
            _selectedWorld = worldName;
            
            // Get available worlds for the current datacenter
            try
            {
                // Get the current world ID from the name
                var allWorlds = Plugin.DataManager.GetExcelSheet<Lumina.Excel.Sheets.World>();
                if (allWorlds != null && !string.IsNullOrEmpty(worldName))
                {
                    // Find the current world by name
                    var currentWorldData = allWorlds.FirstOrDefault(w => w.Name.ExtractText() == worldName);
                    if (currentWorldData.RowId != 0)  // Check if we found a valid world
                    {
                        var currentDatacenterId = currentWorldData.DataCenter.RowId;
                        
                        // Get all worlds in the same datacenter
                        var datacenterWorlds = allWorlds
                            .Where(w => w.DataCenter.RowId == currentDatacenterId && w.IsPublic)
                            .Select(w => w.Name.ExtractText())
                            .Where(name => !string.IsNullOrEmpty(name))
                            .OrderBy(w => w)
                            .ToList();
                            
                        _availableWorlds = datacenterWorlds;
                    }
                    else
                    {
                        _availableWorlds = new List<string> { worldName };
                    }
                }
                else
                {
                    _availableWorlds = new List<string> { worldName };
                }
            }
            catch (Exception ex)
            {
                Plugin.Log.Warning($"Failed to get datacenter worlds: {ex.Message}");
                // Fallback to just the current world
                _availableWorlds = new List<string> { worldName };
            }
            
            _universalisClient.Dispose();
            _universalisClient = new UniversalisClient(Plugin.Log, worldName);
            
            RefreshInventory();
            _initialized = true;
        }
        catch (Exception ex)
        {
            Plugin.Log.Error(ex, "Failed to initialize InventoryManagementModule on main thread");
        }
    }
    
    private void RefreshInventory()
    {
        _allItems = _inventoryHelpers.GetAllItems(_showArmory, false); // Always false for saddlebag
        
        // Update market prices from cache
        foreach (var item in _allItems)
        {
            if (_priceCache.TryGetValue(item.ItemId, out var cached))
            {
                item.MarketPrice = cached.price;
                item.MarketPriceFetchTime = cached.fetchTime;
            }
            
            // Restore selection state
            item.IsSelected = _selectedItems.Contains(item.ItemId);
        }
        
        UpdateCategories();
        
        // Don't fetch prices immediately on refresh - let Update() handle it
    }
    
    private void UpdateCategories()
    {
        var filteredItems = _allItems.AsEnumerable();
        
        // Apply text search filter
        if (!string.IsNullOrWhiteSpace(_searchFilter))
        {
            filteredItems = filteredItems.Where(i => i.Name.Contains(_searchFilter, StringComparison.OrdinalIgnoreCase));
        }
        
        // Apply item filters
        if (_showOnlyHQ)
        {
            filteredItems = filteredItems.Where(i => i.IsHQ);
        }
        
        if (_showOnlyDiscardable)
        {
            filteredItems = filteredItems.Where(i => InventoryHelpers.IsSafeToDiscard(i, Settings.BlacklistedItems));
        }
            
        _categories = filteredItems
            .GroupBy(i => new { i.ItemUICategory, i.CategoryName })
            .Select(categoryGroup => new CategoryGroup
            {
                CategoryId = categoryGroup.Key.ItemUICategory,
                Name = categoryGroup.Key.CategoryName,
                Items = categoryGroup
                    .GroupBy(i => i.ItemId) // Group same items across different locations
                    .Select(itemGroup => 
                    {
                        var first = itemGroup.First();
                        // Create a combined item info
                        return new InventoryItemInfo
                        {
                            ItemId = first.ItemId,
                            Name = first.Name,
                            Quantity = itemGroup.Sum(i => i.Quantity), // Sum quantities
                            Container = first.Container, // Keep first container for reference
                            Slot = first.Slot,
                            IsHQ = first.IsHQ,
                            IconId = first.IconId,
                            CanBeDiscarded = first.CanBeDiscarded,
                            CanBeTraded = first.CanBeTraded,
                            IsCollectable = first.IsCollectable,
                            SpiritBond = first.SpiritBond,
                            Durability = first.Durability,
                            MaxDurability = first.MaxDurability,
                            CategoryName = first.CategoryName,
                            ItemUICategory = first.ItemUICategory,
                            MarketPrice = first.MarketPrice,
                            MarketPriceLoading = first.MarketPriceLoading,
                            MarketPriceFetchTime = first.MarketPriceFetchTime,
                            IsSelected = first.IsSelected
                        };
                    })
                    .OrderBy(i => i.Name)
                    .ToList()
            })
            .OrderBy(c => c.Name)
            .ToList();
    }
    
    private async Task FetchMarketPrice(InventoryItemInfo item)
    {
        if (_fetchingPrices.Contains(item.ItemId)) return;
        if (!item.CanBeTraded) return; // Skip untradable items
        
        _fetchingPrices.Add(item.ItemId);
        item.MarketPriceLoading = true;
        
        try
        {
            var result = await _universalisClient.GetMarketPrice(item.ItemId, item.IsHQ);
            
            if (result != null)
            {
                item.MarketPrice = result.Price;
                item.MarketPriceFetchTime = DateTime.Now;
                _priceCache[item.ItemId] = (result.Price, DateTime.Now);
            }
        }
        catch (Exception ex)
        {
            Plugin.Log.Error(ex, $"Failed to fetch price for {item.Name}");
        }
        finally
        {
            _fetchingPrices.Remove(item.ItemId);
            item.MarketPriceLoading = false;
        }
    }
    
    private void PrepareDiscard()
    {
        _itemsToDiscard = _allItems
            .Where(i => i.IsSelected && InventoryHelpers.IsSafeToDiscard(i, Settings.BlacklistedItems))
            .ToList();
            
        if (_itemsToDiscard.Count > 0)
        {
            _isDiscarding = true;
            _discardProgress = 0;
            _discardError = null;
        }
    }
    
    private void StartDiscarding()
    {
        _taskManager.Abort();
        _taskManager.Enqueue(() => DiscardNextItem());
    }
    
    private unsafe void DiscardNextItem()
    {
        if (_discardProgress >= _itemsToDiscard.Count)
        {
            Plugin.ChatGui.Print("Finished discarding items.");
            CancelDiscard();
            return;
        }
        
        var item = _itemsToDiscard[_discardProgress];
        
        try
        {
            _inventoryHelpers.DiscardItem(item);
            _discardProgress++;
            
            _taskManager.EnqueueDelay(500);
            _taskManager.Enqueue(() => ConfirmDiscard());
        }
        catch (Exception ex)
        {
            _discardError = $"Failed to discard {item.Name}: {ex.Message}";
            Plugin.Log.Error(ex, $"Failed to discard {item.Name}");
        }
    }
    
    private unsafe void ConfirmDiscard()
    {
        var addon = GetDiscardAddon();
        if (addon != null)
        {
            // Click Yes
            var yesButton = addon->UldManager.NodeList[1]->GetAsAtkComponentButton();
            if (yesButton != null)
            {
                yesButton->AtkComponentBase.SetEnabledState(true);
                addon->FireCallbackInt(0);
            }
            
            _taskManager.EnqueueDelay(500);
            _taskManager.Enqueue(() => DiscardNextItem());
        }
        else
        {
            // No dialog, continue
            _taskManager.EnqueueDelay(100);
            _taskManager.Enqueue(() => DiscardNextItem());
        }
    }
    
    private unsafe AtkUnitBase* GetDiscardAddon()
    {
        for (int i = 1; i < 100; i++)
        {
            try
            {
                var addon = (AtkUnitBase*)Plugin.GameGui.GetAddonByName("SelectYesno", i);
                if (addon == null || !addon->IsVisible) continue;
                
                // Check if it's a discard dialog
                var textNode = addon->UldManager.NodeList[15]->GetAsAtkTextNode();
                if (textNode != null)
                {
                    var text = Dalamud.Memory.MemoryHelper.ReadSeString(&textNode->NodeText).TextValue;
                    if (text.Contains("Discard", StringComparison.OrdinalIgnoreCase))
                    {
                        return addon;
                    }
                }
            }
            catch
            {
                // Ignore
            }
        }
        
        return null;
    }
    
    private void CancelDiscard()
    {
        _taskManager.Abort();
        _isDiscarding = false;
        _itemsToDiscard.Clear();
        _discardProgress = 0;
        _discardError = null;
        
        // Refresh inventory after discard
        RefreshInventory();
    }
    
    private string GetLocationName(InventoryType type)
    {
        return type switch
        {
            InventoryType.Inventory1 => "Inventory 1",
            InventoryType.Inventory2 => "Inventory 2",
            InventoryType.Inventory3 => "Inventory 3",
            InventoryType.Inventory4 => "Inventory 4",
            InventoryType.Crystals => "Crystals",
            InventoryType.Currency => "Currency",
            InventoryType.SaddleBag1 => "Saddlebag 1",
            InventoryType.SaddleBag2 => "Saddlebag 2",
            InventoryType.PremiumSaddleBag1 => "P.Saddlebag 1",
            InventoryType.PremiumSaddleBag2 => "P.Saddlebag 2",
            // Armory items with specific categories
            InventoryType.ArmoryMainHand => "Armory (Main)",
            InventoryType.ArmoryOffHand => "Armory (Off)",
            InventoryType.ArmoryHead => "Armory (Head)",
            InventoryType.ArmoryBody => "Armory (Body)",
            InventoryType.ArmoryHands => "Armory (Hands)",
            InventoryType.ArmoryLegs => "Armory (Legs)",
            InventoryType.ArmoryFeets => "Armory (Feet)",
            InventoryType.ArmoryEar => "Armory (Ears)",
            InventoryType.ArmoryNeck => "Armory (Neck)",
            InventoryType.ArmoryWrist => "Armory (Wrist)",
            InventoryType.ArmoryRings => "Armory (Rings)",
            _ => type.ToString()
        };
    }
    
    public override void Dispose()
    {
        // Save any pending config changes
        if (_expandedCategoriesChanged)
        {
            Plugin.Configuration.Save();
        }
        
        _iconCache?.Dispose();
        _taskManager?.Dispose();
        _universalisClient?.Dispose();
        base.Dispose();
    }
}
