using System.Linq;
using NUnit.Framework;
using XerahS.RegionCapture.Detection;

namespace XerahS.Tests.RegionCapture;

[TestFixture]
public class TweetCaptureDetectorTests
{
    private TweetCaptureDetector _detector = null!;

    [SetUp]
    public void SetUp()
    {
        _detector = new TweetCaptureDetector();
    }

    [TestCase("https://x.com/compose/post")]
    [TestCase("https://twitter.com/compose/post")]
    public void IsTweetComposeWindow_SupportsXAndTwitterUrls(string url)
    {
        Assert.That(_detector.IsTweetComposeWindow(url, "Compose post / X"), Is.True);
    }

    [TestCase("https://x.com/someuser/status/1234567890")]
    [TestCase("https://twitter.com/someuser/status/1234567890")]
    public void DetectTweetViewRegion_SupportsXAndTwitterUrls(string url)
    {
        TweetRegionHint? hint = _detector.DetectTweetViewRegion(url, "Some User on X");

        Assert.Multiple(() =>
        {
            Assert.That(hint, Is.Not.Null);
            Assert.That(hint!.ProfileId, Is.EqualTo("twitter-tweet-view"));
            Assert.That(hint.Name, Is.EqualTo("Tweet content"));
            Assert.That(hint.Confidence, Is.GreaterThan(0.8f));
        });
    }

    [Test]
    public void GetSuggestedRegions_ComposeRanksAheadOfTimeline()
    {
        var suggestions = _detector.GetSuggestedRegions("https://x.com/compose/post", "Compose post / X");

        Assert.Multiple(() =>
        {
            Assert.That(suggestions, Has.Count.EqualTo(1));
            Assert.That(suggestions[0].ProfileId, Is.EqualTo("twitter-tweet-compose"));
        });
    }

    [Test]
    public void GetSuggestedRegions_TimelineReturnsLowerConfidenceSuggestion()
    {
        var suggestions = _detector.GetSuggestedRegions("https://twitter.com/", "Home / Twitter");

        Assert.Multiple(() =>
        {
            Assert.That(suggestions, Has.Count.EqualTo(1));
            Assert.That(suggestions[0].ProfileId, Is.EqualTo("twitter-home-timeline"));
            Assert.That(suggestions[0].Confidence, Is.LessThan(0.7f));
        });
    }

    [Test]
    public void GetSuggestedRegions_UnsupportedUrlReturnsNone()
    {
        var suggestions = _detector.GetSuggestedRegions("https://example.com/docs", "Example Docs");

        Assert.That(suggestions, Is.Empty);
    }

    [Test]
    public void GetSuggestedRegions_SortsByConfidenceDescending()
    {
        var suggestions = _detector.GetSuggestedRegions("https://x.com/someuser/status/1234567890", "Some User on X")
            .Select(x => x.Confidence)
            .ToArray();

        Assert.That(suggestions, Is.Ordered.Descending);
    }
}
