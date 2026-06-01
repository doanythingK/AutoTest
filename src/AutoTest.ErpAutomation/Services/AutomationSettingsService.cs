using System.IO;
using System.Text.Json;
using AutoTest.ErpAutomation.Models;

namespace AutoTest.ErpAutomation.Services;

public sealed class AutomationSettingsService
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true
    };

    public string SettingsPath { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "AutoTest.ErpAutomation",
        "settings.json");

    public AutomationSettings Load()
    {
        if (!File.Exists(SettingsPath))
        {
            return new AutomationSettings();
        }

        try
        {
            var json = File.ReadAllText(SettingsPath);
            return Normalize(JsonSerializer.Deserialize<AutomationSettings>(json) ?? new AutomationSettings());
        }
        catch
        {
            return new AutomationSettings();
        }
    }

    public void Save(AutomationSettings settings)
    {
        var directory = Path.GetDirectoryName(SettingsPath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        File.WriteAllText(SettingsPath, JsonSerializer.Serialize(settings, SerializerOptions));
    }

    private static AutomationSettings Normalize(AutomationSettings settings)
    {
        settings.ChromePath ??= string.Empty;
        settings.ChromeProfileDirectory = string.IsNullOrWhiteSpace(settings.ChromeProfileDirectory)
            ? "Default"
            : settings.ChromeProfileDirectory;

        if (settings.RemoteDebuggingPort < 1 || settings.RemoteDebuggingPort > 65535)
        {
            settings.RemoteDebuggingPort = 9222;
        }

        return settings;
    }
}
