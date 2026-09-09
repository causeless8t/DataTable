#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;

namespace Causeless3t.Table
{
    public static class DynamicTypeGenerator
    {
        public static Type CreateRecordType(string className, List<(string propName, string typeName)> schema,
            List<bool> validColumns)
        {
            var assemblyName = new AssemblyName("Assembly-CSharp");
            var assemblyBuilder = AssemblyBuilder.DefineDynamicAssembly(assemblyName, AssemblyBuilderAccess.Run);
            var moduleBuilder = assemblyBuilder.DefineDynamicModule("MainModule");

            var typeBuilder = moduleBuilder.DefineType($"{DataTableSettingsProvider.Settings.Namespace}.{className}",
                TypeAttributes.Public | TypeAttributes.Class | TypeAttributes.Serializable);

            for (int i = 0; i < schema.Count; i++)
            {
                if (!validColumns[i])
                    continue;

                var (propName, typeName) = schema[i];
                var fieldType = TypeParser.GetFieldType(typeName.ToLower());
                if (fieldType == null)
                    throw new Exception($"알 수 없는 타입입니다: {typeName}");

                typeBuilder.DefineField(propName, fieldType, FieldAttributes.Public);
            }

            return typeBuilder.CreateType();
        }
    }
}
#endif