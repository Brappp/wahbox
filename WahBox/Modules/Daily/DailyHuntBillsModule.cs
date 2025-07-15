using System;
using WahBox.Core;
using WahBox.Core.Interfaces;
using ImGuiNET;

namespace WahBox.Modules.Daily;

public class DailyHuntBillsModule : BaseModule
{
    public override string Name => "Daily Hunt Bills";
    public override ModuleType Type => ModuleType.Daily;
    
    private DateTime _nextReset;
    private int _current = 0;
    private int _maximum = 3;

    public DailyHuntBillsModule(Plugin plugin) : base(plugin)
    {
        IconId = 60729; // Module icon
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

        // Update status
        if (_current >= _maximum)
        {
            Status = ModuleStatus.Complete;
        }
        else if (_current > 0)
        {
            Status = ModuleStatus.InProgress;
        }
        else
        {
            Status = ModuleStatus.Incomplete;
        }
    }

    public override void Reset()
    {
        base.Reset();
        _current = 0;
    }

    private void UpdateResetTime()
    {
        var now = DateTime.UtcNow;
        var resetHour = 15; // 15 UTC
        
        _nextReset = new DateTime(now.Year, now.Month, now.Day, resetHour, 0, 0, DateTimeKind.Utc);
        
        if (_nextReset <= now)
        {
            _nextReset = _nextReset.AddDays(1);
        }
    }

    public void IncrementProgress(int amount = 1)
    {
        _current = Math.Min(_current + amount, _maximum);
        
        if (_current >= _maximum)
        {
            Plugin.Instance.NotificationManager.SendModuleComplete(Name, "Daily Hunt Bills completed!");
        }
    }

    public override void DrawConfig()
    {
        ImGui.TextUnformatted("Daily Hunt Bills Settings");
        ImGui.Separator();
        ImGui.TextWrapped("Track completion of daily hunt marks.");
        
        ImGui.Spacing();
        ImGui.Text($"Progress: {_current}/{_maximum}");
        
        if (_maximum > 0)
        {
            var progress = (float)_current / _maximum;
            ImGui.ProgressBar(progress, new System.Numerics.Vector2(-1, 0), $"{progress * 100:F1}%");
        }
        
        ImGui.Separator();
        var timeUntilReset = _nextReset - DateTime.UtcNow;
        ImGui.TextUnformatted($"Next reset in: {timeUntilReset.Hours:D2}:{timeUntilReset.Minutes:D2}:{timeUntilReset.Seconds:D2}");
    }

    public override void DrawStatus()
    {
        var color = Status switch
        {
            ModuleStatus.Complete => new System.Numerics.Vector4(0, 1, 0, 1),
            ModuleStatus.InProgress => new System.Numerics.Vector4(1, 1, 0, 1),
            _ => new System.Numerics.Vector4(1, 1, 1, 1)
        };

        ImGui.TextColored(color, $"{Name}: {_current}/{_maximum}");
    }
}