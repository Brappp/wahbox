using System.Collections.Generic;
using SamplePlugin.Core;
using SamplePlugin.Core.Interfaces;
using SamplePlugin.Models;
using ImGuiNET;
using System.Numerics;

namespace SamplePlugin.Modules.Currency;

public class WolfMarksModule : BaseModule, ICurrencyModule
{
    public override string Name => "Wolf Marks";
    public override ModuleType Type => ModuleType.Currency;
    
    private readonly List<TrackedCurrency> _trackedCurrencies = new();
    private readonly Dictionary<uint, bool> _previousWarningState = new();

    public WolfMarksModule(Plugin plugin) : base(plugin)
    {
    }

    public override void Initialize()
    {
        base.Initialize();
        
        // Initialize Wolf Marks tracking
        _trackedCurrencies.Add(new TrackedCurrency 
        { 
            Type = CurrencyType.Item,
            ItemId = 25,  // Wolf Marks
            Threshold = 18000,
            MaxCount = 20000,  // Wolf Marks cap at 20,000
            Enabled = true,
            ShowInOverlay = true,
            ChatWarning = true,
            WarningText = "Currency near cap"
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
        ImGui.TextUnformatted("Wolf Marks Tracking");
        ImGui.Separator();
        ImGui.TextWrapped("Wolf Marks are earned from PvP activities.");
        ImGui.Spacing();

        foreach (var currency in _trackedCurrencies)
        {
            ImGui.PushID(currency.Name);
            
            if (currency.Icon != null)
            {
                ImGui.Image(currency.Icon.ImGuiHandle, new Vector2(20, 20));
                ImGui.SameLine();
            }

            var maxDisplay = currency.MaxCount > 0 ? currency.MaxCount : currency.Threshold;
            ImGui.Text($"{currency.Name}: {currency.CurrentCount:N0} / {maxDisplay:N0}");
            
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