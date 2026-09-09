using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Causeless3t.Table
{
    public class TableGenerator : Editor
    {
        [MenuItem("Assets/테이블 생성", true)] // true 인자는 활성화 여부를 제어
        private static bool ValidateConvertCSVFiles()
        {
            // 선택한 파일이 CSV 파일만 포함하는지 체크
            var selectedAssets = Selection.GetFiltered<Object>(SelectionMode.Assets);
            bool allCSV = selectedAssets.All(asset =>
            {
                string assetPath = AssetDatabase.GetAssetPath(asset);
                return Path.GetExtension(assetPath).ToLowerInvariant() == ".csv";
            });
            return allCSV;
        }

        [MenuItem("Assets/테이블 생성")]
        private static void ConvertCSVFiles()
        {
            var selectedAssets = Selection.GetFiltered<TextAsset>(SelectionMode.Assets);

            if (selectedAssets.Length == 0)
                return;

            try
            {
                var codeChanged = false;

                foreach (var textAsset in selectedAssets)
                {
                    if (CsvToBinaryConverter.CreateSchemeClass(
                            textAsset.name,
                            textAsset.text))
                    {
                        codeChanged = true;
                    }
                }

                if (codeChanged)
                {
                    TableGenerationState.SetAssetPaths(
                        selectedAssets
                            .Select(AssetDatabase.GetAssetPath)
                            .ToArray());

                    TableGenerationState.WaitingForCompilation = true;

                    AssetDatabase.Refresh();
                    EditorUtility.ClearProgressBar();
                    return;
                }

                GenerateEncryptedTables(selectedAssets);
            }
            catch (Exception e)
            {
                TableGenerationState.Clear();
                Debug.LogException(e);
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
        }
        
        private static void GenerateEncryptedTables(
            TextAsset[] selectedAssets)
        {
            var completed = 0;

            foreach (var textAsset in selectedAssets)
            {
                Directory.CreateDirectory(DataTableSettingsProvider.Settings.EncryptedDataPath);
                
                var targetPath = Path.Combine(
                    DataTableSettingsProvider.Settings.EncryptedDataPath,
                    $"{textAsset.name}_encry.bytes");

                EditorUtility.DisplayProgressBar(
                    "파일 변환 중",
                    $"{targetPath} ({completed + 1}/{selectedAssets.Length})",
                    (float)completed / selectedAssets.Length);

                try
                {
                    CsvToBinaryConverter.Convert(
                        textAsset.name,
                        textAsset.text,
                        targetPath,
                        DataTableSettingsProvider.Settings.AESKey);

                    completed++;
                }
                catch (Exception e)
                {
                    Debug.LogException(e);
                }
            }

            AssetDatabase.Refresh();

            EditorUtility.DisplayDialog(
                "작업 완료",
                $"{completed}/{selectedAssets.Length}개의 CSV 파일 처리가 완료되었습니다.",
                "확인");
        }
        
        internal static void GenerateEncryptedTables()
        {
            var paths = TableGenerationState.GetAssetPaths();

            var assets = paths
                .Select(AssetDatabase.LoadAssetAtPath<TextAsset>)
                .Where(asset => asset != null)
                .ToArray();

            GenerateEncryptedTables(assets);
        }

        // 우클릭 메뉴에서 실행할 함수
        [MenuItem("Assets/테이블 복원", true)] // true 인자는 활성화 여부를 제어
        private static bool ValidateConvertDataFiles()
        {
            // 선택한 파일이 bytes 파일만 포함하는지 체크
            var selectedAssets = Selection.GetFiltered<Object>(SelectionMode.Assets);
            bool allDat = selectedAssets.All(asset =>
            {
                string assetPath = AssetDatabase.GetAssetPath(asset);
                return assetPath.EndsWith("_encry.bytes");
            });
            return allDat;
        }

        private static readonly ResourcesDataLoader _dataLoader = new();
        [MenuItem("Assets/테이블 복원")]
        private static async void ConvertDataFiles()
        {
            // 선택한 CSV 파일들에 대해 작업 실행
            var selectedAssets = Selection.GetFiltered<TextAsset>(SelectionMode.Assets);
            
            if (selectedAssets.Length == 0)
                return;
            
            int completed = 0;

            try
            {
                foreach (var textAsset in selectedAssets)
                {
                    var targetName = RemoveSuffix(textAsset.name, "_encry");
                    EditorUtility.DisplayProgressBar(
                        "테이블 변환",
                        $"{targetName} ({completed + 1}/{selectedAssets.Length})",
                        (float)completed /
                        selectedAssets.Length);

                    try
                    {
                        var type = CsvToBinaryConverter.FindTypeByName(targetName);
                        if (type == null)
                            throw new NotSupportedException($"존재하지 않는 자료형입니다. ({targetName})");

                        var data = await LoadDataByTypeAsync(targetName, type);
                        WriteCSVFileByDataObject(targetName, type, data);

                        completed++;
                    }
                    catch (Exception e)
                    {
                        Debug.LogException(e);
                    }
                }
                
                AssetDatabase.Refresh();

                EditorUtility.DisplayDialog(
                    "변환 완료",
                    $"{completed}/{selectedAssets.Length}개의 테이블을 CSV로 변환했습니다.",
                    "확인");
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
        }
        
        private static async Task<object> LoadDataByTypeAsync(string targetName, Type type)
        {
            // 바이너리 파일 읽기
            byte[] encryptedData = await _dataLoader.LoadAsync($"{targetName}_encry");
            if (encryptedData == null)
                throw new FileNotFoundException($"암호화된 테이블 파일을 찾을 수 없습니다. ({targetName})");
            Debug.Log($"Local Table {targetName} Loading");
            byte[] decryptedData = AesEncryption.Decrypt(encryptedData, DataTableSettingsProvider.Settings.AESKey);

            // 직렬화된 데이터를 역직렬화
            return DeserializeUtil.DeserializeByType(decryptedData, type);
        }
        
        private static string RemoveSuffix(string value, string suffix)
        {
            if (!value.EndsWith(suffix, StringComparison.Ordinal))
                return value;

            return value.Substring(
                0,
                value.Length - suffix.Length);
        }

        private static void WriteCSVFileByDataObject(string targetName, Type dataType, object data)
        {
            StringBuilder sb = new();
            // schema
            var properties = dataType.GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);
            if (properties.Length == 0)
                throw new InvalidOperationException($"CSV로 복원할 public property가 없습니다. ({dataType.Name})");

            // schema
            foreach (var property in properties)
            {
                sb.Append(property.Name);
                sb.Append(':');
                sb.Append(ShortExpressionTypeName(property.PropertyType));
                sb.Append(CsvToBinaryConverter.CSV_SEPARATOR);
            }

            sb.Length--;
            sb.AppendLine();

            // records
            var dataObjectType = typeof(DynamicDataObject<>).MakeGenericType(dataType);

            var listField = dataObjectType.GetField(
                "List",
                BindingFlags.Public | BindingFlags.Instance);

            if (listField == null)
                throw new MissingFieldException(dataObjectType.FullName, "List");

            if (listField.GetValue(data) is not IEnumerable dataList)
                throw new InvalidOperationException($"테이블 데이터의 List를 읽을 수 없습니다. ({targetName})");

            foreach (var item in dataList)
            {
                foreach (var property in properties)
                {
                    var value = property.GetValue(item);

                    sb.Append(ConvertValueToCsv(value));
                    sb.Append(CsvToBinaryConverter.CSV_SEPARATOR);
                }

                sb.Length--;
                sb.AppendLine();
            }

            var outputDirectory = DataTableSettingsProvider.Settings.CsvSourcePath;

            Directory.CreateDirectory(outputDirectory);

            var targetPath = Path.Combine(outputDirectory, $"{targetName}_.csv");
            File.WriteAllText(targetPath, sb.ToString());
        }

        private static string ShortExpressionTypeName(Type type)
        {
            if (type == typeof(string))
                return "string";

            if (type == typeof(int))
                return "int";

            if (type == typeof(long))
                return "long";

            if (type == typeof(float))
                return "float";

            if (type == typeof(double))
                return "double";

            if (type == typeof(DateTime))
                return "datetime";

            if (type == typeof(Vector2))
                return "vector2";

            if (type == typeof(Vector3))
                return "vector3";

            if (type == typeof(List<int>))
                return "list<int>";

            if (type == typeof(List<float>))
                return "list<float>";

            if (type == typeof(List<string>))
                return "list<string>";

            throw new NotSupportedException($"지원하지 않는 CSV 타입입니다. ({type.FullName})");
        }
        
        private static string ConvertValueToCsv(object value)
        {
            if (value == null)
                return string.Empty;

            switch (value)
            {
                case List<int> list:
                    return string.Join(",", list);

                case List<float> list:
                    return string.Join(",", list);

                case List<string> list:
                    return string.Join(",", list);

                case Vector2 vector2:
                    return $"({vector2.x},{vector2.y})";

                case Vector3 vector3:
                    return $"({vector3.x},{vector3.y},{vector3.z})";

                case DateTime dateTime:
                    return dateTime.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);

                default:
                    return value.ToString();
            }
        }
    }
}
