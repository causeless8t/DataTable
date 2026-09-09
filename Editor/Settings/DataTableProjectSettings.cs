#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace Causeless3t.Table
{
    internal static class DataTableProjectSettings
    {
        [SettingsProvider]
        public static SettingsProvider CreateSettingsProvider()
        {
            var provider = new SettingsProvider(
                "Project/Data Table",
                SettingsScope.Project)
            {
                label = "Data Table",

                guiHandler = _ =>
                {
                    DrawSettings();
                },

                keywords = new[]
                {
                    "DataTable",
                    "CSV",
                    "AES",
                    "Namespace",
                    "Generated"
                }
            };

            return provider;
        }

        private static void DrawSettings()
        {
            var settings =
                DataTableSettingsProvider.Settings;

            if (settings == null)
            {
                EditorGUILayout.HelpBox(
                    "DataTableSettings could not be loaded.",
                    MessageType.Error);

                return;
            }

            var serializedObject =
                new SerializedObject(settings);

            serializedObject.Update();

            DrawCodeGenerationSettings(
                serializedObject);

            EditorGUILayout.Space(10f);

            DrawDataSettings(
                serializedObject);

            EditorGUILayout.Space(10f);

            DrawEncryptionSettings(
                serializedObject);

            if (serializedObject.ApplyModifiedProperties())
            {
                EditorUtility.SetDirty(settings);
            }
        }

        private static void DrawCodeGenerationSettings(
            SerializedObject serializedObject)
        {
            EditorGUILayout.LabelField(
                "Code Generation",
                EditorStyles.boldLabel);

            var namespaceProperty =
                serializedObject.FindProperty(
                    "_namespace");

            var generatedCodePathProperty =
                serializedObject.FindProperty(
                    "_generatedCodePath");
            
            var csvSourcePathProperty =
                serializedObject.FindProperty(
                    "_csvSourcePath");

            EditorGUILayout.PropertyField(
                namespaceProperty,
                new GUIContent("Namespace"));

            DataTableEditorGUI.DrawFolderField(
                generatedCodePathProperty,
                new GUIContent("Generated Code Path"));
            
            DataTableEditorGUI.DrawFolderField(
                csvSourcePathProperty,
                new GUIContent("CSV Source Path"));
        }

        private static void DrawDataSettings(
            SerializedObject serializedObject)
        {
            EditorGUILayout.LabelField(
                "Table Data",
                EditorStyles.boldLabel);

            var encryptedDataPathProperty =
                serializedObject.FindProperty(
                    "_encryptedDataPath");

            DataTableEditorGUI.DrawFolderField(
                encryptedDataPathProperty,
                new GUIContent("Encrypted Data Path"));
        }

        private static void DrawEncryptionSettings(
            SerializedObject serializedObject)
        {
            EditorGUILayout.LabelField(
                "Encryption",
                EditorStyles.boldLabel);

            var aesKeyProperty =
                serializedObject.FindProperty(
                    "_aesKey");

            aesKeyProperty.stringValue =
                EditorGUILayout.PasswordField(
                    "AES Key",
                    aesKeyProperty.stringValue);
        }
    }
}
#endif