// IActivatable.cs
namespace RelaxingDrive.World
{
    /// <summary>
    /// Interface for objects that can be activated based on conditions.
    /// Follows Interface Segregation Principle - defines only activation contract.
    /// </summary>
    public interface IActivatable
    {
        /// <summary>
        /// Activates the object, making it visible/functional in the game world.
        /// </summary>
        void Activate();

        /// <summary>
        /// Deactivates the object, hiding it from the game world.
        /// Optional: May not be used if activation is permanent.
        /// </summary>
        void Deactivate();

        /// <summary>
        /// Checks if the object is currently active.
        /// </summary>
        bool IsActive { get; }
    }
}