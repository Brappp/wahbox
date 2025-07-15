using System;
using SamplePlugin.Core;
using SamplePlugin.Core.Interfaces;
using ImGuiNET;
using FFXIVClientStructs.FFXIV.Client.Game;

namespace SamplePlugin.Modules.Weekly;

public class DomanEnclaveModule : BaseModule
{
    public override string Name => "Doman Enclave";
    public override ModuleType Type => ModuleType.Weekly;
    
    private const uint MaxWeeklyDonation = 40000;
    private uint _weeklyDonated = 0;
    private uint _weeklyBudget = 0;
    private DateTime _nextReset;
    private bool _isMaxRank = false;

    public DomanEnclaveModule(Plugin plugin) : base(plugin)
    {
        IconId = 60735; // Module icon
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

        // Get Doman Enclave data from game
        var reconstructionBoxData = DomanEnclaveManager.Instance();
        if (reconstructionBoxData == null) return;

        // Update values from game data
        if (reconstructionBoxData->State.Allowance != 0)
        {
            _weeklyBudget = reconstructionBoxData->State.Allowance;
            _weeklyDonated = reconstructionBoxData->State.Donated;
            // TODO: Check rank when property is available
        }

        // Update status
        if (_weeklyDonated >= _weeklyBudget)
        {
            Status = ModuleStatus.Complete;
        }
        else if (_weeklyDonated > 0)
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
        _weeklyDonated = 0;
    }

    private void UpdateResetTime()
    {
        var now = DateTime.UtcNow;
        
        // Doman Enclave resets on Tuesday at 8:00 UTC
        var daysUntilTuesday = ((int)DayOfWeek.Tuesday - (int)now.DayOfWeek + 7) % 7;
        if (daysUntilTuesday == 0 && now.Hour >= 8)
        {
            daysUntilTuesday = 7;
        }
        
        _nextReset = now.Date.AddDays(daysUntilTuesday).AddHours(8);
    }

    public override void DrawConfig()
    {
        ImGui.TextUnformatted("Doman Enclave Settings");
        ImGui.Separator();
        
        ImGui.TextWrapped("Track your weekly donations to the Doman Enclave reconstruction effort.");
        ImGui.Spacing();
        
        if (_isMaxRank)
        {
            ImGui.TextColored(new System.Numerics.Vector4(0, 1, 0, 1), "Enclave is fully reconstructed!");
        }
        else
        {
            ImGui.TextColored(new System.Numerics.Vector4(1, 1, 0, 1), "Enclave reconstruction in progress");
        }
        
        ImGui.Spacing();
        ImGui.Text($"Weekly budget: {_weeklyBudget:N0} gil");
        ImGui.Text($"Current week donations: {_weeklyDonated:N0} gil");
        
        if (_weeklyBudget > 0)
        {
            var progress = (float)_weeklyDonated / _weeklyBudget;
            ImGui.ProgressBar(progress, new System.Numerics.Vector2(-1, 0), $"{progress * 100:F1}%");
        }
        
        ImGui.Separator();
        var timeUntilReset = _nextReset - DateTime.UtcNow;
        ImGui.TextUnformatted($"Next reset: Tuesday {timeUntilReset.Days}d {timeUntilReset.Hours:D2}h {timeUntilReset.Minutes:D2}m");
    }

    public override void DrawStatus()
    {
        var color = Status switch
        {
            ModuleStatus.Complete => new System.Numerics.Vector4(0, 1, 0, 1),
            ModuleStatus.InProgress => new System.Numerics.Vector4(1, 1, 0, 1),
            _ => new System.Numerics.Vector4(1, 1, 1, 1)
        };

        if (_weeklyBudget > 0)
        {
            ImGui.TextColored(color, $"Doman Enclave: {_weeklyDonated:N0}/{_weeklyBudget:N0} gil donated");
        }
        else
        {
            ImGui.TextColored(new System.Numerics.Vector4(0.5f, 0.5f, 0.5f, 1), "Doman Enclave: Not Available");
        }
    }
} 