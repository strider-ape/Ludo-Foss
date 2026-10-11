using LudoGame.Core;
using UnityEngine;

namespace LudoGame
{
    public static class Palette
    {
        static readonly Color[] Main =
        {
            Hex(0xE53935), // red
            Hex(0x43A047), // green
            Hex(0xFBC02D), // yellow
            Hex(0x1E88E5), // blue
        };

        static readonly Color[] Light =
        {
            Hex(0xFFCDD2),
            Hex(0xC8E6C9),
            Hex(0xFFF59D),
            Hex(0xBBDEFB),
        };

        static readonly Color[] Dark =
        {
            Hex(0x8E1B18),
            Hex(0x1B5E20),
            Hex(0x9A7200),
            Hex(0x0D47A1),
        };

        public static readonly Color Background = Hex(0x1F2A44);
        public static readonly Color BoardLine = Hex(0xB8BEC9);
        public static readonly Color Cell = Color.white;
        public static readonly Color Star = Hex(0x9AA3B2);
        public static readonly Color Text = Color.white;
        public static readonly Color TextDark = Hex(0x1F2A44);
        public static readonly Color Button = Hex(0x3D5AFE);
        public static readonly Color ButtonMuted = Hex(0x46506B);
        public static readonly Color Overlay = new Color(0f, 0f, 0f, 0.65f);
        public static readonly Color Card = Hex(0x2B3858);
        public static readonly Color Glow = Hex(0xFFFFFF);
        public static readonly Color TableCenter = Hex(0x34477E);
        public static readonly Color Wood = Hex(0x9C6B45);
        public static readonly Color Gold = Hex(0xFFD54F);

        public static Color Darken(Color c, float amount = 0.7f) => new Color(c.r * amount, c.g * amount, c.b * amount, c.a);

        public static Color Of(PlayerColor c) => Main[(int)c];

        public static Color LightOf(PlayerColor c) => Light[(int)c];

        public static Color DarkOf(PlayerColor c) => Dark[(int)c];

        public static string Name(PlayerColor c) => c.ToString().ToUpperInvariant();

        static Color Hex(int rgb) => new Color(((rgb >> 16) & 0xFF) / 255f, ((rgb >> 8) & 0xFF) / 255f, (rgb & 0xFF) / 255f);
    }
}
