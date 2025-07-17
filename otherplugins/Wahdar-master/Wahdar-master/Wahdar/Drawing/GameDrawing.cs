using System;
using System.Numerics;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Plugin.Services;
using ImGuiNET;

namespace Wahdar.Drawing
{
    public static class GameDrawing
    {
        private static bool _overlayDrawing = false;
        private static ImDrawListPtr _drawList;

        public static bool BeginOverlayDrawing()
        {
            if (_overlayDrawing) return true;

            // Safety check: Don't create overlay if player is not available
            if (Plugin.ClientState.LocalPlayer == null)
            {
                return false;
            }

            ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, Vector2.Zero);
            ImGui.PushStyleVar(ImGuiStyleVar.WindowBorderSize, 0.0f);
            ImGui.PushStyleVar(ImGuiStyleVar.WindowMinSize, Vector2.Zero);
            
            // Ensure the window background is fully transparent
            ImGui.PushStyleColor(ImGuiCol.WindowBg, new Vector4(0, 0, 0, 0));
            ImGui.PushStyleColor(ImGuiCol.ChildBg, new Vector4(0, 0, 0, 0));
            
            ImGuiWindowFlags windowFlags = 
                ImGuiWindowFlags.NoInputs | 
                ImGuiWindowFlags.NoTitleBar | 
                ImGuiWindowFlags.NoMove | 
                ImGuiWindowFlags.NoResize | 
                ImGuiWindowFlags.NoScrollbar | 
                ImGuiWindowFlags.NoScrollWithMouse | 
                ImGuiWindowFlags.NoCollapse | 
                ImGuiWindowFlags.NoBackground | 
                ImGuiWindowFlags.NoSavedSettings | 
                ImGuiWindowFlags.NoBringToFrontOnFocus | 
                ImGuiWindowFlags.NoDocking;

            ImGui.SetNextWindowPos(Vector2.Zero);
            ImGui.SetNextWindowSize(ImGui.GetIO().DisplaySize);
            
            // Use unique window ID to avoid conflicts with other plugins
            bool opened = ImGui.Begin("WahdarOverlay##WahdarUniqueID", windowFlags);
            if (opened)
            {
                _overlayDrawing = true;
                _drawList = ImGui.GetWindowDrawList();
                return true;
            }
            
            // Clean up if window creation failed
            ImGui.PopStyleColor(2);
            ImGui.PopStyleVar(3);
            return false;
        }

        public static void EndOverlayDrawing()
        {
            if (!_overlayDrawing) return;
            
            ImGui.End();
            ImGui.PopStyleColor(2); // Pop the two color styles we pushed
            ImGui.PopStyleVar(3);   // Pop the three style vars we pushed
            _overlayDrawing = false;
        }

        /// <summary>
        /// Force cleanup of overlay drawing state in case of errors
        /// </summary>
        public static void ForceCleanupOverlay()
        {
            if (_overlayDrawing)
            {
                try
                {
                    ImGui.End();
                    ImGui.PopStyleColor(2);
                    ImGui.PopStyleVar(3);
                }
                catch
                {
                    // Ignore errors during cleanup
                }
                finally
                {
                    _overlayDrawing = false;
                }
            }
        }

        public static void DrawDot(Vector3 position, float thickness, Vector4 color)
        {
            if (!_overlayDrawing) return;
            
            if (Plugin.GameGui.WorldToScreen(position, out Vector2 screenPos))
            {
                _drawList.AddCircleFilled(
                    screenPos,
                    thickness,
                    ImGui.ColorConvertFloat4ToU32(color),
                    12);
            }
        }

        public static void DrawLine(Vector3 start, Vector3 end, float thickness, Vector4 color)
        {
            if (!_overlayDrawing) return;
            
            if (Plugin.GameGui.WorldToScreen(start, out Vector2 startPos) && 
                Plugin.GameGui.WorldToScreen(end, out Vector2 endPos))
            {
                _drawList.AddLine(
                    startPos,
                    endPos,
                    ImGui.ColorConvertFloat4ToU32(color),
                    thickness);
            }
        }

        public static void DrawText(Vector3 position, string text, Vector4 color)
        {
            if (!_overlayDrawing) return;
            
            if (Plugin.GameGui.WorldToScreen(position, out Vector2 screenPos))
            {
                var textSize = ImGui.CalcTextSize(text);
                _drawList.AddText(
                    screenPos - textSize / 2,
                    ImGui.ColorConvertFloat4ToU32(color),
                    text);
            }
        }

        public static void DrawCircle(Vector3 position, float radius, Vector4 color, float thickness = 1.0f)
        {
            if (!_overlayDrawing) return;
            
            const int segments = 36;
            float angleStep = MathF.PI * 2.0f / segments;
            
            Vector2? prevScreenPos = null;
            bool prevValid = false;
            
            for (int i = 0; i <= segments; i++)
            {
                float angle = i * angleStep;
                float x = position.X + radius * MathF.Cos(angle);
                float z = position.Z + radius * MathF.Sin(angle);
                
                Vector3 worldPos = new Vector3(x, position.Y, z);
                bool valid = Plugin.GameGui.WorldToScreen(worldPos, out Vector2 screenPos);
                
                if (valid && prevValid && prevScreenPos.HasValue)
                {
                    _drawList.AddLine(
                        prevScreenPos.Value, 
                        screenPos, 
                        ImGui.ColorConvertFloat4ToU32(color), 
                        thickness);
                }
                
                prevScreenPos = screenPos;
                prevValid = valid;
            }
        }
        
        public static void DrawDirectionalIndicator(Vector3 position, float rotation, float size, Vector4 circleColor, Vector4 arrowColor)
        {
            if (!_overlayDrawing) return;
            
            if (!Plugin.GameGui.WorldToScreen(position, out Vector2 centerScreenPos))
                return;
                
            _drawList.AddCircleFilled(
                centerScreenPos, 
                size, 
                ImGui.ColorConvertFloat4ToU32(circleColor),
                12);
            
            float dirLength = size * 1.5f;
            float arrowSize = size * 0.8f;
            float arrowAngle = MathF.PI / 6;
            
            Vector2 dirVector = new Vector2(
                MathF.Sin(rotation),
                -MathF.Cos(rotation)
            );
            
            Vector2 arrowTip = centerScreenPos + dirVector * dirLength;
            
            _drawList.AddLine(
                centerScreenPos, 
                arrowTip, 
                ImGui.ColorConvertFloat4ToU32(arrowColor), 
                2.0f);
            
            Vector2 leftPoint = arrowTip - new Vector2(
                arrowSize * MathF.Sin(rotation - arrowAngle),
                -arrowSize * MathF.Cos(rotation - arrowAngle)
            );
            
            Vector2 rightPoint = arrowTip - new Vector2(
                arrowSize * MathF.Sin(rotation + arrowAngle),
                -arrowSize * MathF.Cos(rotation + arrowAngle)
            );
            
            _drawList.AddLine(
                arrowTip, 
                leftPoint, 
                ImGui.ColorConvertFloat4ToU32(arrowColor), 
                2.0f);
                
            _drawList.AddLine(
                arrowTip, 
                rightPoint, 
                ImGui.ColorConvertFloat4ToU32(arrowColor), 
                2.0f);
        }
    }
} 