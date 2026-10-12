using UnityEngine;

namespace LudoGame
{
    /// <summary>
    /// Finds which glowing pawn a tap is on. Only pawns are tap targets: the landing markers are just a preview,
    /// so tapping a pawn always moves that pawn, even when another pawn's marker sits on its square.
    /// </summary>
    public static class PawnPicker
    {
        /// <summary>The selectable pawn under <paramref name="tap"/> (screen pixels), or null.</summary>
        public static Pawn3D Pick(Vector2 tap, Pawn3D[,] pawns, CameraRig rig)
        {
            Pawn3D best = null;
            float bestDistance = (rig.WorldToScreen(Vector3.zero) - rig.WorldToScreen(Vector3.right)).magnitude * 0.65f;
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
            head = rig.WorldToScreen(pawn.Body.position + Vector3.up * 1.1f * pawn.BaseScale);
        }

        static float DistanceToSegment(Vector2 p, Vector2 a, Vector2 b)
        {
            var ab = b - a;
            float t = ab.sqrMagnitude > 1e-6f ? Mathf.Clamp01(Vector2.Dot(p - a, ab) / ab.sqrMagnitude) : 0f;
            return (p - (a + ab * t)).magnitude;
        }
    }
}
