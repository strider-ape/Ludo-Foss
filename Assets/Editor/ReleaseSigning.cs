using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;

namespace LudoGame.EditorTools
{
    /// <summary>
    /// Signs Android builds with the permanent release key when it is present on this computer:
    /// %USERPROFILE%\.ludofoss\signing.properties (or the file named by the LUDO_SIGNING environment variable),
    /// with storeFile, storePassword, keyAlias and keyPassword. Without it, builds are debug-signed.
    /// The key never lives in the repository, and nothing about it is saved into the project settings.
    /// </summary>
    public static class ReleaseSigning
    {
        public static string PropertiesPath
        {
            get
            {
                string custom = Environment.GetEnvironmentVariable("LUDO_SIGNING");
                if (!string.IsNullOrEmpty(custom)) return custom;
                return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".ludofoss", "signing.properties");
            }
        }

        /// <summary>Returns true if the release key was found and set up for the next build.</summary>
        public static bool Configure()
        {
            string path = PropertiesPath;
            if (!File.Exists(path))
            {
                Clear();
                return false;
            }
            var values = new Dictionary<string, string>();
            foreach (var raw in File.ReadAllLines(path))
            {
                var line = raw.Trim();
                if (line.Length == 0 || line.StartsWith("#", StringComparison.Ordinal)) continue;
                int eq = line.IndexOf('=');
                if (eq > 0) values[line.Substring(0, eq).Trim()] = line.Substring(eq + 1).Trim();
            }
            foreach (var key in new[] { "storeFile", "storePassword", "keyAlias", "keyPassword" })
            {
                if (!values.ContainsKey(key)) throw new InvalidDataException($"{path} is missing '{key}'");
            }
            string store = values["storeFile"];
            if (!Path.IsPathRooted(store)) store = Path.Combine(Path.GetDirectoryName(path) ?? "", store);
            if (!File.Exists(store)) throw new FileNotFoundException("Release keystore not found", store);

            PlayerSettings.Android.useCustomKeystore = true;
            PlayerSettings.Android.keystoreName = store;
            PlayerSettings.Android.keystorePass = values["storePassword"];
            PlayerSettings.Android.keyaliasName = values["keyAlias"];
            PlayerSettings.Android.keyaliasPass = values["keyPassword"];
            return true;
        }

        /// <summary>Forgets the key again so its path and passwords are never written into ProjectSettings.</summary>
        public static void Clear()
        {
            PlayerSettings.Android.useCustomKeystore = false;
            PlayerSettings.Android.keystoreName = "";
            PlayerSettings.Android.keystorePass = "";
            PlayerSettings.Android.keyaliasName = "";
            PlayerSettings.Android.keyaliasPass = "";
        }
    }
}
