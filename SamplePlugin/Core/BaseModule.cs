using SamplePlugin.Core.Interfaces;
using ImGuiNET;

namespace SamplePlugin.Core;

public abstract class BaseModule : IModule
{
    public abstract string Name { get; }
    public abstract ModuleType Type { get; }
    public virtual ModuleStatus Status { get; protected set; } = ModuleStatus.Unknown;
    public bool IsEnabled { get; set; } = true;

    protected readonly Plugin Plugin;

    protected BaseModule(Plugin plugin)
    {
        Plugin = plugin;
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