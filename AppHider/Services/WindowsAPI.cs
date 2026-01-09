using System.Runtime.InteropServices;
using AppHider.Models;

namespace AppHider.Services;

public static class WindowsAPI
{
    // wtsapi32.dll - Windows Terminal Services API
    [DllImport("wtsapi32.dll", SetLastError = true)]
    public static extern bool WTSEnumerateSessions(
        IntPtr hServer,
        int Reserved,
        int Version,
        ref IntPtr ppSessionInfo,
        ref int pCount);

    [DllImport("wtsapi32.dll", SetLastError = true)]
    public static extern bool WTSQuerySessionInformation(
        IntPtr hServer,
        int sessionId,
        WTSInfoClass wtsInfoClass,
        out IntPtr ppBuffer,
        out int pBytesReturned);

    [DllImport("wtsapi32.dll", SetLastError = true)]
    public static extern bool WTSLogoffSession(
        IntPtr hServer,
        int SessionId,
        bool bWait);

    [DllImport("wtsapi32.dll", SetLastError = true)]
    public static extern bool WTSDisconnectSession(
        IntPtr hServer,
        int sessionId,
        bool bWait);

    [DllImport("wtsapi32.dll")]
    public static extern void WTSFreeMemory(IntPtr pMemory);

    // kernel32.dll - Process and system API
    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern bool TerminateProcess(IntPtr hProcess, uint uExitCode);

    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern IntPtr OpenProcess(
        ProcessAccessFlags processAccess,
        bool bInheritHandle,
        int processId);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool CloseHandle(IntPtr hObject);

    [DllImport("kernel32.dll")]
    public static extern int GetLastError();

    // user32.dll - Window and UI API
    [DllImport("user32.dll", SetLastError = true)]
    public static extern bool GetWindowThreadProcessId(IntPtr hWnd, out int processId);

    [DllImport("user32.dll", SetLastError = true)]
    public static extern IntPtr FindWindow(string? lpClassName, string? lpWindowName);

    // Constants and enums
    public static readonly IntPtr WTS_CURRENT_SERVER_HANDLE = IntPtr.Zero;
    public const int WTS_CURRENT_SESSION = -1;

    public enum WTSInfoClass
    {
        WTSInitialProgram,
        WTSApplicationName,
        WTSWorkingDirectory,
        WTSOEMId,
        WTSSessionId,
        WTSUserName,
        WTSWinStationName,
        WTSDomainName,
        WTSConnectState,
        WTSClientBuildNumber,
        WTSClientName,
        WTSClientDirectory,
        WTSClientProductId,
        WTSClientHardwareId,
        WTSClientAddress,
        WTSClientDisplay,
        WTSClientProtocolType,
        WTSIdleTime,
        WTSLogonTime,
        WTSIncomingBytes,
        WTSOutgoingBytes,
        WTSIncomingFrames,
        WTSOutgoingFrames,
        WTSClientInfo,
        WTSSessionInfo,
        WTSSessionInfoEx,
        WTSConfigInfo,
        WTSValidationInfo,
        WTSSessionAddressV4,
        WTSIsRemoteSession
    }

    [Flags]
    public enum ProcessAccessFlags : uint
    {
        All = 0x001F0FFF,
        Terminate = 0x00000001,
        CreateThread = 0x00000002,
        VirtualMemoryOperation = 0x00000008,
        VirtualMemoryRead = 0x00000010,
        VirtualMemoryWrite = 0x00000020,
        DuplicateHandle = 0x00000040,
        CreateProcess = 0x000000080,
        SetQuota = 0x00000100,
        SetInformation = 0x00000200,
        QueryInformation = 0x00000400,
        QueryLimitedInformation = 0x00001000,
        Synchronize = 0x00100000
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct WTS_SESSION_INFO
    {
        public int SessionId;
        [MarshalAs(UnmanagedType.LPTStr)]
        public string pWinStationName;
        public WTSConnectState State;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct WTS_CLIENT_ADDRESS
    {
        public uint AddressFamily;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 20)]
        public byte[] Address;
    }
}