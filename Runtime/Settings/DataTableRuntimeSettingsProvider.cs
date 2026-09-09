using System;
using UnityEngine;

namespace Causeless3t.Table
{
    public sealed class DataTableRuntimeSettingsProvider : ScriptableObject
    {
        private static DataTableSettings _settings;
        public static DataTableSettings TableSettings
        {
            get
            {
                if (_settings != null)
                    return _settings;

                _settings = Resources.Load<DataTableSettings>("DataTableSettings");

                if (_settings == null)
                {
                    throw new InvalidOperationException("DataTableRuntimeSettings could not be found in Resources.");
                }

                return _settings;
            }
        }
    }
}