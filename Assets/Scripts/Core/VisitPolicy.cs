// VisitPolicy.cs
// Location: Assets/Scripts/Core/VisitPolicy.cs
using UnityEngine;

namespace RelaxingDrive.Core
{
    /// <summary>
    /// STRATEGY PATTERN: decides whether entering a zone counts as a visit.
    ///
    /// VisitManager asks "should this count?" and doesn't care HOW the answer is reached.
    /// Each rule (cooldown, dwell time, once-per-session...) is its own subclass, so new
    /// rules are added without touching VisitManager (Open/Closed principle).
    ///
    /// Why an abstract ScriptableObject instead of a C# interface?
    /// Unity's Inspector can't serialize interface references, so you couldn't drag an
    /// "IVisitPolicy" into a slot. A ScriptableObject base class gives the same
    /// swap-ability AND makes each rule a tweakable asset you can swap at runtime.
    /// </summary>
    public abstract class VisitPolicy : ScriptableObject
    {
        /// <summary>
        /// Should this entry count as a visit?
        /// </summary>
        /// <param name="zoneID">The zone being entered.</param>
        /// <param name="secondsSinceLastCounted">Seconds since this zone last COUNTED a visit
        /// this session. float.PositiveInfinity if it never has.</param>
        /// <param name="reason">If false, a short human-readable reason for debug logs.</param>
        public abstract bool ShouldCount(string zoneID, float secondsSinceLastCounted, out string reason);

        /// <summary>
        /// How many seconds until an entry would count again (0 = it would count now).
        /// Used for debug display only; rules without a timer can leave the default.
        /// </summary>
        public virtual float SecondsUntilCountable(float secondsSinceLastCounted) => 0f;
    }
}