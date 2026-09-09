using UnityEngine;
using System;
using System.Collections.Generic;
using System.Globalization;

namespace Causeless3t.Table
{
    public static class TypeParser
    {
        public static Type GetFieldType(string typeName) => typeName switch
        {
            "int" => typeof(int),
            "float" => typeof(float),
            "string" => typeof(string),
            "long" => typeof(long),
            "vector2" => typeof(Vector2),
            "vector3" => typeof(Vector3),
            "datetime" => typeof(DateTime),
            "list<int>" => typeof(List<int>),
            "list<float>" => typeof(List<float>),
            "list<string>" => typeof(List<string>),
            "double" => typeof(double),
            "bignum" => typeof(double),
            "bignumber" => typeof(double),
            _ => null
        };

        public static string GetFieldTypeString(string typeName) => typeName switch
        {
            "int" => "int",
            "float" => "float",
            "string" => "string",
            "long" => "long",
            "vector2" => "Vector2",
            "vector3" => "Vector3",
            "datetime" => "DateTime",
            "list<int>" => "List<int>",
            "list<float>" => "List<float>",
            "list<string>" => "List<string>",
            "double" => "double",
            "bignum" => "double",
            "bignumber" => "double",
            _ => typeName
        };

        public static object ParseValue(string typeName, string value)
        {
            switch (typeName)
            {
                case "int":
                    return int.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out var numInt)
                        ? numInt
                        : 0;
                case "float":
                    return float.TryParse(value, NumberStyles.Any, CultureInfo.InvariantCulture, out var numFloat)
                        ? numFloat
                        : 0;
                case "string": return value;
                case "long":
                    return long.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out var numLong)
                        ? numLong
                        : 0;
                case "vector2":
                {
                    var parts = value.Trim('(', ')').Split(',');
                    if (parts.Length != 2) return Vector2.zero;
                    return new Vector2(
                        float.TryParse(parts[0], NumberStyles.Any, CultureInfo.InvariantCulture, out var numFloat1)
                            ? numFloat1
                            : 0,
                        float.TryParse(parts[1], NumberStyles.Any, CultureInfo.InvariantCulture, out var numFloat2)
                            ? numFloat2
                            : 0);
                }
                case "vector3":
                {
                    var parts = value.Trim('(', ')').Split(',');
                    if (parts.Length != 3) return Vector3.zero;
                    return new Vector3(
                        float.TryParse(parts[0], NumberStyles.Any, CultureInfo.InvariantCulture, out var numFloat3)
                            ? numFloat3
                            : 0,
                        float.TryParse(parts[1], NumberStyles.Any, CultureInfo.InvariantCulture, out var numFloat4)
                            ? numFloat4
                            : 0,
                        float.TryParse(parts[2], NumberStyles.Any, CultureInfo.InvariantCulture, out var numFloat5)
                            ? numFloat5
                            : 0);
                }
                case "datetime":
                    if (DateTime.TryParseExact(value, "yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture,
                            DateTimeStyles.None, out var time1))
                        return time1;
                    return DateTime.TryParseExact(value, "yyyy-MM-dd", CultureInfo.InvariantCulture,
                        DateTimeStyles.None,
                        out var time2)
                        ? time2
                        : DateTime.MinValue;
                case "list<int>":
                {
                    var parts = value.Split(',');
                    List<int> result = new();
                    foreach (var t in parts)
                        result.Add(int.TryParse(t, NumberStyles.Number, CultureInfo.InvariantCulture, out var num)
                            ? num
                            : 0);
                    return result;
                }
                case "list<float>":
                {
                    var parts = value.Split(',');
                    List<float> result = new();
                    foreach (var t in parts)
                        result.Add(float.TryParse(t, NumberStyles.Float, CultureInfo.InvariantCulture, out var num)
                            ? num
                            : 0);
                    return result;
                }
                case "list<string>":
                {
                    var parts = value.Split(',');
                    List<string> result = new();
                    foreach (var part in parts)
                    {
                        if (string.IsNullOrEmpty(part)) continue;
                        result.Add(part.Trim());
                    }

                    return result;
                }
                case "double":
                case "bignum":
                case "bignumber":
                    return double.TryParse(value, NumberStyles.Any, CultureInfo.InvariantCulture, out var numDouble)
                        ? numDouble
                        : 0;
                default:
                    throw new Exception($"알 수 없는 타입: {typeName}");
            }
        }
    }
}