using System;
using System.Linq;
using System.Numerics;
using Dalamud.Interface.Windowing;
using ImGuiNET;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Components;
using SamplePlugin.Core.Interfaces;

namespace SamplePlugin.Windows;

public class ConfigWindow : Window, IDisposable
{
    private Configuration Configuration;
    private Plugin Plugin;
    private string _moduleSearch = string.Empty;

    public ConfigWindow(Plugin plugin) : base("Wahdori Settings###WahdoriConfig")
    {
        Size = new Vector2(500, 450);
        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(450, 400),
            MaximumSize = new Vector2(600, 600)
        };

        Configuration = plugin.Configuration;
        Plugin = plugin;
    }

    public void Dispose() { }

    public override void Draw()
    {
        // Compact tab bar with icons
        if (ImGui.BeginTabBar("ConfigTabs"))
        {
            if (ImGui.BeginTabItem("⚙ General"))
            {
                DrawGeneralSettings();
                ImGui.EndTabItem();
            }

            if (ImGui.BeginTabItem("📦 Modules"))
            {
                DrawModulesSettings();
                ImGui.EndTabItem();
            }

            if (ImGui.BeginTabItem("🔲 Overlays"))
            {
                DrawOverlaySettings();
                ImGui.EndTabItem();
            }

            if (ImGui.BeginTabItem("🔔 Alerts"))
            {
                DrawAlertSettings();
                ImGui.EndTabItem();
            }

            ImGui.EndTabBar();
        }
    }

    private void DrawGeneralSettings()
    {
        ImGui.TextWrapped("General Settings");
        ImGui.Separator();
        ImGui.Spacing();

        // Window Settings
        if (ImGui.CollapsingHeader("Window Settings", ImGuiTreeNodeFlags.DefaultOpen))
        {
            ImGui.Indent();
            
            var opacity = Configuration.UISettings.WindowOpacity;
            ImGui.SetNextItemWidth(200);
            if (ImGui.SliderFloat("Window Opacity", ref opacity, 0.1f, 1.0f, "%.1f"))
            {
                Configuration.UISettings.WindowOpacity = opacity;
                Configuration.Save();
            }

            var compact = Configuration.UISettings.UseCompactMode;
            if (ImGui.Checkbox("Compact Display Mode", ref compact))
            {
                Configuration.UISettings.UseCompactMode = compact;
                Configuration.Save();
            }
            ImGuiComponents.HelpMarker("Reduces spacing and uses smaller fonts");

            var sortByStatus = Configuration.UISettings.SortByStatus;
            if (ImGui.Checkbox("Sort by Completion Status", ref sortByStatus))
            {
                Configuration.UISettings.SortByStatus = sortByStatus;
                Configuration.Save();
            }
            
            ImGui.Unindent();
        }

        ImGui.Spacing();

        // Behavior Settings
        if (ImGui.CollapsingHeader("Behavior"))
        {
            ImGui.Indent();
            
            var hideInDuties = Configuration.CurrencySettings.HideInDuties;
            if (ImGui.Checkbox("Hide Overlays in Duties", ref hideInDuties))
            {
                Configuration.CurrencySettings.HideInDuties = hideInDuties;
                Configuration.TodoSettings.HideInDuties = hideInDuties;
                Configuration.Save();
            }

            var hideInQuests = Configuration.TodoSettings.HideDuringQuests;
            if (ImGui.Checkbox("Hide Overlays During Quests", ref hideInQuests))
            {
                Configuration.TodoSettings.HideDuringQuests = hideInQuests;
                Configuration.Save();
            }
            
            ImGui.Unindent();
        }
    }

    private void DrawModulesSettings()
    {
        ImGui.TextWrapped("Enable or disable tracking modules");
        ImGui.Separator();
        
        // Search bar
        ImGui.SetNextItemWidth(ImGui.GetContentRegionAvail().X);
        ImGui.InputTextWithHint("##ModuleSearch", "Search modules...", ref _moduleSearch, 50);
        
        ImGui.BeginChild("ModuleList", new Vector2(0, -25));
        
        // Currencies
        if (ImGui.CollapsingHeader("Currency Modules", ImGuiTreeNodeFlags.DefaultOpen))
        {
            DrawModuleGroup(ModuleType.Currency);
        }
        
        // Daily
        if (ImGui.CollapsingHeader("Daily Tasks"))
        {
            DrawModuleGroup(ModuleType.Daily);
        }
        
        // Weekly
        if (ImGui.CollapsingHeader("Weekly Tasks"))
        {
            DrawModuleGroup(ModuleType.Weekly);
        }
        
        // Special
        if (ImGui.CollapsingHeader("Special"))
        {
            DrawModuleGroup(ModuleType.Special);
        }
        
        ImGui.EndChild();
        
        // Quick actions
        if (ImGui.Button("Enable All"))
        {
            foreach (var module in Plugin.ModuleManager.GetModules())
                module.IsEnabled = true;
            Configuration.Save();
        }
        ImGui.SameLine();
        if (ImGui.Button("Disable All"))
        {
            foreach (var module in Plugin.ModuleManager.GetModules())
                module.IsEnabled = false;
            Configuration.Save();
        }
    }

    private void DrawModuleGroup(ModuleType type)
    {
        ImGui.Indent();
        
        var modules = Plugin.ModuleManager.GetModules()
            .Where(m => m.Type == type)
            .Where(m => string.IsNullOrEmpty(_moduleSearch) || 
                       m.Name.Contains(_moduleSearch, StringComparison.OrdinalIgnoreCase))
            .OrderBy(m => m.Name);

        foreach (var module in modules)
        {
            ImGui.PushID(module.Name);
            
            // Module enable checkbox
            var enabled = module.IsEnabled;
            if (ImGui.Checkbox("##Enable", ref enabled))
            {
                module.IsEnabled = enabled;
                Configuration.Save();
            }
            
            ImGui.SameLine();
            
            // Module name (clickable for settings)
            var hasSettings = HasModuleSettings(module);
            if (hasSettings)
            {
                if (ImGui.SmallButton(module.Name))
                {
                    ImGui.OpenPopup($"ModuleSettings_{module.Name}");
                }
            }
            else
            {
                ImGui.Text(module.Name);
            }
            
            // Status indicator
            ImGui.SameLine();
            var statusColor = module.Status switch
            {
                ModuleStatus.Complete => new Vector4(0.3f, 0.8f, 0.3f, 1),
                ModuleStatus.InProgress => new Vector4(1, 0.8f, 0.2f, 1),
                ModuleStatus.Incomplete => new Vector4(0.5f, 0.5f, 0.5f, 1),
                _ => new Vector4(0.5f, 0.5f, 0.5f, 1)
            };