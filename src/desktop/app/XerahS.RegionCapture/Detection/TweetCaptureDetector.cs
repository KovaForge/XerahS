using System;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace XerahS.RegionCapture.Detection;

/// <summary>
/// Detects tweet compose boxes and tweet content regions on x.com (Twitter/X web).
/// Uses URL pattern matching + window class detection as the primary detection strategy.
/// </summary>
public interface ITweetCaptureDetector
{
    /// <summary>
    /// Returns true if the current active window appears to be a tweet compose context.
    /// </summary>
    bool IsTweetComposeWindow(string url, string windowTitle);

    /// <summary>
    /// Returns true if the current active window appears to be viewing a tweet.
    /// </summary>
    bool IsTweetViewWindow(string url, string windowTitle);

    /// <summary>
    /// Returns the bounding region hint for a tweet compose box if detected.
    /// Returns null if no tweet compose box is detected.
    /// </summary>
    TweetRegionHint? DetectComposeRegion(string url, string windowTitle);
}

public class TweetRegionHint
{
    public string ProfileId { get; set; } = "twitter-tweet-compose";
    public string Name { get; set; } = "Twitter Tweet Box";
    public float Confidence { get; set; } // 0.0 – 1.0

    // Relative position hints — actual bounds are determined at capture time
    public int RelativeTop { get; set; }    // % from top of window
    public int RelativeLeft { get; set; }   // % from left of window
    public int RelativeWidth { get; set; }  // % of window width
    public int RelativeHeight { get; set; } // % of window height
}

/// <summary>
/// URL + window title pattern matching for Twitter/X web app.
/// Relies on URL structure rather than DOM parsing (which requires headless browser).
/// </summary>
public class TweetCaptureDetector : ITweetCaptureDetector
{
    // Twitter/X URL patterns
    private static readonly Regex ComposeRegex = new(
        @"https?://(www\.)?x\.com/.*/compose/.*",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex TweetViewRegex = new(
        @"https?://(www\.)?x\.com/.*/status/\d+",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex HomeTimelineRegex = new(
        @"https?://(www\.)?x\.com/?$",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    // Window class for Chrome-based browsers (Twitter/X runs in Chrome)
    private const string ChromeWidgetClass = "Chrome_WidgetWin_1";

    public bool IsTweetComposeWindow(string url, string windowTitle)
    {
        return ComposeRegex.IsMatch(url ?? string.Empty);
    }

    public bool IsTweetViewWindow(string url, string windowTitle)
    {
        return TweetViewRegex.IsMatch(url ?? string.Empty);
    }

    public TweetRegionHint? DetectComposeRegion(string url, string windowTitle)
    {
        if (!IsTweetComposeWindow(url, windowTitle))
            return null;

        // Tweet compose box is typically in the top portion of the page
        // These values are heuristics — actual DOM element positions vary by viewport
        return new TweetRegionHint
        {
            ProfileId = "twitter-tweet-compose",
            Name = "Twitter Tweet Box",
            Confidence = 0.92f, // High confidence when URL matches compose pattern
            RelativeTop = 10,    // ~10% from top
            RelativeLeft = 25,   // Centered-ish at 25% from left
            RelativeWidth = 50,  // 50% of window width
            RelativeHeight = 30  // 30% of window height
        };
    }

    /// <summary>
    /// Returns a confidence score for any X.com URL.
    /// Used to rank multiple detected profiles.
    /// </summary>
    public float GetUrlConfidence(string url)
    {
        if (string.IsNullOrEmpty(url))
            return 0f;

        if (ComposeRegex.IsMatch(url)) return 0.92f;
        if (TweetViewRegex.IsMatch(url)) return 0.85f;
        if (HomeTimelineRegex.IsMatch(url)) return 0.60f;

        return 0f;
    }
}