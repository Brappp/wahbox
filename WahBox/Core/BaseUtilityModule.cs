using WahBox.Core.Interfaces;
using ImGuiNET;
using Dalamud.Interface.Windowing;
using System;
using System.Linq;

namespace WahBox.Core;

/// <summary>
/// Base class for utility modules that have their own dedicated window
/// </summary>
public abstract class BaseUtilityModule : BaseModule
{
    protected Window? ModuleWindow { get; set; }
    
    public override bool HasWindow => true;
    public override ModuleCategory Category => ModuleCategory.Utility;
    
    protected BaseUtilityModule(Plugin plugin) : base(plugin)
    {
    }
    
    public override void Initialize()
    {
        base.Initialize();
        CreateWindow();
        if (ModuleWindow != null)
        {
            Plugin.WindowSystem.AddWindow(ModuleWindow);
        }
    }
    
    /// <summary>
    /// Create the module's window. Override this to create your custom window.
    /// </summary>
    protected abstract void CreateWindow();
    
    public override void OpenWindow()
    {
        if (ModuleWindow != null)
        {
            ModuleWindow.IsOpen = true;
            Status = ModuleStatus.Active;
        }
    }
    
    public override void CloseWindow()
    {
        if (ModuleWindow != null)
        {
            ModuleWindow.IsOpen = false;
            Status = ModuleStatus.Inactive;
        }
    }
    
    public override void Update()
    {
        // Update status based on window state
        if (ModuleWindow != null)
        {
            Status = ModuleWindow.IsOpen ? ModuleStatus.Active : ModuleStatus.Inactive;
        }
    }
    
    public override void Dispose()
    {
        if (ModuleWindow != null)
        {
            // Safely remove the window if it's still registered
            try
            {
                if (Plugin.WindowSystem.Windows.Any(w => w == ModuleWindow))
                {
                    Plugin.WindowSystem.RemoveWindow(ModuleWindow);
                }
            }
            catch (Exception ex)
            {
                Plugin.Log.Warning(ex, $"Failed to remove window for module {Name}");
            }
            ModuleWindow = null;
        }
        base.Dispose();
    }
    
    public override void DrawStatus()
    {
        var statusText = Status == ModuleStatus.Active ? "Active" : "Inactive";
        var color = Status == ModuleStatus.Active 
            ? new System.Numerics.Vector4(0.2f, 0.8f, 0.2f, 1) 
            : new System.Numerics.Vector4(0.5f, 0.5f, 0.5f, 1);
            
        ImGui.TextColored(color, statusText);
    }
}
