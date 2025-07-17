using System;
using System.Numerics;
using ImGuiNET;
using Dalamud.Interface.Utility.Raii;

namespace ZoomiesPlugin.Renderers
{
    public class NyanCatRenderer : IDisposable
    {
        private float maxYalms;
        private readonly uint[] rainbowColors;
        private Vector2 catSize;
        private float animationTimer;
        private int frameCounter;
        private const int MaxTrailSegments = 50;
        private int trailSegments;
        private string nyanCatImagePath;
        private float previousDisplayYalms;
        private float trailFadeTimer;

        public NyanCatRenderer()
        {
            maxYalms = 20.0f;
            animationTimer = 0f;
            frameCounter = 0;
            previousDisplayYalms = 0f;
            trailFadeTimer = 0f;

            // Make cat larger for better visibility
            catSize = new Vector2(160, 80);

            // Classic rainbow colors
            rainbowColors = new uint[6];
            rainbowColors[0] = ImGui.ColorConvertFloat4ToU32(new Vector4(1.0f, 0.0f, 0.0f, 0.8f)); // Red
            rainbowColors[1] = ImGui.ColorConvertFloat4ToU32(new Vector4(1.0f, 0.5f, 0.0f, 0.8f)); // Orange
            rainbowColors[2] = ImGui.ColorConvertFloat4ToU32(new Vector4(1.0f, 1.0f, 0.0f, 0.8f)); // Yellow
            rainbowColors[3] = ImGui.ColorConvertFloat4ToU32(new Vector4(0.0f, 1.0f, 0.0f, 0.8f)); // Green
            rainbowColors[4] = ImGui.ColorConvertFloat4ToU32(new Vector4(0.0f, 0.5f, 1.0f, 0.8f)); // Blue
            rainbowColors[5] = ImGui.ColorConvertFloat4ToU32(new Vector4(0.5f, 0.0f, 1.0f, 0.8f)); // Purple

            // Find image path
            string pluginPath = ZoomiesPlugin.Core.Plugin.PluginInterface.AssemblyLocation.Directory?.FullName!;
            nyanCatImagePath = System.IO.Path.Combine(pluginPath, "nyan.png");
        }

        public void SetMaxYalms(float newMax)
        {
            maxYalms = Math.Max(newMax, 5.0f); // Ensure a minimum value
        }

        public bool WasCloseButtonClicked()
        {
            return false;
        }

        public void Render(float displayYalms)
        {
            Vector2 windowPos = ImGui.GetWindowPos();
            Vector2 windowSize = ImGui.GetWindowSize();

            // Update animation
            animationTimer += ImGui.GetIO().DeltaTime * 5.0f;
            if (animationTimer >= 1.0f)
            {
                animationTimer = 0f;
                frameCounter = (frameCounter + 1) % 6; // 6 animation frames
            }

            // Calculate trail based on speed
            float speedRatio = Math.Clamp(displayYalms / maxYalms, 0.0f, 1.0f);

            // Fade trail when stopping
            if (displayYalms < 0.5f)
            {
                trailFadeTimer += ImGui.GetIO().DeltaTime * 2.0f;
                trailFadeTimer = Math.Min(trailFadeTimer, 1.0f);
            }
            else
            {
                trailFadeTimer = 0f;
            }

            // Apply fade to trail length
            float fadeFactor = 1.0f - trailFadeTimer;
            trailSegments = (int)(speedRatio * MaxTrailSegments * fadeFactor);
            trailSegments = Math.Max(0, Math.Min(trailSegments, MaxTrailSegments));

            previousDisplayYalms = displayYalms;

            var drawList = ImGui.GetWindowDrawList();

            // Make window draggable
            ImGui.SetCursorPos(Vector2.Zero);
            ImGui.InvisibleButton("##draggable", windowSize);
            if (ImGui.IsItemActive() && ImGui.IsMouseDragging(ImGuiMouseButton.Left))
            {
                Vector2 delta = ImGui.GetIO().MouseDelta;
                ImGui.SetWindowPos(new Vector2(windowPos.X + delta.X, windowPos.Y + delta.Y));
            }

            // Transparent background
            drawList.AddRectFilled(
                windowPos,
                new Vector2(windowPos.X + windowSize.X, windowPos.Y + windowSize.Y),
                ImGui.ColorConvertFloat4ToU32(new Vector4(0.0f, 0.0f, 0.0f, 0.0f))
            );

            // Position cat at right side
            Vector2 catPos = new Vector2(
                windowPos.X + windowSize.X - catSize.X - 20,
                windowPos.Y + windowSize.Y / 2 - catSize.Y / 2
            );

            DrawRainbowTrail(drawList, catPos, trailSegments);
            DrawCat(drawList, catPos, displayYalms);
            DrawSpeedText(drawList, catPos, displayYalms);
        }

        private void DrawSpeedText(ImDrawListPtr drawList, Vector2 catPos, float displayYalms)
        {
            string speedText = $"{displayYalms:F1} y/s";
            Vector2 textSize = ImGui.CalcTextSize(speedText);

            // Position text above the poptart
            float toastWidth = catSize.X * 0.65f;
            float toastCenter = catPos.X + 10 + (toastWidth / 2);

            Vector2 textPos = new Vector2(
                toastCenter - (textSize.X / 2) - 6,
                catPos.Y - textSize.Y - 5
            );

            float speedRatio = Math.Clamp(displayYalms / maxYalms, 0.0f, 1.0f);

            // Text color based on speed
            uint textColor;
            if (displayYalms < 0.5f)
            {
                textColor = ImGui.ColorConvertFloat4ToU32(new Vector4(1.0f, 1.0f, 1.0f, 1.0f)); // White when stopped
            }
            else
            {
                int colorIndex = Math.Min((int)(speedRatio * rainbowColors.Length), rainbowColors.Length - 1);
                textColor = rainbowColors[colorIndex]; // Rainbow color based on speed
            }

            // Text background
            float padding = 5.0f;
            Vector2 boxMin = new Vector2(textPos.X - padding, textPos.Y - padding);
            Vector2 boxMax = new Vector2(textPos.X + textSize.X + padding, textPos.Y + textSize.Y + padding);

            drawList.AddRectFilled(
                boxMin,
                boxMax,
                ImGui.ColorConvertFloat4ToU32(new Vector4(0.0f, 0.0f, 0.0f, 0.7f)),
                4.0f // Rounded corners
            );

            // Border matching text color
            drawList.AddRect(
                boxMin,
                boxMax,
                textColor,
                4.0f, // Rounded corners
                ImDrawFlags.None,
                2.0f  // Border thickness
            );

            drawList.AddText(textPos, textColor, speedText);
        }

        private void DrawRainbowTrail(ImDrawListPtr drawList, Vector2 catPos, int segments)
        {
            float segmentWidth = 15.0f;
            float totalTrailHeight = catSize.Y * 0.6f;
            float segmentHeight = totalTrailHeight / 6.0f;

            float yOffset = catSize.Y / 2 - totalTrailHeight / 2;

            // Position trail to overlap with poptart
            float poptartPosition = catPos.X + 10;
            float actualTrailStartX = poptartPosition + 15;

            // Draw segments from right to left
            for (int i = 0; i < segments; i++)
            {
                float xPos = actualTrailStartX - ((i + 1) * segmentWidth);

                // Skip if outside window
                if (xPos < ImGui.GetWindowPos().X - segmentWidth)
                    continue;

                // Add wave animation
                float animOffset = (float)Math.Sin(animationTimer * Math.PI + i * 0.2f) * 1.5f;

                // Draw rainbow stripes
                for (int j = 0; j < 6; j++)
                {
                    float yPos = catPos.Y + yOffset + (j * segmentHeight) + animOffset;

                    drawList.AddRectFilled(
                        new Vector2(xPos, yPos),
                        new Vector2(xPos + segmentWidth, yPos + segmentHeight),
                        rainbowColors[j]
                    );
                }
            }
        }

        private void DrawCat(ImDrawListPtr drawList, Vector2 catPos, float displayYalms)
        {
            // Animate bounce when moving
            float bounce = 0f;
            if (displayYalms > 0.5f)
            {
                float bounceScale = Math.Min(displayYalms / 5.0f, 1.0f);
                bounce = (float)Math.Sin(animationTimer * Math.PI * 2) * 2.0f * bounceScale;
            }
            Vector2 adjustedPos = new Vector2(catPos.X, catPos.Y + bounce);

            // Try to use nyan.png image
            var texture = ZoomiesPlugin.Core.Plugin.TextureProvider.GetFromFile(nyanCatImagePath).GetWrapOrDefault();

            if (texture != null)
            {
                // Maintain aspect ratio
                float aspectRatio = (float)texture.Width / texture.Height;
                Vector2 drawSize = new Vector2(catSize.Y * aspectRatio, catSize.Y);

                drawList.AddImage(
                    texture.ImGuiHandle,
                    adjustedPos,
                    new Vector2(adjustedPos.X + drawSize.X, adjustedPos.Y + drawSize.Y)
                );
            }
            else
            {
                // Fallback to drawn version
                DrawEnhancedNyanCat(drawList, catPos, displayYalms);
            }
        }

        private void DrawEnhancedNyanCat(ImDrawListPtr drawList, Vector2 catPos, float displayYalms)
        {
            // Animate bounce when moving
            float bounce = 0f;
            if (displayYalms > 0.5f)
            {
                float bounceScale = Math.Min(displayYalms / 5.0f, 1.0f);
                bounce = (float)Math.Sin(animationTimer * Math.PI * 2) * 2.0f * bounceScale;
            }
            Vector2 adjustedPos = new Vector2(catPos.X, catPos.Y + bounce);

            // Colors
            uint pinkColor = ImGui.ColorConvertFloat4ToU32(new Vector4(0.95f, 0.6f, 0.8f, 1.0f));
            uint darkPinkColor = ImGui.ColorConvertFloat4ToU32(new Vector4(0.8f, 0.4f, 0.6f, 1.0f));
            uint blackColor = ImGui.ColorConvertFloat4ToU32(new Vector4(0.0f, 0.0f, 0.0f, 1.0f));
            uint whiteColor = ImGui.ColorConvertFloat4ToU32(new Vector4(1.0f, 1.0f, 1.0f, 1.0f));
            uint tanColor = ImGui.ColorConvertFloat4ToU32(new Vector4(0.95f, 0.85f, 0.65f, 1.0f));

            // Main body
            drawList.AddRectFilled(
                adjustedPos,
                new Vector2(adjustedPos.X + catSize.X, adjustedPos.Y + catSize.Y),
                pinkColor
            );

            // Head outline
            float headSize = catSize.Y * 0.9f;
            Vector2 headPos = new Vector2(
                adjustedPos.X + catSize.X - headSize - 5,
                adjustedPos.Y + (catSize.Y - headSize) / 2
            );
            drawList.AddRectFilled(
                headPos,
                new Vector2(headPos.X + headSize, headPos.Y + headSize),
                darkPinkColor
            );

            // Head fill
            float innerHeadSize = headSize - 2;
            Vector2 innerHeadPos = new Vector2(
                headPos.X + 1,
                headPos.Y + 1
            );
            drawList.AddRectFilled(
                innerHeadPos,
                new Vector2(innerHeadPos.X + innerHeadSize, innerHeadPos.Y + innerHeadSize),
                pinkColor
            );

            // Eyes
            float eyeSize = headSize * 0.15f;
            Vector2 leftEyePos = new Vector2(headPos.X + headSize * 0.25f, headPos.Y + headSize * 0.3f);
            Vector2 rightEyePos = new Vector2(headPos.X + headSize * 0.25f, headPos.Y + headSize * 0.7f);

            drawList.AddCircleFilled(leftEyePos, eyeSize, blackColor);
            drawList.AddCircleFilled(rightEyePos, eyeSize, blackColor);

            // Eye highlights
            drawList.AddCircleFilled(
                new Vector2(leftEyePos.X - eyeSize * 0.3f, leftEyePos.Y - eyeSize * 0.3f),
                eyeSize * 0.4f,
                whiteColor
            );
            drawList.AddCircleFilled(
                new Vector2(rightEyePos.X - eyeSize * 0.3f, rightEyePos.Y - eyeSize * 0.3f),
                eyeSize * 0.4f,
                whiteColor
            );

            // Mouth
            Vector2 mouthStart = new Vector2(headPos.X + headSize * 0.6f, headPos.Y + headSize * 0.5f);
            drawList.AddBezierCubic(
                mouthStart,
                new Vector2(mouthStart.X + headSize * 0.2f, mouthStart.Y - headSize * 0.1f),
                new Vector2(mouthStart.X + headSize * 0.3f, mouthStart.Y + headSize * 0.1f),
                new Vector2(mouthStart.X + headSize * 0.4f, mouthStart.Y),
                blackColor,
                2.0f
            );

            // Pop tart body
            float toastWidth = catSize.X * 0.65f;
            float toastHeight = catSize.Y * 0.75f;
            Vector2 toastPos = new Vector2(
                adjustedPos.X + 10,
                adjustedPos.Y + (catSize.Y - toastHeight) / 2
            );
            drawList.AddRectFilled(
                toastPos,
                new Vector2(toastPos.X + toastWidth, toastPos.Y + toastHeight),
                tanColor
            );

            // Animate legs
            float legOffset = (frameCounter % 2 == 0) ? 2.0f : -2.0f;

            // Draw legs
            float legWidth = 8.0f;
            float legHeight = 6.0f;
            float legSpacing = toastHeight / 3;

            // Front legs
            for (int i = 0; i < 2; i++)
            {
                float yPos = toastPos.Y + legSpacing * (i + 1) - legHeight / 2;
                float xOffset = (i % 2 == 0) ? legOffset : -legOffset;

                drawList.AddRectFilled(
                    new Vector2(toastPos.X - legWidth + xOffset, yPos),
                    new Vector2(toastPos.X + xOffset, yPos + legHeight),
                    pinkColor
                );
            }

            // Back legs
            for (int i = 0; i < 2; i++)
            {
                float yPos = toastPos.Y + legSpacing * (i + 1) - legHeight / 2;
                float xOffset = (i % 2 == 0) ? -legOffset : legOffset;

                drawList.AddRectFilled(
                    new Vector2(toastPos.X + toastWidth + xOffset, yPos),
                    new Vector2(toastPos.X + toastWidth + legWidth + xOffset, yPos + legHeight),
                    pinkColor
                );
            }

            // Tail
            Vector2 tailStart = new Vector2(toastPos.X + toastWidth + 2, toastPos.Y + toastHeight / 2);
            Vector2 tailEnd = new Vector2(tailStart.X + catSize.X * 0.15f, tailStart.Y + (float)Math.Sin(animationTimer * Math.PI * 3) * 5.0f);

            drawList.AddBezierCubic(
                tailStart,
                new Vector2(tailStart.X + 10, tailStart.Y - 10),
                new Vector2(tailEnd.X - 10, tailEnd.Y + 10),
                tailEnd,
                pinkColor,
                4.0f
            );
        }

        public void Dispose()
        {
            // Texture is managed by Plugin class
        }
    }
}
