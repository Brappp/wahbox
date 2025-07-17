using System.Collections.Generic;
using WahBox.Core;
using WahBox.Core.Interfaces;
using WahBox.Models;
using ImGuiNET;
using System.Numerics;

namespace WahBox.Modules.Currency;

public class AestheticsTomestoneModule : BaseModule, ICurrencyModule
{
    public override string Name => "Tomestones of Aesthetics";
    public override ModuleType Type => ModuleType.Currency;
    
    private readonly List<TrackedCurrency> _trackedCurrencies = new();
    private readonly Dictionary<uint, bool> _previousWarningState = new();

    public AestheticsTomestoneModule(Plugin plugin) : base(plugin)
    {
        IconId = 65083; // Aesthetics tomestone icon
    }

    public override void Initialize()
    {
        base.Initialize();
        
        // Initialize Aesthetics tomestone tracking
        _trackedCurrencies.Add(new TrackedCurrency 
        { 
            Type = CurrencyType.Item,
            ItemId = 47,  // Tomestones of Aesthetics
            Threshold = 1400,
            MaxCount = 2000,
            Enabled = true,
            ShowInOverlay = true,
            ChatWarning = true,
            WarningText = "Near Cap!"
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
        var currency = _trackedCurrencies[0];
        
        ImGui.Text("Tomestones of Aesthetics Settings");
        ImGui.Separator();
        
        // Threshold setting
        var threshold = currency.Threshold;
        if (ImGui.InputInt("Warning Threshold", ref threshold, 50))
        {
            currency.Threshold = threshold;
        }
        
        ImGui.TextColored(new Vector4(0.7f, 0.7f, 0.7f, 1), "Maximum: 2,000");
        
        ImGui.Spacing();
        
        // Chat warning
        var chatWarning = currency.ChatWarning;
        if (ImGui.Checkbox("Chat Warning", ref chatWarning))
        {
            currency.ChatWarning = chatWarning;
        }
        
        ImGui.Spacing();
        
        // Warning text
        var warningText = currency.WarningText ?? "";
        if (ImGui.InputText("Warning Message", ref warningText, 100))
        {
            currency.WarningText = warningText;
        }
    }

    public override void DrawStatus()
    {
        var currency = _trackedCurrencies[0];
        ImGui.Text($"{currency.Name}: {currency.CurrentCount:N0} / {currency.MaxCount:N0}");
        
        if (currency.HasWarning)
        {
            ImGui.TextColored(new Vector4(1, 0.5f, 0, 1), "⚠ Near cap!");
        }
    }

    public List<TrackedCurrency> GetTrackedCurrencies() => _trackedCurrencies;
}
