// SightingTypes.cs
// Location: Assets/Scripts/Animals/SightingTypes.cs
//
// Small shared types for the sighting system. They live together in one file
// because each is only a few lines and they are always used together.
//
//   SightingRegion  - set on each placed animal (Local = start area / NSW,
//                     Remote = wider world / rest of Australia)
//   SightingTier    - which award tier a sighting counted toward
//   SightingContext - everything a discovery trigger knows at the moment of
//                     a sighting (passed IN to SightingManager)
//   SightingResult  - what SightingManager decided (passed OUT to the UI)
//
// Why structs? They are tiny, immutable "message" objects created once per
// sighting. A readonly struct can't be changed after creation, so no system
// can accidentally edit the context another system is reading.

using UnityEngine;

namespace RelaxingDrive.Animals
{
    /// <summary>Where a placed animal lives. Set per animal in the Inspector.</summary>
    public enum SightingRegion
    {
        Local,  // Start area guarded by water (NSW)
        Remote  // Wider world (rest of Australia)
    }

    /// <summary>The three award tiers, in the order players earn them.</summary>
    public enum SightingTier
    {
        First,
        Local,
        Remote
    }

    /// <summary>
    /// Data about one sighting, built by whatever detected it (today: proximity in
    /// AnimalController; later: stopping, photos, honking). New trigger types only
    /// need to build one of these and call SightingManager.ReportSighting().
    /// </summary>
    public readonly struct SightingContext
    {
        public readonly AnimalData Animal;
        public readonly SightingRegion Region;
        public readonly bool PlayerOnFoot;
        public readonly Vector3 Position;

        public SightingContext(AnimalData animal, SightingRegion region, bool playerOnFoot, Vector3 position)
        {
            Animal = animal;
            Region = region;
            PlayerOnFoot = playerOnFoot;
            Position = position;
        }
    }

    /// <summary>
    /// SightingManager's verdict on a sighting.
    /// Tier  = which tier it counted toward.
    /// IsNew = true if this was the first time for this species in that tier.
    /// </summary>
    public readonly struct SightingResult
    {
        public readonly SightingTier Tier;
        public readonly bool IsNew;

        public SightingResult(SightingTier tier, bool isNew)
        {
            Tier = tier;
            IsNew = isNew;
        }

        public override string ToString() => $"{Tier} ({(IsNew ? "new" : "repeat")})";
    }
}