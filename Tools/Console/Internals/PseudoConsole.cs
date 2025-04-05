using Microsoft.Win32.SafeHandles;
using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace Console.Internals
{
    /// <summary>
    /// Utility functions around the new Pseudo Console APIs.
    /// </summary>
    public class PseudoConsole : IDisposable
    {
        private bool disposed;
        public bool IsDisposed => disposed;
        internal ConPtyClosePseudoConsoleSafeHandle Handle { get; }

        /// <summary>
        /// Required for any 3rd parties trying to implement their own process creation
        /// </summary>
        public IntPtr GetDangerousHandle => Handle.DangerousGetHandle();

        private PseudoConsole(ConPtyClosePseudoConsoleSafeHandle handle)
        {
            Handle = handle;
        }

        public void Resize(int width, int height)
        {
            ResizePseudoConsole(Handle.DangerousGetHandle(), new COORD { X = (short)width, Y = (short)height });
        }

        internal class ConPtyClosePseudoConsoleSafeHandle : SafeHandleZeroOrMinusOneIsInvalid
        {
            public ConPtyClosePseudoConsoleSafeHandle(IntPtr preexistingHandle, bool ownsHandle = true) : base(ownsHandle)
            {
                SetHandle(preexistingHandle);
            }

            protected override bool ReleaseHandle()
            {
                ClosePseudoConsole(handle);
                return true;
            }
        }

        public static PseudoConsole Create(SafeFileHandle inputReadSide, SafeFileHandle outputWriteSide, int width, int height)
        {
            if (width == 0 || height == 0)
            {
                Debug.WriteLine($"PseudoConsole Create called with 0 width height");
                width = 80;
                height = 30;
            }

            var createResult = CreatePseudoConsole(
                new COORD { X = (short)width, Y = (short)height },
                inputReadSide, outputWriteSide,
                0, out IntPtr hPC);

            if (createResult != 0)
            {
                throw new Win32Exception(createResult);
            }

            return new PseudoConsole(new ConPtyClosePseudoConsoleSafeHandle(hPC));
        }

        private void Dispose(bool disposing)
        {
            if (!disposed)
            {
                if (disposing)
                {
                    Handle.Dispose();
                }

                disposed = true;
            }
        }

        public void Dispose()
        {
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern int CreatePseudoConsole(COORD size, SafeFileHandle hInput, SafeFileHandle hOutput, uint dwFlags, out IntPtr phPC);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern void ClosePseudoConsole(IntPtr hPC);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern int ResizePseudoConsole(IntPtr hPC, COORD size);

        [StructLayout(LayoutKind.Sequential)]
        private struct COORD
        {
            public short X;
            public short Y;
        }
    }
}