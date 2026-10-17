using System.Collections;
using UnityEngine;

namespace LudoGame
{
    /// <summary>The die as a match sees it: the flat on-screen die or the 3D one on the table.</summary>
    public interface IDie
    {
        bool IsRollable { get; }

        void SetRollable(bool on);

        /// <summary>Moves to a player's spot: <paramref name="world"/> on the table for the 3D die, <paramref name="canvas"/> on screen for the flat one.</summary>
        void MoveTo(Vector3 world, Vector2 canvas, float duration);

        /// <summary>The roll animation, landing on <paramref name="result"/>.</summary>
        IEnumerator Roll(int result, float duration);

        /// <summary>A happy pop for rolling a six.</summary>
        IEnumerator Celebrate();

        /// <summary>True if a tap on the board (not on a button) hits the die.</summary>
        bool HitTest(Vector2 screen, CameraRig rig);

        void Remove();
    }
}
