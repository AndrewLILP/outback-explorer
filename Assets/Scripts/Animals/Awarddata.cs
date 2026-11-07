// AwardData.cs
// Location: Assets/Scripts/Animals/AwardData.cs
//
// One asset per award (First Sighting, Local Sightings, Remote Sightings).
// AwardManager loads every AwardData in Assets/Resources/Awards/.
//
// The default rule is "every species in the roster sighted in this tier".
// A future award with a different rule (Level 3, a photo award, a dusk-only
// award) can be a subclass that overrides IsEarned / WasJustEarnedBy -
// AwardManager doesn't need to change. That's the Open/Closed principle.
//
// SETUP: Assets > Create > Game > Award Data, saved in Assets/Resources/Awards/

using UnityEngine;

namespace RelaxingDrive.Animals
{
    [CreateAssetMenu(fileName = "New Award", menuName = "Game/Award Data", order = 3)]
    public class AwardData : ScriptableObject
    {
        [Header("Identity")]
        [Tooltip("Stable ID that never changes, e.g. award_first. Used for saves/analytics later.")]
        [SerializeField] private string awardId;

        [Tooltip("Shown as the pop-up title, e.g. 'First Sighting Award'")]
        [SerializeField] private string displayName;

        [Tooltip("Pop-up message. {count} is replaced with the number of species, e.g. 'All {count} animals sighted locally!'")]
        [TextArea(1, 3)]
        [SerializeField] private string popupMessage;

        [Header("Rule")]
        [Tooltip("The tier whose sightings this award counts")]
        [SerializeField] private SightingTier tier;

        [Tooltip("Display order (0 first)")]
        [SerializeField] private int sortOrder;

        [Header("Steam (not used yet)")]
        [Tooltip("Steam achievement API name. Leave empty until the achievement exists in Steamworks.")]
        [SerializeField] private string steamAchievementId;

        public string AwardId => awardId;
        public string DisplayName => displayName;
        public SightingTier Tier => tier;
        public int SortOrder => sortOrder;
        public string SteamAchievementId => steamAchievementId;

        /// <summary>Pop-up text with {count} filled in.</summary>
        public string GetPopupMessage(int speciesCount) =>
            popupMessage != null ? popupMessage.Replace("{count}", speciesCount.ToString()) : string.Empty;

        /// <summary>True if the award's condition is met right now.</summary>
        public virtual bool IsEarned(SightingManager sightings)
        {
            if (sightings == null || sightings.Roster == null || sightings.Roster.Count == 0) return false;
            return sightings.GetSightingCount(tier) >= sightings.Roster.Count;
        }

        /// <summary>
        /// True only on the sighting that completes the award. A tier's count can
        /// only go up through a NEW sighting in that tier, so "new sighting in my
        /// tier AND now complete" happens exactly once. No extra save data needed.
        /// </summary>
        public virtual bool WasJustEarnedBy(SightingResult result, SightingManager sightings)
        {
            return result.IsNew && result.Tier == tier && IsEarned(sightings);
        }
    }
}