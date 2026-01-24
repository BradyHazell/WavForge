using System.Text.Json;
using WavForge.Models;

namespace WavForge.Services;

internal sealed class JsonSettingsService : ISettingsService
{
    private readonly string _settingsPath;
    private readonly JsonSerializerOptions _jsonOptions = new() { WriteIndented = true };

    public JsonSettingsService()
    {
        string dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "WavForge");
        
        Directory.CreateDirectory(dir);
        _settingsPath = Path.Combine(dir, "settings.json");
    }

    public AppSettings Settings { get; private set; }
    public void Load()
    {
        if (!File.Exists(_settingsPath))
        {
            Settings = new AppSettings();
            Save();
        }

        try
        {
            string json = File.ReadAllText(_settingsPath);
            Settings = JsonSerializer.Deserialize<AppSettings>(json) ?? new AppSettings();
        }
        catch
        {
            Settings = new AppSettings();
        }
    }

    public void Save()
    {
        string json = JsonSerializer.Serialize(Settings, _jsonOptions);
        
        File.WriteAllText(_settingsPath, json);
    }
}
