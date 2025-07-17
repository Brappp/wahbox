using Dalamud.Configuration;
using Dalamud.Plugin;
using System;

namespace ZoomiesPlugin.Core
{
    [Serializable]
    public class Configuration : IPluginConfiguration
    {
        public int Version { get; set; } = 0;

        // UI preferences
        public bool ShowSpeedometerOnStartup { get; set; } = true;
        public int SelectedSpeedometerType { get; set; } = 0;
        public int SelectedTab { get; set; } = 0;

        // Speedometer settings
        public float MaxYalms { get; set; } = 20.0f;
        public float RedlineStart { get; set; } = 16.0f;
        public float NeedleDamping { get; set; } = 0.1f;

        // Debug settings
        public bool ShowSimpleMode { get; set; } = true;
        public bool ShowAdvancedInfo { get; set; } = false;
        public bool ShowHistoryTable { get; set; } = false;

        // Helper to save config
        public void Save()
        {
            Plugin.PluginInterface.SavePluginConfig(this);
        }
    }
}
