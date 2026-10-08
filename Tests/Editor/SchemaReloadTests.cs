using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Reflection.Emit;
using NUnit.Framework;
using UnityEngine;

namespace Causeless3t.Table.Tests
{
    public class SchemaReloadTests
    {
        private FieldInfo _cache;
        private object _previousSettings;
        private DataTableSettings _settings;
        private string _directory;
        private const BindingFlags StaticPrivate = BindingFlags.Static | BindingFlags.NonPublic;

        [SetUp]
        public void SetUp()
        {
            var provider = typeof(CsvToBinaryConverter).Assembly.GetType(
                "Causeless3t.Table.DataTableSettingsProvider", true);
            _cache = provider.GetField("_cachedSettings", StaticPrivate);
            _previousSettings = _cache.GetValue(null);
            _settings = ScriptableObject.CreateInstance<DataTableSettings>();
            var relativePath = "Assets/DataTableSchemaTests_" + Guid.NewGuid().ToString("N");
            typeof(DataTableSettings).GetField("_generatedCodePath", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(_settings, relativePath);
            _cache.SetValue(null, _settings);
            _directory = Path.Combine(Directory.GetParent(Application.dataPath).FullName, relativePath);
        }

        [TearDown]
        public void TearDown()
        {
            _cache.SetValue(null, _previousSettings);
            UnityEngine.Object.DestroyImmediate(_settings);
            if (Directory.Exists(_directory)) Directory.Delete(_directory, true);
        }

        [Test]
        public void LatestSourceWithOldLoadedTypeStillRequiresCompilation()
        {
            var name = "ReloadTest" + Guid.NewGuid().ToString("N");
            var schema = Schema(("id", "int"), ("added", "float"));
            CreateType(name, Schema(("id", "int")));
            Assert.That(DynamicClassGenerator.Generate(name, schema, Valid(schema)), Is.True);
            var path = Path.Combine(_directory, name + ".cs");
            var timestamp = File.GetLastWriteTimeUtc(path);

            Assert.That(CsvToBinaryConverter.CreateSchemeClass(name, "id:int|added:float\n1|2"), Is.True);
            Assert.That(File.GetLastWriteTimeUtc(path), Is.EqualTo(timestamp));

            var binaryPath = Path.Combine(_directory, name + ".bytes");
            var originalBinary = new byte[] { 1, 2, 3 };
            File.WriteAllBytes(binaryPath, originalBinary);
            Assert.Throws<InvalidOperationException>(() => CsvToBinaryConverter.Convert(
                name, "id:int|added:float\n1|2", binaryPath, "unused"));
            Assert.That(File.ReadAllBytes(binaryPath), Is.EqualTo(originalBinary));
        }

        [Test]
        public void UnchangedLoadedSchemaDoesNotRequireCompilationOrRewrite()
        {
            var name = "ReloadTest" + Guid.NewGuid().ToString("N");
            var schema = Schema(("id", "int"));
            CreateType(name, schema);
            DynamicClassGenerator.Generate(name, schema, Valid(schema));
            Assert.That(CsvToBinaryConverter.CreateSchemeClass(name, "id:int\n1"), Is.False);
            Assert.That(DynamicClassGenerator.Generate(name, schema, Valid(schema)), Is.False);
        }

        [TestCase("added", "float")]
        [TestCase("id", "long")]
        public void ChangedContractRejectsLoadedType(string name, string type)
        {
            var original = Schema(("id", "int"));
            var loaded = CreateType("ContractTest" + Guid.NewGuid().ToString("N"), original);
            var changed = name == "id" ? Schema((name, type)) : Schema(("id", "int"), (name, type));
            Assert.That(IsCompatible(loaded, changed), Is.False);
        }

        [Test]
        public void ReorderingSameTypedColumnsRejectsLoadedSerializer()
        {
            var loaded = CreateType("OrderTest" + Guid.NewGuid().ToString("N"),
                Schema(("id", "int"), ("value", "int")));
            Assert.That(IsCompatible(loaded, Schema(("value", "int"), ("id", "int"))), Is.False);
        }

        private static bool IsCompatible(Type type, List<(string propName, string typeName)> schema)
        {
            return (bool)typeof(CsvToBinaryConverter).GetMethod("IsTypeCompatible", StaticPrivate)
                .Invoke(null, new object[] { type, schema, Valid(schema) });
        }

        private static Type CreateType(string name, List<(string propName, string typeName)> schema)
        {
            var assembly = AssemblyBuilder.DefineDynamicAssembly(
                new AssemblyName("DataTableTest_" + Guid.NewGuid().ToString("N")), AssemblyBuilderAccess.Run);
            var builder = assembly.DefineDynamicModule("Tests").DefineType(
                "Causeless3t.Table." + name, TypeAttributes.Public);
            var signature = typeof(DynamicClassGenerator).GetMethod("GetSchemaSignature", StaticPrivate)
                .Invoke(null, new object[] { schema, Valid(schema) });
            builder.DefineField("__DataTableSchemaSignature", typeof(string),
                FieldAttributes.Private | FieldAttributes.Static | FieldAttributes.Literal).SetConstant(signature);
            foreach (var column in schema)
                builder.DefineField("_" + column.propName,
                    TypeParser.GetFieldType(column.typeName), FieldAttributes.Private);
            foreach (var method in new[] { "Write", "Read" })
                builder.DefineMethod(method, MethodAttributes.Public, typeof(void),
                    new[] { method == "Write" ? typeof(BinaryWriter) : typeof(BinaryReader) })
                    .GetILGenerator().Emit(OpCodes.Ret);
            return builder.CreateType();
        }

        private static List<(string propName, string typeName)> Schema(params (string, string)[] columns)
            => new(columns);

        private static List<bool> Valid(List<(string propName, string typeName)> schema)
            => schema.ConvertAll(_ => true);
    }
}
