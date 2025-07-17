using WahBox.Core.Interfaces;
using ImGuiNET;

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
    }

    public abstract void Update();

    public virtual void Load()
    {
        // Base load logic
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
        // Cleanup
    }

    public virtual void DrawConfig()
    {
        ImGui.TextWrapped($"No configuration available for {Name}");
    }

    public virtual void DrawStatus()
    {
        ImGui.Text($"{Name}: {Status}");
    }
} 