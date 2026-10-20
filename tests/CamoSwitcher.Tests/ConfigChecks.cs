using System.Text.Json;
using CamoSwitcher.Core;

internal static class ConfigChecks
{
    internal static IEnumerable<(string Name, Action Run)> All()
    {
        yield return ("Configs survive a fresh store with favorites and menu preferences", () => InFolder(folder =>
        {
            var store = new ConfigStore(Path.Combine(folder, "nested"));
            Check.Equal(0, store.Load().Data.Configs.Length);
            var data = ConfigStore.Upsert(UserData.Empty, "Evening setup", 66, "Mastery", false);
            data = data with { Preferences = new(66, "Mastery", "Dark", false, [63, 66], data.Configs[0].Id) };
            store.Save(data);
            var restored = new ConfigStore(Path.Combine(folder, "nested")).Load().Data;
            Check.Equal("Evening setup", restored.Configs.Single().Name);
            Check.Equal((byte)66, restored.Configs.Single().CamoId);
            Check.Equal(data.Configs[0].Id, restored.Preferences.LastConfigId!.Value);
            Check.True(restored.Preferences.FavoriteIds.SequenceEqual(new byte[] { 63, 66 }));
            Check.Equal("Dark", restored.Preferences.Search);
        }));
        yield return ("Saving the same name updates one config and preserves its identity", () =>
        {
            var data = ConfigStore.Upsert(UserData.Empty, "  Night ops  ", 66, "Mastery", false);
            var id = data.Configs.Single().Id;
            data = ConfigStore.Upsert(data, "NIGHT OPS", 61, "Mastery", false);
            Check.Equal(1, data.Configs.Length);
            Check.Equal(id, data.Configs.Single().Id);
            Check.Equal((byte)61, data.Configs.Single().CamoId);
            Check.Throws<ArgumentException>(() => ConfigStore.Upsert(data, "   ", 66, "All camos", false));
            Check.Throws<ArgumentException>(() => ConfigStore.Upsert(data, "bad\nname", 66, "All camos", false));
            Check.Throws<ArgumentException>(() => ConfigStore.Upsert(data, new string('a', 49), 66, "All camos", false));
        });
        yield return ("Invalid config saves preserve the previous file byte for byte", () => InFolder(folder =>
        {
            var store = new ConfigStore(folder);
            var data = ConfigStore.Upsert(UserData.Empty, "Gold", 61, "Mastery", false);
            store.Save(data);
            var before = File.ReadAllBytes(store.FilePath);
            Check.Throws<InvalidDataException>(() => store.Save(data with { Preferences = data.Preferences with { SelectedCamoId = 0 } }));
            Check.True(before.SequenceEqual(File.ReadAllBytes(store.FilePath)));
            Check.Equal(0, Directory.GetFiles(folder, "*.tmp").Length);
        }));
        yield return ("Corrupt local configs are preserved in a backup and recover cleanly", () => InFolder(folder =>
        {
            var store = new ConfigStore(folder);
            File.WriteAllText(store.FilePath, "{ damaged json");
            var loaded = store.Load();
            Check.True(loaded.Warning is not null);
            Check.Equal(0, loaded.Data.Configs.Length);
            var backups = Directory.GetFiles(folder, "*.bak");
            Check.Equal(1, backups.Length);
            Check.Equal("{ damaged json", File.ReadAllText(backups[0]));
            store.Save(ConfigStore.Upsert(loaded.Data, "Recovered", 63, "Mastery", false));
            Check.Equal("Recovered", store.Load().Data.Configs.Single().Name);
        }));
        yield return ("Import merges named configs and favorites without replacing menu choices", () =>
        {
            var current = ConfigStore.Upsert(UserData.Empty, "Night", 66, "Mastery", false) with { Preferences = new(66, "Mastery", "Dark", false, [66]) };
            var existingId = current.Configs[0].Id;
            var imported = ConfigStore.Upsert(UserData.Empty, "NIGHT", 61, "Mastery", false);
            imported = ConfigStore.Upsert(imported, "PAP", 67, "Pack-a-Punch", false) with { Preferences = new(67, "Pack-a-Punch", "", false, [63, 66]) };
            var merged = ConfigStore.Merge(current, imported);
            Check.Equal(2, merged.Configs.Length);
            Check.Equal(existingId, merged.Configs.Single(c => c.Name == "NIGHT").Id);
            Check.Equal((byte)61, merged.Configs.Single(c => c.Name == "NIGHT").CamoId);
            Check.Equal((byte)66, merged.Preferences.SelectedCamoId);
            Check.Equal("Dark", merged.Preferences.Search);
            Check.True(merged.Preferences.FavoriteIds.SequenceEqual(new byte[] { 63, 66 }));
        });
        yield return ("Unsupported and oversized imports are rejected without altering their source", () => InFolder(folder =>
        {
            var path = Path.Combine(folder, "external.json");
            var future = JsonSerializer.Serialize(UserData.Empty with { FormatVersion = 99 });
            File.WriteAllText(path, future);
            Check.Throws<InvalidDataException>(() => ConfigStore.Read(path));
            Check.Equal(future, File.ReadAllText(path));
            File.WriteAllText(path, new string(' ', 256 * 1024 + 1));
            Check.Throws<InvalidDataException>(() => ConfigStore.Read(path));
            Check.Equal(256 * 1024 + 1, (int)new FileInfo(path).Length);
        }));
        yield return ("Config libraries enforce limits, unique identities, and valid favorites", () =>
        {
            var data = UserData.Empty;
            for (var i = 0; i < 100; i++) data = ConfigStore.Upsert(data, $"Config {i}", 66, "Mastery", false);
            Check.Throws<InvalidDataException>(() => ConfigStore.Upsert(data, "Too many", 66, "Mastery", false));
            var duplicate = UserData.Empty with { Configs = [data.Configs[0], data.Configs[0] with { Name = "Different" }] };
            Check.Throws<InvalidDataException>(() => ConfigStore.Validate(duplicate));
            Check.Throws<InvalidDataException>(() => ConfigStore.Validate(UserData.Empty with { Preferences = UserPreferences.Default with { FavoriteIds = [66, 66] } }));
            Check.Throws<InvalidDataException>(() => ConfigStore.Validate(UserData.Empty with { Preferences = UserPreferences.Default with { Category = "Unknown" } }));
        });
        yield return ("Failed replacement cleans up temporary files and keeps the target intact", () => InFolder(folder =>
        {
            var target = Path.Combine(folder, "configs.json");
            Directory.CreateDirectory(target);
            var sentinel = Path.Combine(target, "keep.txt");
            File.WriteAllText(sentinel, "keep");
            var failed = false;
            try { ConfigStore.Write(target, UserData.Empty); }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { failed = true; }
            Check.True(failed);
            Check.Equal("keep", File.ReadAllText(sentinel));
            Check.Equal(0, Directory.GetFiles(folder, "*.tmp").Length);
        }));
    }

    private static void InFolder(Action<string> action)
    {
        var path = Path.Combine(Path.GetTempPath(), "cw-config-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        try { action(path); }
        finally { Directory.Delete(path, true); }
    }
}
