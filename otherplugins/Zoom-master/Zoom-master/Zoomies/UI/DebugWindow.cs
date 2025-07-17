using System;
using System.Numerics;
using System.Collections.Generic;
using Dalamud.Interface.Windowing;
using ImGuiNET;
using ZoomiesPlugin.Core;
using ZoomiesPlugin.Helpers;

namespace ZoomiesPlugin.UI
{
    public class DebugWindow : Window, IDisposable
    {
        // Speed calculation data
        private Vector3 currentPosition;
        private Vector3 previousPosition;
        private DateTime currentTime;
        private DateTime previousTime;
        private float distanceTraveled;
        private float deltaTime;
        private float currentSpeed;
        private float displaySpeed;

        private bool showHistoryTable = false;
        private bool isPaused = false;
        private DateTime lastUpdateTime = DateTime.Now;
        private float updateFrequency = 0.25f; // Update every 250ms

        // Store past calculations for analysis
        private readonly List<(DateTime time, float distance, float deltaTime, float speed)> calculationHistory;
        private const int MaxHistoryEntries = 20;

        private readonly YalmsCalculator yalmsCalculator;
        private readonly Plugin plugin;

        public DebugWindow(YalmsCalculator calculator, Plugin pluginInstance) : base("Zoomies Speed Calculation##DebugWindow",
            ImGuiWindowFlags.AlwaysAutoResize)
        {
            yalmsCalculator = calculator;
            plugin = pluginInstance;
            calculationHistory = new List<(DateTime, float, float, float)>();

            // Initialize with zeros
            currentPosition = Vector3.Zero;
            previousPosition = Vector3.Zero;
            currentTime = DateTime.Now;
            previousTime = DateTime.Now;
            distanceTraveled = 0;
            deltaTime = 0;
            currentSpeed = 0;
            displaySpeed = 0;

            // Load user preferences
            var config = Plugin.PluginInterface.GetPluginConfig() as Configuration;
            if (config != null)
            {
                showHistoryTable = config.ShowHistoryTable;
            }
        }

        public override void Draw()
        {
            var localPlayer = Plugin.ClientState.LocalPlayer;
            if (localPlayer != null)
            {
                // Calculate raw speed for debug display
                if (!isPaused)
                {
                    Vector3 newPosition = localPlayer.Position;
                    DateTime newTime = DateTime.Now;

                    if ((newTime - lastUpdateTime).TotalSeconds >= updateFrequency)
                    {
                        previousPosition = currentPosition;
                        previousTime = currentTime;
                        currentPosition = newPosition;
                        currentTime = newTime;

                        if (previousPosition != Vector3.Zero)
                        {
                            // Only measure horizontal movement (X/Z)
                            Vector2 horizontalDelta = new Vector2(
                                currentPosition.X - previousPosition.X,
                                currentPosition.Z - previousPosition.Z
                            );

                            distanceTraveled = horizontalDelta.Length();
                            deltaTime = (float)(currentTime - previousTime).TotalSeconds;

                            if (deltaTime > 0.01f)
                            {
                                currentSpeed = distanceTraveled / deltaTime;

                                // Record significant movement for history
                                if (distanceTraveled > 0.001f)
                                {
                                    calculationHistory.Insert(0, (currentTime, distanceTraveled, deltaTime, currentSpeed));

                                    if (calculationHistory.Count > MaxHistoryEntries)
                                        calculationHistory.RemoveAt(calculationHistory.Count - 1);
                                }
                            }
                        }

                        lastUpdateTime = newTime;
                    }
                }

                // Always get displayed speed from calculator
                try
                {
                    if (yalmsCalculator != null)
                    {
                        displaySpeed = yalmsCalculator.GetDisplayYalms();
                    }
                    else
                    {
                        ImGui.TextColored(new Vector4(1.0f, 0.2f, 0.2f, 1.0f), "Calculator reference is null!");
                    }
                }
                catch (Exception ex)
                {
                    ImGui.TextColored(new Vector4(1.0f, 0.2f, 0.2f, 1.0f), $"Error reading calculator: {ex.Message}");
                }

                // UI Controls
                if (ImGui.Button(isPaused ? "Resume Updates" : "Pause Updates"))
                {
                    isPaused = !isPaused;

                    // Force immediate update when resuming
                    if (!isPaused)
                    {
                        lastUpdateTime = DateTime.MinValue;
                    }
                }

                ImGui.SameLine();

                ImGui.SetNextItemWidth(150);
                ImGui.SliderFloat("Update Rate", ref updateFrequency, 0.1f, 1.0f, "%.1f sec");

                ImGui.SameLine();

                if (ImGui.Button(showHistoryTable ? "Hide History" : "Show History"))
                {
                    showHistoryTable = !showHistoryTable;
                    SaveViewSettings();
                }

                ImGui.Separator();

                DrawDetailedView();

                if (showHistoryTable)
                {
                    DrawHistoryTable();
                }
            }
            else
            {
                ImGui.Text("Player not available");
            }
        }

        private void DrawDetailedView()
        {
            // Speed displays
            ImGui.TextColored(new Vector4(1.0f, 1.0f, 0.2f, 1.0f), "Displayed Speed:");
            ImGui.SameLine();
            ImGui.TextColored(new Vector4(1.0f, 0.5f, 0.5f, 1.0f), $"{displaySpeed:F2} yalms/second");

            ImGui.TextColored(new Vector4(1.0f, 1.0f, 0.2f, 1.0f), "Raw Speed:");
            ImGui.SameLine();
            ImGui.TextColored(new Vector4(0.5f, 1.0f, 0.5f, 1.0f), $"{currentSpeed:F2} yalms/second");

            ImGui.Text("Formula: Speed = Distance ÷ Time");
            ImGui.Text($"Distance: {distanceTraveled:F3} yalms | Time: {deltaTime:F3} seconds");
            ImGui.Text($"Calculation: {distanceTraveled:F3} ÷ {deltaTime:F3} = {currentSpeed:F3}");

            ImGui.Spacing();
            ImGui.Separator();
            ImGui.Spacing();

            // Technical details
            ImGui.TextColored(new Vector4(0.5f, 0.5f, 1.0f, 1.0f), "Technical Details:");

            ImGui.Text($"Current Position: X:{currentPosition.X:F2} Y:{currentPosition.Y:F2} Z:{currentPosition.Z:F2}");
            ImGui.Text($"Previous Position: X:{previousPosition.X:F2} Y:{previousPosition.Y:F2} Z:{previousPosition.Z:F2}");

            Vector2 horizontalDelta = new Vector2(
                currentPosition.X - previousPosition.X,
                currentPosition.Z - previousPosition.Z
            );

            ImGui.Text($"X Distance: {horizontalDelta.X:F3}");
            ImGui.Text($"Z Distance: {horizontalDelta.Y:F3}");
            ImGui.Text($"Horizontal Distance: √({horizontalDelta.X:F3}² + {horizontalDelta.Y:F3}²) = {horizontalDelta.Length():F3}");

            ImGui.Spacing();
            ImGui.Text($"Update Frequency: {updateFrequency:F2} seconds");
            ImGui.Text($"Time Since Last Update: {(DateTime.Now - lastUpdateTime).TotalSeconds:F2} seconds");
            ImGui.Text($"Damping Value: {GetDampingValue():F2}");

            ImGui.TextColored(new Vector4(1.0f, 0.5f, 0.5f, 1.0f), $"Calculator instance: {(yalmsCalculator == null ? "NULL" : "Valid")}");

            ImGui.Spacing();
            ImGui.TextColored(new Vector4(1.0f, 0.8f, 0.2f, 1.0f), "Note: Only horizontal movement (X/Z) is measured (no Y-axis/up-down)");
        }

        private void DrawHistoryTable()
        {
            ImGui.Spacing();
            ImGui.TextColored(new Vector4(1.0f, 0.8f, 0.2f, 1.0f), "Recent Measurements:");

            if (ImGui.BeginTable("history_table", 4, ImGuiTableFlags.Borders))
            {
                ImGui.TableSetupColumn("Time");
                ImGui.TableSetupColumn("Distance (yalms)");
                ImGui.TableSetupColumn("Time (sec)");
                ImGui.TableSetupColumn("Speed (yalms/s)");
                ImGui.TableHeadersRow();

                foreach (var entry in calculationHistory)
                {
                    ImGui.TableNextRow();

                    ImGui.TableNextColumn();
                    ImGui.Text(entry.time.ToString("HH:mm:ss.fff"));

                    ImGui.TableNextColumn();
                    ImGui.Text($"{entry.distance:F3}");

                    ImGui.TableNextColumn();
                    ImGui.Text($"{entry.deltaTime:F3}");

                    ImGui.TableNextColumn();
                    ImGui.Text($"{entry.speed:F3}");
                }

                ImGui.EndTable();
            }

            if (ImGui.Button("Clear History"))
            {
                calculationHistory.Clear();
            }
        }

        private float GetDampingValue()
        {
            var config = Plugin.PluginInterface.GetPluginConfig() as Configuration;
            return config?.NeedleDamping ?? 0.1f;
        }

        private void SaveViewSettings()
        {
            var config = Plugin.PluginInterface.GetPluginConfig() as Configuration;
            if (config != null)
            {
                config.ShowHistoryTable = showHistoryTable;
                config.Save();
            }
        }

        public void Dispose()
        {
            // No resources to clean up
        }
    }
}
