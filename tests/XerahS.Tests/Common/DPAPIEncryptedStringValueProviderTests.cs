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

#nullable enable
// Mirror production DPAPIEncryptedStringValueProvider / DPAPI surface:
// the type is annotated [SupportedOSPlatform("windows")]. Under
// TreatWarningsAsErrors, constructing it on macOS/Linux fails CA1416
// without this pragma. The null-guard tests never call DPAPI.Encrypt,
// so they are hermetic on every OS.
#pragma warning disable CA1416 // Validate platform compatibility

using System.Reflection;
using NUnit.Framework;
using XerahS.Common;

namespace XerahS.Tests.Common;

[TestFixture]
public class DPAPIEncryptedStringValueProviderTests
{
    private sealed class SecretHolder
    {
        public string? Secret { get; set; }
    }

    private static PropertyInfo SecretProperty =>
        typeof(SecretHolder).GetProperty(nameof(SecretHolder.Secret))
        ?? throw new InvalidOperationException("Secret property missing.");

    [Test]
    public void GetValue_NullTarget_ReturnsNullWithoutThrowing()
    {
        // Regression: fnd_sig-feat-library-16487a56b0-2467_b09e37530f
        // Pre-fix, PropertyInfo.GetValue(null) threw NullReferenceException
        // when Json.NET walked an incomplete object graph.
        var provider = new DPAPIEncryptedStringValueProvider(SecretProperty);

        object? result = null;
        Assert.DoesNotThrow(() => result = provider.GetValue(null!));
        Assert.That(result, Is.Null);
    }

    [Test]
    public void GetValue_NullProperty_ReturnsNull()
    {
        var provider = new DPAPIEncryptedStringValueProvider(SecretProperty);
        var holder = new SecretHolder { Secret = null };

        var result = provider.GetValue(holder);

        Assert.That(result, Is.Null);
    }

    [Test]
    public void GetValue_EmptyProperty_ReturnsEmptyString()
    {
        // Empty string is intentionally not encrypted (IsNullOrEmpty guard).
        var provider = new DPAPIEncryptedStringValueProvider(SecretProperty);
        var holder = new SecretHolder { Secret = string.Empty };

        var result = provider.GetValue(holder);

        Assert.That(result, Is.EqualTo(string.Empty));
    }

    [Test]
    public void SetValue_NullTarget_DoesNotThrow()
    {
        var provider = new DPAPIEncryptedStringValueProvider(SecretProperty);

        Assert.DoesNotThrow(() => provider.SetValue(null!, "anything"));
    }

    [Test]
    public void SetValue_PlainText_WritesThroughWithoutDecrypt()
    {
        // Values without the $DPAPIEncrypted$ prefix are written as-is.
        // This path never touches DPAPI, so it is safe on non-Windows.
        var provider = new DPAPIEncryptedStringValueProvider(SecretProperty);
        var holder = new SecretHolder();

        provider.SetValue(holder, "plain-secret");

        Assert.That(holder.Secret, Is.EqualTo("plain-secret"));
    }
}
