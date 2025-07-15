using Dalamud.Game.Command;
using Dalamud.IoC;
using Dalamud.Plugin;
using System.IO;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin.Services;
using SamplePlugin.Windows;
using ECommons;
using ECommons.Configuration;
using ECommons.Schedulers;

namespace SamplePlugin;

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
    [PluginService] internal static ICondition Condition { get; private set; } = null!;
    [PluginService] internal static IAddonLifecycle AddonLifecycle { get; private set; } = null!;
    [PluginService] internal static IGameInteropProvider GameInteropProvider { get; private set; } = null!;
    [PluginService] internal static IPluginLog PluginLog { get; private set; } = null!;

    public string Name => "Wahdori";
    
    // Core systems
    internal static Plugin Instance { get; private set; } = null!;
    internal Core.ModuleManager ModuleManager { get; private set; } = null!;
    internal Systems.OverlayManager OverlayManager { get; private set; } = null!;
    internal Systems.PayloadSystem PayloadSystem { get; private set; } = null!;
    internal Systems.TeleportManager TeleportManager { get; private set; } = null!;
    internal Systems.NotificationManager NotificationManager { get; private set; } = null!;
    internal Systems.LocalizationManager LocalizationManager { get; private set; } = null!;

    private const string MainCommand = "/wahdori";
    private const string ConfigCommand = "/wdcfg";

    public Configuration Configuration { get; init; }

    public readonly WindowSystem WindowSystem = new("Wahdori");
    private ConfigWindow ConfigWindow { get; init; }
    private MainWindow MainWindow { get; init; }

    public Plugin()
    {
        Instance = this;
        
        // Initialize ECommons - this provides additional functionality
        ECommonsMain.Init(PluginInterface, this, ECommons.Module.All);
        
        Configuration = PluginInterface.GetPluginConfig() as Configuration ?? new Configuration();

        // Initialize core systems
        InitializeSystems();
        
        // Keep the goat image for fun
        var goatImagePath = Path.Combine(PluginInterface.AssemblyLocation.Directory?.FullName!, "goat.png");

        ConfigWindow = new ConfigWindow(this);
        MainWindow = new MainWindow(this, goatImagePath);

        WindowSystem.AddWindow(ConfigWindow);
        WindowSystem.AddWindow(MainWindow);

        // Register commands
        CommandManager.AddHandler(MainCommand, new CommandInfo(OnMainCommand)
        {
            HelpMessage = "Open Wahdori main window - Currency alerts and daily duties tracker"
        });
        
        CommandManager.AddHandler(ConfigCommand, new CommandInfo(OnConfigCommand)
        {
            HelpMessage = "Open Wahdori configuration"
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

        Log.Information($"Wahdori v{PluginInterface.Manifest.AssemblyVersion} loaded successfully!");
    }

    private void InitializeSystems()
    {
        PayloadSystem = new Systems.PayloadSystem();
        TeleportManager = new Systems.TeleportManager();
        NotificationManager = new Systems.NotificationManager();
        LocalizationManager = new Systems.LocalizationManager();
        ModuleManager = new Core.ModuleManager(this);
        OverlayManager = new Systems.OverlayManager();
        
        // Initialize localization first
        LocalizationManager.Initialize();
        
        // Initialize notification system
        NotificationManager.Initialize();
        
        // Register all modules
        RegisterModules();
        
        // Initialize after registration
        ModuleManager.Initialize();
        OverlayManager.Initialize();
    }
    
    private void RegisterModules()
    {
        // Currency modules (from CurrencyAlert)
        ModuleManager.RegisterModule(new Modules.Currency.TomestoneModule(this));
        ModuleManager.RegisterModule(new Modules.Currency.GrandCompanyModule(this));
        ModuleManager.RegisterModule(new Modules.Currency.AlliedSealsModule(this));
        ModuleManager.RegisterModule(new Modules.Currency.CenturioSealsModule(this));
        ModuleManager.RegisterModule(new Modules.Currency.SackOfNutsModule(this));
        ModuleManager.RegisterModule(new Modules.Currency.WolfMarksModule(this));
        ModuleManager.RegisterModule(new Modules.Currency.TrophyCrystalsModule(this));
        ModuleManager.RegisterModule(new Modules.Currency.BicolorGemstonesModule(this));
        ModuleManager.RegisterModule(new Modules.Currency.SkybuildersScripModule(this));
        ModuleManager.RegisterModule(new Modules.Currency.PoeticTomestoneModule(this));
        ModuleManager.RegisterModule(new Modules.Currency.WhiteScripsModule(this));
        ModuleManager.RegisterModule(new Modules.Currency.PurpleScripsModule(this));
        
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
        OverlayManager.RefreshAll();
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
        
        WindowSystem.RemoveAllWindows();

        ConfigWindow.Dispose();
        MainWindow.Dispose();

        CommandManager.RemoveHandler(MainCommand);
        CommandManager.RemoveHandler(ConfigCommand);
        
        // Dispose systems in reverse order
        OverlayManager?.Dispose();
        ModuleManager?.Dispose();
        LocalizationManager?.Dispose();
        NotificationManager?.Dispose();
        TeleportManager?.Dispose();
        PayloadSystem?.Dispose();
        
        ECommonsMain.Dispose();
    }

    private void OnMainCommand(string command, string args)
    {
        ToggleMainUI();
    }
    
    private void OnConfigCommand(string command, string args)
    {
        ToggleConfigUI();
    }

    private void DrawUI()
    {
        WindowSystem.Draw();
    }

    public void ToggleConfigUI() => ConfigWindow.Toggle();
    public void ToggleMainUI() => MainWindow.Toggle();
}
