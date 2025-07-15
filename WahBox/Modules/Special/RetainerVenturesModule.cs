using System;
using System.Collections.Generic;
using WahBox.Core;
using WahBox.Core.Interfaces;
using ImGuiNET;

namespace WahBox.Modules.Special;

public class RetainerVenturesModule : BaseModule
{
    public override string Name => "Retainer Ventures";
    public override ModuleType Type => ModuleType.Special;

    public RetainerVenturesModule(Plugin plugin) : base(plugin)
    {
        IconId = 60425; // Module icon
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
        ImGui.TextUnformatted("Retainer Ventures Settings");
        ImGui.Separator();
        ImGui.TextWrapped("Track retainer venture completion times.");
    }

    public override void DrawStatus()
    {
        ImGui.TextColored(new System.Numerics.Vector4(1, 1, 1, 1), Name);
    }
}