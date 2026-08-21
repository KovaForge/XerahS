namespace XerahS.Platform.Windows.Capture;

internal static class CursorReplacementHelper
{
    public static bool TryReplaceSystemCursors(
        IReadOnlyCollection<uint> cursorIds,
        Func<IntPtr> copyCursor,
        Func<IntPtr, uint, bool> setSystemCursor,
        Action<IntPtr> destroyCursor)
    {
        bool replacedAny = false;

        foreach (uint id in cursorIds)
        {
            IntPtr copy = copyCursor();
            if (copy == IntPtr.Zero)
                continue;

            if (setSystemCursor(copy, id))
            {
                replacedAny = true;
                continue;
            }

            destroyCursor(copy);
        }

        return replacedAny;
    }
}
