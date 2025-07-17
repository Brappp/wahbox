using System;
using System.Numerics;
using Dalamud.Interface.Windowing;
using ImGuiNET;
using ZoomiesPlugin.Core;
using ZoomiesPlugin.Helpers;
using ZoomiesPlugin.Renderers;

namespace ZoomiesPlugin.UI
{
    public class SpeedometerWindow : Window, IDisposable
    {
        private readonly YalmsCalculator yalmsCalculator;
        private readonly ClassicRenderer classicRenderer;

        public SpeedometerWindow() : base("Zoomies##SpeedometerWindow",
            ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse |
            ImGuiWindowFlags.NoBackground | ImGuiWindowFlags.NoTitleBar |
            ImGuiWindowFlags.NoResize)
        {
            Size = new Vector2(350, 350);
            SizeCondition = ImGuiCond.FirstUseEver;

            // Prevent ESC from closing this window
            RespectCloseHotkey = false;

            var config = Plugin.PluginInterface.GetPluginConfig() as Configuration ?? new Configuration();

            yalmsCalculator = new YalmsCalculator();
            classicRenderer = new ClassicRenderer();

            // Apply saved settings
            yalmsCalculator.SetDamping(config.NeedleDamping);
            classicRenderer.SetMaxYalms(config.MaxYalms);
            classicRenderer.SetRedlineStart(config.RedlineStart);
        }

        public override void Draw()
        {
            var localPlayer = Plugin.ClientState.LocalPlayer;
            if (localPlayer != null)
            {
                yalmsCalculator.Update(localPlayer.Position);
            }
            else
            {
                yalmsCalculator.Reset();
            }

            classicRenderer.Render(yalmsCalculator.GetDisplayYalms());
        }

        public void Toggle()
        {
            this.IsOpen = !this.IsOpen;
        }

        public void Dispose()
        {
            // No resources to clean up
        }

        public YalmsCalculator GetCalculator()
        {
            return yalmsCalculator;
        }

        public ClassicRenderer GetRenderer()
        {
            return classicRenderer;
        }

        public void UpdateDamping(float damping)
        {
            yalmsCalculator.SetDamping(damping);
        }

        public void UpdateMaxSpeed(float maxSpeed)
        {
            classicRenderer.SetMaxYalms(maxSpeed);
        }

        public void UpdateRedlineStart(float redlineStart)
        {
            classicRenderer.SetRedlineStart(redlineStart);
        }
    }
}
