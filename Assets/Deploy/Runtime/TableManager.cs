using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Scripting;

namespace Causeless3t.Table
{
    public sealed class TableManager
    {
        private IDataLoader _dataLoader;
        
        public bool IsLoaded { get; private set; }
        
        public TableManager(IDataLoader loader)
        {
            _dataLoader = loader;
        }

        [Preserve]
        public DynamicDataObject<T> Get<T>() where T : class
        {
            if (!_tables.TryGetValue(typeof(T), out var table)) return null;
            return table as DynamicDataObject<T>;
        }

        private readonly Dictionary<Type, object> _tables = new();

        [Preserve]
        public async Task LoadAll()
        {
            IsLoaded = false;
            _tables.Clear();
            var types = AppDomain.CurrentDomain.GetAssemblies().SelectMany(assembly => assembly.GetTypes())
                .Where(t => t.IsDefined(typeof(TableDataAttribute)));
            var method = typeof(TableManager)
                .GetMethod("LoadData", BindingFlags.NonPublic | BindingFlags.Static);
            
            if (method == null)
                throw new MissingMethodException(nameof(LoadData));
            
            foreach (var type in types)
            {
                var table = await LoadDataByTypeAsync(type);

                if (table != null)
                    _tables[type] = table;
            }

            IsLoaded = true;
        }
        
        private async Task<object> LoadDataByTypeAsync(Type type)
        {
            var method = typeof(TableManager).GetMethod(
                nameof(LoadData),
                BindingFlags.NonPublic |
                BindingFlags.Static);

            if (method == null)
                throw new MissingMethodException(nameof(LoadData));

            var genericMethod = method.MakeGenericMethod(type);

            if (genericMethod.Invoke(
                    null,
                    new object[] { type.Name }) is not Task task)
            {
                throw new InvalidOperationException($"Failed to load table type: {type.Name}");
            }

            await task;

            return task.GetType().GetProperty("Result")?.GetValue(task);
        }

        [Preserve]
        public void UnloadAll()
        {
            IsLoaded = false;
            _tables.Clear();
        }

        // 바이너리 파일을 로드하여 데이터를 복원하는 메서드
        private async Task<DynamicDataObject<T>> LoadData<T>(string fileName) where T : class, new()
        {
            try
            {
                // 바이너리 파일 읽기
                byte[] encryptedData = await _dataLoader.LoadAsync($"{fileName}_encry");
                if (encryptedData == null) return null;
                Debug.Log($"Local Table {fileName} Loading");
                byte[] decryptedData = AesEncryption.Decrypt(encryptedData, DataTableRuntimeSettingsProvider.TableSettings.AESKey);

                // 직렬화된 데이터를 역직렬화
                var dataObject = DeserializeUtil.DeserializeObject<T>(decryptedData);
                return dataObject;
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                return null;
            }
        }
    }
}