using System;
using System.Numerics;
using Dalamud.Interface;
using Dalamud.Interface.Windowing;
using ImGuiNET;
using WahBox.Core;
using WahBox.Core.Interfaces;

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

    public override void DrawConfig()
    {
        ImGui.Text("Speedometer Configuration");
        ImGui.Separator();
        
        if (ImGui.CollapsingHeader("Display Settings"))
        {
            ImGui.Checkbox("Show on Startup", ref ShowSpeedometerOnStartup);
            
            string[] speedometerTypes = { "Classic Gauge", "Nyan Cat" };
            ImGui.Combo("Display Style", ref SelectedSpeedometerType, speedometerTypes, speedometerTypes.Length);
        }
        
        if (ImGui.CollapsingHeader("Gauge Settings"))
        {
            if (ImGui.SliderFloat("Max Speed", ref MaxYalms, 5.0f, 50.0f, "%.0f y/s"))
            {
                _speedometerWindow?.GetRenderer()?.SetMaxYalms(MaxYalms);
            }
            
            if (ImGui.SliderFloat("Redline Start", ref RedlineStart, 0.0f, MaxYalms, "%.0f y/s"))
            {
                _speedometerWindow?.GetRenderer()?.SetRedlineStart(RedlineStart);
            }
            
            if (ImGui.SliderFloat("Needle Smoothing", ref NeedleDamping, 0.01f, 1.0f, "%.2f"))
            {
                _yalmsCalculator.SetDamping(NeedleDamping);
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

        float radius = Math.Min(windowSize.X, windowSize.Y) * 0.4f;

        var drawList = ImGui.GetWindowDrawList();

        // Make the window draggable
        ImGui.SetCursorPos(Vector2.Zero);
        ImGui.InvisibleButton("##draggable", windowSize);

        if (ImGui.IsItemActive() && ImGui.IsMouseDragging(ImGuiMouseButton.Left))
        {
            Vector2 delta = ImGui.GetIO().MouseDelta;
            ImGui.SetWindowPos(new Vector2(windowPos.X + delta.X, windowPos.Y + delta.Y));
        }

        // Draw gauge elements
        drawList.AddCircleFilled(center, radius + 20, backgroundColor);
        drawList.AddCircle(center, radius + 10, dialColor, 100, 2.0f);
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
        
        Size = new Vector2(350, 350);
        SizeCondition = ImGuiCond.FirstUseEver;
        
        // Prevent ESC from closing this window
        RespectCloseHotkey = false;
        
        // Create renderer based on configuration
        UpdateRenderer();
    }

    public override void Draw()
    {
        var localPlayer = _module.Plugin.ClientState.LocalPlayer;
        if (localPlayer == null)
        {
            ImGui.Text("Player not available");
            return;
        }
        
        _renderer?.Render(_module.GetCurrentSpeed());
    }
    
    public ISpeedometerRenderer? GetRenderer() => _renderer;
    
    private void UpdateRenderer()
    {
        switch (_module.SelectedSpeedometerType)
        {
            case 0:
            default:
                _renderer = new ClassicRenderer();
                break;
            // case 1: // NyanCat renderer could be added later
        }
        
        // Apply configuration
        _renderer?.SetMaxYalms(_module.MaxYalms);
        _renderer?.SetRedlineStart(_module.RedlineStart);
    }
}
