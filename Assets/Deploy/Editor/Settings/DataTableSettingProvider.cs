#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace Causeless3t.Table
{
    internal static class DataTableSettingsProvider
    {
        private const string SettingsDirectory = "Assets/DataTable";

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

        private static DataTableSettings LoadSettings()
        {
            var settings =
                AssetDatabase.LoadAssetAtPath<DataTableSettings>(
                    SettingsPath);

            if (settings != null)
                return settings;

            var guids = AssetDatabase.FindAssets(
                $"t:{nameof(DataTableSettings)}");

            if (guids.Length == 0)
                return null;

            var path =
                AssetDatabase.GUIDToAssetPath(guids[0]);

            return AssetDatabase.LoadAssetAtPath<DataTableSettings>(
                path);
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

            const string parent = "Assets";
            const string folderName = "DataTable";

            AssetDatabase.CreateFolder(
                parent,
                folderName);
        }
    }
}
#endif