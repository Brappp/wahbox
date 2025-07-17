using System;
using System.Numerics;
using Dalamud.Interface.Windowing;
using ImGuiNET;
using ZoomiesPlugin.Core;
using ZoomiesPlugin.Helpers;

namespace ZoomiesPlugin.UI
{
    public class MainWindow : Window, IDisposable
    {
        private readonly Plugin plugin;
        private readonly SpeedometerWindow speedometerWindow;
        private readonly NyanCatWindow nyanCatWindow;
        private readonly DebugWindow debugWindow;
        private readonly ConfigWindow configWindow;

        public MainWindow(Plugin pluginInstance,
                          SpeedometerWindow speedWindow,
                          NyanCatWindow nyanWindow,
                          DebugWindow debugWin,
                          ConfigWindow configWin) : base("Zoomies##MainWindow")
        {
            plugin = pluginInstance;
            speedometerWindow = speedWindow;
            nyanCatWindow = nyanWindow;
            debugWindow = debugWin;
            configWindow = configWin;

            Size = new Vector2(300, 80);
            SizeCondition = ImGuiCond.FirstUseEver;

            // Apply saved settings
            var config = Plugin.PluginInterface.GetPluginConfig() as Configuration ?? new Configuration();
            if (config.ShowSpeedometerOnStartup)
            {
                ShowSpeedometer(config.SelectedSpeedometerType);
            }
        }

        public override void Draw()
        {
            ImGui.Text("Zoomies Speedometer Controls");
            ImGui.Separator();

            if (ImGui.Button("Toggle Speedometer"))
            {
                ToggleSpeedometer();
            }

            ImGui.SameLine();

            if (ImGui.Button("Configure"))
            {
                configWindow.IsOpen = true;
            }

            ImGui.SameLine();

            if (ImGui.Button("Debug"))
            {
                debugWindow.IsOpen = !debugWindow.IsOpen;
            }
        }

        public void ShowSpeedometer(int type)
        {
            // Hide all speedometers first
            speedometerWindow.IsOpen = false;
            nyanCatWindow.IsOpen = false;

            // Show selected one
            switch (type)
            {
                case 0:
                    speedometerWindow.IsOpen = true;
                    break;
                case 1:
                    nyanCatWindow.IsOpen = true;
                    break;
            }
        }

        public void ToggleSpeedometer()
        {
            var config = Plugin.PluginInterface.GetPluginConfig() as Configuration;
            if (config != null)
            {
                bool isAnyVisible = speedometerWindow.IsOpen || nyanCatWindow.IsOpen;

                if (isAnyVisible)
                {
                    // Hide all speedometers
                    speedometerWindow.IsOpen = false;
                    nyanCatWindow.IsOpen = false;
                }
                else
                {
                    // Show the appropriate speedometer
                    ShowSpeedometer(config.SelectedSpeedometerType);
                }

                // Remember user preference
                config.ShowSpeedometerOnStartup = !isAnyVisible;
                config.Save();
            }
        }

        public void Dispose()
        {
            // Nothing to dispose
        }
    }
}
