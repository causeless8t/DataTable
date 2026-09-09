using System;
using System.Collections.Generic;

[AttributeUsage(AttributeTargets.Class)]
public class TableDataAttribute : Attribute
{
}

[Serializable]
public class DynamicDataObject<TRecord>
{
    public Dictionary<string, TRecord> Map = new();
    public List<TRecord> List = new();
}