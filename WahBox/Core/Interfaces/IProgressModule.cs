namespace WahBox.Core.Interfaces;

/// <summary>
/// Interface for modules that track progress (e.g., daily/weekly tasks)
/// </summary>
public interface IProgressModule : IModule
{
    /// <summary>
    /// Gets the current progress value
    /// </summary>
    int Current { get; }
    
    /// <summary>
    /// Gets the maximum progress value
    /// </summary>
    int Maximum { get; }
    
    /// <summary>
    /// Gets the progress as a percentage (0.0 to 1.0)
    /// </summary>
    float Progress { get; }
}
