using System;
using WahBox.Core.Interfaces;
using ImGuiNET;
using System.Numerics;
using System.Linq;

namespace WahBox.Windows;

public static class ModuleStatusRenderer
{
    public static void DrawCompactStatus(IModule module)
    {
        switch (module.Type)
        {
            case ModuleType.Currency:
                DrawCurrencyStatus(module);
                break;
                
            case ModuleType.Daily:
            case ModuleType.Weekly:
                DrawTaskStatus(module);
                break;
                
            case ModuleType.Special:
                DrawSpecialStatus(module);
                break;
        }
    }
    
    private static void DrawCurrencyStatus(IModule module)
    {
        if (module is not ICurrencyModule currencyModule) return;
        
        var currencies = currencyModule.GetTrackedCurrencies();
        var primary = currencies.FirstOrDefault(c => c.Enabled);
        
        if (primary == null) return;
        
        var percent = primary.MaxCount > 0 ? (float)primary.CurrentCount / primary.MaxCount : 0;
        var color = GetPercentageColor(percent);
        
        // Simple text display with color
        ImGui.TextColored(color, $"{primary.CurrentCount:N0}/{primary.MaxCount:N0}");
    }
    
    private static void DrawTaskStatus(IModule module)
    {
        // Use short status text or icons
        var (text, color) = module.Status switch
        {
            ModuleStatus.Complete => ("Complete", new Vector4(0.2f, 0.8f, 0.2f, 1)),
            ModuleStatus.InProgress => ("Partial", new Vector4(0.8f, 0.8f, 0.2f, 1)),
            ModuleStatus.Incomplete => ("Not Started", new Vector4(0.8f, 0.2f, 0.2f, 1)),
            _ => ("Unknown", new Vector4(0.7f, 0.7f, 0.7f, 1))
        };
        
        ImGui.TextColored(color, text);
    }
    
    private static void DrawSpecialStatus(IModule module)
    {
        var color = module.Status switch
        {
            ModuleStatus.Complete => new Vector4(0.2f, 0.8f, 0.2f, 1),
            ModuleStatus.InProgress => new Vector4(0.8f, 0.8f, 0.2f, 1),
            ModuleStatus.Incomplete => new Vector4(0.8f, 0.2f, 0.2f, 1),
            _ => new Vector4(0.7f, 0.7f, 0.7f, 1)
        };
        
        ImGui.TextColored(color, module.Status.ToString());
    }
    
    private static Vector4 GetPercentageColor(float percent)
    {
        if (percent < 0.5f)
            return new Vector4(0.2f, 0.8f, 0.2f, 1); // Green
        else if (percent < 0.75f)
            return new Vector4(0.8f, 0.8f, 0.2f, 1); // Yellow
        else if (percent < 0.9f)
            return new Vector4(0.8f, 0.5f, 0.2f, 1); // Orange
        else
            return new Vector4(0.8f, 0.2f, 0.2f, 1); // Red
    }
}
