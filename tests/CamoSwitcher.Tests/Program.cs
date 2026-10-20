using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using CamoSwitcher.Core;

if (args is ["--fixture-child"])
{
    if (!OperatingSystem.IsWindows()) throw new PlatformNotSupportedException();
    WindowsChecks.RunChild();
    return;
}

var tests = new (string Name, Action Run)[]
{
    ("Catalog has 121 unique, ordered IDs and the researched mastery values", () =>
    {
        Check.True(CamoCatalog.All.Select(c => (int)c.Id).SequenceEqual(Enumerable.Range(1, 121)));
        Check.Equal("DM Ultra", CamoCatalog.All.Single(c => c.Id == 63).Name);
        Check.Equal("Dark Aether", CamoCatalog.All.Single(c => c.Id == 66).Name);
        Check.Equal(9, CamoCatalog.All.Count(c => c.Category == "Pack-a-Punch"));
    }),
    ("Pattern search handles overlapping prefixes and trailing wildcards", () =>
    {
        Check.True(new BytePattern("AA AA AB").Find(new byte[] { 0xAA, 0xAA, 0xAA, 0xAB }).SequenceEqual(new[] { 1 }));
        Check.True(new BytePattern("AA ? ?").Find(new byte[] { 0xAA, 1, 2 }).SequenceEqual(new[] { 0 }));
        Check.True(new BytePattern("? AA ?").Find(new byte[] { 0, 0xAA, 2 }).SequenceEqual(new[] { 0 }));
        Check.Equal(0, new BytePattern("AA BB").Find(new byte[] { 0xAA }).Count);
        Check.Throws<ArgumentException>(() => new BytePattern("? ??"));
        Check.Throws<FormatException>(() => new BytePattern("AA ZZ"));
    }),
    ("Signature is found across every possible chunk boundary", () =>
    {
        for (var offset = 0; offset < 64; offset++)
        {
            using var memory = FakeMemory.WithSignature(offset, 160);
            var result = MemoryScanner.Find(memory, new BytePattern(CamoSession.ShardsSignature), chunkSize: 23);
            Check.True(result.SequenceEqual(new[] { FakeMemory.BaseAddress + (ulong)offset }));
        }
    }),
    ("Contiguous regions share a boundary; gaps do not", () =>
    {
        using var memory = FakeMemory.WithSignature(25);
        memory.Ranges = [new(FakeMemory.BaseAddress, 32), new(FakeMemory.BaseAddress + 32, 96)];
        Check.Equal(1, MemoryScanner.Find(memory, new BytePattern(CamoSession.ShardsSignature), chunkSize: 23).Count);
        memory.Ranges = [new(FakeMemory.BaseAddress, 32), new(FakeMemory.BaseAddress + 33, 95)];
        Check.Equal(0, MemoryScanner.Find(memory, new BytePattern(CamoSession.ShardsSignature), chunkSize: 23).Count);
    }),
    ("Two matches reject a connection without any writes", () =>
    {
        using var memory = FakeMemory.WithSignature(2);
        FakeMemory.Signature.CopyTo(memory.Data, 70);
        Check.Throws<InvalidOperationException>(() => CamoSession.Connect(memory, chunkSize: 23));
        Check.Equal(0, memory.WriteCount);
    }),
    ("Missing signatures reject a connection", () =>
    {
        using var memory = new FakeMemory(new byte[128]);
        Check.Throws<InvalidOperationException>(() => CamoSession.Connect(memory));
        Check.Equal(0, memory.WriteCount);
    }),
    ("Incomplete reads fail discovery instead of skipping unknown bytes", () =>
    {
        using var memory = FakeMemory.WithSignature(90);
        memory.ThrowOnRead = 2;
        Check.Throws<IOException>(() => CamoSession.Connect(memory, chunkSize: 23));
        Check.Equal(0, memory.WriteCount);
    }),
    ("Cancellation fails discovery without writes", () =>
    {
        using var memory = FakeMemory.WithSignature(90);
        using var cancellation = new CancellationTokenSource();
        memory.AfterRead = () => cancellation.Cancel();
        Check.Throws<OperationCanceledException>(() => CamoSession.Connect(memory, cancellation.Token, chunkSize: 23));
        Check.Equal(0, memory.WriteCount);
    }),
    ("Process exit is rejected during discovery and before a write", () =>
    {
        using var memory = FakeMemory.WithSignature(4);
        memory.IsAlive = false;
        Check.Throws<InvalidOperationException>(() => CamoSession.Connect(memory));
        memory.IsAlive = true;
        using var session = CamoSession.Connect(memory);
        memory.IsAlive = false;
        Check.Throws<InvalidOperationException>(() => session.Apply(66));
        Check.Equal(0, memory.WriteCount);
    }),
    ("Switching and restoring change exactly the selected byte", () =>
    {
        using var memory = FakeMemory.WithSignature(4);
        var original = memory.Data.ToArray();
        using var session = CamoSession.Connect(memory);
        session.Apply(66);
        Check.Equal((byte)66, memory.Data[4]);
        Check.Equal((byte)66, session.CurrentId);
        Check.True(session.IsModified);
        Check.Equal(1, memory.Data.Zip(original).Count(pair => pair.First != pair.Second));
        session.Apply(66);
        Check.Equal(1, memory.WriteCount);
        session.Restore();
        Check.True(memory.Data.SequenceEqual(original));
        Check.True(!session.IsModified);
    }),
    ("Unknown IDs do not write or invalidate a healthy session", () =>
    {
        using var memory = FakeMemory.WithSignature(4);
        using var session = CamoSession.Connect(memory);
        Check.Throws<ArgumentOutOfRangeException>(() => session.Apply(0));
        Check.Equal(0, memory.WriteCount);
        Check.True(!session.IsFaulted);
    }),
    ("Changed context and external camo changes prevent a write", () =>
    {
        foreach (var index in new[] { 4, 8, 23 })
        {
            using var memory = FakeMemory.WithSignature(4);
            using var session = CamoSession.Connect(memory);
            memory.Data[index] ^= 0x10;
            Check.Throws<InvalidOperationException>(() => session.Apply(66));
            Check.Equal(0, memory.WriteCount);
            Check.True(session.IsFaulted);
        }
    }),
    ("Failed writes and failed read-back never report success", () =>
    {
        foreach (var failure in new[] { "write", "ignored", "verify" })
        {
            using var memory = FakeMemory.WithSignature(4);
            using var session = CamoSession.Connect(memory);
            memory.ThrowOnWrite = failure == "write";
            memory.IgnoreWrites = failure == "ignored";
            if (failure == "verify") memory.ThrowOnRead = memory.ReadCount + 2;
            Check.Throws<IOException>(() => session.Apply(66));
            Check.True(session.IsFaulted);
            Check.Equal((byte)1, session.CurrentId);
            var writes = memory.WriteCount;
            Check.Throws<InvalidOperationException>(() => session.Restore());
            Check.Equal(writes, memory.WriteCount);
        }
    }),
    ("Disposed sessions cannot write", () =>
    {
        using var memory = FakeMemory.WithSignature(4);
        var session = CamoSession.Connect(memory);
        session.Dispose();
        Check.Throws<ObjectDisposedException>(() => session.Apply(66));
        Check.Equal(0, memory.WriteCount);
    })
}.Concat(ConfigChecks.All()).ToArray();

var failed = 0;
foreach (var test in tests)
{
    try { test.Run(); Console.WriteLine($"PASS {test.Name}"); }
    catch (Exception ex) { failed++; Console.Error.WriteLine($"FAIL {test.Name}: {ex}"); }
}
if (OperatingSystem.IsWindows())
{
    try { WindowsChecks.Run(); Console.WriteLine("PASS Native Windows child-process memory, restoration and protection checks"); }
    catch (Exception ex) { failed++; Console.Error.WriteLine($"FAIL Native Windows checks: {ex}"); }
}
else Console.WriteLine("SKIP Native Windows checks (require Windows)");
if (failed != 0) Environment.Exit(1);
Console.WriteLine($"{tests.Length} core checks passed.");

internal static class Check
{
    public static void True(bool value) { if (!value) throw new InvalidOperationException("Assertion failed."); }
    public static void Equal<T>(T expected, T actual) { if (!EqualityComparer<T>.Default.Equals(expected, actual)) throw new InvalidOperationException($"Expected {expected}, got {actual}."); }
    public static void Throws<T>(Action action) where T : Exception
    {
        try { action(); }
        catch (T) { return; }
        throw new InvalidOperationException($"Expected {typeof(T).Name}.");
    }
}

internal sealed class FakeMemory(byte[] data) : IProcessMemory
{
    internal const ulong BaseAddress = 0x10000;
    internal static readonly byte[] Signature = [1, 4, 5, 6, 7, 8, 9, 10, 3, 12, 13, 14, 15, 16, 17, 18, 0x0F, 0xA3, 19, 0xAF];
    public byte[] Data { get; } = data;
    public bool IsAlive { get; set; } = true;
    public MemoryRange[] Ranges { get; set; } = [new(BaseAddress, (ulong)data.Length)];
    public int ReadCount { get; private set; }
    public int WriteCount { get; private set; }
    public int ThrowOnRead { get; set; }
    public bool ThrowOnWrite { get; set; }
    public bool IgnoreWrites { get; set; }
    public Action? AfterRead { get; set; }
    public static FakeMemory WithSignature(int offset, int size = 128)
    {
        var bytes = new byte[size];
        Array.Fill(bytes, (byte)0x55);
        Signature.CopyTo(bytes, offset);
        return new FakeMemory(bytes);
    }
    public IEnumerable<MemoryRange> GetReadableDataRanges() => Ranges;
    public void Read(ulong address, Span<byte> destination)
    {
        ReadCount++;
        if (ReadCount == ThrowOnRead) throw new IOException("Simulated incomplete read.");
        Data.AsSpan(checked((int)(address - BaseAddress)), destination.Length).CopyTo(destination);
        AfterRead?.Invoke();
    }
    public void WriteByte(ulong address, byte value)
    {
        WriteCount++;
        if (ThrowOnWrite) throw new IOException("Simulated failed write.");
        if (!IgnoreWrites) Data[checked((int)(address - BaseAddress))] = value;
    }
    public void Dispose() { }
}

[SupportedOSPlatform("windows")]
internal static class WindowsChecks
{
    public static void Run()
    {
        var start = new ProcessStartInfo(Environment.ProcessPath!)
        {
            UseShellExecute = false, RedirectStandardInput = true,
            RedirectStandardOutput = true, RedirectStandardError = true
        };
        start.ArgumentList.Add("--fixture-child");
        using var child = Process.Start(start) ?? throw new IOException("Could not launch the fixture child.");
        try
        {
            var addressText = child.StandardOutput.ReadLineAsync().WaitAsync(TimeSpan.FromSeconds(15)).GetAwaiter().GetResult();
            if (!ulong.TryParse(addressText, out var address)) throw new IOException("The fixture did not provide its address.");
            using var native = Win32ProcessMemory.Open(child.Id);
            Check.True(native.IsAlive);
            Check.True(native.GetReadableDataRanges().Any());
            var restricted = new RestrictedMemory(native, address);
            using (var session = CamoSession.Connect(restricted, chunkSize: 23))
            {
                session.Apply(66);
                Check.Equal("66", Command(child, "READ"));
                session.Restore();
                Check.Equal("1", Command(child, "READ"));
            }
            Check.Equal("READONLY", Command(child, "READONLY"));
            Check.Throws<IOException>(() => native.WriteByte(address, 63));
            Check.Equal("1", Command(child, "READ"));
            Check.Equal("BYE", Command(child, "EXIT"));
            Check.True(child.WaitForExit(5000));
            Check.True(!native.IsAlive);
        }
        finally
        {
            if (!child.HasExited) { child.Kill(true); child.WaitForExit(5000); }
        }
    }

    private static string? Command(Process child, string command)
    {
        child.StandardInput.WriteLine(command);
        return child.StandardOutput.ReadLineAsync().WaitAsync(TimeSpan.FromSeconds(10)).GetAwaiter().GetResult();
    }

    public static void RunChild()
    {
        var pointer = VirtualAlloc(0, 4096, 0x3000, 0x04);
        if (pointer == 0) throw new IOException("Fixture allocation failed.");
        try
        {
            Marshal.Copy(FakeMemory.Signature, 0, pointer, FakeMemory.Signature.Length);
            Console.WriteLine(((ulong)pointer).ToString(System.Globalization.CultureInfo.InvariantCulture));
            while (Console.ReadLine() is string command)
            {
                if (command == "READ") Console.WriteLine(Marshal.ReadByte(pointer));
                else if (command == "READONLY")
                {
                    if (!VirtualProtect(pointer, 4096, 0x02, out _)) throw new IOException("Fixture protection failed.");
                    Console.WriteLine("READONLY");
                }
                else if (command == "EXIT") { Console.WriteLine("BYE"); return; }
                else throw new InvalidOperationException("Unknown fixture command.");
            }
        }
        finally { VirtualFree(pointer, 0, 0x8000); }
    }

    private sealed class RestrictedMemory(Win32ProcessMemory inner, ulong address) : IProcessMemory
    {
        public bool IsAlive => inner.IsAlive;
        public IEnumerable<MemoryRange> GetReadableDataRanges() => [new(address, 20)];
        public void Read(ulong offset, Span<byte> data) => inner.Read(offset, data);
        public void WriteByte(ulong offset, byte value) => inner.WriteByte(offset, value);
        public void Dispose() { }
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern nint VirtualAlloc(nint address, nuint size, uint allocationType, uint protection);
    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool VirtualProtect(nint address, nuint size, uint protection, out uint oldProtection);
    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool VirtualFree(nint address, nuint size, uint freeType);
}
