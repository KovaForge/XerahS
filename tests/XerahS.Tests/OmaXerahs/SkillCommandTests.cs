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
using XerahS.OmaXerahs.Commands;

namespace XerahS.Tests.OmaXerahs;

[TestFixture]
public class SkillCommandTests
{
    private string _root = null!;
    private string _home = null!;
    private string _skillDirectory = null!;

    [SetUp]
    public void SetUp()
    {
        _root = Path.Combine(Path.GetTempPath(), "xerahs-skill-tests-" + Guid.NewGuid().ToString("N"));
        _home = Path.Combine(_root, "home");
        _skillDirectory = Path.Combine(_home, ".local", "share", "xerahs", "agents", "skills", "xerahs");
        Directory.CreateDirectory(_home);
    }

    [TearDown]
    public void TearDown()
    {
        Directory.Delete(_root, recursive: true);
    }

    [Test]
    public void Install_WritesSkillAndLinksOnlyIntoPresentAgents()
    {
        Directory.CreateDirectory(Path.Combine(_home, ".claude"));

        var response = SkillCommand.Install(_home, _skillDirectory);

        string skillFile = Path.Combine(_skillDirectory, "SKILL.md");
        Assert.That(File.ReadAllText(skillFile), Does.StartWith("---\nname: xerahs\n"));
        Assert.That(response.Links.Select(link => link.Status), Is.All.EqualTo("linked"));
        Assert.That(new FileInfo(Path.Combine(_home, ".agents", "skills", "xerahs")).LinkTarget, Is.EqualTo(_skillDirectory));
        Assert.That(new FileInfo(Path.Combine(_home, ".claude", "skills", "xerahs")).LinkTarget, Is.EqualTo(_skillDirectory));
        Assert.That(Directory.Exists(Path.Combine(_home, ".codex")), Is.False, "Agents that are not set up are left alone.");

        var again = SkillCommand.Install(_home, _skillDirectory);
        Assert.That(again.Links.Select(link => link.Status), Is.All.EqualTo("exists"));
    }

    [Test]
    public void Install_LeavesForeignSkillUntouched_AndUninstallRemovesOnlyOwnLinks()
    {
        string foreign = Path.Combine(_home, ".codex", "skills", "xerahs");
        Directory.CreateDirectory(foreign);
        File.WriteAllText(Path.Combine(foreign, "SKILL.md"), "mine");

        var install = SkillCommand.Install(_home, _skillDirectory);
        Assert.That(install.Links.Single(link => link.Path == foreign).Status, Is.EqualTo("skipped"));

        var uninstall = SkillCommand.Uninstall(_home, _skillDirectory);

        Assert.That(uninstall.Links.Select(link => link.Path), Does.Not.Contain(foreign));
        Assert.That(File.ReadAllText(Path.Combine(foreign, "SKILL.md")), Is.EqualTo("mine"));
        Assert.That(Path.Exists(Path.Combine(_home, ".agents", "skills", "xerahs")), Is.False);
        Assert.That(Directory.Exists(_skillDirectory), Is.False);
    }
}
