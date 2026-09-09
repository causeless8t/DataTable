#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEngine;

namespace Causeless3t.Table
{
    public static class CsvToBinaryConverter
    {
        public static readonly char CSV_SEPARATOR = '|';

        public static bool CreateSchemeClass(string fileName, string csvContents)
        {
            var lines = csvContents.Split("\n");
            if (lines.Length < 2)
                throw new Exception("CSV 파일에 데이터가 없습니다.");

            string[] schemaParts = lines[0].Split(CSV_SEPARATOR);
            var schema = ParseSchema(schemaParts, out var validColumns);

            return EnsureRecordClass(fileName, schema, validColumns);
        }
        
        private static bool EnsureRecordClass(
            string className,
            List<(string propName, string typeName)> schema,
            List<bool> validColumns)
        {
            var type = FindTypeByName(className);

            if (type != null && IsTypeCompatible(type, schema, validColumns))
            {
                return false;
            }

            if (DynamicClassGenerator.Generate(
                    className,
                    schema,
                    validColumns))
            {
                Debug.Log($"[{className}] 스키마 클래스 코드를 생성했습니다.");
                return true;
            }
            return false;
        }

        private static List<(string propName, string typeName)> ParseSchema(string[] schemaParts,
            out List<bool> validColumns)
        {
            var schema = new List<(string, string)>();
            validColumns = new List<bool>();

            foreach (var part in schemaParts)
            {
                if (part.StartsWith("#"))
                {
                    schema.Add((string.Empty, string.Empty));
                    validColumns.Add(false);
                    continue;
                }

                var split = part.Split(':');
                if (split.Length != 2)
                {
                    schema.Add((string.Empty, string.Empty));
                    validColumns.Add(false);
                    Debug.LogError($"스키마 오류: {part}");
                    continue;
                }

                schema.Add((split[0], split[1].TrimEnd('\r')));
                validColumns.Add(true);
            }

            return schema;
        }

        public static Type FindTypeByName(string className)
        {
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                var type = assembly.GetType($"{DataTableSettingsProvider.Settings.Namespace}.{className}");
                if (type != null)
                    return type;
            }

            return null;
        }

        private static bool IsTypeCompatible(Type type, List<(string propName, string typeName)> schema,
            List<bool> validColumns)
        {
            var writeMethod = type.GetMethod("Write", new[] { typeof(BinaryWriter) });
            if (writeMethod == null)
                return false;
            var readMethod = type.GetMethod("Read", new[] { typeof(BinaryReader) });
            if (readMethod == null)
                return false;

            var expectedFieldCount = 0;

            for (var i = 0; i < schema.Count; i++)
            {
                if (!validColumns[i])
                    continue;

                expectedFieldCount++;

                var (propName, typeName) = schema[i];

                var field = type.GetField(
                    $"_{propName}",
                    BindingFlags.NonPublic | BindingFlags.Instance);

                if (field == null)
                    return false;

                var expectedType =
                    TypeParser.GetFieldType(
                        typeName.ToLowerInvariant());

                if (field.FieldType != expectedType)
                    return false;
            }

            var fields = type.GetFields(BindingFlags.NonPublic | BindingFlags.Instance);

            return fields.Length == expectedFieldCount;
        }
        
        public static void Convert(string fileName, string csvContents, string outputBinaryPath, string aesKey)
        {
            var lines = csvContents.Split("\n");
            if (lines.Length < 2)
                throw new Exception("CSV 파일에 데이터가 없습니다.");

            string[] schemaParts = lines[0].Split(CSV_SEPARATOR);
            var schema = ParseSchema(schemaParts, out var validColumns);

            var recordType = FindTypeByName(fileName);
            if (recordType == null)
            {
                throw new InvalidOperationException(
                    "테이블 타입을 찾을 수 없습니다. " +
                    $"코드 생성 및 컴파일이 완료되었는지 확인해주세요. ({fileName})");
            }

            if (!IsTypeCompatible(recordType, schema, validColumns))
            {
                throw new InvalidOperationException(
                    $"생성된 테이블 타입이 CSV 스키마와 일치하지 않습니다. ({fileName})");
            }
            
            var fields = new FieldInfo[schema.Count];

            for (var i = 0; i < schema.Count; i++)
            {
                if (!validColumns[i])
                    continue;

                fields[i] = recordType.GetField(
                    $"_{schema[i].propName}",
                    BindingFlags.NonPublic |
                    BindingFlags.Instance);
            }

            var dataDictType = typeof(DynamicDataObject<>).MakeGenericType(recordType);
            var dataObject = Activator.CreateInstance(dataDictType);
            var mapField = dataDictType.GetField("Map");
            var listField = dataDictType.GetField("List");
            var dataDictObj = mapField.GetValue(dataObject);
            var dataListObj = listField.GetValue(dataObject);
            var dictInterface = typeof(Dictionary<,>).MakeGenericType(typeof(string), recordType);
            var listInterface = typeof(List<>).MakeGenericType(recordType);

            var dictAdd = dictInterface.GetMethod(
                "TryAdd",
                new[] { typeof(string), recordType });

            var listAdd = listInterface.GetMethod(
                "Add",
                new[] { recordType });

            if (dictAdd == null || listAdd == null)
            {
                throw new MissingMethodException("DynamicDataObject collection method not found.");
            }
            
            string idFieldName = null;
            bool isValidDict = true;
            for (int i = 1; i < lines.Length; i++)
            {
                var line = lines[i];
                if (string.IsNullOrWhiteSpace(line))
                    continue;

                var parts = line.Split(CSV_SEPARATOR);

                string id = null;
                int idIndex = -1;
                for (int j = 0; j < parts.Length; j++)
                {
                    if (string.IsNullOrEmpty(parts[j]) || !validColumns[j])
                        continue;
                    id = parts[j];
                    idIndex = j;
                    break;
                }

                if (idIndex == -1 || string.IsNullOrEmpty(id))
                    continue;

                if (idFieldName == null)
                {
                    idFieldName = schema[idIndex].propName;
                }

                var record = Activator.CreateInstance(recordType);

                for (int j = idIndex; j < parts.Length && j < schema.Count; j++)
                {
                    if (!validColumns[j])
                        continue;

                    var (propName, typeName) = schema[j];
                    var field = fields[j];
                    if (field == null)
                        continue;

                    object value = TypeParser.ParseValue(typeName.ToLowerInvariant(), parts[j]);
                    field.SetValue(record, value);
                }

                bool isValid = (bool)dictAdd.Invoke(dataDictObj, new[] { id, record });
                if (isValidDict && !isValid)
                    isValidDict = false;
                listAdd.Invoke(dataListObj, new[] { record });
            }

            var dictClear = dictInterface.GetMethod("Clear");
            if (dictClear != null && !isValidDict)
                dictClear.Invoke(dataDictObj, null);

            var binary = SerializeObject(dataObject, idFieldName);
            var encrypted = AesEncryption.Encrypt(binary, aesKey);
            File.WriteAllBytes(outputBinaryPath, encrypted);

            Debug.Log($"CSV 변환 완료: {outputBinaryPath}");
        }

        private static byte[] SerializeObject(object obj, string idFieldName)
        {
            using var ms = new MemoryStream();
            using var bw = new BinaryWriter(ms);

            var listField = obj.GetType().GetField("List", BindingFlags.Public | BindingFlags.Instance);

            if (listField?.GetValue(obj) is IList listObj)
            {
                bw.Write(listObj.Count);
                bw.Write(idFieldName ?? string.Empty);
                foreach (var record in listObj)
                {
                    var writeMethod = record.GetType().GetMethod("Write", new[] { typeof(BinaryWriter) });
                    if (writeMethod == null)
                    {
                        throw new MissingMethodException(
                            record.GetType().FullName,
                            "Write(BinaryWriter)");
                    }
                    
                    writeMethod.Invoke(record, new object[] { bw });
                }
            }
            else
            {
                bw.Write(0);
                bw.Write(string.Empty);
            }

            return ms.ToArray();
        }
    }
}
#endif