using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Dalamud.Interface.Windowing;
using Dalamud.Interface.Components;
using Dalamud.Interface;
using ImGuiNET;

namespace Wahdar.Windows
{
    public class ObjectListWindow : Window, IDisposable
    {
        private Plugin Plugin { get; }
        
        public ObjectListWindow(Plugin plugin) : base("Object List##WahdarObjectList")
        {
            Plugin = plugin;
            
            SizeConstraints = new WindowSizeConstraints
            {
                MinimumSize = new Vector2(400, 200),
                MaximumSize = new Vector2(800, 600)
            };
            
            Flags = ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse;
        }

        public void Dispose() { }
        
        public override void PreDraw()
        {
            // Handle window locking
            if (Plugin.Configuration.LockObjectListWindow)
            {
                Flags |= ImGuiWindowFlags.NoMove;
                ImGui.SetNextWindowPos(Plugin.Configuration.ObjectListWindowLockedPosition, ImGuiCond.Always);
            }
            else
            {
                Flags &= ~ImGuiWindowFlags.NoMove;
            }

            // Set dark background
            ImGui.PushStyleColor(ImGuiCol.WindowBg, new Vector4(0.1f, 0.1f, 0.1f, 0.95f));
            ImGui.PushStyleColor(ImGuiCol.ChildBg, new Vector4(0.05f, 0.05f, 0.05f, 0.95f));
            ImGui.PushStyleColor(ImGuiCol.TableHeaderBg, new Vector4(0.2f, 0.2f, 0.2f, 1.0f));
            ImGui.PushStyleColor(ImGuiCol.TableRowBg, new Vector4(0.15f, 0.15f, 0.15f, 0.5f));
            ImGui.PushStyleColor(ImGuiCol.TableRowBgAlt, new Vector4(0.2f, 0.2f, 0.2f, 0.5f));
        }
        
        public override void PostDraw()
        {
            ImGui.PopStyleColor(5);
        }
        
        public override void Draw()
        {
            // Save current position when not locked (for future locking)
            if (!Plugin.Configuration.LockObjectListWindow)
            {
                Plugin.Configuration.ObjectListWindowLockedPosition = ImGui.GetWindowPos();
            }

            var player = Plugin.ClientState.LocalPlayer;
            if (player == null)
            {
                ImGui.TextColored(new Vector4(0.7f, 0.7f, 0.7f, 1), "Player not available");
                return;
            }
            
            var allTrackedObjects = Plugin.ObjectTracker.GetTrackedObjects();
            var filteredObjects = FilterObjectsForTable(allTrackedObjects);
            
            // Header with object count and detection radius
            ImGui.TextColored(new Vector4(0.9f, 0.9f, 0.9f, 1), "Detected Objects");
            ImGui.SameLine();
            ImGui.TextColored(new Vector4(0.6f, 0.6f, 0.6f, 1), $"({filteredObjects.Count}/{allTrackedObjects.Count} shown, {Plugin.Configuration.DetectionRadius:F0}y range)");
            
            // Add lock button on the left side
            ImGui.SameLine();
            float lockWindowWidth = ImGui.GetWindowWidth();
            float lockCurrentPos = ImGui.GetCursorPosX();
            
            var lockIcon = Plugin.Configuration.LockObjectListWindow ? FontAwesomeIcon.Lock : FontAwesomeIcon.LockOpen;
            var lockSize = new Vector2(20, 20); // Fixed size for icon button
            float lockWidth = lockSize.X + ImGui.GetStyle().FramePadding.X * 2;
            
            // Position lock button before the Stop vNav button
            float lockPos = lockWindowWidth - lockWidth - 110f; // 110f to account for Stop vNav button + padding
            
            if (lockPos > lockCurrentPos)
            {
                ImGui.SetCursorPosX(lockPos);
            }
            
            if (Plugin.Configuration.LockObjectListWindow)
            {
                ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(0.8f, 0.2f, 0.2f, 1.0f));
                ImGui.PushStyleColor(ImGuiCol.ButtonHovered, new Vector4(1.0f, 0.3f, 0.3f, 1.0f));
                ImGui.PushStyleColor(ImGuiCol.ButtonActive, new Vector4(0.6f, 0.1f, 0.1f, 1.0f));
            }
            else
            {
                ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(0.2f, 0.6f, 0.2f, 1.0f));
                ImGui.PushStyleColor(ImGuiCol.ButtonHovered, new Vector4(0.3f, 0.8f, 0.3f, 1.0f));
                ImGui.PushStyleColor(ImGuiCol.ButtonActive, new Vector4(0.1f, 0.4f, 0.1f, 1.0f));
            }
            
            if (ImGuiComponents.IconButton(lockIcon))
            {
                Plugin.Configuration.LockObjectListWindow = !Plugin.Configuration.LockObjectListWindow;
                Plugin.Configuration.Save();
            }
            
            ImGui.PopStyleColor(3);
            
            if (ImGui.IsItemHovered())
            {
                ImGui.SetTooltip(Plugin.Configuration.LockObjectListWindow ? "Unlock object list window position" : "Lock object list window position");
            }
            
            // Add dedicated Stop vNav button on the right side
            if (Plugin.Configuration.EnableNavmeshIntegration && Plugin.NavmeshIPC.IsNavmeshReady())
            {
                ImGui.SameLine();
                
                // Push to right side of window
                float windowWidth = ImGui.GetWindowWidth();
                float buttonWidth = 80f;
                float currentPos = ImGui.GetCursorPosX();
                float rightPos = windowWidth - buttonWidth - 20f; // 20f for padding
                
                if (rightPos > currentPos)
                {
                    ImGui.SetCursorPosX(rightPos);
                }
                
                bool pathfindingInProgress = Plugin.NavmeshIPC.IsPathfindingInProgress();
                
                // Always show the button, but change color based on pathfinding status
                if (pathfindingInProgress)
                {
                    // Red button when pathfinding is active
                    ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(0.8f, 0.2f, 0.2f, 1.0f));
                    ImGui.PushStyleColor(ImGuiCol.ButtonHovered, new Vector4(1.0f, 0.3f, 0.3f, 1.0f));
                    ImGui.PushStyleColor(ImGuiCol.ButtonActive, new Vector4(0.6f, 0.1f, 0.1f, 1.0f));
                }
                else
                {
                    // Orange/yellow button when not pathfinding (but still functional)
                    ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(0.8f, 0.6f, 0.2f, 1.0f));
                    ImGui.PushStyleColor(ImGuiCol.ButtonHovered, new Vector4(1.0f, 0.7f, 0.3f, 1.0f));
                    ImGui.PushStyleColor(ImGuiCol.ButtonActive, new Vector4(0.6f, 0.4f, 0.1f, 1.0f));
                }
                
                if (ImGui.Button("Stop vNav"))
                {
                    Plugin.NavmeshIPC.StopPathfinding();
                    Plugin.StopPathfindingTracking();
                    Plugin.Log.Information("Stopped pathfinding via header button");
                }
                
                ImGui.PopStyleColor(3);
                
                if (ImGui.IsItemHovered())
                {
                    if (pathfindingInProgress)
                    {
                        ImGui.SetTooltip("Stop current pathfinding");
                    }
                    else
                    {
                        ImGui.SetTooltip("Stop any pathfinding (currently none active)");
                    }
                }
            }
            
            if (filteredObjects.Count == 0)
            {
                ImGui.Separator();
                ImGui.TextColored(new Vector4(0.7f, 0.7f, 0.7f, 1), "No objects to display in table");
                ImGui.TextColored(new Vector4(0.5f, 0.5f, 0.5f, 1), "• Check object filters in General tab");
                ImGui.TextColored(new Vector4(0.5f, 0.5f, 0.5f, 1), "• Increase detection radius if needed");
                ImGui.TextColored(new Vector4(0.5f, 0.5f, 0.5f, 1), "• Enable table visibility for object types");
                return;
            }
            
            ImGui.Separator();
            
            // Create scrollable table
            if (ImGui.BeginChild("ObjectTableChild", new Vector2(0, 0), false, ImGuiWindowFlags.HorizontalScrollbar))
            {
                if (ImGui.BeginTable("ObjectTable", 4, ImGuiTableFlags.Resizable | ImGuiTableFlags.BordersOuter | ImGuiTableFlags.BordersV | ImGuiTableFlags.RowBg | ImGuiTableFlags.ScrollY | ImGuiTableFlags.Sortable))
                {
                    // Setup columns
                    ImGui.TableSetupColumn("Name", ImGuiTableColumnFlags.WidthStretch | ImGuiTableColumnFlags.DefaultSort, 0.0f, 0);
                    ImGui.TableSetupColumn("Type", ImGuiTableColumnFlags.WidthFixed, 80, 1);
                    ImGui.TableSetupColumn("Distance", ImGuiTableColumnFlags.WidthFixed, 70, 2);
                    ImGui.TableSetupColumn("Action", ImGuiTableColumnFlags.WidthFixed, 120, 3);
                    ImGui.TableHeadersRow();
                    
                    // Handle sorting
                    var sortSpecs = ImGui.TableGetSortSpecs();
                    var sortedObjects = filteredObjects.AsEnumerable();
                    
                    if (sortSpecs.SpecsCount > 0)
                    {
                        var spec = sortSpecs.Specs;
                        switch (spec.ColumnIndex)
                        {
                            case 0: // Name
                                sortedObjects = spec.SortDirection == ImGuiSortDirection.Ascending 
                                    ? sortedObjects.OrderBy(obj => obj.Name)
                                    : sortedObjects.OrderByDescending(obj => obj.Name);
                                break;
                            case 1: // Type
                                sortedObjects = spec.SortDirection == ImGuiSortDirection.Ascending 
                                    ? sortedObjects.OrderBy(obj => obj.Category.ToString())
                                    : sortedObjects.OrderByDescending(obj => obj.Category.ToString());
                                break;
                            case 2: // Distance
                                sortedObjects = spec.SortDirection == ImGuiSortDirection.Ascending 
                                    ? sortedObjects.OrderBy(obj => obj.Distance)
                                    : sortedObjects.OrderByDescending(obj => obj.Distance);
                                break;
                        }
                    }
                    else
                    {
                        // Default sort by distance
                        sortedObjects = sortedObjects.OrderBy(obj => obj.Distance);
                    }
                    
                    foreach (var obj in sortedObjects)
                    {
                        ImGui.TableNextRow();
                        
                        // Object name with color indicator
                        ImGui.TableSetColumnIndex(0);
                        var color = GetObjectColor(obj.Category);
                        ImGui.TextColored(color, "●");
                        ImGui.SameLine();
                        ImGui.TextUnformatted(obj.Name);
                        
                        // Object type
                        ImGui.TableSetColumnIndex(1);
                        string typeText = GetObjectTypeText(obj.Category);
                        ImGui.TextUnformatted(typeText);
                        
                        // Distance
                        ImGui.TableSetColumnIndex(2);
                        ImGui.TextUnformatted($"{obj.Distance:F1}y");
                        
                        // Action button
                        ImGui.TableSetColumnIndex(3);
                        DrawActionButton(obj);
                    }
                    
                    ImGui.EndTable();
                }
                ImGui.EndChild();
            }
        }
        
        private List<TrackedObject> FilterObjectsForTable(List<TrackedObject> allObjects)
        {
            return allObjects.Where(obj => 
                ShouldShowInTable(obj.Category) && 
                (!Plugin.Configuration.HideUnnamedObjects || !string.IsNullOrWhiteSpace(obj.Name))
            ).ToList();
        }
        
        private bool ShouldShowInTable(ObjectCategory category)
        {
            return category switch
            {
                ObjectCategory.Player => Plugin.Configuration.TableShowPlayers,
                ObjectCategory.NPC or ObjectCategory.FriendlyNPC => Plugin.Configuration.TableShowNPCs,
                ObjectCategory.Treasure => Plugin.Configuration.TableShowTreasure,
                ObjectCategory.GatheringPoint => Plugin.Configuration.TableShowGatheringPoints,
                ObjectCategory.Aetheryte => Plugin.Configuration.TableShowAetherytes,
                ObjectCategory.EventObject => Plugin.Configuration.TableShowEventObjects,
                ObjectCategory.Mount => Plugin.Configuration.TableShowMounts,
                ObjectCategory.Companion => Plugin.Configuration.TableShowCompanions,
                ObjectCategory.Retainer => Plugin.Configuration.TableShowRetainers,
                ObjectCategory.HousingObject => Plugin.Configuration.TableShowHousingObjects,
                ObjectCategory.AreaObject => Plugin.Configuration.TableShowAreaObjects,
                ObjectCategory.CutsceneObject => Plugin.Configuration.TableShowCutsceneObjects,
                ObjectCategory.CardStand => Plugin.Configuration.TableShowCardStands,
                ObjectCategory.Ornament => Plugin.Configuration.TableShowOrnaments,
                ObjectCategory.IslandSanctuaryObject => Plugin.Configuration.TableShowIslandSanctuaryObjects,
                _ => false
            };
        }
        
        private string GetObjectTypeText(ObjectCategory category)
        {
            return category switch
            {
                ObjectCategory.Player => "Player",
                ObjectCategory.NPC or ObjectCategory.FriendlyNPC => "NPC",
                ObjectCategory.Treasure => "Treasure",
                ObjectCategory.GatheringPoint => "Gathering",
                ObjectCategory.Aetheryte => "Aetheryte",
                ObjectCategory.EventObject => "Event",
                ObjectCategory.Mount => "Mount",
                ObjectCategory.Companion => "Companion",
                ObjectCategory.Retainer => "Retainer",
                ObjectCategory.HousingObject => "Housing",
                ObjectCategory.AreaObject => "Area",
                ObjectCategory.CutsceneObject => "Cutscene",
                ObjectCategory.CardStand => "Card",
                ObjectCategory.Ornament => "Ornament",
                ObjectCategory.IslandSanctuaryObject => "Island",
                _ => "Unknown"
            };
        }
        
        private Vector4 GetObjectColor(ObjectCategory category)
        {
            return category switch
            {
                ObjectCategory.Player => new Vector4(0, 0, 1, 1),
                ObjectCategory.NPC or ObjectCategory.FriendlyNPC => new Vector4(1, 1, 0, 1),
                ObjectCategory.Treasure => new Vector4(1, 0.8f, 0, 1),        // Gold
                ObjectCategory.GatheringPoint => new Vector4(0, 1, 0, 1),     // Green
                ObjectCategory.Aetheryte => new Vector4(0.5f, 0.5f, 1, 1),   // Light Blue
                ObjectCategory.EventObject => new Vector4(1, 0.5f, 0, 1),    // Orange
                ObjectCategory.Mount => new Vector4(0.8f, 0.4f, 0.2f, 1),    // Brown
                ObjectCategory.Companion => new Vector4(1, 0.7f, 1, 1),      // Pink
                ObjectCategory.Retainer => new Vector4(0.7f, 0.7f, 0.7f, 1), // Gray
                ObjectCategory.HousingObject => new Vector4(0.6f, 0.3f, 0.1f, 1), // Dark Brown
                ObjectCategory.AreaObject => new Vector4(0.5f, 0.8f, 0.5f, 1), // Light Green
                ObjectCategory.CutsceneObject => new Vector4(1, 0, 1, 1),     // Magenta
                ObjectCategory.CardStand => new Vector4(0.9f, 0.9f, 0.1f, 1), // Bright Yellow
                ObjectCategory.Ornament => new Vector4(0.8f, 0.2f, 0.8f, 1), // Purple
                ObjectCategory.IslandSanctuaryObject => new Vector4(0.2f, 0.8f, 0.6f, 1), // Teal
                _ => new Vector4(0.7f, 0.7f, 0.7f, 1)
            };
        }
        
        private void DrawActionButton(TrackedObject obj)
        {
            // In the object list table, ALL objects should be navigable
            bool navmeshEnabled = Plugin.Configuration.EnableNavmeshIntegration;
            bool navmeshReady = Plugin.NavmeshIPC.IsNavmeshReady();
            bool pathfindingInProgress = Plugin.NavmeshIPC.IsPathfindingInProgress();
            
            if (!navmeshEnabled)
            {
                ImGui.TextColored(new Vector4(0.7f, 0.3f, 0.3f, 1), "Nav Off");
                if (ImGui.IsItemHovered())
                {
                    ImGui.SetTooltip("Navmesh integration is disabled in settings");
                }
                return;
            }
            
            if (!navmeshReady)
            {
                ImGui.BeginDisabled();
                ImGui.Button("N/A");
                ImGui.EndDisabled();
                if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
                {
                    ImGui.SetTooltip("vNavmesh plugin not available or not ready");
                }
                return;
            }
            
            if (pathfindingInProgress)
            {
                // When pathfinding is active, show Stop button
                ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(0.8f, 0.2f, 0.2f, 1.0f));
                ImGui.PushStyleColor(ImGuiCol.ButtonHovered, new Vector4(1.0f, 0.3f, 0.3f, 1.0f));
                ImGui.PushStyleColor(ImGuiCol.ButtonActive, new Vector4(0.6f, 0.1f, 0.1f, 1.0f));
                
                if (ImGui.Button("Stop##" + obj.ObjectId))
                {
                    Plugin.NavmeshIPC.StopPathfinding();
                    Plugin.StopPathfindingTracking();
                    Plugin.Log.Information($"Stopped pathfinding via object list button");
                }
                
                ImGui.PopStyleColor(3);
                
                if (ImGui.IsItemHovered())
                {
                    ImGui.SetTooltip("Stop current pathfinding");
                }
            }
            else
            {
                // Normal vNav button
                ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(0.2f, 0.6f, 0.2f, 1.0f));
                ImGui.PushStyleColor(ImGuiCol.ButtonHovered, new Vector4(0.3f, 0.8f, 0.3f, 1.0f));
                ImGui.PushStyleColor(ImGuiCol.ButtonActive, new Vector4(0.1f, 0.4f, 0.1f, 1.0f));
                
                if (ImGui.Button("vNav##" + obj.ObjectId))
                {
                    HandleObjectClick(obj);
                }
                
                ImGui.PopStyleColor(3);
                
                if (ImGui.IsItemHovered())
                {
                    ImGui.SetTooltip("Click to pathfind to this object using vnavmesh");
                }
            }
        }
        
        private void HandleObjectClick(TrackedObject obj)
        {
            try
            {
                if (!Plugin.NavmeshIPC.IsNavmeshReady())
                {
                    Plugin.Log.Warning("Cannot pathfind: Navmesh is not ready");
                    return;
                }
                
                if (Plugin.NavmeshIPC.IsPathfindingInProgress())
                {
                    Plugin.Log.Information("Pathfinding already in progress, stopping current path");
                    Plugin.NavmeshIPC.StopPathfinding();
                    Plugin.StopPathfindingTracking();
                }
                
                // Attempt to pathfind to the object
                bool success = Plugin.NavmeshIPC.PathfindAndMoveTo(obj.Position, false);
                
                if (success)
                {
                    Plugin.Log.Information($"Started pathfinding to {obj.Name} at {obj.Position}");
                    Plugin.StartPathfindingTo(obj.Position, obj.Name);
                }
                else
                {
                    Plugin.Log.Warning($"Failed to start pathfinding to {obj.Name}");
                }
            }
            catch (Exception ex)
            {
                Plugin.Log.Error($"Error handling object click: {ex.Message}");
            }
        }
    }
} 