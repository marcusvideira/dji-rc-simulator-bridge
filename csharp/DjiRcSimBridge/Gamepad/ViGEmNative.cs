using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace DjiRcSimBridge.Gamepad;

/// <summary>
/// P/Invoke bindings to the native ViGEmClient.dll C API.
/// Uses the same DLL that vgamepad (Python) bundles, ensuring compatibility
/// with the installed ViGEm Bus Driver.
///
/// IMPORTANT: The ViGEm C API uses cdecl calling convention (C default).
/// Every P/Invoke must specify CallConvCdecl to match, otherwise stack
/// corruption causes the device to appear created but not functional.
/// </summary>
public static partial class ViGEmNative
{
    private const string DllName = "ViGEmClient";

    /// <summary>
    /// Register a resolver so the runtime finds ViGEmClient.dll next to the
    /// executable regardless of the process working directory.
    /// Must be called before any P/Invoke into ViGEmClient.
    /// </summary>
    public static void EnsureLoaded()
    {
        NativeLibrary.SetDllImportResolver(
            Assembly.GetExecutingAssembly(),
            (name, assembly, searchPath) =>
            {
                if (name != DllName)
                    return IntPtr.Zero;

                // Try default search first
                if (NativeLibrary.TryLoad(name, assembly, searchPath, out var handle))
                    return handle;

                // Fall back to the directory containing the executable
                var exeDir = AppContext.BaseDirectory;
                var fullPath = Path.Combine(exeDir, "ViGEmClient.dll");
                if (NativeLibrary.TryLoad(fullPath, out handle))
                    return handle;

                return IntPtr.Zero;
            }
        );
    }

    // ── Error codes ──

    internal enum ViGEmError : uint
    {
        None = 0x20000000,
        BusNotFound = 0xE0000001,
        NoFreeSlot = 0xE0000002,
        InvalidTarget = 0xE0000003,
        RemovalFailed = 0xE0000004,
        AlreadyConnected = 0xE0000005,
        TargetUninitialized = 0xE0000006,
        TargetNotPluggedIn = 0xE0000007,
        BusVersionMismatch = 0xE0000008,
        BusAccessFailed = 0xE0000009,
    }

    // ── XUSB report (matches XINPUT_GAMEPAD) ──

    [StructLayout(LayoutKind.Sequential)]
    internal struct XusbReport
    {
        public ushort Buttons;
        public byte LeftTrigger;
        public byte RightTrigger;
        public short ThumbLX;
        public short ThumbLY;
        public short ThumbRX;
        public short ThumbRY;
    }

    // ── XUSB button flags ──

    [Flags]
    internal enum XusbButton : ushort
    {
        DpadUp = 0x0001,
        DpadDown = 0x0002,
        DpadLeft = 0x0004,
        DpadRight = 0x0008,
        Start = 0x0010,
        Back = 0x0020,
        LeftThumb = 0x0040,
        RightThumb = 0x0080,
        LeftShoulder = 0x0100,
        RightShoulder = 0x0200,
        Guide = 0x0400,
        A = 0x1000,
        B = 0x2000,
        X = 0x4000,
        Y = 0x8000,
    }

    // ── Client lifecycle ──

    [LibraryImport(DllName, EntryPoint = "vigem_alloc")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    internal static partial IntPtr Alloc();

    [LibraryImport(DllName, EntryPoint = "vigem_free")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    internal static partial void Free(IntPtr client);

    [LibraryImport(DllName, EntryPoint = "vigem_connect")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    internal static partial ViGEmError Connect(IntPtr client);

    [LibraryImport(DllName, EntryPoint = "vigem_disconnect")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    internal static partial void Disconnect(IntPtr client);

    // ── Target lifecycle ──

    [LibraryImport(DllName, EntryPoint = "vigem_target_x360_alloc")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    internal static partial IntPtr TargetX360Alloc();

    [LibraryImport(DllName, EntryPoint = "vigem_target_free")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    internal static partial void TargetFree(IntPtr target);

    [LibraryImport(DllName, EntryPoint = "vigem_target_add")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    internal static partial ViGEmError TargetAdd(IntPtr client, IntPtr target);

    [LibraryImport(DllName, EntryPoint = "vigem_target_remove")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    internal static partial ViGEmError TargetRemove(IntPtr client, IntPtr target);

    // ── Report submission ──

    [LibraryImport(DllName, EntryPoint = "vigem_target_x360_update")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    internal static partial ViGEmError TargetX360Update(IntPtr client, IntPtr target, XusbReport report);

    // ── Helpers ──

    internal static void CheckError(ViGEmError error, string operation)
    {
        if (error != ViGEmError.None)
            throw new InvalidOperationException($"ViGEm {operation} failed: {error}");
    }
}
