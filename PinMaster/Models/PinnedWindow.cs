namespace PinMaster.Models;

public sealed class PinnedWindow
{
    public nint Handle { get; set; }
    public string Title { get; set; } = string.Empty;
    public string ProcessName { get; set; } = string.Empty;
    public string ProcessPath { get; set; } = string.Empty;
    public DateTime PinnedAt { get; set; } = DateTime.UtcNow;
}

public sealed class PersistedPinnedWindow
{
    public string Title { get; set; } = string.Empty;
    public string ProcessName { get; set; } = string.Empty;
}

public sealed class WindowInfo
{
    public nint Handle { get; set; }
    public string Title { get; set; } = string.Empty;
    public string DisplayTitle => Title.Length <= 40 ? Title : Title[..37] + "...";
    public string ProcessName { get; set; } = string.Empty;
    public string ProcessPath { get; set; } = string.Empty;
    public System.Windows.Media.ImageSource? Icon { get; set; }
    public bool IsPinned { get; set; }
}
