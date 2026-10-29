using UnityEngine;

namespace LudoGame
{
    /// <summary>
    /// Finds which glowing pawn a tap is on. Only pawns are tap targets: the landing markers are just a preview,
    /// so tapping a pawn always moves that pawn, even when another pawn's marker sits on its square.
    /// </summary>
    public static class PawnPicker
    {
        /// <summary>How far from a pawn a tap still counts, in board squares: normal, and with "Generous taps".</summary>
        public const float NormalReach = 0.65f;
        public const float GenerousReach = 1.75f;

        /// <summary>
        /// The selectable pawn nearest to <paramref name="tap"/> (screen pixels) within reach, or null.
        /// Reach defaults to the "Generous taps" setting.
        /// </summary>
        public static Pawn3D Pick(Vector2 tap, Pawn3D[,] pawns, CameraRig rig, float reach = -1f)
        {
            if (reach < 0f) reach = GameSettings.GenerousTaps ? GenerousReach : NormalReach;
            Pawn3D best = null;
            float bestDistance = (rig.WorldToScreen(Vector3.zero) - rig.WorldToScreen(Vector3.right)).magnitude * reach;
            foreach (var pawn in pawns)
            {
                if (pawn == null || !pawn.IsSelectable) continue;
                ScreenSpan(pawn, rig, out var foot, out var head);
                // Distance to the pawn's whole height on screen (base to head), not just one point.
                float d = DistanceToSegment(tap, foot, head);
                if (d < bestDistance)
                {
                    bestDistance = d;
                    best = pawn;
                }
            }
            return best;
        }

        /// <summary>Where a pawn's base and head appear on screen.</summary>
        public static void ScreenSpan(Pawn3D pawn, CameraRig rig, out Vector2 foot, out Vector2 head)
        {
            foot = rig.WorldToScreen(pawn.Body.position);
            head = rig.WorldToScreen(pawn.HeadPosition);
        }

        static float DistanceToSegment(Vector2 p, Vector2 a, Vector2 b)
        {
            var ab = b - a;
            float t = ab.sqrMagnitude > 1e-6f ? Mathf.Clamp01(Vector2.Dot(p - a, ab) / ab.sqrMagnitude) : 0f;
            return (p - (a + ab * t)).magnitude;
        }
    }
}
