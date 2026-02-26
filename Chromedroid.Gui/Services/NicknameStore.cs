using System.IO;
using System.Text.Json;

namespace Chromedroid.Gui;

public sealed class NicknameStore
{
    private static readonly string FilePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "Chromedroid",
        "device-nicknames.json");

    private Dictionary<string, string> _nicknames = new();

    public NicknameStore()
    {
        Load();
    }

    public string? GetNickname(string serial)
    {
        return _nicknames.TryGetValue(serial, out var nickname) ? nickname : null;
    }

    public void SetNickname(string serial, string? nickname)
    {
        if (string.IsNullOrWhiteSpace(nickname))
            _nicknames.Remove(serial);
        else
            _nicknames[serial] = nickname;

        Save();
    }

    private void Load()
    {
        try
        {
            if (File.Exists(FilePath))
            {
                var json = File.ReadAllText(FilePath);
                _nicknames = JsonSerializer.Deserialize<Dictionary<string, string>>(json) ?? new();
            }
        }
        catch
        {
            _nicknames = new();
        }
    }

    private void Save()
    {
        try
        {
            var dir = Path.GetDirectoryName(FilePath)!;
            Directory.CreateDirectory(dir);
            var json = JsonSerializer.Serialize(_nicknames, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(FilePath, json);
        }
        catch
        {
            // Silently fail — nickname persistence is best-effort
        }
    }
}
