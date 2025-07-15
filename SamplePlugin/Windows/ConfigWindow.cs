using System;
using System.Linq;
using System.Numerics;
using Dalamud.Interface.Windowing;
using ImGuiNET;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Components;
using Dalamud.Interface;
using SamplePlugin.Core.Interfaces;

namespace SamplePlugin.Windows;

public class ConfigWindow : Window, IDisposable
{
    private Configuration Configuration;
    private Plugin Plugin;
    private string _moduleSearch = string.Empty;
    private ModuleType? _selectedModuleType = null;
    private IModule? _selectedModule = null;

    public ConfigWindow(Plugin plugin) : base("Wahdori Settings###WahdoriConfig")
    {
        Size = new Vector2(400, 350);
        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(350, 300),
            MaximumSize = new Vector2(float.MaxValue, float.MaxValue)
        };

        Configuration = plugin.Configuration;
        Plugin = plugin;
    }

    public void Dispose() { }

    public override void Draw()
    {
        // Compact tab bar - using text labels since icons in tabs are tricky
        if (ImGui.BeginTabBar("ConfigTabs"))
        {
            if (ImGui.BeginTabItem("General"))
            {
                DrawGeneralSettings();
                ImGui.EndTabItem();
            }

            if (ImGui.BeginTabItem("Modules"))
            {
                DrawModulesSettings();
                ImGui.EndTabItem();
            }

            if (ImGui.BeginTabItem("Overlays"))
            {
                DrawOverlaySettings();
                ImGui.EndTabItem();
            }

            if (ImGui.BeginTabItem("Alerts"))
            {
                DrawAlertSettings();
                ImGui.EndTabItem();
            }

            ImGui.EndTabBar();
        }
    }

    private void DrawGeneralSettings()
    {
        // Window Settings
        ImGui.Text("Window Settings");
        ImGui.Separator();
        
        var opacity = Configuration.UISettings.WindowOpacity;
        ImGui.SetNextItemWidth(150);
        if (ImGui.SliderFloat("Opacity", ref opacity, 0.1f, 1.0f, "%.1f"))
        {
            Configuration.UISettings.WindowOpacity = opacity;
            Configuration.Save();
        }
        
        var hideInCombat = Configuration.UISettings.HideInCombat;
        if (ImGui.Checkbox("Hide in Combat", ref hideInCombat))
        {
            Configuration.UISettings.HideInCombat = hideInCombat;
            Configuration.Save();
        }
        
        ImGui.SameLine();
        var hideInDuty = Configuration.UISettings.HideInDuty;
        if (ImGui.Checkbox("Hide in Duty", ref hideInDuty))
        {
            Configuration.UISettings.HideInDuty = hideInDuty;
            Configuration.Save();
        }
        
        ImGui.Spacing();
        
        // Display Settings
        ImGui.Text("Display");
        ImGui.Separator();
        
        var sortByStatus = Configuration.UISettings.SortByStatus;
        if (ImGui.Checkbox("Sort by Status", ref sortByStatus))
        {
            Configuration.UISettings.SortByStatus = sortByStatus;
            Configuration.Save();
        }
        
        ImGui.SameLine();
        var showDisabledModules = Configuration.UISettings.ShowDisabledModules;
        if (ImGui.Checkbox("Show Disabled", ref showDisabledModules))
        {
            Configuration.UISettings.ShowDisabledModules = showDisabledModules;
            Configuration.Save();
        }
        
        ImGui.Spacing();
        
        // Language
        var currentLang = Configuration.Language;
        ImGui.SetNextItemWidth(150);
        if (ImGui.BeginCombo("Language", currentLang))
        {
            foreach (var lang in new[] { "English", "日本語", "Deutsch", "Français" })
            {
                if (ImGui.Selectable(lang, lang == currentLang))
                {
                    Configuration.Language = lang;
                    Configuration.Save();
                    Plugin.LocalizationManager.SetLanguage(lang);
                }
            }
            ImGui.EndCombo();
        }
    }

    private void DrawModulesSettings()
    {
        // Module type filter
        ImGui.SetNextItemWidth(150);
        if (ImGui.BeginCombo("Filter", _selectedModuleType?.ToString() ?? "All Types"))
        {
            if (ImGui.Selectable("All Types", _selectedModuleType == null))
                _selectedModuleType = null;
                
            foreach (ModuleType type in Enum.GetValues<ModuleType>())
            {
                if (ImGui.Selectable(type.ToString(), _selectedModuleType == type))
                    _selectedModuleType = type;
            }
            ImGui.EndCombo();
        }
        
        ImGui.SameLine();
        ImGui.SetNextItemWidth(200);
        ImGui.InputTextWithHint("##ModuleSearch", "Search modules...", ref _moduleSearch, 100);
        
        ImGui.Separator();
        
        // Module list and configuration
        var childHeight = ImGui.GetContentRegionAvail().Y;
        
        // Left panel - module list
        if (ImGui.BeginChild("ModuleList", new Vector2(200, childHeight), true))
        {
            var modules = Plugin.ModuleManager.GetModules()
                .Where(m => _selectedModuleType == null || m.Type == _selectedModuleType)
                .Where(m => string.IsNullOrEmpty(_moduleSearch) || 
                           m.Name.Contains(_moduleSearch, StringComparison.OrdinalIgnoreCase))
                .OrderBy(m => m.Type)
                .ThenBy(m => m.Name);
                
            foreach (var module in modules)
            {
                var isSelected = _selectedModule == module;
                var isEnabled = module.IsEnabled;
                
                // Module item with checkbox
                ImGui.PushID(module.Name);
                
                if (ImGui.Checkbox("##Enable", ref isEnabled))
                {
                    module.IsEnabled = isEnabled;
                    Configuration.Save();
                }
                
                ImGui.SameLine();
                
                if (ImGui.Selectable(module.Name, isSelected))
                {
                    _selectedModule = module;
                }
                
                ImGui.PopID();
            }
        }
        ImGui.EndChild();
        
        ImGui.SameLine();
        
        // Right panel - module configuration
        if (ImGui.BeginChild("ModuleConfig", new Vector2(0, childHeight), true))
        {
            if (_selectedModule != null)
            {
                ImGui.Text($"{_selectedModule.Name} Configuration");
                ImGui.Text($"Type: {_selectedModule.Type}");
                ImGui.Text($"Status: {_selectedModule.Status}");
                ImGui.Separator();
                
                _selectedModule.DrawConfig();
            }
            else
            {
                ImGui.TextColored(new Vector4(0.5f, 0.5f, 0.5f, 1), "Select a module to configure");
            }
        }
        ImGui.EndChild();
    }

    private void DrawOverlaySettings()
    {
        ImGui.TextWrapped("Overlay Settings");
        ImGui.Separator();
        ImGui.Spacing();
        
        // Global overlay settings
        var overlaysEnabled = Configuration.OverlaySettings.Enabled;
        if (ImGui.Checkbox("Enable Overlays", ref overlaysEnabled))
        {
            Configuration.OverlaySettings.Enabled = overlaysEnabled;
            Configuration.Save();
        }
        
        if (overlaysEnabled)
        {
            ImGui.Spacing();
            
            // Opacity
            var opacity = Configuration.OverlaySettings.Opacity;
            ImGui.SetNextItemWidth(200);
            if (ImGui.SliderFloat("Overlay Opacity", ref opacity, 0.1f, 1.0f, "%.1f"))
            {
                Configuration.OverlaySettings.Opacity = opacity;
                Configuration.Save();
            }
            
            // Background
            var showBackground = Configuration.OverlaySettings.ShowBackground;
            if (ImGui.Checkbox("Show Background", ref showBackground))
            {
                Configuration.OverlaySettings.ShowBackground = showBackground;
                Configuration.Save();
            }
            
            ImGui.Spacing();
            ImGui.Separator();
            ImGui.Spacing();
            
            // Individual overlay settings
            if (ImGui.CollapsingHeader("Currency Warnings Overlay"))
            {
                ImGui.Indent();
                
                var showCurrency = Configuration.OverlaySettings.ShowCurrencyWarnings;
                if (ImGui.Checkbox("Show Currency Warnings", ref showCurrency))
                {
                    Configuration.OverlaySettings.ShowCurrencyWarnings = showCurrency;
                    Configuration.Save();
                }
                
                ImGui.Unindent();
            }
            
            if (ImGui.CollapsingHeader("Daily Tasks Overlay"))
            {
                ImGui.Indent();
                
                var showDaily = Configuration.OverlaySettings.ShowDailyTasks;
                if (ImGui.Checkbox("Show Daily Tasks", ref showDaily))
                {
                    Configuration.OverlaySettings.ShowDailyTasks = showDaily;
                    Configuration.Save();
                }
                
                ImGui.Unindent();
            }
            
            if (ImGui.CollapsingHeader("Weekly Tasks Overlay"))
            {
                ImGui.Indent();
                
                var showWeekly = Configuration.OverlaySettings.ShowWeeklyTasks;
                if (ImGui.Checkbox("Show Weekly Tasks", ref showWeekly))
                {
                    Configuration.OverlaySettings.ShowWeeklyTasks = showWeekly;
                    Configuration.Save();
                }
                
                ImGui.Unindent();
            }
        }
    }

    private void DrawAlertSettings()
    {
        ImGui.TextWrapped("Alert Settings");
        ImGui.Separator();
        ImGui.Spacing();
        
        // Chat alerts
        if (ImGui.CollapsingHeader("Chat Alerts", ImGuiTreeNodeFlags.DefaultOpen))
        {
            ImGui.Indent();
            
            var chatAlerts = Configuration.NotificationSettings.ChatNotifications;
            if (ImGui.Checkbox("Enable Chat Notifications", ref chatAlerts))
            {
                Configuration.NotificationSettings.ChatNotifications = chatAlerts;
                Configuration.Save();
            }
            
            if (chatAlerts)
            {
                var suppressInDuty = Configuration.NotificationSettings.SuppressInDuty;
                if (ImGui.Checkbox("Suppress in Duty", ref suppressInDuty))
                {
                    Configuration.NotificationSettings.SuppressInDuty = suppressInDuty;
                    Configuration.Save();
                }
                
                var cooldown = Configuration.NotificationSettings.NotificationCooldown;
                ImGui.SetNextItemWidth(200);
                if (ImGui.SliderInt("Notification Cooldown (min)", ref cooldown, 1, 60))
                {
                    Configuration.NotificationSettings.NotificationCooldown = cooldown;
                    Configuration.Save();
                }
            }
            
            ImGui.Unindent();
        }
        
        // Sound alerts
        if (ImGui.CollapsingHeader("Sound Alerts"))
        {
            ImGui.Indent();
            
            var soundAlerts = Configuration.NotificationSettings.SoundNotifications;
            if (ImGui.Checkbox("Enable Sound Notifications", ref soundAlerts))
            {
                Configuration.NotificationSettings.SoundNotifications = soundAlerts;
                Configuration.Save();
            }
            
            ImGui.Unindent();
        }
        
        // Module-specific settings
        if (ImGui.CollapsingHeader("Module Alert Settings"))
        {
            ImGui.Indent();
            ImGui.TextWrapped("Configure alerts for specific module types:");
            ImGui.Spacing();
            
            var currencyAlerts = Configuration.NotificationSettings.CurrencyWarningAlerts;
            if (ImGui.Checkbox("Currency Cap Warnings", ref currencyAlerts))
            {
                Configuration.NotificationSettings.CurrencyWarningAlerts = currencyAlerts;
                Configuration.Save();
            }
            
            var taskCompleteAlerts = Configuration.NotificationSettings.TaskCompletionAlerts;
            if (ImGui.Checkbox("Task Completion Alerts", ref taskCompleteAlerts))
            {
                Configuration.NotificationSettings.TaskCompletionAlerts = taskCompleteAlerts;
                Configuration.Save();
            }
            
            ImGui.Unindent();
        }
    }
}
