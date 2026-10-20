namespace CamoSwitcher.Core;

public sealed class CamoSession : IDisposable
{
    // Historical Cold War Shards option signature; Game Pass compatibility is unverified.
    public const string ShardsSignature = "01 ? ? ? ? ? ? ? 03 ? ? ? ? ? ? ? 0F A3 ? AF";
    private readonly IProcessMemory _memory;
    private readonly byte[] _original;
    private bool _disposed;
    public ulong Address { get; }
    public byte CurrentId { get; private set; }
    public bool IsFaulted { get; private set; }
    public bool IsAlive => !_disposed && _memory.IsAlive;
    public bool IsModified => CurrentId != _original[0];

    private CamoSession(IProcessMemory memory, ulong address, byte[] original)
    {
        _memory = memory;
        Address = address;
        _original = original;
        CurrentId = original[0];
    }

    // Ownership transfers only on success. The caller disposes memory when discovery fails.
    public static CamoSession Connect(IProcessMemory memory, CancellationToken cancellationToken = default,
        IProgress<ScanProgress>? progress = null, int chunkSize = 1024 * 1024)
    {
        var pattern = new BytePattern(ShardsSignature);
        var matches = MemoryScanner.Find(memory, pattern, cancellationToken, progress, chunkSize);
        if (matches.Count == 0)
            throw new InvalidOperationException("Shards option not found. Equip Shards, open the camo menu, and scan again. This build may use a different layout.");
        if (matches.Count != 1)
            throw new InvalidOperationException("Multiple signature matches found. No write is allowed because the camo option cannot be identified uniquely.");
        var original = new byte[pattern.Length];
        memory.Read(matches[0], original);
        if (!pattern.Matches(original)) throw new InvalidOperationException("Game data changed during the scan. Scan again.");
        cancellationToken.ThrowIfCancellationRequested();
        return new CamoSession(memory, matches[0], original);
    }

    public void Apply(byte id)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (IsFaulted) throw new InvalidOperationException("This connection is no longer reliable. Disconnect and restart the game before scanning again.");
        if (!CamoCatalog.All.Any(c => c.Id == id)) throw new ArgumentOutOfRangeException(nameof(id), "Select a camo from the catalog.");
        try
        {
            EnsureAlive();
            var before = new byte[_original.Length];
            _memory.Read(Address, before);
            if (before[0] != CurrentId || !before.AsSpan(1).SequenceEqual(_original.AsSpan(1)))
                throw new InvalidOperationException("The camo data changed. No write was made; restart the game and scan again.");
            if (id == CurrentId) return;
            _memory.WriteByte(Address, id);
            var after = new byte[_original.Length];
            _memory.Read(Address, after);
            if (after[0] != id || !after.AsSpan(1).SequenceEqual(_original.AsSpan(1)))
                throw new IOException("The write could not be verified. Restart the game before reconnecting.");
            CurrentId = id;
        }
        catch
        {
            IsFaulted = true;
            throw;
        }
    }

    public void Restore() => Apply(_original[0]);

    private void EnsureAlive()
    {
        if (!_memory.IsAlive) throw new InvalidOperationException("The selected game process has closed.");
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _memory.Dispose();
    }
}
