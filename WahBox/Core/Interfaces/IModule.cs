using System;
using ImGuiNET;

namespace WahBox.Core.Interfaces;

public enum ModuleType
{
    Currency,
    Daily,
    Weekly,
    Special
}

public enum ModuleStatus
{
    Unknown,
    Incomplete,
    Complete,
    Unavailable,
    InProgress
}

public interface IModule
{
    string Name { get; }
    ModuleType Type { get; }
    ModuleStatus Status { get; }
    bool IsEnabled { get; set; }
    uint IconId { get; }
    
    void Initialize();
    void Update();
    void Load();
    void Unload();
    void Reset();
    void Dispose();
    
    void DrawConfig();
    void DrawStatus();
} 