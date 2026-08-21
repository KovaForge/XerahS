#nullable enable

using System.ComponentModel;
using System.Globalization;
using NUnit.Framework;
using XerahS.Common;

namespace XerahS.Tests.Common;

[TestFixture]
public class StringCollectionToStringTypeConverterTests
{
    [Test]
    public void ConvertTo_ListOfStrings_ReturnsCommaJoinedString()
    {
        var converter = new StringCollectionToStringTypeConverter();
        var list = new List<string> { "alpha", "beta", "gamma" };
        var result = converter.ConvertTo(null, null, list, typeof(string));
        Assert.That(result, Is.EqualTo("alpha, beta, gamma"));
    }

    [Test]
    public void ConvertTo_EmptyList_ReturnsEmptyString()
    {
        var converter = new StringCollectionToStringTypeConverter();
        var list = new List<string>();
        var result = converter.ConvertTo(null, null, list, typeof(string));
        Assert.That(result, Is.EqualTo(string.Empty));
    }

    [Test]
    public void ConvertTo_StringArray_DelegatesToBase_NotEmptyString()
    {
        // String[] is not List<string>, so this must delegate to base.ConvertTo.
        // Before the fix, non-List<string> types silently returned string.Empty,
        // erasing supported types like string[] or StringCollection.
        // After the fix, unsupported types delegate to base.
        var converter = new StringCollectionToStringTypeConverter();
        string[] array = [ "item1", "item2" ];
        var result = converter.ConvertTo(null, null, array, typeof(string));
        // base.ConvertTo for string[] returns array's ToString() (type name).
        // After fix: must NOT return string.Empty.
        Assert.That(result, Is.Not.EqualTo(string.Empty));
    }

    [Test]
    public void ConvertTo_Dictionary_DelegatesToBase_NotEmptyString()
    {
        var converter = new StringCollectionToStringTypeConverter();
        var dict = new Dictionary<string, string> { ["key"] = "value" };
        var result = converter.ConvertTo(null, null, dict, typeof(string));
        // Should delegate to base, not silently return string.Empty
        Assert.That(result, Is.Not.EqualTo(string.Empty));
    }

    [Test]
    public void ConvertTo_NonStringDestination_NeverReturnsEmptyString()
    {
        var converter = new StringCollectionToStringTypeConverter();
        var list = new List<string> { "a", "b" };
        // When destination is not string, we must not silently swallow the value
        // and return string.Empty — that would hide real conversion failures.
        // The current implementation delegates to base.ConvertTo which throws
        // for List<string>→object, which is better than silently returning "".
        var result = converter.ConvertTo(null, null, list, typeof(object));
        // Must NOT return string.Empty — that would silently erase the type.
        Assert.That(result, Is.Not.EqualTo(string.Empty));
    }
}
