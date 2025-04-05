using Microsoft.Win32.SafeHandles;
using System;
using System.Runtime.InteropServices;

namespace Console.Internals {
	/// <summary>
	/// PInvoke signatures for Win32's PseudoConsole API.
	/// </summary>
	public static class PseudoConsoleApi {
        [StructLayout(LayoutKind.Sequential)]
        public struct COORD
        {
            public short X;
            public short Y;
        }

        [DllImport("conpty.dll", SetLastError = true)]
		internal static extern int CreatePseudoConsole(COORD size, SafeFileHandle hInput, SafeFileHandle hOutput, uint dwFlags, out IntPtr phPC);
		[DllImport("conpty.dll", SetLastError = true)]
		internal static extern int ClosePseudoConsole(IntPtr hPC);
		[DllImport("conpty.dll", SetLastError = true)]
		internal static extern int ResizePseudoConsole(IntPtr hPC, COORD size);

	}
}
