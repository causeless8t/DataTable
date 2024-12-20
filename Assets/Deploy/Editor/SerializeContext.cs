using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Causeless3t.Data.Editor
{
    public static class SerializeContext
    {
        [MenuItem("Assets/Set TableData Root Folder")]
        private static void SetRootFolder()
        {
            string selectedPath = EditorUtility.OpenFolderPanel("Select Root Folder", "", "");
            if (!string.IsNullOrEmpty(selectedPath))
            {
                DataConverter.SetExportPath(selectedPath);
                File.WriteAllText(Path.Combine(Application.dataPath, "TablePathData.dat"), selectedPath);
                AssetDatabase.Refresh();
            }
        }

        [MenuItem("Assets/Convert TableData")]
        private static void ConvertToProtobuf()
        {
            var filePaths = GetAllDataFilePaths();
            
            if (filePaths.Length == 0)
            {
                EditorUtility.DisplayDialog("Error", "There is no exists to convert file.", "OK");
                return;
            }

            var confirmConvert = EditorUtility.DisplayDialog("Notice", $"Convert to {filePaths.Length} files, Continue?", "Convert", "Cancel");
            
            if (confirmConvert)
            {
                foreach (var path in filePaths)
                {
                    Debug.Log($"{path}");
                }

                DataConverter.ConvertToDataAsync(filePaths);
            }
        }
        
        [MenuItem("Assets/Convert TableData", true)]
        private static bool ConvertValidation()
        {
            var valid = Selection.objects.All(obj =>
            {
                var path = AssetDatabase.GetAssetPath(obj.GetInstanceID());
                var filename = Path.GetFileName(path);
                return !filename.StartsWith("#") && !filename.StartsWith("~") && filename.EndsWith("xls") || filename.EndsWith("xlsx");
            });
            return valid;
        }
        
        private static string[] GetAllDataFilePaths()
        {
            List<string> filePaths = new List<string>();
            var files = Selection.objects.
                Select(AssetDatabase.GetAssetPath)
                .Where(path =>
                {
                    var filename = Path.GetFileName(path);
                    return !filename.StartsWith("#") && !filename.StartsWith("~") && filename.EndsWith("xls") || filename.EndsWith("xlsx");
                })
                .ToArray();
            filePaths.AddRange(files);
            
            return filePaths.ToArray();
        }
    }
}