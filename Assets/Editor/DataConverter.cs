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
        private static Dictionary<string, List<string>> ColumnDic = new();

        public static async Task ConvertToDataAsync(string[] filePaths)
        {
            CSVFilePaths = filePaths;
            InitializeWorks();

            foreach (var work in WorkList)
                await work;
        }

        private static void InitializeWorks()
        {
            ColumnDic.Clear();
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
                await ParseCSV(path);
                await Task.Yield();
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
        private static async Task ParseCSV(string filePath)
        {
            var lines = await File.ReadAllLinesAsync(filePath);
            if (lines.Length == 0) return;
            var columns = lines[0].Split(',');
            if (columns.Length == 0) return;

            var fileName = Path.GetFileNameWithoutExtension(filePath);
            await ParseSchemeLine(fileName, columns);
        }

        private static async Task ParseSchemeLine(string fileName, string[] columns)
        {
            RemovePrevSchemeFiles(fileName);
            if (!ColumnDic.TryGetValue(fileName, out var columnList))
            {
                columnList = new List<string>();
                ColumnDic.TryAdd(fileName, columnList);
            }
            columnList.Clear();
            
            var saveFilePath = Path.Combine(SchemeDirPath, fileName + ".proto");
            await using var fs = new FileStream(saveFilePath, FileMode.CreateNew);
            await using var sw = new StreamWriter(fs);
            await sw.WriteAsync(DefaultProtoImportLine);
            for (int i=0; i<columns.Length; ++i)
            {
                var column = columns[i];
                if (column.StartsWith("#"))
                {
                    columnList.Add("#");
                    continue;
                }
                if (column.StartsWith())
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
