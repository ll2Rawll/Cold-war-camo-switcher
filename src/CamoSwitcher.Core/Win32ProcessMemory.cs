using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using Microsoft.Win32.SafeHandles;

namespace CamoSwitcher.Core;

[SupportedOSPlatform("windows")]
public sealed class Win32ProcessMemory : IProcessMemory
{
    private readonly SafeProcessHandle _handle;
    private readonly MemoryRange _module;
    private bool _disposed;
    public int ProcessId { get; }
    public string ProcessName { get; }
    public string? FileVersion { get; }
    public ulong ModuleSize => _module.Length;
    public bool IsAlive => !_disposed && Native.WaitForSingleObject(_handle, 0) == 0x102;

    private Win32ProcessMemory(SafeProcessHandle handle, Process process)
    {
        _handle = handle;
        ProcessId = process.Id;
        ProcessName = process.ProcessName;
        var module = process.MainModule ?? throw new InvalidOperationException("The game executable module is unavailable.");
        _module = new MemoryRange((ulong)module.BaseAddress, (ulong)module.ModuleMemorySize);
        FileVersion = module.FileVersionInfo.FileVersion;
        if (_module.Length == 0 || _module.Length > int.MaxValue)
            throw new InvalidOperationException("The selected executable has an unsupported module size.");
    }

    public static Win32ProcessMemory Open(int processId)
    {
        if (!Environment.Is64BitProcess) throw new PlatformNotSupportedException("Run the x64 build of this app.");
        // Query, read, write, VM operation, and synchronize. No injection or protection changes.
        var handle = Native.OpenProcess(0x00000438 | 0x00100000, false, processId);
        if (handle.IsInvalid)
        {
            var error = Marshal.GetLastWin32Error();
            handle.Dispose();
            throw Failure("Cannot access the selected process", error);
        }
        try
        {
            if (!Native.IsWow64Process2(handle, out var processMachine, out var nativeMachine))
                throw Failure("Cannot determine process architecture", Marshal.GetLastWin32Error());
            if (processMachine != 0 || nativeMachine != 0x8664)
                throw new PlatformNotSupportedException("Select the native x64 Cold War game process.");
            using var process = Process.GetProcessById(processId);
            var memory = new Win32ProcessMemory(handle, process);
            if (!memory.IsAlive) throw new InvalidOperationException("The selected process has already closed.");
            return memory;
        }
        catch
        {
            handle.Dispose();
            throw;
        }
    }

    public IEnumerable<MemoryRange> GetReadableDataRanges()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        var end = checked(_module.Address + _module.Length);
        var address = _module.Address;
        while (address < end)
        {
            if (!IsAlive) throw new InvalidOperationException("The selected process has closed.");
            var info = Query(address);
            var regionEnd = checked(info.BaseAddress + info.RegionSize);
            if (regionEnd <= address) throw new IOException("Windows returned an invalid memory region.");
            var start = Math.Max(address, info.BaseAddress);
            var stop = Math.Min(end, regionEnd);
            var protection = info.Protect & 0xFF;
            if (info.State == 0x1000 && (info.Protect & 0x100) == 0 &&
                protection is 0x02 or 0x04 or 0x08 &&
                info.Type is 0x20000 or 0x1000000 && stop > start)
                yield return new MemoryRange(start, stop - start);
            address = regionEnd;
        }
    }

    public unsafe void Read(ulong address, Span<byte> destination)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (destination.IsEmpty) return;
        fixed (byte* buffer = destination)
        {
            var success = Native.ReadProcessMemory(_handle, (nint)address, (nint)buffer,
                (nuint)destination.Length, out var read);
            if (!success || read != (nuint)destination.Length)
                throw Failure("Cannot read complete game data; the scan or connection must be retried", Marshal.GetLastWin32Error());
        }
    }

    public unsafe void WriteByte(ulong address, byte value)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (!IsAlive) throw new InvalidOperationException("The selected process has closed.");
        var region = Query(address);
        if (region.State != 0x1000 || (region.Protect & 0x100) != 0 || (region.Protect & 0xFF) is not (0x04 or 0x08))
            throw new IOException("The camo option is not in writable data memory. This layout is unsupported; no protection was changed.");
        if (!Native.WriteProcessMemory(_handle, (nint)address, (nint)(&value), 1, out var written) || written != 1)
            throw Failure("Cannot write the camo option", Marshal.GetLastWin32Error());
    }

    private Native.MemoryBasicInformation Query(ulong address)
    {
        if (Native.VirtualQueryEx(_handle, (nint)address, out var info,
            (nuint)Marshal.SizeOf<Native.MemoryBasicInformation>()) == 0)
            throw Failure("Cannot inspect game memory regions", Marshal.GetLastWin32Error());
        return info;
    }

    private static Win32Exception Failure(string operation, int code) =>
        new(code, $"{operation}: {new Win32Exception(code).Message} (Windows error {code}).");

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _handle.Dispose();
    }

    private static class Native
    {
        [StructLayout(LayoutKind.Sequential)]
        internal struct MemoryBasicInformation
        {
            internal ulong BaseAddress;
            internal ulong AllocationBase;
            internal uint AllocationProtect;
            internal ulong RegionSize;
            internal uint State;
            internal uint Protect;
            internal uint Type;
        }

        [DllImport("kernel32.dll", SetLastError = true)]
        internal static extern SafeProcessHandle OpenProcess(uint access, [MarshalAs(UnmanagedType.Bool)] bool inheritHandle, int processId);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool IsWow64Process2(SafeProcessHandle process, out ushort processMachine, out ushort nativeMachine);

        [DllImport("kernel32.dll", SetLastError = true)]
        internal static extern nuint VirtualQueryEx(SafeProcessHandle process, nint address, out MemoryBasicInformation info, nuint size);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool ReadProcessMemory(SafeProcessHandle process, nint address, nint buffer, nuint size, out nuint bytesRead);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool WriteProcessMemory(SafeProcessHandle process, nint address, nint buffer, nuint size, out nuint bytesWritten);

        [DllImport("kernel32.dll", SetLastError = true)]
        internal static extern uint WaitForSingleObject(SafeProcessHandle process, uint milliseconds);
    }
}
