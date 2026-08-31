using System.Runtime.InteropServices;
using System.Runtime.InteropServices.Marshalling;

namespace ClaudeSwitch;

/// <summary>
/// Windows-only native + COM interop, using NativeAOT-compatible source-generated
/// COM ([GeneratedComInterface]) and [LibraryImport]. Provides:
///   • AUMID-tagged .lnk creation (so a pinned shortcut and its window collapse
///     into ONE coloured taskbar button per account),
///   • live window tagging (AppUserModel.ID + relaunch + per-account icon + regroup),
///   • a cross-process command-line read (via the PEB) to map a Claude window to
///     the account that launched it — the AOT-safe replacement for WMI.
///
/// Everything here is guarded by the caller (WindowsPlatform); the P/Invokes are
/// pure metadata off-Windows and are never called there.
/// </summary>
static partial class WindowsInterop
{
    // PKEY_AppUserModel_* live under this format id; pid selects which property.
    static readonly Guid AppFmt = new("9F4C2855-9F79-4B39-A8D0-E1D42DE1D5F3");
    const uint PID_ID = 5, PID_RELAUNCH_CMD = 2, PID_RELAUNCH_ICON = 3, PID_RELAUNCH_NAME = 4;

    static readonly Guid CLSID_ShellLink = new("00021401-0000-0000-C000-000000000046");
    static readonly Guid IID_IShellLinkW = new("000214F9-0000-0000-C000-000000000046");
    static readonly Guid IID_IPropertyStore = new("886d8eeb-8cf2-4446-8d02-cdba1dbdcf99");
    const uint CLSCTX_INPROC_SERVER = 1;

    static readonly StrategyBasedComWrappers Com = new();

    // ============ AUMID-tagged shortcut ============
    public static void CreateShortcut(string lnkPath, string target, string? args, string? icon,
                                      string? desc, string? workdir, string? aumid)
    {
        int hr = CoCreateInstance(CLSID_ShellLink, IntPtr.Zero, CLSCTX_INPROC_SERVER, IID_IShellLinkW, out IntPtr ppv);
        if (hr < 0 || ppv == IntPtr.Zero) { Config.Log($"CoCreateInstance(ShellLink) hr={hr:X}"); return; }
        object obj = Com.GetOrCreateObjectForComInstance(ppv, CreateObjectFlags.None);
        Marshal.Release(ppv);

        var link = (IShellLinkW)obj;
        link.SetPath(target);
        if (!string.IsNullOrEmpty(args)) link.SetArguments(args);
        if (!string.IsNullOrEmpty(workdir)) link.SetWorkingDirectory(workdir);
        if (!string.IsNullOrEmpty(icon)) link.SetIconLocation(icon, 0);
        if (!string.IsNullOrEmpty(desc)) link.SetDescription(desc);

        if (!string.IsNullOrEmpty(aumid))
        {
            var store = (IPropertyStore)obj; // ShellLink also implements IPropertyStore
            SetStr(store, PID_ID, aumid);
            store.Commit();
        }
        ((IPersistFile)obj).Save(lnkPath, true);
    }

    // ============ live window tagging ============
    public static string? GetWindowAumid(IntPtr hwnd)
    {
        var store = StoreForWindow(hwnd);
        if (store == null) return null;
        var key = new PROPERTYKEY { fmtid = AppFmt, pid = PID_ID };
        if (store.GetValue(key, out PROPVARIANT pv) != 0) return null;
        try { return (pv.p != IntPtr.Zero && (pv.vt == 31 || pv.vt == 8)) ? Marshal.PtrToStringUni(pv.p) : null; }
        finally { PropVariantClear(ref pv); }
    }

    public static void TagWindow(IntPtr hwnd, string aumid, string relaunch, string iconRes, string display)
    {
        var store = StoreForWindow(hwnd);
        if (store == null) return;
        SetStr(store, PID_ID, aumid);
        SetStr(store, PID_RELAUNCH_CMD, relaunch);
        SetStr(store, PID_RELAUNCH_ICON, iconRes);
        SetStr(store, PID_RELAUNCH_NAME, display);
        store.Commit();
    }

    public static void SetWindowIcon(IntPtr hwnd, string icoPath)
    {
        const uint IMAGE_ICON = 1, LR_LOADFROMFILE = 0x10, WM_SETICON = 0x80;
        IntPtr big = LoadImageW(IntPtr.Zero, icoPath, IMAGE_ICON, 32, 32, LR_LOADFROMFILE);
        IntPtr small = LoadImageW(IntPtr.Zero, icoPath, IMAGE_ICON, 16, 16, LR_LOADFROMFILE);
        if (big != IntPtr.Zero) SendMessageW(hwnd, WM_SETICON, (IntPtr)1, big);
        if (small != IntPtr.Zero) SendMessageW(hwnd, WM_SETICON, (IntPtr)0, small);
    }

    public static void Regroup(IntPtr hwnd)
    {
        const int SW_HIDE = 0, SW_MAX = 3, SW_NORM = 1;
        bool max = IsZoomed(hwnd);
        ShowWindow(hwnd, SW_HIDE);
        Thread.Sleep(80);
        ShowWindow(hwnd, max ? SW_MAX : SW_NORM);
    }

    static IPropertyStore? StoreForWindow(IntPtr hwnd)
    {
        if (SHGetPropertyStoreForWindow(hwnd, IID_IPropertyStore, out IntPtr ppv) != 0 || ppv == IntPtr.Zero)
            return null;
        object obj = Com.GetOrCreateObjectForComInstance(ppv, CreateObjectFlags.None);
        Marshal.Release(ppv);
        return (IPropertyStore)obj;
    }

    static void SetStr(IPropertyStore store, uint pid, string value)
    {
        if (string.IsNullOrEmpty(value)) return;
        var key = new PROPERTYKEY { fmtid = AppFmt, pid = pid };
        var pv = new PROPVARIANT { vt = 31 /*VT_LPWSTR*/, p = Marshal.StringToCoTaskMemUni(value) };
        try { store.SetValue(key, pv); } finally { PropVariantClear(ref pv); }
    }

    // ============ cross-process command line (PEB read; 64-bit) ============
    /// <summary>Full command line of another process, or null. AOT-safe (no WMI).</summary>
    public static string? GetCommandLine(int pid)
    {
        const uint PROCESS_QUERY_INFORMATION = 0x0400, PROCESS_VM_READ = 0x0010;
        IntPtr h = OpenProcess(PROCESS_QUERY_INFORMATION | PROCESS_VM_READ, false, (uint)pid);
        if (h == IntPtr.Zero) return null;
        try
        {
            // PROCESS_BASIC_INFORMATION is 6 pointers on x64; PebBaseAddress is the 2nd.
            IntPtr pbi = Marshal.AllocHGlobal(IntPtr.Size * 6);
            try
            {
                if (NtQueryInformationProcess(h, 0, pbi, IntPtr.Size * 6, out _) != 0) return null;
                IntPtr peb = Marshal.ReadIntPtr(pbi, IntPtr.Size); // offset of PebBaseAddress
                if (peb == IntPtr.Zero) return null;

                // PEB.ProcessParameters @ +0x20 (x64)
                if (!ReadPtr(h, peb + 0x20, out IntPtr procParams) || procParams == IntPtr.Zero) return null;
                // RTL_USER_PROCESS_PARAMETERS.CommandLine (UNICODE_STRING) @ +0x70 (x64)
                if (!ReadU16(h, procParams + 0x70, out ushort len) || len == 0) return null;
                if (!ReadPtr(h, procParams + 0x70 + 8, out IntPtr buffer) || buffer == IntPtr.Zero) return null;
                return ReadUnicode(h, buffer, len);
            }
            finally { Marshal.FreeHGlobal(pbi); }
        }
        catch (Exception ex) { Config.Log($"GetCommandLine({pid}) err: {ex.Message}"); return null; }
        finally { CloseHandle(h); }
    }

    static bool ReadPtr(IntPtr h, IntPtr addr, out IntPtr val)
    {
        IntPtr buf = Marshal.AllocHGlobal(IntPtr.Size);
        try
        {
            if (!ReadProcessMemory(h, addr, buf, (IntPtr)IntPtr.Size, out _)) { val = IntPtr.Zero; return false; }
            val = Marshal.ReadIntPtr(buf); return true;
        }
        finally { Marshal.FreeHGlobal(buf); }
    }

    static bool ReadU16(IntPtr h, IntPtr addr, out ushort val)
    {
        IntPtr buf = Marshal.AllocHGlobal(2);
        try
        {
            if (!ReadProcessMemory(h, addr, buf, (IntPtr)2, out _)) { val = 0; return false; }
            val = (ushort)Marshal.ReadInt16(buf); return true;
        }
        finally { Marshal.FreeHGlobal(buf); }
    }

    static string? ReadUnicode(IntPtr h, IntPtr addr, int bytes)
    {
        IntPtr buf = Marshal.AllocHGlobal(bytes);
        try
        {
            if (!ReadProcessMemory(h, addr, buf, (IntPtr)bytes, out _)) return null;
            return Marshal.PtrToStringUni(buf, bytes / 2);
        }
        finally { Marshal.FreeHGlobal(buf); }
    }

    // ============ P/Invoke ============
    [LibraryImport("ole32.dll")]
    private static partial int CoCreateInstance(in Guid clsid, IntPtr outer, uint ctx, in Guid iid, out IntPtr ppv);
    [LibraryImport("ole32.dll")]
    private static partial int PropVariantClear(ref PROPVARIANT pv);
    [LibraryImport("shell32.dll")]
    private static partial int SHGetPropertyStoreForWindow(IntPtr hwnd, in Guid iid, out IntPtr ppv);
    [LibraryImport("user32.dll", StringMarshalling = StringMarshalling.Utf16)]
    private static partial IntPtr LoadImageW(IntPtr hinst, string name, uint type, int cx, int cy, uint flags);
    [LibraryImport("user32.dll")]
    private static partial IntPtr SendMessageW(IntPtr hwnd, uint msg, IntPtr wParam, IntPtr lParam);
    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool ShowWindow(IntPtr hwnd, int cmd);
    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool IsZoomed(IntPtr hwnd);
    [LibraryImport("kernel32.dll", SetLastError = true)]
    private static partial IntPtr OpenProcess(uint access, [MarshalAs(UnmanagedType.Bool)] bool inherit, uint pid);
    [LibraryImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool CloseHandle(IntPtr h);
    [LibraryImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool ReadProcessMemory(IntPtr h, IntPtr baseAddr, IntPtr buffer, IntPtr size, out IntPtr read);
    [LibraryImport("ntdll.dll")]
    private static partial int NtQueryInformationProcess(IntPtr h, int infoClass, IntPtr buffer, int len, out int retLen);
}

// ---- blittable interop structs ----
[StructLayout(LayoutKind.Sequential)]
struct PROPERTYKEY { public Guid fmtid; public uint pid; }

[StructLayout(LayoutKind.Sequential)]
struct PROPVARIANT { public ushort vt; public ushort r1, r2, r3; public IntPtr p; public IntPtr p2; }

// ---- source-generated COM interfaces (vtable order matters; unused slots are
//      declared as no-arg placeholders purely to keep the layout correct) ----
[GeneratedComInterface, Guid("000214F9-0000-0000-C000-000000000046")]
internal partial interface IShellLinkW
{
    void GetPath();
    void GetIDList();
    void SetIDList();
    void GetDescription();
    void SetDescription([MarshalAs(UnmanagedType.LPWStr)] string name);
    void GetWorkingDirectory();
    void SetWorkingDirectory([MarshalAs(UnmanagedType.LPWStr)] string dir);
    void GetArguments();
    void SetArguments([MarshalAs(UnmanagedType.LPWStr)] string args);
    void GetHotkey();
    void SetHotkey();
    void GetShowCmd();
    void SetShowCmd();
    void GetIconLocation();
    void SetIconLocation([MarshalAs(UnmanagedType.LPWStr)] string path, int index);
    void SetRelativePath();
    void Resolve();
    void SetPath([MarshalAs(UnmanagedType.LPWStr)] string file);
}

[GeneratedComInterface, Guid("0000010b-0000-0000-C000-000000000046")]
internal partial interface IPersistFile
{
    void GetClassID();
    void IsDirty();
    void Load([MarshalAs(UnmanagedType.LPWStr)] string file, int mode);
    void Save([MarshalAs(UnmanagedType.LPWStr)] string file, [MarshalAs(UnmanagedType.Bool)] bool remember);
    void SaveCompleted([MarshalAs(UnmanagedType.LPWStr)] string file);
    void GetCurFile();
}

[GeneratedComInterface, Guid("886d8eeb-8cf2-4446-8d02-cdba1dbdcf99")]
internal partial interface IPropertyStore
{
    [PreserveSig] int GetCount(out uint count);
    [PreserveSig] int GetAt(uint index, out PROPERTYKEY key);
    [PreserveSig] int GetValue(in PROPERTYKEY key, out PROPVARIANT pv);
    [PreserveSig] int SetValue(in PROPERTYKEY key, in PROPVARIANT pv);
    [PreserveSig] int Commit();
}
