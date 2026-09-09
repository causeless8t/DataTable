#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEngine;

namespace Causeless3t.Table
{
    internal static class DataTableEditorGUI
    {
        public static void DrawFolderField(
            SerializedProperty property,
            GUIContent label)
        {
            EditorGUILayout.BeginHorizontal();

            EditorGUILayout.PropertyField(property, label);

            if (GUILayout.Button("Select", GUILayout.Width(60f)))
            {
                SelectFolder(property);
            }

            EditorGUILayout.EndHorizontal();
        }

        private static void SelectFolder(
            SerializedProperty property)
        {
            var selectedPath = EditorUtility.OpenFolderPanel("Select Folder",
                Application.dataPath,
                string.Empty);

            if (string.IsNullOrEmpty(selectedPath))
                return;

            selectedPath = selectedPath.Replace("\\", "/");

            var assetsPath = Application.dataPath.Replace("\\", "/");

            if (!selectedPath.StartsWith(assetsPath, StringComparison.Ordinal))
            {
                EditorUtility.DisplayDialog(
                    "Invalid Path",
                    "The selected folder must be inside the project's Assets folder.",
                    "OK");

                return;
            }

            property.stringValue = "Assets" +
                                   selectedPath.Substring(
                                       assetsPath.Length);
        }
    }
}
#endif