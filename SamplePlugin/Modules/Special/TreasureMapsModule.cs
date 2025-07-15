using System;
using System.Collections.Generic;
using SamplePlugin.Core;
using SamplePlugin.Core.Interfaces;
using ImGuiNET;

namespace SamplePlugin.Modules.Special;

public class TreasureMapsModule : BaseModule
{
    public override string Name => "Treasure Maps";
    public override ModuleType Type => ModuleType.Special;

    public TreasureMapsModule(Plugin plugin) : base(plugin)
    {
        IconId = 60758; // Module icon
    }

    public override void Initialize()
    {
        base.Initialize();
    }

    public override void Update()
    {
        if (!Plugin.ClientState.IsLoggedIn) return;
        
        // Special module update logic
        Status = ModuleStatus.Incomplete;
    }

    public override void DrawConfig()
    {
        ImGui.TextUnformatted("Treasure Maps Settings");
        ImGui.Separator();
        ImGui.TextWrapped("Track treasure map gathering cooldown.");
    }

    public override void DrawStatus()
    {
        ImGui.TextColored(new System.Numerics.Vector4(1, 1, 1, 1), Name);
    }
}