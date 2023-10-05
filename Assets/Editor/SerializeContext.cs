using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Causeless3t.Data.Editor
{
    public static class SerializeContext
    {
        [MenuItem("Assets/Convert TableData")]
        static async void ConvertToProtobuf()
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

                await DataConverter.ConvertToDataAsync(filePaths);
            }
        }
        
        [MenuItem("Assets/Convert TableData", true)]
        private static bool ConvertValidation()
        {
            var valid = Selection.objects.All(obj =>
            {
                var path = AssetDatabase.GetAssetPath(obj.GetInstanceID());
                return path.EndsWith("csv");
            });
            return valid;
        }
        
        private static string[] GetAllDataFilePaths()
        {
            List<string> filePaths = new List<string>();
            var files = Selection.objects.
                Select(AssetDatabase.GetAssetPath)
                .Where(path => path.EndsWith("csv"))
                .ToArray();
            filePaths.AddRange(files);
            
            return filePaths.ToArray();
        }
    }
}