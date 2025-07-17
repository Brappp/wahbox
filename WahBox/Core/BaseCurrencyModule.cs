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
    // Alert threshold for this currency (0 = disabled)
    public int AlertThreshold 
    { 
        get => GetThresholdFromConfig();
        set => SaveThresholdToConfig(value);
    }
    
    private int _defaultThreshold = 90;
    
    protected BaseCurrencyModule(Plugin plugin) : base(plugin)
    {
        // Category is determined by base class
    }
    
    // Override IconId to get it from the first currency item
    public override uint IconId
    {
        get
        {
            if (_currencyIds.Count == 0) return 0;
            
            try
            {
                var item = Plugin.DataManager.GetExcelSheet<Lumina.Excel.Sheets.Item>()?.GetRow(_currencyIds[0]);
                return item?.Icon ?? 0;
            }
            catch
            {
                return 0;
            }
        }
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
        var config = new Dictionary<string, object>
        {
            ["AlertThreshold"] = AlertThreshold
        };
        
        Plugin.Log.Debug($"Currency module {Name} saving configuration: AlertThreshold = {AlertThreshold}");
        return config;
    }
    
    protected override void SetConfigurationData(object config)
    {
        if (config is Dictionary<string, object> dict)
        {
            try
            {
                if (dict.TryGetValue("AlertThreshold", out var threshold))
                {
                    var newThreshold = Convert.ToInt32(threshold);
                    Plugin.Log.Debug($"Currency module {Name} loading configuration: AlertThreshold = {newThreshold}");
                    _defaultThreshold = newThreshold;
                }
                else
                {
                    Plugin.Log.Debug($"Currency module {Name} no AlertThreshold found in config, using default: {_defaultThreshold}");
                }
            }
            catch (Exception ex)
            {
                Plugin.Log.Warning(ex, $"Currency module {Name} failed to load AlertThreshold, using default: 90");
                _defaultThreshold = 90;
            }
        }
        else
        {
            Plugin.Log.Debug($"Currency module {Name} invalid config format, using default AlertThreshold: {_defaultThreshold}");
        }
    }
    
    private int GetThresholdFromConfig()
    {
        // Get from direct configuration storage
        if (Plugin.Configuration.UISettings.CurrencyAlertThresholds.TryGetValue(Name, out var threshold))
        {
            return threshold;
        }
        
        return _defaultThreshold;
    }
    
    private void SaveThresholdToConfig(int value)
    {
        // Store directly in UISettings for immediate persistence
        Plugin.Configuration.UISettings.CurrencyAlertThresholds[Name] = value;
        Plugin.Configuration.Save();
        
        Plugin.Log.Information($"Currency module {Name} saved AlertThreshold directly to config: {value}");
    }
}
