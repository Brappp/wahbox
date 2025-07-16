using System;
using System.Collections.Generic;
using System.Linq;
using WahBox.Core;
using WahBox.Core.Interfaces;
using ImGuiNET;
using Lumina.Excel.Sheets;

namespace WahBox.Modules.Daily;

public class DutyRouletteModule : BaseModule
{
    public override string Name => "Duty Roulette";
    public override ModuleType Type => ModuleType.Daily;

    public DutyRouletteModule(Plugin plugin) : base(plugin)
    {
        IconId = 60582; // Duty Roulette icon
    }

    private readonly Dictionary<uint, RouletteInfo> _roulettes = new();
    private DateTime _nextReset;

    public class RouletteInfo
    {
        public uint Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public bool IsCompleted { get; set; }
        public bool IsTracked { get; set; } = true;
        public int RewardTomestones { get; set; }
        public int RewardExp { get; set; }
    }

    public override void Initialize()
    {
        base.Initialize();
        
        // Initialize roulettes from game data
        var contentRoulettes = Plugin.DataManager.GetExcelSheet<ContentRoulette>();
        if (contentRoulettes == null) return;

        foreach (var roulette in contentRoulettes)
        {
            if (roulette.RowId == 0 || string.IsNullOrEmpty(roulette.Name.ExtractText())) continue;

            _roulettes[roulette.RowId] = new RouletteInfo
            {
                Id = roulette.RowId,
                Name = roulette.Name.ExtractText(),
                IsTracked = IsImportantRoulette(roulette.RowId)
            };
        }

        UpdateResetTime();
    }

    private bool IsImportantRoulette(uint id)
    {
        // Track important roulettes by default
        return id switch
        {
            1 => true,  // Leveling
            2 => true,  // Level 50/60/70/80 Dungeons
            3 => true,  // Main Scenario
            4 => true,  // Guildhests
            5 => true,  // Expert
            6 => true,  // Trials
            7 => true,  // Frontline
            8 => true,  // Mentor
            9 => true,  // Alliance Raids
            15 => true, // Normal Raids
            _ => false
        };
    }

    public override unsafe void Update()
    {
        if (!Plugin.ClientState.IsLoggedIn) return;

        // Check if we've passed reset time
        if (DateTime.UtcNow > _nextReset)
        {
            ResetRoulettes();
            UpdateResetTime();
        }

        // Update completion status using InstanceContent
        var instanceContent = FFXIVClientStructs.FFXIV.Client.Game.UI.InstanceContent.Instance();
        if (instanceContent == null) return;

        foreach (var roulette in _roulettes.Values)
        {
            roulette.IsCompleted = instanceContent->IsRouletteComplete((byte)roulette.Id);
        }

        // Update module status
        var trackedRoulettes = _roulettes.Values.Where(r => r.IsTracked).ToList();
        var completedCount = trackedRoulettes.Count(r => r.IsCompleted);
        
        if (completedCount == 0)
            Status = ModuleStatus.Incomplete;
        else if (completedCount == trackedRoulettes.Count)
            Status = ModuleStatus.Complete;
        else
            Status = ModuleStatus.InProgress;
    }

    private void ResetRoulettes()
    {
        foreach (var roulette in _roulettes.Values)
        {
            roulette.IsCompleted = false;
        }
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

    public override void DrawConfig()
    {
        ImGui.TextUnformatted("Select which roulettes to track:");
        ImGui.Separator();

        foreach (var roulette in _roulettes.Values.OrderBy(r => r.Id))
        {
            var isTracked = roulette.IsTracked;
            if (ImGui.Checkbox($"{roulette.Name}##roulette{roulette.Id}", ref isTracked))
            {
                roulette.IsTracked = isTracked;
            }
        }

        ImGui.Separator();
        var timeUntilReset = _nextReset - DateTime.UtcNow;
        ImGui.TextUnformatted($"Next reset in: {timeUntilReset.Hours:D2}:{timeUntilReset.Minutes:D2}:{timeUntilReset.Seconds:D2}");
    }

    public override void DrawStatus()
    {
        ImGui.TextUnformatted("Duty Roulettes:");
        
        foreach (var roulette in _roulettes.Values.Where(r => r.IsTracked).OrderBy(r => r.Id))
        {
            var color = roulette.IsCompleted 
                ? new System.Numerics.Vector4(0, 1, 0, 1) 
                : new System.Numerics.Vector4(1, 1, 1, 1);
                
            ImGui.TextColored(color, $"  {(roulette.IsCompleted ? "✓" : "○")} {roulette.Name}");
        }
    }
} 