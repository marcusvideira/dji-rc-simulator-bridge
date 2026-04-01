using System.Text.Json;
using System.Text.Json.Serialization;
using DjiRcSimBridge.Gamepad;

namespace DjiRcSimBridge.Config;

/// <summary>
/// Persistent user configuration stored alongside the executable.
/// </summary>
public sealed class AppConfig
{
    private static readonly string ConfigPath = Path.Combine(
        AppContext.BaseDirectory, "config.json"
    );

    [JsonPropertyName("mode_style")]
    public ModeStyle ModeStyle { get; set; } = ModeStyle.Pulse;

    public static AppConfig Load()
    {
        try
        {
            var json = File.ReadAllText(ConfigPath);
            return JsonSerializer.Deserialize(json, AppConfigContext.Default.AppConfig) ?? new AppConfig();
        }
        catch
        {
            return new AppConfig();
        }
    }

    public void Save()
    {
        var json = JsonSerializer.Serialize(this, AppConfigContext.Default.AppConfig);
        File.WriteAllText(ConfigPath, json);
    }
}

/// <summary>
/// Source-generated JSON serialization context for trim-safe serialization.
/// </summary>
[JsonSourceGenerationOptions(
    WriteIndented = true,
    UseStringEnumConverter = true,
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase
)]
[JsonSerializable(typeof(AppConfig))]
internal sealed partial class AppConfigContext : JsonSerializerContext;
