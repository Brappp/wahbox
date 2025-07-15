using System.Collections.Generic;
using SamplePlugin.Core;
using SamplePlugin.Core.Interfaces;
using SamplePlugin.Models;
using ImGuiNET;
using System.Numerics;

namespace SamplePlugin.Modules.Currency;

public class TomestoneModule : BaseModule, ICurrencyModule
{
    public override string Name => "Tomestone Tracker";
    public override ModuleType Type => ModuleType.Currency;
    
    private readonly List<TrackedCurrency> _trackedCurrencies = new();
    private readonly Dictionary<uint, bool> _previousWarningState = new();

    public TomestoneModule(Plugin plugin) : base(plugin)
    {
        IconId = 65049; // Module icon
    }

    public override void Initialize()
    {
        base.Initialize();
        
        // Initialize default tomestone tracking
        _trackedCurrencies.AddRange(new[]
        {
            new TrackedCurrency 
            { 
                Type = CurrencyType.NonLimitedTomestone, 
                Threshold = 1400,
                Enabled = true,
                ShowInOverlay = true,
                ChatWarning = true,
                WarningText = "Near Cap!"
            },
            new TrackedCurrency 
            { 
                Type = CurrencyType.LimitedTomestone, 
                Threshold = 1400,
                Enabled = true,
                ShowInOverlay = true,
                ChatWarning = true,
                WarningText = "Near Cap!"
            },
            new TrackedCurrency 
            { 
                Type = CurrencyType.Item, 
                ItemId = 28, // Allagan Tomestone of Poetics
                Threshold = 1400,
            MaxCount = 2000,
                Enabled = true,
                ShowInOverlay = true,
                ChatWarning = true,
                WarningText = "Near Cap!"
            }
        });
    }

    public override void Update()
    {
        if (!Plugin.ClientState.IsLoggedIn) return;

        var hasWarning = false;
        foreach (var currency in _trackedCurrencies)
        {
            if (!currency.Enabled) continue;

            var currentWarning = currency.HasWarning;
            var previousWarning = _previousWarningState.GetValueOrDefault(currency.ItemId, false);

            // Send notification if warning state changed from false to true
            if (currentWarning && !previousWarning && currency.ChatWarning)
            {
                Plugin.Instance.NotificationManager.SendCurrencyWarning(
                    currency.Name, 
                    currency.CurrentCount, 
                    currency.Threshold
                );
            }

            _previousWarningState[currency.ItemId] = currentWarning;

            if (currentWarning)
            {
                hasWarning = true;
            }
        }

        Status = hasWarning ? ModuleStatus.InProgress : ModuleStatus.Complete;
    }

    public override void DrawConfig()
    {
        ImGui.TextUnformatted("Wahdori");
        ImGui.Separator();

        foreach (var currency in _trackedCurrencies)
        {
            ImGui.PushID(currency.Name);
            
            if (currency.Icon != null)
            {
                ImGui.Image(currency.Icon.ImGuiHandle, new Vector2(20, 20));
                ImGui.SameLine();
            }

            ImGui.Text($"{currency.Name}: {currency.CurrentCount} / {currency.Threshold}");
            
            ImGui.SameLine();
            var enabled = currency.Enabled;
            if (ImGui.Checkbox("Enabled", ref enabled))
            {
                currency.Enabled = enabled;
            }
            
            ImGui.SameLine();
            ImGui.SetNextItemWidth(100);
            var threshold = currency.Threshold;
            if (ImGui.InputInt("Warning Threshold", ref threshold, 0, 0))
            {
                currency.Threshold = System.Math.Max(0, threshold);
            }
            
            ImGui.SameLine();
            var chatWarning = currency.ChatWarning;
            if (ImGui.Checkbox("Chat Alerts", ref chatWarning))
            {
                currency.ChatWarning = chatWarning;
            }
            
            if (currency.HasWarning)
            {
                ImGui.SameLine();
                ImGui.TextColored(new Vector4(1, 0.5f, 0, 1), currency.WarningText);
            }
            
            ImGui.PopID();
        }
    }

    public override void DrawStatus()
    {
        foreach (var currency in _trackedCurrencies)
        {
            if (!currency.Enabled) continue;

            var color = currency.HasWarning ? new Vector4(1, 0.5f, 0, 1) : new Vector4(1, 1, 1, 1);
            
            var maxDisplay = currency.MaxCount > 0 ? currency.MaxCount : currency.Threshold;
            ImGui.TextColored(color, $"{currency.Name}: {currency.CurrentCount:N0}/{maxDisplay:N0}");
        }
    }

    public List<TrackedCurrency> GetTrackedCurrencies() => _trackedCurrencies;
} 