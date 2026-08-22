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

namespace XerahS.Tests.Build;

[TestFixture]
public class DistroRepoPackagingTests
{
    private static string RepoRoot =>
        Path.GetFullPath(Path.Combine(TestContext.CurrentContext.TestDirectory, "../../../../../"));

    [Test]
    public void RepoStagingTemplates_ExistWithStampTokens()
    {
        string staging = Path.Combine(RepoRoot, "build", "linux", "repo-staging");
        string spec = File.ReadAllText(Path.Combine(staging, "xerahs.spec"));
        string obsSpec = File.ReadAllText(Path.Combine(staging, "xerahs.obs.spec"));
        string changelog = File.ReadAllText(Path.Combine(staging, "debian", "changelog.in"));
        string control = File.ReadAllText(Path.Combine(staging, "debian", "control"));
        string service = File.ReadAllText(Path.Combine(staging, "_service"));

        Assert.That(spec, Does.Contain("@VERSION@"));
        Assert.That(spec, Does.Contain("@REPO@"));
        Assert.That(spec, Does.Contain("@TARBALL_ARCH@"));
        Assert.That(spec, Does.Contain("ExclusiveArch:"));
        Assert.That(spec, Does.Not.Contain("linux-linux-"));

        Assert.That(obsSpec, Does.Contain("linux-x64.tar.gz"));
        Assert.That(obsSpec, Does.Contain("linux-arm64.tar.gz"));
        Assert.That(obsSpec, Does.Contain("99-xerahs-input.rules"));

        Assert.That(changelog, Does.Contain("@UBUNTU_SERIES@"));
        Assert.That(changelog, Does.Not.Contain("unstable"));
        Assert.That(control, Does.Contain("debhelper-compat"));
        Assert.That(File.Exists(Path.Combine(staging, "debian", "postinst")), Is.True);
        Assert.That(File.Exists(Path.Combine(staging, "debian", "postrm")), Is.True);

        Assert.That(service, Does.Contain("linux-x64.tar.gz"));
        Assert.That(service, Does.Contain("linux-arm64.tar.gz"));
    }

    [Test]
    public void PublishReleaseScripts_ExposeDistroRepoFlags()
    {
        string skillDir = Path.Combine(RepoRoot, ".ai", "skills", "publish-release");
        string skill = File.ReadAllText(Path.Combine(skillDir, "SKILL.md"));
        string sequence = File.ReadAllText(Path.Combine(skillDir, "scripts", "run-release-sequence.sh"));
        string publish = File.ReadAllText(Path.Combine(skillDir, "scripts", "publish-distro-repos.sh"));

        Assert.That(File.Exists(Path.Combine(skillDir, "scripts", "prepare-distro-repo-assets.sh")), Is.True);
        Assert.That(File.Exists(Path.Combine(skillDir, "scripts", "publish-distro-repos.sh")), Is.True);
        Assert.That(skill, Does.Contain("--publish-distro-repos"));
        Assert.That(skill, Does.Contain("docs/linux/distro-repos.md"));
        Assert.That(sequence, Does.Contain("--publish-distro-repos"));
        Assert.That(publish, Does.Contain("LAUNCHPAD_GPG_PRIVATE_KEY"));
        Assert.That(publish, Does.Contain("COPR_CONFIG"));
        Assert.That(publish, Does.Contain("OSC_USERNAME"));
        Assert.That(publish, Does.StartWith("#!/usr/bin/env bash"));
        Assert.That(publish, Does.Not.Contain("\r"));
    }

    [Test]
    public void ReleaseWorkflow_HasSecretsGatedLinuxRepoJob()
    {
        string workflow = File.ReadAllText(Path.Combine(RepoRoot,
            ".github", "workflows", "release-build-all-platforms.yml"));

        Assert.That(workflow, Does.Contain("publish-linux-repos"));
        Assert.That(workflow, Does.Contain("publish-distro-repos.sh"));
        Assert.That(workflow, Does.Contain("LAUNCHPAD_GPG_PRIVATE_KEY"));
        Assert.That(workflow, Does.Contain("COPR_CONFIG"));
        Assert.That(workflow, Does.Contain("OSC_PASSWORD"));
    }

    [Test]
    public void PrepareDistroRepoAssets_StampsUbuntuSeriesAndObsSpec()
    {
        string? bash = FindBash();
        if (string.IsNullOrEmpty(bash))
        {
            Assert.Ignore("bash is not available on this host.");
        }

        string output = Path.Combine(Path.GetTempPath(), "xerahs-distro-repo-test-" + Guid.NewGuid().ToString("N"));
        try
        {
            string unixOutput = output.Replace('\\', '/');
            if (unixOutput.Length >= 2 && unixOutput[1] == ':')
            {
                unixOutput = "/" + char.ToLowerInvariant(unixOutput[0]) + unixOutput[2..];
            }

            var psi = new System.Diagnostics.ProcessStartInfo
            {
                FileName = bash!,
                Arguments = "./.ai/skills/publish-release/scripts/prepare-distro-repo-assets.sh --tag v0.28.0 --repo ShareX/XerahS --output-dir " + unixOutput + " --ubuntu-series jammy",
                WorkingDirectory = RepoRoot,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
            };
            using var proc = System.Diagnostics.Process.Start(psi);
            Assert.That(proc, Is.Not.Null);
            string stderr = proc!.StandardError.ReadToEnd();
            string stdout = proc.StandardOutput.ReadToEnd();
            proc.WaitForExit(60000);
            Assert.That(proc.ExitCode, Is.EqualTo(0), stdout + Environment.NewLine + stderr);

            string changelog = File.ReadAllText(Path.Combine(output, "ppa", "debian", "changelog"));
            Assert.That(changelog, Does.Contain("jammy"));
            Assert.That(changelog, Does.Contain("0.28.0-1~jammy1"));
            Assert.That(File.Exists(Path.Combine(output, "copr", "xerahs-linux-x64.spec")), Is.True);
            Assert.That(File.Exists(Path.Combine(output, "copr", "xerahs-linux-arm64.spec")), Is.True);
            Assert.That(File.Exists(Path.Combine(output, "obs", "xerahs.spec")), Is.True);
            Assert.That(File.Exists(Path.Combine(output, "obs", "_service")), Is.True);
            string x64Spec = File.ReadAllText(Path.Combine(output, "copr", "xerahs-linux-x64.spec"));
            Assert.That(x64Spec, Does.Contain("linux-x64.tar.gz"));
            Assert.That(x64Spec, Does.Not.Contain("@VERSION@"));
            Assert.That(x64Spec, Does.Contain("ExclusiveArch:  x86_64"));
            string obsSpec = File.ReadAllText(Path.Combine(output, "obs", "xerahs.spec"));
            Assert.That(obsSpec, Does.Contain("linux-arm64.tar.gz"));
            Assert.That(File.Exists(Path.Combine(output, "ppa", "debian", "99-xerahs-input.rules")), Is.True);
        }
        finally
        {
            if (Directory.Exists(output))
            {
                Directory.Delete(output, true);
            }
        }
    }

    private static string? FindBash()
    {
        string[] candidates =
        [
            "bash",
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Git", "bin", "bash.exe"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Git", "bin", "bash.exe"),
        ];
        foreach (string candidate in candidates)
        {
            if (candidate == "bash")
            {
                try
                {
                    var psi = new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = "bash",
                        Arguments = "-lc \"echo ok\"",
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        UseShellExecute = false,
                    };
                    using var proc = System.Diagnostics.Process.Start(psi);
                    proc?.WaitForExit(5000);
                    if (proc is { ExitCode: 0 })
                    {
                        return "bash";
                    }
                }
                catch
                {
                    // ignored
                }

                continue;
            }

            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        return null;
    }
}
