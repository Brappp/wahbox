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

public class InventoryManagementModule : BaseModule, IDrawable
{
    public override string Name => "Inventory Manager";
    public override ModuleType Type => ModuleType.Special;
    public override ModuleCategory Category => ModuleCategory.Other;
    public override bool HasWindow => false; // We'll draw directly in the main window
    
    private readonly InventoryHelpers _inventoryHelpers;
    private readonly UniversalisClient _universalisClient;
    private readonly TaskManager _taskManager;
    
    // UI State
    private List<CategoryGroup> _categories = new();
    private List<InventoryItemInfo> _allItems = new();
    private string _searchFilter = string.Empty;
    private Dictionary<uint, bool> ExpandedCategories => Settings.ExpandedCategories;
    private readonly HashSet<uint> _selectedItems = new();
    private readonly Dictionary<uint, (long price, DateTime fetchTime)> _priceCache = new();
    private readonly HashSet<uint> _fetchingPrices = new();
    
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
        
        var worldName = Plugin.ClientState.LocalPlayer?.CurrentWorld?.Value?.Name.ExtractText() ?? "Aether";
        _universalisClient = new UniversalisClient(Plugin.Log, worldName);
        _taskManager = new TaskManager();
    }
    
    public override void Initialize()
    {
        base.Initialize();
        RefreshInventory();
    }
    
    public override void Update()
    {
        base.Update();
        
        // Auto-refresh prices if enabled
        if (Settings.AutoRefreshPrices && !_isDiscarding)
        {
            var stalePrices = _allItems.Where(item => 
                !_fetchingPrices.Contains(item.ItemId) &&
                (!_priceCache.TryGetValue(item.ItemId, out var cached) || 
                 DateTime.Now - cached.fetchTime > TimeSpan.FromMinutes(Settings.PriceCacheDurationMinutes)))
                .Take(5)
                .ToList();
                
            foreach (var item in stalePrices)
            {
                _ = FetchMarketPrice(item);
            }
        }
    }
    
    public void Draw()
    {
        if (_isDiscarding)
        {
            DrawDiscardConfirmation();
            return;
        }
        
        // Search bar
        ImGui.SetNextItemWidth(300);
        if (ImGui.InputTextWithHint("##Search", "Search items...", ref _searchFilter, 100))
        {
            UpdateCategories();
        }
        
        ImGui.SameLine();
        if (ImGui.Button("Refresh"))
        {
            RefreshInventory();
        }
        
        ImGui.SameLine();
        ImGui.Text($"Total Items: {_allItems.Count} | Selected: {_selectedItems.Count}");
        
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
            
            var isExpanded = ExpandedCategories.GetValueOrDefault((uint)category.Name.GetHashCode(), true);
            
            ImGui.PushID(category.Name);
            
            // Category header
            var headerText = $"{category.Name} ({category.Items.Count} items, {category.TotalQuantity} total)";
            if (Settings.ShowMarketPrices && category.TotalValue.HasValue)
            {
                headerText += $" - {category.TotalValue.Value:N0} gil";
            }
            
            if (ImGui.CollapsingHeader(headerText, isExpanded ? ImGuiTreeNodeFlags.DefaultOpen : ImGuiTreeNodeFlags.None))
            {
                ExpandedCategories[(uint)category.Name.GetHashCode()] = true;
                Plugin.Configuration.Save();
                DrawCategoryItems(category);
            }
            else
            {
                ExpandedCategories[(uint)category.Name.GetHashCode()] = false;
                Plugin.Configuration.Save();
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
                
                // Try to draw icon
                if (item.IconId > 0)
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
    
    private void RefreshInventory()
    {
        _allItems = _inventoryHelpers.GetAllItems();
        
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
        
        // Fetch some initial prices if enabled
        if (Settings.ShowMarketPrices && Settings.AutoRefreshPrices)
        {
            var itemsNeedingPrices = _allItems
                .Where(i => !i.MarketPrice.HasValue)
                .Take(10)
                .ToList();
                
            foreach (var item in itemsNeedingPrices)
            {
                _ = FetchMarketPrice(item);
            }
        }
    }
    
    private void UpdateCategories()
    {
        var filteredItems = string.IsNullOrWhiteSpace(_searchFilter)
            ? _allItems
            : _allItems.Where(i => i.Name.Contains(_searchFilter, StringComparison.OrdinalIgnoreCase));
            
        _categories = filteredItems
            .GroupBy(i => i.CategoryName)
            .Select(g => new CategoryGroup
            {
                Name = g.Key,
                Items = g.OrderBy(i => i.Name).ToList()
            })
            .OrderBy(c => c.Name)
            .ToList();
    }
    
    private async Task FetchMarketPrice(InventoryItemInfo item)
    {
        if (_fetchingPrices.Contains(item.ItemId)) return;
        
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
                    var text = SeString.Parse(textNode->NodeText.ToString()).TextValue;
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
            InventoryType.SaddleBag1 => "Saddlebag 1",
            InventoryType.SaddleBag2 => "Saddlebag 2",
            InventoryType.PremiumSaddleBag1 => "P.Saddlebag 1",
            InventoryType.PremiumSaddleBag2 => "P.Saddlebag 2",
            _ when type >= InventoryType.ArmoryMainHand && type <= InventoryType.ArmoryRings => "Armory",
            _ => type.ToString()
        };
    }
    
    public override void Dispose()
    {
        _taskManager?.Dispose();
        _universalisClient?.Dispose();
        base.Dispose();
    }
}
