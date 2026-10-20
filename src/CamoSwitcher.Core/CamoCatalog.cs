using System.Collections.ObjectModel;
using System.Text.Json;

namespace CamoSwitcher.Core;

public sealed record Camo(byte Id, string Name, string Category, string SourceKey)
{
    public string Display => $"{Name}  ·  {Id}";
}

public static class CamoCatalog
{
    public static ReadOnlyCollection<Camo> All { get; } = Load();

    private static ReadOnlyCollection<Camo> Load()
    {
        using var stream = typeof(CamoCatalog).Assembly.GetManifestResourceStream("CamoSwitcher.Core.camos.json")
            ?? throw new InvalidDataException("The camo catalog is missing.");
        var camos = JsonSerializer.Deserialize<Camo[]>(stream, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        }) ?? throw new InvalidDataException("The camo catalog is empty.");
        if (camos.Length != 121 || camos.Select(c => c.Id).Distinct().Count() != camos.Length ||
            camos.Any(c => c.Id is < 1 or > 121 || string.IsNullOrWhiteSpace(c.Name)))
            throw new InvalidDataException("The camo catalog is invalid.");
        return Array.AsReadOnly(camos);
    }
}
