using System;
using System.IO;
using System.Text.Json;

namespace RoomBookingClient;

public sealed class SettingsStore
{
    private readonly JsonSerializerOptions _jsonOptions = new() { WriteIndented = true };

    private static string ConfigPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "InterlinkRoomBookingClient",
        "settings.json");

    public ClientSettings Load()
    {
        try
        {
            if (!File.Exists(ConfigPath))
            {
                return ClientSettings.Default();
            }

            var json = File.ReadAllText(ConfigPath);
            return JsonSerializer.Deserialize<ClientSettings>(json) ?? ClientSettings.Default();
        }
        catch
        {
            return ClientSettings.Default();
        }
    }

    public void Save(ClientSettings settings)
    {
        var directory = Path.GetDirectoryName(ConfigPath)!;
        Directory.CreateDirectory(directory);

        var json = JsonSerializer.Serialize(settings, _jsonOptions);
        File.WriteAllText(ConfigPath, json);
    }
}
