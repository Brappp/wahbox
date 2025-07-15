using System;
using SamplePlugin.Core;
using SamplePlugin.Core.Interfaces;
using ImGuiNET;

namespace SamplePlugin.Modules.Weekly;

public class FashionReportModule : BaseModule
{
    public override string Name => "Fashion Report";
    public override ModuleType Type => ModuleType.Weekly;
    
    public enum FashionReportMode
    {
        All,        // Complete all 4 attempts
        Single,     // Complete at least 1 attempt
        Plus80      // Score 80+ points
    }
    
    private int _allowancesRemaining = 4;
    private int _highestWeeklyScore = 0;
    private bool _fashionReportAvailable = false;
    private FashionReportMode _completionMode = FashionReportMode.Single;
    private DateTime _nextReset;
    private DateTime _reportOpenTime;
    private DateTime _reportCloseTime;

    public FashionReportModule(Plugin plugin) : base(plugin)
    {
        IconId = 60810; // Module icon
    }

    public override void Initialize()
    {
        base.Initialize();
        UpdateResetTime();
    }

    public override void Update()
    {
        if (!Plugin.ClientState.IsLoggedIn) return;

        // Check if we've passed reset time
        if (DateTime.UtcNow > _nextReset)
        {
            Reset();
            UpdateResetTime();
        }

        // Fashion Report is available from Friday to Tuesday reset
        var now = DateTime.UtcNow;
        _fashionReportAvailable = now >= _reportOpenTime && now < _reportCloseTime;

        // Update status based on availability and completion mode
        if (!_fashionReportAvailable)
        {
            Status = ModuleStatus.Unknown;
        }
        else
        {
            Status = _completionMode switch
            {
                FashionReportMode.Single when _allowancesRemaining < 4 => ModuleStatus.Complete,
                FashionReportMode.All when _allowancesRemaining == 0 => ModuleStatus.Complete,
                FashionReportMode.Plus80 when _highestWeeklyScore >= 80 => ModuleStatus.Complete,
                _ => ModuleStatus.Incomplete
            };
        }
    }

    public override void Reset()
    {
        base.Reset();
        _highestWeeklyScore = 0;
        _allowancesRemaining = 4;
        _fashionReportAvailable = false;
    }

    private void UpdateResetTime()
    {
        var now = DateTime.UtcNow;
        
        // Fashion Report resets on Tuesday at 8:00 UTC
        var daysUntilTuesday = ((int)DayOfWeek.Tuesday - (int)now.DayOfWeek + 7) % 7;
        if (daysUntilTuesday == 0 && now.Hour >= 8)
        {
            daysUntilTuesday = 7;
        }
        
        _nextReset = now.Date.AddDays(daysUntilTuesday).AddHours(8);
        
        // Fashion Report opens on Friday (4 days before Tuesday)
        _reportOpenTime = _nextReset.AddDays(-4);
        _reportCloseTime = _nextReset;
    }

    // This would need to be called from an event handler or manual update
    public void UpdateFashionReportData(int score, int allowancesUsed)
    {
        _allowancesRemaining = 4 - allowancesUsed;
        if (score > _highestWeeklyScore)
        {
            _highestWeeklyScore = score;
            
            if (score >= 80)
            {
                Plugin.Instance.NotificationManager.SendModuleComplete(Name, $"Scored {score} points!");
            }
        }
    }

    public override void DrawConfig()
    {
        ImGui.TextUnformatted("Fashion Report Settings");
        ImGui.Separator();
        
        ImGui.SetNextItemWidth(ImGui.GetContentRegionAvail().X / 2.0f);
        if (ImGui.BeginCombo("Completion Mode", _completionMode.ToString()))
        {
            foreach (FashionReportMode mode in Enum.GetValues<FashionReportMode>())
            {
                if (ImGui.Selectable(mode.ToString(), _completionMode == mode))
                {
                    _completionMode = mode;
                }
            }
            ImGui.EndCombo();
        }
        
        ImGui.Spacing();
        ImGui.TextWrapped("Completion modes:");
        ImGui.BulletText("All: Complete all 4 attempts");
        ImGui.BulletText("Single: Complete at least 1 attempt");
        ImGui.BulletText("Plus80: Score 80 or more points");
        
        ImGui.Separator();
        var timeUntilReset = _nextReset - DateTime.UtcNow;
        ImGui.TextUnformatted($"Next reset: Tuesday {timeUntilReset.Days}d {timeUntilReset.Hours:D2}h {timeUntilReset.Minutes:D2}m");
        
        if (_fashionReportAvailable)
        {
            var timeRemaining = _reportCloseTime - DateTime.UtcNow;
            ImGui.TextColored(new System.Numerics.Vector4(0, 1, 0, 1), 
                $"Fashion Report is OPEN! Closes in {timeRemaining.Days}d {timeRemaining.Hours}h");
        }
        else
        {
            var timeUntilOpen = _reportOpenTime - DateTime.UtcNow;
            if (timeUntilOpen.TotalSeconds > 0)
            {
                ImGui.TextColored(new System.Numerics.Vector4(1, 0.5f, 0, 1), 
                    $"Opens Friday in {timeUntilOpen.Days}d {timeUntilOpen.Hours}h");
            }
        }
    }

    public override void DrawStatus()
    {
        if (!_fashionReportAvailable)
        {
            ImGui.TextColored(new System.Numerics.Vector4(0.5f, 0.5f, 0.5f, 1), "Fashion Report: Not Available");
            return;
        }

        var color = Status switch
        {
            ModuleStatus.Complete => new System.Numerics.Vector4(0, 1, 0, 1),
            ModuleStatus.InProgress => new System.Numerics.Vector4(1, 1, 0, 1),
            _ => new System.Numerics.Vector4(1, 1, 1, 1)
        };

        var statusText = _completionMode switch
        {
            FashionReportMode.All => $"{_allowancesRemaining} allowances available",
            FashionReportMode.Single when _allowancesRemaining == 4 => $"{_allowancesRemaining} allowances available",
            FashionReportMode.Plus80 => $"Highest score: {_highestWeeklyScore}",
            _ => "Ready"
        };

        ImGui.TextColored(color, $"Fashion Report: {statusText}");
    }
} 