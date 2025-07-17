using System;
using System.IO;
using System.Runtime.InteropServices;
using Dalamud.Game.Text;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Interface.ImGuiNotification;

namespace WahBox.Systems;

public class NotificationManager : IDisposable
{
    private readonly Plugin _plugin;
    
    [DllImport("winmm.dll", SetLastError = true)]
    private static extern bool PlaySound(string pszSound, IntPtr hmod, uint fdwSound);
    
    private const uint SND_ASYNC = 0x0001;
    private const uint SND_FILENAME = 0x00020000;
    
    public NotificationManager()
    {
        _plugin = Plugin.Instance;
    }
    
    public void Initialize()
    {
        // Any initialization if needed
    }
    
    public void SendNotification(string message, WahBoxNotificationType type = WahBoxNotificationType.Info)
    {
        var config = _plugin.Configuration.NotificationSettings;
        
        // Check if we should suppress notifications
        if (config.SuppressInDuty && Plugin.Condition[Dalamud.Game.ClientState.Conditions.ConditionFlag.BoundByDuty])
            return;
        
        // Chat notification
        if (config.ChatNotifications)
        {
            var chatType = type switch
            {
                WahBoxNotificationType.Warning => XivChatType.ErrorMessage,
                WahBoxNotificationType.Error => XivChatType.ErrorMessage,
                WahBoxNotificationType.Success => XivChatType.Echo,
                _ => XivChatType.Echo
            };
            
            Plugin.ChatGui.Print(new SeStringBuilder()
                .AddText("[WahBox] ")
                .AddText(message)
                .Build());
        }
        
        // Toast notification
        if (config.EnableToastNotifications)
        {
            var notification = new Notification
            {
                Content = message,
                Title = "WahBox",
                Type = type switch
                {
                    WahBoxNotificationType.Warning => NotificationType.Warning,
                    WahBoxNotificationType.Error => NotificationType.Error,
                    WahBoxNotificationType.Success => NotificationType.Success,
                    _ => NotificationType.Info
                }
            };
            
            Plugin.PluginInterface.UiBuilder.AddNotification(notification);
        }
        
        // Sound notification
        if (config.EnableSoundAlerts)
        {
            var soundId = type switch
            {
                WahBoxNotificationType.Warning => config.CurrencyAlertSound,
                WahBoxNotificationType.Success => config.TaskCompleteSound,
                _ => 0
            };
            
            PlaySound(soundId);
        }
    }
    
    public void PlaySound(int soundId)
    {
        try
        {
            string soundFileName = soundId switch
            {
                0 => "ping.wav",
                1 => "alert.wav",
                2 => "notification.wav",
                3 => "alarm.wav",
                _ => "ping.wav"
            };
            
            var soundFilePath = Path.Combine(Plugin.PluginInterface.AssemblyLocation.DirectoryName!, "Data", "sounds", soundFileName);
            
            if (!File.Exists(soundFilePath))
            {
                Plugin.Log.Error($"Could not find sound file: {soundFileName} at {soundFilePath}");
                return;
            }
            
            PlaySound(soundFilePath, IntPtr.Zero, SND_FILENAME | SND_ASYNC);
        }
        catch (Exception ex)
        {
            Plugin.Log.Error($"Error playing sound: {ex.Message}");
        }
    }
    
    public void SendModuleComplete(string moduleName, string? details = null)
    {
        if (_plugin.Configuration.NotificationSettings.TaskCompletionAlerts)
        {
            var message = string.IsNullOrEmpty(details) ? $"{moduleName} completed!" : details;
            SendNotification(message, WahBoxNotificationType.Success);
        }
    }
    
    public void SendCurrencyWarning(string currencyName, int current, int threshold)
    {
        if (_plugin.Configuration.NotificationSettings.CurrencyWarningAlerts)
        {
            SendNotification($"{currencyName} is near cap: {current:N0}/{threshold:N0}", WahBoxNotificationType.Warning);
        }
    }
    
    public void Dispose()
    {
        // Cleanup if needed
    }
}

public enum WahBoxNotificationType
{
    Info,
    Success,
    Warning,
    Error
}
