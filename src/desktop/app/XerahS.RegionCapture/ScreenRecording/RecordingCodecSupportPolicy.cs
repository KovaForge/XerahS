using System.Collections.Generic;

namespace XerahS.RegionCapture.ScreenRecording;

/// <summary>
/// Centralizes which screen-recording codecs are directly supported by the native backend
/// versus requiring the FFmpeg fallback in the current build.
/// </summary>
public static class RecordingCodecSupportPolicy
{
    private static readonly IReadOnlyList<VideoCodec> NativeOnlyCodecs = new[]
    {
        VideoCodec.H264
    };

    private static readonly IReadOnlyList<VideoCodec> AllCodecs = new[]
    {
        VideoCodec.H264,
        VideoCodec.HEVC,
        VideoCodec.VP9,
        VideoCodec.AV1
    };

    public static IReadOnlyList<VideoCodec> GetSelectableCodecs(bool ffmpegAvailable)
    {
        return GetSelectableCodecs(
            ffmpegAvailable,
            OperatingSystem.IsWindows(),
            OperatingSystem.IsMacOS(),
            OperatingSystem.IsLinux());
    }

    public static bool RequiresFfmpegFallback(VideoCodec codec)
    {
        return RequiresFfmpegFallback(
            codec,
            OperatingSystem.IsWindows(),
            OperatingSystem.IsMacOS(),
            OperatingSystem.IsLinux());
    }

    internal static IReadOnlyList<VideoCodec> GetSelectableCodecs(
        bool ffmpegAvailable,
        bool isWindows,
        bool isMacOS,
        bool isLinux)
    {
        if (isLinux)
        {
            return AllCodecs;
        }

        if ((isWindows || isMacOS) && ffmpegAvailable)
        {
            return AllCodecs;
        }

        return NativeOnlyCodecs;
    }

    internal static bool RequiresFfmpegFallback(
        VideoCodec codec,
        bool isWindows,
        bool isMacOS,
        bool isLinux)
    {
        if (codec == VideoCodec.H264)
        {
            return false;
        }

        if (isLinux)
        {
            return false;
        }

        return isWindows || isMacOS;
    }
}
