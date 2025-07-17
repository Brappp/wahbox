using System;
using System.Collections.Generic;
using System.Numerics;
using Dalamud.Interface;
using Dalamud.Interface.Windowing;
using Dalamud.Interface.Components;
using ImGuiNET;
using WahBox.Core;
using WahBox.Core.Interfaces;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Game.ClientState.Objects.Enums;
using System.Linq;
using WahBox.Helpers;
using ECommons.DalamudServices;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;

namespace WahBox.Modules.Utility;

public enum ObjectCategory
{
    Unknown,
    Player,
    NPC,
    FriendlyNPC,
    Treasure,
    GatheringPoint,
    Aetheryte,
    EventObject,
    Mount,
    Companion,
    Retainer,
    HousingObject,
    AreaObject,
    CutsceneObject,
    CardStand,
    Ornament,
    IslandSanctuaryObject
}

public enum AlertFrequencyMode
{
    OnlyOnce,
    EveryInterval,
    OnEnterLeaveReenter
}

public class TrackedObject
{
    public string ObjectId { get; }
    public string Name { get; }
    public ObjectCategory Category { get; }
    public Vector3 Position { get; }
    public float Distance { get; }
    
    public TrackedObject(string objectId, string name, ObjectCategory category, Vector3 position, float distance)
    {
        ObjectId = objectId;
        Name = name;
        Category = category;
        Position = position;
        Distance = distance;
    }
}

public class RadarModule : BaseUtilityModule
{
    public override string Name => "Player Radar";
    public override ModuleType Type => ModuleType.Radar;
    
    private RadarWindow? _radarWindow;
    private readonly GameObjectTracker _objectTracker;
    private readonly Dictionary<string, DateTime> _recentlyAlertedPlayers = new();
    private Timer? _alertTimer;
    private readonly object _alertLock = new object();
    private bool _pendingAlertCheck = false;
    private DateTime _lastAlertTime = DateTime.MinValue;
    private HashSet<string> _alertedPlayerIds = new HashSet<string>();
    private HashSet<string> _playersCurrentlyInRange = new HashSet<string>();
    
    public const double ALERT_HIGHLIGHT_DURATION = 5.0;
    private const int ALERT_CHECK_INTERVAL_MS = 50;
    
    [DllImport("winmm.dll", SetLastError = true)]
    private static extern bool PlaySound(string pszSound, IntPtr hmod, uint fdwSound);
    
    private const uint SND_ASYNC = 0x0001;
    private const uint SND_FILENAME = 0x00020000;
    
    // Configuration
    public bool ShowRadarWindow { get; set; } = true;
    public float DetectionRadius { get; set; } = 50f;
    public bool RotateWithCamera { get; set; } = false;
    public bool ShowRadiusCircles { get; set; } = true;
    public bool DrawPlayerLines { get; set; } = true;
    public bool TransparentBackground { get; set; } = false;
    public bool ShowAlertRing { get; set; } = true;
    
    // Object visibility filters
    public bool ShowPlayers { get; set; } = true;
    public bool ShowNPCs { get; set; } = true;
    public bool ShowTreasure { get; set; } = true;
    public bool ShowGatheringPoints { get; set; } = true;
    public bool ShowAetherytes { get; set; } = true;
    public bool ShowEventObjects { get; set; } = false;
    public bool ShowMounts { get; set; } = false;
    public bool ShowCompanions { get; set; } = false;
    public bool ShowRetainers { get; set; } = false;
    public bool HideUnnamedObjects { get; set; } = true;
    
    // Tether/Line options
    public bool DrawNPCTethers { get; set; } = false;
    public bool DrawTreasureTethers { get; set; } = false;
    public bool DrawGatheringTethers { get; set; } = false;
    public bool DrawAetheryteTethers { get; set; } = false;
    
    // Alert settings
    public bool EnablePlayerProximityAlert { get; set; } = false;
    public float PlayerProximityAlertDistance { get; set; } = 25f;
    public float PlayerProximityAlertCooldown { get; set; } = 5.0f;
    public AlertFrequencyMode PlayerAlertFrequency { get; set; } = AlertFrequencyMode.OnEnterLeaveReenter;
    public bool EnableAlertSound { get; set; } = true;
    public int PlayerProximityAlertSound { get; set; } = 1; // 0=ping, 1=alert, 2=notification, 3=alarm
    
    // In-game overlay settings
    public bool EnableInGameDrawing { get; set; } = false;
    public bool DrawPlayerCircle { get; set; } = true;
    public bool DrawObjectDots { get; set; } = true;
    public bool DrawDistanceText { get; set; } = true;
    public float InGameDotSize { get; set; } = 3.0f;
    public float InGameLineThickness { get; set; } = 1.0f;
    
    // Colors
    public Vector4 InGamePlayerColor { get; set; } = new Vector4(0, 0, 1, 1);
    public Vector4 InGameNPCColor { get; set; } = new Vector4(1, 1, 0, 1);
    public Vector4 InGameTreasureColor { get; set; } = new Vector4(1, 0.8f, 0, 1);
    public Vector4 InGameGatheringColor { get; set; } = new Vector4(0, 1, 0, 1);
    public Vector4 InGameAetheryteColor { get; set; } = new Vector4(0.5f, 0.5f, 1, 1);
    public Vector4 InGameRadiusColor { get; set; } = new Vector4(0.5f, 0.5f, 0.5f, 0.7f);
    public Vector4 InGameTextColor { get; set; } = new Vector4(1, 1, 1, 1);

    // Window lock settings
    public bool LockRadarWindow { get; set; } = false;
    public Vector2 RadarWindowLockedPosition { get; set; } = new Vector2(100, 100);

    public IReadOnlyDictionary<string, DateTime> RecentlyAlertedPlayers => _recentlyAlertedPlayers;

    public RadarModule(Plugin plugin) : base(plugin)
    {
        IconId = 60961; // Map/Radar icon
        _objectTracker = new GameObjectTracker(this);
    }

    protected override void CreateWindow()
    {
        _radarWindow = new RadarWindow(this);
        ModuleWindow = _radarWindow;
    }

    public override void Initialize()
    {
        base.Initialize();
        StartAlertTimer();
        ApplyRadarVisibility();
    }

    public override void Load()
    {
        base.Load();
        ApplyRadarVisibility();
    }

    private void ApplyRadarVisibility()
    {
        if (ShowRadarWindow && IsEnabled)
        {
            if (ModuleWindow != null && !ModuleWindow.IsOpen)
            {
                ModuleWindow.IsOpen = true;
            }
        }
        else
        {
            if (ModuleWindow != null && ModuleWindow.IsOpen)
            {
                ModuleWindow.IsOpen = false;
            }
        }
    }

    public override void OpenWindow()
    {
        base.OpenWindow();
        ShowRadarWindow = true;
    }

    public override void CloseWindow()
    {
        base.CloseWindow();
        ShowRadarWindow = false;
    }

    public override void Update()
    {
        base.Update();
        
        // Check for pending alerts
        bool shouldCheck = false;
        lock (_alertLock)
        {
            if (_pendingAlertCheck)
            {
                shouldCheck = true;
                _pendingAlertCheck = false;
            }
        }
        
        if (shouldCheck)
        {
            try
            {
                CheckPlayerProximity();
            }
            catch (Exception ex)
            {
                Plugin.Log.Error($"Error checking player proximity: {ex.Message}");
            }
        }
        
        // Clean up old alert highlights
        var now = DateTime.Now;
        var keysToRemove = _recentlyAlertedPlayers
            .Where(kvp => (now - kvp.Value).TotalSeconds > ALERT_HIGHLIGHT_DURATION)
            .Select(kvp => kvp.Key)
            .ToList();
        
        foreach (var key in keysToRemove)
        {
            _recentlyAlertedPlayers.Remove(key);
        }
    }

    public override void Dispose()
    {
        StopAlertTimer();
        base.Dispose();
    }

    private void StartAlertTimer()
    {
        StopAlertTimer();
        
        _alertTimer = new Timer(
            _ => RequestAlertCheck(),
            null,
            0,
            ALERT_CHECK_INTERVAL_MS
        );
    }
    
    private void StopAlertTimer()
    {
        if (_alertTimer != null)
        {
            _alertTimer.Dispose();
            _alertTimer = null;
        }
    }
    
    private void RequestAlertCheck()
    {
        lock (_alertLock)
        {
            _pendingAlertCheck = true;
        }
    }

    private void CheckPlayerProximity()
    {
        if (!EnablePlayerProximityAlert || Plugin.ClientState.LocalPlayer == null)
            return;
            
        var currentTime = DateTime.Now;
        bool cooldownExpired = (currentTime - _lastAlertTime).TotalSeconds >= PlayerProximityAlertCooldown;
        
        // Skip if still in cooldown
        if (!cooldownExpired)
            return;
            
        // Process different alert frequency modes
        switch (PlayerAlertFrequency)
        {
            case AlertFrequencyMode.OnlyOnce:
                break;
                
            case AlertFrequencyMode.EveryInterval:
                _alertedPlayerIds.Clear();
                break;
                
            case AlertFrequencyMode.OnEnterLeaveReenter:
                break;
        }
        
        var trackedObjects = GetTrackedObjects();
        if (trackedObjects.Count == 0)
            return;
            
        bool anyPlayerInRange = false;
        List<string> playersInRange = new List<string>();
        HashSet<string> currentlyInRange = new HashSet<string>();
        
        foreach (var obj in trackedObjects)
        {
            if (obj.Category != ObjectCategory.Player)
                continue;
                
            if (obj.Distance <= PlayerProximityAlertDistance)
            {
                currentlyInRange.Add(obj.ObjectId);
                
                if (PlayerAlertFrequency == AlertFrequencyMode.OnEnterLeaveReenter)
                {
                    if (!_playersCurrentlyInRange.Contains(obj.ObjectId) || 
                        (!_playersCurrentlyInRange.Contains(obj.ObjectId) && !_alertedPlayerIds.Contains(obj.ObjectId)))
                    {
                        anyPlayerInRange = true;
                        playersInRange.Add($"{obj.Name} ({obj.Distance:F1} yalms)");
                        _alertedPlayerIds.Add(obj.ObjectId);
                    }
                }
                else
                {
                    if (!_alertedPlayerIds.Contains(obj.ObjectId))
                    {
                        anyPlayerInRange = true;
                        playersInRange.Add($"{obj.Name} ({obj.Distance:F1} yalms)");
                        _alertedPlayerIds.Add(obj.ObjectId);
                    }
                }
            }
        }
        
        // Handle player tracking for enter/leave/reenter mode
        if (PlayerAlertFrequency == AlertFrequencyMode.OnEnterLeaveReenter)
        {
            foreach (var playerId in _playersCurrentlyInRange)
            {
                if (!currentlyInRange.Contains(playerId))
                {
                    // Player left range, remove from alerts to enable redetection
                    _alertedPlayerIds.Remove(playerId);
                }
            }
            
            // Update tracking list for next check
            _playersCurrentlyInRange = currentlyInRange;
        }
        
        // Play alert if any players triggered
        if (anyPlayerInRange)
        {
            if (EnableAlertSound)
            {
                PlayAlertSound(PlayerProximityAlertSound);
            }
            _lastAlertTime = currentTime;
            
            if (playersInRange.Count > 0)
            {
                Plugin.Log.Debug($"Alert triggered by: {string.Join(", ", playersInRange)}");
                Plugin.ChatGui.Print($"[Radar] Player nearby: {string.Join(", ", playersInRange)}");
                
                // Add triggering players to highlighted list
                foreach (var obj in trackedObjects)
                {
                    if (obj.Category == ObjectCategory.Player && 
                        obj.Distance <= PlayerProximityAlertDistance &&
                        playersInRange.Any(p => p.StartsWith(obj.Name)))
                    {
                        _recentlyAlertedPlayers[obj.ObjectId] = currentTime;
                    }
                }
            }
        }
    }

    public void PlayAlertSound(int soundId)
    {
        try
        {
            string soundFilePath;
            string soundFileName;
            
            switch (soundId)
            {
                case 0:
                    soundFileName = "ping.wav";
                    break;
                case 1:
                    soundFileName = "alert.wav";
                    break;
                case 2:
                    soundFileName = "notification.wav";
                    break;
                case 3:
                    soundFileName = "alarm.wav";
                    break;
                default:
                    soundFileName = "ping.wav";
                    break;
            }
            
            // Try to find the sound file in the Data/sounds directory
            soundFilePath = Path.Combine(Plugin.PluginInterface.AssemblyLocation.DirectoryName!, "Data", "sounds", soundFileName);
            
            if (!File.Exists(soundFilePath))
            {
                Plugin.Log.Error($"Could not find sound file: {soundFileName} at {soundFilePath}");
                return;
            }
            
            PlaySound(soundFilePath, IntPtr.Zero, SND_FILENAME | SND_ASYNC);
        }
        catch (Exception ex)
        {
            Plugin.Log.Error($"Error playing alert sound: {ex.Message}");
        }
    }

    public void ClearAlertData()
    {
        lock (_alertLock)
        {
            _alertedPlayerIds.Clear();
            _playersCurrentlyInRange.Clear();
            _recentlyAlertedPlayers.Clear();
            Plugin.Log.Debug("Alert tracking data has been cleared due to frequency mode change");
        }
    }

    protected override Dictionary<string, object> GetConfigurationData()
    {
        return new Dictionary<string, object>
        {
            // Basic settings
            ["ShowRadarWindow"] = ShowRadarWindow,
            ["DetectionRadius"] = DetectionRadius,
            ["RotateWithCamera"] = RotateWithCamera,
            ["ShowRadiusCircles"] = ShowRadiusCircles,
            ["DrawPlayerLines"] = DrawPlayerLines,
            ["TransparentBackground"] = TransparentBackground,
            ["ShowAlertRing"] = ShowAlertRing,
            
            // Object visibility filters
            ["ShowPlayers"] = ShowPlayers,
            ["ShowNPCs"] = ShowNPCs,
            ["ShowTreasure"] = ShowTreasure,
            ["ShowGatheringPoints"] = ShowGatheringPoints,
            ["ShowAetherytes"] = ShowAetherytes,
            ["ShowEventObjects"] = ShowEventObjects,
            ["ShowMounts"] = ShowMounts,
            ["ShowCompanions"] = ShowCompanions,
            ["ShowRetainers"] = ShowRetainers,
            ["HideUnnamedObjects"] = HideUnnamedObjects,
            
            // Tether/Line options
            ["DrawNPCTethers"] = DrawNPCTethers,
            ["DrawTreasureTethers"] = DrawTreasureTethers,
            ["DrawGatheringTethers"] = DrawGatheringTethers,
            ["DrawAetheryteTethers"] = DrawAetheryteTethers,
            
            // Alert settings
            ["EnablePlayerProximityAlert"] = EnablePlayerProximityAlert,
            ["PlayerProximityAlertDistance"] = PlayerProximityAlertDistance,
            ["PlayerProximityAlertCooldown"] = PlayerProximityAlertCooldown,
            ["PlayerAlertFrequency"] = (int)PlayerAlertFrequency,
            ["EnableAlertSound"] = EnableAlertSound,
            ["PlayerProximityAlertSound"] = PlayerProximityAlertSound,
            
            // In-game overlay settings
            ["EnableInGameDrawing"] = EnableInGameDrawing,
            ["DrawPlayerCircle"] = DrawPlayerCircle,
            ["DrawObjectDots"] = DrawObjectDots,
            ["DrawDistanceText"] = DrawDistanceText,
            ["InGameDotSize"] = InGameDotSize,
            ["InGameLineThickness"] = InGameLineThickness,
            
            // Colors
            ["InGamePlayerColor"] = new { InGamePlayerColor.X, InGamePlayerColor.Y, InGamePlayerColor.Z, InGamePlayerColor.W },
            ["InGameNPCColor"] = new { InGameNPCColor.X, InGameNPCColor.Y, InGameNPCColor.Z, InGameNPCColor.W },
            ["InGameTreasureColor"] = new { InGameTreasureColor.X, InGameTreasureColor.Y, InGameTreasureColor.Z, InGameTreasureColor.W },
            ["InGameGatheringColor"] = new { InGameGatheringColor.X, InGameGatheringColor.Y, InGameGatheringColor.Z, InGameGatheringColor.W },
            ["InGameAetheryteColor"] = new { InGameAetheryteColor.X, InGameAetheryteColor.Y, InGameAetheryteColor.Z, InGameAetheryteColor.W },
            ["InGameRadiusColor"] = new { InGameRadiusColor.X, InGameRadiusColor.Y, InGameRadiusColor.Z, InGameRadiusColor.W },
            ["InGameTextColor"] = new { InGameTextColor.X, InGameTextColor.Y, InGameTextColor.Z, InGameTextColor.W },
            
            // Window lock settings
            ["LockRadarWindow"] = LockRadarWindow,
            ["RadarWindowLockedPosition"] = new { RadarWindowLockedPosition.X, RadarWindowLockedPosition.Y }
        };
    }

    protected override void SetConfigurationData(object config)
    {
        if (config is not Dictionary<string, object> configDict) return;

        try
        {
            // Basic settings
            if (configDict.TryGetValue("ShowRadarWindow", out var showRadarWindow))
                ShowRadarWindow = Convert.ToBoolean(showRadarWindow);
            if (configDict.TryGetValue("DetectionRadius", out var detectionRadius))
                DetectionRadius = Convert.ToSingle(detectionRadius);
            if (configDict.TryGetValue("RotateWithCamera", out var rotateWithCamera))
                RotateWithCamera = Convert.ToBoolean(rotateWithCamera);
            if (configDict.TryGetValue("ShowRadiusCircles", out var showRadiusCircles))
                ShowRadiusCircles = Convert.ToBoolean(showRadiusCircles);
            if (configDict.TryGetValue("DrawPlayerLines", out var drawPlayerLines))
                DrawPlayerLines = Convert.ToBoolean(drawPlayerLines);
            if (configDict.TryGetValue("TransparentBackground", out var transparentBackground))
                TransparentBackground = Convert.ToBoolean(transparentBackground);
            if (configDict.TryGetValue("ShowAlertRing", out var showAlertRing))
                ShowAlertRing = Convert.ToBoolean(showAlertRing);
            
            // Object visibility filters
            if (configDict.TryGetValue("ShowPlayers", out var showPlayers))
                ShowPlayers = Convert.ToBoolean(showPlayers);
            if (configDict.TryGetValue("ShowNPCs", out var showNPCs))
                ShowNPCs = Convert.ToBoolean(showNPCs);
            if (configDict.TryGetValue("ShowTreasure", out var showTreasure))
                ShowTreasure = Convert.ToBoolean(showTreasure);
            if (configDict.TryGetValue("ShowGatheringPoints", out var showGatheringPoints))
                ShowGatheringPoints = Convert.ToBoolean(showGatheringPoints);
            if (configDict.TryGetValue("ShowAetherytes", out var showAetherytes))
                ShowAetherytes = Convert.ToBoolean(showAetherytes);
            if (configDict.TryGetValue("ShowEventObjects", out var showEventObjects))
                ShowEventObjects = Convert.ToBoolean(showEventObjects);
            if (configDict.TryGetValue("ShowMounts", out var showMounts))
                ShowMounts = Convert.ToBoolean(showMounts);
            if (configDict.TryGetValue("ShowCompanions", out var showCompanions))
                ShowCompanions = Convert.ToBoolean(showCompanions);
            if (configDict.TryGetValue("ShowRetainers", out var showRetainers))
                ShowRetainers = Convert.ToBoolean(showRetainers);
            if (configDict.TryGetValue("HideUnnamedObjects", out var hideUnnamedObjects))
                HideUnnamedObjects = Convert.ToBoolean(hideUnnamedObjects);
            
            // Tether/Line options
            if (configDict.TryGetValue("DrawNPCTethers", out var drawNPCTethers))
                DrawNPCTethers = Convert.ToBoolean(drawNPCTethers);
            if (configDict.TryGetValue("DrawTreasureTethers", out var drawTreasureTethers))
                DrawTreasureTethers = Convert.ToBoolean(drawTreasureTethers);
            if (configDict.TryGetValue("DrawGatheringTethers", out var drawGatheringTethers))
                DrawGatheringTethers = Convert.ToBoolean(drawGatheringTethers);
            if (configDict.TryGetValue("DrawAetheryteTethers", out var drawAetheryteTethers))
                DrawAetheryteTethers = Convert.ToBoolean(drawAetheryteTethers);
            
            // Alert settings
            if (configDict.TryGetValue("EnablePlayerProximityAlert", out var enablePlayerProximityAlert))
                EnablePlayerProximityAlert = Convert.ToBoolean(enablePlayerProximityAlert);
            if (configDict.TryGetValue("PlayerProximityAlertDistance", out var playerProximityAlertDistance))
                PlayerProximityAlertDistance = Convert.ToSingle(playerProximityAlertDistance);
            if (configDict.TryGetValue("PlayerProximityAlertCooldown", out var playerProximityAlertCooldown))
                PlayerProximityAlertCooldown = Convert.ToSingle(playerProximityAlertCooldown);
            if (configDict.TryGetValue("PlayerAlertFrequency", out var playerAlertFrequency))
                PlayerAlertFrequency = (AlertFrequencyMode)Convert.ToInt32(playerAlertFrequency);
            if (configDict.TryGetValue("EnableAlertSound", out var enableAlertSound))
                EnableAlertSound = Convert.ToBoolean(enableAlertSound);
            if (configDict.TryGetValue("PlayerProximityAlertSound", out var playerProximityAlertSound))
                PlayerProximityAlertSound = Convert.ToInt32(playerProximityAlertSound);
            
            // In-game overlay settings
            if (configDict.TryGetValue("EnableInGameDrawing", out var enableInGameDrawing))
                EnableInGameDrawing = Convert.ToBoolean(enableInGameDrawing);
            if (configDict.TryGetValue("DrawPlayerCircle", out var drawPlayerCircle))
                DrawPlayerCircle = Convert.ToBoolean(drawPlayerCircle);
            if (configDict.TryGetValue("DrawObjectDots", out var drawObjectDots))
                DrawObjectDots = Convert.ToBoolean(drawObjectDots);
            if (configDict.TryGetValue("DrawDistanceText", out var drawDistanceText))
                DrawDistanceText = Convert.ToBoolean(drawDistanceText);
            if (configDict.TryGetValue("InGameDotSize", out var inGameDotSize))
                InGameDotSize = Convert.ToSingle(inGameDotSize);
            if (configDict.TryGetValue("InGameLineThickness", out var inGameLineThickness))
                InGameLineThickness = Convert.ToSingle(inGameLineThickness);
            
            // Colors (simplified - just load as default if missing)
            if (configDict.TryGetValue("LockRadarWindow", out var lockRadarWindow))
                LockRadarWindow = Convert.ToBoolean(lockRadarWindow);
            
            // Apply configuration changes that require special handling
            // Note: ApplyRadarVisibility will be called in Initialize() after window creation
        }
        catch (Exception ex)
        {
            Plugin.Log.Warning(ex, "Failed to load some radar configuration values, using defaults");
        }
    }

    public List<TrackedObject> GetTrackedObjects()
    {
        return _objectTracker.GetTrackedObjects();
    }

    private void SaveConfigurationWithAction()
    {
        SaveConfiguration();
    }

    public override void DrawConfig()
    {
        ImGui.Text("Radar Configuration");
        ImGui.Separator();
        
        // Basic settings
        if (ImGui.CollapsingHeader("Basic Settings"))
        {
            bool showRadarWindow = ShowRadarWindow;
            if (ImGui.Checkbox("Show Radar Window", ref showRadarWindow))
            {
                ShowRadarWindow = showRadarWindow;
                ApplyRadarVisibility();
                SaveConfiguration();
            }
            
            float detectionRadius = DetectionRadius;
            if (ImGui.SliderFloat("Detection Radius", ref detectionRadius, 10f, 100f, "%.0f yalms"))
            {
                DetectionRadius = detectionRadius;
                SaveConfiguration();
            }
            
            bool rotateWithCamera = RotateWithCamera;
            if (ImGui.Checkbox("Rotate with Camera", ref rotateWithCamera))
            {
                RotateWithCamera = rotateWithCamera;
                SaveConfiguration();
            }
            
            bool showRadiusCircles = ShowRadiusCircles;
            if (ImGui.Checkbox("Show Radius Circles", ref showRadiusCircles))
            {
                ShowRadiusCircles = showRadiusCircles;
                SaveConfiguration();
            }
            
            bool drawPlayerLines = DrawPlayerLines;
            if (ImGui.Checkbox("Draw Player Lines", ref drawPlayerLines))
            {
                DrawPlayerLines = drawPlayerLines;
                SaveConfiguration();
            }
            
            bool transparentBackground = TransparentBackground;
            if (ImGui.Checkbox("Transparent Background", ref transparentBackground))
            {
                TransparentBackground = transparentBackground;
                SaveConfiguration();
            }
            
            bool lockRadarWindow = LockRadarWindow;
            if (ImGui.Checkbox("Lock Window Position", ref lockRadarWindow))
            {
                LockRadarWindow = lockRadarWindow;
                SaveConfiguration();
            }
        }
        
        // Object filters
        if (ImGui.CollapsingHeader("Object Filters"))
        {
            bool showPlayers = ShowPlayers;
            if (ImGui.Checkbox("Show Players", ref showPlayers))
            {
                ShowPlayers = showPlayers;
                SaveConfiguration();
            }
            
            bool showNPCs = ShowNPCs;
            if (ImGui.Checkbox("Show NPCs", ref showNPCs))
            {
                ShowNPCs = showNPCs;
                SaveConfiguration();
            }
            
            bool showTreasure = ShowTreasure;
            if (ImGui.Checkbox("Show Treasure", ref showTreasure))
            {
                ShowTreasure = showTreasure;
                SaveConfiguration();
            }
            
            bool showGatheringPoints = ShowGatheringPoints;
            if (ImGui.Checkbox("Show Gathering Points", ref showGatheringPoints))
            {
                ShowGatheringPoints = showGatheringPoints;
                SaveConfiguration();
            }
            
            bool showAetherytes = ShowAetherytes;
            if (ImGui.Checkbox("Show Aetherytes", ref showAetherytes))
            {
                ShowAetherytes = showAetherytes;
                SaveConfiguration();
            }
            
            bool showEventObjects = ShowEventObjects;
            if (ImGui.Checkbox("Show Event Objects", ref showEventObjects))
            {
                ShowEventObjects = showEventObjects;
                SaveConfiguration();
            }
            
            bool hideUnnamedObjects = HideUnnamedObjects;
            if (ImGui.Checkbox("Hide Unnamed Objects", ref hideUnnamedObjects))
            {
                HideUnnamedObjects = hideUnnamedObjects;
                SaveConfiguration();
            }
        }
        
        // Tether settings
        if (ImGui.CollapsingHeader("Tether/Line Settings"))
        {
            bool drawNPCTethers = DrawNPCTethers;
            if (ImGui.Checkbox("Draw NPC Tethers", ref drawNPCTethers))
            {
                DrawNPCTethers = drawNPCTethers;
                SaveConfiguration();
            }
            
            bool drawTreasureTethers = DrawTreasureTethers;
            if (ImGui.Checkbox("Draw Treasure Tethers", ref drawTreasureTethers))
            {
                DrawTreasureTethers = drawTreasureTethers;
                SaveConfiguration();
            }
            
            bool drawGatheringTethers = DrawGatheringTethers;
            if (ImGui.Checkbox("Draw Gathering Tethers", ref drawGatheringTethers))
            {
                DrawGatheringTethers = drawGatheringTethers;
                SaveConfiguration();
            }
            
            bool drawAetheryteTethers = DrawAetheryteTethers;
            if (ImGui.Checkbox("Draw Aetheryte Tethers", ref drawAetheryteTethers))
            {
                DrawAetheryteTethers = drawAetheryteTethers;
                SaveConfiguration();
            }
        }
        
        // Alert settings
        if (ImGui.CollapsingHeader("Alert Settings"))
        {
            bool enablePlayerProximityAlert = EnablePlayerProximityAlert;
            if (ImGui.Checkbox("Enable Player Proximity Alert", ref enablePlayerProximityAlert))
            {
                EnablePlayerProximityAlert = enablePlayerProximityAlert;
                SaveConfiguration();
            }
            
            if (EnablePlayerProximityAlert)
            {
                float alertDistance = PlayerProximityAlertDistance;
                if (ImGui.SliderFloat("Alert Distance", ref alertDistance, 5f, 50f, "%.0f yalms"))
                {
                    PlayerProximityAlertDistance = alertDistance;
                    SaveConfiguration();
                }
                
                bool showAlertRing = ShowAlertRing;
                if (ImGui.Checkbox("Show Alert Ring", ref showAlertRing))
                {
                    ShowAlertRing = showAlertRing;
                    SaveConfiguration();
                }

                float alertCooldown = PlayerProximityAlertCooldown;
                if (ImGui.SliderFloat("Alert Cooldown (seconds)", ref alertCooldown, 0.1f, 10f, "%.1f"))
                {
                    PlayerProximityAlertCooldown = alertCooldown;
                    SaveConfiguration();
                }

                int alertFrequency = (int)PlayerAlertFrequency;
                if (ImGui.Combo("Alert Frequency", ref alertFrequency, new[] { "Only Once", "Every Interval", "On Enter/Leave/Reenter" }, 3))
                {
                    PlayerAlertFrequency = (AlertFrequencyMode)alertFrequency;
                    ClearAlertData(); // Clear alerts when frequency changes
                    SaveConfiguration();
                }

                bool enableAlertSound = EnableAlertSound;
                if (ImGui.Checkbox("Enable Alert Sound", ref enableAlertSound))
                {
                    EnableAlertSound = enableAlertSound;
                    SaveConfiguration();
                }

                if (EnableAlertSound)
                {
                    int alertSound = PlayerProximityAlertSound;
                    if (ImGui.Combo("Alert Sound", ref alertSound, new[] { "Ping", "Alert", "Notification", "Alarm" }, 4))
                    {
                        PlayerProximityAlertSound = alertSound;
                        SaveConfiguration();
                    }
                }
            }
        }
        
        // In-game overlay
        if (ImGui.CollapsingHeader("In-Game Overlay"))
        {
            bool enableInGameDrawing = EnableInGameDrawing;
            if (ImGui.Checkbox("Enable In-Game Drawing", ref enableInGameDrawing))
            {
                EnableInGameDrawing = enableInGameDrawing;
                SaveConfiguration();
            }
            
            if (EnableInGameDrawing)
            {
                bool drawPlayerCircle = DrawPlayerCircle;
                if (ImGui.Checkbox("Draw Detection Circle", ref drawPlayerCircle))
                {
                    DrawPlayerCircle = drawPlayerCircle;
                    SaveConfiguration();
                }
                
                bool drawObjectDots = DrawObjectDots;
                if (ImGui.Checkbox("Draw Object Dots", ref drawObjectDots))
                {
                    DrawObjectDots = drawObjectDots;
                    SaveConfiguration();
                }
                
                bool drawDistanceText = DrawDistanceText;
                if (ImGui.Checkbox("Draw Distance Text", ref drawDistanceText))
                {
                    DrawDistanceText = drawDistanceText;
                    SaveConfiguration();
                }
                
                float dotSize = InGameDotSize;
                if (ImGui.SliderFloat("Dot Size", ref dotSize, 1f, 10f, "%.1f"))
                {
                    InGameDotSize = dotSize;
                    SaveConfiguration();
                }
                
                float lineThickness = InGameLineThickness;
                if (ImGui.SliderFloat("Line Thickness", ref lineThickness, 0.5f, 5f, "%.1f"))
                {
                    InGameLineThickness = lineThickness;
                    SaveConfiguration();
                }
            }
        }
    }

    public override void DrawStatus()
    {
        if (Status == ModuleStatus.Active)
        {
            ImGui.TextColored(new Vector4(0.2f, 0.8f, 0.2f, 1), "Radar Active");
            var trackedCount = GetTrackedObjects().Count;
            ImGui.Text($"Tracking: {trackedCount} entities");
            ImGui.Text($"Range: {DetectionRadius:F0}y");
        }
        else
        {
            ImGui.TextColored(new Vector4(0.5f, 0.5f, 0.5f, 1), "Radar Inactive");
        }
    }
    
    public void DrawInGameOverlay()
    {
        var player = Plugin.ClientState.LocalPlayer;
        if (player == null || !EnableInGameDrawing)
            return;
            
        // Begin overlay drawing - if this fails, don't attempt to draw anything
        if (!GameDrawing.BeginOverlayDrawing())
            return;
            
        try
        {
            var trackedObjects = GetTrackedObjects();
            
            // Draw detection radius circle if enabled
            if (DrawPlayerCircle)
            {
                GameDrawing.DrawCircle(
                    player.Position,
                    DetectionRadius,
                    InGameRadiusColor,
                    InGameLineThickness);
            }
            
            // Draw objects
            foreach (var obj in trackedObjects)
            {
                var color = GetInGameObjectColor(obj.Category);
                
                // Draw object dot
                if (DrawObjectDots)
                {
                    GameDrawing.DrawDot(
                        obj.Position, 
                        InGameDotSize, 
                        color);
                }
                
                // Draw tethers/lines based on object type and configuration
                bool shouldDrawTether = ShouldDrawInGameTether(obj.Category);
                
                if (shouldDrawTether)
                {
                    var tetherColor = obj.Category == ObjectCategory.Player ? InGamePlayerColor : color;
                    
                    GameDrawing.DrawLine(
                        player.Position,
                        obj.Position,
                        InGameLineThickness,
                        tetherColor);
                        
                    // Draw distance text if enabled
                    if (DrawDistanceText)
                    {
                        // Calculate midpoint for text
                        Vector3 midpoint = new Vector3(
                            (player.Position.X + obj.Position.X) / 2,
                            (player.Position.Y + obj.Position.Y) / 2,
                            (player.Position.Z + obj.Position.Z) / 2
                        );
                        
                        string distanceText = $"{obj.Distance:F1}";
                        GameDrawing.DrawText(
                            midpoint,
                            distanceText,
                            InGameTextColor);
                    }
                }
            }
        }
        finally
        {
            // Always end overlay drawing, even if an exception occurs
            GameDrawing.EndOverlayDrawing();
        }
    }
    
    private Vector4 GetInGameObjectColor(ObjectCategory category)
    {
        return category switch
        {
            ObjectCategory.Player => InGamePlayerColor,
            ObjectCategory.NPC or ObjectCategory.FriendlyNPC => InGameNPCColor,
            ObjectCategory.Treasure => InGameTreasureColor,
            ObjectCategory.GatheringPoint => InGameGatheringColor,
            ObjectCategory.Aetheryte => InGameAetheryteColor,
            _ => new Vector4(0.7f, 0.7f, 0.7f, 1)
        };
    }
    
    private bool ShouldDrawInGameTether(ObjectCategory category)
    {
        return category switch
        {
            ObjectCategory.Player => DrawPlayerLines,
            ObjectCategory.NPC or ObjectCategory.FriendlyNPC => DrawNPCTethers,
            ObjectCategory.Treasure => DrawTreasureTethers,
            ObjectCategory.GatheringPoint => DrawGatheringTethers,
            ObjectCategory.Aetheryte => DrawAetheryteTethers,
            _ => false
        };
    }
}

public class GameObjectTracker
{
    private readonly RadarModule _module;
    
    public GameObjectTracker(RadarModule module)
    {
        _module = module;
    }
    
    public List<TrackedObject> GetTrackedObjects()
    {
        var player = Plugin.ClientState.LocalPlayer;
        if (player == null)
            return new List<TrackedObject>();
            
        var result = new List<TrackedObject>();
        
        // Use ECommons service locator for ObjectTable
        var objects = Svc.Objects;
        if (objects == null)
            return result;
            
        foreach (var obj in objects)
        {
            if (obj == null)
                continue;
                
            if (obj.GameObjectId == player.GameObjectId)
                continue;
                
            var distance = Vector3.Distance(player.Position, obj.Position);
            if (distance > _module.DetectionRadius)
                continue;
                
            var category = GetCategory(obj);
            if (!ShouldDisplay(category))
                continue;
                
            if (_module.HideUnnamedObjects && string.IsNullOrWhiteSpace(obj.Name.TextValue))
                continue;
                
            result.Add(new TrackedObject(
                obj.GameObjectId.ToString(),
                obj.Name.TextValue,
                category,
                obj.Position,
                distance
            ));
        }
        
        return result;
    }
    
    private ObjectCategory GetCategory(IGameObject obj)
    {
        switch (obj.ObjectKind)
        {
            case ObjectKind.Player:
                return ObjectCategory.Player;
                
            case ObjectKind.BattleNpc:
                return ObjectCategory.NPC;
                
            case ObjectKind.EventNpc:
                return ObjectCategory.FriendlyNPC;
                
            case ObjectKind.Treasure:
                return ObjectCategory.Treasure;
                
            case ObjectKind.GatheringPoint:
                return ObjectCategory.GatheringPoint;
                
            case ObjectKind.Aetheryte:
                return ObjectCategory.Aetheryte;
                
            case ObjectKind.EventObj:
                return ObjectCategory.EventObject;
                
            case ObjectKind.MountType:
                return ObjectCategory.Mount;
                
            case ObjectKind.Companion:
                return ObjectCategory.Companion;
                
            case ObjectKind.Retainer:
                return ObjectCategory.Retainer;
                
            case ObjectKind.Housing:
                return ObjectCategory.HousingObject;
                
            case ObjectKind.Area:
                return ObjectCategory.AreaObject;
                
            case ObjectKind.Cutscene:
                return ObjectCategory.CutsceneObject;
                
            case ObjectKind.CardStand:
                return ObjectCategory.CardStand;
                
            case ObjectKind.Ornament:
                return ObjectCategory.Ornament;
                
            default:
                if ((byte)obj.ObjectKind == 14)
                    return ObjectCategory.IslandSanctuaryObject;
                
                return ObjectCategory.Unknown;
        }
    }
    
    private bool ShouldDisplay(ObjectCategory category)
    {
        return category switch
        {
            ObjectCategory.Player => _module.ShowPlayers,
            ObjectCategory.NPC or ObjectCategory.FriendlyNPC => _module.ShowNPCs,
            ObjectCategory.Treasure => _module.ShowTreasure,
            ObjectCategory.GatheringPoint => _module.ShowGatheringPoints,
            ObjectCategory.Aetheryte => _module.ShowAetherytes,
            ObjectCategory.EventObject => _module.ShowEventObjects,
            ObjectCategory.Mount => _module.ShowMounts,
            ObjectCategory.Companion => _module.ShowCompanions,
            ObjectCategory.Retainer => _module.ShowRetainers,
            _ => false
        };
    }
}

public class RadarWindow : Window
{
    private RadarModule _module;
    private float _radarRange = 50f;
    private Vector2 _radarCenter;
    
    private Vector4 RadiusCircleColor = new Vector4(0.5f, 0.5f, 0.5f, 0.7f);
    private Vector4 PlayerLineColor = new Vector4(0.0f, 0.5f, 1.0f, 0.7f);
    private Vector4 AlertRingColor = new Vector4(1.0f, 0.1f, 0.1f, 0.8f);
    private Vector4 AlertedPlayerHighlight = new Vector4(1.0f, 0.3f, 0.3f, 0.9f);

    public RadarWindow(RadarModule module) : base("Player Radar##RadarWindow")
    {
        _module = module;
        
        Size = new Vector2(300, 300);
        SizeCondition = ImGuiCond.FirstUseEver;
        
        Flags = ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse | ImGuiWindowFlags.NoCollapse;
    }

    public override void PreDraw()
    {
        // Handle window locking
        if (_module.LockRadarWindow)
        {
            Flags |= ImGuiWindowFlags.NoMove;
            ImGui.SetNextWindowPos(_module.RadarWindowLockedPosition, ImGuiCond.Always);
        }
        else
        {
            Flags &= ~ImGuiWindowFlags.NoMove;
        }

        if (_module.TransparentBackground)
        {
            Flags |= ImGuiWindowFlags.NoBackground;
            Flags |= ImGuiWindowFlags.NoTitleBar;
            Flags &= ~ImGuiWindowFlags.AlwaysAutoResize;
        }
        else
        {
            Flags &= ~ImGuiWindowFlags.NoBackground;
            Flags &= ~ImGuiWindowFlags.NoTitleBar;
        }
    }

    public override void Draw()
    {
        // Save current position when not locked (for future locking)
        if (!_module.LockRadarWindow)
        {
            _module.RadarWindowLockedPosition = ImGui.GetWindowPos();
        }
        
        var player = Plugin.ClientState.LocalPlayer;
        if (player == null)
        {
            ImGui.TextUnformatted("Player not available");
            return;
        }
        
        float cameraRotation = 0f;
        if (_module.RotateWithCamera)
        {
            try
            {
                // Get camera rotation from view matrix
                unsafe
                {
                    var controlCamera = FFXIVClientStructs.FFXIV.Client.Game.Control.CameraManager.Instance()->GetActiveCamera();
                    if (controlCamera != null)
                    {
                        var renderCamera = controlCamera->SceneCamera.RenderCamera;
                        if (renderCamera != null)
                        {
                            var viewMatrix = renderCamera->ViewMatrix;
                            cameraRotation = MathF.Atan2(viewMatrix.M13, viewMatrix.M33);
                        }
                        else
                        {
                            cameraRotation = player.Rotation;
                        }
                    }
                    else
                    {
                        cameraRotation = player.Rotation;
                    }
                }
            }
            catch
            {
                cameraRotation = player.Rotation;
            }
        }
        
        var drawList = ImGui.GetWindowDrawList();
        var windowSize = ImGui.GetWindowSize();
        var contentRegion = ImGui.GetContentRegionAvail();
        
        // Add control buttons
        DrawControlButtons();
        
        // Account for button height
        float buttonHeight = 30f;
        var radarSize = Math.Min(contentRegion.X, contentRegion.Y - buttonHeight);
        
        // Add padding to center the radar
        float centerPadX = (contentRegion.X - radarSize) * 0.5f;
        if (centerPadX > 0)
            ImGui.Indent(centerPadX);
        
        var center = ImGui.GetCursorScreenPos() + new Vector2(radarSize / 2, radarSize / 2);
        
        // Draw radar background
        drawList.AddCircleFilled(center, radarSize / 2, ImGui.ColorConvertFloat4ToU32(new Vector4(0.1f, 0.1f, 0.1f, 0.7f)));
        drawList.AddCircle(center, radarSize / 2, ImGui.ColorConvertFloat4ToU32(new Vector4(0.5f, 0.5f, 0.5f, 1.0f)));
        
        // Draw radius indicator circles if enabled
        if (_module.ShowRadiusCircles)
        {
            DrawRadiusCircles(drawList, center, radarSize, cameraRotation);
        }
        
        // Draw alert distance ring if enabled
        if (_module.ShowAlertRing && _module.EnablePlayerProximityAlert)
        {
            DrawAlertRing(drawList, center, radarSize, cameraRotation);
        }
        
        // Draw player at center
        float playerDotSize = 6.0f;
        drawList.AddCircleFilled(center, playerDotSize, ImGui.ColorConvertFloat4ToU32(new Vector4(0, 1, 0, 1)));
        
        // Draw direction indicators
        DrawDirectionIndicators(drawList, center, radarSize, cameraRotation);
        
        // Draw tracked objects
        DrawTrackedObjects(drawList, center, radarSize, cameraRotation, player);
        
        // Leave space for the radar
        ImGui.Dummy(new Vector2(radarSize, radarSize));
        
        // Reset indent if we added any
        if (centerPadX > 0)
            ImGui.Unindent(centerPadX);
    }
    
    private void DrawControlButtons()
    {
        var lockIcon = _module.LockRadarWindow ? FontAwesomeIcon.Lock : FontAwesomeIcon.LockOpen;
        var settingsIcon = FontAwesomeIcon.Cog;
        var lockSize = new Vector2(20, 20);
        var settingsSize = new Vector2(20, 20);
        float lockWidth = lockSize.X + ImGui.GetStyle().FramePadding.X * 2;
        float settingsWidth = settingsSize.X + ImGui.GetStyle().FramePadding.X * 2;
        float totalWidth = settingsWidth + lockWidth + ImGui.GetStyle().ItemSpacing.X;
        float windowCenter = ImGui.GetWindowWidth() * 0.5f;
        
        ImGui.SetCursorPosX(windowCenter - totalWidth * 0.5f);
        
        float buttonAlpha = _module.TransparentBackground ? 0.7f : 1.0f;
        
        // Lock button
        if (_module.LockRadarWindow)
        {
            ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(0.8f, 0.2f, 0.2f, buttonAlpha));
            ImGui.PushStyleColor(ImGuiCol.ButtonHovered, new Vector4(1.0f, 0.3f, 0.3f, buttonAlpha));
            ImGui.PushStyleColor(ImGuiCol.ButtonActive, new Vector4(0.6f, 0.1f, 0.1f, buttonAlpha));
        }
        else
        {
            ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(0.2f, 0.6f, 0.2f, buttonAlpha));
            ImGui.PushStyleColor(ImGuiCol.ButtonHovered, new Vector4(0.3f, 0.8f, 0.3f, buttonAlpha));
            ImGui.PushStyleColor(ImGuiCol.ButtonActive, new Vector4(0.1f, 0.4f, 0.1f, buttonAlpha));
        }
        
        if (ImGuiComponents.IconButton(lockIcon))
        {
            _module.LockRadarWindow = !_module.LockRadarWindow;
        }
        
        ImGui.PopStyleColor(3);
        
        if (ImGui.IsItemHovered())
        {
            ImGui.SetTooltip(_module.LockRadarWindow ? "Unlock radar window position" : "Lock radar window position");
        }
        
        ImGui.SameLine();
        
        // Settings button
        ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(0.4f, 0.4f, 0.4f, buttonAlpha));
        ImGui.PushStyleColor(ImGuiCol.ButtonHovered, new Vector4(0.6f, 0.6f, 0.6f, buttonAlpha));
        ImGui.PushStyleColor(ImGuiCol.ButtonActive, new Vector4(0.3f, 0.3f, 0.3f, buttonAlpha));
        
        if (ImGuiComponents.IconButton(settingsIcon))
        {
            // TODO: Open configuration window
        }
        
        ImGui.PopStyleColor(3);
        
        if (ImGui.IsItemHovered())
        {
            ImGui.SetTooltip("Open settings");
        }
    }
    
    private void DrawRadiusCircles(ImDrawListPtr drawList, Vector2 center, float radarSize, float rotation)
    {
        float[] rangeRings = { 0.25f, 0.5f, 0.75f, 1.0f };
        for (int i = 0; i < rangeRings.Length; i++)
        {
            var alpha = i == rangeRings.Length - 1 ? 0.8f : 0.3f;
            var ringColor = new Vector4(RadiusCircleColor.X, RadiusCircleColor.Y, RadiusCircleColor.Z, alpha);
            drawList.AddCircle(
                center, 
                radarSize / 2 * rangeRings[i], 
                ImGui.ColorConvertFloat4ToU32(ringColor),
                32,
                i == rangeRings.Length - 1 ? 2.0f : 1.0f
            );
            
            // Add distance labels
            if (i > 0)
            {
                var labelPos = center - new Vector2(0, radarSize / 2 * rangeRings[i]);
                
                if (_module.RotateWithCamera)
                {
                    float x = labelPos.X - center.X;
                    float y = labelPos.Y - center.Y;
                    
                    float rotatedX = x * MathF.Cos(-rotation) - y * MathF.Sin(-rotation);
                    float rotatedY = x * MathF.Sin(-rotation) + y * MathF.Cos(-rotation);
                    
                    labelPos = center + new Vector2(rotatedX, rotatedY);
                }
                
                var distanceText = $"{_module.DetectionRadius * rangeRings[i]:F0}";
                var textSize = ImGui.CalcTextSize(distanceText);
                
                drawList.AddRectFilled(
                    labelPos - new Vector2(textSize.X / 2 + 2, 0),
                    labelPos + new Vector2(textSize.X / 2 + 2, textSize.Y),
                    ImGui.ColorConvertFloat4ToU32(new Vector4(0, 0, 0, 0.5f))
                );
                
                drawList.AddText(
                    labelPos - new Vector2(textSize.X / 2, 0),
                    ImGui.ColorConvertFloat4ToU32(new Vector4(1, 1, 1, 0.8f)),
                    distanceText
                );
            }
        }
    }
    
    private void DrawAlertRing(ImDrawListPtr drawList, Vector2 center, float radarSize, float rotation)
    {
        float alertRingRadius = (_module.PlayerProximityAlertDistance / _module.DetectionRadius) * (radarSize / 2);
        
        drawList.AddCircle(
            center,
            alertRingRadius,
            ImGui.ColorConvertFloat4ToU32(AlertRingColor),
            48,
            2.5f
        );
        
        var alertLabelPos = center - new Vector2(0, alertRingRadius);
        
        if (_module.RotateWithCamera)
        {
            float x = alertLabelPos.X - center.X;
            float y = alertLabelPos.Y - center.Y;
            
            float rotatedX = x * MathF.Cos(-rotation) - y * MathF.Sin(-rotation);
            float rotatedY = x * MathF.Sin(-rotation) + y * MathF.Cos(-rotation);
            
            alertLabelPos = center + new Vector2(rotatedX, rotatedY);
        }
        
        var alertText = $"Alert: {_module.PlayerProximityAlertDistance:F0}";
        var alertTextSize = ImGui.CalcTextSize(alertText);
        
        drawList.AddRectFilled(
            alertLabelPos - new Vector2(alertTextSize.X / 2 + 3, 0),
            alertLabelPos + new Vector2(alertTextSize.X / 2 + 3, alertTextSize.Y),
            ImGui.ColorConvertFloat4ToU32(new Vector4(0.7f, 0, 0, 0.7f))
        );
        
        drawList.AddText(
            alertLabelPos - new Vector2(alertTextSize.X / 2, 0),
            ImGui.ColorConvertFloat4ToU32(new Vector4(1, 1, 1, 1.0f)),
            alertText
        );
    }
    
    private void DrawDirectionIndicators(ImDrawListPtr drawList, Vector2 center, float radarSize, float rotation)
    {
        float rotationAngle = _module.RotateWithCamera ? -rotation : 0;
        
        var directionIndicatorLength = radarSize * 0.05f;
        var directionLabelOffset = radarSize * 0.48f;
        
        Vector2 northLabelPos = center + RotatePoint(new Vector2(0, -directionLabelOffset), rotationAngle);
        Vector2 eastLabelPos = center + RotatePoint(new Vector2(directionLabelOffset, 0), rotationAngle);
        Vector2 southLabelPos = center + RotatePoint(new Vector2(0, directionLabelOffset), rotationAngle);
        Vector2 westLabelPos = center + RotatePoint(new Vector2(-directionLabelOffset, 0), rotationAngle);
        
        // Note: Game coordinate system has Z pointing North (negative Z is North)
        // So we don't need to swap N/S labels
        
        float labelScale = 1.2f;
        
        // North
        DrawDirectionLabel(drawList, northLabelPos, "N", labelScale);
        
        // East
        DrawDirectionLabel(drawList, eastLabelPos, "E", labelScale);
        
        // South
        DrawDirectionLabel(drawList, southLabelPos, "S", labelScale);
        
        // West
        DrawDirectionLabel(drawList, westLabelPos, "W", labelScale);
    }
    
    private void DrawDirectionLabel(ImDrawListPtr drawList, Vector2 pos, string label, float scale)
    {
        var labelSize = ImGui.CalcTextSize(label);
        
        drawList.AddRectFilled(
            pos - new Vector2(labelSize.X / 2 * scale + 4, labelSize.Y / 2 * scale + 4),
            pos + new Vector2(labelSize.X / 2 * scale + 4, labelSize.Y / 2 * scale + 4),
            ImGui.ColorConvertFloat4ToU32(new Vector4(0, 0, 0, 0.85f))
        );
        
        drawList.AddRect(
            pos - new Vector2(labelSize.X / 2 * scale + 4, labelSize.Y / 2 * scale + 4),
            pos + new Vector2(labelSize.X / 2 * scale + 4, labelSize.Y / 2 * scale + 4),
            ImGui.ColorConvertFloat4ToU32(new Vector4(1, 1, 1, 0.9f)),
            0, 0, 1.0f
        );
        
        drawList.AddText(
            pos - new Vector2(labelSize.X / 2, labelSize.Y / 2),
            ImGui.ColorConvertFloat4ToU32(new Vector4(1, 1, 1, 1.0f)),
            label
        );
    }
    
    private void DrawTrackedObjects(ImDrawListPtr drawList, Vector2 center, float radarSize, float rotation, IGameObject player)
    {
        var trackedObjects = _module.GetTrackedObjects();
        var recentlyAlertedPlayers = _module.RecentlyAlertedPlayers;
        
        foreach (var obj in trackedObjects)
        {
            var relPos = obj.Position - player.Position;
            
            Vector2 screenPos;
            if (_module.RotateWithCamera)
            {
                var pos2D = new Vector2(relPos.X, -relPos.Z);
                pos2D = RotatePoint(pos2D, -rotation);
                var scale = (radarSize / 2) / _module.DetectionRadius;
                screenPos = center + pos2D * scale;
            }
            else
            {
                var scale = (radarSize / 2) / _module.DetectionRadius;
                screenPos = center + new Vector2(relPos.X * scale, -relPos.Z * scale);
            }
            
            var color = GetObjectColor(obj.Category);
            
            if (ShouldDrawTether(obj.Category))
            {
                var tetherColor = obj.Category == ObjectCategory.Player ? PlayerLineColor : color;
                
                drawList.AddLine(
                    center, 
                    screenPos, 
                    ImGui.ColorConvertFloat4ToU32(tetherColor), 
                    1.5f
                );
                
                // Draw distance text
                var midPoint = (center + screenPos) / 2;
                var distanceText = $"{obj.Distance:F1}";
                var textSize = ImGui.CalcTextSize(distanceText);
                
                drawList.AddRectFilled(
                    midPoint - new Vector2(textSize.X / 2 + 2, textSize.Y / 2 + 2),
                    midPoint + new Vector2(textSize.X / 2 + 2, textSize.Y / 2 + 2),
                    ImGui.ColorConvertFloat4ToU32(new Vector4(0, 0, 0, 0.5f))
                );
                
                drawList.AddText(
                    midPoint - new Vector2(textSize.X / 2, textSize.Y / 2),
                    ImGui.ColorConvertFloat4ToU32(new Vector4(1, 1, 1, 0.8f)),
                    distanceText
                );
            }
            
            // Draw object dot
            drawList.AddCircleFilled(screenPos, 3, ImGui.ColorConvertFloat4ToU32(color));
            
            // Draw alert highlight for players
            if (obj.Category == ObjectCategory.Player && 
                recentlyAlertedPlayers.TryGetValue(obj.ObjectId, out DateTime alertTime))
            {
                var timeSinceAlert = (DateTime.Now - alertTime).TotalSeconds;
                float pulseProgress = (float)(timeSinceAlert % 1.0);
                float pulseSize = 5.0f + MathF.Sin(pulseProgress * MathF.PI * 2) * 3.0f;
                float pulseAlpha = MathF.Max(0, 1.0f - (float)(timeSinceAlert / RadarModule.ALERT_HIGHLIGHT_DURATION));
                
                var highlightColor = AlertedPlayerHighlight;
                highlightColor.W = pulseAlpha;
                
                drawList.AddCircle(
                    screenPos, 
                    pulseSize, 
                    ImGui.ColorConvertFloat4ToU32(highlightColor),
                    12,
                    2.0f
                );
            }
            
            // Show tooltip on hover
            var mousePos = ImGui.GetMousePos();
            if (Vector2.Distance(mousePos, screenPos) < 5)
            {
                ImGui.BeginTooltip();
                ImGui.TextUnformatted($"{obj.Name} ({obj.Distance:F1} yalms)");
                ImGui.EndTooltip();
            }
        }
    }
    
    private Vector4 GetObjectColor(ObjectCategory category)
    {
        return category switch
        {
            ObjectCategory.Player => new Vector4(0, 0, 1, 1),
            ObjectCategory.NPC or ObjectCategory.FriendlyNPC => new Vector4(1, 1, 0, 1),
            ObjectCategory.Treasure => new Vector4(1, 0.8f, 0, 1),
            ObjectCategory.GatheringPoint => new Vector4(0, 1, 0, 1),
            ObjectCategory.Aetheryte => new Vector4(0.5f, 0.5f, 1, 1),
            ObjectCategory.EventObject => new Vector4(1, 0.5f, 0, 1),
            ObjectCategory.Mount => new Vector4(0.8f, 0.4f, 0.2f, 1),
            ObjectCategory.Companion => new Vector4(1, 0.7f, 1, 1),
            ObjectCategory.Retainer => new Vector4(0.7f, 0.7f, 0.7f, 1),
            _ => new Vector4(0.7f, 0.7f, 0.7f, 1)
        };
    }
    
    private bool ShouldDrawTether(ObjectCategory category)
    {
        return category switch
        {
            ObjectCategory.Player => _module.DrawPlayerLines,
            ObjectCategory.NPC or ObjectCategory.FriendlyNPC => _module.DrawNPCTethers,
            ObjectCategory.Treasure => _module.DrawTreasureTethers,
            ObjectCategory.GatheringPoint => _module.DrawGatheringTethers,
            ObjectCategory.Aetheryte => _module.DrawAetheryteTethers,
            _ => false
        };
    }
    
    private Vector2 RotatePoint(Vector2 point, float angle)
    {
        float cs = MathF.Cos(angle);
        float sn = MathF.Sin(angle);
        
        return new Vector2(
            point.X * cs - point.Y * sn,
            point.X * sn + point.Y * cs
        );
    }
}
