using System.IO;
using System.Text.Json;
using VMAzureApp.Models;

namespace VMAzureApp.Services;

public sealed class VmScheduleStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    private readonly string _filePath;

    public VmScheduleStore()
    {
        string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        string directory = Path.Combine(appData, "AzureVMManager");
        Directory.CreateDirectory(directory);
        _filePath = Path.Combine(directory, "schedules.json");
    }

    public IReadOnlyList<VmSchedule> Load()
    {
        if (!File.Exists(_filePath))
        {
            return [];
        }

        using FileStream stream = File.OpenRead(_filePath);
        return JsonSerializer.Deserialize<List<VmSchedule>>(stream, JsonOptions) ?? [];
    }

    public void Save(IEnumerable<VmSchedule> schedules)
    {
        string tempPath = $"{_filePath}.tmp";
        using (FileStream stream = File.Create(tempPath))
        {
            JsonSerializer.Serialize(stream, schedules, JsonOptions);
        }

        File.Move(tempPath, _filePath, overwrite: true);
    }
}
