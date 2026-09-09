#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEngine;

public static class CsvToBinaryConverter
{
    public static readonly char CSV_SEPARATOR = '|';
    
    public static async Convert(string fileName, string csvContents, string outputBinaryPath, string aesKey)
    {
        var lines = csvContents.Split("\n");
        if (lines.Length < 2)
            throw new Exception("CSV 파일에 데이터가 없습니다.");

        string[] schemaParts = lines[0].Split(CSV_SEPARATOR);
        var schema = ParseSchema(schemaParts, out var validColumns);

        var recordType = await CreateOrGetRecordType(fileName, schema, validColumns);
        if (recordType == null) return;

        var dataDictType = typeof(DynamicDataObject<>).MakeGenericType(recordType);
        var dataObject = Activator.CreateInstance(dataDictType);
        var mapField = dataDictType.GetField("Map");
        var listField = dataDictType.GetField("List");
        var dataDictObj = mapField.GetValue(dataObject);
        var dataListObj = listField.GetValue(dataObject);
        var dictInterface = typeof(Dictionary<,>).MakeGenericType(typeof(string), recordType);
        var listInterface = typeof(List<>).MakeGenericType(recordType);

        int idFieldIndex = -1;
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

            // Calculate idFieldIndex based on the first valid idIndex found
            if (idFieldIndex == -1)
            {
                idFieldIndex = 0;
                for (int j = 0; j < idIndex; j++)
                {
                    if (validColumns[j])
                        idFieldIndex++;
                }
            }

            var record = Activator.CreateInstance(recordType);

            for (int j = idIndex; j < parts.Length && j < schema.Count; j++)
            {
                if (!validColumns[j])
                    continue;

                var (propName, typeName) = schema[j];
                var field = recordType.GetField($"_{propName}", BindingFlags.NonPublic | BindingFlags.Instance);
                if (field == null)
                    continue;

                try
                {
                    // [최태현] 칭호 테이블에서 string 공란이 생기는 경우가 있어 조건 추가
                    // csv에서의 "none"은 string.Empty로 처리
                    if (string.Equals(parts[j], "none", StringComparison.Ordinal))
                    {
                        parts[j] = string.Empty;
                    }

                    object value = TypeParser.ParseValue(typeName.ToLower(), parts[j]);
                    field.SetValue(record, value);
                }
                catch (Exception e)
                {
                    Debug.LogException(e);
                    throw;
                }
            }

            var dictAdd = dictInterface.GetMethod("TryAdd", new[] { typeof(string), recordType });
            var listAdd = listInterface.GetMethod("Add", new[] { recordType });

            bool isValid = (bool)dictAdd.Invoke(dataDictObj, new[] { id, record });
            if (isValidDict && !isValid)
                isValidDict = false;
            listAdd.Invoke(dataListObj, new[] { record });
        }
        var dictClear = dictInterface.GetMethod("Clear");
        if (!isValidDict)
            dictClear.Invoke(dataDictObj, null);

        var binary = SerializeObject(dataObject, idFieldIndex);
        var encrypted = AesEncryption.Encrypt(binary, aesKey);
        await File.WriteAllBytesAsync(outputBinaryPath, encrypted);

        Debug.Log($"CSV 변환 완료: {outputBinaryPath}");
    }

    private static List<(string propName, string typeName)> ParseSchema(string[] schemaParts, out List<bool> validColumns)
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

    private static async UniTask<Type> CreateOrGetRecordType(string className, List<(string propName, string typeName)> schema, List<bool> validColumns)
    {
        var recordClassName = $"{className}";
        var type = FindTypeByName(recordClassName);

        if (type != null && IsTypeCompatible(type, schema, validColumns))
        {
            return type;
        }

        await DynamicClassGenerator.Generate(recordClassName, schema, validColumns);
        // type = DynamicTypeGenerator.CreateRecordType(recordClassName, schema, validColumns);
        Debug.LogWarning($"[{recordClassName}] 타입을 동적 생성하였습니다. 컴파일 후 다시 실행해주세요.");
        return null;
    }
    
    public static Type FindTypeByName(string className)
    {
        foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            var type = assembly.GetType($"{TableManager.TableNamespace}.{className}");
            if (type != null)
                return type;
        }
        return null;
    }

    private static bool IsTypeCompatible(Type type, List<(string propName, string typeName)> schema, List<bool> validColumns)
    {
        var fields = type.GetFields(BindingFlags.NonPublic | BindingFlags.Instance);
        var writeMethod = type.GetMethod("Write", new[] { typeof(BinaryWriter) });
        if (writeMethod == null)
            return false;
        var readMethod = type.GetMethod("Read", new[] { typeof(BinaryReader) });
        if (readMethod == null)
            return false;

        int expectedFieldCount = 0;
        for (int i = 0; i < schema.Count; i++)
        {
            if (validColumns[i])
                expectedFieldCount++;
        }

        if (fields.Length != expectedFieldCount)
            return false;

        int fieldIndex = 0;
        for (int i = 0; i < schema.Count; i++)
        {
            if (!validColumns[i])
                continue;

            var (propName, typeName) = schema[i];
            if (fields[fieldIndex].Name != $"_{propName}")
                return false;
            if (fields[fieldIndex].FieldType != TypeParser.GetFieldType(typeName.ToLower()))
                return false;

            fieldIndex++;
        }

        return true;
    }

    private static byte[] SerializeObject(object obj, int idFieldIndex)
    {
        using var ms = new MemoryStream();
        using var bw = new BinaryWriter(ms);

        var listField = obj.GetType().GetField("List", BindingFlags.Public | BindingFlags.Instance);
        var listObj = listField.GetValue(obj) as IList;

        if (listObj != null)
        {
            bw.Write(listObj.Count);
            bw.Write(idFieldIndex);
            foreach (var record in listObj)
            {
                var writeMethod = record.GetType().GetMethod("Write", new[] { typeof(BinaryWriter) });
                writeMethod?.Invoke(record, new object[] { bw });
            }
        }
        else
        {
            bw.Write(0);
            bw.Write(-1);
        }

        return ms.ToArray();
    }
}
#endif