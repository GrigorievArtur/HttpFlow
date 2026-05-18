using System;
using System.IO;
using System.Text.Json;

namespace Httpflow.Desktop.Services.Settings;

public enum AppThemeMode
{
    Light,
    Dark
}

public sealed class AppSettingsService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true
    };

    private readonly string _settingsPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "Httpflow.Desktop",
        "settings.json");

    public AppThemeMode? LoadThemeMode()
    {
        if (!File.Exists(_settingsPath))
        {
            return null;
        }

        try
        {
            var settings = JsonSerializer.Deserialize<AppSettings>(
                File.ReadAllText(_settingsPath),
                JsonOptions);

            return settings?.ThemeMode;
        }
        catch (JsonException)
        {
            return null;
        }
        catch (IOException)
        {
            return null;
        }
    }

    public void SaveThemeMode(AppThemeMode themeMode)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_settingsPath)!);

        var settings = new AppSettings
        {
            ThemeMode = themeMode
        };

        File.WriteAllText(_settingsPath, JsonSerializer.Serialize(settings, JsonOptions));
    }

    private sealed class AppSettings
    {
        public AppThemeMode ThemeMode { get; init; }
    }
}
