using Dalamud.Configuration;
using Dalamud.Plugin;
using System;
using System.Numerics;
using Dalamud.Plugin.Services;

namespace Wahdar;

[Serializable]
public class Configuration : IPluginConfiguration
{
    public int Version { get; set; } = 1;
    
    // Radar window
    public bool ShowRadarWindow { get; set; } = true;
    public float DetectionRadius { get; set; } = 50f;
    public bool RotateWithCamera { get; set; } = false;
    public bool ShowRadiusCircles { get; set; } = true;
    public bool DrawPlayerLines { get; set; } = true;
    public bool ShowObjectList { get; set; } = true;
    public bool ShowObjectListWindow { get; set; } = false;
    
    // Table visibility for each object type
    public bool TableShowPlayers { get; set; } = true;
    public bool TableShowNPCs { get; set; } = true;
    public bool TableShowTreasure { get; set; } = true;
    public bool TableShowGatheringPoints { get; set; } = true;
    public bool TableShowAetherytes { get; set; } = true;
    public bool TableShowEventObjects { get; set; } = true;
    public bool TableShowMounts { get; set; } = false;
    public bool TableShowCompanions { get; set; } = false;
    public bool TableShowRetainers { get; set; } = false;
    public bool TableShowHousingObjects { get; set; } = false;
    public bool TableShowAreaObjects { get; set; } = false;
    public bool TableShowCutsceneObjects { get; set; } = false;
    public bool TableShowCardStands { get; set; } = false;
    public bool TableShowOrnaments { get; set; } = false;
    public bool TableShowIslandSanctuaryObjects { get; set; } = true;
    
    // Clickable objects for navmesh pathfinding
    public bool EnableNavmeshIntegration { get; set; } = true;
    public bool HideUnnamedObjects { get; set; } = true;
    public bool ClickableNPCs { get; set; } = false;
    public bool ClickableTreasure { get; set; } = true;
    public bool ClickableGatheringPoints { get; set; } = true;
    public bool ClickableAetherytes { get; set; } = true;
    public bool ClickableEventObjects { get; set; } = true;
    public bool ClickableMounts { get; set; } = false;
    public bool ClickableCompanions { get; set; } = false;
    public bool ClickableRetainers { get; set; } = false;
    public bool ClickableHousingObjects { get; set; } = false;
    public bool ClickableAreaObjects { get; set; } = false;
    public bool ClickableCutsceneObjects { get; set; } = false;
    public bool ClickableCardStands { get; set; } = false;
    public bool ClickableOrnaments { get; set; } = false;
    public bool ClickableIslandSanctuaryObjects { get; set; } = true;
    
    // Tether/Line options for each object type
    public bool DrawTreasureTethers { get; set; } = false;
    public bool DrawGatheringTethers { get; set; } = false;
    public bool DrawAetheryteTethers { get; set; } = false;
    public bool DrawEventObjectTethers { get; set; } = false;
    public bool DrawMountTethers { get; set; } = false;
    public bool DrawCompanionTethers { get; set; } = false;
    public bool DrawRetainerTethers { get; set; } = false;
    public bool DrawHousingTethers { get; set; } = false;
    public bool DrawAreaTethers { get; set; } = false;
    public bool DrawCutsceneTethers { get; set; } = false;
    public bool DrawNPCTethers { get; set; } = false;
    public bool DrawCardStandTethers { get; set; } = false;
    public bool DrawOrnamentTethers { get; set; } = false;
    public bool DrawIslandSanctuaryTethers { get; set; } = false;
    
    // Alert options for each object type
    public bool AlertOnPlayers { get; set; } = false;
    public bool AlertOnNPCs { get; set; } = false;
    public bool AlertOnTreasure { get; set; } = false;
    public bool AlertOnGatheringPoints { get; set; } = false;
    public bool AlertOnAetherytes { get; set; } = false;
    public bool AlertOnEventObjects { get; set; } = false;
    public bool AlertOnMounts { get; set; } = false;
    public bool AlertOnCompanions { get; set; } = false;
    public bool AlertOnRetainers { get; set; } = false;
    public bool AlertOnHousingObjects { get; set; } = false;
    public bool AlertOnAreaObjects { get; set; } = false;
    public bool AlertOnCutsceneObjects { get; set; } = false;
    public bool AlertOnCardStands { get; set; } = false;
    public bool AlertOnOrnaments { get; set; } = false;
    public bool AlertOnIslandSanctuaryObjects { get; set; } = false;
    public bool TransparentBackground { get; set; } = false;
    public bool ShowAlertRing { get; set; } = true;
    
    // Filters
    public bool ShowPlayers { get; set; } = true;
    public bool ShowNPCs { get; set; } = true;
    public bool ShowTreasure { get; set; } = true;
    public bool ShowGatheringPoints { get; set; } = true;
    public bool ShowAetherytes { get; set; } = true;
    public bool ShowEventObjects { get; set; } = false;
    public bool ShowMounts { get; set; } = false;
    public bool ShowCompanions { get; set; } = false;
    public bool ShowRetainers { get; set; } = false;
    public bool ShowHousingObjects { get; set; } = false;
    public bool ShowAreaObjects { get; set; } = false;
    public bool ShowCutsceneObjects { get; set; } = false;
    public bool ShowCardStands { get; set; } = false;
    public bool ShowOrnaments { get; set; } = false;
    public bool ShowIslandSanctuaryObjects { get; set; } = true;
    
    // In-game overlay
    public bool EnableInGameDrawing { get; set; } = false;
    public bool DrawPlayerCircle { get; set; } = true;
    public bool DrawObjectDots { get; set; } = true;
    public bool DrawDistanceText { get; set; } = true;
    
    public float InGameDotSize { get; set; } = 3.0f;
    public float InGameLineThickness { get; set; } = 1.0f;
    
    // Colors
    public Vector4 InGamePlayerColor { get; set; } = new Vector4(0, 0, 1, 1);
    public Vector4 InGameNPCColor { get; set; } = new Vector4(1, 1, 0, 1);
    public Vector4 InGameTreasureColor { get; set; } = new Vector4(1, 0.8f, 0, 1);        // Gold
    public Vector4 InGameGatheringColor { get; set; } = new Vector4(0, 1, 0, 1);          // Green
    public Vector4 InGameAetheryteColor { get; set; } = new Vector4(0.5f, 0.5f, 1, 1);   // Light Blue
    public Vector4 InGameEventObjectColor { get; set; } = new Vector4(1, 0.5f, 0, 1);    // Orange
    public Vector4 InGameMountColor { get; set; } = new Vector4(0.8f, 0.4f, 0.2f, 1);    // Brown
    public Vector4 InGameCompanionColor { get; set; } = new Vector4(1, 0.7f, 1, 1);      // Pink
    public Vector4 InGameRetainerColor { get; set; } = new Vector4(0.7f, 0.7f, 0.7f, 1); // Gray
    public Vector4 InGameHousingColor { get; set; } = new Vector4(0.6f, 0.3f, 0.1f, 1);  // Dark Brown
    public Vector4 InGameAreaColor { get; set; } = new Vector4(0.5f, 0.8f, 0.5f, 1);     // Light Green
    public Vector4 InGameCutsceneColor { get; set; } = new Vector4(1, 0, 1, 1);          // Magenta
    public Vector4 InGameCardStandColor { get; set; } = new Vector4(0.9f, 0.9f, 0.1f, 1); // Bright Yellow
    public Vector4 InGameOrnamentColor { get; set; } = new Vector4(0.8f, 0.2f, 0.8f, 1); // Purple
    public Vector4 InGameIslandSanctuaryColor { get; set; } = new Vector4(0.2f, 0.8f, 0.6f, 1); // Teal
    public Vector4 InGameRadiusColor { get; set; } = new Vector4(0.5f, 0.5f, 0.5f, 0.7f);
    public Vector4 InGameLineColor { get; set; } = new Vector4(0.0f, 0.5f, 1.0f, 0.7f);
    public Vector4 InGameTextColor { get; set; } = new Vector4(1, 1, 1, 1);
    
    // Alerts
    public bool EnablePlayerProximityAlert { get; set; } = false;
    public bool EnableAlertSound { get; set; } = true;
    public float PlayerProximityAlertDistance { get; set; } = 25f;
    public float PlayerProximityAlertCooldown { get; set; } = 5f;
    public int PlayerProximityAlertSound { get; set; } = 0;
    
    public enum AlertFrequencyMode
    {
        OnlyOnce,         // Alert once per player until restart
        EveryInterval,    // Alert every cooldown period
        OnEnterLeaveReenter  // Alert on enter, then only after leaving and returning
    }
    
    public AlertFrequencyMode PlayerAlertFrequency { get; set; } = AlertFrequencyMode.EveryInterval;

    // Window lock settings
    public bool LockRadarWindow { get; set; } = false;
    public bool LockObjectListWindow { get; set; } = false;
    public Vector2 RadarWindowLockedPosition { get; set; } = new Vector2(100, 100);
    public Vector2 ObjectListWindowLockedPosition { get; set; } = new Vector2(300, 100);

    public void Initialize(IDalamudPluginInterface pluginInterface)
    {
    }
    
    public void Save()
    {
        Plugin.PluginInterface.SavePluginConfig(this);
    }
}
