using LudoTurbo.Core;
using UnityEngine;

namespace LudoTurbo
{
    /// <summary>Player preferences, saved on the device between sessions.</summary>
    public static class GameSettings
    {
        public const int MinSpeed = 1;
        public const int MaxSpeed = 10;

        static readonly SeatType[] DefaultSeats = { SeatType.Human, SeatType.Computer, SeatType.Computer, SeatType.Computer };

        /// <summary>1 = classic pace, 2..9 = that many times faster, 10 = instant.</summary>
        public static int Speed
        {
            get => Mathf.Clamp(PlayerPrefs.GetInt("speed", 4), MinSpeed, MaxSpeed);
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
