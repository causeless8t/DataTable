using System;
using System.IO;
using System.Reflection;

namespace Causeless3t.Table
{
    public static class DeserializeUtil
    {
        public static DynamicDataObject<T> DeserializeObject<T>(byte[] data) where T : class, new()
        {
            using var ms = new MemoryStream(data);
            using var br = new BinaryReader(ms);

            var dataObject = new DynamicDataObject<T>();
            int count = br.ReadInt32();
            string idFieldName = br.ReadString();

            var recordType = typeof(T);
            var readMethod = recordType.GetMethod("Read", new[] { typeof(BinaryReader) });
            
            if (readMethod == null)
            {
                throw new MissingMethodException(recordType.FullName, "Read(BinaryReader)");
            }
            
            FieldInfo idField = null;

            if (!string.IsNullOrEmpty(idFieldName))
            {
                idField = recordType.GetField(
                    $"_{idFieldName}",
                    BindingFlags.NonPublic |
                    BindingFlags.Instance);

                if (idField == null)
                {
                    throw new MissingFieldException(
                        recordType.FullName,
                        $"_{idFieldName}");
                }
            }

            for (int i = 0; i < count; i++)
            {
                var record = new T();
                readMethod.Invoke(record, new object[] { br });

                dataObject.List.Add(record);
                
                if (idField == null)
                    continue;

                var idValue =
                    idField.GetValue(record)?.ToString();

                if (string.IsNullOrEmpty(idValue))
                    continue;

                dataObject.Map.TryAdd(
                    idValue,
                    record);
            }

            return dataObject;
        }
        
        public static object DeserializeByType(byte[] data, Type recordType)
        {
            var method = typeof(DeserializeUtil).GetMethod(
                nameof(DeserializeObject),
                BindingFlags.Public | BindingFlags.Static);

            if (method == null)
                throw new MissingMethodException(
                    typeof(DeserializeUtil).FullName,
                    nameof(DeserializeObject));

            var genericMethod = method.MakeGenericMethod(recordType);

            return genericMethod.Invoke(
                null,
                new object[] { data });
        }
    }
}