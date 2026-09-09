using System.IO;
using System.Threading.Tasks;
using UnityEngine;

namespace Causeless3t.Table
{
    public sealed class ResourcesDataLoader : IDataLoader
    {
        public async Task<byte[]> LoadAsync(string filename)
        {
            var path = DataTableRuntimeSettingsProvider.TableSettings.EncryptedDataPath.Replace(
                Path.Combine(Application.dataPath, "Resources") + Path.DirectorySeparatorChar, "");
            var request = Resources.LoadAsync<TextAsset>(Path.Combine(path, filename));

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
