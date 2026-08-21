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

using NUnit.Framework;
using XerahS.Common;
using XerahS.Core;
using XerahS.UI.Services;

namespace XerahS.Tests.Helpers;

[TestFixture]
public class UpdateServicePreReleaseSourceTests
{
    [Test]
    public void ResolveUpdateRepository_Release_UsesShareX()
    {
        var settings = new ApplicationConfig
        {
            UpdateChannel = UpdateChannel.Release,
            PreReleaseUpdateSource = PreReleaseUpdateSource.KovaForge
        };

        var repository = UpdateService.ResolveUpdateRepository(settings);

        Assert.That(repository.Owner, Is.EqualTo("ShareX"));
        Assert.That(repository.Repo, Is.EqualTo("XerahS"));
    }

    [Test]
    public void ResolveUpdateRepository_PreReleaseShareX_UsesShareX()
    {
        var settings = new ApplicationConfig
        {
            UpdateChannel = UpdateChannel.PreRelease,
            PreReleaseUpdateSource = PreReleaseUpdateSource.ShareX
        };

        var repository = UpdateService.ResolveUpdateRepository(settings);

        Assert.That(repository.Owner, Is.EqualTo("ShareX"));
        Assert.That(repository.Repo, Is.EqualTo("XerahS"));
    }

    [Test]
    public void ResolveUpdateRepository_PreReleaseDefault_UsesKovaForge()
    {
        var settings = new ApplicationConfig
        {
            UpdateChannel = UpdateChannel.PreRelease,
            PreReleaseUpdateSource = PreReleaseUpdateSource.KovaForge
        };

        var repository = UpdateService.ResolveUpdateRepository(settings);

        Assert.That(repository.Owner, Is.EqualTo("KovaForge"));
        Assert.That(repository.Repo, Is.EqualTo("XerahS"));
    }

    [Test]
    public void ResolveUpdateRepository_CustomOwner_UsesOwnerWithDefaultRepo()
    {
        var settings = new ApplicationConfig
        {
            UpdateChannel = UpdateChannel.PreRelease,
            PreReleaseUpdateSource = PreReleaseUpdateSource.Custom,
            CustomPreReleaseUpdateSource = "ExampleOwner"
        };

        var repository = UpdateService.ResolveUpdateRepository(settings);

        Assert.That(repository.Owner, Is.EqualTo("ExampleOwner"));
        Assert.That(repository.Repo, Is.EqualTo("XerahS"));
    }

    [Test]
    public void ResolveUpdateRepository_CustomOwnerRepo_UsesBothValues()
    {
        var settings = new ApplicationConfig
        {
            UpdateChannel = UpdateChannel.PreRelease,
            PreReleaseUpdateSource = PreReleaseUpdateSource.Custom,
            CustomPreReleaseUpdateSource = "ExampleOwner/ExampleRepo"
        };

        var repository = UpdateService.ResolveUpdateRepository(settings);

        Assert.That(repository.Owner, Is.EqualTo("ExampleOwner"));
        Assert.That(repository.Repo, Is.EqualTo("ExampleRepo"));
    }

    [Test]
    public void ResolveUpdateRepository_BlankCustomSource_FallsBackToKovaForge()
    {
        var settings = new ApplicationConfig
        {
            UpdateChannel = UpdateChannel.PreRelease,
            PreReleaseUpdateSource = PreReleaseUpdateSource.Custom,
            CustomPreReleaseUpdateSource = " "
        };

        var repository = UpdateService.ResolveUpdateRepository(settings);

        Assert.That(repository.Owner, Is.EqualTo("KovaForge"));
        Assert.That(repository.Repo, Is.EqualTo("XerahS"));
    }

    [Test]
    public void ResolveUpdateRepositories_Any_UsesShareXAndKovaForge()
    {
        var settings = new ApplicationConfig
        {
            UpdateChannel = UpdateChannel.PreRelease,
            PreReleaseUpdateSource = PreReleaseUpdateSource.Any
        };

        IReadOnlyList<(string Owner, string Repo)> repositories = UpdateService.ResolveUpdateRepositories(settings);

        Assert.That(repositories, Is.EqualTo(new[]
        {
            ("ShareX", "XerahS"),
            ("KovaForge", "XerahS")
        }));
    }

    [Test]
    public void ResolveUpdateRepository_Any_UsesShareXAsPrimary()
    {
        var settings = new ApplicationConfig
        {
            UpdateChannel = UpdateChannel.PreRelease,
            PreReleaseUpdateSource = PreReleaseUpdateSource.Any
        };

        var repository = UpdateService.ResolveUpdateRepository(settings);

        Assert.That(repository.Owner, Is.EqualTo("ShareX"));
        Assert.That(repository.Repo, Is.EqualTo("XerahS"));
    }

    [Test]
    public void PreReleaseUpdateSources_PlacesAnyAfterCustom()
    {
        PreReleaseUpdateSource[] sources = (PreReleaseUpdateSource[])Enum.GetValues(typeof(PreReleaseUpdateSource));

        Assert.That(sources[^2], Is.EqualTo(PreReleaseUpdateSource.Custom));
        Assert.That(sources[^1], Is.EqualTo(PreReleaseUpdateSource.Any));
        Assert.That(EnumExtensions.GetDescription(PreReleaseUpdateSource.Any), Is.EqualTo("Any source"));
    }
}
