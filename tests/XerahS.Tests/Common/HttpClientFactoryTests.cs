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

using System;
using System.Net.Http;
using System.Threading;
using NUnit.Framework;
using XerahS.Common;

namespace XerahS.Tests.Common;

[TestFixture, NonParallelizable]
public sealed class HttpClientFactoryTests
{
    private ProxyMethod _originalMethod;
    private string _originalHost = string.Empty;
    private int _originalPort;
    private string _originalUsername = string.Empty;
    private string _originalPassword = string.Empty;

    [SetUp]
    public void SetUp()
    {
        ProxyInfo proxy = HelpersOptions.CurrentProxy;
        _originalMethod = proxy.ProxyMethod;
        _originalHost = proxy.Host;
        _originalPort = proxy.Port;
        _originalUsername = proxy.Username;
        _originalPassword = proxy.Password;
        HttpClientFactory.Reset();
    }

    [TearDown]
    public void TearDown()
    {
        HelpersOptions.CurrentProxy.ProxyMethod = _originalMethod;
        HelpersOptions.CurrentProxy.Host = _originalHost;
        HelpersOptions.CurrentProxy.Port = _originalPort;
        HelpersOptions.CurrentProxy.Username = _originalUsername;
        HelpersOptions.CurrentProxy.Password = _originalPassword;
        HttpClientFactory.Reset();
    }

    [Test]
    public void Create_ReturnsPooledInstance()
    {
        HttpClient first = HttpClientFactory.Create();
        HttpClient second = HttpClientFactory.Create();

        Assert.That(second, Is.SameAs(first));
        Assert.That(first.Timeout, Is.EqualTo(TimeSpan.FromSeconds(100)));
    }

    [Test]
    public void Create_RedirectAndTimeoutKeys_AreDistinct()
    {
        HttpClient defaults = HttpClientFactory.Create();
        HttpClient noRedirect = HttpClientFactory.Create(allowAutoRedirect: false, infiniteTimeout: false);
        HttpClient upload = HttpClientFactory.Create(allowAutoRedirect: true, infiniteTimeout: true);

        Assert.That(noRedirect, Is.Not.SameAs(defaults));
        Assert.That(upload, Is.Not.SameAs(defaults));
        Assert.That(upload.Timeout, Is.EqualTo(Timeout.InfiniteTimeSpan));
    }

    [Test]
    public void Create_ProxyChange_RecyclesPool()
    {
        HttpClient before = HttpClientFactory.Create();

        HelpersOptions.CurrentProxy.ProxyMethod = ProxyMethod.Manual;
        HelpersOptions.CurrentProxy.Host = "127.0.0.1";
        HelpersOptions.CurrentProxy.Port = 8888;

        HttpClient after = HttpClientFactory.Create();

        Assert.That(after, Is.Not.SameAs(before));
    }

    [Test]
    public void Reset_DisposesPooledClient()
    {
        HttpClient client = HttpClientFactory.Create();
        HttpClientFactory.Reset();

        Assert.That(
            async () => await client.GetAsync("https://example.invalid/xerahs-factory-reset"),
            Throws.TypeOf<ObjectDisposedException>());
    }
}
