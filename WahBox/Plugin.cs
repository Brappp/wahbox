using Dalamud.Game.Command;
using Dalamud.IoC;
using Dalamud.Plugin;
using System.IO;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin.Services;
using WahBox.Windows;
using WahBox.Core.Interfaces;
using ECommons;
using ECommons.Configuration;
using ECommons.Schedulers;

namespace WahBox;

public sealed class Plugin : IDalamudPlugin
{
    // Keep the original service injection pattern
    [PluginService] internal static IDalamudPluginInterface PluginInterface { get; private set; } = null!;
    [PluginService] internal static ITextureProvider TextureProvider { get; private set; } = null!;
    [PluginService] internal static ICommandManager CommandManager { get; private set; } = null!;
    [PluginService] internal static IClientState ClientState { get; private set; } = null!;
    [PluginService] internal static IDataManager DataManager { get; private set; } = null!;
    [PluginService] internal static IPluginLog Log { get; private set; } = null!;
    [PluginService] internal static IFramework Framework { get; private set; } = null!;
    [PluginService] internal static IChatGui ChatGui { get; private set; } = null!;
    [PluginService] internal static IGameGui GameGui { get; private set; } = null!;
    [PluginService] internal static ICondition Condition { get; private set; } = null!;
    [PluginService] internal static IAddonLifecycle AddonLifecycle { get; private set; } = null!;
    [PluginService] internal static IGameInteropProvider GameInteropProvider { get; private set; } = null!;
    [PluginService] internal static IPluginLog PluginLog { get; private set; } = null!;
    [PluginService] internal static INotificationManager DalamudNotificationManager { get; private set; } = null!;

    public string Name => "WahBox";
    
    // Core systems
    internal static Plugin Instance { get; private set; } = null!;
    internal Core.ModuleManager ModuleManager { get; private set; } = null!;
    internal Systems.TeleportManager TeleportManager { get; private set; } = null!;
    internal Systems.NotificationManager NotificationManager { get; private set; } = null!;

    private const string MainCommand = "/wahbox";

    public Configuration Configuration { get; init; }

    public readonly WindowSystem WindowSystem = new("WahBox");
    private MainWindow MainWindow { get; init; }

    public Plugin()
    {
        Instance = this;
        
        // Initialize ECommons - this provides additional functionality
        ECommonsMain.Init(PluginInterface, this, ECommons.Module.All);
        
        Configuration = PluginInterface.GetPluginConfig() as Configuration ?? new Configuration();
        
        // Debug: Log configuration loading
        Log.Information($"Configuration loaded. Version: {Configuration.Version}, Enabled modules: {Configuration.EnabledModules.Count}, Module configs: {Configuration.ModuleConfigs.Count}");

        // Initialize core systems
        InitializeSystems();
        
        // Keep the goat image for fun
        MainWindow = new MainWindow(this);
        WindowSystem.AddWindow(MainWindow);

        // Register commands
        CommandManager.AddHandler(MainCommand, new CommandInfo(OnMainCommand)
        {
            HelpMessage = "Open WahBox main window - Currency alerts and daily duties tracker"
        });

        PluginInterface.UiBuilder.Draw += DrawUI;
        PluginInterface.UiBuilder.OpenConfigUi += ToggleConfigUI;
        PluginInterface.UiBuilder.OpenMainUi += ToggleMainUI;

        // Register event handlers
        RegisterEventHandlers();
        
        // Load modules if logged in
        if (ClientState.IsLoggedIn)
        {
            _ = new TickScheduler(LoadCharacterData);
        }

        Log.Information($"WahBox v{PluginInterface.Manifest.AssemblyVersion} loaded successfully!");
    }

    private void InitializeSystems()
    {
        TeleportManager = new Systems.TeleportManager();
        NotificationManager = new Systems.NotificationManager();
        ModuleManager = new Core.ModuleManager(this);
        
        // Initialize localization first
        // (Localization is simple now, no initialization needed)
        
        // Initialize notification system
        NotificationManager.Initialize();
        
        // Register all modules
        RegisterModules();
        
        // Initialize after registration
        ModuleManager.Initialize();
    }
    
    private void RegisterModules()
    {
        // Currency modules (from CurrencyAlert)
        // Tomestones
        ModuleManager.RegisterModule(new Modules.Currency.PoeticTomestoneModule(this));
        ModuleManager.RegisterModule(new Modules.Currency.AestheticsTomestoneModule(this));
        ModuleManager.RegisterModule(new Modules.Currency.HeliometryTomestoneModule(this));
        
        // Grand Company
        ModuleManager.RegisterModule(new Modules.Currency.GrandCompanyModule(this));
        
        // Hunt Currencies
        ModuleManager.RegisterModule(new Modules.Currency.AlliedSealsModule(this));
        ModuleManager.RegisterModule(new Modules.Currency.CenturioSealsModule(this));
        ModuleManager.RegisterModule(new Modules.Currency.SackOfNutsModule(this));
        
        // PvP
        ModuleManager.RegisterModule(new Modules.Currency.WolfMarksModule(this));
        ModuleManager.RegisterModule(new Modules.Currency.TrophyCrystalsModule(this));
        
        // Other Currencies
        ModuleManager.RegisterModule(new Modules.Currency.BicolorGemstonesModule(this));
        ModuleManager.RegisterModule(new Modules.Currency.SkybuildersScripModule(this));
        ModuleManager.RegisterModule(new Modules.Currency.WhiteScripsModule(this));
        ModuleManager.RegisterModule(new Modules.Currency.PurpleScripsModule(this));
        ModuleManager.RegisterModule(new Modules.Currency.MGPModule(this));
        
        // Daily modules (from DailyDuty)
        ModuleManager.RegisterModule(new Modules.Daily.DutyRouletteModule(this));
        ModuleManager.RegisterModule(new Modules.Daily.BeastTribeModule(this));
        ModuleManager.RegisterModule(new Modules.Daily.MiniCactpotModule(this));
        ModuleManager.RegisterModule(new Modules.Daily.GrandCompanyProvisionModule(this));
        ModuleManager.RegisterModule(new Modules.Daily.GrandCompanySupplyModule(this));
        ModuleManager.RegisterModule(new Modules.Daily.TribalQuestsModule(this));
        ModuleManager.RegisterModule(new Modules.Daily.LevequestsModule(this));
        ModuleManager.RegisterModule(new Modules.Daily.DailyHuntBillsModule(this));
        
        // Weekly modules
        ModuleManager.RegisterModule(new Modules.Weekly.WondrousTailsModule(this));
        ModuleManager.RegisterModule(new Modules.Weekly.CustomDeliveryModule(this));
        ModuleManager.RegisterModule(new Modules.Weekly.FashionReportModule(this));
        ModuleManager.RegisterModule(new Modules.Weekly.DomanEnclaveModule(this));
        ModuleManager.RegisterModule(new Modules.Weekly.JumboCactpotModule(this));
        ModuleManager.RegisterModule(new Modules.Weekly.ChallengeLogModule(this));
        ModuleManager.RegisterModule(new Modules.Weekly.HuntMarkWeeklyModule(this));
        ModuleManager.RegisterModule(new Modules.Weekly.MaskedCarnivaleModule(this));
        
        // Special modules
        ModuleManager.RegisterModule(new Modules.Special.TreasureMapsModule(this));
        ModuleManager.RegisterModule(new Modules.Special.RetainerVenturesModule(this));
        
        // Utility modules
        ModuleManager.RegisterModule(new Modules.Utility.RadarModule(this));
        ModuleManager.RegisterModule(new Modules.Utility.SpeedometerModule(this));
        
        // Inventory module
        ModuleManager.RegisterModule(new Modules.Inventory.InventoryManagementModule(this));
    }
    
    private void RegisterEventHandlers()
    {
        ClientState.Login += OnLogin;
        ClientState.Logout += OnLogout;
        ClientState.TerritoryChanged += OnTerritoryChanged;
        Framework.Update += OnFrameworkUpdate;
    }
    
    private void UnregisterEventHandlers()
    {
        ClientState.Login -= OnLogin;
        ClientState.Logout -= OnLogout;
        ClientState.TerritoryChanged -= OnTerritoryChanged;
        Framework.Update -= OnFrameworkUpdate;
    }
    
    private void OnLogin()
    {
        _ = new TickScheduler(LoadCharacterData);
    }
    
    private void OnLogout(int type, int code)
    {
        SaveCharacterData();
        ModuleManager.UnloadAll();
    }
    
    private void OnTerritoryChanged(ushort territory)
    {
        ModuleManager.OnTerritoryChanged(territory);
    }
    
    private void OnFrameworkUpdate(IFramework framework)
    {
        ModuleManager.UpdateAll();
    }
    
    private void LoadCharacterData()
    {
        if (!ClientState.IsLoggedIn || ClientState.LocalContentId == 0) return;
        
        Configuration.LoadCharacterData(ClientState.LocalContentId);
        ModuleManager.LoadAll();
    }
    
    private void SaveCharacterData()
    {
        if (ClientState.LocalContentId == 0) return;
        
        Configuration.SaveCharacterData(ClientState.LocalContentId);
    }

    public void Dispose()
    {
        SaveCharacterData();
        UnregisterEventHandlers();
        
        // Dispose systems in reverse order (modules first so they can clean up their windows)
        ModuleManager?.Dispose();
        // Removed localization
        NotificationManager?.Dispose();
        TeleportManager?.Dispose();
        
        // Remove any remaining windows after modules have cleaned up
        WindowSystem.RemoveAllWindows();

        MainWindow.Dispose();

        CommandManager.RemoveHandler(MainCommand);
        
        ECommonsMain.Dispose();
    }

    private void OnMainCommand(string command, string args)
    {
        ToggleMainUI();
    }
    


    private void DrawUI()
    {
        WindowSystem.Draw();
        
        // Draw in-game overlays for active utility modules
        if (ClientState.LocalPlayer != null)
        {
            DrawInGameOverlays();
        }
    }
    
    private void DrawInGameOverlays()
    {
        foreach (var module in ModuleManager.GetModules())
        {
            if (module is Modules.Utility.RadarModule radarModule && 
                radarModule.Status == Core.Interfaces.ModuleStatus.Active && 
                radarModule.EnableInGameDrawing)
            {
                radarModule.DrawInGameOverlay();
            }
        }
    }

    public void ToggleConfigUI() => MainWindow.Toggle();
    public void ToggleMainUI() => MainWindow.Toggle();
}
