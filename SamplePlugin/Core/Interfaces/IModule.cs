using System;
using ImGuiNET;

namespace SamplePlugin.Core.Interfaces;

public enum ModuleType
{
    Currency,
    Daily,
    Weekly,
    Special
}

public enum ModuleStatus
{
    Unknown,
    Incomplete,
    Complete,
    Unavailable,
    InProgress
}

public interface IModule
{
    string Name { get; }
    ModuleType Type { get; }
    ModuleStatus Status { get; }
    bool IsEnabled { get; set; }
    uint IconId { get; }
    
    void Initialize();
    void Update();
    void Load();
    void Unload();
    void Reset();
    void Dispose();
    
    void DrawConfig();
    void DrawStatus();
}

public interface IModuleDrawable
{
    void DrawStatus();
    void DrawConfig();
    void DrawData();
    bool HasTooltip { get; }
    string TooltipText { get; }
}

public interface IModuleClickable
{
    bool HasClickableLink { get; }
    PayloadId ClickableLinkPayloadId { get; }
}

public interface IGoldSaucerReceiver
{
    void OnGoldSaucerUpdate(GoldSaucerEventArgs args);
}

public enum ModuleCategory
{
    Currency,
    Tasks
}

public enum PayloadId : uint
{
    OpenWondrousTailsBook = 1000,
    IdyllshireTeleport,
    DomanEnclaveTeleport,
    OpenDutyFinderRoulette,
    OpenDutyFinderRaid,
    OpenDutyFinderAllianceRaid,
    GoldSaucerTeleport,
    OpenPartyFinder,
    UldahTeleport,
    OpenChallengeLog,
    OpenContentsFinder,
}

public class GoldSaucerEventArgs : EventArgs
{
    public byte EventId { get; set; }
    public int[] Data { get; set; } = Array.Empty<int>();
} 