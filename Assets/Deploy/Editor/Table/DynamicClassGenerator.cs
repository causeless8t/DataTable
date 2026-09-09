#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

public static class DynamicClassGenerator
{
    private static readonly string SourcePath = Path.Combine(Application.dataPath, "__Main", "Script", "Tables");
    
    public static async UniTask Generate(string className, List<(string propName, string typeName)> schema, List<bool> validColumns)
    {
        var path = Path.Combine(SourcePath, $"{className}.cs");
        var code = new StringBuilder();

        code.AppendLine("using System;");
        code.AppendLine("using System.Collections.Generic;");
        code.AppendLine("using System.IO;");
        code.AppendLine("using UnityEngine;");
        code.AppendLine($"namespace {TableManager.TableNamespace}");
        code.AppendLine("{");
        code.AppendLine("    [Serializable][TableData]");
        code.AppendLine($"    public class {className}");
        code.AppendLine("    {");

        for (int i = 0; i < schema.Count; i++)
        {
            if (!validColumns[i])
                continue;

            var (propName, typeName) = schema[i];
            var typeStr = TypeParser.GetFieldTypeString(typeName.ToLower());
            code.AppendLine($"        [SerializeField] private {typeStr} _{propName};");
            code.AppendLine($"        public {typeStr} @{propName} => _{propName};");
        }

        // Write(BinaryWriter bw) 구현
        code.AppendLine("        public void Write(BinaryWriter bw)");
        code.AppendLine("        {");
        for (int i = 0; i < schema.Count; i++)
        {
            if (!validColumns[i])
                continue;

            var (propName, typeName) = schema[i];
            var lowerTypeName = typeName.ToLower();

            if (lowerTypeName == "int" || lowerTypeName == "float" || lowerTypeName == "string" || 
                lowerTypeName == "long" || lowerTypeName == "double" || lowerTypeName == "bignum" || lowerTypeName == "bignumber")
            {
                code.AppendLine($"            bw.Write(_{propName});");
            }
            else if (lowerTypeName == "vector2")
            {
                code.AppendLine($"            bw.Write(_{propName}.x); bw.Write(_{propName}.y);");
            }
            else if (lowerTypeName == "vector3")
            {
                code.AppendLine($"            bw.Write(_{propName}.x); bw.Write(_{propName}.y); bw.Write(_{propName}.z);");
            }
            else if (lowerTypeName == "datetime")
            {
                code.AppendLine($"            bw.Write(_{propName}.Ticks);");
            }
            else if (lowerTypeName == "list<int>" || lowerTypeName == "list<string>" || lowerTypeName == "list<float>")
            {
                code.AppendLine($"            bw.Write(_{propName}.Count);");
                code.AppendLine($"            foreach (var item in _{propName}) bw.Write(item);");
            }
            else if (lowerTypeName == "curpair")
            {
                code.AppendLine($"            bw.Write(_{propName}.ID); bw.Write(_{propName}.Amount);");
            }
            else if (lowerTypeName == "list<curpair>")
            {
                code.AppendLine($"            bw.Write(_{propName}.Count);");
                code.AppendLine($"            foreach (var item in _{propName}) {{ bw.Write(item.ID); bw.Write(item.Amount); }}");
            }
            else
            {
                code.AppendLine($"            // unknown type: {lowerTypeName}");
            }
        }
        code.AppendLine("        }");
        
        // Read(BinaryReader br) 구현
        code.AppendLine("        public void Read(BinaryReader br)");
        code.AppendLine("        {");
        for (int i = 0; i < schema.Count; i++)
        {
            if (!validColumns[i])
                continue;

            var (propName, typeName) = schema[i];
            var lowerTypeName = typeName.ToLower();

            if (lowerTypeName == "int")
                code.AppendLine($"            _{propName} = br.ReadInt32();");
            else if (lowerTypeName == "float")
                code.AppendLine($"            _{propName} = br.ReadSingle();");
            else if (lowerTypeName == "string")
                code.AppendLine($"            _{propName} = br.ReadString();");
            else if (lowerTypeName == "long")
                code.AppendLine($"            _{propName} = br.ReadInt64();");
            else if (lowerTypeName == "double" || lowerTypeName == "bignum" || lowerTypeName == "bignumber")
                code.AppendLine($"            _{propName} = br.ReadDouble();");
            else if (lowerTypeName == "vector2")
                code.AppendLine($"            _{propName} = new Vector2(br.ReadSingle(), br.ReadSingle());");
            else if (lowerTypeName == "vector3")
                code.AppendLine($"            _{propName} = new Vector3(br.ReadSingle(), br.ReadSingle(), br.ReadSingle());");
            else if (lowerTypeName == "datetime")
                code.AppendLine($"            _{propName} = new DateTime(br.ReadInt64());");
            else if (lowerTypeName == "list<int>")
            {
                code.AppendLine($"            int count_{propName} = br.ReadInt32();");
                code.AppendLine($"            _{propName} = new List<int>(count_{propName});");
                code.AppendLine($"            for (int i = 0; i < count_{propName}; i++) _{propName}.Add(br.ReadInt32());");
            }
            else if (lowerTypeName == "list<float>")
            {
                code.AppendLine($"            int count_{propName} = br.ReadInt32();");
                code.AppendLine($"            _{propName} = new List<float>(count_{propName});");
                code.AppendLine($"            for (int i = 0; i < count_{propName}; i++) _{propName}.Add(br.ReadSingle());");
            }
            else if (lowerTypeName == "list<string>")
            {
                code.AppendLine($"            int count_{propName} = br.ReadInt32();");
                code.AppendLine($"            _{propName} = new List<string>(count_{propName});");
                code.AppendLine($"            for (int i = 0; i < count_{propName}; i++) _{propName}.Add(br.ReadString());");
            }
            else if (lowerTypeName == "curpair")
            {
                code.AppendLine($"            _{propName} = new CurPair {{ ID = br.ReadString(), Amount = br.ReadDouble() }};");
            }
            else if (lowerTypeName == "list<curpair>")
            {
                code.AppendLine($"            int count_{propName} = br.ReadInt32();");
                code.AppendLine($"            _{propName} = new List<CurPair>(count_{propName});");
                code.AppendLine($"            for (int i = 0; i < count_{propName}; i++) _{propName}.Add(new CurPair {{ ID = br.ReadString(), Amount = br.ReadDouble() }});");
            }
            else
            {
                code.AppendLine($"            // unknown type: {lowerTypeName}");
            }
        }
        code.AppendLine("        }");

        code.AppendLine("    }");
        code.AppendLine("}");

        await File.WriteAllTextAsync(path, code.ToString());
    }
}
#endif