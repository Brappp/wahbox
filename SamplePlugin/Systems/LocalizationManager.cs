using System;
using System.Collections.Generic;
using System.Globalization;
using Dalamud.Game;

namespace SamplePlugin.Systems;

public class LocalizationManager : IDisposable
{
    private readonly Dictionary<string, Dictionary<string, string>> _translations = new();
    private string _currentLanguage = "en";

    public void Initialize()
    {
        // Set language based on client language
        _currentLanguage = Plugin.PluginInterface.UiLanguage switch
        {
            "ja" => "ja",
            "en" => "en",
            "de" => "de",
            "fr" => "fr",
            _ => "en"
        };

        // Initialize translations
        InitializeTranslations();
    }

    private void InitializeTranslations()
    {
        // English translations
        _translations["en"] = new Dictionary<string, string>
        {
            ["plugin.name"] = "Wahdori",
            ["plugin.description"] = "Currency alerts and daily duties tracker",
            ["currency.near_cap"] = "Near Cap!",
            ["currency.above_threshold"] = "Above Threshold",
            ["module.complete"] = "Complete!",
            ["module.incomplete"] = "Incomplete",
            ["module.in_progress"] = "In Progress",
            ["roulette.next_reset"] = "Next reset in: {0}",
            ["config.threshold"] = "Threshold",
            ["config.enabled"] = "Enabled",
            ["config.track"] = "Track",
            ["config.chat_alert"] = "Chat Alert",
            ["config.show_overlay"] = "Show in Overlay"
        };

        // Japanese translations
        _translations["ja"] = new Dictionary<string, string>
        {
            ["plugin.name"] = "Wahdori",
            ["plugin.description"] = "通貨アラートとデイリー進捗トラッカー",
            ["currency.near_cap"] = "上限間近！",
            ["currency.above_threshold"] = "しきい値超過",
            ["module.complete"] = "完了！",
            ["module.incomplete"] = "未完了",
            ["module.in_progress"] = "進行中",
            ["roulette.next_reset"] = "次のリセットまで: {0}",
            ["config.threshold"] = "しきい値",
            ["config.enabled"] = "有効",
            ["config.track"] = "追跡",
            ["config.chat_alert"] = "チャット通知",
            ["config.show_overlay"] = "オーバーレイに表示"
        };

        // German translations
        _translations["de"] = new Dictionary<string, string>
        {
            ["plugin.name"] = "Wahdori",
            ["plugin.description"] = "Währungsalarme und tägliche Aufgaben-Tracker",
            ["currency.near_cap"] = "Fast am Limit!",
            ["currency.above_threshold"] = "Über Schwellenwert",
            ["module.complete"] = "Abgeschlossen!",
            ["module.incomplete"] = "Unvollständig",
            ["module.in_progress"] = "In Bearbeitung",
            ["roulette.next_reset"] = "Nächster Reset in: {0}",
            ["config.threshold"] = "Schwellenwert",
            ["config.enabled"] = "Aktiviert",
            ["config.track"] = "Verfolgen",
            ["config.chat_alert"] = "Chat-Benachrichtigung",
            ["config.show_overlay"] = "Im Overlay anzeigen"
        };

        // French translations
        _translations["fr"] = new Dictionary<string, string>
        {
            ["plugin.name"] = "Wahdori",
            ["plugin.description"] = "Alertes de devises et suivi des tâches quotidiennes",
            ["currency.near_cap"] = "Proche du maximum !",
            ["currency.above_threshold"] = "Au-dessus du seuil",
            ["module.complete"] = "Terminé !",
            ["module.incomplete"] = "Incomplet",
            ["module.in_progress"] = "En cours",
            ["roulette.next_reset"] = "Prochain reset dans : {0}",
            ["config.threshold"] = "Seuil",
            ["config.enabled"] = "Activé",
            ["config.track"] = "Suivre",
            ["config.chat_alert"] = "Alerte chat",
            ["config.show_overlay"] = "Afficher dans l'overlay"
        };
    }

    public string GetString(string key, params object[] args)
    {
        if (_translations.TryGetValue(_currentLanguage, out var langDict) && 
            langDict.TryGetValue(key, out var translation))
        {
            return args.Length > 0 ? string.Format(translation, args) : translation;
        }

        // Fallback to English
        if (_translations["en"].TryGetValue(key, out var englishTranslation))
        {
            return args.Length > 0 ? string.Format(englishTranslation, args) : englishTranslation;
        }

        // Return key if no translation found
        return key;
    }

    public void SetLanguage(string languageCode)
    {
        if (_translations.ContainsKey(languageCode))
        {
            _currentLanguage = languageCode;
        }
    }

    public string CurrentLanguage => _currentLanguage;

    public void Dispose()
    {
        // Nothing to dispose
    }
} 