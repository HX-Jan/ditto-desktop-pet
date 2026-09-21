using System.Text.Json;

namespace Ditto.Core;

public sealed record Settings(int Version = 2, int Size = 128, double X = 160, ActivityMode Activity = ActivityMode.Lively)
{
    public static Settings Load(string path)
    {
        try
        {
            var value = JsonSerializer.Deserialize<Settings>(File.ReadAllText(path));
            if(value is not { Version: 1 or 2, Size: 96 or 128 or 160 } || !double.IsFinite(value.X)) return new();
            return value with { Version=2, Activity=value.Version==1 || !Enum.IsDefined(value.Activity)?ActivityMode.Lively:value.Activity };
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
