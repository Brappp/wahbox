using System;
using System.IO;
using System.Numerics;
using Dalamud.Game.Command;
using Dalamud.IoC;
using Dalamud.Plugin;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin.Services;
using ZoomiesPlugin.UI;
using ZoomiesPlugin.Helpers;
using ZoomiesPlugin.Renderers;

namespace ZoomiesPlugin.Core
{
    public sealed class Plugin : IDalamudPlugin
    {
        public string Name => "Zoomies";

        // Plugin services
        [PluginService] internal static IDalamudPluginInterface PluginInterface { get; private set; } = null!;
        [PluginService] internal static ITextureProvider TextureProvider { get; private set; } = null!;
        [PluginService] internal static ICommandManager CommandManager { get; private set; } = null!;
        [PluginService] internal static IClientState ClientState { get; private set; } = null!;
        [PluginService] internal static IDataManager DataManager { get; private set; } = null!;
        [PluginService] internal static IPluginLog Log { get; private set; } = null!;

        private const string ZoomiesCommandName = "/zoomies";

        public Configuration Configuration { get; init; }

        public readonly WindowSystem WindowSystem = new("ZoomiesPlugin");

        // UI windows
        private SpeedometerWindow SpeedometerWindow { get; init; }
        private NyanCatWindow NyanCatWindow { get; init; }
        private DebugWindow DebugWindow { get; init; }
        private ConfigWindow ConfigWindow { get; init; }

        // Texture info for Nyan Cat
        private static IntPtr nyanCatTextureHandle = IntPtr.Zero;
        private static Vector2 nyanCatTextureSize = new Vector2(0, 0);

        // Texture loading
        private string textureToLoad = string.Empty;
        private bool isCustomTexture = false;

        public Plugin()
        {
            // Load settings
            Configuration = PluginInterface.GetPluginConfig() as Configuration ?? new Configuration();

            // Check for necessary images
            CheckForNyanCatImage();
            LoadUserImages();

            // Create UI windows
            SpeedometerWindow = new SpeedometerWindow();
            NyanCatWindow = new NyanCatWindow();
            DebugWindow = new DebugWindow(SpeedometerWindow.GetCalculator(), this);
            ConfigWindow = new ConfigWindow(this);

            // Register windows
            WindowSystem.AddWindow(SpeedometerWindow);
            WindowSystem.AddWindow(NyanCatWindow);
            WindowSystem.AddWindow(DebugWindow);
            WindowSystem.AddWindow(ConfigWindow);

            // Register command
            CommandManager.AddHandler(ZoomiesCommandName, new CommandInfo(OnZoomiesCommand)
            {
                HelpMessage = "Toggle the Zoomies speedometer or open the UI if using /zoomies config"
            });

            // Register event handlers
            PluginInterface.UiBuilder.Draw += DrawUI;
            PluginInterface.UiBuilder.OpenConfigUi += ToggleConfigUI;
            PluginInterface.UiBuilder.OpenMainUi += ToggleMainUI;
            ClientState.Login += OnLogin;
            ClientState.Logout += OnLogout;

            // Initialize UI state
            if (ClientState.IsLoggedIn)
            {
                SetInitialState();
            }
            else
            {
                HideAllSpeedometers();
                ConfigWindow.IsOpen = false;
                DebugWindow.IsOpen = false;
            }

            Log.Information($"===Zoomies Plugin loaded===");
        }

        private void SetInitialState()
        {
            // Start with all windows closed
            SpeedometerWindow.IsOpen = false;
            NyanCatWindow.IsOpen = false;
            DebugWindow.IsOpen = false;

            // Show speedometer if configured
            if (Configuration.ShowSpeedometerOnStartup)
            {
                switch (Configuration.SelectedSpeedometerType)
                {
                    case 0:
                        SpeedometerWindow.IsOpen = true;
                        break;
                    case 1:
                        NyanCatWindow.IsOpen = true;
                        break;
                }
            }
        }

        // Game login event
        private void OnLogin()
        {
            Log.Information("Login detected, showing speedometer if configured.");

            if (Configuration.ShowSpeedometerOnStartup)
            {
                switch (Configuration.SelectedSpeedometerType)
                {
                    case 0:
                        SpeedometerWindow.IsOpen = true;
                        break;
                    case 1:
                        NyanCatWindow.IsOpen = true;
                        break;
                }
            }
        }

        // Game logout event
        private void OnLogout(int type, int code)
        {
            Log.Information($"Logout detected. Type: {type}, Code: {code}");

            HideAllSpeedometers();
            ConfigWindow.IsOpen = false;
            DebugWindow.IsOpen = false;
        }

        // Look for nyan.png in plugin folder
        private void CheckForNyanCatImage()
        {
            try
            {
                string pluginPath = PluginInterface.AssemblyLocation.DirectoryName ?? string.Empty;
                string imagePath = Path.Combine(pluginPath, "nyan.png");

                Log.Information($"Looking for nyan.png at: {imagePath}");

                if (File.Exists(imagePath))
                {
                    LoadTextureOnNextDraw(imagePath, false);
                }
                else
                {
                    Log.Error($"Nyan cat image not found at: {imagePath}");
                    Log.Information("Using drawn Nyan Cat as fallback");
                }
            }
            catch (Exception ex)
            {
                Log.Error($"Error checking for nyan.png: {ex.Message}");
            }
        }

        // Check for user-provided custom images
        private void LoadUserImages()
        {
            try
            {
                string configDir = PluginInterface.GetPluginConfigDirectory();
                string imagesDir = Path.Combine(configDir, "images");

                if (!Directory.Exists(imagesDir))
                {
                    Directory.CreateDirectory(imagesDir);
                    Log.Information($"Created images directory at: {imagesDir}");
                    return;
                }

                string customNyanPath = Path.Combine(imagesDir, "nyan.png");
                if (File.Exists(customNyanPath))
                {
                    Log.Information($"Found custom nyan.png at: {customNyanPath}");
                    LoadTextureOnNextDraw(customNyanPath, true);
                }
            }
            catch (Exception ex)
            {
                Log.Error($"Error loading user images: {ex.Message}");
            }
        }

        // Queue texture loading for next draw cycle
        private void LoadTextureOnNextDraw(string path, bool isCustom)
        {
            textureToLoad = path;
            isCustomTexture = isCustom;
        }

        // Texture access methods
        public static IntPtr GetNyanCatTextureHandle()
        {
            return nyanCatTextureHandle;
        }

        public static Vector2 GetNyanCatTextureSize()
        {
            return nyanCatTextureSize;
        }

        public void Dispose()
        {
            // Remove windows
            WindowSystem.RemoveAllWindows();

            // Clean up resources
            SpeedometerWindow.Dispose();
            NyanCatWindow.Dispose();
            DebugWindow.Dispose();
            ConfigWindow.Dispose();

            // Unsubscribe events
            ClientState.Login -= OnLogin;
            ClientState.Logout -= OnLogout;
            PluginInterface.UiBuilder.Draw -= DrawUI;
            PluginInterface.UiBuilder.OpenConfigUi -= ToggleConfigUI;
            PluginInterface.UiBuilder.OpenMainUi -= ToggleMainUI;

            // Remove command
            CommandManager.RemoveHandler(ZoomiesCommandName);
        }

        // Handle /zoomies command
        private void OnZoomiesCommand(string command, string args)
        {
            if (args.Trim().ToLower() == "config")
            {
                ConfigWindow.IsOpen = true;
            }
            else
            {
                ToggleSpeedometer();
            }
        }

        // Handle UI drawing
        private void DrawUI()
        {
            // Load texture if needed
            if (!string.IsNullOrEmpty(textureToLoad))
            {
                try
                {
                    var textureResult = TextureProvider.GetFromFile(textureToLoad);
                    if (textureResult != null)
                    {
                        var texture = textureResult.GetWrapOrDefault();
                        if (texture != null)
                        {
                            nyanCatTextureHandle = texture.ImGuiHandle;
                            nyanCatTextureSize = new Vector2(texture.Width, texture.Height);
                            Log.Information($"Successfully loaded {(isCustomTexture ? "custom " : "")}nyan.png texture");
                        }
                    }
                }
                catch (Exception ex)
                {
                    Log.Error($"Exception during texture loading: {ex.Message}");
                    if (!isCustomTexture)
                        Log.Information("Using drawn Nyan Cat as fallback");
                }

                textureToLoad = string.Empty;
            }

            WindowSystem.Draw();
        }

        // UI toggle methods
        public void ToggleConfigUI()
        {
            ConfigWindow.IsOpen = !ConfigWindow.IsOpen;
        }

        public void ToggleMainUI()
        {
            ToggleSpeedometer();
        }

        public void ToggleDebugUI()
        {
            DebugWindow.IsOpen = !DebugWindow.IsOpen;
        }

        // Speedometer controls
        public void ToggleSpeedometer()
        {
            bool isAnyVisible = IsAnySpeedometerVisible();

            if (isAnyVisible)
            {
                HideAllSpeedometers();
            }
            else
            {
                switch (Configuration.SelectedSpeedometerType)
                {
                    case 0:
                        SwitchToClassicSpeedometer();
                        break;
                    case 1:
                        SwitchToNyanSpeedometer();
                        break;
                }
            }
        }

        public void SwitchToClassicSpeedometer()
        {
            NyanCatWindow.IsOpen = false;
            SpeedometerWindow.IsOpen = true;

            Configuration.SelectedSpeedometerType = 0;
            Configuration.ShowSpeedometerOnStartup = true;
            Configuration.Save();
        }

        public void SwitchToNyanSpeedometer()
        {
            SpeedometerWindow.IsOpen = false;
            NyanCatWindow.IsOpen = true;

            Configuration.SelectedSpeedometerType = 1;
            Configuration.ShowSpeedometerOnStartup = true;
            Configuration.Save();
        }

        public void HideAllSpeedometers()
        {
            SpeedometerWindow.IsOpen = false;
            NyanCatWindow.IsOpen = false;

            Configuration.ShowSpeedometerOnStartup = false;
            Configuration.Save();
        }

        public bool IsAnySpeedometerVisible()
        {
            return SpeedometerWindow.IsOpen || NyanCatWindow.IsOpen;
        }

        // Setting update methods
        public void UpdateMaxSpeed(float maxSpeed)
        {
            SpeedometerWindow.UpdateMaxSpeed(maxSpeed);
            NyanCatWindow.UpdateMaxSpeed(maxSpeed);
        }

        public void UpdateRedlineStart(float redlineStart)
        {
            SpeedometerWindow.UpdateRedlineStart(redlineStart);
        }

        public void UpdateDamping(float damping)
        {
            SpeedometerWindow.UpdateDamping(damping);
            NyanCatWindow.UpdateDamping(damping);
        }
    }
}
