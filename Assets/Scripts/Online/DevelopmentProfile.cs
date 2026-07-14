using System;
using UnityEngine;

namespace CierzoArena.Online
{
    public static class DevelopmentProfile
    {
        private const string PreferenceKey = "CierzoArena.M24.Profile";
        public static string Resolve()
        {
            if (!Application.isEditor && !Debug.isDebugBuild) return "production";
            string[] arguments = System.Environment.GetCommandLineArgs();
            for (int i = 0; i + 1 < arguments.Length; i++) if (string.Equals(arguments[i], "-cierzoProfile", StringComparison.OrdinalIgnoreCase)) return Sanitize(arguments[i + 1]);
            string environment = System.Environment.GetEnvironmentVariable("CIERZO_PROFILE");
            return Sanitize(string.IsNullOrWhiteSpace(environment) ? PlayerPrefs.GetString(PreferenceKey, "host") : environment);
        }
        public static void SetForDevelopment(string value)
        {
            if (!Application.isEditor && !Debug.isDebugBuild) return;
            PlayerPrefs.SetString(PreferenceKey, Sanitize(value)); PlayerPrefs.Save();
        }
        private static string Sanitize(string value)
        {
            value = string.IsNullOrWhiteSpace(value) ? "host" : value.Trim().ToLowerInvariant();
            return value.Length > 24 ? value.Substring(0, 24) : value;
        }
    }
}
