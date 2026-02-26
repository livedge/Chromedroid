namespace Chromedroid;

/// <summary>
/// Thrown when an ADB operation fails. Wraps underlying ADB client exceptions
/// so consumers don't need a direct dependency on AdvancedSharpAdbClient.
/// </summary>
public sealed class AdbException : Exception
{
    public AdbException(string message) : base(message) { }

    public AdbException(string message, Exception innerException) : base(message, innerException) { }
}
