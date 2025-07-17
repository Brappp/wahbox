using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Dalamud.Interface;
using Dalamud.Interface.Components;
using Dalamud.Interface.Windowing;
using ImGuiNET;
using WahBox.Core.Interfaces;

namespace WahBox.Windows;

public class DashboardWindow : Window, IDisposable
{
    private Plugin PluginInstance;
    private string _searchFilter = string.Empty;
    private ModuleCategory? _selectedCategory = null;
    
    // Tab tracking
    private readonly Dictionary<ModuleCategory, string> CategoryIcons = new()
    {
        { ModuleCategory.Tracking, FontAwesomeIcon.ChartLine.ToIconString() },
        { ModuleCategory.Utility, FontAwesomeIcon.Tools.ToIconString() },
        { ModuleCategory.Display, FontAwesomeIcon.Desktop.ToIconString() },
        { ModuleCategory.Tools, FontAwesomeIcon.Wrench.ToIconString() }
    };

    public DashboardWindow(Plugin plugin) : base("WahBox Dashboard##Main")
    {
        PluginInstance = plugin;
        
        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(600, 400),
            MaximumSize = new Vector2(float.MaxValue, float.MaxValue)
        };
        
        Flags = ImGuiWindowFlags.None;
    }

    public void Dispose() { }

    public override void Draw()
    {
        try
        {
            // Header with branding
            DrawHeader();
            
            ImGui.Separator();
            
            // Quick access toolbar
            DrawQuickAccessBar();
            
            ImGui.Separator();
            
            // Main content area
            if (ImGui.BeginChild("MainContent", new Vector2(0, -30), false))
            {
                DrawModuleGrid();
            }
            ImGui.EndChild();
            
            // Footer
            ImGui.Separator();
            DrawFooter();
        }
        catch (Exception ex)
        {
            ImGui.TextColored(new Vector4(1, 0, 0, 1), $"Error: {ex.Message}");
        }
    }

    private void DrawHeader()
    {
        // Title and branding
        ImGui.PushFont(UiBuilder.IconFont);
        ImGui.Text(FontAwesomeIcon.Toolbox.ToIconString());
        ImGui.PopFont();
        ImGui.SameLine();
        
        ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(0.9f, 0.7f, 0.2f, 1));
        ImGui.Text("WahBox");
        ImGui.PopStyleColor();
        ImGui.SameLine();
        ImGui.TextColored(new Vector4(0.7f, 0.7f, 0.7f, 1), "- All-in-One Utility Suite");
        
        // Search on the right
        ImGui.SameLine(ImGui.GetContentRegionMax().X - 200);
        ImGui.SetNextItemWidth(200);
        ImGui.InputTextWithHint("##Search", "Search modules...", ref _searchFilter, 100);
    }

    private void DrawQuickAccessBar()
    {
        ImGui.Text("Quick Access:");
        ImGui.SameLine();
        
        // Currency button
        if (ImGuiComponents.IconButton(FontAwesomeIcon.Coins))
        {
            OpenCurrencyOverview();
        }
        if (ImGui.IsItemHovered())
            ImGui.SetTooltip("Currency Overview");
        
        ImGui.SameLine();
        
        // Daily tasks button
        if (ImGuiComponents.IconButton(FontAwesomeIcon.CalendarDay))
        {
            OpenDailyTasks();
        }
        if (ImGui.IsItemHovered())
            ImGui.SetTooltip("Daily Tasks");
        
        ImGui.SameLine();
        
        // Weekly tasks button
        if (ImGuiComponents.IconButton(FontAwesomeIcon.CalendarWeek))
        {
            OpenWeeklyTasks();
        }
        if (ImGui.IsItemHovered())
            ImGui.SetTooltip("Weekly Tasks");
        
        ImGui.SameLine();
        ImGui.Dummy(new Vector2(20, 0));
        ImGui.SameLine();
        
        // Utility modules
        foreach (var module in PluginInstance.ModuleManager.GetModules()
            .Where(m => m.Category == ModuleCategory.Utility && m.HasWindow))
        {
            if (module.IconId > 0)
            {
                var icon = Plugin.TextureProvider.GetFromGameIcon(module.IconId).GetWrapOrEmpty();
                if (icon != null && ImGui.ImageButton(icon.ImGuiHandle, new Vector2(20, 20)))
                {
                    ToggleModuleWindow(module);
                }
            }
            else
            {
                if (ImGui.Button(module.Name.Substring(0, Math.Min(3, module.Name.Length))))
                {
                    ToggleModuleWindow(module);
                }
            }
            
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip($"{module.Name} ({(module.Status == ModuleStatus.Active ? "Active" : "Inactive")})");
            
            ImGui.SameLine();
        }
        
        // Settings button on the right
        ImGui.SameLine(ImGui.GetContentRegionMax().X - 25);
        if (ImGuiComponents.IconButton(FontAwesomeIcon.Cog))
        {
            PluginInstance.ToggleConfigUI();
        }
        if (ImGui.IsItemHovered())
            ImGui.SetTooltip("Settings");
    }

    private void DrawModuleGrid()
    {
        var modules = PluginInstance.ModuleManager.GetModules()
            .Where(m => string.IsNullOrEmpty(_searchFilter) || 
                       m.Name.Contains(_searchFilter, StringComparison.OrdinalIgnoreCase))
            .GroupBy(m => m.Category)
            .OrderBy(g => g.Key);

        foreach (var categoryGroup in modules)
        {
            if (_selectedCategory.HasValue && _selectedCategory.Value != categoryGroup.Key)
                continue;

            // Category header
            var isExpanded = DrawCategoryHeader(categoryGroup.Key, categoryGroup.Count());
            
            if (isExpanded)
            {
                // Module cards
                ImGui.Indent();
                DrawModuleCards(categoryGroup);
                ImGui.Unindent();
            }
            
            ImGui.Spacing();
        }
    }

    private bool DrawCategoryHeader(ModuleCategory category, int count)
    {
        var icon = CategoryIcons.GetValueOrDefault(category, FontAwesomeIcon.Folder.ToIconString());
        
        ImGui.PushStyleColor(ImGuiCol.Header, new Vector4(0.2f, 0.2f, 0.25f, 0.8f));
        ImGui.PushStyleColor(ImGuiCol.HeaderHovered, new Vector4(0.3f, 0.3f, 0.35f, 0.8f));
        ImGui.PushStyleColor(ImGuiCol.HeaderActive, new Vector4(0.4f, 0.4f, 0.45f, 0.8f));
        
        var expanded = ImGui.CollapsingHeader($"{icon} {category} ({count} modules)###{category}", 
            ImGuiTreeNodeFlags.DefaultOpen);
        
        ImGui.PopStyleColor(3);
        
        return expanded;
    }

    private void DrawModuleCards(IGrouping<ModuleCategory, IModule> categoryGroup)
    {
        var availableWidth = ImGui.GetContentRegionAvail().X;
        var cardWidth = 280f;
        var cardsPerRow = Math.Max(1, (int)(availableWidth / (cardWidth + 10)));
        
        var moduleList = categoryGroup.ToList();
        for (int i = 0; i < moduleList.Count; i++)
        {
            if (i % cardsPerRow != 0)
                ImGui.SameLine();
            
            DrawModuleCard(moduleList[i]);
        }
    }

    private void DrawModuleCard(IModule module)
    {
        ImGui.PushID(module.Name);
        
        var drawList = ImGui.GetWindowDrawList();
        var startPos = ImGui.GetCursorScreenPos();
        var cardSize = new Vector2(270, 100);
        
        // Card background
        drawList.AddRectFilled(
            startPos,
            startPos + cardSize,
            ImGui.GetColorU32(new Vector4(0.15f, 0.15f, 0.18f, 0.9f)),
            5.0f
        );
        
        // Status color accent
        var statusColor = GetModuleStatusColor(module);
        drawList.AddRectFilled(
            startPos,
            startPos + new Vector2(4, cardSize.Y),
            ImGui.GetColorU32(statusColor),
            2.0f
        );
        
        // Content area
        ImGui.SetCursorScreenPos(startPos + new Vector2(10, 10));
        
        // Header with icon and name
        ImGui.BeginGroup();
        
        // Icon
        if (module.IconId > 0)
        {
            try
            {
                var icon = Plugin.TextureProvider.GetFromGameIcon(module.IconId).GetWrapOrEmpty();
                if (icon != null)
                {
                    ImGui.Image(icon.ImGuiHandle, new Vector2(24, 24));
                    ImGui.SameLine();
                }
            }
            catch { }
        }
        
        // Module name and type
        ImGui.BeginGroup();
        ImGui.Text(module.Name);
        ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(0.6f, 0.6f, 0.6f, 1));
        ImGui.TextWrapped($"{module.Type}");
        ImGui.PopStyleColor();
        ImGui.EndGroup();
        
        ImGui.EndGroup();
        
        // Status area
        ImGui.SetCursorScreenPos(startPos + new Vector2(10, 45));
        module.DrawStatus();
        
        // Action buttons
        ImGui.SetCursorScreenPos(startPos + new Vector2(cardSize.X - 80, cardSize.Y - 30));
        
        // Enable/Disable toggle
        var enabled = module.IsEnabled;
        ImGui.PushStyleVar(ImGuiStyleVar.FramePadding, new Vector2(4, 2));
        if (ImGui.Checkbox("##Enable", ref enabled))
        {
            module.IsEnabled = enabled;
            PluginInstance.Configuration.Save();
        }
        ImGui.PopStyleVar();
        
        ImGui.SameLine();
        
        // View/Settings button
        if (module.HasWindow)
        {
            if (ImGui.SmallButton("View"))
            {
                ToggleModuleWindow(module);
            }
        }
        else
        {
            if (ImGui.SmallButton("Info"))
            {
                ImGui.OpenPopup($"ModuleInfo_{module.Name}");
            }
        }
        
        // Info popup
        if (ImGui.BeginPopup($"ModuleInfo_{module.Name}"))
        {
            ImGui.Text(module.Name);
            ImGui.Separator();
            module.DrawStatus();
            ImGui.EndPopup();
        }
        
        // Move cursor past the card
        ImGui.SetCursorScreenPos(startPos + new Vector2(cardSize.X + 10, 0));
        ImGui.Dummy(new Vector2(0, cardSize.Y));
        
        ImGui.PopID();
    }

    private Vector4 GetModuleStatusColor(IModule module)
    {
        return module.Status switch
        {
            ModuleStatus.Complete => new Vector4(0.2f, 0.8f, 0.2f, 1),
            ModuleStatus.Active => new Vector4(0.2f, 0.6f, 0.9f, 1),
            ModuleStatus.InProgress => new Vector4(0.8f, 0.8f, 0.2f, 1),
            ModuleStatus.Incomplete => new Vector4(0.8f, 0.2f, 0.2f, 1),
            ModuleStatus.Inactive => new Vector4(0.5f, 0.5f, 0.5f, 1),
            _ => new Vector4(0.7f, 0.7f, 0.7f, 1)
        };
    }

    private void DrawFooter()
    {
        var enabledCount = PluginInstance.ModuleManager.GetModules().Count(m => m.IsEnabled);
        var totalCount = PluginInstance.ModuleManager.GetModules().Count();
        
        ImGui.TextColored(new Vector4(0.6f, 0.6f, 0.6f, 1), 
            $"{enabledCount} of {totalCount} modules enabled");
        
        ImGui.SameLine(ImGui.GetContentRegionMax().X - 100);
        
        // Category filter buttons
        if (ImGui.SmallButton("All"))
            _selectedCategory = null;
        
        ImGui.SameLine();
        if (ImGui.SmallButton("Tracking"))
            _selectedCategory = ModuleCategory.Tracking;
        
        ImGui.SameLine();
        if (ImGui.SmallButton("Utility"))
            _selectedCategory = ModuleCategory.Utility;
    }

    private void ToggleModuleWindow(IModule module)
    {
        if (module.Status == ModuleStatus.Active)
        {
            module.CloseWindow();
        }
        else
        {
            module.OpenWindow();
        }
    }

    private void OpenCurrencyOverview()
    {
        // TODO: Open currency overview window
    }

    private void OpenDailyTasks()
    {
        // TODO: Open daily tasks window
    }

    private void OpenWeeklyTasks()
    {
        // TODO: Open weekly tasks window
    }
}
