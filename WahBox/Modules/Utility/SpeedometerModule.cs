using System;
using System.Numerics;
using Dalamud.Interface;
using Dalamud.Interface.Windowing;
using ImGuiNET;
using WahBox.Core;
using WahBox.Core.Interfaces;
using System.Collections.Generic;

namespace WahBox.Modules.Utility;

public class SpeedometerModule : BaseUtilityModule
{
    public override string Name => "Speedometer";
    public override ModuleType Type => ModuleType.Speedometer;
    
    private SpeedometerWindow? _speedometerWindow;
    private YalmsCalculator _yalmsCalculator;
    
    // Configuration
    public bool ShowSpeedometerOnStartup { get; set; } = true;
    public float MaxYalms { get; set; } = 20.0f;
    public float RedlineStart { get; set; } = 16.0f;
    public float NeedleDamping { get; set; } = 0.1f;
    public int SelectedSpeedometerType { get; set; } = 0; // 0 = Classic, 1 = NyanCat

    // Public accessor for ClientState
    public Dalamud.Plugin.Services.IClientState ClientState => Plugin.ClientState;

    public SpeedometerModule(Plugin plugin) : base(plugin)
    {
        IconId = 60954; // Speed/Movement icon
        _yalmsCalculator = new YalmsCalculator();
    }

    protected override void CreateWindow()
    {
        _speedometerWindow = new SpeedometerWindow(this);
        ModuleWindow = _speedometerWindow;
    }

    public override void Initialize()
    {
        base.Initialize();
        ApplySpeedometerVisibility();
    }

    public override void Load()
    {
        base.Load();
        ApplySpeedometerVisibility();
    }

    private void ApplySpeedometerVisibility()
    {
        if (ShowSpeedometerOnStartup && IsEnabled)
        {
            if (ModuleWindow != null && !ModuleWindow.IsOpen)
            {
                ModuleWindow.IsOpen = true;
            }
        }
        else
        {
            if (ModuleWindow != null && ModuleWindow.IsOpen)
            {
                ModuleWindow.IsOpen = false;
            }
        }
    }

    public override void OpenWindow()
    {
        base.OpenWindow();
        ShowSpeedometerOnStartup = true;
    }

    public override void CloseWindow()
    {
        base.CloseWindow();
        ShowSpeedometerOnStartup = false;
    }

    public override void Update()
    {
        base.Update();
        
        if (Plugin.ClientState.LocalPlayer != null)
        {
            _yalmsCalculator.Update(Plugin.ClientState.LocalPlayer.Position);
        }
        else
        {
            _yalmsCalculator.Reset();
        }
    }

    public float GetCurrentSpeed() => _yalmsCalculator.GetDisplayYalms();
    public YalmsCalculator GetCalculator() => _yalmsCalculator;

    protected override Dictionary<string, object> GetConfigurationData()
    {
        return new Dictionary<string, object>
        {
            ["ShowSpeedometerOnStartup"] = ShowSpeedometerOnStartup,
            ["MaxYalms"] = MaxYalms,
            ["RedlineStart"] = RedlineStart,
            ["NeedleDamping"] = NeedleDamping,
            ["SelectedSpeedometerType"] = SelectedSpeedometerType
        };
    }

    protected override void SetConfigurationData(object config)
    {
        if (config is not Dictionary<string, object> configDict) return;

        try
        {
            if (configDict.TryGetValue("ShowSpeedometerOnStartup", out var showOnStartup))
                ShowSpeedometerOnStartup = Convert.ToBoolean(showOnStartup);
            if (configDict.TryGetValue("MaxYalms", out var maxYalms))
                MaxYalms = Convert.ToSingle(maxYalms);
            if (configDict.TryGetValue("RedlineStart", out var redlineStart))
                RedlineStart = Convert.ToSingle(redlineStart);
            if (configDict.TryGetValue("NeedleDamping", out var needleDamping))
                NeedleDamping = Convert.ToSingle(needleDamping);
            if (configDict.TryGetValue("SelectedSpeedometerType", out var selectedType))
                SelectedSpeedometerType = Convert.ToInt32(selectedType);
            
            // Apply configuration changes that require special handling
            // Note: ApplySpeedometerVisibility will be called in Initialize() after window creation
            _speedometerWindow?.UpdateRenderer();
        }
        catch (Exception ex)
        {
            Plugin.Log.Warning(ex, "Failed to load some speedometer configuration values, using defaults");
        }
    }

    public override void DrawConfig()
    {
        ImGui.Text("Speedometer Configuration");
        ImGui.Separator();
        
        if (ImGui.CollapsingHeader("Display Settings"))
        {
            bool showOnStartup = ShowSpeedometerOnStartup;
            if (ImGui.Checkbox("Show on Startup", ref showOnStartup))
            {
                ShowSpeedometerOnStartup = showOnStartup;
                ApplySpeedometerVisibility();
                SaveConfiguration();
            }
            
            string[] speedometerTypes = { "Classic Gauge", "Nyan Cat" };
            int selectedType = SelectedSpeedometerType;
            if (ImGui.Combo("Display Style", ref selectedType, speedometerTypes, speedometerTypes.Length))
            {
                SelectedSpeedometerType = selectedType;
                _speedometerWindow?.UpdateRenderer();
                SaveConfiguration();
            }
        }
        
        if (ImGui.CollapsingHeader("Gauge Settings"))
        {
            float maxYalms = MaxYalms;
            if (ImGui.SliderFloat("Max Speed", ref maxYalms, 5.0f, 50.0f, "%.0f y/s"))
            {
                MaxYalms = maxYalms;
                _speedometerWindow?.GetRenderer()?.SetMaxYalms(MaxYalms);
                SaveConfiguration();
            }
            
            float redlineStart = RedlineStart;
            if (ImGui.SliderFloat("Redline Start", ref redlineStart, 0.0f, MaxYalms, "%.0f y/s"))
            {
                RedlineStart = redlineStart;
                _speedometerWindow?.GetRenderer()?.SetRedlineStart(RedlineStart);
                SaveConfiguration();
            }
            
            float needleDamping = NeedleDamping;
            if (ImGui.SliderFloat("Needle Smoothing", ref needleDamping, 0.01f, 1.0f, "%.2f"))
            {
                NeedleDamping = needleDamping;
                _yalmsCalculator.SetDamping(NeedleDamping);
                SaveConfiguration();
            }
            
            ImGui.TextWrapped("Lower smoothing values make the needle movement smoother but less responsive.");
        }
    }

    public override void DrawStatus()
    {
        if (Status == ModuleStatus.Active)
        {
            var speed = GetCurrentSpeed();
            ImGui.TextColored(new Vector4(0.2f, 0.8f, 0.2f, 1), "Speedometer Active");
            ImGui.Text($"Current: {speed:F1} y/s");
            
            // Color based on speed
            var speedPercent = Math.Min(speed / MaxYalms, 1.0f);
            var color = speedPercent switch
            {
                < 0.3f => new Vector4(0.2f, 0.8f, 0.2f, 1),
                < 0.6f => new Vector4(0.8f, 0.8f, 0.2f, 1),
                < 0.8f => new Vector4(0.8f, 0.5f, 0.2f, 1),
                _ => new Vector4(0.8f, 0.2f, 0.2f, 1)
            };
            
            var status = speed switch
            {
                0 => "Stationary",
                < 6 => "Walking",
                < 15 => "Running",
                < 20 => "Sprinting",
                _ => "Mount Speed"
            };
            
            ImGui.TextColored(color, status);
        }
        else
        {
            ImGui.TextColored(new Vector4(0.5f, 0.5f, 0.5f, 1), "Speedometer Inactive");
        }
    }
}

public class YalmsCalculator
{
    // State for calculating speed
    private Vector3 previousPosition;
    private DateTime previousTime;
    private float currentYalms;
    private float displayYalms;
    private float damping;

    public YalmsCalculator()
    {
        previousPosition = Vector3.Zero;
        previousTime = DateTime.Now;
        currentYalms = 0.0f;
        displayYalms = 0.0f;
        damping = 0.1f; // Lower values create smoother needle movement
    }

    public float GetDisplayYalms() => displayYalms;
    public float GetCurrentYalms() => currentYalms;
    public Vector3 GetPreviousPosition() => previousPosition;
    public DateTime GetPreviousTime() => previousTime;

    public void SetDamping(float newDamping)
    {
        damping = Math.Clamp(newDamping, 0.01f, 1.0f);
    }

    public void Update(Vector3 currentPosition)
    {
        // Initialize position data on first call
        if (previousPosition == Vector3.Zero)
        {
            previousPosition = currentPosition;
            previousTime = DateTime.Now;
            return;
        }

        DateTime currentTime = DateTime.Now;
        double deltaTime = (currentTime - previousTime).TotalSeconds;

        // Only update if we have a reasonable time difference
        if (deltaTime > 0.01)
        {
            // Only measure horizontal movement (X/Z axes)
            float distanceTraveled = new Vector2(
                currentPosition.X - previousPosition.X,
                currentPosition.Z - previousPosition.Z
            ).Length();

            currentYalms = distanceTraveled / (float)deltaTime;
            previousPosition = currentPosition;
            previousTime = currentTime;
        }

        // Apply damping for smooth animation
        displayYalms = displayYalms + (currentYalms - displayYalms) * damping;
    }

    public void Reset()
    {
        currentYalms = 0.0f;
        displayYalms = 0.0f;
        previousPosition = Vector3.Zero;
        previousTime = DateTime.Now;
    }
}

public interface ISpeedometerRenderer
{
    void Render(float displayYalms);
    void SetMaxYalms(float maxYalms);
    void SetRedlineStart(float redlineStart);
}

public class ClassicRenderer : ISpeedometerRenderer
{
    private float maxYalms;
    private float redlineStart;

    // UI colors
    private readonly uint dialColor;
    private readonly uint needleColor;
    private readonly uint markingsColor;
    private readonly uint textColor;
    private readonly uint backgroundColor;

    public ClassicRenderer()
    {
        maxYalms = 20.0f;
        redlineStart = 16.0f;

        // Dark theme colors for gauge
        backgroundColor = ImGui.ColorConvertFloat4ToU32(new Vector4(0.05f, 0.05f, 0.05f, 1.0f));
        dialColor = ImGui.ColorConvertFloat4ToU32(new Vector4(0.2f, 0.2f, 0.2f, 1.0f));
        needleColor = ImGui.ColorConvertFloat4ToU32(new Vector4(0.9f, 0.3f, 0.3f, 1.0f));
        markingsColor = ImGui.ColorConvertFloat4ToU32(new Vector4(0.8f, 0.8f, 0.8f, 1.0f));
        textColor = ImGui.ColorConvertFloat4ToU32(new Vector4(0.9f, 0.9f, 0.9f, 1.0f));
    }

    public void SetMaxYalms(float newMax)
    {
        maxYalms = Math.Max(newMax, 5.0f);
    }

    public void SetRedlineStart(float newStart)
    {
        redlineStart = Math.Clamp(newStart, 0.0f, maxYalms);
    }

    public void Render(float displayYalms)
    {
        // Get window dimensions
        Vector2 windowPos = ImGui.GetWindowPos();
        Vector2 windowSize = ImGui.GetWindowSize();
        Vector2 center = new Vector2(
            windowPos.X + windowSize.X / 2,
            windowPos.Y + windowSize.Y / 2
        );

        float radius = Math.Min(windowSize.X, windowSize.Y) * 0.35f;

        var drawList = ImGui.GetWindowDrawList();

        // Make the window draggable
        ImGui.SetCursorPos(Vector2.Zero);
        ImGui.InvisibleButton("##draggable", windowSize);

        if (ImGui.IsItemActive() && ImGui.IsMouseDragging(ImGuiMouseButton.Left))
        {
            Vector2 delta = ImGui.GetIO().MouseDelta;
            ImGui.SetWindowPos(new Vector2(windowPos.X + delta.X, windowPos.Y + delta.Y));
        }

        // Draw gauge elements - ensure they fit within window bounds
        float maxBackgroundRadius = Math.Min(radius + 15, Math.Min(windowSize.X, windowSize.Y) * 0.45f);
        drawList.AddCircleFilled(center, maxBackgroundRadius, backgroundColor);
        drawList.AddCircle(center, radius + 8, dialColor, 100, 2.0f);
        drawList.AddCircleFilled(center, radius, dialColor);

        // Draw danger zone
        DrawRedline(drawList, center, radius, redlineStart, maxYalms);

        // Draw gauge markings
        DrawSpeedMarkings(drawList, center, radius);

        // Draw needle pointing to current speed
        DrawNeedle(drawList, center, radius, displayYalms / maxYalms);

        // Draw digital speed readout
        DrawDigitalReadout(drawList, center, radius, displayYalms);
    }

    private void DrawRedline(ImDrawListPtr drawList, Vector2 center, float radius, float startValue, float endValue)
    {
        float startPercent = startValue / maxYalms;
        float endPercent = endValue / maxYalms;

        startPercent = Math.Clamp(startPercent, 0.0f, 1.0f);
        endPercent = Math.Clamp(endPercent, 0.0f, 1.0f);

        float startAngle = 150 * (float)Math.PI / 180;
        float endAngle = 30 * (float)Math.PI / 180;
        float totalAngle = (endAngle - startAngle + 2 * (float)Math.PI) % (2 * (float)Math.PI);

        float redStartAngle = startAngle + totalAngle * startPercent;
        float redEndAngle = startAngle + totalAngle * endPercent;

        int segments = (int)(40 * (endPercent - startPercent));
        segments = Math.Max(segments, 12);

        float innerRadius = radius - 5;
        float outerRadius = radius + 5;

        uint redlineColor = ImGui.ColorConvertFloat4ToU32(new Vector4(0.9f, 0.1f, 0.1f, 0.7f));

        for (int i = 0; i < segments; i++)
        {
            float angle1 = redStartAngle + (redEndAngle - redStartAngle) * i / segments;
            float angle2 = redStartAngle + (redEndAngle - redStartAngle) * (i + 1) / segments;

            float innerStartX = center.X + innerRadius * (float)Math.Cos(angle1);
            float innerStartY = center.Y + innerRadius * (float)Math.Sin(angle1);
            float outerStartX = center.X + outerRadius * (float)Math.Cos(angle1);
            float outerStartY = center.Y + outerRadius * (float)Math.Sin(angle1);

            float innerEndX = center.X + innerRadius * (float)Math.Cos(angle2);
            float innerEndY = center.Y + innerRadius * (float)Math.Sin(angle2);
            float outerEndX = center.X + outerRadius * (float)Math.Cos(angle2);
            float outerEndY = center.Y + outerRadius * (float)Math.Sin(angle2);

            drawList.AddQuadFilled(
                new Vector2(innerStartX, innerStartY),
                new Vector2(outerStartX, outerStartY),
                new Vector2(outerEndX, outerEndY),
                new Vector2(innerEndX, innerEndY),
                redlineColor
            );
        }
    }

    private void DrawSpeedMarkings(ImDrawListPtr drawList, Vector2 center, float radius)
    {
        float startAngle = 150 * (float)Math.PI / 180;
        float endAngle = 30 * (float)Math.PI / 180;
        float totalAngle = (endAngle - startAngle + 2 * (float)Math.PI) % (2 * (float)Math.PI);

        int majorMarkings = 5;
        int minorMarkingsPerMajor = 4;

        uint redlineColor = ImGui.ColorConvertFloat4ToU32(new Vector4(0.9f, 0.1f, 0.1f, 1.0f));

        for (int i = 0; i <= majorMarkings; i++)
        {
            float speed = (maxYalms / majorMarkings) * i;
            float angle = startAngle + (totalAngle * i / majorMarkings);

            float outerX = center.X + (radius - 2) * (float)Math.Cos(angle);
            float outerY = center.Y + (radius - 2) * (float)Math.Sin(angle);
            float innerX = center.X + (radius - 15) * (float)Math.Cos(angle);
            float innerY = center.Y + (radius - 15) * (float)Math.Sin(angle);

            bool isInRedZone = speed >= redlineStart && speed <= maxYalms;
            uint markingColor = isInRedZone ? redlineColor : markingsColor;

            drawList.AddLine(new Vector2(innerX, innerY), new Vector2(outerX, outerY), markingColor, 2.0f);

            float textX = center.X + (radius - 30) * (float)Math.Cos(angle);
            float textY = center.Y + (radius - 30) * (float)Math.Sin(angle);
            drawList.AddText(new Vector2(textX - 10, textY - 10), markingColor, speed.ToString("0"));

            if (i < majorMarkings)
            {
                for (int j = 1; j <= minorMarkingsPerMajor; j++)
                {
                    float minorSpeed = speed + (maxYalms / majorMarkings) * (j / (float)minorMarkingsPerMajor);
                    bool minorInRedZone = minorSpeed >= redlineStart && minorSpeed <= maxYalms;
                    uint minorMarkingColor = minorInRedZone ? redlineColor : markingsColor;

                    float minorAngle = startAngle + (totalAngle * (i + (float)j / minorMarkingsPerMajor) / majorMarkings);
                    float minorOuterX = center.X + (radius - 2) * (float)Math.Cos(minorAngle);
                    float minorOuterY = center.Y + (radius - 2) * (float)Math.Sin(minorAngle);
                    float minorInnerX = center.X + (radius - 10) * (float)Math.Cos(minorAngle);
                    float minorInnerY = center.Y + (radius - 10) * (float)Math.Sin(minorAngle);

                    drawList.AddLine(
                        new Vector2(minorInnerX, minorInnerY),
                        new Vector2(minorOuterX, minorOuterY),
                        minorMarkingColor, 1.0f
                    );
                }
            }
        }
    }

    private void DrawNeedle(ImDrawListPtr drawList, Vector2 center, float radius, float speedFraction)
    {
        speedFraction = Math.Clamp(speedFraction, 0.0f, 1.0f);

        float startAngle = 150 * (float)Math.PI / 180;
        float endAngle = 30 * (float)Math.PI / 180;
        float totalAngle = (endAngle - startAngle + 2 * (float)Math.PI) % (2 * (float)Math.PI);
        float needleAngle = startAngle + totalAngle * speedFraction;

        float needleLength = radius - 20;
        float tipX = center.X + needleLength * (float)Math.Cos(needleAngle);
        float tipY = center.Y + needleLength * (float)Math.Sin(needleAngle);

        drawList.AddLine(center, new Vector2(tipX, tipY), needleColor, 2.0f);
        drawList.AddCircleFilled(center, 10, needleColor);
        drawList.AddCircleFilled(center, 6, dialColor);
    }

    private void DrawDigitalReadout(ImDrawListPtr drawList, Vector2 center, float radius, float yalms)
    {
        Vector2 textPos = new Vector2(center.X - 40, center.Y + radius / 2);

        drawList.AddRectFilled(
            new Vector2(textPos.X - 5, textPos.Y - 5),
            new Vector2(textPos.X + 85, textPos.Y + 25),
            ImGui.ColorConvertFloat4ToU32(new Vector4(0.1f, 0.1f, 0.1f, 1.0f))
        );

        string speedText = $"{yalms:F1} yalms/s";
        drawList.AddText(textPos, textColor, speedText);
    }
}

public class NyanCatRenderer : ISpeedometerRenderer
{
    private float maxYalms;
    private readonly uint[] rainbowColors;
    private Vector2 catSize;
    private float animationTimer;
    private int frameCounter;
    private const int MaxTrailSegments = 35;
    private int trailSegments;
    private float previousDisplayYalms;
    private float trailFadeTimer;

    public NyanCatRenderer()
    {
        maxYalms = 20.0f;
        animationTimer = 0f;
        frameCounter = 0;
        previousDisplayYalms = 0f;
        trailFadeTimer = 0f;

        // Make cat size appropriate for the 450x150 window
        catSize = new Vector2(100, 60);

        // Classic rainbow colors
        rainbowColors = new uint[6];
        rainbowColors[0] = ImGui.ColorConvertFloat4ToU32(new Vector4(1.0f, 0.0f, 0.0f, 0.8f)); // Red
        rainbowColors[1] = ImGui.ColorConvertFloat4ToU32(new Vector4(1.0f, 0.5f, 0.0f, 0.8f)); // Orange
        rainbowColors[2] = ImGui.ColorConvertFloat4ToU32(new Vector4(1.0f, 1.0f, 0.0f, 0.8f)); // Yellow
        rainbowColors[3] = ImGui.ColorConvertFloat4ToU32(new Vector4(0.0f, 1.0f, 0.0f, 0.8f)); // Green
        rainbowColors[4] = ImGui.ColorConvertFloat4ToU32(new Vector4(0.0f, 0.5f, 1.0f, 0.8f)); // Blue
        rainbowColors[5] = ImGui.ColorConvertFloat4ToU32(new Vector4(0.5f, 0.0f, 1.0f, 0.8f)); // Purple
    }

    public void SetMaxYalms(float newMax)
    {
        maxYalms = Math.Max(newMax, 5.0f);
    }

    public void SetRedlineStart(float redlineStart)
    {
        // NyanCat doesn't use redline concept
    }

    public void Render(float displayYalms)
    {
        Vector2 windowPos = ImGui.GetWindowPos();
        Vector2 windowSize = ImGui.GetWindowSize();

        // Update animation - faster animation when moving
        float animSpeed = displayYalms > 0.5f ? 8.0f : 3.0f;
        animationTimer += ImGui.GetIO().DeltaTime * animSpeed;
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

        // Position cat at right side of the 450px window
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

        // Position text above the cat center
        Vector2 textPos = new Vector2(
            catPos.X + (catSize.X / 2) - (textSize.X / 2),
            catPos.Y - textSize.Y - 8
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
        float segmentWidth = 10.0f;
        float totalTrailHeight = catSize.Y * 0.6f;
        float segmentHeight = totalTrailHeight / 6.0f;

        float yOffset = catSize.Y / 2 - totalTrailHeight / 2;

        // Position trail to start from the cat's poptart body
        float poptartPosition = catPos.X + 10;
        float actualTrailStartX = poptartPosition + 15;
        
        Vector2 windowPos = ImGui.GetWindowPos();

        // Draw segments from right to left
        for (int i = 0; i < segments; i++)
        {
            float xPos = actualTrailStartX - ((i + 1) * segmentWidth);

            // Stop drawing if we go outside the window bounds
            if (xPos < windowPos.X)
                break;

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
        // Try to use nyan.png image if available
        string pluginPath = Plugin.PluginInterface.AssemblyLocation.Directory?.FullName ?? "";
        string nyanCatImagePath = System.IO.Path.Combine(pluginPath, "Data", "nyan.png");
        
        try
        {
            var texture = Plugin.TextureProvider.GetFromFile(nyanCatImagePath).GetWrapOrDefault();
            if (texture != null)
            {
                // Animate bounce when moving
                float bounce = 0f;
                if (displayYalms > 0.5f)
                {
                    float bounceScale = Math.Min(displayYalms / 5.0f, 1.0f);
                    bounce = (float)Math.Sin(animationTimer * Math.PI * 2) * 2.0f * bounceScale;
                }
                Vector2 adjustedPos = new Vector2(catPos.X, catPos.Y + bounce);

                // Maintain aspect ratio
                float aspectRatio = (float)texture.Width / texture.Height;
                Vector2 drawSize = new Vector2(catSize.Y * aspectRatio, catSize.Y);

                drawList.AddImage(
                    texture.ImGuiHandle,
                    adjustedPos,
                    new Vector2(adjustedPos.X + drawSize.X, adjustedPos.Y + drawSize.Y)
                );
                return;
            }
        }
        catch
        {
            // Fall back to drawn version if image loading fails
        }

        // Fallback to drawn version
        DrawEnhancedNyanCat(drawList, catPos, displayYalms);
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
}

public class SpeedometerWindow : Window
{
    private SpeedometerModule _module;
    private ISpeedometerRenderer? _renderer;

    public SpeedometerWindow(SpeedometerModule module) : base("Speedometer##SpeedWindow",
        ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse |
        ImGuiWindowFlags.NoBackground | ImGuiWindowFlags.NoTitleBar |
        ImGuiWindowFlags.NoResize)
    {
        _module = module;
        
        // Start with classic size, will be updated by renderer
        Size = new Vector2(350, 350);
        SizeCondition = ImGuiCond.FirstUseEver;
        
        // Prevent ESC from closing this window
        RespectCloseHotkey = false;
        
        // Create renderer based on configuration
        UpdateRenderer();
    }

    public override void Draw()
    {
        var localPlayer = _module.ClientState.LocalPlayer;
        if (localPlayer == null)
        {
            ImGui.Text("Player not available");
            return;
        }
        
        _renderer?.Render(_module.GetCurrentSpeed());
    }
    
    public ISpeedometerRenderer? GetRenderer() => _renderer;
    
    public void UpdateRenderer()
    {
        switch (_module.SelectedSpeedometerType)
        {
            case 0:
            default:
                _renderer = new ClassicRenderer();
                // Square window for classic gauge (original size)
                Size = new Vector2(350, 350);
                break;
            case 1:
                _renderer = new NyanCatRenderer();
                // Wider window for NyanCat trail (original size)
                Size = new Vector2(450, 150);
                break;
        }
        
        // Apply configuration
        _renderer?.SetMaxYalms(_module.MaxYalms);
        _renderer?.SetRedlineStart(_module.RedlineStart);
    }
}
