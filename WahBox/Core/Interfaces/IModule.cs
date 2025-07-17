using System;
using ImGuiNET;

namespace WahBox.Core.Interfaces;

public enum ModuleType
{
    Currency,
    Daily,
    Weekly,
    Special,
    Radar,
    Speedometer,
    Utility
}

public enum ModuleCategory
{
    Tracking,    // Currency, Daily, Weekly tasks
    Utility,     // Radar, Speedometer, etc.
    Display,     // Overlays, HUD elements
    Tools        // Misc tools
}

public enum ModuleStatus
{
    Unknown,
    Incomplete,
    Complete,
    Unavailable,
    InProgress,
    Active,      // For utility modules that are running
    Inactive     // For utility modules that are stopped
}

public interface IModule
{
    string Name { get; }
    ModuleType Type { get; }
    ModuleCategory Category { get; }
    ModuleStatus Status { get; }
    bool IsEnabled { get; set; }
    uint IconId { get; }
    
    // Add support for modules with their own windows
    bool HasWindow { get; }
    void OpenWindow();
    void CloseWindow();
    
    void Initialize();
    void Update();
    void Load();
    void Unload();
    void Reset();
    void Dispose();
    
    void DrawConfig();
    void DrawStatus();
    
    // Configuration methods
    void SaveConfiguration();
    void LoadConfiguration();
} 