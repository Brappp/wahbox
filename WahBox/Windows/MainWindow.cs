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
using WahBox.Models;

namespace WahBox.Windows;

public class MainWindow : Window, IDisposable
{
    private string GoatImagePath;
    private ISharedImmediateTexture? GoatTexture;
    private Plugin PluginInstance;
    
    private string _searchFilter = string.Empty;
    private bool _showCompleted = true;
    private ModuleType? _filterType = null;

    public MainWindow(Plugin plugin, string goatImagePath) : base("WahBox##Main")
    {
        GoatImagePath = goatImagePath;
        PluginInstance = plugin;
        
        // Load the texture once using the shared texture system
        GoatTexture = Plugin.TextureProvider.GetFromFile(GoatImagePath);
        
        // Set default size to prevent cutoff
        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(400, 300),
            MaximumSize = new Vector2(float.MaxValue, float.MaxValue)
        };
        
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
        // Compact header with better spacing
        ImGui.AlignTextToFramePadding();
        
        // Search box
        ImGui.SetNextItemWidth(120);
        ImGui.InputTextWithHint("##Search", "Search...", ref _searchFilter, 50);
        
        ImGui.SameLine();
        
        // Filter combo
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
        
        // Show completed checkbox
        ImGui.Checkbox("Show Completed", ref _showCompleted);
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
                    .OrderBy(m => m.Type)
                    .ThenBy(m => m.Status == ModuleStatus.Complete)
                    .ThenBy(m => m.Name)
                    .ToList();

                if (!modules.Any())
                {
                    ImGui.TextColored(new Vector4(0.5f, 0.5f, 0.5f, 1), "No modules to display");
                }
                else
                {
                    // Use table for better alignment
                    if (ImGui.BeginTable("ModuleTable", 2, ImGuiTableFlags.ScrollY | ImGuiTableFlags.RowBg))
                    {
                        ImGui.TableSetupColumn("Module", ImGuiTableColumnFlags.WidthStretch);
                        ImGui.TableSetupColumn("Status", ImGuiTableColumnFlags.WidthFixed, 150);
                        
                        // Group by type
                        var grouped = modules.GroupBy(m => m.Type);
                        
                        foreach (var group in grouped)
                        {
                            // Category header
                            ImGui.TableNextRow();
                            ImGui.TableNextColumn();
                            ImGui.TableSetBgColor(ImGuiTableBgTarget.RowBg0, ImGui.GetColorU32(new Vector4(0.2f, 0.2f, 0.2f, 0.3f)));
                            
                            ImGui.PushStyleColor(ImGuiCol.Text, GetTypeColor(group.Key));
                            ImGui.PushFont(UiBuilder.IconFont);
                            ImGui.Text(GetTypeIcon(group.Key));
                            ImGui.PopFont();
                            ImGui.SameLine();
                            ImGui.Text(group.Key.ToString());
                            ImGui.PopStyleColor();
                            
                            ImGui.TableNextColumn(); // Status column
                            
                            // Modules in this category
                            foreach (var module in group)
                            {
                                DrawModuleRow(module);
                            }
                        }
                        
                        ImGui.EndTable();
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

    private void DrawModuleRow(IModule module)
    {
        ImGui.TableNextRow();
        ImGui.PushID(module.Name);
        
        // Store current cursor position for hover detection
        var rowMin = ImGui.GetCursorScreenPos();
        
        // Module name column
        ImGui.TableNextColumn();
        
        // Icon
        if (module.IconId > 0)
        {
            try
            {
                var icon = Plugin.TextureProvider.GetFromGameIcon(module.IconId).GetWrapOrEmpty();
                if (icon != null)
                {
                    ImGui.Image(icon.ImGuiHandle, new Vector2(ImGui.GetTextLineHeight(), ImGui.GetTextLineHeight()));
                    ImGui.SameLine();
                }
            }
            catch
            {
                // Fallback to type icon
                ImGui.PushFont(UiBuilder.IconFont);
                ImGui.Text(GetTypeIcon(module.Type));
                ImGui.PopFont();
                ImGui.SameLine();
            }
        }
        
        // Module name (truncate if too long)
        var name = module.Name;
        if (name.Length > 30)
            name = name.Substring(0, 27) + "...";
        ImGui.Text(name);
        
        // Hover for full name
        if (ImGui.IsItemHovered() && module.Name.Length > 30)
        {
            ImGui.SetTooltip(module.Name);
        }
        
        // Status column
        ImGui.TableNextColumn();
        try
        {
            ModuleStatusRenderer.DrawCompactStatus(module);
        }
        catch (Exception ex)
        {
            ImGui.TextColored(new Vector4(1, 0, 0, 1), "Error");
            Plugin.Log.Error(ex, $"Error drawing status for module {module.Name}");
        }
        
        // Calculate row bounds for hover detection
        var rowMax = new Vector2(
            rowMin.X + ImGui.GetContentRegionAvail().X,
            rowMin.Y + ImGui.GetTextLineHeight() + ImGui.GetStyle().CellPadding.Y * 2
        );
        
        // Check if mouse is hovering over the row
        if (ImGui.IsMouseHoveringRect(rowMin, rowMax))
        {
            // Draw hover background
            ImGui.GetWindowDrawList().AddRectFilled(
                rowMin, 
                rowMax, 
                ImGui.GetColorU32(ImGuiCol.TableRowBgAlt),
                0.0f
            );
            
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
            }
            
            ImGui.EndTooltip();
        }
        
        ImGui.PopID();
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

    private Vector4 GetTypeColor(ModuleType type)
    {
        return type switch
        {
            ModuleType.Currency => new Vector4(0.9f, 0.7f, 0.2f, 1),
            ModuleType.Daily => new Vector4(0.3f, 0.7f, 0.9f, 1),
            ModuleType.Weekly => new Vector4(0.7f, 0.3f, 0.9f, 1),
            ModuleType.Special => new Vector4(0.9f, 0.3f, 0.5f, 1),
            _ => new Vector4(0.7f, 0.7f, 0.7f, 1)
        };
    }
}
