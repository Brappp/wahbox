using System;
using System.Numerics;
using Dalamud.Interface.Windowing;
using ImGuiNET;
using ZoomiesPlugin.Core;
using ZoomiesPlugin.Helpers;
using ZoomiesPlugin.Renderers;

namespace ZoomiesPlugin.UI
{
    public class NyanCatWindow : Window, IDisposable
    {
        private readonly YalmsCalculator yalmsCalculator;
        private readonly NyanCatRenderer nyanRenderer;

        public NyanCatWindow() : base("NyanCat##NyanCatWindow",
            ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse |
            ImGuiWindowFlags.NoBackground | ImGuiWindowFlags.NoTitleBar |
            ImGuiWindowFlags.NoResize)
        {
            Size = new Vector2(450, 150);
            SizeCondition = ImGuiCond.FirstUseEver;

            // Prevent ESC from closing this window
            RespectCloseHotkey = false;

            var config = Plugin.PluginInterface.GetPluginConfig() as Configuration ?? new Configuration();

            yalmsCalculator = new YalmsCalculator();
            nyanRenderer = new NyanCatRenderer();

            // Apply saved settings
            yalmsCalculator.SetDamping(config.NeedleDamping);
            nyanRenderer.SetMaxYalms(config.MaxYalms);
        }

        public override void Draw()
        {
            // Update speed calculation
            var localPlayer = Plugin.ClientState.LocalPlayer;
            if (localPlayer != null)
            {
                yalmsCalculator.Update(localPlayer.Position);
            }
            else
            {
                yalmsCalculator.Reset();
            }

            // Draw the speedometer
            nyanRenderer.Render(yalmsCalculator.GetDisplayYalms());
        }

        public void Toggle()
        {
            this.IsOpen = !this.IsOpen;
        }

        public void Dispose()
        {
            // Clean up texture resources
            if (nyanRenderer != null)
            {
                nyanRenderer.Dispose();
            }
        }

        public YalmsCalculator GetCalculator()
        {
            return yalmsCalculator;
        }

        public NyanCatRenderer GetRenderer()
        {
            return nyanRenderer;
        }

        public void UpdateDamping(float damping)
        {
            yalmsCalculator.SetDamping(damping);
        }

        public void UpdateMaxSpeed(float maxSpeed)
        {
            nyanRenderer.SetMaxYalms(maxSpeed);
        }
    }
}
