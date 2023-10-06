using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace Causeless3t.Data.Editor
{
    public static class DataConverter
    {
        private static readonly string SchemeDirPath = Path.Combine(Application.dataPath, "Deploy/Runtime/Scheme");
        private static readonly string DataDirPath = Path.Combine(Application.dataPath, "Deploy/Runtime/Data");
        private static readonly string DefaultProtoImportLine = "syntax = \"proto3\";\npackage Protocols;";
        private static readonly string BaseProtoFilePath = Path.Combine(SchemeDirPath, "Base.proto");
        private static readonly string BaseProtoFileContents = "message Vector2 {\n  float x = 1;\n  float y = 2;\n}\n" +
                                                               "message Vector3 {\n  float x = 1;\n  float y = 2;\n  float z = 3;\n}\n" +
                                                               "message Vector4 {\n  float x = 1;\n  float y = 2;\n  float z = 3;\n  float w = 4;\n}\n";

        #region Convert Process
        private static readonly List<Task> WorkList = new();
        private static int CurrentStep = 0;
        private static string[] CSVFilePaths;
        private static Dictionary<string, SchemeInfo> SchemeDic = new();

        internal class SchemeInfo
        {
            public enum eFeatureType
            {
                None = 0,
                Comment,
                Primary,
                Set,
                Get
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
            CSVFilePaths = filePaths;
            InitializeWorks();

            foreach (var work in WorkList)
                await work;
        }

        private static void InitializeWorks()
        {
            SchemeDic.Clear();
            WorkList.Clear();
            CurrentStep = 0;

            WorkList.Add(CreateBaseProtoFile());
            WorkList.Add(ConvertCSVToSchemeProto());
            WorkList.Add(CompileProtobuf());
            
            ShowProgress("Initialize");
        }
        
        private static async Task CreateBaseProtoFile()
        {
            if (File.Exists(BaseProtoFilePath)) return;
            
            await using var fs = new FileStream(BaseProtoFilePath, FileMode.CreateNew);
            await using var sw = new StreamWriter(fs);
            await sw.WriteLineAsync(DefaultProtoImportLine);
            await sw.WriteLineAsync("");
            await sw.WriteLineAsync(BaseProtoFileContents);
        }

        private static async Task ConvertCSVToSchemeProto()
        {
            ShowProgress("Convert CSV to Protobuf Scheme");
            foreach (var path in CSVFilePaths)
            {
                if (!await ParseCSV(path)) continue;
                await CreateSchemeFile(path);
            }
        }

        private static Task CompileProtobuf()
        {
            ShowProgress("Compile Protobuf");
            AssetDatabase.Refresh();
            CompileAllInProject();
            return Task.CompletedTask;
        }
        
        private static void ShowProgress(string message)
        {
            EditorUtility.ClearProgressBar();
            var progress = (float) CurrentStep / WorkList.Count;
            EditorUtility.DisplayProgressBar("Converting", $"{CurrentStep}/{WorkList.Count} {progress * 100}% {message}..", progress);
        }
        #endregion Convert Process

        #region Scheme
        private static async Task<bool> ParseCSV(string filePath)
        {
            var lines = await File.ReadAllLinesAsync(filePath);
            if (lines.Length == 0) return false;
            var columns = lines[0].Split(',');
            if (columns.Length <= 1) return false;
            
            ParseSchemeLine(Path.GetFileNameWithoutExtension(filePath), columns);
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
            
            for (int i=0; i<columns.Length; ++i)
            {
                var column = columns[i];
                if (column.StartsWith("#"))
                {
                    AddFeatureColumn(schemeInfo);
                    continue;
                }
                
                var featureSplit = column.Split('/');
                if (featureSplit.Length == 2)
                {
                    if (!IsValidTypeFeature(featureSplit[0]))
                    {
                        AddFeatureColumn(schemeInfo);
                        continue;
                    }

                    var featureType = GetFeatureType(featureSplit[0]);
                    schemeInfo.Features.Add(featureType);

                    switch (featureType)
                    {
                        case SchemeInfo.eFeatureType.Primary:
                        case SchemeInfo.eFeatureType.Get:
                            column = featureSplit[1];
                            break;
                        case SchemeInfo.eFeatureType.Set:
                            schemeInfo.Types.Add(featureSplit[1]);
                            schemeInfo.Names.Add("");
                            continue;
                    }
                }
                else if (featureSplit.Length != 1)
                {
                    AddFeatureColumn(schemeInfo);
                    continue;
                }
                
                var split = column.Split(':');
                if (split.Length != 2)
                {
                    AddFeatureColumn(schemeInfo);
                    continue;
                }
                if (GetFeatureType(featureSplit[0]) == SchemeInfo.eFeatureType.Get)
                    schemeInfo.Types.Add($"e{split[0]}");
                else
                    schemeInfo.Types.Add(GetTypeString(split[0]));
                schemeInfo.Names.Add(split[1]);
                if (schemeInfo.Features.Count < schemeInfo.Types.Count)
                    schemeInfo.Features.Add(SchemeInfo.eFeatureType.None);
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

        private static SchemeInfo.eFeatureType GetFeatureType(string featureType) => featureType.ToLower() switch
        {
            "primary" => SchemeInfo.eFeatureType.Primary,
            "set" => SchemeInfo.eFeatureType.Set,
            "get" => SchemeInfo.eFeatureType.Get,
            _ => SchemeInfo.eFeatureType.None
        };

        private static string GetTypeString(string name) => name.ToLower() switch
        {
            "string" => "string",
            "int" => "int32",
            "long" => "int64",
            "bool" => "bool",
            "short" => "int32",
            "float" => "float",
            "vector2" => "Vector2",
            "vector3" => "Vector3",
            "vector4" => "Vector4",
            _ => throw new ArgumentException($"{name} type doesn't exist parser!")
        };

        private static bool IsValidTypeFeature(string feature) => feature.ToLower().Equals("primary") ||
                                                                  feature.ToLower().Equals("set") ||
                                                                  feature.ToLower().Equals("get");

        private static void AddFeatureColumn(SchemeInfo schemeInfo)
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
            await using var fs = new FileStream(saveFilePath, FileMode.CreateNew);
            await using var sw = new StreamWriter(fs);
            await sw.WriteLineAsync(DefaultProtoImportLine);
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
                    case SchemeInfo.eFeatureType.Set:
                        continue;
                    case SchemeInfo.eFeatureType.Get:
                    case SchemeInfo.eFeatureType.Primary:
                    case SchemeInfo.eFeatureType.None:
                        await WriteIndentTab(sw);
                        await sw.WriteLineAsync($"{schemeInfo.Types[i]} {schemeInfo.Names[i]} = {index++};");
                        break;
                }
            }
            await WriteCloseBrace(sw);
        }

        private static async Task WriteEnumType(string filePath, StreamWriter sw, SchemeInfo schemeInfo)
        {
            for (int i = 0; i < schemeInfo.Types.Count; ++i)
            {
                if (schemeInfo.Features[i] != SchemeInfo.eFeatureType.Set) continue;
                
                await WriteEnumOpenBrace(sw, schemeInfo.Types[i]);
                var lines = await File.ReadAllLinesAsync(filePath);
                for (int j = 1; j < lines.Length; ++j)
                {
                    var columns = lines[j].Split(',');
                    if (columns.Length <= 1 || columns.Length <= i) continue;
                    await WriteIndentTab(sw);
                    await sw.WriteLineAsync($"{columns[i]} = {j-1};");
                }
                await WriteCloseBrace(sw);
            }
        }
        
        private static async Task WriteIndentTab(StreamWriter sw) => await sw.WriteAsync("  ");
        private static async Task WriteCloseBrace(StreamWriter sw) => await sw.WriteLineAsync("}");
        private static async Task WriteEnumOpenBrace(StreamWriter sw, string name) => await sw.WriteLineAsync(string.Format("enum e{0} {{", name));
        private static async Task WriteMessageOpenBrace(StreamWriter sw, string name) => await sw.WriteLineAsync(string.Format("message {0} {{", name));
        #endregion Scheme
        
        #region ProtoBuf
        private static readonly string ProtocPath = Path.Combine(Application.dataPath, "Protoc~/protoc");
        
        private static void CompileAllInProject()
        {
            string[] protoFiles = Directory.GetFiles(Application.dataPath, "*.proto", SearchOption.AllDirectories);
            string[] includePaths = new string[protoFiles.Length];
            for (int i = 0; i < protoFiles.Length; i++)
            {
                string protoFolder = Path.GetDirectoryName(protoFiles[i]);
                includePaths[i] = protoFolder;
            }
            foreach (string s in protoFiles) 
                CompileProtobufSystemPath(s, includePaths);
            AssetDatabase.Refresh();
        }

        private static void CompileProtobufSystemPath(string protoFileSystemPath, string[] includePaths)
        {
            if (Path.GetExtension(protoFileSystemPath) != ".proto") return;
            string outputPath = Path.GetDirectoryName(protoFileSystemPath);

            string options = " --csharp_out \"{0}\" ";
            foreach (string s in includePaths)
            {
                options += string.Format(" --proto_path \"{0}\" ", s);
            }

            string finalArguments = string.Format("\"{0}\"", protoFileSystemPath) + string.Format(options, outputPath);

            ProcessStartInfo startInfo = new ProcessStartInfo() { FileName = ProtocPath, Arguments = finalArguments };

            Process proc = new Process { StartInfo = startInfo };
            proc.StartInfo.UseShellExecute = false;
            proc.StartInfo.RedirectStandardOutput = true;
            proc.StartInfo.RedirectStandardError = true;
            proc.Start();

            string output = proc.StandardOutput.ReadToEnd();
            string error = proc.StandardError.ReadToEnd();
            proc.WaitForExit();

            if (output != "")
            {
                Debug.Log("Protobuf Unity : " + output);
            }
            Debug.Log("Protobuf Unity : Compiled " + Path.GetFileName(protoFileSystemPath));

            if (error != "")
            {
                Debug.LogError("Protobuf Unity : " + error);
            }
        }
        #endregion ProtoBuf
    }
}
