using System.Text.Json;
using SmartMouth.App.Models;

namespace SmartMouth.App.Services;

public sealed class ConfigService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    private readonly string _configPath;

    public ConfigService()
    {
        var appDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "SmartMouth");
        _configPath = Path.Combine(appDir, "config.json");
    }

    public AppConfig Load()
    {
        try
        {
            if (!File.Exists(_configPath))
            {
                var config = new AppConfig();
                config.Clamp();
                return config;
            }

            var json = File.ReadAllText(_configPath);
            var parsed = JsonSerializer.Deserialize<AppConfig>(json, JsonOptions) ?? new AppConfig();
            parsed.Clamp();
            return parsed;
        }
        catch
        {
            var config = new AppConfig();
            config.Clamp();
            return config;
        }
    }

    public void Save(AppConfig config)
    {
        config.Clamp();
        var parent = Path.GetDirectoryName(_configPath);
        if (!string.IsNullOrWhiteSpace(parent))
        {
            Directory.CreateDirectory(parent);
        }

        var json = JsonSerializer.Serialize(config, JsonOptions);
        File.WriteAllText(_configPath, json);
    }
}
