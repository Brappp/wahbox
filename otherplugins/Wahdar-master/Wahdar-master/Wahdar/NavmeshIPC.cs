using System;
using System.Numerics;
using Dalamud.Plugin.Services;
using Dalamud.Plugin;

namespace Wahdar;

public class NavmeshIPC : IDisposable
{
    private readonly IDalamudPluginInterface _pluginInterface;
    private readonly IPluginLog _log;
    
    public NavmeshIPC(IDalamudPluginInterface pluginInterface, IPluginLog log)
    {
        _pluginInterface = pluginInterface;
        _log = log;
    }
    
    public bool IsNavmeshReady()
    {
        try
        {
            var provider = _pluginInterface.GetIpcSubscriber<bool>("vnavmesh.Nav.IsReady");
            return provider.InvokeFunc();
        }
        catch (Exception ex)
        {
            _log.Debug($"Failed to check navmesh ready state: {ex.Message}");
            return false;
        }
    }
    
    public bool IsPathfindingInProgress()
    {
        try
        {
            var provider = _pluginInterface.GetIpcSubscriber<bool>("vnavmesh.SimpleMove.PathfindInProgress");
            return provider.InvokeFunc();
        }
        catch (Exception ex)
        {
            _log.Debug($"Failed to check pathfinding progress: {ex.Message}");
            return false;
        }
    }
    
    public bool PathfindAndMoveTo(Vector3 destination, bool fly = false)
    {
        try
        {
            if (!IsNavmeshReady())
            {
                _log.Warning("Navmesh is not ready for pathfinding");
                return false;
            }
            
            var provider = _pluginInterface.GetIpcSubscriber<Vector3, bool, bool>("vnavmesh.SimpleMove.PathfindAndMoveTo");
            return provider.InvokeFunc(destination, fly);
        }
        catch (Exception ex)
        {
            _log.Error($"Failed to pathfind to destination: {ex.Message}");
            return false;
        }
    }
    
    public void StopPathfinding()
    {
        try
        {
            var provider = _pluginInterface.GetIpcSubscriber<object>("vnavmesh.Path.Stop");
            provider.InvokeAction();
        }
        catch (Exception ex)
        {
            _log.Debug($"Failed to stop pathfinding: {ex.Message}");
        }
    }
    
    public void Dispose()
    {
        // No cleanup needed for IPC subscribers
    }
} 