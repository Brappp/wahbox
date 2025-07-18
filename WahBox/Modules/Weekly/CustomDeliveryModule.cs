using System;
using WahBox.Core;
using WahBox.Core.Interfaces;
using ImGuiNET;
using FFXIVClientStructs.FFXIV.Client.Game;

namespace WahBox.Modules.Weekly;

public class CustomDeliveryModule : BaseModule, IProgressModule, IDetailedStatus
{
    public override string Name => "Custom Deliveries";
    public override ModuleType Type => ModuleType.Weekly;
    
    private const int MaxWeeklyAllowances = 12;
    private int _remainingAllowances = MaxWeeklyAllowances;
    private DateTime _nextReset;
    
    // IProgressModule implementation
    public int Current => MaxWeeklyAllowances - _remainingAllowances; // Used allowances
    public int Maximum => MaxWeeklyAllowances;
    public float Progress => Maximum > 0 ? (float)Current / Maximum : 0f;
    
    // Comparison modes for flexible tracking
    public enum ComparisonMode
    {
        LessThan,
        EqualTo,
        LessThanOrEqual
    }
    
    private int _notificationThreshold = 12;
    private ComparisonMode _comparisonMode = ComparisonMode.LessThan;

    public CustomDeliveryModule(Plugin plugin) : base(plugin)
    {
        IconId = 60734; // Module icon
    }

    public override void Initialize()
    {
        base.Initialize();
        UpdateResetTime();
    }

    public override unsafe void Update()
    {
        if (!Plugin.ClientState.IsLoggedIn) return;

        // Check if we've passed reset time
        if (DateTime.UtcNow > _nextReset)
        {
            Reset();
            UpdateResetTime();
        }

        // Update remaining allowances
        var satisfactionSupply = SatisfactionSupplyManager.Instance();
        if (satisfactionSupply == null) return;

        var previousAllowances = _remainingAllowances;
        _remainingAllowances = satisfactionSupply->GetRemainingAllowances();

        // Update status based on comparison mode
        Status = _comparisonMode switch
        {
            ComparisonMode.LessThan when _notificationThreshold > _remainingAllowances => ModuleStatus.Complete,
            ComparisonMode.EqualTo when _notificationThreshold == _remainingAllowances => ModuleStatus.Complete,
            ComparisonMode.LessThanOrEqual when _notificationThreshold >= _remainingAllowances => ModuleStatus.Complete,
            _ => ModuleStatus.Incomplete
        };
        
        // Send notification if we've used allowances
        if (previousAllowances > _remainingAllowances && _remainingAllowances == 0)
        {
            Plugin.Instance.NotificationManager.SendModuleComplete(Name, "All custom deliveries completed!");
        }
    }

    public override void Reset()
    {
        base.Reset();
        _remainingAllowances = MaxWeeklyAllowances;
    }

    private void UpdateResetTime()
    {
        var now = DateTime.UtcNow;
        
        // Custom Deliveries reset on Tuesday at 8:00 UTC
        var daysUntilTuesday = ((int)DayOfWeek.Tuesday - (int)now.DayOfWeek + 7) % 7;
        if (daysUntilTuesday == 0 && now.Hour >= 8)
        {
            daysUntilTuesday = 7;
        }
        
        _nextReset = now.Date.AddDays(daysUntilTuesday).AddHours(8);
    }

    public override void DrawConfig()
    {
        ImGui.TextUnformatted("Custom Delivery Settings");
        ImGui.Separator();
        
        ImGui.SetNextItemWidth(ImGui.GetContentRegionAvail().X / 2.0f);
        if (ImGui.BeginCombo("Completion Mode", _comparisonMode.ToString()))
        {
            foreach (ComparisonMode mode in Enum.GetValues<ComparisonMode>())
            {
                if (ImGui.Selectable(mode.ToString(), _comparisonMode == mode))
                {
                    _comparisonMode = mode;
                }
            }
            ImGui.EndCombo();
        }
        
        ImGui.SetNextItemWidth(ImGui.GetContentRegionAvail().X / 2.0f);
        ImGui.SliderInt("Notification Threshold", ref _notificationThreshold, 1, 12);
        
        ImGui.Spacing();
        ImGui.TextWrapped("Set when to mark as complete based on remaining allowances.");
        
        ImGui.Separator();
        var timeUntilReset = _nextReset - DateTime.UtcNow;
        ImGui.TextUnformatted($"Next reset: Tuesday {timeUntilReset.Days}d {timeUntilReset.Hours:D2}h {timeUntilReset.Minutes:D2}m");
    }

    public override void DrawStatus()
    {
        // Status is now drawn by the main window using progress bars
        // This method is kept for compatibility but no longer used in the tracking tab
        var color = Status switch
        {
            ModuleStatus.Complete => new System.Numerics.Vector4(0, 1, 0, 1),
            ModuleStatus.InProgress => new System.Numerics.Vector4(1, 1, 0, 1),
            _ => new System.Numerics.Vector4(1, 1, 1, 1)
        };

        ImGui.TextColored(color, $"{_remainingAllowances}/{MaxWeeklyAllowances} allowances");
    }
    
    public string GetDetailedStatus()
    {
        return $"{_remainingAllowances} allowances left";
    }
} 