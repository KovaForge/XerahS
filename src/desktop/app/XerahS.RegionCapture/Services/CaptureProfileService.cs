using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace XerahS.RegionCapture.Services;

public class CaptureProfile
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public int Version { get; set; } = 1;
    public DateTime Created { get; set; } = DateTime.UtcNow;
    public string? Hotkey { get; set; }
    public DetectionProfile? Detection { get; set; }
    public CaptureSpec? Capture { get; set; }
}

public class DetectionProfile
{
    public string Type { get; set; } = string.Empty; // "url+element", "window-class", "accessibility"
    public string? UrlPattern { get; set; }
    public string? WindowClass { get; set; }
    public string? ElementClass { get; set; }
    public Padding? Padding { get; set; }
}

public class CaptureSpec
{
    public string Type { get; set; } = "relative";
    public string Anchor { get; set; } = "element";
    public bool IncludeDecoration { get; set; } = false;
}

public class Padding
{
    public int Top { get; set; }
    public int Bottom { get; set; }
    public int Left { get; set; }
    public int Right { get; set; }
}

/// <summary>
/// Service for managing capture profiles — reusable, named capture regions
/// that auto-detect common UI patterns (tweets, chat windows, code blocks).
/// </summary>
public interface ICaptureProfileService
{
    /// <summary>
    /// Returns all saved profiles, including curated defaults.
    /// </summary>
    Task<IReadOnlyList<CaptureProfile>> GetProfilesAsync();

    /// <summary>
    /// Saves a user-created profile.
    /// </summary>
    Task SaveProfileAsync(CaptureProfile profile);

    /// <summary>
    /// Deletes a user profile by ID. Curated defaults cannot be deleted.
    /// </summary>
    Task DeleteProfileAsync(string id);

    /// <summary>
    /// Detects capture regions in the current active window.
    /// Returns up to 3 suggested profiles ordered by confidence.
    /// </summary>
    Task<IReadOnlyList<CaptureProfile>> DetectRegionsAsync();
}

public class CaptureProfileService : ICaptureProfileService
{
    private readonly List<CaptureProfile> _profiles = new();

    public Task<IReadOnlyList<CaptureProfile>> GetProfilesAsync()
    {
        return Task.FromResult<IReadOnlyList<CaptureProfile>>(_profiles);
    }

    public Task SaveProfileAsync(CaptureProfile profile)
    {
        _profiles.Add(profile);
        return Task.CompletedTask;
    }

    public Task DeleteProfileAsync(string id)
    {
        _profiles.RemoveAll(p => p.Id == id && p.Detection?.Type != "curated");
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<CaptureProfile>> DetectRegionsAsync()
    {
        return Task.FromResult<IReadOnlyList<CaptureProfile>>(Array.Empty<CaptureProfile>());
    }
}