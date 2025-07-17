using Dalamud.Configuration;
using Dalamud.Plugin;
using System;
using System.Collections.Generic;
using System.IO;
using System.Numerics;
using WahBox.Models;

namespace WahBox;

[Serializable]
public class Configuration : IPluginConfiguration
{
    public int Version { get; set; } = 1;
    
    // Module management
    public HashSet<string> EnabledModules { get; set; } = new();
    public Dictionary<string, object> ModuleConfigs { get; set; } = new();
    
    // UI settings
    public UISettings UISettings { get; set; } = new();
    
    // Notification settings
    public NotificationSettings NotificationSettings { get; set; } = new();
    
    // Character-specific data management
    private Dictionary<ulong, CharacterData> _characterData = new();

    // the below exist just to make saving less cumbersome
    public void Save()
    {
        try
        {
            Plugin.PluginInterface.SavePluginConfig(this);
            Plugin.Log.Debug($"Configuration saved successfully. Enabled modules: {EnabledModules.Count}, Module configs: {ModuleConfigs.Count}");
        }
        catch (Exception ex)
        {
            Plugin.Log.Error(ex, "Failed to save configuration");
        }
    }
    
    public void ResetToDefaults()
    {
        // Keep character data but reset all settings
        EnabledModules = new();
        ModuleConfigs = new();
        UISettings = new();
        NotificationSettings = new();
        
        Plugin.Log.Information("Configuration reset to defaults completed");
    }
    
    public void LoadCharacterData(ulong contentId)
    {
        if (!_characterData.ContainsKey(contentId))
        {
            _characterData[contentId] = new CharacterData { ContentId = contentId };
        }
    }
    
    public void SaveCharacterData(ulong contentId)
    {
        Save(); // Just save the whole config
    }
    
    public CharacterData? GetCharacterData(ulong contentId)
    {
        return _characterData.TryGetValue(contentId, out var data) ? data : null;
    }
}

// Settings classes
public class UISettings
{
    public bool ShowWarningsOnly { get; set; } = false;
    public bool HideCompleted { get; set; } = false;
    public bool ShowDisabledModules { get; set; } = false;
    public bool HideInCombat { get; set; } = false;
    public bool HideInDuty { get; set; } = false;
    public Vector2 MainWindowSize { get; set; } = new(800, 600);
    public Vector2 MainWindowPosition { get; set; } = new(100, 100);
    public Dictionary<string, bool> ExpandedSections { get; set; } = new();
    
    // Module visibility settings
    public Dictionary<string, bool> VisibleModules { get; set; } = new();
    
    // Currency alert thresholds
    public Dictionary<string, int> CurrencyAlertThresholds { get; set; } = new();
    public bool ShowCurrencyModules { get; set; } = true;
    public bool ShowDailyModules { get; set; } = true;
    public bool ShowWeeklyModules { get; set; } = true;
    public bool ShowSpecialModules { get; set; } = true;
}

public class NotificationSettings
{
    // Main alert channels
    public bool ChatNotifications { get; set; } = true;
    public bool EnableToastNotifications { get; set; } = true;
    public bool EnableSoundAlerts { get; set; } = true;
    public bool SuppressInDuty { get; set; } = true;
    
    // Currency alerts
    public int NotificationThreshold { get; set; } = 90;
    public int NotificationCooldown { get; set; } = 5;
    public bool CurrencyWarningAlerts { get; set; } = true;
    public int CurrencyAlertSound { get; set; } = 1; // 0=ping, 1=alert, 2=notification, 3=alarm
    
    // Task alerts
    public bool TaskCompletionAlerts { get; set; } = true;
    public int TaskCompleteSound { get; set; } = 0; // 0=ping
    public bool DailyResetReminder { get; set; } = true;
    public int DailyResetReminderMinutes { get; set; } = 15;
}

public class CharacterData
{
    public ulong ContentId { get; set; }
    public string CharacterName { get; set; } = string.Empty;
    public string WorldName { get; set; } = string.Empty;
    public Dictionary<string, ModuleData> ModuleData { get; set; } = new();
}

public class ModuleData
{
    public DateTime LastReset { get; set; }
    public Dictionary<string, object> CustomData { get; set; } = new();
}
