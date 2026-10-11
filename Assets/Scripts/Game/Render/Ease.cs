namespace LudoGame
{
    /// <summary>Easing curves for animations; t runs from 0 to 1.</summary>
    public static class Ease
    {
        public static float Smooth(float t) => t * t * (3f - 2f * t);

        public static float OutQuad(float t) => 1f - (1f - t) * (1f - t);

        public static float OutCubic(float t)
        {
            float u = 1f - t;
            return 1f - u * u * u;
        }

        public static float OutBack(float t)
        {
            const float s = 1.70158f;
            float u = t - 1f;
            return 1f + (s + 1f) * u * u * u + s * u * u;
        }

        public static float OutBounce(float t)
        {
            const float n = 7.5625f, d = 2.75f;
            if (t < 1f / d) return n * t * t;
            if (t < 2f / d) return n * (t -= 1.5f / d) * t + 0.75f;
            if (t < 2.5f / d) return n * (t -= 2.25f / d) * t + 0.9375f;
            return n * (t -= 2.625f / d) * t + 0.984375f;
        }

        /// <summary>A jump: 0 at the ends, 1 in the middle.</summary>
        public static float Arc(float t) => 4f * t * (1f - t);
    }
}
