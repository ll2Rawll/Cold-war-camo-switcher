namespace CamoSwitcher.Core;

public readonly record struct MemoryRange(ulong Address, ulong Length);
public readonly record struct ScanProgress(ulong BytesRead, int RangesRead);

public interface IProcessMemory : IDisposable
{
    bool IsAlive { get; }
    IEnumerable<MemoryRange> GetReadableDataRanges();
    void Read(ulong address, Span<byte> destination);
    void WriteByte(ulong address, byte value);
}

public static class MemoryScanner
{
    public static IReadOnlyList<ulong> Find(IProcessMemory memory, BytePattern pattern,
        CancellationToken cancellationToken = default, IProgress<ScanProgress>? progress = null,
        int chunkSize = 1024 * 1024)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(chunkSize, pattern.Length);
        var buffer = new byte[checked(chunkSize + pattern.Length - 1)];
        var matches = new List<ulong>();
        var carry = 0;
        ulong previousEnd = 0, bytesRead = 0;
        var rangesRead = 0;
        foreach (var range in memory.GetReadableDataRanges())
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (range.Length == 0) continue;
            var end = checked(range.Address + range.Length);
            if (range.Address < previousEnd) throw new InvalidDataException("Memory ranges overlap or are unordered.");
            if (range.Address != previousEnd) carry = 0;
            var address = range.Address;
            while (address < end)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (!memory.IsAlive) throw new InvalidOperationException("The selected game process has closed.");
                var count = (int)Math.Min((ulong)chunkSize, end - address);
                // Any incomplete read fails the scan: uniqueness cannot be established if bytes are skipped.
                memory.Read(address, buffer.AsSpan(carry, count));
                var total = carry + count;
                foreach (var offset in pattern.Find(buffer.AsSpan(0, total)))
                {
                    matches.Add(checked(address - (ulong)carry + (ulong)offset));
                    if (matches.Count > 1) return matches;
                }
                carry = Math.Min(pattern.Length - 1, total);
                buffer.AsSpan(total - carry, carry).CopyTo(buffer);
                address += (ulong)count;
                bytesRead += (ulong)count;
                progress?.Report(new ScanProgress(bytesRead, rangesRead));
            }
            previousEnd = end;
            rangesRead++;
        }
        cancellationToken.ThrowIfCancellationRequested();
        progress?.Report(new ScanProgress(bytesRead, rangesRead));
        return matches;
    }
}
