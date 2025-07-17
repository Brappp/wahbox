using System;
using System.Collections.Generic;
using System.Linq;
using WahBox.Core.Interfaces;

namespace WahBox.Core;

public class ModuleManager : IDisposable
{
    private readonly List<IModule> _modules = new();
    private readonly Plugin _plugin;

    public IReadOnlyList<IModule> Modules => _modules.AsReadOnly();

    public ModuleManager(Plugin plugin)
    {
        _plugin = plugin;
    }

    public void RegisterModule(IModule module)
    {
        if (_modules.Any(m => m.Name == module.Name))
        {
            Plugin.Log.Warning($"Module {module.Name} is already registered");
            return;
        }

        _modules.Add(module);
        
        // Load existing configuration for this module
        module.LoadConfiguration();
        
        // If this is a new module (not in EnabledModules), add it as enabled by default
        if (!_plugin.Configuration.EnabledModules.Contains(module.Name))
        {
            _plugin.Configuration.EnabledModules.Add(module.Name);
            module.IsEnabled = true;
            module.SaveConfiguration();
        }
        
        Plugin.Log.Information($"Registered module: {module.Name} (Enabled: {module.IsEnabled})");
    }

    public void Initialize()
    {
        foreach (var module in _modules)
        {
            try
            {
                module.Initialize();
                Plugin.Log.Debug($"Initialized module: {module.Name}");
            }
            catch (Exception ex)
            {
                Plugin.Log.Error(ex, $"Failed to initialize module: {module.Name}");
            }
        }
    }

    public void LoadAll()
    {
        foreach (var module in _modules.Where(m => m.IsEnabled))
        {
            try
            {
                module.Load();
                Plugin.Log.Debug($"Loaded module: {module.Name}");
            }
            catch (Exception ex)
            {
                Plugin.Log.Error(ex, $"Failed to load module: {module.Name}");
            }
        }
    }

    public void UnloadAll()
    {
        foreach (var module in _modules)
        {
            try
            {
                module.Unload();
                Plugin.Log.Debug($"Unloaded module: {module.Name}");
            }
            catch (Exception ex)
            {
                Plugin.Log.Error(ex, $"Failed to unload module: {module.Name}");
            }
        }
    }

    public void UpdateAll()
    {
        foreach (var module in _modules.Where(m => m.IsEnabled))
        {
            try
            {
                module.Update();
            }
            catch (Exception ex)
            {
                Plugin.Log.Error(ex, $"Failed to update module: {module.Name}");
            }
        }
    }

    public void ResetAll()
    {
        foreach (var module in _modules)
        {
            try
            {
                module.Reset();
                Plugin.Log.Debug($"Reset module: {module.Name}");
            }
            catch (Exception ex)
            {
                Plugin.Log.Error(ex, $"Failed to reset module: {module.Name}");
            }
        }
    }

    public void OnTerritoryChanged(ushort territory)
    {
        // Modules can override Update() to handle territory changes
        Plugin.Log.Debug($"Territory changed to {territory}");
    }

    public IEnumerable<IModule> GetModulesByType(ModuleType type)
    {
        return _modules.Where(m => m.Type == type);
    }

    public IEnumerable<IModule> GetModules()
    {
        return _modules;
    }

    public T? GetModule<T>() where T : IModule
    {
        return _modules.OfType<T>().FirstOrDefault();
    }

    public void Dispose()
    {
        foreach (var module in _modules)
        {
            try
            {
                module.Dispose();
            }
            catch (Exception ex)
            {
                Plugin.Log.Error(ex, $"Failed to dispose module: {module.Name}");
            }
        }
        _modules.Clear();
    }
} 