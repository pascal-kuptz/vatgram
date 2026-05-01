using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Vatgram.Tray;

public sealed class Settings
{
    public string? TelegramBotToken { get; set; }
    public long? TelegramChatId { get; set; }

    public bool ForwardPrivateMessages { get; set; } = true;
    public bool ForwardRadioMessages { get; set; } = false;
    public bool ForwardBroadcastMessages { get; set; } = true;
    public bool ForwardSelcal { get; set; } = true;
    public bool ForwardConnectionEvents { get; set; } = true;

    public bool QuietHoursEnabled { get; set; } = false;
    /// <summary>Minutes since midnight (0..1410, step 30).</summary>
    public int QuietHoursStart { get; set; } = 22 * 60;
    /// <summary>Minutes since midnight (0..1410, step 30).</summary>
    public int QuietHoursEnd { get; set; } = 7 * 60;
    /// <summary>Schema version for migration. Bump when changing field semantics.</summary>
    public int SchemaVersion { get; set; } = 2;

    public bool StartWithWindows { get; set; } = false;

    public List<string> IgnoredCallsigns { get; set; } = new();

    [JsonIgnore]
    public bool IsConfigured => !string.IsNullOrWhiteSpace(TelegramBotToken) && TelegramChatId.HasValue;

    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public static string AppDataDirectory
    {
        get
        {
            var dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "vatgram");
            Directory.CreateDirectory(dir);
            return dir;
        }
    }

    public static string SettingsPath => Path.Combine(AppDataDirectory, "settings.json");

    public static string LogDirectory
    {
        get
        {
            var dir = Path.Combine(AppDataDirectory, "logs");
            Directory.CreateDirectory(dir);
            return dir;
        }
    }

    public static Settings Load()
    {
        if (!File.Exists(SettingsPath)) return new Settings();
        try
        {
            var json = File.ReadAllText(SettingsPath);
            var s = JsonSerializer.Deserialize<Settings>(json, Options) ?? new Settings();
            // Migrate: schema v1 stored hours (0..23); v2 stores minutes since midnight (0..1410)
            if (s.SchemaVersion < 2)
            {
                s.QuietHoursStart *= 60;
                s.QuietHoursEnd *= 60;
                s.SchemaVersion = 2;
                try { s.Save(); } catch { }
            }
            return s;
        }
        catch
        {
            return new Settings();
        }
    }

    public void Save()
    {
        File.WriteAllText(SettingsPath, JsonSerializer.Serialize(this, Options));
    }
}
