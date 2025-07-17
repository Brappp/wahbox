using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Dalamud.Interface;
using Dalamud.Interface.Components;
using Dalamud.Interface.Windowing;
using ImGuiNET;
using WahBox.Core.Interfaces;
using WahBox.Systems;
using Lumina.Text.ReadOnly;

namespace WahBox.Windows;

public class MainWindow : Window, IDisposable
{
    private Plugin PluginInstance;
    private string _searchFilter = string.Empty;
    private Tab _currentTab = Tab.Tracking;
    private readonly Dictionary<string, bool> _expandedSections = new();
    
    private enum Tab
    {
        Tracking,
        Utilities,
        Settings
    }

    public MainWindow(Plugin plugin) : base("WahBox##MainWindow")
    {
        PluginInstance = plugin;
        
        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(700, 500),
            MaximumSize = new Vector2(float.MaxValue, float.MaxValue)
        };
        
        Size = new Vector2(800, 600);
        SizeCondition = ImGuiCond.FirstUseEver;
    }

    public void Dispose() 
    {
        // Save any UI state
        SaveUIState();
    }

    public override void Draw()
    {
        // Header
        DrawHeader();
        
        ImGui.Separator();
        
        // Tab bar
        if (ImGui.BeginTabBar("MainTabs"))
        {
            if (ImGui.BeginTabItem($"{FontAwesomeIcon.ChartLine.ToIconString()} Tracking"))
            {
                _currentTab = Tab.Tracking;
                ImGui.EndTabItem();
            }
            
            if (ImGui.BeginTabItem($"{FontAwesomeIcon.Tools.ToIconString()} Utilities"))
            {
                _currentTab = Tab.Utilities;
                ImGui.EndTabItem();
            }
            
            if (ImGui.BeginTabItem($"{FontAwesomeIcon.Cog.ToIconString()} Settings"))
            {
                _currentTab = Tab.Settings;
                ImGui.EndTabItem();
            }
            
            ImGui.EndTabBar();
        }
        
        // Tab content
        ImGui.BeginChild("TabContent", new Vector2(0, 0), false);
        
        switch (_currentTab)
        {
            case Tab.Tracking:
                DrawTrackingTab();
                break;
            case Tab.Utilities:
                DrawUtilitiesTab();
                break;
            case Tab.Settings:
                DrawSettingsTab();
                break;
        }
        
        ImGui.EndChild();
    }

    private void DrawHeader()
    {
        // Logo and title
        ImGui.PushFont(UiBuilder.IconFont);
        ImGui.Text(FontAwesomeIcon.Toolbox.ToIconString());
        ImGui.PopFont();
        ImGui.SameLine();
        
        ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(0.9f, 0.7f, 0.2f, 1));
        ImGui.Text("WahBox");
        ImGui.PopStyleColor();
        
        // Search box on the right
        ImGui.SameLine(ImGui.GetContentRegionMax().X - 250);
        ImGui.SetNextItemWidth(200);
        ImGui.InputTextWithHint("##Search", "Search...", ref _searchFilter, 100);
        
        // Settings button
        ImGui.SameLine();
        if (ImGuiComponents.IconButton(FontAwesomeIcon.Cog))
        {
            _currentTab = Tab.Settings;
        }
        if (ImGui.IsItemHovered())
            ImGui.SetTooltip("Settings");
    }

    private void DrawTrackingTab()
    {
        // Quick alerts bar
        DrawQuickAlerts();
        
        ImGui.Spacing();
        
        // Filter bar
        DrawFilterBar();
        
        ImGui.Spacing();
        
        // Main content - scrollable
        ImGui.BeginChild("TrackingContent", new Vector2(0, 0), false);
        
        // Currencies section
        DrawCurrenciesSection();
        
        ImGui.Spacing();
        
        // Daily tasks section
        DrawDailyTasksSection();
        
        ImGui.Spacing();
        
        // Weekly tasks section  
        DrawWeeklyTasksSection();
        
        ImGui.Spacing();
        
        // Special tasks section
        DrawSpecialTasksSection();
        
        ImGui.EndChild();
    }

    private void DrawQuickAlerts()
    {
        var alertBgColor = new Vector4(0.8f, 0.2f, 0.2f, 0.2f);
        var warningBgColor = new Vector4(0.8f, 0.8f, 0.2f, 0.2f);
        
        // Get alert data
        var currencyWarnings = GetCurrencyWarnings();
        var pendingTasks = GetPendingTaskCount();
        
        if (currencyWarnings.Any() || pendingTasks > 0)
        {
            ImGui.PushStyleColor(ImGuiCol.ChildBg, warningBgColor);
            ImGui.BeginChild("QuickAlerts", new Vector2(0, 80), true);
            
            // Build alert text in a single string to avoid UI overlap issues
            var alertParts = new List<string>();
            
            if (currencyWarnings.Any())
            {
                alertParts.Add($"⚠️ Near Cap: {string.Join(" | ", currencyWarnings)}");
            }
            
            if (pendingTasks > 0)
            {
                alertParts.Add($"📋 {pendingTasks} tasks pending");
            }
            
            // Display alerts on one line if they fit, otherwise split
            if (alertParts.Any())
            {
                var alertText = string.Join(" | ", alertParts);
                ImGui.TextWrapped(alertText);
            }
            
            // Reset timers on separate line
            var dailyReset = GetTimeUntilDailyReset();
            var weeklyReset = GetTimeUntilWeeklyReset();
            ImGui.TextWrapped($"⏰ Resets: Daily in {FormatTimeSpan(dailyReset)} | Weekly in {FormatTimeSpan(weeklyReset)}");
            
            ImGui.EndChild();
            ImGui.PopStyleColor();
        }
    }

    private void DrawFilterBar()
    {
        // Filter options
        ImGui.Text("Filter:");
        ImGui.SameLine();
        
        var warningsOnly = PluginInstance.Configuration.UISettings.ShowWarningsOnly;
        if (ImGui.Checkbox("Warnings Only", ref warningsOnly))
        {
            PluginInstance.Configuration.UISettings.ShowWarningsOnly = warningsOnly;
            PluginInstance.Configuration.Save();
        }
        
        ImGui.SameLine();
        
        var hideComplete = PluginInstance.Configuration.UISettings.HideCompleted;
        if (ImGui.Checkbox("Hide Complete", ref hideComplete))
        {
            PluginInstance.Configuration.UISettings.HideCompleted = hideComplete;
            PluginInstance.Configuration.Save();
        }
        
        ImGui.SameLine();
        
        var hideDisabled = PluginInstance.Configuration.UISettings.ShowDisabledModules;
        if (ImGui.Checkbox("Show Disabled", ref hideDisabled))
        {
            PluginInstance.Configuration.UISettings.ShowDisabledModules = hideDisabled;
            PluginInstance.Configuration.Save();
        }
    }

    private void DrawCurrenciesSection()
    {
        var currencyModules = PluginInstance.ModuleManager.GetModules()
            .Where(m => m.Type == ModuleType.Currency)
            .Where(m => PassesFilter(m))
            .GroupBy(m => GetCurrencyCategory(m.Name))
            .OrderBy(g => g.Key);

        ImGui.PushStyleVar(ImGuiStyleVar.FramePadding, new Vector2(5, 5));
        
        foreach (var group in currencyModules)
        {
            if (DrawSectionHeader($"Currencies - {group.Key}", $"Currencies_{group.Key}"))
            {
                ImGui.Indent();
                
                foreach (var module in group)
                {
                    DrawCurrencyModule(module);
                }
                
                ImGui.Unindent();
            }
        }
        
        ImGui.PopStyleVar();
    }

    private void DrawCurrencyModule(IModule module)
    {
        ImGui.PushID(module.Name);
        
        // Module row
        ImGui.BeginGroup();
        
        // Icon and name
        if (module.IconId > 0)
        {
            try
            {
                var icon = Plugin.TextureProvider.GetFromGameIcon(module.IconId).GetWrapOrEmpty();
                if (icon != null && icon.ImGuiHandle != IntPtr.Zero)
                {
                    ImGui.Image(icon.ImGuiHandle, new Vector2(20, 20));
                    ImGui.SameLine();
                }
            }
            catch
            {
                // Skip broken icons gracefully
            }
        }
        
        // Enable checkbox
        var enabled = module.IsEnabled;
        if (ImGui.Checkbox($"##{module.Name}_enable", ref enabled))
        {
            module.IsEnabled = enabled;
            module.SaveConfiguration();
        }
        
        ImGui.SameLine();
        ImGui.Text(module.Name);
        
        // Currency specific display
        if (module is ICurrencyModule currencyModule)
        {
            ImGui.SameLine(280);
            
            var current = currencyModule.GetCurrentAmount();
            var max = currencyModule.GetMaxAmount();
            var percent = max > 0 ? (float)current / max * 100f : 0f;
            
            // Progress bar
            var progressBarSize = new Vector2(120, 20);
            var color = percent >= currencyModule.AlertThreshold ? new Vector4(0.8f, 0.2f, 0.2f, 1) :
                       percent >= 75 ? new Vector4(0.8f, 0.8f, 0.2f, 1) :
                       new Vector4(0.2f, 0.8f, 0.2f, 1);
            
            ImGui.PushStyleColor(ImGuiCol.PlotHistogram, color);
            ImGui.ProgressBar(percent / 100f, progressBarSize, $"{current:N0}/{max:N0}");
            ImGui.PopStyleColor();
            
            ImGui.SameLine();
            ImGui.Text($"{percent:F0}%");
            
            // Alert threshold settings
            ImGui.SameLine();
            ImGui.Text("Alert at:");
            ImGui.SameLine();
            ImGui.SetNextItemWidth(40);
            
            var threshold = currencyModule.AlertThreshold;
            if (ImGui.InputInt($"##{module.Name}_threshold", ref threshold, 0, 0))
            {
                if (threshold >= 0 && threshold <= 100)
                {
                    currencyModule.AlertThreshold = threshold;
                    module.SaveConfiguration();
                }
            }
            ImGui.SameLine();
            ImGui.Text("%");
        }
        else
        {
            // Fallback for non-currency modules
            module.DrawStatus();
        }
        
        ImGui.EndGroup();
        
        ImGui.PopID();
    }

    private void DrawDailyTasksSection()
    {
        var dailyModules = PluginInstance.ModuleManager.GetModules()
            .Where(m => m.Type == ModuleType.Daily)
            .Where(m => PassesFilter(m))
            .ToList();

        if (!dailyModules.Any()) return;

        var timeUntilReset = GetTimeUntilDailyReset();
        var headerText = $"Daily Tasks - Reset: {FormatTimeSpan(timeUntilReset)}";
        
        if (DrawSectionHeader(headerText, "DailyTasks"))
        {
            ImGui.Indent();
            
            foreach (var module in dailyModules)
            {
                DrawTaskModule(module);
            }
            
            ImGui.Unindent();
        }
    }

    private void DrawWeeklyTasksSection()
    {
        var weeklyModules = PluginInstance.ModuleManager.GetModules()
            .Where(m => m.Type == ModuleType.Weekly)
            .Where(m => PassesFilter(m))
            .ToList();

        if (!weeklyModules.Any()) return;

        var timeUntilReset = GetTimeUntilWeeklyReset();
        var headerText = $"Weekly Tasks - Reset: {FormatTimeSpan(timeUntilReset)}";
        
        if (DrawSectionHeader(headerText, "WeeklyTasks"))
        {
            ImGui.Indent();
            
            foreach (var module in weeklyModules)
            {
                DrawTaskModule(module);
            }
            
            ImGui.Unindent();
        }
    }

    private void DrawSpecialTasksSection()
    {
        var specialModules = PluginInstance.ModuleManager.GetModules()
            .Where(m => m.Type == ModuleType.Special)
            .Where(m => PassesFilter(m))
            .ToList();

        if (!specialModules.Any()) return;

        if (DrawSectionHeader("Special", "SpecialTasks"))
        {
            ImGui.Indent();
            
            foreach (var module in specialModules)
            {
                DrawTaskModule(module);
            }
            
            ImGui.Unindent();
        }
    }

    private void DrawTaskModule(IModule module)
    {
        ImGui.PushID(module.Name);
        
        // Module row
        ImGui.BeginGroup();
        
        // Enable checkbox
        var enabled = module.IsEnabled;
        if (ImGui.Checkbox($"##{module.Name}_enable", ref enabled))
        {
            module.IsEnabled = enabled;
            module.SaveConfiguration();
        }
        
        ImGui.SameLine();
        
        // Status icon
        var statusIcon = GetStatusIcon(module.Status);
        ImGui.Text(statusIcon);
        
        ImGui.SameLine();
        ImGui.Text(module.Name);
        
        // Module-specific status
        ImGui.SameLine(300);
        module.DrawStatus();
        
        // Action buttons
        if (module.HasWindow)
        {
            ImGui.SameLine(ImGui.GetContentRegionMax().X - 100);
            if (ImGui.SmallButton("Open"))
            {
                module.OpenWindow();
            }
        }
        
        // Settings gear
        ImGui.SameLine();
        if (ImGuiComponents.IconButton($"##{module.Name}_settings", FontAwesomeIcon.Cog))
        {
            ImGui.OpenPopup($"{module.Name}_SettingsPopup");
        }
        
        // Settings popup
        if (ImGui.BeginPopup($"{module.Name}_SettingsPopup"))
        {
            ImGui.Text($"{module.Name} Settings");
            ImGui.Separator();
            module.DrawConfig();
            ImGui.EndPopup();
        }
        
        ImGui.EndGroup();
        
        ImGui.PopID();
    }

    private void DrawUtilitiesTab()
    {
        var utilityModules = PluginInstance.ModuleManager.GetModules()
            .Where(m => m.Category == ModuleCategory.Utility)
            .ToList();

        // Active utilities summary
        var activeUtils = utilityModules.Where(m => m.Status == ModuleStatus.Active).ToList();
        if (activeUtils.Any())
        {
            ImGui.BeginChild("ActiveUtilities", new Vector2(0, 30), true);
            ImGui.Text("Active: ");
            ImGui.SameLine();
            
            foreach (var util in activeUtils)
            {
                ImGui.Text($"{util.Name}");
                if (util != activeUtils.Last())
                {
                    ImGui.SameLine();
                    ImGui.Text(" | ");
                    ImGui.SameLine();
                }
            }
            
            ImGui.EndChild();
        }
        
        ImGui.Spacing();
        
        // Utility modules
        ImGui.BeginChild("UtilityModules", new Vector2(0, 0), false);
        
        foreach (var module in utilityModules)
        {
            DrawUtilityModule(module);
            ImGui.Spacing();
        }
        
        ImGui.EndChild();
    }

    private void DrawUtilityModule(IModule module)
    {
        ImGui.PushStyleVar(ImGuiStyleVar.ChildBorderSize, 1.0f);
        ImGui.BeginChild($"{module.Name}_Frame", new Vector2(0, 200), true);
        
        // Header
        ImGui.Text(module.Name);
        ImGui.SameLine();
        
        var statusColor = module.Status == ModuleStatus.Active ? 
            new Vector4(0.2f, 0.8f, 0.2f, 1) : 
            new Vector4(0.5f, 0.5f, 0.5f, 1);
        ImGui.TextColored(statusColor, $"● {module.Status}");
        
        ImGui.Separator();
        
        // Enable/Disable and action buttons
        var enabled = module.IsEnabled;
        if (ImGui.Checkbox("Enable", ref enabled))
        {
            module.IsEnabled = enabled;
            module.SaveConfiguration();
        }
        
        ImGui.SameLine();
        
        if (module.HasWindow)
        {
            if (ImGui.Button("Open Window"))
            {
                if (module.Status == ModuleStatus.Active)
                    module.CloseWindow();
                else
                    module.OpenWindow();
            }
        }
        
        ImGui.SameLine();
        
        if (ImGui.Button("Configure Details"))
        {
            ImGui.OpenPopup($"{module.Name}_DetailedConfig");
        }
        
        // Quick settings area
        ImGui.Spacing();
        ImGui.Text("Quick Settings:");
        ImGui.Separator();
        
        // Module draws its own config
        ImGui.BeginChild($"{module.Name}_QuickSettings", new Vector2(0, 100), false);
        module.DrawConfig();
        ImGui.EndChild();
        
        // Detailed config popup
        if (ImGui.BeginPopupModal($"{module.Name}_DetailedConfig", ref enabled, ImGuiWindowFlags.AlwaysAutoResize))
        {
            ImGui.Text($"{module.Name} Detailed Configuration");
            ImGui.Separator();
            
            ImGui.BeginChild("DetailedConfigContent", new Vector2(500, 400), false);
            module.DrawConfig();
            ImGui.EndChild();
            
            if (ImGui.Button("Close", new Vector2(120, 0)))
            {
                ImGui.CloseCurrentPopup();
            }
            
            ImGui.EndPopup();
        }
        
        ImGui.EndChild();
        ImGui.PopStyleVar();
    }

    private void DrawSettingsTab()
    {
        ImGui.BeginChild("SettingsContent", new Vector2(0, 0), false);
        
        // Window Behavior
        if (ImGui.CollapsingHeader("Window Behavior", ImGuiTreeNodeFlags.DefaultOpen))
        {
            ImGui.Indent();
            
            var hideInCombat = PluginInstance.Configuration.UISettings.HideInCombat;
            if (ImGui.Checkbox("Hide in Combat", ref hideInCombat))
            {
                PluginInstance.Configuration.UISettings.HideInCombat = hideInCombat;
                PluginInstance.Configuration.Save();
            }
            
            ImGui.SameLine();
            
            var hideInDuty = PluginInstance.Configuration.UISettings.HideInDuty;
            if (ImGui.Checkbox("Hide in Duty", ref hideInDuty))
            {
                PluginInstance.Configuration.UISettings.HideInDuty = hideInDuty;
                PluginInstance.Configuration.Save();
            }
            
            ImGui.Unindent();
        }
        
        ImGui.Spacing();
        
        // Notifications
        if (ImGui.CollapsingHeader("Notifications", ImGuiTreeNodeFlags.DefaultOpen))
        {
            ImGui.Indent();
            
            ImGui.Text("Alert Channels:");
            
            var chatAlerts = PluginInstance.Configuration.NotificationSettings.ChatNotifications;
            if (ImGui.Checkbox("Chat", ref chatAlerts))
            {
                PluginInstance.Configuration.NotificationSettings.ChatNotifications = chatAlerts;
                PluginInstance.Configuration.Save();
            }
            
            ImGui.SameLine();
            
            var toastAlerts = PluginInstance.Configuration.NotificationSettings.EnableToastNotifications;
            if (ImGui.Checkbox("Toast", ref toastAlerts))
            {
                PluginInstance.Configuration.NotificationSettings.EnableToastNotifications = toastAlerts;
                PluginInstance.Configuration.Save();
            }
            
            ImGui.SameLine();
            
            var soundEnabled = PluginInstance.Configuration.NotificationSettings.EnableSoundAlerts;
            if (ImGui.Checkbox("Sound", ref soundEnabled))
            {
                PluginInstance.Configuration.NotificationSettings.EnableSoundAlerts = soundEnabled;
                PluginInstance.Configuration.Save();
            }
            
            var suppressInDuty = PluginInstance.Configuration.NotificationSettings.SuppressInDuty;
            if (ImGui.Checkbox("Suppress all alerts in duty", ref suppressInDuty))
            {
                PluginInstance.Configuration.NotificationSettings.SuppressInDuty = suppressInDuty;
                PluginInstance.Configuration.Save();
            }
            
            ImGui.Spacing();
            ImGui.Separator();
            ImGui.Spacing();
            
            // Currency alerts
            ImGui.Text("Currency Alerts:");
            
            ImGui.SetNextItemWidth(100);
            var defaultThreshold = PluginInstance.Configuration.NotificationSettings.NotificationThreshold;
            if (ImGui.InputInt("Default threshold %", ref defaultThreshold))
            {
                if (defaultThreshold >= 0 && defaultThreshold <= 100)
                {
                    PluginInstance.Configuration.NotificationSettings.NotificationThreshold = defaultThreshold;
                    PluginInstance.Configuration.Save();
                }
            }
            
            ImGui.SetNextItemWidth(100);
            var cooldown = PluginInstance.Configuration.NotificationSettings.NotificationCooldown;
            if (ImGui.InputInt("Cooldown (minutes)", ref cooldown))
            {
                if (cooldown >= 0)
                {
                    PluginInstance.Configuration.NotificationSettings.NotificationCooldown = cooldown;
                    PluginInstance.Configuration.Save();
                }
            }
            
            if (soundEnabled)
            {
                ImGui.SetNextItemWidth(150);
                var soundType = PluginInstance.Configuration.NotificationSettings.CurrencyAlertSound;
                if (ImGui.Combo("Sound", ref soundType, new[] { "Ping", "Alert", "Notification", "Alarm" }, 4))
                {
                    PluginInstance.Configuration.NotificationSettings.CurrencyAlertSound = soundType;
                    PluginInstance.Configuration.Save();
                }
                
                ImGui.SameLine();
                if (ImGui.Button("Test"))
                {
                    PluginInstance.NotificationManager.PlaySound(soundType);
                }
            }
            
            ImGui.Spacing();
            ImGui.Separator();
            ImGui.Spacing();
            
            // Task alerts
            ImGui.Text("Task Alerts:");
            
            var dailyReminder = PluginInstance.Configuration.NotificationSettings.DailyResetReminder;
            if (ImGui.Checkbox("Daily reset reminder", ref dailyReminder))
            {
                PluginInstance.Configuration.NotificationSettings.DailyResetReminder = dailyReminder;
                PluginInstance.Configuration.Save();
            }
            
            if (dailyReminder)
            {
                ImGui.SameLine();
                ImGui.SetNextItemWidth(100);
                var reminderTime = PluginInstance.Configuration.NotificationSettings.DailyResetReminderMinutes;
                if (ImGui.InputInt("minutes before", ref reminderTime))
                {
                    if (reminderTime >= 0)
                    {
                        PluginInstance.Configuration.NotificationSettings.DailyResetReminderMinutes = reminderTime;
                        PluginInstance.Configuration.Save();
                    }
                }
            }
            
            var taskComplete = PluginInstance.Configuration.NotificationSettings.TaskCompletionAlerts;
            if (ImGui.Checkbox("Task completion alerts", ref taskComplete))
            {
                PluginInstance.Configuration.NotificationSettings.TaskCompletionAlerts = taskComplete;
                PluginInstance.Configuration.Save();
            }
            
            if (taskComplete && soundEnabled)
            {
                ImGui.SetNextItemWidth(150);
                var taskSound = PluginInstance.Configuration.NotificationSettings.TaskCompleteSound;
                if (ImGui.Combo("Sound##TaskSound", ref taskSound, new[] { "Ping", "Alert", "Notification", "Alarm" }, 4))
                {
                    PluginInstance.Configuration.NotificationSettings.TaskCompleteSound = taskSound;
                    PluginInstance.Configuration.Save();
                }
                
                ImGui.SameLine();
                if (ImGui.Button("Test##TaskTest"))
                {
                    PluginInstance.NotificationManager.PlaySound(taskSound);
                }
            }
            
            ImGui.Unindent();
        }
        
        ImGui.Spacing();
        
        // Data Management
        if (ImGui.CollapsingHeader("Data Management"))
        {
            ImGui.Indent();
            
            var player = Plugin.ClientState.LocalPlayer;
            if (player != null)
            {
                ImGui.Text($"Current: {player.Name} @ {player.HomeWorld.ValueNullable?.Name.ExtractText() ?? "Unknown"}");
            }
            else
            {
                ImGui.Text("Current: Not logged in");
            }
            
            ImGui.Spacing();
            
            if (ImGui.Button("Export Settings"))
            {
                // TODO: Implement settings export
            }
            
            ImGui.SameLine();
            
            if (ImGui.Button("Import Settings"))
            {
                // TODO: Implement settings import
            }
            
            ImGui.SameLine();
            
            if (ImGui.Button("Reset to Defaults"))
            {
                if (ImGui.IsKeyDown(ImGuiKey.LeftShift))
                {
                    PluginInstance.Configuration.ResetToDefaults();
                    PluginInstance.Configuration.Save();
                }
            }
            if (ImGui.IsItemHovered())
            {
                ImGui.SetTooltip("Hold Shift and click to reset all settings to defaults");
            }
            
            ImGui.Unindent();
        }
        
        ImGui.EndChild();
    }

    private bool DrawSectionHeader(string title, string id)
    {
        var isExpanded = _expandedSections.TryGetValue(id, out var expanded) ? expanded : true;
        
        ImGui.PushStyleColor(ImGuiCol.Header, new Vector4(0.2f, 0.2f, 0.25f, 0.8f));
        ImGui.PushStyleColor(ImGuiCol.HeaderHovered, new Vector4(0.3f, 0.3f, 0.35f, 0.8f));
        ImGui.PushStyleColor(ImGuiCol.HeaderActive, new Vector4(0.4f, 0.4f, 0.45f, 0.8f));
        
        var flags = isExpanded ? ImGuiTreeNodeFlags.DefaultOpen : ImGuiTreeNodeFlags.None;
        var newExpanded = ImGui.CollapsingHeader(title, flags);
        
        ImGui.PopStyleColor(3);
        
        if (newExpanded != isExpanded)
        {
            _expandedSections[id] = newExpanded;
            SaveUIState();
        }
        
        return newExpanded;
    }

    private bool PassesFilter(IModule module)
    {
        // Search filter
        if (!string.IsNullOrEmpty(_searchFilter))
        {
            if (!module.Name.Contains(_searchFilter, StringComparison.OrdinalIgnoreCase))
                return false;
        }
        
        // Hide disabled
        if (!PluginInstance.Configuration.UISettings.ShowDisabledModules && !module.IsEnabled)
            return false;
        
        // Hide complete
        if (PluginInstance.Configuration.UISettings.HideCompleted && module.Status == ModuleStatus.Complete)
            return false;
        
        // Warnings only (for currency modules)
        if (PluginInstance.Configuration.UISettings.ShowWarningsOnly && module is ICurrencyModule currencyModule)
        {
            var percent = currencyModule.GetMaxAmount() > 0 ? 
                (float)currencyModule.GetCurrentAmount() / currencyModule.GetMaxAmount() * 100f : 0f;
            if (percent < currencyModule.AlertThreshold)
                return false;
        }
        
        return true;
    }

    private string GetCurrencyCategory(string moduleName)
    {
        if (moduleName.Contains("Tomestone")) return "Tomestones";
        if (moduleName.Contains("Seal") || moduleName.Contains("Nuts")) return "Hunt Currencies";
        if (moduleName.Contains("Scrip")) return "Scrips";
        if (moduleName.Contains("Grand Company")) return "Grand Company";
        return "Other";
    }

    private string GetStatusIcon(ModuleStatus status)
    {
        return status switch
        {
            ModuleStatus.Complete => "✅",
            ModuleStatus.InProgress => "⏳",
            ModuleStatus.Incomplete => "❌",
            ModuleStatus.Active => "🟢",
            ModuleStatus.Inactive => "⚫",
            _ => "❓"
        };
    }

    private List<string> GetCurrencyWarnings()
    {
        var warnings = new List<string>();
        
        var currencyModules = PluginInstance.ModuleManager.GetModules()
            .Where(m => m.Type == ModuleType.Currency && m.IsEnabled)
            .OfType<ICurrencyModule>();
        
        foreach (var module in currencyModules)
        {
            var percent = module.GetMaxAmount() > 0 ? 
                (float)module.GetCurrentAmount() / module.GetMaxAmount() * 100f : 0f;
            
            if (percent >= module.AlertThreshold)
            {
                warnings.Add($"{module.Name} ({percent:F0}%)");
            }
        }
        
        return warnings;
    }

    private int GetPendingTaskCount()
    {
        return PluginInstance.ModuleManager.GetModules()
            .Count(m => (m.Type == ModuleType.Daily || m.Type == ModuleType.Weekly) && 
                       m.IsEnabled && 
                       (m.Status == ModuleStatus.Incomplete || m.Status == ModuleStatus.InProgress));
    }

    private TimeSpan GetTimeUntilDailyReset()
    {
        var now = DateTime.UtcNow;
        var resetTime = new DateTime(now.Year, now.Month, now.Day, 15, 0, 0, DateTimeKind.Utc);
        
        if (now > resetTime)
            resetTime = resetTime.AddDays(1);
        
        return resetTime - now;
    }

    private TimeSpan GetTimeUntilWeeklyReset()
    {
        var now = DateTime.UtcNow;
        var daysUntilTuesday = ((int)DayOfWeek.Tuesday - (int)now.DayOfWeek + 7) % 7;
        if (daysUntilTuesday == 0 && now.Hour >= 8)
            daysUntilTuesday = 7;
        
        var resetTime = now.Date.AddDays(daysUntilTuesday).AddHours(8);
        return resetTime - now;
    }

    private string FormatTimeSpan(TimeSpan time)
    {
        if (time.TotalDays >= 1)
            return $"{(int)time.TotalDays}d {time.Hours}h {time.Minutes}m";
        else
            return $"{time.Hours}h {time.Minutes}m";
    }

    private void SaveUIState()
    {
        PluginInstance.Configuration.UISettings.ExpandedSections = new Dictionary<string, bool>(_expandedSections);
        PluginInstance.Configuration.Save();
    }
    
    private void LoadUIState()
    {
        if (PluginInstance.Configuration.UISettings.ExpandedSections != null)
        {
            _expandedSections.Clear();
            foreach (var kvp in PluginInstance.Configuration.UISettings.ExpandedSections)
            {
                _expandedSections[kvp.Key] = kvp.Value;
            }
        }
    }

    public override void OnOpen()
    {
        base.OnOpen();
        LoadUIState();
    }

    public override void OnClose()
    {
        base.OnClose();
        SaveUIState();
    }
}
