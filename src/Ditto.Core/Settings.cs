using System.Text.Json;

namespace Ditto.Core;

public sealed record Settings(int Version = 1, int Size = 128, double X = 160)
{
    public static Settings Load(string path)
    {
        try
        {
            var value = JsonSerializer.Deserialize<Settings>(File.ReadAllText(path));
            return value is { Version: 1, Size: 96 or 128 or 160 } && double.IsFinite(value.X)
                ? value : new();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        { return new(); }
    }
    public bool Save(string path)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
            var temp = path + ".tmp";
            File.WriteAllText(temp, JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true }));
            File.Move(temp, path, true);
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { return false; }
    }
}
