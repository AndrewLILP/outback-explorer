// IActivePlayerProvider.cs
// Location: Assets/Scripts/Core/IActivePlayerProvider.cs
using UnityEngine;
 
namespace RelaxingDrive.Core
{
    /// <summary>
    /// Tells other systems where the player currently "is" — the car while driving,
    /// the walking character while on foot.
    ///
    /// Why an interface? (Dependency Inversion — the "D" in SOLID)
    /// VisitZone needs to know the player's position, but it shouldn't care HOW the
    /// game decides that. By depending on this small contract instead of on
    /// PlayerStateManager directly, VisitZone keeps working even if player-state
    /// logic is rewritten, and you could hand it a fake provider for testing.
    /// </summary>
    public interface IActivePlayerProvider
    {
        /// <summary>The transform the player is currently controlling (car or walker). Null if not ready yet.</summary>
        Transform ActivePlayerTransform { get; }
 
        /// <summary>True while the player is walking. Used later by the on-foot visit bonus (story #4).</summary>
        bool IsOnFoot { get; }
    }
}
 