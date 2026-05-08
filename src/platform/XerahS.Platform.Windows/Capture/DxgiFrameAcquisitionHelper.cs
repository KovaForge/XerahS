namespace XerahS.Platform.Windows.Capture;

internal static class DxgiFrameAcquisitionHelper
{
    public static bool IsUsableFrame(bool acquireSucceeded, bool desktopResourceAvailable)
    {
        return acquireSucceeded && desktopResourceAvailable;
    }

    public static bool ShouldRetryFrameAcquisition(bool acquireSucceeded, bool desktopResourceAvailable)
    {
        return !IsUsableFrame(acquireSucceeded, desktopResourceAvailable);
    }
}
