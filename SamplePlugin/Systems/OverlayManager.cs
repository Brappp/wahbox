using System;
using System.Collections.Generic;
using System.Numerics;
using ImGuiNET;
using Dalamud.Interface.Windowing;
using Dalamud.Interface.Utility;
using SamplePlugin.Core.Interfaces;

namespace SamplePlugin.Systems;

public class OverlayManager : IDisposable
{
    private readonly List<OverlayWindow> _overlayWindows = new();
    private bool _isEnabled = true;
    private Vector2 _overlayPosition = new(100, 100);

    public void Initialize()
    {
        Plugin.Log.Information("OverlayManager initialized");
        
        // Create overlay windows
        _overlayWindows.Add(new CurrencyOverlay());
        _overlayWindows.Add(new ModuleStatusOverlay());
        
        // Register draw handlers
        Plugin.PluginInterface.UiBuilder.Draw += DrawOverlays;
    }
    
    public void RefreshAll()
    {
        // Overlay content is drawn dynamically, no need to refresh
    }
    
    private void DrawOverlays()
    {
        if (!_isEnabled || !Plugin.ClientState.IsLoggedIn) return;
        
        foreach (var overlay in _overlayWindows)
        {
            if (overlay.IsEnabled)
            {
                overlay.Draw();
            }
        }
    }
    
    public void ToggleOverlays()
    {
        _isEnabled = !_isEnabled;
    }
    
    public void DrawConfiguration()
    {
        ImGui.Checkbox("Enable Overlays", ref _isEnabled);
        
        if (_isEnabled)
        {
            ImGui.Separator();
            foreach (var overlay in _overlayWindows)
            {
                if (ImGui.CollapsingHeader(overlay.Name))
                {
                    overlay.DrawConfig();
                }
            }
        }
    }
    
    public void Dispose()
    {
        Plugin.PluginInterface.UiBuilder.Draw -= DrawOverlays;
        
        foreach (var overlay in _overlayWindows)
        {
            overlay.Dispose();
        }
    }
}

public abstract class OverlayWindow : IDisposable
{
    public abstract string Name { get; }
    public bool IsEnabled { get; set; } = true;
    public Vector2 Position { get; set; } = new(100, 100);
    public bool LockPosition { get; set; } = false;
    public float Opacity { get; set; } = 1.0f;
    
    public abstract void Draw();
    
    public virtual void DrawConfig()
    {
        var isEnabled = IsEnabled;
        if (ImGui.Checkbox($"Enable {Name}", ref isEnabled))
        {
            IsEnabled = isEnabled;
        }
        
        var lockPosition = LockPosition;
        if (ImGui.Checkbox("Lock Position", ref lockPosition))
        {
            LockPosition = lockPosition;
        }
        
        var opacity = Opacity;
        if (ImGui.SliderFloat("Opacity", ref opacity, 0.1f, 1.0f))
        {
            Opacity = opacity;
        }
        
        if (!LockPosition)
        {
            var pos = Position;
            if (ImGui.DragFloat2("Position", ref pos))
            {
                Position = pos;
            }
        }
    }
    
    public virtual void Dispose() { }
}

public class CurrencyOverlay : OverlayWindow
{
    public override string Name => "Currency Warnings";
    
    public override void Draw()
    {
        var flags = ImGuiWindowFlags.NoDecoration | ImGuiWindowFlags.NoSavedSettings | 
                   ImGuiWindowFlags.NoFocusOnAppearing | ImGuiWindowFlags.NoNav |
                   ImGuiWindowFlags.AlwaysAutoResize;
        
        if (LockPosition)
        {
            flags |= ImGuiWindowFlags.NoMove;
        }
        
        ImGui.SetNextWindowPos(Position, ImGuiCond.FirstUseEver);
        ImGui.SetNextWindowBgAlpha(Opacity);
        
        if (ImGui.Begin("##CurrencyOverlay", flags))
        {
            var hasContent = false;
            
            // Draw currency warnings
            foreach (var module in Plugin.Instance.ModuleManager.GetModules())
            {
                if (module.Type == ModuleType.Currency && module.IsEnabled)
                {
                    ImGui.PushID(module.Name);
                    module.DrawStatus();
                    ImGui.PopID();
                    hasContent = true;
                }
            }
            
            if (!hasContent)
            {
                ImGui.TextColored(new Vector4(0.7f, 0.7f, 0.7f, 1), "No currency warnings");
            }
            
            // Update position if moved
            if (!LockPosition)
            {
                Position = ImGui.GetWindowPos();
            }
        }
        ImGui.End();
    }
}

public class ModuleStatusOverlay : OverlayWindow
{
    public override string Name => "Daily/Weekly Status";
    public bool ShowCompleted { get; set; } = false;
    
    public override void Draw()
    {
        var flags = ImGuiWindowFlags.NoDecoration | ImGuiWindowFlags.NoSavedSettings | 
                   ImGuiWindowFlags.NoFocusOnAppearing | ImGuiWindowFlags.NoNav |
                   ImGuiWindowFlags.AlwaysAutoResize;
        
        if (LockPosition)
        {
            flags |= ImGuiWindowFlags.NoMove;
        }
        
        ImGui.SetNextWindowPos(Position, ImGuiCond.FirstUseEver);
        ImGui.SetNextWindowBgAlpha(Opacity);
        
        if (ImGui.Begin("##ModuleStatusOverlay", flags))
        {
            var hasContent = false;
            
            // Draw daily modules
            ImGui.TextColored(new Vector4(1, 0.8f, 0, 1), "Daily Tasks");
            foreach (var module in Plugin.Instance.ModuleManager.GetModules())
            {
                if (module.Type == ModuleType.Daily && module.IsEnabled)
                {
                    if (!ShowCompleted && module.Status == ModuleStatus.Complete) continue;
                    
                    ImGui.PushID(module.Name);
                    DrawModuleStatus(module);
                    ImGui.PopID();
                    hasContent = true;
                }
            }
            
            ImGui.Spacing();
            
            // Draw weekly modules
            ImGui.TextColored(new Vector4(0, 0.8f, 1, 1), "Weekly Tasks");
            foreach (var module in Plugin.Instance.ModuleManager.GetModules())
            {
                if (module.Type == ModuleType.Weekly && module.IsEnabled)
                {
                    if (!ShowCompleted && module.Status == ModuleStatus.Complete) continue;
                    
                    ImGui.PushID(module.Name);
                    DrawModuleStatus(module);
                    ImGui.PopID();
                    hasContent = true;
                }
            }
            
            if (!hasContent)
            {
                ImGui.TextColored(new Vector4(0.7f, 0.7f, 0.7f, 1), "No active tasks");
            }
            
            // Update position if moved
            if (!LockPosition)
            {
                Position = ImGui.GetWindowPos();
            }
        }
        ImGui.End();
    }
    
    private void DrawModuleStatus(IModule module)
    {
        var color = module.Status switch
        {
            ModuleStatus.Complete => new Vector4(0, 1, 0, 1),
            ModuleStatus.InProgress => new Vector4(1, 1, 0, 1),
            ModuleStatus.Incomplete => new Vector4(1, 0.3f, 0.3f, 1),
            _ => new Vector4(1, 1, 1, 1)
        };
        
        var icon = module.Status switch
        {
            ModuleStatus.Complete => "✓",
            ModuleStatus.InProgress => "◐",
            ModuleStatus.Incomplete => "○",
            _ => "?"
        };
        
        ImGui.TextColored(color, $"{icon} {module.Name}");
    }
    
    public override void DrawConfig()
    {
        base.DrawConfig();
        
        var showCompleted = ShowCompleted;
        if (ImGui.Checkbox("Show Completed Tasks", ref showCompleted))
        {
            ShowCompleted = showCompleted;
        }
    }
} 