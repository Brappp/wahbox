using System;
using System.Linq;
using Dalamud.Plugin.Services;
using Lumina.Excel.Sheets;

namespace WahBox.Systems;

public class TeleportManager : IDisposable
{
    private readonly Plugin _plugin;
    
    public TeleportManager()
    {
        _plugin = Plugin.Instance;
    }
    
    public void TeleportToAetheryte(uint aetheryteId)
    {
        try
        {
            // Use the teleport command
            var sheet = Plugin.DataManager.GetExcelSheet<Aetheryte>();
            if (sheet != null && sheet.TryGetRow(aetheryteId, out var aetheryte))
            {
                var placeName = aetheryte.PlaceName.ValueNullable?.Name.ExtractText();
                if (!string.IsNullOrEmpty(placeName))
                {
                    Plugin.CommandManager.ProcessCommand($"/tp {placeName}");
                }
            }
        }
        catch (Exception ex)
        {
            Plugin.Log.Error($"Failed to teleport: {ex.Message}");
        }
    }
    
    public void TeleportToLocation(string locationName)
    {
        try
        {
            Plugin.CommandManager.ProcessCommand($"/tp {locationName}");
        }
        catch (Exception ex)
        {
            Plugin.Log.Error($"Failed to teleport to {locationName}: {ex.Message}");
        }
    }
    
    public void Dispose()
    {
        // Cleanup if needed
    }
}
