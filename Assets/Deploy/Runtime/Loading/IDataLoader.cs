using System.Threading.Tasks;

namespace Causeless3t.Table
{
    public interface IDataLoader
    {
        Task<byte[]> LoadAsync(string path);
    }
}