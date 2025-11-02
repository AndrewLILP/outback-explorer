// PlayerState.cs
using UnityEngine;

namespace RelaxingDrive.Player
{
    /// <summary>
    /// Base class for player states (Driving, OnFoot).
    /// Implements the State Pattern for clean state management.
    /// Each state handles its own input, movement, and camera settings.
    /// </summary>
    public abstract class PlayerState
    {
        protected PlayerStateManager stateManager;
        
        /// <summary>
        /// Constructor - receives reference to state manager
        /// </summary>
        public PlayerState(PlayerStateManager manager)
        {
            stateManager = manager;
        }
        
        /// <summary>
        /// Called once when entering this state.
        /// Setup camera, enable/disable components, etc.
        /// </summary>
        public abstract void Enter();
        
        /// <summary>
        /// Called every frame while in this state.
        /// Handle input and update logic here.
        /// </summary>
        public abstract void Update();
        
        /// <summary>
        /// Called once when exiting this state.
        /// Cleanup, disable components, etc.
        /// </summary>
        public abstract void Exit();
        
        /// <summary>
        /// Optional: Handle fixed update (physics)
        /// </summary>
        public virtual void FixedUpdate() { }
    }
}