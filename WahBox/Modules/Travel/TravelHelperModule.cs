using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using WahBox.Core;
using WahBox.Core.Interfaces;
using ImGuiNET;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using FFXIVClientStructs.FFXIV.Client.Game.Control;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Component.GUI;
using Lumina.Excel.Sheets;
using ECommons.GameHelpers;
using ECommons.DalamudServices;
using ECommons.GameFunctions;
using ECommons.UIHelpers;
using ECommons.Automation;
using ECommons.Throttlers;
using static ECommons.GenericHelpers;
using Dalamud.Memory;

namespace WahBox.Modules.Travel;

public unsafe class TravelHelperModule : BaseModule, IDrawable
{
    public override string Name => "Travel Helper";
    public override ModuleType Type => ModuleType.Special;
    private readonly Plugin _plugin;
    private Dictionary<string, object> Configuration = new();
    private string _searchQuery = "";
    private string _coordinateInput = "";
    private List<AetheryteInfo> _searchResults = new();
    
    // UI state variables
    private int _autoTravelTerritoryId = 0;
    private string _autoTravelCoordInput = "";
    
    // Auto-travel system
    private bool _isAutoTraveling = false;
    private uint _targetTerritoryId = 0;
    private Vector2 _targetCoordinates = Vector2.Zero;
    private AetheryteInfo? _nearestAetheryte = null;
    private AutoTravelStep _currentAutoTravelStep = AutoTravelStep.None;
    
    // Aethernet automation
    private bool _isAutomatingAethernet = false;
    private AetheryteInfo? _pendingAethernetShard = null;
    private DateTime _lastActionTime = DateTime.MinValue;
    private AethernetAutomationStep _currentStep = AethernetAutomationStep.None;
    
    private enum AutoTravelStep
    {
        None,
        TeleportToTerritory,
        WaitingForTeleport,
        FindingNearestAetheryte,
        TravelingToAetheryte,
        Complete
    }
    
    private enum AethernetAutomationStep
    {
        None,
        TargetingAetheryte,
        InteractingWithAetheryte,
        SelectingAethernet,
        SelectingDestination,
        ConfirmingDestination,
        Complete
    }
    
    // Aetheryte database - main aetherytes with their aethernet shards
    private readonly Dictionary<uint, AetheryteZone> _aetheryteZones = new();
    private readonly Dictionary<uint, string> _territoryNames = new();
    
    private class AetheryteInfo
    {
        public uint Id { get; set; }
        public uint SubId { get; set; }
        public string Name { get; set; } = "";
        public uint TerritoryId { get; set; }
        public string TerritoryName { get; set; } = "";
        public bool IsMainAetheryte { get; set; }
        public uint AethernetGroup { get; set; }
        public Vector2 Position { get; set; }
        public bool IsAttuned { get; set; }
        public AetheryteInfo? ParentAetheryte { get; set; }
    }
    
    private class AetheryteZone
    {
        public uint TerritoryId { get; set; }
        public string TerritoryName { get; set; } = "";
        public AetheryteInfo MainAetheryte { get; set; } = null!;
        public List<AetheryteInfo> AethernetShards { get; set; } = new();
    }
    
    public TravelHelperModule(Plugin plugin) : base(plugin)
    {
        _plugin = plugin;
        IconId = 66316; // Aetheryte icon
        
        // Build aetheryte database
        BuildAetheryteDatabase();
    }
    
    /// <summary>
    /// Start auto-travel to a specific location
    /// </summary>
    /// <param name="territoryId">Target territory ID</param>
    /// <param name="x">X coordinate</param>
    /// <param name="y">Y coordinate (Z in game)</param>
    public void StartAutoTravel(uint territoryId, float x, float y)
    {
        if (_isAutoTraveling)
        {
            Plugin.ChatGui.PrintError("[Travel Helper] Already traveling!");
            return;
        }
        
        _targetTerritoryId = territoryId;
        _targetCoordinates = new Vector2(x, y);
        _isAutoTraveling = true;
        _currentAutoTravelStep = AutoTravelStep.TeleportToTerritory;
        _nearestAetheryte = null;
        
        var territoryName = _territoryNames.GetValueOrDefault(territoryId, "Unknown");
        Plugin.ChatGui.Print($"[Travel Helper] Starting auto-travel to {territoryName} ({x:F1}, {y:F1})");
    }
    
    /// <summary>
    /// Cancel auto-travel
    /// </summary>
    public void CancelAutoTravel()
    {
        _isAutoTraveling = false;
        _currentAutoTravelStep = AutoTravelStep.None;
        _nearestAetheryte = null;
        Plugin.ChatGui.Print("[Travel Helper] Auto-travel cancelled.");
    }
    
    private void BuildAetheryteDatabase()
    {
        var aetheryteSheet = Plugin.DataManager.GetExcelSheet<Aetheryte>();
        var territorySheet = Plugin.DataManager.GetExcelSheet<TerritoryType>();
        var mapSheet = Plugin.DataManager.GetExcelSheet<Lumina.Excel.Sheets.Map>();
        
        if (aetheryteSheet == null || territorySheet == null || mapSheet == null) return;
        
        // First, cache territory names
        foreach (var territory in territorySheet)
        {
            var name = territory.PlaceName.ValueNullable?.Name.ExtractText();
            if (!string.IsNullOrEmpty(name))
            {
                _territoryNames[territory.RowId] = name;
            }
        }
        
        // Build main aetheryte dictionary
        var mainAetherytes = new Dictionary<uint, AetheryteInfo>();
        
        // First pass - collect main aetherytes
        foreach (var aetheryte in aetheryteSheet)
        {
            if (aetheryte.RowId == 0) continue;
            if (aetheryte.AethernetGroup == 0) continue; // Skip non-grouped aetherytes
            
            var territoryId = aetheryte.Territory.RowId;
            var territoryName = _territoryNames.GetValueOrDefault(territoryId, "Unknown");
            
            var info = new AetheryteInfo
            {
                Id = aetheryte.RowId,
                SubId = 0,
                Name = aetheryte.PlaceName.ValueNullable?.Name.ExtractText() ?? 
                       aetheryte.AethernetName.ValueNullable?.Name.ExtractText() ?? 
                       "Unknown",
                TerritoryId = territoryId,
                TerritoryName = territoryName,
                IsMainAetheryte = aetheryte.IsAetheryte,
                AethernetGroup = aetheryte.AethernetGroup,
                Position = GetAetherytePosition(aetheryte, territoryId)
            };
            
            if (aetheryte.IsAetheryte)
            {
                mainAetherytes[aetheryte.AethernetGroup] = info;
                
                if (!_aetheryteZones.ContainsKey(territoryId))
                {
                    _aetheryteZones[territoryId] = new AetheryteZone
                    {
                        TerritoryId = territoryId,
                        TerritoryName = territoryName,
                        MainAetheryte = info
                    };
                }
            }
        }
        
        // Second pass - collect aethernet shards
        foreach (var aetheryte in aetheryteSheet)
        {
            if (aetheryte.RowId == 0) continue;
            if (aetheryte.AethernetGroup == 0) continue;
            if (aetheryte.IsAetheryte) continue; // Skip main aetherytes
            
            var territoryId = aetheryte.Territory.RowId;
            var territoryName = _territoryNames.GetValueOrDefault(territoryId, "Unknown");
            
            var info = new AetheryteInfo
            {
                Id = aetheryte.RowId,
                SubId = 0,
                Name = aetheryte.AethernetName.ValueNullable?.Name.ExtractText() ?? "Unknown",
                TerritoryId = territoryId,
                TerritoryName = territoryName,
                IsMainAetheryte = false,
                AethernetGroup = aetheryte.AethernetGroup,
                Position = GetAetherytePosition(aetheryte, territoryId),
                ParentAetheryte = mainAetherytes.GetValueOrDefault(aetheryte.AethernetGroup)
            };
            
            if (_aetheryteZones.TryGetValue(territoryId, out var zone))
            {
                zone.AethernetShards.Add(info);
            }
        }
        
        Plugin.Log.Information($"Built aetheryte database: {_aetheryteZones.Count} zones, " +
                             $"{_aetheryteZones.Values.Sum(z => z.AethernetShards.Count)} aethernet shards");
    }
    
    private Vector2 GetAetherytePosition(Aetheryte aetheryte, uint territoryId)
    {
        // For now, return a default position
        // TODO: Implement proper position calculation using map markers
        return new Vector2(0, 0);
    }
    
    public void Draw()
    {
        if (ImGui.BeginTabBar("TravelHelperTabs"))
        {
            if (ImGui.BeginTabItem("Quick Travel"))
            {
                DrawQuickTravelTab();
                ImGui.EndTabItem();
            }
            
            if (ImGui.BeginTabItem("Auto Travel"))
            {
                DrawAutoTravelTab();
                ImGui.EndTabItem();
            }
            
            if (ImGui.BeginTabItem("Search"))
            {
                DrawSearchTab();
                ImGui.EndTabItem();
            }
            
            if (ImGui.BeginTabItem("Aethernet"))
            {
                DrawAethernetTab();
                ImGui.EndTabItem();
            }
            
            if (ImGui.BeginTabItem("Debug"))
            {
                DrawDebugSection();
                ImGui.EndTabItem();
            }
            
            ImGui.EndTabBar();
        }
        
        // Show status for ongoing operations
        DrawStatusSection();
    }
    
    private void DrawQuickTravelTab()
    {
        ImGui.Text("Quick Teleport to Major Cities:");
        ImGui.Spacing();
        
        // Row 1
        if (ImGui.Button("Limsa Lominsa", new Vector2(120, 30)))
            Plugin.TeleportManager.TeleportToLocation("Limsa Lominsa");
        ImGui.SameLine();
        if (ImGui.Button("Gridania", new Vector2(120, 30)))
            Plugin.TeleportManager.TeleportToLocation("Gridania");
        ImGui.SameLine();
        if (ImGui.Button("Ul'dah", new Vector2(120, 30)))
            Plugin.TeleportManager.TeleportToLocation("Ul'dah");
        
        // Row 2
        if (ImGui.Button("Ishgard", new Vector2(120, 30)))
            Plugin.TeleportManager.TeleportToLocation("Ishgard");
        ImGui.SameLine();
        if (ImGui.Button("Kugane", new Vector2(120, 30)))
            Plugin.TeleportManager.TeleportToLocation("Kugane");
        ImGui.SameLine();
        if (ImGui.Button("Crystarium", new Vector2(120, 30)))
            Plugin.TeleportManager.TeleportToLocation("Crystarium");
        
        // Row 3
        if (ImGui.Button("Old Sharlayan", new Vector2(120, 30)))
            Plugin.TeleportManager.TeleportToLocation("Old Sharlayan");
        ImGui.SameLine();
        if (ImGui.Button("Radz-at-Han", new Vector2(120, 30)))
            Plugin.TeleportManager.TeleportToLocation("Radz-at-Han");
        ImGui.SameLine();
        if (ImGui.Button("Solution Nine", new Vector2(120, 30)))
            Plugin.TeleportManager.TeleportToLocation("Solution Nine");
    }
    
    private void DrawAutoTravelTab()
    {
        ImGui.Text("Auto-Travel to Specific Coordinates");
        ImGui.Separator();
        
        ImGui.TextWrapped("Automatically travel to the nearest aetheryte/aethernet shard to your target coordinates.");
        ImGui.Spacing();
        
        // Territory selection
        ImGui.Text("Territory ID:");
        ImGui.SameLine();
        ImGui.SetNextItemWidth(100);
        ImGui.InputInt("##territory", ref _autoTravelTerritoryId, 0);
        
        ImGui.SameLine();
        ImGui.Text($"Current: {Plugin.ClientState.TerritoryType}");
        if (_territoryNames.TryGetValue(Plugin.ClientState.TerritoryType, out var currentName))
        {
            ImGui.SameLine();
            ImGui.TextColored(new Vector4(0.7f, 0.7f, 1.0f, 1), $"({currentName})");
        }
        
        // Coordinate input
        ImGui.Text("Coordinates (X, Z):");
        ImGui.SetNextItemWidth(200);
        ImGui.InputText("##coords", ref _autoTravelCoordInput, 50);
        ImGui.SameLine();
        ImGui.TextDisabled("(?)");
        if (ImGui.IsItemHovered())
            ImGui.SetTooltip("Format: X, Z (e.g., 12.5, 15.3)");
        
        ImGui.Spacing();
        
        // Start button
        if (ImGui.Button("Start Auto-Travel", new Vector2(200, 30)))
        {
            var parts = _autoTravelCoordInput.Split(',');
            if (parts.Length == 2 && 
                float.TryParse(parts[0].Trim(), out var x) && 
                float.TryParse(parts[1].Trim(), out var z) &&
                _autoTravelTerritoryId > 0)
            {
                StartAutoTravel((uint)_autoTravelTerritoryId, x, z);
            }
            else
            {
                Plugin.ChatGui.PrintError("[Travel Helper] Invalid input. Please enter territory ID and coordinates.");
            }
        }
        
        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Text("Quick Examples:");
        
        if (ImGui.Button("Current Zone Center"))
        {
            _autoTravelTerritoryId = (int)Plugin.ClientState.TerritoryType;
            _autoTravelCoordInput = "0, 0";
        }
        
        ImGui.SameLine();
        if (ImGui.Button("Limsa Market (11.0, 11.0)"))
        {
            _autoTravelTerritoryId = 129; // Limsa Lominsa Lower Decks
            _autoTravelCoordInput = "11.0, 11.0";
        }
    }
    
    private void DrawSearchTab()
    {
        DrawSearchSection();
    }
    
    private void DrawAethernetTab()
    {
        DrawAethernetSection();
    }
    
    private void DrawStatusSection()
    {
        // Show automation status
        if (_isAutomatingAethernet)
        {
            ImGui.Separator();
            ImGui.TextColored(new Vector4(1.0f, 1.0f, 0.0f, 1.0f), 
                $"Automating aethernet teleport... Step: {_currentStep}");
            if (ImGui.Button("Cancel"))
            {
                CancelAethernetAutomation();
            }
        }
        
        // Show auto-travel status
        if (_isAutoTraveling)
        {
            ImGui.Separator();
            ImGui.TextColored(new Vector4(0.0f, 1.0f, 1.0f, 1.0f), 
                $"Auto-traveling... Step: {_currentAutoTravelStep}");
            if (_nearestAetheryte != null)
            {
                ImGui.Text($"Target: {_nearestAetheryte.Name}");
            }
            ImGui.Text($"Destination: Territory {_targetTerritoryId}, Coords: {_targetCoordinates.X:F1}, {_targetCoordinates.Y:F1}");
            if (ImGui.Button("Cancel Travel"))
            {
                CancelAutoTravel();
            }
        }
    }
    
    private void DrawSearchSection()
    {
        ImGui.Text("Search All Aetherytes:");
        ImGui.SetNextItemWidth(300);
        if (ImGui.InputText("##search", ref _searchQuery, 100))
        {
            UpdateSearchResults();
        }
        
        if (_searchResults.Any())
        {
            ImGui.BeginChild("SearchResults", new Vector2(0, 200), true);
            
            string? lastTerritory = null;
            foreach (var result in _searchResults.Take(20))
            {
                // Group by territory
                if (lastTerritory != result.TerritoryName)
                {
                    ImGui.TextColored(new Vector4(0.7f, 0.7f, 1.0f, 1), result.TerritoryName);
                    lastTerritory = result.TerritoryName;
                }
                
                var isAttuned = IsAetheryteAttuned(result.Id);
                if (!isAttuned)
                    ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(0.5f, 0.5f, 0.5f, 1));
                
                var prefix = result.IsMainAetheryte ? "  [Main] " : "    [Shard] ";
                if (ImGui.Selectable($"{prefix}{result.Name}"))
                {
                    if (isAttuned)
                    {
                        if (result.IsMainAetheryte)
                        {
                            Plugin.TeleportManager.TeleportToAetheryte(result.Id);
                        }
                        else
                        {
                            Plugin.ChatGui.Print($"[Travel Helper] Aethernet teleport to {result.Name} requires interaction with main aetheryte first.");
                            if (result.ParentAetheryte != null)
                            {
                                Plugin.ChatGui.Print($"[Travel Helper] Teleport to {result.ParentAetheryte.Name} and select Aethernet → {result.Name}");
                            }
                        }
                    }
                }
                
                if (!isAttuned)
                {
                    ImGui.PopStyleColor();
                    if (ImGui.IsItemHovered())
                        ImGui.SetTooltip("Not attuned to this aetheryte");
                }
            }
            ImGui.EndChild();
        }
    }
    
    private void DrawAethernetSection()
    {
        ImGui.Text("Current Zone Aethernet:");
        
        var currentTerritory = Plugin.ClientState.TerritoryType;
        if (_aetheryteZones.TryGetValue(currentTerritory, out var zone))
        {
            ImGui.TextColored(new Vector4(0.8f, 0.8f, 0.2f, 1), $"{zone.TerritoryName} Aethernet");
            
            // Check if main aetheryte is attuned
            var mainAttuned = IsAetheryteAttuned(zone.MainAetheryte.Id);
            
            if (!mainAttuned)
            {
                ImGui.TextColored(new Vector4(1.0f, 0.3f, 0.3f, 1), "Not attuned to main aetheryte!");
                ImGui.Text($"Please attune to {zone.MainAetheryte.Name} first.");
            }
            else if (zone.AethernetShards.Any())
            {
                ImGui.BeginChild("CurrentAethernet", new Vector2(0, 150), true);
                
                // Show main aetheryte
                if (ImGui.Button($"Main: {zone.MainAetheryte.Name}"))
                {
                    TargetNearestAetheryte();
                }
                ImGui.SameLine();
                ImGui.TextColored(new Vector4(0.2f, 0.8f, 0.2f, 1), "[Attuned]");
                
                ImGui.Separator();
                
                // Show aethernet shards
                foreach (var shard in zone.AethernetShards)
                {
                    if (ImGui.Button($"→ {shard.Name}"))
                    {
                        StartAethernetTeleport(shard);
                    }
                    
                    // Show if we can use this shard
                    ImGui.SameLine();
                    if (mainAttuned)
                    {
                        ImGui.TextColored(new Vector4(0.2f, 0.8f, 0.2f, 1), "[Available]");
                    }
                    else
                    {
                        ImGui.TextColored(new Vector4(0.5f, 0.5f, 0.5f, 1), "[Locked]");
                    }
                }
                
                ImGui.EndChild();
            }
            else
            {
                ImGui.Text("No aethernet in this zone.");
            }
        }
        else
        {
            ImGui.Text("No aethernet data for current zone.");
        }
    }
    

    private void DrawDebugSection()
    {
        if (ImGui.CollapsingHeader("Debug Information"))
        {
            ImGui.Text("Current Territory: " + Plugin.ClientState.TerritoryType);
            
            // Show raw aetheryte data from game
            if (ImGui.CollapsingHeader("Raw Aetheryte Sheet Data"))
            {
                var aetheryteSheet = Plugin.DataManager.GetExcelSheet<Aetheryte>();
                if (aetheryteSheet != null)
                {
                    ImGui.BeginChild("AetheryteDebug", new Vector2(0, 300), true);
                    
                    var currentTerritory = Plugin.ClientState.TerritoryType;
                    int count = 0;
                    foreach (var aetheryte in aetheryteSheet)
                    {
                        if (aetheryte.Territory.RowId == currentTerritory && count < 20)
                        {
                            ImGui.Text($"ID: {aetheryte.RowId}");
                            ImGui.Text($"  PlaceName: {aetheryte.PlaceName.ValueNullable?.Name.ExtractText() ?? "null"}");
                            ImGui.Text($"  AethernetName: {aetheryte.AethernetName.ValueNullable?.Name.ExtractText() ?? "null"}");
                            ImGui.Text($"  IsAetheryte: {aetheryte.IsAetheryte}");
                            ImGui.Text($"  AethernetGroup: {aetheryte.AethernetGroup}");
                            ImGui.Text($"  Territory: {aetheryte.Territory.RowId}");
                            ImGui.Separator();
                            count++;
                        }
                    }
                    
                    ImGui.EndChild();
                }
            }
            
            // Show our parsed data
            if (ImGui.CollapsingHeader("Our Aetheryte Database"))
            {
                var currentTerritory = Plugin.ClientState.TerritoryType;
                if (_aetheryteZones.TryGetValue(currentTerritory, out var zone))
                {
                    ImGui.Text($"Zone: {zone.TerritoryName}");
                    ImGui.Text($"Main Aetheryte: {zone.MainAetheryte.Name} (ID: {zone.MainAetheryte.Id})");
                    ImGui.Text($"Aethernet Shards ({zone.AethernetShards.Count}):");
                    
                    ImGui.BeginChild("ShardDebug", new Vector2(0, 200), true);
                    foreach (var shard in zone.AethernetShards)
                    {
                        ImGui.Text($"- {shard.Name} (ID: {shard.Id}, Group: {shard.AethernetGroup})");
                    }
                    ImGui.EndChild();
                }
                else
                {
                    ImGui.Text("No data for current territory");
                }
            }
            
            // Show TelepotTown data if open
            if (ImGui.CollapsingHeader("TelepotTown Data (if open)"))
            {
                if (TryGetAddonByName<AtkUnitBase>("TelepotTown", out var telepotTown) && IsAddonReady(telepotTown))
                {
                    ImGui.Text("TelepotTown is open!");
                    
                    var destinations = GetTelepotTownDestinationsSimple(telepotTown);
                    ImGui.Text($"Found {destinations.Count} destinations:");
                    
                    ImGui.BeginChild("TelepotDebug", new Vector2(0, 200), true);
                    for (int i = 0; i < destinations.Count; i++)
                    {
                        ImGui.Text($"[{i}]: {destinations[i]}");
                    }
                    ImGui.EndChild();
                }
                else
                {
                    ImGui.Text("TelepotTown is not open");
                }
            }
        }
    }
    
    private void UpdateSearchResults()
    {
        _searchResults.Clear();
        
        if (string.IsNullOrWhiteSpace(_searchQuery))
            return;
            
        var query = _searchQuery.ToLower();
        
        foreach (var zone in _aetheryteZones.Values)
        {
            // Check main aetheryte
            if (zone.MainAetheryte.Name.ToLower().Contains(query) || 
                zone.TerritoryName.ToLower().Contains(query))
            {
                zone.MainAetheryte.IsAttuned = IsAetheryteAttuned(zone.MainAetheryte.Id);
                _searchResults.Add(zone.MainAetheryte);
            }
            
            // Check aethernet shards
            foreach (var shard in zone.AethernetShards)
            {
                if (shard.Name.ToLower().Contains(query))
                {
                    shard.IsAttuned = IsAetheryteAttuned(shard.Id);
                    _searchResults.Add(shard);
                }
            }
        }
        
        _searchResults = _searchResults
            .OrderBy(a => a.TerritoryName)
            .ThenBy(a => !a.IsMainAetheryte)
            .ThenBy(a => a.Name)
            .ToList();
    }
    
    private void TargetNearestAetheryte()
    {
        try
        {
            // Find the nearest aetheryte object
            IGameObject? nearestAetheryte = null;
            float nearestDistance = float.MaxValue;
            
            foreach (var obj in Svc.Objects)
            {
                if (obj.ObjectKind == Dalamud.Game.ClientState.Objects.Enums.ObjectKind.Aetheryte)
                {
                    var distance = Vector3.Distance(Player.Position, obj.Position);
                    if (distance < nearestDistance && distance < 30f) // Within 30 yalms
                    {
                        nearestAetheryte = obj;
                        nearestDistance = distance;
                    }
                }
            }
            
            if (nearestAetheryte != null)
            {
                Svc.Targets.Target = nearestAetheryte;
                Plugin.ChatGui.Print($"[Travel Helper] Targeted {nearestAetheryte.Name}. Interact to open menu.");
            }
            else
            {
                Plugin.ChatGui.Print("[Travel Helper] No aetheryte found nearby.");
            }
        }
        catch (Exception ex)
        {
            Plugin.Log.Error($"Error targeting aetheryte: {ex.Message}");
        }
    }
    
    private void StartAethernetTeleport(AetheryteInfo shard)
    {
        try
        {
            // Find the nearest aetheryte
            IGameObject? nearestAetheryte = null;
            float nearestDistance = float.MaxValue;
            
            foreach (var obj in Svc.Objects)
            {
                if (obj.ObjectKind == Dalamud.Game.ClientState.Objects.Enums.ObjectKind.Aetheryte)
                {
                    var distance = Vector3.Distance(Player.Position, obj.Position);
                    if (distance < nearestDistance && distance < 30f)
                    {
                        nearestAetheryte = obj;
                        nearestDistance = distance;
                    }
                }
            }
            
            if (nearestAetheryte == null)
            {
                Plugin.ChatGui.Print("[Travel Helper] No aetheryte nearby. Please approach an aetheryte first.");
                return;
            }
            
            // Start automation
            _pendingAethernetShard = shard;
            _isAutomatingAethernet = true;
            _currentStep = AethernetAutomationStep.TargetingAetheryte;
            _lastActionTime = DateTime.Now;
            
            Plugin.ChatGui.Print($"[Travel Helper] Starting automated teleport to {shard.Name}...");
        }
        catch (Exception ex)
        {
            Plugin.Log.Error($"Error starting aethernet teleport: {ex.Message}");
            CancelAethernetAutomation();
        }
    }
    
    public override void Update()
    {
        if (_isAutomatingAethernet && _pendingAethernetShard != null)
        {
            ProcessAethernetAutomation();
        }
        
        if (_isAutoTraveling)
        {
            ProcessAutoTravel();
        }
    }
    
    private void ProcessAutoTravel()
    {
        // Add throttling
        if (!EzThrottler.Throttle("AutoTravel", 500))
            return;
            
        switch (_currentAutoTravelStep)
        {
            case AutoTravelStep.TeleportToTerritory:
                // Check if we're already in the target territory
                if (Plugin.ClientState.TerritoryType == _targetTerritoryId)
                {
                    Plugin.ChatGui.Print("[Travel Helper] Already in target territory, finding nearest aetheryte...");
                    _currentAutoTravelStep = AutoTravelStep.FindingNearestAetheryte;
                }
                else
                {
                    // Find a main aetheryte in the target territory
                    if (_aetheryteZones.TryGetValue(_targetTerritoryId, out var zone) && 
                        zone.MainAetheryte != null && 
                        IsAetheryteAttuned(zone.MainAetheryte.Id))
                    {
                        Plugin.ChatGui.Print($"[Travel Helper] Teleporting to {zone.MainAetheryte.Name}...");
                        Plugin.TeleportManager.TeleportToAetheryte(zone.MainAetheryte.Id);
                        _currentAutoTravelStep = AutoTravelStep.WaitingForTeleport;
                        _lastActionTime = DateTime.Now;
                    }
                    else
                    {
                        Plugin.ChatGui.PrintError($"[Travel Helper] No attuned aetheryte found in target territory!");
                        CancelAutoTravel();
                    }
                }
                break;
                
            case AutoTravelStep.WaitingForTeleport:
                // Wait for teleport to complete
                if (Plugin.ClientState.TerritoryType == _targetTerritoryId)
                {
                    Plugin.ChatGui.Print("[Travel Helper] Arrived in target territory!");
                    _currentAutoTravelStep = AutoTravelStep.FindingNearestAetheryte;
                }
                else if ((DateTime.Now - _lastActionTime).TotalSeconds > 15)
                {
                    Plugin.ChatGui.PrintError("[Travel Helper] Teleport timed out.");
                    CancelAutoTravel();
                }
                break;
                
            case AutoTravelStep.FindingNearestAetheryte:
                // Find the nearest aetheryte to our target coordinates
                _nearestAetheryte = FindNearestAetheryteToCoordinates(_targetTerritoryId, _targetCoordinates);
                
                if (_nearestAetheryte != null)
                {
                    Plugin.ChatGui.Print($"[Travel Helper] Nearest aetheryte: {_nearestAetheryte.Name}");
                    
                    if (_nearestAetheryte.IsMainAetheryte)
                    {
                        // We're already at the main aetheryte
                        Plugin.ChatGui.Print($"[Travel Helper] Arrived at {_nearestAetheryte.Name}!");
                        _currentAutoTravelStep = AutoTravelStep.Complete;
                    }
                    else
                    {
                        // It's an aethernet shard, start the aethernet teleport
                        StartAethernetTeleport(_nearestAetheryte);
                        _currentAutoTravelStep = AutoTravelStep.TravelingToAetheryte;
                    }
                }
                else
                {
                    Plugin.ChatGui.PrintError("[Travel Helper] No suitable aetheryte found near target coordinates.");
                    CancelAutoTravel();
                }
                break;
                
            case AutoTravelStep.TravelingToAetheryte:
                // Wait for aethernet teleport to complete
                if (!_isAutomatingAethernet)
                {
                    Plugin.ChatGui.Print($"[Travel Helper] Arrived at destination!");
                    Plugin.ChatGui.Print($"[Travel Helper] Target coordinates: {_targetCoordinates.X:F1}, {_targetCoordinates.Y:F1}");
                    _currentAutoTravelStep = AutoTravelStep.Complete;
                }
                break;
                
            case AutoTravelStep.Complete:
                Plugin.ChatGui.Print("[Travel Helper] Auto-travel complete!");
                CancelAutoTravel();
                break;
        }
    }
    
    private void ProcessAethernetAutomation()
    {
        // Add throttling to prevent spamming
        if (!EzThrottler.Throttle("AethernetAutomation", 250))
            return;
            
        try
        {
            switch (_currentStep)
            {
                case AethernetAutomationStep.TargetingAetheryte:
                    // Target the nearest aetheryte
                    IGameObject? aetheryte = null;
                    foreach (var obj in Svc.Objects)
                    {
                        if (obj.ObjectKind == Dalamud.Game.ClientState.Objects.Enums.ObjectKind.Aetheryte)
                        {
                            var distance = Vector3.Distance(Player.Position, obj.Position);
                            if (distance < 10f)
                            {
                                aetheryte = obj;
                                break;
                            }
                        }
                    }
                    
                    if (aetheryte != null)
                    {
                        Svc.Targets.Target = aetheryte;
                        _currentStep = AethernetAutomationStep.InteractingWithAetheryte;
                        _lastActionTime = DateTime.Now;
                        Plugin.Log.Debug("Step 1: Targeted aetheryte");
                    }
                    else if ((DateTime.Now - _lastActionTime).TotalSeconds > 5)
                    {
                        Plugin.ChatGui.PrintError("[Travel Helper] No aetheryte found nearby.");
                        CancelAethernetAutomation();
                    }
                    break;
                    
                case AethernetAutomationStep.InteractingWithAetheryte:
                    // Wait a bit after targeting before interacting
                    if ((DateTime.Now - _lastActionTime).TotalMilliseconds < 500)
                        return;
                        
                    if (Svc.Targets.Target != null && 
                        Svc.Targets.Target.ObjectKind == Dalamud.Game.ClientState.Objects.Enums.ObjectKind.Aetheryte)
                    {
                        var targetSystem = TargetSystem.Instance();
                        if (targetSystem != null)
                        {
                            targetSystem->InteractWithObject((FFXIVClientStructs.FFXIV.Client.Game.Object.GameObject*)Svc.Targets.Target.Address, false);
                            _currentStep = AethernetAutomationStep.SelectingAethernet;
                            _lastActionTime = DateTime.Now;
                            Plugin.Log.Debug("Step 2: Interacted with aetheryte");
                        }
                    }
                    break;
                    
                case AethernetAutomationStep.SelectingAethernet:
                    // Wait for menu to appear and stabilize
                    if ((DateTime.Now - _lastActionTime).TotalMilliseconds < 1000)
                        return;
                        
                    // Wait for SelectString addon
                    if (TryGetAddonByName<AtkUnitBase>("SelectString", out var selectString) && 
                        IsAddonReady(selectString))
                    {
                        // Read the menu options
                        var entries = GetSelectStringEntries(selectString);
                        Plugin.Log.Debug($"Step 3: Found {entries.Count} menu options");
                        
                        for (int i = 0; i < entries.Count; i++)
                        {
                            Plugin.Log.Debug($"SelectString[{i}]: {entries[i]}");
                            
                            // Look for "Aethernet" option
                            if (entries[i].Contains("Aethernet", StringComparison.OrdinalIgnoreCase))
                            {
                                // Add a small delay before clicking
                                if (!EzThrottler.Throttle("ClickAethernet", 500))
                                    return;
                                    
                                // Click it!
                                Callback.Fire(selectString, true, i);
                                _currentStep = AethernetAutomationStep.SelectingDestination;
                                _lastActionTime = DateTime.Now;
                                Plugin.Log.Information($"Step 3: Clicked Aethernet at index {i}");
                                return;
                            }
                        }
                        
                        // If we didn't find Aethernet, maybe we're at a different aetheryte
                        Plugin.ChatGui.PrintError("[Travel Helper] Could not find 'Aethernet' option.");
                        CancelAethernetAutomation();
                    }
                    break;
                    
                case AethernetAutomationStep.SelectingDestination:
                    // Wait for the destination menu to appear and load
                    if ((DateTime.Now - _lastActionTime).TotalMilliseconds < 1500)
                        return;
                        
                    // Wait for TelepotTown addon
                    if (TryGetAddonByName<AtkUnitBase>("TelepotTown", out var telepotTown) && 
                        IsAddonReady(telepotTown))
                    {
                        // Use the simple method that reads the actual destination items
                        var destinations = GetTelepotTownDestinationsSimple(telepotTown);
                        
                        Plugin.Log.Debug($"Step 4: Found {destinations.Count} destinations in TelepotTown");
                        
                        // Find the index of our destination
                        int destinationIndex = -1;
                        for (int i = 0; i < destinations.Count; i++)
                        {
                            Plugin.Log.Debug($"Checking destination[{i}]: '{destinations[i]}' against '{_pendingAethernetShard.Name}'" );
                            
                            if (destinations[i] == _pendingAethernetShard.Name)
                            {
                                destinationIndex = i;
                                break;
                            }
                        }
                        
                        if (destinationIndex >= 0)
                        {
                            // Add delay before clicking destination
                            if (!EzThrottler.Throttle("ClickDestination", 500))
                                return;
                            
                            // Simple approach: Just subtract 1 from the index
                            // [0] = Zone name
                            // [1] = Current location  
                            // [2] = First aethernet destination (index 1 in clickable list)
                            // It seems the clickable list includes the current location
                            int clickIndex = destinationIndex - 1;
                            
                            if (clickIndex < 0)
                            {
                                Plugin.Log.Error($"Invalid click index {clickIndex} for destination at index {destinationIndex}");
                                Plugin.ChatGui.PrintError($"[Travel Helper] Cannot teleport to {_pendingAethernetShard.Name} - not a valid aethernet destination.");
                                CancelAethernetAutomation();
                                return;
                            }
                            
                            Plugin.Log.Information($"Attempting to click '{_pendingAethernetShard.Name}' using index {clickIndex} (was at position {destinationIndex} in list)");
                            
                            try
                            {
                                // Lifestream uses the CallbackData from the destination data structure
                                // We need to read the callback data at the right offset
                                // According to ReaderTelepotTown: Data is at offset 6, step 4, max 20
                                // CallbackData is at offset 3 within each data entry
                                
                                // For now, let's use the click index as the callback data
                                // In Lifestream, they fire the callback TWICE with the same parameters
                                
                                Plugin.Log.Information($"Firing double callback for destination at index {clickIndex}");
                                
                                // Fire the callback twice as Lifestream does
                                Callback.Fire(telepotTown, true, 11, clickIndex);
                                Callback.Fire(telepotTown, true, 11, clickIndex);
                                
                                Plugin.ChatGui.Print($"[Travel Helper] Teleporting to {_pendingAethernetShard.Name}!");
                                _currentStep = AethernetAutomationStep.Complete;
                                _lastActionTime = DateTime.Now;
                            }
                            catch (Exception ex)
                            {
                                Plugin.Log.Error($"Error firing callback: {ex.Message}");
                                Plugin.ChatGui.PrintError($"[Travel Helper] Failed to teleport: {ex.Message}");
                                CancelAethernetAutomation();
                            }
                            return;
                        }
                        
                        Plugin.ChatGui.PrintError($"[Travel Helper] Could not find destination '{_pendingAethernetShard.Name}' in the list.");
                        CancelAethernetAutomation();
                    }
                    break;
                    
                case AethernetAutomationStep.Complete:
                    Plugin.ChatGui.Print("[Travel Helper] Aethernet teleport complete!");
                    CancelAethernetAutomation();
                    break;
            }
            
            // Timeout after 30 seconds
            if ((DateTime.Now - _lastActionTime).TotalSeconds > 30)
            {
                Plugin.ChatGui.PrintError("[Travel Helper] Aethernet automation timed out.");
                CancelAethernetAutomation();
            }
        }
        catch (Exception ex)
        {
            Plugin.Log.Error($"Error in aethernet automation: {ex.Message}");
            CancelAethernetAutomation();
        }
    }
    
    private List<string> GetSelectStringEntries(AtkUnitBase* addon)
    {
        var entries = new List<string>();
        if (addon == null) return entries;
        
        try
        {
            // SelectString stores its text entries starting at AtkValue index 7
            var stringArrayStart = 7;
            var stringArraySize = 16; // Maximum entries
            
            for (int i = 0; i < stringArraySize; i++)
            {
                var atkValue = addon->AtkValues[stringArrayStart + i];
                if (atkValue.Type == FFXIVClientStructs.FFXIV.Component.GUI.ValueType.String)
                {
                    var str = MemoryHelper.ReadSeStringNullTerminated(new IntPtr(atkValue.String.Value)).ToString();
                    if (!string.IsNullOrEmpty(str))
                    {
                        entries.Add(str);
                    }
                    else
                    {
                        break; // Empty string means end of list
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Plugin.Log.Error($"Error reading SelectString entries: {ex.Message}");
        }
        
        return entries;
    }
    
    private List<string> GetTelepotTownDestinationsSimple(AtkUnitBase* addon)
    {
        var destinations = new List<string>();
        if (addon == null) return destinations;
        
        try
        {
            // Based on Lifestream's ReaderTelepotTown:
            // - NumEntries at offset 0
            // - Destination names start at offset 262
            // - Each name is 1 AtkValue
            // - Maximum 20 destinations
            
            if (addon->AtkValuesCount > 0 && addon->AtkValues[0].Type == FFXIVClientStructs.FFXIV.Component.GUI.ValueType.UInt)
            {
                var numEntries = addon->AtkValues[0].UInt;
                Plugin.Log.Debug($"TelepotTown has {numEntries} entries");
                
                // Read ALL destination names starting at offset 262
                // We'll include everything and let the caller figure out the right index
                var nameOffset = 262;
                for (uint i = 0; i < Math.Min(numEntries, 20u); i++)
                {
                    var valueIndex = nameOffset + i;
                    if (valueIndex < addon->AtkValuesCount && 
                        addon->AtkValues[valueIndex].Type == FFXIVClientStructs.FFXIV.Component.GUI.ValueType.String)
                    {
                        var name = MemoryHelper.ReadSeStringNullTerminated(new IntPtr(addon->AtkValues[valueIndex].String.Value)).ToString();
                        if (!string.IsNullOrEmpty(name))
                        {
                            destinations.Add(name);
                            Plugin.Log.Debug($"Raw Destination[{i}]: {name}");
                        }
                    }
                }
            }
            
            // Fallback: scan for text if the above doesn't work
            if (destinations.Count == 0)
            {
                Plugin.Log.Debug("Offset-based reading failed, scanning all text nodes");
                
                // Scan all string values in AtkValues
                for (int i = 0; i < Math.Min((int)addon->AtkValuesCount, 300); i++)
                {
                    if (addon->AtkValues[i].Type == FFXIVClientStructs.FFXIV.Component.GUI.ValueType.String)
                    {
                        var str = MemoryHelper.ReadSeStringNullTerminated(new IntPtr(addon->AtkValues[i].String.Value)).ToString();
                        if (!string.IsNullOrEmpty(str) && str.Length > 2)
                        {
                            Plugin.Log.Debug($"AtkValue[{i}]: {str}");
                            
                            // Look for destination-like strings
                            if (!str.StartsWith("<") && 
                                !str.StartsWith("Current") &&
                                !str.StartsWith("Previous") &&
                                !str.Contains(":") &&
                                !str.Contains("None") &&
                                str.Length < 50) // Destination names shouldn't be too long
                            {
                                if (!destinations.Contains(str))
                                {
                                    destinations.Add(str);
                                }
                            }
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Plugin.Log.Error($"Error reading TelepotTown destinations: {ex.Message}");
        }
        
        Plugin.Log.Information($"Found {destinations.Count} destinations: {string.Join(", ", destinations)}");
        return destinations;
    }
    
    private void CancelAethernetAutomation()
    {
        _isAutomatingAethernet = false;
        _pendingAethernetShard = null;
        _currentStep = AethernetAutomationStep.None;
    }
    
    private AetheryteInfo? FindNearestAetheryteToCoordinates(uint territoryId, Vector2 targetCoords)
    {
        if (!_aetheryteZones.TryGetValue(territoryId, out var zone))
            return null;
            
        AetheryteInfo? nearest = null;
        float nearestDistance = float.MaxValue;
        
        // Check main aetheryte
        if (zone.MainAetheryte != null && IsAetheryteAttuned(zone.MainAetheryte.Id))
        {
            // For now, we'll consider the main aetheryte as a candidate
            // In a real implementation, you'd calculate actual distance based on map coordinates
            nearest = zone.MainAetheryte;
        }
        
        // Check aethernet shards if we're attuned to the main aetheryte
        if (zone.MainAetheryte != null && IsAetheryteAttuned(zone.MainAetheryte.Id))
        {
            foreach (var shard in zone.AethernetShards)
            {
                // In a real implementation, you'd calculate the actual distance
                // For now, we'll just return the first available shard
                // You would need map marker data to get actual positions
                
                // Prefer shards with certain keywords that suggest proximity
                if (shard.Name.Contains("Plaza") || shard.Name.Contains("Gate") || shard.Name.Contains("Aetheryte"))
                {
                    return shard;
                }
            }
            
            // If no special shard found, return the first one
            if (zone.AethernetShards.Count > 0)
            {
                return zone.AethernetShards[0];
            }
        }
        
        return nearest;
    }
    
    private void FindNearestToCoordinates()
    {
        var parts = _coordinateInput.Split(',');
        if (parts.Length == 2 && 
            float.TryParse(parts[0].Trim(), out var x) && 
            float.TryParse(parts[1].Trim(), out var y))
        {
            var currentTerritory = Plugin.ClientState.TerritoryType;
            if (_aetheryteZones.TryGetValue(currentTerritory, out var zone))
            {
                Plugin.ChatGui.Print($"[Travel Helper] Aetherytes in {zone.TerritoryName}:");
                Plugin.ChatGui.Print($"  Main: {zone.MainAetheryte.Name}");
                
                if (zone.AethernetShards.Any())
                {
                    Plugin.ChatGui.Print("  Aethernet shards:");
                    foreach (var shard in zone.AethernetShards.Take(5))
                    {
                        Plugin.ChatGui.Print($"    - {shard.Name}");
                    }
                }
            }
        }
        else
        {
            Plugin.ChatGui.PrintError("[Travel Helper] Invalid coordinates format.");
        }
    }
    
    private bool IsAetheryteAttuned(uint aetheryteId)
    {
        try
        {
            // For main aetherytes, check the aetheryte list
            foreach (var entry in Svc.AetheryteList)
            {
                if (entry.AetheryteId == aetheryteId)
                {
                    return true;
                }
            }
            
            // For aethernet shards, we need to check if we're in the right zone
            // and if the aetheryte exists in the game data
            var aetheryteSheet = Plugin.DataManager.GetExcelSheet<Aetheryte>();
            if (aetheryteSheet == null) return false;
            
            if (!aetheryteSheet.TryGetRow(aetheryteId, out var aetheryte)) return false;
            
            // If it's not a main aetheryte (it's an aethernet shard)
            if (!aetheryte.IsAetheryte)
            {
                // Check if we're in the same territory
                var currentTerritory = Plugin.ClientState.TerritoryType;
                if (aetheryte.Territory.RowId == currentTerritory)
                {
                    // If we're in the same zone as the aethernet shard,
                    // we need to check if the parent aetheryte is attuned
                    var parentGroup = aetheryte.AethernetGroup;
                    
                    // Find the main aetheryte in this group
                    foreach (var mainAetheryte in aetheryteSheet)
                    {
                        if (mainAetheryte.AethernetGroup == parentGroup && mainAetheryte.IsAetheryte)
                        {
                            // Check if we're attuned to the main aetheryte
                            foreach (var entry in Svc.AetheryteList)
                            {
                                if (entry.AetheryteId == mainAetheryte.RowId)
                                {
                                    return true; // We're attuned to the parent, so we can use the shard
                                }
                            }
                        }
                    }
                }
                return false;
            }
            
            return false;
        }
        catch (Exception ex)
        {
            Plugin.Log.Error($"Error checking attunement for aetheryte {aetheryteId}: {ex.Message}");
            return false;
        }
    }
    
    private void CreateMapLink()
    {
        var parts = _coordinateInput.Split(',');
        if (parts.Length == 2 && 
            float.TryParse(parts[0].Trim(), out var x) && 
            float.TryParse(parts[1].Trim(), out var y))
        {
            var territory = Plugin.ClientState.TerritoryType;
            var territoryName = _territoryNames.GetValueOrDefault(territory, "Unknown");
            
            // Create a map link in chat
            var mapLink = $"<flag>{territoryName} ( {x:F1}  , {y:F1} )</flag>";
            Plugin.ChatGui.Print($"[Travel Helper] Map link created for {territoryName}:");
            Plugin.ChatGui.Print(mapLink);
        }
        else
        {
            Plugin.ChatGui.PrintError("[Travel Helper] Invalid coordinates. Use format: X, Y (e.g., 12.5, 15.3)");
        }
    }
    
    protected override Dictionary<string, object>? GetConfigurationData()
    {
        return Configuration;
    }
    
    protected override void SetConfigurationData(object config)
    {
        if (config is Dictionary<string, object> dict)
        {
            Configuration = dict;
        }
    }
    
    public override void DrawConfig()
    {
        ImGui.Text("Travel Helper Settings");
        ImGui.Separator();
        
        ImGui.TextWrapped("This module helps you navigate using the complete aetheryte network, including aethernet shards.");
        
        ImGui.Spacing();
        
        ImGui.Text("Features:");
        ImGui.BulletText("Search all aetherytes and aethernet shards");
        ImGui.BulletText("View current zone's aethernet");
        ImGui.BulletText("Quick teleport to major cities");
        ImGui.BulletText("Create map links for coordinates");
        ImGui.BulletText("Semi-automated aethernet navigation");
        
        ImGui.Spacing();
        
        ImGui.Text("Tips:");
        ImGui.BulletText("Main aetherytes can be teleported to directly");
        ImGui.BulletText("Aethernet shards require interaction with a main aetheryte");
        ImGui.BulletText("The automation will help target and interact with aetherytes");
        ImGui.BulletText("You still need to manually select menu options for now");
    }
}
