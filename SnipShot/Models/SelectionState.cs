namespace SnipShot.Models
{
    /// <summary>
    /// Estados posibles de la selección en ventanas de captura
    /// </summary>
    public enum SelectionState
    {
        None,                   // No selection started
        Selecting,              // User is dragging to create selection
        Selected,               // Selection is finalized and ready for adjustment
        Adjusting               // User is dragging a resize handle
    }
}
