using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Dalamud.Interface.Textures;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Windowing;
using Dalamud.Interface.Components;
using Dalamud.Interface;
using ImGuiNET;
using WahBox.Core.Interfaces;

namespace WahBox.Windows;

public class MainWindow : Window, IDisposable
{
    private string GoatImagePath;
    private ISharedImmediateTexture? GoatTexture;
    private Plugin PluginInstance;
    
    private string _searchFilter = string.Empty;
    private bool _showCompleted = true;
    private ModuleType? _filterType = null;

    public MainWindow(Plugin plugin, string goatImagePath) : base("Wahdori##Main")
    {
        GoatImagePath = goatImagePath;
        PluginInstance = plugin;
        
        // Load the texture once using the shared texture system
        GoatTexture = Plugin.TextureProvider.GetFromFile(GoatImagePath);
        
        // Let the user resize as they want
        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(280, 200),
            MaximumSize = new Vector2(float.MaxValue, float.MaxValue)
        };
        
        // Remove flags that prevent normal window behavior
        Flags = ImGuiWindowFlags.None;
        
        // Add title bar button for settings
        TitleBarButtons = new List<Window.TitleBarButton>
        {
            new Window.TitleBarButton
            {
                Icon = FontAwesomeIcon.Cog,
                Click = (msg) => PluginInstance.ToggleConfigUI(),
                IconOffset = new Vector2(2, 1),
                ShowTooltip = () => ImGui.SetTooltip("Settings")
            }
        };
    }

    public void Dispose()
    {
        // No need to dispose shared textures - Dalamud manages them
    }

    public override void Draw()
    {
        try
        {
            DrawHeader();
            ImGui.Separator();
            DrawContent();
        }
        catch (Exception ex)
        {
            ImGui.TextColored(new Vector4(1, 0, 0, 1), "Critical error in window draw");
            ImGui.Text($"Error: {ex.Message}");
            Plugin.Log.Error(ex, "Critical error in MainWindow.Draw");
        }
    }

    private void DrawHeader()
    {
        try
        {
            // Compact filter bar
            ImGui.SetNextItemWidth(120);
            ImGui.InputTextWithHint("##Search", "Search...", ref _searchFilter, 50);
            
            ImGui.SameLine();
            ImGui.SetNextItemWidth(120);
            if (ImGui.BeginCombo("##Filter", _filterType?.ToString() ?? "All"))
            {
                if (ImGui.Selectable("All", _filterType == null))
                    _filterType = null;
                
                foreach (ModuleType type in Enum.GetValues<ModuleType>())
                {
                    if (ImGui.Selectable(type.ToString(), _filterType == type))
                        _filterType = type;
                }
                ImGui.EndCombo();
            }
            
            ImGui.SameLine();
            ImGui.Checkbox("Show Complete", ref _showCompleted);
        }
        catch (Exception ex)
        {
            ImGui.TextColored(new Vector4(1, 0, 0, 1), "Error in header");
            Plugin.Log.Error(ex, "Error in DrawHeader");
        }
    }

    private void DrawContent()
    {
        var availableHeight = ImGui.GetContentRegionAvail().Y;
        
        if (ImGui.BeginChild("ModuleList", new Vector2(0, availableHeight), false))
        {
            try
            {
                var allModules = PluginInstance.ModuleManager?.GetModules();
                if (allModules == null)
                {
                    ImGui.TextColored(new Vector4(1, 0, 0, 1), "Module manager not initialized");
                    ImGui.EndChild();
                    return;
                }
                
                var modules = allModules
                    .Where(m => m != null && m.IsEnabled)
                    .Where(m => _filterType == null || m.Type == _filterType)
                    .Where(m => string.IsNullOrEmpty(_searchFilter) || 
                               m.Name.Contains(_searchFilter, StringComparison.OrdinalIgnoreCase))
                    .Where(m => _showCompleted || m.Status != ModuleStatus.Complete)
                    .OrderBy(m => m.Status == ModuleStatus.Complete)
                    .ThenBy(m => m.Type)
                    .ThenBy(m => m.Name)
                    .ToList();

                if (!modules.Any())
                {
                    ImGui.TextColored(new Vector4(0.5f, 0.5f, 0.5f, 1), "No modules to display");
                }
                else
                {
                    // Group by type for better organization
                    var grouped = modules.GroupBy(m => m.Type);
                    
                    foreach (var group in grouped)
                    {
                                            ImGui.PushStyleColor(ImGuiCol.Text, GetTypeColor(group.Key));
                    ImGui.PushFont(UiBuilder.IconFont);
                    ImGui.Text(GetTypeIcon(group.Key));
                    ImGui.PopFont();
                    ImGui.SameLine();
                    ImGui.Text(group.Key.ToString());
                    ImGui.PopStyleColor();
                        
                        ImGui.Indent();
                        foreach (var module in group)
                        {
                            DrawCompactModule(module);
                        }
                        ImGui.Unindent();
                        
                        ImGui.Spacing();
                    }
                }
            }
            catch (Exception ex)
            {
                ImGui.TextColored(new Vector4(1, 0, 0, 1), "Error displaying modules");
                ImGui.Text($"Details: {ex.Message}");
                Plugin.Log.Error(ex, "Error in DrawContent");
            }
        }
        ImGui.EndChild();
    }

    private void DrawCompactModule(IModule module)
    {
        var status = module.Status;
        var statusColor = GetStatusColor(status);
        
        ImGui.PushID(module.Name);
        
        // Create an invisible button for the entire row (for hover detection)
        var startPos = ImGui.GetCursorPos();
        ImGui.SetCursorPos(startPos);
        ImGui.InvisibleButton($"module_{module.Name}", new Vector2(ImGui.GetContentRegionAvail().X, ImGui.GetTextLineHeight()));
        var isHovered = ImGui.IsItemHovered();
        
        // Draw on top of the invisible button
        ImGui.SetCursorPos(startPos);
        
        // Module icon
        if (module.IconId > 0)
        {
            try
            {
                var icon = Plugin.TextureProvider.GetFromGameIcon(module.IconId).GetWrapOrEmpty();
                if (icon != null)
                {
                    ImGui.Image(icon.ImGuiHandle, new Vector2(ImGui.GetTextLineHeight(), ImGui.GetTextLineHeight()));
                }
                else
                {
                    // Fallback to type icon if texture fails
                    ImGui.PushFont(UiBuilder.IconFont);
                    ImGui.Text(GetTypeIcon(module.Type));
                    ImGui.PopFont();
                }
            }
            catch
            {
                // If icon fails to load, use type icon
                ImGui.PushFont(UiBuilder.IconFont);
                ImGui.Text(GetTypeIcon(module.Type));
                ImGui.PopFont();
            }
        }
        else
        {
            // Use type icon if no specific icon
            ImGui.PushFont(UiBuilder.IconFont);
            ImGui.Text(GetTypeIcon(module.Type));
            ImGui.PopFont();
        }
        
        ImGui.SameLine();
        ImGui.Text(module.Name);
        
        // Right-aligned status details
        var statusStartX = ImGui.GetContentRegionMax().X - 100;
        ImGui.SameLine(statusStartX);
        
        // Draw module-specific compact status
        try
        {
            DrawModuleCompactStatus(module);
        }
        catch (Exception ex)
        {
            ImGui.TextColored(new Vector4(1, 0, 0, 1), "Error");
            Plugin.Log.Error(ex, $"Error drawing compact status for module {module.Name}");
        }
        
        // Ensure we move to next line
        ImGui.Dummy(new Vector2(0, 0));
        
        ImGui.PopID();
        
        // Hover tooltip with more details
        if (isHovered)
        {
            ImGui.BeginTooltip();
            ImGui.Text($"{module.Name} ({module.Type})");
            ImGui.Separator();
            
            try
            {
                module.DrawStatus();
            }
            catch (Exception ex)
            {
                ImGui.TextColored(new Vector4(1, 0, 0, 1), $"Error: {ex.Message}");
                Plugin.Log.Error(ex, $"Error drawing tooltip status for module {module.Name}");
            }
            
            ImGui.EndTooltip();
        }
    }

    private void DrawModuleCompactStatus(IModule module)
    {
        switch (module.Type)
        {
            case ModuleType.Currency:
                // Show count/threshold for currencies
                if (module is ICurrencyModule currencyModule)
                {
                    var currencies = currencyModule.GetTrackedCurrencies();
                    var primary = currencies.FirstOrDefault(c => c.Enabled);
                    if (primary != null)
                    {
                        var color = primary.HasWarning ? new Vector4(1, 0.5f, 0, 1) : GetStatusColor(module.Status);
                        var maxDisplay = primary.MaxCount > 0 ? primary.MaxCount : primary.Threshold;
                        ImGui.TextColored(color, $"{primary.CurrentCount:N0}/{maxDisplay:N0}");
                    }
                }
                break;
                
            case ModuleType.Daily:
            case ModuleType.Weekly:
                // Show simple status text with icon
                if (module.Status == ModuleStatus.Complete)
                {
                    ImGui.PushFont(UiBuilder.IconFont);
                    ImGui.TextColored(GetStatusColor(module.Status), ((char)FontAwesomeIcon.Check).ToString());
                    ImGui.PopFont();
                }
                else
                {
                    var statusText = module.Status switch
                    {
                        ModuleStatus.InProgress => "In Progress",
                        ModuleStatus.Incomplete => "Not Started",
                        _ => "?"
                    };
                    ImGui.TextColored(GetStatusColor(module.Status), statusText);
                }
                break;
                
            case ModuleType.Special:
                // Show custom status
                ImGui.TextColored(GetStatusColor(module.Status), GetStatusText(module.Status));
                break;
        }
    }

    private string GetTypeIcon(ModuleType type)
    {
        var icon = type switch
        {
            ModuleType.Currency => FontAwesomeIcon.Coins,
            ModuleType.Daily => FontAwesomeIcon.CalendarDay,
            ModuleType.Weekly => FontAwesomeIcon.CalendarWeek,
            ModuleType.Special => FontAwesomeIcon.Star,
            _ => FontAwesomeIcon.List
        };
        return ((char)icon).ToString();
    }

    private string GetStatusIcon(ModuleStatus status)
    {
        var icon = status switch
        {
            ModuleStatus.Complete => FontAwesomeIcon.CheckCircle,
            ModuleStatus.InProgress => FontAwesomeIcon.ExclamationTriangle,
            ModuleStatus.Incomplete => FontAwesomeIcon.Circle,
            _ => FontAwesomeIcon.Question
        };
        return ((char)icon).ToString();
    }

    private Vector4 GetTypeColor(ModuleType type)
    {
        return type switch
        {
            ModuleType.Currency => new Vector4(0.8f, 0.7f, 0.2f, 1),
            ModuleType.Daily => new Vector4(0.2f, 0.7f, 0.8f, 1),
            ModuleType.Weekly => new Vector4(0.7f, 0.2f, 0.8f, 1),
            ModuleType.Special => new Vector4(0.8f, 0.2f, 0.5f, 1),
            _ => new Vector4(0.7f, 0.7f, 0.7f, 1)
        };
    }

    private Vector4 GetStatusColor(ModuleStatus status, float alpha = 1.0f)
    {
        return status switch
        {
            ModuleStatus.Complete => new Vector4(0.2f, 0.8f, 0.2f, alpha),
            ModuleStatus.InProgress => new Vector4(0.8f, 0.8f, 0.2f, alpha),
            ModuleStatus.Incomplete => new Vector4(0.8f, 0.2f, 0.2f, alpha),
            _ => new Vector4(0.7f, 0.7f, 0.7f, alpha)
        };
    }

    private string GetStatusText(ModuleStatus status)
    {
        return status switch
        {
            ModuleStatus.Complete => "Complete",
            ModuleStatus.InProgress => "In Progress",
            ModuleStatus.Incomplete => "Incomplete",
            _ => "Unknown"
        };
    }
}
