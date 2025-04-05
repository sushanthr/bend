using System;
using System.ComponentModel;
using System.Runtime.InteropServices;
using static Console.Internals.Process;

namespace Console.Internals
{
    public interface IProcess : IDisposable
    {
        void WaitForExit();
        bool HasExited { get; }
        void Kill();
    }

    public interface IProcessFactory
    {
        IProcess Start(string command, UIntPtr attributes, PseudoConsole console);
    }

    public static class ProcessFactory
    {
        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool InitializeProcThreadAttributeList(
            IntPtr lpAttributeList,
            int dwAttributeCount,
            int dwFlags,
            ref IntPtr lpSize);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool UpdateProcThreadAttribute(
            IntPtr lpAttributeList,
            uint dwFlags,
            UIntPtr Attribute,
            IntPtr lpValue,
            IntPtr cbSize,
            IntPtr lpPreviousValue,
            IntPtr lpReturnSize);

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern bool CreateProcess(
            string lpApplicationName,
            string lpCommandLine,
            ref SECURITY_ATTRIBUTES lpProcessAttributes,
            ref SECURITY_ATTRIBUTES lpThreadAttributes,
            bool bInheritHandles,
            uint dwCreationFlags,
            IntPtr lpEnvironment,
            string lpCurrentDirectory,
            [In] ref STARTUPINFOEX lpStartupInfo,
            out PROCESS_INFORMATION lpProcessInformation);

        [StructLayout(LayoutKind.Sequential)]
        private struct SECURITY_ATTRIBUTES
        {
            public int nLength;
            public IntPtr lpSecurityDescriptor;
            public bool bInheritHandle;
        }

        public class WrappedProcess : IDisposable, IProcess
        {
            internal WrappedProcess(Process process) { _process = process; }
            internal Process _process;
            public int Pid => (int)_process.ProcessInfo.dwProcessId;
            public System.Diagnostics.Process Process
            {
                get
                {
                    if (_Process == null)
                    {
                        _Process = System.Diagnostics.Process.GetProcessById(Pid);
                    }
                    return _Process;
                }
            }

            public bool HasExited => Process.HasExited;
            public void WaitForExit() => Process.WaitForExit();
            public void Kill()
            {
                    Process.Kill();
            }

            private System.Diagnostics.Process _Process;
            private bool IsDisposed;

            protected virtual void Dispose(bool disposing)
            {
                if (!IsDisposed)
                {
                    if (disposing)
                    {
                        _process.Dispose();
                    }
                    IsDisposed = true;
                }
            }

            public void Dispose()
            {
                Dispose(disposing: true);
                GC.SuppressFinalize(this);
            }
        }

        public static WrappedProcess Start(string command, UIntPtr attributes, PseudoConsole console)
        {
            var startupInfo = ConfigureProcessThread(console.Handle, attributes);
            var processInfo = RunProcess(startupInfo, command);
            return new WrappedProcess(new Process(startupInfo, processInfo));
        }

        private static STARTUPINFOEX ConfigureProcessThread(PseudoConsole.ConPtyClosePseudoConsoleSafeHandle hPC, UIntPtr attributes)
        {
            IntPtr lpSize = IntPtr.Zero;
            bool success = InitializeProcThreadAttributeList(
                IntPtr.Zero,
                1,
                0,
                ref lpSize);

            if (success || lpSize == IntPtr.Zero)
            {
                throw new Win32Exception(Marshal.GetLastWin32Error(), "Could not calculate the number of bytes for the attribute list.");
            }

            var startupInfo = new STARTUPINFOEX();
            startupInfo.StartupInfo.cb = (int)Marshal.SizeOf<STARTUPINFOEX>();
            startupInfo.lpAttributeList = Marshal.AllocHGlobal((int)lpSize);

            success = InitializeProcThreadAttributeList(
                startupInfo.lpAttributeList,
                1,
                0,
                ref lpSize);

            if (!success)
            {
                throw new Win32Exception(Marshal.GetLastWin32Error(), "Could not set up attribute list.");
            }

            success = UpdateProcThreadAttribute(
                startupInfo.lpAttributeList,
                0,
                attributes,
                hPC.DangerousGetHandle(),
                (IntPtr)IntPtr.Size,
                IntPtr.Zero,
                IntPtr.Zero);

            if (!success)
            {
                throw new Win32Exception(Marshal.GetLastWin32Error(), "Could not set pseudoconsole thread attribute.");
            }

            return startupInfo;
        }

        private static PROCESS_INFORMATION RunProcess(STARTUPINFOEX sInfoEx, string commandLine)
        {
            var pSec = new SECURITY_ATTRIBUTES { nLength = Marshal.SizeOf<SECURITY_ATTRIBUTES>() };
            var tSec = new SECURITY_ATTRIBUTES { nLength = Marshal.SizeOf<SECURITY_ATTRIBUTES>() };

            const uint EXTENDED_STARTUPINFO_PRESENT = 0x00080000;

            bool success = CreateProcess(
                null,
                commandLine,
                ref pSec,
                ref tSec,
                false,
                EXTENDED_STARTUPINFO_PRESENT,
                IntPtr.Zero,
                null,
                ref sInfoEx,
                out PROCESS_INFORMATION pInfo);

            if (!success)
                throw new Win32Exception(Marshal.GetLastWin32Error(), "Could not create process.");

            return pInfo;
        }
    }
}
