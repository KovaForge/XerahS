using NUnit.Framework;
using XerahS.Common;

namespace XerahS.Tests.Helpers;

[TestFixture]
public class SettingsBaseEnumLoadingTests
{
    private enum LowercaseEnum
    {
        aac,
        other
    }

    private sealed class LowercaseEnumSettings : SettingsBase<LowercaseEnumSettings>
    {
        public LowercaseEnumSettings()
        {
        }

        public LowercaseEnum Value { get; set; } = LowercaseEnum.other;
    }

    [Test]
    public void Load_ParsesLowercaseEnumValue()
    {
        string path = CreateSettingsFile("""{"Value":"aac"}""");

        LowercaseEnumSettings settings = LowercaseEnumSettings.Load(path, fallbackSupport: false);

        Assert.That(settings.Value, Is.EqualTo(LowercaseEnum.aac));
    }

    [Test]
    public void Load_KeepsExistingDefault_WhenEnumValueIsUnknown()
    {
        string path = CreateSettingsFile("""{"Value":"missing"}""");

        LowercaseEnumSettings settings = LowercaseEnumSettings.Load(path, fallbackSupport: false);

        Assert.That(settings.Value, Is.EqualTo(LowercaseEnum.other));
    }

    private static string CreateSettingsFile(string json)
    {
        string directory = Path.Combine(TestContext.CurrentContext.WorkDirectory, Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);

        string path = Path.Combine(directory, "Settings.json");
        File.WriteAllText(path, json);
        return path;
    }
}
