namespace WahBox.Core.Interfaces;

/// <summary>
/// Interface for modules that want to display additional status details
/// </summary>
public interface IDetailedStatus
{
    /// <summary>
    /// Gets detailed status text to display alongside the progress bar
    /// </summary>
    string GetDetailedStatus();
}
