using System;
using System.Collections.Generic;
using UnityEngine.Scripting;

[AttributeUsage(AttributeTargets.Class)]
public class TableDataAttribute : PreserveAttribute
{
}

[Serializable]
public class DynamicDataObject<TRecord>
{
    public Dictionary<string, TRecord> Map = new();
    public List<TRecord> List = new();
}