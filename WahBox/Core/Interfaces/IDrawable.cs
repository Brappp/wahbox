namespace WahBox.Core.Interfaces;

/// <summary>
/// Interface for modules that can draw their content directly in the main window
/// </summary>
public interface IDrawable
{
    /// <summary>
    /// Draw the module's content
    /// </summary>
    void Draw();
}
