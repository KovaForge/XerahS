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

using System.IO;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using XerahS.Uploaders;
using XerahS.Uploaders.PluginSystem;

namespace XerahS.Tests.Uploaders;

[TestFixture]
public sealed class DestinationContractTests
{
    [Test]
    public void Manifest_DeclaredCapabilities_AreAdditive()
    {
        PluginManifest manifest = new()
        {
            SupportsCancellation = true,
            SupportsProgress = true,
            SupportsExplorer = true
        };

        Assert.That(manifest.GetDeclaredCapabilities(),
            Is.EqualTo(UploaderCapabilities.Cancellation | UploaderCapabilities.Progress | UploaderCapabilities.Explorer));
        Assert.That(manifest.GetDeclaredCapabilities().HasFlag(UploaderCapabilities.Resume), Is.False);
    }

    [Test]
    public void UploadOutcome_SuccessAndFailed_Factories()
    {
        UploadOutcome ok = UploadOutcome.Success("https://example.com/a.png", "done");
        Assert.That(ok.Succeeded, Is.True);
        Assert.That(ok.Url, Is.EqualTo("https://example.com/a.png"));

        UploadOutcome fail = UploadOutcome.Failed("nope", "auth", retryable: true);
        Assert.That(fail.Succeeded, Is.False);
        Assert.That(fail.Retryable, Is.True);
        Assert.That(fail.ErrorCode, Is.EqualTo("auth"));
    }

    [Test]
    public async Task Adapter_PrefersIUploadHandler()
    {
        HandlerStub stub = new();
        using MemoryStream stream = new(new byte[] { 1, 2, 3 });
        UploadRequest request = new()
        {
            Content = stream,
            FileName = "a.png",
            Category = UploaderCategory.Image
        };

        UploadOutcome outcome = await UploaderUploadAdapter.UploadAsync(stub, request, CancellationToken.None);

        Assert.That(outcome.Succeeded, Is.True);
        Assert.That(outcome.Url, Is.EqualTo("https://example.test/a.png"));
        Assert.That(stub.SawFileName, Is.EqualTo("a.png"));
    }

    [Test]
    public void SchemaConfig_RoundTripsJson()
    {
        UploaderConfigSchema schema = new()
        {
            Title = "Demo",
            Fields =
            [
                new UploaderConfigField { Key = "serverUrl", Label = "Server", Kind = UploaderConfigFieldKind.Url, Required = true },
                new UploaderConfigField { Key = "enabled", Label = "On", Kind = UploaderConfigFieldKind.Boolean }
            ]
        };

        XerahS.UI.ViewModels.SchemaConfigViewModel vm = new(schema);
        vm.LoadFromJson("""{"serverUrl":"https://cloud.example","enabled":true}""");

        Assert.That(vm.Validate(), Is.True);
        Assert.That(vm.ToJson(), Does.Contain("https://cloud.example"));
        Assert.That(vm.ToJson(), Does.Contain("true"));
    }

    private sealed class HandlerStub : IUploadHandler
    {
        public string? SawFileName { get; private set; }

        public Task<UploadOutcome> UploadAsync(UploadRequest request, CancellationToken cancellationToken = default)
        {
            SawFileName = request.FileName;
            return Task.FromResult(UploadOutcome.Success("https://example.test/a.png"));
        }
    }
}
