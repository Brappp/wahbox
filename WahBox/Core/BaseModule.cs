using WahBox.Core.Interfaces;
using ImGuiNET;
using System.Collections.Generic;
using System;

namespace WahBox.Core;

public abstract class BaseModule : IModule
{
    public abstract string Name { get; }
    public abstract ModuleType Type { get; }
    public virtual ModuleCategory Category => DetermineCategory();
    public virtual ModuleStatus Status { get; protected set; } = ModuleStatus.Unknown;
    public bool IsEnabled { get; set; } = true;
    public virtual uint IconId { get; protected set; } = 0;
    
    // Window support
    public virtual bool HasWindow => false;
    public virtual void OpenWindow() { }
    public virtual void CloseWindow() { }
    
    protected readonly Plugin Plugin;

    protected BaseModule(Plugin plugin)
    {
        Plugin = plugin;
    }
    
    private ModuleCategory DetermineCategory()
    {
        return Type switch
        {
            ModuleType.Currency or ModuleType.Daily or ModuleType.Weekly or ModuleType.Special => ModuleCategory.Tracking,
            ModuleType.Radar or ModuleType.Speedometer => ModuleCategory.Utility,
            _ => ModuleCategory.Tools
        };
    }

    public virtual void Initialize()
    {
        // Base initialization
        // Note: LoadConfiguration is called by ModuleManager during registration
    }

    public abstract void Update();

    public virtual void Load()
    {
        // Base load logic
        // Ensure configuration is loaded when the module is first used
        LoadConfiguration();
    }

    public virtual void Unload()
    {
        // Base unload logic
    }

    public virtual void Reset()
    {
        Status = ModuleStatus.Unknown;
    }

    public virtual void Dispose()
    {
        // Save configuration before disposing
        SaveConfiguration();
    }

    public virtual void DrawConfig()
    {
        ImGui.TextWrapped($"No configuration available for {Name}");
    }

    public virtual void DrawStatus()
    {
        ImGui.Text($"{Name}: {Status}");
    }

    // Configuration methods
    public virtual void SaveConfiguration()
    {
        try
        {
            // Save module enabled state
            if (IsEnabled)
            {
                Plugin.Configuration.EnabledModules.Add(Name);
            }
            else
            {
                Plugin.Configuration.EnabledModules.Remove(Name);
            }
            
            // Save module-specific configuration
            var config = GetConfigurationData();
            if (config != null)
            {
                Plugin.Configuration.ModuleConfigs[Name] = config;
            }
            
            Plugin.Configuration.Save();
        }
        catch (Exception ex)
        {
            Plugin.Log.Error(ex, $"Failed to save configuration for module {Name}");
        }
    }

    public virtual void LoadConfiguration()
    {
        try
        {
            // Load module enabled state
            IsEnabled = Plugin.Configuration.EnabledModules.Contains(Name);
            Plugin.Log.Debug($"Module {Name} loaded enabled state: {IsEnabled}");
            
            // Load module-specific configuration
            if (Plugin.Configuration.ModuleConfigs.TryGetValue(Name, out var config))
            {
                Plugin.Log.Debug($"Module {Name} found saved configuration, loading...");
                SetConfigurationData(config);
            }
            else
            {
                Plugin.Log.Debug($"Module {Name} no saved configuration found, using defaults");
            }
        }
        catch (Exception ex)
        {
            Plugin.Log.Error(ex, $"Failed to load configuration for module {Name}");
        }
    }

    // Virtual methods for modules to override
    protected virtual Dictionary<string, object>? GetConfigurationData()
    {
        // Base implementation returns null - modules should override this
        return null;
    }

    protected virtual void SetConfigurationData(object config)
    {
        // Base implementation does nothing - modules should override this
    }
} 