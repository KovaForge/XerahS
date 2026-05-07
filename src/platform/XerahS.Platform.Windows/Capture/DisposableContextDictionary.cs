#region License Information (GPL v3)

/*
    XerahS - The Avalonia UI implementation of ShareX
    Copyright (c) 2007-2026 ShareX Team

    This program is free software; you can redistribute it and/or
    modify it under the terms of the GNU General Public License
    as published by the Free Software Foundation; either version 2
    of the License, or (at your option) any later version.
*/

#endregion License Information (GPL v3)

using System;
using System.Collections.Generic;

namespace XerahS.Platform.Windows.Capture;

internal static class DisposableContextDictionary
{
    public static void Replace<TKey, TValue>(IDictionary<TKey, TValue> contexts, TKey key, TValue replacement)
        where TKey : notnull
        where TValue : class, IDisposable
    {
        if (contexts.TryGetValue(key, out var existing) && !ReferenceEquals(existing, replacement))
        {
            existing.Dispose();
        }

        contexts[key] = replacement;
    }
}
