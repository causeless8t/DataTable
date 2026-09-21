using System;
using UnityEngine;

namespace Causeless3t.Table
{
    public sealed class DataTableSettings : ScriptableObject
    {
        private const string AssetsRoot = "Assets";
        private const string ResourcesRoot = "Assets/Resources";

        [SerializeField]
        private string _namespace = "Causeless3t.Table";

        [SerializeField]
        private string _encryptedDataPath = "Assets/Resources/Table";

        [SerializeField]
        private string _generatedCodePath = "Assets/Scripts/Generated/Tables";
        
        [SerializeField]
        private string _csvSourcePath = "Assets/CSV";

        [SerializeField]
        private string _aesKey = "yourpassword16!!";

        public string Namespace => _namespace;

        public string EncryptedDataPath => NormalizeProjectPath(_encryptedDataPath);

        public string GeneratedCodePath => NormalizeProjectPath(_generatedCodePath);
        
        public string CsvSourcePath => NormalizeProjectPath(_csvSourcePath);

        public string AESKey => _aesKey;

        public string EncryptedDataResourcesPath
        {
            get
            {
                var path = EncryptedDataPath;

                if (string.Equals(path, ResourcesRoot, StringComparison.OrdinalIgnoreCase))
                    return string.Empty;

                const string prefix = ResourcesRoot + "/";

                if (!path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidOperationException(
                        $"Encrypted Data Path must be inside {ResourcesRoot}. Current path: {path}");
                }

                return path.Substring(prefix.Length);
            }
        }

        public bool NormalizeSerializedPaths()
        {
            var encryptedDataPath = NormalizeProjectPath(_encryptedDataPath);
            var generatedCodePath = NormalizeProjectPath(_generatedCodePath);
            var csvSourcePath = NormalizeProjectPath(_csvSourcePath);

            if (_encryptedDataPath == encryptedDataPath &&
                _generatedCodePath == generatedCodePath &&
                _csvSourcePath == csvSourcePath)
            {
                return false;
            }

            _encryptedDataPath = encryptedDataPath;
            _generatedCodePath = generatedCodePath;
            _csvSourcePath = csvSourcePath;
            return true;
        }

        private static string NormalizeProjectPath(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                return AssetsRoot;

            var normalized = path.Replace('\\', '/').TrimEnd('/');

            if (string.Equals(normalized, AssetsRoot, StringComparison.OrdinalIgnoreCase))
                return AssetsRoot;

            if (normalized.StartsWith(AssetsRoot + "/", StringComparison.OrdinalIgnoreCase))
                return AssetsRoot + normalized.Substring(AssetsRoot.Length);

            var assetsIndex = normalized.LastIndexOf(
                "/Assets/",
                StringComparison.OrdinalIgnoreCase);

            if (assetsIndex >= 0)
                return AssetsRoot + normalized.Substring(assetsIndex + "/Assets".Length);

            if (normalized.EndsWith("/Assets", StringComparison.OrdinalIgnoreCase))
                return AssetsRoot;

            throw new InvalidOperationException(
                $"DataTable path must be inside the project's Assets folder. Current path: {path}");
        }
    }
}
