using System;
using System.Collections.Generic;
using System.Linq;

namespace VP_RevitAPI.Helper.Sheets;

public static class PrintSheetSelection
{
    public static bool[] Restore(long[] ids, bool?[] previous)
    {
        if (ids.Length != previous.Length) throw new ArgumentException("Selection lengths differ.");
        var existing = new HashSet<long>(ids.Where((id, index) => previous[index].HasValue));
        var checkedIds = new HashSet<long>();
        var result = new bool[ids.Length];
        // Retain the user's occurrence, even when a new source is inserted before it.
        for (int i = 0; i < ids.Length; i++)
            if (previous[i] == true) result[i] = checkedIds.Add(ids[i]);
        for (int i = 0; i < ids.Length; i++)
            if (!existing.Contains(ids[i])) result[i] = checkedIds.Add(ids[i]);
        return result;
    }

    public static void SetSelection(long[] ids, bool[] selected, int index, bool value)
    {
        if (ids.Length != selected.Length) throw new ArgumentException("Selection lengths differ.");
        if (index < 0 || index >= ids.Length) throw new ArgumentOutOfRangeException(nameof(index));
        selected[index] = value;
        if (value)
            for (int i = 0; i < ids.Length; i++)
                if (i != index && ids[i] == ids[index]) selected[i] = false;
    }

    public static bool[] SelectAll(long[] ids, bool value)
    {
        var seen = new HashSet<long>();
        return ids.Select(id => value && seen.Add(id)).ToArray();
    }

    public static List<T> DistinctInOrder<T>(IEnumerable<T> rows, Func<T, long> id)
    {
        var seen = new HashSet<long>();
        return rows.Where(row => seen.Add(id(row))).ToList();
    }
}

public static class PrintSheetsBuild
{
    public const string Version = "2026.10.03.1";
}
