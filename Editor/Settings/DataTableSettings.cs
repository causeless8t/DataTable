using System.IO;
using UnityEngine;

namespace Causeless3t.Table
{
    public sealed class DataTableSettings : ScriptableObject
    {
        [SerializeField]
        private string _namespace = "Causeless3t.Table";

        [SerializeField]
        private string _encryptedDataPath = Path.Combine(Application.dataPath, "Resources", "Table");

        [SerializeField]
        private string _generatedCodePath = Path.Combine(Application.dataPath, "Scripts", "Generated", "Tables");
        
        [SerializeField]
        private string _csvSourcePath = Path.Combine(Application.dataPath, "CSV");

        [SerializeField]
        private string _aesKey = "yourpassword16!!";

        public string Namespace => _namespace;

        public string EncryptedDataPath => _encryptedDataPath;

        public string GeneratedCodePath => _generatedCodePath;
        
        public string CsvSourcePath => _csvSourcePath;

        public string AESKey => _aesKey;
    }
}
