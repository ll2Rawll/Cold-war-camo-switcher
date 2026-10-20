using System.Globalization;

namespace CamoSwitcher.Core;

public sealed class BytePattern
{
    private readonly byte?[] _bytes;
    private readonly int _anchor;
    public int Length => _bytes.Length;

    public BytePattern(string expression)
    {
        var tokens = expression.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (tokens.Length is < 2 or > 256)
            throw new ArgumentException("A pattern must have between 2 and 256 bytes.", nameof(expression));
        _bytes = tokens.Select(t => t is "?" or "??" ? (byte?)null :
            t.Length == 2 ? byte.Parse(t, NumberStyles.HexNumber, CultureInfo.InvariantCulture) :
            throw new FormatException("Use two hex digits or a wildcard per byte.")).ToArray();
        _anchor = Array.FindIndex(_bytes, b => b.HasValue);
        if (_anchor < 0) throw new ArgumentException("A pattern needs a fixed byte.", nameof(expression));
    }

    public bool Matches(ReadOnlySpan<byte> data)
    {
        if (data.Length < Length) return false;
        for (var i = 0; i < Length; i++)
            if (_bytes[i] is byte value && data[i] != value) return false;
        return true;
    }

    public IReadOnlyList<int> Find(ReadOnlySpan<byte> data, int limit = 2)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(limit, 1);
        var matches = new List<int>();
        var start = 0;
        while (start <= data.Length - Length)
        {
            var index = data.Slice(start + _anchor, data.Length - Length - start + 1)
                .IndexOf(_bytes[_anchor]!.Value);
            if (index < 0) break;
            start += index;
            if (Matches(data[start..]))
            {
                matches.Add(start);
                if (matches.Count >= limit) break;
            }
            start++;
        }
        return matches;
    }
}
