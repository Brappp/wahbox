using System.Collections.Generic;
using SamplePlugin.Core;
using SamplePlugin.Core.Interfaces;
using SamplePlugin.Models;
using ImGuiNET;
using System.Numerics;

namespace SamplePlugin.Modules.Currency;

public class AlliedSealsModule : BaseModule
{
    public override string Name => "Allied Seals";
    public override ModuleType Type => ModuleType.Currency;
    
    private readonly List<TrackedCurrency> _trackedCurrencies = new();
    private readonly Dictionary<uint, bool> _previousWarningState = new();

    public AlliedSealsModule(Plugin plugin) : base(plugin)
    {
    }

    public override void Initialize()
    {
        base.Initialize();
        
        // Initialize Allied Seals tracking
        _trackedCurrencies.Add(new TrackedCurrency 
        { 
            Type = CurrencyType.Item,
            ItemId = 27,  // Allied Seals
            Threshold = 3500,
            Enabled = true,
            ShowInOverlay = true,
            ChatWarning = true,
            WarningText = Plugin.Instance.LocalizationManager.GetString("currency.near_cap")
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
        ImGui.TextUnformatted("Allied Seals Tracking");
        ImGui.Separator();
        ImGui.TextWrapped("Allied Seals are earned from A Realm Reborn hunt marks.");
        ImGui.Spacing();

        foreach (var currency in _trackedCurrencies)
        {
            ImGui.PushID(currency.Name);
            
            if (currency.Icon != null)
            {
                ImGui.Image(currency.Icon.ImGuiHandle, new Vector2(20, 20));
                ImGui.SameLine();
            }

            ImGui.Text($"{currency.Name}: {currency.CurrentCount:N0} / {currency.Threshold:N0}");
            
            ImGui.SameLine();
            var enabled = currency.Enabled;
            if (ImGui.Checkbox(Plugin.Instance.LocalizationManager.GetString("config.enabled"), ref enabled))
            {
                currency.Enabled = enabled;
            }
            
            ImGui.SameLine();
            ImGui.SetNextItemWidth(100);
            var threshold = currency.Threshold;
            if (ImGui.InputInt(Plugin.Instance.LocalizationManager.GetString("config.threshold"), ref threshold, 0, 0))
            {
                currency.Threshold = System.Math.Max(0, threshold);
            }
            
            ImGui.SameLine();
            var chatWarning = currency.ChatWarning;
            if (ImGui.Checkbox(Plugin.Instance.LocalizationManager.GetString("config.chat_alert"), ref chatWarning))
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
            
            ImGui.TextColored(color, $"{currency.Name}: {currency.CurrentCount:N0}/{currency.Threshold:N0}");
        }
    }

    public List<TrackedCurrency> GetTrackedCurrencies() => _trackedCurrencies;
} 