using System;
using System.Collections.Generic;
using Dalamud.Game.Text;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Game.Text.SeStringHandling.Payloads;
using ECommons.ChatMethods;

namespace SamplePlugin.Systems;

public class NotificationManager : IDisposable
{
    private readonly HashSet<string> _recentNotifications = new();
    private DateTime _lastCleanup = DateTime.UtcNow;
    private readonly TimeSpan _notificationCooldown = TimeSpan.FromMinutes(5);

    public void Initialize()
    {
        Plugin.Framework.Update += OnFrameworkUpdate;
    }

    public void Dispose()
    {
        Plugin.Framework.Update -= OnFrameworkUpdate;
    }

    private void OnFrameworkUpdate(Dalamud.Plugin.Services.IFramework framework)
    {
        // Clean up old notifications every minute
        if (DateTime.UtcNow - _lastCleanup > TimeSpan.FromMinutes(1))
        {
            _recentNotifications.Clear();
            _lastCleanup = DateTime.UtcNow;
        }
    }

    public void SendCurrencyWarning(string currencyName, int current, int threshold)
    {
        var key = $"currency_{currencyName}_{DateTime.UtcNow:yyyyMMddHH}";
        if (_recentNotifications.Contains(key)) return;

        var message = new SeStringBuilder()
            .AddUiForeground($"[Wahdori] ", 506)
            .AddText($"{currencyName} is at ")
            .AddUiForeground($"{current:N0}/{threshold:N0}", 500)
            .AddText(" - Near cap!")
            .Build();

        Plugin.ChatGui.Print(message);
        _recentNotifications.Add(key);
    }

    public void SendModuleComplete(string moduleName, string details = "")
    {
        var key = $"module_{moduleName}_{DateTime.UtcNow:yyyyMMdd}";
        if (_recentNotifications.Contains(key)) return;

        var message = new SeStringBuilder()
            .AddUiForeground($"[Wahdori] ", 506)
            .AddText($"{moduleName} ")
            .AddUiForeground("Complete!", 43)
            .Build();

        if (!string.IsNullOrEmpty(details))
        {
            message.Append($" {details}");
        }

        Plugin.ChatGui.Print(message);
        _recentNotifications.Add(key);
    }

    public void SendReminder(string title, string message)
    {
        var seMessage = new SeStringBuilder()
            .AddUiForeground($"[Wahdori] {title}: ", 506)
            .AddText(message)
            .Build();

        Plugin.ChatGui.Print(seMessage);
    }

    public void SendError(string message)
    {
        var seMessage = new SeStringBuilder()
            .AddUiForeground($"[Wahdori Error] ", 17)
            .AddText(message)
            .Build();

        Plugin.ChatGui.PrintError(seMessage);
    }
} 