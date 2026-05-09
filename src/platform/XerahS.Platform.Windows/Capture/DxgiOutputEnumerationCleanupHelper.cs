using System;
using System.Collections.Generic;

namespace XerahS.Platform.Windows.Capture;

internal static class DxgiOutputEnumerationCleanupHelper
{
    public static void DisposeOutputsAndAdapters<TItem, TOutput, TAdapter>(
        IEnumerable<TItem> items,
        Func<TItem, TOutput> outputSelector,
        Func<TItem, TAdapter> adapterSelector)
        where TOutput : IDisposable
        where TAdapter : class, IDisposable
    {
        var adapters = new HashSet<TAdapter>(ReferenceEqualityComparer<TAdapter>.Instance);

        foreach (var item in items)
        {
            outputSelector(item).Dispose();
            adapters.Add(adapterSelector(item));
        }

        foreach (var adapter in adapters)
        {
            adapter.Dispose();
        }
    }

    private sealed class ReferenceEqualityComparer<T> : IEqualityComparer<T>
        where T : class
    {
        public static readonly ReferenceEqualityComparer<T> Instance = new();

        public bool Equals(T? x, T? y) => ReferenceEquals(x, y);

        public int GetHashCode(T obj) => System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(obj);
    }
}
