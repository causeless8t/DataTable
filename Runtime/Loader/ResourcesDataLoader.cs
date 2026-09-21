using System.Threading.Tasks;
using UnityEngine;

namespace Causeless3t.Table
{
    public sealed class ResourcesDataLoader : IDataLoader
    {
        public async Task<byte[]> LoadAsync(string filename)
        {
            var directory = DataTableRuntimeSettingsProvider
                .TableSettings
                .EncryptedDataResourcesPath;
            var path = string.IsNullOrEmpty(directory)
                ? filename
                : $"{directory}/{filename}";
            var request = Resources.LoadAsync<TextAsset>(path);

            var tcs = new TaskCompletionSource<TextAsset>();

            request.completed += _ =>
            {
                tcs.TrySetResult(request.asset as TextAsset);
            };

            var asset = await tcs.Task;

            return asset?.bytes;
        }
    }
}
