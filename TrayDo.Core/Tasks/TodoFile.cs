using System.Text.Json;
using System.Text.Json.Serialization;

namespace TrayDo.Tasks;

/// <summary>
/// Reads and writes the task file. Writes go to a temporary file first and then replace the real
/// one, so a crash mid-save never leaves a half-written list behind.
/// </summary>
public static class TodoFile
{
    public static TodoData Load(string path)
    {
        try
        {
            if (!File.Exists(path))
            {
                return new TodoData();
            }

            using FileStream stream = File.OpenRead(path);
            return JsonSerializer.Deserialize(stream, TodoJsonContext.Default.TodoData) ?? new TodoData();
        }
        catch (JsonException)
        {
            // Keep the unreadable file for recovery instead of overwriting it on the next save.
            TryBackUp(path);
            return new TodoData();
        }
    }

    public static void Save(string path, TodoData data)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        string temp = path + ".tmp";
        using (FileStream stream = File.Create(temp))
        {
            JsonSerializer.Serialize(stream, data, TodoJsonContext.Default.TodoData);
        }

        File.Move(temp, path, overwrite: true);
    }

    private static void TryBackUp(string path)
    {
        try
        {
            File.Copy(path, $"{path}.{DateTime.Now:yyyyMMdd-HHmmss}.bad", overwrite: true);
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }
}

[JsonSourceGenerationOptions(WriteIndented = true, PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(TodoData))]
internal sealed partial class TodoJsonContext : JsonSerializerContext;
