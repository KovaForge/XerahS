#region License Information (GPL v3)

/*
    XerahS - The Avalonia UI implementation of ShareX
    Copyright (c) 2007-2026 ShareX Team

    This program is free software; you can redistribute it and/or
    modify it under the terms of the GNU General Public License
    as published by the Free Software Foundation; either version 2
    of the License, or (at your option) any later version.

    This program is distributed in the hope that it will be useful,
    but WITHOUT ANY WARRANTY; without even the implied warranty of
    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
    GNU General Public License for more details.

    You should have received a copy of the GNU General Public License
    along with this program; if not, write to the Free Software
    Foundation, Inc., 51 Franklin Street, Fifth Floor, Boston, MA  02110-1301, USA.

    Optionally you can also view the license at <http://www.gnu.org/licenses/>.
*/

#endregion License Information (GPL v3)

using System.Runtime.InteropServices;

namespace XerahS.Platform.MacOS.Services;

internal enum MacOSActivationPolicy : long
{
    Regular = 0,
    Accessory = 1
}

internal static partial class MacOSApplicationActivationPolicy
{
    private const string ObjCLibrary = "/usr/lib/libobjc.A.dylib";

    internal static MacOSActivationPolicy GetPolicyForMenuBarOnlyMode(bool enabled)
    {
        return enabled ? MacOSActivationPolicy.Accessory : MacOSActivationPolicy.Regular;
    }

    internal static bool TryApplyMenuBarOnlyMode(bool enabled)
    {
        if (!OperatingSystem.IsMacOS())
        {
            return true;
        }

        try
        {
            IntPtr application = GetSharedApplication();
            if (application == IntPtr.Zero)
            {
                return false;
            }

            MacOSActivationPolicy policy = GetPolicyForMenuBarOnlyMode(enabled);
            IntPtr selector = sel_registerName("setActivationPolicy:");
            return objc_msgSend_bool_nint(application, selector, (nint)policy);
        }
        catch
        {
            return false;
        }
    }

    private static IntPtr GetSharedApplication()
    {
        IntPtr nsApplicationClass = objc_getClass("NSApplication");
        if (nsApplicationClass == IntPtr.Zero)
        {
            return IntPtr.Zero;
        }

        IntPtr selector = sel_registerName("sharedApplication");
        return objc_msgSend_intptr(nsApplicationClass, selector);
    }

    [LibraryImport(ObjCLibrary, EntryPoint = "objc_getClass", StringMarshalling = StringMarshalling.Utf8)]
    private static partial IntPtr objc_getClass(string name);

    [LibraryImport(ObjCLibrary, EntryPoint = "sel_registerName", StringMarshalling = StringMarshalling.Utf8)]
    private static partial IntPtr sel_registerName(string selectorName);

    [LibraryImport(ObjCLibrary, EntryPoint = "objc_msgSend")]
    private static partial IntPtr objc_msgSend_intptr(IntPtr receiver, IntPtr selector);

    [LibraryImport(ObjCLibrary, EntryPoint = "objc_msgSend")]
    [return: MarshalAs(UnmanagedType.I1)]
    private static partial bool objc_msgSend_bool_nint(IntPtr receiver, IntPtr selector, nint value);
}
