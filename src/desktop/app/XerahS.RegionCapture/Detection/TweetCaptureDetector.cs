using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace XerahS.RegionCapture.Detection;

/// <summary>
/// Detects tweet compose boxes and tweet content regions on X/Twitter web.
/// Uses URL pattern matching plus conservative window-title heuristics.
/// </summary>
public interface ITweetCaptureDetector
{
    bool IsTweetComposeWindow(string? url, string? windowTitle);
    bool IsTweetViewWindow(string? url, string? windowTitle);
    bool IsTimelineWindow(string? url, string? windowTitle);
    TweetRegionHint? DetectComposeRegion(string? url, string? windowTitle);
    TweetRegionHint? DetectTweetViewRegion(string? url, string? windowTitle);
    IReadOnlyList<TweetRegionHint> GetSuggestedRegions(string? url, string? windowTitle);
}

public class TweetRegionHint
{
    public string ProfileId { get; set; } = "twitter-tweet-compose";
    public string Name { get; set; } = "Tweet composer";
    public float Confidence { get; set; }
    public int RelativeTop { get; set; }
    public int RelativeLeft { get; set; }
    public int RelativeWidth { get; set; }
    public int RelativeHeight { get; set; }
}

/// <summary>
/// URL + window title pattern matching for Twitter/X web app.
/// Relies on URL structure rather than DOM parsing.
/// </summary>
public class TweetCaptureDetector : ITweetCaptureDetector
{
    private static readonly Regex ComposeRegex = new(
        @"https?://(www\.)?(x|twitter)\.com/.*/compose/.*",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex TweetViewRegex = new(
        @"https?://(www\.)?(x|twitter)\.com/.*/status/\d+",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex HomeTimelineRegex = new(
        @"https?://(www\.)?(x|twitter)\.com/?$",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public bool IsTweetComposeWindow(string? url, string? windowTitle)
    {
        return ComposeRegex.IsMatch(url ?? string.Empty)
            || IsTitleMatch(windowTitle, "compose", "post", "tweet");
    }

    public bool IsTweetViewWindow(string? url, string? windowTitle)
    {
        return TweetViewRegex.IsMatch(url ?? string.Empty)
            || IsTitleMatch(windowTitle, " on x", " on twitter");
    }

    public bool IsTimelineWindow(string? url, string? windowTitle)
    {
        return HomeTimelineRegex.IsMatch(url ?? string.Empty)
            || IsTitleMatch(windowTitle, "home / x", "home / twitter");
    }

    public TweetRegionHint? DetectComposeRegion(string? url, string? windowTitle)
    {
        if (!IsTweetComposeWindow(url, windowTitle))
        {
            return null;
        }

        return new TweetRegionHint
        {
            ProfileId = "twitter-tweet-compose",
            Name = "Tweet composer",
            Confidence = ComposeRegex.IsMatch(url ?? string.Empty) ? 0.92f : 0.76f,
            RelativeTop = 10,
            RelativeLeft = 25,
            RelativeWidth = 50,
            RelativeHeight = 30
        };
    }

    public TweetRegionHint? DetectTweetViewRegion(string? url, string? windowTitle)
    {
        if (!IsTweetViewWindow(url, windowTitle))
        {
            return null;
        }

        return new TweetRegionHint
        {
            ProfileId = "twitter-tweet-view",
            Name = "Tweet content",
            Confidence = TweetViewRegex.IsMatch(url ?? string.Empty) ? 0.86f : 0.68f,
            RelativeTop = 18,
            RelativeLeft = 22,
            RelativeWidth = 56,
            RelativeHeight = 46
        };
    }

    public IReadOnlyList<TweetRegionHint> GetSuggestedRegions(string? url, string? windowTitle)
    {
        List<TweetRegionHint> suggestions = [];

        TweetRegionHint? composeRegion = DetectComposeRegion(url, windowTitle);
        if (composeRegion is not null)
        {
            suggestions.Add(composeRegion);
        }

        TweetRegionHint? tweetViewRegion = DetectTweetViewRegion(url, windowTitle);
        if (tweetViewRegion is not null)
        {
            suggestions.Add(tweetViewRegion);
        }

        if (IsTimelineWindow(url, windowTitle))
        {
            suggestions.Add(new TweetRegionHint
            {
                ProfileId = "twitter-home-timeline",
                Name = "Timeline column",
                Confidence = HomeTimelineRegex.IsMatch(url ?? string.Empty) ? 0.60f : 0.45f,
                RelativeTop = 14,
                RelativeLeft = 24,
                RelativeWidth = 52,
                RelativeHeight = 70
            });
        }

        return suggestions
            .OrderByDescending(hint => hint.Confidence)
            .ThenBy(hint => hint.Name, StringComparer.Ordinal)
            .ToArray();
    }

    public float GetUrlConfidence(string? url)
    {
        if (string.IsNullOrEmpty(url))
        {
            return 0f;
        }

        if (ComposeRegex.IsMatch(url)) return 0.92f;
        if (TweetViewRegex.IsMatch(url)) return 0.86f;
        if (HomeTimelineRegex.IsMatch(url)) return 0.60f;

        return 0f;
    }

    private static bool IsTitleMatch(string? windowTitle, params string[] fragments)
    {
        if (string.IsNullOrWhiteSpace(windowTitle))
        {
            return false;
        }

        return fragments.Any(fragment => windowTitle.Contains(fragment, StringComparison.OrdinalIgnoreCase));
    }
}
