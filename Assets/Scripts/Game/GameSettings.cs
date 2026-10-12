using LudoGame.Core;
using UnityEngine;

namespace LudoGame
{
    public enum PreviewMode { Off = 0, Marker = 1, MarkerAndPath = 2 }

    public enum PawnLook { Classic = 0, Outline = 1, OutlineCoin = 2 }

    /// <summary>Player preferences, saved on the device between sessions.</summary>
    public static class GameSettings
    {
        public const int MinSpeed = 1;
        public const int MaxSpeed = 10;

        static readonly SeatType[] DefaultSeats = { SeatType.Human, SeatType.Computer, SeatType.Computer, SeatType.Computer };

        /// <summary>1 = classic pace, 2..9 = that many times faster, 10 = instant.</summary>
        public static int Speed
        {
            get => Mathf.Clamp(PlayerPrefs.GetInt("speed", 3), MinSpeed, MaxSpeed);
            set => SetInt("speed", Mathf.Clamp(value, MinSpeed, MaxSpeed));
        }

        public static bool AutoMove
        {
            get => PlayerPrefs.GetInt("autoMove", 1) == 1;
            set => SetInt("autoMove", value ? 1 : 0);
        }

        public static bool ThreeSixesRule
        {
            get => PlayerPrefs.GetInt("threeSixes", 1) == 1;
            set => SetInt("threeSixes", value ? 1 : 0);
        }

        public static bool Sound
        {
            get => PlayerPrefs.GetInt("sound", 1) == 1;
            set => SetInt("sound", value ? 1 : 0);
        }

        public static bool Vibration
        {
            get => PlayerPrefs.GetInt("vibration", 1) == 1;
            set => SetInt("vibration", value ? 1 : 0);
        }

        /// <summary>Caps animations at 30 fps instead of 60.</summary>
        public static bool BatterySaver
        {
            get => PlayerPrefs.GetInt("batterySaver", 0) == 1;
            set
            {
                SetInt("batterySaver", value ? 1 : 0);
                FrameGovernor.ApplyTargetFrameRate();
            }
        }

        public static PawnLook Pawns
        {
            get => (PawnLook)Mathf.Clamp(PlayerPrefs.GetInt("pawnLook", (int)PawnLook.Outline), 0, 2);
            set => SetInt("pawnLook", (int)value);
        }

        /// <summary>A shape per colour (circle, triangle, square, star) on pawns, bases and avatars, for colour-blind players.</summary>
        public static bool ColorShapes
        {
            get => PlayerPrefs.GetInt("colorShapes", 0) == 1;
            set => SetInt("colorShapes", value ? 1 : 0);
        }

        /// <summary>How much to show about a move before you choose it.</summary>
        public static PreviewMode Preview
        {
            get => (PreviewMode)Mathf.Clamp(PlayerPrefs.GetInt("preview", (int)PreviewMode.MarkerAndPath), 0, 2);
            set => SetInt("preview", (int)value);
        }

        /// <summary>Keep playing for 2nd and 3rd place after someone finishes (off: the first winner ends the game).</summary>
        public static bool PlayForPlaces
        {
            get => PlayerPrefs.GetInt("playForPlaces", 1) == 1;
            set => SetInt("playForPlaces", value ? 1 : 0);
        }

        public static bool ResumeGames
        {
            get => PlayerPrefs.GetInt("resumeGames", 1) == 1;
            set
            {
                SetInt("resumeGames", value ? 1 : 0);
                if (!value) SaveGame.Clear();
            }
        }

        public static readonly int[] TimerChoices = { 0, 10, 15, 30 };

        /// <summary>Seconds per human turn, or 0 for no timer.</summary>
        public static int TurnTimer
        {
            get
            {
                int value = PlayerPrefs.GetInt("turnTimer", 0);
                return System.Array.IndexOf(TimerChoices, value) >= 0 ? value : 0;
            }
            set => SetInt("turnTimer", value);
        }

        /// <summary>When the timer runs out: play the turn for you (true) or skip it (false).</summary>
        public static bool TimerAutoPlays
        {
            get => PlayerPrefs.GetInt("timerAutoPlays", 1) == 1;
            set => SetInt("timerAutoPlays", value ? 1 : 0);
        }

        public static bool Instant => Speed >= MaxSpeed;

        public static string SpeedLabel => Instant ? "Instant" : Speed == 1 ? "1x (classic)" : Speed + "x";

        /// <summary>Scales an animation duration (given at classic pace) by the current speed.</summary>
        public static float Scale(float seconds) => Instant ? 0f : seconds / Speed;

        public static SeatType GetSeat(PlayerColor c) =>
            (SeatType)PlayerPrefs.GetInt("seat" + (int)c, (int)DefaultSeats[(int)c]);

        public static void SetSeat(PlayerColor c, SeatType type) => SetInt("seat" + (int)c, (int)type);

        static void SetInt(string key, int value)
        {
            PlayerPrefs.SetInt(key, value);
            PlayerPrefs.Save();
        }
    }
}
