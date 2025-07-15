using SamplePlugin.Core.Interfaces;

namespace SamplePlugin.Systems;

public class LocalizationManager : ILocalizationManager
{
    private string _currentLanguage = "English";
    
    public void SetLanguage(string language)
    {
        _currentLanguage = language;
        // In a real implementation, this would load language files
    }
    
    public string GetString(string key)
    {
        // For now, just return the key
        return key;
    }
} 