using System;
using UnityEditor;

namespace Causeless3t.Table
{
    internal static class TableGenerationState
    {
        private const string WaitingKey =
            "Causeless3t.Table.WaitingForCompilation";

        private const string AssetPathsKey =
            "Causeless3t.Table.AssetPaths";

        public static bool WaitingForCompilation
        {
            get => SessionState.GetBool(WaitingKey, false);
            set => SessionState.SetBool(WaitingKey, value);
        }

        public static void SetAssetPaths(string[] paths)
        {
            SessionState.SetString(
                AssetPathsKey,
                string.Join("\n", paths));
        }

        public static string[] GetAssetPaths()
        {
            var value =
                SessionState.GetString(
                    AssetPathsKey,
                    string.Empty);

            if (string.IsNullOrEmpty(value))
                return Array.Empty<string>();

            return value.Split('\n');
        }

        public static void Clear()
        {
            SessionState.EraseBool(WaitingKey);
            SessionState.EraseString(AssetPathsKey);
        }
    }
}