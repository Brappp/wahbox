using Dalamud.Configuration;
using Dalamud.Plugin;
using System;
using System.Collections.Generic;
using System.IO;
using System.Numerics;
using SamplePlugin.Models;

namespace SamplePlugin;

[Serializable]
public class Configuration : IPluginConfiguration
{
    public int Version { get; set; } = 1;
    
    // Example setting from original
    public bool SomePropertyToBeSavedAndWithADefault { get; set; } = true;
    
    // Module management
    public HashSet<string> EnabledModules { get; set; } = new();
    public Dictionary<string, object> ModuleConfigs { get; set; } = new();
    
    // Currency Alert settings (migrated)
    public CurrencySettings CurrencySettings { get; set; } = new();
    
    // Daily Duty settings (migrated)
    public TodoSettings TodoSettings { get; set; } = new();
    public TimerSettings TimerSettings { get; set; } = new();
    
    // UI settings
    public UISettings UISettings { get; set; } = new();
    
    // Notification settings
    public NotificationSettings NotificationSettings { get; set; } = new();
    
    // Overlay settings
    public OverlaySettings OverlaySettings { get; set; } = new();
    
    // Language setting
    public string Language { get; set; } = "English";
    
    // Character-specific data management
    private Dictionary<ulong, CharacterData> _characterData = new();

    // the below exist just to make saving less cumbersome
    public void Save()
    {
        Plugin.PluginInterface.SavePluginConfig(this);
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
public class CurrencySettings
{
    public bool ShowCurrencyOverlay { get; set; } = true;
    public bool HideInDuties { get; set; } = false;
    public bool ChatWarning { get; set; } = false;
    public List<TrackedCurrency> TrackedCurrencies { get; set; } = new();
    public Vector2 OverlayPosition { get; set; } = new(960, 540);
    public Vector2 OverlaySize { get; set; } = new(600, 200);
    public bool ShowOverlayBackground { get; set; } = true;
    public Vector4 OverlayBackgroundColor { get; set; } = new(0, 0, 0, 0.3f);
}

public class TodoSettings
{
    public bool Enabled { get; set; } = true;
    public bool HideInDuties { get; set; } = true;
    public bool HideDuringQuests { get; set; } = true;
    public Vector2 Position { get; set; } = new(750, 375);
    public Vector2 Size { get; set; } = new(600, 200);
    public Dictionary<string, bool> EnabledTasks { get; set; } = new();
}

public class TimerSettings
{
    public bool Enabled { get; set; } = true;
    public bool EnableDailyTimer { get; set; } = true;
    public bool EnableWeeklyTimer { get; set; } = true;
    public bool HideInDuties { get; set; } = true;
    public bool HideInQuestEvents { get; set; } = true;
    public bool HideTimerSeconds { get; set; } = false;
    public Vector2 DailyTimerPosition { get; set; } = new(400, 475);
    public Vector2 WeeklyTimerPosition { get; set; } = new(400, 400);
}

public class UISettings
{
    public float WindowOpacity { get; set; } = 1.0f;
    public bool UseCompactMode { get; set; } = false;
    public bool ShowCategoryHeaders { get; set; } = true;
    public bool SortByStatus { get; set; } = true;
    public bool ShowDisabledModules { get; set; } = false;
    public bool HideInCombat { get; set; } = false;
    public bool HideInDuty { get; set; } = false;
    public Vector2 MainWindowSize { get; set; } = new(800, 600);
    public Vector2 MainWindowPosition { get; set; } = new(100, 100);
}

public class NotificationSettings
{
    public bool EnableChatNotifications { get; set; } = true;
    public bool EnableToastNotifications { get; set; } = true;
    public bool EnableSoundNotifications { get; set; } = false;
    public int NotificationThreshold { get; set; } = 90;
    public bool ChatNotifications { get; set; } = true;
    public bool SoundNotifications { get; set; } = false;
    public bool SuppressInDuty { get; set; } = true;
    public int NotificationCooldown { get; set; } = 5;
    public bool CurrencyWarningAlerts { get; set; } = true;
    public bool TaskCompletionAlerts { get; set; } = true;
}

public class OverlaySettings
{
    public bool Enabled { get; set; } = true;
    public float Opacity { get; set; } = 0.8f;
    public bool ShowBackground { get; set; } = true;
    public bool ShowCurrencyWarnings { get; set; } = true;
    public bool ShowDailyTasks { get; set; } = true;
    public bool ShowWeeklyTasks { get; set; } = true;
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
