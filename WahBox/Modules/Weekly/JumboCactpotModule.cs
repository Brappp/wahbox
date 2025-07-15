using System;
using WahBox.Core;
using WahBox.Core.Interfaces;
using ImGuiNET;

namespace WahBox.Modules.Weekly;

public class JumboCactpotModule : BaseModule
{
    public override string Name => "Jumbo Cactpot";
    public override ModuleType Type => ModuleType.Weekly;
    
    private const int MaxWeeklyTickets = 3;
    private int _ticketsPurchased = 0;
    private DateTime _nextReset;
    private DateTime _drawTime;
    private bool _resultsAvailable = false;

    public JumboCactpotModule(Plugin plugin) : base(plugin)
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

        // Check if results are available (Saturday after draw time)
        var now = DateTime.UtcNow;
        _resultsAvailable = now >= _drawTime && now < _nextReset;

        // Update status
        if (_ticketsPurchased >= MaxWeeklyTickets)
        {
            Status = ModuleStatus.Complete;
        }
        else if (_ticketsPurchased > 0)
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
        _ticketsPurchased = 0;
        _resultsAvailable = false;
    }

    private void UpdateResetTime()
    {
        var now = DateTime.UtcNow;
        
        // Jumbo Cactpot resets on Saturday at 19:00 UTC (before draw)
        var daysUntilSaturday = ((int)DayOfWeek.Saturday - (int)now.DayOfWeek + 7) % 7;
        if (daysUntilSaturday == 0 && now.Hour >= 19)
        {
            daysUntilSaturday = 7;
        }
        
        _nextReset = now.Date.AddDays(daysUntilSaturday).AddHours(19);
        
        // Drawing happens at 20:00 UTC on Saturday
        _drawTime = _nextReset.AddHours(1);
    }

    // This would be called from event handlers
    public void OnTicketPurchased()
    {
        _ticketsPurchased++;
        
        if (_ticketsPurchased >= MaxWeeklyTickets)
        {
            Plugin.Instance.NotificationManager.SendModuleComplete(Name, "All weekly tickets purchased!");
        }
    }

    public override void DrawConfig()
    {
        ImGui.TextUnformatted("Jumbo Cactpot Settings");
        ImGui.Separator();
        
        ImGui.TextWrapped("Track your weekly Jumbo Cactpot tickets at the Gold Saucer.");
        ImGui.TextWrapped("You can purchase up to 3 tickets per week.");
        ImGui.Spacing();

        var now = DateTime.UtcNow;
        
        if (_resultsAvailable)
        {
            ImGui.TextColored(new System.Numerics.Vector4(0, 1, 1, 1), "Drawing results are available!");
            ImGui.TextWrapped("Visit the Gold Saucer to check your numbers.");
        }
        else if (now < _drawTime)
        {
            var timeUntilDraw = _drawTime - now;
            ImGui.Text($"Next drawing: Saturday at 20:00 UTC");
            ImGui.Text($"Time until drawing: {timeUntilDraw.Days}d {timeUntilDraw.Hours:D2}h {timeUntilDraw.Minutes:D2}m");
        }
        
        ImGui.Separator();
        var timeUntilReset = _nextReset - DateTime.UtcNow;
        ImGui.TextUnformatted($"Ticket sales end: Saturday {timeUntilReset.Days}d {timeUntilReset.Hours:D2}h {timeUntilReset.Minutes:D2}m");
    }

    public override void DrawStatus()
    {
        var color = Status switch
        {
            ModuleStatus.Complete => new System.Numerics.Vector4(0, 1, 0, 1),
            ModuleStatus.InProgress => new System.Numerics.Vector4(1, 1, 0, 1),
            _ => new System.Numerics.Vector4(1, 1, 1, 1)
        };

        ImGui.TextColored(color, $"Jumbo Cactpot: {_ticketsPurchased}/{MaxWeeklyTickets} tickets");
        
        if (_resultsAvailable)
        {
            ImGui.TextColored(new System.Numerics.Vector4(0, 1, 1, 1), "  Check your results!");
        }
    }
} 