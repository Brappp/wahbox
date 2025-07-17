using System;
using System.Collections.Generic;
using System.IO;
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

    private Tab _currentTab = Tab.Tracking;
    private readonly Dictionary<string, bool> _expandedSections = new();
    
    private enum Tab
    {
        Tracking,
        Utilities,
        Inventory,
        Settings
    }

    public MainWindow(Plugin plugin) : base("WahBox")
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
        // Tab bar
        if (ImGui.BeginTabBar("MainTabs"))
        {
            if (ImGui.BeginTabItem("Tracking"))
            {
                _currentTab = Tab.Tracking;
                ImGui.EndTabItem();
            }
            
            if (ImGui.BeginTabItem("Utilities"))
            {
                _currentTab = Tab.Utilities;
                ImGui.EndTabItem();
            }
            
            if (ImGui.BeginTabItem("Inventory"))
            {
                _currentTab = Tab.Inventory;
                ImGui.EndTabItem();
            }
            
            if (ImGui.BeginTabItem("Settings"))
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
            case Tab.Inventory:
                DrawInventoryTab();
                break;
            case Tab.Settings:
                DrawSettingsTab();
                break;
        }
        
        ImGui.EndChild();
    }



    private void DrawTrackingTab()
    {
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
        if (!PluginInstance.Configuration.UISettings.ShowCurrencyModules) return;
        
        var currencyModules = PluginInstance.ModuleManager.GetModules()
            .Where(m => m.Type == ModuleType.Currency)
            .Where(m => PassesFilter(m))
            .Where(m => GetModuleVisibility(m.Name))
            .GroupBy(m => GetCurrencyCategory(m.Name))
            .OrderBy(g => g.Key);

        if (!currencyModules.Any()) return;

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
            ImGui.SameLine(300);
            
            var current = currencyModule.GetCurrentAmount();
            var max = currencyModule.GetMaxAmount();
            var percent = max > 0 ? (float)current / max * 100f : 0f;
            
            // Progress bar with visual threshold coloring (always shows red at 90%+ for visual feedback)
            var progressBarSize = new Vector2(150, 20);
            var threshold = currencyModule.AlertThreshold;
            var visualThreshold = threshold > 0 ? threshold : 90; // Use custom threshold or default to 90% for visual feedback
            var color = percent >= visualThreshold ? new Vector4(0.8f, 0.2f, 0.2f, 1) :
                       percent >= 75 ? new Vector4(0.8f, 0.8f, 0.2f, 1) :
                       new Vector4(0.2f, 0.8f, 0.2f, 1);
            
            ImGui.PushStyleColor(ImGuiCol.PlotHistogram, color);
            ImGui.ProgressBar(percent / 100f, progressBarSize, $"{current:N0}/{max:N0}");
            ImGui.PopStyleColor();
            
            // Inline alert threshold settings
            ImGui.SameLine();
            ImGui.TextUnformatted("Alert:");
            ImGui.SameLine();
            ImGui.SetNextItemWidth(50);
            
            var alertThreshold = currencyModule.AlertThreshold;
            if (ImGui.InputInt($"##{module.Name}_threshold", ref alertThreshold, 0, 0))
            {
                if (alertThreshold >= 0 && alertThreshold <= 100)
                {
                    Plugin.Log.Information($"User changed {module.Name} alert threshold from {currencyModule.AlertThreshold} to {alertThreshold}");
                    currencyModule.AlertThreshold = alertThreshold;
                    module.SaveConfiguration();
                }
                else
                {
                    Plugin.Log.Warning($"User tried to set invalid alert threshold for {module.Name}: {alertThreshold}");
                }
            }
            ImGui.SameLine();
            ImGui.TextUnformatted("%");
            
            // No Alert button
            ImGui.SameLine();
            if (ImGui.SmallButton($"No Alert##{module.Name}"))
            {
                Plugin.Log.Information($"User clicked 'No Alert' for {module.Name}, setting threshold from {currencyModule.AlertThreshold} to 0");
                currencyModule.AlertThreshold = 0;
                module.SaveConfiguration();
            }
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
        if (!PluginInstance.Configuration.UISettings.ShowDailyModules) return;
        
        var dailyModules = PluginInstance.ModuleManager.GetModules()
            .Where(m => m.Type == ModuleType.Daily)
            .Where(m => PassesFilter(m))
            .Where(m => GetModuleVisibility(m.Name))
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
        if (!PluginInstance.Configuration.UISettings.ShowWeeklyModules) return;
        
        var weeklyModules = PluginInstance.ModuleManager.GetModules()
            .Where(m => m.Type == ModuleType.Weekly)
            .Where(m => PassesFilter(m))
            .Where(m => GetModuleVisibility(m.Name))
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
        if (!PluginInstance.Configuration.UISettings.ShowSpecialModules) return;
        
        var specialModules = PluginInstance.ModuleManager.GetModules()
            .Where(m => m.Type == ModuleType.Special)
            .Where(m => PassesFilter(m))
            .Where(m => GetModuleVisibility(m.Name))
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
            ImGui.SameLine(ImGui.GetContentRegionMax().X - 50);
            if (ImGui.SmallButton("Open"))
            {
                module.OpenWindow();
            }
        }
        
        ImGui.EndGroup();
        
        ImGui.PopID();
    }

    private void DrawUtilitiesTab()
    {
        var utilityModules = PluginInstance.ModuleManager.GetModules()
            .Where(m => m.Category == ModuleCategory.Utility)
            .ToList();

        // Utility modules as dropdowns
        ImGui.BeginChild("UtilityModules", new Vector2(0, 0), false);
        
        foreach (var module in utilityModules)
        {
            DrawUtilityDropdown(module);
        }
        
        ImGui.EndChild();
    }

    private void DrawUtilityDropdown(IModule module)
    {
        ImGui.PushID(module.Name);
        
        // Enable checkbox on the left
        var enabled = module.IsEnabled;
        if (ImGui.Checkbox("", ref enabled))
        {
            module.IsEnabled = enabled;
            
            // If disabling an active utility module, stop it
            if (!enabled && module.Status == ModuleStatus.Active)
            {
                module.CloseWindow();
            }
            // If enabling and the module has a window, open it
            else if (enabled && module.HasWindow)
            {
                module.OpenWindow();
            }
            
            module.SaveConfiguration();
        }
        
        ImGui.SameLine();
        
        // Status indicator
        var statusColor = module.Status == ModuleStatus.Active ? 
            new Vector4(0.2f, 0.8f, 0.2f, 1) : 
            new Vector4(0.5f, 0.5f, 0.5f, 1);
        var statusIcon = module.Status == ModuleStatus.Active ? "●" : "○";
        
        ImGui.TextColored(statusColor, statusIcon);
        ImGui.SameLine();
        
        // Collapsible tree node with module name
        if (ImGui.TreeNode($"{module.Name}"))
        {
            ImGui.Indent();
            
            // Settings area - all configuration directly here
            module.DrawConfig();
            
            ImGui.Unindent();
            ImGui.TreePop();
        }
        
        ImGui.PopID();
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
        
        // Module Visibility
        if (ImGui.CollapsingHeader("Module Visibility", ImGuiTreeNodeFlags.DefaultOpen))
        {
            ImGui.Indent();
            
            var showCurrency = PluginInstance.Configuration.UISettings.ShowCurrencyModules;
            if (ImGui.Checkbox("Show Currency Modules", ref showCurrency))
            {
                PluginInstance.Configuration.UISettings.ShowCurrencyModules = showCurrency;
                PluginInstance.Configuration.Save();
            }
            
            var showDaily = PluginInstance.Configuration.UISettings.ShowDailyModules;
            if (ImGui.Checkbox("Show Daily Modules", ref showDaily))
            {
                PluginInstance.Configuration.UISettings.ShowDailyModules = showDaily;
                PluginInstance.Configuration.Save();
            }
            
            var showWeekly = PluginInstance.Configuration.UISettings.ShowWeeklyModules;
            if (ImGui.Checkbox("Show Weekly Modules", ref showWeekly))
            {
                PluginInstance.Configuration.UISettings.ShowWeeklyModules = showWeekly;
                PluginInstance.Configuration.Save();
            }
            
            var showSpecial = PluginInstance.Configuration.UISettings.ShowSpecialModules;
            if (ImGui.Checkbox("Show Special Modules", ref showSpecial))
            {
                PluginInstance.Configuration.UISettings.ShowSpecialModules = showSpecial;
                PluginInstance.Configuration.Save();
            }
            
            ImGui.Unindent();
        }
        
        ImGui.Spacing();
        
        // Notifications
        if (ImGui.CollapsingHeader("Tracking Notifications", ImGuiTreeNodeFlags.DefaultOpen))
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
                try
                {
                    var json = System.Text.Json.JsonSerializer.Serialize(PluginInstance.Configuration, new System.Text.Json.JsonSerializerOptions 
                    { 
                        WriteIndented = true 
                    });
                    
                    // Copy to clipboard
                    ImGui.SetClipboardText(json);
                    Plugin.Log.Information("Configuration exported to clipboard");
                    
                    // Optional: Save to file
                    var exportPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "WahBox_Config_Export.json");
                    File.WriteAllText(exportPath, json);
                    Plugin.Log.Information($"Configuration exported to: {exportPath}");
                }
                catch (Exception ex)
                {
                    Plugin.Log.Error(ex, "Failed to export configuration");
                }
            }
            if (ImGui.IsItemHovered())
            {
                ImGui.SetTooltip("Export configuration to clipboard and desktop file");
            }
            
            ImGui.SameLine();
            
            if (ImGui.Button("Import Settings"))
            {
                try
                {
                    var clipboardText = ImGui.GetClipboardText();
                    if (!string.IsNullOrEmpty(clipboardText))
                    {
                        var importedConfig = System.Text.Json.JsonSerializer.Deserialize<Configuration>(clipboardText);
                        if (importedConfig != null)
                        {
                            // Copy imported settings (character data is preserved automatically)
                            PluginInstance.Configuration.EnabledModules = importedConfig.EnabledModules;
                            PluginInstance.Configuration.ModuleConfigs = importedConfig.ModuleConfigs;
                            PluginInstance.Configuration.UISettings = importedConfig.UISettings;
                            PluginInstance.Configuration.NotificationSettings = importedConfig.NotificationSettings;
                            
                            PluginInstance.Configuration.Save();
                            Plugin.Log.Information("Configuration imported from clipboard successfully");
                        }
                    }
                    else
                    {
                        Plugin.Log.Warning("Clipboard is empty or contains no text");
                    }
                }
                catch (Exception ex)
                {
                    Plugin.Log.Error(ex, "Failed to import configuration from clipboard");
                }
            }
            if (ImGui.IsItemHovered())
            {
                ImGui.SetTooltip("Import configuration from clipboard (JSON format)");
            }
            
            ImGui.SameLine();
            
            if (ImGui.Button("Reset to Defaults"))
            {
                if (ImGui.IsKeyDown(ImGuiKey.LeftShift))
                {
                    PluginInstance.Configuration.ResetToDefaults();
                    PluginInstance.Configuration.Save();
                    
                    // Reload all module configurations
                    foreach (var module in PluginInstance.ModuleManager.GetModules())
                    {
                        module.LoadConfiguration();
                    }
                    
                    Plugin.Log.Information("All modules reloaded with default configuration");
                }
            }
            if (ImGui.IsItemHovered())
            {
                ImGui.SetTooltip("Hold Shift and click to reset all settings to defaults");
            }
            
            ImGui.SameLine();
            
            if (ImGui.Button("Debug Config"))
            {
                Plugin.Log.Information("=== Configuration Debug Info ===");
                Plugin.Log.Information($"Enabled Modules: {string.Join(", ", PluginInstance.Configuration.EnabledModules)}");
                Plugin.Log.Information($"Module Configs Count: {PluginInstance.Configuration.ModuleConfigs.Count}");
                
                foreach (var kvp in PluginInstance.Configuration.ModuleConfigs)
                {
                    Plugin.Log.Information($"Module '{kvp.Key}' config: {System.Text.Json.JsonSerializer.Serialize(kvp.Value)}");
                }
                Plugin.Log.Information("=== End Configuration Debug ===");
            }
            if (ImGui.IsItemHovered())
            {
                ImGui.SetTooltip("Log current configuration to debug output");
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
            ModuleStatus.Complete => "[✓]",
            ModuleStatus.InProgress => "[~]",
            ModuleStatus.Incomplete => "[X]",
            ModuleStatus.Active => "[●]",
            ModuleStatus.Inactive => "[○]",
            _ => "[?]"
        };
    }


    
    private void DrawModuleVisibilityToggles()
    {
        var allModules = PluginInstance.ModuleManager.GetModules()
            .GroupBy(m => m.Type)
            .OrderBy(g => g.Key);
        
        foreach (var typeGroup in allModules)
        {
            if (ImGui.TreeNode($"{typeGroup.Key} Modules"))
            {
                foreach (var module in typeGroup.OrderBy(m => m.Name))
                {
                    var isVisible = GetModuleVisibility(module.Name);
                    if (ImGui.Checkbox($"{module.Name}##visibility", ref isVisible))
                    {
                        SetModuleVisibility(module.Name, isVisible);
                    }
                }
                ImGui.TreePop();
            }
        }
    }
    
    private bool GetModuleVisibility(string moduleName)
    {
        return PluginInstance.Configuration.UISettings.VisibleModules.GetValueOrDefault(moduleName, true);
    }
    
    private void SetModuleVisibility(string moduleName, bool visible)
    {
        PluginInstance.Configuration.UISettings.VisibleModules[moduleName] = visible;
        PluginInstance.Configuration.Save();
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

    private void DrawInventoryTab()
    {
        // Get the inventory module
        var inventoryModule = PluginInstance.ModuleManager.GetModules()
            .FirstOrDefault(m => m.Name == "Inventory Manager");

        if (inventoryModule == null)
        {
            ImGui.Text("Inventory module not loaded.");
            return;
        }

        // If the module has a window, we'll embed its content here
        if (inventoryModule is IDrawable drawable)
        {
            drawable.Draw();
        }
        else if (inventoryModule.HasWindow)
        {
            // Open module window button
            if (ImGui.Button("Open Inventory Manager"))
            {
                inventoryModule.OpenWindow();
            }
        }
        else
        {
            ImGui.Text("Inventory module does not have a drawable interface.");
        }
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
        
        // Load window size from config (but not position - let ImGui handle positioning)
        if (PluginInstance.Configuration.UISettings.MainWindowSize != Vector2.Zero)
        {
            Size = PluginInstance.Configuration.UISettings.MainWindowSize;
        }
    }

    public override void OnClose()
    {
        base.OnClose();
        SaveUIState();
        
        // Save window size to config (but not position - let ImGui handle positioning)
        if (Size.HasValue)
        {
            PluginInstance.Configuration.UISettings.MainWindowSize = Size.Value;
            PluginInstance.Configuration.Save();
        }
    }
}
