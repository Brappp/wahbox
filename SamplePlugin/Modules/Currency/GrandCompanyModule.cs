using System.Collections.Generic;
using System.Linq;
using SamplePlugin.Core;
using SamplePlugin.Core.Interfaces;
using SamplePlugin.Models;
using ImGuiNET;
using System.Numerics;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;

namespace SamplePlugin.Modules.Currency;

public class GrandCompanyModule : BaseModule, ICurrencyModule
{
    public override string Name => "Grand Company Seals";
    public override ModuleType Type => ModuleType.Currency;
    
    private readonly List<TrackedCurrency> _trackedCurrencies = new();
    private readonly Dictionary<uint, bool> _previousWarningState = new();

    public GrandCompanyModule(Plugin plugin) : base(plugin)
    {
    }

    public override void Initialize()
    {
        base.Initialize();
        
        // Initialize Grand Company seal tracking
        _trackedCurrencies.AddRange(new[]
        {
            new TrackedCurrency 
            { 
                Type = CurrencyType.Item, 
                ItemId = 20,  // Storm Seals
                Threshold = 75000,
            MaxCount = 90000,
                Enabled = true,
                ShowInOverlay = true,
                WarningText = "Currency near cap",
                ChatWarning = true
            },
            new TrackedCurrency 
            { 
                Type = CurrencyType.Item, 
                ItemId = 21,  // Serpent Seals
                Threshold = 75000,
            MaxCount = 90000,
                Enabled = true,
                ShowInOverlay = true,
                WarningText = "Currency near cap",
                ChatWarning = true
            },
            new TrackedCurrency 
            { 
                Type = CurrencyType.Item, 
                ItemId = 22,  // Flame Seals
                Threshold = 75000,
            MaxCount = 90000,
                Enabled = true,
                ShowInOverlay = true,
                WarningText = "Currency near cap",
                ChatWarning = true
            }
        });
    }

    public override unsafe void Update()
    {
        if (!Plugin.ClientState.IsLoggedIn) return;

        var hasWarning = false;
        var playerGC = GetPlayerGrandCompany();
        
        foreach (var currency in _trackedCurrencies)
        {
            if (!currency.Enabled) continue;
            
            // Only check the seal type for the player's current Grand Company
            if (playerGC != 0 && IsCorrectGrandCompanySeal(currency.ItemId, playerGC))
            {
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
        }

        Status = hasWarning ? ModuleStatus.InProgress : ModuleStatus.Complete;
    }

    private unsafe byte GetPlayerGrandCompany()
    {
        var playerState = FFXIVClientStructs.FFXIV.Client.Game.UI.PlayerState.Instance();
        if (playerState == null) return 0;
        
        return playerState->GrandCompany;
    }

    private bool IsCorrectGrandCompanySeal(uint itemId, byte grandCompany)
    {
        return grandCompany switch
        {
            1 => itemId == 20,  // Maelstrom - Storm Seals
            2 => itemId == 21,  // Twin Adder - Serpent Seals
            3 => itemId == 22,  // Immortal Flames - Flame Seals
            _ => true           // Check all if unknown
        };
    }

    private string GetGrandCompanyName()
    {
        var gc = GetPlayerGrandCompany();
        return gc switch
        {
            1 => "Maelstrom",
            2 => "Twin Adder",
            3 => "Immortal Flames",
            _ => "No Grand Company"
        };
    }

    public override void DrawConfig()
    {
        ImGui.TextUnformatted("Grand Company Seal Tracking");
        ImGui.Separator();

        var playerGC = GetPlayerGrandCompany();

        foreach (var currency in _trackedCurrencies)
        {
            // Only show config for the player's current GC
            if (playerGC != 0 && !IsCorrectGrandCompanySeal(currency.ItemId, playerGC))
                continue;

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
            if (ImGui.Checkbox("Track", ref enabled))
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

        ImGui.Spacing();
        ImGui.TextWrapped("Note: Only your current Grand Company's seals will be tracked.");
    }

    public override void DrawStatus()
    {
        var gcName = GetGrandCompanyName();
        ImGui.TextColored(new Vector4(0.7f, 0.7f, 0.7f, 1), gcName);
        
        var color = Status switch
        {
            ModuleStatus.Complete => new Vector4(0, 1, 0, 1),
            ModuleStatus.InProgress => new Vector4(1, 0.5f, 0, 1),
            _ => new Vector4(1, 1, 1, 1)
        };

        var activeCurrency = _trackedCurrencies.FirstOrDefault(c => c.Enabled);
        if (activeCurrency != null)
        {
            ImGui.TextColored(color, $"Seals: {activeCurrency.CurrentCount:N0}/{activeCurrency.Threshold:N0}");
        }
        else
        {
            ImGui.TextColored(new Vector4(0.5f, 0.5f, 0.5f, 1), "No Grand Company");
        }
    }

    public List<TrackedCurrency> GetTrackedCurrencies() => _trackedCurrencies;
} 