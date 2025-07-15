using System;
using System.Linq;
using WahBox.Core;
using WahBox.Core.Interfaces;
using ImGuiNET;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using FFXIVClientStructs.FFXIV.Client.UI;

namespace WahBox.Modules.Weekly;

public class WondrousTailsModule : BaseModule
{
    public override string Name => "Wondrous Tails";
    public override ModuleType Type => ModuleType.Weekly;

    private const uint WondrousTailsItemId = 2002023; // Wondrous Tails journal
    private const uint KhloeNpcId = 1017653;
    
    private int _stickerCount = 0;
    private int _completedLines = 0;
    private uint _secondChancePoints = 0;
    private bool _hasBook = false;
    private DateTime _deadline;
    private DateTime _nextReset;
    private bool _hasSentNotification = false;

    public WondrousTailsModule(Plugin plugin) : base(plugin)
    {
        IconId = 60732; // Module icon
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

        // Check if player has Wondrous Tails book
        var playerState = PlayerState.Instance();
        if (playerState == null)
        {
            Status = ModuleStatus.Unknown;
            return;
        }

        _hasBook = playerState->HasWeeklyBingoJournal;
        
        if (_hasBook)
        {
            _stickerCount = playerState->WeeklyBingoNumPlacedStickers;
            _secondChancePoints = playerState->WeeklyBingoNumSecondChancePoints;
            
            // Calculate deadline
            var expiryTimestamp = playerState->GetWeeklyBingoExpireUnixTimestamp();
            _deadline = DateTimeOffset.FromUnixTimeSeconds(expiryTimestamp).DateTime;
            
            // Check if book is expired
            var bookExpired = playerState->IsWeeklyBingoExpired();
            
            if (bookExpired)
            {
                Status = ModuleStatus.Incomplete;
                _hasBook = false;
            }
            else
            {
                // Count completed lines by checking tasks
                _completedLines = CountCompletedLines(playerState);

                // Update status based on completion
                if (_stickerCount >= 9 || _completedLines >= 1)
                {
                    Status = ModuleStatus.Complete;
                    
                    // Send notification when ready to turn in
                    if (_stickerCount == 9 && !_hasSentNotification)
                    {
                        var linesText = _completedLines > 0 ? $" ({_completedLines} line{(_completedLines > 1 ? "s" : "")})" : "";
                        Plugin.Instance.NotificationManager.SendModuleComplete(Name, 
                            $"Ready to turn in{linesText}!");
                        _hasSentNotification = true;
                    }
                }
                else if (_stickerCount > 0)
                {
                    Status = ModuleStatus.InProgress;
                    _hasSentNotification = false;
                }
                else
                {
                    Status = ModuleStatus.Incomplete;
                    _hasSentNotification = false;
                }
            }
        }
        else
        {
            _stickerCount = 0;
            _completedLines = 0;
            _secondChancePoints = 0;
            _hasSentNotification = false;
            Status = ModuleStatus.Incomplete;
        }
    }

    private unsafe int CountCompletedLines(PlayerState* playerState)
    {
        var lines = 0;
        var stickers = new bool[16];
        
        // Get sticker positions
        for (var i = 0; i < 16; i++)
        {
            var taskStatus = playerState->GetWeeklyBingoTaskStatus(i);
            // Claimable means the task has a sticker on it
            stickers[i] = taskStatus == PlayerState.WeeklyBingoTaskStatus.Claimable || 
                         taskStatus == PlayerState.WeeklyBingoTaskStatus.Claimed;
        }
        
        // Check all possible lines (rows, columns, diagonals)
        // This is a 4x4 grid
        
        // Rows
        for (var row = 0; row < 4; row++)
        {
            var complete = true;
            for (var col = 0; col < 4; col++)
            {
                if (!stickers[row * 4 + col])
                {
                    complete = false;
                    break;
                }
            }
            if (complete) lines++;
        }
        
        // Columns
        for (var col = 0; col < 4; col++)
        {
            var complete = true;
            for (var row = 0; row < 4; row++)
            {
                if (!stickers[row * 4 + col])
                {
                    complete = false;
                    break;
                }
            }
            if (complete) lines++;
        }
        
        // Diagonals
        // Top-left to bottom-right
        if (stickers[0] && stickers[5] && stickers[10] && stickers[15])
        {
            lines++;
        }
        
        // Top-right to bottom-left
        if (stickers[3] && stickers[6] && stickers[9] && stickers[12])
        {
            lines++;
        }
        
        return lines;
    }

    private void UpdateResetTime()
    {
        var now = DateTime.UtcNow;
        
        // Wondrous Tails resets on Tuesday at 15:00 UTC
        var daysUntilTuesday = ((int)DayOfWeek.Tuesday - (int)now.DayOfWeek + 7) % 7;
        if (daysUntilTuesday == 0 && now.Hour >= 15)
        {
            daysUntilTuesday = 7;
        }
        
        _nextReset = now.Date.AddDays(daysUntilTuesday).AddHours(15);
    }

    public override void Reset()
    {
        base.Reset();
        _hasSentNotification = false;
    }

    public override void DrawConfig()
    {
        ImGui.TextUnformatted("Wondrous Tails Configuration");
        ImGui.Separator();
        
        ImGui.TextUnformatted("This module tracks your weekly Wondrous Tails progress.");
        ImGui.TextUnformatted("Pick up your book from Khloe in Idyllshire!");
        
        ImGui.Spacing();
        if (_hasBook)
        {
            var timeUntilDeadline = _deadline - DateTime.UtcNow;
            if (timeUntilDeadline.TotalHours > 0)
            {
                ImGui.TextUnformatted($"Book expires in: {timeUntilDeadline.Days}d {timeUntilDeadline.Hours:D2}h {timeUntilDeadline.Minutes:D2}m");
            }
            else
            {
                ImGui.TextColored(new System.Numerics.Vector4(1, 0, 0, 1), "Book has expired!");
            }
        }
        
        ImGui.Separator();
        var timeUntilReset = _nextReset - DateTime.UtcNow;
        ImGui.TextUnformatted($"Next reset (Tuesday): {timeUntilReset.Days}d {timeUntilReset.Hours:D2}h {timeUntilReset.Minutes:D2}m");
    }

    public override void DrawStatus()
    {
        if (!_hasBook)
        {
            ImGui.TextColored(new System.Numerics.Vector4(1, 0.5f, 0, 1), "Wondrous Tails: No book");
            return;
        }

        var color = Status switch
        {
            ModuleStatus.Complete => new System.Numerics.Vector4(0, 1, 0, 1),
            ModuleStatus.InProgress => new System.Numerics.Vector4(1, 1, 0, 1),
            _ => new System.Numerics.Vector4(1, 1, 1, 1)
        };

        ImGui.TextColored(color, $"Wondrous Tails: {_stickerCount}/9 stickers");
        
        if (_completedLines > 0)
        {
            ImGui.TextColored(new System.Numerics.Vector4(0, 1, 1, 1), 
                $"  {_completedLines} line{(_completedLines > 1 ? "s" : "")} complete!");
        }
        
        if (_secondChancePoints > 0)
        {
            ImGui.TextUnformatted($"  Second Chance: {_secondChancePoints} points");
        }
        
        var timeUntilDeadline = _deadline - DateTime.UtcNow;
        if (timeUntilDeadline.TotalHours < 24 && timeUntilDeadline.TotalHours > 0)
        {
            ImGui.TextColored(new System.Numerics.Vector4(1, 0.5f, 0, 1), 
                $"  Expires in {timeUntilDeadline.Hours}h {timeUntilDeadline.Minutes}m!");
        }
    }
} 