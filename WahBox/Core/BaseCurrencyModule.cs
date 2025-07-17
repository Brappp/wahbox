using System;
using System.Collections.Generic;
using System.Linq;
using Dalamud.Interface.Components;
using ImGuiNET;
using WahBox.Core.Interfaces;
using WahBox.Models;
using WahBox.Systems;

namespace WahBox.Core;

public abstract class BaseCurrencyModule : BaseModule, ICurrencyModule
{
    protected readonly List<uint> _currencyIds = new();
    public int AlertThreshold { get; set; } = 90;
    
    protected BaseCurrencyModule(Plugin plugin) : base(plugin)
    {
        // Category is determined by base class
    }
    
    public override ModuleType Type => ModuleType.Currency;
    
    public virtual List<TrackedCurrency> GetTrackedCurrencies()
    {
        return _currencyIds.Select(id => new TrackedCurrency
        {
            Type = CurrencyType.Item,
            ItemId = id,
            Threshold = (int)(CurrencyHelper.GetCurrencyMax(id) * AlertThreshold / 100.0),
            MaxCount = CurrencyHelper.GetCurrencyMax(id)
        }).ToList();
    }
    
    public virtual int GetCurrentAmount()
    {
        return _currencyIds.Sum(id => CurrencyHelper.GetCurrencyCount(id));
    }
    
    public virtual int GetMaxAmount()
    {
        return _currencyIds.Sum(id => CurrencyHelper.GetCurrencyMax(id));
    }
    
    protected abstract string GetCurrencyName(uint itemId);
    
    public override void Update()
    {
        // Check for alerts
        var current = GetCurrentAmount();
        var max = GetMaxAmount();
        
        if (max > 0)
        {
            var percent = (float)current / max * 100f;
            Status = percent >= AlertThreshold ? ModuleStatus.InProgress : ModuleStatus.Incomplete;
            
            // Send notification if we hit the threshold
            if (IsEnabled && percent >= AlertThreshold)
            {
                CheckAndSendAlert(current, max, percent);
            }
        }
        else
        {
            Status = ModuleStatus.Unknown;
        }
    }
    
    private DateTime _lastAlertTime = DateTime.MinValue;
    
    private void CheckAndSendAlert(int current, int max, float percent)
    {
        var config = Plugin.Configuration.NotificationSettings;
        if (!config.CurrencyWarningAlerts) return;
        
        var now = DateTime.Now;
        if ((now - _lastAlertTime).TotalMinutes < config.NotificationCooldown)
            return;
        
        _lastAlertTime = now;
        Plugin.NotificationManager.SendNotification(
            $"{Name} is at {percent:F0}% capacity ({current:N0}/{max:N0})",
            WahBoxNotificationType.Warning);
    }
    
    public override void DrawStatus()
    {
        var current = GetCurrentAmount();
        var max = GetMaxAmount();
        
        if (max > 0)
        {
            var percent = (float)current / max * 100f;
            ImGui.Text($"{current:N0}/{max:N0} ({percent:F0}%)");
        }
        else
        {
            ImGui.Text("Unknown");
        }
    }
    
    public override void DrawConfig()
    {
        ImGui.Text("Alert Settings");
        ImGui.Separator();
        
        ImGui.SetNextItemWidth(200);
        int alertThreshold = AlertThreshold;
        if (ImGui.SliderInt("Alert Threshold %", ref alertThreshold, 0, 100))
        {
            AlertThreshold = alertThreshold;
            SaveConfiguration();
        }
        ImGui.SameLine();
        ImGuiComponents.HelpMarker("Alert when currency reaches this percentage of maximum capacity");
    }
    
    protected override Dictionary<string, object>? GetConfigurationData()
    {
        return new Dictionary<string, object>
        {
            ["AlertThreshold"] = AlertThreshold
        };
    }
    
    protected override void SetConfigurationData(object config)
    {
        if (config is Dictionary<string, object> dict)
        {
            try
            {
                if (dict.TryGetValue("AlertThreshold", out var threshold))
                {
                    AlertThreshold = Convert.ToInt32(threshold);
                }
            }
            catch
            {
                AlertThreshold = 90;
            }
        }
    }
}
