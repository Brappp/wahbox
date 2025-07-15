using System;
using System.Numerics;
using Dalamud.Interface.Windowing;
using ImGuiNET;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;

namespace SamplePlugin.Windows;

public class ConfigWindow : Window, IDisposable
{
    private Configuration Configuration;
    private Plugin Plugin;

    public ConfigWindow(Plugin plugin) : base("Wahdori Configuration###WahdoriConfig")
    {
        Size = new Vector2(600, 400);
        SizeCondition = ImGuiCond.FirstUseEver;

        Configuration = plugin.Configuration;
        Plugin = plugin;
    }

    public void Dispose() { }

    public override void Draw()
    {
        if (ImGui.BeginTabBar("ConfigTabs"))
        {
            if (ImGui.BeginTabItem("General"))
            {
                DrawGeneralSettings();
                ImGui.EndTabItem();
            }

            if (ImGui.BeginTabItem("Currency"))
            {
                DrawCurrencySettings();
                ImGui.EndTabItem();
            }

            if (ImGui.BeginTabItem("Tasks"))
            {
                DrawTaskSettings();
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

            if (ImGui.BeginTabItem("Notifications"))
            {
                DrawNotificationSettings();
                ImGui.EndTabItem();
            }

            ImGui.EndTabBar();
        }
    }

    private void DrawGeneralSettings()
    {
        ImGui.TextWrapped("General settings for Wahdori");
        ImGui.Separator();

        var configValue = Configuration.SomePropertyToBeSavedAndWithADefault;
        if (ImGui.Checkbox("Example Setting", ref configValue))
        {
            Configuration.SomePropertyToBeSavedAndWithADefault = configValue;
            Configuration.Save();
        }

        ImGui.Spacing();

        if (ImGui.CollapsingHeader("UI Settings"))
        {
            var opacity = Configuration.UISettings.WindowOpacity;
            if (ImGui.SliderFloat("Window Opacity", ref opacity, 0.1f, 1.0f))
            {
                Configuration.UISettings.WindowOpacity = opacity;
                Configuration.Save();
            }

            var compactMode = Configuration.UISettings.UseCompactMode;
            if (ImGui.Checkbox("Use Compact Mode", ref compactMode))
            {
                Configuration.UISettings.UseCompactMode = compactMode;
                Configuration.Save();
            }

            var sortByStatus = Configuration.UISettings.SortByStatus;
            if (ImGui.Checkbox("Sort Modules by Status", ref sortByStatus))
            {
                Configuration.UISettings.SortByStatus = sortByStatus;
                Configuration.Save();
            }
        }
    }

    private void DrawCurrencySettings()
    {
        ImGui.TextWrapped("Configure currency tracking and alerts");
        ImGui.Separator();

        var showOverlay = Configuration.CurrencySettings.ShowCurrencyOverlay;
        if (ImGui.Checkbox("Show Currency Overlay", ref showOverlay))
        {
            Configuration.CurrencySettings.ShowCurrencyOverlay = showOverlay;
            Configuration.Save();
        }

        var hideInDuties = Configuration.CurrencySettings.HideInDuties;
        if (ImGui.Checkbox("Hide in Duties", ref hideInDuties))
        {
            Configuration.CurrencySettings.HideInDuties = hideInDuties;
            Configuration.Save();
        }

        var chatWarning = Configuration.CurrencySettings.ChatWarning;
        if (ImGui.Checkbox("Enable Chat Warnings", ref chatWarning))
        {
            Configuration.CurrencySettings.ChatWarning = chatWarning;
            Configuration.Save();
        }
    }

    private void DrawTaskSettings()
    {
        ImGui.TextWrapped("Configure task tracking");
        ImGui.Separator();

        var todoEnabled = Configuration.TodoSettings.Enabled;
        if (ImGui.Checkbox("Enable Todo List Overlay", ref todoEnabled))
        {
            Configuration.TodoSettings.Enabled = todoEnabled;
            Configuration.Save();
        }

        var hideInDuties = Configuration.TodoSettings.HideInDuties;
        if (ImGui.Checkbox("Hide in Duties", ref hideInDuties))
        {
            Configuration.TodoSettings.HideInDuties = hideInDuties;
            Configuration.Save();
        }

        var hideInQuests = Configuration.TodoSettings.HideDuringQuests;
        if (ImGui.Checkbox("Hide During Quests", ref hideInQuests))
        {
            Configuration.TodoSettings.HideDuringQuests = hideInQuests;
            Configuration.Save();
        }
    }

    private void DrawModulesSettings()
    {
        ImGui.TextWrapped("Configure individual modules");
        ImGui.Separator();

        // Group modules by type
        if (ImGui.CollapsingHeader("Currency Modules"))
        {
            foreach (var module in Plugin.ModuleManager.GetModules())
            {
                if (module.Type == Core.Interfaces.ModuleType.Currency)
                {
                    ImGui.PushID(module.Name);
                    
                    var enabled = module.IsEnabled;
                    if (ImGui.Checkbox($"{module.Name}", ref enabled))
                    {
                        module.IsEnabled = enabled;
                        Configuration.Save();
                    }
                    
                    ImGui.SameLine();
                    ImGui.TextDisabled($"({module.Status})");
                    
                    if (ImGui.TreeNode($"Settings##{module.Name}"))
                    {
                        module.DrawConfig();
                        ImGui.TreePop();
                    }
                    
                    ImGui.PopID();
                }
            }
        }

        if (ImGui.CollapsingHeader("Daily Modules"))
        {
            foreach (var module in Plugin.ModuleManager.GetModules())
            {
                if (module.Type == Core.Interfaces.ModuleType.Daily)
                {
                    ImGui.PushID(module.Name);
                    
                    var enabled = module.IsEnabled;
                    if (ImGui.Checkbox($"{module.Name}", ref enabled))
                    {
                        module.IsEnabled = enabled;
                        Configuration.Save();
                    }
                    
                    ImGui.SameLine();
                    ImGui.TextDisabled($"({module.Status})");
                    
                    if (ImGui.TreeNode($"Settings##{module.Name}"))
                    {
                        module.DrawConfig();
                        ImGui.TreePop();
                    }
                    
                    ImGui.PopID();
                }
            }
        }

        if (ImGui.CollapsingHeader("Weekly Modules"))
        {
            foreach (var module in Plugin.ModuleManager.GetModules())
            {
                if (module.Type == Core.Interfaces.ModuleType.Weekly)
                {
                    ImGui.PushID(module.Name);
                    
                    var enabled = module.IsEnabled;
                    if (ImGui.Checkbox($"{module.Name}", ref enabled))
                    {
                        module.IsEnabled = enabled;
                        Configuration.Save();
                    }
                    
                    ImGui.SameLine();
                    ImGui.TextDisabled($"({module.Status})");
                    
                    if (ImGui.TreeNode($"Settings##{module.Name}"))
                    {
                        module.DrawConfig();
                        ImGui.TreePop();
                    }
                    
                    ImGui.PopID();
                }
            }
        }
    }

    private void DrawOverlaySettings()
    {
        ImGui.TextWrapped("Configure overlay positions and appearance");
        ImGui.Separator();

        // Add OverlayManager configuration
        Plugin.OverlayManager.DrawConfiguration();

        ImGui.Separator();

        if (ImGui.CollapsingHeader("Currency Overlay"))
        {
            var pos = Configuration.CurrencySettings.OverlayPosition;
            if (ImGui.DragFloat2("Position", ref pos))
            {
                Configuration.CurrencySettings.OverlayPosition = pos;
                Configuration.Save();
            }

            var size = Configuration.CurrencySettings.OverlaySize;
            if (ImGui.DragFloat2("Size", ref size))
            {
                Configuration.CurrencySettings.OverlaySize = size;
                Configuration.Save();
            }
        }

        if (ImGui.CollapsingHeader("Todo List Overlay"))
        {
            var pos = Configuration.TodoSettings.Position;
            if (ImGui.DragFloat2("Position", ref pos))
            {
                Configuration.TodoSettings.Position = pos;
                Configuration.Save();
            }

            var size = Configuration.TodoSettings.Size;
            if (ImGui.DragFloat2("Size", ref size))
            {
                Configuration.TodoSettings.Size = size;
                Configuration.Save();
            }
        }

        if (ImGui.CollapsingHeader("Timer Overlays"))
        {
            var dailyPos = Configuration.TimerSettings.DailyTimerPosition;
            if (ImGui.DragFloat2("Daily Timer Position", ref dailyPos))
            {
                Configuration.TimerSettings.DailyTimerPosition = dailyPos;
                Configuration.Save();
            }

            var weeklyPos = Configuration.TimerSettings.WeeklyTimerPosition;
            if (ImGui.DragFloat2("Weekly Timer Position", ref weeklyPos))
            {
                Configuration.TimerSettings.WeeklyTimerPosition = weeklyPos;
                Configuration.Save();
            }
        }
    }

    private void DrawNotificationSettings()
    {
        ImGui.TextWrapped("Configure notifications");
        ImGui.Separator();

        var chatNotif = Configuration.NotificationSettings.EnableChatNotifications;
        if (ImGui.Checkbox("Enable Chat Notifications", ref chatNotif))
        {
            Configuration.NotificationSettings.EnableChatNotifications = chatNotif;
            Configuration.Save();
        }

        var toastNotif = Configuration.NotificationSettings.EnableToastNotifications;
        if (ImGui.Checkbox("Enable Toast Notifications", ref toastNotif))
        {
            Configuration.NotificationSettings.EnableToastNotifications = toastNotif;
            Configuration.Save();
        }

        var soundNotif = Configuration.NotificationSettings.EnableSoundNotifications;
        if (ImGui.Checkbox("Enable Sound Notifications", ref soundNotif))
        {
            Configuration.NotificationSettings.EnableSoundNotifications = soundNotif;
            Configuration.Save();
        }

        var threshold = Configuration.NotificationSettings.NotificationThreshold;
        if (ImGui.SliderInt("Notification Threshold %", ref threshold, 0, 100))
        {
            Configuration.NotificationSettings.NotificationThreshold = threshold;
            Configuration.Save();
        }
    }
}
