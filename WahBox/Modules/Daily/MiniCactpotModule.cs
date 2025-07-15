using System;
using WahBox.Core;
using WahBox.Core.Interfaces;
using ImGuiNET;

namespace WahBox.Modules.Daily;

public class MiniCactpotModule : BaseModule
{
    public override string Name => "Mini Cactpot";
    public override ModuleType Type => ModuleType.Daily;
    
    private const int MaxDailyTickets = 3;
    private int _ticketsAvailable = MaxDailyTickets;
    private int _ticketsScratchedToday = 0;
    private DateTime _nextReset;
    private bool _goldSaucerAvailable = false;

    public MiniCactpotModule(Plugin plugin) : base(plugin)
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

        // Check if we're in or near Gold Saucer
        var territoryId = Plugin.ClientState.TerritoryType;
        _goldSaucerAvailable = territoryId == 144; // Gold Saucer territory ID

        // Update tickets available (would need actual game data access)
        _ticketsAvailable = MaxDailyTickets - _ticketsScratchedToday;

        // Update status
        if (_ticketsScratchedToday >= MaxDailyTickets)
        {
            Status = ModuleStatus.Complete;
        }
        else if (_ticketsScratchedToday > 0)
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
        _ticketsScratchedToday = 0;
        _ticketsAvailable = MaxDailyTickets;
    }

    private void UpdateResetTime()
    {
        var now = DateTime.UtcNow;
        var resetHour = 15; // 3 PM UTC (daily reset)
        
        _nextReset = new DateTime(now.Year, now.Month, now.Day, resetHour, 0, 0, DateTimeKind.Utc);
        
        if (_nextReset <= now)
        {
            _nextReset = _nextReset.AddDays(1);
        }
    }

    // This would be called from event handlers
    public void OnTicketScratched()
    {
        _ticketsScratchedToday++;
        _ticketsAvailable = MaxDailyTickets - _ticketsScratchedToday;
        
        if (_ticketsScratchedToday >= MaxDailyTickets)
        {
            Plugin.Instance.NotificationManager.SendModuleComplete(Name, "All daily tickets scratched!");
        }
    }

    public override void DrawConfig()
    {
        ImGui.TextUnformatted("Mini Cactpot Settings");
        ImGui.Separator();
        
        ImGui.TextWrapped("Track your daily Mini Cactpot tickets at the Gold Saucer.");
        ImGui.TextWrapped("You can scratch up to 3 tickets per day.");
        
        ImGui.Spacing();
        
        if (_goldSaucerAvailable)
        {
            ImGui.TextColored(new System.Numerics.Vector4(0, 1, 0, 1), "You are at the Gold Saucer!");
        }
        else
        {
            ImGui.TextColored(new System.Numerics.Vector4(0.7f, 0.7f, 0.7f, 1), "Visit the Gold Saucer to play Mini Cactpot");
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

        ImGui.TextColored(color, $"Mini Cactpot: {_ticketsAvailable}/{MaxDailyTickets} tickets available");
        
        if (_goldSaucerAvailable && _ticketsAvailable > 0)
        {
            ImGui.TextColored(new System.Numerics.Vector4(0, 1, 1, 1), "  Ready to play!");
        }
    }
} 