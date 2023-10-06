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

        public static async Task ConvertToDataAsync(string[] filePaths)
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

            WorkList.Add(new Task(ConvertCSVToSchemeProto));
            WorkList.Add(new Task(CompileProtobuf));
            
            ShowProgress("Initialize");
        }

        private static async void ConvertCSVToSchemeProto()
        {
            ShowProgress("Convert CSV to Protobuf Scheme");
            foreach (var path in CSVFilePaths)
            {
                if (!await ParseCSV(path)) continue;
                await CreateSchemeFile(Path.GetFileNameWithoutExtension(path));
            }
        }

        private static async void CompileProtobuf()
        {
            ShowProgress("Compile Protobuf");
            await CompileAllInProject();
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
            if (columns.Length == 0) return false;
            
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
                    
                    var featureType = featureSplit[0].ToLower() switch
                    {
                        "primary" => SchemeInfo.eFeatureType.Primary,
                        "set" => SchemeInfo.eFeatureType.Set,
                        "get" => SchemeInfo.eFeatureType.Get,
                        _ => SchemeInfo.eFeatureType.None
                    };
                    schemeInfo.Features.Add(featureType);

                    switch (featureType)
                    {
                        case SchemeInfo.eFeatureType.Primary:
                            column = featureSplit[1];
                            break;
                        case SchemeInfo.eFeatureType.Set:
                        case SchemeInfo.eFeatureType.Get:
                            schemeInfo.Types.Add(featureSplit[1]);
                            schemeInfo.Names.Add("");
                            continue;
                    }
                }
                else if (featureSplit.Length != 0)
                {
                    AddFeatureColumn(schemeInfo);
                    continue;
                }
                
                var split = column.Split(':');
                if (split.Length != 2 || !IsValidTypeValue(split[0]))
                {
                    AddFeatureColumn(schemeInfo);
                    continue;
                }
                schemeInfo.Types.Add(split[0]);
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

        private static bool IsValidTypeFeature(string feature) => feature.ToLower().Equals("primary") ||
                                                                  feature.ToLower().Equals("set") ||
                                                                  feature.ToLower().Equals("get");
        
        private static bool IsValidTypeValue(string value) => value.ToLower().Equals("string") ||
                                                              value.ToLower().Equals("int") ||
                                                              value.ToLower().Equals("long") ||
                                                              value.ToLower().Equals("bool") ||
                                                              value.ToLower().Equals("short") ||
                                                              value.ToLower().Equals("vector2") ||
                                                              value.ToLower().Equals("vector3") ||
                                                              value.ToLower().Equals("vector4") ||
                                                              value.ToLower().Equals("float");

        private static void AddFeatureColumn(SchemeInfo schemeInfo)
        {
            schemeInfo.Types.Add("");
            schemeInfo.Names.Add("");
            schemeInfo.Features.Add(SchemeInfo.eFeatureType.Comment);
        }

        private static async Task CreateSchemeFile(string fileName)
        {
            if (!SchemeDic.TryGetValue(fileName, out var schemeInfo)) return;
            
            var saveFilePath = Path.Combine(SchemeDirPath, fileName + ".proto");
            await using var fs = new FileStream(saveFilePath, FileMode.CreateNew);
            await using var sw = new StreamWriter(fs);
            await sw.WriteAsync(DefaultProtoImportLine);
            
            
        }
        #endregion Scheme
        
        #region ProtoBuf
        private static readonly string ProtocPath = Path.Combine(Application.dataPath, "Protoc~/protoc");
        
        private static async Task CompileAllInProject()
        {
            string[] protoFiles = Directory.GetFiles(Application.dataPath, "*.proto", SearchOption.AllDirectories);
            string[] includePaths = new string[protoFiles.Length];
            for (int i = 0; i < protoFiles.Length; i++)
            {
                string protoFolder = Path.GetDirectoryName(protoFiles[i]);
                includePaths[i] = protoFolder;
            }
            foreach (string s in protoFiles)
            {
                CompileProtobufSystemPath(s, includePaths);
                await Task.Yield();
            }
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

            Process proc = new Process() { StartInfo = startInfo };
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
