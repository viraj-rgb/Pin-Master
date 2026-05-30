using System.IO;
using System.Text.Json;
using PinMaster.Models;

namespace PinMaster.Services;

public sealed class AppSettings
{
    public bool LaunchAtStartup { get; set; }
    public string ThemeOverride { get; set; } = "System";
    public string HotkeyGesture { get; set; } = "Win+Shift+P";
    public List<PersistedPinnedWindow> PinnedWindows { get; set; } = new();
}

public sealed class PersistenceService
{
    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true };
    public string SettingsDirectory { get; } = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "PinMaster");
    public string SettingsPath => Path.Combine(SettingsDirectory, "settings.json");

    public AppSettings Load()
    {
        try
        {
            if (!File.Exists(SettingsPath))
                return new AppSettings();

            var json = File.ReadAllText(SettingsPath);
            return JsonSerializer.Deserialize<AppSettings>(json, Options) ?? new AppSettings();
        }
        catch
        {
            return new AppSettings();
        }
    }

    public void Save(AppSettings settings)
    {
        Directory.CreateDirectory(SettingsDirectory);
        File.WriteAllText(SettingsPath, JsonSerializer.Serialize(settings, Options));
    }

    public void SavePinnedWindows(IEnumerable<PinnedWindow> pinnedWindows, AppSettings settings)
    {
        settings.PinnedWindows = pinnedWindows
            .Select(p => new PersistedPinnedWindow { Title = p.Title, ProcessName = p.ProcessName })
            .DistinctBy(p => $"{p.ProcessName}|{p.Title}", StringComparer.OrdinalIgnoreCase)
            .ToList();
        Save(settings);
    }
}
