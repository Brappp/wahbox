using System;
using System.Collections.Generic;
using System.Linq;
using WahBox.Core;
using WahBox.Core.Interfaces;
using ImGuiNET;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Game.UI;

namespace WahBox.Modules.Daily;

public class BeastTribeModule : BaseModule
{
    public override string Name => "Beast Tribe Quests";
    public override ModuleType Type => ModuleType.Daily;

    private const int MaxDailyQuests = 12;
    private int _questsCompleted = 0;
    private int _questsAvailable = MaxDailyQuests;
    private DateTime _nextReset;
    
    // Beast tribe reputation IDs
    private readonly Dictionary<uint, string> _beastTribes = new()
    {
        // ARR Beast Tribes
        { 1, "Amalj'aa" },
        { 2, "Sylph" },
        { 3, "Kobold" },
        { 4, "Sahagin" },
        { 5, "Ixal" },
        
        // HW Beast Tribes
        { 6, "Vanu Vanu" },
        { 7, "Vath" },
        { 8, "Moogle" },
        
        // SB Beast Tribes
        { 9, "Kojin" },
        { 10, "Ananta" },
        { 11, "Namazu" },
        
        // ShB Beast Tribes
        { 12, "Pixie" },
        { 13, "Qitari" },
        { 14, "Dwarf" },
        
        // EW Beast Tribes
        { 15, "Arkasodara" },
        { 16, "Omicron" },
        { 17, "Loporrits" }
    };

    private readonly Dictionary<uint, BeastTribeInfo> _tribeProgress = new();

    public class BeastTribeInfo
    {
        public uint Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public byte Rank { get; set; }
        public byte Reputation { get; set; }
        public bool IsMaxRank { get; set; }
        public bool IsTracked { get; set; } = true;
    }

    public BeastTribeModule(Plugin plugin) : base(plugin)
    {
        IconId = 60547; // Module icon - Beast Tribe quest icon
    }

    public override void Initialize()
    {
        base.Initialize();
        
        foreach (var tribe in _beastTribes)
        {
            _tribeProgress[tribe.Key] = new BeastTribeInfo
            {
                Id = tribe.Key,
                Name = tribe.Value,
                IsTracked = true
            };
        }
        
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

        // Update quest counts using the correct API
        var questManager = QuestManager.Instance();
        if (questManager == null) return;

        // Get remaining beast tribe allowances
        var remainingAllowances = questManager->GetBeastTribeAllowance();
        _questsAvailable = (int)remainingAllowances;
        _questsCompleted = MaxDailyQuests - (int)remainingAllowances;

        // Update tribe progress - For now, just show quest allowances
        // Individual tribe reputation tracking would need proper API research
        // This shows the overall beast tribe quest allowance system

        // Update status
        if (_questsCompleted >= MaxDailyQuests)
            Status = ModuleStatus.Complete;
        else if (_questsCompleted > 0)
            Status = ModuleStatus.InProgress;
        else
            Status = ModuleStatus.Incomplete;
            
        // Send notification when all quests completed
        if (Status == ModuleStatus.Complete && _questsCompleted == MaxDailyQuests)
        {
            Plugin.Instance.NotificationManager.SendModuleComplete(Name, "All daily beast tribe quests completed!");
        }
    }

    private byte GetMaxRank(uint tribeId)
    {
        // Different tribes have different max ranks
        return tribeId switch
        {
            <= 4 => 4,   // ARR tribes (Neutral to Trusted)
            5 => 7,       // Ixal (Neutral to Sworn)
            <= 14 => 8,   // HW/SB/ShB tribes (Neutral to Bloodsworn)
            _ => 5        // EW tribes (Neutral to ?)
        };
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

    public override void Reset()
    {
        base.Reset();
        _questsCompleted = 0;
        _questsAvailable = MaxDailyQuests;
    }

    public override void DrawConfig()
    {
        ImGui.TextUnformatted("Select beast tribes to track:");
        ImGui.Separator();

        // Group by expansion
        DrawTribeGroup("A Realm Reborn", 1, 5);
        DrawTribeGroup("Heavensward", 6, 8);
        DrawTribeGroup("Stormblood", 9, 11);
        DrawTribeGroup("Shadowbringers", 12, 14);
        DrawTribeGroup("Endwalker", 15, 17);

        ImGui.Separator();
        var timeUntilReset = _nextReset - DateTime.UtcNow;
        ImGui.TextUnformatted($"Next reset in: {timeUntilReset.Hours:D2}:{timeUntilReset.Minutes:D2}:{timeUntilReset.Seconds:D2}");
    }

    private void DrawTribeGroup(string expansion, uint startId, uint endId)
    {
        if (ImGui.TreeNode(expansion))
        {
            for (uint i = startId; i <= endId; i++)
            {
                if (_tribeProgress.TryGetValue(i, out var tribe))
                {
                    var isTracked = tribe.IsTracked;
                    if (ImGui.Checkbox($"{tribe.Name} (Rank {tribe.Rank})##tribe{i}", ref isTracked))
                    {
                        tribe.IsTracked = isTracked;
                    }
                    
                    if (tribe.IsMaxRank)
                    {
                        ImGui.SameLine();
                        ImGui.TextColored(new System.Numerics.Vector4(0, 1, 0, 1), "(Max Rank)");
                    }
                }
            }
            ImGui.TreePop();
        }
    }

    public override void DrawStatus()
    {
        var color = Status switch
        {
            ModuleStatus.Complete => new System.Numerics.Vector4(0, 1, 0, 1),
            ModuleStatus.InProgress => new System.Numerics.Vector4(1, 1, 0, 1),
            _ => new System.Numerics.Vector4(1, 1, 1, 1)
        };

        ImGui.TextColored(color, $"Beast Tribe Quests: {_questsCompleted}/{MaxDailyQuests}");
        
        if (_questsAvailable > 0)
        {
            ImGui.TextUnformatted($"  {_questsAvailable} allowances remaining");
        }
        else
        {
            ImGui.TextUnformatted($"  All allowances used");
        }
    }
} 