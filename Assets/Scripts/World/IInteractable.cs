// IInteractable.cs
namespace RelaxingDrive.World
{
    /// <summary>
    /// Interface for objects the player can interact with.
    /// Buildings, NPCs, vehicles, and other interactive elements implement this.
    /// Follows Interface Segregation Principle.
    /// </summary>
    public interface IInteractable
    {
        /// <summary>
        /// Called when player presses E near this object.
        /// Trigger dialogue, open door, enter vehicle, etc.
        /// </summary>
        void Interact();
        
        /// <summary>
        /// Returns the prompt to show player.
        /// Example: "Press E to enter Bakery", "Press E to talk to Baker"
        /// </summary>
        string GetInteractionPrompt();
        
        /// <summary>
        /// Check if this object can currently be interacted with.
        /// Useful for locked doors, NPCs busy, etc.
        /// </summary>
        bool CanInteract();
    }
}