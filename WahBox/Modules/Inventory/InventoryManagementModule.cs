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
    public override ModuleCategory Category => ModuleCategory.Tracking;
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
    private bool _windowIsOpen = false; // Tracks if window is currently being drawn for price fetch optimization
    
    // UI State
    private List<CategoryGroup> _categories = new();
    private List<InventoryItemInfo> _allItems = new();
    private List<InventoryItemInfo> _originalItems = new(); // Keep track of original individual items
    private string _searchFilter = string.Empty;
    private Dictionary<uint, bool> ExpandedCategories => Settings.ExpandedCategories;
    private readonly HashSet<uint> _selectedItems = new();
    private readonly Dictionary<uint, (long price, DateTime fetchTime)> _priceCache = new();
    private readonly HashSet<uint> _fetchingPrices = new();
    private readonly Dictionary<uint, DateTime> _fetchStartTimes = new();
    private readonly TimeSpan _fetchTimeout = TimeSpan.FromSeconds(30);
    
    // Filter options
    private bool _showArmory = false;
    private bool _showOnlyHQ = false;
    
    // Safety filter options
    private bool _showSafetyFilters = true;
    private bool _showOnlyFlagged = false;
    
    private string _selectedWorld = "";
    private List<string> _availableWorlds = new();
    
    // Settings references
    private InventorySettings Settings => Plugin.Configuration.InventorySettings;
    
    // Discard state
    private bool _isDiscarding = false;
    private List<InventoryItemInfo> _itemsToDiscard = new();
    private int _discardProgress = 0;
    private string? _discardError = null;
    private DateTime _discardStartTime = DateTime.MinValue;
    private int _confirmRetryCount = 0;
    
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
        
        // Update task manager to process queued discard tasks
        // Note: TaskManager.Update() is automatically called by ECommons framework
        
        // Clean up stuck price fetches
        CleanupStuckFetches();
        
        // Only update prices when window is open and every few seconds to reduce CPU load
        if (_windowIsOpen && Settings.AutoRefreshPrices && !_isDiscarding && DateTime.Now - _lastRefresh > _refreshInterval)
        {
            _lastRefresh = DateTime.Now;
            
            // Only fetch prices for currently visible (not filtered out) items
            var visibleItems = GetVisibleItems();
            var stalePrices = visibleItems.Where(item => 
                item.CanBeTraded && // Only check prices for tradable items
                !_fetchingPrices.Contains(item.ItemId) &&
                (!_priceCache.TryGetValue(item.ItemId, out var cached) || 
                 DateTime.Now - cached.fetchTime > TimeSpan.FromMinutes(Settings.PriceCacheDurationMinutes)))
                .Take(5) // Reduced back to 5 since we're only fetching visible items now
                .ToList();
                
            // Only fetch if there are visible items that need pricing
            if (stalePrices.Count > 0)
            {
                foreach (var item in stalePrices)
                {
                    _ = FetchMarketPrice(item);
                }
            }
        }
        
        // Mark window as closed when not actively drawing
        _windowIsOpen = false;
        
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
        // Mark window as open for price fetching optimization
        _windowIsOpen = true;
        
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
        
        // Top row: Search and controls
        DrawTopControls();
        
        // Filter and settings sections  
        DrawFiltersAndSettings();
        
        ImGui.Separator();
        
        // Main content area with tabs
        ImGui.BeginChild("InventoryContent", new Vector2(0, 0), false);
        
        // Tab bar for Not Filtered vs Filtered Items with inline action buttons
        if (ImGui.BeginTabBar("InventoryTabs"))
        {
            // Calculate filtered items count for tab display
            var filteredItems = GetFilteredOutItems();
            
            // Not Filtered tab (items available for discard)
            var notFilteredTabText = $"Available Items ({_categories.Sum(c => c.Items.Count)})";
            if (ImGui.BeginTabItem(notFilteredTabText))
            {
                DrawAvailableItemsTab();
                ImGui.EndTabItem();
            }
            
            // Filtered Items tab (items being protected)
            var filteredTabText = $"Protected Items ({filteredItems.Count})";
            if (ImGui.BeginTabItem(filteredTabText))
            {
                DrawFilteredItemsTab(filteredItems);
                ImGui.EndTabItem();
            }
            
            // Action buttons on the right side of tab bar
            ImGui.SameLine();
            var availableWidth = ImGui.GetContentRegionAvail().X;
            var buttonWidth = 80f;
            var spacing = ImGui.GetStyle().ItemSpacing.X;
            var totalButtonWidth = buttonWidth * 2 + spacing;
            
            if (availableWidth > totalButtonWidth + 20)
            {
                ImGui.SetCursorPosX(ImGui.GetCursorPosX() + availableWidth - totalButtonWidth);
                
                if (ImGui.Button("Clear", new Vector2(buttonWidth, 0)))
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
                    if (ImGui.Button($"Discard ({_selectedItems.Count})", new Vector2(buttonWidth, 0)))
                    {
                        PrepareDiscard();
                    }
                    ImGui.PopStyleColor();
                }
                else
                {
                    ImGui.BeginDisabled();
                    ImGui.Button("Discard (0)", new Vector2(buttonWidth, 0));
                    ImGui.EndDisabled();
                }
            }
            
            ImGui.EndTabBar();
        }
        
        ImGui.EndChild();
    }
    
    private void DrawCategoryItems(CategoryGroup category)
    {
        ImGui.Indent();
        
        // Add category-specific selection controls
        DrawCategoryControls(category);
        
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
    
    private void DrawCategoryControls(CategoryGroup category)
    {
        var discardableItems = category.Items.Where(i => i.SafetyAssessment?.IsSafeToDiscard == true).ToList();
        var selectedInCategory = category.Items.Count(i => _selectedItems.Contains(i.ItemId));
        var allSelectedInCategory = discardableItems.Count > 0 && discardableItems.All(i => _selectedItems.Contains(i.ItemId));
        
        // Show selection info
        ImGui.TextColored(new Vector4(0.7f, 0.7f, 0.7f, 1), 
            $"Selected: {selectedInCategory}/{category.Items.Count} | Discardable: {discardableItems.Count}");
        
        if (discardableItems.Count > 0)
        {
            ImGui.SameLine();
            
            // Select/Deselect category button
            var buttonText = allSelectedInCategory ? $"Deselect All ({discardableItems.Count})" : $"Select All ({discardableItems.Count})";
            var buttonColor = allSelectedInCategory ? new Vector4(0.6f, 0.6f, 0.6f, 1) : new Vector4(0.2f, 0.7f, 0.2f, 1);
            
            ImGui.PushStyleColor(ImGuiCol.Button, buttonColor);
            if (ImGui.Button(buttonText))
            {
                if (allSelectedInCategory)
                {
                    // Deselect all items in this category
                    foreach (var item in discardableItems)
                    {
                        _selectedItems.Remove(item.ItemId);
                        item.IsSelected = false;
                    }
                }
                else
                {
                    // Select all discardable items in this category
                    foreach (var item in discardableItems)
                    {
                        _selectedItems.Add(item.ItemId);
                        item.IsSelected = true;
                    }
                }
            }
            ImGui.PopStyleColor();
            
            // Add warning for dangerous categories
            if (IsDangerousCategory(category))
            {
                ImGui.SameLine();
                ImGui.PushFont(UiBuilder.IconFont);
                ImGui.TextColored(new Vector4(0.8f, 0.5f, 0.2f, 1), FontAwesomeIcon.ExclamationTriangle.ToIconString());
                ImGui.PopFont();
                if (ImGui.IsItemHovered())
                {
                    ImGui.SetTooltip("This category may contain valuable items. Please review carefully!");
                }
            }
        }
        
        ImGui.Spacing();
    }
    
    private bool IsDangerousCategory(CategoryGroup category)
    {
        // Mark categories that might contain valuable items
        var dangerousCategories = new[]
        {
            "Weapons", "Tools", "Armor", "Accessories", "Materia", "Crystals",
            "Medicine & Meals", "Materials", "Other"
        };
        
        return dangerousCategories.Any(dangerous => 
            category.Name.Contains(dangerous, StringComparison.OrdinalIgnoreCase));
    }
    
    private void DrawDiscardConfirmation()
    {
        var windowSize = new Vector2(800, 600);
        ImGui.SetNextWindowSize(windowSize, ImGuiCond.Always);
        ImGui.SetNextWindowPos(ImGui.GetMainViewport().GetCenter(), ImGuiCond.Always, new Vector2(0.5f, 0.5f));
        
        ImGui.Begin("Confirm Discard", ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoMove);
        
        // Header with warning
        ImGui.PushFont(UiBuilder.IconFont);
        ImGui.TextColored(new Vector4(0.8f, 0.2f, 0.2f, 1), FontAwesomeIcon.ExclamationTriangle.ToIconString());
        ImGui.PopFont();
        ImGui.SameLine();
        ImGui.TextColored(new Vector4(0.8f, 0.2f, 0.2f, 1), "WARNING: This will permanently delete the following items!");
        
        ImGui.Separator();
        
        // Summary section
        DrawDiscardSummary();
        
        ImGui.Separator();
        
        // Items table
        ImGui.Text("Items to discard:");
        ImGui.BeginChild("ItemTable", new Vector2(0, 350), true);
        DrawDiscardItemsTable();
        ImGui.EndChild();
        
        // Error display
        if (!string.IsNullOrEmpty(_discardError))
        {
            ImGui.Separator();
            ImGui.TextColored(new Vector4(0.8f, 0.2f, 0.2f, 1), _discardError);
        }
        
        // Progress bar
        if (_discardProgress > 0)
        {
            ImGui.Separator();
            var progress = (float)_discardProgress / _itemsToDiscard.Count;
            ImGui.ProgressBar(progress, new Vector2(-1, 0), $"Discarding... {_discardProgress}/{_itemsToDiscard.Count}");
        }
        
        ImGui.Separator();
        
        // Buttons
        DrawDiscardButtons();
        
        ImGui.End();
    }
    
    private void DrawDiscardSummary()
    {
        var totalItems = _itemsToDiscard.Count;
        var totalQuantity = _itemsToDiscard.Sum(i => i.Quantity);
        var totalValue = _itemsToDiscard.Where(i => i.MarketPrice.HasValue).Sum(i => i.MarketPrice!.Value * i.Quantity);
        var totalValueFormatted = totalValue > 0 ? $"{totalValue:N0} gil" : "Unknown";
        
        // Create a nice summary box
        if (ImGui.BeginTable("SummaryTable", 2, ImGuiTableFlags.Borders))
        {
            ImGui.TableSetupColumn("Label", ImGuiTableColumnFlags.WidthFixed, 150);
            ImGui.TableSetupColumn("Value", ImGuiTableColumnFlags.WidthStretch);
            
            ImGui.TableNextRow();
            ImGui.TableSetColumnIndex(0);
            ImGui.Text("Total Items:");
            ImGui.TableSetColumnIndex(1);
            ImGui.Text($"{totalItems} unique items");
            
            ImGui.TableNextRow();
            ImGui.TableSetColumnIndex(0);
            ImGui.Text("Total Quantity:");
            ImGui.TableSetColumnIndex(1);
            ImGui.Text($"{totalQuantity} items");
            
            ImGui.TableNextRow();
            ImGui.TableSetColumnIndex(0);
            ImGui.Text("Market Value:");
            ImGui.TableSetColumnIndex(1);
            if (totalValue > 0)
            {
                ImGui.TextColored(new Vector4(1f, 0.8f, 0.2f, 1), totalValueFormatted);
            }
            else
            {
                ImGui.TextColored(new Vector4(0.6f, 0.6f, 0.6f, 1), totalValueFormatted);
            }
            
            ImGui.EndTable();
        }
    }
    
    private void DrawDiscardItemsTable()
    {
        if (ImGui.BeginTable("DiscardItemsTable", Settings.ShowMarketPrices ? 6 : 5, 
            ImGuiTableFlags.Borders | ImGuiTableFlags.RowBg | ImGuiTableFlags.ScrollY))
        {
            // Setup columns
            ImGui.TableSetupColumn("Icon", ImGuiTableColumnFlags.WidthFixed, 40);
            ImGui.TableSetupColumn("Item", ImGuiTableColumnFlags.WidthStretch);
            ImGui.TableSetupColumn("Qty", ImGuiTableColumnFlags.WidthFixed, 60);
            ImGui.TableSetupColumn("Location", ImGuiTableColumnFlags.WidthFixed, 120);
            if (Settings.ShowMarketPrices)
            {
                ImGui.TableSetupColumn("Unit Price", ImGuiTableColumnFlags.WidthFixed, 100);
                ImGui.TableSetupColumn("Total Value", ImGuiTableColumnFlags.WidthFixed, 100);
            }
            else
            {
                ImGui.TableSetupColumn("HQ", ImGuiTableColumnFlags.WidthFixed, 40);
            }
            
            ImGui.TableSetupScrollFreeze(0, 1);
            ImGui.TableHeadersRow();
            
            foreach (var item in _itemsToDiscard)
            {
                DrawDiscardItemRow(item);
            }
            
            ImGui.EndTable();
        }
    }
    
    private void DrawDiscardItemRow(InventoryItemInfo item)
    {
        ImGui.TableNextRow();
        
        // Icon column
        ImGui.TableSetColumnIndex(0);
        var icon = _iconCache.GetIcon(item.IconId);
        if (icon != null)
        {
            ImGui.Image(icon.ImGuiHandle, new Vector2(32, 32));
        }
        
        // Item name column
        ImGui.TableSetColumnIndex(1);
        var nameColor = item.IsHQ ? new Vector4(0.8f, 0.8f, 1f, 1f) : new Vector4(1f, 1f, 1f, 1f);
        ImGui.TextColored(nameColor, item.Name);
        if (item.IsHQ)
        {
            ImGui.SameLine();
            ImGui.TextColored(new Vector4(0.6f, 0.8f, 1f, 1f), " [HQ]");
        }
        
        // Quantity column
        ImGui.TableSetColumnIndex(2);
        ImGui.Text($"{item.Quantity}");
        
        // Location column
        ImGui.TableSetColumnIndex(3);
        ImGui.Text(GetContainerDisplayName(item.Container));
        
        if (Settings.ShowMarketPrices)
        {
            // Unit price column
            ImGui.TableSetColumnIndex(4);
            if (item.MarketPrice.HasValue && item.MarketPrice.Value > 0)
            {
                ImGui.Text($"{item.MarketPrice.Value:N0}");
            }
            else if (item.MarketPrice.HasValue && item.MarketPrice.Value == -1)
            {
                ImGui.TextColored(new Vector4(0.6f, 0.6f, 0.6f, 1), "N/A");
            }
            else
            {
                ImGui.TextColored(new Vector4(0.8f, 0.8f, 0.2f, 1), "Loading...");
            }
            
            // Total value column
            ImGui.TableSetColumnIndex(5);
            if (item.MarketPrice.HasValue && item.MarketPrice.Value > 0)
            {
                var totalValue = item.MarketPrice.Value * item.Quantity;
                ImGui.TextColored(new Vector4(1f, 0.8f, 0.2f, 1), $"{totalValue:N0}");
            }
            else if (item.MarketPrice.HasValue && item.MarketPrice.Value == -1)
            {
                ImGui.TextColored(new Vector4(0.6f, 0.6f, 0.6f, 1), "N/A");
            }
            else
            {
                ImGui.TextColored(new Vector4(0.8f, 0.8f, 0.2f, 1), "...");
            }
        }
        else
        {
            // HQ indicator column
            ImGui.TableSetColumnIndex(4);
            if (item.IsHQ)
            {
                ImGui.TextColored(new Vector4(0.6f, 0.8f, 1f, 1f), "HQ");
            }
        }
    }
    
    private void DrawDiscardButtons()
    {
        var buttonWidth = 150f;
        var spacing = ImGui.GetStyle().ItemSpacing.X;
        var totalWidth = buttonWidth * 2 + spacing;
        var availableWidth = ImGui.GetContentRegionAvail().X;
        var centerPos = (availableWidth - totalWidth) * 0.5f;
        
        if (centerPos > 0)
            ImGui.SetCursorPosX(ImGui.GetCursorPosX() + centerPos);
        
        // Start/Cancel button
        if (_discardProgress == 0)
        {
            if (ImGui.Button("Start Discarding", new Vector2(buttonWidth, 35)))
            {
                StartDiscarding();
            }
            
            ImGui.SameLine();
            
            if (ImGui.Button("Cancel", new Vector2(buttonWidth, 35)))
            {
                CancelDiscard();
            }
        }
        else
        {
            ImGui.BeginDisabled();
            ImGui.Button("Discarding...", new Vector2(buttonWidth, 35));
            ImGui.EndDisabled();
            
            ImGui.SameLine();
            
            if (ImGui.Button("Cancel", new Vector2(buttonWidth, 35)))
            {
                CancelDiscard();
            }
        }
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
        _originalItems = _inventoryHelpers.GetAllItems(_showArmory, false); // Always false for saddlebag
        _allItems = _originalItems; // For compatibility, keep _allItems pointing to the same list
        
        // Assess safety for all items and update market prices from cache
        foreach (var item in _allItems)
        {
            item.SafetyAssessment = InventoryHelpers.AssessItemSafety(item, Settings);
            
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
        
        // Apply safety filters directly - each enabled filter hides those items
        var filters = Settings.SafetyFilters;
        if (filters.FilterUltimateTokens)
            filteredItems = filteredItems.Where(i => !InventoryHelpers.HardcodedBlacklist.Contains(i.ItemId));
        if (filters.FilterCurrencyItems)
            filteredItems = filteredItems.Where(i => !InventoryHelpers.CurrencyRange.Contains(i.ItemId));
        if (filters.FilterCrystalsAndShards)
            filteredItems = filteredItems.Where(i => !(i.ItemUICategory == 63 || i.ItemUICategory == 64));
        if (filters.FilterGearsetItems)
            filteredItems = filteredItems.Where(i => !InventoryHelpers.IsInGearset(i.ItemId));
        if (filters.FilterIndisposableItems)
            filteredItems = filteredItems.Where(i => !i.IsIndisposable);
        if (filters.FilterHighLevelGear)
            filteredItems = filteredItems.Where(i => !(i.EquipSlotCategory > 0 && i.ItemLevel >= filters.MaxGearItemLevel));
        if (filters.FilterUniqueUntradeable)
            filteredItems = filteredItems.Where(i => !(i.IsUnique && i.IsUntradable && !InventoryHelpers.SafeUniqueItems.Contains(i.ItemId)));
        if (filters.FilterHQItems)
            filteredItems = filteredItems.Where(i => !i.IsHQ);
        if (filters.FilterCollectables)
            filteredItems = filteredItems.Where(i => !i.IsCollectable);
        if (filters.FilterSpiritbondedItems)
            filteredItems = filteredItems.Where(i => i.SpiritBond < filters.MinSpiritbondToFilter);
        
        if (_showOnlyFlagged)
        {
            filteredItems = filteredItems.Where(i => i.SafetyAssessment?.SafetyFlags.Any() == true);
        }
            
        _categories = filteredItems
            .GroupBy(i => new { i.ItemUICategory, i.CategoryName })
            .Select(categoryGroup => 
            {
                var items = categoryGroup
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
                            // MarketPriceLoading is tracked via _fetchingPrices set
                            MarketPriceFetchTime = first.MarketPriceFetchTime,
                            IsSelected = first.IsSelected
                        };
                    })
                    .OrderBy(i => i.Name)
                    .ToList();
                    
                return new CategoryGroup
                {
                    CategoryId = categoryGroup.Key.ItemUICategory,
                    Name = categoryGroup.Key.CategoryName,
                    Items = items
                };
            })
            .OrderBy(c => c.Name)
            .ToList();
    }
    
    private List<InventoryItemInfo> GetVisibleItems()
    {
        // Return items that are currently visible based on applied filters
        // This is the same logic as UpdateCategories but just returns the filtered items
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
        
        // Apply safety filters directly - each enabled filter hides those items
        var filters = Settings.SafetyFilters;
        if (filters.FilterUltimateTokens)
            filteredItems = filteredItems.Where(i => !InventoryHelpers.HardcodedBlacklist.Contains(i.ItemId));
        if (filters.FilterCurrencyItems)
            filteredItems = filteredItems.Where(i => !InventoryHelpers.CurrencyRange.Contains(i.ItemId));
        if (filters.FilterCrystalsAndShards)
            filteredItems = filteredItems.Where(i => !(i.ItemUICategory == 63 || i.ItemUICategory == 64));
        if (filters.FilterGearsetItems)
            filteredItems = filteredItems.Where(i => !InventoryHelpers.IsInGearset(i.ItemId));
        if (filters.FilterIndisposableItems)
            filteredItems = filteredItems.Where(i => !i.IsIndisposable);
        if (filters.FilterHighLevelGear)
            filteredItems = filteredItems.Where(i => !(i.EquipSlotCategory > 0 && i.ItemLevel >= filters.MaxGearItemLevel));
        if (filters.FilterUniqueUntradeable)
            filteredItems = filteredItems.Where(i => !(i.IsUnique && i.IsUntradable && !InventoryHelpers.SafeUniqueItems.Contains(i.ItemId)));
        if (filters.FilterHQItems)
            filteredItems = filteredItems.Where(i => !i.IsHQ);
        if (filters.FilterCollectables)
            filteredItems = filteredItems.Where(i => !i.IsCollectable);
        if (filters.FilterSpiritbondedItems)
            filteredItems = filteredItems.Where(i => i.SpiritBond < filters.MinSpiritbondToFilter);
        
        if (_showOnlyFlagged)
        {
            filteredItems = filteredItems.Where(i => i.SafetyAssessment?.SafetyFlags.Any() == true);
        }
        
        return filteredItems.ToList();
    }
    
    private void CleanupStuckFetches()
    {
        var stuckItems = _fetchStartTimes.Where(kvp => 
            DateTime.Now - kvp.Value > _fetchTimeout).Select(kvp => kvp.Key).ToList();
        
        foreach (var stuckItem in stuckItems)
        {
            _fetchingPrices.Remove(stuckItem);
            _fetchStartTimes.Remove(stuckItem);
            
            // Find the stuck item and mark it as failed (N/A)
            var stuckItemInfo = _allItems.FirstOrDefault(i => i.ItemId == stuckItem);
            if (stuckItemInfo != null)
            {
                // MarketPriceLoading is tracked via _fetchingPrices set - no need to set
                stuckItemInfo.MarketPrice = -1; // Use -1 to indicate "N/A" state
                _priceCache[stuckItem] = (-1, DateTime.Now); // Cache the N/A result
            }
            
            Plugin.Log.Warning($"Cleaned up stuck price fetch for item {stuckItem}");
        }
    }

    private async Task FetchMarketPrice(InventoryItemInfo item)
    {
        if (_fetchingPrices.Contains(item.ItemId)) return;
        if (!item.CanBeTraded) return; // Skip untradable items
        
        _fetchingPrices.Add(item.ItemId);
        _fetchStartTimes[item.ItemId] = DateTime.Now;
                    // MarketPriceLoading is tracked via _fetchingPrices set
        
        try
        {
            var result = await _universalisClient.GetMarketPrice(item.ItemId, item.IsHQ);
            
            if (result != null)
            {
                item.MarketPrice = result.Price;
                item.MarketPriceFetchTime = DateTime.Now;
                _priceCache[item.ItemId] = (result.Price, DateTime.Now);
            }
            else
            {
                // No price data available - mark as N/A
                item.MarketPrice = -1;
                _priceCache[item.ItemId] = (-1, DateTime.Now);
            }
        }
        catch (Exception ex)
        {
            Plugin.Log.Error(ex, $"Failed to fetch price for {item.Name}");
            // Mark as N/A on error
            item.MarketPrice = -1;
            _priceCache[item.ItemId] = (-1, DateTime.Now);
        }
        finally
        {
            _fetchingPrices.Remove(item.ItemId);
            _fetchStartTimes.Remove(item.ItemId);
                            // MarketPriceLoading is tracked via _fetchingPrices set
        }
    }
    
    private void PrepareDiscard()
    {
        Plugin.Log.Information($"PrepareDiscard called. Selected items count: {_selectedItems.Count}");
        
        // Get the actual individual items from inventory, not the grouped/combined ones
        var actualItemsToDiscard = new List<InventoryItemInfo>();
        
        foreach (var selectedItemId in _selectedItems)
        {
            // Find all actual inventory instances of this item ID from the original items
            var actualItems = _originalItems.Where(i => 
                i.ItemId == selectedItemId && 
                InventoryHelpers.IsSafeToDiscard(i, Settings.BlacklistedItems)).ToList();
                
            Plugin.Log.Information($"Found {actualItems.Count} instances of item {selectedItemId} to discard");
            actualItemsToDiscard.AddRange(actualItems);
        }
        
        _itemsToDiscard = actualItemsToDiscard;
        Plugin.Log.Information($"Items to discard after filtering: {_itemsToDiscard.Count}");
        
        if (_itemsToDiscard.Count > 0)
        {
            _isDiscarding = true;
            _discardProgress = 0;
            _discardError = null;
            Plugin.Log.Information("Discard preparation successful, showing confirmation dialog");
        }
        else
        {
            Plugin.Log.Warning("No items to discard after filtering");
            Plugin.ChatGui.PrintError("No items selected for discard or all selected items cannot be safely discarded.");
        }
    }
    
    private void StartDiscarding()
    {
        Plugin.Log.Information($"StartDiscarding called. Items to discard: {_itemsToDiscard.Count}");
        _taskManager.Abort();
        _taskManager.Enqueue(() => DiscardNextItem());
        Plugin.Log.Information("Discard task enqueued");
    }
    
    private unsafe void DiscardNextItem()
    {
        Plugin.Log.Information($"DiscardNextItem called. Progress: {_discardProgress}/{_itemsToDiscard.Count}");
        
        if (_discardProgress >= _itemsToDiscard.Count)
        {
            Plugin.ChatGui.Print("Finished discarding items.");
            CancelDiscard();
            return;
        }
        
        var item = _itemsToDiscard[_discardProgress];
        Plugin.Log.Information($"Attempting to discard item: {item.Name} (ID: {item.ItemId})");
        
        try
        {
            _inventoryHelpers.DiscardItem(item);
            _discardProgress++;
            Plugin.Log.Information($"Discard call completed for {item.Name}");
            
            // Reset confirmation state for this item
            _confirmRetryCount = 0;
            _discardStartTime = DateTime.Now;
            
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
        Plugin.Log.Information($"ConfirmDiscard called, looking for dialog (retry {_confirmRetryCount})");
        
        // Check for timeout
        if (DateTime.Now - _discardStartTime > TimeSpan.FromSeconds(15))
        {
            Plugin.Log.Warning("Discard confirmation timed out");
            _discardError = "Discard confirmation timed out";
            CancelDiscard();
            return;
        }
        
        var addon = GetDiscardAddon();
        if (addon != null)
        {
            Plugin.Log.Information("Found discard dialog, clicking Yes");
            
            // Get the Yes button (should be YesButton like in ARDiscard)
            var selectYesno = (FFXIVClientStructs.FFXIV.Client.UI.AddonSelectYesno*)addon;
            if (selectYesno->YesButton != null)
            {
                Plugin.Log.Information("Yes button found, enabling and clicking");
                selectYesno->YesButton->AtkComponentBase.SetEnabledState(true);
                addon->FireCallbackInt(0);
                
                Plugin.Log.Information("Yes button clicked, waiting for response");
                _confirmRetryCount = 0; // Reset retry count
                _taskManager.EnqueueDelay(500);
                _taskManager.Enqueue(() => WaitForDiscardComplete());
            }
            else
            {
                Plugin.Log.Warning("Yes button not found in dialog");
                _confirmRetryCount++;
                if (_confirmRetryCount > 10)
                {
                    Plugin.Log.Error("Too many retries trying to find Yes button");
                    _discardError = "Could not find Yes button in dialog";
                    CancelDiscard();
                    return;
                }
                _taskManager.EnqueueDelay(100);
                _taskManager.Enqueue(() => ConfirmDiscard());
            }
        }
        else
        {
            _confirmRetryCount++;
            if (_confirmRetryCount > 50) // 5 seconds total
            {
                Plugin.Log.Warning("No discard dialog found after many retries, assuming no confirmation needed");
                _confirmRetryCount = 0;
                _taskManager.EnqueueDelay(200);
                _taskManager.Enqueue(() => DiscardNextItem());
            }
            else
            {
                Plugin.Log.Information("No discard dialog found yet, retrying");
                _taskManager.EnqueueDelay(100);
                _taskManager.Enqueue(() => ConfirmDiscard());
            }
        }
    }
    
    private unsafe void WaitForDiscardComplete()
    {
        Plugin.Log.Information("WaitForDiscardComplete called");
        
        // Check if dialog is still visible
        var addon = GetDiscardAddon();
        if (addon != null)
        {
            Plugin.Log.Information("Dialog still visible, waiting longer");
            _taskManager.EnqueueDelay(100);
            _taskManager.Enqueue(() => WaitForDiscardComplete());
        }
        else
        {
            Plugin.Log.Information("Dialog dismissed, continuing to next item");
            _taskManager.EnqueueDelay(200);
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
                if (addon == null) return null;
                
                if (addon->IsVisible && addon->UldManager.LoadedState == FFXIVClientStructs.FFXIV.Component.GUI.AtkLoadState.Loaded)
                {
                    // Check if it's a discard dialog
                    var textNode = addon->UldManager.NodeList[15]->GetAsAtkTextNode();
                    if (textNode != null)
                    {
                        var text = Dalamud.Memory.MemoryHelper.ReadSeString(&textNode->NodeText).TextValue;
                        Plugin.Log.Information($"YesNo dialog text: {text}");
                        
                        if (text.Contains("Discard", StringComparison.OrdinalIgnoreCase) || 
                            text.Contains("discard", StringComparison.OrdinalIgnoreCase))
                        {
                            Plugin.Log.Information("Found discard confirmation dialog");
                            return addon;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Plugin.Log.Warning(ex, $"Error checking addon {i}");
                return null;
            }
        }
        
        Plugin.Log.Information("No discard dialog found");
        return null;
    }
    
    private void CancelDiscard()
    {
        _taskManager.Abort();
        _isDiscarding = false;
        _itemsToDiscard.Clear();
        _discardProgress = 0;
        _discardError = null;
        _confirmRetryCount = 0;
        _discardStartTime = DateTime.MinValue;
        
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
        
        // Clean up any pending fetches
        _fetchingPrices.Clear();
        _fetchStartTimes.Clear();
        _windowIsOpen = false;
        
        _iconCache?.Dispose();
        _taskManager?.Dispose();
        _universalisClient?.Dispose();
        base.Dispose();
    }
    
    private void DrawTopControls()
    {
        var availableWidth = ImGui.GetContentRegionAvail().X;
        var spacing = ImGui.GetStyle().ItemSpacing.X;
        
        // Compact search bar
        ImGui.SetNextItemWidth(200f);
        if (ImGui.InputTextWithHint("##Search", "Search...", ref _searchFilter, 100))
        {
            _lastCategoryUpdate = DateTime.Now;
        }
        
        // Only update categories after a delay
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
        
        // Core filters on same line
        ImGui.SameLine();
        ImGui.Text("|");
        
        ImGui.SameLine();
        if (ImGui.Checkbox("Armory", ref _showArmory)) 
        {
            RefreshInventory();
        }
        
        ImGui.SameLine();
        if (ImGui.Checkbox("HQ Only", ref _showOnlyHQ)) UpdateCategories();
        
        ImGui.SameLine();
        if (ImGui.Checkbox("Flagged", ref _showOnlyFlagged)) UpdateCategories();
        
        // Just show item counts inline with the controls
        ImGui.SameLine();
        ImGui.TextColored(new Vector4(0.7f, 0.7f, 0.7f, 1), $"({_allItems.Count} items, {_selectedItems.Count} selected)");
    }
    
    private void DrawFiltersAndSettings()
    {
        ImGui.Separator();
        
        // Safety Filters in a compact grid
        ImGui.Text("Safety Filters:");
        ImGui.SameLine();
        DrawCompactSafetyFilters();
        
        // Market price settings on one line if enabled
        if (Settings.ShowMarketPrices)
        {
            DrawCompactMarketSettings();
        }
        else
        {
            var showPrices = Settings.ShowMarketPrices;
            if (ImGui.Checkbox("Show Market Prices", ref showPrices))
            {
                Settings.ShowMarketPrices = showPrices;
                Plugin.Configuration.Save();
            }
        }
    }
    
    private void DrawCompactSafetyFilters()
    {
        var filters = Settings.SafetyFilters;
        bool changed = false;
        
        // Show active filter count
        var activeCount = 0;
        if (filters.FilterUltimateTokens) activeCount++;
        if (filters.FilterCurrencyItems) activeCount++;
        if (filters.FilterCrystalsAndShards) activeCount++;
        if (filters.FilterGearsetItems) activeCount++;
        if (filters.FilterIndisposableItems) activeCount++;
        if (filters.FilterHighLevelGear) activeCount++;
        if (filters.FilterUniqueUntradeable) activeCount++;
        if (filters.FilterHQItems) activeCount++;
        if (filters.FilterCollectables) activeCount++;
        
        ImGui.TextColored(new Vector4(0.7f, 0.7f, 0.7f, 1), $"({activeCount}/9 active)");
        ImGui.SameLine();
        
        // Draw filters in a compact 3-column layout
        ImGui.BeginGroup();
        
        // Column 1
        var filterUltimate = filters.FilterUltimateTokens;
        if (ImGui.Checkbox("##UltimateTokens", ref filterUltimate))
        {
            filters.FilterUltimateTokens = filterUltimate;
            changed = true;
        }
        if (ImGui.IsItemHovered()) ImGui.SetTooltip("Ultimate Tokens: Raid tokens, preorder items");
        ImGui.SameLine();
        ImGui.Text("Ultimate");
        
        ImGui.SameLine(150);
        var filterCrystals = filters.FilterCrystalsAndShards;
        if (ImGui.Checkbox("##Crystals", ref filterCrystals))
        {
            filters.FilterCrystalsAndShards = filterCrystals;
            changed = true;
        }
        if (ImGui.IsItemHovered()) ImGui.SetTooltip("Crystals & Shards: Crafting materials");
        ImGui.SameLine();
        ImGui.Text("Crystals");
        
        ImGui.SameLine(300);
        var filterGearset = filters.FilterGearsetItems;
        if (ImGui.Checkbox("##Gearset", ref filterGearset))
        {
            filters.FilterGearsetItems = filterGearset;
            changed = true;
        }
        if (ImGui.IsItemHovered()) ImGui.SetTooltip("Gearset Items: Equipment in any gearset");
        ImGui.SameLine();
        ImGui.Text("Gearsets");
        
        // Column 2
        var filterCurrency = filters.FilterCurrencyItems;
        if (ImGui.Checkbox("##Currency", ref filterCurrency))
        {
            filters.FilterCurrencyItems = filterCurrency;
            changed = true;
        }
        if (ImGui.IsItemHovered()) ImGui.SetTooltip("Currency: Gil, tomestones, MGP, etc.");
        ImGui.SameLine();
        ImGui.Text("Currency");
        
        ImGui.SameLine(150);
        var filterHighLevel = filters.FilterHighLevelGear;
        if (ImGui.Checkbox("##HighLevel", ref filterHighLevel))
        {
            filters.FilterHighLevelGear = filterHighLevel;
            changed = true;
        }
        if (ImGui.IsItemHovered()) ImGui.SetTooltip($"High Level Gear: i{filters.MaxGearItemLevel}+");
        ImGui.SameLine();
        ImGui.Text("High Lvl");
        
        ImGui.SameLine(300);
        var filterUnique = filters.FilterUniqueUntradeable;
        if (ImGui.Checkbox("##Unique", ref filterUnique))
        {
            filters.FilterUniqueUntradeable = filterUnique;
            changed = true;
        }
        if (ImGui.IsItemHovered()) ImGui.SetTooltip("Unique & Untradeable: Cannot be reacquired");
        ImGui.SameLine();
        ImGui.Text("Unique");
        
        // Column 3
        var filterHQ = filters.FilterHQItems;
        if (ImGui.Checkbox("##HQ", ref filterHQ))
        {
            filters.FilterHQItems = filterHQ;
            changed = true;
        }
        if (ImGui.IsItemHovered()) ImGui.SetTooltip("High Quality: HQ items");
        ImGui.SameLine();
        ImGui.Text("HQ Items");
        
        ImGui.SameLine(150);
        var filterCollectable = filters.FilterCollectables;
        if (ImGui.Checkbox("##Collectables", ref filterCollectable))
        {
            filters.FilterCollectables = filterCollectable;
            changed = true;
        }
        if (ImGui.IsItemHovered()) ImGui.SetTooltip("Collectables: Turn-in items");
        ImGui.SameLine();
        ImGui.Text("Collectables");
        
        ImGui.SameLine(300);
        var filterIndisposable = filters.FilterIndisposableItems;
        if (ImGui.Checkbox("##Indisposable", ref filterIndisposable))
        {
            filters.FilterIndisposableItems = filterIndisposable;
            changed = true;
        }
        if (ImGui.IsItemHovered()) ImGui.SetTooltip("Indisposable: Cannot be discarded");
        ImGui.SameLine();
        ImGui.Text("Protected");
        
        ImGui.EndGroup();
        
        if (changed)
        {
            Plugin.Configuration.Save();
            RefreshInventory();
        }
    }
    
    private void DrawCompactMarketSettings()
    {
        ImGui.Text("Market:");
        ImGui.SameLine();
        
        var showPrices = Settings.ShowMarketPrices;
        if (ImGui.Checkbox("Prices", ref showPrices))
        {
            Settings.ShowMarketPrices = showPrices;
            Plugin.Configuration.Save();
        }
        
        ImGui.SameLine();
        ImGui.Text("World:");
        ImGui.SameLine();
        ImGui.SetNextItemWidth(100);
        if (ImGui.BeginCombo("##World", _selectedWorld))
        {
            foreach (var world in _availableWorlds)
            {
                bool isSelected = world == _selectedWorld;
                if (ImGui.Selectable(world, isSelected))
                {
                    _selectedWorld = world;
                    _universalisClient.Dispose();
                    _universalisClient = new UniversalisClient(Plugin.Log, _selectedWorld);
                    _priceCache.Clear();
                    foreach (var item in _allItems)
                    {
                        item.MarketPrice = null;
                        item.MarketPriceFetchTime = null;
                    }
                }
            }
            ImGui.EndCombo();
        }
        
        ImGui.SameLine();
        ImGui.Text("Cache:");
        ImGui.SameLine();
        ImGui.SetNextItemWidth(50);
        var cacheMinutes = Settings.PriceCacheDurationMinutes;
        if (ImGui.InputInt("##Cache", ref cacheMinutes, 0))
        {
            Settings.PriceCacheDurationMinutes = Math.Max(1, cacheMinutes);
            Plugin.Configuration.Save();
        }
        ImGui.SameLine();
        ImGui.Text("min");
        
        ImGui.SameLine();
        var autoRefresh = Settings.AutoRefreshPrices;
        if (ImGui.Checkbox("Auto-refresh", ref autoRefresh))
        {
            Settings.AutoRefreshPrices = autoRefresh;
            Plugin.Configuration.Save();
        }
    }
    
    private (Vector4 color, string text, string tooltip) GetSafetyStatusSummary()
    {
        var filters = Settings.SafetyFilters;
        var activeFilters = 0;
        var totalFilters = 9; // Total number of safety filters
        
        if (filters.FilterUltimateTokens) activeFilters++;
        if (filters.FilterCurrencyItems) activeFilters++;
        if (filters.FilterCrystalsAndShards) activeFilters++;
        if (filters.FilterGearsetItems) activeFilters++;
        if (filters.FilterIndisposableItems) activeFilters++;
        if (filters.FilterHighLevelGear) activeFilters++;
        if (filters.FilterUniqueUntradeable) activeFilters++;
        if (filters.FilterHQItems) activeFilters++;
        if (filters.FilterCollectables) activeFilters++;
        
        if (activeFilters >= 7)
            return (new Vector4(0.2f, 0.8f, 0.2f, 1), "🛡️ Maximum Safety", $"All core protections active ({activeFilters}/{totalFilters})");
        else if (activeFilters >= 5)
            return (new Vector4(0.8f, 0.8f, 0.2f, 1), "⚠️ High Safety", $"Most protections active ({activeFilters}/{totalFilters})");
        else if (activeFilters >= 3)
            return (new Vector4(0.9f, 0.5f, 0.1f, 1), "⚡ Medium Safety", $"Some protections active ({activeFilters}/{totalFilters})");
        else
            return (new Vector4(0.8f, 0.2f, 0.2f, 1), "🔥 Low Safety", $"Few protections active ({activeFilters}/{totalFilters}) - BE CAREFUL!");
    }
    
    // Removed old DrawMarketPriceSettings - using compact version now
    
    // Removed old table-based DrawSafetyFiltersCompact - using new compact grid version
    
    // Removed old filter section methods - using compact grid now
    

    
    // Removed old filter methods - now using compact grid layout
    
    private void DrawItemSafetyFlags(InventoryItemInfo item)
    {
        // Only show additional safety flags (non-filter related info)
        if (item.SafetyAssessment?.SafetyFlags.Any() != true)
            return;
        
        // Filter out flags that are already shown as filter tags
        var additionalFlags = item.SafetyAssessment.SafetyFlags
            .Where(flag => !IsFilterRelatedFlag(flag))
            .ToList();
        
        if (!additionalFlags.Any())
            return;
        
        ImGui.SameLine();
        
        var flagColor = item.SafetyAssessment.FlagColor switch
        {
            SafetyFlagColor.Critical => new Vector4(0.8f, 0.2f, 0.2f, 1), // Red
            SafetyFlagColor.Warning => new Vector4(0.9f, 0.5f, 0.1f, 1),  // Orange
            SafetyFlagColor.Caution => new Vector4(0.9f, 0.9f, 0.2f, 1),  // Yellow
            SafetyFlagColor.Info => new Vector4(0.3f, 0.7f, 1.0f, 1),     // Blue
            _ => ImGui.GetStyle().Colors[(int)ImGuiCol.Text]
        };
        
        ImGui.PushStyleColor(ImGuiCol.Text, flagColor);
        
        var flagText = string.Join(", ", additionalFlags);
        ImGui.Text($"[{flagText}]");
        
        if (ImGui.IsItemHovered())
        {
            ImGui.BeginTooltip();
            ImGui.Text("Additional Safety Info:");
            foreach (var flag in additionalFlags)
            {
                ImGui.BulletText(flag);
            }
            ImGui.EndTooltip();
        }
        
        ImGui.PopStyleColor();
    }
    
    private bool IsFilterRelatedFlag(string flag)
    {
        // These flags are already shown as filter tags, don't duplicate them
        return flag.Contains("Ultimate") ||
               flag.Contains("Currency") ||
               flag.Contains("Crystal") ||
               flag.Contains("Gearset") ||
               flag.Contains("Indisposable") ||
               flag.Contains("High Level") ||
               flag.Contains("Unique") ||
               flag.Contains("High Quality") ||
               flag.Contains("Collectable") ||
               flag.Contains("Spiritbond");
    }
    
    private List<(string filterName, bool isActive)> GetAppliedFilters(InventoryItemInfo item)
    {
        var appliedFilters = new List<(string, bool)>();
        var filters = Settings.SafetyFilters;
        
        // Check each filter to see if it applies to this item
        if (InventoryHelpers.HardcodedBlacklist.Contains(item.ItemId))
            appliedFilters.Add(("Ultimate Tokens", filters.FilterUltimateTokens));
        
        if (InventoryHelpers.CurrencyRange.Contains(item.ItemId))
            appliedFilters.Add(("Currency", filters.FilterCurrencyItems));
        
        if (item.ItemUICategory == 63 || item.ItemUICategory == 64) // Crystals/Shards
            appliedFilters.Add(("Crystals", filters.FilterCrystalsAndShards));
        
        if (InventoryHelpers.IsInGearset(item.ItemId))
            appliedFilters.Add(("Gearset", filters.FilterGearsetItems));
        
        if (item.IsIndisposable)
            appliedFilters.Add(("Indisposable", filters.FilterIndisposableItems));
        
        if (item.EquipSlotCategory > 0 && item.ItemLevel >= filters.MaxGearItemLevel)
            appliedFilters.Add(("High-Level", filters.FilterHighLevelGear));
        
        if (item.IsUnique && item.IsUntradable && !InventoryHelpers.SafeUniqueItems.Contains(item.ItemId))
            appliedFilters.Add(("Unique", filters.FilterUniqueUntradeable));
        
        if (item.IsHQ)
            appliedFilters.Add(("HQ", filters.FilterHQItems));
        
        if (item.IsCollectable)
            appliedFilters.Add(("Collectable", filters.FilterCollectables));
        
        if (item.SpiritBond >= filters.MinSpiritbondToFilter)
            appliedFilters.Add(("Spiritbond", filters.FilterSpiritbondedItems));
        
        return appliedFilters;
    }
    
    private void DrawItemFilterTags(InventoryItemInfo item)
    {
        var appliedFilters = GetAppliedFilters(item);
        
        if (!appliedFilters.Any())
            return;
        
        // Draw compact filter tags inline
        foreach (var (filterName, isActive) in appliedFilters)
        {
            ImGui.SameLine();
            
            // Color based on whether filter is active (hiding the item)
            var tagColor = isActive ? 
                new Vector4(0.8f, 0.2f, 0.2f, 1) :  // Red if currently hiding
                new Vector4(0.6f, 0.6f, 0.6f, 1);   // Gray if disabled
            
            ImGui.PushStyleColor(ImGuiCol.Text, tagColor);
            
            // Use shorter names for compact display
            var shortName = GetShortFilterName(filterName);
            ImGui.Text($"[{shortName}]");
            
            ImGui.PopStyleColor();
            
            if (ImGui.IsItemHovered())
            {
                ImGui.SetTooltip(isActive ? 
                    $"HIDDEN by '{filterName}' filter" :
                    $"Would be hidden by '{filterName}' filter (disabled)");
            }
        }
    }
    
    private string GetShortFilterName(string filterName)
    {
        return filterName switch
        {
            "Ultimate Tokens" => "Ultimate",
            "Currency" => "Currency",
            "Crystals" => "Crystals", 
            "Gearset" => "Gearset",
            "Indisposable" => "Protected",
            "High-Level" => "HiLvl",
            "Unique" => "Unique",
            "HQ" => "HQ",
            "Collectable" => "Collect",
            "Spiritbond" => "SB",
            _ => filterName
        };
    }
    
    private void DrawAvailableItemsTab()
    {
        // Draw the regular categories for items available for discard
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
    }
    
    private void DrawFilteredItemsTab(List<InventoryItemInfo> filteredItems)
    {
        if (!filteredItems.Any())
        {
            ImGui.TextColored(new Vector4(0.6f, 0.8f, 0.6f, 1), "No items are currently being filtered out.");
            ImGui.Text("All items in your inventory are available for selection.");
            return;
        }
        
        // Group filtered items by category like main display
        var filteredCategories = filteredItems
            .GroupBy(i => new { i.ItemUICategory, i.CategoryName })
            .Select(categoryGroup => new
            {
                CategoryId = categoryGroup.Key.ItemUICategory,
                CategoryName = categoryGroup.Key.CategoryName,
                Items = categoryGroup.ToList()
            })
            .OrderBy(c => c.CategoryName)
            .ToList();
        
        foreach (var category in filteredCategories)
        {
            var isExpanded = ExpandedCategories.GetValueOrDefault(category.CategoryId, true);
            
            ImGui.PushID($"Filtered_{category.CategoryName}");
            
            // Category header for filtered items (with protection styling)
            var categoryHeaderText = $"{category.CategoryName} ({category.Items.Count} protected)";
            
            ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(0.8f, 0.6f, 0.6f, 1)); // Reddish tint for protection
            var headerOpen = ImGui.CollapsingHeader(categoryHeaderText, isExpanded ? ImGuiTreeNodeFlags.DefaultOpen : ImGuiTreeNodeFlags.None);
            ImGui.PopStyleColor();
            
            if (headerOpen)
            {
                ExpandedCategories[category.CategoryId] = true;
                _expandedCategoriesChanged = true;
                
                // Draw filtered items table (no checkboxes, read-only)
                ImGui.Indent();
                DrawFilteredItemsTable(category.Items);
                ImGui.Unindent();
            }
            else
            {
                ExpandedCategories[category.CategoryId] = false;
                _expandedCategoriesChanged = true;
            }
            
            ImGui.PopID();
        }
    }
    
    private List<InventoryItemInfo> GetFilteredOutItems()
    {
        // Start with all items and apply filters to see what gets excluded
        var allItems = _originalItems.AsEnumerable();
        var filteredOutItems = new List<InventoryItemInfo>();
        var filters = Settings.SafetyFilters;
        
        foreach (var item in allItems)
        {
            // Check if this item would be filtered out by any active filter
            bool isFilteredOut = false;
            
            if (filters.FilterUltimateTokens && InventoryHelpers.HardcodedBlacklist.Contains(item.ItemId))
                isFilteredOut = true;
            else if (filters.FilterCurrencyItems && InventoryHelpers.CurrencyRange.Contains(item.ItemId))
                isFilteredOut = true;
            else if (filters.FilterCrystalsAndShards && (item.ItemUICategory == 63 || item.ItemUICategory == 64))
                isFilteredOut = true;
            else if (filters.FilterGearsetItems && InventoryHelpers.IsInGearset(item.ItemId))
                isFilteredOut = true;
            else if (filters.FilterIndisposableItems && item.IsIndisposable)
                isFilteredOut = true;
            else if (filters.FilterHighLevelGear && item.EquipSlotCategory > 0 && item.ItemLevel >= filters.MaxGearItemLevel)
                isFilteredOut = true;
            else if (filters.FilterUniqueUntradeable && item.IsUnique && item.IsUntradable && !InventoryHelpers.SafeUniqueItems.Contains(item.ItemId))
                isFilteredOut = true;
            else if (filters.FilterHQItems && item.IsHQ)
                isFilteredOut = true;
            else if (filters.FilterCollectables && item.IsCollectable)
                isFilteredOut = true;
            else if (filters.FilterSpiritbondedItems && item.SpiritBond >= filters.MinSpiritbondToFilter)
                isFilteredOut = true;
            
            if (isFilteredOut)
            {
                filteredOutItems.Add(item);
            }
        }
        
        return filteredOutItems;
    }
    
    private void DrawFilteredItemsTable(List<InventoryItemInfo> items)
    {
        if (ImGui.BeginTable("FilteredItemsTable", Settings.ShowMarketPrices ? 5 : 4, 
            ImGuiTableFlags.Borders | ImGuiTableFlags.RowBg | ImGuiTableFlags.SizingStretchProp))
        {
            // No checkbox column for filtered items
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
                ImGui.TableSetupColumn("Reason", ImGuiTableColumnFlags.WidthFixed, 150);
            }
            
            ImGui.TableHeadersRow();
            
            foreach (var item in items)
            {
                DrawFilteredItemRow(item);
            }
            
            ImGui.EndTable();
        }
    }
    
    private void DrawFilteredItemRow(InventoryItemInfo item)
    {
        ImGui.TableNextRow();
        
        // Item name with icon (grayed out)
        ImGui.TableNextColumn();
        ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(0.6f, 0.6f, 0.6f, 1)); // Gray text
        
        // Show icon if available
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
        ImGui.PopStyleColor();
        
        // Add filter tags to show why it's filtered
        DrawItemFilterTags(item);
        
        if (item.IsHQ)
        {
            ImGui.SameLine();
            ImGui.TextColored(new Vector4(0.4f, 0.6f, 0.4f, 1), "[HQ]"); // Dimmed HQ
        }
        
        // Quantity
        ImGui.TableNextColumn();
        ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(0.6f, 0.6f, 0.6f, 1));
        ImGui.Text(item.Quantity.ToString());
        ImGui.PopStyleColor();
        
        // Location
        ImGui.TableNextColumn();
        ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(0.6f, 0.6f, 0.6f, 1));
        ImGui.Text(GetContainerDisplayName(item.Container));
        ImGui.PopStyleColor();
        
        // Price or reason
        if (Settings.ShowMarketPrices)
        {
            // Unit price
            ImGui.TableNextColumn();
            ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(0.6f, 0.6f, 0.6f, 1));
            var priceText = item.GetFormattedPrice();
            ImGui.Text(priceText);
            ImGui.PopStyleColor();
            
            // Total price
            ImGui.TableNextColumn();
            if (item.MarketPrice.HasValue && item.MarketPrice.Value > 0)
            {
                ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(0.6f, 0.6f, 0.6f, 1));
                ImGui.Text($"{item.MarketPrice.Value * item.Quantity:N0}g");
                ImGui.PopStyleColor();
            }
        }
        else
        {
            // Show reason for filtering
            ImGui.TableNextColumn();
            ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(0.8f, 0.6f, 0.6f, 1));
            var reason = GetFilterReason(item);
            ImGui.Text(reason);
            ImGui.PopStyleColor();
        }
    }
    
    private string GetFilterReason(InventoryItemInfo item)
    {
        var filters = Settings.SafetyFilters;
        
        if (filters.FilterUltimateTokens && InventoryHelpers.HardcodedBlacklist.Contains(item.ItemId))
            return "Ultimate/Special";
        if (filters.FilterCurrencyItems && InventoryHelpers.CurrencyRange.Contains(item.ItemId))
            return "Currency";
        if (filters.FilterCrystalsAndShards && (item.ItemUICategory == 63 || item.ItemUICategory == 64))
            return "Crystal/Shard";
        if (filters.FilterGearsetItems && InventoryHelpers.IsInGearset(item.ItemId))
            return "In Gearset";
        if (filters.FilterIndisposableItems && item.IsIndisposable)
            return "Indisposable";
        if (filters.FilterHighLevelGear && item.EquipSlotCategory > 0 && item.ItemLevel >= filters.MaxGearItemLevel)
            return $"High Level (i{item.ItemLevel})";
        if (filters.FilterUniqueUntradeable && item.IsUnique && item.IsUntradable && !InventoryHelpers.SafeUniqueItems.Contains(item.ItemId))
            return "Unique & Untradeable";
        if (filters.FilterHQItems && item.IsHQ)
            return "High Quality";
        if (filters.FilterCollectables && item.IsCollectable)
            return "Collectable";
        if (filters.FilterSpiritbondedItems && item.SpiritBond >= filters.MinSpiritbondToFilter)
            return $"Spiritbond {item.SpiritBond}%";
        
        return "Protected";
    }
    
    private void DrawActionButtons()
    {
        // No separator - buttons will be inline with tabs
    }
    
    private string GetContainerDisplayName(InventoryType container)
    {
        return container switch
        {
            InventoryType.Inventory1 or InventoryType.Inventory2 or 
            InventoryType.Inventory3 or InventoryType.Inventory4 => "Inventory",
            InventoryType.ArmoryMainHand => "Armory (Main)",
            InventoryType.ArmoryOffHand => "Armory (Off)",
            InventoryType.ArmoryHead => "Armory (Head)",
            InventoryType.ArmoryBody => "Armory (Body)",
            InventoryType.ArmoryHands => "Armory (Hands)",
            InventoryType.ArmoryLegs => "Armory (Legs)",
            InventoryType.ArmoryFeets => "Armory (Feet)",
            InventoryType.ArmoryEar => "Armory (Ears)",
            InventoryType.ArmoryNeck => "Armory (Neck)",
            InventoryType.ArmoryWrist => "Armory (Wrists)",
            InventoryType.ArmoryRings => "Armory (Rings)",
            InventoryType.ArmorySoulCrystal => "Armory (Soul)",
            _ => container.ToString()
        };
    }
}
