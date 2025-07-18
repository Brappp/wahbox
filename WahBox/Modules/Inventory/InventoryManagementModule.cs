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
    private bool _showInventory = true;
    private bool _showArmory = false;
    private bool _showOnlyHQ = false;
    private bool _showOnlyDiscardable = true; // Default to showing only discardable items for safety
    
    // Safety filter options
    private bool _showSafetyFilters = true;
    private bool _showOnlyFlagged = false;
    
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
        
        // Action buttons
        DrawActionButtons();
        
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
        
        if (_showOnlyDiscardable)
        {
            filteredItems = filteredItems.Where(i => i.SafetyAssessment?.IsSafeToDiscard == true);
        }
        
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
        
        if (_showOnlyDiscardable)
        {
            filteredItems = filteredItems.Where(i => i.SafetyAssessment?.IsSafeToDiscard == true);
        }
        
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
        
        // Search bar - responsive width
        var searchWidth = Math.Min(300f, availableWidth * 0.4f);
        ImGui.SetNextItemWidth(searchWidth);
        if (ImGui.InputTextWithHint("##Search", "Search items...", ref _searchFilter, 100))
        {
            _lastCategoryUpdate = DateTime.Now;
        }
        
        // Only update categories after a delay
        if (_lastCategoryUpdate != DateTime.MinValue && DateTime.Now - _lastCategoryUpdate > _categoryUpdateInterval)
        {
            UpdateCategories();
            _lastCategoryUpdate = DateTime.MinValue;
        }
        
        // Refresh button
        var refreshWidth = ImGui.CalcTextSize("Refresh").X + ImGui.GetStyle().FramePadding.X * 2;
        if (availableWidth > searchWidth + refreshWidth + spacing * 2)
        {
            ImGui.SameLine();
        }
        if (ImGui.Button("Refresh"))
        {
            RefreshInventory();
        }
        
        // Stats text - wrap if needed
        var statsText = $"Total Items: {_allItems.Count} | Selected: {_selectedItems.Count}";
        var statsWidth = ImGui.CalcTextSize(statsText).X;
        
        if (availableWidth > searchWidth + refreshWidth + statsWidth + spacing * 3)
        {
            ImGui.SameLine();
        }
        ImGui.Text(statsText);
    }
    
    private void DrawFiltersAndSettings()
    {
        ImGui.Separator();
        
        var availableWidth = ImGui.GetContentRegionAvail().X;
        
        // Location filters row
        ImGui.AlignTextToFramePadding();
        ImGui.Text("Show:");
        ImGui.SameLine();
        ImGui.TextColored(new Vector4(0.7f, 0.7f, 0.7f, 1), "[Inventory]");
        ImGui.SameLine();
        if (ImGui.Checkbox("Armory", ref _showArmory)) 
        {
            RefreshInventory();
        }
        
        ImGui.SameLine();
        ImGui.TextColored(new Vector4(0.5f, 0.5f, 0.5f, 1), "|");
        
        ImGui.SameLine();
        if (ImGui.Checkbox("HQ Only", ref _showOnlyHQ)) UpdateCategories();
        ImGui.SameLine();
        if (ImGui.Checkbox("Discardable Only", ref _showOnlyDiscardable)) UpdateCategories();
        
        // Safety filters section
        ImGui.Separator();
        DrawSafetyFilters();
        
        // Market price settings
        ImGui.Separator();
        DrawMarketPriceSettings(availableWidth);
    }
    
    private void DrawMarketPriceSettings(float availableWidth)
    {
        var showPrices = Settings.ShowMarketPrices;
        if (ImGui.Checkbox("Show Market Prices", ref showPrices))
        {
            Settings.ShowMarketPrices = showPrices;
            Plugin.Configuration.Save();
        }
        
        if (!Settings.ShowMarketPrices) return;
        
        var spacing = ImGui.GetStyle().ItemSpacing.X;
        var usedWidth = ImGui.CalcTextSize("Show Market Prices").X + ImGui.GetStyle().FramePadding.X * 2 + spacing;
        
        // Auto-refresh checkbox
        var autoRefreshWidth = ImGui.CalcTextSize("Auto-refresh Prices").X + 20; // 20 for checkbox
        if (availableWidth > usedWidth + autoRefreshWidth + spacing)
        {
            ImGui.SameLine();
        }
        
        var autoRefresh = Settings.AutoRefreshPrices;
        if (ImGui.Checkbox("Auto-refresh Prices", ref autoRefresh))
        {
            Settings.AutoRefreshPrices = autoRefresh;
            Plugin.Configuration.Save();
        }
        
        usedWidth += autoRefreshWidth + spacing;
        
        // Cache duration input
        var cacheWidth = 100f;
        if (availableWidth > usedWidth + cacheWidth + spacing)
        {
            ImGui.SameLine();
        }
        
        ImGui.SetNextItemWidth(cacheWidth);
        var cacheMinutes = Settings.PriceCacheDurationMinutes;
        if (ImGui.InputInt("Cache (min)", ref cacheMinutes))
        {
            Settings.PriceCacheDurationMinutes = Math.Max(1, cacheMinutes);
            Plugin.Configuration.Save();
        }
        
        usedWidth += cacheWidth + spacing;
        
        // World selection
        var worldWidth = 150f;
        if (availableWidth > usedWidth + worldWidth + spacing)
        {
            ImGui.SameLine();
        }
        
        ImGui.SetNextItemWidth(worldWidth);
        if (ImGui.BeginCombo("World", _selectedWorld))
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
                if (isSelected)
                    ImGui.SetItemDefaultFocus();
            }
            ImGui.EndCombo();
        }
    }
    
    private void DrawSafetyFilters()
    {
        ImGui.Text("Safety Filters:");
        
        if (ImGui.Checkbox("Show Only Flagged Items", ref _showOnlyFlagged))
        {
            UpdateCategories();
        }
        if (ImGui.IsItemHovered())
            ImGui.SetTooltip("Show only items with safety flags");
        
        if (ImGui.CollapsingHeader("Safety Protections", _showSafetyFilters ? ImGuiTreeNodeFlags.DefaultOpen : ImGuiTreeNodeFlags.None))
        {
            ImGui.Indent();
            
            var filters = Settings.SafetyFilters;
            bool changed = false;
            
            // Core safety protections
            ImGui.Text("Core Protections:");
            
            var filterUltimate = filters.FilterUltimateTokens;
            if (ImGui.Checkbox("Filter Ultimate Tokens", ref filterUltimate))
            {
                filters.FilterUltimateTokens = filterUltimate;
                changed = true;
            }
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip("Prevents discarding ultimate raid tokens and preorder earrings");
            
            ImGui.SameLine();
            var filterCurrency = filters.FilterCurrencyItems;
            if (ImGui.Checkbox("Filter Currency Items", ref filterCurrency))
            {
                filters.FilterCurrencyItems = filterCurrency;
                changed = true;
            }
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip("Prevents discarding gil, tomestones, and other currencies");
            
            var filterCrystals = filters.FilterCrystalsAndShards;
            if (ImGui.Checkbox("Filter Crystals & Shards", ref filterCrystals))
            {
                filters.FilterCrystalsAndShards = filterCrystals;
                changed = true;
            }
            
            ImGui.SameLine();
            var filterGearset = filters.FilterGearsetItems;
            if (ImGui.Checkbox("Filter Gearset Items", ref filterGearset))
            {
                filters.FilterGearsetItems = filterGearset;
                changed = true;
            }
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip("Prevents discarding items equipped in any gearset");
            
            var filterIndisposable = filters.FilterIndisposableItems;
            if (ImGui.Checkbox("Filter Indisposable Items", ref filterIndisposable))
            {
                filters.FilterIndisposableItems = filterIndisposable;
                changed = true;
            }
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip("Items the game explicitly marks as non-discardable");
            
            ImGui.Spacing();
            
            // Gear protection
            ImGui.Text("Gear Protection:");
            var filterHighLevel = filters.FilterHighLevelGear;
            if (ImGui.Checkbox("Filter High-Level Gear", ref filterHighLevel))
            {
                filters.FilterHighLevelGear = filterHighLevel;
                changed = true;
            }
            
            if (filters.FilterHighLevelGear)
            {
                ImGui.SameLine();
                ImGui.SetNextItemWidth(100);
                var maxLevel = filters.MaxGearItemLevel;
                if (ImGui.InputInt("Max iLevel", ref maxLevel))
                {
                    filters.MaxGearItemLevel = maxLevel;
                    changed = true;
                }
                if (ImGui.IsItemHovered())
                    ImGui.SetTooltip("Gear above this item level will be protected");
            }
            
            var filterUnique = filters.FilterUniqueUntradeable;
            if (ImGui.Checkbox("Filter Unique & Untradeable", ref filterUnique))
            {
                filters.FilterUniqueUntradeable = filterUnique;
                changed = true;
            }
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip("Prevents discarding most unique and untradeable items");
            
            ImGui.Spacing();
            
            // Quality filters
            ImGui.Text("Quality & Enhancement:");
            var filterHQ = filters.FilterHQItems;
            if (ImGui.Checkbox("Filter HQ Items", ref filterHQ))
            {
                filters.FilterHQItems = filterHQ;
                changed = true;
            }
            
            ImGui.SameLine();
            var filterCollectable = filters.FilterCollectables;
            if (ImGui.Checkbox("Filter Collectables", ref filterCollectable))
            {
                filters.FilterCollectables = filterCollectable;
                changed = true;
            }
            
            var filterSpiritbond = filters.FilterSpiritbondedItems;
            if (ImGui.Checkbox("Filter Spiritbonded Items", ref filterSpiritbond))
            {
                filters.FilterSpiritbondedItems = filterSpiritbond;
                changed = true;
            }
            
            if (filters.FilterSpiritbondedItems)
            {
                ImGui.SameLine();
                ImGui.SetNextItemWidth(80);
                var minSpiritbond = filters.MinSpiritbondToFilter;
                if (ImGui.InputInt("Min %", ref minSpiritbond))
                {
                    filters.MinSpiritbondToFilter = minSpiritbond;
                    changed = true;
                }
                if (ImGui.IsItemHovered())
                    ImGui.SetTooltip("Items with spiritbond >= this value will be protected");
            }
            
            if (changed)
            {
                Plugin.Configuration.Save();
                RefreshInventory();
            }
            
            ImGui.Unindent();
        }
    }
    
    private void DrawItemSafetyFlags(InventoryItemInfo item)
    {
        if (item.SafetyAssessment?.SafetyFlags.Any() != true)
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
        
        var flagText = string.Join(", ", item.SafetyAssessment.SafetyFlags);
        ImGui.Text($"[{flagText}]");
        
        if (ImGui.IsItemHovered())
        {
            ImGui.BeginTooltip();
            ImGui.Text("Safety Assessment:");
            foreach (var flag in item.SafetyAssessment.SafetyFlags)
            {
                ImGui.BulletText(flag);
            }
            
            if (!item.SafetyAssessment.IsSafeToDiscard)
            {
                ImGui.Separator();
                ImGui.TextColored(new Vector4(0.8f, 0.2f, 0.2f, 1), "This item is protected from discarding");
            }
            
            ImGui.EndTooltip();
        }
        
        ImGui.PopStyleColor();
    }
    
    private void DrawActionButtons()
    {
        ImGui.Separator();
        
        var availableWidth = ImGui.GetContentRegionAvail().X;
        var spacing = ImGui.GetStyle().ItemSpacing.X;
        
        // Calculate button widths
        var clearWidth = ImGui.CalcTextSize("Clear Selection").X + ImGui.GetStyle().FramePadding.X * 2;
        var discardText = _selectedItems.Count > 0 ? $"Discard Selected ({_selectedItems.Count})" : "Discard Selected (0)";
        var discardWidth = ImGui.CalcTextSize(discardText).X + ImGui.GetStyle().FramePadding.X * 2;
        
        var totalButtonWidth = clearWidth + discardWidth + spacing;
        
        // Center buttons if there's enough space, otherwise let them wrap
        if (availableWidth > totalButtonWidth)
        {
            var centerOffset = (availableWidth - totalButtonWidth) * 0.5f;
            ImGui.SetCursorPosX(ImGui.GetCursorPosX() + centerOffset);
        }
        
        // Clear Selection button
        if (ImGui.Button("Clear Selection"))
        {
            _selectedItems.Clear();
            foreach (var item in _allItems)
            {
                item.IsSelected = false;
            }
        }
        
        // Discard button
        if (availableWidth > totalButtonWidth)
        {
            ImGui.SameLine();
        }
        
        if (_selectedItems.Count > 0)
        {
            ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(0.8f, 0.2f, 0.2f, 1));
            if (ImGui.Button(discardText))
            {
                PrepareDiscard();
            }
            ImGui.PopStyleColor();
        }
        else
        {
            ImGui.BeginDisabled();
            ImGui.Button(discardText);
            ImGui.EndDisabled();
        }
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
