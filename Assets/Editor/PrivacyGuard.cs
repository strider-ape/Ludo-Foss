using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using UnityEditor;
using Debug = UnityEngine.Debug;

namespace LudoGame.EditorTools
{
    /// <summary>
    /// Checks a built APK keeps the "no internet, no tracking" promise: only allowed permissions, and none of
    /// the common ad / analytics / tracking SDKs in its code or native libraries. The Android build fails if not.
    /// Check any APK: Unity.exe -batchmode -quit -projectPath . -executeMethod LudoGame.EditorTools.PrivacyGuard.CheckApk -apk path\to\app.apk
    /// </summary>
    public static class PrivacyGuard
    {
        static readonly string[] AllowedPermissions = { "android.permission.VIBRATE" };

        /// <summary>Java packages of well-known ad, analytics, attribution and crash-reporting SDKs.</summary>
        static readonly string[] TrackerPackages =
        {
            "com/google/firebase/", "com/google/android/gms/ads/", "com/google/android/gms/measurement/",
            "com/google/android/gms/analytics/", "com/google/ads/", "com/facebook/", "com/appsflyer/", "com/adjust/sdk/",
            "com/unity3d/ads/", "com/unity3d/services/", "com/ironsource/", "com/applovin/", "com/bytedance/",
            "com/mbridge/", "com/vungle/", "com/inmobi/", "com/fyber/", "com/chartboost/", "com/crashlytics/",
            "io/sentry/", "com/bugsnag/", "com/onesignal/", "com/amplitude/", "com/mixpanel/", "com/flurry/",
            "com/branch/", "io/branch/", "com/kochava/", "com/singular/", "io/agora/",
        };

        static readonly string[] TrackerLibraryWords = { "firebase", "crashlytics", "agora", "appsflyer", "adjust", "facebook", "applovin", "ironsource", "sentry", "bugsnag" };

        /// <summary>Returns false (with the reasons in <paramref name="report"/>) if the APK breaks the privacy promise.</summary>
        public static bool Check(string apkPath, string ownPackage, string aaptPath, out string report)
        {
            var problems = new List<string>();
            var permissions = ReadPermissions(aaptPath, apkPath, problems);
            foreach (var permission in permissions)
            {
                bool own = ownPackage != null && permission.StartsWith(ownPackage + ".", StringComparison.Ordinal);
                if (!own && !AllowedPermissions.Contains(permission)) problems.Add("permission " + permission);
            }

            var libraries = new List<string>();
            using (var zip = new ZipArchive(File.OpenRead(apkPath), ZipArchiveMode.Read))
            {
                foreach (var entry in zip.Entries)
                {
                    if (entry.FullName.StartsWith("lib/", StringComparison.Ordinal) && entry.Name.EndsWith(".so", StringComparison.Ordinal))
                    {
                        if (!libraries.Contains(entry.Name)) libraries.Add(entry.Name);
                        foreach (var word in TrackerLibraryWords)
                        {
                            if (entry.Name.IndexOf(word, StringComparison.OrdinalIgnoreCase) >= 0) problems.Add("native library " + entry.FullName);
                        }
                    }
                    if (!entry.Name.StartsWith("classes", StringComparison.Ordinal) || !entry.Name.EndsWith(".dex", StringComparison.Ordinal)) continue;
                    byte[] dex;
                    using (var stream = entry.Open())
                    using (var memory = new MemoryStream())
                    {
                        stream.CopyTo(memory);
                        dex = memory.ToArray();
                    }
                    foreach (var package in TrackerPackages)
                    {
                        if (Contains(dex, Encoding.ASCII.GetBytes("L" + package))) problems.Add("code from " + package.TrimEnd('/').Replace('/', '.') + " in " + entry.Name);
                    }
                }
            }

            var allowed = permissions.Count == 0 ? "none" : string.Join(", ", permissions.Select(p => p.Replace("android.permission.", "")));
            report = problems.Count == 0
                ? $"[Ludo] Privacy check passed. Permissions: {allowed}. No ad, analytics or tracking code. Native libraries: {string.Join(", ", libraries)}"
                : "[Ludo] PRIVACY CHECK FAILED:\n  - " + string.Join("\n  - ", problems.Distinct());
            return problems.Count == 0;
        }

        /// <summary>Command-line check of any APK, e.g. to compare with another Ludo app.</summary>
        public static void CheckApk()
        {
            var args = Environment.GetCommandLineArgs();
            int i = Array.IndexOf(args, "-apk");
            string apk = i >= 0 && i + 1 < args.Length ? args[i + 1] : null;
            if (apk == null || !File.Exists(apk))
            {
                Debug.LogError("[Ludo] Pass -apk <path to an existing APK>");
                EditorApplication.Exit(1);
                return;
            }
            bool ok = Check(apk, null, FindAapt(), out var report);
            Debug.Log(report);
            EditorApplication.Exit(ok ? 0 : 1);
        }

        static List<string> ReadPermissions(string aapt, string apk, List<string> problems)
        {
            var permissions = new List<string>();
            if (aapt == null || !File.Exists(aapt))
            {
                problems.Add("could not find aapt to read the APK's permissions");
                return permissions;
            }
            var info = new ProcessStartInfo(aapt, $"dump permissions \"{apk}\"")
            {
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
            };
            using (var process = Process.Start(info))
            {
                string output = process.StandardOutput.ReadToEnd();
                process.WaitForExit();
                if (process.ExitCode != 0) problems.Add("aapt could not read the APK");
                foreach (var line in output.Split('\n'))
                {
                    var t = line.Trim();
                    if (!t.StartsWith("uses-permission", StringComparison.Ordinal)) continue;
                    int start = t.IndexOf('\'');
                    int end = t.IndexOf('\'', start + 1);
                    string name = start >= 0 && end > start ? t.Substring(start + 1, end - start - 1) : t.Substring(t.IndexOf(':') + 1).Trim();
                    if (!permissions.Contains(name)) permissions.Add(name);
                }
            }
            return permissions;
        }

        /// <summary>The newest aapt in the Android SDK that Unity uses.</summary>
        public static string FindAapt()
        {
#if UNITY_ANDROID
            string sdk = UnityEditor.Android.AndroidExternalToolsSettings.sdkRootPath;
#else
            string sdk = Path.Combine(EditorApplication.applicationContentsPath, "PlaybackEngines", "AndroidPlayer", "SDK");
#endif
            string tools = Path.Combine(sdk ?? "", "build-tools");
            if (!Directory.Exists(tools)) return null;
            return Directory.GetDirectories(tools)
                .OrderByDescending(d => new DirectoryInfo(d).Name, StringComparer.Ordinal)
                .Select(d => Path.Combine(d, "aapt.exe"))
                .FirstOrDefault(File.Exists);
        }

        static bool Contains(byte[] haystack, byte[] needle)
        {
            int last = haystack.Length - needle.Length;
            byte first = needle[0];
            for (int i = 0; i <= last; i++)
            {
                if (haystack[i] != first) continue;
                int k = 1;
                while (k < needle.Length && haystack[i + k] == needle[k]) k++;
                if (k == needle.Length) return true;
            }
            return false;
        }
    }
}
