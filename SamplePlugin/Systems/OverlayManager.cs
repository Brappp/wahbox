using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using ImGuiNET;
using Dalamud.Interface.Utility;
using Dalamud.Interface;
using SamplePlugin.Core.Interfaces;

namespace SamplePlugin.Systems;

public class OverlayManager : IDisposable
{
    private readonly List<OverlayWindow> _overlayWindows = new();
    private readonly Plugin _plugin;
    private bool _isEnabled = true;

    public OverlayManager(Plugin plugin)
    {
        _plugin = plugin;
    }

    public void Initialize()
    {
        _overlayWindows.Add(new CompactCurrencyOverlay(_plugin));
        _overlayWindows.Add(new CompactTaskOverlay(_plugin));
        
        Plugin.PluginInterface.UiBuilder.Draw += DrawOverlays;
    }
    
    private void DrawOverlays()
    {
        if (!_isEnabled || !Plugin.ClientState.IsLoggedIn) return;
        
        // Hide overlays in certain conditions
        if (Plugin.Condition[Dalamud.Game.ClientState.Conditions.ConditionFlag.InCombat] && 
            _plugin.Configuration.UISettings.HideInCombat)
            return;
            
        if (Plugin.Condition[Dalamud.Game.ClientState.Conditions.ConditionFlag.BoundByDuty] && 
            _plugin.Configuration.UISettings.HideInDuty)
            return;
            
        if (Plugin.Condition[Dalamud.Game.ClientState.Conditions.ConditionFlag.BetweenAreas] ||
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
    
    public void RefreshAll()
    {
        foreach (var overlay in _overlayWindows)
        {
            overlay.Refresh();
        }
    }
    
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
    public bool IsMoveable { get; set; } = false;
    protected Plugin Plugin { get; }
    protected Vector2 Position { get; set; }
    protected Vector2 Size { get; set; }
    
    protected OverlayWindow(Plugin plugin)
    {
        Plugin = plugin;
        Position = new Vector2(100, 100);
        Size = new Vector2(250, 100);
    }
    
    public abstract void Draw();
    public virtual void DrawConfig()
    {
        var enabled = IsEnabled;
        if (ImGui.Checkbox($"Enable {Name}", ref enabled))
        {
            IsEnabled = enabled;
        }
        
        var moveable = IsMoveable;
        if (ImGui.Checkbox("Allow Moving", ref moveable))
        {
            IsMoveable = moveable;
        }
        
        if (ImGui.Button("Reset Position"))
        {
            Position = new Vector2(100, 100);
        }
    }
    
    public virtual void Refresh() { }
    public virtual void Dispose() { }
    
    protected void DrawWindow(string id, Action contentAction)
    {
        var flags = ImGuiWindowFlags.NoDecoration | ImGuiWindowFlags.AlwaysAutoResize | 
                   ImGuiWindowFlags.NoFocusOnAppearing | ImGuiWindowFlags.NoNav;
                   
        if (!IsMoveable)
        {
            flags |= ImGuiWindowFlags.NoMove;
        }
        
        if (Plugin.Configuration.OverlaySettings.ShowBackground)
        {
            ImGui.PushStyleColor(ImGuiCol.WindowBg, new Vector4(0, 0, 0, 0.4f));
        }
        else
        {
            ImGui.PushStyleColor(ImGuiCol.WindowBg, Vector4.Zero);
            flags |= ImGuiWindowFlags.NoBackground;
        }
        
        ImGui.PushStyleVar(ImGuiStyleVar.WindowBorderSize, 0.0f);
        ImGui.PushStyleVar(ImGuiStyleVar.WindowRounding, 5.0f);
        ImGui.SetNextWindowPos(Position, ImGuiCond.FirstUseEver);
        
        if (ImGui.Begin(id, flags))
        {
            if (IsMoveable)
            {
                Position = ImGui.GetWindowPos();
            }
            
            contentAction();
        }
        ImGui.End();
        
        ImGui.PopStyleVar(2);
        ImGui.PopStyleColor();
    }
}

public class CompactCurrencyOverlay : OverlayWindow
{
    public override string Name => "Currency Warnings";
    
    public CompactCurrencyOverlay(Plugin plugin) : base(plugin)
    {
        Position = new Vector2(10, 100);
    }
    
    public override void Draw()
    {
        if (!Plugin.Configuration.OverlaySettings.ShowCurrencyWarnings) return;
        
        DrawWindow("##CurrencyOverlay", () =>
        {
            var currencies = Plugin.ModuleManager.GetModules()
                .Where(m => m.Type == ModuleType.Currency && m.IsEnabled)
                .OfType<ICurrencyModule>()
                .SelectMany(m => m.GetTrackedCurrencies())
                .Where(c => c.Enabled && c.HasWarning)
                .ToList();
                
            if (!currencies.Any())
            {
                ImGui.TextColored(new Vector4(0.5f, 0.5f, 0.5f, 1), "No currency warnings");
                return;
            }
            
            ImGui.TextColored(new Vector4(1, 0.5f, 0, 1), "Currency Warnings:");
            ImGui.Separator();
            
            foreach (var currency in currencies)
            {
                var maxDisplay = currency.MaxCount > 0 ? currency.MaxCount : currency.Threshold;
                var percentage = (float)currency.CurrentCount / maxDisplay * 100;
                ImGui.Text($"{currency.Name}:");
                ImGui.SameLine();
                ImGui.TextColored(new Vector4(1, 0.5f, 0, 1), 
                    $"{currency.CurrentCount:N0}/{maxDisplay:N0} ({percentage:F0}%)");
            }
        });
    }
}

public class CompactTaskOverlay : OverlayWindow
{
    public override string Name => "Task Tracker";
    
    public CompactTaskOverlay(Plugin plugin) : base(plugin)
    {
        Position = new Vector2(10, 250);
    }
    
    public override void Draw()
    {
        DrawWindow("##TaskOverlay", () =>
        {
            var showDaily = Plugin.Configuration.OverlaySettings.ShowDailyTasks;
            var showWeekly = Plugin.Configuration.OverlaySettings.ShowWeeklyTasks;
            
            if (!showDaily && !showWeekly)
            {
                ImGui.TextColored(new Vector4(0.5f, 0.5f, 0.5f, 1), "Task tracking disabled");
                return;
            }
            
            if (showDaily)
            {
                DrawTaskSection("Daily Tasks", ModuleType.Daily);
            }
            
            if (showWeekly)
            {
                if (showDaily) ImGui.Spacing();
                DrawTaskSection("Weekly Tasks", ModuleType.Weekly);
            }
        });
    }
    
    private void DrawTaskSection(string title, ModuleType type)
    {
        ImGui.TextColored(new Vector4(0.7f, 0.7f, 0.7f, 1), title);
        
        var tasks = Plugin.ModuleManager.GetModules()
            .Where(m => m.Type == type && m.IsEnabled && m.Status != ModuleStatus.Complete)
            .OrderBy(m => m.Status)
            .ToList();
            
        if (!tasks.Any())
        {
            ImGui.TextColored(new Vector4(0.2f, 0.8f, 0.2f, 1), "  All complete!");
        }
        else
        {
            foreach (var task in tasks)
            {
                var icon = task.Status switch
                {
                    ModuleStatus.InProgress => FontAwesomeIcon.ExclamationTriangle,
                    ModuleStatus.Incomplete => FontAwesomeIcon.Circle,
                    _ => FontAwesomeIcon.Question
                };
                
                var color = task.Status switch
                {
                    ModuleStatus.InProgress => new Vector4(1, 1, 0, 1),
                    ModuleStatus.Incomplete => new Vector4(1, 0.2f, 0.2f, 1),
                    _ => new Vector4(0.7f, 0.7f, 0.7f, 1)
                };
                
                ImGui.Text("  ");
                ImGui.SameLine();
                ImGui.PushFont(UiBuilder.IconFont);
                ImGui.TextColored(color, ((char)icon).ToString());
                ImGui.PopFont();
                ImGui.SameLine();
                ImGui.TextColored(color, task.Name);
            }
        }
    }
} 