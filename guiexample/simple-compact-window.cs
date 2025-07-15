using System;
using System.Linq;
using System.Numerics;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Windowing;
using Dalamud.Interface.Components;
using ImGuiNET;
using SamplePlugin.Core.Interfaces;

namespace SamplePlugin.Windows;

public class MainWindow : Window, IDisposable
{
    private Plugin Plugin;
    private string _searchFilter = string.Empty;
    private bool _showCompleted = true;
    private ModuleType? _filterType = null;

    public MainWindow(Plugin plugin, string goatImagePath) : base("Wahdori##Main")
    {
        Plugin = plugin;
        
        // Small, focused window size
        Size = new Vector2(320, 450);
        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(280, 350),
            MaximumSize = new Vector2(400, 600)
        };
    }

    public void Dispose() { }

    public override void Draw()
    {
        DrawHeader();
        ImGui.Separator();
        DrawContent();
    }

    private void DrawHeader()
    {
        // Title and quick stats on same line
        ImGui.Text("Wahdori");
        
        ImGui.SameLine();
        var modules = Plugin.ModuleManager.GetModules().Where(m => m.IsEnabled).ToList();
        var warnings = modules.Count(m => m.Type == ModuleType.Currency && m.Status == ModuleStatus.InProgress);
        if (warnings > 0)
        {
            ImGui.TextColored(new Vector4(1, 0.5f, 0, 1), $"({warnings} warnings)");
        }
        
        // Settings button on the right
        ImGui.SameLine(ImGui.GetContentRegionMax().X - 25);
        if (ImGuiComponents.IconButton(Dalamud.Interface.FontAwesomeIcon.Cog))
        {
            Plugin.ToggleConfigUI();
        }
        if (ImGui.IsItemHovered())
            ImGui.SetTooltip("Settings");
        
        // Compact filter bar
        ImGui.SetNextItemWidth(100);
        ImGui.InputTextWithHint("##Search", "Search...", ref _searchFilter, 50);
        
        ImGui.SameLine();
        ImGui.SetNextItemWidth(100);
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
        ImGui.Checkbox("Complete", ref _showCompleted);
    }

    private void DrawContent()
    {
        var availableHeight = ImGui.GetContentRegionAvail().Y;
        
        if (ImGui.BeginChild("ModuleList", new Vector2(0, availableHeight), false))
        {
            var modules = Plugin.ModuleManager.GetModules()
                .Where(m => m.IsEnabled)
                .Where(m => _filterType == null || m.Type == _filterType)
                .Where(m => string.IsNullOrEmpty(_searchFilter) || 
                           m.Name.Contains(_searchFilter, StringComparison.OrdinalIgnoreCase))
                .Where(m => _showCompleted || m.Status != ModuleStatus.Complete)
                .OrderBy(m => m.Status == ModuleStatus.Complete)
                .ThenBy(m => m.Type)
                .ThenBy(m => m.Name);

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
                    ImGui.Text(GetTypeIcon(group.Key) + " " + group.Key.ToString());
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
        ImGui.EndChild();
    }

    private void DrawCompactModule(IModule module)
    {
        var status = module.Status;
        var statusColor = GetStatusColor(status);
        var statusIcon = GetStatusIcon(status);
        
        // Module name with status icon
        ImGui.TextColored(statusColor, statusIcon);
        ImGui.SameLine();
        ImGui.Text(module.Name);
        
        // Right-aligned status details
        ImGui.SameLine(ImGui.GetContentRegionMax().X - 100);
        
        // Draw module-specific compact status
        ImGui.PushID(module.Name);
        ImGui.BeginGroup();
        
        try
        {
            // Currency modules show count/threshold
            if (module.Type == ModuleType.Currency)
            {
                if (module is Modules.Currency.TomestoneModule tomestones)
                {
                    var currencies = tomestones.GetTrackedCurrencies();
                    foreach (var currency in currencies.Where(c => c.Enabled && c.HasWarning))
                    {
                        ImGui.TextColored(statusColor, $"{currency.CurrentCount:N0}/{currency.Threshold:N0}");
                        break; // Only show first warning
                    }
                }
                else if (module is Modules.Currency.GrandCompanyModule gc)
                {
                    var currencies = gc.GetTrackedCurrencies();
                    var active = currencies.FirstOrDefault(c => c.Enabled && c.CurrentCount > 0);
                    if (active != null)
                    {
                        var color = active.HasWarning ? new Vector4(1, 0.5f, 0, 1) : new Vector4(0.7f, 0.7f, 0.7f, 1);
                        ImGui.TextColored(color, $"{active.CurrentCount:N0}/{active.Threshold:N0}");
                    }
                }
                // Add similar compact displays for other currency modules
            }
            // Daily/Weekly modules show completion status
            else if (module.Type == ModuleType.Daily || module.Type == ModuleType.Weekly)
            {
                if (module is Modules.Daily.DutyRouletteModule roulette)
                {
                    // Show completed count
                    ImGui.TextColored(statusColor, GetStatusText(status));
                }
                else if (module is Modules.Weekly.WondrousTailsModule wt)
                {
                    ImGui.TextColored(statusColor, GetStatusText(status));
                }
                else
                {
                    ImGui.TextColored(statusColor, GetStatusText(status));
                }
            }
        }
        catch
        {
            ImGui.TextColored(new Vector4(0.5f, 0.5f, 0.5f, 1), "---");
        }
        
        ImGui.EndGroup();
        ImGui.PopID();
        
        // Hover tooltip with more details
        if (ImGui.IsItemHovered())
        {
            ImGui.BeginTooltip();
            ImGui.Text($"{module.Name} ({module.Type})");
            ImGui.Separator();
            
            try
            {
                module.DrawStatus();
            }
            catch
            {
                ImGui.TextColored(new Vector4(1, 0, 0, 1), "Error displaying status");
            }
            
            ImGui.EndTooltip();
        }
    }

    private string GetTypeIcon(ModuleType type) => type switch
    {
        ModuleType.Currency => "◉",
        ModuleType.Daily => "◐",
        ModuleType.Weekly => "◈",
        ModuleType.Special => "◆",
        _ => "•"
    };

    private Vector4 GetTypeColor(ModuleType type) => type switch
    {
        ModuleType.Currency => new Vector4(1, 0.8f, 0.3f, 1),
        ModuleType.Daily => new Vector4(0.3f, 0.8f, 1, 1),
        ModuleType.Weekly => new Vector4(0.8f, 0.3f, 1, 1),
        ModuleType.Special => new Vector4(0.3f, 1, 0.8f, 1),
        _ => new Vector4(1, 1, 1, 1)
    };

    private string GetStatusIcon(ModuleStatus status) => status switch
    {
        ModuleStatus.Complete => "✓",
        ModuleStatus.InProgress => "!",
        ModuleStatus.Incomplete => "○",
        _ => "?"
    };

    private Vector4 GetStatusColor(ModuleStatus status) => status switch
    {
        ModuleStatus.Complete => new Vector4(0.3f, 0.8f, 0.3f, 1),
        ModuleStatus.InProgress => new Vector4(1, 0.8f, 0.2f, 1),
        ModuleStatus.Incomplete => new Vector4(0.8f, 0.3f, 0.3f, 1),
        _ => new Vector4(0.5f, 0.5f, 0.5f, 1)
    };

    private string GetStatusText(ModuleStatus status) => status switch
    {
        ModuleStatus.Complete => "Done",
        ModuleStatus.InProgress => "Active",
        ModuleStatus.Incomplete => "Todo",
        _ => "---"
    };
}
