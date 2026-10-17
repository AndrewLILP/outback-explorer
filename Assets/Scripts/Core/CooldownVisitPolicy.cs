// CooldownVisitPolicy.cs
// Location: Assets/Scripts/Core/CooldownVisitPolicy.cs
// Assets live in: Assets/Data/VisitRules/
using UnityEngine;

namespace RelaxingDrive.Core
{
    /// <summary>
    /// A visit counts only if this zone hasn't counted one in the last N seconds.
    ///
    /// Combined with VisitZone's "must fully leave first" rule, this gives:
    ///   enter (counts) → leave → come straight back (doesn't count) → ...60 s later... → counts.
    /// The timer runs from the last COUNTED visit, so looping around repeatedly doesn't
    /// keep pushing the next count further away.
    ///
    /// Testing tips:
    /// - Keep two assets: VisitPolicy_Default (60 s) and VisitPolicy_QuickTest (5 s),
    ///   and swap which one VisitManager uses.
    /// - You can change Cooldown Seconds while in Play mode and it applies immediately.
    ///   CAUTION: unlike scene objects, ScriptableObject edits made in Play mode are KEPT
    ///   after you stop. That's why a separate QuickTest asset is safer than editing Default.
    /// </summary>
    [CreateAssetMenu(fileName = "VisitPolicy_Cooldown", menuName = "Sunday Drive/Visit Policy/Cooldown", order = 0)]
    public class CooldownVisitPolicy : VisitPolicy
    {
        [Tooltip("Seconds after a counted visit before the same zone can count again. " +
                 "60 = default; 5 is handy for quick testing.")]
        [SerializeField, Min(0f)] private float cooldownSeconds = 60f;

        public float CooldownSeconds => cooldownSeconds;

        public override bool ShouldCount(string zoneID, float secondsSinceLastCounted, out string reason)
        {
            if (secondsSinceLastCounted >= cooldownSeconds)
            {
                reason = null;
                return true;
            }

            reason = $"cooldown, {cooldownSeconds - secondsSinceLastCounted:0}s remaining";
            return false;
        }

        public override float SecondsUntilCountable(float secondsSinceLastCounted)
        {
            return Mathf.Max(0f, cooldownSeconds - secondsSinceLastCounted);
        }
    }
}