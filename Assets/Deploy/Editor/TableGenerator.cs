using System;
using System.Collections;
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
        [MenuItem("Assets/TSV > CSV 구분자 변환", true)] // true 인자는 활성화 여부를 제어
        private static bool ValidateConvertTSVFiles()
        {
            // 선택한 파일이 CSV 파일만 포함하는지 체크
            var selectedAssets = Selection.GetFiltered<Object>(SelectionMode.Assets);
            bool allCSV = selectedAssets.All(asset =>
            {
                string assetPath = AssetDatabase.GetAssetPath(asset);
                return Path.GetExtension(assetPath).ToLower() == ".tsv";
            });
            return allCSV;
        }

        [MenuItem("Assets/TSV > CSV 구분자 변환")]
        private static void ConvertTSVFiles()
        {
            // 선택한 CSV 파일들에 대해 작업 실행
            var selectedAssets = Selection.GetFiltered<Object>(SelectionMode.Assets);
            int completed = 0;
            EditorUtility.ClearProgressBar();
            foreach (var asset in selectedAssets)
            {
                string assetPath = AssetDatabase.GetAssetPath(asset);
                string targetPath = Path.Combine(DataTableSettingsProvider.Settings.CsvSourcePath, $"{Path.GetFileNameWithoutExtension(assetPath)}.csv");
                EditorUtility.DisplayProgressBar("파일 변환 중", $"{targetPath} ({completed}/{selectedAssets.Length})",
                    (float)completed / selectedAssets.Length);
                string tsv = File.ReadAllText(assetPath);
                string csv = tsv.Replace("\t", "|");
                File.WriteAllText(targetPath, csv);
                File.Delete(assetPath);
                completed++;
                EditorUtility.DisplayProgressBar("파일 변환 중", $"{targetPath} ({completed}/{selectedAssets.Length})",
                    (float)completed / selectedAssets.Length);
            }

            AssetDatabase.Refresh();
            EditorUtility.ClearProgressBar();
            // 작업이 완료되었음을 알리는 다이얼로그 띄우기
            EditorUtility.DisplayDialog("작업 완료", "TSV > CSV(|) 파일 처리가 완료되었습니다.", "확인");
        }

        [MenuItem("Assets/테이블 생성", true)] // true 인자는 활성화 여부를 제어
        private static bool ValidateConvertCSVFiles()
        {
            // 선택한 파일이 CSV 파일만 포함하는지 체크
            var selectedAssets = Selection.GetFiltered<Object>(SelectionMode.Assets);
            bool allCSV = selectedAssets.All(asset =>
            {
                string assetPath = AssetDatabase.GetAssetPath(asset);
                return Path.GetExtension(assetPath).ToLower() == ".csv";
            });
            return allCSV;
        }

        [MenuItem("Assets/테이블 생성")]
        private static void ConvertCSVFiles()
        {
            // 선택한 CSV 파일들에 대해 작업 실행
            var selectedAssets = Selection.GetFiltered<Object>(SelectionMode.Assets);
            int completed = 0;
            EditorUtility.ClearProgressBar();
            foreach (var asset in selectedAssets)
            {
                if (asset is TextAsset textAsset)
                {
                    var targetPath = Path.Combine(DataTableSettingsProvider.Settings.EncryptedDataPath, $"{textAsset.name}_encry.bytes");
                    EditorUtility.DisplayProgressBar("파일 변환 중", $"{targetPath} ({completed}/{selectedAssets.Length})",
                        (float)completed / selectedAssets.Length);
                    try
                    {
                        CsvToBinaryConverter.Convert(textAsset.name, textAsset.text, targetPath,
                            DataTableSettingsProvider.Settings.AESKey);
                    }
                    catch (Exception e)
                    {
                        Debug.LogException(e);
                    }

                    completed++;
                    EditorUtility.DisplayProgressBar("파일 변환 중", $"{targetPath} ({completed}/{selectedAssets.Length})",
                        (float)completed / selectedAssets.Length);
                }
            }

            AssetDatabase.Refresh();
            EditorUtility.ClearProgressBar();
            // 작업이 완료되었음을 알리는 다이얼로그 띄우기
            EditorUtility.DisplayDialog("작업 완료", "CSV 파일 처리가 완료되었습니다.", "확인");
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

                    AssetDatabase.Refresh();

                    EditorUtility.DisplayDialog(
                        "변환 완료",
                        $"{completed}/{selectedAssets.Length}개의 테이블을 CSV로 변환했습니다.",
                        "확인");
                }
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
        }
        
        private static async Task<object> LoadDataByTypeAsync(
            string targetName,
            Type type)
        {
            try
            {
                // 바이너리 파일 읽기
                byte[] encryptedData = await _dataLoader.LoadAsync($"Table/{targetName}_encry");
                if (encryptedData == null) return null;
                Debug.Log($"Local Table {targetName} Loading");
                byte[] decryptedData = AesEncryption.Decrypt(encryptedData, DataTableSettingsProvider.Settings.AESKey);

                // 직렬화된 데이터를 역직렬화
                var dataObject = DeserializeUtil.DeserializeObject<ResourcesDataLoader>(decryptedData);
                return dataObject;
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                return null;
            }
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
            var fields = dataType.GetFields(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);
            foreach (var field in fields)
            {
                sb.Append(field.Name);
                sb.Append(':');
                sb.Append(ShortExpressionTypeName(field.FieldType.Name));
                sb.Append(CsvToBinaryConverter.CSV_SEPARATOR);
            }

            sb.Remove(sb.Length - 1, 1);
            sb.Append("\n");

            // records
            var dataDictType = typeof(DynamicDataObject<>).MakeGenericType(dataType);
            var listField = dataDictType.GetField("List");
            var dataListObj = listField.GetValue(data);
            foreach (var item in (IEnumerable)dataListObj)
            {
                foreach (var field in fields)
                {
                    sb.Append(field.GetValue(item));
                    sb.Append(CsvToBinaryConverter.CSV_SEPARATOR);
                }

                sb.Remove(sb.Length - 1, 1);
                sb.Append("\n");
            }

            var targetPath = Path.Combine(DataTableSettingsProvider.Settings.CsvSourcePath, $"{targetName}_.csv");
            File.WriteAllText(targetPath, sb.ToString());
        }

        private static string ShortExpressionTypeName(string type) => type switch
        {
            "String" => "string",
            "Int32" => "int",
            "Int64" => "long",
            "Single" => "float",
            "DateTime" => "datetime",
            "List<System.Int32>" => "list<int>",
            "List<System.Single>" => "list<float>",
            "List<String>" => "list<string>",
            "Double" => "double",
            "BigNum" => "double",
            "BigNumber" => "double",
            "(String, Double)" => "curpair",
            "List<(String, Double)>" => "list<curpair>",
            _ => type,
        };
    }
}
