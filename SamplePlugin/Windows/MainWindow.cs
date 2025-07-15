using System;
using System.Linq;
using System.Numerics;
using Dalamud.Interface.Textures.TextureWraps;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Windowing;
using Dalamud.Interface.Components;
using ImGuiNET;
using SamplePlugin.Core.Interfaces;

namespace SamplePlugin.Windows;

public class MainWindow : Window, IDisposable
{
    private string GoatImagePath;
    private IDalamudTextureWrap? GoatImage;
    private Plugin Plugin;
    
    private ModuleType? _selectedType = null;
    private string _searchFilter = string.Empty;

    // We give this window a hidden ID using ##
    // So that the user will see "Wahdori" as window title,
    // but for ImGui the ID is "Wahdori##Main"
    public MainWindow(Plugin plugin, string goatImagePath) : base("Wahdori##Main")
    {
        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(800, 600),
            MaximumSize = new Vector2(float.MaxValue, float.MaxValue)
        };

        GoatImagePath = goatImagePath;
        Plugin = plugin;
    }

    public void Dispose()
    {
        GoatImage?.Dispose();
    }

    public override void Draw()
    {
        DrawHeader();
        ImGui.Separator();
        
        // Main content area with sidebar
        var contentSize = ImGui.GetContentRegionAvail();
        
        // Sidebar
        if (ImGui.BeginChild("Sidebar", new Vector2(200, contentSize.Y), true))
        {
            DrawSidebar();
        }
        ImGui.EndChild();
        
        ImGui.SameLine();
        
        // Content area
        if (ImGui.BeginChild("Content", new Vector2(contentSize.X - 210, contentSize.Y), true))
        {
            DrawContent();
        }
        ImGui.EndChild();
    }

    private void DrawHeader()
    {
        // Title with goat image
        if (GoatImage == null)
        {
            var goatTex = Plugin.TextureProvider.GetFromFile(GoatImagePath);
            if (goatTex != null && goatTex.TryGetWrap(out var wrap, out _))
            {
                GoatImage = wrap;
            }
        }

        if (GoatImage != null)
        {
            ImGui.Image(GoatImage.ImGuiHandle, new Vector2(32, 32));
            ImGui.SameLine();
        }
        
        ImGui.Text("Wahdori - Currency & Activity Tracker");
        ImGui.SameLine();
        
        // Quick actions on the right
        var buttonWidth = 100f;
        var spacing = 5f;
        ImGui.SetCursorPosX(ImGui.GetContentRegionMax().X - (buttonWidth * 3 + spacing * 2));
        
        if (ImGuiComponents.IconButton(Dalamud.Interface.FontAwesomeIcon.Cog))
        {
            Plugin.ToggleConfigUI();
        }
        if (ImGui.IsItemHovered())
            ImGui.SetTooltip("Settings");
        
        ImGui.SameLine();
        if (ImGuiComponents.IconButton(Dalamud.Interface.FontAwesomeIcon.Sync))
        {
            Plugin.ModuleManager.UpdateAll();
        }
        if (ImGui.IsItemHovered())
            ImGui.SetTooltip("Refresh All");
        
        ImGui.SameLine();
        if (ImGuiComponents.IconButton(Dalamud.Interface.FontAwesomeIcon.Eye))
        {
            Plugin.OverlayManager.ToggleOverlays();
        }
        if (ImGui.IsItemHovered())
            ImGui.SetTooltip("Toggle Overlays");
        
        // Search bar
        ImGui.SetNextItemWidth(200);
        ImGui.InputTextWithHint("##Search", "Search modules...", ref _searchFilter, 100);
    }

    private void DrawSidebar()
    {
        // Category buttons
        if (ImGui.Button($"{GetTypeIcon(null)} All Modules", new Vector2(-1, 35)))
        {
            _selectedType = null;
        }
        
        foreach (ModuleType type in Enum.GetValues<ModuleType>())
        {
            var isSelected = _selectedType == type;
            
            if (isSelected)
                ImGui.PushStyleColor(ImGuiCol.Button, ImGui.GetColorU32(ImGuiCol.ButtonActive));
            
            if (ImGui.Button($"{GetTypeIcon(type)} {type}", new Vector2(-1, 35)))
            {
                _selectedType = type;
            }
            
            if (isSelected)
                ImGui.PopStyleColor();
        }
        
        ImGui.Separator();
        
        // Quick stats
        ImGui.TextWrapped("Quick Stats:");
        var modules = Plugin.ModuleManager.GetModules();
        var enabledModules = modules.Where(m => m.IsEnabled).ToList();
        var completed = enabledModules.Count(m => m.Status == ModuleStatus.Complete);
        var inProgress = enabledModules.Count(m => m.Status == ModuleStatus.InProgress);
        var total = enabledModules.Count;
        
        ImGui.Text($"Enabled: {total}");
        ImGui.TextColored(new Vector4(0.2f, 0.8f, 0.2f, 1), $"Complete: {completed}");
        ImGui.TextColored(new Vector4(0.8f, 0.8f, 0.2f, 1), $"In Progress: {inProgress}");
        
        if (total > 0)
        {
            ImGui.ProgressBar((float)completed / total, new Vector2(-1, 0), $"{completed}/{total}");
        }
        
        ImGui.Separator();
        
        // Type-specific stats
        if (_selectedType == ModuleType.Currency)
        {
            ImGui.TextWrapped("Currency Warnings:");
            var currencyModules = modules.Where(m => m.Type == ModuleType.Currency && m.IsEnabled);
            var warnings = currencyModules.Count(m => m.Status == ModuleStatus.InProgress);
            ImGui.TextColored(new Vector4(1, 0.5f, 0, 1), $"Active Warnings: {warnings}");
        }
        else if (_selectedType == ModuleType.Daily)
        {
            ImGui.TextWrapped("Daily Progress:");
            var dailyModules = modules.Where(m => m.Type == ModuleType.Daily && m.IsEnabled);
            var dailyComplete = dailyModules.Count(m => m.Status == ModuleStatus.Complete);
            var dailyTotal = dailyModules.Count();
            ImGui.Text($"Daily: {dailyComplete}/{dailyTotal}");
        }
        else if (_selectedType == ModuleType.Weekly)
        {
            ImGui.TextWrapped("Weekly Progress:");
            var weeklyModules = modules.Where(m => m.Type == ModuleType.Weekly && m.IsEnabled);
            var weeklyComplete = weeklyModules.Count(m => m.Status == ModuleStatus.Complete);
            var weeklyTotal = weeklyModules.Count();
            ImGui.Text($"Weekly: {weeklyComplete}/{weeklyTotal}");
        }
    }

    private void DrawContent()
    {
        var modules = Plugin.ModuleManager.GetModules()
            .Where(m => _selectedType == null || m.Type == _selectedType)
            .Where(m => string.IsNullOrEmpty(_searchFilter) || 
                       m.Name.Contains(_searchFilter, StringComparison.OrdinalIgnoreCase));
        
        if (!modules.Any())
        {
            ImGui.TextColored(new Vector4(0.5f, 0.5f, 0.5f, 1), 
                string.IsNullOrEmpty(_searchFilter) 
                    ? "No modules in this category" 
                    : "No modules match your search");
            return;
        }
        
        // Sort modules
        if (Plugin.Configuration.UISettings.SortByStatus)
        {
            modules = modules.OrderBy(m => m.Status).ThenBy(m => m.Name);
        }
        else
        {
            modules = modules.OrderBy(m => m.Type).ThenBy(m => m.Name);
        }
        
        // Draw module cards
        var drawWidth = ImGui.GetContentRegionAvail().X;
        foreach (var module in modules)
        {
            DrawModuleCard(module, drawWidth);
            ImGui.Spacing();
        }
    }

    private void DrawModuleCard(IModule module, float width)
    {
        var isEnabled = module.IsEnabled;
        var status = module.Status;
        
        ImGui.PushStyleVar(ImGuiStyleVar.ChildRounding, 5.0f);
        ImGui.PushStyleColor(ImGuiCol.ChildBg, GetStatusColor(status, 0.1f));
        
        var height = 120f;
        
        if (ImGui.BeginChild($"Module_{module.Name}", new Vector2(width - 10, height), true))
        {
            // Header
            ImGui.Columns(2, null, false);
            ImGui.SetColumnWidth(0, ImGui.GetContentRegionAvail().X - 150);
            
            // Module name and type
            ImGui.Text($"{GetTypeIcon(module.Type)} {module.Name}");
            ImGui.SameLine();
            ImGui.TextColored(new Vector4(0.5f, 0.5f, 0.5f, 1), $"[{module.Type}]");
            
            // Status
            ImGui.TextColored(GetStatusColor(status), GetStatusText(status));
            
            ImGui.NextColumn();
            
            // Controls column
            ImGui.SetCursorPosY(ImGui.GetCursorPosY() + 10);
            
            // Enable checkbox
            if (ImGui.Checkbox($"##Enable_{module.Name}", ref isEnabled))
            {
                module.IsEnabled = isEnabled;
                Plugin.Configuration.Save();
            }
            ImGui.SameLine();
            ImGui.Text("Enabled");
            
            // Settings button
            if (ImGuiComponents.IconButton($"##Settings_{module.Name}", Dalamud.Interface.FontAwesomeIcon.Cog))
            {
                Plugin.ToggleConfigUI();
                // Would be nice to jump to the module's settings
            }
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip("Configure Module");
            
            ImGui.Columns(1);
            
            // Module-specific content
            ImGui.Separator();
            
            // Wrap DrawStatus in try-catch to prevent window from going blank
            try
            {
                module.DrawStatus();
            }
            catch (Exception ex)
            {
                ImGui.TextColored(new Vector4(1, 0, 0, 1), $"Error: {ex.Message}");
                Plugin.Log.Error(ex, $"Error drawing status for module {module.Name}");
            }
        }
        ImGui.EndChild();
        
        ImGui.PopStyleColor();
        ImGui.PopStyleVar();
    }

    private string GetTypeIcon(ModuleType? type)
    {
        var icon = type switch
        {
            ModuleType.Currency => Dalamud.Interface.FontAwesomeIcon.Coins,
            ModuleType.Daily => Dalamud.Interface.FontAwesomeIcon.CalendarDay,
            ModuleType.Weekly => Dalamud.Interface.FontAwesomeIcon.CalendarWeek,
            ModuleType.Special => Dalamud.Interface.FontAwesomeIcon.Star,
            _ => Dalamud.Interface.FontAwesomeIcon.List
        };
        
        return ((char)icon).ToString();
    }

    private Vector4 GetStatusColor(ModuleStatus status, float alpha = 1.0f) => status switch
    {
        ModuleStatus.Complete => new Vector4(0.2f, 0.8f, 0.2f, alpha),
        ModuleStatus.InProgress => new Vector4(0.8f, 0.8f, 0.2f, alpha),
        ModuleStatus.Incomplete => new Vector4(0.8f, 0.2f, 0.2f, alpha),
        _ => new Vector4(0.7f, 0.7f, 0.7f, alpha)
    };

    private string GetStatusText(ModuleStatus status) => status switch
    {
        ModuleStatus.Complete => "Complete",
        ModuleStatus.InProgress => "In Progress",
        ModuleStatus.Incomplete => "Incomplete",
        _ => "Unknown"
    };
}
