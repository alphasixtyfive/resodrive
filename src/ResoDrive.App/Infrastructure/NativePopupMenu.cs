using System.ComponentModel;
using System.Runtime.InteropServices;

namespace ResoDrive.App;

/// <summary>A system-drawn popup menu used for notification-area commands.</summary>
/// <remarks>The root menu owns all submenu handles and destroys them together.</remarks>
internal sealed partial class NativePopupMenu : IDisposable
{
    private const uint MfString = 0x0000;
    private const uint MfGrayed = 0x0001;
    private const uint MfPopup = 0x0010;
    private const uint MfSeparator = 0x0800;
    private const uint TpmRightButton = 0x0002;
    private const uint TpmReturnCommand = 0x0100;
    private const uint WmNull = 0x0000;

    private readonly CommandRegistry _commands;
    private readonly bool _ownsHandle;
    private IntPtr _handle;

    internal NativePopupMenu()
        : this(CreateMenu(), new CommandRegistry(), true)
    {
    }

    private NativePopupMenu(IntPtr handle, CommandRegistry commands, bool ownsHandle)
    {
        _handle = handle;
        _commands = commands;
        _ownsHandle = ownsHandle;
    }

    internal void Add(string text, Action? action, bool enabled = true)
    {
        ThrowIfDisposed();
        var command = action is null ? 0U : _commands.Add(action);
        Append(MfString | (enabled ? 0U : MfGrayed), (nuint)command, EscapeMnemonics(text));
    }

    internal void AddSeparator()
    {
        ThrowIfDisposed();
        Append(MfSeparator, 0, null);
    }

    internal NativePopupMenu AddSubmenu(string text)
    {
        ThrowIfDisposed();
        var submenu = new NativePopupMenu(CreateMenu(), _commands, false);
        try
        {
            Append(MfPopup, (nuint)submenu._handle, EscapeMnemonics(text));
            return submenu;
        }
        catch
        {
            DestroyMenu(submenu._handle);
            submenu._handle = IntPtr.Zero;
            throw;
        }
    }

    internal void Show(IntPtr owner)
    {
        ThrowIfDisposed();
        if (!GetCursorPos(out var point)) throw new Win32Exception(Marshal.GetLastWin32Error());

        if (owner != IntPtr.Zero) SetForegroundWindow(owner);
        var selected = TrackPopupMenuEx(
            _handle,
            TpmRightButton | TpmReturnCommand,
            point.X,
            point.Y,
            owner,
            IntPtr.Zero);

        // Required for reliable dismissal when a notification icon owns the menu.
        if (owner != IntPtr.Zero) PostMessage(owner, WmNull, IntPtr.Zero, IntPtr.Zero);
        if (selected != 0 && _commands.TryGet(selected, out var action)) action();
    }

    public void Dispose()
    {
        if (_handle == IntPtr.Zero) return;
        if (_ownsHandle) DestroyMenu(_handle);
        _handle = IntPtr.Zero;
    }

    private void Append(uint flags, nuint item, string? text)
    {
        if (!AppendMenu(_handle, flags, item, text))
            throw new Win32Exception(Marshal.GetLastWin32Error());
    }

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(_handle == IntPtr.Zero, this);
    }

    private static IntPtr CreateMenu()
    {
        var handle = CreatePopupMenu();
        return handle != IntPtr.Zero ? handle : throw new Win32Exception(Marshal.GetLastWin32Error());
    }

    private static string EscapeMnemonics(string text) => text.Replace("&", "&&", StringComparison.Ordinal);

    private sealed class CommandRegistry
    {
        private readonly Dictionary<uint, Action> _actions = [];
        private uint _nextId = 1;

        internal uint Add(Action action)
        {
            var id = _nextId++;
            _actions.Add(id, action);
            return id;
        }

        internal bool TryGet(uint id, out Action action) => _actions.TryGetValue(id, out action!);
    }

    [StructLayout(LayoutKind.Sequential)]
    private readonly struct Point
    {
        internal readonly int X;
        internal readonly int Y;
    }

    [LibraryImport("user32.dll", SetLastError = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static partial IntPtr CreatePopupMenu();

    [LibraryImport("user32.dll", SetLastError = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool DestroyMenu(IntPtr menu);

    [LibraryImport(
        "user32.dll",
        EntryPoint = "AppendMenuW",
        SetLastError = true,
        StringMarshalling = StringMarshalling.Utf16)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool AppendMenu(IntPtr menu, uint flags, nuint item, string? text);

    [LibraryImport("user32.dll", SetLastError = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static partial uint TrackPopupMenuEx(
        IntPtr menu, uint flags, int x, int y, IntPtr owner, IntPtr parameters);

    [LibraryImport("user32.dll", SetLastError = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool GetCursorPos(out Point point);

    [LibraryImport("user32.dll")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool SetForegroundWindow(IntPtr window);

    [LibraryImport("user32.dll", EntryPoint = "PostMessageW", SetLastError = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool PostMessage(IntPtr window, uint message, IntPtr wParam, IntPtr lParam);
}
