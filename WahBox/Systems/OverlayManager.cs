using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using ImGuiNET;
using Dalamud.Interface.Utility;
using Dalamud.Interface;
using WahBox.Core.Interfaces;

namespace WahBox.Systems;

public class OverlayManager : IDisposable
{
    private readonly Plugin _plugin;
    private readonly UnifiedOverlay _overlay;
    private bool _isEnabled = true;

    public OverlayManager(Plugin plugin)
    {
        _plugin = plugin;
        _overlay = new UnifiedOverlay(plugin);
    }

    public void Initialize()
    {
        Plugin.PluginInterface.UiBuilder.Draw += DrawOverlay;
    }
    
    private void DrawOverlay()
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
        
        _overlay.Draw();
    }
    
    public void ToggleOverlay()
    {
        _isEnabled = !_isEnabled;
    }
    
    public void RefreshOverlay()
    {
        _overlay.Refresh();
    }
    
    public void DrawConfiguration()
    {
        ImGui.Checkbox("Enable Overlay", ref _isEnabled);
        
        if (_isEnabled)
        {
            ImGui.Separator();
            _overlay.DrawConfig();
        }
    }
    
    public void Dispose()
    {
        Plugin.PluginInterface.UiBuilder.Draw -= DrawOverlay;
        _overlay.Dispose();
    }
}

public class UnifiedOverlay : IDisposable
{
    public string Name => "Wahdori Overlay";
    public bool IsMoveable { get; set; } = true;
    protected Plugin Plugin { get; }
    protected Vector2 Position { get; set; }
    
    public UnifiedOverlay(Plugin plugin)
    {
        Plugin = plugin;
        Position = new Vector2(10, 100);
    }
    
    public void Draw()
    {
        var flags = ImGuiWindowFlags.NoDecoration | ImGuiWindowFlags.AlwaysAutoResize | 
                   ImGuiWindowFlags.NoFocusOnAppearing | ImGuiWindowFlags.NoNav;
                   
        if (!IsMoveable)
        {
            flags |= ImGuiWindowFlags.NoMove;
        }
        
        // Use SetNextWindowBgAlpha instead of pushing style colors
        if (Plugin.Configuration.OverlaySettings.ShowBackground)
        {
            ImGui.SetNextWindowBgAlpha(1.0f);
        }
        else
        {
            ImGui.SetNextWindowBgAlpha(0.0f);
            flags |= ImGuiWindowFlags.NoBackground;
        }
        
        ImGui.PushStyleVar(ImGuiStyleVar.WindowBorderSize, 0.0f);
        ImGui.PushStyleVar(ImGuiStyleVar.WindowRounding, 5.0f);
        ImGui.SetNextWindowPos(Position, ImGuiCond.FirstUseEver);
        
        var isOpen = ImGui.Begin("##WahdoriOverlay", flags);
        if (isOpen)
        {
            if (IsMoveable)
            {
                Position = ImGui.GetWindowPos();
            }
            
            DrawContent();
        }
        ImGui.End();
        
        ImGui.PopStyleVar(2);
    }
    
    private void DrawContent()
    {
        var showCurrencies = Plugin.Configuration.OverlaySettings.ShowCurrencyWarnings;
        var showDaily = Plugin.Configuration.OverlaySettings.ShowDailyTasks;
        var showWeekly = Plugin.Configuration.OverlaySettings.ShowWeeklyTasks;
        
        if (!showCurrencies && !showDaily && !showWeekly)
        {
            ImGui.TextColored(new Vector4(0.5f, 0.5f, 0.5f, 1), "No overlay items enabled");
            return;
        }
        
        bool firstSection = true;
        
        // Currency Warnings
        if (showCurrencies)
        {
            DrawCurrencyWarnings();
            firstSection = false;
        }
        
        // Daily Tasks
        if (showDaily)
        {
            if (!firstSection) 
            {
                ImGui.Spacing();
                ImGui.Separator();
                ImGui.Spacing();
            }
            DrawTaskSection("Daily Tasks", ModuleType.Daily);
            firstSection = false;
        }
        
        // Weekly Tasks
        if (showWeekly)
        {
            if (!firstSection) 
            {
                ImGui.Spacing();
                ImGui.Separator();
                ImGui.Spacing();
            }
            DrawTaskSection("Weekly Tasks", ModuleType.Weekly);
        }
    }
    
    private void DrawCurrencyWarnings()
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
        
        if (Plugin.Configuration.OverlaySettings.ShowText)
        {
            ImGui.TextColored(new Vector4(1, 0.5f, 0, 1), "Currency Warnings:");
        }
        
        foreach (var currency in currencies)
        {
            var maxDisplay = currency.MaxCount > 0 ? currency.MaxCount : currency.Threshold;
            var percentage = (float)currency.CurrentCount / maxDisplay * 100;
            
            // Currency icon
            if (currency.Icon != null)
            {
                try
                {
                    ImGui.Image(currency.Icon.ImGuiHandle, new Vector2(ImGui.GetTextLineHeight(), ImGui.GetTextLineHeight()));
                    ImGui.SameLine();
                }
                catch
                {
                    // Icon failed, just continue without it
                }
            }
            
            if (Plugin.Configuration.OverlaySettings.ShowText)
            {
                ImGui.Text($"{currency.Name}:");
                ImGui.SameLine();
                ImGui.TextColored(new Vector4(1, 0.5f, 0, 1), 
                    $"{currency.CurrentCount:N0}/{maxDisplay:N0} ({percentage:F0}%)");
            }
            else
            {
                // Just show the numbers when text is disabled
                ImGui.TextColored(new Vector4(1, 0.5f, 0, 1), 
                    $"{currency.CurrentCount:N0}/{maxDisplay:N0}");
            }
        }
    }
    
    private void DrawTaskSection(string title, ModuleType type)
    {
        if (Plugin.Configuration.OverlaySettings.ShowText)
        {
            ImGui.TextColored(new Vector4(0.7f, 0.7f, 0.7f, 1), title);
        }
        
        var tasks = Plugin.ModuleManager.GetModules()
            .Where(m => m.Type == type && m.IsEnabled && m.Status != ModuleStatus.Complete)
            .OrderBy(m => m.Status)
            .ToList();
            
        if (!tasks.Any())
        {
            if (Plugin.Configuration.OverlaySettings.ShowText)
            {
                ImGui.TextColored(new Vector4(0.2f, 0.8f, 0.2f, 1), "  All complete!");
            }
            else
            {
                // Show a checkmark when all complete
                ImGui.PushFont(UiBuilder.IconFont);
                ImGui.TextColored(new Vector4(0.2f, 0.8f, 0.2f, 1), ((char)FontAwesomeIcon.CheckCircle).ToString());
                ImGui.PopFont();
            }
        }
        else
        {
            foreach (var task in tasks)
            {
                var color = task.Status switch
                {
                    ModuleStatus.InProgress => new Vector4(1, 1, 0, 1),
                    ModuleStatus.Incomplete => new Vector4(1, 0.2f, 0.2f, 1),
                    _ => new Vector4(0.7f, 0.7f, 0.7f, 1)
                };
                
                if (Plugin.Configuration.OverlaySettings.ShowText)
                {
                    ImGui.Text("  ");
                    ImGui.SameLine();
                }
                
                // Module icon
                if (task.IconId > 0)
                {
                    try
                    {
                        var icon = Plugin.TextureProvider.GetFromGameIcon(task.IconId).GetWrapOrEmpty();
                        if (icon != null)
                        {
                            ImGui.Image(icon.ImGuiHandle, new Vector2(ImGui.GetTextLineHeight(), ImGui.GetTextLineHeight()));
                        }
                        else
                        {
                            // Fallback to font icon
                            ImGui.PushFont(UiBuilder.IconFont);
                            ImGui.Text(((char)FontAwesomeIcon.Tasks).ToString());
                            ImGui.PopFont();
                        }
                    }
                    catch
                    {
                        // If icon fails to load, use font icon
                        ImGui.PushFont(UiBuilder.IconFont);
                        ImGui.Text(((char)FontAwesomeIcon.Tasks).ToString());
                        ImGui.PopFont();
                    }
                }
                else
                {
                    // Use font icon if no specific icon
                    ImGui.PushFont(UiBuilder.IconFont);
                    ImGui.Text(((char)FontAwesomeIcon.Tasks).ToString());
                    ImGui.PopFont();
                }
                
                if (Plugin.Configuration.OverlaySettings.ShowText)
                {
                    ImGui.SameLine();
                    ImGui.TextColored(color, task.Name);
                }
                else
                {
                    // Show status indicator when text is disabled
                    ImGui.SameLine();
                    ImGui.PushFont(UiBuilder.IconFont);
                    var statusIcon = task.Status switch
                    {
                        ModuleStatus.InProgress => FontAwesomeIcon.ExclamationTriangle,
                        ModuleStatus.Incomplete => FontAwesomeIcon.Circle,
                        _ => FontAwesomeIcon.Question
                    };
                    ImGui.TextColored(color, ((char)statusIcon).ToString());
                    ImGui.PopFont();
                }
            }
        }
    }
    
    public void DrawConfig()
    {
        var moveable = IsMoveable;
        if (ImGui.Checkbox("Allow Moving", ref moveable))
        {
            IsMoveable = moveable;
        }
        
        ImGui.SameLine();
        if (ImGui.Button("Reset Position"))
        {
            Position = new Vector2(10, 100);
        }
        
        ImGui.Separator();
        ImGui.Text("Display Options:");
        
        var showBg = Plugin.Configuration.OverlaySettings.ShowBackground;
        if (ImGui.Checkbox("Show Background", ref showBg))
        {
            Plugin.Configuration.OverlaySettings.ShowBackground = showBg;
            Plugin.Configuration.Save();
        }
        
        var showText = Plugin.Configuration.OverlaySettings.ShowText;
        if (ImGui.Checkbox("Show Text Labels", ref showText))
        {
            Plugin.Configuration.OverlaySettings.ShowText = showText;
            Plugin.Configuration.Save();
        }
        
        var opacity = Plugin.Configuration.OverlaySettings.Opacity;
        if (ImGui.SliderFloat("Opacity", ref opacity, 0.1f, 1.0f))
        {
            Plugin.Configuration.OverlaySettings.Opacity = opacity;
            Plugin.Configuration.Save();
        }
        
        ImGui.Separator();
        ImGui.Text("Content to Display:");
        
        var showCurrencies = Plugin.Configuration.OverlaySettings.ShowCurrencyWarnings;
        if (ImGui.Checkbox("Currency Warnings", ref showCurrencies))
        {
            Plugin.Configuration.OverlaySettings.ShowCurrencyWarnings = showCurrencies;
            Plugin.Configuration.Save();
        }
        
        var showDaily = Plugin.Configuration.OverlaySettings.ShowDailyTasks;
        if (ImGui.Checkbox("Daily Tasks", ref showDaily))
        {
            Plugin.Configuration.OverlaySettings.ShowDailyTasks = showDaily;
            Plugin.Configuration.Save();
        }
        
        var showWeekly = Plugin.Configuration.OverlaySettings.ShowWeeklyTasks;
        if (ImGui.Checkbox("Weekly Tasks", ref showWeekly))
        {
            Plugin.Configuration.OverlaySettings.ShowWeeklyTasks = showWeekly;
            Plugin.Configuration.Save();
        }
    }
    
    public void Refresh() { }
    public void Dispose() { }
} 