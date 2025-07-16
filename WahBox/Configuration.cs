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
    public int NotificationThreshold { get; set; } = 90;
    public bool ChatNotifications { get; set; } = true;
    public bool SuppressInDuty { get; set; } = true;
    public int NotificationCooldown { get; set; } = 5;
    public bool CurrencyWarningAlerts { get; set; } = true;
    public bool TaskCompletionAlerts { get; set; } = true;
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
