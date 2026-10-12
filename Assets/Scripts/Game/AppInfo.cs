namespace LudoGame
{
    /// <summary>The app's identity in one place, so renaming it is a one-line change.</summary>
    public static class AppInfo
    {
        public const string Name = "Ludo FOSS";
        public const string AndroidId = "io.github.strider_ape.ludo";
        public const string Version = "0.5.0";
        public const int VersionCode = 7;

        public static string FileName => Name.Replace(" ", "");
    }
}
