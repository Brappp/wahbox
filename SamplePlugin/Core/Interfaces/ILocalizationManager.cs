namespace SamplePlugin.Core.Interfaces;

public interface ILocalizationManager
{
    void SetLanguage(string language);
    string GetString(string key);
} 