#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Causeless3t.Table
{
    internal static class DataTableSettingsProvider
    {
        private const string SettingsDirectory = "Assets/Resources";

        private const string SettingsPath = SettingsDirectory + "/DataTableSettings.asset";

        private static DataTableSettings _cachedSettings;

        public static DataTableSettings Settings
        {
            get
            {
                if (_cachedSettings != null)
                    return _cachedSettings;

                _cachedSettings = LoadSettings();

                if (_cachedSettings == null)
                    _cachedSettings = CreateSettings();

                return _cachedSettings;
            }
        }

        public static string GetAbsolutePath(string projectRelativePath)
        {
            var projectRoot = Directory.GetParent(Application.dataPath)?.FullName;

            if (string.IsNullOrEmpty(projectRoot))
                throw new DirectoryNotFoundException("Unity project root could not be resolved.");

            return Path.GetFullPath(
                Path.Combine(projectRoot, projectRelativePath));
        }

        private static DataTableSettings LoadSettings()
        {
            var settings =
                AssetDatabase.LoadAssetAtPath<DataTableSettings>(
                    SettingsPath);

            if (settings != null)
                return NormalizeSettings(settings);

            var guids = AssetDatabase.FindAssets(
                $"t:{nameof(DataTableSettings)}");

            if (guids.Length == 0)
                return null;

            var path =
                AssetDatabase.GUIDToAssetPath(guids[0]);

            return NormalizeSettings(
                AssetDatabase.LoadAssetAtPath<DataTableSettings>(path));
        }

        private static DataTableSettings NormalizeSettings(
            DataTableSettings settings)
        {
            if (settings == null || !settings.NormalizeSerializedPaths())
                return settings;

            EditorUtility.SetDirty(settings);
            AssetDatabase.SaveAssets();
            return settings;
        }

        private static DataTableSettings CreateSettings()
        {
            EnsureDirectoryExists();

            var settings =
                ScriptableObject.CreateInstance<DataTableSettings>();

            AssetDatabase.CreateAsset(
                settings,
                SettingsPath);

            AssetDatabase.SaveAssets();

            return settings;
        }

        private static void EnsureDirectoryExists()
        {
            if (AssetDatabase.IsValidFolder(SettingsDirectory))
                return;

            const string root = "Assets";
            const string parent = "Resources";

            AssetDatabase.CreateFolder(
                root,
                parent);
        }
    }
}
#endif
