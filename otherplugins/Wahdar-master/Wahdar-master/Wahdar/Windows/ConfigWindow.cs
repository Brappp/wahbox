using System;
using System.Numerics;
using Dalamud.Interface.Windowing;
using ImGuiNET;
using Dalamud.Interface.Components;

namespace Wahdar.Windows;

public class ConfigWindow : Window, IDisposable
{
    private Configuration Configuration;
    private Plugin Plugin;

    public ConfigWindow(Plugin plugin) : base("Wahdar Configuration")
    {
        Flags = ImGuiWindowFlags.NoCollapse | ImGuiWindowFlags.NoScrollWithMouse | ImGuiWindowFlags.AlwaysAutoResize;

        Configuration = plugin.Configuration;
        Plugin = plugin;
    }

    public void Dispose() { }

    public override void PreDraw()
    {
    }

    public override void Draw()
    {
        if (ImGui.BeginTabBar("##configTabs"))
        {
            if (ImGui.BeginTabItem("General"))
            {
                DrawGeneralTab();
                ImGui.EndTabItem();
            }
            
            if (ImGui.BeginTabItem("In-Game Overlay"))
            {
                DrawInGameOverlayTab();
                ImGui.EndTabItem();
            }
            
            if (ImGui.BeginTabItem("Navmesh"))
            {
                DrawNavmeshTab();
                ImGui.EndTabItem();
            }
            
            if (ImGui.BeginTabItem("Alerts"))
            {
                DrawAlertsTab();
                ImGui.EndTabItem();
            }
            
            if (ImGui.BeginTabItem("Colors"))
            {
                DrawColorsTab();
                ImGui.EndTabItem();
            }
            
            ImGui.EndTabBar();
        }
    }
    
    private void DrawGeneralTab()
    {
        var showRadar = Configuration.ShowRadarWindow;
        if (ImGui.Checkbox("Show Radar Window", ref showRadar))
        {
            Configuration.ShowRadarWindow = showRadar;
            Configuration.Save();
            
            // Apply visibility change directly
            Plugin.ApplyRadarVisibility();
        }
        
        ImGui.Separator();
        ImGui.TextUnformatted("Radar Settings");
        
        var radius = Configuration.DetectionRadius;
        if (ImGui.SliderFloat("Detection Radius", ref radius, 10f, 300f, "%.1f yalms"))
        {
            Configuration.DetectionRadius = radius;
            Configuration.Save();
        }
        
        var rotateWithCamera = Configuration.RotateWithCamera;
        if (ImGui.Checkbox("Rotate with Camera", ref rotateWithCamera))
        {
            Configuration.RotateWithCamera = rotateWithCamera; 
            Configuration.Save();
        }
        ImGui.SameLine();
        ImGuiComponents.HelpMarker("When enabled, the radar will rotate to match your camera orientation");
        
        var showRadiusCircles = Configuration.ShowRadiusCircles;
        if (ImGui.Checkbox("Show Radius Circles", ref showRadiusCircles))
        {
            Configuration.ShowRadiusCircles = showRadiusCircles;
            Configuration.Save();
        }
        
        var drawLinesToPlayers = Configuration.DrawPlayerLines;
        if (ImGui.Checkbox("Draw Lines to Players", ref drawLinesToPlayers))
        {
            Configuration.DrawPlayerLines = drawLinesToPlayers;
            Configuration.Save();
        }
        
        var showObjectListWindow = Configuration.ShowObjectListWindow;
        if (ImGui.Checkbox("Show Object List Window", ref showObjectListWindow))
        {
            Configuration.ShowObjectListWindow = showObjectListWindow;
            Configuration.Save();
            Plugin.ApplyObjectListVisibility();
            
            // Set the window state to match the checkbox
            Plugin.SetObjectListWindowState(showObjectListWindow);
        }
        ImGui.SameLine();
        ImGuiComponents.HelpMarker("Shows a separate window with a sortable table of detected objects");
        
        var hideUnnamedObjects = Configuration.HideUnnamedObjects;
        if (ImGui.Checkbox("Hide Unnamed Objects", ref hideUnnamedObjects))
        {
            Configuration.HideUnnamedObjects = hideUnnamedObjects;
            Configuration.Save();
        }
        ImGui.SameLine();
        ImGuiComponents.HelpMarker("Hide objects with empty or missing names from radar and object list");
        
        var transparentBackground = Configuration.TransparentBackground;
        if (ImGui.Checkbox("Transparent Background", ref transparentBackground))
        {
            Configuration.TransparentBackground = transparentBackground;
            Configuration.Save();
        }
        ImGui.SameLine();
        ImGuiComponents.HelpMarker("Makes the radar window background transparent");
        
        ImGui.Separator();
        ImGui.TextUnformatted("Window Lock Settings");
        
        var lockRadarWindow = Configuration.LockRadarWindow;
        if (ImGui.Checkbox("Lock Radar Window Position", ref lockRadarWindow))
        {
            Configuration.LockRadarWindow = lockRadarWindow;
            Configuration.Save();
        }
        ImGui.SameLine();
        ImGuiComponents.HelpMarker("Prevents the radar window from being moved. Position is saved when unlocked.");
        
        var lockObjectListWindow = Configuration.LockObjectListWindow;
        if (ImGui.Checkbox("Lock Object List Window Position", ref lockObjectListWindow))
        {
            Configuration.LockObjectListWindow = lockObjectListWindow;
            Configuration.Save();
        }
        ImGui.SameLine();
        ImGuiComponents.HelpMarker("Prevents the object list window from being moved. Position is saved when unlocked.");
        
        ImGui.Separator();
        ImGui.TextUnformatted("Object Configuration");
        
        // Create table with better resizing
        if (ImGui.BeginTable("ObjectConfigTable", 6, ImGuiTableFlags.Resizable | ImGuiTableFlags.BordersOuter | ImGuiTableFlags.BordersV | ImGuiTableFlags.RowBg))
        {
            // Setup columns with better sizing
            ImGui.TableSetupColumn("Object", ImGuiTableColumnFlags.WidthFixed, 100);
            ImGui.TableSetupColumn("Show", ImGuiTableColumnFlags.WidthFixed, 45);
            ImGui.TableSetupColumn("Tether", ImGuiTableColumnFlags.WidthFixed, 50);
            ImGui.TableSetupColumn("Alert", ImGuiTableColumnFlags.WidthFixed, 45);
            ImGui.TableSetupColumn("Table", ImGuiTableColumnFlags.WidthFixed, 45);
            ImGui.TableSetupColumn("Color", ImGuiTableColumnFlags.WidthStretch);
            ImGui.TableHeadersRow();
            
            DrawObjectRow("Players", "Other players in the area", Configuration.ShowPlayers, Configuration.DrawPlayerLines, Configuration.AlertOnPlayers, Configuration.TableShowPlayers, Configuration.InGamePlayerColor, 
                         v => Configuration.ShowPlayers = v, v => Configuration.DrawPlayerLines = v, v => Configuration.AlertOnPlayers = v, v => Configuration.TableShowPlayers = v, v => Configuration.InGamePlayerColor = v);
            DrawObjectRow("NPCs", "Non-player characters and enemies", Configuration.ShowNPCs, Configuration.DrawNPCTethers, Configuration.AlertOnNPCs, Configuration.TableShowNPCs, Configuration.InGameNPCColor,
                         v => Configuration.ShowNPCs = v, v => Configuration.DrawNPCTethers = v, v => Configuration.AlertOnNPCs = v, v => Configuration.TableShowNPCs = v, v => Configuration.InGameNPCColor = v);
            DrawObjectRow("Treasure", "Treasure chests and valuable items", Configuration.ShowTreasure, Configuration.DrawTreasureTethers, Configuration.AlertOnTreasure, Configuration.TableShowTreasure, Configuration.InGameTreasureColor,
                         v => Configuration.ShowTreasure = v, v => Configuration.DrawTreasureTethers = v, v => Configuration.AlertOnTreasure = v, v => Configuration.TableShowTreasure = v, v => Configuration.InGameTreasureColor = v);
            DrawObjectRow("Gathering", "Mining nodes, botanist nodes, fishing spots", Configuration.ShowGatheringPoints, Configuration.DrawGatheringTethers, Configuration.AlertOnGatheringPoints, Configuration.TableShowGatheringPoints, Configuration.InGameGatheringColor,
                         v => Configuration.ShowGatheringPoints = v, v => Configuration.DrawGatheringTethers = v, v => Configuration.AlertOnGatheringPoints = v, v => Configuration.TableShowGatheringPoints = v, v => Configuration.InGameGatheringColor = v);
            DrawObjectRow("Aetherytes", "Teleportation crystals and aether currents", Configuration.ShowAetherytes, Configuration.DrawAetheryteTethers, Configuration.AlertOnAetherytes, Configuration.TableShowAetherytes, Configuration.InGameAetheryteColor,
                         v => Configuration.ShowAetherytes = v, v => Configuration.DrawAetheryteTethers = v, v => Configuration.AlertOnAetherytes = v, v => Configuration.TableShowAetherytes = v, v => Configuration.InGameAetheryteColor = v);
            DrawObjectRow("Events", "Interactive objects like doors, switches, quest items", Configuration.ShowEventObjects, Configuration.DrawEventObjectTethers, Configuration.AlertOnEventObjects, Configuration.TableShowEventObjects, Configuration.InGameEventObjectColor,
                         v => Configuration.ShowEventObjects = v, v => Configuration.DrawEventObjectTethers = v, v => Configuration.AlertOnEventObjects = v, v => Configuration.TableShowEventObjects = v, v => Configuration.InGameEventObjectColor = v);
            DrawObjectRow("Mounts", "Player and NPC mounts", Configuration.ShowMounts, Configuration.DrawMountTethers, Configuration.AlertOnMounts, Configuration.TableShowMounts, Configuration.InGameMountColor,
                         v => Configuration.ShowMounts = v, v => Configuration.DrawMountTethers = v, v => Configuration.AlertOnMounts = v, v => Configuration.TableShowMounts = v, v => Configuration.InGameMountColor = v);
            DrawObjectRow("Companions", "Minions and pets", Configuration.ShowCompanions, Configuration.DrawCompanionTethers, Configuration.AlertOnCompanions, Configuration.TableShowCompanions, Configuration.InGameCompanionColor,
                         v => Configuration.ShowCompanions = v, v => Configuration.DrawCompanionTethers = v, v => Configuration.AlertOnCompanions = v, v => Configuration.TableShowCompanions = v, v => Configuration.InGameCompanionColor = v);
            DrawObjectRow("Retainers", "Player retainers and vendors", Configuration.ShowRetainers, Configuration.DrawRetainerTethers, Configuration.AlertOnRetainers, Configuration.TableShowRetainers, Configuration.InGameRetainerColor,
                         v => Configuration.ShowRetainers = v, v => Configuration.DrawRetainerTethers = v, v => Configuration.AlertOnRetainers = v, v => Configuration.TableShowRetainers = v, v => Configuration.InGameRetainerColor = v);
            DrawObjectRow("Housing", "Furniture and housing-related items", Configuration.ShowHousingObjects, Configuration.DrawHousingTethers, Configuration.AlertOnHousingObjects, Configuration.TableShowHousingObjects, Configuration.InGameHousingColor,
                         v => Configuration.ShowHousingObjects = v, v => Configuration.DrawHousingTethers = v, v => Configuration.AlertOnHousingObjects = v, v => Configuration.TableShowHousingObjects = v, v => Configuration.InGameHousingColor = v);
            DrawObjectRow("Area", "Zone-specific interactive objects", Configuration.ShowAreaObjects, Configuration.DrawAreaTethers, Configuration.AlertOnAreaObjects, Configuration.TableShowAreaObjects, Configuration.InGameAreaColor,
                         v => Configuration.ShowAreaObjects = v, v => Configuration.DrawAreaTethers = v, v => Configuration.AlertOnAreaObjects = v, v => Configuration.TableShowAreaObjects = v, v => Configuration.InGameAreaColor = v);
            DrawObjectRow("Cutscene", "Objects used in cutscenes and events", Configuration.ShowCutsceneObjects, Configuration.DrawCutsceneTethers, Configuration.AlertOnCutsceneObjects, Configuration.TableShowCutsceneObjects, Configuration.InGameCutsceneColor,
                         v => Configuration.ShowCutsceneObjects = v, v => Configuration.DrawCutsceneTethers = v, v => Configuration.AlertOnCutsceneObjects = v, v => Configuration.TableShowCutsceneObjects = v, v => Configuration.InGameCutsceneColor = v);
            DrawObjectRow("Card Stands", "Triple Triad card game objects", Configuration.ShowCardStands, Configuration.DrawCardStandTethers, Configuration.AlertOnCardStands, Configuration.TableShowCardStands, Configuration.InGameCardStandColor,
                         v => Configuration.ShowCardStands = v, v => Configuration.DrawCardStandTethers = v, v => Configuration.AlertOnCardStands = v, v => Configuration.TableShowCardStands = v, v => Configuration.InGameCardStandColor = v);
            DrawObjectRow("Ornaments", "Fashion accessories and decorative items", Configuration.ShowOrnaments, Configuration.DrawOrnamentTethers, Configuration.AlertOnOrnaments, Configuration.TableShowOrnaments, Configuration.InGameOrnamentColor,
                         v => Configuration.ShowOrnaments = v, v => Configuration.DrawOrnamentTethers = v, v => Configuration.AlertOnOrnaments = v, v => Configuration.TableShowOrnaments = v, v => Configuration.InGameOrnamentColor = v);
            DrawObjectRow("Island", "Crops, animals, buildings, and other island-specific items", Configuration.ShowIslandSanctuaryObjects, Configuration.DrawIslandSanctuaryTethers, Configuration.AlertOnIslandSanctuaryObjects, Configuration.TableShowIslandSanctuaryObjects, Configuration.InGameIslandSanctuaryColor,
                         v => Configuration.ShowIslandSanctuaryObjects = v, v => Configuration.DrawIslandSanctuaryTethers = v, v => Configuration.AlertOnIslandSanctuaryObjects = v, v => Configuration.TableShowIslandSanctuaryObjects = v, v => Configuration.InGameIslandSanctuaryColor = v);
            
            ImGui.EndTable();
        }
    }
    
    private void DrawObjectRow(string name, string tooltip, bool show, bool? tether, bool? alert, bool? table, Vector4 color, 
                              Action<bool> setShow, Action<bool>? setTether, Action<bool>? setAlert, Action<bool> setTable, Action<Vector4> setColor)
    {
        ImGui.TableNextRow();
        
        ImGui.TableSetColumnIndex(0);
        ImGui.TextUnformatted(name);
        ImGui.SameLine();
        ImGuiComponents.HelpMarker(tooltip);
        
        ImGui.TableSetColumnIndex(1);
        var showValue = show;
        if (ImGui.Checkbox($"##show_{name}", ref showValue))
        {
            setShow(showValue);
            Configuration.Save();
        }
        
        ImGui.TableSetColumnIndex(2);
        if (tether.HasValue && setTether != null)
        {
            var tetherValue = tether.Value;
            if (ImGui.Checkbox($"##tether_{name}", ref tetherValue))
            {
                setTether(tetherValue);
                Configuration.Save();
            }
        }
        else
        {
            ImGui.TextColored(new Vector4(0.5f, 0.5f, 0.5f, 1), "-");
        }
        
        ImGui.TableSetColumnIndex(3);
        if (alert.HasValue && setAlert != null)
        {
            var alertValue = alert.Value;
            if (ImGui.Checkbox($"##alert_{name}", ref alertValue))
            {
                setAlert(alertValue);
                Configuration.Save();
            }
        }
        else
        {
            ImGui.TextColored(new Vector4(0.5f, 0.5f, 0.5f, 1), "-");
        }
        
        ImGui.TableSetColumnIndex(4);
        if (table.HasValue && setTable != null)
        {
            var tableValue = table.Value;
            if (ImGui.Checkbox($"##table_{name}", ref tableValue))
            {
                setTable(tableValue);
                Configuration.Save();
            }
        }
        else
        {
            ImGui.TextColored(new Vector4(0.5f, 0.5f, 0.5f, 1), "-");
        }
        
        ImGui.TableSetColumnIndex(5);
        ImGui.SetNextItemWidth(-1); // Use full width
        var colorValue = color;
        if (ImGui.ColorEdit4($"##color_{name}", ref colorValue, ImGuiColorEditFlags.NoInputs | ImGuiColorEditFlags.NoLabel))
        {
            setColor(colorValue);
            Configuration.Save();
        }
    }
    
    private void DrawInGameOverlayTab()
    {
        ImGui.TextUnformatted("In-Game Drawing Options");
        ImGui.Separator();
        
        var enableInGameDrawing = Configuration.EnableInGameDrawing;
        if (ImGui.Checkbox("Enable In-Game Drawing", ref enableInGameDrawing))
        {
            Configuration.EnableInGameDrawing = enableInGameDrawing;
            Configuration.Save();
        }
        
        if (!Configuration.EnableInGameDrawing)
        {
            ImGui.TextColored(new Vector4(1, 0.5f, 0, 1), "In-game drawing is currently disabled.");
            return;
        }
        
        ImGui.Separator();
        ImGui.TextUnformatted("What to Draw");
        
        var drawPlayerCircle = Configuration.DrawPlayerCircle;
        if (ImGui.Checkbox("Draw Detection Radius Circle", ref drawPlayerCircle))
        {
            Configuration.DrawPlayerCircle = drawPlayerCircle;
            Configuration.Save();
        }
        
        var drawObjectDots = Configuration.DrawObjectDots;
        if (ImGui.Checkbox("Draw Object Dots", ref drawObjectDots))
        {
            Configuration.DrawObjectDots = drawObjectDots;
            Configuration.Save();
        }
        
        var drawDistanceText = Configuration.DrawDistanceText;
        if (ImGui.Checkbox("Draw Distance Text", ref drawDistanceText))
        {
            Configuration.DrawDistanceText = drawDistanceText;
            Configuration.Save();
        }
        
        ImGui.Separator();
        ImGui.TextUnformatted("Size Settings");
        
        var dotSize = Configuration.InGameDotSize;
        if (ImGui.SliderFloat("Dot Size", ref dotSize, 1.0f, 10.0f, "%.1f"))
        {
            Configuration.InGameDotSize = dotSize;
            Configuration.Save();
        }
        
        var lineThickness = Configuration.InGameLineThickness;
        if (ImGui.SliderFloat("Line Thickness", ref lineThickness, 0.5f, 5.0f, "%.1f"))
        {
            Configuration.InGameLineThickness = lineThickness;
            Configuration.Save();
        }
    }
    
    private void DrawAlertsTab()
    {
        ImGui.TextUnformatted("Player Proximity Alerts");
        ImGui.Separator();
        
        var enableAlerts = Configuration.EnablePlayerProximityAlert;
        if (ImGui.Checkbox("Enable Player Proximity Alerts", ref enableAlerts))
        {
            Configuration.EnablePlayerProximityAlert = enableAlerts;
            Configuration.Save();
        }
        ImGui.SameLine();
        ImGuiComponents.HelpMarker("Visual alerts when players enter radar range");
        
        if (Configuration.EnablePlayerProximityAlert)
        {
            ImGui.Indent(20);
            
            var alertDistance = Configuration.PlayerProximityAlertDistance;
            if (ImGui.SliderFloat("Alert Distance", ref alertDistance, 5f, Configuration.DetectionRadius, "%.1f yalms"))
            {
                Configuration.PlayerProximityAlertDistance = alertDistance;
                Configuration.Save();
            }
            ImGui.SameLine();
            ImGuiComponents.HelpMarker($"Alert distance relative to radar range (max: {Configuration.DetectionRadius:F0} yalms)");
            
            var enableSound = Configuration.EnableAlertSound;
            if (ImGui.Checkbox("Enable Alert Sound", ref enableSound))
            {
                Configuration.EnableAlertSound = enableSound;
                Configuration.Save();
            }
            ImGui.SameLine();
            ImGuiComponents.HelpMarker("Play a sound when alerts trigger");
            
            var showAlertRing = Configuration.ShowAlertRing;
            if (ImGui.Checkbox("Show Alert Ring on Radar", ref showAlertRing))
            {
                Configuration.ShowAlertRing = showAlertRing;
                Configuration.Save();
            }
            ImGui.SameLine();
            ImGuiComponents.HelpMarker("Displays a red ring on the radar at your alert distance");
            
            var cooldown = Configuration.PlayerProximityAlertCooldown;
            if (ImGui.SliderFloat("Alert Cooldown", ref cooldown, 1f, 30f, "%.1f seconds"))
            {
                Configuration.PlayerProximityAlertCooldown = cooldown;
                Configuration.Save();
            }
            ImGui.SameLine();
            ImGuiComponents.HelpMarker("How long to wait between alerts");
            
            if (Configuration.EnableAlertSound)
            {
                string[] soundOptions = { "Sound 1 (Ping)", "Sound 2 (Alert)", "Sound 3 (Notification)", "Sound 4 (Alarm)" };
                int currentSound = Configuration.PlayerProximityAlertSound;
                if (ImGui.Combo("Alert Sound", ref currentSound, soundOptions, soundOptions.Length))
                {
                    Configuration.PlayerProximityAlertSound = currentSound;
                    Configuration.Save();
                }
            }
            
            ImGui.Separator();
            ImGui.TextUnformatted("Alert Frequency");
            ImGui.Indent(20);
            
            int currentFrequency = (int)Configuration.PlayerAlertFrequency;
            bool changed = false;
            
            bool isOnlyOnce = currentFrequency == 0;
            if (ImGui.RadioButton("Only Once", isOnlyOnce))
            {
                currentFrequency = 0;
                changed = true;
            }
            ImGui.SameLine();
            ImGui.TextColored(new Vector4(0.7f, 0.7f, 0.7f, 1.0f), "Alert once per player until restart");
            
            bool isEveryInterval = currentFrequency == 1;
            if (ImGui.RadioButton("Every Interval", isEveryInterval))
            {
                currentFrequency = 1;
                changed = true;
            }
            ImGui.SameLine();
            ImGui.TextColored(new Vector4(0.7f, 0.7f, 0.7f, 1.0f), "Alert every cooldown period");
            
            bool isEnterLeaveReenter = currentFrequency == 2;
            if (ImGui.RadioButton("Enter/Leave/Reenter", isEnterLeaveReenter))
            {
                currentFrequency = 2;
                changed = true;
            }
            ImGui.SameLine();
            ImGui.TextColored(new Vector4(0.7f, 0.7f, 0.7f, 1.0f), "Alert on enter, then only after leaving and returning");
            
            if (changed)
            {
                Configuration.PlayerAlertFrequency = (Configuration.AlertFrequencyMode)currentFrequency;
                Configuration.Save();
                
                // Clear alert tracking data when frequency mode changes
                Plugin.ClearAlertData();
            }
            
            ImGui.Unindent(20);
            ImGui.Separator();
            
            if (Configuration.EnableAlertSound && ImGui.Button("Test Sound"))
            {
                Plugin.PlayAlertSound(Configuration.PlayerProximityAlertSound);
            }
            
            ImGui.Unindent(20);
        }
    }
    
    private void DrawColorsTab()
    {
        ImGui.TextUnformatted("Color Settings");
        ImGui.Separator();
        
        ImGui.TextUnformatted("Radar Window Colors");
        
        var radarRadiusColor = new Vector4(0.5f, 0.5f, 0.5f, 0.7f);
        if (ImGui.ColorEdit4("Radius Circle Color", ref radarRadiusColor))
        {
        }
        
        var radarPlayerLineColor = new Vector4(0.0f, 0.5f, 1.0f, 0.7f);
        if (ImGui.ColorEdit4("Player Line Color", ref radarPlayerLineColor))
        {
        }
        
        ImGui.Separator();
        ImGui.TextUnformatted("Note: Object colors are configured in the General tab's Object Configuration table.");
        
        ImGui.Separator();
        ImGui.TextUnformatted("Interface Colors");
        
        var radiusColor = Configuration.InGameRadiusColor;
        if (ImGui.ColorEdit4("Radius Circle Color", ref radiusColor))
        {
            Configuration.InGameRadiusColor = radiusColor;
            Configuration.Save();
        }
        
        var lineColor = Configuration.InGameLineColor;
        if (ImGui.ColorEdit4("Line Color", ref lineColor))
        {
            Configuration.InGameLineColor = lineColor;
            Configuration.Save();
        }
        
        var textColor = Configuration.InGameTextColor;
        if (ImGui.ColorEdit4("Text Color", ref textColor))
        {
            Configuration.InGameTextColor = textColor;
            Configuration.Save();
        }
    }
    
    private void DrawNavmeshTab()
    {
        ImGui.TextUnformatted("Navmesh Integration");
        ImGui.Separator();
        
        var enableNavmesh = Configuration.EnableNavmeshIntegration;
        if (ImGui.Checkbox("Enable Navmesh Integration", ref enableNavmesh))
        {
            Configuration.EnableNavmeshIntegration = enableNavmesh;
            Configuration.Save();
        }
        ImGui.SameLine();
        ImGuiComponents.HelpMarker("Allows clicking on objects in the radar to pathfind to them using vnavmesh");
        
        if (!Configuration.EnableNavmeshIntegration)
        {
            ImGui.TextColored(new Vector4(1, 0.5f, 0, 1), "Navmesh integration is currently disabled.");
            return;
        }
        
        // Check navmesh status
        bool navmeshReady = Plugin.NavmeshIPC.IsNavmeshReady();
        bool pathfindingInProgress = Plugin.NavmeshIPC.IsPathfindingInProgress();
        
        ImGui.Separator();
        ImGui.TextUnformatted("Navmesh Status");
        
        if (navmeshReady)
        {
            ImGui.TextColored(new Vector4(0, 1, 0, 1), "✓ Navmesh is ready");
        }
        else
        {
            ImGui.TextColored(new Vector4(1, 0, 0, 1), "✗ Navmesh is not available");
            ImGui.TextColored(new Vector4(0.7f, 0.7f, 0.7f, 1), "Make sure the vnavmesh plugin is installed and enabled");
        }
        
        if (pathfindingInProgress)
        {
            ImGui.TextColored(new Vector4(1, 1, 0, 1), "⚠ Pathfinding in progress...");
            if (ImGui.Button("Stop Pathfinding"))
            {
                Plugin.NavmeshIPC.StopPathfinding();
            }
        }
        
        ImGui.Separator();
        ImGui.TextUnformatted("Clickable Objects");
        ImGui.TextColored(new Vector4(0.7f, 0.7f, 0.7f, 1), "Select which object types can be clicked to pathfind to them:");
        
        // Create a table for better organization
        if (ImGui.BeginTable("ClickableObjectsTable", 2, ImGuiTableFlags.Resizable | ImGuiTableFlags.BordersOuter | ImGuiTableFlags.BordersV | ImGuiTableFlags.RowBg))
        {
            ImGui.TableSetupColumn("Object Type", ImGuiTableColumnFlags.WidthFixed, 150);
            ImGui.TableSetupColumn("Clickable", ImGuiTableColumnFlags.WidthStretch);
            ImGui.TableHeadersRow();
            
            DrawClickableObjectRow("NPCs", "Non-player characters and enemies", Configuration.ClickableNPCs, v => Configuration.ClickableNPCs = v);
            DrawClickableObjectRow("Treasure", "Treasure chests and valuable items", Configuration.ClickableTreasure, v => Configuration.ClickableTreasure = v);
            DrawClickableObjectRow("Gathering Points", "Mining nodes, botanist nodes, fishing spots", Configuration.ClickableGatheringPoints, v => Configuration.ClickableGatheringPoints = v);
            DrawClickableObjectRow("Aetherytes", "Teleportation crystals and aether currents", Configuration.ClickableAetherytes, v => Configuration.ClickableAetherytes = v);
            DrawClickableObjectRow("Event Objects", "Interactive objects like doors, switches, quest items", Configuration.ClickableEventObjects, v => Configuration.ClickableEventObjects = v);
            DrawClickableObjectRow("Mounts", "Player and NPC mounts", Configuration.ClickableMounts, v => Configuration.ClickableMounts = v);
            DrawClickableObjectRow("Companions", "Minions and pets", Configuration.ClickableCompanions, v => Configuration.ClickableCompanions = v);
            DrawClickableObjectRow("Retainers", "Player retainers and vendors", Configuration.ClickableRetainers, v => Configuration.ClickableRetainers = v);
            DrawClickableObjectRow("Housing Objects", "Furniture and housing-related items", Configuration.ClickableHousingObjects, v => Configuration.ClickableHousingObjects = v);
            DrawClickableObjectRow("Area Objects", "Zone-specific interactive objects", Configuration.ClickableAreaObjects, v => Configuration.ClickableAreaObjects = v);
            DrawClickableObjectRow("Cutscene Objects", "Objects used in cutscenes and events", Configuration.ClickableCutsceneObjects, v => Configuration.ClickableCutsceneObjects = v);
            DrawClickableObjectRow("Card Stands", "Triple Triad card game objects", Configuration.ClickableCardStands, v => Configuration.ClickableCardStands = v);
            DrawClickableObjectRow("Ornaments", "Fashion accessories and decorative items", Configuration.ClickableOrnaments, v => Configuration.ClickableOrnaments = v);
            DrawClickableObjectRow("Island Sanctuary", "Crops, animals, buildings, and other island-specific items", Configuration.ClickableIslandSanctuaryObjects, v => Configuration.ClickableIslandSanctuaryObjects = v);
            
            ImGui.EndTable();
        }
        
        ImGui.Separator();
        ImGui.TextColored(new Vector4(0.7f, 0.7f, 0.7f, 1), "Usage: Hover over objects on the radar to see if they're clickable, then left-click to pathfind to them.");
    }
    
    private void DrawClickableObjectRow(string name, string tooltip, bool clickable, Action<bool> setClickable)
    {
        ImGui.TableNextRow();
        
        ImGui.TableSetColumnIndex(0);
        ImGui.TextUnformatted(name);
        ImGui.SameLine();
        ImGuiComponents.HelpMarker(tooltip);
        
        ImGui.TableSetColumnIndex(1);
        var clickableValue = clickable;
        if (ImGui.Checkbox($"##clickable_{name}", ref clickableValue))
        {
            setClickable(clickableValue);
            Configuration.Save();
        }
    }
}
