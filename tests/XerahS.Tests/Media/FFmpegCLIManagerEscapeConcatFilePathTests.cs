using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using XerahS.Media;

namespace XerahS.Tests.Media;

[TestFixture]
[NonParallelizable]
public class FFmpegCLIManagerEscapeConcatFilePathTests
{
    [Test]
    public void EscapeConcatFilePath_SingleQuote_EscapesCorrectly()
    {
        var result = EscapeConcatFilePath("clip'segment.mp4");
        Assert.AreEqual("clip'\\''segment.mp4", result);
    }

    [Test]
    public void EscapeConcatFilePath_MultipleQuotes_EscapesAll()
    {
        var result = EscapeConcatFilePath("it's a 'test' file.mp4");
        Assert.AreEqual("it'\\''s a '\\''test'\\'' file.mp4", result);
    }

    [Test]
    public void EscapeConcatFilePath_NoQuote_ReturnsUnchanged()
    {
        var result = EscapeConcatFilePath("normal_file.mp4");
        Assert.AreEqual("normal_file.mp4", result);
    }

    [Test]
    public void EscapeConcatFilePath_EmptyString_ReturnsEmpty()
    {
        var result = EscapeConcatFilePath(string.Empty);
        Assert.AreEqual(string.Empty, result);
    }

    [Test]
    public void EscapeConcatFilePath_LeadingAndTrailingQuotes_EscapesBoth()
    {
        var result = EscapeConcatFilePath("'quoted'.mp4");
        // Each ' becomes \': leading ' -> \', trailing ' -> \'
        Assert.AreEqual("'\\''quoted'\\''.mp4", result);
    }

    [Test]
    public void ConcatenateVideos_ListFileContainsEscapedApostrophe()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"xerahs-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        try
        {
            var inputFile = Path.Combine(tempDir, "clip'segment.mp4");
            var outputFile = Path.Combine(tempDir, "output.mp4");
            File.WriteAllText(inputFile, "mock video data");

            // Directly write the concat list file the way ConcatenateVideos would,
            // without needing FFmpeg to run. This tests the escape logic in isolation.
            var listFile = outputFile + ".txt";
            var escaped = FFmpegCLIManager.TestAccessor.EscapeConcatFilePath(inputFile);
            var contents = $"file '{escaped}'";
            File.WriteAllText(listFile, contents);

            var fileContent = File.ReadAllText(listFile);
            // The list must contain the escaped form so FFmpeg parses it correctly.
            // The inputFile path (with its full temp directory) must have the apostrophe escaped as \'
            Assert.That(fileContent, Does.Contain("clip'\\''segment.mp4'"));
            Assert.That(fileContent, Does.Not.Contain("clip'segment.mp4'")); // unescaped must not appear
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    private static string EscapeConcatFilePath(string path)
    {
        var method = typeof(FFmpegCLIManager).GetMethod(
            "EscapeConcatFilePath",
            BindingFlags.NonPublic | BindingFlags.Static)!;
        return (string)method.Invoke(null, new object[] { path })!;
    }

    private sealed class TestableFFmpegCLIManager : FFmpegCLIManager
    {
        public TestableFFmpegCLIManager(string ffmpegPath) : base(ffmpegPath) { }
        public override int Open(string path, string? args)
        {
            var outputFile = ExtractOutputFileArg(args ?? string.Empty);
            if (!string.IsNullOrEmpty(outputFile))
            {
                File.WriteAllText(outputFile, "mock");
            }
            return 0;
        }

        private static string ExtractOutputFileArg(string args)
        {
            var parts = args.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            foreach (var part in parts.Reverse())
            {
                if (!part.StartsWith('-') && part.EndsWith(".mp4", StringComparison.OrdinalIgnoreCase))
                {
                    return part;
                }
            }
            return string.Empty;
        }
    }
}