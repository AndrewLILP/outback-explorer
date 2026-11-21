// BalloonPathPoint.cs
// Location: Assets/Scripts/World/Balloon/BalloonPathPoint.cs
//
// OPTIONAL. Add to a PathRoute child point to change how high the balloon may
// climb above THAT point. Points without it use the balloon's default (15 m).
// The point itself is the floor - place it at the lowest height you want there.
// Between two points the ceiling blends smoothly.

using UnityEngine;

namespace RelaxingDrive.World
{
    public class BalloonPathPoint : MonoBehaviour
    {
        [Tooltip("Metres the balloon may climb above this point. 0 = use the balloon's Default Max Height.")]
        [SerializeField, Min(0f)] private float maxHeight = 0f;

        public float MaxHeight => maxHeight;
    }
}