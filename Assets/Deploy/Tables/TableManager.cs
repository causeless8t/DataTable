using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Causeless3t.Core;
using UnityEngine;
using UnityEngine.Scripting;

public class TableManager : Singleton<TableManager>
{
    private bool _isLoaded = false;
    public bool IsLoaded => _isLoaded;
    
    [Preserve]
    public TableManager() {}

    [Preserve]
    public DynamicDataObject<T> Get<T>() where T : class
    {
        var typeName = typeof(T).Name;
        if (!_tables.TryGetValue(typeName, out var table)) return null;
        return table as DynamicDataObject<T>;
    }

    private readonly Dictionary<string, object> _tables = new();

    public async LoadAll()
    {
        _isLoaded = false;
        _tables.Clear();
        var types = Assembly.GetExecutingAssembly().GetTypes().Where(t => t.IsDefined(typeof(TableDataAttribute)));
        var method = typeof(TableManager)
            .GetMethod("LoadData", BindingFlags.NonPublic | BindingFlags.Static);
        foreach (var type in types)
        {
            var typeName = type.Name;
            var genericMethod = method.MakeGenericMethod(type);
            var unitaskObj = genericMethod.Invoke(null, new object[] { typeName });
            var getAwaiter = unitaskObj.GetType().GetMethod("GetAwaiter");
            var awaiter = getAwaiter.Invoke(unitaskObj, null);
            var isCompletedProp = awaiter.GetType().GetProperty("IsCompleted");
            while (!(bool)isCompletedProp.GetValue(awaiter))
                await UniTask.Yield(); // Unity-safe yield
            var getResult = awaiter.GetType().GetMethod("GetResult");
            var result = getResult.Invoke(awaiter, null); // result: object (DynamicDataObject<T>)
            if (result != null)
                _tables.TryAdd(typeName, result);
        }

        player.ins.title.CacheSortedTable();
        _isLoaded = true;
    }

    public void UnloadAll()
    {
        _isLoaded = false;
        _tables.Clear();
    }
    
    public static readonly string TableNamespace = "Daerisoft.Girl.Table";
    public static readonly string TargetPath = Path.Combine(Application.dataPath, "Resources", "Table");
    public static readonly string AESKey = "daerigirladrift!";
    
    // 바이너리 파일을 로드하여 데이터를 복원하는 메서드
    [Preserve]
    private static async UniTask<DynamicDataObject<T>> LoadData<T>(string fileName) where T : class
    {
        try
        {
            // 바이너리 파일 읽기
            TextAsset textAsset = await ResourceManager.LoadAsync<TextAsset>($"Table/{fileName}_encry");
            if (textAsset == null) return null;
            DSDebug.Log($"Local Table {fileName} Loading");
            byte[] encryptedData = textAsset.bytes;
            byte[] decryptedData = AesEncryption.Decrypt(encryptedData, AESKey);

            // 직렬화된 데이터를 역직렬화
            var dataObject = DeserializeObject<T>(decryptedData);
            return dataObject;
        }
        catch (Exception e)
        {
            DSDebug.LogException(e);
            return null;
        }
    }

    private static DynamicDataObject<T> DeserializeObject<T>(byte[] data) where T : class
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

    #region CDN Table

    private static string tableRoot => Application.persistentDataPath + "/table";
    private static readonly string CDNDevURL = "https://cdn.girladriftre.daerisoft.com/dev/";
    private static readonly string CDNLiveURL = "https://cdn.girladriftre.daerisoft.com/live/";

    public async UniTask LoadCDNAll()
    {
        var types = Assembly.GetExecutingAssembly().GetTypes().Where(t => t.IsDefined(typeof(TableDataAttribute)));
        var method = typeof(TableManager).GetMethod("LoadCDNData", BindingFlags.Instance | BindingFlags.NonPublic);
        foreach (var type in types)
        {
            var genericMethod = method.MakeGenericMethod(type);
            var unitaskObj = genericMethod.Invoke(this, new object[] { });
            var getAwaiter = unitaskObj.GetType().GetMethod("GetAwaiter");
            var awaiter = getAwaiter.Invoke(unitaskObj, null);
            var isCompletedProp = awaiter.GetType().GetProperty("IsCompleted");
            while (!(bool)isCompletedProp.GetValue(awaiter))
                await UniTask.Yield(); // Unity-safe yield
        }
    }
    
    // CDN 데이터 추가용 테이블 로드
    [Preserve]
    private async UniTask LoadCDNData<T>() where T : class
    {
        if (!Directory.Exists(tableRoot))
            Directory.CreateDirectory(tableRoot);
        
        var fileName = typeof(T).Name;
        var url = (Main.ins.BuildOptions.Development_Build ? CDNDevURL : CDNLiveURL) + fileName + "_encry.bytes";
        var filePath = Path.Combine(tableRoot, fileName + "_encry.bytes");
        await CDNDownloadHelper.DownloadCDNFile(url, filePath);
        if (!File.Exists(filePath)) return;
        DSDebug.Log($"CDN Table {fileName} Loading");
        try
        {
            var encryptedData = await File.ReadAllBytesAsync(filePath);
            byte[] decryptedData = AesEncryption.Decrypt(encryptedData, AESKey);
            // 직렬화된 데이터를 역직렬화
            var dataObject = DeserializeObject<T>(decryptedData);
            var tableData = dataObject;
            if (_tables.ContainsKey(fileName))
            {
                _tables[fileName] = tableData;
                DSDebug.LogWarning("Replaced table file: " + fileName);
            }
            else
                _tables.TryAdd(fileName, tableData);
        }
        catch (Exception e)
        {
            DSDebug.LogException(e);
        }
    }

    #endregion

    // [최태현] TryGetValue() 사용시 코드가 길어져 static 함수로 구현;
    public static bool TryGetValue<T>(string key, out T value) where T : class
    { 
        return Instance.Get<T>().Map.TryGetValue(key, out value);
    }

    /// <summary> [최태현] 그룹별 상품 데이터 </summary>
    public bool TryGetShopDataGroup<T>(int group, out List<T> result) where T : ShopData
    {
        List<T> list = Get<T>().List;
        if (null == list || 0 >= list.Count)
        {
            result = null;
            return false;
        }

        result = new List<T>();
        for (int i = 0; i < list.Count; ++i)
        {
            if (list[i].groupindex == group)
            {
                result.Add(list[i]);
            }
        }

        return result.Count > 0;
    }

    public bool TryGetCookingDataGroup<T>(int group, out List<T> result) where T : CookingData
    {
        List<T> list = Get<T>().List;
        if (null == list || 0 >= list.Count)
        {
            result = null;
            return false;
        }

        result = new List<T>();
        for (int i = 0; i < list.Count; ++i)
        {
            if (list[i].group == group)
            {
                result.Add(list[i]);
            }
        }

        return result.Count > 0;
    }
}
