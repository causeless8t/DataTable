using System;
using System.IO;
using System.Reflection;

namespace Causeless3t.Table
{
    public static class DeserializeUtil
    {
        public static DynamicDataObject<T> DeserializeObject<T>(byte[] data) where T : class
        {
            using var ms = new MemoryStream(data);
            using var br = new BinaryReader(ms);

            var dataObject = new DynamicDataObject<T>();
            int count = br.ReadInt32();
            int idFieldIndex = br.ReadInt32();

            var recordType = typeof(T);
            var readMethod = recordType.GetMethod("Read", new[] { typeof(BinaryReader) });

            var fields = recordType.GetFields(BindingFlags.NonPublic | BindingFlags.Instance);

            for (int i = 0; i < count; i++)
            {
                var record = Activator.CreateInstance<T>();
                readMethod?.Invoke(record, new object[] { br });

                dataObject.List.Add(record);

                // ID 필드 인덱스를 사용하여 Map에 추가
                if (idFieldIndex >= 0 && idFieldIndex < fields.Length)
                {
                    var idValue = fields[idFieldIndex].GetValue(record)?.ToString();
                    if (!string.IsNullOrEmpty(idValue))
                    {
                        dataObject.Map.TryAdd(idValue, record);
                    }
                }
            }

            return dataObject;
        }
    }
}