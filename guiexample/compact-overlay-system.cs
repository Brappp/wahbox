using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using ImGuiNET;
using Dalamud.Interface.Utility;
using SamplePlugin.Core.Interfaces;

namespace SamplePlugin.Systems;

public class OverlayManager : IDisposable
{
    private readonly List<OverlayWindow> _overlayWindows = new();
    private bool _isEnabled = true;

    public void Initialize()
    {
        _overlayWindows.Add(new CompactWarningOverlay());
        _overlayWindows.Add(new CompactTaskOverlay());
        
        Plugin.PluginInterface.UiBuilder.Draw += DrawOverlays;
    }
    
    private void DrawOverlays()
    {
        if (!_isEnabled || !Plugin.ClientState.IsLoggedIn) return;
        
        // Hide overlays in certain conditions
        if (Plugin.Condition[Dalamud.Game.ClientState.Conditions.ConditionFlag.InCombat] ||
            Plugin.Condition[Dalamud.Game.ClientState.Conditions.ConditionFlag.BetweenAreas] ||
            Plugin.Condition[Dalamud.Game.ClientState.Conditions.ConditionFlag.WatchingCutscene])
            return;
        
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
    
    public void RefreshAll() { }
    
    public void DrawConfiguration()
    {
        ImGui.Checkbox("Enable All Overlays", ref _isEnabled);
        
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
    public float Scale { get; set; } = 1.0f;
    public float BackgroundAlpha { get; set; } = 0.3f;
    
    public abstract void Draw();
    
    public virtual void DrawConfig()
    {
        ImGui.Checkbox($"Enable##{Name}", ref IsEnabled);
        ImGui.Checkbox($"Lock Position##{Name}", ref LockPosition);
        
        ImGui.SetNextItemWidth(150);
        ImGui.SliderFloat($"Scale##{Name}", ref Scale, 0.5f, 2.0f);
        
        ImGui.SetNextItemWidth(150);
        ImGui.SliderFloat($"Background##{Name}", ref BackgroundAlpha, 0.0f, 1.0f);
        
        if (!LockPosition)
        {
            ImGui.DragFloat2($"Position##{Name}", ref Position);
        }
    }
    
    public virtual void Dispose() { }
}

public class CompactWarningOverlay : OverlayWindow
{
    public override string Name => "Currency Warnings";
    public bool ShowOnlyWarnings { get; set; } = true;
    public bool CompactMode { get; set; } = true;
    
    public override void Draw()
    {
        var flags = ImGuiWindowFlags.NoDecoration | 
                   ImGuiWindowFlags.NoSavedSettings | 
                   ImGuiWindowFlags.NoFocusOnAppearing | 
                   ImGuiWindowFlags.NoNav |
                   ImGuiWindowFlags.AlwaysAutoResize |
                   ImGuiWindowFlags.NoMouseInputs;
        
        if (LockPosition)
            flags |= ImGuiWindowFlags.NoMove;
        else
            flags &= ~ImGuiWindowFlags.NoMouseInputs;
        
        ImGui.SetNextWindowPos(Position, ImGuiCond.FirstUseEver);
        ImGui.SetNextWindowBgAlpha(BackgroundAlpha);
        
        ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, new Vector2(8, 6) * Scale);
        ImGui.PushStyleVar(ImGuiStyleVar.WindowRounding, 5.0f);
        ImGui.PushStyleVar(ImGuiStyleVar.ItemSpacing, new Vector2(4, 2) * Scale);
        
        ImGuiHelpers.SetNextWindowPosRelativeMainViewport(Position);
        ImGui.SetNextWindowSize(Vector2.Zero); // Auto-size
        
        if (ImGui.Begin("##CurrencyWarningOverlay", flags))
        {
            ImGui.SetWindowFontScale(Scale);
            
            var hasContent = false;
            var modules = Plugin.Instance.ModuleManager.GetModules()
                .Where(m => m.Type == ModuleType.Currency && m.IsEnabled);
            
            foreach (var module in modules)
            {
                if (module.Status == ModuleStatus.InProgress || !ShowOnlyWarnings)
                {
                    DrawCurrencyWarning(module);
                    hasContent = true;
                }
            }
            
            if (!hasContent)
            {
                ImGui.TextColored(new Vector4(0.5f, 0.5f, 0.5f, 0.8f), "No warnings");
            }
            
            if (!LockPosition)
                Position = ImGui.GetWindowPos();
        }
        ImGui.End();
        
        ImGui.PopStyleVar(3);
    }
    
    private void DrawCurrencyWarning(IModule module)
    {
        // Get warning color based on severity
        var warningColor = new Vector4(1, 0.5f, 0, 1); // Orange for warnings
        
        if (CompactMode)
        {
            // Ultra compact: Icon + Name + Value
            ImGui.TextColored(warningColor, "!");
            ImGui.SameLine();
            ImGui.Text(module.Name.Length > 10 ? module.Name[..10] + ".." : module.Name);
            
            // Show first warning value
            if (module is Modules.Currency.TomestoneModule tomestones)
            {
                var currency = tomestones.GetTrackedCurrencies()
                    .FirstOrDefault(c => c.Enabled && c.HasWarning);
                if (currency != null)
                {
                    ImGui.SameLine();
                    ImGui.TextColored(warningColor, $"{currency.CurrentCount:N0}");
                }
            }
        }
        else
        {
            // Normal mode with more detail
            module.DrawStatus();
        }
    }
    
    public override void DrawConfig()
    {
        base.DrawConfig();
        ImGui.Checkbox($"Only Show Warnings##{Name}", ref ShowOnlyWarnings);
        ImGui.Checkbox($"Compact Mode##{Name}", ref CompactMode);
    }
}

public class CompactTaskOverlay : OverlayWindow
{
    public override string Name => "Active Tasks";
    public bool ShowDaily { get; set; } = true;
    public bool ShowWeekly { get; set; } = true;
    public bool HideCompleted { get; set; } = true;
    public int MaxItems { get; set; } = 5;
    
    public override void Draw()
    {
        var flags = ImGuiWindowFlags.NoDecoration | 
                   ImGuiWindowFlags.NoSavedSettings | 
                   ImGuiWindowFlags.NoFocusOnAppearing | 
                   ImGuiWindowFlags.NoNav |
                   ImGuiWindowFlags.AlwaysAutoResize |
                   ImGuiWindowFlags.NoMouseInputs;
        
        if (LockPosition)
            flags |= ImGuiWindowFlags.NoMove;
        else
            flags &= ~ImGuiWindowFlags.NoMouseInputs;
        
        ImGui.SetNextWindowPos(Position, ImGuiCond.FirstUseEver);
        ImGui.SetNextWindowBgAlpha(BackgroundAlpha);
        
        ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, new Vector2(8, 6) * Scale);
        ImGui.PushStyleVar(ImGuiStyleVar.WindowRounding, 5.0f);
        ImGui.PushStyleVar(ImGuiStyleVar.ItemSpacing, new Vector2(4, 2) * Scale);
        
        ImGuiHelpers.SetNextWindowPosRelativeMainViewport(Position);
        
        if (ImGui.Begin("##TaskOverlay", flags))
        {
            ImGui.SetWindowFontScale(Scale);
            
            var tasks = GetActiveTasks().Take(MaxItems);
            
            if (!tasks.Any())
            {
                ImGui.TextColored(new Vector4(0.5f, 0.5f, 0.5f, 0.8f), "All tasks complete!");
            }
            else
            {
                foreach (var task in tasks)
                {
                    DrawCompactTask(task);
                }
            }
            
            if (!LockPosition)
                Position = ImGui.GetWindowPos();
        }
        ImGui.End();
        
        ImGui.PopStyleVar(3);
    }
    
    private IEnumerable<IModule> GetActiveTasks()
    {
        var modules = Plugin.Instance.ModuleManager.GetModules()
            .Where(m => m.IsEnabled);
        
        if (ShowDaily)
            modules = modules.Where(m => m.Type == ModuleType.Daily || !ShowWeekly);
        if (ShowWeekly)
            modules = modules.Where(m => m.Type == ModuleType.Weekly || !ShowDaily);
        if (HideCompleted)
            modules = modules.Where(m => m.Status != ModuleStatus.Complete);
        
        return modules.OrderBy(m => m.Status == ModuleStatus.Complete)
                     .ThenBy(m => m.Type)
                     .ThenBy(m => m.Name);
    }
    
    private void DrawCompactTask(IModule module)
    {
        var color = module.Status switch
        {
            ModuleStatus.Complete => new Vector4(0.3f, 0.8f, 0.3f, 0.8f),
            ModuleStatus.InProgress => new Vector4(1, 0.8f, 0.2f, 1),
            ModuleStatus.Incomplete => new Vector4(0.8f, 0.3f, 0.3f, 1),
            _ => new Vector4(0.5f, 0.5f, 0.5f, 0.8f)
        };
        
        var icon = module.Status switch
        {
            ModuleStatus.Complete => "✓",
            ModuleStatus.InProgress => "◐",
            ModuleStatus.Incomplete => "○",
            _ => "?"
        };
        
        var typeIcon = module.Type switch
        {
            ModuleType.Daily => "D",
            ModuleType.Weekly => "W",
            _ => ""
        };
        
        ImGui.TextColored(color, icon);
        ImGui.SameLine();
        if (!string.IsNullOrEmpty(typeIcon))
        {
            ImGui.TextColored(new Vector4(0.6f, 0.6f, 0.6f, 0.8f), $"[{typeIcon}]");
            ImGui.SameLine();
        }
        ImGui.Text(module.Name.Length > 15 ? module.Name[..15] + ".." : module.Name);
    }
    
    public override void DrawConfig()
    {
        base.DrawConfig();
        ImGui.Checkbox($"Show Daily Tasks##{Name}", ref ShowDaily);
        ImGui.Checkbox($"Show Weekly Tasks##{Name}", ref ShowWeekly);
        ImGui.Checkbox($"Hide Completed##{Name}", ref HideCompleted);
        ImGui.SetNextItemWidth(100);
        ImGui.SliderInt($"Max Items##{Name}", ref MaxItems, 1, 10);
    }
}
