using System.Text.Json;

namespace CamoSwitcher.Core;

public sealed record SavedConfig(Guid Id, string Name, byte CamoId, string Category, bool FavoritesOnly, DateTimeOffset UpdatedAtUtc)
{
    public SilentAimSettings SilentAim { get; init; } = SilentAimSettings.Default;
}
public sealed record UserPreferences(byte SelectedCamoId, string Category, string Search, bool FavoritesOnly, byte[] FavoriteIds, Guid? LastConfigId = null)
{
    public static UserPreferences Default => new(1, "All camos", "", false, []);
    public SilentAimSettings SilentAim { get; init; } = SilentAimSettings.Default;
}
public sealed record UserData(int FormatVersion, SavedConfig[] Configs, UserPreferences Preferences)
{
    public static UserData Empty => new(1, [], UserPreferences.Default);
}
public sealed record ConfigLoadResult(UserData Data, string? Warning);

public sealed class ConfigStore(string directory)
{
    public static readonly string[] Categories = ["All camos", "Mastery", "Multiplayer", "Zombies", "Pack-a-Punch", "CDL variants"];
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true, PropertyNameCaseInsensitive = true };
    public string FilePath { get; } = Path.Combine(Path.GetFullPath(directory), "configs.json");

    public ConfigLoadResult Load()
    {
        if (!File.Exists(FilePath)) return new(UserData.Empty, null);
        try { return new(Read(FilePath), null); }
        catch (Exception ex) when (ex is JsonException or InvalidDataException)
        {
            var backup = FilePath + $".unreadable-{DateTime.UtcNow:yyyyMMdd-HHmmss}-{Guid.NewGuid():N}.bak";
            File.Move(FilePath, backup, false);
            return new(UserData.Empty, "Unreadable configs were backed up. A fresh config library is ready.");
        }
    }

    public static UserData Read(string path)
    {
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        if (stream.Length > 256 * 1024) throw new InvalidDataException("Config files must be smaller than 256 KB.");
        var data = JsonSerializer.Deserialize<UserData>(stream, JsonOptions)
            ?? throw new InvalidDataException("The config file is empty.");
        Validate(data);
        return data;
    }

    public void Save(UserData data) => Write(FilePath, data);

    public static void Write(string path, UserData data)
    {
        Validate(data);
        var bytes = JsonSerializer.SerializeToUtf8Bytes(data, JsonOptions);
        if (bytes.Length > 256 * 1024) throw new InvalidDataException("The config library is too large.");
        var fullPath = Path.GetFullPath(path);
        var folder = Path.GetDirectoryName(fullPath)!;
        Directory.CreateDirectory(folder);
        var temporaryPath = Path.Combine(folder, $".{Path.GetFileName(fullPath)}.{Guid.NewGuid():N}.tmp");
        try
        {
            using (var stream = new FileStream(temporaryPath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                stream.Write(bytes);
                stream.Flush(true);
            }
            File.Move(temporaryPath, fullPath, true);
        }
        finally { if (File.Exists(temporaryPath)) File.Delete(temporaryPath); }
    }

    public static UserData Upsert(UserData data, string name, byte camoId, string category, bool favoritesOnly, SilentAimSettings? silentAim = null)
    {
        name = name.Trim();
        if (!ValidName(name)) throw new ArgumentException("Give this config a name using 1–48 printable characters.", nameof(name));
        var existing = data.Configs.FirstOrDefault(c => c.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
        var config = new SavedConfig(existing?.Id ?? Guid.NewGuid(), name, camoId, category, favoritesOnly, DateTimeOffset.UtcNow)
        {
            SilentAim = silentAim ?? existing?.SilentAim ?? SilentAimSettings.Default
        };
        var result = data with { Configs = data.Configs.Where(c => c.Id != config.Id).Append(config).OrderBy(c => c.Name, StringComparer.OrdinalIgnoreCase).ToArray() };
        Validate(result);
        return result;
    }

    public static UserData Merge(UserData current, UserData imported)
    {
        Validate(current);
        Validate(imported);
        var result = current;
        foreach (var config in imported.Configs)
            result = Upsert(result, config.Name, config.CamoId, config.Category, config.FavoritesOnly, config.SilentAim);
        return result with { Preferences = result.Preferences with { FavoriteIds = current.Preferences.FavoriteIds.Union(imported.Preferences.FavoriteIds).Order().ToArray() } };
    }

    public static void Validate(UserData data)
    {
        if (data.FormatVersion != 1) throw new InvalidDataException("This config file version is not supported.");
        if (data.Configs is null || data.Configs.Length > 100 || data.Preferences is null)
            throw new InvalidDataException("A config library can contain up to 100 named configs.");
        if (data.Configs.Any(c => c is null || c.Id == Guid.Empty || !ValidName(c.Name) || !ValidId(c.CamoId) || !Categories.Contains(c.Category)) ||
            data.Configs.Select(c => c.Id).Distinct().Count() != data.Configs.Length ||
            data.Configs.Select(c => c.Name).Distinct(StringComparer.OrdinalIgnoreCase).Count() != data.Configs.Length)
            throw new InvalidDataException("Config names, IDs or camo choices are invalid or duplicated.");
        foreach (var config in data.Configs) SilentAimSettings.Validate(config.SilentAim);
        var p = data.Preferences;
        SilentAimSettings.Validate(p.SilentAim);
        if (!ValidId(p.SelectedCamoId) || !Categories.Contains(p.Category) || p.Search is null || p.Search.Length > 100 ||
            p.FavoriteIds is null || p.FavoriteIds.Length > 121 || p.FavoriteIds.Any(id => !ValidId(id)) || p.FavoriteIds.Distinct().Count() != p.FavoriteIds.Length)
            throw new InvalidDataException("Saved menu preferences are invalid.");
    }

    private static bool ValidId(byte id) => id is >= 1 and <= 121;
    private static bool ValidName(string? name) => !string.IsNullOrWhiteSpace(name) && name.Length <= 48 && name == name.Trim() && !name.Any(char.IsControl);
}
