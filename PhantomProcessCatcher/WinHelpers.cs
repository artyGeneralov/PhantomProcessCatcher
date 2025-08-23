
using NtApiDotNet;
using System;
using System.Runtime.InteropServices;

namespace PhantomProcessCatcher
{
    internal static class WinHelpers
    {
        [Flags] public enum ProcessAccess: uint { PROCESS_DUP_HANDLE = 0x0040, PROCESS_QUERY_INFORMATION = 0x0400 }
        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern IntPtr OpenProcess(ProcessAccess access, bool inheritHandle, int pid);

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern bool CloseHandle(IntPtr handleObject);
        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern bool DuplicateHandle(IntPtr sourceProcessHandle, IntPtr SourceHandle, IntPtr targetProcessHandle, out IntPtr duplicateHandle,
                                                  ProcessAccess access, bool inheritHandle, int options);
        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern IntPtr GetCurrentProcess();
        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern uint GetFinalPathNameByHandleA(IntPtr handle, out string filePath, uint len, uint flags);

        [DllImport("advapi32.dll", SetLastError = true)]
        public static extern bool OpenProcessToken(IntPtr processHandle, ProcessAccess access, out IntPtr tokenHandle);
        [DllImport("advapi32.dll", SetLastError = true)]
        public static extern bool LookupPrivilegeValueW(string systemName, string lpName, out LUID luid);
        [DllImport("advapi32.dll", SetLastError = true)]
        public static extern bool AdjustTokenPrivileges(IntPtr handle, bool disableAllPrivileges, ref TOKEN_PRIVILEGES newState, int bufferLength, IntPtr prev, IntPtr retLen);
        const uint TOKEN_ADJUST_PRIVILEGES = 0X20, TOKEN_QUERY = 0X8, SE_PRIVILEGES_ENABLED=0X2;
        [StructLayout(LayoutKind.Sequential)] public struct LUID { public uint LowPart; public int HighPart; }
        [StructLayout(LayoutKind.Sequential)] public struct LUID_AND_ATTRIBUTES { public LUID Luid; public uint Attributes; }
        [StructLayout(LayoutKind.Sequential)] public struct TOKEN_PRIVILEGES { public uint PrivilegeCount; public LUID_AND_ATTRIBUTES Privileges; }


    }
}
