using Dalamud.Game.Command;
using Dalamud.IoC;
using Dalamud.Plugin;
using System.IO;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin.Services;
using Wahdar.Windows;
using Wahdar.Drawing;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading;
using System.Linq;
using System.Numerics;

namespace Wahdar;

public sealed class Plugin : IDalamudPlugin
{
    [PluginService] internal static IDalamudPluginInterface PluginInterface { get; private set; } = null!;
    [PluginService] internal static ITextureProvider TextureProvider { get; private set; } = null!;
    [PluginService] internal static ICommandManager CommandManager { get; private set; } = null!;
    [PluginService] internal static IClientState ClientState { get; private set; } = null!;
    [PluginService] internal static IDataManager DataManager { get; private set; } = null!;
    [PluginService] internal static IPluginLog Log { get; private set; } = null!;
    [PluginService] internal static IObjectTable ObjectTable { get; private set; } = null!;
    [PluginService] internal static IFramework Framework { get; private set; } = null!;
    [PluginService] internal static IGameGui GameGui { get; private set; } = null!;
    [PluginService] internal static IGameConfig GameConfig { get; private set; } = null!;

    private const string CommandName = "/wahdar";

    public Configuration Configuration { get; init; }
    public GameObjectTracker ObjectTracker { get; init; }
    public NavmeshIPC NavmeshIPC { get; init; }

    public readonly WindowSystem WindowSystem = new("Wahdar");
    private ConfigWindow ConfigWindow { get; init; }
    private RadarWindow RadarWindow { get; init; }
    private ObjectListWindow ObjectListWindow { get; init; }
    
    private DateTime lastAlertTime = DateTime.MinValue;
    private HashSet<string> alertedPlayerIds = new HashSet<string>();
    
    private Dictionary<string, DateTime> recentlyAlertedPlayers = new Dictionary<string, DateTime>();
    public const float ALERT_HIGHLIGHT_DURATION = 5.0f;
    
    public IReadOnlyDictionary<string, DateTime> RecentlyAlertedPlayers => recentlyAlertedPlayers;
    
    private Timer? _alertTimer;
    private readonly object _alertLock = new object();
    private bool _pendingAlertCheck = false;
    private const int ALERT_CHECK_INTERVAL_MS = 50;
    
    private HashSet<string> playersCurrentlyInRange = new HashSet<string>();
    private int _debugCounter = 0;

    [DllImport("winmm.dll", SetLastError = true)]
    private static extern bool PlaySound(string pszSound, IntPtr hmod, uint fdwSound);
    
    private const uint SND_ASYNC = 0x0001;
    private const uint SND_FILENAME = 0x00020000;

    public Plugin()
    {
        Configuration = PluginInterface.GetPluginConfig() as Configuration ?? new Configuration();
        ObjectTracker = new GameObjectTracker(ObjectTable, Configuration);
        NavmeshIPC = new NavmeshIPC(PluginInterface, Log);

        ConfigWindow = new ConfigWindow(this);
        RadarWindow = new RadarWindow(this);
        ObjectListWindow = new ObjectListWindow(this);

        WindowSystem.AddWindow(ConfigWindow);
        ApplyRadarVisibility();
        ApplyObjectListVisibility();

        CommandManager.AddHandler(CommandName, new CommandInfo(OnCommand)
        {
            HelpMessage = "Opens radar window. Use '/wahdar draw' to toggle in-game overlay, '/wahdar config' for settings, '/wahdar alerts' to toggle alerts, or '/wahdar nav' to check navmesh status."
        });

        PluginInterface.UiBuilder.Draw += DrawUI;
        PluginInterface.UiBuilder.OpenConfigUi += ToggleConfigUI;
        PluginInterface.UiBuilder.OpenMainUi += ToggleRadarUI;
        
        Framework.Update += OnFrameworkUpdate;
        StartAlertTimer();

        Log.Information("Wahdar plugin has been loaded!");
    }

    public void Dispose()
    {
        WindowSystem.RemoveAllWindows();
        
        ConfigWindow.Dispose();
        RadarWindow.Dispose();
        ObjectListWindow.Dispose();
        NavmeshIPC.Dispose();
        
        Framework.Update -= OnFrameworkUpdate;
        StopAlertTimer();

        // Force cleanup of any overlay drawing state
        GameDrawing.ForceCleanupOverlay();

        CommandManager.RemoveHandler(CommandName);
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
    
    private void OnFrameworkUpdate(IFramework framework)
    {
        bool shouldCheck = false;
        
        lock (_alertLock)
        {
            if (_pendingAlertCheck)
            {
                shouldCheck = true;
                _pendingAlertCheck = false;
                
                if (_debugCounter == 0)
                {
                    Log.Debug("Alert system initialized and running");
                }
                _debugCounter++;
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
                Log.Error($"Error checking player proximity: {ex.Message}");
            }
        }
    }

    private void OnCommand(string command, string args)
    {
        var argsParts = args.Trim().ToLower().Split(' ');
        
        if (argsParts.Length > 0 && !string.IsNullOrEmpty(argsParts[0]))
        {
            switch (argsParts[0])
            {
                case "overlay":
                case "draw":
                    Configuration.EnableInGameDrawing = !Configuration.EnableInGameDrawing;
                    Configuration.Save();
                    
                    var status = Configuration.EnableInGameDrawing ? "enabled" : "disabled";
                    Log.Information($"In-game overlay has been {status}.");
                    return;
                    
                case "config":
                    ToggleConfigUI();
                    return;
                
                case "alerts":
                    Configuration.EnablePlayerProximityAlert = !Configuration.EnablePlayerProximityAlert;
                    Configuration.Save();
                    
                    var alertStatus = Configuration.EnablePlayerProximityAlert ? "enabled" : "disabled";
                    Log.Information($"Player proximity alerts have been {alertStatus}.");
                    return;
                    
                case "navmesh":
                case "nav":
                    if (Configuration.EnableNavmeshIntegration)
                    {
                        bool isReady = NavmeshIPC.IsNavmeshReady();
                        bool inProgress = NavmeshIPC.IsPathfindingInProgress();
                        
                        Log.Information($"Navmesh Integration: Enabled");
                        Log.Information($"Navmesh Ready: {(isReady ? "Yes" : "No")}");
                        Log.Information($"Pathfinding in Progress: {(inProgress ? "Yes" : "No")}");
                        
                        if (!isReady)
                        {
                            Log.Information("Make sure the vnavmesh plugin is installed and enabled.");
                        }
                    }
                    else
                    {
                        Log.Information("Navmesh integration is disabled. Enable it in the Navmesh tab of the configuration window.");
                    }
                    return;
            }
        }
        
        ToggleRadarUI();
    }

    private void DrawUI()
    {
        WindowSystem.Draw();
        
        if (Configuration.EnableInGameDrawing && ClientState.LocalPlayer != null)
        {
            DrawInGameOverlay();
        }
    }
    
    private void DrawInGameOverlay()
    {
        if (RadarWindow != null)
        {
            RadarWindow.DrawInGameOverlay();
        }
    }
    
    private void CheckPlayerProximity()
    {
        if (!Configuration.EnablePlayerProximityAlert || ClientState.LocalPlayer == null)
            return;
                
        var currentTime = DateTime.Now;
        bool cooldownExpired = (currentTime - lastAlertTime).TotalSeconds >= Configuration.PlayerProximityAlertCooldown;
                
        // Skip if still in cooldown
        if (!cooldownExpired)
            return;
                
        // Process different alert frequency modes
        switch (Configuration.PlayerAlertFrequency)
        {
            case Configuration.AlertFrequencyMode.OnlyOnce:
                break;
                
            case Configuration.AlertFrequencyMode.EveryInterval:
                alertedPlayerIds.Clear();
                break;
                
            case Configuration.AlertFrequencyMode.OnEnterLeaveReenter:
                break;
        }
                
        var trackedObjects = ObjectTracker.GetTrackedObjects();
        if (trackedObjects.Count == 0)
            return;
                
        bool anyPlayerInRange = false;
        List<string> playersInRange = new List<string>();
        HashSet<string> currentlyInRange = new HashSet<string>();
            
        foreach (var obj in trackedObjects)
        {
            if (obj.Category != ObjectCategory.Player)
                continue;
                    
            if (obj.Distance <= Configuration.PlayerProximityAlertDistance)
            {
                currentlyInRange.Add(obj.ObjectId);
                
                if (Configuration.PlayerAlertFrequency == Configuration.AlertFrequencyMode.OnEnterLeaveReenter)
                {
                    if (!playersCurrentlyInRange.Contains(obj.ObjectId) || 
                        (!playersCurrentlyInRange.Contains(obj.ObjectId) && !alertedPlayerIds.Contains(obj.ObjectId)))
                    {
                        anyPlayerInRange = true;
                        playersInRange.Add($"{obj.Name} ({obj.Distance:F1} yalms)");
                        alertedPlayerIds.Add(obj.ObjectId);
                    }
                }
                else
                {
                    if (!alertedPlayerIds.Contains(obj.ObjectId))
                    {
                        anyPlayerInRange = true;
                        playersInRange.Add($"{obj.Name} ({obj.Distance:F1} yalms)");
                        alertedPlayerIds.Add(obj.ObjectId);
                    }
                }
            }
        }
        
        // Handle player tracking for enter/leave/reenter mode
        if (Configuration.PlayerAlertFrequency == Configuration.AlertFrequencyMode.OnEnterLeaveReenter)
        {
            foreach (var playerId in playersCurrentlyInRange)
            {
                if (!currentlyInRange.Contains(playerId))
                {
                    // Player left range, remove from alerts to enable redetection
                    alertedPlayerIds.Remove(playerId);
                }
            }
            
            // Update tracking list for next check
            playersCurrentlyInRange = currentlyInRange;
        }
            
        // Play alert if any players triggered
        if (anyPlayerInRange)
        {
            if (Configuration.EnableAlertSound)
            {
                PlayAlertSound(Configuration.PlayerProximityAlertSound);
            }
            lastAlertTime = currentTime;
            
            if (playersInRange.Count > 0)
            {
                Log.Debug($"Alert triggered by: {string.Join(", ", playersInRange)}");
                
                // Add triggering players to highlighted list
                foreach (var obj in trackedObjects)
                {
                    if (obj.Category == ObjectCategory.Player && 
                        obj.Distance <= Configuration.PlayerProximityAlertDistance &&
                        playersInRange.Any(p => p.StartsWith(obj.Name)))
                    {
                        recentlyAlertedPlayers[obj.ObjectId] = currentTime;
                    }
                }
            }
        }
        
        // Remove expired highlights
        var playersToRemove = recentlyAlertedPlayers
            .Where(kvp => (currentTime - kvp.Value).TotalSeconds > ALERT_HIGHLIGHT_DURATION)
            .Select(kvp => kvp.Key)
            .ToList();
            
        foreach (var playerId in playersToRemove)
        {
            recentlyAlertedPlayers.Remove(playerId);
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
            
            soundFilePath = Path.Combine(PluginInterface.AssemblyLocation.DirectoryName!, soundFileName);
            
            if (!File.Exists(soundFilePath))
            {
                string dataPath = Path.Combine(PluginInterface.AssemblyLocation.DirectoryName!, "..", "..", "..", "..", "Data", "sounds", soundFileName);
                if (File.Exists(dataPath))
                {
                    soundFilePath = dataPath;
                }
            }
            
            if (!File.Exists(soundFilePath))
            {
                Log.Error($"Could not find sound file: {soundFileName}. Searched in {soundFilePath}");
                return;
            }
            
            PlaySound(soundFilePath, IntPtr.Zero, SND_FILENAME | SND_ASYNC);
        }
        catch (Exception ex)
        {
            Log.Error($"Error playing alert sound: {ex.Message}");
        }
    }

    public void ToggleConfigUI() => ConfigWindow.Toggle();
    public void ToggleRadarUI() => RadarWindow.Toggle();
    public void ToggleObjectListUI() => ObjectListWindow.Toggle();
    
    public void SetObjectListWindowState(bool isOpen)
    {
        if (isOpen && !ObjectListWindow.IsOpen)
        {
            ObjectListWindow.IsOpen = true;
        }
        else if (!isOpen && ObjectListWindow.IsOpen)
        {
            ObjectListWindow.IsOpen = false;
        }
    }

    // Method to clear alert tracking data when frequency mode changes
    public void ClearAlertData()
    {
        lock (_alertLock)
        {
            alertedPlayerIds.Clear();
            playersCurrentlyInRange.Clear();
            recentlyAlertedPlayers.Clear();
            Log.Debug("Alert tracking data has been cleared due to frequency mode change");
        }
    }

    public void ApplyRadarVisibility()
    {
        if (Configuration.ShowRadarWindow)
        {
            if (!IsWindowInSystem(RadarWindow))
            {
                WindowSystem.AddWindow(RadarWindow);
            }
        }
        else
        {
            if (IsWindowInSystem(RadarWindow))
            {
                WindowSystem.RemoveWindow(RadarWindow);
            }
        }
    }
    
    private bool IsWindowInSystem(Window window)
    {
        foreach (var w in WindowSystem.Windows)
        {
            if (w == window)
                return true;
        }
        return false;
    }

    public void ApplyObjectListVisibility()
    {
        if (Configuration.ShowObjectListWindow)
        {
            if (!IsWindowInSystem(ObjectListWindow))
            {
                WindowSystem.AddWindow(ObjectListWindow);
            }
            ObjectListWindow.IsOpen = true;
        }
        else
        {
            if (IsWindowInSystem(ObjectListWindow))
            {
                WindowSystem.RemoveWindow(ObjectListWindow);
            }
            ObjectListWindow.IsOpen = false;
        }
    }

    public void StartPathfindingTo(Vector3 position, string targetName)
    {
        Log.Information($"Pathfinding to {targetName}");
    }
    
    public void StopPathfindingTracking()
    {
    }
    
    public void PressStopVNavButton()
    {
        CommandManager.ProcessCommand("/vnav stop");
        StopPathfindingTracking();
    }
}

