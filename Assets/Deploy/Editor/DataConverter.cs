using ExcelDataReader;
using Google.Protobuf;
using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEditor;
using UnityEditor.Compilation;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace Causeless3t.Data.Editor
{
    public static class DataConverter
    {
        private static string SchemeDirPath = Path.Combine(Application.dataPath, "Deploy/Runtime/Scheme");
        private static string DataDirPath = Path.Combine(Application.dataPath, "Deploy/Runtime/Data");
        private static readonly string DefaultProtoImportLine = "syntax = \"proto3\";\nimport \"Base.proto\";\npackage Causeless3t.{0};";
        private static string BaseProtoFilePath => Path.Combine(SchemeDirPath, "Base.proto");
        private static readonly string BaseProtoFileContents = "syntax = \"proto3\";\npackage Causeless3t;\n\n" +
                                                               "message DateTime {\n  string time = 1;\n}\n" +
                                                               "message Vector2 {\n  float x = 1;\n  float y = 2;\n}\n" +
                                                               "message Vector3 {\n  float x = 1;\n  float y = 2;\n  float z = 3;\n}\n" +
                                                               "message Vector4 {\n  float x = 1;\n  float y = 2;\n  float z = 3;\n  float w = 4;\n}\n";
        private static readonly string CommonProtoMessage = "message Table{0} {{\n  repeated {0} list = 1;\n  map<{1}, {0}> dict = 2;\n}}\n";
        private static readonly string[] BaseValidTypes = { "int", "string", "float", "Vector2", "Vector3", "Vector4", "DateTime", "bool", "long" };
        private static readonly List<string> ValidTypes = new List<string>();

        public static void SetExportPath(string exportRootPath)
        {
            SchemeDirPath = Path.Combine(exportRootPath, "Scheme");
            if (!Directory.Exists(SchemeDirPath))
                Directory.CreateDirectory(SchemeDirPath);
            DataDirPath = Path.Combine(exportRootPath, "Data");
            if (!Directory.Exists(DataDirPath))
                Directory.CreateDirectory(DataDirPath);
        }

        #region Convert Process
        private static readonly List<Task> WorkList = new();
        private static string CurrentMessage;
        private static int CurrentStep;
        private static string[] ExcelFilePaths;
        private static Dictionary<string, SchemeInfo> SchemeDic = new();
        private static Dictionary<string, DataSet> ExcelDataDic = new();

        [Serializable]
        internal class SchemeInfo
        {
            public enum eFeatureType
            {
                Normal = 0,
                Comment,
                Enum
            }

            public List<string> Names;
            public List<string> Types;
            public List<eFeatureType> Features;

            public SchemeInfo(int columnCount)
            {
                Names = new(columnCount);
                Types = new(columnCount);
                Features = new(columnCount);
            }
        }

        public static async void ConvertToDataAsync(string[] filePaths)
        {
            if (File.Exists(Path.Combine(Application.dataPath, "TablePathData.dat")))
            {
                var rootPath = await File.ReadAllTextAsync(Path.Combine(Application.dataPath, "TablePathData.dat"));
                SetExportPath(rootPath);
            }

            ExcelFilePaths = filePaths;
            InitializeWorks();

            await CreateBaseProtoFile();
            await ConvertExcelToSchemeProto();
            await CompileProtobuf();
            ParseDataProtobuf();

            AssetDatabase.Refresh();
            EditorUtility.ClearProgressBar();
        }

        private static void InitializeWorks()
        {
            SchemeDic.Clear();
            ExcelDataDic.Clear();
            ValidTypes.Clear();
            ValidTypes.AddRange(BaseValidTypes);

            WorkList.Clear();
            CurrentStep = 0;

            ShowProgress("Initialize");
        }

        private static async Task CreateBaseProtoFile()
        {
            CurrentStep++;
            CurrentMessage = "Create Base.proto File";
            ShowProgress(CurrentMessage);
            if (File.Exists(BaseProtoFilePath)) return;

            using var fs = new FileStream(BaseProtoFilePath, FileMode.CreateNew);
            using var sw = new StreamWriter(fs);
            await sw.WriteLineAsync(BaseProtoFileContents);
        }

        private static async Task ConvertExcelToSchemeProto()
        {
            CurrentStep++;
            CurrentMessage = "Convert Excel to Protobuf Scheme";
            ShowProgress(CurrentMessage);
            foreach (var path in ExcelFilePaths)
            {
                if (!await ParseExcel(path)) continue;
                await CreateSchemeFile(path);
            }
        }

        private static async Task CompileProtobuf()
        {
            CurrentStep++;
            CurrentMessage = "Compile Protobuf";
            ShowProgress(CurrentMessage);
            await CompileAllInProject();
        }

        private static void ShowProgress(string message)
        {
            EditorUtility.ClearProgressBar();
            var progress = (float)CurrentStep / 4;
            EditorUtility.DisplayProgressBar("Converting", $"{CurrentStep}/4 {progress * 100}% {message}..", progress);
        }
        #endregion Convert Process

        #region Scheme
        private static async Task<bool> ParseExcel(string filePath)
        {
            using var stream = File.Open(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
            using var reader = ExcelReaderFactory.CreateReader(stream);
            var result = reader.AsDataSet();

            if (result.Tables.Count == 0 || result.Tables[0].Rows.Count < 4) return false;

            List<string> columns = new();
            foreach (var column in result.Tables[0].Rows[0].ItemArray)
                columns.Add(column.ToString());
            if (columns.Count <= 1) return false;

            ExcelDataDic.Add(filePath, result);

            ParseSchemeLine(Path.GetFileNameWithoutExtension(filePath), columns.ToArray());
            return true;
        }

        private static void ParseSchemeLine(string fileName, string[] columns)
        {
            RemovePrevSchemeFiles(fileName);
            if (!SchemeDic.TryGetValue(fileName, out var schemeInfo))
            {
                schemeInfo = new SchemeInfo(columns.Length);
                SchemeDic.TryAdd(fileName, schemeInfo);
            }

            for (int i = 0; i < columns.Length; ++i)
            {
                var column = columns[i];
                if (column.StartsWith("#"))
                {
                    AddFeatureCommentColumn(schemeInfo);
                    continue;
                }

                var featureSplit = column.Replace(" ", "").Split(':');
                if (featureSplit.Length == 2)
                {
                    if (!IsValidType(featureSplit[1]))
                    {
                        AddFeatureCommentColumn(schemeInfo);
                        continue;
                    }

                    schemeInfo.Types.Add(featureSplit[1]);
                    schemeInfo.Names.Add(featureSplit[0]);
                    schemeInfo.Features.Add(SchemeInfo.eFeatureType.Normal);
                }
                else if (featureSplit.Length == 1 && featureSplit[0].Length > 1 && featureSplit[0].StartsWith("e"))
                {
                    schemeInfo.Types.Add(featureSplit[0]);
                    schemeInfo.Names.Add("");
                    schemeInfo.Features.Add(SchemeInfo.eFeatureType.Enum);
                    ValidTypes.Add(featureSplit[0]);
                    continue;
                }
                else
                {
                    AddFeatureCommentColumn(schemeInfo);
                    continue;
                }
            }
        }

        private static void RemovePrevSchemeFiles(string fileName)
        {
            var csFilePath = Path.Combine(SchemeDirPath, fileName + ".cs");
            var protoFilePath = Path.Combine(SchemeDirPath, fileName + ".proto");
            if (File.Exists(csFilePath))
                File.Delete(csFilePath);
            if (File.Exists(protoFilePath))
                File.Delete(protoFilePath);
        }

        private static bool IsBaseType(string typeString) => BaseValidTypes.FirstOrDefault(t => t.Equals(typeString)) != null;
        private static bool IsValidType(string typeString) => ValidTypes.FirstOrDefault(t => t.Equals(typeString)) != null;

        private static string ToConvertProtoType(string typeString) => typeString switch
        {
            "int" => "int32",
            "long" => "int64",
            _ => typeString
        };

        private static void AddFeatureCommentColumn(SchemeInfo schemeInfo)
        {
            schemeInfo.Types.Add("");
            schemeInfo.Names.Add("");
            schemeInfo.Features.Add(SchemeInfo.eFeatureType.Comment);
        }

        private static async Task CreateSchemeFile(string filePath)
        {
            var fileName = Path.GetFileNameWithoutExtension(filePath);
            if (!SchemeDic.TryGetValue(fileName, out var schemeInfo)) return;

            var saveFilePath = Path.Combine(SchemeDirPath, fileName + ".proto");
            using var fs = new FileStream(saveFilePath, FileMode.CreateNew);
            using var sw = new StreamWriter(fs);
            await sw.WriteLineAsync(string.Format(DefaultProtoImportLine, fileName));
            await sw.WriteLineAsync("");

            await WriteEnumType(filePath, sw, schemeInfo);
            await sw.WriteLineAsync("");
            await WriteMessageOpenBrace(sw, fileName);

            int index = 1;
            for (int i = 0; i < schemeInfo.Types.Count; ++i)
            {
                switch (schemeInfo.Features[i])
                {
                    case SchemeInfo.eFeatureType.Comment:
                    case SchemeInfo.eFeatureType.Enum:
                        continue;
                    default:
                        await WriteIndentTab(sw);
                        await sw.WriteLineAsync($"{ToConvertProtoType(schemeInfo.Types[i])} {schemeInfo.Names[i]} = {index++};");
                        break;
                }
            }
            await WriteCloseBrace(sw);

            // common
            await sw.WriteLineAsync("");
            await sw.WriteLineAsync(string.Format(CommonProtoMessage, fileName, ToConvertProtoType(schemeInfo.Types[0])));
        }

        private static async Task WriteEnumType(string filePath, StreamWriter sw, SchemeInfo schemeInfo)
        {
            if (!ExcelDataDic.TryGetValue(filePath, out var result)) return;

            for (int i = 0; i < schemeInfo.Types.Count; ++i)
            {
                if (schemeInfo.Features[i] != SchemeInfo.eFeatureType.Enum) continue;

                await WriteEnumOpenBrace(sw, schemeInfo.Types[i]);
                int autoIndex = 0;
                for (int j = 3; j < result.Tables[0].Rows.Count; ++j)
                {
                    var columnSplit = result.Tables[0].Rows[j][i].ToString().Split('=');
                    if (columnSplit.Length < 1 || columnSplit.Length > 2) break;
                    if (string.IsNullOrEmpty(columnSplit[0])) break;
                    await WriteIndentTab(sw);
                    if (columnSplit.Length == 1)
                        await sw.WriteLineAsync($"{columnSplit[0]} = {autoIndex++};");
                    else
                    {
                        if (!int.TryParse(columnSplit[1], out var index))
                            index = autoIndex;
                        await sw.WriteLineAsync($"{columnSplit[0]} = {index};");
                        autoIndex = index + 1;
                    }
                }
                await WriteCloseBrace(sw);
            }
        }

        private static async Task WriteIndentTab(StreamWriter sw) => await sw.WriteAsync("  ");
        private static async Task WriteCloseBrace(StreamWriter sw) => await sw.WriteLineAsync("}");
        private static async Task WriteEnumOpenBrace(StreamWriter sw, string name) => await sw.WriteLineAsync($"enum {name} {{");
        private static async Task WriteMessageOpenBrace(StreamWriter sw, string name) => await sw.WriteLineAsync($"message {name} {{");
        #endregion Scheme

        #region ProtoBuf
#if UNITY_EDITOR_WIN
        private static readonly string ProtocPackagePath = System.IO.Path.Combine(Application.dataPath, "../Library/PackageCache/com.causeless3t.datatable/grpc-protoc_24.3/protoc32.exe");
        private static readonly string ProtocLocalPath = Path.Combine(Application.dataPath, "Deploy/grpc-protoc_24.3/protoc32.exe");
#else
        private static readonly string ProtocPackagePath = System.IO.Path.Combine(Application.dataPath, "../Library/PackageCache/com.causeless3t.datatable/grpc-protoc_24.3/protoc");
        private static readonly string ProtocLocalPath = Path.Combine(Application.dataPath, "Deploy/grpc-protoc_24.3/protoc");
#endif

        private static async Task CompileAllInProject()
        {
            string[] protoFiles = Directory.GetFiles(SchemeDirPath, "*.proto", SearchOption.AllDirectories);
            StringBuilder sb = new();
            foreach (string protoFile in protoFiles)
                sb.Append($" \"{protoFile}\"");
            await CompileProtobufSystemPath(sb.ToString());
        }

        private static async Task CompileProtobufSystemPath(string protoFileSystemPath)
        {
            string options = $"--csharp_out=\"{SchemeDirPath}\" --proto_path=\"{SchemeDirPath}\" {protoFileSystemPath}";
            Debug.Log(options);

            var protocPath = File.Exists(ProtocPackagePath) ? ProtocPackagePath : ProtocLocalPath;
            ProcessStartInfo startInfo = new ProcessStartInfo() { FileName = protocPath, Arguments = options };

            Process proc = new Process { StartInfo = startInfo };
            proc.StartInfo.UseShellExecute = false;
            proc.StartInfo.RedirectStandardOutput = true;
            proc.StartInfo.RedirectStandardError = true;
            proc.Start();

            string output = await proc.StandardOutput.ReadToEndAsync();
            string error = await proc.StandardError.ReadToEndAsync();
            await proc.WaitForExitAsync();

            if (!string.IsNullOrEmpty(output))
                Debug.Log("Protobuf Unity : " + output);
            Debug.Log("Protobuf Unity : Compiled");
            if (!string.IsNullOrEmpty(error))
                Debug.LogError("Protobuf Unity : " + error);

            CompilationPipeline.compilationFinished += OnDomainReloaded;
            AssetDatabase.Refresh();            
        }

        private static void OnDomainReloaded(object obj)
        {
            // 도메인 리로딩으로 인한 재빌드
            CompilationPipeline.compilationFinished -= OnDomainReloaded;
            Debug.Log("Domain reloaded");
            ParseDataProtobuf();
            AssetDatabase.Refresh();
            EditorUtility.ClearProgressBar();
        }

        /// <summary>
        /// Waits asynchronously for the process to exit.
        /// </summary>
        /// <param name="process">The process to wait for cancellation.</param>
        /// <param name="cancellationToken">A cancellation token. If invoked, the task will return 
        /// immediately as canceled.</param>
        /// <returns>A Task representing waiting for the process to end.</returns>
        private static Task WaitForExitAsync(this Process process,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            if (process.HasExited) return Task.CompletedTask;

            var tcs = new TaskCompletionSource<bool>();
            process.EnableRaisingEvents = true;
            process.Exited += (sender, args) => tcs.TrySetResult(true);
            if (cancellationToken != default(CancellationToken))
                cancellationToken.Register(() => tcs.SetCanceled());

            return process.HasExited ? Task.CompletedTask : tcs.Task;
        }
        #endregion ProtoBuf

        #region Parsing
        private static void ParseDataProtobuf()
        {
            CurrentStep++;
            CurrentMessage = "Parsing Protobuf";
            ShowProgress(CurrentMessage);
            ParseData();
        }

        private static void ParseData()
        {
            foreach (var path in ExcelFilePaths)
            {
                var className = Path.GetFileNameWithoutExtension(path);
                if (!SchemeDic.TryGetValue(className, out var schemeInfo)) continue;
                if (!ExcelDataDic.TryGetValue(path, out var excel)) continue;

                Type classType = Type.GetType($"Causeless3T.{className}.{className}, Assembly-CSharp");
                Type tableType = Type.GetType($"Causeless3T.{className}.Table{className}, Assembly-CSharp");
                if (classType == null || tableType == null)
                {
                    Debug.LogWarning($"Generated class {className} not found!");
                    return;
                }
                Debug.Log($"Successfully loaded type: {classType.Name}");

                // 동적으로 생성된 클래스 인스턴스화
                object tableInstance = Activator.CreateInstance(tableType);
                MethodInfo listAddMethod = null;
                MethodInfo dictAddMethod = null;
                var list = tableType.GetProperty("List").GetValue(tableInstance);
                var methods = list.GetType().GetMethods();
                foreach (var method in methods)
                {
                    if (!method.Name.Equals("Add")) continue;
                    var parameters = method.GetParameters();
                    if (parameters.Length == 1 && parameters[0].ParameterType == classType)
                    {
                        listAddMethod = method;
                        break;
                    }
                }
                var dict = tableType.GetProperty("Dict").GetValue(tableInstance);
                methods = dict.GetType().GetMethods();
                foreach (var method in methods)
                {
                    if (!method.Name.Equals("Add")) continue;
                    var parameters = method.GetParameters();
                    if (parameters.Length == 2 && parameters[1].ParameterType == classType)
                    {
                        dictAddMethod = method;
                        break;
                    }
                }
                if (listAddMethod == null || dictAddMethod == null) continue;

                for (int i = 3; i < excel.Tables[0].Rows.Count; ++i)
                {
                    object instance = null;
                    for (int j = 0; j < schemeInfo.Features.Count; ++j)
                    {
                        if (schemeInfo.Features[j] != SchemeInfo.eFeatureType.Normal) continue;
                        instance = Activator.CreateInstance(classType);
                        if (IsBaseType(schemeInfo.Types[j]))
                            SetProperty(instance, schemeInfo.Names[j], ConvertValue(schemeInfo.Types[j], excel.Tables[0].Rows[i][j].ToString()));
                        else
                            SetProperty(instance, schemeInfo.Names[j], Enum.Parse(classType.Assembly.GetType($"Causeless3T.{className}.{schemeInfo.Types[j]}"), excel.Tables[0].Rows[i][j].ToString()));
                    }
                    if (instance == null) continue;

                    listAddMethod.Invoke(list, new object[] { instance });
                    dictAddMethod.Invoke(dict, new object[] { ConvertValue(schemeInfo.Types[0], excel.Tables[0].Rows[i][0].ToString()), instance });
                }

                SerializeProtobuf(tableInstance, className);
            }
            AssetDatabase.Refresh();
        }

        private static object ConvertValue(string typeString, string value)
        {
            switch (typeString)
            {
                case "int": return Convert.ToInt32(value);
                case "long": return Convert.ToInt64(value);
                case "bool": return Convert.ToBoolean(value);
                case "float": return Convert.ToSingle(value);
                default: return value;
            }
        }

        private static void SetProperty<T>(object instance, string fieldName, T value)
        {
            Type type = instance.GetType();
            var field = type.GetProperty(fieldName);
            if (field == null || !field.CanWrite)
            {
                Debug.LogError($"필드 '{fieldName}'를 찾을 수 없거나 쓰기가 불가능합니다.");
                return;
            }
            field.SetValue(instance, value);
        }

        private static void SerializeProtobuf(object instance, string fileName)
        {
            var saveFilePath = Path.Combine(DataDirPath, fileName + ".dat");
            using var fs = File.Create(saveFilePath);
            using var codedStream = new CodedOutputStream(fs);
            MethodInfo writeToMethod = instance.GetType().GetMethod("WriteTo");
            if (writeToMethod == null)
            {
                Debug.LogError("WriteTo 메서드를 찾을 수 없습니다.");
                return;
            }
            writeToMethod.Invoke(instance, new object[] { codedStream });
            Debug.Log($"Successfully wrote file: {saveFilePath}");
        }
        #endregion Parsing
    }
}
