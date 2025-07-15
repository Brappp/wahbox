using System;
using Dalamud.Game.Text;
using Dalamud.Game.Text.SeStringHandling;
using FFXIVClientStructs.FFXIV.Client.Game.UI;

namespace WahBox.Systems;

public unsafe class TeleportManager : IDisposable
{
    public void Teleport(uint aetheryteId)
    {
        try
        {
            Telepo.Instance()->Teleport(aetheryteId, 0);
            Plugin.ChatGui.Print(new XivChatEntry
            {
                Message = new SeStringBuilder()
                    .AddUiForeground($"[Wahdori] ", 45)
                    .AddUiForeground("[Teleport] ", 62)
                    .AddText($"Teleporting to Aetheryte {aetheryteId}")
                    .Build(),
            });
        }
        catch (Exception ex)
        {
            Plugin.Log.Error($"Failed to teleport: {ex}");
        }
    }
    
    public void Dispose()
    {
        // Nothing to dispose
    }
} 